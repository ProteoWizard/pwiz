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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using Ionic.Zip;
using Microsoft.Win32.TaskScheduler;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    // ReSharper disable LocalizableElement
    public class Nightly
    {

        private const string NIGHTLY_TASK_NAME = "Skyline nightly build";

        private const string TEAM_CITY_ZIP_URL = "https://teamcity.labkey.org/guestAuth/repository/download/{0}/.lastFinished/SkylineTester.zip{1}";
        private const string TEAM_CITY_BUILD_TYPE_64_MASTER = "bt209";

        // N.B. choice of "release" and "integration" branches is made in TeamCity VCS Roots "pwiz Github Skyline_Integration_Only" and "pwiz Github Skyline_Release_Only"
        // Thus TC admins can easily change the "release" and "integration" git branches at http://teamcity.labkey.org/admin/editProject.html?projectId=ProteoWizard&tab=projectVcsRoots
        private const string TEAM_CITY_BUILD_TYPE_64_RELEASE = "ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional";
        private const string TEAM_CITY_BUILD_TYPE_64_INTEGRATION = "ProteoWizard_SkylineIntegrationBranchX8664";

        private const string TEAM_CITY_USER_NAME = "guest";
        private const string TEAM_CITY_USER_PASSWORD = "guest";
        private const string LABKEY_PROTOCOL = "https";
        private const string LABKEY_SERVER_ROOT = "skyline.ms";
        private const string LABKEY_MODULE = "testresults";
        private const string LABKEY_ACTION = "post";
        private const string LABKEY_EMAIL_NOTIFICATION_ACTION = "sendEmailNotification";

        private static string GetPostUrl(string path)
        {
            return GetUrl(path, LABKEY_MODULE, LABKEY_ACTION);
        }

        private static string GetUrl(string path, string controller, string action)
        {
            return LABKEY_PROTOCOL + "://" + LABKEY_SERVER_ROOT + "/" + controller + "/" + path + "/" +
                   action + ".view";
        }

        private static string LABKEY_URL = GetPostUrl("home/development/Nightly%20x64");
        private static string LABKEY_PERF_URL = GetPostUrl("home/development/Performance%20Tests");
        private static string LABKEY_STRESS_URL = GetPostUrl("home/development/NightlyStress");
        private static string LABKEY_RELEASE_URL = GetPostUrl("home/development/Release%20Branch");
        private static string LABKEY_RELEASE_PERF_URL = GetPostUrl("home/development/Release%20Branch%20Performance%20Tests");
        private static string LABKEY_INTEGRATION_URL = GetPostUrl("home/development/Integration");
        private static string LABKEY_INTEGRATION_PERF_URL = GetPostUrl("home/development/Integration%20With%20Perf%20Tests");
        private static string LABKEY_HOME_URL = GetUrl("home", "project", "begin");

        public static string LABKEY_EMAIL_NOTIFICATION_URL = GetUrl("home/development/Nightly%20x64", LABKEY_MODULE, LABKEY_EMAIL_NOTIFICATION_ACTION);

        private static string LABKEY_CSRF = @"X-LABKEY-CSRF";

        private const string GIT_MASTER_URL = "https://github.com/ProteoWizard/pwiz";
        private const string GIT_BRANCHES_URL = GIT_MASTER_URL + "/tree/";

        private DateTime _startTime;
        public string LogFileName { get; private set; }
        private readonly Xml _nightly;
        private readonly Xml _failures;
        private readonly Xml _leaks;
        private Xml _pass;
        private readonly string _logDir;
        private readonly RunMode _runMode;
        private string PwizDir
        {
            get
            {
                // Place source code in SkylineTester instead of next to it, so we can 
                // still proceed if the source tree is locked against delete for any reason
                return Path.Combine(_skylineTesterDir, "pwiz");
            }
        }
        private string _skylineTesterDir;

        public const int DEFAULT_DURATION_HOURS = 9;
        public const int PERF_DURATION_HOURS = 12;

        public Nightly(RunMode runMode, string decorateSrcDirName = null)
        {
            _runMode = runMode;
            _nightly = new Xml("nightly");
            _failures = _nightly.Append("failures");
            _leaks = _nightly.Append("leaks");
            
            // Locate relevant directories.
            var nightlyDir = GetNightlyDir();
            _logDir = Path.Combine(nightlyDir, "Logs");
            // Clean up after any old screengrab directories
            var logDirScreengrabs = Path.Combine(_logDir, "NightlyScreengrabs");
            if (Directory.Exists(logDirScreengrabs))
                Directory.Delete(logDirScreengrabs, true);
            // First guess at working directory - distinguish between run types for machines that do double duty
            _skylineTesterDir = Path.Combine(nightlyDir, "SkylineTesterForNightly_"+runMode + (decorateSrcDirName ?? string.Empty));
        }

        public static string NightlyTaskName { get { return NIGHTLY_TASK_NAME; } }
        public static string NightlyTaskNameWithUser { get { return string.Format("{0} ({1})", NIGHTLY_TASK_NAME, Environment.UserName);} }
        public static Task NightlyTask
        {
            get
            {
                using (var ts = new TaskService())
                    return ts.FindTask(NightlyTaskName) ?? ts.FindTask(NightlyTaskNameWithUser);
            }
        }

        public bool WithPerfTests => _runMode != RunMode.trunk && _runMode != RunMode.integration && _runMode != RunMode.release;

        public TimeSpan TargetDuration
        {
            get
            {
                if (_runMode == RunMode.stress)
                {
                    return TimeSpan.FromHours(168);  // Let it go as long as a week
                }
                else if (WithPerfTests)
                {
                    return TimeSpan.FromHours(PERF_DURATION_HOURS); // Let it go a bit longer than standard 9 hours
                }
                return TimeSpan.FromHours(DEFAULT_DURATION_HOURS);
            }
        }

        public void Finish(string message, string errMessage)
        {
            // Leave a note for the user, in a way that won't interfere with our next run
            Log("Done.  Exit message: ");
            Log(!string.IsNullOrEmpty(message) ? message : "none");
            if (!string.IsNullOrEmpty(errMessage))
                Log(errMessage);
            if (string.IsNullOrEmpty(LogFileName))
            {
                if (!string.IsNullOrEmpty(message))
                {
                    MessageBox.Show(message, @"SkylineNightly Help");
                }
            }
            else
            {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = "notepad.exe", 
                        Arguments = LogFileName
                    }
                };
                process.Start();
            }
        }

        public enum RunMode { parse, post, trunk, perf, release, stress, integration, release_perf, integration_perf }

        public static string SkylineTesterStoppedByUser = "SkylineTester stopped by user";

        public string RunAndPost()
        {
            var runResult = Run() ?? string.Empty;
            if (runResult.Equals(SkylineTesterStoppedByUser))
            {
                Log("No results posted");
                return runResult;
            }
            Parse();
            var postResult = Post(_runMode);
            if (!string.IsNullOrEmpty(postResult))
            {
                if (!string.IsNullOrEmpty(runResult))
                    runResult += "\n"; 
                runResult += postResult;
            }
            return runResult;
        }

        /// <summary>
        /// Run nightly build/test and report results to server.
        /// </summary>
        public string Run()
        {
            string result = string.Empty;
            // Locate relevant directories.
            var nightlyDir = GetNightlyDir();
            var skylineNightlySkytr = Path.Combine(nightlyDir, "SkylineNightly.skytr");

            // Kill any other instance of SkylineNightly, unless this is
            // the StressTest mode, in which case assume that a previous invocation
            // is still running and just exit to stay out of its way.
            foreach (var process in Process.GetProcessesByName("skylinenightly"))
            {
                if (process.Id != Process.GetCurrentProcess().Id)
                {
                    if (_runMode == RunMode.stress)
                    {
                        Application.Exit();  // Just let the already (long!) running process do its thing
                    }
                    else
                    {
                        process.Kill();
                    }
                }
            }

            // Kill processes started within the proposed working directory - most likely SkylineTester and/or TestRunner.
            // This keeps stuck tests around for 24 hours, which should be sufficient, but allows us to replace directory
            // on a daily basis - otherwise we could fill the hard drive on smaller machines
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Modules[0].FileName.StartsWith(_skylineTesterDir) &&
                        process.Id != Process.GetCurrentProcess().Id)
                    {
                        process.Kill();
                    }
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }
            }

            // Create place to put run logs
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
            // Start the nightly log file
            StartLog(_runMode);

            // Delete source tree and old SkylineTester.
            Delete(skylineNightlySkytr);
            Log("Delete SkylineTester");
            var skylineTesterDirBasis = _skylineTesterDir; // Default name
            const int maxRetry = 1000;  // Something would have to be very wrong to get here, but better not to risk a hang
            string nextDir = _skylineTesterDir;
            for (var retry = 1; retry < maxRetry; retry++)
            {
                try
                {
                    if (!Directory.Exists(nextDir))
                        break;

                    string deleteDir = nextDir;
                    // Keep going until a directory is found that does not exist
                    nextDir = skylineTesterDirBasis + "_" + retry;

                    Delete(deleteDir);
                }
                catch (Exception e)
                {
                    if (Directory.Exists(_skylineTesterDir))
                    {
                        // Work around undeletable file that sometimes appears under Windows 10
                        Log("Unable to delete " + _skylineTesterDir + "(" + e + "),  using " + nextDir + " instead.");
                        _skylineTesterDir = nextDir;
                    }
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
            const int retryTimeoutInMinutes = 10;
            const int attempts = 120/ retryTimeoutInMinutes; // Retry for up to two hours
            var useLastSuccessfulInsteadOfLastFinished = false;
            string branchUrl = null;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    DownloadSkylineTester(skylineTesterZip, _runMode, useLastSuccessfulInsteadOfLastFinished);
                }
                catch (Exception ex)
                {
                    if (i == attempts - 1)
                    {
                        QuitWithError("Unable to download SkylineTester");
                    }

                    // After 30 minutes, start trying for lastSuccessful build instead
                    useLastSuccessfulInsteadOfLastFinished = i > (30 / retryTimeoutInMinutes); 
                    if (useLastSuccessfulInsteadOfLastFinished)
                    {
                        Log("Exception while downloading SkylineTester: " + ex.Message + " (TeamCity outage? Retrying every 60 seconds for an additional 90 minutes, attempting download of lastSuccessful build instead of lastFinished.)");
                    }
                    else
                    {
                        Log("Exception while downloading SkylineTester: " + ex.Message + " (Retrying every 60 seconds for 30 minutes.)");
                    }
                    Thread.Sleep(60*1000 * retryTimeoutInMinutes);
                    continue;
                }

                // Install SkylineTester.
                if (!InstallSkylineTester(skylineTesterZip, _skylineTesterDir))
                    QuitWithError("SkylineTester installation failed.");
                try
                {
                    // Delete zip file.
                    Log("Delete zip file " + skylineTesterZip);
                    File.Delete(skylineTesterZip);

                    // Figure out which branch we're working in - there's a file in the downloaded SkylineTester zip that tells us.
                    var branchLine = File.ReadAllLines(Path.Combine(_skylineTesterDir, "SkylineTester Files", "Version.cpp")).FirstOrDefault(l => l.Contains("Version::Branch"));
                    if (!string.IsNullOrEmpty(branchLine))
                    {
                        // Looks like std::string Version::Branch()   {return "Skyline/skyline_9_7";}
                        var branch = branchLine.Split(new[] { "\"" }, StringSplitOptions.None)[1];
                        if (branch.Equals("master"))
                        {
                            branchUrl = GIT_MASTER_URL;
                        }
                        else
                        {
                            branchUrl = GIT_BRANCHES_URL + branch; // Looks like https://github.com/ProteoWizard/pwiz/tree/Skyline/skyline_9_7
                        }
                    }
                        
                    break;
                }
                catch (Exception ex)
                {
                    Log("Exception while unzipping SkylineTester: " + ex.Message + " (Probably still being built, will retry every 60 seconds for 30 minutes.)");
                    if (i == attempts - 1)
                    {
                        QuitWithError("Unable to identify branch from Version.cpp in SkylineTester");
                    }
                    Thread.Sleep(60 * 1000 * retryTimeoutInMinutes);
                }
            }
            // Create ".skytr" file to execute nightly build in SkylineTester.
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "SkylineNightly.SkylineNightly.skytr";
            double durationHours;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    QuitWithError(result = "Embedded resource is broken");
                    return result; 
                }
                using (var reader = new StreamReader(stream))
                {
                    var skylineTester = Xml.FromString(reader.ReadToEnd());
                    skylineTester.GetChild("nightlyStartTime").Set(DateTime.Now.ToShortTimeString());
                    skylineTester.GetChild("nightlyRoot").Set(nightlyDir);
                    skylineTester.GetChild("buildRoot").Set(_skylineTesterDir);
                    skylineTester.GetChild("nightlyRunPerfTests").Set(WithPerfTests ? "true" : "false");
                    skylineTester.GetChild("nightlyDuration").Set(((int)TargetDuration.TotalHours).ToString());
                    skylineTester.GetChild("nightlyRepeat").Set(_runMode == RunMode.stress ? "100" : "1");
                    skylineTester.GetChild("nightlyRandomize").Set(_runMode == RunMode.stress ? "true" : "false");
                    if (!string.IsNullOrEmpty(branchUrl) && branchUrl.Contains("tree"))
                    {
                        skylineTester.GetChild("nightlyBuildTrunk").Set("false");
                        skylineTester.GetChild("nightlyBranch").Set("true");
                        skylineTester.GetChild("nightlyBranchUrl").Set(branchUrl);
                        Log("Testing branch at " + branchUrl);
                    }
                    skylineTester.Save(skylineNightlySkytr);
                    durationHours = double.Parse(skylineTester.GetChild("nightlyDuration").Value);
                }
            }


            // Start SkylineTester to do the build.
            var skylineTesterExe = Path.Combine(_skylineTesterDir, "SkylineTester Files", "SkylineTester.exe");
            Log(string.Format("Starting {0} with config file {1}, which contains:", skylineTesterExe, skylineNightlySkytr));
            foreach (var line in File.ReadAllLines(skylineNightlySkytr))
            {
                Log(line);
            }

            var processInfo = new ProcessStartInfo(skylineTesterExe, skylineNightlySkytr)
            {
                WorkingDirectory = Path.GetDirectoryName(skylineTesterExe) ?? ""
            };

            bool retryTester;
            const int maxRetryMinutes = 60;

            var logMonitor = new LogFileMonitor(_logDir, LogFileName, _runMode);
            logMonitor.Start();

            do
            {
                var skylineTesterProcess = Process.Start(processInfo);
                if (skylineTesterProcess == null)
                {
                    QuitWithError(result = "SkylineTester did not start");
                    return result;
                }
                Log("SkylineTester started");

                // Calculate end time: convert to UTC, add the duration, then convert back to local time.
                // Conversion to UTC before adding the duration avoids DST issues.
                var endTime = skylineTesterProcess.StartTime.ToUniversalTime().AddHours(durationHours).ToLocalTime();
                var originalEndTime = endTime;
                for (;; Thread.Sleep(1000))
                {
                    if (skylineTesterProcess.HasExited)
                    {
                        if (skylineTesterProcess.ExitCode == 0xDEAD)
                        {
                            // User killed, don't post
                            Log(result = SkylineTesterStoppedByUser);
                            return result;
                        }
                        Log("SkylineTester finished");
                        break;
                    }
                    else if (DateTime.Now > endTime.AddMinutes(30)) // 30 minutes grace before we kill SkylineTester
                    {
                        SaveErrorScreenshot();
                        Log(result = "SkylineTester has exceeded its " + durationHours + " hour runtime.  You should investigate.");
                        break;
                    }

                    if (endTime == originalEndTime)
                    {
                        if (logMonitor.IsHang)
                        {
                            var now = DateTime.Now;
                            if (9 <= now.Hour && now.Hour < 17)
                            {
                                // between 9am-5pm, set end time to 4 hours from now (unless scheduled end is already 4+ hours from now)
                                var newEndTime = DateTime.Now.AddHours(4);
                                if (newEndTime > originalEndTime && SetEndTime(newEndTime))
                                    endTime = newEndTime;
                            }
                            else
                            {
                                // extend the end time until 12pm to give us more time to attach a debugger
                                var newEndTime = originalEndTime.AddHours(16);
                                newEndTime = new DateTime(newEndTime.Year, newEndTime.Month, newEndTime.Day, 12, 0, 0);
                                if (SetEndTime(newEndTime))
                                    endTime = newEndTime;
                            }
                        }
                    }
                    else if (!logMonitor.IsHang)
                    {
                        // If we get here, we've already extended the end time due to a hang and log file is now being modified again.
                        DateTime newEndTime;
                        if (logMonitor.IsDebugger)
                        {
                            // Assume that the log file is being modified because someone has taken manual action, and extend the end time further
                            // to prevent SkylineTester from being killed while someone is looking at it.
                            newEndTime = originalEndTime.AddDays(2);
                        }
                        else
                        {
                            // SkylineTester continued without a debugger being attached. Restore original end time.
                            newEndTime = originalEndTime;
                            var min = DateTime.Now.AddMinutes(1);
                            if (newEndTime <= min)
                                newEndTime = min;
                        }
                        if (endTime != newEndTime && SetEndTime(newEndTime))
                            endTime = newEndTime;
                    }
                }

                var actualDuration = DateTime.UtcNow - skylineTesterProcess.StartTime.ToUniversalTime();
                retryTester = actualDuration.TotalMinutes < maxRetryMinutes;
                if (retryTester)
                {
                    // Retry a very short test run if there is no log file or the log file does not contain any tests
                    string logFile = GetLatestLog();
                    if (logFile != null && File.Exists(logFile))
                        retryTester = ParseTests(File.ReadAllText(logFile), false) == 0;
                    if (retryTester)
                    {
                        Log("No tests run in " + Math.Round(actualDuration.TotalMinutes) + " minutes. Will try again in " + retryTimeoutInMinutes + " minutes.");
                        Thread.Sleep(60 * 1000 * retryTimeoutInMinutes);
                    }
                }
            }
            while (retryTester);

            logMonitor.Stop();
            return result;
        }

        public void StartLog(RunMode runMode)
        {
            _startTime = DateTime.Now;

            // Create log file.
            LogFileName = Path.Combine(_logDir, string.Format("SkylineNightly-{0}-{1}.log", runMode, _startTime.ToString("yyyy-MM-dd-HH-mm", CultureInfo.InvariantCulture)));
            Log(_startTime.ToShortDateString());
        }

        private void DownloadSkylineTester(string skylineTesterZip, RunMode mode, bool desperate)
        {
            // The current recommendation from MSFT for future-proofing HTTPS https://docs.microsoft.com/en-us/dotnet/framework/network-programming/tls
            // is don't specify TLS levels at all, let the OS decide. But we worry that this will mess up Win7 and Win8 installs, so we continue to specify explicitly
            try
            {
                var Tls13 = (SecurityProtocolType)12288; // From decompiled SecurityProtocolType - compiler has no definition for some reason
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | Tls13;
            }
            catch (NotSupportedException)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12; // Probably an older Windows Server
            }

            using (var client = new WebClient())
            {
                client.Credentials = new NetworkCredential(TEAM_CITY_USER_NAME, TEAM_CITY_USER_PASSWORD);
                var isRelease = ((mode == RunMode.release) || (mode == RunMode.release_perf));
                var isIntegration = mode == RunMode.integration || mode == RunMode.integration_perf;
                var branchType = (isRelease||isIntegration) ? "" : "?branch=master"; // TC has a config just for release branch, and another for integration branch, but main config builds pull requests, other branches etc
                var buildType = isIntegration ? TEAM_CITY_BUILD_TYPE_64_INTEGRATION : isRelease ? TEAM_CITY_BUILD_TYPE_64_RELEASE : TEAM_CITY_BUILD_TYPE_64_MASTER;

                string zipFileLink = string.Format(TEAM_CITY_ZIP_URL, buildType, branchType);
                if (desperate)
                {
                    zipFileLink = zipFileLink.Replace(".lastFinished", ".lastSuccessful");
                    Log("In retry, download possibly stale (\".lastSuccessful\" rather than \".lastFinished\") SkylineTester zip file as " + zipFileLink);
                }
                else
                {
                    Log("Download SkylineTester zip file as " + zipFileLink);
                }
                client.DownloadFile(zipFileLink, skylineTesterZip); // N.B. depending on caller to do try/catch
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
                catch (Exception e)
                {
                    Log("Error attempting to unzip SkylineTester: " + e);
                    return false;
                }
            }
            return true;
        }

        public RunMode Parse(string logFile = null, bool parseOnlyNoXmlOut = false)
        {
            if (logFile == null)
                logFile = GetLatestLog();
            if (logFile == null || !File.Exists(logFile))
                throw new Exception(string.Format("cannot locate {0}", logFile ?? "current log"));
            var log = File.ReadAllText(logFile);
            var parsedDuration = TargetDuration;

            // Extract log start time from log contents
            var reStartTime = new Regex(@"\n\# Nightly started (.*)\r\n", RegexOptions.Compiled); // As in "# Nightly started Thursday, May 12, 2016 8:00 PM"
            var reStoppedTime = new Regex(@"\n\# Stopped (.*)\r\n");
            var stMatch = reStartTime.Match(log);
            if (stMatch.Success)
            {
                var dateTimeStr = stMatch.Groups[1].Value;
                if (DateTime.TryParse(dateTimeStr, out _startTime))
                {
                    _startTime = DateTime.SpecifyKind(_startTime, DateTimeKind.Local);
                }
            }
            var endMatch = reStoppedTime.Match(log);
            if (endMatch.Success)
            {
                var dateTimeEnd = endMatch.Groups[1].Value;
                if (DateTime.TryParse(dateTimeEnd, out var endTime))
                {
                    endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Local);
                    parsedDuration = endTime.ToUniversalTime() - _startTime.ToUniversalTime();
                }
            }

            // Extract all test lines.
            var testCount = ParseTests(log);

            // Extract failures.
            ParseFailures(log);

            // Extract leaks.
            ParseLeaks(log);

            var hasPerftests = log.Contains("# Perf tests");
            var isIntegration = new Regex(@"git\.exe.*clone.*-b").IsMatch(log);
            var isTrunk = !isIntegration && !log.Contains("Testing branch at");

            var machineName = Environment.MachineName;
            // Get machine name from logfile name, in case it's not from this machine
            var reMachineName = new Regex(@"(.*)_\d+\-\d+\-\d+_\d+\-\d+\-\d+\.\w+", RegexOptions.Compiled); // As in "NATBR-LAB-PC_2016-05-12_20-00-19.log"
            var mnMatch = reMachineName.Match(Path.GetFileName(logFile));
            if (mnMatch.Success)
            {
                machineName = mnMatch.Groups[1].Value.ToUpperInvariant();
            }

            // See if we can parse revision info from the log
            string revisionInfo = null;
            string gitHash = null;
            // Checked out revision 9708.
            var reRevision = new Regex(@"\nChecked out revision (.*)\.\r\n", RegexOptions.Compiled); // As in "Checked out revision 9708."
            var revMatch = reRevision.Match(log);
            if (revMatch.Success)
            {
                revisionInfo = revMatch.Groups[1].Value;
                gitHash = "(svn)";
            }
            else // Look for log message where we emit our build ID
            {
                // look for build message like "ProteoWizard 3.0.18099.a0147f2 x64 AMD64"
                reRevision = new Regex(@"\nProteoWizard \d+\.\d+\.([^ ]*)\.([^ ]*).*\r\n", RegexOptions.Compiled); 
                revMatch = reRevision.Match(log);
                if (revMatch.Success)
                {
                    revisionInfo = revMatch.Groups[1].Value;
                    gitHash = revMatch.Groups[2].Value;
                }
            }

            _nightly["id"] = machineName;
            _nightly["os"] = Environment.OSVersion;
            var buildroot = ParseBuildRoot(log);
            _nightly["revision"] = revisionInfo ?? GetRevision(buildroot);
            _nightly["git_hash"] = gitHash ?? string.Empty;
            _nightly["start"] = _startTime;
            int durationMinutes = (int)parsedDuration.TotalMinutes;
            // Round down or up by 1 minute to report even hours in this common case
            if (durationMinutes % 60 == 1)
                durationMinutes--;
            else if (durationMinutes % 60 == 59)
                durationMinutes++;
            _nightly["duration"] = durationMinutes;
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
                ? (hasPerftests ? RunMode.perf : RunMode.trunk)
                : (isIntegration ? (hasPerftests ? RunMode.integration_perf : RunMode.integration) :  (hasPerftests ? RunMode.release_perf : RunMode.release));
        }

        private class TestLogLineProperties
        {
            private enum EndType { heaps, handles, old, none }

            private static Regex END_TEST_OLD = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+) MB, (\d+) sec\.\r\n", RegexOptions.Compiled);
            private static Regex END_TEST_HANDLES = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+) MB, ([\.\d]+)/([\.\d]+) handles, (\d+) sec\.\r\n", RegexOptions.Compiled);
            private static Regex END_TEST_HEAPS = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+)/([\.\d]+) MB, ([\.\d]+)/([\.\d]+) handles, (\d+) sec\.\r\n", RegexOptions.Compiled);

            private Match _startMatch;
            private Match _endMatch;
            private EndType _endMatchType;

            public TestLogLineProperties(Match startMatch, string log)
            {
                _startMatch = startMatch;
                _endMatchType = FindEndMatch(log);
            }

            private EndType FindEndMatch(string log)
            {
                // Enumerate through possible formats, starting with the most recent first
                var regexes = new[] { END_TEST_HEAPS, END_TEST_HANDLES, END_TEST_OLD };
                for (int i = 0; i < regexes.Length; i++)
                {
                    var match = regexes[i].Match(log, _startMatch.Index);
                    if (match.Success)
                    {
                        _endMatch = match;
                        return (EndType) i;
                    }
                }

                return EndType.none;
            }

            public string Timestamp { get { return _startMatch.Groups[1].Value; } }
            public string PassId { get { return _startMatch.Groups[2].Value; } }
            public string TestId { get { return _startMatch.Groups[3].Value; } }
            public string Name { get { return _startMatch.Groups[4].Value; } }
            public string Language { get { return _startMatch.Groups[5].Value; } }

            public string Managed { get { return _endMatch.Groups[1].Value; } }
            public string Heaps { get { return _endMatchType == EndType.heaps ? _endMatch.Groups[2].Value : null; } }
            public string Total { get { return _endMatch.Groups[_endMatchType == EndType.heaps ? 3 : 2].Value; } }
            public string UserGdiHandles { get { return _endMatchType != EndType.old ? _endMatch.Groups[_endMatch.Groups.Count-3].Value : null; } }
            public string TotalHandles { get { return _endMatchType != EndType.old ? _endMatch.Groups[_endMatch.Groups.Count-2].Value : null; } }
            public string Duration { get { return _endMatch.Groups[_endMatch.Groups.Count-1].Value; } }

            public bool IsEnded
            {
                get
                {
                    return _endMatchType != EndType.none &&
                           !string.IsNullOrEmpty(Managed) &&
                           !string.IsNullOrEmpty(Total) &&
                           !string.IsNullOrEmpty(Duration);
                }
            }
        }

        private int ParseTests(string log, bool storeXml = true)
        {
            var startTest = new Regex(@"\r\n\[(\d\d:\d\d)\] +(\d+).(\d+) +(\S+) +\((\w\w)\) ", RegexOptions.Compiled);

            string lastPass = null;
            int testCount = 0;
            for (var startMatch = startTest.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var lineProperties = new TestLogLineProperties(startMatch, log);

                if (!lineProperties.IsEnded)
                    continue;

                if (lastPass != lineProperties.PassId)
                {
                    lastPass = lineProperties.PassId;
                    if (storeXml)
                    {
                        _pass = _nightly.Append("pass");
                        _pass["id"] = lineProperties.PassId;
                    }
                }

                if (storeXml)
                {
                    var test = _pass.Append("test");
                    test["id"] = lineProperties.TestId;
                    test["name"] = lineProperties.Name;
                    test["language"] = lineProperties.Language;
                    test["timestamp"] = lineProperties.Timestamp;
                    test["duration"] = lineProperties.Duration;
                    test["managed"] = lineProperties.Managed;
                    if (!string.IsNullOrEmpty(lineProperties.Heaps))
                        test["committed"] = lineProperties.Heaps;
                    test["total"] = lineProperties.Total;
                    if (!string.IsNullOrEmpty(lineProperties.UserGdiHandles))
                        test["user_gdi"] = lineProperties.UserGdiHandles;
                    if (!string.IsNullOrEmpty(lineProperties.TotalHandles))
                        test["handles"] = lineProperties.TotalHandles;
                }

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
            // Leaks in Private Bytes
            var leakPattern = new Regex(@"!!! (\S+) LEAKED ([0-9.]+) bytes", RegexOptions.Compiled);
            for (var match = leakPattern.Match(log); match.Success; match = match.NextMatch())
            {
                var leak = _leaks.Append("leak");
                leak["name"] = match.Groups[1].Value;
                leak["bytes"] = match.Groups[2].Value;
            }
            // Leaks in Process and Managed Heaps
            var leakTypePattern = new Regex(@"!!! (\S+) LEAKED ([0-9.]+) ([^ ]*) bytes", RegexOptions.Compiled);
            for (var match = leakTypePattern.Match(log); match.Success; match = match.NextMatch())
            {
                var leak = _leaks.Append("leak");
                leak["name"] = match.Groups[1].Value;
                leak["bytes"] = match.Groups[2].Value;
                leak["type"] = match.Groups[3].Value;
            }
            // Handle leaks
            var leakHandlesPattern = new Regex(@"!!! (\S+) HANDLE-LEAKED ([.0-9]+) (\S+)", RegexOptions.Compiled);
            for (var match = leakHandlesPattern.Match(log); match.Success; match = match.NextMatch())
            {
                var leak = _leaks.Append("leak");
                leak["name"] = match.Groups[1].Value;
                leak["handles"] = match.Groups[2].Value;
                leak["type"] = match.Groups[3].Value;
            }
        }

        private string ParseBuildRoot(string log)
        {
            // Look for:  > "C:\Program Files\Git\cmd\git.exe" clone "https://github.com/ProteoWizard/pwiz" "C:\Nightly\SkylineTesterForNightly_trunk\pwiz"
            var brPattern = new Regex(@".*"".*git\.exe"" clone "".*"" ""(.*)""", RegexOptions.Compiled);
            var match = brPattern.Match(log);
            if (!match.Success)
            {
                brPattern = new Regex(@"Deleting Build directory\.\.\.\r\n\> rmdir /s ""(\S+)""", RegexOptions.Compiled);
                match = brPattern.Match(log);
            }
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return PwizDir;
        }

        /// <summary>
        /// Post the latest results to the server.
        /// </summary>
        public string Post(RunMode mode, string xmlFile = null)
        {
            if (xmlFile == null)
            {
                xmlFile = GetLatestLog();    // Change extension to .xml below
                if (xmlFile == null)
                    return string.Empty;
                xmlFile = Path.ChangeExtension(xmlFile, ".xml"); // In case it's actually the log file name
            }
            
            var xml = File.ReadAllText(xmlFile);
            var logFile = GetLatestLog();
            if (logFile != null)
            {
                var log = File.ReadAllText(logFile);
                XDocument doc = XDocument.Parse(xml);
                if (doc.Root != null)
                {
                    doc.Root.Add(new XElement("Log", log));
                    xml = doc.ToString();
                }
            }

            if (string.IsNullOrEmpty(xml) || !xml.Contains("<test id"))
            {
                return @"No tests found in log. No results posted";
            }

            string url;
            // Post to server.
            if (mode == RunMode.integration)
                url = LABKEY_INTEGRATION_URL;
            else if (mode == RunMode.integration_perf)
                url = LABKEY_INTEGRATION_PERF_URL;
            else if (mode == RunMode.release_perf)
                url = LABKEY_RELEASE_PERF_URL;
            else if (mode == RunMode.release)
                url = LABKEY_RELEASE_URL;
            else if (mode == RunMode.perf)
                url = LABKEY_PERF_URL;
            else if (mode == RunMode.stress)
                url = LABKEY_STRESS_URL;
            else
                url = LABKEY_URL;
            var result = PostToLink(url, xml, xmlFile);
            var resultParts = result.ToLower().Split(':');
            if (resultParts.Length == 2 && resultParts[0].Contains("success") && resultParts[1].Contains("true"))
                result = string.Empty;
            return result;
        }

        public string GetLatestLog()
        {
            return GetLatestLog(_logDir);
        }

        public static string GetLatestLog(string logDir)
        {
            var directory = new DirectoryInfo(logDir);
            var logFile = directory.GetFiles()
                .Where(f => f.Name.StartsWith(Environment.MachineName) && f.Name.EndsWith(".log"))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            return logFile == null ? null : logFile.FullName;
        }

        /// <summary>
        /// Post data to the given link URL.
        /// </summary>
        private string PostToLink(string link, string postData, string filePath)
        {
            var errmessage = string.Empty;
            Log("Posting results to " + link);
            for (var retry = 5; retry > 0; retry--)
            {
                string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
                byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

                var wr = (HttpWebRequest)WebRequest.Create(link);
                wr.ProtocolVersion = HttpVersion.Version10;
                wr.ContentType = "multipart/form-data; boundary=" + boundary;
                wr.Method = "POST";
                wr.KeepAlive = true;
                wr.Credentials = CredentialCache.DefaultCredentials;

                if (SetCSRFToken(wr, LogFileName))
                {
                    var rs = wr.GetRequestStream();

                    rs.Write(boundarybytes, 0, boundarybytes.Length);
                    const string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
                    string header = string.Format(headerTemplate, "xml_file", filePath != null ? Path.GetFileName(filePath) : "xml_file", "text/xml");
                    byte[] headerbytes = Encoding.UTF8.GetBytes(header);
                    rs.Write(headerbytes, 0, headerbytes.Length);
                    var bytes = Encoding.UTF8.GetBytes(postData);
                    rs.Write(bytes, 0, bytes.Length);

                    byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                    rs.Write(trailer, 0, trailer.Length);
                    rs.Close();

                    WebResponse wresp = null;
                    try
                    {
                        wresp = wr.GetResponse();
                        var stream2 = wresp.GetResponseStream();
                        if (stream2 != null)
                        {
                            var reader2 = new StreamReader(stream2);
                            var result = reader2.ReadToEnd();
                            return result;
                        }
                    }
                    catch (Exception e)
                    {
                        Log(errmessage = e.ToString());
                        if (wresp != null)
                        {
                            wresp.Close();
                        }
                    }
                }
                if (retry > 1)
                {
                    Thread.Sleep(30000);
                    Log("Retrying post");
                    errmessage = String.Empty;
                }
            }
            Log(errmessage = "Failed to post results: " + errmessage); 
            return errmessage;
        }

        public static string SendEmailNotification(string to, string subject, string message)
        {
            var postParams = new List<string>();
            if (!string.IsNullOrEmpty(to))
                postParams.Add("to=" + Uri.EscapeDataString(to));
            if (!string.IsNullOrEmpty(subject))
                postParams.Add("subject=" + Uri.EscapeDataString(subject));
            if (!string.IsNullOrEmpty(message))
                postParams.Add("message=" + Uri.EscapeDataString(message));
            var postData = Encoding.ASCII.GetBytes(string.Join("&", postParams));
            for (var retry = 5; retry > 0; retry--)
            {
                var request = (HttpWebRequest) WebRequest.Create(LABKEY_EMAIL_NOTIFICATION_URL);
                request.ProtocolVersion = HttpVersion.Version11;
                request.ContentType = "application/x-www-form-urlencoded";
                request.Method = "POST";
                request.KeepAlive = false;
                request.Credentials = CredentialCache.DefaultCredentials;
                request.Timeout = 30000; // 30 second timeout

                if (SetCSRFToken(request, null))
                {
                    try
                    {
                        using (var stream = request.GetRequestStream())
                        {
                            stream.Write(postData, 0, postData.Length);
                        }

                        using (var response = (HttpWebResponse)request.GetResponse())
                        using (var responseStream = response.GetResponseStream())
                        {
                            if (responseStream != null)
                            {
                                using (var responseReader = new StreamReader(responseStream))
                                {
                                    return responseReader.ReadToEnd();
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // We will retry
                    }
                }
                if (retry > 1)
                    Thread.Sleep(30000);
            }
            return null;
        }

        private static string GetNightlyDir()
        {
            var nightlyDir = Settings.Default.NightlyFolder;
            return Path.IsPathRooted(nightlyDir)
                ? nightlyDir
                // Kept for backward compatibility, but we don't allow this anymore, because the Documents
                // folder is a terrible place to be running these high-use, nightly tests from.
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nightlyDir);
        }

        private static string GitCommand(string workingdir, string cmd)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var exe = Path.Combine(programFiles, @"Git\cmd\git.exe");
            Process git = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = exe,
                    WorkingDirectory = workingdir,
                    Arguments = cmd,
                    CreateNoWindow = true
                }
            };
            git.Start();
            var gitOutput = git.StandardOutput.ReadToEnd();
            git.WaitForExit();
            return gitOutput.Trim();
        }

        private string GetRevision(string pwizDir)
        {

            var revisionHash = GitCommand(pwizDir, @"rev-parse --short HEAD");
            var revision = "unknownDate." + revisionHash;
            return revision;
        }

        /// <summary>
        /// Delete a file or directory, with quite a lot of retry on the expectation that 
        /// it's probably the TortoiseGit windows explorer icon plugin getting in your way
        /// </summary>
        private void Delete(string fileOrDir)
        {
            for (var i = 5; i >0; i--)
            {
                try
                {
                    DeleteRecursive(fileOrDir);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == 1)
                        throw;
                    Log("Retrying failed delete of " + fileOrDir + ": " + ex.Message);
                    var random = new Random();
                    Thread.Sleep(1000 + random.Next(0, 5000)); // A little stutter-step to avoid unlucky sync with TortoiseGit icon update
                }
            }
        }

        private void DeleteRecursive(string fileOrDir)
        {
            if (File.Exists(fileOrDir))
            {
                File.SetAttributes(fileOrDir, FileAttributes.Normal);   // Protect against failing on read-only files
                File.Delete(fileOrDir);
            }
            else if (Directory.Exists(fileOrDir))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(fileOrDir))
                {
                    DeleteRecursive(entry);
                }
                Directory.Delete(fileOrDir, true);
            }
        }

        private void Log(string message)
        {
            Log(LogFileName, message);
        }

        public static string Log(string logFileName, string message)
        {
            var time = DateTime.Now;
            var timestampedMessage = string.Format(
                "[{0}:{1}:{2}] {3}",
                time.Hour.ToString("D2"),
                time.Minute.ToString("D2"),
                time.Second.ToString("D2"),
                message);
            if (!string.IsNullOrEmpty(logFileName))
                File.AppendAllText(logFileName, timestampedMessage + Environment.NewLine);
            return timestampedMessage;
        }

        private void QuitWithError(string message)
        {
            SaveErrorScreenshot();
            Finish("Quit with error",message);
        }

        private void SaveErrorScreenshot()
        {
            // Capture the screen in hopes of finding exception dialogs etc
            // From http://stackoverflow.com/questions/362986/capture-the-screen-into-a-bitmap

            try
            {
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
            catch (Exception x)
            {
                Log("Could not create diagnostic screenshot: got exception \"" + x.Message + "\"");
            }
        }

        private static bool SetCSRFToken(HttpWebRequest postReq, string logFileName)
        {
            var url = LABKEY_HOME_URL;

            var sessionCookies = new CookieContainer();
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = @"GET";
                request.CookieContainer = sessionCookies;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    postReq.CookieContainer = sessionCookies;
                    var csrf = response.Cookies[LABKEY_CSRF];
                    if (csrf != null)
                    {
                        // The server set a cookie called X-LABKEY-CSRF, get its value and add a header to the POST request
                        postReq.Headers.Add(LABKEY_CSRF, csrf.Value);
                    }
                    else
                    {
                        Log(logFileName, @"CSRF token not found.");
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Log(logFileName, $@"Error establishing a session and getting a CSRF token: {e}");
                return false;
            }
        }

        private static readonly ChannelFactory<IEndTimeSetter> END_TIME_SETTER_FACTORY =
            new ChannelFactory<IEndTimeSetter>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/Nightly/SetEndTime"));

        // Set the end time of an already running nightly run (e.g. if there is a hang and we want to give more time for someone to attach a debugger)
        public bool SetEndTime(DateTime endTime)
        {
            Exception exception = null;
            try
            {
                END_TIME_SETTER_FACTORY.CreateChannel().SetEndTime(endTime);
            }
            catch (Exception x)
            {
                exception = x;
            }
            Log(string.Format("Setting nightly end time to {0} {1}: {2}",
                endTime.ToShortDateString(), endTime.ToShortTimeString(), exception == null ? "OK" : exception.Message));
            return exception == null;
        }

        // Allows SkylineNightly to change the stop time of a nightly run via IPC
        [ServiceContract]
        public interface IEndTimeSetter
        {
            [OperationContract]
            void SetEndTime(DateTime endTime);
        }
    }

    // ReSharper restore LocalizableElement
}
