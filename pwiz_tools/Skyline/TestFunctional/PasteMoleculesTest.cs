﻿/*
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
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
        [TestMethod]
        public void TestPasteMolecules()
        {
            TestFilesZip = @"TestFunctional\PasteMoleculeTest.zip";
            RunFunctionalTest();
        }


        private void TestError(string clipText, string errText,
            string[] columnOrder)
        {
            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            RunUI(() =>
            {
                pasteDlg.IsMolecule = true;
                if (columnOrder != null)
                {
                    pasteDlg.SetSmallMoleculeColumns(columnOrder.ToList());
                    WaitForConditionUI(() => pasteDlg.GetUsableColumnCount() == columnOrder.ToList().Count);                    
                }
                else
                {
                    WaitForConditionUI(() => pasteDlg.GetUsableColumnCount() == Settings.Default.CustomMoleculeTransitionInsertColumnsList.Count);                    
                }
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
            const double slens = 6.789;
            const double coneVoltage = 7.89;
            const double compensationVoltage = 8.901;
            const double declusteringPotential = 9.012;
            const double precursorRT = 3.45;
            const double precursorRTWindow = 4.567;
            const string note = "noted!";

            var docEmpty = NewDocument();

            TestToolServiceAccess();
            TestLabelsNoFormulas();
            TestPrecursorTransitions();
            TestTransitionListArrangementAndReporting();

            var fullColumnOrder = new[]
                {
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.nameProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.rtWindowPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.cePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.note,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.adductProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.dtPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.dtHighEnergyOffset,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.slens,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.coneVoltage,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.compensationVoltage,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.declusteringPotential,
                };

            // Default col order is listname, preName, PreFormula, preAdduct, preMz, preCharge, prodName, ProdFormula, prodAdduct, prodMz, prodCharge
            string line1 = "MyMolecule\tMyMol\tMyFrag\tC34H12O4\tC34H3O\t" + precursorMzAtZNeg2 + "\t" + productMzAtZNeg2 + "\t-2\t-2\tlight\t" +
                precursorRT + "\t" + precursorRTWindow + "\t" + precursorCE + "\t" + note + "\t\t\t" + precursorDT + "\t" + highEnergyDtOffset + "\t" + slens + "\t" + coneVoltage +
                "\t" + compensationVoltage + "\t" + declusteringPotential; // Legit
            const string line2start = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCH3O\t";
            const string line3 = "\r\nMyMolecule2\tMyMol2\tMyFrag2\tCH12O4\tCHH500000000\t\t\t1\t1";
            const string line4 = "\r\nMyMolecule3\tMyMol3\tMyFrag3\tH2\tH\t\t\t1\t1";
            string line5 = line1.Replace("C34H12O4","C34H14O4[M-2H]").Replace("C34H3O","C34H32").Replace(note + "\t\t\t", note + "\t\tM-2H\t"); // Legit
            string line6 = line1.Replace("C34H12O4", "").Replace("C34H3O", "").Replace(note + "\t\t\t", note + "\t\tM-3H\t"); // mz only, but charge and adduct disagree

            // Provoke some errors
            TestError(line1.Replace("\t-2\t-2", "\t-2\t2"), // precursor and charge polarities disagree
                Resources.Transition_Validate_Precursor_and_product_ion_polarity_do_not_agree_, fullColumnOrder);
            TestError(line1.Replace("C34H12O4", "C77H12O4"), // mz and formula disagree
                String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Error_on_line__0___Precursor_m_z__1__does_not_agree_with_value__2__as_calculated_from_ion_formula_and_charge_state__delta____3___Transition_Settings___Instrument___Method_match_tolerance_m_z____4_____Correct_the_m_z_value_in_the_table__or_leave_it_blank_and_Skyline_will_calculate_it_for_you_,
                1, (float)precursorMzAtZNeg2, 500.0373, 258, docEmpty.Settings.TransitionSettings.Instrument.MzMatchTolerance), fullColumnOrder);
            TestError(line1.Replace("C34H3", "C76H3"), // mz and formula disagree
                String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Error_on_line__0___Product_m_z__1__does_not_agree_with_value__2__as_calculated_from_ion_formula_and_charge_state__delta____3___Transition_Settings___Instrument___Method_match_tolerance_m_z____4_____Correct_the_m_z_value_in_the_table__or_leave_it_blank_and_Skyline_will_calculate_it_for_you_,
                1, (float)productMzAtZNeg2, 465.5097, 252, docEmpty.Settings.TransitionSettings.Instrument.MzMatchTolerance), fullColumnOrder);
            var badcharge = Transition.MAX_PRODUCT_CHARGE + 1;
            TestError(line1 + line2start + "\t\t1\t" + badcharge, // Excessively large charge for product
                String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__,
                badcharge, -Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE), fullColumnOrder);
            badcharge = 120;
            TestError(line1 + line2start + "\t\t" + badcharge + "\t1", // Insanely large charge for precursor
                String.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                badcharge, -TransitionGroup.MAX_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE), fullColumnOrder);
            TestError(line1 + line2start + "\t\t1\t", // No mz or charge for product
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 2), fullColumnOrder);
            TestError(line1 + line2start + "19\t5", // Precursor Formula and m/z don't make sense together
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_formula_and_m_z_value_do_not_agree_for_any_charge_state_, 2), fullColumnOrder);
            TestError(line1 + line2start + "\t5\t1", // Product Formula and m/z don't make sense together
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_, 2), fullColumnOrder);
            TestError(line1 + line2start + "\t", // No mz or charge for precursor or product
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 2), fullColumnOrder);
            TestError(line1 + line3, // Insanely large molecule
                string.Format(Resources.CustomIon_Validate_The_mass_of_the_custom_ion_exceeeds_the_maximum_of__0_, CustomIon.MAX_MASS), fullColumnOrder);
            TestError(line1 + line4, // Insanely small molecule
                string.Format(Resources.CustomIon_Validate_The_mass_of_the_custom_ion_is_less_than_the_minimum_of__0__, CustomIon.MIN_MASS), fullColumnOrder);
            TestError(line1 + line2start + +precursorMzAtZNeg2 + "\t" + productMzAtZNeg2 + "\t-2\t-2\t\t\t" + precursorRTWindow + "\t" + precursorCE + "\t" + note + "\t\t\t" + precursorDT + "\t" + highEnergyDtOffset, // Explicit retention time window without retention time
                Resources.Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_, fullColumnOrder);
            TestError(line5.Replace("[M-2H]", "[M+H]"), string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_, "[M+H]", 1, -2), fullColumnOrder);
            TestError(line6, string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_, "M-3H", -3, -2), fullColumnOrder);
            for (int withSpecials = 2; withSpecials-- > 0; )
            {
                // By default we don't show drift or other exotic columns
                var columnOrder = (withSpecials == 0) ? fullColumnOrder.Take(16).ToArray() : fullColumnOrder;
                // Take a legit full paste and mess with each field in turn
                string[] fields = { "MyMol", "MyPrecursor", "MyProduct", "C12H9O4", "C6H4O2", "217.049535420091", "108.020580420091", "1", "1", "heavy", "123", "5", "25", "this is a note", "[M+]", "[M+]", "7", "9", "88.5", "99.6", "77.3", "66.2" };
                string[] badfields = { "", "", "", "123", "C6H2O2[M+2H]", "fish", "-345", "cat", "pig", "12", "frog", "hamster", "boston", "", "[M+foo]", "wut", "foosball", "greasy", "mumble", "gumdrop", "dingle", "gorse", "AHHHHHRGH" };
                var expectedErrors = new List<string>()
                {
                    Resources.PasteDlg_ShowNoErrors_No_errors, Resources.PasteDlg_ShowNoErrors_No_errors, Resources.PasteDlg_ShowNoErrors_No_errors,  // No name, no problem
                    BioMassCalc.MONOISOTOPIC.FormatArgumentExceptionMessage(badfields[3]),
                    Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Formula_already_contains_an_adduct_description__and_it_does_not_match_,
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_, badfields[5]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_,  badfields[6]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_,  badfields[7]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_,  badfields[8]),
                    string.Format(Resources.SrmDocument_ReadLabelType_The_isotope_modification_type__0__does_not_exist_in_the_document_settings,  badfields[9]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_retention_time_value__0_,  badfields[10]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_retention_time_window_value__0_,  badfields[11]),
                    string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_collision_energy_value__0_,  badfields[12]),
                    badfields[13], // This is empty, as notes are freeform, so any value is fine
                    string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Unknown_symbol___0___in_adduct_description___1__,  "foo", badfields[14]),
                    string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0__,  badfields[15]),
                 };
                if (withSpecials > 0)
                {
                     var s = expectedErrors.Count;
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_value__0_,badfields[s++]));
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_high_energy_offset_value__0_, badfields[s++]));
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_S_Lens_value__0_, badfields[s++]));
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_cone_voltage_value__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_compensation_voltage__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_declustering_potential__0_, badfields[s++]));
                }
                expectedErrors.Add(Resources.PasteDlg_ShowNoErrors_No_errors); // N+1'th pass is unadulterated
                for (var bad = 0; bad < expectedErrors.Count(); bad++)
                {
                    var line = "";
                    for (var f = 0; f < expectedErrors.Count()-1; f++)
                        line += ((bad == f) ? badfields[f] : fields[f]).Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator) + "\t";
                    if (!string.IsNullOrEmpty(expectedErrors[bad]))
                        TestError(line, expectedErrors[bad], columnOrder);
                }
            }
            TestError(line1.Replace("34H12O4\tC34H3O", "34H14O4[M-H]\tC34H5O[M-H]") + line2start + "\t\t1\t1", 
                string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_,"[M-H]",-1,-2), fullColumnOrder);

            // Now load the document with a legit paste
            TestError(line1.Replace("34H12O4\tC34H3O", "34H14O4[M-2H]\tC34H5O[M-2H]") + line2start.Replace("CH3O", "CH29") + "\t\t1\t\t\t\t\t\t\t\tM+H", String.Empty, fullColumnOrder);
            var docOrig = WaitForDocumentChange(docEmpty);
            var testTransitionGroups = docOrig.MoleculeTransitionGroups.ToArray();
            Assert.AreEqual(2, testTransitionGroups.Count());
            var transitionGroup = testTransitionGroups[0];
            var precursor = docOrig.Molecules.First();
            var product = transitionGroup.Transitions.First();
            Assert.AreEqual(precursorCE, transitionGroup.ExplicitValues.CollisionEnergy);
            Assert.AreEqual(precursorDT, transitionGroup.ExplicitValues.DriftTimeMsec);
            Assert.AreEqual(slens, transitionGroup.ExplicitValues.SLens);
            Assert.AreEqual(coneVoltage, transitionGroup.ExplicitValues.ConeVoltage);
            Assert.AreEqual(compensationVoltage, transitionGroup.ExplicitValues.CompensationVoltage);
            Assert.AreEqual(declusteringPotential, transitionGroup.ExplicitValues.DeclusteringPotential);
            Assert.AreEqual(note, product.Annotations.Note);
            Assert.AreEqual(highEnergyDtOffset, transitionGroup.ExplicitValues.DriftTimeHighEnergyOffsetMsec.Value, 1E-7);
            Assert.AreEqual(precursorRT, precursor.ExplicitRetentionTime.RetentionTime);
            Assert.AreEqual(precursorRTWindow, precursor.ExplicitRetentionTime.RetentionTimeWindow);
            Assert.IsTrue(ReferenceEquals(transitionGroup.TransitionGroup, product.Transition.Group));
            Assert.AreEqual(precursorMzAtZNeg2, BioMassCalc.CalculateIonMz(transitionGroup.CustomIon.MonoisotopicMass, transitionGroup.PrecursorCharge), 1E-7);
            Assert.AreEqual(productMzAtZNeg2, BioMassCalc.CalculateIonMz(product.GetIonMass(), product.Transition.Charge), 1E-7);
            // Does that produce the expected transition list file?
            TestTransitionListOutput(docOrig, "PasteMoleculeTinyTest.csv", "PasteMoleculeTinyTestExpected.csv", ExportFileType.IsolationList);
            // Does serialization of imported values work properly?
            AssertEx.Serializable(docOrig);

            // Reset
            docOrig = NewDocument();

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
            PasteMoleculesTestImportResults(paths);
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

            // Verify that MS1 filtering works properly
            var pasteText =
            "Steryl esters [ST0102],12:0 Cholesteryl ester,C39H68O2NH4,1\r\n" +
            "Steryl esters [ST0102],14:0 Cholesteryl ester,C41H72O2NH4,1\r\n" +
            "Steryl esters [ST0102],14:1 Cholesteryl ester,C41H70O2NH4,1\r\n" +
            "Steryl esters [ST0102],15:1 Cholesteryl ester,C42H72O2NH4,1";

            var columnOrderB = new[]
                {
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                };

            // Doc is set for MS1 filtering, precursor transitions, charge=1, two peaks, should show M and M+1, M+2 after filter is invoked by changing to 3 peaks
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("small_molecule_missing_m1.sky")));
            WaitForDocumentLoaded();
            TestError(pasteText, String.Empty, columnOrderB);
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
            Assert.AreEqual(12, docB.MoleculeTransitionCount);

            // Verify that we can import heavy/light pairs
            var columnOrderC = new[]
                {
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType,
                };
            pasteText =
                "A,27-HC,C36H57N2O3,135,1,1,light\r\n" +
                "A,27-HC,C36H57N2O3,181,1,1,light\r\n" +
                "A,27-HC,C36H57N2O3,367,1,1,light\r\n" +
                "A,27-HC,C36H51H'6N2O3,135,1,1,heavy\r\n" +
                "A,27-HC,C36H51H'6N2O3,181,1,1,heavy\r\n" +
                "A,27-HC,C36H51H'6N2O3,215,1,1,heavy\r\n";
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC);
            var docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);

            // Verify adduct usage - none, or in own column, or as part of formula
            columnOrderC = new[]
                {
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType,
                };
            pasteText =
                "A,27-HC,C36H57N2O3,,1,135,1,light\r\n" + // No adduct
                "A,27-HC,C36H57N2O3,[M+],1,130,1,light\r\n" + 
                "A,27-HC,C36H56N2O3,M+H,,181,1,light\r\n" +
                "A,27-HC,C36H56N2O3[M+H],,,367,1,light\r\n" ;
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC);
            docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(1, docC.MoleculeTransitionGroupCount);  // Names override formulas

            // Verify adduct usage - none, or in own column, or as part of formula, when no name hints are given
            columnOrderC = new[]
                {
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType,
                };
            pasteText =
                "C36H57N2O3,,1,135,1,light\r\n" + // No adduct
                "C36H57N2O3,[M+],1,130,1,light\r\n" +
                "C36H56N2O3,M+H,,181,1,light\r\n" +
                "C36H56N2O3[M+H],,,367,1,light\r\n";
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC);
            docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(1, docC.MoleculeTransitionGroupCount);  // Formula descriptions devolve to C36H56N2O3[M+H] and C36H57N2O3

            pasteText =
                "C36H56N2O3,M+H,,181,1,light\r\n" +
                "C36H56N2O3[M+H],,,367,1,light\r\n" +
                "C36H56N2O3,M+2H,,81,2,light\r\n" +
                "C36H56N2O3[M+2H],,,167,2,light\r\n";
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC);
            docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);  

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
            WaitForConditionUI(() => pasteDlg2.GetUsableColumnCount()==columnOrder.ToList().Count);

            // Bad charge states mid-list were handled ungracefully due to lookahead in figuring out transition groups
            const int badcharge = Transition.MAX_PRODUCT_CHARGE + 1;
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

            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            RunUI(() =>
            {
                pasteDlg.IsMolecule = true;
                pasteDlg.SetSmallMoleculeColumns(columnOrder.ToList());
            });
            WaitForConditionUI(() => pasteDlg.GetUsableColumnCount() == columnOrder.ToList().Count);

            SetCsvFileClipboardText(TestFilesDir.GetTestPath("small_molecule_paste_test.csv"));
            RunUI(pasteDlg.PasteTransitions);
            OkDialog(pasteDlg, pasteDlg.OkDialog);
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
                RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides));
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

                // Simulate user editing the precursor in the document grid
                EnableDocumentGridColumns(documentGrid, Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors, 16, new[] {
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitDriftTimeMsec",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitDriftTimeHighEnergyOffsetMsec",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitCollisionEnergy",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitDeclusteringPotential",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitCompensationVoltage"});

                const double explicitCE = 123.45;
                var colCE = FindDocumentGridColumn(documentGrid, "ExplicitCollisionEnergy");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colCE.Index].Value = explicitCE);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.CollisionEnergy.Equals(explicitCE)));

                const double explicitDP = 12.345;
                var colDP = FindDocumentGridColumn(documentGrid, "ExplicitDeclusteringPotential");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colDP.Index].Value = explicitDP);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.DeclusteringPotential.Equals(explicitDP)));

                const double explicitCV = 13.45;
                var colCV = FindDocumentGridColumn(documentGrid, "ExplicitCompensationVoltage");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colCV.Index].Value = explicitCV);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.CompensationVoltage.Equals(explicitCV)));

                const double explicitDT = 23.465;
                var colDT = FindDocumentGridColumn(documentGrid, "ExplicitDriftTimeMsec");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colDT.Index].Value = explicitDT);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.DriftTimeMsec.Equals(explicitDT)));

                const double explicitDTOffset = -3.4657;
                var colDTOffset = FindDocumentGridColumn(documentGrid, "ExplicitDriftTimeHighEnergyOffsetMsec");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colDTOffset.Index].Value = explicitDTOffset);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitionGroups.First().ExplicitValues.DriftTimeHighEnergyOffsetMsec.Equals(explicitDTOffset)));

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
            //  If product ion info supplied matches the precursor ion info, interpret as a precursor transition.

            var saveColumnOrder = Settings.Default.CustomMoleculeTransitionInsertColumnsList;

            var docOrig = NewDocument();
            var pasteDlg2 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            //non-standard column order
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
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.note,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType,
           };
            // If user omits some product info but not others, complain
            RunUI(() =>
            {
                pasteDlg2.IsMolecule = true;
                pasteDlg2.SetSmallMoleculeColumns(columnOrder.ToList());
            });
            WaitForConditionUI(() => pasteDlg2.GetUsableColumnCount() == columnOrder.ToList().Count);
            const string inconsistent =
                "Oly\tlager\tbubbles\t452\t\t\t\t1\t" + "\n" +
                "Oly\tlager\tfoam\t234\t163\t\t\t1\t1\tduly noted?";
            SetClipboardText(inconsistent);
            RunUI(pasteDlg2.PasteTransitions);
            RunUI(pasteDlg2.OkDialog);  // Don't expect this to work, form stays open
            WaitForConditionUI(() => pasteDlg2.ErrorText != null);
            var errText = String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 1);
            RunUI(() => Assert.IsTrue(pasteDlg2.ErrorText.Contains(errText),
                string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"", errText, pasteDlg2.ErrorText)));
            OkDialog(pasteDlg2, pasteDlg2.CancelDialog);

            // If user omits product info altogether, that implies precursor transitions
            var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            RunUI(() =>
            {
                pasteDlg.IsMolecule = true;
                pasteDlg.SetSmallMoleculeColumns(columnOrder.ToList());
            });
            WaitForConditionUI(() => pasteDlg.GetUsableColumnCount() == columnOrder.ToList().Count);
            const string implied =
                "Oly\tlager\tbubbles\t452\t\t\t\t1\t\tmacrobrew" + "\n" +
                "Oly\tlager\tfoam\t234\t\t\t\t1\t\tmacrobrew";
            SetClipboardText(implied);
            RunUI(pasteDlg.PasteTransitions);
            OkDialog(pasteDlg, pasteDlg.OkDialog);
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
            var pasteDlg3 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            RunUI(() =>
            {
                pasteDlg3.IsMolecule = true;
                pasteDlg3.SetSmallMoleculeColumns(columnOrder.ToList());
            });
            WaitForConditionUI(() => pasteDlg3.GetUsableColumnCount() == columnOrder.ToList().Count);
            const string matching =
                "Schmidt\tlager\tbubbles\t150\t150\t\t\t2\t2\tnotated!" + "\n" +
                "Schmidt\tlager\tfoam\t159\t159\t\t\t3\t3\tnote!";
            SetClipboardText(matching);
            RunUI(pasteDlg3.PasteTransitions);
            OkDialog(pasteDlg3, pasteDlg3.OkDialog);
            pastedDoc = WaitForDocumentChange(docOrig);
            // We expect precursor transitions
            foreach (var trans in pastedDoc.MoleculeTransitions)
            {
                Assert.IsTrue(trans.Transition.IsPrecursor());
                Assert.IsTrue(trans.Annotations.Note.StartsWith("not") || trans.Annotations.Note.StartsWith("macro"));
            }

            docOrig = NewDocument();
            var pasteDlg4 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            RunUI(() =>
            {
                pasteDlg4.IsMolecule = true;
                pasteDlg4.SetSmallMoleculeColumns(columnOrder.ToList());
            });
            WaitForConditionUI(() => pasteDlg4.GetUsableColumnCount() == columnOrder.ToList().Count);
            const string impliedLabeled =
                "Oly\tlager\tbubbles\t452.1\t\t\t\t1\t\tmacrobrew\theavy" + "\n" +
                "Oly\tlager\tfoam\t234.5\t\t\t\t1\t\tmacrobrew\tlight";
            SetClipboardText(impliedLabeled.Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator));
            RunUI(pasteDlg4.PasteTransitions);
            OkDialog(pasteDlg4, pasteDlg4.OkDialog);
            pastedDoc = WaitForDocumentChange(docOrig);
            // We expect a single heavy/light pair
            Assert.AreEqual(1, pastedDoc.MoleculeCount);
            foreach (var trans in pastedDoc.MoleculeTransitions)
            {
                Assert.IsTrue(trans.Transition.IsPrecursor());
                Assert.AreEqual(trans.Annotations.Note, "macrobrew");
            }

            NewDocument();
            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
        }

        private void TestToolServiceAccess()
        {
            // Test the tool service logic without actually using tool service (there's a test for that too)
            var header = string.Join(",", new string[]
            {
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.nameProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
           });
           var textCSV = header + "\n" +
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

            var docOrig = SkylineWindow.Document;
            SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textCSV, Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            }));

            var pastedDoc = WaitForDocumentChange(docOrig);
            Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assert.AreEqual(4, pastedDoc.MoleculeCount);

            // Now feed it some nonsense headers, verify helpful error message
            var textCSV2 = textCSV.Replace(PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType, "labbel").Replace(PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,"grommet");
            AssertEx.ThrowsException<LineColNumberedIoException>(() => SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textCSV2,
                    Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            })),
                string.Format(Resources.SmallMoleculeTransitionListReader_SmallMoleculeTransitionListReader_,
                    TextUtil.LineSeparate(new[] { "grommet", "labbel"}),
                    TextUtil.LineSeparate(PasteDlg.SmallMoleculeTransitionListColumnHeaders.KnownHeaders())));
            // This should still be close enough to correct that we can tell that's what the user was going for
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV2));
           

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

            // Check our ability to help users with localized headers understand that we need standard strings
            // They might reasonably guess that we would support the names visible in the pasteDlg, but we prefer internal space-free names
            var textCSV4 = textCSV3.Replace(PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Name);
            AssertEx.ThrowsException<LineColNumberedIoException>(() => SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textCSV4,
                    Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            })),
                string.Format(Resources.SmallMoleculeTransitionListReader_SmallMoleculeTransitionListReader_,
                    Resources.PasteDlg_UpdateMoleculeType_Precursor_Name,
                    TextUtil.LineSeparate(PasteDlg.SmallMoleculeTransitionListColumnHeaders.KnownHeaders())));
            // This should still be close enough to correct that we can tell that's what the user was going for
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV4));

            // Check ability to paste into the Skyline window
            NewDocument();
            docOrig = WaitForDocumentChange(pastedDoc);
            RunUI(() =>
            {
                SetClipboardText(textCSV);
                SkylineWindow.Paste();
            });
            pastedDoc = WaitForDocumentChange(docOrig);
            Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assert.AreEqual(4, pastedDoc.MoleculeCount);
        }

        private void TestLabelsNoFormulas()
        {
            // Test our handling of labels without formulas

            var saveColumnOrder = Settings.Default.CustomMoleculeTransitionInsertColumnsList;

            var docOrig = NewDocument();
            var pasteDlg2 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            //non-standard column order
            var columnOrder = new[]
            {
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.nameProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                PasteDlg.SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
           };
            // If user omits some product info but not others, complain
            RunUI(() =>
            {
                pasteDlg2.IsMolecule = true;
                pasteDlg2.SetSmallMoleculeColumns(columnOrder.ToList());
            });
            WaitForConditionUI(() => pasteDlg2.GetUsableColumnCount() == columnOrder.ToList().Count);

            const string transistionList =
                "Amino Acids B\tAlaB\t\tlight\t\t\t225\t44\t-1\t-1\t3\n" +
                "Amino Acids B\tArgB\t\tlight\t\t\t310\t217\t-1\t-1\t19\n" +
                "Amino Acids\tAla\t\tlight\t\t\t225\t44\t1\t1\t3\n" +
                "Amino Acids\tAla\t\theavy\t\t\t229\t48\t1\t1\t4\n" + // NB we ignore RT conflicts
                "Amino Acids\tArg\t\tlight\t\t\t310\t217\t1\t1\t19\n" +
                "Amino Acids\tArg\t\theavy\t\t\t312\t219\t1\t1\t19\n" +
                "Amino Acids B\tAlaB\t\tlight\t\t\t225\t45\t-1\t-1\t3\n" +
                "Amino Acids B\tAlaB\t\theavy\t\t\t229\t48\t-1\t-1\t4\n" + // NB we ignore RT conflicts
                "Amino Acids B\tAlaB\t\theavy\t\t\t229\t49\t-1\t-1\t4\n" + // NB we ignore RT conflicts
                "Amino Acids B\tArgB\t\tlight\t\t\t310\t218\t-1\t-1\t19\n" +
                "Amino Acids B\tArgB\t\theavy\t\t\t312\t219\t-1\t-1\t19\n" +
                "Amino Acids B\tArgB\t\theavy\t\t\t312\t220\t-1\t-1\t19\n";


            SetClipboardText(transistionList);
            RunUI(pasteDlg2.PasteTransitions);
            OkDialog(pasteDlg2, pasteDlg2.OkDialog);
            var pastedDoc = WaitForDocumentChange(docOrig);
            Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assert.AreEqual(4, pastedDoc.MoleculeCount);
            
            NewDocument();
            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
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
    }  
}
