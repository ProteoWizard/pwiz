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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class IonMobilityTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestIonMobility()
        {
            TestFilesZip = @"TestFunctional\IonMobilityTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            using (var testFilesDir = new TestFilesDir(TestContext, TestFilesZip))
            {
                // do a few unit tests on the UI error handlers
                TestGetIonMobilityDBErrorHandling(testFilesDir);
                TestImportIonMobilityFromSpectralLibraryErrorHandling();
                TestEditIonMobilityLibraryDlgErrorHandling();
                TestEditDriftTimePredictorDlgErrorHandling();

                // Now exercise the UI

                var goodPeptide = new ValidatingIonMobilityPeptide("SISIVGSYVGNR", 133.3210342);
                Assert.IsNull(goodPeptide.Validate());
                var badPeptides = new[]
                {
                    new ValidatingIonMobilityPeptide("@#$!", 133.3210342),
                    new ValidatingIonMobilityPeptide("SISIVGSYVGNR", 0),
                    new ValidatingIonMobilityPeptide("SISIVGSYVGNR", -133.3210342),
                };
                foreach (var badPeptide in badPeptides)
                {
                    Assert.IsNotNull(badPeptide.Validate());
                }

                var ionMobilityPeptides = new[]
                {
                    new ValidatingIonMobilityPeptide("SISIVGSYVGNR",133.3210342),  // These are made-up values
                    new ValidatingIonMobilityPeptide("SISIVGSYVGNR",133.3210342),
                    new ValidatingIonMobilityPeptide("CCSDVFNQVVK",131.2405487),
                    new ValidatingIonMobilityPeptide("CCSDVFNQVVK",131.2405487),
                    new ValidatingIonMobilityPeptide("ANELLINVK",119.2825783),
                    new ValidatingIonMobilityPeptide("ANELLINVK",119.2825783),
                    new ValidatingIonMobilityPeptide("EALDFFAR",110.6867676),
                    new ValidatingIonMobilityPeptide("EALDFFAR",110.6867676),
                    new ValidatingIonMobilityPeptide("GVIFYESHGK",123.7844632),
                    new ValidatingIonMobilityPeptide("GVIFYESHGK",123.7844632),
                    new ValidatingIonMobilityPeptide("EKDIVGAVLK",124.3414249),
                    new ValidatingIonMobilityPeptide("EKDIVGAVLK",124.3414249),
                    new ValidatingIonMobilityPeptide("VVGLSTLPEIYEK",149.857687),
                    new ValidatingIonMobilityPeptide("VVGLSTLPEIYEK",149.857687),
                    new ValidatingIonMobilityPeptide("VVGLSTLPEIYEK",149.857687),
                    new ValidatingIonMobilityPeptide("ANGTTVLVGMPAGAK",144.7461979),
                    new ValidatingIonMobilityPeptide("ANGTTVLVGMPAGAK",144.7461979),
                    new ValidatingIonMobilityPeptide("IGDYAGIK", 102.2694763),
                    new ValidatingIonMobilityPeptide("IGDYAGIK", 102.2694763),
                    new ValidatingIonMobilityPeptide("GDYAGIK", 91.09155861),
                    new ValidatingIonMobilityPeptide("GDYAGIK", 91.09155861),
                    new ValidatingIonMobilityPeptide("IFYESHGK",111.2756406),
                    new ValidatingIonMobilityPeptide("EALDFFAR",110.6867676),
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
                double? centerDriftTime = doc.Settings.PeptideSettings.Prediction.GetDriftTime(
                    new LibKey("ANELLINV", 2), null, out windowDT);
                Assert.AreEqual((4 * (119.2825783)) + 5, centerDriftTime);
                Assert.AreEqual(2 * ((4 * (119.2825783)) + 5)/resolvingPower, windowDT);

                //
                // Test importing collisional cross sections from a spectral lib that has drift times
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
                RunUI(() =>
                {
                    peptideSettingsUI.PickedLibraries = new[] { libname }; 
                    peptideSettingsUI.OkDialog();
                });
                WaitForClosedForm(peptideSettingsUI);
                WaitForDocumentLoaded(); // Let that library load

                // In this lib: ANGTTVLVGMPAGAK at z=2, with drift time 4.99820623749102
                // and, a single CCS value, for ANELLINVK, which is 3.8612432898618

                // Present the Prediction tab of the peptide settings dialog
                var peptideSettingsDlg3 = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
                // Simulate picking "Add..." from the Drift Time Predictor combo control
                var driftTimePredictorDlg3 = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg3.AddDriftTimePredictor);
                const double deadeelsDT = 3.456;
                RunUI(() =>
                {
                    driftTimePredictorDlg3.SetResolvingPower(resolvingPower);
                    driftTimePredictorDlg3.SetPredictorName("test3");
                    SetClipboardText("1\t2\t3\n2\t4\t5"); // Silly values: z=1 s=2 i=3, z=2 s=4 i=5
                    driftTimePredictorDlg3.PasteRegressionValues();
                    // Simulate user pasting in some measured drift time info
                    SetClipboardText("DEADEELS\t5\t" + deadeelsDT.ToString(CultureInfo.CurrentCulture));
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
                RunUI(driftTimePredictorDlg3.OkDialog);
                WaitForClosedForm(driftTimePredictorDlg3);
                RunUI(peptideSettingsDlg3.OkDialog);
                WaitForClosedForm(peptideSettingsDlg3);
                doc = WaitForDocumentChangeLoaded(doc); // Let that library load

                // Do some DT calculations with this new library
                centerDriftTime = doc.Settings.PeptideSettings.Prediction.GetDriftTime(
                    new LibKey("ANELLINVK", 2), null, out windowDT);
                double ccs = 3.8612432898618; // should have imported CCS without any transformation
                Assert.AreEqual((4 * (ccs)) + 5, centerDriftTime ?? ccs, .000001);
                Assert.AreEqual(2 * ((4 * (ccs)) + 5) / resolvingPower, windowDT, .000001);
                centerDriftTime = doc.Settings.PeptideSettings.Prediction.GetDriftTime(
                    new LibKey("ANGTTVLVGMPAGAK", 2), null, out windowDT);
                ccs = (4.99820623749102 - 2)/2; // should have imported CCS as a converted drift time
                Assert.AreEqual((4 * (ccs)) + 5, centerDriftTime ?? ccs, .000001);
                Assert.AreEqual(2 * ((4 * (ccs)) + 5) / resolvingPower, windowDT, .000001);

                // Do some DT calculations with the measured drift time
                centerDriftTime = doc.Settings.PeptideSettings.Prediction.GetDriftTime(
                    new LibKey("DEADEELS", 3), null, out windowDT); // Should fail
                Assert.AreEqual(windowDT, 0);
                Assert.IsFalse(centerDriftTime.HasValue);

                centerDriftTime = doc.Settings.PeptideSettings.Prediction.GetDriftTime(
                    new LibKey("DEADEELS", 5), null, out windowDT);
                Assert.AreEqual(deadeelsDT, centerDriftTime ?? -1, .000001);
                Assert.AreEqual(2 * (deadeelsDT / resolvingPower), windowDT, .0001); // Directly measured, should match
            }
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
                pasteBuilder.Append(adjustSeq(peptide.Sequence))
                    .Append('\t')
                    .Append(peptide.CollisionalCrossSection)
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
            peptides.Add(new ValidatingIonMobilityPeptide(seq, 1));
            peptides.Add(new ValidatingIonMobilityPeptide(seq, 1));
            var message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            Assert.IsNull(message);
            Assert.AreEqual(1, peptideSet.Count);

            peptides[1].CollisionalCrossSection = 1.1;
            message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            AssertEx.Contains(message,
                string.Format(
                    Resources
                        .EditIonMobilityLibraryDlg_ValidateUniqueChargedPeptides_The_peptide__0__has_inconsistent_ion_mobility_values_in_the_added_list_,
                    peptides[1].PeptideModSeq));

            string seqB = seq + "L";
            peptides.Add(new ValidatingIonMobilityPeptide(seqB, 1.1));
            peptides.Add(new ValidatingIonMobilityPeptide(seqB, 1.2));
            message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            AssertEx.Contains(message,
                Resources
                    .EditIonMobilityLibraryDlg_ValidateUniqueChargedPeptides_The_following_peptides_appear_in_the_added_list_with_inconsistent_ion_mobility_values_);

            for (int n = 0; n < 20; n++)
            {
                seqB = seqB + "M";
                peptides.Add(new ValidatingIonMobilityPeptide(seqB, n));
                peptides.Add(new ValidatingIonMobilityPeptide(seqB, n + 1));
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

            AssertEx.Contains(MeasuredDriftTimeTable.ValidateCharge(0),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                0, TransitionGroup.MAX_PRECURSOR_CHARGE));
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateCharge(99),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                99, TransitionGroup.MAX_PRECURSOR_CHARGE));
            string[] dtValues = { null, null, null };
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(new[] { "", "" }),
                Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_pasted_text_must_have_three_columns_);
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_A_modified_peptide_sequence_is_required_for_each_entry_);
            dtValues[0] = "$%$%!";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, dtValues[0]));
            dtValues[0] = "JKLM";
            dtValues[1] = "dog";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                    dtValues[EditDriftTimePredictorDlg.COLUMN_CHARGE].Trim(), TransitionGroup.MAX_PRECURSOR_CHARGE));
            dtValues[2] = (17.9).ToString(CultureInfo.CurrentCulture);
            dtValues[1] = "2";
            Assert.IsNull(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues), 
                string.Format("unexpected error {0}", MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues)));
            dtValues[2] = "fish";
            AssertEx.Contains(MeasuredDriftTimeTable.ValidateMeasuredDriftTimeCellValues(dtValues),
                String.Format(Resources.MeasuredDriftTimeTable_ValidateMeasuredDriftTimeCellValues_The_value__0__is_not_a_valid_drift_time_, dtValues[EditDriftTimePredictorDlg.COLUMN_DRIFT_TIME_MSEC].Trim()));

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
               Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_pasted_text_must_have_two_columns_);

            object[] columns = { "", "" };
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

            var pep = new ValidatingIonMobilityPeptide(null, 0);
            AssertEx.Contains(pep.Validate(), Resources.ValidatingIonMobilityPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry_);

            const string seq = "@#%!";
            pep = new ValidatingIonMobilityPeptide(seq, 0);
            AssertEx.Contains(pep.Validate(), string.Format(
                Resources.ValidatingIonMobilityPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, seq));

            pep = new ValidatingIonMobilityPeptide("JLKM", 0);
            AssertEx.Contains(pep.Validate(),
                Resources.ValidatingIonMobilityPeptide_ValidateCollisionalCrossSection_Measured_collisional_cross_section_values_must_be_valid_decimal_numbers_greater_than_zero_);

            pep = new ValidatingIonMobilityPeptide("JLKM", 1);
            Assert.IsNull(pep.Validate());

        }



    }
}
