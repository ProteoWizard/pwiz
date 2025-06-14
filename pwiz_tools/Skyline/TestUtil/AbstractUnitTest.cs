﻿/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using TestRunnerLib;

// Once-per-application setup information to perform logging with log4net.
[assembly: log4net.Config.XmlConfigurator(ConfigFile = "SkylineLog4Net.config", Watch = true)]

namespace pwiz.SkylineTestUtil
{

    /// <summary>
    /// This is the base class for every unit test in Skyline.  It enables logging
    /// and also provides quick information about the running time of the test.
    /// </summary>
    [DeploymentItem("SkylineLog4Net.config")]
    public class AbstractUnitTest
    {
        private static readonly Stopwatch STOPWATCH = new Stopwatch();

        // NB this text needs to agree with that in UpdateRun() in pwiz_tools\Skyline\SkylineTester\TabQuality.cs
        public const string MSG_SKIPPING_SMALLMOLECULE_TEST_VERSION = " (RunSmallMoleculeTestVersions=False, skipping.) ";

        public const string MSG_SKIPPING_SLOW_RESHARPER_ANALYSIS_TEST = " (test is too slow running under ReSharper analysis, skippiing.) ";

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBeProtected.Global
        public TestContext TestContext { get; set; }
// ReSharper restore MemberCanBeProtected.Global
// ReSharper restore UnusedAutoPropertyAccessor.Global

        /// <summary>
        /// When true, the test attempts to remove all the files it creates in downloads folder.
        /// This helps reduce disk space required to run the tests just once, but adds a lot of
        /// download overhead when running the tests multiple times. At present, this only gets
        /// set to true for TeamCity, where the VMs start clean every time, tests only get run
        /// once, and limiting VM disk space is cost effective.
        /// </summary>
        protected DesiredCleanupLevel DesiredCleanupLevel
        {
            get { return TestContext.GetEnumValue("DesiredCleanupLevel", DesiredCleanupLevel.none); }  // Return none if unspecified
            set { TestContext.Properties["DesiredCleanupLevel"] = value.ToString(); }
        }

        /// <summary>
        /// When false, tests should not access resources on the internet other than
        /// downloading the test ZIP files. e.g. UniProt, Koina, Chorus, etc.
        /// </summary>
        protected bool AllowInternetAccess
        {
            get { return TestContext.GetBoolValue("AccessInternet", false); }  // Return false if unspecified
            set { TestContext.Properties["AccessInternet"] = value.ToString(CultureInfo.InvariantCulture); }
        }

        /// <summary>
        /// When false, perf tests get short-circuited doing no actual testing
        /// </summary>
        protected bool RunPerfTests
        {
            get { return TestContext.GetBoolValue("RunPerfTests", true); }  // Return true if unspecified
            set { TestContext.Properties["RunPerfTests"] = value.ToString(CultureInfo.InvariantCulture); }
        }

        /// <summary>
        /// When true, re-download data sets on test failure in case it's due to stale data.
        /// </summary>
        protected bool RetryDataDownloads
        {
            get { return TestContext.GetBoolValue("RetryDataDownloads", false); }  // Return false if unspecified
            set { TestContext.Properties["RetryDataDownloads"] = value.ToString(CultureInfo.InvariantCulture); }
        }

        /// <summary>
        /// Determines whether or not to (re)record audit logs for tests.
        /// </summary>
        protected bool RecordAuditLogs
        {
            get { return TestContext.GetBoolValue("RecordAuditLogs", false); }  // Return false if unspecified
            set { TestContext.Properties["RecordAuditLogs"] = value.ToString(CultureInfo.InvariantCulture); }
        }

        protected bool RunSmallMoleculeTestVersions
        {
            get { return TestContext.GetBoolValue("RunSmallMoleculeTestVersions", true); }
            set { TestContext.Properties["RunSmallMoleculeTestVersions"] = value.ToString(CultureInfo.InvariantCulture); }
        }

        protected int TestPass
        {
            get { return (int) TestContext.GetLongValue("TestPass", 0); }
            set { TestContext.Properties["TestPass"] = value.ToString(); }
        }

        /// <summary>
        /// Controls whether or not certain machines run the often flaky TestToolService
        /// </summary>
        protected bool SkipTestToolService => Environment.MachineName.Equals(@"BRENDANX-UW7");
        public const string MSG_SKIPPING_TEST_TOOL_SERVICE = @"AbstractUnitTest.SkipTestToolService is set for this machine, no test was actually performed";

        /// <summary>
        /// Perf tests (long running, huge-data-downloading) should be declared
        /// in the TestPerf namespace so that they can be skipped when the RunPerfTests 
        /// flag is unset.
        /// </summary>
        public const string PERFTEST_NAMESPACE = @"TestPerf";
        public bool IsPerfTest
        {
            get { return (PERFTEST_NAMESPACE.Equals(GetType().Namespace)); }
        }

