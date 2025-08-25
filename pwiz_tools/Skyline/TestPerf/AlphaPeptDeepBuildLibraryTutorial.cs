/*
 * Original author: David Shteynberg <dshteynberg .at. gmail.com >
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class AlphaPeptDeepBuildLibraryTutorial : AbstractFunctionalTestEx
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestAlphaPeptDeepBuildLibraryTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;
            IsAutoScreenShotMode = true;
            //IsCoverShotMode = true;
            //RunPerfTests = true;
            CoverShotName = "AlphaPeptDeepBuildLibrary";

            if (IsRecordMode)
            {
                TestContext.EnsureTestResultsDir();
            }

            if (IsCleanPythonMode)
                AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());

            string currentDirectory = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(TESTDATA_DIR);
            TestFilesZipPaths = new[]
            {
                GetPerfTestDataURL($@"{TESTDATA_FILE}")
            };

            FreshenTestDataDownloads();

            var originalInstallationState = PythonInstaller.SimulatedInstallationState;
            try
            {
                PythonInstaller.SimulatedInstallationState = PythonInstaller.eSimulatedInstallationState.NONVIDIAHARD;
                RunFunctionalTest();
            }
            finally
            {
                PythonInstaller.SimulatedInstallationState = originalInstallationState;
            }
        }
        

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        protected override void DoTest()
        {
            TestAlphaPeptDeepBuildLibrary();

            if (IsCleanPythonMode)
                AssertEx.IsTrue(PythonInstaller.DeleteToolsPythonDirectory());
        }
        
        private bool IsCleanPythonMode => true;

        /// <summary>
        /// Change to true to write new Assert statements instead of testing them.
        /// </summary>
        protected override bool IsRecordMode => false;

        private string LibraryName => @"AlphaPeptDeepLibraryTutorial";
        private string LibraryPath => TestContext.GetTestPath(@"TestAlphaPeptDeepBuildLibrary\AlphaPeptDeepLibraryTutorial.blib");

        private const string TESTDATA_FILE = @"CarafeBuildLibraryTestSmall.zip";
        private const string TESTDATA_DIR = @"TestPerf";
        private string SkyTestFile => TestFilesDir.GetTestPath(@"Lumos_8mz_staggered_reCID_human_small\Lumos_8mz_staggered_reCID_human.sky");

        private void TestAlphaPeptDeepBuildLibrary()
        {
            //RunUI(SkylineWindow.NewDocument);
            OpenDocument(TestFilesDir.GetTestPath(SkyTestFile));

            PauseForScreenShot<SkylineWindow>("Skyline - Document loaded");


            // Set standard type to None
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library);

            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Library tab");

            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(() =>
            {
                peptideSettingsUI.ShowBuildLibraryDlg();
            });

            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath) ?? string.Empty);
            // We're on the "Name" page of the wizard.
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = LibraryName;
                buildLibraryDlg.LibraryPath = LibraryPath;
                buildLibraryDlg.AlphaPeptDeep = true;
                buildLibraryDlg.IrtStandard = IrtStandard.BIOGNOSYS_11;
            });

            PauseForScreenShot<BuildLibraryDlg>("Build Library - Select Library Name and Data source");
            RunUI(() =>
            {
                buildLibraryDlg.OkWizardPage();
            });

            if (IsCleanPythonMode || PythonInstaller.SimulatedInstallationState == PythonInstaller.eSimulatedInstallationState.NAIVE)
            {
                var installPythonDlg = ShowDialog<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage);
                // Expect the offer to install Python
                PauseForScreenShot<MultiButtonMsgDlg>("Build Library - Install Python");

                var pythonSuccessDlg = ShowDialog<MessageDlg>(installPythonDlg.OkDialog, WAIT_TIME * 4);
                PauseForScreenShot<MessageDlg>("Build Library - Install Python success");
                OkDialog(pythonSuccessDlg, pythonSuccessDlg.OkDialog);
            }
            else
            {
                RunUI(() => { buildLibraryDlg.OkWizardPage(); });
            }

            var addRtStdDlg = WaitForOpenForm<AddIrtPeptidesDlg>(WAIT_TIME * 10);
            PauseForScreenShot<AddIrtPeptidesDlg>("Add iRT Peptides");
            OkDialog(addRtStdDlg, addRtStdDlg.OkDialog);
            var recalibrateIrtDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            StringAssert.StartsWith(recalibrateIrtDlg.Message,
                Resources
                    .LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_);
            PauseForScreenShot<MultiButtonMsgDlg>("Skyline - Recalibrate iRT");
            OkDialog(recalibrateIrtDlg, recalibrateIrtDlg.ClickNo);
            var addRtPredDlg = WaitForOpenForm<AddRetentionTimePredictorDlg>();
            PauseForScreenShot<AddRetentionTimePredictorDlg>("Skyline - Recalibrate iRT");
            OkDialog(addRtPredDlg, addRtPredDlg.OkDialog);

            var AlphaPeptDeepLibraryBuilder = (AlphapeptdeepLibraryBuilder)buildLibraryDlg.Builder;
            var builtLibraryPath = AlphaPeptDeepLibraryBuilder.OutputSpectraLibFilepath;
            WaitForCondition(() => File.Exists(builtLibraryPath));
            WaitForCondition(() =>
            {
                try
                {
                    using (FileStream fs = File.Open(builtLibraryPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        return true; // File is accessible and not locked
                    }
                }
                catch (Exception)
                {
                    // ignored
                }

                return false;
            });
            WaitForCondition(() => peptideSettingsUI.AvailableLibraries.Length == 2);

            RunUI(() =>
            {
                peptideSettingsUI.PickedLibraries = new [] {LibraryName};
                peptideSettingsUI.SetSelectedLibrary(LibraryName);
            });

            var peptideSettingsChangedDlg = ShowDialog<MultiButtonMsgDlg>(() =>peptideSettingsUI.ShowViewLibraryDlg());
            PauseForScreenShot<MultiButtonMsgDlg>("Skyline - Peptide settings changed");

            OkDialog(peptideSettingsChangedDlg, peptideSettingsChangedDlg.ClickNo);

            var viewLibraryDlg = WaitForOpenForm<ViewLibraryDlg>(WAIT_TIME * 10);

            RunUI(() =>
            {
                viewLibraryDlg.BtnBIons.Checked = true;
                viewLibraryDlg.Charge2Button.Checked = true;
            });

            PauseForScreenShot<ViewLibraryDlg>("Library Explorer");

            RunUI(() =>
            {
                viewLibraryDlg.Close();
                peptideSettingsUI.Close();
                SkylineWindow.SaveDocument();
            });
        }

        private void RefreshGraphs()
        {
            WaitForGraphs();
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Parent);
            WaitForGraphs();
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Nodes[0]);
            WaitForGraphs();
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(documentFile)));
        }

        internal class FilterTimeMessageLock : SearchControl.IProgressLock
        {
            public int? LockLineCount => null;
            public string FilterMessage(string message)
            {
                return !message.Contains("time") ? message : null;
            }
        }

        internal class FixedLineCountLock : SearchControl.IProgressLock
        {
            public FixedLineCountLock(int lockLineCount)
            {
                LockLineCount = lockLineCount;
            }

            public int? LockLineCount { get; }
            public string FilterMessage(string message)
            {
                return message;
            }
        }
    }
}
