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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Once-per-application setup information to perform logging with log4net.
[assembly: log4net.Config.XmlConfigurator(ConfigFile = "SkylineLog4Net.config", Watch = true)]

namespace SharedBatchTest
{
   
    /// <summary>
    /// This is the base class for every unit test in Skyline.  It enables logging
    /// and also provides quick information about the running time of the test.
    /// </summary>
    [TestClass]
    [DeploymentItem("SkylineLog4Net.config")]
    public class AbstractUnitTest
    {

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

        protected bool RetryDataDownloads
        {
            get { return GetBoolValue("RetryDataDownloads", false); }  // When true, re-download data sets on test failure in case it's due to stale data
            set { TestContext.Properties["RetryDataDownloads"] = value ? "true" : "false"; }
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

        public static string GetPerfTestDataURL(string filename)
        {
            return @"https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/" + filename;
        }

        protected bool GetBoolValue(string property, bool defaultValue)
        {
            var value = TestContext.Properties[property];
            return (value == null) ? defaultValue :
                string.Compare(value.ToString(), "true", true, CultureInfo.InvariantCulture) == 0;
        }


        /*public string TestFilesZip
        {
            get
            {
                // ReSharper disable LocalizableElement
                Assert.AreEqual(1, _testFilesZips.Length, "Attempt to use TestFilesZip on test with multiple ZIP files.\nUse TestFilesZipPaths instead.");
                // ReSharper restore LocalizableElement
                return _testFilesZips[0];
            }
            set { TestFilesZipPaths = new[] { value }; }
        }*/

        /// <summary>
        /// Optional list of files to be retained from run to run. Useful for really
        /// large data files which are expensive to extract and keep as local copies.
        /// </summary>
        public string[] TestFilesPersistent { get; set; }

        /*/// <summary>
        /// Tracks which zip files were downloaded this run, and which might possibly be stale
        /// </summary>
        public Dictionary<string, bool> DictZipFileIsKnownCurrent { get; private set; }*/

        /// <summary>
        /// One bool per TestFilesZipPaths indicating whether to unzip in the root directory (true) or a sub-directory (false or null)
        /// </summary>
        public bool[] TestFilesZipExtractHere { get; set; }

        public bool IsExtractHere(int zipPathIndex)
        {
            return TestFilesZipExtractHere != null && TestFilesZipExtractHere[zipPathIndex];
        }
    }
}