        /// <summary>
        /// True if the test is being run by MSTest, i.e. inside Visual Studio or through MSBuild.
        /// </summary>
        public bool IsMsTestRun
        {
            // Lots of properties with MSTest which are not supplied by TestRunner. Not sure this is the best one.
            get { return TestContext.Properties.Contains("DeploymentDirectory"); }
        }

        /// <summary>
        /// True if TestRunner is running the test and not MSTest (or presumably ReSharper)
        /// </summary>
        public bool IsRunningInTestRunner
        {
            get { return TestContext is TestRunnerContext; }
        }

        /// <summary>
        /// Returns true if the test is not running in TestRunner. Also outputs a message to the console
        /// indicating that the test is being skipped. It is the caller's responsibility to actually
        /// skip the test if this method returns true.
        /// </summary>
        protected bool SkipWiff2TestInTestExplorer(string testName)
        {
            if (IsRunningInTestRunner)
            {
                return false;
            }
            Console.Out.WriteLine("Skipping {0} because Wiff2 DLLs do not load in the correct order when test is executed by Test Explorer.", testName);
            Console.Out.WriteLine("This test only runs to completion when executed by TestRunner or SkylineTester.");
            return true;
        }

        public static string PanoramaDomainAndPath => @"panoramaweb.org/_webdav/MacCoss/software/%40files";

        public static string GetPerfTestDataURL(string filename)
        {
            return @"https://" + PanoramaDomainAndPath + @"/perftests/" + filename;
        }

        private string[] _testFilesZips;
        public string TestFilesZip
        {
            get
            {
                // ReSharper disable LocalizableElement
                Assert.AreEqual(1, _testFilesZips.Length, "Attempt to use TestFilesZip on test with multiple ZIP files.\nUse TestFilesZipPaths instead.");
                // ReSharper restore LocalizableElement
                return _testFilesZips[0];
            }
            set { TestFilesZipPaths = new[] { value }; }
        }

        /// <summary>
        /// Optional list of files to be retained from run to run. Useful for really
        /// large data files which are expensive to extract and keep as local copies.
        /// </summary>
        public string[] TestFilesPersistent { get; set; }

        /// <summary>
        /// Tracks which zip files were downloaded this run, and which might possibly be stale
        /// </summary>
        public Dictionary<string, bool> DictZipFileIsKnownCurrent { get; private set; }

        /// <summary>
        /// One bool per TestFilesZipPaths indicating whether to unzip in the root directory (true) or a sub-directory (false or null)
        /// </summary>
        public bool[] TestFilesZipExtractHere { get; set; }

        public bool IsExtractHere(int zipPathIndex)
        {
            return TestFilesZipExtractHere != null && TestFilesZipExtractHere[zipPathIndex];
        }

        public string[] TestFilesZipPaths
        {
            get { return _testFilesZips; }
            set
            {
                string[] zipPaths = value;
                _testFilesZips = new string[zipPaths.Length];
                DictZipFileIsKnownCurrent = new Dictionary<string, bool>();
                for (int i = 0; i < zipPaths.Length; i++)
                {
                    var zipPath = zipPaths[i];
                    // If the file is on the web, save it to the local disk in the developer's
                    // Downloads folder for future use
                    if (zipPath.Substring(0, 8).ToLower().Equals(@"https://") || zipPath.Substring(0, 7).ToLower().Equals(@"http://"))
                    {
                        var targetFolder = GetTargetZipFilePath(zipPath, out var zipFilePath);
                        if (!File.Exists(zipFilePath) &&
                           (!IsPerfTest || RunPerfTests)) // If this is a perf test, skip download unless perf tests are enabled
                        {
                            zipPath = DownloadZipFile(targetFolder, zipPath, zipFilePath);
                            DictZipFileIsKnownCurrent.Add(zipPath, true);
                        }
                        else
                        {
                            DictZipFileIsKnownCurrent.Add(zipPath, false); // May wish to retry test with a fresh download if it fails
                        }
                        zipPath = zipFilePath;
                    }
                    _testFilesZips[i] = zipPath;
                }
            }
        }

        public void UnzipTestFiles()
        {
            // Unzip test files.
            if (TestFilesZipPaths != null)
            {
                TestFilesDirs = new TestFilesDir[TestFilesZipPaths.Length];
                for (int i = 0; i < TestFilesZipPaths.Length; i++)
                {
                    TestFilesDirs[i] = new TestFilesDir(TestContext, TestFilesZipPaths[i], TestDirectoryName,
                        TestFilesPersistent, IsExtractHere(i));
                }
                CleanupPersistentDir(); // Clean up before recording metrics
                foreach (var dir in TestFilesDirs)
                {
                    dir.RecordMetrics();
                }
            }
        }

