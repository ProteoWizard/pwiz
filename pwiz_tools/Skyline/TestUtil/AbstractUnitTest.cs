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

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
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

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBeProtected.Global
        public TestContext TestContext { get; set; }
// ReSharper restore MemberCanBeProtected.Global
// ReSharper restore UnusedAutoPropertyAccessor.Global

        protected bool AllowInternetAccess
        {
            get { return GetBoolValue("AccessInternet", false); }  // Return false if unspecified
        }

        protected bool RunPerfTests
        {
            get { return GetBoolValue("RunPerfTests", false); }  // Return false if unspecified
            set { TestContext.Properties["RunPerfTests"] = value ? "true" : "false";  }
        }

        private bool? _testSmallMolecules;
        public bool TestSmallMolecules
        {
            get
            {
                if (_testSmallMolecules.HasValue)
                {
                    if (Settings.Default.TestSmallMolecules != _testSmallMolecules.Value)
                        _testSmallMolecules = Settings.Default.TestSmallMolecules;  // Probably changed by IsPauseForScreenShots, honor that
                }
                else
                {
                    _testSmallMolecules = GetBoolValue("TestSmallMolecules", false); 
                    Settings.Default.TestSmallMolecules = _testSmallMolecules.Value; // Communicate this value to Skyline via Settings.Default
                }
                return _testSmallMolecules.Value;
            }
            set
            {
                // Communicate this value to Skyline via Settings.Default
                Settings.Default.TestSmallMolecules = (_testSmallMolecules = value).Value;
            }
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
                Assert.AreEqual(1, _testFilesZips.Length, "Attempt to use TestFilesZip on test with multiple ZIP files.\nUse TestFilesZipPaths instead."); // Not L10N
                return _testFilesZips[0];
            }
            set { TestFilesZipPaths = new[] { value }; }
        }

        /// <summary>
        /// Optional list of files to be retained from run to run. Useful for really
        /// large data files which are expensive to extract and keep as local copies.
        /// </summary>
        public string[] TestFilesPersistent { get; set; }

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
                    if (zipPath.Substring(0, 8).ToLower().Equals("https://") || zipPath.Substring(0, 7).ToLower().Equals("http://")) // Not L10N
                    {
                        string downloadsFolder = PathEx.GetDownloadsPath();
                        string urlFolder = zipPath.Split('/')[zipPath.Split('/').Length - 2]; // usually "tutorial" or "PerfTest"
                        string targetFolder = Path.Combine(downloadsFolder, char.ToUpper(urlFolder[0]) + urlFolder.Substring(1)); // "tutorial"->"Tutorial"
                        string fileName = zipPath.Substring(zipPath.LastIndexOf('/') + 1); // Not L10N
                        string zipFilePath = Path.Combine(targetFolder, fileName);
                        if (!File.Exists(zipFilePath) &&
                           (!IsPerfTest || RunPerfTests)) // If this is a perf test, skip download unless perf tests are enabled

                        {
                            if (!Directory.Exists(targetFolder))
                                Directory.CreateDirectory(targetFolder);

                            WebClient webClient = new WebClient();
                            using (var fs = new FileSaver(zipFilePath))
                            {
                                webClient.DownloadFile(zipPath, fs.SafeName);
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
                Assert.AreEqual(1, TestFilesDirs.Length, "Attempt to use TestFilesDir on test with multiple directories.\nUse TestFilesDirs instead."); // Not L10N
                return TestFilesDirs[0];
            }
            set { TestFilesDirs = new[] { value }; }
        }
        public TestFilesDir[] TestFilesDirs { get; set; }

        /// <summary>
        /// Called by the unit test framework when a test begins.
        /// </summary>
        [TestInitialize]
        public void MyTestInitialize()
        {
            // Stop profiler if we are profiling.  The unit test will start profiling explicitly when it wants to.
            DotTraceProfile.Stop(true);

//            var log = new Log<AbstractUnitTest>();
//            log.Info(TestContext.TestName + " started");

            Settings.Init();

            // ReSharper disable once UnusedVariable
            var dummy = TestSmallMolecules; // First access turns on the non-proteomic test node behavior if needed

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
