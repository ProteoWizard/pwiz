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
using pwiz.Common.Chemistry;
using pwiz.Skyline;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
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

        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)] 
        public void MeasuredDriftValuesPerfTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfMeauredDriftTimes.zip");
            TestFilesPersistent = new[] { "BSA_Frag_100nM_18May15_Fir_15-04-02.d", "Yeast_0pt1ug_BSA_50nM_18May15_Fir_15-04-01.d" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
            
        }

        protected override void DoTest()
        {
            // IsPauseForScreenShots = true; // For a quick demo when you need it
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
            List<ValidatingIonMobilityPrecursor> curatedDTs = null;
            var measuredDTs = new List<List<ValidatingIonMobilityPrecursor>>();
            var precursors = new LibKeyIndex(document.MoleculePrecursorPairs.Select(
                p => p.NodePep.ModifiedTarget.GetLibKey(p.NodeGroup.PrecursorAdduct).LibraryKey));
            PauseForScreenShot(@"Legacy ion mobility values loaded, placed in .imsdb database file"); // For a quick demo when you need it
            for (var pass = 0; pass < 2; pass++)
            {
                // Verify ability to extract predictions from raw data
                var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                PauseForScreenShot("new Transition Settings tab"); // For a quick demo when you need it
                // Simulate user picking Edit Current from the Ion Mobility Library combo control
                var ionMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg.IonMobilityControl.EditIonMobilityLibrary);
                PauseForScreenShot("next, we'll update values with 'Use Results' button"); // For a quick demo when you need it
                RunUI(() =>
                {
                    if (curatedDTs == null)
                        curatedDTs = ionMobilityLibraryDlg.LibraryMobilitiesFlat.ToList();
                    ionMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(true);
                    ionMobilityLibraryDlg.GetIonMobilitiesFromResults();
                });
                PauseForScreenShot("values updated");// For a quick demo when you need it
                RunUI(() => measuredDTs.Add(ionMobilityLibraryDlg.LibraryMobilitiesFlat.ToList()));
                OkDialog(ionMobilityLibraryDlg, ionMobilityLibraryDlg.OkDialog);
                OkDialog(transitionSettingsDlg, transitionSettingsDlg.OkDialog);
                
                document = SkylineWindow.Document;
                var count = 0;
                for (var n = 0; n < curatedDTs.Count; n++)
                {
                    var cdt = curatedDTs[n];
                    var key = cdt.Precursor;
                    var indexM = measuredDTs[pass].FindIndex(m => m.Precursor.Equals(key));
                    var measured = measuredDTs[pass][indexM];
                    var measuredDT = measured.IonMobility;
                    var measuredHEO = measured.HighEnergyIonMobilityOffset;
                    if (precursors.ItemsMatching(key, true).Any())
                    {
                        count++;
                        AssertEx.AreNotEqual(cdt.IonMobility, measuredDT, "measured drift time should differ somewhat for "+measured.Precursor);
                    }

                    AssertEx.AreEqual(cdt.IonMobility, measuredDT, 1.0, "measured drift time differs too much for " + key);
                    AssertEx.AreEqual(cdt.HighEnergyIonMobilityOffset, measuredHEO, 2.0, "measured drift time high energy offset differs too much for " + key);
                }
                AssertEx.AreEqual(document.MoleculeTransitionGroupCount, count, "did not find drift times for all precursors"); // Expect to find a value for each precursor

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
                AssertEx.AreEqual(1, document.Settings.MeasuredResults.Chromatograms.Count);
            }
            // Results should be slightly different without the training set of chromatograms to contain a potentially stronger peak
            var ccount = 0;
            var noChange = new List<LibKey>();
            for (var n = 0; n < measuredDTs[0].Count; n++)
            {
                var validatingIonMobilityPeptide0 = measuredDTs[0][n];
                var validatingIonMobilityPeptide1 = measuredDTs[1][n];
                var key = measuredDTs[0][n].Precursor;
                if (precursors.ItemsMatching(key, true).Any())
                {
                    ccount++;
                    if (validatingIonMobilityPeptide0.HighEnergyIonMobilityOffset == validatingIonMobilityPeptide1.HighEnergyIonMobilityOffset)
                        noChange.Add(key);
                }
                AssertEx.AreEqual(validatingIonMobilityPeptide0.IonMobility, validatingIonMobilityPeptide1.IonMobility, 1.0, "averaged measured drift time differs for " + key);
                AssertEx.AreEqual(validatingIonMobilityPeptide0.HighEnergyIonMobilityOffset, validatingIonMobilityPeptide1.HighEnergyIonMobilityOffset, 2.0, "averaged measured drift time high energy offset differs for " + key);
                AssertEx.AreEqual(validatingIonMobilityPeptide0.CollisionalCrossSectionSqA, validatingIonMobilityPeptide1.CollisionalCrossSectionSqA, 1.0, "averaged measured CCS differs for " + key);
            }
            AssertEx.AreEqual(document.MoleculeTransitionGroupCount, ccount, "did not find drift times for all precursors"); // Expect to find a value for each precursor
            AssertEx.IsTrue(noChange.Count < ccount/2,"expected most values to shift a little without the nice clean training data");


            // And finally verify ability to reimport with altered drift filter (would formerly fail on an erroneous Assume)

            // Simulate user picking Edit Current from the Ion Mobility Library combo control, and messing with all the measured drift time values
            var transitionSettingsDlg2 = ShowDialog<TransitionSettingsUI>(
                () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.Prediction));
            var editIonMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg2.IonMobilityControl.EditIonMobilityLibrary);
            RunUI(() =>
            {
                var revised = new List<ValidatingIonMobilityPrecursor>();
                foreach (var item in editIonMobilityLibraryDlg.LibraryMobilitiesFlat)
                {
                    var im = item.IonMobility;
                    var heo = item.HighEnergyIonMobilityOffset;
                    revised.Add(new ValidatingIonMobilityPrecursor(item.Precursor,
                        IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(im * 1.02, item.IonMobilityUnits),
                            item.CollisionalCrossSectionSqA * 1.02, heo *1.02)));
                }
                editIonMobilityLibraryDlg.LibraryMobilitiesFlat = revised;
            });
            OkDialog(editIonMobilityLibraryDlg, editIonMobilityLibraryDlg.OkDialog);
            OkDialog(transitionSettingsDlg2, transitionSettingsDlg2.OkDialog);
            var docChangedDriftTimePredictor = WaitForDocumentChange(document);

            // Reimport data for a replicate - without the fix this will throw
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                var chromatograms = docChangedDriftTimePredictor.Settings.MeasuredResults.Chromatograms;
                dlg.SelectedChromatograms = new[] { chromatograms[0] };
                dlg.ReimportResults();
                dlg.OkDialog();
            });

            WaitForDocumentChangeLoaded(docChangedDriftTimePredictor, WAIT_TIME*2);
        }  
    }
}
