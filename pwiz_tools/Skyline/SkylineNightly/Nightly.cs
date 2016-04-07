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
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Ionic.Zip;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    // ReSharper disable NonLocalizedString
    public class Nightly
    {
        public const string NIGHTLY_TASK_NAME = "Skyline nightly build";

        private const string TEAM_CITY_BUILD_URL = "https://teamcity.labkey.org/viewType.html?buildTypeId={0}";
        private const string TEAM_CITY_ZIP_URL = "https://teamcity.labkey.org/repository/download/{0}/{1}:id/SkylineTester.zip";
        private const string TEAM_CITY_BUILD_TYPE_64 = "bt209";
        private const string TEAM_CITY_BUILD_TYPE_RELEASE_64 = "ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional";
        private const string TEAM_CITY_BUILD_TYPE_32 = "bt19";
        private const string TEAM_CITY_BUILD_TYPE = TEAM_CITY_BUILD_TYPE_64;
        private const string TEAM_CITY_USER_NAME = "guest";
        private const string TEAM_CITY_USER_PASSWORD = "guest";
        private const string LABKEY_URL = "https://skyline.gs.washington.edu/labkey/testresults/home/development/Nightly%20x64/post.view?";
        private const string LABKEY_PERF_URL = "https://skyline.gs.washington.edu/labkey/testresults/home/development/Performance%20Tests/post.view?";
        private const string LABKEY_STRESS_URL = "https://skyline.gs.washington.edu/labkey/testresults/home/development/NightlyStress/post.view?";
        private const string LABKEY_RELEASE_PERF_URL = "https://skyline.gs.washington.edu/labkey/testresults/home/development/Release%20Branch/post.view?";

        private DateTime _startTime;
        private string _logFile;
        private readonly Xml _nightly;
        private readonly Xml _failures;
        private readonly Xml _leaks;
        private Xml _pass;
        private readonly string _logDir;
        static string _logDirScreengrabs;
        private string PwizDir
        {
            get
            {
                // Place source code in SkylineTester instead of next to it, so we can 
                // still proceed if the source tree is locked against delete for any reason
                return Path.Combine(_skylineTesterDir, "pwiz");
            }
        }
        private TimeSpan _duration;
        private string _skylineTesterDir;

        public Nightly()
        {
            _nightly = new Xml("nightly");
            _failures = _nightly.Append("failures");
            _leaks = _nightly.Append("leaks");
            
            // Locate relevant directories.
            var nightlyDir = GetNightlyDir();
            _logDir = Path.Combine(nightlyDir, "Logs");
            _logDirScreengrabs = Path.Combine(_logDir, "NightlyScreengrabs");
            // First guess at working directory
            _skylineTesterDir = Path.Combine(nightlyDir, "SkylineTesterForNightly");

            // Default duration.
            _duration = TimeSpan.FromHours(9);
        }

        public class PeriodicScreengrabs
        {
            public void DoWork()
            {
                while (_logDirScreengrabs != null)
                {
                    try
                    {
                        // Once started, continue until exit
                        var now = DateTime.Now.ToString(CultureInfo.InvariantCulture).Replace('/','_').Replace(' ','_').Replace(':','_');
                        var s = 0;
                        foreach (var screen in Screen.AllScreens) // Handle multi-monitor
                        {
                            // Create a new bitmap.
                            using(var bmpScreenshot = new Bitmap(screen.Bounds.Width, screen.Bounds.Height,
                                PixelFormat.Format32bppArgb))
                            {
                                // Create a graphics object from the bitmap.
                                using(var gfxScreenshot = Graphics.FromImage(bmpScreenshot))
                                {
                                    // Take the screenshot from the upper left corner to the right bottom corner.
                                    gfxScreenshot.CopyFromScreen(screen.Bounds.X, screen.Bounds.Y,
                                        0, 0, screen.Bounds.Size, CopyPixelOperation.SourceCopy);

                                    // Save the screenshot
                                    var name = now + "_screen" + s +".png";
                                    var fileScreenshot = Path.Combine(_logDirScreengrabs, name);
                                    bmpScreenshot.Save(fileScreenshot, ImageFormat.Png);
                                }                                
                            }
                        }
                    }
                    catch
                    {
                        // Not a big deal if this doesn't work for some reason, just carry on
                    }
                    Thread.Sleep(60000); // 1 frame per minute
                }
            }
        }

        public enum RunMode { nightly, nightly_with_perftests, release_branch_with_perftests, nightly_with_stresstests}

        /// <summary>
        /// Run nightly build/test and report results to server.
        /// </summary>
        public void Run(RunMode mode)
        {
            // Locate relevant directories.
            var nightlyDir = GetNightlyDir();
            var skylineNightlySkytr = Path.Combine(nightlyDir, "SkylineNightly.skytr");

            bool withPerfTests = mode != RunMode.nightly;

            if (mode == RunMode.nightly_with_stresstests)
            {
                _duration = TimeSpan.FromHours(168);  // Let it go as long as a week
            }
            else if (withPerfTests)
            {
                _duration = TimeSpan.FromHours(12); // Let it go a bit longer than standard 9 hours
            }

            // Kill any other instance of SkylineNightly, unless this is
            // the StressTest mode.
            foreach (var process in Process.GetProcessesByName("skylinenightly"))
            {
                if (process.Id != Process.GetCurrentProcess().Id)
                {
                    if (mode == RunMode.nightly_with_stresstests)
                    {
                        Application.Exit();  // Just let the already (long!) running process do its thing
                    }
                    else
                    {
                        process.Kill();
                    }
                }
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

            try
            {
                if (Directory.Exists(_logDirScreengrabs))
                    Directory.Delete(_logDirScreengrabs, true);
                Directory.CreateDirectory(_logDirScreengrabs);
                // Start a thread to capture the screen once a minute to help track down anything that escapes the logs
                Log("Screengrabs will be written once every 60 seconds to " + _logDirScreengrabs);
                var screenGrabber = new PeriodicScreengrabs();
                var screenGrabberThread = new Thread(screenGrabber.DoWork);
                screenGrabberThread.Start();
            }
            catch (Exception x)
            {
                _logDirScreengrabs = null;
                Log("Unable to start screengrab thread, proceeding anyway: " + x.Message);
            }

            // Delete source tree and old SkylineTester.
            Delete(skylineNightlySkytr);
            Log("Delete SkylineTester");
            var skylineTesterDirBasis = _skylineTesterDir; // Default name
            const int maxRetry = 1000;  // Something would have to be very wrong to get here, but better not to risk a hang
            for (var retry = 1; retry < maxRetry; retry++)
            {
                try
                {
                    Delete(_skylineTesterDir);
                    break;
                }
                catch (Exception e)
                {
                    // Work around undeletable file that sometimes appears under Windows 10
                    var newDir = skylineTesterDirBasis + "_" +  retry;
                    Log("Unable to delete " + _skylineTesterDir + "(" + e + "),  using " + newDir + " instead.");
                    _skylineTesterDir = newDir;
                }
            }
            Log("buildRoot is " + PwizDir);

            // We used to put source tree alongside SkylineTesterDir instead of under it, delete that too
            try
            {
                Delete(Path.Combine(nightlyDir, "pwiz"));
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            Directory.CreateDirectory(_skylineTesterDir);

            // Download most recent build of SkylineTester.
            var skylineTesterZip = Path.Combine(_skylineTesterDir, skylineTesterDirBasis + ".zip");
            const int attempts = 30;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    DownloadSkylineTester(skylineTesterZip, mode);
                    break;
                }
                catch (Exception ex)
                {
                    Log("Exception while downloading SkylineTester: " + ex.Message + " (Probably still being built, will retry every 60 seconds for 30 minutes.)");
                    if (i == attempts-1)
                    {
                        LogAndThrow("Unable to download SkylineTester");
                    }
                    Thread.Sleep(60*1000);  // one minute
                }
            }

            // Install SkylineTester.
            if (!InstallSkylineTester(skylineTesterZip, _skylineTesterDir))
                LogAndThrow("SkylineTester installation failed.");

            // Delete zip file.
            Log("Delete zip file " + skylineTesterZip);
            File.Delete(skylineTesterZip);

            // Now figure out which branch we're working in - there's a file in the downloaded SkylineTester zip that tells us.
            string branchUrl = null;
            var lines = File.ReadAllLines(Path.Combine(_skylineTesterDir, "SkylineTester Files", "SVN_info.txt"));
            if (lines[1].Contains("branches"))
            {
                // Looks like  $URL: https://svn.code.sf.net/p/proteowizard/code/branches/skyline_3_5/SVN_info.txt $
                branchUrl =
                    lines[1].Split(new[] {"URL: "}, StringSplitOptions.None)[1].Split(new[] {"/SVN_info"},StringSplitOptions.None)[0];
            }

            // Create ".skytr" file to execute nightly build in SkylineTester.
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "SkylineNightly.SkylineNightly.skytr";
            int durationSeconds;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    LogAndThrow("Embedded resource is broken");
                    return; // Just so we don't get the "possible null" warning below
                }
                using (var reader = new StreamReader(stream))
                {
                    var skylineTester = Xml.FromString(reader.ReadToEnd());
                    skylineTester.GetChild("nightlyStartTime").Set(DateTime.Now.ToShortTimeString());
                    skylineTester.GetChild("nightlyRoot").Set(nightlyDir);
                    skylineTester.GetChild("buildRoot").Set(_skylineTesterDir);
                    skylineTester.GetChild("nightlyRunPerfTests").Set(withPerfTests?"true":"false");
                    skylineTester.GetChild("nightlyDuration").Set(((int)_duration.TotalHours).ToString());
                    skylineTester.GetChild("nightlyRepeat").Set(mode == RunMode.nightly_with_stresstests ? "100" : "1");
                    skylineTester.GetChild("nightlyRandomize").Set(mode == RunMode.nightly_with_stresstests ? "true" : "false");
                    if (!string.IsNullOrEmpty(branchUrl) && !branchUrl.Contains("trunk"))
                    {
                        skylineTester.GetChild("nightlyBuildTrunk").Set("false");
                        skylineTester.GetChild("nightlyBranch").Set("true");
                        skylineTester.GetChild("nightlyBranchUrl").Set(branchUrl);
                        Log("Testing branch at " + branchUrl);
                    }
                    skylineTester.Save(skylineNightlySkytr);
                    var durationHours = double.Parse(skylineTester.GetChild("nightlyDuration").Value);
                    durationSeconds = (int) (durationHours*60*60) + 30*60;  // 30 minutes grace before we kill SkylineTester
                }
            }

            // Start SkylineTester to do the build.
            var skylineTesterExe = Path.Combine(_skylineTesterDir, "SkylineTester Files", "SkylineTester.exe");

            var processInfo = new ProcessStartInfo(skylineTesterExe, skylineNightlySkytr)
            {
                WorkingDirectory = Path.GetDirectoryName(skylineTesterExe) ?? ""
            };

            var startTime = DateTime.Now;
            var skylineTesterProcess = Process.Start(processInfo);
            if (skylineTesterProcess == null)
            {
                LogAndThrow("SkylineTester did not start");
                return; // Just so we don't get the "possible null" warning below
            }
            Log("SkylineTester started");
            if (!skylineTesterProcess.WaitForExit(durationSeconds*1000))
            {
                SaveErrorScreenshot();
                skylineTesterProcess.Kill();
                Log("SkylineTester killed after " + durationSeconds + " second WaitForExit timeout");
            }
            else
                Log("SkylineTester finished");

            _duration = DateTime.Now - startTime;
        }

        public void Finish()
        {
            _logDirScreengrabs = null; // Signal the screengrab thread that we're done
        }

        private void DownloadSkylineTester(string skylineTesterZip, RunMode mode)
        {
            using (var client = new WebClient())
            {
                client.Credentials = new NetworkCredential(TEAM_CITY_USER_NAME, TEAM_CITY_USER_PASSWORD);
                var buildType = (mode == RunMode.release_branch_with_perftests)
                    ? TEAM_CITY_BUILD_TYPE_RELEASE_64
                    : TEAM_CITY_BUILD_TYPE;
                var buildPageUrl = string.Format(TEAM_CITY_BUILD_URL, buildType);
                Log("Download Team City build page");
                var buildStatusPage = client.DownloadString(buildPageUrl);

                var match = Regex.Match(buildStatusPage, @"<span\sid=""build:([^""]*):text"">Tests\spassed:");
                if (match.Success)
                {
                    string id = match.Groups[1].Value;
                    string zipFileLink = string.Format(TEAM_CITY_ZIP_URL, buildType, id);
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

        public RunMode Parse(string logFile = null, bool parseOnlyNoXmlOut = false)
        {
            logFile = logFile ?? GetLatestLog();
            if (logFile == null)
                throw new Exception("cannot locate current log");
            var log = File.ReadAllText(logFile);

            // Extract all test lines.
            var testCount = ParseTests(log);

            // Extract failures.
            ParseFailures(log);

            // Extract leaks.
            ParseLeaks(log);

            var hasPerftests = log.Contains("# Perf tests");
            var isTrunk = !log.Contains("Testing branch at");

            _nightly["id"] = Environment.MachineName;
            _nightly["os"] = Environment.OSVersion;
            var buildroot = ParseBuildRoot(log);
            _nightly["revision"] = GetRevision(buildroot);
            _nightly["start"] = _startTime;
            _nightly["duration"] = (int)_duration.TotalMinutes;
            _nightly["testsrun"] = testCount;
            _nightly["failures"] = _failures.Count;
            _nightly["leaks"] = _leaks.Count;

            // Save XML file.
            if (!parseOnlyNoXmlOut)
            {
                var xmlFile = Path.ChangeExtension(logFile, ".xml");
                File.WriteAllText(xmlFile, _nightly.ToString());
            }
            return isTrunk
                ? (hasPerftests ? RunMode.nightly_with_perftests : RunMode.nightly)
                : RunMode.release_branch_with_perftests;
        }

        private int ParseTests(string log)
        {
            var startTest = new Regex(@"\r\n\[(\d\d:\d\d)\] +(\d+).(\d+) +(\S+) +\((\w\w)\) ", RegexOptions.Compiled);
            var endTest = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+) MB, (\d+) sec\.\r\n", RegexOptions.Compiled);

            string lastPass = null;
            int testCount = 0;
            for (var startMatch = startTest.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var timestamp = startMatch.Groups[1].Value;
                var passId = startMatch.Groups[2].Value;
                var testId = startMatch.Groups[3].Value;
                var name = startMatch.Groups[4].Value;
                var language = startMatch.Groups[5].Value;

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
                test["timestamp"] = timestamp;
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
            var failureTest = new Regex(@"\r\n\[(\d\d:\d\d)\] +(\d+).(\d+) +(\S+)\s+\(+(\S+)\)",
                RegexOptions.Compiled | RegexOptions.RightToLeft);

            for (var startMatch = startFailure.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var name = startMatch.Groups[1].Value;
                var endMatch = endFailure.Match(log, startMatch.Index);
                var failureTestMatch = failureTest.Match(log, startMatch.Index);
                var timestamp = failureTestMatch.Groups[1].Value;
                var passId = failureTestMatch.Groups[2].Value;
                var testId = failureTestMatch.Groups[3].Value;
                var language = failureTestMatch.Groups[5].Value;
                if (string.IsNullOrEmpty(passId) || string.IsNullOrEmpty(testId))
                    continue;
                var failureDescription = log.Substring(startMatch.Index + startMatch.Length,
                    endMatch.Index - startMatch.Index - startMatch.Length);
                var failure = _failures.Append("failure");
                failure["name"] = name;
                failure["timestamp"] = timestamp;
                failure["pass"] = passId;
                failure["test"] = testId;
                failure["language"] = language;
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

        private string ParseBuildRoot(string log)
        {
            var brPattern = new Regex(@"Deleting Build directory\.\.\.\r\n\> rmdir /s ""(\S+)""", RegexOptions.Compiled);
            var match = brPattern.Match(log); 
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return PwizDir;
        }

        /// <summary>
        /// Post the latest results to the server.
        /// </summary>
        public void Post(RunMode mode, string xmlFile = null)
        {
            xmlFile = xmlFile ?? GetLatestXml();
            if (xmlFile == null)
                return;
            
            var xml = File.ReadAllText(xmlFile);
            string url;
            // Post to server.
            if (mode == RunMode.release_branch_with_perftests)
                url = LABKEY_RELEASE_PERF_URL;
            else if (mode == RunMode.nightly_with_perftests)
                url = LABKEY_PERF_URL;
            else if (mode == RunMode.nightly_with_stresstests)
                url = LABKEY_STRESS_URL;
            else
                url = LABKEY_URL;
            PostToLink(url, xml);
        }

        public string GetLatestLog()
        {
            var directory = new DirectoryInfo(_logDir);
            var logFile = directory.GetFiles()
                .Where(f => f.Name.EndsWith(".log"))
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

        /// <summary>
        /// Delete a file or directory, with quite a lot of retry on the expectation that 
        /// it's probably the TortoiseSVN windows explorer icon plugin getting in your way
        /// </summary>
        private void Delete(string fileOrDir)
        {
            for (var i = 5; i >0; i--)
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
                    if (i == 1)
                        throw;
                    Log("Retrying failed delete of " + fileOrDir + ": " + ex.Message);
                    var random = new Random();
                    Thread.Sleep(1000 + random.Next(0, 5000)); // A little stutter-step to avoid unlucky sync with TortoiseSVN icon update
                }
            }
        }

        private string Log(string message)
        {
            var time = DateTime.Now;
            var timestampedMessage = string.Format(
                "[{0}:{1}:{2}] {3}",
                time.Hour.ToString("D2"),
                time.Minute.ToString("D2"),
                time.Second.ToString("D2"),
                message);
            if (_logFile != null)
                File.AppendAllText(_logFile, timestampedMessage
                              + Environment.NewLine);
            return timestampedMessage;
        }

        private void LogAndThrow(string message)
        {
            var timestampedMessage = Log(message);
            SaveErrorScreenshot();
            throw new Exception(timestampedMessage);
        }

        private void SaveErrorScreenshot()
        {
            // Capture the screen in hopes of finding exception dialogs etc
            // From http://stackoverflow.com/questions/362986/capture-the-screen-into-a-bitmap

            foreach (var screen in Screen.AllScreens) // Handle multi-monitor
            {
                // Create a new bitmap.
                using (var bmpScreenshot = new Bitmap(screen.Bounds.Width, screen.Bounds.Height, PixelFormat.Format32bppArgb))
                {
                    // Create a graphics object from the bitmap.
                    using (var gfxScreenshot = Graphics.FromImage(bmpScreenshot))
                    {
                        // Take the screenshot from the upper left corner to the right bottom corner.
                        gfxScreenshot.CopyFromScreen(screen.Bounds.X, screen.Bounds.Y,
                            0, 0, screen.Bounds.Size, CopyPixelOperation.SourceCopy);

                        // Save the screenshot
                        const string basename = "SkylineNightly_error_screenshot";
                        const string ext = ".png";
                        var fileScreenshot = Path.Combine(GetNightlyDir(), basename + ext);
                        for (var retry = 0; File.Exists(fileScreenshot); retry++)
                            fileScreenshot = Path.Combine(GetNightlyDir(), basename + "_" + retry + ext);
                        bmpScreenshot.Save(fileScreenshot, ImageFormat.Png);
                        Log("Diagnostic screenshot saved to \"" + fileScreenshot + "\"");
                    }
                }
            }
        }
    }

    // ReSharper restore NonLocalizedString
}
