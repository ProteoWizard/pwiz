/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PasteMoleculesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPasteMolecules()
        {
            TestFilesZip = @"TestFunctional\PasteMoleculeTest.zip";
            RunFunctionalTest();
        }


        private void TestError(string clipText, string errText)
        {
            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            RunUI(() =>
            {
                pasteDlg.IsMolecule = true;
            });
            SetClipboardTextUI(clipText);
            RunUI(pasteDlg.PasteTransitions);
            RunUI(pasteDlg.ValidateCells);
            WaitForConditionUI(() => pasteDlg.ErrorText != null);
            RunUI(() => Assert.IsTrue(pasteDlg.ErrorText.Contains(errText), string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"", errText, pasteDlg.ErrorText)));
            if (string.IsNullOrEmpty(errText))
                RunUI(pasteDlg.OkDialog);  // We expect this to work, go ahead and load it
            else
                RunUI(pasteDlg.CancelDialog);
            WaitForClosedForm(pasteDlg);
        }

        protected override void DoTest()
        {
            const double precursorMzAtZNeg2 = 242.0373281;
            const double productMzAtZNeg2 = 213.5097436;
            const double precursorCE = 1.23;
            const double precursorDT = 2.34;
            const double highEnergyDtOffset = -.012;
            const double precursorRT = 3.45;
            const double productDT = precursorDT + highEnergyDtOffset;

            var docEmpty = SkylineWindow.Document;

            string line1 = "MyMolecule\tMyMol\tMyFrag\tC34H12O4\tC34H3O\t" + precursorMzAtZNeg2 + "\t" + productMzAtZNeg2 + "\t-2\t-2\t" + precursorRT + "\t" + precursorCE + "\t" + precursorDT + "\t" + productDT; // Legit
            const string line2start = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCH3O\t";
            const string line3 = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCHH500000000\t\t\t1\t1";

            // Provoke some errors
            var badcharge = Transition.MAX_PRODUCT_CHARGE + 1;
            TestError(line1 + line2start + "\t\t1\t" + badcharge, // Excessively large charge for product
                String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__, 
                badcharge, -Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE));
            badcharge = 120;
            TestError(line1 + line2start + "\t\t" + badcharge + "\t1", // Insanely large charge for precursor
                String.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                badcharge, -TransitionGroup.MAX_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
            TestError(line1 + line2start + "\t\t1\t", // No mz or charge for product
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 2));
            TestError(line1 + line2start + "19\t5", // Precursor Formula and m/z don't make sense together
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_formula_and_m_z_value_do_not_agree_for_any_charge_state_, 2));
            TestError(line1 + line2start + "\t5\t1", // Product Formula and m/z don't make sense together
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_, 2));
            TestError(line1 + line2start + "\t", // No mz or charge for precursor or product
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 2));
            TestError(line1 + line3, // Insanely large molecule
                string.Format(Resources.CustomIon_Validate_The_mass_of_the_custom_ion_exceeeds_the_maximum_of__0_, CustomIon.MAX_MASS));
            // Take a legit full paste and mess with each field in turn
            string[] fields = { "MyMol", "MyPrecursor", "MyProduct", "C12H9O4", "C6H4O2", "217.049535420091", "108.020580420091", "1", "1", "123", "25", "7", "9" };
            string[] badfields = { "", "", "", "123", "123", "fish", "-345", "cat", "pig", "frog", "hamster", "boston", "foosball" };
            string[] expectedErrors =
            {
                Resources.PasteDlg_ShowNoErrors_No_errors, Resources.PasteDlg_ShowNoErrors_No_errors, Resources.PasteDlg_ShowNoErrors_No_errors,  // No name, no problem
                BioMassCalc.MONOISOTOPIC.FormatArgumentExceptionMessage(badfields[3]),
                BioMassCalc.MONOISOTOPIC.FormatArgumentExceptionMessage(badfields[4]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_, badfields[5]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_,  badfields[6]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_,  badfields[7]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_,  badfields[8]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_retention_time_value__0_,  badfields[9]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_collision_energy_value__0_,  badfields[10]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_value__0_,  badfields[11]),
                string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_value__0_,  badfields[12]),
                Resources.PasteDlg_ShowNoErrors_No_errors // N+1'th pass is unadulterated
            };
            for (int bad = 0; bad <= fields.Count(); bad++)
            {
                var line = "";
                for (var f = 0; f < fields.Count(); f++)
                    line += ((bad == f) ? badfields[f] : fields[f]) + "\t";
                TestError(line, expectedErrors[bad]);
            }

            // Now load the document with a legit paste
            TestError(line1+line2start+"\t\t1\t1", String.Empty); 
            var docOrig = WaitForDocumentChange(docEmpty);
            var testTransitionGroups = docOrig.MoleculeTransitionGroups.ToArray();
            Assert.AreEqual(2, testTransitionGroups.Count());
            var transitionGroup = testTransitionGroups[0];
            var precursor = docOrig.Molecules.First();
            var product = transitionGroup.Transitions.First();
            Assert.AreEqual(precursorCE, transitionGroup.ExplicitValues.CollisionEnergy);
            Assert.AreEqual(precursorDT, transitionGroup.ExplicitValues.DriftTimeMsec);
            Assert.AreEqual(highEnergyDtOffset, transitionGroup.ExplicitValues.DriftTimeHighEnergyOffsetMsec.Value, 1E-7);
            Assert.AreEqual(precursorRT, precursor.ExplicitRetentionTime);
            Assert.AreEqual(precursorMzAtZNeg2, BioMassCalc.CalculateIonMz(precursor.Peptide.CustomIon.MonoisotopicMass, product.Transition.Group.PrecursorCharge), 1E-7);
            Assert.AreEqual(productMzAtZNeg2, BioMassCalc.CalculateIonMz(product.GetIonMass(), product.Transition.Charge), 1E-7);
            // Does that produce the expected transition list file?
            TestTransitionListOutput(docOrig, "PasteMoleculeTinyTest.csv", "PasteMoleculeTinyTestExpected.csv", ExportFileType.IsolationList);
            // Does serialization of imported values work properly?
            AssertEx.Serializable(docOrig);

            // Reset
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                docOrig = SkylineWindow.Document;
            });

            // Now a proper user data set
            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            PasteDlg dlg = pasteDlg;
            RunUI(() =>
            {
                dlg.IsMolecule = true;
                dlg.SetSmallMoleculeColumns(null); // Reset column headers to default selection and order
            });
            // Formerly SetExcelFileClipboardText(TestFilesDir.GetTestPath("MoleculeTransitionList.xlsx"),"sheet1",6,false); but TeamCity doesn't like that
            SetCsvFileClipboardText(TestFilesDir.GetTestPath("MoleculeTransitionList.csv")); 
            RunUI(pasteDlg.PasteTransitions);
            OkDialog(pasteDlg,pasteDlg.OkDialog);
            var pastedDoc = WaitForDocumentChange(docOrig);
            AssertEx.Serializable(pastedDoc);
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("PasteMolecules.Sky")));
            
            var paths = new []
            {
                "13417_02_WAA283_3805_071514"+ExtensionTestContext.ExtWatersRaw,
                "13417_03_WAA283_3805_071514"+ExtensionTestContext.ExtWatersRaw,
                "13418_02_WAA283_3805_071514"+ExtensionTestContext.ExtWatersRaw,
                "13418_03_WAA283_3805_071514"+ExtensionTestContext.ExtWatersRaw,
            };
            var doc = SkylineWindow.Document;
            RunUI(() => SkylineWindow.ChangeSettings(doc.Settings.
                ChangeTransitionSettings(
                    doc.Settings.TransitionSettings.ChangeInstrument(
                        doc.Settings.TransitionSettings.Instrument.ChangeMzMatchTolerance(0.6))), true));
            ImportResults(paths);
            var importDoc = SkylineWindow.Document;
            var transitionCount = importDoc.MoleculeTransitionCount;
            var tranWithResults = importDoc.MoleculeTransitions.Count(tran => tran.HasResults);
            var tranWithPeaks = importDoc.MoleculeTransitions.Count(tran => 
            {
                for (int i = 0; i < 4; i ++)
                {
                    if (tran.GetPeakCountRatio(i) > 0)
                        return true;
                }
                return false;
            });
            // PauseTest(); // Pretty pictures!
            Assert.AreEqual(98,transitionCount);
            Assert.AreEqual(98,tranWithResults);
            Assert.AreEqual(90,tranWithPeaks);

            // Does that produce the expected transition list file?
            TestTransitionListOutput(importDoc, "PasteMoleculeTest.csv", "PasteMoleculeTestExpected.csv", ExportFileType.List);

            // Now test that we arrange the Targets tree as expected. 
            // (tests fix for Issue 373: Small molecules: Insert Transition list doesn't construct the tree properly)
            RunUI(() => SkylineWindow.NewDocument(true));
            docOrig = SkylineWindow.Document;
            var pasteDlg2 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            // small_molecule_paste_test.csv has non-standard column order (mz and formula swapped)
            var columnOrder = new[]
            {
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.nameProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct
            };
            RunUI(() =>
            {
                pasteDlg2.IsMolecule = true;
                pasteDlg2.SetSmallMoleculeColumns(columnOrder.ToList()); 
            });

            // Bad charge states mid-list were handled ungracefully due to lookahead in figuring out transition groups
            badcharge = Transition.MAX_PRODUCT_CHARGE + 1;
            SetClipboardText(GetCsvFileText(TestFilesDir.GetTestPath("small_molecule_paste_test.csv")).Replace(
                ",4,4".Replace(',', TextUtil.CsvSeparator), (",4," + badcharge).Replace(',', TextUtil.CsvSeparator)));
            RunUI(pasteDlg2.PasteTransitions);
            RunUI(pasteDlg2.OkDialog);  // Don't expect this to work, form stays open
            WaitForConditionUI(() => pasteDlg2.ErrorText != null);
            var errText =
                String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__,
                    badcharge, -Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
            RunUI(() => Assert.IsTrue(pasteDlg2.ErrorText.Contains(errText), 
                string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"", errText, pasteDlg2.ErrorText)));
            OkDialog(pasteDlg2, pasteDlg2.CancelDialog);

            pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            RunUI(() =>
            {
                pasteDlg.IsMolecule = true;
                pasteDlg.SetSmallMoleculeColumns(columnOrder.ToList());
            });

            SetCsvFileClipboardText(TestFilesDir.GetTestPath("small_molecule_paste_test.csv"));
            RunUI(pasteDlg.PasteTransitions);
            OkDialog(pasteDlg, pasteDlg.OkDialog);
            pastedDoc = WaitForDocumentChange(docOrig);
            // We expect four molecule groups
            var moleculeGroupNames = new [] {"Vitamin R", "Weinhards", "Oly", "Schmidt"};
            Assert.AreEqual(4, pastedDoc.MoleculeGroupCount);
            PeptideGroupDocNode[] moleculeGroups = pastedDoc.MoleculeGroups.ToArray();
            for (int n = 0; n < pastedDoc.MoleculeGroupCount; n++)
            {
                Assert.AreEqual(moleculeGroupNames[n], moleculeGroups[n].Name);
                // We expect two molecules in each group
                var precursors = moleculeGroups[n].Molecules.ToArray();
                Assert.AreEqual(2, precursors.Count());
                Assert.AreEqual("lager", precursors[0].RawTextId);
                Assert.AreEqual("dark", precursors[1].RawTextId);
                for (int m = 0; m < 2; m++)
                {
                    // We expect two transition groups per molecule
                    var transitionGroups = precursors[m].TransitionGroups.ToArray();
                    Assert.AreEqual(2, transitionGroups.Count());
                    for (int t = 0; t < 2; t++)
                    {
                        // We expect two transitions per group
                        Assert.AreEqual(2, transitionGroups[t].TransitionCount);
                        var transitions = transitionGroups[t].Transitions.ToArray();
                        Assert.AreEqual("bubbles", transitions[0].FragmentIonName);
                        Assert.AreEqual("foam", transitions[1].FragmentIonName);
                    }
            }
            }
        }

        private void TestTransitionListOutput(SrmDocument importDoc, string outputName, string expectedName, ExportFileType fileType)
        {
            // Write out a transition list
            string csvPath = TestContext.GetTestPath(outputName);
            string csvExpectedPath = TestFilesDir.GetTestPath(expectedName);
            // Open Export Method dialog, and set method to scheduled or standard.
            var exportMethodDlg =
                ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(fileType));
            if (fileType == ExportFileType.IsolationList)
            {
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.AGILENT_TOF;
                    exportMethodDlg.ExportStrategy = ExportStrategy.Single;
                    exportMethodDlg.OptimizeType = ExportOptimize.CE;
                    exportMethodDlg.MethodType = ExportMethodType.Standard;
                });
            }
            else
            {
                RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO; // Choose one that exercises CE regression
                });
            }
            OkDialog(exportMethodDlg, () => exportMethodDlg.OkDialog(csvPath));

            // Check for expected output.
            var csvOut = File.ReadAllText(csvPath);
            var csvExpected = File.ReadAllText(csvExpectedPath);
            AssertEx.Contains(csvExpected, csvOut);
        }

        private void ImportResults(string[] paths)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                var pathHolder = new KeyValuePair<string, MsDataFileUri[]>[4];
                for (int i = 0; i < paths.Length; i++)
                {
                    pathHolder[i] = new KeyValuePair<string, MsDataFileUri[]>(paths[i].Remove(paths[i].Length - ExtensionTestContext.ExtWatersRaw.Length),
                        new[] {new MsDataFilePath(TestFilesDir.GetTestPath(paths[i]))});
                }
                importResultsDlg.NamedPathSets = pathHolder;
            });
            var keepPrefixDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
            RunUI(keepPrefixDlg.NoDialog);
            WaitForClosedForm(keepPrefixDlg);
            WaitForClosedForm(importResultsDlg);
            WaitForDocumentLoaded();
        }
    }  
}
