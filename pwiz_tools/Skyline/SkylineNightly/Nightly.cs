/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Ionic.Zip;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    // ReSharper disable NonLocalizedString
    public class Nightly
    {
        public const string NIGHTLY_TASK_NAME = "Skyline nightly build";
        public const string SCHEDULED_ARG = "scheduled";

        private const string TEAM_CITY_BUILD_URL = "https://teamcity.labkey.org/viewType.html?buildTypeId=bt{0}";
        private const string TEAM_CITY_ZIP_URL = "https://teamcity.labkey.org/repository/download/bt{0}/{1}:id/SkylineTester.zip";
        private const int TEAM_CITY_BUILD_TYPE_64 = 209;
        private const int TEAM_CITY_BUILD_TYPE_32 = 19;
        private const int TEAM_CITY_BUILD_TYPE = TEAM_CITY_BUILD_TYPE_64;
        private const string TEAM_CITY_USER_NAME = "guest";
        private const string TEAM_CITY_USER_PASSWORD = "guest";
        private const string LABKEY_URL = "https://skyline.gs.washington.edu/labkey/postreport/home/software/Skyline/begin.view?";

        private string _logFile;
        private string _line;
        private int _index;
        private Xml _nightly;
        private Xml _failures;
        private Xml _leaks;
        private Xml _pass;
        private int _currentPassId;
        private string _failedTest;
        private StringBuilder _stackTrace;
        private int _testCount;

        public void Run()
        {
            // Locate relevant directories.
            var nightlyDir = Settings.Default.NightlyFolder;
            if (!Path.IsPathRooted(nightlyDir))
                nightlyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nightlyDir);
            var logDir = Path.Combine(nightlyDir, "Logs");
            var pwizDir = Path.Combine(nightlyDir, "pwiz");
            var skylineTesterDir = Path.Combine(nightlyDir, "SkylineTester");
            var skylineNightlySkytr = Path.Combine(nightlyDir, "SkylineNightly.skytr");

            // Create log file.
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            _logFile = Path.Combine(nightlyDir, "SkylineNightly.log");
            Delete(_logFile);
            Log(DateTime.Now.ToShortDateString());

            // Delete source tree and old SkylineTester.
            Delete(skylineNightlySkytr);
            Log("Delete pwiz folder");
            Delete(pwizDir);
            Log("Delete SkylineTester");
            Delete(skylineTesterDir);

            // Download most recent build of SkylineTester.
            var skylineTesterZip = skylineTesterDir + ".zip";
            using (var client = new WebClient())
            {
                client.Credentials = new NetworkCredential(TEAM_CITY_USER_NAME, TEAM_CITY_USER_PASSWORD);

                var buildPageUrl = string.Format(TEAM_CITY_BUILD_URL, TEAM_CITY_BUILD_TYPE);
                Log("Download Team City build page");
                var buildStatusPage = client.DownloadString(buildPageUrl);

                var match = Regex.Match(buildStatusPage, @"<span\sid=""build:([^""]*):text"">Tests\spassed:");
                if (match.Success)
                {
                    string id = match.Groups[1].Value;
                    string zipFileLink = string.Format(TEAM_CITY_ZIP_URL, TEAM_CITY_BUILD_TYPE, id);
                    Log("Download SkylineTester zip file");
                    client.DownloadFile(zipFileLink, skylineTesterZip);
                }
            }

            // Install SkylineTester.
            using (var zipFile = new ZipFile(skylineTesterZip))
            {
                try
                {
                    Log("Unzip SkylineTester");
                    zipFile.ExtractAll(skylineTesterDir, ExtractExistingFileAction.OverwriteSilently);
                }
                catch (Exception)
                {
                    Log("Error attempting to unzip SkylineTester");
                    return;
                }
            }

            // Delete zip file.
            Log("Delete zip file");
            File.Delete(skylineTesterZip);

            // Create ".skytr" file to execute nightly build in SkylineTester.
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "SkylineNightly.SkylineNightly.skytr";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Log("Embedded resource is broken");
                    return;
                }
                using (StreamReader reader = new StreamReader(stream))
                {
                    var skylineTester = Xml.FromString(reader.ReadToEnd());
                    skylineTester.GetChild("nightlyStartTime").Set(DateTime.Now.ToShortTimeString());
                    skylineTester.GetChild("nightlyRoot").Set(nightlyDir);
                    skylineTester.Save(skylineNightlySkytr);
                }
            }

            // Start SkylineTester to do the build.
            var skylineTesterExe = Path.Combine(skylineTesterDir, "SkylineTester Files", "SkylineTester.exe");

            var processInfo = new ProcessStartInfo(skylineTesterExe, skylineNightlySkytr)
            {
                WorkingDirectory = Path.GetDirectoryName(skylineTesterExe) ?? ""
            };

            var startTime = DateTime.Now;
            var skylineTesterProcess = Process.Start(processInfo);
            if (skylineTesterProcess == null)
            {
                Log("SkylineTester did not start");
                return;
            }
            Log("SkylineTester started");
            skylineTesterProcess.WaitForExit();
            Log("SkylineTester finished");

            // Upload log information.
            var duration = DateTime.Now - startTime;
            UploadLog(logDir, duration);
        }

        private enum ReadState
        {
            startTest,
            endTest,
            failure,
        }

        private void UploadLog(string logDir, TimeSpan duration)
        {
            _nightly = new Xml("nightly");
            _failures = _nightly.Append("failures");
            _leaks = _nightly.Append("leaks");

            var directory = new DirectoryInfo(logDir);
            var logFile = directory.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .SkipWhile(f => f.Name == "Summary.log")
                .First();
            if (logFile != null)
                ParseLog(logFile);

            _nightly["id"] = Environment.MachineName;
            _nightly["duration"] = duration.Hours + ":" + duration.Minutes.ToString("D2");
            _nightly["testsrun"] = _testCount;
            _nightly["failures"] = _failures.Count;
            _nightly["leaks"] = _leaks.Count;

            var xml = _nightly.ToString();
            if (logFile != null)
            {
                var xmlFile = Path.ChangeExtension(logFile.FullName, ".xml");
                File.WriteAllText(xmlFile, xml);
            }

            // Post to server.
            PostToLink(LABKEY_URL, xml);
        }

        public static void PostToLink(string link, string postData)
        {
            string javaScript = string.Format(
@"<script type=""text/javascript"">
function submitForm()
{{
    document.getElementById(""my_form"").submit();
}}
window.onload = submitForm;
</script>
<form id=""my_form"" action=""{0}"" method=""post"" style=""visibility: hidden;"">
<textarea name=""SkylineReport"">{1}</textarea>
</form>", // Not L10N
                link, WebUtility.HtmlEncode(postData));

            string filePath = Path.GetTempFileName() + ".html"; // Not L10N
            File.WriteAllText(filePath, javaScript);
            Process.Start(filePath);

            // Allow time for browser to load file.
            Thread.Sleep(3000);
            File.Delete(filePath);
        }

        private void ParseLog(FileInfo logFile)
        {
            using (var stream = logFile.OpenText())
            {
                var state = ReadState.startTest;
                _currentPassId = -1;
                int passId = 0;
                int testId = 0;
                string name = "";
                string language = "";

                while (true)
                {
                    _line = stream.ReadLine();
                    _index = 0;
                    if (_line == null)
                        break;

                    switch (state)
                    {
                        case ReadState.startTest:
                            state = ProcessStartTest(ref passId, ref testId, ref name, ref language);
                            break;

                        case ReadState.endTest:
                            state = ProcessEndTest(passId, testId, name, language);
                            break;

                        case ReadState.failure:
                            state = ProcessFailure(passId, testId);
                            break;
                    }
                }
            }
        }

        private ReadState ProcessStartTest(ref int passId, ref int testId, ref string name, ref string language)
        {
            if (_line.Length < 9)
                return ReadState.startTest;

            // Scan for beginning of failure stack trace.
            if (_line.StartsWith("!!! "))
            {
                if (_line.EndsWith(" FAILED"))
                {
                    _failedTest = _line.Split(' ')[1];
                    _stackTrace = new StringBuilder();
                    _stackTrace.AppendLine();
                    return ReadState.failure;
                }

                // Scan for leak.
                if (_line.EndsWith(" bytes"))
                {
                    var parts = _line.Split(' ');
                    var leakingTest = parts[1];
                    var leakedBytes = parts[3];
                    var leak = _leaks.Append("leak");
                    leak["name"] = leakingTest;
                    leak["bytes"] = leakedBytes;
                }

                return ReadState.startTest;
            }

            // Scan for beginning of test information line.
            if (!(_line[0] == '[' &&
                  Char.IsDigit(_line[1]) && Char.IsDigit(_line[2]) &&
                  _line[3] == ':' &&
                  Char.IsDigit(_line[4]) && Char.IsDigit(_line[5]) &&
                  _line[6] == ']' && _line[7] == ' '))
                return ReadState.startTest;
            _index = 8;
            passId = ParseInt();
            if (_line[_index++] != '.')
                return ReadState.startTest;
            testId = ParseInt();
            if (!IsSpace())
                return ReadState.startTest;
            name = ParseString();
            language = ParseString();
            language = language.Substring(1, language.Length - 2);
            _testCount++;

            // Scan for end of test information line.
            return ProcessEndTest(passId, testId, name, language);
        }

        private ReadState ProcessEndTest(int passId, int testId, string name, string language)
        {
            if (!Skip(" failures, "))
                return ReadState.startTest;
            var managed = ParseDouble();
            if (_index == _line.Length || _line[_index++] != '/')
                return ReadState.startTest;
            var total = ParseDouble();
            if (!Skip(" MB, "))
                return ReadState.startTest;
            var duration = ParseInt();

            if (_currentPassId != passId)
            {
                _currentPassId = passId;
                _pass = _nightly.Append("pass");
                _pass["id"] = passId;
            }

            var test = _pass.Append("test");
            test["id"] = testId;
            test["name"] = name;
            test["language"] = language;
            test["duration"] = duration;
            test["managed"] = managed;
            test["total"] = total;

            return ReadState.startTest;
        }

        private ReadState ProcessFailure(int passId, int testId)
        {
            // Look for end of stack trace.
            if (_line == "!!!")
            {
                var failure = _failures.Append("failure");
                failure["name"] = _failedTest;
                failure["pass"] = passId;
                failure["test"] = testId;
                failure.Set(_stackTrace.ToString());
                return ReadState.startTest;
            }

            // Append to stack trace.
            _stackTrace.AppendLine(_line);
            return ReadState.failure;
        }

        private void Delete(string fileOrDir)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (File.Exists(fileOrDir))
                        File.Delete(fileOrDir);
                    else if (Directory.Exists(fileOrDir))
                        Directory.Delete(fileOrDir, true);
                }
                catch (Exception ex)
                {
                    Log("Problem deleting " + fileOrDir + ": " + ex.Message);
                    if (i == 4)
                        throw;
                    Thread.Sleep(1000);
                }
            }
        }

        private void Log(string message)
        {
            var time = DateTime.Now;
            File.AppendAllText(_logFile, string.Format(
                "[{0}:{1}:{2}] {3}",
                time.Hour.ToString("D2"),
                time.Minute.ToString("D2"),
                time.Second.ToString("D2"),
                message)
                + Environment.NewLine);
        }

        private void SkipSpaces()
        {
            while (IsSpace())
                _index++;
        }

        private bool IsSpace()
        {
            return _index < _line.Length && Char.IsWhiteSpace(_line[_index]);
        }

        private bool IsDigit()
        {
            return _index < _line.Length && Char.IsDigit(_line[_index]);
        }

        private int ParseInt()
        {
            SkipSpaces();
            if (!IsDigit())
                return 0;
            int start = _index++;
            while (IsDigit())
                _index++;
            return int.Parse(_line.Substring(start, _index - start));
        }

        private double ParseDouble()
        {
            SkipSpaces();
            if (!IsDigit())
                return 0.0;
            int start = _index++;
            while (IsDigit() || _line[_index] == '.')
                _index++;
            return double.Parse(_line.Substring(start, _index - start));
        }

        private string ParseString()
        {
            SkipSpaces();
            if (_index == _line.Length)
                return string.Empty;
            int start = _index++;
            while (!IsSpace())
                _index++;
            return _line.Substring(start, _index - start);
        }

        private bool Skip(string substring)
        {
            if (_index < _line.Length)
            {
                int j = _line.IndexOf(substring, _index, StringComparison.Ordinal);
                if (j >= 0)
                {
                    _index = j + substring.Length;
                    return true;
                }
            }

            return false;
        }
    }
    // ReSharper restore NonLocalizedString
}
