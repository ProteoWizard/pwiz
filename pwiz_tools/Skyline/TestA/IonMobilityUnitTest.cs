/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class IonMobilityUnitTest : AbstractUnitTest
    {
        /// <summary>
        /// Test various error conditions in IonMobilityDb.cs
        /// </summary>
        [TestMethod]
        public void TestGetIonMobilityDBErrorHandling()
        {
            AssertEx.ThrowsException<DatabaseOpeningException>(() => IonMobilityDb.GetIonMobilityDb(null, null),
                Resources.IonMobilityDb_GetIonMobilityDb_Please_provide_a_path_to_an_existing_ion_mobility_database_);

            const string badfilename = "nonexistent_file.imdb";
            AssertEx.ThrowsException<DatabaseOpeningException>(
                () => IonMobilityDb.GetIonMobilityDb(badfilename, null),
                String.Format(
                    Resources
                        .IonMobilityDb_GetIonMobilityDb_The_ion_mobility_database_file__0__could_not_be_found__Perhaps_you_did_not_have_sufficient_privileges_to_create_it_,
                    badfilename));

            const string bogusfile = "bogus.imdb";
            using (FileStream fs = File.Create(bogusfile))
            {
                Byte[] info = new UTF8Encoding(true).GetBytes("This is a bogus file.");
                fs.Write(info, 0, info.Length);
            }
            AssertEx.ThrowsException<DatabaseOpeningException>(
                () => IonMobilityDb.GetIonMobilityDb(bogusfile, null),
                String.Format(
                    Resources.IonMobilityDb_GetIonMobilityDb_The_file__0__is_not_a_valid_ion_mobility_database_file_,
                    bogusfile));
        }

        /// <summary>
        /// Test various error conditions in AddDriftTimeSpectralLibrary.cs
        /// </summary>
        [TestMethod]
        public void TestAddDriftTimeSpectralLibraryErrorHandling()
        {
            var message = AddDriftTimeSpectralLibrary.ValidateSpectralLibraryPath(null);
            Assert.AreEqual(message,
                Resources
                    .AddDriftTimeSpectralLibrary_ValidateSpectralLibraryPath_Please_specify_a_path_to_an_existing_spectral_library);

            message =
                AddDriftTimeSpectralLibrary.ValidateSpectralLibraryPath("redundant." + BiblioSpecLiteSpec.EXT_REDUNDANT);
            AssertEx.Contains(message,
                Resources.AddDriftTimeSpectralLibrary_ValidateSpectralLibraryPath_Please_choose_a_non_redundant_library_);

            message = AddDriftTimeSpectralLibrary.ValidateSpectralLibraryPath("badchoice.nist");
                // Only wants to see .blib
            AssertEx.Contains(message,
                Resources
                    .AddDriftTimeSpectralLibrary_ValidateSpectralLibraryPath_Only_BiblioSpec_libraries_contain_enough_ion_mobility_information_to_support_this_operation_);

            message = AddDriftTimeSpectralLibrary.ValidateSpectralLibraryPath("fakefile." + BiblioSpecLiteSpec.EXT);
                // File is well named but doesn't exist
            AssertEx.Contains(message,
                Resources
                    .AddDriftTimeSpectralLibrary_ValidateSpectralLibraryPath_Please_specify_a_path_to_an_existing_spectral_library_);
        }

        /// <summary>
        /// Test various error conditions in EditIonMobilityLibraryDlg.cs,
        /// mostly around handling conflicting values
        /// </summary>
        [TestMethod]
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
                peptides.Add(new ValidatingIonMobilityPeptide(seqB, n+1));
            }
            message = EditIonMobilityLibraryDlg.ValidateUniqueChargedPeptides(peptides, out peptideSet);
            AssertEx.Contains(message,
                string.Format(Resources.EditIonMobilityLibraryDlg_ValidateUniqueChargedPeptides_The_added_list_contains__0__charged_peptides_with_inconsistent_ion_mobility_values_,
                                        22));
        }

        /// <summary>
        /// Test various error conditions in EEditDriftTimePredictorDlg.cs
        /// </summary>
        [TestMethod]
        public void TestEditDriftTimePredictorDlgErrorHandling()
        {
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateResolvingPower(0), Resources.EditDriftTimePredictorDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_);
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateResolvingPower(-1), Resources.EditDriftTimePredictorDlg_ValidateResolvingPower_Resolving_power_must_be_greater_than_0_);
            Assert.IsNull(EditDriftTimePredictorDlg.ValidateResolvingPower(1));

            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateCharge(0),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                0, TransitionGroup.MAX_PRECURSOR_CHARGE));
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateCharge(99),
                String.Format(
                Resources.EditDriftTimePredictorDlg_ValidateCharge_The_entry__0__is_not_a_valid_charge__Precursor_charges_must_be_integer_values_between_1_and__1__,
                99, TransitionGroup.MAX_PRECURSOR_CHARGE));
            Assert.IsNull(EditDriftTimePredictorDlg.ValidateCharge(1));
            Assert.IsNull(EditDriftTimePredictorDlg.ValidateCharge(TransitionGroup.MAX_PRECURSOR_CHARGE));

            string[] values = {"", "", ""};
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateRegressionCellValues(values), 
                string.Format(
                Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_charge__Charges_must_be_integer_values_between_1_and__1__, 
                values[0], TransitionGroup.MAX_PRECURSOR_CHARGE));

            values[0] = "1";
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateRegressionCellValues(values),
                string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_slope_, values[1]));

            values[1] = "1";
            AssertEx.Contains(EditDriftTimePredictorDlg.ValidateRegressionCellValues(values),
                string.Format(Resources.EditDriftTimePredictorDlg_ValidateRegressionCellValues_the_value__0__is_not_a_valid_intercept_, values[2]));

            values[2] = "1";
            Assert.IsNull(EditDriftTimePredictorDlg.ValidateRegressionCellValues(values));

            object[] column = {"1"};
            AssertEx.Contains(CollisionalCrossSectionGridViewDriverBase<ValidatingIonMobilityPeptide>.ValidateRow(column, 1),
               Resources.CollisionalCrossSectionGridViewDriverBase_ValidateRow_The_pasted_text_must_have_two_columns_);

            object[] columns = {"", ""};
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

        /// <summary>
        /// Test the inner workings of ion mobility libraries
        /// </summary>
        [TestMethod]
        public void TestLibIonMobilityInfo()
        {
            var dictCCS1 = new Dictionary<LibKey, IonMobilityInfo[]>();
            var ccs1 = new List<IonMobilityInfo> {new IonMobilityInfo(1, true), new IonMobilityInfo(2, true)}; // Collisional cross sections
            var ccs2 = new List<IonMobilityInfo> {new IonMobilityInfo(3, true), new IonMobilityInfo(4, true)}; // Collisional cross sections
            const string seq1 = "JKLM";
            const string seq2 = "KLMN";
            dictCCS1.Add(new LibKey(seq1,1),ccs1.ToArray());
            dictCCS1.Add(new LibKey(seq2,1),ccs2.ToArray());
            var lib = new List<LibraryIonMobilityInfo> { new LibraryIonMobilityInfo("test", dictCCS1) };

            var peptideTimes = CollisionalCrossSectionGridViewDriver.ProcessIonMobilityValues(null,
                lib, 1, null);
            var validatingIonMobilityPeptides = peptideTimes as ValidatingIonMobilityPeptide[] ?? peptideTimes.ToArray();
            Assert.AreEqual(2, validatingIonMobilityPeptides.Count());
            Assert.AreEqual(1.5, validatingIonMobilityPeptides[0].CollisionalCrossSection);
            Assert.AreEqual(3.5, validatingIonMobilityPeptides[1].CollisionalCrossSection);

            var dictCCS2 = new Dictionary<LibKey, IonMobilityInfo[]>();
            var ccs3 = new List<IonMobilityInfo> { new IonMobilityInfo(4, false), new IonMobilityInfo(5, false) }; // Drift times
            const string seq3 = "KLMNJ";
            dictCCS2.Add(new LibKey(seq3, 1), ccs3.ToArray());
            lib.Add(new LibraryIonMobilityInfo("test2", dictCCS2));
            List<LibraryIonMobilityInfo> lib1 = lib;
            AssertEx.ThrowsException<Exception>(() => CollisionalCrossSectionGridViewDriver.ProcessIonMobilityValues(null,
                lib1, 2, null),
                String.Format(
                        Resources.CollisionalCrossSectionGridViewDriver_ProcessIonMobilityValues_Cannot_import_measured_drift_time_for_sequence__0___no_regression_was_provided_for_charge_state__1__,
                        seq3, 1));

            var regressions = new Dictionary<int, RegressionLine> {{1, new RegressionLine(2, 1)}};
            lib = new List<LibraryIonMobilityInfo> { new LibraryIonMobilityInfo("test", dictCCS2) };
            peptideTimes = CollisionalCrossSectionGridViewDriver.ProcessIonMobilityValues(null,
                            lib, 1, regressions);
            validatingIonMobilityPeptides = peptideTimes as ValidatingIonMobilityPeptide[] ?? peptideTimes.ToArray();
            Assert.AreEqual(1, validatingIonMobilityPeptides.Count());
            Assert.AreEqual(1.75, validatingIonMobilityPeptides[0].CollisionalCrossSection);
        }

    }
}