        /// <summary>
        /// Override this function with any specific file deletions that need to
        /// happen to avoid leaving files in the PersistentFilesDir.
        /// </summary>
        protected virtual void CleanupPersistentDir()
        {
        }

        private static string DownloadZipFile(string targetFolder, string zipPath, string zipFilePath)
        {
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            bool downloadFromS3 = Environment.GetEnvironmentVariable("SKYLINE_DOWNLOAD_FROM_S3") == "1";
            string s3hostname = @"ci.skyline.ms";
            string message = string.Empty;
            for (var retry = true; ; retry = false)
            {
                var zipURL = downloadFromS3
                    ? zipPath.Replace(@"skyline.gs.washington.edu", s3hostname).Replace(@"https://skyline.ms", @"https://" + s3hostname)
                        .Replace(PanoramaDomainAndPath, s3hostname)
                    : zipPath;

                try
                {
                    WebClient webClient = new WebClient();
                    using (var fs = new FileSaver(zipFilePath))
                    {
                        var timer = new Stopwatch();
                        Console.Write(@"# Downloading test data file {0}...", zipURL);
                        timer.Start();
                        webClient.DownloadFile(zipURL.Split('\\')[0],
                            fs.SafeName); // We encode a Chorus anonymous download string as two parts: url\localName
                        Console.Write(@" done. Download time {0} sec ", timer.ElapsedMilliseconds / 1000);
                        fs.Commit();
                    }
                    return zipURL;
                }
                catch (Exception x)
                {
                    message += string.Format("Could not download {0}: {1} ", zipURL, x.Message);
                    if (!retry)
                    {
                        AssertEx.Fail(message);
                    }
                    Console.Write(message);
                    downloadFromS3 = !downloadFromS3; // Maybe it just never got copied to S3 or vice versa
                }
            }
        }

        private static string GetTargetZipFilePath(string zipPath, out string zipFilePath)
        {
            var downloadsFolder = PathEx.GetDownloadsPath();
            var urlFolder = zipPath.Split('/')[zipPath.Split('/').Length - 2]; // usually "tutorial" or "PerfTest"
            var targetFolder =
                Path.Combine(downloadsFolder, char.ToUpper(urlFolder[0]) + urlFolder.Substring(1)); // "tutorial"->"Tutorial"
            var fileName = zipPath.Substring(zipPath.LastIndexOf('/') + 1);
            zipFilePath = Path.Combine(targetFolder, fileName);
            return targetFolder;
        }

        public string TestDirectoryName { get; set; }
        public TestFilesDir TestFilesDir
        {
            get
            {
                // ReSharper disable LocalizableElement
                Assert.AreEqual(1, TestFilesDirs.Length, "Attempt to use TestFilesDir on test with multiple directories.\nUse TestFilesDirs instead.");
                // ReSharper restore LocalizableElement
                return TestFilesDirs[0];
            }
            set { TestFilesDirs = new[] { value }; }
        }
        public TestFilesDir[] TestFilesDirs { get; set; }

        /// <summary>
        /// If there are any stale downloads, freshen them
        /// </summary>
        /// <returns>true if any files are shown to be stale and thus worthy of a retry of the test that uses them</returns>
        public bool FreshenTestDataDownloads() 
        {
            if (DictZipFileIsKnownCurrent == null || DictZipFileIsKnownCurrent.All(kvp => kvp.Value))
                return false;
            var knownStale = false;
            foreach (var zipPath in DictZipFileIsKnownCurrent.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToArray())
            {
                var targetFolder = GetTargetZipFilePath(zipPath, out var zipFilePath);
                var zipFilePathTest = zipFilePath + @".new";
                DownloadZipFile(targetFolder, zipPath, zipFilePathTest);
                if (!FileEx.AreIdenticalFiles(zipFilePath, zipFilePathTest))
                {
                    knownStale = true;
                    File.Delete(zipFilePath);
                    File.Move(zipFilePathTest, zipFilePath);
                }
                else
                {
                    File.Delete(zipFilePathTest);
                }
                DictZipFileIsKnownCurrent[zipPath] = true;
            }

            return knownStale;
        }
        
