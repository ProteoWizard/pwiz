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


using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify measured ion mobility derviation and filtering with Bruker TIMS data
    /// </summary>
    [TestClass]
    public class MeasuredInverseK0PerfTest : AbstractFunctionalTestEx
    {

        private const string bsaFmolTimsInfusionesiPrecMz5Mz5 = "BSA_50fmol_TIMS_InfusionESI_10prec_mz5.mz5";

        [TestMethod] 
        public void MeasuredInverseK0ValuesPerfTest()
        {
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfMeasuredInverseK0.zip";
            TestFilesPersistent = new[] { "BSA_50fmol_TIMS_InfusionESI_10prec.d", bsaFmolTimsInfusionesiPrecMz5Mz5 }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
            
        }

        protected override void DoTest()
        {
            // Load a document with results loaded without drift filter
            string skyFile = TestFilesDir.GetTestPath("tims_test.sky");
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir; // So we don't have to reload the raw files, which have moved relative to skyd file 
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            var document = WaitForDocumentLoaded(240000);  // If it decides to remake chromatograms this can take awhile
            AssertEx.IsDocumentState(document, null, 1, 34, 89, 1007);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("local.sky")); // Avoid "document changed since last edit" message
                document = SkylineWindow.DocumentUI;
            });
            var transitions = document.MoleculeTransitions.ToArray();

            // Verify ability to extract predictions from raw data
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(
                () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));

            // Simulate user picking Add from the ion mobility Predictor combo control
            var driftTimePredictorDlg = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg.AddDriftTimePredictor);
            RunUI(() =>
            {
                driftTimePredictorDlg.SetOffsetHighEnergySpectraCheckbox(true);
                driftTimePredictorDlg.SetPredictorName("test_tims");
                driftTimePredictorDlg.SetResolvingPower(40);
                driftTimePredictorDlg.GetDriftTimesFromResults();
                driftTimePredictorDlg.OkDialog(true); // Force overwrite if a named predictor already exists
            });
            WaitForClosedForm(driftTimePredictorDlg);
            RunUI(() =>
            {
                peptideSettingsDlg.OkDialog();
            });
            WaitForClosedForm(peptideSettingsDlg);

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
                Assume.AreEqual(transitions[i].Mz, transitionsNew[i].Mz);
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
                    Assume.AreEqual(transitions[i].AveragePeakArea, transitionsNew[i].AveragePeakArea);
                }
            }
            Assume.IsTrue(nChanges >= nNonEmpty*.9); // We expect nearly all peaks to change in area with IMS filter in use

            // And read some mz5 converted from Bruker, then compare replicates - should be identical
            var mz5 = TestFilesDir.GetTestPath(bsaFmolTimsInfusionesiPrecMz5Mz5);
            ImportResultsFile(mz5);
            document = WaitForDocumentChange(document);
            foreach (var nodeGroup in document.MoleculeTransitionGroups)
            {
                Assume.AreEqual(2, nodeGroup.Results.Count);
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    Assume.AreEqual(2, nodeTran.Results.Count);
                    Assume.AreEqual(nodeTran.Results[0], nodeTran.Results[1]);
                }
            }
        }  
    }
}
