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

        private ValidatingIonMobilityPeptide BuildValidatingIonMobilityPeptide(string seq, Adduct precursorAdduct,
            double ccs, double highEnergyDriftTimeOffsetMsec)
        {
            return new ValidatingIonMobilityPeptide(new Target(seq), precursorAdduct, ccs,
                highEnergyDriftTimeOffsetMsec);
        }

        protected override void DoTest()
        {
            using (var testFilesDir = new TestFilesDir(TestContext, TestFilesZip))
            {
                // Make sure we haven't forgotten to update anything if a new IMS type has been added
                foreach(eIonMobilityUnits units in Enum.GetValues(typeof(eIonMobilityUnits)))
                {
                    Assume.IsNotNull(IonMobilityFilter.IonMobilityUnitsL10NString(units));
                }

                const double HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC = -.1;

                // do a few unit tests on the UI error handlers
                TestGetIonMobilityDBErrorHandling(testFilesDir);
                TestImportIonMobilityFromSpectralLibraryErrorHandling();
                TestEditIonMobilityLibraryDlgErrorHandling();
                TestEditDriftTimePredictorDlgErrorHandling();

                // Now exercise the UI

                var goodPeptide = BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 133.3210342, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC);
                Assert.IsNull(goodPeptide.Validate());
                var badPeptides = new[]
                {
                    BuildValidatingIonMobilityPeptide("@#$!", Adduct.SINGLY_PROTONATED, 133.3210342, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 0, 0),
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, -133.3210342, -HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                };
                foreach (var badPeptide in badPeptides)
                {
                    Assert.IsNotNull(badPeptide.Validate());
                }

                var CCS_ANELLINVK = 119.2825783;
                var ionMobilityPeptides = new[]
                {
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 133.3210342, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),  // These are made-up values
                    BuildValidatingIonMobilityPeptide("SISIVGSYVGNR", Adduct.SINGLY_PROTONATED, 133.3210342, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("CCSDVFNQVVK", Adduct.SINGLY_PROTONATED, 131.2405487, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("CCSDVFNQVVK", Adduct.SINGLY_PROTONATED, 131.2405487, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("ANELLINVK", Adduct.SINGLY_PROTONATED, CCS_ANELLINVK, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("ANELLINVK", Adduct.SINGLY_PROTONATED, CCS_ANELLINVK, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EALDFFAR", Adduct.SINGLY_PROTONATED, 110.6867676, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EALDFFAR", Adduct.SINGLY_PROTONATED, 110.6867676, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("GVIFYESHGK", Adduct.SINGLY_PROTONATED, 123.7844632, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("GVIFYESHGK", Adduct.SINGLY_PROTONATED, 123.7844632, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EKDIVGAVLK", Adduct.SINGLY_PROTONATED, 124.3414249, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EKDIVGAVLK", Adduct.SINGLY_PROTONATED, 124.3414249, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("VVGLSTLPEIYEK", Adduct.SINGLY_PROTONATED, 149.857687, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("VVGLSTLPEIYEK", Adduct.SINGLY_PROTONATED, 149.857687, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("VVGLSTLPEIYEK", Adduct.SINGLY_PROTONATED, 149.857687, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("ANGTTVLVGMPAGAK", Adduct.SINGLY_PROTONATED, 144.7461979, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("ANGTTVLVGMPAGAK", Adduct.SINGLY_PROTONATED, 144.7461979, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("IGDYAGIK", Adduct.SINGLY_PROTONATED,  102.2694763, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("IGDYAGIK", Adduct.SINGLY_PROTONATED,  102.2694763, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("GDYAGIK", Adduct.SINGLY_PROTONATED,  91.09155861, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("GDYAGIK", Adduct.SINGLY_PROTONATED,  91.09155861, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("IFYESHGK", Adduct.SINGLY_PROTONATED, 111.2756406, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                    BuildValidatingIonMobilityPeptide("EALDFFAR", Adduct.SINGLY_PROTONATED, 110.6867676, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),
                };
                List<ValidatingIonMobilityPeptide> minimalSet;
                var message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(ionMobilityPeptides, out minimalSet); // Check for conflicts, strip out dupes
                Assert.IsNull(message, "known good data set failed import");
                Assert.AreEqual(11, minimalSet.Count, "known good data imported but with wrong result count");

                var save = ionMobilityPeptides[0].CollisionalCrossSection;
                ionMobilityPeptides[0].CollisionalCrossSection += 1.0; // Same sequence and charge, different cross section
                message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(ionMobilityPeptides, out minimalSet); // Check for conflicts, strip out dupes
                Assert.IsNotNull(message, message);
                Assert.IsNull(minimalSet, "bad inputs to drift time library paste should be rejected wholesale");
                ionMobilityPeptides[0].CollisionalCrossSection = save; // restore

                // Present the Prediction tab of the peptide settings dialog
                var peptideSettingsDlg1 = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));

                // Simulate picking "Add..." from the Ion Mobility Libraries button context menu
                var ionMobilityLibDlg1 = ShowDialog<EditIonMobilityLibraryDlg>(peptideSettingsDlg1.AddIonMobilityLibrary);
                // Simulate user pasting in collisional cross section data to create a new drift time library
                const string testlibName = "testlib";
                string databasePath = testFilesDir.GetTestPath(testlibName + IonMobilityDb.EXT);
                RunUI(() =>
                {
                    string libraryText = BuildPasteLibraryText(ionMobilityPeptides, seq => seq.Substring(0, seq.Length - 1));
                    ionMobilityLibDlg1.LibraryName = testlibName;
                    ionMobilityLibDlg1.CreateDatabase(databasePath);
                    SetClipboardText(libraryText);
                    ionMobilityLibDlg1.DoPasteLibrary();
                    ionMobilityLibDlg1.OkDialog();
                });
                WaitForClosedForm(ionMobilityLibDlg1);
                RunUI(peptideSettingsDlg1.OkDialog);
                WaitForClosedForm(peptideSettingsDlg1);

                // Use that drift time database in a differently named library
                const string testlibName2 = "testlib2";
                var peptideSettingsDlg2 = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
                // Simulate user picking Add... from the Drift Time Predictor combo control
                var driftTimePredictorDlg = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg2.AddDriftTimePredictor);
                // ... and reopening an existing drift time database
                var ionMobility = ShowDialog<EditIonMobilityLibraryDlg>(driftTimePredictorDlg.AddIonMobilityLibrary);
                RunUI(() =>
                {
                    ionMobility.LibraryName = testlibName2;
                    ionMobility.OpenDatabase(databasePath);
                    ionMobility.OkDialog();
                });
                WaitForClosedForm(ionMobility);

                // Set other parameters - name, resolving power, per-charge slope+intercept
                const string predictorName = "test";
                const double resolvingPower = 123.4;
                RunUI(() =>
                {
                    driftTimePredictorDlg.SetIonMobilityUnits(eIonMobilityUnits.drift_time_msec); 
                    driftTimePredictorDlg.SetResolvingPower(resolvingPower);
                    driftTimePredictorDlg.SetPredictorName(predictorName);
                    SetClipboardText("1\t2\t3\n2\t4\t5"); // Silly values: z=1 s=2 i=3, z=2 s=4 i=5
                    driftTimePredictorDlg.PasteRegressionValues();
                });
                var olddoc = SkylineWindow.Document;
                RunUI(() =>
                {
                    // Go back to the first library we created
                    driftTimePredictorDlg.ChooseIonMobilityLibrary(testlibName);
                    driftTimePredictorDlg.OkDialog();
                    var docUI = SkylineWindow.DocumentUI;
                    if (docUI != null)
                        SetUiDocument(docUI.ChangeSettings(docUI.Settings.ChangePeptideSettings(
                            docUI.Settings.PeptideSettings.ChangePrediction(
                                docUI.Settings.PeptideSettings.Prediction.ChangeDriftTimePredictor(driftTimePredictorDlg.Predictor)))));
                });

                WaitForClosedForm(driftTimePredictorDlg);
                RunUI(peptideSettingsDlg2.OkDialog);

                /*
             * Check that the database was created successfully
             * Check that it has the correct number peptides
             */
                IonMobilityDb db = IonMobilityDb.GetIonMobilityDb(databasePath, null);
                Assert.AreEqual(11, db.GetPeptides().Count());
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
                double windowDT;
                double driftTimeMax = 1000.0;
                var node = FindNodes("ANELLINV", Adduct.SINGLY_PROTONATED);
                IonMobilityAndCCS centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax, out windowDT);
                var slope = 2;
                var intercept = 3;
                Assert.AreEqual((slope * CCS_ANELLINVK) + intercept, centerIonMobility.IonMobility.Mobility);
                Assert.AreEqual(2 * ((slope * CCS_ANELLINVK) + intercept) / resolvingPower, windowDT);
                Assert.AreEqual((slope * CCS_ANELLINVK) + intercept + HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, centerIonMobility.GetHighEnergyDriftTimeMsec());

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

                // Check error cases for resolving power (caused unexpected exception)
                RunUI(() =>
                {
                    peptideSettingsUI.IsUseSpectralLibraryDriftTimes = true;
                    peptideSettingsUI.SpectralLibraryDriftTimeResolvingPower = null;

                });
                RunDlg<MessageDlg>(peptideSettingsUI.OkDialog, dlg =>
                {
                    AssertEx.AreComparableStrings(Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value, dlg.Message);
                    dlg.OkDialog();
                });
                RunUI(() => peptideSettingsUI.SpectralLibraryDriftTimeResolvingPower = 0);
                RunDlg<MessageDlg>(peptideSettingsUI.OkDialog, dlg =>
                {
                    Assert.AreEqual(Resources.EditDriftTimePredictorDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_, dlg.Message);
                    dlg.OkDialog();
                });

                RunUI(() => peptideSettingsUI.IsUseSpectralLibraryDriftTimes = false);

                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                WaitForDocumentLoaded(); // Let that library load

                // In this lib: ANGTTVLVGMPAGAK at z=2, with drift time 4.99820623749102
                // and, a single CCS value, for ANELLINVK, which is 3.8612432898618

                // Present the Prediction tab of the peptide settings dialog
                var peptideSettingsDlg3 = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
                // Simulate picking "Add..." from the Drift Time Predictor combo control
                var driftTimePredictorDlg3 = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg3.AddDriftTimePredictor);
                const double deadeelsDT = 3.456;
                const double deadeelsCCS = 345.67;
                const double deadeelsDTHighEnergyOffset = -0.1;
                var fourCols = "DEADEELS\t5\t" + deadeelsDT.ToString(CultureInfo.CurrentCulture) + "\t" +
                               deadeelsCCS.ToString(CultureInfo.CurrentCulture);
                var fiveCols = "DEADEELS\t5\t" + deadeelsDT.ToString(CultureInfo.CurrentCulture) + "\t" +
                               deadeelsCCS.ToString(CultureInfo.CurrentCulture) + "\t" +
                               deadeelsDTHighEnergyOffset.ToString(CultureInfo.CurrentCulture);
                RunUI(() =>
                {
                    driftTimePredictorDlg3.SetIonMobilityUnits(eIonMobilityUnits.drift_time_msec); 
                    driftTimePredictorDlg3.SetResolvingPower(resolvingPower);
                    driftTimePredictorDlg3.SetPredictorName("test3");
                    SetClipboardText("1\t2\t3\n2\t4\t5"); // Silly values: z=1 s=2 i=3, z=2 s=4 i=5
                    driftTimePredictorDlg3.PasteRegressionValues();
                    // Simulate user pasting in some measured drift time info without high energy offset, even though its enabled - should not throw
                    driftTimePredictorDlg3.SetOffsetHighEnergySpectraCheckbox(true);
                    SetClipboardText(fourCols);
                    driftTimePredictorDlg3.PasteMeasuredDriftTimes();
                    // Simulate user pasting in some measured drift time info with high energy offset
                    SetClipboardText(fiveCols);
                    driftTimePredictorDlg3.PasteMeasuredDriftTimes();
                    // Now turn off the high energy column and paste in five columns - should fail
                    driftTimePredictorDlg3.SetOffsetHighEnergySpectraCheckbox(false);
                    SetClipboardText(fiveCols);
                });
                // An error will appear because the column count is wrong
                ShowDialog<MessageDlg>(driftTimePredictorDlg3.PasteMeasuredDriftTimes);
                var errorDlg = WaitForOpenForm<MessageDlg>();
                Assert.AreEqual(string.Format(Resources.SettingsUIUtil_DoPasteText_Incorrect_number_of_columns__0__found_on_line__1__, 5, 1), errorDlg.Message);
                errorDlg.OkDialog();
                RunUI(() =>
                {
                    // And now paste in four columns, should be OK
                    SetClipboardText(fourCols);
                    driftTimePredictorDlg3.PasteMeasuredDriftTimes();

                    // Finally turn the high energy column back on, and put in a value
                    driftTimePredictorDlg3.SetOffsetHighEnergySpectraCheckbox(true);
                    SetClipboardText(fiveCols);
                    driftTimePredictorDlg3.PasteMeasuredDriftTimes();
                });
                // Simulate picking "Add..." from the Ion Mobility Library combo control
                var ionMobilityLibDlg3 = ShowDialog<EditIonMobilityLibraryDlg>(driftTimePredictorDlg3.AddIonMobilityLibrary);
                const string testlibName3 = "testlib3";
                string databasePath3 = testFilesDir.GetTestPath(testlibName3 + IonMobilityDb.EXT);
                RunUI(() =>
                {
                    ionMobilityLibDlg3.LibraryName = testlibName3;
                    ionMobilityLibDlg3.CreateDatabase(databasePath3);
                });
                // Simulate pressing "Import" button from the Edit Ion Mobility Library dialog
                var importSpectralLibDlg =
                    ShowDialog<ImportIonMobilityFromSpectralLibraryDlg>(ionMobilityLibDlg3.ImportFromSpectralLibrary);
                RunUI(() =>
                {
                    // Set up to fail - don't provide z=2 info
                    importSpectralLibDlg.Source = SpectralLibrarySource.settings; // Simulate user selecting 1st radio button
                    SetClipboardText("1\t1\t0"); // This will fail - no z=2 information
                    importSpectralLibDlg.PasteRegressionValues();
                });
                importSpectralLibDlg.BeginInvoke(new Action(importSpectralLibDlg.OkDialog)); // User clicks OK - we expect an error dialog to follow
                WaitForOpenForm<MessageDlg>().OkDialog(); // Dismiss the error message, we'll be dropped back into the dialog
                RunUI(() =>
                {
                    importSpectralLibDlg.Source = SpectralLibrarySource.file; // Simulate user selecting 2nd radio button
                    importSpectralLibDlg.FilePath = blibPath; // Simulate user entering filename
                    SetClipboardText("1\t1\t0\n2\t2\t2"); // Note non-unity slope and charge for z=2, for test purposes
                    importSpectralLibDlg.PasteRegressionValues();
                    importSpectralLibDlg.OkDialog();
                });
                WaitForClosedForm(importSpectralLibDlg);
                WaitForCondition(() => ionMobilityLibDlg3.LibraryPeptideCount > 8); // Let that library load
                RunUI(ionMobilityLibDlg3.OkDialog);
                WaitForClosedForm(ionMobilityLibDlg3);
                RunUI(() => driftTimePredictorDlg3.OkDialog());
                WaitForClosedForm(driftTimePredictorDlg3);
                RunUI(() => peptideSettingsDlg3.OkDialog());
                WaitForClosedForm(peptideSettingsDlg3);
                doc = WaitForDocumentChangeLoaded(doc); // Let that library load

                // Do some DT calculations with this new library
                node = FindNodes("ANELLINVK", Adduct.DOUBLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax, out windowDT);
                double ccs = 3.8612432898618; // should have imported CCS without any transformation
                Assert.AreEqual((4 * (ccs)) + 5, centerIonMobility.IonMobility.Mobility.Value, .000001);
                Assert.AreEqual(2 * ((4 * (ccs)) + 5) / resolvingPower, windowDT, .000001);
                node = FindNodes("ANGTTVLVGMPAGAK", Adduct.DOUBLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax, out windowDT);
                ccs = (4.99820623749102 - 2)/2; // should have imported CCS as a converted drift time
                Assert.AreEqual((4 * (ccs)) + 5, centerIonMobility.IonMobility.Mobility.Value, .000001);
                Assert.AreEqual(2 * ((4 * (ccs)) + 5) / resolvingPower, windowDT, .000001);

                // Do some DT calculations with the measured drift time
                node = FindNodes("DEADEELS", Adduct.TRIPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax, out windowDT); // Should fail
                Assert.AreEqual(windowDT, 0);
                Assert.IsFalse(centerIonMobility.IonMobility.HasValue);

                node = FindNodes("DEADEELS", Adduct.QUINTUPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax, out windowDT);
                Assert.AreEqual(deadeelsDT, centerIonMobility.IonMobility.Mobility.Value, .000001);
                Assert.AreEqual(deadeelsDT+deadeelsDTHighEnergyOffset, centerIonMobility.GetHighEnergyDriftTimeMsec() ?? -1, .000001);
                Assert.AreEqual(2 * (deadeelsDT / resolvingPower), windowDT, .0001); // Directly measured, should match

                // Now check handling of scenario where user pastes in high energy offsets then unchecks the "Use High Energy Offsets" box
                var peptideSettingsDlg4 = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
                // Simulate picking "Edit Current..." from the Drift Time Predictor combo control
                var driftTimePredictorDlg4 = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg4.EditDriftTimePredictor);
                RunUI(() =>
                {
                    Assert.IsTrue(driftTimePredictorDlg4.GetOffsetHighEnergySpectraCheckbox()); // Should start out enabled if we have offsets
                    driftTimePredictorDlg4.SetOffsetHighEnergySpectraCheckbox(false); // Turn off the high energy offset column
                    driftTimePredictorDlg4.SetPredictorName("test4");
                });
                RunUI(()=>driftTimePredictorDlg4.OkDialog());
                WaitForClosedForm(driftTimePredictorDlg4);
                RunUI(peptideSettingsDlg4.OkDialog);
                WaitForClosedForm(peptideSettingsDlg4);
                doc = WaitForDocumentChangeLoaded(doc);
                node = FindNodes("DEADEELS", Adduct.QUINTUPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                    node.NodePep, node.NodeGroup, null, null, driftTimeMax, out windowDT);
                Assert.AreEqual(deadeelsDT, centerIonMobility.IonMobility.Mobility.Value, .000001);
                Assert.AreEqual(deadeelsDT, centerIonMobility.GetHighEnergyDriftTimeMsec() ?? -1, .000001); // High energy value should now be same as low energy value
                Assert.AreEqual(2 * (deadeelsDT / resolvingPower), windowDT, .0001); // Directly measured, should match

                // Now make sure that high energy checkbox initial state is as we expect
                var peptideSettingsDlg5 = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
                // Simulate picking "Edit Current..." from the Drift Time Predictor combo control
                var driftTimePredictorDlg5 = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg5.EditDriftTimePredictor);
                RunUI(() => Assert.IsFalse(driftTimePredictorDlg5.GetOffsetHighEnergySpectraCheckbox()));
                RunUI(driftTimePredictorDlg5.CancelDialog);
                WaitForClosedForm(driftTimePredictorDlg5);
                OkDialog(peptideSettingsDlg5, () => peptideSettingsDlg5.OkDialog());

                // Try it with linear range instead of resolving power
                var peptideSettingsDlg6 = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
                // Simulate picking "Edit Current..." from the Drift Time Predictor combo control
                var driftTimePredictorDlg6 = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg6.EditDriftTimePredictor);
                RunUI(() => driftTimePredictorDlg6.SetLinearRangeCheckboxState(true));
                var widthAtDtZero = 10;
                var widthAtDtMax = 1000;
                RunUI(() => driftTimePredictorDlg6.SetWidthAtDtZero(widthAtDtZero));
                RunUI(() => driftTimePredictorDlg6.SetWidthAtDtMax(widthAtDtMax));
                RunUI(() => driftTimePredictorDlg6.SetPredictorName("test6"));
                OkDialog(driftTimePredictorDlg6, () => driftTimePredictorDlg6.OkDialog());
                OkDialog(peptideSettingsDlg6, () => peptideSettingsDlg6.OkDialog());
                doc = WaitForDocumentChangeLoaded(doc);
                node = FindNodes("DEADEELS", Adduct.QUINTUPLY_PROTONATED);
                centerIonMobility = doc.Settings.GetIonMobilityHelper(
                     node.NodePep, node.NodeGroup, null, null, driftTimeMax, out windowDT);
                Assert.AreEqual(deadeelsDT, centerIonMobility.IonMobility.Mobility.Value, .000001);
                Assert.AreEqual(deadeelsDT, centerIonMobility.GetHighEnergyDriftTimeMsec() ?? -1, .000001); // High energy value should now be same as low energy value
                Assert.AreEqual(widthAtDtZero + deadeelsDT * (widthAtDtMax - widthAtDtZero) / driftTimeMax, windowDT, .0001); 

            }
            TestMeasuredDriftTimes();
        }

        private void SetUiDocument(SrmDocument newDocument)
        {
            RunUI(() => Assert.IsTrue(SkylineWindow.SetDocument(newDocument, SkylineWindow.DocumentUI)));
        }

        private static string BuildPasteLibraryText(IEnumerable<ValidatingIonMobilityPeptide> peptides, Func<string, string> adjustSeq)
        {
            var pasteBuilder = new StringBuilder();
            foreach (var peptide in peptides)
            {
                pasteBuilder.Append(adjustSeq(peptide.Target.ToString()))
                    .Append('\t')
                    .Append(peptide.CollisionalCrossSection)
                    .Append('\t')
                    .Append(peptide.HighEnergyDriftTimeOffsetMsec)
                    .AppendLine();
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

            const string badfilename = "nonexistent_file.imdb";
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
            Assert.AreEqual(message,
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

        /// <summary>
        /// Test various error conditions in EditIonMobilityLibraryDlg.cs,
        /// mostly around handling conflicting values
        /// </summary>
        public void TestEditIonMobilityLibraryDlgErrorHandling()
        {
            var peptides = new List<ValidatingIonMobilityPeptide>();
            var peptideSet = new List<ValidatingIonMobilityPeptide>();

            const string seq = "JKLMN";
            const double HIGH_ENERGY_DRIFT_OFFSET_MSEC = -0.5;
            peptides.Add(BuildValidatingIonMobilityPeptide(seq, Adduct.SINGLY_PROTONATED, 1, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            peptides.Add(BuildValidatingIonMobilityPeptide(seq, Adduct.SINGLY_PROTONATED, 1, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            var message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            Assert.IsNull(message);
            Assert.AreEqual(1, peptideSet.Count);

            peptides[1].CollisionalCrossSection = 1.1;
            message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            AssertEx.Contains(message,
                string.Format(
                    Resources
                        .EditIonMobilityLibraryDlg_ValidateUniqueChargedPeptides_The_peptide__0__has_inconsistent_ion_mobility_values_in_the_added_list_,
                    peptides[1].GetLibKey()));

            string seqB = seq + "L";
            peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, 1.1, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, 1.2, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            AssertEx.Contains(message,
                Resources
                    .EditIonMobilityLibraryDlg_ValidateUniqueChargedPeptides_The_following_peptides_appear_in_the_added_list_with_inconsistent_ion_mobility_values_);

            for (int n = 0; n < 20; n++)
            {
                seqB = seqB + "M";
                peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, n, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
                peptides.Add(BuildValidatingIonMobilityPeptide(seqB, Adduct.SINGLY_PROTONATED, n + 1, HIGH_ENERGY_DRIFT_OFFSET_MSEC));
            }
            message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            AssertEx.Contains(message,
                string.Format(Resources.EditIonMobilityLibraryDlg_ValidateUniqueChargedPeptides_The_added_list_contains__0__charged_peptides_with_inconsistent_ion_mobility_values_,
                                        22));
        }

        /// <summary>
        /// Test various error conditions in EditDriftTimePredictorDlg.cs
        /// </summary>
        public void TestEditDriftTimePredictorDlgErrorHandling()
        {
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateResolvingPower(0), Resources.EditDriftTimePredictorDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_);
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateResolvingPower(-1), Resources.EditDriftTimePredictorDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_);
            Assert.IsNull(EditDriftTimePredictorDlg.ValidateResolvingPower(1));

            AssertEx.Contains(MeasuredDriftTimeTable.ValidateCharge(Adduct.FromChargeProtonated(0)),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                0, TransitionGroup.MAX_PRECURSOR_CHARGE));
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateCharge(Adduct.FromChargeProtonated(99)),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                99, TransitionGroup.MAX_PRECURSOR_CHARGE));
            string[] dtValues = { null, null, null, null, null };
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(new[] { "", "" }),
                Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_pasted_text_must_have_three_columns_);
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_A_modified_peptide_sequence_is_required_for_each_entry_);
            dtValues[EditDriftTimePredictorDlg.COLUMN_SEQUENCE] = "$%$%!";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, dtValues[0]));
            dtValues[EditDriftTimePredictorDlg.COLUMN_SEQUENCE] = "JKLM";
            dtValues[EditDriftTimePredictorDlg.COLUMN_CHARGE] = "dog";
            dtValues[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY] = "-0.2";
            dtValues[EditDriftTimePredictorDlg.COLUMN_CCS] = "1";
            dtValues[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET] = "";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                    dtValues[EditDriftTimePredictorDlg.COLUMN_CHARGE].Trim(), TransitionGroup.MAX_PRECURSOR_CHARGE));
            dtValues[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY] = (17.9).ToString(CultureInfo.CurrentCulture);
            dtValues[EditDriftTimePredictorDlg.COLUMN_CHARGE] = "2";
            Assert.IsNull(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues), 
                string.Format("unexpected error {0}", MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues)));
            dtValues[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY] = "fish";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_drift_time_, dtValues[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY].Trim()));
            dtValues[EditDriftTimePredictorDlg.COLUMN_ION_MOBILITY] = (17.9).ToString(CultureInfo.CurrentCulture);
            dtValues[EditDriftTimePredictorDlg.COLUMN_CCS] = "fish";
            dtValues[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET] = "-.3";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_collisional_cross_section_, dtValues[EditDriftTimePredictorDlg.COLUMN_CCS].Trim()));
            dtValues[EditDriftTimePredictorDlg.COLUMN_CCS] = "123";
            dtValues[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET] = "dog";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_high_energy_offset_, dtValues[EditDriftTimePredictorDlg.COLUMN_HIGH_ENERGY_OFFSET].Trim()));

            AssertEx.Contains(ChargeRegressionTable.ValidateCharge(0),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                0, TransitionGroup.MAX_PRECURSOR_CHARGE));
            AssertEx.Contains(ChargeRegressionTable.ValidateCharge(99),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                99, TransitionGroup.MAX_PRECURSOR_CHARGE));
            Assert.IsNull(ChargeRegressionTable.ValidateCharge(1));
            Assert.IsNull(ChargeRegressionTable.ValidateCharge(TransitionGroup.MAX_PRECURSOR_CHARGE));

            string[] values = { "", "", "" };
            AssertEx.Contains(ChargeRegressionTable.ValidateRegressionCellValues(values),
                string.Format(
                Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_charge__Charges_must_be_integer_values_between_1_and__1__,
                values[0], TransitionGroup.MAX_PRECURSOR_CHARGE));

            values[0] = "1";
            AssertEx.Contains(ChargeRegressionTable.ValidateRegressionCellValues(values),
                string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_slope_, values[1]));

            values[1] = "1";
            AssertEx.Contains(ChargeRegressionTable.ValidateRegressionCellValues(values),
                string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_intercept_, values[2]));

            values[2] = "1";
            Assert.IsNull(ChargeRegressionTable.ValidateRegressionCellValues(values));

            object[] column = { "1" };
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(column, 1),
               Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_pasted_text_must_have_at_least_two_columns_);

            object[] columns = { "", "", "" };
            const int lineNumber = 1;
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber),
               string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_peptide_sequence_on_line__0__, lineNumber));

            columns[0] = "@#%!";
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber),
               string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_text__0__is_not_a_valid_peptide_sequence_on_line__1__, columns[0], lineNumber));

            columns[0] = "JKLM";
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber),
               string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Missing_collisional_cross_section_value_on_line__0__, lineNumber));

            columns[1] = "fish";
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber),
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Invalid_number_format__0__for_collisional_cross_section_on_line__1__,
                            columns[1], lineNumber));

            columns[1] = "0";
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber),
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_collisional_cross_section__0__must_be_greater_than_zero_on_line__1__,
                                columns[1], lineNumber));

            columns[1] = "1";
            Assert.IsNull(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber));

            columns[2] = "zeke"; // HighEnergyDriftTimeOffsetMsec
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber),
                string.Format(Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_Invalid_number_format__0__for_high_energy_drift_time_offset_on_line__1__,
                                columns[2], lineNumber));

            columns[2] = ""; // HighEnergyDriftTimeOffsetMsec
            Assert.IsNull(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber));

            columns[2] = "1"; // HighEnergyDriftTimeOffsetMsec (usually negative, but we don't demand it)
            Assert.IsNull(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(columns, lineNumber));


            var pep = BuildValidatingIonMobilityPeptide(string.Empty, Adduct.EMPTY, 0, 0);
            AssertEx.Contains(pep.Validate(), Resources.ValidatingIonMobilityPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry_);

            const string seq = "@#%!";
            pep = BuildValidatingIonMobilityPeptide(seq, Adduct.EMPTY, 0, 0);
            AssertEx.Contains(pep.Validate(), string.Format(
                Resources.ValidatingIonMobilityPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, seq));

            pep = BuildValidatingIonMobilityPeptide("JLKM", Adduct.EMPTY, 0, 0);
            AssertEx.Contains(pep.Validate(),
                Resources.ValidatingIonMobilityPeptide_ValidateCollisionalCrossSection_Measured_collisional_cross_section_values_must_be_valid_decimal_numbers_greater_than_zero_);

            pep = BuildValidatingIonMobilityPeptide("JLKM", Adduct.EMPTY, 1, 0);
            AssertEx.Contains(pep.Validate(),
                Resources.ValidatingIonMobilityPeptide_ValidateAdduct_A_valid_adduct_description__e_g____M_H____must_be_provided_);

            pep = BuildValidatingIonMobilityPeptide("JLKM", Adduct.SINGLY_PROTONATED, 1, 0);
            Assert.IsNull(pep.Validate());

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
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(
                () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));

            // Simulate user picking Add... from the Drift Time Predictor combo control
            var driftTimePredictorDlg = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg.AddDriftTimePredictor);
            const string predictorName = "TestMeasuredDriftTimes";
            const double resolvingPower = 123.4;
            RunUI(() =>
            {
                driftTimePredictorDlg.SetIonMobilityUnits(eIonMobilityUnits.drift_time_msec); 
                driftTimePredictorDlg.SetResolvingPower(resolvingPower);
                driftTimePredictorDlg.SetPredictorName(predictorName);
                driftTimePredictorDlg.SetOffsetHighEnergySpectraCheckbox(true);
                driftTimePredictorDlg.GetDriftTimesFromResults();
                driftTimePredictorDlg.OkDialog();
            });
            WaitForClosedForm(driftTimePredictorDlg);
            var pepSetDlg = peptideSettingsDlg;
            RunUI(() =>
            {
                pepSetDlg.OkDialog();
            });
            WaitForClosedForm(peptideSettingsDlg);
            doc = WaitForDocumentChange(doc);
            
            var result = doc.Settings.PeptideSettings.Prediction.IonMobilityPredictor.MeasuredMobilityIons;
            Assert.AreEqual(2, result.Count);
            var key3 = new LibKey("GLAGVENVTELKK", Adduct.TRIPLY_PROTONATED);
            var key2 = new LibKey("GLAGVENVTELKK", Adduct.DOUBLY_PROTONATED);
            const double expectedDT3= 4.0709;
            const double expectedOffset3 = 0.8969;
            Assert.AreEqual(expectedDT3, result[key3].IonMobility.Mobility.Value, .001);
            Assert.AreEqual(expectedOffset3, result[key3].HighEnergyIonMobilityValueOffset, .001); // High energy offset
            const double expectedDT2 = 5.5889;
            const double expectedOffset2 = -1.1039;
            Assert.AreEqual(expectedDT2, result[key2].IonMobility.Mobility.Value, .001);
            Assert.AreEqual(expectedOffset2, result[key2].HighEnergyIonMobilityValueOffset, .001);  // High energy offset
            var doc2 = WaitForDocumentLoaded();

            // Reimport with these new settings, then export a spectral library and verify it got IMS data
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                var chromatograms = doc2.Settings.MeasuredResults.Chromatograms;
                dlg.SelectedChromatograms = new[] { chromatograms[0] };
                dlg.ReimportResults();
                dlg.OkDialog();
            });
            WaitForDocumentLoaded();
            var progress = new SilentProgressMonitor();
            var exported = testFilesDir.GetTestPath("export.blib");
            new SpectralLibraryExporter(SkylineWindow.Document, SkylineWindow.DocumentFilePath)
                .ExportSpectralLibrary(exported, progress);
            var refSpectra = GetRefSpectra(exported);
            Assert.IsTrue(refSpectra.All(r => (r.IonMobility??0) > 0));

            // Verify exception handling by deleting the msdata file
            File.Delete(testFilesDir.GetTestPath(@"..\BlibDriftTimeTest\ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5));
            peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(
                            () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
            var driftTimePredictorDoomedDlg = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg.AddDriftTimePredictor);
            RunUI(() =>
            {
                driftTimePredictorDoomedDlg.SetIonMobilityUnits(eIonMobilityUnits.drift_time_msec); 
                driftTimePredictorDoomedDlg.SetResolvingPower(resolvingPower);
                driftTimePredictorDoomedDlg.SetPredictorName(predictorName+"_doomed");
            });
            RunDlg<MessageDlg>(driftTimePredictorDoomedDlg.GetDriftTimesFromResults,
                messageDlg =>
                {
                    AssertEx.AreComparableStrings(
                        Resources.DriftTimeFinder_HandleLoadScanException_Problem_using_results_to_populate_drift_time_library__,
                        messageDlg.Message);
                    messageDlg.OkDialog();
                });

            RunUI(() => driftTimePredictorDoomedDlg.CancelDialog());
            WaitForClosedForm(driftTimePredictorDoomedDlg);
            RunUI(() =>
            {
                peptideSettingsDlg.OkDialog();
            });
            WaitForClosedForm(peptideSettingsDlg);
        }
    }
}
