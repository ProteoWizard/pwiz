/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class IonMobilityTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestIonMobility()
        {
            TestFilesZip = @"TestFunctional\IonMobilityTest.zip";
            RunFunctionalTest();
        }

        private ValidatingIonMobilityPrecursor BuildValidatingIonMobilityPeptide(string seq, Adduct precursorAdduct,
            double ccs, double driftTime, double highEnergyDriftTimeOffsetMsec, eIonMobilityUnits units = eIonMobilityUnits.drift_time_msec)
        {
            return new ValidatingIonMobilityPrecursor(new Target(seq), precursorAdduct, ccs, driftTime,
                highEnergyDriftTimeOffsetMsec, units);
        }

        protected override void DoTest()
        {
            using (var testFilesDir = new TestFilesDir(TestContext, TestFilesZip))
            {
                // Make sure we haven't forgotten to update anything if a new IMS type has been added
                foreach(eIonMobilityUnits units in Enum.GetValues(typeof(eIonMobilityUnits)))
                {
                    if (units != eIonMobilityUnits.unknown)
                    {
                        AssertEx.IsNotNull(IonMobilityFilter.IonMobilityUnitsL10NString(units));
                    }
                }

                // Verify fix for issue where we would not preserve a simple change to IM window width
                TestMobilityWindowWidth();

                const double HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC = -.1;

                // do a few unit tests on the UI error handlers
                TestGetIonMobilityDBErrorHandling(testFilesDir);
                TestImportIonMobilityFromSpectralLibraryErrorHandling();
                TestEditIonMobilityLibraryDlgErrorHandling();

                // Now exercise the UI

                var goodPeptide = BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 133.3210342, 23.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC);
                AssertEx.IsNull(goodPeptide.Validate());
                var badPeptides = new[]
                {
                    BuildValidatingIonMobilityPeptide("@#$!", Adduct.SINGLY_PROTONATED, 133.3210342, 23.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 0, 0, 0),
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, -133.3210342, -23.4, -HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                };
                foreach (var badPeptide in badPeptides)
                {
                    AssertEx.IsNotNull(badPeptide.Validate());
                }

                var CCS_ANELLINVK_MH = 119.2825783;
                var DRIFTTIME_ANELLINVK = 21.4;
                var ionMobilityPeptides = new[]  // N.B. These are made-up values
                {
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 133.3210342, 23.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 133.3210342, 23.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 134.3210342, 24.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Multiple conformers - should be retained
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 134.3210342, 123.4, 0, eIonMobilityUnits.inverse_K0_Vsec_per_cm2), // Different measurement units - should be retained
                    BuildValidatingIonMobilityPeptide("CCSDVFNQVVK", Adduct.SINGLY_PROTONATED, 131.2405487, 22.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("CCSDVFNQVVK", Adduct.SINGLY_PROTONATED, 131.2405487, 22.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("ANELLINVK", Adduct.SINGLY_PROTONATED, CCS_ANELLINVK_MH, DRIFTTIME_ANELLINVK, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("ANELLINVK", Adduct.SINGLY_PROTONATED, CCS_ANELLINVK_MH, DRIFTTIME_ANELLINVK, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("EALDFFAR", Adduct.SINGLY_PROTONATED, 110.6867676, 20.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EALDFFAR", Adduct.SINGLY_PROTONATED, 110.6867676, 20.4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("GVIFYESHGK", Adduct.SINGLY_PROTONATED, 123.7844632, 20.6, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("GVIFYESHGK", Adduct.SINGLY_PROTONATED, 123.7844632, 20.6, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("EKDIVGAVLK", Adduct.SINGLY_PROTONATED, 124.3414249, 20.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EKDIVGAVLK", Adduct.SINGLY_PROTONATED, 124.3414249, 20.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("VVGLSTLPEIYEK", Adduct.SINGLY_PROTONATED, 149.857687, 25.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("VVGLSTLPEIYEK", Adduct.SINGLY_PROTONATED, 149.857687, 25.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("VVGLSTLPEIYEK", Adduct.SINGLY_PROTONATED, 149.857687, 25.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("ANGTTVLVGMPAGAK", Adduct.SINGLY_PROTONATED, 144.7461979, 24.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("ANGTTVLVGMPAGAK", Adduct.SINGLY_PROTONATED, 144.7461979, 24.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("IGDYAGIK", Adduct.SINGLY_PROTONATED,  102.2694763, 14.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("IGDYAGIK", Adduct.SINGLY_PROTONATED,  102.2694763, 14.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("GDYAGIK", Adduct.SINGLY_PROTONATED,  91.09155861, 13.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("GDYAGIK", Adduct.SINGLY_PROTONATED,  91.09155861, 13.7, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Redundant - should be tossed
                    BuildValidatingIonMobilityPeptide("IFYESHGK", Adduct.SINGLY_PROTONATED, 111.2756406, 14.2, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EALDFFAR", Adduct.SINGLY_PROTONATED, 110.6867676, 14.3, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // CCS conflict, should be tossed
                    BuildValidatingIonMobilityPeptide("EALDFFAR", Adduct.DOUBLY_PROTONATED, 90.6867676, 7.3, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), // Different charge, should be retained
                };
                var message = EditIonMobilityLibraryDlg.ValidateUniquePrecursors(ionMobilityPeptides, out var minimalSet); // Check for conflicts, strip out dupes

                AssertEx.AreComparableStrings(Resources.EditIonMobilityLibraryDlg_ValidateUniquePrecursors_The_ion__0__has_multiple_ion_mobility_values__Skyline_supports_multiple_conformers__so_this_may_be_intentional_,
                    message);
                const int EXPECTED_DB_ENTRIES = 14;
                AssertEx.AreEqual(EXPECTED_DB_ENTRIES, minimalSet.Count, "known good data imported but with wrong result count");

                // Present the Ion Mobility tab of the transition settings dialog
                var transitionSettingsDlg1 = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                // Simulate picking "Add..." from the Ion Mobility Libraries button context menu
                var ionMobilityLibDlg1 = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg1.IonMobilityControl.AddIonMobilityLibrary);
                // Simulate user pasting in collisional cross section data to create a new ion mobility library
                string testlibName = "testlib";
                string databasePath = testFilesDir.GetTestPath(testlibName + IonMobilityDb.EXT);
                RunUI(() =>
                {
                    // N.B no library name provided, so we'll automatically use filename as basis
                    ionMobilityLibDlg1.CreateDatabaseFile(databasePath); // Simulate user click on Create button
                    ionMobilityLibDlg1.SetOffsetHighEnergySpectraCheckbox(true);
                    string libraryText = BuildPasteLibraryText(ionMobilityPeptides,
                        seq => seq.Substring(0, seq.Length - 1),
                        ionMobilityLibDlg1.GetOffsetHighEnergySpectraCheckbox());
                    SetClipboardText(libraryText);
                });
                AssertEx.AreEqual(testlibName, ionMobilityLibDlg1.LibraryName);
                // Expect to be warned about multiple conformer
                var warnDlg = ShowDialog<MessageDlg>(() => ionMobilityLibDlg1.DoPasteLibrary());
                RunUI(() =>
                {
                    warnDlg.OkDialog();
                    ionMobilityLibDlg1.OkDialog();
                });
                RunUI(() => transitionSettingsDlg1.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power);
                RunUI(() => transitionSettingsDlg1.IonMobilityControl.SetResolvingPower(50));
                WaitForClosedForm(ionMobilityLibDlg1);
                RunUI(transitionSettingsDlg1.OkDialog);
                WaitForClosedForm(transitionSettingsDlg1);
                // Rename that library
                const string testlibName2 = "testlib2";
                var transitionSettingsDlg2 = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));

                // Simulate user picking Edit... from the Ion Mobility Library combo control
                var editIonMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg2.IonMobilityControl.EditIonMobilityLibrary);

                RunUI(() =>
                {
                    editIonMobilityLibraryDlg.LibraryName = testlibName2;
                    editIonMobilityLibraryDlg.OkDialog();
                });
                WaitForClosedForm(editIonMobilityLibraryDlg);

                var olddoc = SkylineWindow.Document;
                // Set other parameters - name, resolving power
                const double resolvingPower = 123.4;
                RunUI(() =>
                {
                    transitionSettingsDlg2.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
                    transitionSettingsDlg2.IonMobilityControl.IonMobilityFilterResolvingPower = resolvingPower;
                    // Go back to the first library we created
                    transitionSettingsDlg2.IonMobilityControl.SelectedIonMobilityLibrary= testlibName;
                });
                RunUI(transitionSettingsDlg2.OkDialog);

                /*
                * Check that the database was created successfully
                * Check that it has the correct number of entries
                */
                IonMobilityDb db = IonMobilityDb.GetIonMobilityDb(databasePath, null);
                var dbPrecursorAndIonMobilities = db.GetIonMobilities();
                AssertEx.AreEqual(EXPECTED_DB_ENTRIES, dbPrecursorAndIonMobilities.Count());
                WaitForDocumentChange(olddoc);

                // Check serialization and background loader
                WaitForDocumentLoaded();
                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(TestContext.GetTestPath("test.sky"));
                    SkylineWindow.NewDocument();
                    SkylineWindow.OpenFile(TestContext.GetTestPath("test.sky"));
                });
                var doc = WaitForDocumentLoaded();

                // Verify that the schema has been updated to include these new settings
                AssertEx.ValidatesAgainstSchema(doc);
                // Do some DT calculations
                double driftTimeMax = 1000.0;
                var node = FindNodes("ANELLINV", Adduct.SINGLY_PROTONATED);
                var centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                AssertEx.AreEqual(DRIFTTIME_ANELLINVK, centerIonMobility.IonMobilityAndCCS.IonMobility.Mobility);
                AssertEx.AreEqual(2 * DRIFTTIME_ANELLINVK / resolvingPower, centerIonMobility.IonMobilityExtractionWindowWidth);
                AssertEx.AreEqual(DRIFTTIME_ANELLINVK + HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, centerIonMobility.IonMobilityAndCCS.GetHighEnergyIonMobility());

                var centerIonMobilityNoHighEnergy = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                AssertEx.AreEqual(DRIFTTIME_ANELLINVK, centerIonMobilityNoHighEnergy.IonMobilityAndCCS.IonMobility.Mobility);
                AssertEx.AreEqual(2 * DRIFTTIME_ANELLINVK / resolvingPower, centerIonMobilityNoHighEnergy.IonMobilityExtractionWindowWidth);
                AssertEx.AreEqual(DRIFTTIME_ANELLINVK+HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, centerIonMobilityNoHighEnergy.IonMobilityAndCCS.GetHighEnergyIonMobility());

                //
                // Test importing collisional cross sections from a spectral lib that has drift times but no high energy offset info
                //
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                const string libname = "libIMS";
                var blibPath = TestContext.GetTestPath("IonMobilityTest\\mse-mobility.filtered-scaled.blib");
                var editListUI =
                    ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
                RunDlg<EditLibraryDlg>(editListUI.AddItem, editLibraryDlg =>
                {
                    editLibraryDlg.LibrarySpec = new BiblioSpecLibSpec(libname, blibPath); 
                    editLibraryDlg.OkDialog();
                });
                OkDialog(editListUI, editListUI.OkDialog);
                RunUI(() => peptideSettingsUI.PickedLibraries = new[] { libname });
                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                var ionMobilityControl = transitionSettingsUI.IonMobilityControl;

                // Check error cases for resolving power (caused unexpected exception)
                RunUI(() =>
                {
                    ionMobilityControl.IsUseSpectralLibraryIonMobilities = true;
                    ionMobilityControl.IonMobilityFilterResolvingPower = null;

                });
                RunDlg<MessageDlg>(transitionSettingsUI.OkDialog, dlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, dlg.Message);
                    dlg.OkDialog();
                });
                RunUI(() => ionMobilityControl.IonMobilityFilterResolvingPower = -1);
                RunDlg<MessageDlg>(transitionSettingsUI.OkDialog, dlg =>
                {
                    AssertEx.AreEqual(Resources.EditIonMobilityLibraryDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_, dlg.Message);
                    dlg.OkDialog();
                });
                RunUI(() => ionMobilityControl.IonMobilityFilterResolvingPower = 50);

                RunUI(() => ionMobilityControl.IsUseSpectralLibraryIonMobilities = false);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                WaitForDocumentLoaded(); // Let that library load

                // In this lib: ANGTTVLVGMPAGAK at z=2, with drift time 4.99820623749102
                // and, a single CCS value, for ANELLINVK, which is 3.8612432898618
                // Present the Prediction tab of the peptide settings dialog
                var transitionSettingsDlg3 = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                RunUI(() => transitionSettingsDlg3.IonMobilityControl.SetResolvingPower(resolvingPower));
                // Simulate picking "Add..." from the Ion Mobility Library combo control
                var ionMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg3.IonMobilityControl.AddIonMobilityLibrary);
                const double deadeelsDT = 3.456;
                const double deadeelsCCS = 345.67;
                const double deadeelsDTHighEnergyOffset = -0.1;
                var fourCols = "DEADEELS\t5\t" +
                               deadeelsCCS.ToString(CultureInfo.CurrentCulture) + "\t" +
                               deadeelsDT.ToString(CultureInfo.CurrentCulture) + "\t" +
                               Resources.IonMobilityFilter_IonMobilityUnitsString_Drift_Time__ms_;
                var fiveCols = fourCols + "\t" + deadeelsDTHighEnergyOffset.ToString(CultureInfo.CurrentCulture);
                testlibName = "test3";
                RunUI(() =>
                {
                    ionMobilityLibraryDlg.LibraryName = testlibName;
                    // Simulate user pasting in some measured drift time info without high energy offset, even though its enabled - should not throw
                    ionMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(true);
                    SetClipboardText(fourCols);
                    ionMobilityLibraryDlg.DoPasteLibrary();
                    // Simulate user pasting in some measured drift time info with high energy offset
                    SetClipboardText(fiveCols);
                    ionMobilityLibraryDlg.DoPasteLibrary();
                    // Now turn off the high energy column and paste in five columns - should fail
                    ionMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(false);
                    SetClipboardText(fiveCols);
                });
                // An error will appear because the column count is wrong
                ShowDialog<MessageDlg>(ionMobilityLibraryDlg.DoPasteLibrary);
                var errorDlg = WaitForOpenForm<MessageDlg>();
                AssertEx.AreEqual(string.Format(Resources.SettingsUIUtil_DoPasteText_Incorrect_number_of_columns__0__found_on_line__1__, 6, 1), errorDlg.Message);
                errorDlg.OkDialog();
                RunUI(() =>
                {
                    // And now paste in four columns, should be OK
                    SetClipboardText(fourCols);
                    ionMobilityLibraryDlg.DoPasteLibrary();

                    // Finally turn the high energy column back on, and put in a value
                    ionMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(true);
                    SetClipboardText(fiveCols);
                    ionMobilityLibraryDlg.DoPasteLibrary();
                });
                // Expect an error message requesting a filename for the new library
                ShowDialog<MessageDlg>(ionMobilityLibraryDlg.OkDialog);
                errorDlg = WaitForOpenForm<MessageDlg>();
                AssertEx.IsTrue(errorDlg.Message.Contains(Resources.EditIonMobilityLibraryDlg_OkDialog_Please_choose_a_file_for_the_ion_mobility_library));
                errorDlg.OkDialog();
                RunUI(() =>
                {
                    ionMobilityLibraryDlg.CreateDatabaseFile(testlibName + IonMobilityDb.EXT);
                });
                // Expect an error message requesting a full path for the new library
                ShowDialog<MessageDlg>(ionMobilityLibraryDlg.OkDialog);
                errorDlg = WaitForOpenForm<MessageDlg>();
                AssertEx.IsTrue(errorDlg.Message.Contains(Resources.EditIonMobilityLibraryDlg_OkDialog_Please_use_a_full_path_to_a_file_for_the_ion_mobility_library_));
                errorDlg.OkDialog();
                RunUI(() =>
                {
                    ionMobilityLibraryDlg.CreateDatabaseFile(testFilesDir.GetTestPath(testlibName + IonMobilityDb.EXT));
                });

                /* We don't handle any external ion mobility library formats yet
                // Test import ion mobility data from an external file format (other than spectral libs).
                var ionMobilityLibDlg3 = ShowDialog<EditIonMobilityLibraryDlg>(ionMobilityLibraryDlg.AddIonMobilityLibrary);
                const string testlibName3 = "testlib3";
                string databasePath3 = testFilesDir.GetTestPath(testlibName3 + IonMobilityDb.EXT);
                RunUI(() =>
                {
                    ionMobilityLibDlg3.LibraryName = testlibName3;
                    ionMobilityLibDlg3.CreateDatabase(databasePath3, testlibName3);
                });
                */
                // Simulate pressing "Import" button from the Edit Ion Mobility Library dialog
                var importSpectralLibDlg =
                    ShowDialog<ImportIonMobilityFromSpectralLibraryDlg>(ionMobilityLibraryDlg.ImportFromSpectralLibrary);
                RunUI(() =>
                {
                    // Set up to fail - don't provide z=2 info
                    importSpectralLibDlg.Source = SpectralLibrarySource.settings; // Simulate user selecting 1st radio button
                });
                RunUI(() => importSpectralLibDlg.OkDialog()); // User clicks OK 
                RunUI(() =>
                {
                    importSpectralLibDlg.Source = SpectralLibrarySource.file; // Simulate user selecting 2nd radio button
                    importSpectralLibDlg.FilePath = blibPath; // Simulate user entering filename
                    importSpectralLibDlg.OkDialog();
                });
                WaitForClosedForm(importSpectralLibDlg);
                WaitForCondition(() => ionMobilityLibraryDlg.LibraryMobilitiesFlatCount > 8); // Let that library load
                RunUI(ionMobilityLibraryDlg.OkDialog);
                WaitForClosedForm(ionMobilityLibraryDlg);
                RunUI(() => transitionSettingsDlg3.OkDialog());
                WaitForClosedForm(transitionSettingsDlg3);
                doc = WaitForDocumentChangeLoaded(doc); // Let that library load

                // Do some DT calculations with this new library
                node = FindNodes("ANELLINVK", Adduct.DOUBLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                AssertEx.IsTrue(centerIonMobility.IsEmpty); // This library entry was CCS only, so GetIonMobilityHelp with no ionMobilityFunctionsProvider returns EMPTY
                node = FindNodes("ANGTTVLVGMPAGAK", Adduct.DOUBLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                var dt = 4.99820623749102;
                AssertEx.IsFalse(centerIonMobility.IonMobilityAndCCS.CollisionalCrossSectionSqA.HasValue);
                AssertEx.AreEqual(dt, centerIonMobility.IonMobilityAndCCS.IonMobility.Mobility.Value, .000001);
                AssertEx.AreEqual(2 * dt / resolvingPower, centerIonMobility.IonMobilityExtractionWindowWidth ?? 0, .000001);

                node = FindNodes("DEADEELS", Adduct.TRIPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax); // Should fail to find anything
                AssertEx.IsTrue(centerIonMobility.IsEmpty);

                node = FindNodes("DEADEELS", Adduct.QUINTUPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                AssertEx.AreEqual(deadeelsDT, centerIonMobility.IonMobilityAndCCS.IonMobility.Mobility.Value, .000001);
                AssertEx.AreEqual(deadeelsDT+deadeelsDTHighEnergyOffset, centerIonMobility.IonMobilityAndCCS.GetHighEnergyIonMobility() ?? -1, .000001);
                AssertEx.AreEqual(2 * (deadeelsDT / resolvingPower), centerIonMobility.IonMobilityExtractionWindowWidth??0, .0001); // Directly measured, should match

                // Now check handling of scenario where user pastes in high energy offsets then unchecks the "Use High Energy Offsets" box
                var transitionSettingsDlg4 = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                // Simulate picking "Edit Current..." from the Ion Mobility Library combo control
                var mobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg4.IonMobilityControl.EditIonMobilityLibrary);
                RunUI(() =>
                {
                    AssertEx.IsTrue(mobilityLibraryDlg.GetOffsetHighEnergySpectraCheckbox()); // Should start out enabled if we have offsets
                    mobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(false); // Turn off the high energy offset column
                    mobilityLibraryDlg.LibraryName="test4";
                });
                RunUI(()=>mobilityLibraryDlg.OkDialog());
                WaitForClosedForm(mobilityLibraryDlg);
                RunUI(transitionSettingsDlg4.OkDialog);
                WaitForClosedForm(transitionSettingsDlg4);
                doc = WaitForDocumentChangeLoaded(doc);
                node = FindNodes("DEADEELS", Adduct.QUINTUPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                AssertEx.AreEqual(deadeelsDT, centerIonMobility.IonMobilityAndCCS.IonMobility.Mobility.Value, .000001);
                AssertEx.AreEqual(deadeelsDT-0.1, centerIonMobility.IonMobilityAndCCS.GetHighEnergyIonMobility() ?? -1, .000001); // High energy value should now be same as low energy value
                AssertEx.AreEqual(2 * (deadeelsDT / resolvingPower), centerIonMobility.IonMobilityExtractionWindowWidth??0, .0001); // Directly measured, should match

                // Now make sure that high energy checkbox initial state is as we expect (expect it checked since lib has HE offsets)
                var transitionSettingsDlg5 = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                // Simulate picking "Edit Current..." from the Ion Mobility Library combo control
                var driftTimePredictorDlg5 = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg5.IonMobilityControl.EditIonMobilityLibrary);
                RunUI(() => AssertEx.IsTrue(driftTimePredictorDlg5.GetOffsetHighEnergySpectraCheckbox()));
                RunUI(driftTimePredictorDlg5.CancelDialog);
                WaitForClosedForm(driftTimePredictorDlg5);
                OkDialog(transitionSettingsDlg5, () => transitionSettingsDlg5.OkDialog());

                // Try it with linear range instead of resolving power
                var transitionSettingsDlg6 = ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                RunUI(() => transitionSettingsDlg6.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.linear_range);
                // Simulate picking "Edit Current..." from the Ion Mobility Library combo control
                var driftTimePredictorDlg6 = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg6.IonMobilityControl.EditIonMobilityLibrary);
                var widthAtDtZero = 10;
                var widthAtDtMax = 1000;
                RunUI(() => transitionSettingsDlg6.IonMobilityControl.SetWidthAtIonMobilityMax(widthAtDtMax));
                RunUI(() => transitionSettingsDlg6.IonMobilityControl.SetWidthAtIonMobilityZero(widthAtDtZero));
                OkDialog(driftTimePredictorDlg6, () => driftTimePredictorDlg6.OkDialog());
                OkDialog(transitionSettingsDlg6, () => transitionSettingsDlg6.OkDialog());
                doc = WaitForDocumentChangeLoaded(doc);
                node = FindNodes("DEADEELS", Adduct.QUINTUPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                AssertEx.AreEqual(deadeelsDT, centerIonMobility.IonMobilityAndCCS.IonMobility.Mobility.Value, .000001);
                AssertEx.AreEqual(deadeelsDT-0.1, centerIonMobility.IonMobilityAndCCS.GetHighEnergyIonMobility() ?? -1, .000001); // High energy value should now be same as low energy value
                AssertEx.AreEqual(widthAtDtZero + deadeelsDT * (widthAtDtMax - widthAtDtZero) / driftTimeMax, centerIonMobility.IonMobilityExtractionWindowWidth??0, .0001);

                // Try it with fixed width
                var transitionSettingsDlg7= ShowDialog<TransitionSettingsUI>(
                    () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                RunUI(() => transitionSettingsDlg7.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width);
                // Simulate picking "Edit Current..." from the Ion Mobility Library combo control
                var driftTimePredictorDlg7 = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg7.IonMobilityControl.EditIonMobilityLibrary);
                var fixedWidth = 100;
                RunUI(() => transitionSettingsDlg7.IonMobilityControl.SetFixedWidth(fixedWidth));
                OkDialog(driftTimePredictorDlg7, () => driftTimePredictorDlg7.OkDialog());
                OkDialog(transitionSettingsDlg7, () => transitionSettingsDlg7.OkDialog());
                doc = WaitForDocumentChangeLoaded(doc);
                node = FindNodes("DEADEELS", Adduct.QUINTUPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax);
                AssertEx.AreEqual(deadeelsDT, centerIonMobility.IonMobilityAndCCS.IonMobility.Mobility.Value, .000001);
                AssertEx.AreEqual(deadeelsDT-0.1, centerIonMobility.IonMobilityAndCCS.GetHighEnergyIonMobility() ?? -1, .000001); // High energy value should now be same as low energy value
                AssertEx.AreEqual(fixedWidth, centerIonMobility.IonMobilityExtractionWindowWidth??0, .0001);

            }
            TestMeasuredDriftTimes();
        }

        private void SetUiDocument(SrmDocument newDocument)
        {
            RunUI(() => AssertEx.IsTrue(SkylineWindow.SetDocument(newDocument, SkylineWindow.DocumentUI)));
        }

        // Verify fix for issue where we would not preserve a simple change to IM window width
        private static void TestMobilityWindowWidth()
        {
            var doc = SkylineWindow.Document;
            if (IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none !=
                doc.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.WindowWidthMode)
            {
                var transitionSettingsDlg0 = ShowDialog<TransitionSettingsUI>(() => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
                RunUI(() => transitionSettingsDlg0.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none);
                RunUI(() =>  transitionSettingsDlg0.OkDialog());
                doc = WaitForDocumentChange(doc);
                AssertEx.AreEqual(IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.none,
                    doc.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.WindowWidthMode);
            }

            var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(() => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
            RunUI(() => transitionSettingsDlg.IonMobilityControl.WindowWidthType = IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width);
            RunUI(() => transitionSettingsDlg.IonMobilityControl.SetFixedWidth(5));
            RunUI(() => transitionSettingsDlg.OkDialog());
            doc = WaitForDocumentChange(doc);
            AssertEx.AreEqual(IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.fixed_width,
                doc.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.WindowWidthMode);
            AssertEx.AreEqual(5,
                doc.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.FixedWindowWidth);

            // If the bug has not been fixed, we won't preserve changing just the window width
            transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(() => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
            RunUI(() => transitionSettingsDlg.IonMobilityControl.SetFixedWidth(55));
            RunUI(() => transitionSettingsDlg.OkDialog());
            doc = WaitForDocumentChange(doc);
            AssertEx.AreEqual(55,
                doc.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator.FixedWindowWidth);

        }

        private static string BuildPasteLibraryText(IEnumerable<ValidatingIonMobilityPrecursor> mobilities, Func<string, string> adjustSeq, bool useHighEnergyOffset)
        {
            var pasteBuilder = new StringBuilder();
            foreach (var item in mobilities)
            {
                var target = item.Precursor.Target;
                var adduct = item.Precursor.Adduct;
                pasteBuilder.Append(adjustSeq(target.ToString()))
                    .Append('\t')
                    .Append(adduct)
                    .Append('\t')
                    .Append(item.CollisionalCrossSectionSqA == 0 ? string.Empty : item.CollisionalCrossSectionSqA.ToString(CultureInfo.CurrentCulture))
                    .Append('\t')
                    .Append(item.IonMobility == 0 ? string.Empty : item.IonMobility.ToString(CultureInfo.CurrentCulture))
                    .Append('\t')
                    .Append(item.IonMobilityUnitsDisplay);
                if (useHighEnergyOffset)
                    pasteBuilder.Append('\t').Append(item.HighEnergyIonMobilityOffset.ToString(CultureInfo.CurrentCulture));
                pasteBuilder.AppendLine();
            }

            return pasteBuilder.ToString();
        }

        /// <summary>
        /// Test various error conditions in IonMobilityDb.cs
        /// </summary>
        public void TestGetIonMobilityDBErrorHandling(TestFilesDir testFilesDir)
        {
            AssertEx.ThrowsException<DatabaseOpeningException>(() => IonMobilityDb.GetIonMobilityDb(null, null),
                Resources.IonMobilityDb_GetIonMobilityDb_Please_provide_a_path_to_an_existing_ion_mobility_library_);

            const string badfilename = "nonexistent_file.imsdb";
            AssertEx.ThrowsException<DatabaseOpeningException>(
                () => IonMobilityDb.GetIonMobilityDb(badfilename, null),
                String.Format(
                    Resources.IonMobilityDb_GetIonMobilityDb_The_ion_mobility_library_file__0__could_not_be_found__Perhaps_you_did_not_have_sufficient_privileges_to_create_it_,
                    badfilename));

            string bogusfile = testFilesDir.GetTestPath("bogus.imdb");
            using (FileStream fs = File.Create(bogusfile))
            {
                Byte[] info = new UTF8Encoding(true).GetBytes("This is a bogus file.");
                fs.Write(info, 0, info.Length);
            }
            AssertEx.ThrowsException<DatabaseOpeningException>(
                () => IonMobilityDb.GetIonMobilityDb(bogusfile, null),
                String.Format(
                    Resources.IonMobilityDb_GetIonMobilityDb_The_file__0__is_not_a_valid_ion_mobility_library_file_,
                    bogusfile));
        }

        /// <summary>
        /// Test various error conditions in ImportIonMobilityFromSpectralLibrary.cs
        /// </summary>
        public void TestImportIonMobilityFromSpectralLibraryErrorHandling()
        {
            var message = ImportIonMobilityFromSpectralLibraryDlg.ValidateSpectralLibraryPath(null);
            AssertEx.AreEqual(message,
                Resources
                    .ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Please_specify_a_path_to_an_existing_spectral_library);

            message =
                ImportIonMobilityFromSpectralLibraryDlg.ValidateSpectralLibraryPath("redundant." + BiblioSpecLiteSpec.EXT_REDUNDANT);
            AssertEx.Contains(message,
                Resources.ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Please_choose_a_non_redundant_library_);

            message = ImportIonMobilityFromSpectralLibraryDlg.ValidateSpectralLibraryPath("badchoice.nist");
            // Only wants to see .blib
            AssertEx.Contains(message,
                Resources
                    .ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Only_BiblioSpec_libraries_contain_enough_ion_mobility_information_to_support_this_operation_);

            message = ImportIonMobilityFromSpectralLibraryDlg.ValidateSpectralLibraryPath("fakefile." + BiblioSpecLiteSpec.EXT);
            // File is well named but doesn't exist
            AssertEx.Contains(message,
                Resources
                    .ImportIonMobilityFromSpectralLibrary_ValidateSpectralLibraryPath_Please_specify_a_path_to_an_existing_spectral_library_);
        }

        private void TestParser(int errCol, string errVal, string expectedMessage)
        {
            var dtValues = InitializeIonMobilityTestColumns();
            dtValues[errCol] = errVal;
            var message = CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(dtValues,
                1, TargetResolver.EMPTY, out _);
            if (string.IsNullOrEmpty(expectedMessage))
                AssertEx.AreEqualLines(string.Empty, message ?? string.Empty);
            else
                AssertEx.Contains(message, expectedMessage);
        }

        /// <summary>
        /// Test various error conditions in EditIonMobilityLibraryDlg.cs,
        /// mostly around handling conflicting values
        /// </summary>
        public void TestEditIonMobilityLibraryDlgErrorHandling()
        {
            var peptides = new List<ValidatingIonMobilityPrecursor>();
            var targetResolver = TargetResolver.EMPTY;

            var seq = "JKLMN";
            const double HIGH_ENERGY_DRIFT_OFFSET_MSEC = -0.5;
            peptides.Add(BuildValidatingIonMobilityPeptide(seq, Adduct.SINGLY_PROTONATED, 1, .5,HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            peptides.Add(BuildValidatingIonMobilityPeptide(seq, Adduct.DOUBLY_PROTONATED, 2.1, 1.52, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            peptides.Add(BuildValidatingIonMobilityPeptide(seq, Adduct.SINGLY_PROTONATED, 1.1, .52, HIGH_ENERGY_DRIFT_OFFSET_MSEC)); // Multiple conformers
            var message = EditIonMobilityLibraryDlg.ValidateUniquePrecursors(peptides, out var peptideSet);
            AssertEx.AreEqual(message, string.Format(Resources.EditIonMobilityLibraryDlg_ValidateUniquePrecursors_The_ion__0__has_multiple_ion_mobility_values__Skyline_supports_multiple_conformers__so_this_may_be_intentional_,
                peptides.First().Precursor));
            AssertEx.AreEqual(3, peptideSet.Count);


            string seqB = seq + "L";
            peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, 1.1, 3.3, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, 1.2, 3.4, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            message = EditIonMobilityLibraryDlg.ValidateUniquePrecursors(peptides, out peptideSet);
            AssertEx.Contains(message,
                Resources.EditIonMobilityLibraryDlg_ValidateUniquePrecursors_The_following_ions_have_multiple_ion_mobility_values__Skyline_supports_multiple_conformers__so_this_may_be_intentional_);

            for (int n = 0; n < 20; n++)
            {
                seqB = seqB + "M";
                peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, n, (n+1)*.5, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
                peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, n + 1, (n+2)*.5, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            }
            message = EditIonMobilityLibraryDlg.ValidateUniquePrecursors(peptides, out peptideSet);
            const int lineNumber = 1;
            AssertEx.Contains(message,
                string.Format(Resources.EditIonMobilityLibraryDlg_ValidateUniquePrecursors_This_list_contains__0__ions_with_multiple_ion_mobility_values__Skyline_supports_multiple_conformers__so_this_may_be_intentional_,
                    22));
            RunUI(() =>
            {
                using (var IonMobilityFilteringUserControl = new IonMobilityFilteringUserControl())
                {
                    AssertEx.Contains(IonMobilityFilteringUserControl.ValidateResolvingPower(-1),
                        Resources
                            .EditIonMobilityLibraryDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_);
                    AssertEx.IsNull(IonMobilityFilteringUserControl.ValidateResolvingPower(1));

                    AssertEx.Contains(IonMobilityFilteringUserControl.ValidateWidth(-1),
                        Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_);
                    AssertEx.IsNull(IonMobilityFilteringUserControl.ValidateWidth(1));
                    AssertEx.IsNull(IonMobilityFilteringUserControl.ValidateWidth(0));
                    AssertEx.IsNull(IonMobilityFilteringUserControl.ValidateResolvingPower(0));

                    IonMobilityFilteringUserControl
                        .ShowOnlyResolvingPowerControls(300); // In this mode, insist on non-negative width parameters
                    AssertEx.Contains(IonMobilityFilteringUserControl.ValidateWidth(-1),
                        Resources.DriftTimeWindowWidthCalculator_Validate_Peak_width_must_be_non_negative_);
                    AssertEx.Contains(IonMobilityFilteringUserControl.ValidateResolvingPower(-1), // Negative values are nonsense, but we allow zero as meaning "no filtering"
                        Resources
                            .EditIonMobilityLibraryDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_);
                }
            });


            TestParser(EditIonMobilityLibraryDlg.COLUMN_ADDUCT, "99",String.Format(
                Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow__0__is_not_a_valid_charge__Precursor_charges_must_be_integers_with_absolute_value_between_1_and__1__,
                99, TransitionGroup.MAX_PRECURSOR_CHARGE));
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(new[] { "", "" }, lineNumber, TargetResolver.EMPTY, out _),
                Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_pasted_text_must_at_a_minimum_contain_columns_for_peptide_and_adduct__along_with_collisional_cross_section_and_or_ion_mobility_);
            TestParser(EditIonMobilityLibraryDlg.COLUMN_TARGET, "",
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_peptide_sequence_on_line__0__, lineNumber));
            TestParser(EditIonMobilityLibraryDlg.COLUMN_TARGET, "$%$%!",
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_text__0__is_not_a_valid_peptide_sequence_on_line__1__, "$%$%!", lineNumber));
            TestParser(EditIonMobilityLibraryDlg.COLUMN_ADDUCT, "dog",
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Could_not_parse_adduct_description___0___on_line__1_, "dog", lineNumber));
            TestParser(EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY, (17.9).ToString(CultureInfo.CurrentCulture), null);
            TestParser(EditIonMobilityLibraryDlg.COLUMN_ADDUCT, "",
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_adduct_description_on_line__0_, lineNumber));

            object[] column = { "1" };
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(column, 1, targetResolver, out _),
                Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_pasted_text_must_at_a_minimum_contain_columns_for_peptide_and_adduct__along_with_collisional_cross_section_and_or_ion_mobility_);

            var dtValues = InitializeIonMobilityTestColumns();
            dtValues[EditIonMobilityLibraryDlg.COLUMN_CCS] =
                dtValues[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY] = "";
            AssertEx.Contains(
                CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(dtValues,
                    1, TargetResolver.EMPTY, out _), string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_collisional_cross_section_value_on_line__0__, lineNumber));
            AssertEx.Contains(
                CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(dtValues,
                    1, TargetResolver.EMPTY, out _), string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_ion_mobility_value_on_line__0__, lineNumber));

            dtValues = InitializeIonMobilityTestColumns();
            dtValues[EditIonMobilityLibraryDlg.COLUMN_ADDUCT] = "M+H";
            AssertEx.IsNull(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(dtValues, lineNumber, TargetResolver.EMPTY, out _));

            dtValues = InitializeIonMobilityTestColumns();
            dtValues[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET] = "zeke"; // HighEnergyDriftTimeOffsetMsec
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(dtValues, lineNumber, TargetResolver.EMPTY, out _),
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Cannot_read_high_energy_ion_mobility_offset_value___0___on_line__1__,
                    "zeke", lineNumber));

            dtValues[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET] = ""; // HighEnergyDriftTimeOffset
            AssertEx.IsNull(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(dtValues, lineNumber, TargetResolver.EMPTY, out _));

            dtValues[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET] = "1"; // HighEnergyDriftTimeOffset (usually negative, but we don't demand it)
            AssertEx.IsNull(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPrecursor>.ValidateRow(dtValues, lineNumber, TargetResolver.EMPTY, out _));

            var pep = BuildValidatingIonMobilityPeptide(string.Empty, Adduct.EMPTY, 0, 0, 0);
            AssertEx.Contains(pep.Validate(), Resources.ValidatingIonMobilityPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry_);

            seq = "@#%!";
            pep = BuildValidatingIonMobilityPeptide(seq, Adduct.EMPTY, -1, 0, 0);
            AssertEx.Contains(pep.Validate(), string.Format(
                Resources.ValidatingIonMobilityPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, seq));
            AssertEx.Contains(pep.Validate(),
                Resources.ValidatingIonMobilityPeptide_ValidateAdduct_A_valid_adduct_description__e_g____M_H____must_be_provided_);
            AssertEx.Contains(pep.Validate(),
                Resources.ValidatingIonMobilityPeptide_ValidateCollisionalCrossSection_Measured_collisional_cross_section_values_must_be_valid_decimal_numbers_greater_than_zero_);

            pep = BuildValidatingIonMobilityPeptide(seq, Adduct.EMPTY, 0, 0, 0);
            AssertEx.Contains(pep.Validate(), string.Format(
                Resources.ValidatingIonMobilityPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, seq));

            pep = BuildValidatingIonMobilityPeptide("JLKM", Adduct.SINGLY_PROTONATED, 1, 2, 0);
            AssertEx.IsNull(pep.Validate());

            pep = BuildValidatingIonMobilityPeptide("JLKM", Adduct.SINGLY_PROTONATED, 1, 0, 0);
            AssertEx.IsNull(pep.Validate());

            pep = BuildValidatingIonMobilityPeptide("JLKM", Adduct.SINGLY_PROTONATED, 0, 1, 0);
            AssertEx.IsNull(pep.Validate());

        }

        private static string[] InitializeIonMobilityTestColumns()
        {
            var dtValues = new string[] {null, null, null, null, null, null};
            dtValues[EditIonMobilityLibraryDlg.COLUMN_TARGET] = "JKLM";
            dtValues[EditIonMobilityLibraryDlg.COLUMN_ADDUCT] = "+2";
            dtValues[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY] = (0.2).ToString(CultureInfo.CurrentCulture);
            dtValues[EditIonMobilityLibraryDlg.COLUMN_CCS] = "1";
            dtValues[EditIonMobilityLibraryDlg.COLUMN_HIGH_ENERGY_OFFSET] = (-0.1).ToString(CultureInfo.CurrentCulture);
            dtValues[EditIonMobilityLibraryDlg.COLUMN_ION_MOBILITY_UNITS] = @"ドリフト時間(ms)"; // Japanese, tests our ability to handle cross-culture imports
            return dtValues;
        }

        private PeptidePrecursorPair FindNodes(string seq, Adduct adduct)
        {
            var result = SkylineWindow.Document.PeptidePrecursorPairs.FirstOrDefault(pair =>
              pair.NodePep.IsProteomic &&
              pair.NodePep.Target.Sequence.Equals(seq) &&
              pair.NodeGroup.PrecursorAdduct.Equals(adduct));
            if (result?.NodePep == null)
            {
                var targ = new Target(seq);
                var pep = new Peptide(targ);
                var pepnode = new PeptideDocNode(pep);
                var group = new TransitionGroup(pep, adduct, IsotopeLabelType.light);
                var groupnode = new TransitionGroupDocNode(group, new TransitionDocNode[0]);
                result = new PeptidePrecursorPair(pepnode, groupnode);
            }
            return result;
        }

        /// <summary>
        /// Tests our ability to discover drift times by inspecting loaded results
        /// </summary>
        private void TestMeasuredDriftTimes()
        {
            var testFilesDir = new TestFilesDir(TestContext, @"TestData\Results\BlibDriftTimeTest.zip"); // Re-used from BlibDriftTimeTest
            // Open document with some peptides but no results
            var documentFile = TestFilesDir.GetTestPath(@"..\BlibDriftTimeTest\BlibDriftTimeTest.sky");
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            WaitForDocumentLoaded();
            var doc = SkylineWindow.Document;

            // Import an mz5 file that contains drift info
            ImportResultsFile(testFilesDir.GetTestPath(@"..\BlibDriftTimeTest\ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5));
            // Verify ability to extract predictions from raw data
            var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(
                () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));

            // Simulate user picking Add... from the Ion Mobility Library combo control
            var ionMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg.IonMobilityControl.AddIonMobilityLibrary);
            string libName = "TestMeasuredDriftTimes";
            const double resolvingPower = 123.4;
            RunUI(() => transitionSettingsDlg.IonMobilityControl.IonMobilityFilterResolvingPower = resolvingPower);

            var databasePath = TestFilesDir.GetTestPath(libName + IonMobilityDb.EXT);

            RunUI(() =>
            {
                ionMobilityLibraryDlg.LibraryName = libName;
                ionMobilityLibraryDlg.CreateDatabaseFile(databasePath); // Simulate user click on Create button
                ionMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(true);
                ionMobilityLibraryDlg.GetIonMobilitiesFromResults();
                ionMobilityLibraryDlg.OkDialog();
            });
            WaitForClosedForm(ionMobilityLibraryDlg);
            RunUI(() =>
            {
                transitionSettingsDlg.OkDialog();
            });
            WaitForClosedForm(transitionSettingsDlg);
            WaitForDocumentChange(doc);

            // Now close the document, then reopen to verify imsdb background loader operation
            var docName = TestContext.GetTestPath("reloaded.sky");
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(docName);
                SkylineWindow.NewDocument();
                SkylineWindow.OpenFile(docName);
            });
            doc = WaitForDocumentLoaded();

            var result = doc.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary;
            AssertEx.AreEqual(2, result.Count);
            var key3 = new LibKey("GLAGVENVTELKK", Adduct.TRIPLY_PROTONATED);
            var key2 = new LibKey("GLAGVENVTELKK", Adduct.DOUBLY_PROTONATED);
            const double expectedDT3= 4.0709;
            const double expectedOffset3 = 0.8969;
            AssertEx.AreEqual(expectedDT3, result.GetIonMobilityInfo(key3).First().IonMobility.Mobility.Value, .001);
            AssertEx.AreEqual(expectedOffset3, result.GetIonMobilityInfo(key3).First().HighEnergyIonMobilityValueOffset.Value, .001); // High energy offset
            const double expectedDT2 = 5.5889;
            const double expectedOffset2 = -1.1039;
            AssertEx.AreEqual(expectedDT2, result.GetIonMobilityInfo(key2).First().IonMobility.Mobility.Value, .001);
            AssertEx.AreEqual(expectedOffset2, result.GetIonMobilityInfo(key2).First().HighEnergyIonMobilityValueOffset.Value, .001);  // High energy offset
            var doc2 = WaitForDocumentLoaded();

            // Reimport with these new settings, then export a spectral library and verify it got IMS data
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                var chromatograms = doc2.Settings.MeasuredResults.Chromatograms;
                dlg.SelectedChromatograms = new[] { chromatograms[0] };
                dlg.ReimportResults();
                dlg.OkDialog();
            });
            doc = WaitForDocumentLoaded();
            var progress = new SilentProgressMonitor();
            var exported = testFilesDir.GetTestPath("export.blib");
            new SpectralLibraryExporter(SkylineWindow.Document, SkylineWindow.DocumentFilePath)
                .ExportSpectralLibrary(exported, progress);
            var refSpectra = GetRefSpectra(exported);
            AssertEx.IsTrue(refSpectra.All(r => (r.IonMobility??0) > 0));

            // Now simulate user tinkering with IMS library values - make sure they persist
            transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(() => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
            ionMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg.IonMobilityControl.EditIonMobilityLibrary);
            RunUI(() =>
            {
                var values = ionMobilityLibraryDlg.LibraryMobilitiesFlat;
                values[0].IonMobility = expectedDT2+1;
                ionMobilityLibraryDlg.LibraryMobilitiesFlat = values;
            });
            RunUI(() =>
            {
                ionMobilityLibraryDlg.OkDialog();
            });
            WaitForClosedForm(ionMobilityLibraryDlg);
            RunUI(() =>
            {
                transitionSettingsDlg.OkDialog();
            });
            WaitForClosedForm(transitionSettingsDlg);
            doc = WaitForDocumentChange(doc);
            result = doc.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary;
            AssertEx.AreEqual(expectedDT2+1, result.GetIonMobilityInfo(key2).First().IonMobility.Mobility.Value, .001);
            AssertEx.AreEqual(expectedOffset2, result.GetIonMobilityInfo(key2).First().HighEnergyIonMobilityValueOffset.Value, .001);  // High energy offset

            // Deleting the msdata file prevents us from reading the raw IM data, expect an exception
            File.Delete(testFilesDir.GetTestPath(@"..\BlibDriftTimeTest\ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5)); // So we can't read raw IMS data
            transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(
                () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
            RunUI(() => transitionSettingsDlg.IonMobilityControl.IonMobilityFilterResolvingPower = resolvingPower);
            var driftTimePredictorDoomedDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg.IonMobilityControl.AddIonMobilityLibrary);

            libName += "_doomed";
            databasePath = TestFilesDir.GetTestPath(libName + IonMobilityDb.EXT);

            RunUI(() =>
            {
                driftTimePredictorDoomedDlg.LibraryName = libName;
                driftTimePredictorDoomedDlg.CreateDatabaseFile(databasePath); // Simulate user click on Create button
            });
            RunDlg<MessageDlg>(driftTimePredictorDoomedDlg.GetIonMobilitiesFromResults,
                messageDlg =>
                {
                    AssertEx.AreComparableStrings(
                        Resources.IonMobilityFinder_ProcessMSLevel_Failed_using_results_to_populate_ion_mobility_library_,
                        messageDlg.Message);
                    messageDlg.OkDialog();
                });

            RunUI(() => driftTimePredictorDoomedDlg.CancelDialog());
            WaitForClosedForm(driftTimePredictorDoomedDlg);
            RunUI(() =>
            {
                transitionSettingsDlg.OkDialog();
            });
            WaitForClosedForm(transitionSettingsDlg);
        }

    }
}
