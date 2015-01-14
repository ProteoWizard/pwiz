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
            const double precursorMz = 88.0730104200905;
            const double productMz = 31.0178414200905;
            const double precursorCE = 1.23;
            const double precursorDT = 2.34;
            const double highEnergyDtOffset = -.012;
            const double precursorRT = 3.45;
            const double productDT = precursorDT + highEnergyDtOffset;

            var docEmpty = SkylineWindow.Document;

            string line1 = "MyMolecule\tMyMol\tMyFrag\tCH12O4\tCH3O\t" + precursorMz + "\t" + productMz + "\t1\t1\t" + precursorRT + "\t" + precursorCE + "\t" + precursorDT + "\t" + productDT; // Legit
            const string line2start = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCH3O\t";
            const string line3 = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCHH500000000\t\t\t1\t1";

            // Provoke some errors
            int badcharge = Transition.MAX_PRODUCT_CHARGE + 1;
            TestError(line1 + line2start + "\t\t1\t" + badcharge, // Excessively large charge for product
                String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_between__1__and__2__, 
                badcharge, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE));
            badcharge = 120;
            TestError(line1 + line2start + "\t\t" + badcharge + "\t1", // Insanely large charge for precursor
                String.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_between__1__and__2__,
                badcharge, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
            TestError(line1 + line2start + "\t\t1\t", // No mz or charge for product
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 2));
            TestError(line1 + line2start + "99\t15", // Precursor Formula and m/z don't make sense together
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_formula_and_m_z_value_do_not_agree_for_any_charge_state_, 2));
            TestError(line1 + line2start + "\t15\t1", // Product Formula and m/z don't make sense together
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_, 2));
            TestError(line1 + line2start + "\t", // No mz or charge for precursor or product
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 2));
            TestError(line1 + line3, // Insanely large molecule
                string.Format(Resources.EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__, CustomIon.MAX_MASS));

            // Now load the document with a legit paste
            TestError(line1, String.Empty); 
            var docOrig = WaitForDocumentChange(docEmpty);
            var testTransitionGroups = docOrig.MoleculeTransitionGroups.ToArray();
            Assert.AreEqual(1, testTransitionGroups.Count());
            var transitionGroup = testTransitionGroups[0];
            var precursor = docOrig.Molecules.First();
            var product = transitionGroup.Transitions.First();
            Assert.AreEqual(precursorCE, transitionGroup.ExplicitValues.CollisionEnergy);
            Assert.AreEqual(precursorDT, transitionGroup.ExplicitValues.DriftTimeMsec);
            Assert.AreEqual(highEnergyDtOffset, transitionGroup.ExplicitValues.DriftTimeHighEnergyOffsetMsec.Value, 1E-12);
            Assert.AreEqual(precursorRT, precursor.ExplicitRetentionTime);
            Assert.AreEqual(precursorMz, BioMassCalc.CalculateMz(precursor.Peptide.CustomIon.MonoisotopicMass, product.Transition.Group.PrecursorCharge), 1E-12);
            Assert.AreEqual(productMz, BioMassCalc.CalculateMz(product.GetIonMass(), product.Transition.Charge), 1E-12);
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
            SetClipboardText(GetCsvFileText(TestFilesDir.GetTestPath("small_molecule_paste_test.csv")).Replace(",4,4", ",4," + badcharge));
            RunUI(pasteDlg2.PasteTransitions);
            RunUI(pasteDlg2.OkDialog);  // Don't expect this to work, form stays open
            WaitForConditionUI(() => pasteDlg2.ErrorText != null);
            var errText =
                String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_between__1__and__2__,
                    badcharge, Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
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
                    exportMethodDlg.MethodType = ExportMethodType.Scheduled;
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
            string csvOut = File.ReadAllText(csvPath);
            string csvExpected = File.ReadAllText(csvExpectedPath);
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
