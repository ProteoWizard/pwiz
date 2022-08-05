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

using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.IonMobility
{
    /// <summary>
    /// A ValidatingIonMobilityPrecursor consists of an ion and a single ion mobility.
    /// In the UI, the multiple conformer case is handled by two lines with the same ion
    /// and different ion mobilities.
    /// </summary>
    public class ValidatingIonMobilityPrecursor : DbPrecursorAndIonMobility
    {
        public ValidatingIonMobilityPrecursor(Target target, Adduct precursorAdduct, IonMobilityAndCCS ionMobility) :
            base(new DbPrecursorIon(target, precursorAdduct), 
                ionMobility.CollisionalCrossSectionSqA, ionMobility.IonMobility.Mobility??0, ionMobility.IonMobility.Units, ionMobility.HighEnergyIonMobilityValueOffset)
        {
        }

        public ValidatingIonMobilityPrecursor(Target seq,
            Adduct precursorAdduct,
            double collisionalCrossSection,
            double ionMobility,
            double highEnergyIonMobilityOffset,
            eIonMobilityUnits units)
            : base(new DbPrecursorIon(seq, precursorAdduct), collisionalCrossSection, ionMobility, units, highEnergyIonMobilityOffset)
        {
        }

        public ValidatingIonMobilityPrecursor(SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes,
            Adduct precursorAdduct,
            double collisionalCrossSection,
            double ionMobility,
            double highEnergyIonMobilityOffset,
            eIonMobilityUnits units)
            : base(new DbPrecursorIon(new Target(smallMoleculeLibraryAttributes), precursorAdduct),
                collisionalCrossSection, ionMobility, units, highEnergyIonMobilityOffset)
        {
        }

        public ValidatingIonMobilityPrecursor(DbPrecursorAndIonMobility value)
            : this(value.DbPrecursorIon.GetTarget(), value.DbPrecursorIon.GetPrecursorAdduct(), value.GetIonMobilityAndCCS())
        {
        }

        public ValidatingIonMobilityPrecursor(ValidatingIonMobilityPrecursor other)
            : base(other)
        {
        }

        public LibKey Precursor
        {
            get { return base.GetLibKey(); }
        }

        public string Validate()
        {
            if (base.DbPrecursorIon.IsEmpty)
            {
                return Resources.ValidatingIonMobilityPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry_;
            }
            var messages = new List<string>();
            string result;
            if ((result = ValidateSequence(Precursor.Target)) != null)
                messages.Add(result);
            if ((result = ValidateAdduct(Precursor.Adduct)) != null)
                messages.Add(result);
            if (CollisionalCrossSectionSqA == 0 && (IonMobilityNullable == null || IonMobility == 0))
                messages.Add(Resources.ValidatingIonMobilityPeptide_Validate_No_ion_mobility_information_found);
            if (CollisionalCrossSectionSqA < 0)
                messages.Add(Resources.ValidatingIonMobilityPeptide_ValidateCollisionalCrossSection_Measured_collisional_cross_section_values_must_be_valid_decimal_numbers_greater_than_zero_);
            if (IonMobility!= 0 && IonMobilityUnits == eIonMobilityUnits.none)
                messages.Add(Resources.ValidatingIonMobilityPeptide_Validate_No_ion_mobility_units_found);
            return messages.Count > 0 ? TextUtil.LineSeparate(messages) : null;
        }

        public static string ValidateSequence(Target sequence)
        {
            if (sequence.IsEmpty)
                return Resources.ValidatingIonMobilityPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry_;
            if (sequence.IsProteomic && !FastaSequence.IsExSequence(sequence.Sequence))
                return string.Format(Resources.ValidatingIonMobilityPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, sequence);
            return null;
        }

        public static string ValidateAdduct(Adduct adduct)
        {
            if (adduct == null || adduct.IsEmpty)
                return Resources.ValidatingIonMobilityPeptide_ValidateAdduct_A_valid_adduct_description__e_g____M_H____must_be_provided_;
            return null;
        }

        public override string ToString() // For debug convenience
        {
            return string.Format(@"{0} ccs={1} im={2}{3}", Precursor.ToString() , CollisionalCrossSectionSqA, IonMobility, IonMobilityUnits);
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return null;
        }
    }

    /// <summary>
    /// A PrecursorIonMobilities object consists of a single precursor ion, and one
    /// or more IonMobilityAndCCS objects (this supports the "multiple conformer" case
    /// where an ion is likely to have more than one collisional cross section due to folding etc.)
    /// </summary>
    public class PrecursorIonMobilities
    {
        // Multiple conformer ctor
        public PrecursorIonMobilities(Target target, Adduct precursorAdduct, IEnumerable<IonMobilityAndCCS> ionMobilities)
        {
            Target = target;
            PrecursorAdduct = precursorAdduct;
            IonMobilities = ionMobilities.ToList();
        }

        // Single conformer ctor
        public PrecursorIonMobilities(Target target, 
            Adduct precursorAdduct, 
            double collisionalCrossSection, 
            double ionMobility, 
            double highEnergyIonMobilityOffset, 
            eIonMobilityUnits units)
            : this(target, precursorAdduct, 
                IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, units, collisionalCrossSection, highEnergyIonMobilityOffset))
        {
        }

        // Single conformer ctor
        public PrecursorIonMobilities(SmallMoleculeLibraryAttributes smallMoleculeLibraryAttributes,
            Adduct precursorAdduct,
            double collisionalCrossSection,
            double ionMobility,
            double highEnergyIonMobilityOffset,
            eIonMobilityUnits units)
            : this(new Target(smallMoleculeLibraryAttributes), precursorAdduct,
                IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, units, collisionalCrossSection, highEnergyIonMobilityOffset))
        {
        }

        // Single conformer ctor
        public PrecursorIonMobilities(Target target, Adduct precursorAdduct, IonMobilityAndCCS im)
            : this(target, precursorAdduct, new List<IonMobilityAndCCS>() { im })
        {
        }

        public PrecursorIonMobilities(PrecursorIonMobilities other)
            : this(other.Target, other.PrecursorAdduct, other.IonMobilities)
        {
        }

        public Target Target { get; set; }
        public Adduct PrecursorAdduct { get; private set; }
        public IList<IonMobilityAndCCS> IonMobilities { get; private set; } // Allow for multiple conformers

        public string Validate()
        {
            var messages = new List<string>();
            string result;
            if ((result = ValidateSequence(Target)) != null)
                messages.Add(result);
            if ((result = ValidateAdduct(PrecursorAdduct)) != null)
                messages.Add(result);
            if (IonMobilities == null || IonMobilities.Count == 0)
                messages.Add(Resources.ValidatingIonMobilityPeptide_Validate_No_ion_mobility_information_found);
            else foreach(var im in IonMobilities)
            {
                if ((im.CollisionalCrossSectionSqA??0) == 0 && (im.IonMobility.Mobility??0) == 0)
                    messages.Add(Resources.ValidatingIonMobilityPeptide_Validate_No_ion_mobility_information_found);
                if ((im.CollisionalCrossSectionSqA ?? 0) < 0)
                    messages.Add(Resources.ValidatingIonMobilityPeptide_ValidateCollisionalCrossSection_Measured_collisional_cross_section_values_must_be_valid_decimal_numbers_greater_than_zero_);
                if ((im.IonMobility.Mobility ?? 0) != 0 && im.IonMobility.Units == eIonMobilityUnits.none)
                    messages.Add(Resources.ValidatingIonMobilityPeptide_Validate_No_ion_mobility_units_found);
            }
            return messages.Count > 0 ? TextUtil.LineSeparate(messages) : null;
        }

        public static string ValidateSequence(Target sequence)
        {
            if (sequence.IsEmpty)
                return Resources.ValidatingIonMobilityPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry_;
            if (sequence.IsProteomic && !FastaSequence.IsExSequence(sequence.Sequence))
                return string.Format(Resources.ValidatingIonMobilityPeptide_ValidateSequence_The_sequence__0__is_not_a_valid_modified_peptide_sequence_, sequence);
            return null;
        }

        public static string ValidateAdduct(Adduct adduct)
        {
            if (adduct == null || adduct.IsEmpty)
                return Resources.ValidatingIonMobilityPeptide_ValidateAdduct_A_valid_adduct_description__e_g____M_H____must_be_provided_;
            return null;
        }

    }
}