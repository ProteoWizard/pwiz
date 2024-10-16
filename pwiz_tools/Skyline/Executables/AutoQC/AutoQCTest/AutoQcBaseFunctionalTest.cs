using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.GUI;
using SharedBatch;
using SharedBatchTest;

namespace AutoQCTest
{
    public abstract class AutoQcBaseFunctionalTest : AbstractBaseFunctionalTest
    {
        public string TestFolder;  // Where the test runs
        public MainForm MainForm;

        [DeploymentItem(@"..\AutoQC\FileAcquisitionTime.skyr")]
        [DeploymentItem(@"..\AutoQC\SkylineRunner.exe")]
        [DeploymentItem(@"..\AutoQC\SkylineDailyRunner.exe")]
        protected override void DoTest()
        {
            TestFolder = TestFilesDirs[0].FullPath;

            MainForm = MainFormWindow() as MainForm;
            Assert.IsNotNull(MainForm, "Main program window is not an instance of MainForm.");
            WaitForShownForm(MainForm);

            Assert.AreEqual(0, MainForm.ConfigCount());
        }

        protected override Form MainFormWindow()
        {
            return Program.MainWindow;
        }

        protected override void ResetSettings()
        {
            SharedBatch.Properties.Settings.Default.Reset();
            AutoQC.Properties.Settings.Default.Reset();
        }

        protected override void InitProgram()
        {
            // throw new NotImplementedException();
        }

        protected override void StartProgram()
        {
            Program.Main(Array.Empty<string>());
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

        public int StartConfig(MainForm mainForm, string configName)
        {
            var configIndex = mainForm.GetConfigIndex(configName);
            Assert.IsFalse(configIndex == -1, $"Could not find an index for configuration '{configName}'.");
            RunUI(() =>
            {
                mainForm.ClickConfig(configIndex);
                mainForm.StartConfig(configIndex);
            });
            WaitForConfigState(mainForm, configIndex, RunnerStatus.Running);
            return configIndex;
        }

        public void StopConfig(MainForm mainForm, int configIndex, string configName)
        {
            RunUI(() =>
            {
                mainForm.ClickConfig(configIndex);
            });
            RunDlg<CommonAlertDlg>(() => mainForm.StopConfig(configIndex),
                dlg =>
                {
                    Assert.AreEqual(string.Format(
                        AutoQC.Properties.Resources
                            .AutoQcConfigManager_StopConfiguration_Are_you_sure_you_want_to_stop_the_configuration___0___,
                        configName), dlg.Message);
                    dlg.ClickYes();
                });

            WaitForConfigState(mainForm, configIndex, RunnerStatus.Stopped);
            RunUI(() => Assert.IsFalse(mainForm.IsConfigEnabled(configIndex)));
        }

        public void WaitForConfigState(MainForm mainForm, int configIndex, RunnerStatus waitForStatus)
        {
            if (!WaitForCondition(mainForm, configIndex, cRunner => cRunner.GetStatus() == waitForStatus, 
                    out var timeout, out var configRunner))
            {
                var status = configRunner?.GetStatus().ToString() ?? "Unknown";
                Assert.Fail(@"Timeout {0} seconds exceeded in WaitForConfigState. Expected config status: {1}. Found status: {2}",
                    timeout, waitForStatus, status);
            }
        }

        public void WaitForConfigRunnerWaiting(MainForm mainForm, int configIndex)
        {
            if (!WaitForCondition(mainForm, configIndex,
                configRunner => configRunner.Waiting, out var timeout, out _))
            {
                Assert.Fail(@"Timeout {0} seconds exceeded in WaitForConfigRunnerWaiting.", timeout);
            }
        }

        public bool WaitForCondition(MainForm mainForm, int configIndex, Func<ConfigRunner, bool> condition, 
            out int timeout, out ConfigRunner configRunner)
        {
            var waitCycles = GetWaitCycles();
            timeout = waitCycles * SLEEP_INTERVAL / 1000;
            
            for (var i = 0; i < waitCycles; i++)
            {
                Assert.IsFalse(GetTestExceptions().Any(), "Exception while running test");

                configRunner = mainForm.GetConfigRunner(configIndex);
                Assert.IsNotNull(configRunner);

                if (condition(configRunner)) return true; // Condition satisfied

                Thread.Sleep(SLEEP_INTERVAL); // Wait before the next check
            }
            configRunner = mainForm.GetConfigRunner(configIndex);
            return false;
        }

        public void WaitForAnnotationsFileUpdated(MainForm mainForm, int configIndex)
        {
            if (!WaitForCondition(mainForm, configIndex,
                    cRunner => cRunner.AnnotationsFileUpdated, out var timeout, out _))
            {
                Assert.Fail(@"Timeout {0} seconds exceeded in WaitForAnnotationsFileImported. ", timeout);
            }
        }

        public string CreateRawDataDir()
        {
            var rawDataDir = Path.Combine(TestFolder, "RawData");
            if (Directory.Exists(rawDataDir))
            {
                Directory.Delete(rawDataDir, true);
            }

            Assert.IsFalse(Directory.Exists(rawDataDir));
            Directory.CreateDirectory(rawDataDir);
            Assert.IsTrue(Directory.Exists(rawDataDir));
            return rawDataDir;
        }
    }
}
