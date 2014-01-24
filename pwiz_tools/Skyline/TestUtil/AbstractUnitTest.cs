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
using System.IO;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                        string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string downloadsFolder = Path.Combine(Path.GetDirectoryName(desktopFolder) ?? String.Empty, "Downloads");
                        string urlFolder = zipPath.Split('/')[zipPath.Split('/').Length - 2]; // usually "tutorial" or "PerfTest"
                        string targetFolder = Path.Combine(downloadsFolder, char.ToUpper(urlFolder[0]) + urlFolder.Substring(1)); // "tutorial"->"Tutorial"
                        string fileName = zipPath.Substring(zipPath.LastIndexOf('/') + 1); // Not L10N
                        string zipFilePath = Path.Combine(targetFolder, fileName);
                        if (!File.Exists(zipFilePath))
                        {
                            if (!Directory.Exists(targetFolder))
                                Directory.CreateDirectory(targetFolder);

                            WebClient webClient = new WebClient();
                            webClient.DownloadFile(zipPath, zipFilePath);
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

            var log = new Log<AbstractUnitTest>();
            log.Info(TestContext.TestName + " started");

            Settings.Default.Reset();

            STOPWATCH.Restart();
        }

        /// <summary>
        /// Called by the unit test framework when a test is finished.
        /// </summary>
        [TestCleanup]
        public void MyTestCleanup()
        {
            STOPWATCH.Stop();

            // Save profile snapshot if we are profiling.
            DotTraceProfile.Save();

            var log = new Log<AbstractUnitTest>();
            log.Info(
                string.Format(TestContext.TestName + " finished in {0:0.000} sec.\r\n-----------------------",
                STOPWATCH.ElapsedMilliseconds / 1000.0));
        }

        protected bool IsProfiling
        {
            get { return DotTraceProfile.IsProfiling; }
        }
    }
}
