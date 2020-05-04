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
            SetClipboardTextUI(clipText.Replace(".", LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator));
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

            TestProductNeutralLoss();
            TestUnsortedMzPrecursors();
            TestNameCollisions();
            TestAmbiguousPrecursorFragment();
            TestPerTransitionValues();
            TestToolServiceAccess();
            TestLabelsNoFormulas();
            TestPrecursorTransitions();
            TestFullyDescribedPrecursors();
            TestTransitionListArrangementAndReporting();

            // Load a document whose settings understand heavy labeling
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("heavy.sky"))); 

            var fullColumnOrder = new[]
                {
                    SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.nameProduct,
                    SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                    SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    SmallMoleculeTransitionListColumnHeaders.labelType,
                    SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.rtWindowPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.cePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.note,
                    SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.adductProduct,
                    SmallMoleculeTransitionListColumnHeaders.dtPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.dtHighEnergyOffset,
                    SmallMoleculeTransitionListColumnHeaders.ccsPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.slens,
                    SmallMoleculeTransitionListColumnHeaders.coneVoltage,
                    SmallMoleculeTransitionListColumnHeaders.compensationVoltage,
                    SmallMoleculeTransitionListColumnHeaders.declusteringPotential,
                    SmallMoleculeTransitionListColumnHeaders.idInChiKey,
                    SmallMoleculeTransitionListColumnHeaders.idHMDB,
                    SmallMoleculeTransitionListColumnHeaders.idInChi,
                    SmallMoleculeTransitionListColumnHeaders.idCAS,
                    SmallMoleculeTransitionListColumnHeaders.idSMILES,
                    SmallMoleculeTransitionListColumnHeaders.idKEGG,
                    SmallMoleculeTransitionListColumnHeaders.imPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.imHighEnergyOffset,
                    SmallMoleculeTransitionListColumnHeaders.imUnits,
                    SmallMoleculeTransitionListColumnHeaders.neutralLossProduct,
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
                     var s = expectedErrors.Count;
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_value__0_,badfields[s++]));
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_high_energy_offset_value__0_, badfields[s++]));
                     expectedErrors.Add(
                         string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_collisional_cross_section_value__0_, badfields[s++]));
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_S_Lens_value__0_, badfields[s++]));
                     expectedErrors.Add(
                         string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_cone_voltage_value__0_, badfields[s++]));
                    expectedErrors.Add(
                        string.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_compensation_voltage__0_, badfields[s++]));
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
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_, badfields[s++]));
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
                Assert.AreEqual(explicitCE, product.ExplicitValues.CollisionEnergy);
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
                
            }
            // Reset
            var docOrig = NewDocument();

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
                    SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
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

            // Verify that we can import heavy/light pairs
            var columnOrderC = new[]
                {
                    SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    SmallMoleculeTransitionListColumnHeaders.labelType,
                };
            pasteText =
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

            // Verify adduct usage - none, or in own column, or as part of formula
            columnOrderC = new[]
                {
                    SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    SmallMoleculeTransitionListColumnHeaders.labelType,
                };
            pasteText =
                "A,27-HC,C36H57N2O3,,1,135,1,light\r\n" + // No adduct, just charge
                "A,27-HC,C36H57N2O3,[M+],1,130,1,light\r\n" + // Note this claims a charge with no protonation, thus not the same precursor as these others
                "A,27-HC,C36H57N2O3,MH,,181,1,light\r\n" + // Note the implicit postive ion mode "MH"
                "A,27-HC,C36H57N2O3[M+H],,,367,1,light\r\n" ;
            NewDocument();
            TestError(pasteText, String.Empty, columnOrderC);
            docC = SkylineWindow.Document;
            Assert.AreEqual(1, docC.MoleculeGroupCount);
            Assert.AreEqual(1, docC.MoleculeCount);
            Assert.AreEqual(2, docC.MoleculeTransitionGroupCount);
            Assert.AreEqual(3, docC.MoleculeTransitionGroups.First().TransitionCount);  

            // Verify adduct usage - none, or in own column, or as part of formula, when no name hints are given
            columnOrderC = new[]
                {
                    SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                    SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                    SmallMoleculeTransitionListColumnHeaders.mzProduct,
                    SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                    SmallMoleculeTransitionListColumnHeaders.labelType,
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
            var pasteDlg2 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
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

            var text = GetCsvFileText(TestFilesDir.GetTestPath("small_molecule_paste_test.csv"));
            // Now that we support charge-only adducts, "1" means "[M+]" rather than "[M+H]" in a numbers-only transition list
            // But these mz values were calculated with protonation in mind
            for (var c = 1; c <= 4; c++)
            {
                var pattern = string.Format("{0}{0}{1}{0}{1}{0}", TextUtil.CsvSeparator, c);
                var subst = string.Format("{0}{0}M+{1}H{0}M+{1}H{0}", TextUtil.CsvSeparator, c);
                text = text.Replace(pattern, subst);
            }
            // Tack on some molecule IDs so we can test reports (NB these don't match formula, so may fail in future)
            var rows = text.Replace("\r","").Split('\n').Select(line => line.Contains("lager") ?
                line + TextUtil.CsvSeparator + string.Join(TextUtil.CsvSeparator.ToString(), caffeineCAS, caffeineHMDB, "\"" + caffeineInChi + "\"", caffeineInChiKey, caffeineSMILES, caffeineKEGG) : 
                line);
            text = TextUtil.LineSeparate(rows);
            SetClipboardText(text); 
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
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.ExplicitIonMobilityHighEnergyOffset",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitCollisionalCrossSection",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.ExplicitCollisionEnergy",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.ExplicitDeclusteringPotential",
                    "Proteins!*.Peptides!*.Precursors!*.ExplicitCompensationVoltage",
                    "Proteins!*.Peptides!*.InChiKey",
                    "Proteins!*.Peptides!*.InChI",
                    "Proteins!*.Peptides!*.HMDB",
                    "Proteins!*.Peptides!*.SMILES",
                    "Proteins!*.Peptides!*.CAS",
                    "Proteins!*.Peptides!*.KEGG"});

                const double explicitCE2= 123.45;
                var colCE = FindDocumentGridColumn(documentGrid, "ExplicitCollisionEnergy");
                RunUI(() => documentGrid.DataGridView.Rows[0].Cells[colCE.Index].Value = explicitCE2);
                WaitForCondition(() => (SkylineWindow.Document.MoleculeTransitionGroups.Any() &&
                  SkylineWindow.Document.MoleculeTransitions.First().ExplicitValues.CollisionEnergy.Equals(explicitCE2)));

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
            var pasteDlg2 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            //non-standard column order
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
                SmallMoleculeTransitionListColumnHeaders.labelType,
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
                "Oly\tlager\tlager\t452\t\t\t\t1\t\tmacrobrew" + "\n" +
                "Oly\tlager\tlager\t234\t\t\t\t1\t\tmacrobrew";
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
                "Schmidt\tlager\t\t150\t150\t\t\t2\t2\tnotated!" + "\n" +
                "Schmidt\tlager\t\t159\t159\t\t\t3\t3\tnote!";
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
                "Oly\tlager\t\t452.1\t\t\t\t1\t\tmacrobrew\theavy" + "\n" +
                "Oly\tlager\t\t234.5\t\t\t\t1\t\tmacrobrew\tlight";
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

            // Load a document whose settings call for different mass type for precursors and fragments
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("mixed_mass_types.sky")));
            docOrig = SkylineWindow.Document;
            var pasteDlg5 = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var columnOrder5 = new[]
            {
                SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
            };
            RunUI(() =>
            {
                pasteDlg5.IsMolecule = true;
                pasteDlg5.SetSmallMoleculeColumns(columnOrder5.ToList());
            });
            WaitForConditionUI(() => pasteDlg5.GetUsableColumnCount() == columnOrder5.ToList().Count);
            const string precursorOnly = "15xT\tC150H197N30O103P14\t-3";
            SetClipboardText(precursorOnly);
            RunUI(pasteDlg5.PasteTransitions);
            OkDialog(pasteDlg5, pasteDlg5.OkDialog);
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
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV2));
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV2.ToLowerInvariant())); // Be case insensitive
            // But the word "peptide" should prevent us from trying to read this as small molecule data
            Assert.IsFalse(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV2.Replace("grommet", "Peptide")));
           

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
            Assert.IsTrue(SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(textCSV4));

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


            SetClipboardText(transistionList.Replace(".",
                CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator));
            RunUI(pasteDlg2.PasteTransitions);
            OkDialog(pasteDlg2, pasteDlg2.OkDialog);
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
                SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                SmallMoleculeTransitionListColumnHeaders.nameProduct,
                SmallMoleculeTransitionListColumnHeaders.neutralLossProduct,
                SmallMoleculeTransitionListColumnHeaders.adductProduct,
                SmallMoleculeTransitionListColumnHeaders.note,
                SmallMoleculeTransitionListColumnHeaders.cePrecursor
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
            "MoleculeGroup,PrecursorName,PrecursorFormula,PrecursorAdduct,PrecursorMz,PrecursorCharge,ProductName,ProductFormula,ProductAdduct,ProductMz,ProductCharge,Note,PrecursorCE\n"+
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,precursor,C20H32O3,[M-H]1-,319.227868554909,-1,,21\n" + 
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,m/z 301.2172,,[M-H]1-,301.2172,-1,,21\n" + 
            "12-HETE,12-HETE,C20H32O3,[M-H]1-,319.227868554909,-1,m/z 275.2377,,[M-H]1-,275.2377,-1,,21\n" + 
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,precursor,C20H32O3,[M8H2-H]1-,327.278082506909,-1,,21\n" + 
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,m/z 309.2674,,[M-H]1-,309.2674,-1,,21\n" + 
            "12-HETE,12-HETE(+[2]H8),C20H32O3,[M8H2-H]1-,327.278082506909,-1,m/z 283.2879,,[M-H]1-,283.2879,-1,,21\n";
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
                "ThompsonIS,Apain,heavy,455,1,387,1,20,1\n" +
                "ThompsonIS,Apain,heavy,455,1,191,1,25,1\n";
            SetClipboardText(precursorsTransitionList);

            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());

            var pastedDoc = WaitForDocumentChange(docOrig);
            Assume.AreEqual(1, pastedDoc.MoleculeGroupCount);
            Assume.AreEqual(1, pastedDoc.MoleculeCount);
            var precursors = pastedDoc.MoleculeTransitionGroups.ToArray();
            Assume.IsTrue(!precursors[0].PrecursorAdduct.HasIsotopeLabels);
            Assume.IsTrue(precursors[1].PrecursorAdduct.HasIsotopeLabels);
            var transitions = pastedDoc.MoleculeTransitions.ToArray();
            Assume.AreEqual(2, transitions.Count(t => t.ExplicitValues.CollisionEnergy == 20));
            Assume.AreEqual(2, transitions.Count(t => t.ExplicitValues.CollisionEnergy == 25));
            TestTransitionListOutput(pastedDoc, "per_trans.csv", "per_trans_expected.csv", ExportFileType.List);
            

            docOrig = NewDocument();
            const string precursorsTransitionListHEOffset =
                "Precursor Name,Precursor Formula,Precursor Adduct,Precursor charge,Explicit Retention Time,Collisional Cross Section (Sq A),Product m/z,product charge,explicit ion mobility High energy Offset\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,,,\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,156.0112,1,0.5\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+H],1,1.85,157.7,92.0498,1,0.51\n" +
                "Sulfamethizole,C9H10N4O2S2,[M+Na],1,1.85,173.43,,,\n" +
                "Sulfamethazine,C12H14N4O2S,[M+H],1,2.01,163.56,,,\n" +
                "Sulfamethazine,C12H14N4O2S,[M+H],1,2.01,,186.0336,1,0.2\n" +
                "Sulfamethazine,C12H14N4O2S,[M+H],1,2.01,,124.0873,1,0.21\n" +
                "Sulfamethazine,C12H14N4O2S,[M+Na],1,2.01,172.47,,,\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+H],1,2.51,161.23,,,\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+H],1,2.51,161.23,156.011,1,0.1\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+H],1,2.51,161.23,92.0495,1,0.11\n" +
                "Sulfachloropyridazine,C10H9ClN4O2S,[M+Na],1,2.51,171.16,,,\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+H],1,3.68,170.01,,,\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+H],1,3.68,170.01,156.077,1,0.3\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+H],1,3.68,170.01,108.0445,1,0.31\n" +
                "Sulfadimethoxine,C12H14N4O4S,[M+Na],1,3.68,177.96,,,\n";
            SetClipboardText(precursorsTransitionListHEOffset);

            // Paste directly into targets area
            RunUI(() => SkylineWindow.Paste());

            pastedDoc = WaitForDocumentChange(docOrig);
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
    }  
}
