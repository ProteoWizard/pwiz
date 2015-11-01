/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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


using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify measured drift time derviation against a curated set of drift times
    /// </summary>
    [TestClass]
    public class MeasuredDriftTimesPerfTest : AbstractFunctionalTest
    {

        [TestMethod] 
        public void MeasuredDriftValuesPerfTest()
        {
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfMeauredDriftTimes.zip";
            TestFilesPersistent = new[] { "BSA_Frag_100nM_18May15_Fir_15-04-02.d", "Yeast_0pt1ug_BSA_50nM_18May15_Fir_15-04-01.d" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
            
        }

        protected override void DoTest()
        {
            string skyFile = TestFilesDir.GetTestPath("test_measured_drift_times_perf.sky");
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir; // So we don't have to reload the raw files, which have moved relative to skyd file 
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            var document = WaitForDocumentLoaded(240000);  // If it decides to remake chromatograms this can take awhile
            AssertEx.IsDocumentState(document, null, 1, 34, 38, 398);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("local.sky")); // Avoid "document changed since last edit" message
                document = SkylineWindow.DocumentUI;
            });

            var curatedDTs = document.Settings.PeptideSettings.Prediction.DriftTimePredictor.MeasuredDriftTimePeptides;
            var measuredDTs = new List<IDictionary<LibKey, DriftTimeInfo>>();
            var precursors = document.MoleculePrecursorPairs.Select(p => new LibKey(p.NodePep.RawTextId, p.NodeGroup.PrecursorCharge)).ToArray();
            for (var pass = 0; pass < 2; pass++)
            {
                // Verify ability to extract predictions from raw data
                var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));

                // Simulate user picking Edit Current from the Drift Time Predictor combo control
                var driftTimePredictorDlg = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg.EditDriftTimePredictor);
                RunUI(() =>
                {
                    driftTimePredictorDlg.GetDriftTimesFromResults();
                    driftTimePredictorDlg.OkDialog();
                });
                WaitForClosedForm(driftTimePredictorDlg);
                RunUI(() =>
                {
                    peptideSettingsDlg.OkDialog();
                });
                WaitForClosedForm(peptideSettingsDlg);
                
                document = SkylineWindow.Document;
                measuredDTs.Add(document.Settings.PeptideSettings.Prediction.DriftTimePredictor.MeasuredDriftTimePeptides);
                var count = 0;
                foreach (var key in curatedDTs.Keys)
                {
                    if (precursors.Contains(key))
                    {
                        count++;
                        Assert.AreNotEqual(curatedDTs[key].DriftTimeMsec(false).Value, measuredDTs[pass][key].DriftTimeMsec(false).Value, "measured drift time should differ somewhat for "+key);
                    }
                    Assert.AreEqual(curatedDTs[key].DriftTimeMsec(false).Value, measuredDTs[pass][key].DriftTimeMsec(false).Value, 1.0, "measured drift time differs too much for " + key);
                    Assert.AreEqual(curatedDTs[key].HighEnergyDriftTimeOffsetMsec, measuredDTs[pass][key].HighEnergyDriftTimeOffsetMsec, 2.0, "measured drift time high energy offset differs too much for " + key);
                }
                Assert.AreEqual(document.MoleculeTransitionGroupCount, count, "did not find drift times for all precursors"); // Expect to find a value for each precursor

                if (pass == 1)
                    break;

                // Verify that we select based on strongest results by removing the training set and relying on the other noisier set
                RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
                {
                    var chromatograms = document.Settings.MeasuredResults.Chromatograms;
                    var chromTraining = chromatograms[0];
                    var chroms = new[] { chromTraining };
                    dlg.SelectedChromatograms = chroms; // Passing this chromatograms[0] results in the other set being deleted - compiler problem?
                    dlg.RemoveReplicates();
                    dlg.OkDialog();
                });

                document = WaitForDocumentChange(document);
                Assert.AreEqual(1, document.Settings.MeasuredResults.Chromatograms.Count);
            }
            // Results should be slightly different without the training set of chromatograms to contain a potentially stronger peak
            var ccount = 0;
            var noChange = new List<LibKey>();
            foreach (var key in measuredDTs[0].Keys)
            {
                if (precursors.Contains(key))
                {
                    ccount++;
                    if (measuredDTs[0][key].DriftTimeMsec(true).Value == measuredDTs[1][key].DriftTimeMsec(true).Value)
                        noChange.Add(key);
                }
                Assert.AreEqual(measuredDTs[0][key].DriftTimeMsec(false).Value, measuredDTs[1][key].DriftTimeMsec(false).Value, 1.0, "averaged measured drift time differs for " + key);
                Assert.AreEqual(measuredDTs[0][key].HighEnergyDriftTimeOffsetMsec, measuredDTs[1][key].HighEnergyDriftTimeOffsetMsec, 2.0, "averaged measured drift time high energy offset differs for " + key);
            }
            Assert.AreEqual(document.MoleculeTransitionGroupCount, ccount, "did not find drift times for all precursors"); // Expect to find a value for each precursor
            Assert.IsTrue(noChange.Count < ccount/2,"expected most values to shift a little without the nice clean training data");
        }  
    }
}
