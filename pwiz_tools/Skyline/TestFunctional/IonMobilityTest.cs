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
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class IonMobilityTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void IonMobilityFunctionalTest()
        {
            TestFilesZip = @"TestFunctional\IonMobilityTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip);

            // do a few unit tests
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

            // Now exercise the UI

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
            RunUI(() =>
            {
                driftTimePredictorDlg.SetResolvingPower(123.4);
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
                            new LibKey("ANELLINV", 2), out windowDT);
            Assert.AreEqual((4 * (119.2825783)) + 5, centerDriftTime);
            Assert.AreEqual(2 * ((4 * (119.2825783)) + 5)/123.4, windowDT);


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

    }
}
