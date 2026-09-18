using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using SharedBatch;
using SkylineBatch;
using SharedBatchTest;
using SkylineBatch.Properties;

namespace SkylineBatchTest
{
    /// <summary>
    /// All functional tests MUST derive from this base class.
    /// Inherits from AbstractSkylineBatchUnitTest to provide TestContext and helper methods.
    /// </summary>
    public abstract class AbstractSkylineBatchFunctionalTest : AbstractBaseFunctionalTest
    {
        // Helper: test-specific results path (uses TestUtils for backward compatibility)
        protected string GetTestResultsPath(string relativePath = null)
        {
            return TestUtils.GetTestResultsPath(TestContext, relativePath);
        }

        // Helper: logger rooted in TestResults (returns SkylineBatch Logger instance)
        protected Logger GetTestLogger(string logSubfolder = "")
        {
            return TestUtils.GetTestLogger(TestContext, logSubfolder);
        }

        public const string SKYLINE_BATCH_FOLDER = @"Executables\SkylineBatch\";


        public new string TestFilesZip
        {
            get => base.TestFilesZip;
            set => base.TestFilesZip = SKYLINE_BATCH_FOLDER + value;
        }

        public new string[] TestFilesZipPaths
        {
            get => base.TestFilesZipPaths;
            set
            {
                var testFilesZipPaths = value;
                for (int i = 0; i < testFilesZipPaths.Length; i++)
                    testFilesZipPaths[i] = SKYLINE_BATCH_FOLDER + testFilesZipPaths[i];
                base.TestFilesZipPaths = testFilesZipPaths;
            }
        }


        protected override Form MainFormWindow()
        {
            return Program.MainWindow;
        }

        protected override void ResetSettings()
        {
            Settings.Default.Reset();

            // SkylineBatch's Settings.Reset() also resets SharedBatch's, which wipes the Skyline
            // installation paths FindSkyline() discovered - SkylineLocalCommandPath,
            // SkylineAdminCmdPath, SkylineRunnerPath. Nothing re-discovers them, so an imported
            // configuration is typed from its XML instead of being retyped Local, and validates
            // against a CmdPath of null: "Could not find a Skyline installation on this computer".
            // Re-running discovery restores the state the application has after its own startup.
            SkylineInstallations.FindSkyline();
        }

        protected override void InitProgram()
        {
        }

        [TestCleanup]
        public void CleanupHttpTestBehavior()
        {
            // Defensive: a test that installed an HttpClientWithProgress.TestBehavior (e.g.
            // RemoteFileSourceFunctionalTest's Panorama mock) must not leak it to later tests.
            HttpClientWithProgress.TestBehavior = null;
        }

        protected override void StartProgram()
        {
            Program.TestDirectory = Path.GetDirectoryName(TestFilesDirs[0].FullPath);
            Program.Main(new string[0]);
        }

        protected override void InitTestExceptions()
        {
            Program.TestExceptions = new List<Exception>();
        }

        protected override void AddTestException(Exception exception)
        {
            Program.AddTestException(exception);
        }

        protected override List<Exception> GetTestExceptions()
        {
            return Program.TestExceptions;
        }

        protected override void SetFunctionalTest()
        {
            Program.FunctionalTest = true;
        }
    }
}