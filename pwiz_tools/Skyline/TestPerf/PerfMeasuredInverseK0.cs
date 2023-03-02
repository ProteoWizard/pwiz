/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify measured ion mobility derivation and filtering with Bruker TIMS data
    /// </summary>
    [TestClass]
    public class MeasuredInverseK0PerfTest : AbstractFunctionalTestEx
    {

        private const string bsaFmolTimsInfusionesiPrecMz5Mz5 = "_BSA_50fmol_TIMS_InfusionESI_10prec_mz5.mz5";
        private const string  BSA_50fmol_TIMS_InfusionESI_10precd =  "BSA_50fmol_TIMS_InfusionESI_10prec.d";

        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)]
        public void MeasuredInverseK0ValuesPerfTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfMeasuredInverseK0_v3.zip");
            TestFilesPersistent = new[] { BSA_50fmol_TIMS_InfusionESI_10precd, bsaFmolTimsInfusionesiPrecMz5Mz5 }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
            
        }

        protected override void DoTest()
        {
            string skyFile = TestFilesDir.GetTestPath("tims_test.sky");
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir;
            var testPath = TestFilesDir.GetTestPath("local.sky");

            // Make sure that commandline loading employs 3-array IMS (should be quick if it does)
            TestCommandlineImport(skyFile, testPath);

            RunUI(() => Settings.Default.ImportResultsSimultaneousFiles = (int)MultiFileLoader.ImportResultsSimultaneousFileOptions.many); // use maximum threads for multiple file import
            RunUI(() => SkylineWindow.OpenFile(skyFile));
            ImportResults(BSA_50fmol_TIMS_InfusionESI_10precd);
            var document = WaitForDocumentLoaded(240000);  // mz5 part of this this can take awhile
            AssertEx.IsDocumentState(document, null, 1, 34, 89, 1007);
            RunUI(() =>
            {
                // Show that we can reopen a document with 3-array data in it
                SkylineWindow.SaveDocument(testPath); 
                SkylineWindow.NewDocument();
                VerifySerialization(testPath, false);
                SkylineWindow.LoadFile(testPath);
            });
            document = WaitForDocumentLoaded(240000);
            var transitions = document.MoleculeTransitions.ToArray();

            // Verify ability to extract ion mobility peaks from raw data
            var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(
                () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
            RunUI(()=>
            {
                transitionSettingsDlg.IonMobilityControl.WindowWidthType =
                    IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
                transitionSettingsDlg.IonMobilityControl.IonMobilityFilterResolvingPower = 40;
            });

            // Simulate user picking Add from the ion mobility Predictor combo control
            var editIonMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg.IonMobilityControl.AddIonMobilityLibrary);
            var testlibName = "test_tims";
            var databasePath = TestFilesDir.GetTestPath(testlibName + IonMobilityDb.EXT);
            RunUI(() =>
            {
                editIonMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(true);
                editIonMobilityLibraryDlg.LibraryName = testlibName;
                editIonMobilityLibraryDlg.CreateDatabaseFile(databasePath); // Simulate user click on Create button
                editIonMobilityLibraryDlg.GetIonMobilitiesFromResults();
            });
            // PauseTest(); // Uncomment this to inspect ion mobility finder results
            OkDialog(editIonMobilityLibraryDlg, editIonMobilityLibraryDlg.OkDialog);
            OkDialog(transitionSettingsDlg, transitionSettingsDlg.OkDialog);

            var docChangedDriftTimePredictor = WaitForDocumentChange(document);

            // Reimport data - should shift precursors
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                var chromatograms = docChangedDriftTimePredictor.Settings.MeasuredResults.Chromatograms;
                dlg.SelectedChromatograms = new[] { chromatograms[0] };
                dlg.ReimportResults();
                dlg.OkDialog();
            });

            document = WaitForDocumentChangeLoaded(docChangedDriftTimePredictor);
            var transitionsNew = document.MoleculeTransitions.ToArray();
            var nChanges = 0;
            var nNonEmpty = 0;
            for (var i = 0; i < transitions.Length; i++)
            {
                Assert.AreEqual(transitions[i].Mz, transitionsNew[i].Mz);
                if (transitions[i].AveragePeakArea.HasValue)
                {
                    nNonEmpty++;
                    if (transitions[i].AveragePeakArea != transitionsNew[i].AveragePeakArea) // Using filter should alter peak area
                    {
                        nChanges++;
                    }
                }
                else
                {
                    Assert.AreEqual(transitions[i].AveragePeakArea, transitionsNew[i].AveragePeakArea);
                }
            }
            Assert.IsTrue(nChanges >= nNonEmpty*.9); // We expect nearly all peaks to change in area with IMS filter in use

            // And read some mz5 converted from Bruker in 2-array IMS format, then compare replicates - should be identical
            var mz5 = TestFilesDir.GetTestPath(bsaFmolTimsInfusionesiPrecMz5Mz5);
            ImportResultsFile(mz5);
            document = WaitForDocumentChange(document);
            var sb = new StringBuilder();
            int trials = 0;
            int diffs = 0;
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                Assert.AreEqual(2, nodeGroup.Results.Count);
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    Assume.AreEqual(2, nodeTran.Results.Count);
                    if (nodeTran.Results[0].Any() || nodeTran.Results[1].Any())
                        trials++;
                    if (Equals(nodeTran.Results[0], nodeTran.Results[1]))
                        continue;
                    if (nodeTran.Results[0].First().Area != nodeTran.Results[1].First().Area)
                    {
                        diffs++;
                        var diff =
                            Math.Abs(nodeTran.Results[0].First().Area - nodeTran.Results[1].First().Area) /
                            Math.Min(nodeTran.Results[0].First().Area, nodeTran.Results[1].First().Area);
                        sb.AppendLine(string.Format("{0}% difference {3} vs {4}(mz5) in precursor {1} transition {2}",
                            diff, nodeGroup, nodeTran.Transition, nodeTran.Results[0].First().Area, nodeTran.Results[1].First().Area));
                    }
                    else
                    {
                        sb.AppendLine(string.Format("No area difference in precursor {0} transition {1}",
                            nodeGroup, nodeTran.Transition));
                    }
                }
            }
            if (diffs != 0)
                Assert.Fail(TextUtil.LineSeparate(string.Format("{0} differences found in peak areas between 2- and 3- array spectra", diffs), sb.ToString()));

            // Verify that the data was loaded in 3-array IMS format for .d and 2-array for .mz5 (which was converted that way on purpose) by looking at serialization
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(testPath);
            });
            VerifySerialization(testPath, true);

        }

        private void TestCommandlineImport(string skyFile, string testPath)
        {
            if (File.Exists(testPath))
                File.Delete(testPath);

            // Show anyone watching that work is being performed
            RunUI(() => {
                using (var longWait = new LongWaitDlg(SkylineWindow) { Message = "Running command-line import" })
                {
                    longWait.PerformWork(SkylineWindow, 500, () =>
                        RunCommand("--in=" + skyFile,
                            "--import-file=" + TestFilesDir.GetTestPath(BSA_50fmol_TIMS_InfusionESI_10precd),
                            "--out=" + testPath));
                }
            });
            
            VerifySerialization(testPath, false);
        }

        private void VerifySerialization(string testPath, bool expect_mz5)
        {
            var lines = File.ReadAllLines(testPath);
            VerifyFileInfoSerialization(lines, TestFilesDir.GetTestPath(BSA_50fmol_TIMS_InfusionESI_10precd), !MsDataFileImpl.ForceUncombinedIonMobility);
            if (expect_mz5)
                VerifyFileInfoSerialization(lines, TestFilesDir.GetTestPath(bsaFmolTimsInfusionesiPrecMz5Mz5), false);
        }

        private void VerifyFileInfoSerialization(string[] lines, string filePath, bool combinedIms)
        {
            var encodePath = PathEx.EscapePathForXML(SampleHelp.EncodePath(filePath, null, -1, null));
            var lineFilePath = lines.FirstOrDefault(l => l.Contains(encodePath + '"'));
            Assert.IsNotNull(lineFilePath);
            // Nothing gets serialized to the XML about whether the MsData had combined spectra - can only be found in the SKYD file
        }
    }
}
