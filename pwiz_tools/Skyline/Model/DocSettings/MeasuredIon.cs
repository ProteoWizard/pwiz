/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Expresses a special type of ion that should always be measured.  Two different types
    /// of ions are covered:
    /// <list type="numbered">
    /// <item>Special fragment ions, which use terms similar to enzyme cleave to express
    /// where they occur in peptides</item>
    /// <item>Reporter ions, which can be free form chemical formulas or even constant masses</item>
    /// </list>
    /// </summary>
    [XmlRoot("measured_ion")]
    public sealed class MeasuredIon : XmlNamedElement
    {
        public const int DEFAULT_MIN_FRAGMENT_LENGTH = 3;
        public const int MIN_MIN_FRAGMENT_LENGTH = 1;
        public const int MAX_MIN_FRAGMENT_LENGTH = 10;

        public const double MIN_REPORTER_MASS = 5;
        public const double MAX_REPORTER_MASS = 5000;

        private SettingsCustomIon SettingsCustomIon { get; set; }

        /// <summary>
        /// Constructor for a special fragment
        /// </summary>
        /// <param name="name">The name by which it is stored in the settings list</param>
        /// <param name="fragment">Amino acid residues with weak bonds</param>
        /// <param name="restrict">Adjacent amino acids that form stronger bonds</param>
        /// <param name="terminus">Terminal side which has the weak bond</param>
        /// <param name="minFragmentLength">Minimum length to allow as a special fragment</param>
        public MeasuredIon(string name,
                           string fragment,
                           string restrict,
                           SequenceTerminus terminus,
                           int minFragmentLength)
            : base(name)
        {
            Fragment = fragment;
            Restrict = restrict;
            Terminus = terminus;
            MinFragmentLength = minFragmentLength;
            Charge = 1;
            Validate();
        }

        /// <summary>
        /// Constructor for a reporter ion
        /// </summary>
        /// <param name="name">The name by which it is stored in the settings list</param>
        /// <param name="formula">Chemical formula of the ion</param>
        /// <param name="monoisotopicMass">Constant monoisotopic mass of the ion, if formula is not given</param>
        /// <param name="averageMass">Constant average mass of the ion, if formula is not given</param>
        /// <param name="charge">The charge for this custom ion</param>
        /// <param name="isOptional">Whether or not the reporter ion will automatically be added to all proteins</param>
        public MeasuredIon(string name,
            string formula,
            double? monoisotopicMass,
            double? averageMass,
            int charge,
            bool isOptional = false)
            : base(name)
        {
            SettingsCustomIon = new SettingsCustomIon(formula, monoisotopicMass, averageMass, name); // Like a custom ion, but returns false for IsEditableInstance
            Charge = charge;
            IsOptional = isOptional;
            Validate();
        }

        /// <summary>
        /// Set of amino acid residues with an especially weak bond, causing
        /// them to be highly expressed during fragmentation.
        /// </summary>
        public string Fragment { get; private set; }

        /// <summary>
        /// Set of amino acid residues that bond more tightly to the <see cref="Fragment"/>
        /// residues, causing the fragment not to be so highly expressed.
        /// </summary>
        public string Restrict { get; private set; }

        /// <summary>
        /// The terminus (n- or c-) side of the <see cref="Fragment"/> amino acid residues
        /// that has the weak bond.  (e.g. n-terminal proline)
        /// </summary>
        public SequenceTerminus? Terminus { get; private set; }

        /// <summary>
        /// Minimum length allowed for special fragments to avoid choosing fragments with
        /// extremely low specificity.  (e.g. y2 for n-terminal proline and tryptic digestion
        /// is either PR or PK)
        /// </summary>
        public int? MinFragmentLength { get; private set; }
        public bool IsNTerm() { return Terminus.HasValue && Terminus.Value == SequenceTerminus.N; }
        public bool IsCTerm() { return Terminus.HasValue && Terminus.Value == SequenceTerminus.C; }

        public bool IsMatch(string sequence, IonType ionType, int cleavageOffset)
        {
            if (!IsFragment)
                return false;
            int ordinal = Transition.OffsetToOrdinal(ionType, cleavageOffset, sequence.Length);
            if (ordinal < MinFragmentLength)
                return false;
            char aaN = Transition.GetFragmentNTermAA(sequence, cleavageOffset);
            char aaC = Transition.GetFragmentCTermAA(sequence, cleavageOffset);
            // Make sure the specified amino acid is in the fragment set for this ion
            char aa = (IsNTerm() ? aaN : aaC);
            if (Fragment.IndexOf(aa) == -1)
                return false;
            // Make suer the adjacent amino acid is not in the restricted set for this ion
            aa = (IsNTerm() ? aaC : aaN);
            if (Restrict != null && Restrict.IndexOf(aa) != -1)
                return false;
            return true;
        }

        public int Charge { get; private set; }

        public bool IsFragment { get { return Fragment != null; } }
        public bool IsCustom { get { return !IsFragment; } }

        public MeasuredIonType MeasuredIonType
        {
            get { return IsFragment ? MeasuredIonType.fragment : MeasuredIonType.reporter; }
        }

        public bool IsOptional { get; private set; }

        public CustomIon CustomIon { get { return SettingsCustomIon;  } }

        #region Property change methods

        public MeasuredIon ChangeIsOptional(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsOptional = prop);
        }

        #endregion

        #region Implementation of IXmlSerializable


        /// <summary>
        /// For serialization
        /// </summary>
        private MeasuredIon()
        {
            Charge = 1;
        }

        private enum ATTR
        {
            // Fragment
            cut,
            no_cut,
            sense,
            min_length,
            // Reporter
            // formula,  moved to CustomIon
            ion_formula,
            mass_monoisotopic,
            mass_average,
            charges,  // Backward compatibility with v2.6
            charge,
            optional
        }

        private void Validate()
        {
            TransitionFilter.ValidateCharges(Resources.TransitionFilter_ProductCharges_Product_ion_charges, new[] { Charge },
                Transition.MIN_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);

            if (IsFragment)
            {
                if (string.IsNullOrEmpty(Fragment))
                    throw new InvalidDataException(Resources.MeasuredIon_Validate_Special_fragment_ions_must_have_at_least_one_fragmentation_residue);
                AminoAcid.ValidateAAList(Fragment);
                if (!string.IsNullOrEmpty(Restrict))
                    AminoAcid.ValidateAAList(Restrict);
                if (!Terminus.HasValue)
                    throw new InvalidDataException(Resources.MeasuredIon_Validate_Special_fragment_ions_must_specify_the_terminal_side_of_the_amino_acid_residue_on_which_fragmentation_occurs);
                if (MIN_MIN_FRAGMENT_LENGTH > MinFragmentLength || MinFragmentLength > MAX_MIN_FRAGMENT_LENGTH)
                {
                    throw new InvalidDataException(string.Format(Resources.MeasuredIon_Validate_The_minimum_length__0__must_be_between__1__and__2__,
                                                                 MinFragmentLength, MIN_MIN_FRAGMENT_LENGTH, MAX_MIN_FRAGMENT_LENGTH));
                }
            }
            else
            {
                if (SettingsCustomIon.MonoisotopicMass == 0 || SettingsCustomIon.AverageMass == 0)
                    throw new InvalidDataException(Resources.MeasuredIon_Validate_Reporter_ions_must_specify_a_formula_or_valid_monoisotopic_and_average_masses);
                if (SettingsCustomIon.MonoisotopicMass < MIN_REPORTER_MASS || SettingsCustomIon.AverageMass < MIN_REPORTER_MASS)
                    throw new InvalidDataException(string.Format(Resources.MeasuredIon_Validate_Reporter_ion_masses_must_be_greater_than_or_equal_to__0__, MIN_REPORTER_MASS));
                if (SettingsCustomIon.MonoisotopicMass > MAX_REPORTER_MASS || SettingsCustomIon.AverageMass > MAX_REPORTER_MASS)
                    throw new InvalidDataException(string.Format(Resources.MeasuredIon_Validate_Reporter_ion_masses_must_be_less_than_or_equal_to__0__, MAX_REPORTER_MASS));
            }
        }

        private static SequenceTerminus ToSeqTerminus(string value)
        {
            return (SequenceTerminus)Enum.Parse(typeof(SequenceTerminus), value, true);
        }

        public static MeasuredIon Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MeasuredIon());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Fragment = reader.GetAttribute(ATTR.cut);
            if (IsFragment)
            {
                Restrict = reader.GetAttribute(ATTR.no_cut);
                Terminus = reader.GetAttribute(ATTR.sense, ToSeqTerminus);
                MinFragmentLength = reader.GetNullableIntAttribute(ATTR.min_length) ??
                    DEFAULT_MIN_FRAGMENT_LENGTH;
            }
            else
            {
                var charges = TextUtil.ParseInts(reader.GetAttribute(ATTR.charges)); // Old version?
                if (charges.Count() > 1)
                    throw new InvalidDataException(Resources.MeasuredIon_ReadXml_Multiple_charge_states_for_custom_ions_are_no_longer_supported_);
                SettingsCustomIon = SettingsCustomIon.Deserialize(reader);
                if (charges.Any())  // Old style - fix it up a little for our revised ideas about custom ion ionization
                {
                    Charge = charges[0];
                    if (string.IsNullOrEmpty(SettingsCustomIon.Formula)) // Adjust the user-supplied masses
                    {
                        SettingsCustomIon = new SettingsCustomIon(SettingsCustomIon.Formula,
                            Math.Round(SettingsCustomIon.MonoisotopicMass + BioMassCalc.MONOISOTOPIC.GetMass(BioMassCalc.H), SequenceMassCalc.MassPrecision), // Assume user provided neutral mass.  Round new value easiest XML roundtripping.
                            Math.Round(SettingsCustomIon.AverageMass + BioMassCalc.AVERAGE.GetMass(BioMassCalc.H), SequenceMassCalc.MassPrecision), // Assume user provided neutral mass.  Round new value easiest XML roundtripping.
                            SettingsCustomIon.Name);
                    }
                }
                else
                {
                    Charge = reader.GetIntAttribute(ATTR.charge);
                }
                IsOptional = reader.GetBoolAttribute(ATTR.optional);
            }
            // Consume tag
            reader.Read();

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            if (IsFragment)
            {
                writer.WriteAttributeString(ATTR.cut, Fragment);
                writer.WriteAttributeIfString(ATTR.no_cut, Restrict);
                writer.WriteAttributeNullable(ATTR.sense, Terminus);
                writer.WriteAttributeNullable(ATTR.min_length, MinFragmentLength);
            }
            else
            {
                writer.WriteAttributeIfString(ATTR.ion_formula, SettingsCustomIon.Formula);
                // Masses are information only, if their is a formula, but Panorama may need these
                writer.WriteAttribute(ATTR.mass_monoisotopic, SettingsCustomIon.MonoisotopicMass);
                writer.WriteAttribute(ATTR.mass_average, SettingsCustomIon.AverageMass);
                writer.WriteAttributeString(ATTR.charge, Charge.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttribute(ATTR.optional, IsOptional);
            }
        }

        #endregion

        #region object overrides

        public bool Equals(MeasuredIon other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(other.Fragment, Fragment) &&
                   Equals(other.Restrict, Restrict) &&
                   other.Terminus.Equals(Terminus) &&
                   other.MinFragmentLength.Equals(MinFragmentLength) &&
                   Equals(other.SettingsCustomIon, SettingsCustomIon) &&
                   Equals(other.Charge, Charge) &&
                   other.IsOptional == IsOptional;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as MeasuredIon);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (Fragment != null ? Fragment.GetHashCode() : 0);
                result = (result*397) ^ (Restrict != null ? Restrict.GetHashCode() : 0);
                result = (result*397) ^ (Terminus.HasValue ? Terminus.Value.GetHashCode() : 0);
                result = (result*397) ^ (MinFragmentLength.HasValue ? MinFragmentLength.Value : 0);
                result = (result*397) ^ (SettingsCustomIon != null ? SettingsCustomIon.GetHashCode() : 0);
                result = (result*397) ^ Charge;
                result = (result*397) ^ IsOptional.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    public enum MeasuredIonType { fragment, reporter }
}
