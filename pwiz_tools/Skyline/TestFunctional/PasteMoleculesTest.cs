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
using System.Globalization;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PasteMoleculesTest : AbstractFunctionalTestEx
    {
        [TestMethod,
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)] // Don't leak test this - it takes a long time to run even once
        public void TestPasteMolecules()
        {
            TestFilesZip = @"TestFunctional\PasteMoleculeTest.zip";
            RunFunctionalTest();
        }

        private void TestError(string clipText, string errText,
            string[] columnOrder, bool expectAutoManageDlg = false)
        {
            var allErrorText = string.Empty;
            clipText = ToLocalText(clipText);
            var transitionDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => transitionDlg.TransitionListText = clipText);

            RunUI(() =>
            {
                windowDlg.radioMolecule.PerformClick();
                if (columnOrder != null)
                {
                    windowDlg.SetSelectedColumnTypes(columnOrder);
                }
            });

            if (string.IsNullOrEmpty(errText))
            {
                // We expect this to work, go ahead and load it
                var docCurrent = SkylineWindow.Document;
                OkDialog(windowDlg, windowDlg.OkDialog); 
                if (expectAutoManageDlg)
                {
                    DismissAutoManageDialog();  // Say no to the offer to set new nodes to automanage
                }
            }
            else
            {
                if (!Equals(errText, Resources.PasteDlg_ShowNoErrors_No_errors))
                {
                    var errDlg = ShowDialog<ImportTransitionListErrorDlg>(windowDlg.OkDialog);
                    RunUI(() =>
                    {
                        foreach (var err in errDlg.ErrorList)
                        {
                            allErrorText += err.ErrorMessage + "\n";
                        }
                        Assert.IsTrue(allErrorText.Contains(errText),
                            string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                                errText, allErrorText));
                    });
                    OkDialog(errDlg, errDlg.Close);
                } else {
                    // If we are expecting no errors, we will get a MessageDlg instead of an ImportTransitionListErrorDlg
                    RunDlg<MessageDlg>(windowDlg.buttonCheckForErrors.PerformClick, messageDlg => { messageDlg.OkDialog(); });
                }
                OkDialog(windowDlg, windowDlg.CancelDialog);
            }
            WaitForClosedForm(transitionDlg);
        }

        private static string ToLocalText(string text)
        {
            if (Equals(LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator, TextUtil.SEPARATOR_CSV.ToString()) &&
                !text.Contains(TextUtil.SEPARATOR_TSV)) // Don't double-convert
            {
                text = text.Replace(TextUtil.SEPARATOR_CSV, TextUtil.SEPARATOR_TSV);
            }

            text = text.Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            return text;
        }

        const string caffeineInChiKey = "RYYVLZVUVIJVGH-UHFFFAOYSA-N";
        const string caffeineHMDB = "HMDB01847";
        const string caffeineInChi = "InChI=1S/C8H10N4O2/c1-10-4-9-6-5(10)7(13)12(3)8(14)11(6)2/h4H,1-3H3";
        const string caffeineCAS = "58-08-2";
        const string caffeineSMILES = "Cn1cnc2n(C)c(=O)n(C)c(=O)c12";
        const string caffeineKEGG = "C07481";
        const string caffeineFormula = "C8H10N4O2";
        const string caffeineFormulaUnicode = "C\u2088H\u2081\u2080N\u2084O\u2082"; // Unicode subscripts
        const string caffeineFragment = "C6H5N2O"; // Not really a known fragment of caffeine

        const double precursorMzAtZNeg2 = 96.0329118;
        const double productMzAtZNeg2 = 59.5128179;
        const double explicitCE = 1.23;
        const double precursorDT = 2.34;
        const double highEnergyDtOffset = -.012;
        const double precursorCCS = 345.6;
        const double slens = 6.789;
        const double coneVoltage = 7.89;
        const double compensationVoltage = 8.901;
        const double declusteringPotential = 9.012;
        const double precursorRT = 3.45;
        const double precursorRTWindow = 4.567;
        const string note = "noted!";
        
        protected override void DoTest()
        {
            var docEmpty = NewDocument();
            TestSimilarMzIsotopes();
            TestIsotopeLabelsInInChi();
            TestNotes();
            TestAutoManage();
            TestErrors();
            TestFormulaWithAtomCountZero();
            TestIrregularColumnCounts();
            TestMissingAccessionNumbers();
            TestMzOrderIndependence();
            TestNegativeModeLabels();
            TestEmptyTransitionList();
            TestErrorDialog();
            TestImportMethods();
            TestImpliedAdductWithSynonyms();
            TestMissingProductMZ();
            TestImpliedFragmentAdduct();
            TestRecognizeChargeState();
            TestLabelsNoFormulas();
            TestHeavyLightPairs();
            TestHeavyPrecursorNoFormulas();
            TestImportAllData(true);
            TestImportAllData(false);
            TestInconsistentMoleculeDescriptions();
            TestProductNeutralLoss();
            TestUnsortedMzPrecursors();
            TestNameCollisions();
            TestAmbiguousPrecursorFragment();
            TestPerTransitionValues();
            TestToolServiceAccess();
            TestPrecursorTransitions();
            TestFullyDescribedPrecursors();
            TestTransitionListArrangementAndReporting();
            TestProperData();
        }

        private void TestProperData()
        {
            // Now a proper user data set
            var docOrig = NewDocument();
            var showDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            // Formerly SetExcelFileClipboardText(TestFilesDir.GetTestPath("MoleculeTransitionList.xlsx"),"sheet1",6,false); but TeamCity doesn't like that
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => showDialog.TransitionListText = GetCsvFileText(TestFilesDir.GetTestPath("MoleculeTransitionList.csv")));

            RunUI(() =>
            {
                windowDlg.radioMolecule.PerformClick();
                // Example line from that file: "PC,PC aa C24:0,,C32H64N1O8P1,C5H14N1O4P1,622.445,184.074"
                windowDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    null, // Ignored empty column
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z);
            });
            OkDialog(windowDlg, windowDlg.OkDialog);
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
            PasteMoleculesTestImportResults(paths);
            var importDoc = SkylineWindow.Document;
            var transitionCount = importDoc.MoleculeTransitionCount;
            var tranWithResults = importDoc.MoleculeTransitions.Count(tran => tran.HasResults);
            var tranWithPeaks = importDoc.MoleculeTransitions.Count(tran => 
            {
                for (int i = 0; i < 4; i ++)
                {
                    if (tran.GetPeakCountRatio(i, importDoc.Settings.TransitionSettings.Integration.IsIntegrateAll) > 0)
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

            // Verify that MS1 filtering works properly
            var pasteText =
                "Steryl esters [ST0102],12:0 Cholesteryl ester,C39H68O2NH4,1\r\n" +
                "Steryl esters [ST0102],14:0 Cholesteryl ester,C41H72O2NH4,1\r\n" +
                "Steryl esters [ST0102],14:1 Cholesteryl ester,C41H70O2NH4,1\r\n" +
                "Steryl esters [ST0102],15:1 Cholesteryl ester,C42H72O2NH4,1";

            var columnOrderB = new[]
            {
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
            };

            // Doc is set for MS1 filtering, fragment transitions, charge=1, two peaks, should show M and M+1, M+2 after filter is invoked by changing to 3 peaks
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("small_molecule_missing_m1.sky")));
            WaitForDocumentLoaded();
            var docA = SkylineWindow.Document;
            var transitionSettingsUIa = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUIa.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUIa.SmallMoleculeFragmentTypes = "p"; // Change filter from "f" (fragments) to "p" (precursors)
            });
            OkDialog(transitionSettingsUIa, transitionSettingsUIa.OkDialog);
            WaitForDocumentChange(docA);

            TestError(pasteText, String.Empty, columnOrderB, true);
            var docB = SkylineWindow.Document;
            Assert.AreEqual(4, docB.MoleculeTransitionCount); // Initial import is faithful to what's pasted

            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUI.Peaks = 3;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            docB = WaitForDocumentChange(docB);
            docB = EnableAutomanageChildren(docB); // Settings change has no effect until automanage is turned on
            Assert.AreEqual(12, docB.MoleculeTransitionCount);

            // Verify adduct usage - none, or in own column, or as part of formula
            var columnOrderC = new[]
            {
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
            };
            pasteText =
                "A,27-HC,C36H57N2O3,,1,135,1,light\r\n" + // No adduct, just charge
                "A,27-HC,C36H57N2O3,[M+],1,130,1,light\r\n" + // Note this claims a charge with no protonation, thus not the same precursor as these others
                "A,27-HC,C36H57N2O3,MH,,181,1,light\r\n" + // Note the implicit postive ion mode "MH"
                "A,27-HC,C36H57N2O3[M+H],,,367,1,light\r\n" ;
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC, true);
            var docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);
            Assert.AreEqual(1, docC.MoleculeTransitionGroups.First().TransitionCount); // M+ first (lowest m/z)
            Assert.AreEqual(3, docC.MoleculeTransitionGroups.Last().TransitionCount);  // Then M+H

            // Verify adduct usage - none, or in own column, or as part of formula, when no name hints are given
            columnOrderC = new[]
            {
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
            };
            pasteText =
                "C36H57N2O3,,1,135,1,light\r\n" + // No adduct
                "C36H57N2O3,[M+],1,130,1,light\r\n" +
                "C36H56N2O3,M+H,,181,1,light\r\n" +
                "C36H56N2O3[M+H],,,367,1,light\r\n";
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC, true);
            docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(2, docC.MoleculeCount);
            Assert.AreEqual(3, docC.MoleculeTransitionGroupCount);  // Formula descriptions devolve to C36H56N2O3[M+H] and C36H57N2O3
            Assert.AreEqual(1, docC.MoleculeTransitionGroups.First().TransitionCount);  

            pasteText =
                "C36H56N2O3,M+H,,181,1,light\r\n" +
                "C36H56N2O3[M+H],,,367,1,light\r\n" +
                "C36H56N2O3,M+2H,,81,2,light\r\n" +
                "C36H56N2O3[M+2H],,,167,2,light\r\n";
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC, true);
            docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);

            pasteText =
                "C36H56N2O3,M+S,,181,,light\r\n"; // Adduct with unknown charge
            NewDocument();
            TestError(pasteText,
                string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Cannot_derive_charge_from_adduct_description___0____Use_the_corresponding_Charge_column_to_set_this_explicitly__or_change_the_adduct_description_as_needed_, "[M+S]"),
                columnOrderC, true);
            pasteText =
                "C36H56N2O3,M+S,1,181,1,light\r\n"; // Adduct with unknown charge, but charge provided seperately
            NewDocument();
            TestError(pasteText,
                string.Empty,
                columnOrderC, true);
        }

        private void TestErrors()
        {
            var docEmpty = NewDocument();

            // Load a document whose settings understand heavy labeling
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("heavy.sky")));

            var fullColumnOrder = new[]
            {
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time,
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window,
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy,
                Resources.PasteDlg_UpdateMoleculeType_Note,
                Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,
                Resources.PasteDlg_UpdateMoleculeType_Product_Adduct,
                Resources
                    .ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column, // Drift time columns are now obsolete
                Resources
                    .ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column, // Drift time columns are now obsolete
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_,
                Resources.PasteDlg_UpdateMoleculeType_S_Lens,
                Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage,
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage,
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential,
                @"InChiKey",
                @"HMDB",
                @"InChi",
                @"CAS",
                @"SMILES",
                @"KEGG",
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility,
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset,
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units,
                Resources.PasteDlg_UpdateMoleculeType_Product_Neutral_Loss,
            };

            // Default col order is listname, preName, PreFormula, preAdduct, preMz, preCharge, prodName, ProdFormula, prodAdduct, prodMz, prodCharge
            var line1 = BuildTestLine(true);
            const string line2start = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCH3O\t";
            const string line3 = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCHH500000000\t\t\t1\t1";
            const string line4 = "\r\nMyMolecule3\tMyMol3\tMyFrag3\tH2\tH\t\t\t1\t1";
            string line5 = line1.Replace(caffeineFormula, "C8H12N4O2[M-2H]").Replace(caffeineFragment, "C34H32")
                .Replace(note + "\t\t\t", note + "\t\tM-2H\t"); // Legit
            string line6 = line1.Replace(caffeineFormula, "").Replace(caffeineFragment, "")
                .Replace(note + "\t\t\t", note + "\t\tM-3H\t"); // mz only, but charge and adduct disagree

            TestLegitimatePaste(line2start, fullColumnOrder);

            // Provoke some errors
            TestError(
                line1.Replace("\t-2\t-2", "\t-2\t2")
                    .Replace(productMzAtZNeg2.ToString(CultureInfo.CurrentCulture),
                        ""), // precursor and charge polarities disagree
                Resources.Transition_Validate_Precursor_and_product_ion_polarity_do_not_agree_, fullColumnOrder);
            TestError(line1.Replace(caffeineFormula, "C77H12O4"), // mz and formula disagree
                String.Format(Resources.SmallMoleculeTransitionListReader_Precursor_mz_does_not_agree_with_calculated_value_,
                    (float)precursorMzAtZNeg2, 499.0295, 402.9966,
                    docEmpty.Settings.TransitionSettings.Instrument.MzMatchTolerance), fullColumnOrder);
            TestError(line1.Replace(caffeineFragment, "C76H3"), // mz and formula disagree
                String.Format(Resources.SmallMoleculeTransitionListReader_Product_mz_does_not_agree_with_calculated_value_,
                    (float)productMzAtZNeg2, 456.5045, 396.9916,
                    docEmpty.Settings.TransitionSettings.Instrument.MzMatchTolerance), fullColumnOrder);
            var badcharge = Transition.MAX_PRODUCT_CHARGE + 1;
            TestError(line1 + line2start + "\t\t1\t" + badcharge, // Excessively large charge for product
                String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__,
                    badcharge, -Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE), fullColumnOrder);
            badcharge = 120;
            TestError(line1 + line2start + "\t\t" + badcharge + "\t1", // Insanely large charge for precursor
                String.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                    badcharge, -TransitionGroup.MAX_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE), fullColumnOrder);
            TestError(line1 + line2start + "\t\t1\t", // No mz or charge for product
                Resources
                    .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_,
                fullColumnOrder);
            TestError(line1 + line2start + "19\t5", // Precursor Formula and m/z don't make sense together
                Resources
                    .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Precursor_formula_and_m_z_value_do_not_agree_for_any_charge_state_,
                fullColumnOrder);
            TestError(line1 + line2start + "\t7\t1", // Product Formula and m/z don't make sense together
                Resources
                    .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_,
                fullColumnOrder);
            TestError(line1 + line2start + "\t", // No mz or charge for precursor or product
                Resources
                    .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_,
                fullColumnOrder);
            TestError(line1 + line3, // Insanely large molecule
                string.Format(
                    Resources.CustomMolecule_Validate_The_mass__0__of_the_custom_molecule_exceeeds_the_maximum_of__1__,
                    503970013.01879, CustomMolecule.MAX_MASS), fullColumnOrder);
            TestError(line1 + line4, // Insanely small molecule
                string.Format(
                    Resources.CustomMolecule_Validate_The_mass__0__of_the_custom_molecule_is_less_than_the_minimum_of__1__,
                    2.01588, CustomMolecule.MIN_MASS), fullColumnOrder);
            TestError(
                line1 + line2start + +precursorMzAtZNeg2 + "\t" + productMzAtZNeg2 + "\t-2\t-2\t\t\t" + precursorRTWindow +
                "\t" + explicitCE + "\t" + note + "\t\t\t" + precursorDT + "\t" +
                highEnergyDtOffset, // Explicit retention time window without retention time
                Resources
                    .Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_,
                fullColumnOrder);
            TestError(line5.Replace("[M-2H]", "[M+H]"),
                string.Format(
                    Resources
                        .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_,
                    "[M+H]", 1, -2), fullColumnOrder);
            TestError(line6,
                string.Format(
                    Resources
                        .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_,
                    "M-3H", -3, -2), fullColumnOrder);
            for (int withSpecials = 2; withSpecials-- > 0;)
            {
                // By default we don't show drift or other exotic columns
                var columnOrder = (withSpecials == 0) ? fullColumnOrder.Take(16).ToArray() : fullColumnOrder;
                int imIndex = 0;
                int covIndex = 0;
                // Take a legit full paste and mess with each field in turn
                string[] fields =
                {
                    "MyMol", "MyPrecursor", "MyProduct", "C12H9O4", "C6H4O2", "217.049535420091", "108.020580420091", "1", "1",
                    "heavy", "123", "5", "25", "this is a note", "[M+]", "[M+]", "7", "9", "123", "88.5", "99.6", "77.3",
                    "66.2",
                    caffeineInChiKey, caffeineHMDB, caffeineInChi, caffeineCAS, caffeineSMILES, caffeineKEGG, "123.4", "-0.234",
                    "Vsec/cm2", "C6H5O2"
                };
                string[] badfields =
                {
                    "", "", "", "123z", "C6H2O2[M+2H]", "fish", "-345", "cat", "pig", "12", "frog", "hamster", "boston", "",
                    "[M+foo]", "wut", "foosballDT", "greasyDTHEO", "mumbleCCS", "gumdropSLEN", "dingleConeV", "dangleCompV",
                    "gorseDP", "AHHHHHRGHinchik", "bananananahndb",
                    "shamble-raft4-inchi", "bags34cas", "flansmile", "boozlekegg", "12-fooim", "bumbleimheo", "dingoimunit",
                    "C6H15O5"
                };
                Assert.AreEqual(fields.Length, badfields.Length);

                var expectedErrors = new List<string>()
                {
                    Resources.PasteDlg_ShowNoErrors_No_errors, Resources.PasteDlg_ShowNoErrors_No_errors,
                    Resources.PasteDlg_ShowNoErrors_No_errors, // No name, no problem
                    BioMassCalc.FormatArgumentExceptionMessage(badfields[3]),
                    Resources
                        .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Formula_already_contains_an_adduct_description__and_it_does_not_match_,
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_, badfields[5]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_, badfields[6]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_, badfields[7]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_, badfields[8]),
                    string.Format(
                        Resources
                            .SrmDocument_ReadLabelType_The_isotope_modification_type__0__does_not_exist_in_the_document_settings,
                        badfields[9]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_retention_time_value__0_,
                        badfields[10]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_retention_time_window_value__0_,
                        badfields[11]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_collision_energy_value__0_,
                        badfields[12]),
                    badfields[13], // This is empty, as notes are freeform, so any value is fine
                    string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Unknown_symbol___0___in_adduct_description___1__,
                        "foo", badfields[14]),
                    string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0__,
                        "[" + badfields[15] + "]"),
                };
                if (withSpecials > 0)
                {
                    // With addition of Explicit Ion Mobility, Explicit Compensation Voltage becomes a conflict
                    expectedErrors[0] = Resources
                        .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Multiple_ion_mobility_declarations;

                    var s = expectedErrors.Count;
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ShowNoErrors_No_errors));
                    s++; // No longer possible to have both "drift" and "ion mobility" columns at once, user would have to set this as "Ignore" so no error
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ShowNoErrors_No_errors));
                    s++; // No longer possible to have both "drift" and "ion mobility" columns at once, user would have to set this as "Ignore" so no error
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_collisional_cross_section_value__0_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_S_Lens_value__0_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_cone_voltage_value__0_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_compensation_voltage__0_,
                            badfields[covIndex = s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_declustering_potential__0_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(
                            Resources.SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_InChiKey_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_HMDB_identifier_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_InChI_identifier_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_CAS_registry_number_,
                            badfields[s++]));
                    expectedErrors.Add(
                        Resources.PasteDlg_ShowNoErrors_No_errors);
                    s++; // We don't have a proper SMILES syntax check yet
                    expectedErrors.Add(
                        Resources.PasteDlg_ShowNoErrors_No_errors);
                    s++; // We don't have a proper KEGG syntax check yet
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_,
                            badfields[imIndex = s++]));
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_high_energy_offset_value__0_,
                            badfields[s++]));
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_units_value__0___accepted_values_are__1__,
                            badfields[s++], SmallMoleculeTransitionListReader.GetAcceptedIonMobilityUnitsString()));
                    expectedErrors.Add(
                        string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ProcessNeutralLoss_Precursor_molecular_formula__0__does_not_contain_sufficient_atoms_to_be_used_with_neutral_loss__1_,
                            fields[3], badfields[s++]));
                }

                expectedErrors.Add(Resources.PasteDlg_ShowNoErrors_No_errors); // N+1'th pass is unadulterated
                for (var bad = 0; bad < expectedErrors.Count; bad++)
                {
                    var line = "";
                    for (var f = 0; f < expectedErrors.Count - 1; f++)
                        line += ((bad == f) ? badfields[f] : fields[f]).Replace(".",
                            LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator) + "\t";
                    if (!string.IsNullOrEmpty(expectedErrors[bad]))
                        TestError(line, expectedErrors[bad], columnOrder);
                    if (imIndex > 0)
                    {
                        // Now that we have tested the warning, clear up the conflict between declared CoV and declared ion mobility
                        if (bad < covIndex)
                        {
                            columnOrder[imIndex] =
                                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column;
                            columnOrder[covIndex] = Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage;
                        }
                        else
                        {
                            columnOrder[imIndex] = Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility;
                            columnOrder[covIndex] =
                                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column;
                        }
                    }
                }
            }

            TestError(
                line1.Replace(caffeineFormula, caffeineFormula + "[M-H]")
                    .Replace(caffeineFragment, caffeineFragment + "[M-H]") + line2start + "\t\t1\t1",
                string.Format(
                    Resources
                        .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_,
                    "[M-H]", -1, -2), fullColumnOrder);

            // Reset
            NewDocument();
        }

        private void TestLegitimatePaste(string line2start, string[] fullColumnOrder)
        {
            SrmDocument docEmpty;
            string line1;
            // Now load the document with a legit paste
            foreach (var imTypeIsDrift in
                     new[] { true, false }) // Check interplay of explicit Compensation Voltage and explicit IM
            {
                docEmpty = NewDocument();
                line1 = BuildTestLine(imTypeIsDrift);
                if (imTypeIsDrift)
                {
                    // Nothing to do with imType, just want to alternate styles here
                    line1 = line1.Replace(caffeineFormula+"\t", caffeineFormulaUnicode + "\t"); // Test with unicode subscript numbers
                }
                var expectedIM = imTypeIsDrift ? precursorDT : compensationVoltage;
                double? expectedCV = imTypeIsDrift ? (double?)null : compensationVoltage;
                var expectedTypeIM = imTypeIsDrift ? eIonMobilityUnits.drift_time_msec : eIonMobilityUnits.compensation_V;
                TestError(line1 + line2start.Replace("CH3O", "CH29") + "\t\t1\t\t\t\t\t\t\t\tM+H", String.Empty,
                    fullColumnOrder, true);
                var docTest = WaitForDocumentChange(docEmpty);
                var testTransitionGroups = docTest.MoleculeTransitionGroups.ToArray();
                Assert.AreEqual(2, testTransitionGroups.Length);
                var transitionGroup = testTransitionGroups[0];
                var precursor = docTest.Molecules.First();
                var product = transitionGroup.Transitions.First();
                Assert.AreEqual(explicitCE,
                    product.ExplicitValues.CollisionEnergy ?? transitionGroup.ExplicitValues.CollisionEnergy);
                Assert.AreEqual(expectedIM, transitionGroup.ExplicitValues.IonMobility);
                Assert.AreEqual(expectedTypeIM, transitionGroup.ExplicitValues.IonMobilityUnits);
                Assert.AreEqual(precursorCCS, transitionGroup.ExplicitValues.CollisionalCrossSectionSqA);
                Assert.AreEqual(slens, product.ExplicitValues.SLens);
                Assert.AreEqual(coneVoltage, product.ExplicitValues.ConeVoltage);
                Assert.AreEqual(expectedCV, transitionGroup.ExplicitValues.CompensationVoltage);
                Assert.AreEqual(declusteringPotential, product.ExplicitValues.DeclusteringPotential);
                Assert.AreEqual(note, product.Annotations.Note);
                Assert.AreEqual(highEnergyDtOffset, product.ExplicitValues.IonMobilityHighEnergyOffset.Value, 1E-7);
                Assert.AreEqual(precursorRT, precursor.ExplicitRetentionTime.RetentionTime);
                Assert.AreEqual(precursorRTWindow, precursor.ExplicitRetentionTime.RetentionTimeWindow);
                Assert.IsTrue(ReferenceEquals(transitionGroup.TransitionGroup, product.Transition.Group));
                Assert.AreEqual(precursorMzAtZNeg2,
                    transitionGroup.PrecursorAdduct.MzFromNeutralMass(transitionGroup.CustomMolecule.MonoisotopicMass), 1E-6);
                Assert.AreEqual(productMzAtZNeg2, product.Transition.Adduct.MzFromNeutralMass(product.GetMoleculeMass()), 1E-6);
                Assert.AreEqual(precursorMzAtZNeg2,
                    transitionGroup.PrecursorAdduct.MzFromNeutralMass(transitionGroup.CustomMolecule.MonoisotopicMass.Value,
                        transitionGroup.CustomMolecule.MonoisotopicMass.MassType), 1E-6);
                Assert.AreEqual(productMzAtZNeg2,
                    product.Transition.Adduct.MzFromNeutralMass(product.GetMoleculeMass().Value,
                        product.GetMoleculeMass().MassType), 1E-6);
                Assert.AreEqual(caffeineInChiKey,
                    precursor.CustomMolecule.PrimaryEquivalenceKey); // Use InChiKey as primary library key when available
                Assert.AreEqual(caffeineInChiKey,
                    precursor.CustomMolecule.AccessionNumbers
                        .PrimaryAccessionValue); // Use InChiKey as primary library key when available
                Assert.AreEqual(MoleculeAccessionNumbers.TagInChiKey,
                    precursor.CustomMolecule.AccessionNumbers
                        .PrimaryAccessionType); // Use InChiKey as primary library key when available
                Assert.AreEqual(caffeineInChiKey,
                    precursor.CustomMolecule.AccessionNumbers.AccessionNumbers[0]
                        .Value); // Use InChiKey as primary library key when available
                string hmdb;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers.TryGetValue("HMDB", out hmdb);
                Assert.AreEqual(caffeineHMDB.Substring(4), hmdb);
                string inchi;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers.TryGetValue("InChi", out inchi);
                Assert.AreEqual(caffeineInChi.Substring(6), inchi);
                string cas;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers
                    .TryGetValue("cAs", out cas); // Should be case insensitive
                Assert.AreEqual(caffeineCAS, cas);
                string smiles;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers
                    .TryGetValue("smILes", out smiles); // Should be case insensitive
                Assert.AreEqual(caffeineSMILES, smiles);
                string kegg;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers
                    .TryGetValue("kEgG", out kegg); // Should be case insensitive
                Assert.AreEqual(caffeineKEGG, kegg);
                // Does that produce the expected transition list file?
                TestTransitionListOutput(docTest, "PasteMoleculeTinyTest.csv", "PasteMoleculeTinyTestExpected.csv",
                    ExportFileType.IsolationList);
                // Does serialization of imported values work properly?
                AssertEx.Serializable(docTest);

                // Verify that this text can be imported as a file with File > Import > Transition List
                TestFileImportTransitionList(line1);
            }

            NewDocument(); // Reset
        }

        private void TestHeavyLightPairs()
        {
            // Verify that we can import heavy/light pairs
            var columnOrderC = new[]
            {
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
            };
            var pasteText =
                "A,27-HC,C36H57N2O3,135,1,1,light\r\n" +
                "A,27-HC,C36H57N2O3,181,1,1,light\r\n" +
                "A,27-HC,C36H57N2O3,367,1,1,light\r\n" +
                "A,27-HC,C36H51H'6N2O3,135,1,1,heavy\r\n" +
                "A,27-HC,C36H51H'6N2O3,181,1,1,heavy\r\n" + // H' should translate to H2 in adduct isotope description
                "A,27-HC,C36H51D6N2O3,215,1,1,heavy\r\n"; // D should translate to H2 in adduct isotope description
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC);
            var docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);
            var groupsC = docC.MoleculeTransitionGroups.ToArray();
            Assert.AreEqual(Adduct.M_PLUS_H, groupsC[0].PrecursorAdduct);
            Assert.AreEqual(Adduct.FromString("[M6H2+H]", Adduct.ADDUCT_TYPE.non_proteomic, null), groupsC[1].PrecursorAdduct);
        }

        private static string BuildTestLine(bool asDriftTime)
        {
            eIonMobilityUnits imType = asDriftTime ? eIonMobilityUnits.drift_time_msec : eIonMobilityUnits.compensation_V;
            var dtValueStr = asDriftTime ? precursorDT.ToString(CultureInfo.CurrentCulture) : string.Empty;
            var imValueStr = asDriftTime ? precursorDT.ToString(CultureInfo.CurrentCulture) : compensationVoltage.ToString(CultureInfo.CurrentCulture);
            var cvValueStr = asDriftTime ? string.Empty : compensationVoltage.ToString(CultureInfo.CurrentCulture);
            return "MyMolecule\tMyMol\tMyFrag\t" + caffeineFormula + "\t" + caffeineFragment + "\t" +
                           precursorMzAtZNeg2 + "\t" + productMzAtZNeg2 + "\t-2\t-2\tlight\t" +
                           precursorRT + "\t" + precursorRTWindow + "\t" + explicitCE + "\t" + note + "\t\t\t" + dtValueStr +
                           "\t" + highEnergyDtOffset + "\t" + precursorCCS + "\t" + slens + "\t" + coneVoltage +
                           "\t" + cvValueStr + "\t" + declusteringPotential + "\t" + caffeineInChiKey + "\t" +
                           caffeineHMDB + "\t" + caffeineInChi + "\t" + caffeineCAS + "\t" + caffeineSMILES + "\t" + caffeineKEGG
                           + "\t" + imValueStr + "\t" + highEnergyDtOffset + "\t" + imType;
        }

        private static SrmDocument NewDocument()
        {
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.ModifyDocument("Set Vantage CE", docInit =>
                    docInit.ChangeSettings(docInit.Settings.ChangeTransitionPrediction(pred =>
                            pred.ChangeCollisionEnergy(CollisionEnergyList.GetDefault0_6()))));
            });
            return SkylineWindow.Document;
        }

        private void TestTransitionListArrangementAndReporting()
        {
            var saveColumnOrder = Settings.Default.CustomMoleculeTransitionInsertColumnsList;

            // Now test that we arrange the Targets tree as expected. 
            // (tests fix for Issue 373: Small molecules: Insert Transition list doesn't construct the tree properly)
            var docOrig = NewDocument();
            // var pasteDlg2 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            // small_molecule_paste_test.csv has non-standard column order (mz and formula swapped)
            var columnOrder = new[]
            {
                SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                SmallMoleculeTransitionListColumnHeaders.nameProduct,
                SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                SmallMoleculeTransitionListColumnHeaders.mzProduct,
                SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                SmallMoleculeTransitionListColumnHeaders.transitionNote,
                SmallMoleculeTransitionListColumnHeaders.idCAS,
                SmallMoleculeTransitionListColumnHeaders.idHMDB,
                SmallMoleculeTransitionListColumnHeaders.idInChi,
                SmallMoleculeTransitionListColumnHeaders.idInChiKey,
                SmallMoleculeTransitionListColumnHeaders.idSMILES,
                SmallMoleculeTransitionListColumnHeaders.idKEGG,
            };
            

            // Bad charge states mid-list were handled ungracefully due to lookahead in figuring out transition groups
            const int badcharge = Transition.MAX_PRODUCT_CHARGE + 1;
            var showDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => showDialog.TransitionListText = GetCsvFileText(TestFilesDir.GetTestPath("small_molecule_paste_test.csv")).Replace(
                ",4,4".Replace(',', TextUtil.CsvSeparator), (",4," + badcharge).Replace(',', TextUtil.CsvSeparator)));
            RunUI(() => {
                windowDlg.radioMolecule.PerformClick();
                windowDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge);
            });
            var errText =
                String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__,
                    badcharge, -Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
            string allErrorList = "";
            var errDlg = ShowDialog<ImportTransitionListErrorDlg>(windowDlg.OkDialog);
            RunUI(() =>
            {
                foreach (var err in errDlg.ErrorList)
                {
                    allErrorList += err.ErrorMessage;
                }
                Assert.IsTrue(allErrorList.Contains(errText),
                    string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                        errText, errDlg.ErrorList));
            });
            OkDialog(errDlg, errDlg.Close);
            OkDialog(windowDlg, windowDlg.CancelDialog);
            WaitForClosedForm(showDialog);
            var text = GetCsvFileText(TestFilesDir.GetTestPath("SmallMolDataFix.csv"));
            var text1Dialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var col0Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => text1Dialog.TransitionListText = text);
            RunUI(() => {
                col0Dlg.radioMolecule.PerformClick();
                col0Dlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Note,
                    @"CAS",
                    @"HMDB",
                    @"InChi",
                    @"InChiKey",
                    @"SMILES",
                    @"KEGG");
            });
            OkDialog(col0Dlg, col0Dlg.OkDialog);
            DismissAutoManageDialog();  // Say no to the offer to set new nodes to automanage
            var pastedDoc = WaitForDocumentChange(docOrig);
            // We expect four molecule groups
            var moleculeGroupNames = new [] {"Vitamin R", "Weinhards", "Oly", "Schmidt"};
            Assert.AreEqual(4, pastedDoc.MoleculeGroupCount);
            PeptideGroupDocNode[] moleculeGroups = pastedDoc.MoleculeGroups.ToArray();
            for (int n = 0; n < pastedDoc.MoleculeGroupCount; n++)
            {
                Assert.AreEqual(moleculeGroupNames[n], moleculeGroups[n].Name);
                // We expect two molecules in each group
                var precursors = moleculeGroups[n].Molecules.ToArray();
                Assert.AreEqual(2, precursors.Length);
                Assert.AreEqual(caffeineInChiKey, precursors[0].RawTextId);
                Assert.AreEqual("dark", precursors[1].RawTextId);
                for (int m = 0; m < 2; m++)
                {
                    // We expect two transition groups per molecule
                    var transitionGroups = precursors[m].TransitionGroups.ToArray();
                    Assert.AreEqual(2, transitionGroups.Length,"unexpected transition group count for molecule group "+moleculeGroupNames[n]);
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

            // Verify small molecule handling in Document Grid
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Transitions));
            WaitForCondition(() => (documentGrid.RowCount == 32));  // Let it initialize
            // Simulate user editing the transition in the document grid
            const string noteText = "let's see some ID";
            RunUI(() =>
            {
                var colNote = documentGrid.FindColumn(PropertyPath.Parse("Note"));
                try
                {
                    documentGrid.DataGridView.Rows[0].Cells[colNote.Index].Value = noteText;
                }
                catch (Exception x)
                {
                    String message = "Error setting column " + colNote.Index + " to " + noteText;
                    HandleDocumentGridException(documentGrid.DataboundGridControl, message, x);
                }
            });
            WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitions.Any() &&
                SkylineWindow.Document.MoleculeTransitions.First().Note.Equals(noteText)));

                // Simulate user editing the peptide in the document grid
                RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Molecules));
                WaitForCondition(() => (documentGrid.RowCount == 8));  // Let it initialize
                const double explicitRT = 123.45;
                var colRT = FindDocumentGridColumn(documentGrid, "ExplicitRetentionTime");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colRT.Index].Value = explicitRT);
                WaitForCondition(() => (SkylineWindow.Document.Molecules.Any() &&
                  SkylineWindow.Document.Molecules.First().ExplicitRetentionTime.RetentionTime.Equals(explicitRT)));

                const double explicitRTWindow = 3.45;
                var colRTWindow = FindDocumentGridColumn(documentGrid, "ExplicitRetentionTimeWindow");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colRTWindow.Index].Value = explicitRTWindow);
                WaitForCondition(() => (SkylineWindow.Document.Molecules.Any() &&
                  SkylineWindow.Document.Molecules.First().ExplicitRetentionTime.RetentionTimeWindow.Equals(explicitRTWindow)));

                // Simulate user editing the precursor in the document grid, also check for molecule IDs
                EnableDocumentGridColumns(documentGrid, Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors, 16, new[] {
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitIonMobility",
                    "Proteins!*.Peptides!*.Precursors!*.PrecursorExplicitCollisionEnergy",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.ExplicitIonMobilityHighEnergyOffset",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitCollisionalCrossSection",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.ExplicitCollisionEnergy", // Overrides Precursors!*.ExplicitCollisionEnergy
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.ExplicitDeclusteringPotential",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitCompensationVoltage",
                    "Proteins!*.Peptides!*.InChiKey",
                    "Proteins!*.Peptides!*.InChI",
                    "Proteins!*.Peptides!*.HMDB",
                    "Proteins!*.Peptides!*.SMILES",
                    "Proteins!*.Peptides!*.CAS",
                    "Proteins!*.Peptides!*.KEGG"}, null, 32);
                const double explicitCE2= 123.45;
                var colCE = FindDocumentGridColumn(documentGrid, "ExplicitCollisionEnergy");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colCE.Index].Value = explicitCE2);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitions.First().ExplicitValues.CollisionEnergy.Equals(explicitCE2)));

                const double explicitPrecursorCE = 234.567;
                var colPCE = FindDocumentGridColumn(documentGrid, "Precursor.PrecursorExplicitCollisionEnergy");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colPCE.Index].Value = explicitPrecursorCE);
                WaitForCondition(() => SkylineWindow.Document.MoleculeTransitionGroups.Any(tg => tg.ExplicitValues.CollisionEnergy.Equals(explicitPrecursorCE)));
                // Expect the next line, which depicts a sibling transition, to share this precursor value
                WaitForCondition(() => Equals(explicitPrecursorCE, documentGrid.DataGridView.Rows[1].Cells[colPCE.Index].Value));

                const double explicitDP = 12.345;
                var colDP = FindDocumentGridColumn(documentGrid, "ExplicitDeclusteringPotential");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colDP.Index].Value = explicitDP);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitions.First().ExplicitValues.DeclusteringPotential.Equals(explicitDP)));

                const double explicitCV = 13.45;
                var colCV = FindDocumentGridColumn(documentGrid, "Precursor.ExplicitCompensationVoltage");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colCV.Index].Value = explicitCV);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.CompensationVoltage.Equals(explicitCV)));

                const double explicitDT = 23.465;
                var colDT = FindDocumentGridColumn(documentGrid, "Precursor.ExplicitIonMobility");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colDT.Index].Value = explicitDT);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.IonMobility.Equals(explicitDT)));

                const double explicitDTOffset = -3.4657;
                var colDTOffset = FindDocumentGridColumn(documentGrid, "ExplicitIonMobilityHighEnergyOffset");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colDTOffset.Index].Value = explicitDTOffset);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitions.First().ExplicitValues.IonMobilityHighEnergyOffset.Equals(explicitDTOffset)));

                const double explicitCCS = 345.6;
                var colCCS = FindDocumentGridColumn(documentGrid, "Precursor.ExplicitCollisionalCrossSection");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colCCS.Index].Value = explicitCCS);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.CollisionalCrossSectionSqA.Equals(explicitCCS)));

            var colInChiKey = FindDocumentGridColumn(documentGrid, "Precursor.Peptide.InChiKey");
            var reportedInChiKey = string.Empty;
            RunUI(() => reportedInChiKey = documentGrid.DataGridView.Rows[0].Cells[colInChiKey.Index].Value.ToString());
            Assume.AreEqual(caffeineInChiKey, reportedInChiKey, "unexpected molecule inchikey");

            var colInChI = FindDocumentGridColumn(documentGrid, "Precursor.Peptide.InChI");
            var reportedInChI = string.Empty;
            RunUI(() => reportedInChI = documentGrid.DataGridView.Rows[0].Cells[colInChI.Index].Value.ToString());
            Assume.AreEqual(caffeineInChi.Substring(6), reportedInChI, "unexpected molecule inchi");

            var colHMDB = FindDocumentGridColumn(documentGrid, "Precursor.Peptide.HMDB");
            var reportedHMDB = string.Empty;
            RunUI(() => reportedHMDB = documentGrid.DataGridView.Rows[0].Cells[colHMDB.Index].Value.ToString());
            Assume.AreEqual(caffeineHMDB.Substring(4), reportedHMDB, "unexpected molecule hmdb");

            var colCAS = FindDocumentGridColumn(documentGrid, "Precursor.Peptide.CAS");
            var reportedCAS = string.Empty;
            RunUI(() => reportedCAS = documentGrid.DataGridView.Rows[0].Cells[colCAS.Index].Value.ToString());
            Assume.AreEqual(caffeineCAS, reportedCAS, "unexpected molecule cas");

            var colSMILES = FindDocumentGridColumn(documentGrid, "Precursor.Peptide.SMILES");
            var reportedSMILES = string.Empty;
            RunUI(() => reportedSMILES = documentGrid.DataGridView.Rows[0].Cells[colSMILES.Index].Value.ToString());
            Assume.AreEqual(caffeineSMILES, reportedSMILES, "unexpected molecule smiles");

            var colKEGG = FindDocumentGridColumn(documentGrid, "Precursor.Peptide.KEGG");
            var reportedKEGG = string.Empty;
            RunUI(() => reportedKEGG = documentGrid.DataGridView.Rows[0].Cells[colKEGG.Index].Value.ToString());
            Assume.AreEqual(caffeineKEGG, reportedKEGG, "unexpected molecule kegg");
            // PauseTest(); // Pretty pictures!

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
            NewDocument();
            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
        }

        private void TestPrecursorTransitions()
        {
            // Test our handling of precursor transitions:
            //  If no product ion info supplied, interpret as a list of precursor transitions.
            //  If some product ion info supplied, and some missing, reject the input.
            //  If product ion info supplied matches the precursor molecule info, interpret as a precursor transition.

            var saveColumnOrder = Settings.Default.CustomMoleculeTransitionInsertColumnsList;

            var docOrig = NewDocument();
            
            //non-standard column order
            var columnOrder = new[]
            {
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                Resources.PasteDlg_UpdateMoleculeType_Note,
                Resources.PasteDlg_UpdateMoleculeType_Label_Type
            };
            const string inconsistent =
                "Oly\tlager\tbubbles\t452\t\t\t\t1\t\t\n" +
                "Oly\tlager\tfoam\t234\t163\t\t\t1\t1\tduly noted?";
            var errText = string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 1);
            TestError(inconsistent, errText, columnOrder);

            // If user omits product info altogether, that implies precursor transitions
            docOrig = NewDocument();
            const string implied =
                "Oly\tlager\tlager\t452\t\t\t\t1\t\tmacrobrew" + "\n" +
                "Oly\tlager\tlager\t234\t\t\t\t1\t\tmacrobrew";
            TestError(implied, string.Empty, columnOrder);
            var pastedDoc = WaitForDocumentChange(docOrig);
            // We expect precursor transitions, but since both are light, in two different groups
            Assert.AreEqual(2, pastedDoc.MoleculeTransitionGroupCount);
            foreach (var trans in pastedDoc.MoleculeTransitions)
            {
                Assert.IsTrue(trans.Transition.IsPrecursor());
                Assert.AreEqual(trans.Annotations.Note,"macrobrew");
            }

            // If product mass matches precursor, that implies precursor transitions
            docOrig = pastedDoc;
            const string matching =
                "Schmidt\tlager\t\t150\t150\t\t\t2\t2\tnotated!" + "\n" +
                "Schmidt\tlager\t\t159\t159\t\t\t3\t3\tnote!";
            TestError(matching, string.Empty, columnOrder);
            pastedDoc = WaitForDocumentChange(docOrig);
            // We expect precursor transitions
            foreach (var trans in pastedDoc.MoleculeTransitions)
            {
                Assert.IsTrue(trans.Transition.IsPrecursor());
                Assert.IsTrue(trans.Annotations.Note.StartsWith("not") || trans.Annotations.Note.StartsWith("macro"));
            }

            docOrig = NewDocument();
            const string impliedLabeled =
                "Oly\tlager\t\t452.1\t\t\t\t1\t\tmacrobrew\theavy" + "\n" +
                "Oly\tlager\t\t234.5\t\t\t\t1\t\tmacrobrew\tlight";
            TestError(impliedLabeled, string.Empty, columnOrder);
            pastedDoc = WaitForDocumentChange(docOrig);
            // We expect a single heavy/light pair
            Assert.AreEqual(1, pastedDoc.MoleculeCount);
            foreach (var trans in pastedDoc.MoleculeTransitions)
            {
                Assert.IsTrue(trans.Transition.IsPrecursor());
                Assert.AreEqual(trans.Annotations.Note, "macrobrew");
            }

            // Load a document whose settings call for different mass type for precursors and fragments
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("mixed_mass_types.sky")));
            docOrig = SkylineWindow.Document;

            const string precursorOnly = "15xT\tC150H197N30O103P14\t-3";
            TestError(precursorOnly, string.Empty, new[] {
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge
            });
            pastedDoc = WaitForDocumentChange(docOrig);
            Assume.AreEqual(pastedDoc.MoleculePrecursorPairs.First().NodeGroup.PrecursorMz,
                pastedDoc.MoleculeTransitions.First().Mz);
            Assume.AreEqual(pastedDoc.MoleculePrecursorPairs.First().NodeGroup.PrecursorMzMassType,
                pastedDoc.MoleculeTransitions.First().MzMassType);
            Assume.AreEqual(MassType.Average, pastedDoc.MoleculeTransitions.First().MzMassType);

            NewDocument();
            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
        }

        private void TestIrregularColumnCounts()
        {
            var header = GetAminoAcidsTransitionListText(out var textCSV) + "\n";
            var lineEnd = @",-1,3";

            TestIrregularColumnCountCases(textCSV, lineEnd, false);

            // Now check again when the anomaly is past the end of the lines that get shown to the user
            textCSV = textCSV.Replace(header, string.Empty);
            var chunk = textCSV.Replace(lineEnd, @",-2,4").Split('\n');
            var bigText = header;
            int lineCount = 1;
            while (lineCount < ImportTransitionListColumnSelectDlg.N_DISPLAY_LINES)
            {
                foreach (var lineText in chunk.Where(l => !string.IsNullOrEmpty(l)))
                {
                    bigText += lineCount + lineText + "\n";
                    lineCount++;
                }
            }
            textCSV = bigText + textCSV;

            TestIrregularColumnCountCases(textCSV, lineEnd, true);
        }

        private void TestIrregularColumnCountCases(string textCSV, string lineEnd, bool withDsvReaderError)
        {
            var docOrig = SkylineWindow.Document;
            TestError(textCSV, null, null, true); // That should just work
            WaitForDocumentChange(docOrig);
            NewDocument();

            // Now see what happens when we remove the trailing field in the header
            var shortHeader =
                textCSV.Replace(@"," + SmallMoleculeTransitionListColumnHeaders.rtPrecursor, string.Empty);
            AssertEx.AreNotEqual(shortHeader, textCSV, "did something change in the test code?");
            docOrig = SkylineWindow.Document;
            TestError(shortHeader, null, null, true);
            WaitForDocumentChange(docOrig);
            NewDocument();

            // Now see what happens when we remove the trailing field in a data line (so fewer columns in line than in header)
            var shortData = textCSV.Replace(lineEnd, @",-1");
            AssertEx.AreNotEqual(shortData, textCSV, "did something change in the test code?");
            docOrig = SkylineWindow.Document;
            TestError(shortData, null, null, true);
            WaitForDocumentChange(docOrig);
            NewDocument();

            // Now see what happens when we add an extra trailing field in a data line (so more columns in line than in header)
            const string suffixField = @",paintball";
            var longData = textCSV.Replace(lineEnd, lineEnd + suffixField);
            AssertEx.AreNotEqual(longData, textCSV, "did something change in the test code?");
            docOrig = SkylineWindow.Document;
            // When the user can't see the field to use in making column decisions, an error is shown
            string expectedError = withDsvReaderError ? GetDsvReaderError(longData, suffixField) : null;
            TestError(longData, expectedError, null, true);
            if (expectedError == null)
                WaitForDocumentChange(docOrig);
            NewDocument();
        }

        private static string GetDsvReaderError(string longData, string suffixField)
        {
            var lineText = ToLocalText(longData.Split('\n').First(l => l.EndsWith(suffixField)));
            return string.Format(Resources.DsvFileReader_ReadLine_Line__0__has__1__fields_when__2__expected_, lineText, 12, 11);
        }

        private void TestToolServiceAccess()
        {
            // Test the tool service logic without actually using tool service (there's a test for that too)
            var header = GetAminoAcidsTransitionListText(out var textCSV);

            var docOrig = SkylineWindow.Document;
            var textClean = textCSV;
            SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textClean, Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            }));

            var pastedDoc = WaitForDocumentChange(docOrig);
            Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assert.AreEqual(4, pastedDoc.MoleculeCount);

            // Inserting the header row by itself should produce an error message
            AssertEx.ThrowsException<InvalidDataException>(() => SkylineWindow.Invoke(new Action(() =>
                {
                    SkylineWindow.InsertSmallMoleculeTransitionList(header,
                        Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
                })),
                Resources.MassListImporter_Import_Empty_transition_list);

            // Now feed it some nonsense headers, verify helpful error message
            var textCSV2 = textCSV.Replace(SmallMoleculeTransitionListColumnHeaders.labelType, "labbel").Replace(SmallMoleculeTransitionListColumnHeaders.moleculeGroup,"grommet");
            AssertEx.ThrowsException<LineColNumberedIoException>(() => SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textCSV2,
                    Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            })),
                string.Format(Resources.SmallMoleculeTransitionListReader_SmallMoleculeTransitionListReader_,
                    TextUtil.LineSeparate(new[] { "grommet", "labbel", string.Empty }),
                    TextUtil.LineSeparate(SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms.Keys)));
            // This should still be close enough to correct that we can tell that's what the user was going for
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV2, SkylineWindow.Document.Settings));
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV2.ToLowerInvariant(), SkylineWindow.Document.Settings)); // Be case insensitive
            // But the word "peptide" should prevent us from trying to read this as small molecule data
            Assert.IsFalse(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV2.Replace("grommet", "Peptide"), SkylineWindow.Document.Settings));
           

            // And check for handling of localization
            var textCSV3 = textCSV.Replace(',', TextUtil.GetCsvSeparator(LocalizationHelper.CurrentCulture)).Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            NewDocument();
            docOrig =  WaitForDocumentChange(pastedDoc);
            SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textCSV3, Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            }));

            pastedDoc = WaitForDocumentChange(docOrig);
            Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assert.AreEqual(4, pastedDoc.MoleculeCount);

            // Check our ability to help users with nearly correct headers
            var nearly = "precsr";
            var textCSV4 = textCSV3.Replace(SmallMoleculeTransitionListColumnHeaders.namePrecursor, nearly);
            AssertEx.ThrowsException<LineColNumberedIoException>(() => SkylineWindow.Invoke(new Action(() =>
                {
                    SkylineWindow.InsertSmallMoleculeTransitionList(textCSV4,
                        Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
                })),
                string.Format(Resources.SmallMoleculeTransitionListReader_SmallMoleculeTransitionListReader_,
                    TextUtil.LineSeparate(new[] { nearly, string.Empty }),
                    TextUtil.LineSeparate(SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms.Keys)));
            // This should still be close enough to correct that we can tell that's what the user was going for
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV4, SkylineWindow.Document.Settings));

            // Check our ability to help users with localized headers that match the human readable names we use in the UI
            NewDocument();
            docOrig = WaitForDocumentChange(pastedDoc);
            var textCSV5 = textCSV3.Replace(SmallMoleculeTransitionListColumnHeaders.namePrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Name);
            SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textCSV5,
                    Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            }));
            pastedDoc = WaitForDocumentChange(docOrig);
            Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assert.AreEqual(4, pastedDoc.MoleculeCount);

            // Check ability to paste into the Skyline window
            // Use various combinations of CSV vs TSV and . vs ,
            var textCSV6 = textCSV;
            for (var style = 0; style < (CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator.Equals(".") ? 2 : 4); style++)
            {
                switch (style)
                {
                    case 0:
                        // CSV US
                        break;
                    case 1:
                        textCSV6 = textCSV6.Replace(",", "\t"); // TSV US
                        break;
                    case 2:
                        textCSV6 = textCSV6.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator); // TSV FR
                        break;
                    case 3:
                        textCSV6 = textCSV6.Replace("\t", ";"); // Excel FR
                        break;
                }
                pastedDoc = PasteNewDocument(textCSV6);
                Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
                Assert.AreEqual(4, pastedDoc.MoleculeCount);
            }

            // Check handling of transition list where precursor is indicated by leaving product columns empty
            var textCSV7 =
                "Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Explicit Retention Time,Collisional Cross Section (sq A),Product m/z,Product Charge\n" +
                "Lipid,L1,C41H74NO8P,[M+H],6.75,273.41,,\n" +
                "Lipid,L1,C41H74NO8P,[M+H],6.75,273.41,263.2371,1\n" +
                "Lipid,L2,C42H82NO8P,[M+Na],7.3,288.89,,\n" +
                "Lipid,L2,C42H82NO8P,[M+Na],7.3,288.89,184.0785,1\n";
            PasteNewDocument(textCSV7);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 2, 2, 4);

            // Check case insensitivity, m/z vs mz
            var textCSV8 =
                "MOLECULE LIST NAME,PRECURSOR NAME,PRECURSOR FORMULA,PRECURSOR ADDUCT,EXPLICIT RETENTION TIME,COLLISIONAL CROSS SECTION (SQ A),PRODUCT MZ,PRODUCT CHARGE\n" +
                "Lipid,L1,C41H74NO8P,[M+H],6.75,273.41,,\n" +
                "Lipid,L1,C41H74NO8P,[M+H],6.75,273.41,263.2371,1\n" +
                "Lipid,L2,C42H82NO8P,[M+Na],7.3,288.89,,\n" +
                "Lipid,L2,C42H82NO8P,[M+Na],7.3,288.89,184.0785,1\n";
            PasteNewDocument(textCSV8);  // Say no to the offer to set new nodes to automanage
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 2, 2, 4);

            // Paste in a peptide transition list with some distinctive small molecule headers
            var textCSV9 =
                "Protein Name,Modified Sequence, Precursor Mz,Precursor Charge, Collision Energy,Product Mz, MoleculeGroup, SMILES, KEGG\n" +
                "peptides1,PEPTIDER,478.737814,2,16.6,478.737814,2,precursor,precursor\n" +
                "peptides1,PEPTIDER,478.737814,2,16.6,730.372994,1,y6,y\n" +
                "peptides1,PEPTIDER,478.737814,2,16.6,633.32023,1,y5,y\n" +
                "peptides1,PEPTIDER,478.737814,2,16.6,532.272552,1,y4,y\n";
            // Check that we ignored the headers and looked for matching amino acid sequence and precursor m/z columns
            Assert.IsFalse(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV9, SkylineWindow.Document.Settings));
            // Paste in the document to make sure it imports properly
            LoadNewDocument(true);
            SetClipboardText(textCSV9);
            
            var peptideTransitionList = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            OkDialog(peptideTransitionList, peptideTransitionList.OkDialog);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 1, 4);

            // Examine a transition list with an amino acid sequence, but no precursor m/z column and some distinctive small molecule headers
            var textCSV10 =
                "Protein Name,Modified Sequence,Precursor Charge, Collision Energy,Product Mz, MoleculeGroup, SMILES, KEGG\n" +
                "peptides1,PEPTIDER,2,16.6,478.737814,2,precursor,precursor\n" +
                "peptides1,PEPTIDER,2,16.6,730.372994,1,y6,y\n" +
                "peptides1,PEPTIDER,2,16.6,633.32023,1,y5,y\n" +
                "peptides1,PEPTIDER,2,16.6,532.272552,1,y4,y\n";
            // We should realize the lack of a matching precursor m/z column, rely on the headers to make the decision,
            // and classify it as a small molecule transition list
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV10, SkylineWindow.Document.Settings));

            // Test how we categorize lists without peptide sequence columns or small molecule headers as proteomic or small molecule
            // If we cannot recognize the format of a transition list either way, we should rely on the mode set in the UI
            LoadNewDocument(true);
            var textCSV11 =
                "DrugX,Drug,light,283.04,1,129.96,1,26,16,2.7\n" +
                "DrugX,Drug,heavy,286.04,1,133.00,1,26,16,2.7\n";
            SetClipboardText(textCSV11);
            // Set the UI mode to small molecule
            RunUI(() =>
            {
                SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules);
            });
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            // Since this we are in small molecule mode the column selection page should be set to small molecule when it opens
            Assert.IsTrue(columnSelectDlg.radioMolecule.Checked);
            OkDialog(columnSelectDlg, columnSelectDlg.CancelButton.PerformClick);

            // If we set the UI mode to proteomics and paste the same transition list it should be categorized as a proteomics transition list
            LoadNewDocument(true);
            // Set the UI mode to proteomic
            RunUI(() =>
            {
                SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic);
            });
            // Because we recognize it as a peptide list, the column select mode should be set to proteomic
            var columnSelectDlg1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            Assert.IsFalse(columnSelectDlg1.radioMolecule.Checked);
            OkDialog(columnSelectDlg1, columnSelectDlg1.CancelDialog);
        }

        private static SrmDocument PasteNewDocument(string text, bool expectAutoManage = true)
        {
            NewDocument();
            return expectAutoManage ? PasteSmallMoleculeListNoAutoManage(text) : PasteSmallMoleculeList(text);
        }

        private static string GetAminoAcidsTransitionListText(out string textCSV)
        {
            var header = string.Join(",", new string[]
            {
                SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                SmallMoleculeTransitionListColumnHeaders.nameProduct,
                SmallMoleculeTransitionListColumnHeaders.labelType,
                SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                SmallMoleculeTransitionListColumnHeaders.mzProduct,
                SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
            });
            textCSV = header + "\n" +
                      "Amino Acids B,AlaB,,light,,,225.1,44,-1,-1,3\n" +
                      "Amino Acids B,ArgB,,light,,,310.2,217,-1,-1,19\n" +
                      "Amino Acids,Ala,,light,,,225,44,1,1,3\n" +
                      "Amino Acids,Ala,,heavy,,,229,48,1,1,4\n" + // NB we ignore RT conflicts
                      "Amino Acids,Arg,,light,,,310,217,1,1,19\n" +
                      "Amino Acids,Arg,,heavy,,,312,219,1,1,19\n" +
                      "Amino Acids B,AlaB,,light,,,225.1,45,-1,-1,3\n" +
                      "Amino Acids B,AlaB,,heavy,,,229,48,-1,-1,4\n" + // NB we ignore RT conflicts
                      "Amino Acids B,AlaB,,heavy,,,229,49,-1,-1,4\n" + // NB we ignore RT conflicts
                      "Amino Acids B,ArgB,,light,,,310.2,218,-1,-1,19\n" +
                      "Amino Acids B,ArgB,,heavy,,,312,219,-1,-1,19\n" +
                      "Amino Acids B,ArgB,,heavy,,,312,220,-1,-1,19\n";
            return header;
        }

        // Test uniform handling in File>Import>TransitionList, Edit>Insert>TransitionList, and pasting into Targets area
        private void TestImportMethods()
        {
            var filename = TestFilesDir.GetTestPath("heavy_and_light.txt");
            for (var pass = 0; pass < 3; pass++)
            {
                var doc = SkylineWindow.Document;
                switch (pass)
                {
                    case 0: // Use Edit | Insert | Transition List
                        TestError(GetCsvFileText(filename), null, null, true);
                        break;
                    case 1: // Use File | Import | Transition List
                        ImportTransitionListSkipColumnSelect(filename, null, true, true);
                        break;
                    case 2: // Paste into Targets window
                        var dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste(GetCsvFileText(filename)));
                        OkDialog(dlg, dlg.OkDialog);
                        DismissAutoManageDialog();  // Say no to the offer to set new nodes to automanage
                        break;
                }
                doc = WaitForDocumentChangeLoaded(doc);
                AssertEx.IsDocumentState(doc, null, 1, 244, 488, 1430);

                // In this data set, all heavy labeled precursors should have heavy labeled transitions
                foreach (var precursor in doc.MoleculeTransitionGroups)
                {
                    foreach (var transition in precursor.Transitions)
                    {
                        AssertEx.IsTrue(precursor.PrecursorAdduct.HasIsotopeLabels == transition.Transition.Adduct.HasIsotopeLabels ||
                                        !transition.Transition.CustomIon.ParsedMolecule.Molecule.TryGetValue(@"C", out _)); // Can't C13-label a fragment with no C in it
                    }
                }
                NewDocument();
            }
        }

        private void TestImpliedAdductWithSynonyms()
        {
            // Deal with implied adducts for which we support synonyms (this caused trouble with use of a dictionary in parser code, since synonyms yield identical mz matches) 
            var text = "moleculename\tmolecularformula\tprecursormz\nMyMol\tC40H23NO6\t658.1507"; // This m/z results from [M+FA-H], but we also accept [M+HCOO] as a synonym for that
            TestError(text, String.Empty, null); // Should load with no problem
            NewDocument();
        }

        private void TestIsotopeLabelsInInChi()
        {
            // Deal with isotope labels declared in inchi
            var text = "moleculename\tmolecularformula\tprecursormz\tinchi\nMyMol\tC8H8O\t126.076345\t1S/C8H8O/c1-7(9)8-5-3-2-4-6-8/h2-6H,1H3/i1D3,2C13,4C14"; // "i1D3,2C13,4C14"means Labeled with 3 H', C' at position 2 and C" at position 4
            TestError(text, String.Empty, null); // Should load with no problem
            var adduct = SkylineWindow.Document.MoleculeTransitions.First().Transition.Adduct;
            AssertEx.AreEqual("[M3H2C13C14+]", adduct.ToString());
            NewDocument();
        }

        private void TestImpliedFragmentAdduct()
        {
            // Test ability to infer fragment adduct from formula and stated m/z
            var text = 
                "MoleculeListName\tscan\tPrecursorCharge\tPrecursorAdduct\tinchikey\tmolecularformula\tmoleculename\thmdb or cas\texplicitretentiontime\texplicitretentiontimewindow\tMW\tprecursor m/z\tproductformula\tproduct m/z\tfrag1calc\tfrag2comp\n" +
                "QEP1_2021_0823_RJ_21_2ab55prm.raw\t3423\t1\t[M+H]\tQQVDJLLNRSOCEL-UHFFFAOYSA-N\tC2H8NO3P\t(2-AMINOETHYL)PHOSPHONATE\tHMDB11747\t5\t2\t125.0241796\t126.0320046\tCH6O3P\t97.0054556\tH4O4P\t98.9847214\n" + // Implied fragment M+
                "QEP1_2021_0823_RJ_31_2ef55prm.raw\t5909\t1\t[M+H]\tXFNJVJPLKCPIBV-UHFFFAOYSA-N\tC3H10N2\t\"1,3 - DIAMINOPROPANE\"\tHMDB00002\t8.9\t2\t74.0844\t75.09222333\tC3H8N\t59.0656708\tna\tna\n"; // Implied fragment M+H
            var docOrig = NewDocument();
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText = text);
            OkDialog(columnSelectDlg, columnSelectDlg.OkDialog);
            WaitForClosedForm(importDialog);

            var pastedDoc = WaitForDocumentChange(docOrig);
            AssertEx.IsDocumentState(pastedDoc, null, 2, 2, 2, 2);
            var count = 0;
            foreach (var transition in pastedDoc.MoleculeTransitions)
            {
                AssertEx.AreEqual(count++ == 0? Adduct.M_PLUS : Adduct.M_PLUS_H, transition.Transition.Adduct);
            }
            NewDocument();
        }

        private void TestRecognizeChargeState()
        {
            // Make sure we are properly recognizing these common header types
            var text = "Molecular Formula, Precursor Charge,Product Formula, Product Charge\nH2O10,1,HO10,1\nH2O10,1,HO6,1";
            var docOrig = NewDocument();
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText = text);
            OkDialog(columnSelectDlg, columnSelectDlg.OkDialog);
            WaitForClosedForm(importDialog);

            var pastedDoc = WaitForDocumentChange(docOrig);
            AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 1, 2);
            NewDocument();
        }

        private void TestMissingProductMZ()
        {
            // Make sure we properly handle missing product mz info
            var saveColumnOrder = Settings.Default.CustomMoleculeTransitionInsertColumnsList;
            var text = "Molecular Formula\tPrecursor Charge\tProduct Name\tProduct Charge\nH2O10\t1\tHO10\t1\nH2O10\t1\tHO6\t1";
            var docOrig = NewDocument();
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText = text);

            var errDlg = ShowDialog<ImportTransitionListErrorDlg>(columnSelectDlg.OkDialog);
            RunUI(() =>
            {
                var lineNum = 2;
                AssertEx.AreEqual(2, errDlg.ErrorList.Count);
                foreach (var err in errDlg.ErrorList)
                {
                    var errText = string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, lineNum++);
                    Assert.IsTrue(err.ErrorMessage.Contains(errText),
                        "Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                        errText, err.ErrorMessage);
                }
            });
            OkDialog(errDlg, errDlg.Close);
            OkDialog(columnSelectDlg, columnSelectDlg.CancelDialog);
            WaitForClosedForm(importDialog);

            NewDocument();
            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
        }

        private void TestLabelsNoFormulas()
        {
            // Test our handling of labels without formulas

            var saveColumnOrder = Settings.Default.CustomMoleculeTransitionInsertColumnsList;

            var docOrig = NewDocument();
            var importDialog3 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            const string transistionList =
                "Amino Acids B\tAlaB\t\tlight\t\t\t225\t44\t-1\t-1\t3\n" +
                "Amino Acids B\tArgB\t\tlight\t\t\t310\t217\t-1\t-1\t19\n" +
                "Amino Acids\tAla\t\tlight\t\t\t226.001\t226\t1\t1\t3\n" + // This should be read as a precursor transition
                "Amino Acids\tAla\t\tlight\t\t\t226.001\t44\t1\t1\t3\n" +
                "Amino Acids\tAla\t\theavy\t\t\t229\t48\t1\t1\t4\n" + // NB we ignore RT conflicts
                "Amino Acids\tArg\t\tlight\t\t\t310\t217\t1\t1\t19\n" +
                "Amino Acids\tArg\t\theavy\t\t\t312\t219\t1\t1\t19\n" +
                "Amino Acids B\tAlaB\t\tlight\t\t\t225\t45\t-1\t-1\t3\n" +
                "Amino Acids B\tAlaB\t\theavy\t\t\t229\t48\t-1\t-1\t4\n" + // NB we ignore RT conflicts
                "Amino Acids B\tAlaB\t\theavy\t\t\t229\t49\t-1\t-1\t4\n" + // NB we ignore RT conflicts
                "Amino Acids B\tArgB\t\tlight\t\t\t310\t218\t-1\t-1\t19\n" +
                "Amino Acids B\tArgB\t\theavy\t\t\t312\t219\t-1\t-1\t19\n" +
                "Amino Acids B\tArgB\t\theavy\t\t\t312\t220\t-1\t-1\t19\n";
            var col4Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.TransitionListText = transistionList.Replace(".",
                CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator));

            RunUI(() => {
                col4Dlg.radioMolecule.PerformClick();
                col4Dlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time);
            });

            OkDialog(col4Dlg, col4Dlg.OkDialog);
            DismissAutoManageDialog();  // Say no to the offer to set new nodes to automanage
            WaitForClosedForm(importDialog3);

            var pastedDoc = WaitForDocumentChange(docOrig);
            Assume.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(4, pastedDoc.MoleculeCount);
            var precursors = pastedDoc.MoleculeTransitionGroups.ToArray();
            Assume.IsTrue(!precursors[0].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(precursors[1].PrecursorAdduct.HasIsotopeLabels);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(1, transitions.Count(t => t.IsMs1));
            NewDocument();
            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
        }

        private void TestMissingAccessionNumbers()
        {
            // If bug is not fixed, we have trouble with entries which describe the same molecule but with a different but
            // non-conflicting set of accessions. This seems to happen when users cobble together transition lists from multiple sources.
            var text = 
                "Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Precursor Charge,Product m/z,Product Charge,Explicit Collision Energy,InChiKey,Explicit Declustering Potential,CAS,SMILES\n" +
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-L-fucose,C16H25N5O15P2,[M-H],-1,441.9,-1,24,LQEBEXMHBLQMDB-QIXZNPMTSA-N,168,,\n" + // Has InChiKey, no CAS
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-L-fucose,C16H25N5O15P2,[M-H],-1,158.8,-1,48,LQEBEXMHBLQMDB-QIXZNPMTSA-N,168,15839-70-0,\n" + // Has CAS in addition to InChiKey, 
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-L-fucose,C16H25N5O15P2,[M-H],-1,79,-1,92,,168,15839-70-0,\n" + // Has CAS, no InChiKey
                "\"Glycan, Amino and Nucleotide sugar metabolism\",,C16H25N5O15P2,[M+H],1,152,1,36,,124,15839-70-0,C[C@@H]1OC(OP(O)(=O)OP(O)(=O)OC[C@H]2O[C@H]([C@H](O)[C@@H]2O)[n]2c[n]c3c2N=C(N)NC3=O)[C@@H](O)[C@H](O)[C@@H]1O\n" + // No name, but same formula and same CAS, plus SMILES
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-L-fucose,C'16H25N5O15P2,[M16C13-H],-1,452.034,-1,32,,168,,\n" +
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-L-fucose,C'16H25N5O15P2,[M16C13-H],-1,79,-1,92,,168,,\n" +
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-mannose,C16H25N5O16P2,[M-H],-1,423.9,-1,32,,178,,\n" +
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-mannose,C16H25N5O16P2,[M-H],-1,158.8,-1,48,MVMSCBBUIHUTGJ-GDJBGNAASA-N,178,,\n" +
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-mannose,C16H25N5O16P2,[M-H],-1,79,-1,100,MVMSCBBUIHUTGJ-GDJBGNAASA-N,173,,\n" +
                "\"Glycan, Amino and Nucleotide sugar metabolism\",,C'16H25N5O16P2,[M16C13-H],-1,434.03355,-1,42,MVMSCBBUIHUTGJ-GDJBGNAASA-N,173,,\n" + // No name, same InChiKey, formula matches when unlabeled
                "\"Glycan, Amino and Nucleotide sugar metabolism\",GDP-mannose,C'16H25N5O16P2,[M16C13-H],-1,79,-1,100,MVMSCBBUIHUTGJ-GDJBGNAASA-N,173,,";

            PasteToTargetsWindow(text); // Paste text, expect only header confirmation dialog

            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 2, 5, 11);
            // All those lines with bits and pieces of non-conflict accession info should unite into a single molecule with all that info
            var accessionNumbers = SkylineWindow.Document.CustomMolecules.First().CustomMolecule.AccessionNumbers;
            AssertEx.AreEqual("LQEBEXMHBLQMDB-QIXZNPMTSA-N", accessionNumbers.GetInChiKey());
            AssertEx.AreEqual("15839-70-0", accessionNumbers.GetCAS());
            AssertEx.AreEqual("C[C@@H]1OC(OP(O)(=O)OP(O)(=O)OC[C@H]2O[C@H]([C@H](O)[C@@H]2O)[n]2c[n]c3c2N=C(N)NC3=O)[C@@H](O)[C@H](O)[C@@H]1O",accessionNumbers.GetSMILES());
            AssertEx.AreEqual("MVMSCBBUIHUTGJ-GDJBGNAASA-N", SkylineWindow.Document.CustomMolecules.Last().CustomMolecule.AccessionNumbers.GetInChiKey());
            NewDocument();
        }

        private void TestMzOrderIndependence()
        {
            // Ensure that the line order of an m/z-only mixed polarity transition list does not matter
            var texts = new[]
            {
                "Molecule list name,Molecule name,Precursor m/z,Precursor Charge,Product m/z,Product Charge,Label type\n" +
                "Compounds,Mol1,430.1,1,236.1,1,light\n" +
                "Compounds,Mol1,228.1,-1,144.1,-1,light\n", // If bug is not fixed, this order won't give same doc structure as the other

                "Molecule list name,Molecule name,Precursor m/z,Precursor Charge,Product m/z,Product Charge,Label type\n" +
                "Compounds,Mol1,228.1,-1,144.1,-1,light\n" +
                "Compounds,Mol1,430.1,1,236.1,1,light\n"
            };
            foreach (var text in texts)
            {
                PasteToTargetsWindow(text); // Paste text, expect only header confirmation dialog
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 2, 2);
                AssertEx.AreEqual(228, SkylineWindow.Document.Molecules.First().Target.Molecule.AverageMass, 1);
                AssertEx.IsTrue(SkylineWindow.Document.MoleculeTransitionGroups.Contains(t => 
                    t.PrecursorAdduct.AdductCharge < 0 && !t.PrecursorAdduct.HasIsotopeLabels));
                AssertEx.IsTrue(SkylineWindow.Document.MoleculeTransitionGroups.Contains(t =>
                    t.PrecursorAdduct.AdductCharge > 0 && t.PrecursorAdduct.HasIsotopeLabels));
                AssertEx.IsTrue(SkylineWindow.Document.MoleculeTransitions.Contains(t => !t.IsMs1 &&
                    t.Transition.IsNegative() && Math.Abs(t.Transition.CustomIon.AverageMassMz - 144.1) < 1));
                AssertEx.IsTrue(SkylineWindow.Document.MoleculeTransitions.Contains(t => !t.IsMs1 &&
                    !t.Transition.IsNegative() && Math.Abs(t.Transition.CustomIon.AverageMassMz - 236.1) < 1));
                NewDocument();
            }
        }

        private static void PasteToTargetsWindow(string text)
        {
            SetClipboardText(text);
            // Paste directly into targets area - no interaction expected beyond header confirmation dialog
            var docCurrent = SkylineWindow.Document;
            var confirmHdrsDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            OkDialog(confirmHdrsDlg, confirmHdrsDlg.OkDialog);
            WaitForDocumentChange(docCurrent);
        }

        private void TestFormulaWithAtomCountZero()
        {
            // Make sure that H'0 doesn't cause trouble - throws "System.Collections.Generic.KeyNotFoundException: The given key was not present in the dictionary."
            // in StripLabelsFromFormula() in pwiz_tools\Skyline\Util\BioMassCalc.cs if not fixed.
            var text =
                "MoleculeGroup,PrecursorName,PrecursorFormula,PrecursorAdduct,PrecursorMz,PrecursorCharge,ProductName,ProductFormula,ProductAdduct,ProductMz,ProductCharge\n" +
                "AMPP_FA,AMPP_16:0_1.04,C28H42N2O1XeH'0,[M]1+,,1,AMPP_16:0_precursor,C28H42N2O1H'0,[M]1+,,1\n" + // Data from the wild
                "AMPP_FA,AMPP_18:0_1.04,C30H46N2O1XeH'0,[M]1+,,1,AMPP_18:0_precursor,C30H46N2O1H'0,[M]1+,,1\n" + // Data from the wild
                "AMPP_FA,AMPP_17:0_1.04,C28H0N2O1XeH'41,[M]1+,,1,AMPP_17:0_precursor,C28H0N2O1XeH'41,[M]1+,,1\n" + // Made up values
                "AMPP_FA,AMPP_14:0_1.04,C28H0N2O1XeH'0,[M]1+,,1,AMPP_14:0_precursor,C28H0N2O1XeH'0,[M]1+,,1\n" + // Made up values
                "AMPP_FA,AMPP_19:0_1.04,C30N2O1XeH'45,[M]1+,,1,AMPP_19:0_precursor,C30N2O1XeH'45,[M]1+,,1\n"; // Made up values

            // Paste directly into targets area - expect to be asked about automanage
            PasteNewDocument(text);  // Say no to the offer to set new nodes to automanage
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 5, 5, 5);
            var docMolecules = SkylineWindow.Document.CustomMolecules.Select(mol => mol.CustomMolecule.ParsedMolecule).ToArray();
            AssertEx.AreEqual("C28H42N2O1Xe", docMolecules[0].ToString());
            AssertEx.AreEqual("C30H46N2O1Xe", docMolecules[1].ToString());
            AssertEx.AreEqual("C28N2OXeH41", docMolecules[2].ToString());
            AssertEx.AreEqual("C28N2O1Xe", docMolecules[3].ToString());
            AssertEx.AreEqual("C30N2OXeH45", docMolecules[4].ToString()); // We intentionally preserve nonstandard order
            NewDocument();
        }

        private void TestNegativeModeLabels()
        {
            // If bug is not fixed, the negative heavy does not appear in the document
            var text = "Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Precursor Charge,Label Type\n" +
                       "compound_of_interest,Taurin,C2H7NO3S,[M+H]+,1,light\n" +
                       "compound_of_interest, Taurin, C'2H7NO3S,[M+H]+,1,heavy\n" +
                       "compound_of_interest,Taurin,C2H7NO3S,[M-H]-,-1,light\n" +
                       "compound_of_interest, Taurin, C'2H7NO3S,[M-H]-,-1,heavy";
            PasteToTargetsWindow(text); // Paste text, expect only header confirmation dialog

            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 4, 4);
            NewDocument();
        }
        
        private void TestEmptyTransitionList()
        {
            var text = "Precursor Name\t Precursor Formula \tPrecursor Charge \tPrecursor Adduct \tPrecursor m/z\n" +
                       "\n";
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var errDlg = ShowDialog<MessageDlg>(() => importDialog.TransitionListText = text); // Testing that we catch the exception properly
            OkDialog(errDlg, errDlg.Close);
        }

        private void TestErrorDialog()
        {
            var text = "Precursor Name	Precursor Formula \tPrecursor Charge \tPrecursor Adduct \tPrecursor m/z\n" +
                       "Acetic Acid\tC2H4O2\t1\t[M-H]\t59\n" +
                       "Acetic Acid\tC2H4O2\t1\t[6M-H6+Fe3+O]\t536.88\n" +
                       "Acetic Acid\tC2H4O2\t1\t[6M-H6+H2O+Fe3+O]\t555.88\n" +
                       "Acetic Acid\tC2H4O2\t1\t[7M-H6+Fe3+O]\t596.9\n" +
                       "Acetic Acid\tC2H4O2\t1\t[8M-H6+Fe3+O]\t657.92\n";
            SetClipboardText(text);
            // Paste directly into targets area
            var transitionDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            var errDlg = ShowDialog<ImportTransitionListErrorDlg>(() => transitionDlg.buttonCheckForErrors.PerformClick());
            RunUI(() => errDlg.ShowLineText(true));

            /*
             Expect errors
                Adduct[M - H] charge - 1 does not agree with declared charge 1    2   4   Acetic Acid C2H4O2 1 [M-H] 59
                Error on line 3: Precursor m/ z 536,88 does not agree with value 537,879 as calculated from ion formula and charge state(delta = 0,9990012, Transition Settings | Instrument | Method match tolerance m / z = 0,055).  Correct the m / z value in the table, or leave it blank and Skyline will calculate it for you.   3   5   Acetic Acid C2H4O2 1 [6M-H6+Fe3+O] 536.88
                Error on line 5: Precursor m / z 596, 9 does not agree with value 597, 9001 as calculated from ion formula and charge state(delta = 1, 000131, Transition Settings | Instrument | Method match tolerance m / z = 0, 055).Correct the m / z value in the table, or leave it blank and Skyline will calculate it for you.    5   5   Acetic Acid C2H4O2 1 [7M-H6+Fe3+O] 596.9
            */
            AssertEx.AreEqual(3, errDlg.ErrorList.Count);
            AssertEx.AreEqual(new TransitionImportErrorInfo(string.Format(
                    Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_, "[M-H]", -1, 1),
                3, 2, "Acetic Acid C2H4O2 1 [M-H] 59"), errDlg.ErrorList[0]);
            AssertEx.AreEqual(new TransitionImportErrorInfo(string.Format(
                    Resources.SmallMoleculeTransitionListReader_Precursor_mz_does_not_agree_with_calculated_value_,
                    536.88, 537.879, 0.9990012, (float)SkylineWindow.Document.Settings.TransitionSettings.Instrument.MzMatchTolerance), 
                4, 3, "Acetic Acid C2H4O2 1 [6M-H6+Fe3+O] 536.88"), errDlg.ErrorList[1]);
            AssertEx.AreEqual(new TransitionImportErrorInfo(string.Format(
                    Resources.SmallMoleculeTransitionListReader_Precursor_mz_does_not_agree_with_calculated_value_,
                    596.9, 597.9001, 1.000131, (float)SkylineWindow.Document.Settings.TransitionSettings.Instrument.MzMatchTolerance),
                4, 5, "Acetic Acid C2H4O2 1 [7M-H6+Fe3+O] 596.9"), errDlg.ErrorList[2]);
            OkDialog(errDlg, errDlg.OkDialog);
            OkDialog(transitionDlg, transitionDlg.CancelDialog);
        }

        private void TestProductNeutralLoss()
        {
            // Test our handling of fragment product loss formulas

            var columns = new[]
            {
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                Resources.PasteDlg_UpdateMoleculeType_Product_Neutral_Loss,
                Resources.PasteDlg_UpdateMoleculeType_Product_Adduct,
                Resources.PasteDlg_UpdateMoleculeType_Note,
                Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy
            };
            TestError("12-HETE\t12-HETE\t\t[M-H]1-\t319.227868554909\t-1\tfrag1\tC20H32O3\t[M-H]1-\tblah\t21", Resources.SmallMoleculeTransitionListReader_ProcessNeutralLoss_Cannot_use_product_neutral_loss_chemical_formula_without_a_precursor_chemical_formula, columns);
            TestError("12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tfrag1\tgreebles\t[M-H]1-\tblah\t21", string.Format(Resources.BioMassCalc_CalculateMass_The_expression__0__is_not_a_valid_chemical_formula, "greebles"), columns);
            TestError("12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tfrag1\t77\t[M-H]1-\tblah\t21", string.Format(Resources.BioMassCalc_CalculateMass_The_expression__0__is_not_a_valid_chemical_formula, "77"), columns);

            var precursorsTransitionList =
                "MoleculeGroup\tPrecursorName\tPrecursorFormula\tPrecursorAdduct\tPrecursorMz\tPrecursorCharge\tProductName\tProductFormula\tProductNeutralLoss\tProductAdduct\tNote\tPrecursorCE\n" +
                "12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tprecursor\tC20H32O3\t\t[M-H]1-\t\t21\n" +
                "12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tfrag1\tC20H30O\t\t[M-H]1-\t\t21\n" +
                "12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tfrag2\t\tH2O\t[M-H]1-\t\t21\n";
            var text = precursorsTransitionList.Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            // Paste directly into targets area
            var pastedDoc = PasteNewDocument(text);  // Say no to the offer to set new nodes to automanage

            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(1, pastedDoc.MoleculeCount);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(2, transitions.Count(t => !t.IsMs1));
            Assume.AreEqual("C20H30O", transitions[1].CustomIon.ParsedMolecule.ToString()); // As given literally
            Assume.AreEqual("C20H30O2", transitions[2].CustomIon.ParsedMolecule.ToString()); // As given by neutral loss
            NewDocument();

        }

        private void TestFullyDescribedPrecursors()
        {
            // Test our handling of fully described precursors

            const string precursorsTransitionList =
            "MoleculeGroup,PrecursorName,PrecursorFormula,PrecursorAdduct,PrecursorMz,PrecursorCharge,ProductName,ProductFormula,ProductAdduct,ProductMz,ProductCharge,Note,PrecursorCE,Collisional Cross Section\n"+
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,precursor,C20H32O3,[M-H]1-,319.227868554909,-1,,21,123\n" +
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,m/z 301.2172,,[M-H]1-,301.2172,-1,,21,123\n" +
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,m/z 275.2377,,[M-H]1-,275.2377,-1,,21,123\n" +
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,precursor,C20H32O3,[M8H2-H]1-,327.278082506909,-1,,21,126\n" +
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,m/z 309.2674,,[M-H]1-,309.2674,-1,,21,126\n" +
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,m/z 283.2879,,[M-H]1-,283.2879,-1,,21,126\n";

            // Paste directly into targets area
            var pastedDoc = PasteNewDocument(precursorsTransitionList);  // Say no to the offer to set new nodes to automanage

            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(2, pastedDoc.MoleculeCount);
            var precursors = pastedDoc.MoleculeTransitionGroups.ToArray();
            Assume.IsTrue(!precursors[0].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(precursors[1].PrecursorAdduct.HasIsotopeLabels);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(2, transitions.Count(t => t.IsMs1));
            Assume.AreEqual(123.0, precursors[0].ExplicitValues.CollisionalCrossSectionSqA);
            Assume.AreEqual(126.0, precursors[1].ExplicitValues.CollisionalCrossSectionSqA);
            NewDocument();
        }

        // We want to preserve order while dealing with lists that show heavy versions before light
        private void TestUnsortedMzPrecursors()
        {
            // Version with light mz presented first
            const string precursorsTransitionListSorted =
                "Molecule List Name\tPrecursor Name\tPrecursor Formula\tPrecursor Adduct\tPrecursor m/z\tPrecursor Charge\tProduct Formula\tProduct Adduct\tProduct m/z\tProduct Charge\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t230.1\t1\t\tM+\t73.1\t1\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t234.1\t1\t\tM+\t73.1\t1\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t263.1\t1\t\tM+\t147.1\t1\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t258.1\t1\t\tM+\t147.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t130.1\t1\t\tM+\t73.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t134.1\t1\t\tM+\t73.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t163.1\t1\t\tM+\t147.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t158.1\t1\t\tM+\t147.1\t1\n";

            // Version with heavy mz presented first - this used to screw us up
            const string precursorsTransitionListUnsorted =
                "Molecule List Name\tPrecursor Name\tPrecursor Formula\tPrecursor Adduct\tPrecursor m/z\tPrecursor Charge\tProduct Formula\tProduct Adduct\tProduct m/z\tProduct Charge\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t234.1\t1\t\tM+\t73.1\t1\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t263.1\t1\t\tM+\t147.1\t1\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t230.1\t1\t\tM+\t73.1\t1\n" +
                "Pyr-Glu\tPyr-Glu\t\tM+\t258.1\t1\t\tM+\t147.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t134.1\t1\t\tM+\t73.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t163.1\t1\t\tM+\t147.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t130.1\t1\t\tM+\t73.1\t1\n" +
                "aPyr-GluB\taPyr-GluB\t\tM+\t158.1\t1\t\tM+\t147.1\t1\n";

            var docOrig = NewDocument();
            PasteToTargetsWindow(precursorsTransitionListSorted); // Paste text, expect only header confirmation dialog
            var pastedDocSorted = WaitForDocumentChange(docOrig);

            docOrig = NewDocument();
            PasteToTargetsWindow(precursorsTransitionListUnsorted); // Paste text, expect only header confirmation dialog
            var pastedDocUnsorted = WaitForDocumentChange(docOrig);

            Assume.AreEqual(2, pastedDocUnsorted.MoleculeGroupCount);
            Assume.AreEqual(2, pastedDocUnsorted.MoleculeCount);
            Assume.AreEqual(pastedDocSorted, pastedDocUnsorted);
            var precursors = pastedDocUnsorted.MoleculeTransitionGroups.ToArray();
            Assume.IsTrue(!precursors[0].PrecursorAdduct.HasIsotopeLabels);
            Assume.AreEqual(precursors[0].PrecursorMz.Value, 230.1);
            Assume.IsTrue(precursors[1].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(!precursors[4].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(precursors[5].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(precursors[6].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(precursors[7].PrecursorAdduct.HasIsotopeLabels);

            docOrig = NewDocument();
            SetClipboardText(precursorsTransitionListUnsorted.Replace("M+", "[123.456]")); // This used to throw in SortSiblingsByMass()
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste()); // Instead it should show the column select dialog
            OkDialog(columnSelectDlg, columnSelectDlg.CancelDialog);

            NewDocument();
        }

        private static SrmDocument SetValuesAffectingAutomanage(bool wantAutoManage = false)
        {
            var docResult = SkylineWindow.Document;
            // Set up FullScan filter as ion types precursor+custom, with automanage as desired
            RunUI(() => SkylineWindow.ModifyDocument("Change isotope peaks count and ion types",
                doc => doc.ChangeSettings(doc.Settings
                    .ChangeTransitionInstrument(instrument => instrument.ChangeMaxMz(2500))
                    .ChangeTransitionFullScan(fs =>
                        fs.ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 3, IsotopeEnrichmentsList.GetDefault())
                            .ChangeAcquisitionMethod(FullScanAcquisitionMethod.DIA, new IsolationScheme("Test", 2)))
                    .ChangeTransitionFilter(f => f.ChangeSmallMoleculeIonTypes(new[] { IonType.custom, IonType.precursor }).ChangeAutoSelect(wantAutoManage)))));
            docResult = WaitForDocumentChange(docResult);
            return docResult;
        }

        private void TestAutoManage()
        {


            const string text =
                "Precursor Name,Precursor Formula,Precursor Adduct,Precursor charge,Explicit Retention Time,Collisional Cross Section (Sq A),Product m/z,product charge,explicit ion mobility High energy Offset,Explicit Collision Energy\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,,,,1\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,156.0112,1,0.5,1\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,92.0498,1,0.51,1\n";

            NewDocument();
            var docOrig = SetValuesAffectingAutomanage(); // Turn off automanage, so the user gets asked
            SetClipboardText(text);
            // Paste directly into targets area, expect to be asked about automanage
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            // Since this we are in small molecule mode the column selection page should be set to small molecule when it opens
            Assert.IsTrue(columnSelectDlg.radioMolecule.Checked);
            var wantAutoManageDlg = ShowDialog<MultiButtonMsgDlg>(() => columnSelectDlg.OkDialog());
            OkDialog(wantAutoManageDlg, wantAutoManageDlg.CancelDialog);
            AssertEx.IsTrue(SkylineWindow.Document.MoleculeCount == 0); // Canceled

            // Paste again, this time rejecting the auto manage
            docOrig = SkylineWindow.Document;
            SetClipboardText(text);
            var columnSelectDlg2 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            wantAutoManageDlg = ShowDialog<MultiButtonMsgDlg>(() => columnSelectDlg2.OkDialog());
            OkDialog(wantAutoManageDlg, wantAutoManageDlg.ClickNo);
            var pastedDoc = WaitForDocumentChange(docOrig);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 1, 3);

            // Because we created nodes with auto manage off, changing these settings should not change the nodes (1 precursor, two fragments)
            pastedDoc = SetValuesAffectingAutomanage();
            AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 1, 3);

            foreach (var managed in new[] {false, true})
            {
                AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 1, managed ? 5 : 3);
                RunUI(() => SkylineWindow.ModifyDocument(" Turn off precursors",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                        f.ChangeSmallMoleculeIonTypes(new[] { IonType.custom })))));  
                pastedDoc = WaitForDocumentChange(pastedDoc);
                AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 1, managed ? 2 : 3);
                RunUI(() => SkylineWindow.ModifyDocument("Turn on precursors",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                        f.ChangeSmallMoleculeIonTypes(new[] { IonType.custom, IonType.precursor })))));  
                pastedDoc = WaitForDocumentChange(pastedDoc);
                AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 1, managed ? 5 : 3);

                // Automanage has no effect on fragments, since we can't generate those for small molecules the way we do for peptides
                RunUI(() => SkylineWindow.ModifyDocument(" Turn off fragments",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                        f.ChangeSmallMoleculeIonTypes(new[] { IonType.precursor })))));
                pastedDoc = WaitForDocumentChange(pastedDoc);
                AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 1, managed ? 5 : 3);
                RunUI(() => SkylineWindow.ModifyDocument("Turn on fragments",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                        f.ChangeSmallMoleculeIonTypes(new[] { IonType.custom, IonType.precursor })))));
                pastedDoc = WaitForDocumentChange(pastedDoc);
                AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 1, managed ? 5 : 3);

                if (!managed)
                {
                    // Now turn on auto manage children, settings should have an effect on doc structure
                    pastedDoc = SetValuesAffectingAutomanage(true);
                    pastedDoc = EnableAutomanageChildren(pastedDoc);
                }
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 1, 5);
            }

            // Now import again, this time with auto manage on
            NewDocument();
            docOrig = SetValuesAffectingAutomanage();
            SetClipboardText(text);
            var columnSelectDlg3 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            // Since this we are in small molecule mode the column selection page should be set to small molecule when it opens
            Assert.IsTrue(columnSelectDlg3.radioMolecule.Checked);
            wantAutoManageDlg = ShowDialog<MultiButtonMsgDlg>(() => columnSelectDlg3.OkDialog());
            OkDialog(wantAutoManageDlg, wantAutoManageDlg.OkDialog);
            pastedDoc = WaitForDocumentChange(docOrig);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 1, 5);

            NewDocument(); // Clean up

            // Now try with transition list that has more detail
            const string text2 =
                "Precursor name;Precursor formula;Precursor charge;Product name;Product formula;Product charge\n" +
                "H7N2;H106C65N4O46;1;N1;H13C8N1O5;1\n" +
                "H5N3F1;H109C67N5O45;1;N1;H13C8N1O5;1\n" +
                "H7N2;H106C65N4O46;1;N1-2AB;H23C15N3O6;1\n" +
                "H5N3F1;H109C67N5O45;1;N1-2AB;H23C15N3O6;1\n" +
                "H7N2;H106C65N4O46;1;H1N3-2AB;H59C37N5O21;1\n" +
                "H5N3F1;H109C67N5O45;1;H1N3-2AB;H59C37N5O21;1\n" +
                "H7N2;H106C65N4O46;1;H1N3F1-2AB;H69C43N5O25;1\n" +
                "H5N3F1;H109C67N5O45;1;H1N3F1-2AB;H69C43N5O25;1\n" +
                "H7N2;H106C65N4O46;1;N2;H26C16N2O10;1\n" +
                "H5N3F1;H109C67N5O45;1;N2;H26C16N2O10;1\n" +
                "H7N2;H106C65N4O46;1;H1N1;H23C14N1O10;1\n" +
                "H5N3F1;H109C67N5O45;1;H1N1;H23C14N1O10;1\n" +
                "H7N2;H106C65N4O46;1;N1F1-2AB;H33C21N3O10;1\n" +
                "H5N3F1;H109C67N5O45;1;N1F1-2AB;H33C21N3O10;1\n" +
                "H7N2;H106C65N4O46;1;H1N1F1;H33C20N1O14;1";
            docOrig = SetValuesAffectingAutomanage();
            SetClipboardText(text2);
            var columnSelectDlg4 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            // Since this we are in small molecule mode the column selection page should be set to small molecule when it opens
            Assert.IsTrue(columnSelectDlg4.radioMolecule.Checked);
            wantAutoManageDlg = ShowDialog<MultiButtonMsgDlg>(() => columnSelectDlg4.OkDialog());
            OkDialog(wantAutoManageDlg, wantAutoManageDlg.OkDialog);
            pastedDoc = WaitForDocumentChange(docOrig);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 2, 2, 21);
            NewDocument(); // Clean up
        }

        private static SrmDocument EnableAutomanageChildren(SrmDocument pastedDoc)
        {
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.AutoPrecursors = true;
                refineDlg.AutoTransitions = true;
                refineDlg.OkDialog();
            });
            pastedDoc = WaitForDocumentChange(pastedDoc);
            return pastedDoc;
        }

        private void TestPerTransitionValues()
        {
            // Test our handling of fragments with unique explicit values
            const string precursorsTransitionList =
                "Molecule List Name,Molecule,Label Type,Precursor m/z,Precursor Charge,Product m/z,Product Charge,Explicit Collision Energy,Explicit Retention Time\n" +
                "ThompsonIS,Apain,light,452,1,384,1,20,1\n" +
                "ThompsonIS,Apain,light,452,1,188,1,25,1\n" +
                "ThompsonIS,Apain,light,452,1,160,1,,1\n" + // No explicit CE
                "ThompsonIS,Apain,light,452,1,140,1,20,1\n" + // Same explicit CE as first
                "ThompsonIS,Apain,heavy,455,1,387,1,21,1\n" +
                "ThompsonIS,Apain,heavy,455,1,191,1,26,1\n" +
                "ThompsonIS,Bpain,light,567,1,,,35,1\n"; // Precursor-only explicit CE

            // Paste directly into targets area
            var pastedDoc = PasteNewDocument(precursorsTransitionList);  // Say no to the offer to set new nodes to automanage

            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            AssertEx.AreEqual(2, pastedDoc.MoleculeCount);
            var molecules = pastedDoc.Molecules.ToArray();
            var precursors = pastedDoc.MoleculeTransitionGroups.ToArray();
            Assume.IsTrue(!precursors[0].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(precursors[1].PrecursorAdduct.HasIsotopeLabels);
            AssertEx.AreEqual(20, precursors[0].ExplicitValues.CollisionEnergy); // First-seen CE is taken as default for transition group
            AssertEx.AreEqual(21, precursors[1].ExplicitValues.CollisionEnergy);
            AssertEx.AreEqual(35, precursors[2].ExplicitValues.CollisionEnergy);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();

            // Apain light
            AssertEx.IsFalse(transitions[0].ExplicitValues.CollisionEnergy.HasValue); // Should pull from precursor explicit CE
            AssertEx.AreEqual(20, pastedDoc.GetCollisionEnergy(molecules[0], precursors[0], transitions[0], 0));
            AssertEx.AreEqual(25, pastedDoc.GetCollisionEnergy(molecules[0], precursors[0], transitions[1], 0));
            AssertEx.IsFalse(transitions[2].ExplicitValues.CollisionEnergy.HasValue); // Should pull from precursor explicit CE
            int stepsize = 1;
            AssertEx.AreEqual(20 + stepsize, pastedDoc.GetCollisionEnergy(molecules[0], precursors[0], transitions[2], stepsize));
            AssertEx.IsFalse(transitions[3].ExplicitValues.CollisionEnergy.HasValue); // Should pull from precursor explicit CE
            stepsize++;
            AssertEx.AreEqual(20 + stepsize, pastedDoc.GetCollisionEnergy(molecules[0], precursors[0], transitions[3], stepsize));

            // Apain heavy
            AssertEx.IsFalse(transitions[4].ExplicitValues.CollisionEnergy.HasValue); // Should pull from precursor explicit CE
            AssertEx.AreEqual(21, pastedDoc.GetCollisionEnergy(molecules[0], precursors[1], transitions[4], 0));
            AssertEx.AreEqual(26, pastedDoc.GetCollisionEnergy(molecules[0], precursors[1], transitions[5], 0));

            // Bpain
            AssertEx.IsFalse(transitions[6].ExplicitValues.CollisionEnergy.HasValue); // Should pull from precursor explicit CE
            AssertEx.AreEqual(35, pastedDoc.GetCollisionEnergy(molecules[1], precursors[2], transitions[6], 0));


            TestTransitionListOutput(pastedDoc, "per_trans.csv", "per_trans_expected.csv", ExportFileType.List);
            

            NewDocument();
            const string precursorsTransitionListHEOffset =
                "Precursor Name,Precursor Formula,Precursor Adduct,Precursor charge,Explicit Retention Time,Collisional Cross Section (Sq A),Product m/z,product charge,explicit ion mobility High energy Offset,Explicit Collision Energy\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,,,,1\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,156.0112,1,0.5,1\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,92.0498,1,0.51,1\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+Na],1,1.85,173.43,,,,2\n" +
                "Sulfamethazine,C12H14N4O2S,[M+H],1,2.01,163.56,,,,1\n" +
                "Sulfamethazine,C12H14N4O2S,[M+H],1,2.01,,186.0336,1,0.2,1\n" +
                "Sulfamethazine,C12H14N4O2S,[M+H],1,2.01,,124.0873,1,0.21,1\n" +
                "Sulfamethazine,C12H14N4O2S,[M+Na],1,2.01,172.47,,,,2\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+H],1,2.51,161.23,,,,1\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+H],1,2.51,161.23,156.011,1,0.1,2\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+H],1,2.51,161.23,92.0495,1,0.11,3\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+Na],1,2.51,171.16,,,,\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+H],1,3.68,170.01,,,,\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+H],1,3.68,170.01,156.077,1,0.3,1\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+H],1,3.68,170.01,108.0445,1,0.31,1\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+Na],1,3.68,177.96,,,,\n";
            SetClipboardText(precursorsTransitionListHEOffset);

            // Paste directly into targets area
            pastedDoc = PasteNewDocument(precursorsTransitionListHEOffset);  // Say no to the offer to set new nodes to automanage

            for (var roundtrips = 0; roundtrips < 2; roundtrips++)
            {
                Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
                Assume.AreEqual(4, pastedDoc.MoleculeCount);
                precursors = pastedDoc.MoleculeTransitionGroups.ToArray();
                Assume.AreEqual(8, precursors.Length);
                transitions = pastedDoc.MoleculeTransitions.ToArray();
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.5));
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.51));
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.2));
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.21));
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.1));
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.11));
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.3));
                Assume.AreEqual(1, transitions.Count(t => t.ExplicitValues.IonMobilityHighEnergyOffset == 0.31));

                // Testing explicit CE behavior
                foreach (var precursor in pastedDoc.MoleculeTransitionGroups)
                {
                    switch (precursor.Peptide.Target.DisplayName)
                    {
                        case "Sulfamethizole":
                        {
                            // Sulfamethizole[M+H] all set to 1, should be stored at transition group level
                            // Sulfamethizole[M+Na] all set to 2, should be stored at transition group level
                            var expectedPrecursorExplicitCE = precursor.PrecursorAdduct.Equals(Adduct.M_PLUS_H) ? 1 : 2;
                            AssertEx.AreEqual(expectedPrecursorExplicitCE, precursor.ExplicitValues.CollisionEnergy);
                            AssertEx.IsTrue(precursor.Transitions.All(t => !t.ExplicitValues.CollisionEnergy.HasValue));
                            break;
                        }
                        case "Sulfachloropyridazine":
                        {
                            // Sulfachloropyridazine[M+H] all set differently, all but first should be stored at transition level
                            // Sulfachloropyridazine[M+Na] no value set
                            var expectedPrecursorExplicitCE = precursor.PrecursorAdduct.Equals(Adduct.M_PLUS_H) ? 1 : (double?)null;
                            AssertEx.AreEqual(expectedPrecursorExplicitCE, precursor.ExplicitValues.CollisionEnergy);
                            foreach (var transition in precursor.Transitions)
                            {
                                if (expectedPrecursorExplicitCE == null)
                                {
                                    AssertEx.IsFalse(transition.ExplicitValues.CollisionEnergy.HasValue); // First-seen sets the default precursor value
                                    break;
                                }
                                else if (expectedPrecursorExplicitCE == 1)
                                {
                                    AssertEx.IsFalse(transition.ExplicitValues.CollisionEnergy.HasValue); // First-seen sets the default precursor value
                                }
                                else
                                {
                                    AssertEx.AreEqual(expectedPrecursorExplicitCE, transition.ExplicitValues.CollisionEnergy.Value);
                                }

                                expectedPrecursorExplicitCE++;
                            }
                            break;
                        }
                        case "Sulfadimethoxine":
                        {
                            AssertEx.IsFalse(precursor.ExplicitValues.CollisionEnergy.HasValue);
                            double? expectedCE = null;
                            foreach (var transition in precursor.Transitions)
                            {
                                // Sulfadimethoxine[M+H] only two of three transitions have explicit values
                                // Sulfadimethoxine[M+Na] has no explicit value
                                AssertEx.AreEqual(expectedCE, transition.ExplicitValues.CollisionEnergy);
                                if (precursor.PrecursorAdduct.Equals(Adduct.M_PLUS_H))
                                {
                                    expectedCE = 1;
                                }
                            }
                            break;
                        }
                    }
                }
                // Test serialization of explicit values
                pastedDoc = AssertEx.Serializable(pastedDoc, TestContext.GetTestResultsPath(), SkylineVersion.CURRENT); 
            }
            NewDocument();

        }

        private void TestTransitionListOutput(SrmDocument importDoc, string outputName, string expectedName, ExportFileType fileType)
        {
            // Write out a transition list
            string csvPath = TestFilesDir.GetTestPath(outputName);
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
            var csvOut = File.ReadAllText(csvPath).
                Replace("_","."). // Watch out for alternate fragment format in culture "fr"
                Replace(Resources.CustomIon_DisplayName_Ion, "Ion"); // Watch out for L10N of display name
            var csvExpected = File.ReadAllText(csvExpectedPath);
            AssertEx.FieldsEqual(csvExpected, csvOut, 0.0000011);
        }

        private void PasteMoleculesTestImportResults(string[] paths)
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

        private void HandleDocumentGridException(DataboundGridControl grid, String firstMessage, Exception exception)
        {
            StringBuilder message = new StringBuilder();
            if (!string.IsNullOrEmpty(firstMessage))
            {
                message.AppendLine(firstMessage);
            }
            message.AppendLine("Column Names:" + string.Join(",", grid.ColumnHeaderNames));
            BindingSource bindingSource = grid.BindingListSource;
            if (bindingSource != null)
            {
                PropertyDescriptorCollection propertyDescriptorCollection = bindingSource.GetItemProperties(null);
                if (propertyDescriptorCollection != null)
                {
                    message.AppendLine("Properties:");
                    foreach (PropertyDescriptor prop in propertyDescriptorCollection)
                    {
                        message.AppendLine(prop.DisplayName + ":" + (prop.IsReadOnly ? "RO" : "Writeable"));
                    }
                }
            }
            throw new ApplicationException(message.ToString(), exception);
        }

        private void TestAmbiguousPrecursorFragment()
        {
            // Check that we understand this first line to be a precursor transition - we were getting confused 
            // over "M-H" precursor adduct vs "-1" fragment charge, they're describing the same thing of course
            var input =
                "Molecule List Name,Precursor Name,Precursor m/z,Precursor Adduct,Precursor Charge,Explicit Retention Time,Product m/z,Product Charge,Explicit Collision Energy\r\n" +
                ", \"(6R)-5,6,7,8-tetrahydrobiopterin 1\",344.1364,[M-H],-1,2.8,344.1364,-1,35\r\n" +
                ", \"(6R)-5,6,7,8-tetrahydrobiopterin 1\",344.1364,[M-H],-1,2.8,147.9208,-1,35\r\n";

            // Paste directly into targets area
            var pastedDoc = PasteNewDocument(input);  // Say no to the offer to set new nodes to automanage

            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(1, pastedDoc.MoleculeCount);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(1, transitions.Count(t => t.IsMs1)); // Formerly we saw both as fragment transitions
            Assume.AreEqual(1, transitions.Count(t => !t.IsMs1));
            NewDocument();

        }

        private void TestNameCollisions()
        {
            // Check that we handle items with same name but different InChiKey, which is legitimate
            var input =
                "Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Precursor m/z,Precursor Charge,Product Name,Product Formula,Product Adduct,Product m/z,Product Charge,Label Type,Explicit Retention Time,Explicit Retention Time Window,Explicit Collision Energy,Note,InChiKey\n" +
                "quant,ecgonine methyl ester,C10H17NO3,[M+H],,1,,,,,,,,,,HMDB0006406,QIQNNBXHAYSQRY-ABIFROTESA-N\n" +
                "quant,ecgonine methyl ester,C10H17NO3,[M-H],,-1,,,,,,,,,,HMDB0006406,QIQNNBXHAYSQRY-ABIFROTESA-N\n" +
                "quant,(4S)-4-{[(9Z)-3-hydroxyoctadec-9-enoyl]oxy}-4-(trimethylammonio)butanoate,C10H17NO4,[M+H],,1,,,,,,,,,,HMDB0013124,YUCNWOKTRWJLGY-QMMMGPOBSA-N\n" +
                "quant,(4S)-4-{[(9Z)-3-hydroxyoctadec-9-enoyl]oxy}-4-(trimethylammonio)butanoate,C10H17NO4,[M-H],,-1,,,,,,,,,,HMDB0013124,YUCNWOKTRWJLGY-QMMMGPOBSA-N\n" +
                "quant,7-(carboxymethylcarbamoyl)heptanoic acid,C10H17NO5,[M+H],,1,,,,,,,,,,HMDB0000953,HXATVKDSYDWTCX-UHFFFAOYSA-N\n" +
                "quant,7-(carboxymethylcarbamoyl)heptanoic acid,C10H17NO5,[M-H],,-1,,,,,,,,,,HMDB0000953,HXATVKDSYDWTCX-UHFFFAOYSA-N\n" +
                "quant,(4S)-4-{[(9Z)-3-hydroxyoctadec-9-enoyl]oxy}-4-(trimethylammonio)butanoate,C10H19NO5,[M+H],,1,,,,,,,,,,HMDB0013125,QJGJXKFJFRSERW-QMMMGPOBSA-N\n" +
                "quant,(4S)-4-{[(9Z)-3-hydroxyoctadec-9-enoyl]oxy}-4-(trimethylammonio)butanoate,C10H19NO5,[M-H],,-1,,,,,,,,,,HMDB0013125,QJGJXKFJFRSERW-QMMMGPOBSA-N\n" +
                "quant,menthol,C10H20O,[M+H],,1,,,,,,,,,,HMDB0003352,NOOLISFMXDJSKH-KXUCPTDWSA-N\n" +
                "quant,menthol,C10H20O,[M-H],,-1,,,,,,,,,,HMDB0003352,NOOLISFMXDJSKH-KXUCPTDWSA-N\n";

            // Paste directly into targets area
            var pastedDoc = PasteNewDocument(input, false);

            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(5, pastedDoc.MoleculeCount);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(10, transitions.Count(t => t.IsMs1));
            Assume.AreEqual(0, transitions.Count(t => !t.IsMs1));
            NewDocument();
        }

        private void TestInconsistentMoleculeDescriptions()
        {
            // Check that we handle items with same name but different InChiKey, which is legitimate - they are two different molecules
            // Also checks that we handle LipidCreator output where everything is quoted
            var input =
                "Molecule List Name, Precursor Name,Precursor Formula, Precursor Adduct,Precursor Charge, Product m/z,Product Charge, Explicit Retention Time, Explicit Collision Energy, InChiKey, Explicit Declustering potential\n" +
                "\"bob\",\"D-Erythrose 4-phosphate\",\"C4H9O7P\",\"[M-H]\",\"-1\",\"97\",\"-1\",\"\",\"8\",\"NGHMDNPXVRFFGS-IUYQGCFVSA-N\",\"60\"\n" +
                "\"bob\",\"D-Erythrose 4-phosphate\",\"C4H9O7P\",\"[M+H]\",\"1\",\"99\",\"1\",\"\",\"8\",\"NGHMDNPXVRFFGS-IUYQGCFVSA-L\",\"60\"\n";
            // Paste directly into targets area, which should proceed with no error
            var doc = PasteNewDocument(input, false);
            AssertEx.IsDocumentState(doc, null, 1, 2, 2, 2);

            // Now check that we notice items with some accessions that agree but others that do not 
            var docOrig = NewDocument();
            input =
                "Molecule List Name, Precursor Name,Precursor Formula, Precursor Adduct,Precursor Charge, Product m/z,Product Charge, Explicit Retention Time, Explicit Collision Energy, InChiKey, CAS, Explicit Declustering potential\n" +
                "\"bob\",\"D-Erythrose 4-phosphate\",\"C4H9O7P\",\"[M-H]\",\"-1\",\"97\",\"-1\",\"\",\"8\",\"NGHMDNPXVRFFGS-IUYQGCFVSA-N\",585-18-2,\"60\"\n" +
                "\"bob\",\"D-Erythrose 4-phosphate\",\"C4H9O7P\",\"[M+H]\",\"1\",\"99\",\"1\",\"\",\"8\",\"NGHMDNPXVRFFGS-IUYQGCFVSA-N\",232-14-3,\"60\"\n"; // CAS conflict
            SetClipboardText(input);
            // Paste directly into targets area, which should create an error and then send us to ColumnSelectDlg
            var errDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            // This should produce an inconsistent molecule description error
            var errmsg = string.Empty;
            RunDlg<ImportTransitionListErrorDlg>(errDlg.OkDialog, msgDlg =>
            {
                errmsg = msgDlg.ErrorList.First().ErrorMessage;
                msgDlg.Close();
            }); // Dismiss it
            // Cancel the window
            OkDialog(errDlg, errDlg.CancelDialog);
            AssertEx.AreComparableStrings(Resources.SmallMoleculeTransitionListReader_GetMoleculeTransitionGroup_Inconsistent_molecule_description, errmsg);
        }

        /// <summary>
        /// Verify handling of small molecule transition lists in File>Import>TransitionLIst
        /// </summary>
        void TestFileImportTransitionList(string knownGood)
        {
            var filename = TestFilesDir.GetTestPath("known_good.csv");
            var headers = Settings.Default.CustomImportTransitionListColumnTypesList.Select(header => header.ToString()).ToArray();
            var contents = string.Join("\t", 
                               headers.Take(headers.Length)) + // Leave off the product neutral loss column
                           Environment.NewLine + knownGood;
            File.WriteAllText(filename, contents);
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
            });
            var confirmHeadersDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportMassList(filename));
            OkDialog(confirmHeadersDlg, confirmHeadersDlg.OkDialog);

            WaitForCondition(() => 0 != SkylineWindow.Document.MoleculeCount);

            // Now verify error handling
            var filename2 = TestFilesDir.GetTestPath("known_bad.csv");
            File.WriteAllText(filename2, @"foo"+contents);
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
            });

            // One of the headers cannot be understood
            var badDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportMassList(filename2));
            RunUI(() => {
                badDlg.radioMolecule.PerformClick();
                badDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy,
                    Resources.PasteDlg_UpdateMoleculeType_Note,
                    Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Adduct,
                    null, // No useful info this column
                    null, // No useful info this column
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_,
                    Resources.PasteDlg_UpdateMoleculeType_S_Lens,
                    Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential,
                    @"InChiKey",
                    @"HMDB",
                    @"InChi",
                    @"CAS",
                    @"SMILES",
                    @"KEGG",
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units);
            });

            // This should work because we manually set the headers
            OkDialog(badDlg, badDlg.OkDialog);
            //var messageDlg = ShowDialog<ImportTransitionListErrorDlg>(() => SkylineWindow.ImportMassList(filename));
            //OkDialog(messageDlg, messageDlg.AcceptButton.PerformClick); // Acknowledge the error
        }

        // Test for an issue where a mass-only description of a labeled precursor would come up with the unlabeled mass for the precursor transition
        void TestHeavyPrecursorNoFormulas()
        {
            var input = 
                "Molecule List Name,Precursor Name, Precursor m/z,Precursor Charge, Product m/z,Product Charge, Label Type,Explicit Retention Time,Explicit Retention Time Window\n" +
                "molecules1,2′deoxycitidine,330.1095,-1,330.1095,-1,light,7.7,2\n" +
                "molecules1,2′deoxycitidine,336.12963,-1,336.12963,-1,heavy,7.7,2\n";
            foreach (var asFile in new[] {true, false})
            {
                var docOrig = NewDocument();
                var tempFile = TestFilesDir.GetTestPath(@"transitions_heavy_tmp.csv");
                if (asFile)
                {
                    File.WriteAllText(tempFile, input);
                    var confirmHdrsDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportMassList(tempFile));
                    OkDialog(confirmHdrsDlg, confirmHdrsDlg.OkDialog);
                }
                else
                {
                    PasteToTargetsWindow(input); // Paste text, expect only header confirmation dialog
                }
                var pastedDoc = WaitForDocumentChange(docOrig);
                AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 2, 2);
                foreach (var pair in pastedDoc.MoleculePrecursorPairs)
                {
                    // Before the fix, we'd come up with the second set as 336.12963/330.1095 instead of 336.12963/336.12963
                    AssertEx.AreEqual(pair.NodeGroup.PrecursorMz, pair.NodeGroup.Transitions.First().Mz);
                }
                AssertEx.Serializable(pastedDoc); // Original error report was in terms of not being able to reload the inconsistent document, so check that
            }
        }

        void TestNotes()
        {

            var notSeattle = "Not made in Seattle";
            var notTumwater = "Not made in Tumwater";
            var notPortland = "Not made in Portland";
            var notAnywhere = "Not made at all";
            var rainier = "Vitamin R";
            var henrys = "Weinhards";
            var animals = "Schmidt";
            var oly = "olympia";
            var funky = "funky";
            var watery = "watery";
            var C20H20O12 = "C20H20O12";
            var C20H18O12 = "C20H18O12";
            var darkAndLager = "dark and lager molecule formulas are same";
            var lagerAndDark = "lager and dark molecule formulas are same";
            var CO2 = "CO2";
            var C12H19 = "C12H19";
            var C12H17 = "C12H17";

            var lines = new[]
            {
                $"Molecule List Name,Molecule Name,Fragment Name,Precursor m/z,Product m/z,Molecule Formula,Fragment Formula,Precursor Charge,Fragment Charge,Molecule Note,Molecule List Note,Precursor Note,Note", // N.B. using old style "note" instead of "transition note"
                $"{rainier},lager,bubbles,,,{C20H20O12},{CO2},1,1,dark has different molecule formula,{notSeattle},1,1",
                $"{rainier},lager,foam,,,{C20H20O12},{C12H19},1,1,,,2,2",
                $"{rainier},dark,bubbles,,,{C20H18O12},{CO2},1,1,light has different molecule formula,{notSeattle},3,3",
                $"{rainier},dark,foam,,,{C20H18O12},{C12H19},1,1,,,4,4",
                $"{rainier},lager,bubbles,,,{C20H20O12},{CO2},2,2,,,5,5",
                $"{rainier},lager,foam,,,{C20H20O12},{C12H19},2,2,,,6,6",
                $"{rainier},dark,bubbles,,,{C20H18O12},{CO2},2,2,,,7,7",
                $"{rainier},dark,foam,,,{C20H18O12},{C12H19},2,2,,,8,8",
                $"{henrys},lager,bubbles,,,{C20H20O12},{CO2},3,3,{darkAndLager},{notPortland},9,9",
                $"{henrys},lager,foam,,,{C20H20O12},{C12H17},3,3,,,10,10",
                $"{henrys},dark,bubbles,,,{C20H20O12},{CO2},3,3,{lagerAndDark},,11,11",
                $"{henrys},dark,foam,,,{C20H20O12},{C12H17},3,3,,This is not same note,12,12",
                $"{henrys},lager,bubbles,,,{C20H20O12},{CO2},4,4,,,13,13",
                $"{henrys},lager,foam,,,{C20H20O12},{C12H17},4,4,,,14,14",
                $"{henrys},dark,bubbles,,,{C20H20O12},{CO2},4,4,,,15,15",
                $"{henrys},dark,foam,,,{C20H20O12},{C12H17},4,4,,,16,16",
                $"{oly},dark,bubbles,451.0871025,44.99710546,,,1,1,{funky},{notTumwater},17,17",
                $"{oly},dark,foam,451.0871025,164.1559525,,,1,1,,,18,18",
                $"{oly},lager,bubbles,453.1027525,44.99710546,,,1,1,{watery},,19,19",
                $"{oly},lager,foam,453.1027525,164.1559525,,,1,1,,,20,20",
                $"{oly},lager,bubbles,227.0550145,23.00219096,,,2,2,,,21,21", // Heavier precursor before lighter, expect sort
                $"{oly},lager,foam,227.0550145,82.58161446,,,2,2,,,22,22",
                $"{oly},dark,foam,226.0471895,82.58161446,,,2,2,,,23,23",
                $"{oly},dark,bubbles,226.0471895,23.00219096,,,2,2,,,24,24",
                $"{animals},lager,bubbles,151.7057685,15.67055279,,,3,3,,{notAnywhere},25,25",
                $"{animals},lager,foam,151.7057685,54.71828512,,,3,3,,,26,26",
                $"{animals},dark,bubbles,151.7057685,15.67055279,,,3,3,,,27,27",
                $"{animals},dark,foam,151.7057685,54.71828512,,,3,3,,,28,28",
                $"{animals},lager,bubbles,114.0311455,12.00473371,,,4,4,,,29,29",
                $"{animals},lager,foam,114.0311455,41.29053296,,,4,4,,,30,30",
                $"{animals},dark,bubbles,114.0311455,12.00473371,,,4,4,,,31,31",
                $"{animals},dark,foam,114.0311455,41.29053296,,,4,4,,,32,32",
            };
            var reportFile = TestFilesDir.GetTestPath("notes.csv");

            for (var roundtrip = 0; roundtrip < 2; roundtrip++)
            {
                var docOrig = NewDocument();
                ImportTransitionListColumnSelectDlg testImportDlg;
                if (roundtrip == 1) // Test report roundtrip
                {
                    // Import the file, which should send us to ColumnSelectDlg
                    testImportDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportMassList(reportFile));
                }
                else
                {
                    // Paste directly into targets area, which should send us to ColumnSelectDlg
                    SetClipboardText(string.Join("\r\n", lines));
                    testImportDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
                }
                OkDialog(testImportDlg, testImportDlg.OkDialog);
                DismissAutoManageDialog();  // Say no to the offer to set new nodes to automanage

                var pastedDoc = WaitForDocumentChange(docOrig);
                AssertEx.IsDocumentState(pastedDoc, null, 4, 8, 16, 32);

                foreach (var kvp in new Dictionary<string, string>() { { rainier, notSeattle }, {henrys, notPortland}, {oly, notTumwater}, {animals, notAnywhere} })
                {
                    var node = pastedDoc.MoleculeGroups.First(n => n.Name.Equals(kvp.Key));
                    AssertEx.AreEqual(node.Annotations.Note, kvp.Value);
                }


                var henrysGroup = pastedDoc.MoleculeGroups.First(n => n.Name.Equals(henrys));
                AssertEx.AreEqual(notPortland, henrysGroup.Annotations.Note);
                var precursorNotes = new[] { 9, 13, 11, 15 }; // Some sorting and coalescing is expected, but it takes the first-seen precursor name
                var p = 0;
                var transitionNotes = new[] { 9 + roundtrip, 10 - roundtrip, 13 + roundtrip, 14 - roundtrip, 11 + roundtrip, 12 - roundtrip, 15 + roundtrip, 16 - roundtrip }; // Some sorting and coalescing is expected
                var t = 0;
                foreach (var mol in henrysGroup.Molecules)
                {
                    AssertEx.AreEqual(C20H20O12, mol.CustomMolecule.Formula);
                    foreach (var precursor in mol.TransitionGroups)
                    {
                        switch (precursor.CustomMolecule.DisplayName)
                        {
                            case "lager":
                                AssertEx.AreEqual(darkAndLager, mol.Annotations.Note);
                                break;
                            case "dark":
                                AssertEx.AreEqual(lagerAndDark, mol.Annotations.Note);
                                break;
                        }
                        AssertEx.AreEqual(precursorNotes[p++].ToString(), precursor.Annotations.Note, "precursor note");

                        foreach (var transition in precursor.Transitions)
                        {
                            AssertEx.AreEqual(transitionNotes[t++].ToString(), transition.Annotations.Note, "transition note");
                            switch (transition.CustomIon.DisplayName)
                            {
                                case "foam":
                                    AssertEx.AreEqual(C12H17, transition.CustomIon.Formula);
                                    break;
                                case "bubbles":
                                    AssertEx.AreEqual(CO2, transition.CustomIon.Formula);
                                    break;
                            }
                        }
                    }
                }

                var rainierGroup = pastedDoc.MoleculeGroups.First(n => n.Name.Equals(rainier));
                AssertEx.AreEqual(notSeattle, rainierGroup.Annotations.Note);
                precursorNotes = new[] { 1, 5, 3, 7 }; // Some sorting and coalescing is expected, but it takes the first-seen precursor name
                p = 0;
                transitionNotes = new[] { 1 + roundtrip, 2 - roundtrip, 5 + roundtrip, 6 - roundtrip, 3 + roundtrip, 4 - roundtrip, 7 + roundtrip, 8 - roundtrip }; // Some sorting and coalescing is expected
                t = 0;
                foreach (var mol in rainierGroup.Molecules)
                {
                    foreach (var precursor in mol.TransitionGroups)
                    {
                        switch (precursor.CustomMolecule.DisplayName)
                        {
                            case "dark":
                                AssertEx.AreEqual(C20H18O12, mol.CustomMolecule.Formula);
                                break;
                            case "lager":
                                AssertEx.AreEqual(C20H20O12, mol.CustomMolecule.Formula);
                                break;
                        }
                        AssertEx.AreEqual(precursorNotes[p++].ToString(), precursor.Annotations.Note);

                        foreach (var transition in precursor.Transitions)
                        {
                            AssertEx.AreEqual(transitionNotes[t++].ToString(), transition.Annotations.Note, "transition note");
                            switch (transition.CustomIon.DisplayName)
                            {
                                case "foam":
                                    AssertEx.AreEqual(C12H19, transition.CustomIon.Formula);
                                    break;
                                case "bubbles":
                                    AssertEx.AreEqual(CO2, transition.CustomIon.Formula);
                                    break;
                            }
                        }
                    }
                }


                var olyGroup = pastedDoc.MoleculeGroups.First(n => n.Name.Equals(oly));
                AssertEx.AreEqual(notTumwater, olyGroup.Annotations.Note);
                precursorNotes = new[] { 17, 23, 19, 21 }; // Some sorting and coalescing is expected, but it takes the first-seen precursor name
                p = 0;
                transitionNotes = new[] { 17+roundtrip, 18-roundtrip, 24-roundtrip, 23+roundtrip, 19+roundtrip, 20-roundtrip, 21+roundtrip, 22-roundtrip }; // Some sorting and coalescing is expected
                t = 0;
                foreach (var mol in olyGroup.Molecules)
                {
                    foreach (var precursor in mol.TransitionGroups)
                    {
                        switch (precursor.CustomMolecule.DisplayName)
                        {
                            case "dark":
                                AssertEx.AreEqual(funky, mol.Annotations.Note);
                                break;
                            case "lager":
                                AssertEx.AreEqual(watery, mol.Annotations.Note);
                                break;
                        }
                        AssertEx.AreEqual(precursorNotes[p++].ToString(), precursor.Annotations.Note);

                        foreach (var transition in precursor.Transitions)
                        {
                            AssertEx.AreEqual(transitionNotes[t++].ToString(), transition.Annotations.Note);
                        }
                    }
                }

                AssertEx.Serializable(pastedDoc);

                if (roundtrip == 0)
                {
                    // Prepare to check report roundtrip
                    var allNotes = "AllNotes";
                    var notesReport =
                        @"<views>" +
                        @$"  <view name=""{allNotes}"" rowsource=""pwiz.Skyline.Model.Databinding.Entities.Transition"" sublist=""Results!*"" uimode=""small_molecules"">" +
                        @"    <column name="""" />" +
                        @"    <column name=""Precursor"" />" +
                        @"    <column name=""Precursor.Mz"" />" +
                        @"    <column name=""Precursor.Adduct"" />" +
                        @"    <column name=""ProductCharge"" />" +
                        @"    <column name=""ProductMz"" />" +
                        @"    <column name=""ProductIonFormula"" />" +
                        @"    <column name=""ProductNeutralFormula"" />" +
                        @"    <column name=""ProductAdduct"" />" +
                        @"    <column name=""Quantitative"" />" +
                        @"    <column name=""Note"" />" +
                        @"    <column name=""Precursor.Peptide.Protein.Note"" />" +
                        @"    <column name=""Precursor.Peptide.Note"" />" +
                        @"    <column name=""Precursor.Note"" />" +
                        @"    <column name=""Precursor.Peptide.Protein.Name"" />" +
                        @"    <column name=""Precursor.Peptide.MoleculeName"" />" +
                        @"    <column name=""Precursor.Peptide.MoleculeFormula"" />" +
                        @"  </view>" +
                        @"</views>";
                    var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id);
                    var viewSpecListToAdd = (ViewSpecList)new XmlSerializer(typeof(ViewSpecList)).Deserialize(new StringReader(notesReport));
                    Settings.Default.PersistedViews.SetViewSpecList(PersistedViews.MainGroup.Id, viewSpecList.AddOrReplaceViews(viewSpecListToAdd.ViewSpecLayouts));

                    var exportReportDlg =
                        ShowDialog<ExportLiveReportDlg>(() => SkylineWindow.ShowExportReportDialog());
                    RunUI(() => exportReportDlg.ReportName = allNotes);
                    OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(reportFile, TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture)));
                    WaitForCondition(() => File.Exists(reportFile));
                }
            }

        }

        void TestImportAllData(bool asFile)
        {
            // Check that we are importing all the data in the case where there is not a header line provided
            var inputNoHeaders =
                "Amino Acids B,AlaB,,light,,,225.1,44,-1,-1,3\n" +
                "Amino Acids B,ArgB,,light,,,310.2,217,-1,-1,19\n" +
                "Amino Acids,Ala,,light,,,225,44,1,1,3\n" +
                "Amino Acids,Ala,,heavy,,,229,48,1,1,4\n" +
                "Amino Acids,Arg,,light,,,310,217,1,1,19\n" +
                "Amino Acids,Arg,,heavy,,,312,219,1,1,19\n" +
                "Amino Acids B,AlaB,,light,,,225.1,45,-1,-1,3\n" +
                "Amino Acids B,AlaB,,heavy,,,229,48,-1,-1,4\n" +
                "Amino Acids B,AlaB,,heavy,,,229,49,-1,-1,4\n" +
                "Amino Acids B,ArgB,,light,,,310.2,218,-1,-1,19\n" +
                "Amino Acids B,ArgB,,heavy,,,312,219,-1,-1,19\n" +
                "Amino Acids B,ArgB,,heavy,,,312,220,-1,-1,19\n";
            for (var pass = 0; pass < 2; pass++) 
            {
                var withHeaders = pass == 1;     // Double check that headers work too

                var input = withHeaders ?
                    string.Join(",", new string[]
                    {
                        SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                        SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                        SmallMoleculeTransitionListColumnHeaders.nameProduct,
                        SmallMoleculeTransitionListColumnHeaders.labelType,
                        SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                        SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                        SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                        SmallMoleculeTransitionListColumnHeaders.mzProduct,
                        SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                        SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                        SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
                    }) + "\n" + inputNoHeaders :
                    inputNoHeaders; 

                var docOrig = NewDocument();

                ImportTransitionListColumnSelectDlg testImportDlg;
                if (asFile)
                {
                    var tempFile = TestFilesDir.GetTestPath(string.Format("transitions_tmp{0}.csv", pass));
                    File.WriteAllText(tempFile, input);
                    // Import the file, which should send us to ColumnSelectDlg
                    testImportDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportMassList(tempFile));
                }
                else
                {
                    SetClipboardText(input);
                    // Paste directly into targets area, which should send us to ColumnSelectDlg
                    testImportDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
                }


                if (!withHeaders)
                {
                    // Correct the header assignments
                    RunUI(() => {
                        testImportDlg.radioMolecule.PerformClick();
                        testImportDlg.SetSelectedColumnTypes(
                            Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                            Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                            Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
                            Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                            Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                            Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                            Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                            Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time);
                    });
                
                    // Import the list
                    OkDialog(testImportDlg, testImportDlg.OkDialog);
                    DismissAutoManageDialog();  // Say no to the offer to set new nodes to automanage
                }
            
                // Import the list
                OkDialog(testImportDlg, testImportDlg.OkDialog);
                if (pass == 1)
                {
                    DismissAutoManageDialog();  // Say no to the offer to set new nodes to automanage
                }

                var pastedDoc = WaitForDocumentChange(docOrig);
                AssertEx.IsDocumentState(pastedDoc, null, 2, 4, 8, 12);
            }
        }

        void TestSimilarMzIsotopes()
        {
            // Check that we are noticing different labels even when m/z is similar
            var input =
                "Molecule Name,Molecular Formula,Precursor Adduct,Precursor Charge\r\n"+
                "Glutamine,C5H10N2O3,M2C13-H,-1\r\n" +
                "Glutamine,C5H10N2O3,M1C131N15-H,-1\r\n"+ 
                "Glutamine,C5H10N2O3,M1C131H2-H,-1";

            var docOrig = NewDocument();

            SetClipboardText(input);
            // Paste directly into targets area, which should send us to ColumnSelectDlg
            var testImportDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            // Import the list
            OkDialog(testImportDlg, testImportDlg.OkDialog);

            var pastedDoc = WaitForDocumentChange(docOrig);
            AssertEx.IsDocumentState(pastedDoc, null, 1, 1, 3, 3);

        }
    }
}
