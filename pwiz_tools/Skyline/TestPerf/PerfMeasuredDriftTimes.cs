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


using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
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
    /// Verify measured drift time derivation against a curated set of drift times
    /// </summary>
    [TestClass]
    public class MeasuredDriftTimesPerfTest : AbstractFunctionalTestEx
    {

        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)] 
        public void MeasuredDriftValuesPerfTest()
        {
            TestFilesZipPaths = new[] {
                GetPerfTestDataURL(@"PerfMeauredDriftTimes.zip"), // Get the .d files
                @"http://skyline.ms/tutorials/IMSFiltering.zip", // Get the Skyline document
            };
            TestFilesPersistent = new[] { "BSA_Frag_100nM_18May15_Fir_15-04-02.d", "Yeast_0pt1ug_BSA_50nM_18May15_Fir_15-04-01.d" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
            
        }


        protected override void DoTest()
        {
            // IsPauseForScreenShots = true; // For a quick demo when you need it
            var errors = new List<string>();
            var measuredMobilities = new List<List<ValidatingIonMobilityPrecursor>>();
            var document = OpenDocument("IMSFiltering\\BSA-Training.sky");
            AssertEx.IsDocumentState(document, null, 1, 34, 38, 404);
            for (var pass = 0; pass < 2; pass++)
            {
                document = SkylineWindow.Document;
                if (pass > 0)
                {
                    // Remove the clean BSA file so we're only looking at messy mix for IM re-extraction
                    // Reimport data for a replicate - without the fix this will throw
                    RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
                    {
                        var chromatograms = document.Settings.MeasuredResults.Chromatograms;
                        dlg.SelectedChromatograms = new[] { chromatograms[0] };
                        dlg.RemoveReplicates();
                        dlg.OkDialog();
                    });
                    document = WaitForDocumentChange(document);
                }

                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(TestFilesDirs[1]
                        .GetTestPath($"local{pass}.sky")); // Avoid "document changed since last edit" message
                });

                // Verify ability to extract predictions from raw data - first pass, use the clean BSA sample
                // N.B. second pass gets benefit of first pass IM findings so we get similar peaks picked
                ImportResultsAsync(TestFilesDirs[0].GetTestPath(TestFilesPersistent[pass]));
                document = WaitForDocumentChangeLoaded(document);
                var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                // Simulate user setting ion mobility resolving power then picking Edit New from the Ion Mobility Library combo control
                RunUI(() =>
                {
                    transitionSettingsDlg.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator
                        .IonMobilityWindowWidthType.resolving_power;
                    transitionSettingsDlg.IonMobilityControl.IonMobilityFilterResolvingPower = 30;
                });
                var ionMobilityLibraryDlg =
                    ShowDialog<EditIonMobilityLibraryDlg>(
                        transitionSettingsDlg.IonMobilityControl.AddIonMobilityLibrary);
                RunUI(() =>
                {
                    ionMobilityLibraryDlg.CreateDatabaseFile(TestFilesDirs[1]
                        .GetTestPath($"local{pass}{IonMobilityDb.EXT}"));
                    ionMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(true);
                    ionMobilityLibraryDlg.GetIonMobilitiesFromResults();
                    measuredMobilities.Add(ionMobilityLibraryDlg.LibraryMobilitiesFlat.ToList());
                });
                OkDialog(ionMobilityLibraryDlg, ionMobilityLibraryDlg.OkDialog);
                OkDialog(transitionSettingsDlg, transitionSettingsDlg.OkDialog);
            }

            // Now compare the derived mobilities
            var expectedMobilityBSA = new[]
            {
                26.46691, 25.65003, 28.75418, 28.26405, 22.87264, 27.77392, 24.5064, 29.40768, 22.21914,
                25.81341,  // NB this one looks like it has a strong conformer at about 24.8
                23.19939, 23.36277,
                27.77392,  // NB this one looks like it has a strong conformer at about 26
                29.2443, 29.40768, 24.01627,
                27.61054, 24.99653, 30.38794, 29.2443,
                23.19939, 27.28379, 27.77392, 29.57106, 27.44717, 29.08093,
                22.87264,  // NB this one looks like it has a strong conformer at about 24.5
                28.75418, 24.01627, 23.19939,
                26.14016, 24.99653, 23.8529, 25.65003, 24.99653, 28.26405, 25.97678, 24.66978,
            };

            document = SkylineWindow.Document;
            var precursors = new LibKeyIndex(document.MoleculePrecursorPairs.Select(
                p => p.NodePep.ModifiedTarget.GetLibKey(p.NodeGroup.PrecursorAdduct).LibraryKey));
            var count = 0;
            for (var n = 0; n < measuredMobilities[0]?.Count; n++)
            {
                var mobilityBSA = measuredMobilities[0][n];
                if (Math.Abs(mobilityBSA.IonMobility - expectedMobilityBSA[n]) > .01)
                {
                    errors.Add($"BSA measured drift time {mobilityBSA.IonMobility} differs from expected {expectedMobilityBSA[n]} for {mobilityBSA.Precursor}");
                }
                var key = mobilityBSA.Precursor;
                var indexYeastMix = measuredMobilities[1].FindIndex(m => m.Precursor.Equals(key));
                var mobilityYeastMix = measuredMobilities[1][indexYeastMix].IonMobility;
                var heoYeastMix = measuredMobilities[1][indexYeastMix].HighEnergyIonMobilityOffset;
                if (precursors.ItemsMatching(key, true).Any())
                {
                    count++;
                }

                var tolerance = 1.0;
                var expected = mobilityBSA.IonMobility;
                if (Math.Abs(expected - mobilityYeastMix) > tolerance)
                {
                    errors.Add( string.Format(
                        $"{key} measured BSA drift time is {expected} but YeastMix measurement is {mobilityYeastMix} #{n}"));
                    
                }
                if (Math.Abs(mobilityBSA.HighEnergyIonMobilityOffset - heoYeastMix) > 2.0)
                    errors.Add($"measured drift time high energy offset {mobilityBSA.HighEnergyIonMobilityOffset} vs {heoYeastMix} differs too much for " + key);
            }
            if (!Equals(document.MoleculeTransitionGroupCount, count))
                errors.Add("did not find drift times for all precursors"); // Expect to find a value for each precursor
            AssertEx.AreEqual(0, errors.Count, string.Join("\n", errors));

            // And finally verify ability to reimport with altered drift filter (would formerly fail on an erroneous Assume)
            // Simulate user picking Edit Current from the Ion Mobility Library combo control, and messing with all the measured drift time values
            var transitionSettingsDlg2 = ShowDialog<TransitionSettingsUI>(
                () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
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
