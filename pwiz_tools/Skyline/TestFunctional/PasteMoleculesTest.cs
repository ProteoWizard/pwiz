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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
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
            var allErrorText = string.Empty;
            clipText = clipText.Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            var transitionDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => transitionDlg.textBox1.Text = clipText);

            RunUI(() =>
            {
                windowDlg.radioMolecule.PerformClick();
                if (columnOrder != null)
                {
                    windowDlg.SetSelectedColumnTypes(columnOrder);
                }
            });

            if (string.IsNullOrEmpty(errText))
                OkDialog(windowDlg, windowDlg.OkDialog);  // We expect this to work, go ahead and load it
            else
            {
                if (!Equals(errText, Resources.PasteDlg_ShowNoErrors_No_errors))
                {
                    var errDlg = ShowDialog<ImportTransitionListErrorDlg>(windowDlg.OkDialog);
                    RunUI(() =>
                    {
                        foreach (var err in errDlg.ErrorList)
                        {
                            allErrorText += err.ErrorMessage;
                        }
                        Assert.IsTrue(allErrorText.Contains(errText),
                            string.Format("Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                                errText, errDlg.ErrorList));
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

        const string caffeineInChiKey = "RYYVLZVUVIJVGH-UHFFFAOYSA-N";
        const string caffeineHMDB = "HMDB01847";
        const string caffeineInChi = "InChI=1S/C8H10N4O2/c1-10-4-9-6-5(10)7(13)12(3)8(14)11(6)2/h4H,1-3H3";
        const string caffeineCAS = "58-08-2";
        const string caffeineSMILES = "Cn1cnc2n(C)c(=O)n(C)c(=O)c12";
        const string caffeineKEGG = "C07481";
        const string caffeineFormula = "C8H10N4O2";
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
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column, // Drift time columns are now obsolete
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column, // Drift time columns are now obsolete
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
            string line5 = line1.Replace(caffeineFormula,"C8H12N4O2[M-2H]").Replace(caffeineFragment,"C34H32").Replace(note + "\t\t\t", note + "\t\tM-2H\t"); // Legit
            string line6 = line1.Replace(caffeineFormula, "").Replace(caffeineFragment, "").Replace(note + "\t\t\t", note + "\t\tM-3H\t"); // mz only, but charge and adduct disagree

            // Provoke some errors
            TestError(line1.Replace("\t-2\t-2", "\t-2\t2").Replace(productMzAtZNeg2.ToString(CultureInfo.CurrentCulture),""), // precursor and charge polarities disagree
                Resources.Transition_Validate_Precursor_and_product_ion_polarity_do_not_agree_, fullColumnOrder);
            TestError(line1.Replace(caffeineFormula, "C77H12O4"), // mz and formula disagree
                String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Error_on_line__0___Precursor_m_z__1__does_not_agree_with_value__2__as_calculated_from_ion_formula_and_charge_state__delta____3___Transition_Settings___Instrument___Method_match_tolerance_m_z____4_____Correct_the_m_z_value_in_the_table__or_leave_it_blank_and_Skyline_will_calculate_it_for_you_,
                1, (float)precursorMzAtZNeg2, 499.0295, 402.9966, docEmpty.Settings.TransitionSettings.Instrument.MzMatchTolerance), fullColumnOrder);
            TestError(line1.Replace(caffeineFragment, "C76H3"), // mz and formula disagree
                String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Error_on_line__0___Product_m_z__1__does_not_agree_with_value__2__as_calculated_from_ion_formula_and_charge_state__delta____3___Transition_Settings___Instrument___Method_match_tolerance_m_z____4_____Correct_the_m_z_value_in_the_table__or_leave_it_blank_and_Skyline_will_calculate_it_for_you_,
                1, (float)productMzAtZNeg2, 456.5045, 396.9916, docEmpty.Settings.TransitionSettings.Instrument.MzMatchTolerance), fullColumnOrder);
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
            TestError(line1 + line2start + "\t7\t1", // Product Formula and m/z don't make sense together
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_, 2), fullColumnOrder);
            TestError(line1 + line2start + "\t", // No mz or charge for precursor or product
                String.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 2), fullColumnOrder);
            TestError(line1 + line3, // Insanely large molecule
                string.Format(Resources.CustomMolecule_Validate_The_mass__0__of_the_custom_molecule_exceeeds_the_maximum_of__1__, 503970013.01879, CustomMolecule.MAX_MASS), fullColumnOrder);
            TestError(line1 + line4, // Insanely small molecule
                string.Format(Resources.CustomMolecule_Validate_The_mass__0__of_the_custom_molecule_is_less_than_the_minimum_of__1__, 2.01588, CustomMolecule.MIN_MASS), fullColumnOrder);
            TestError(line1 + line2start + +precursorMzAtZNeg2 + "\t" + productMzAtZNeg2 + "\t-2\t-2\t\t\t" + precursorRTWindow + "\t" + explicitCE + "\t" + note + "\t\t\t" + precursorDT + "\t" + highEnergyDtOffset, // Explicit retention time window without retention time
                Resources.Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_, fullColumnOrder);
            TestError(line5.Replace("[M-2H]", "[M+H]"), string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_, "[M+H]", 1, -2), fullColumnOrder);
            TestError(line6, string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_, "M-3H", -3, -2), fullColumnOrder);
            for (int withSpecials = 2; withSpecials-- > 0; )
            {
                // By default we don't show drift or other exotic columns
                var columnOrder = (withSpecials == 0) ? fullColumnOrder.Take(16).ToArray() : fullColumnOrder;
                int imIndex = 0;
                int covIndex = 0;
                // Take a legit full paste and mess with each field in turn
                string[] fields =
                {
                    "MyMol", "MyPrecursor", "MyProduct", "C12H9O4", "C6H4O2", "217.049535420091", "108.020580420091", "1", "1", "heavy", "123", "5", "25", "this is a note", "[M+]", "[M+]", "7", "9", "123", "88.5", "99.6", "77.3", "66.2",
                                caffeineInChiKey, caffeineHMDB, caffeineInChi, caffeineCAS, caffeineSMILES, caffeineKEGG, "123.4", "-0.234", "Vsec/cm2", "C6H5O2"  };
                string[] badfields =
                {
                    "", "", "", "123", "C6H2O2[M+2H]", "fish", "-345", "cat", "pig", "12", "frog", "hamster", "boston", "", "[M+foo]", "wut", "foosballDT", "greasyDTHEO", "mumbleCCS", "gumdropSLEN", "dingleConeV", "dangleCompV", "gorseDP", "AHHHHHRGHinchik", "bananananahndb",
                    "shamble-raft4-inchi", "bags34cas","flansmile", "boozlekegg", "12-fooim", "bumbleimheo", "dingoimunit", "C6H15O5"};
                Assert.AreEqual(fields.Length, badfields.Length);

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
                    string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0__, "["+ badfields[15] + "]"),
                };
                if (withSpecials > 0)
                {
                    // With addition of Explicit Ion Mobility, Explicit Compensation Voltage becomes a conflict
                    expectedErrors[0] = Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Multiple_ion_mobility_declarations;

                    var s = expectedErrors.Count;
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ShowNoErrors_No_errors)); s++; // No longer possible to have both "drift" and "ion mobility" columns at once, user would have to set this as "Ignore" so no error
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ShowNoErrors_No_errors)); s++; // No longer possible to have both "drift" and "ion mobility" columns at once, user would have to set this as "Ignore" so no error
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_collisional_cross_section_value__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_S_Lens_value__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_cone_voltage_value__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_compensation_voltage__0_, badfields[covIndex = s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_declustering_potential__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_InChiKey_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_HMDB_identifier_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_InChI_identifier_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_CAS_registry_number_, badfields[s++]));
                    expectedErrors.Add(
                        Resources.PasteDlg_ShowNoErrors_No_errors); s++;  // We don't have a proper SMILES syntax check yet
                    expectedErrors.Add(
                        Resources.PasteDlg_ShowNoErrors_No_errors); s++;  // We don't have a proper KEGG syntax check yet
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_, badfields[imIndex = s++]));
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_high_energy_offset_value__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_units_value__0___accepted_values_are__1__, badfields[s++], SmallMoleculeTransitionListReader.GetAcceptedIonMobilityUnitsString()));
                    expectedErrors.Add(
                        string.Format(Resources.SmallMoleculeTransitionListReader_ProcessNeutralLoss_Precursor_molecular_formula__0__does_not_contain_sufficient_atoms_to_be_used_with_neutral_loss__1_, fields[3], badfields[s++]));
                }
                expectedErrors.Add(Resources.PasteDlg_ShowNoErrors_No_errors); // N+1'th pass is unadulterated
                for (var bad = 0; bad < expectedErrors.Count; bad++)
                {
                    var line = "";
                    for (var f = 0; f < expectedErrors.Count-1; f++)
                        line += ((bad == f) ? badfields[f] : fields[f]).Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator) + "\t";
                    if (!string.IsNullOrEmpty(expectedErrors[bad]))
                        TestError(line, expectedErrors[bad], columnOrder);
                    if (imIndex > 0)
                    {
                        // Now that we have tested the warning, clear up the conflict between declared CoV and declared ion mobility
                        if (bad < covIndex)
                        {
                            columnOrder[imIndex] = Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column;
                            columnOrder[covIndex] = Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage;
                        }
                        else
                        {
                            columnOrder[imIndex] = Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility;
                            columnOrder[covIndex] = Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column;
                        }
                    }
                }
            }
            TestError(line1.Replace(caffeineFormula, caffeineFormula + "[M-H]").Replace(caffeineFragment, caffeineFragment + "[M-H]") + line2start + "\t\t1\t1", 
                string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_,"[M-H]",-1,-2), fullColumnOrder);

            // Now load the document with a legit paste
            foreach (var imTypeIsDrift in new[]{ true, false }) // Check interplay of explicit Compensation Voltage and explicit IM
            {
                docEmpty = NewDocument();
                line1 = BuildTestLine(imTypeIsDrift);
                var expectedIM = imTypeIsDrift ? precursorDT : compensationVoltage;
                double? expectedCV = imTypeIsDrift ? (double?)null : compensationVoltage;
                var expectedTypeIM = imTypeIsDrift ? eIonMobilityUnits.drift_time_msec : eIonMobilityUnits.compensation_V;
                TestError(line1 + line2start.Replace("CH3O", "CH29") + "\t\t1\t\t\t\t\t\t\t\tM+H", String.Empty, fullColumnOrder);
                var docTest = WaitForDocumentChange(docEmpty);
                var testTransitionGroups = docTest.MoleculeTransitionGroups.ToArray();
                Assert.AreEqual(2, testTransitionGroups.Length);
                var transitionGroup = testTransitionGroups[0];
                var precursor = docTest.Molecules.First();
                var product = transitionGroup.Transitions.First();
                Assert.AreEqual(explicitCE, product.ExplicitValues.CollisionEnergy?? transitionGroup.ExplicitValues.CollisionEnergy);
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
                Assert.AreEqual(precursorMzAtZNeg2, transitionGroup.PrecursorAdduct.MzFromNeutralMass(transitionGroup.CustomMolecule.MonoisotopicMass), 1E-6);
                Assert.AreEqual(productMzAtZNeg2, product.Transition.Adduct.MzFromNeutralMass(product.GetMoleculeMass()), 1E-6);
                Assert.AreEqual(precursorMzAtZNeg2, transitionGroup.PrecursorAdduct.MzFromNeutralMass(transitionGroup.CustomMolecule.MonoisotopicMass.Value, transitionGroup.CustomMolecule.MonoisotopicMass.MassType), 1E-6);
                Assert.AreEqual(productMzAtZNeg2, product.Transition.Adduct.MzFromNeutralMass(product.GetMoleculeMass().Value, product.GetMoleculeMass().MassType), 1E-6);
                Assert.AreEqual(caffeineInChiKey, precursor.CustomMolecule.PrimaryEquivalenceKey); // Use InChiKey as primary library key when available
                Assert.AreEqual(caffeineInChiKey, precursor.CustomMolecule.AccessionNumbers.PrimaryAccessionValue); // Use InChiKey as primary library key when available
                Assert.AreEqual(MoleculeAccessionNumbers.TagInChiKey, precursor.CustomMolecule.AccessionNumbers.PrimaryAccessionType); // Use InChiKey as primary library key when available
                Assert.AreEqual(caffeineInChiKey, precursor.CustomMolecule.AccessionNumbers.AccessionNumbers[0].Value); // Use InChiKey as primary library key when available
                string hmdb;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers.TryGetValue("HMDB", out hmdb);
                Assert.AreEqual(caffeineHMDB.Substring(4), hmdb);
                string inchi;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers.TryGetValue("InChi", out inchi);
                Assert.AreEqual(caffeineInChi.Substring(6), inchi);
                string cas;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers.TryGetValue("cAs", out cas); // Should be case insensitive
                Assert.AreEqual(caffeineCAS, cas);
                string smiles;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers.TryGetValue("smILes", out smiles); // Should be case insensitive
                Assert.AreEqual(caffeineSMILES, smiles);
                string kegg;
                precursor.CustomMolecule.AccessionNumbers.AccessionNumbers.TryGetValue("kEgG", out kegg); // Should be case insensitive
                Assert.AreEqual(caffeineKEGG, kegg);
                // Does that produce the expected transition list file?
                TestTransitionListOutput(docTest, "PasteMoleculeTinyTest.csv", "PasteMoleculeTinyTestExpected.csv", ExportFileType.IsolationList);
                // Does serialization of imported values work properly?
                AssertEx.Serializable(docTest);

                // Verify that this text can be imported as a file with File > Import > Transition List
                TestFileImportTransitionList(line1);

            }
            // Reset
            var docOrig = NewDocument();

            // Now a proper user data set
            var showDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            // Formerly SetExcelFileClipboardText(TestFilesDir.GetTestPath("MoleculeTransitionList.xlsx"),"sheet1",6,false); but TeamCity doesn't like that
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => showDialog.textBox1.Text = GetCsvFileText(TestFilesDir.GetTestPath("MoleculeTransitionList.csv")));

            RunUI(() => {
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
            TestError(pasteText, String.Empty, columnOrderC);
            var docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);
            Assert.AreEqual(3, docC.MoleculeTransitionGroups.First().TransitionCount);  

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
            TestError(pasteText, String.Empty, columnOrderC);
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
            TestError(pasteText, String.Empty, columnOrderC);
            docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);

            pasteText =
                "C36H56N2O3,M+S,,181,,light\r\n"; // Adduct with unknown charge
            NewDocument();
            TestError(pasteText,
                string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Cannot_derive_charge_from_adduct_description___0____Use_the_corresponding_Charge_column_to_set_this_explicitly__or_change_the_adduct_description_as_needed_, "[M+S]"),
                columnOrderC);
            pasteText =
                "C36H56N2O3,M+S,1,181,1,light\r\n"; // Adduct with unknown charge, but charge provided seperately
            NewDocument();
            TestError(pasteText,
                string.Empty,
                columnOrderC);
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
                SmallMoleculeTransitionListColumnHeaders.note,
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
            var windowDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => showDialog.textBox1.Text = GetCsvFileText(TestFilesDir.GetTestPath("small_molecule_paste_test.csv")).Replace(
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
            var col0Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => text1Dialog.textBox1.Text = text);
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
            var errText = string.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 1);
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

        private void TestToolServiceAccess()
        {
            // Test the tool service logic without actually using tool service (there's a test for that too)
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
            var textClean = textCSV;
            SkylineWindow.Invoke(new Action(() =>
            {
                SkylineWindow.InsertSmallMoleculeTransitionList(textClean, Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            }));

            var pastedDoc = WaitForDocumentChange(docOrig);
            Assert.AreEqual(2, pastedDoc.MoleculeGroupCount);
            Assert.AreEqual(4, pastedDoc.MoleculeCount);

            var exception = new LineColNumberedIoException(Resources.MassListImporter_Import_Failed_to_find_peptide_column, 1,
                -1);

            // Inserting the header row by itself should produce an error message
            AssertEx.ThrowsException<LineColNumberedIoException>(() => SkylineWindow.Invoke(new Action(() =>
                {
                    SkylineWindow.Paste(header);
                })),
                exception.Message);

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
                NewDocument();
                docOrig = WaitForDocumentChange(pastedDoc);
                var csv6 = textCSV6;
                RunUI(() =>
                {
                    SetClipboardText(csv6);
                    SkylineWindow.Paste();
                });
                pastedDoc = WaitForDocumentChange(docOrig);
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
            NewDocument();
            RunUI(() =>
            {
                SetClipboardText(textCSV7);
                SkylineWindow.Paste();
            });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 2, 2, 4);

            // Check case insensitivity, m/z vs mz
            var textCSV8 =
                "MOLECULE LIST NAME,PRECURSOR NAME,PRECURSOR FORMULA,PRECURSOR ADDUCT,EXPLICIT RETENTION TIME,COLLISIONAL CROSS SECTION (SQ A),PRODUCT MZ,PRODUCT CHARGE\n" +
                "Lipid,L1,C41H74NO8P,[M+H],6.75,273.41,,\n" +
                "Lipid,L1,C41H74NO8P,[M+H],6.75,273.41,263.2371,1\n" +
                "Lipid,L2,C42H82NO8P,[M+Na],7.3,288.89,,\n" +
                "Lipid,L2,C42H82NO8P,[M+Na],7.3,288.89,184.0785,1\n";
            NewDocument();
            RunUI(() =>
            {
                SetClipboardText(textCSV8);
                SkylineWindow.Paste();
            });
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
                        TestError(GetCsvFileText(filename), null, null);
                        break;
                    case 1: // Use File | Import | Transition List
                        ImportTransitionListSkipColumnSelect(filename);
                        break;
                    case 2: // Paste into Targets window
                        var dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste(GetCsvFileText(filename)));
                        OkDialog(dlg, dlg.OkDialog);
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
                                        !transition.Transition.CustomIon.Formula.Contains(@"C")); // Can't C13-label a fragment with no C in it
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

        private void TestImpliedFragmentAdduct()
        {
            // Test ability to infer fragment adduct from formula and stated m/z
            var text = 
                "MoleculeListName\tscan\tPrecursorCharge\tPrecursorAdduct\tinchikey\tmolecularformula\tmoleculename\thmdb or cas\texplicitretentiontime\texplicitretentiontimewindow\tMW\tprecursor m/z\tproductformula\tproduct m/z\tfrag1calc\tfrag2comp\n" +
                "QEP1_2021_0823_RJ_21_2ab55prm.raw\t3423\t1\t[M+H]\tQQVDJLLNRSOCEL-UHFFFAOYSA-N\tC2H8NO3P\t(2-AMINOETHYL)PHOSPHONATE\tHMDB11747\t5\t2\t125.0241796\t126.0320046\tCH6O3P\t97.0054556\tH4O4P\t98.9847214\n" + // Implied fragment M+
                "QEP1_2021_0823_RJ_31_2ef55prm.raw\t5909\t1\t[M+H]\tXFNJVJPLKCPIBV-UHFFFAOYSA-N\tC3H10N2\t\"1,3 - DIAMINOPROPANE\"\tHMDB00002\t8.9\t2\t74.0844\t75.09222333\tC3H8N\t59.0656708\tna\tna\n"; // Implied fragment M+H
            var docOrig = NewDocument();
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.textBox1.Text = text);
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
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.textBox1.Text = text);
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
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.textBox1.Text = text);

            var errDlg = ShowDialog<ImportTransitionListErrorDlg>(columnSelectDlg.OkDialog);
            RunUI(() =>
            {
                var allErrorText = string.Empty;
                foreach (var err in errDlg.ErrorList)
                {
                    allErrorText += err.ErrorMessage;
                }
                var errText = string.Format(Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, 1);
                Assert.IsTrue(allErrorText.Contains(errText),
                    "Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                    errText, errDlg.ErrorList);
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
            var col4Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.textBox1.Text = transistionList.Replace(".",
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

            var docOrig = NewDocument();
            var precursorsTransitionList =
                "MoleculeGroup\tPrecursorName\tPrecursorFormula\tPrecursorAdduct\tPrecursorMz\tPrecursorCharge\tProductName\tProductFormula\tProductNeutralLoss\tProductAdduct\tNote\tPrecursorCE\n" +
                "12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tprecursor\tC20H32O3\t\t[M-H]1-\t\t21\n" +
                "12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tfrag1\tC20H30O\t\t[M-H]1-\t\t21\n" +
                "12-HETE\t12-HETE\tC20H32O3\t[M-H]1-\t319.227868554909\t-1\tfrag2\t\tH2O\t[M-H]1-\t\t21\n";
            SetClipboardText(precursorsTransitionList.Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator));

            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());

            var pastedDoc = WaitForDocumentChange(docOrig);
            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(1, pastedDoc.MoleculeCount);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(2, transitions.Count(t => !t.IsMs1));
            Assume.AreEqual("C20H30O", transitions[1].CustomIon.NeutralFormula); // As given literally
            Assume.AreEqual("C20H30O2", transitions[2].CustomIon.NeutralFormula); // As given by neutral loss
            NewDocument();

        }

        private void TestFullyDescribedPrecursors()
        {
            // Test our handling of fully described precursors

            var docOrig = NewDocument();
            const string precursorsTransitionList =
            "MoleculeGroup,PrecursorName,PrecursorFormula,PrecursorAdduct,PrecursorMz,PrecursorCharge,ProductName,ProductFormula,ProductAdduct,ProductMz,ProductCharge,Note,PrecursorCE,Collisional Cross Section\n"+
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,precursor,C20H32O3,[M-H]1-,319.227868554909,-1,,21,123\n" +
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,m/z 301.2172,,[M-H]1-,301.2172,-1,,21,123\n" +
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,m/z 275.2377,,[M-H]1-,275.2377,-1,,21,123\n" +
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,precursor,C20H32O3,[M8H2-H]1-,327.278082506909,-1,,21,126\n" +
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,m/z 309.2674,,[M-H]1-,309.2674,-1,,21,126\n" +
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,m/z 283.2879,,[M-H]1-,283.2879,-1,,21,126\n";
            SetClipboardText(precursorsTransitionList);

            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());

            var pastedDoc = WaitForDocumentChange(docOrig);
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
            SetClipboardText(precursorsTransitionListSorted);
            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());
            var pastedDocSorted = WaitForDocumentChange(docOrig);

            docOrig = NewDocument();
            SetClipboardText(precursorsTransitionListUnsorted);
            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());
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
            NewDocument();

        }


        private void TestPerTransitionValues()
        {
            // Test our handling of fragments with unique explicit values
            var docOrig = NewDocument();
            const string precursorsTransitionList =
                "Molecule List Name,Molecule,Label Type,Precursor m/z,Precursor Charge,Product m/z,Product Charge,Explicit Collision Energy,Explicit Retention Time\n" +
                "ThompsonIS,Apain,light,452,1,384,1,20,1\n" +
                "ThompsonIS,Apain,light,452,1,188,1,25,1\n" +
                "ThompsonIS,Apain,light,452,1,160,1,,1\n" + // No explicit CE
                "ThompsonIS,Apain,light,452,1,140,1,20,1\n" + // Same explicit CE as first
                "ThompsonIS,Apain,heavy,455,1,387,1,21,1\n" +
                "ThompsonIS,Apain,heavy,455,1,191,1,26,1\n" +
                "ThompsonIS,Bpain,light,567,1,,,35,1\n"; // Precursor-only explicit CE
            SetClipboardText(precursorsTransitionList);

            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());

            var pastedDoc = WaitForDocumentChange(docOrig);
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
            

            docOrig = NewDocument();
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
            RunUI(() => SkylineWindow.Paste());

            pastedDoc = WaitForDocumentChange(docOrig);

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
                pastedDoc = AssertEx.Serializable(pastedDoc, TestDirectoryName, SkylineVersion.CURRENT); 
            }
            NewDocument();

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

            var docOrig = NewDocument();
            SetClipboardText(input);

            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());

            var pastedDoc = WaitForDocumentChange(docOrig);
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
            var docOrig = NewDocument();
            SetClipboardText(input);

            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());

            var pastedDoc = WaitForDocumentChange(docOrig);
            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(5, pastedDoc.MoleculeCount);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(10, transitions.Count(t => t.IsMs1));
            Assume.AreEqual(0, transitions.Count(t => !t.IsMs1));
            NewDocument();
        }

        private void TestInconsistentMoleculeDescriptions()
        {
            // Check that we handle items with same name but different InChiKey, which is legitimate
            // Also checks that we handle LipidCreator output where everything is quoted
            var input =
                "Molecule List Name, Precursor Name,Precursor Formula, Precursor Adduct,Precursor Charge, Product m/z,Product Charge, Explicit Retention Time, Explicit Collision Energy, InChiKey, Explicit Declustering potential\n" +
                "\"bob\",\"D-Erythrose 4-phosphate\",\"C4H9O7P\",\"[M-H]\",\"-1\",\"97\",\"-1\",\"\",\"8\",\"NGHMDNPXVRFFGS-IUYQGCFVSA-N\",\"60\"\n" +
                "\"bob\",\"D-Erythrose 4-phosphate\",\"C4H9O7P\",\"[M+H]\",\"1\",\"99\",\"1\",\"\",\"8\",\"\",\"60\"\n";
            var docOrig = NewDocument();
            SetClipboardText(input);

            // Paste directly into targets area, which should create an error and then send us to ColumnSelectDlg
            var errDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            WaitForDocumentLoaded();
            // Correct the header assignments
            RunUI(() => {
                errDlg.radioMolecule.PerformClick();
                errDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time,
                    Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy,
                    @"InChiKey",
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential);
            });

            // This should produce an inconsistent molecule description error
            RunDlg<ImportTransitionListErrorDlg>(errDlg.OkDialog, msgDlg => msgDlg.Close()); // Dismiss it
            // Cancel the window
            OkDialog(errDlg, errDlg.CancelDialog);
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
                SkylineWindow.ImportMassList(filename);
            });
            WaitForCondition(() => 0 != SkylineWindow.Document.MoleculeCount);

            // Now verify error handling
            filename = TestFilesDir.GetTestPath("known_bad.csv");
            File.WriteAllText(filename, @"foo"+contents);
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
            });

            // One of the headers cannot be understood so we should see a ColumnSelectDlg come up
            var badDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportMassList(filename));
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
                    RunUI(() => SkylineWindow.ImportMassList(tempFile));
                }
                else
                {
                    SetClipboardText(input);
                    RunUI(() => SkylineWindow.Paste());
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

                var tempFile = TestFilesDir.GetTestPath(string.Format("transitions_tmp{0}.csv", pass));
                if (asFile)
                {
                    File.WriteAllText(tempFile, input);
                }
                else
                {
                    SetClipboardText(input);
                }

                if (withHeaders)
                {
                    // With headers, should be no need for header selection
                    if (asFile)
                    {
                        RunUI(() => SkylineWindow.ImportMassList(tempFile));
                    }
                    else
                    {
                        RunUI(() => SkylineWindow.Paste());
                    }
                }
                else
                {
                    var testImportDlg = asFile ?
                        // Import the file, which should send us to ColumnSelectDlg
                        ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.ImportMassList(tempFile)) :
                        // Paste directly into targets area, which should send us to ColumnSelectDlg
                        ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
                    WaitForDocumentLoaded();
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
                }
                var pastedDoc = WaitForDocumentChange(docOrig);
                AssertEx.IsDocumentState(pastedDoc, null, 2, 4, 8, 12);
            }
        }
    }
}
