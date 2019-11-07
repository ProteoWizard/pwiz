/*
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

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

// Once-per-application setup information to perform logging with log4net.
[assembly: log4net.Config.XmlConfigurator(ConfigFile = "SkylineLog4Net.config", Watch = true)]

namespace pwiz.SkylineTestUtil
{
   
    /// <summary>
    /// This is the base class for every unit test in Skyline.  It enables logging
    /// and also provides quick information about the running time of the test.
    /// </summary>
    [TestClass]
    [DeploymentItem("SkylineLog4Net.config")]
    public class AbstractUnitTest
    {
        private static readonly Stopwatch STOPWATCH = new Stopwatch();

        // NB this text needs to agree with that in UpdateRun() in pwiz_tools\Skyline\SkylineTester\TabQuality.cs
        public const string MSG_SKIPPING_SMALLMOLECULE_TEST_VERSION = " (RunSmallMoleculeTestVersions=False, skipping.) ";

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBeProtected.Global
        public TestContext TestContext { get; set; }
// ReSharper restore MemberCanBeProtected.Global
// ReSharper restore UnusedAutoPropertyAccessor.Global

        protected bool AllowInternetAccess
        {
            get { return GetBoolValue("AccessInternet", false); }  // Return false if unspecified
            set { TestContext.Properties["AccessInternet"] = value ? "true" : "false"; } // Only appropriate to use in perf tests, really
        }

        protected bool RunPerfTests
        {
            get { return GetBoolValue("RunPerfTests", false); }  // Return false if unspecified
            set { TestContext.Properties["RunPerfTests"] = value ? "true" : "false"; }
        }

        /// <summary>
        /// Determines whether or not to (re)record audit logs for tests.
        /// </summary>
        protected bool RecordAuditLogs
        {
            get { return GetBoolValue("RecordAuditLogs", false); }  // Return false if unspecified
            set { TestContext.Properties["RecordAuditLogs"] = value ? "true" : "false"; }
        }

        /// <summary>
        /// This controls whether we run the various tests that are small molecule versions of our standard tests,
        /// for example DocumentExportImportTestAsSmallMolecules().  Such tests convert the entire document to small
        /// molecule representations before proceeding.
        /// Developers that want to see such tests execute within the IDE can add their machine name to the SmallMoleculeDevelopers
        /// list below (partial matches suffice, so name carefully!)
        /// </summary>
        private static string[] SmallMoleculeDevelopers = {"BSPRATT", "TOBIASR"}; 
        protected bool RunSmallMoleculeTestVersions
        {
            get { return GetBoolValue("RunSmallMoleculeTestVersions", false) || SmallMoleculeDevelopers.Any(smd => Environment.MachineName.Contains(smd)); }
            set { TestContext.Properties["RunSmallMoleculeTestVersions"] = value ? "true" : "false"; }
        }

        /// <summary>
        /// Perf tests (long running, huge-data-downloading) should be declared
        /// in the TestPerf namespace so that they can be skipped when the RunPerfTests 
        /// flag is unset.
        /// </summary>
        public bool IsPerfTest
        {
            get { return ("TestPerf".Equals(GetType().Namespace)); }
        }

        protected bool GetBoolValue(string property, bool defaultValue)
        {
            var value = TestContext.Properties[property];
            return (value == null) ? defaultValue :
                string.Compare(value.ToString(), "true", true, CultureInfo.InvariantCulture) == 0;
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
                for (int i = 0; i < zipPaths.Length; i++)
                {
                    var zipPath = zipPaths[i];
                    // If the file is on the web, save it to the local disk in the developer's
                    // Downloads folder for future use
                    if (zipPath.Substring(0, 8).ToLower().Equals(@"https://") || zipPath.Substring(0, 7).ToLower().Equals(@"http://"))
                    {
                        string downloadsFolder = PathEx.GetDownloadsPath();
                        string urlFolder = zipPath.Split('/')[zipPath.Split('/').Length - 2]; // usually "tutorial" or "PerfTest"
                        string targetFolder = Path.Combine(downloadsFolder, char.ToUpper(urlFolder[0]) + urlFolder.Substring(1)); // "tutorial"->"Tutorial"
                        string fileName = zipPath.Substring(zipPath.LastIndexOf('/') + 1);
                        string zipFilePath = Path.Combine(targetFolder, fileName);
                        if (!File.Exists(zipFilePath) &&
                           (!IsPerfTest || RunPerfTests)) // If this is a perf test, skip download unless perf tests are enabled

                        {
                            if (!Directory.Exists(targetFolder))
                                Directory.CreateDirectory(targetFolder);

                            bool downloadFromS3 = Environment.GetEnvironmentVariable("SKYLINE_DOWNLOAD_FROM_S3") == "1";
                            string s3hostname = @"skyline-perftest.s3-us-west-2.amazonaws.com";
                            if (downloadFromS3)
                                zipPath = zipPath.Replace(@"skyline.gs.washington.edu", s3hostname).Replace(@"skyline.ms", s3hostname);

                            WebClient webClient = new WebClient();
                            using (var fs = new FileSaver(zipFilePath))
                            {
                                try
                                {
                                    webClient.DownloadFile(zipPath.Split('\\')[0], fs.SafeName); // We encode a Chorus anonymous download string as two parts: url\localName
                                }
                                catch (Exception x)
                                {
                                   Assert.Fail("Could not download {0}: {1}", zipPath, x.Message);
                                }
                                fs.Commit();
                            }
                        }
                        zipPath = zipFilePath;
                    }
                    _testFilesZips[i] = zipPath;
                }
            }
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

            // Stop profiler if we are profiling.  The unit test will start profiling explicitly when it wants to.
            DotTraceProfile.Stop(true);

            SecurityProtocolInitializer.Initialize(); // Enable maximum available HTTPS security level

//            var log = new Log<AbstractUnitTest>();
//            log.Info(TestContext.TestName + " started");

            Settings.Init();

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

            // Delete unzipped test files if test otherwise passed to make sure file handles
            // are not still open. Files may still be open otherwise, and trying this could
            // mask the original error.
            if (TestFilesDirs != null && TestContext.CurrentTestOutcome == UnitTestOutcome.Passed)
            {
                foreach (TestFilesDir dir in TestFilesDirs)
                {
                    if (dir != null)
                        dir.Dispose();
                }
            }

            STOPWATCH.Stop();

            Settings.Release();

            // Save profile snapshot if we are profiling.
            DotTraceProfile.Save();

//            var log = new Log<AbstractUnitTest>();
//            log.Info(
//                string.Format(TestContext.TestName + " finished in {0:0.000} sec.\r\n-----------------------",
//                STOPWATCH.ElapsedMilliseconds / 1000.0));
        }

        protected virtual void Initialize() {}
        protected virtual void Cleanup() {}

        protected bool IsProfiling
        {
            get { return DotTraceProfile.IsProfiling; }
        }
    }
}
