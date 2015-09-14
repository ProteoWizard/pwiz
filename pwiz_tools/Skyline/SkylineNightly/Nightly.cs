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
using System.Net.Http;
using System.Reflection;
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

        private const string TEAM_CITY_BUILD_URL = "https://teamcity.labkey.org/viewType.html?buildTypeId=bt{0}";
        private const string TEAM_CITY_ZIP_URL = "https://teamcity.labkey.org/repository/download/bt{0}/{1}:id/SkylineTester.zip";
        private const int TEAM_CITY_BUILD_TYPE_64 = 209;
        private const int TEAM_CITY_BUILD_TYPE_32 = 19;
        private const int TEAM_CITY_BUILD_TYPE = TEAM_CITY_BUILD_TYPE_64;
        private const string TEAM_CITY_USER_NAME = "guest";
        private const string TEAM_CITY_USER_PASSWORD = "guest";
        private const string LABKEY_URL = "https://skyline.gs.washington.edu/labkey/testresults/home/development/Nightly%20x64/post.view?";

        private DateTime _startTime;
        private string _logFile;
        private readonly Xml _nightly;
        private readonly Xml _failures;
        private readonly Xml _leaks;
        private Xml _pass;
        private readonly string _logDir;
        private readonly string _pwizDir;
        private TimeSpan _duration;

        public Nightly()
        {
            _nightly = new Xml("nightly");
            _failures = _nightly.Append("failures");
            _leaks = _nightly.Append("leaks");
            
            // Locate relevant directories.
            var nightlyDir = GetNightlyDir();
            _logDir = Path.Combine(nightlyDir, "Logs");
            _pwizDir = Path.Combine(nightlyDir, "pwiz");

            // Default duration.
            _duration = TimeSpan.FromHours(9);
        }

        /// <summary>
        /// Run nightly build/test and report results to server.
        /// </summary>
        public void Run()
        {
            // Locate relevant directories.
            var nightlyDir = GetNightlyDir();
            var skylineTesterDir = Path.Combine(nightlyDir, "SkylineTester");
            var skylineNightlySkytr = Path.Combine(nightlyDir, "SkylineNightly.skytr");

            // Kill any other instance of SkylineNightly.
            foreach (var process in Process.GetProcessesByName("skylinenightly"))
            {
                if (process.Id != Process.GetCurrentProcess().Id)
                    process.Kill();
            }

            // Kill processes started within the nightly directory.
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Modules[0].FileName.StartsWith(nightlyDir) &&
                        process.Id != Process.GetCurrentProcess().Id)
                        process.Kill();
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }
            }

            _startTime = DateTime.Now;

            // Create log file.
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
            _logFile = Path.Combine(nightlyDir, "SkylineNightly.log");
            Delete(_logFile);
            Log(DateTime.Now.ToShortDateString());

            // Delete source tree and old SkylineTester.
            Delete(skylineNightlySkytr);
            Log("Delete SkylineTester");
            Delete(skylineTesterDir);
            Log("Delete pwiz folder");
            Delete(_pwizDir);

            // Download most recent build of SkylineTester.
            var skylineTesterZip = skylineTesterDir + ".zip";
            const int attempts = 30;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    DownloadSkylineTester(skylineTesterZip);
                    break;
                }
                catch (Exception ex)
                {
                    Log("Exception while downloading SkylineTester: " + ex.Message);
                    if (i == attempts-1)
                    {
                        Log("Unable to download SkylineTester");
                        return;
                    }
                    Thread.Sleep(60*1000);  // one minute
                }
            }

            // Install SkylineTester.
            if (!InstallSkylineTester(skylineTesterZip, skylineTesterDir))
                return;

            // Delete zip file.
            Log("Delete zip file");
            File.Delete(skylineTesterZip);

            // Create ".skytr" file to execute nightly build in SkylineTester.
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "SkylineNightly.SkylineNightly.skytr";
            int durationSeconds;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Log("Embedded resource is broken");
                    return;
                }
                using (var reader = new StreamReader(stream))
                {
                    var skylineTester = Xml.FromString(reader.ReadToEnd());
                    skylineTester.GetChild("nightlyStartTime").Set(DateTime.Now.ToShortTimeString());
                    skylineTester.GetChild("nightlyRoot").Set(nightlyDir);
                    skylineTester.Save(skylineNightlySkytr);
                    var durationHours = double.Parse(skylineTester.GetChild("nightlyDuration").Value);
                    durationSeconds = (int) (durationHours*60*60) + 30*60;  // 30 minutes grace before we kill SkylineTester
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
            if (!skylineTesterProcess.WaitForExit(durationSeconds*1000))
            {
                skylineTesterProcess.Kill();
                Log("SkylineTester killed");
            }
            else
                Log("SkylineTester finished");

            _duration = DateTime.Now - startTime;
        }

        private void DownloadSkylineTester(string skylineTesterZip)
        {
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
        }

        private bool InstallSkylineTester(string skylineTesterZip, string skylineTesterDir)
        {
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
                    return false;
                }
            }
            return true;
        }

        public void Parse(string logFile = null)
        {
            logFile = logFile ?? GetLatestLog();
            if (logFile == null)
                return;
            var log = File.ReadAllText(logFile);

            // Extract all test lines.
            var testCount = ParseTests(log);

            // Extract failures.
            ParseFailures(log);

            // Extract leaks.
            ParseLeaks(log);

            _nightly["id"] = Environment.MachineName;
            _nightly["os"] = Environment.OSVersion;
            _nightly["revision"] = GetRevision(_pwizDir);
            _nightly["start"] = _startTime;
            _nightly["duration"] = (int)_duration.TotalMinutes;
            _nightly["testsrun"] = testCount;
            _nightly["failures"] = _failures.Count;
            _nightly["leaks"] = _leaks.Count;

            // Save XML file.
            var xmlFile = Path.ChangeExtension(logFile, ".xml");
            File.WriteAllText(xmlFile, _nightly.ToString());
        }

        private int ParseTests(string log)
        {
            var startTest = new Regex(@"\r\n\[\d\d:\d\d\] +(\d+).(\d+) +(\S+) +\((\w\w)\) ", RegexOptions.Compiled);
            var endTest = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+) MB, (\d+) sec\.\r\n", RegexOptions.Compiled);

            string lastPass = null;
            int testCount = 0;
            for (var startMatch = startTest.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var passId = startMatch.Groups[1].Value;
                var testId = startMatch.Groups[2].Value;
                var name = startMatch.Groups[3].Value;
                var language = startMatch.Groups[4].Value;

                var endMatch = endTest.Match(log, startMatch.Index);
                var managed = endMatch.Groups[1].Value;
                var total = endMatch.Groups[2].Value;
                var duration = endMatch.Groups[3].Value;

                if (string.IsNullOrEmpty(managed) || string.IsNullOrEmpty(total) || string.IsNullOrEmpty(duration))
                    continue;

                if (lastPass != passId)
                {
                    lastPass = passId;
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

                testCount++;
            }
            return testCount;
        }

        private void ParseFailures(string log)
        {
            var startFailure = new Regex(@"\r\n!!! (\S+) FAILED\r\n", RegexOptions.Compiled);
            var endFailure = new Regex(@"\r\n!!!\r\n", RegexOptions.Compiled);
            var failureTest = new Regex(@"\r\n\[\d\d:\d\d\] +(\d+).(\d+) +(\S+) ",
                RegexOptions.Compiled | RegexOptions.RightToLeft);

            for (var startMatch = startFailure.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var name = startMatch.Groups[1].Value;
                var endMatch = endFailure.Match(log, startMatch.Index);
                var failureTestMatch = failureTest.Match(log, startMatch.Index);
                var passId = failureTestMatch.Groups[1].Value;
                var testId = failureTestMatch.Groups[2].Value;
                if (string.IsNullOrEmpty(passId) || string.IsNullOrEmpty(testId))
                    continue;
                var failureDescription = log.Substring(startMatch.Index + startMatch.Length,
                    endMatch.Index - startMatch.Index - startMatch.Length);
                var failure = _failures.Append("failure");
                failure["name"] = name;
                failure["pass"] = passId;
                failure["test"] = testId;
                failure.Set(Environment.NewLine + failureDescription + Environment.NewLine);
            }
        }

        private void ParseLeaks(string log)
        {
            var leakPattern = new Regex(@"!!! (\S+) LEAKED (\d+) bytes", RegexOptions.Compiled);
            for (var match = leakPattern.Match(log); match.Success; match = match.NextMatch())
            {
                var leak = _leaks.Append("leak");
                leak["name"] = match.Groups[1].Value;
                leak["bytes"] = match.Groups[2].Value;
            }
        }

        /// <summary>
        /// Post the latest results to the server.
        /// </summary>
        public void Post(string xmlFile = null)
        {
            xmlFile = xmlFile ?? GetLatestXml();
            if (xmlFile == null)
                return;
            
            var xml = File.ReadAllText(xmlFile);

            // Post to server.
            PostToLink(LABKEY_URL, xml);
        }

        public string GetLatestLog()
        {
            var directory = new DirectoryInfo(_logDir);
            var logFile = directory.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .SkipWhile(f => f.Name == "Summary.log")
                .First();
            return logFile == null ? null : logFile.FullName;
        }

        public string GetLatestXml()
        {
            var directory = new DirectoryInfo(_logDir);
            var xmlFile = directory.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .SkipWhile(f => !f.Name.EndsWith(".xml"))
                .First();
            return xmlFile == null ? null : xmlFile.FullName;
        }

        /// <summary>
        /// Post data to the given link URL.
        /// </summary>
        private void PostToLink(string link, string postData)
        {
            Log("Posting results to " + link); // Not L10N
            for (var retry = 5; retry > 0; retry--)
            {
                try
                {
                    var client = new HttpClient();
                    var content = new MultipartFormDataContent { { new StringContent(postData), "xml" } }; // Not L10N
                    var result = client.PostAsync(link, content);
                    if (result == null)
                    {
                        Log("no response"); // Not L10N
                    }
                    else
                    {
                        if (result.Result.IsSuccessStatusCode)
                            return;
                        Log(result.Result.ToString());
                    }
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                }
                if (retry > 1)
                {
                    Thread.Sleep(30000);
                    Log("Retrying post"); // Not L10N
                }
            }
            Log("Failed to post results"); // Not L10N
        }

        private static string GetNightlyDir()
        {
            var nightlyDir = Settings.Default.NightlyFolder;
            return Path.IsPathRooted(nightlyDir)
                ? nightlyDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nightlyDir);
        }

        private int GetRevision(string pwizDir)
        {
            int revision = 0;
            try
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var subversion = Path.Combine(programFiles, @"Subversion\bin\svn.exe");

                Process svn = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        FileName = subversion,
                        Arguments = @"info " + pwizDir,
                        CreateNoWindow = true
                    }
                };
                svn.Start();
                string svnOutput = svn.StandardOutput.ReadToEnd();
                svn.WaitForExit();
                var revisionString = Regex.Match(svnOutput, @".*Revision: (\d+)").Groups[1].Value;
                revision = int.Parse(revisionString);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            return revision;
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
            if (_logFile == null)
                return;
            var time = DateTime.Now;
            File.AppendAllText(_logFile, string.Format(
                "[{0}:{1}:{2}] {3}",
                time.Hour.ToString("D2"),
                time.Minute.ToString("D2"),
                time.Second.ToString("D2"),
                message)
                + Environment.NewLine);
        }
    }

    // ReSharper restore NonLocalizedString
}