        public static int CountInstances(string search, string searchSpace)
        {
            if (search.Length == 0)
                return 0;

            int count = 0;
            for (int lastIndex = searchSpace.IndexOf(search, StringComparison.Ordinal);
                lastIndex != -1;
                lastIndex = searchSpace.IndexOf(search, lastIndex + 1, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        public static int CountErrors(string searchSpace, bool allowUnlocalized = false)
        {
            const string enError = "Error";
            string localError = Resources.CommandLineTest_ConsoleAddFastaTest_Error;
            int count = CountInstances(localError, searchSpace);
            if (allowUnlocalized && !Equals(localError, enError))
                count += CountInstances(enError, searchSpace);
            return count;
        }

        /// <summary>
        /// Called by the unit test framework when a test begins.
        /// </summary>
        [TestInitialize]
        public void MyTestInitialize()
        {
            Program.UnitTest = true;
            Program.TestName = TestContext.TestName;

            // Stop profiler if we are profiling.  The unit test will start profiling explicitly when it wants to.
            DotTraceProfile.Stop(true);

            SecurityProtocolInitializer.Initialize(); // Enable maximum available HTTPS security level

//            var log = new Log<AbstractUnitTest>();
//            log.Info(TestContext.TestName + " started");

            Settings.Init();

            // DesiredCleanupLevel is set in TestRunner, but we need it to work for VS Test also for code
            // coverage builds
            if (IsMsTestRun)
            {
                var isTeamCity = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"TEAMCITY_VERSION"));
                if (isTeamCity)
                    DesiredCleanupLevel = DesiredCleanupLevel.all;
            }
            STOPWATCH.Restart();
            Initialize();
        }

        /// <summary>
        /// Called by the unit test framework when a test is finished.
        /// </summary>
        [TestCleanup]
        public void MyTestCleanup()
        {
            Cleanup();
            CleanupFiles();

            STOPWATCH.Stop();

            Settings.Release();

            // Save profile snapshot if we are profiling.
            DotTraceProfile.Save();
            Settings.Init();

//            var log = new Log<AbstractUnitTest>();
//            log.Info(
//                string.Format(TestContext.TestName + " finished in {0:0.000} sec.\r\n-----------------------",
//                STOPWATCH.ElapsedMilliseconds / 1000.0));

            // Prevent any weird interactions between tests on reused processes
            Program.UnitTest = Program.FunctionalTest = false;
            Program.TestName = null;

        }

        public bool IsParallelClient => TestContext.Properties.Contains("ParallelClientId");

        private void CleanupFiles()
        {
            // If test passed, dispose the working directories to make sure file handles are not still open.
            // Note: Normally this has no impact on the directory contents, because the directory is
            // simply renamed and then renamed back. If the rename fails, the directory gets
            // deleted to raise a useful error about what is locked.
            // In a failure case, files may still be open otherwise, and trying this could
            // mask the original error.
            if (TestFilesDirs != null && TestContext.CurrentTestOutcome == UnitTestOutcome.Passed)
            {
                CleanupPersistentDir();

                foreach (var dir in TestFilesDirs.Where(d => d != null))
                {
                    dir.Cleanup();
                }
            }

            // We normally persist downloaded data files, along with selected large expensive-to-extract files
            // contained therein because this is highly beneficial for cases that run tests multiple times,
            // like nightly test runs or on a developer machine where the same downloaded files can be
            // used for months on end, and even in cases where the computer is disconnected from a network.
            // In a single run case on a pristine VM (like TeamCity), however, removing them greatly reduces
            // the disk space required to run the tests.
            if (_testFilesZips != null && DesiredCleanupLevel > DesiredCleanupLevel.persistent_files)
            {
                // Only remove files from the Downloads folder
                string downloadFolder = PathEx.GetDownloadsPath();
                foreach (var zipFilePath in _testFilesZips.Where(p => p.StartsWith(downloadFolder)))
                {
                    FileEx.SafeDelete(zipFilePath, true);
                }
            }

            // Audit logging can create this folder with no other association
            TestFilesDir.CheckForFileLocks(TestContext.GetTestResultsPath(),
                DesiredCleanupLevel == DesiredCleanupLevel.all);
        }

        protected virtual void Initialize() {}
        protected virtual void Cleanup() {}

        protected bool IsProfiling
        {
            get { return DotTraceProfile.IsProfiling; }
        }

        /// <summary>
        /// Used by tests that just take much too long under code coverage analysis
        /// </summary>
        /// <returns>true iff ReSharper code analysis is detected</returns>
        public static bool SkipForResharperAnalysis()
        {
            if (TryHelper.RunningResharperAnalysis)
            {
                Console.Write(MSG_SKIPPING_SLOW_RESHARPER_ANALYSIS_TEST); // Log this via console for TestRunner
                return true;
            }
            return false;
        }

        /// <summary>
        /// Used by tests that convert proteomic data sets to small molecule for extra coverage - which we don't always want
        /// </summary>
        /// <returns>true iff we don't want the small molecule versions of tests</returns>
        public bool SkipSmallMoleculeTestVersions()
        {
            if (!RunSmallMoleculeTestVersions)
            {
                Console.Write(MSG_SKIPPING_SMALLMOLECULE_TEST_VERSION); // Log this via console for TestRunner
                return true;
            }
            return false;
        }
    }
}
