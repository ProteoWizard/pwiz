/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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

using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.IonMobility
{
    public class ValidatingIonMobilityPeptide : DbIonMobilityPeptide
    {
        public ValidatingIonMobilityPeptide(LibKey libKey, IonMobilityAndCCS im)
            : this(libKey.SmallMoleculeLibraryAttributes, libKey.Adduct, 
                im.CollisionalCrossSectionSqA??0, im.IonMobility.Mobility??0, im.HighEnergyIonMobilityValueOffset, im.IonMobility.Units)
        {
        }

        public ValidatingIonMobilityPeptide(Target seq, Adduct precursorAdduct, double ccs, double ionMobility, double highEnergyIonMobilityOffset, eIonMobilityUnits units)
            : base(seq, precursorAdduct, ccs, ionMobility, highEnergyIonMobilityOffset, units)
        {
        }

        public ValidatingIonMobilityPeptide(SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes,
            Adduct precursorAdduct,
            double collisionalCrossSection,
            double ionMobility,
            double highEnergyIonMobilityOffset,
            eIonMobilityUnits units)
            : base(smallMoleculeLibraryAttributes, precursorAdduct, 
                collisionalCrossSection, ionMobility, highEnergyIonMobilityOffset, units)
        {
        }
        
        public ValidatingIonMobilityPeptide(DbIonMobilityPeptide other)
            : base(other)
        {
        }

        public string Validate()
        {
            return ValidateSequence(Target) ?? ValidateCollisionalCrossSection(CollisionalCrossSection) ?? ValidateAdduct(PrecursorAdduct);
        }

        public static string ValidateSequence(Target sequence)
        {
            if (sequence.IsEmpty)
                return Resources.ValidatingIonMobilityPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry_;
            if (sequence.IsProteomic && !FastaSequence.IsExSequence(sequence.Sequence))
                return string.Format(Resources.ValidatingIonMobilityPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, sequence);
            return null;
        }

        public static string ValidateCollisionalCrossSection(string ccsText)
        {
            double ccsValue;
            if (ccsText == null || !double.TryParse(ccsText, out ccsValue))
                ccsValue = 0;
            return ValidateCollisionalCrossSection(ccsValue);
        }

        public static string ValidateCollisionalCrossSection(double ccsValue)
        {
            if (ccsValue <= 0)
                return Resources.ValidatingIonMobilityPeptide_ValidateCollisionalCrossSection_Measured_collisional_cross_section_values_must_be_valid_decimal_numbers_greater_than_zero_;
            return null;
        }

        public static string ValidateAdduct(string adductText)
        {
            Adduct adduct;
            if (!Adduct.TryParse(adductText, out adduct))
                return Resources.ValidatingIonMobilityPeptide_ValidateAdduct_A_valid_adduct_description__e_g____M_H____must_be_provided_;
            return null;
        }

        public static string ValidateHighEnergyIonMobilityOffset(string offsetText)
        {
            double offsetValue;
            if (!string.IsNullOrEmpty(offsetText) && !double.TryParse(offsetText, out offsetValue))
                return Resources.ValidatingIonMobilityPeptide_ValidateHighEnergyIonMobilityOffset_High_energy_ion_mobility_offsets_should_be_empty__or_express_an_offset_value_for_ion_mobility_in_high_collision_energy_scans_which_may_add_velocity_to_ions_;
            return null;
        }

    }
}