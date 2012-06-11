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
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    /// <summary>
    /// Expresses a special type of ion that should always be measured.  Two different types
    /// of ions are covered:
    /// <list type="numbered">
    /// <item>Special fragment ions, which use terms similar to enzyme cleave to express
    /// where they occur in peptids</item>
    /// <item>Reporter ions, which can be free form chemical formulas or even constant masses</item>
    /// </list>
    /// </summary>
    [XmlRoot("measured_ion")]
    public sealed class MeasuredIon : XmlNamedElement
    {
        public const int DEFAULT_MIN_FRAGMENT_LENGTH = 3;
        public const int MIN_MIN_FRAGMENT_LENGTH = 1;
        public const int MAX_MIN_FRAGMENT_LENGTH = 10;

        public const double MIN_REPORTER_MASS = 50;
        public const double MAX_REPORTER_MASS = 300;

        private string _formula;

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

            Validate();
        }

        /// <summary>
        /// Constructor for a reporter ion
        /// </summary>
        /// <param name="name">The name by which it is stored in the settings list</param>
        /// <param name="formula">Chemical formula of the ion</param>
        /// <param name="monoisotopicMass">Constant monoisotopic mass of the ion, if formula is not given</param>
        /// <param name="averageMass">Constant average mass of the ion, if formula is not given</param>
        public MeasuredIon(string name,
                           string formula,
                           double? monoisotopicMass,
                           double? averageMass)
            : base(name)
        {
            MonoisotopicMass = monoisotopicMass;
            AverageMass = averageMass;
            Formula = formula;

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

        /// <summary>
        /// Chemical formula for a constant reporter ion
        /// </summary>
        public string Formula
        {
            get { return _formula; }
            private set
            {
                _formula = value;
                if (_formula != null)
                {
                    MonoisotopicMass = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, Formula);
                    AverageMass = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, Formula);
                }
            }
        }

        public double? MonoisotopicMass { get; private set; }
        public double? AverageMass { get; private set; }

        public bool IsFragment { get { return Fragment != null; } }
        public bool IsReporter { get { return !IsFragment; } }

        public MeasuredIonType MeasuredIonType
        {
            get { return IsFragment ? MeasuredIonType.fragment : MeasuredIonType.reporter; }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private MeasuredIon()
        {
        }

        private enum ATTR
        {
            // Fragment
            cut,
            no_cut,
            sense,
            min_length,
            // Reporter
            formula,
            mass_monoisotopic,
            mass_average
        }

        private void Validate()
        {
            if (IsFragment)
            {
                if (string.IsNullOrEmpty(Fragment))
                    throw new InvalidDataException("Special fragment ions must have at least one fragmentation residue.");
                AminoAcid.ValidateAAList(Fragment);
                if (!string.IsNullOrEmpty(Restrict))
                    AminoAcid.ValidateAAList(Restrict);
                if (!Terminus.HasValue)
                    throw new InvalidDataException("Special fragment ions must specify the terminal side of the amino acid residue on which fragmentation occurs.");
                if (MIN_MIN_FRAGMENT_LENGTH > MinFragmentLength || MinFragmentLength > MAX_MIN_FRAGMENT_LENGTH)
                {
                    throw new InvalidDataException(string.Format("The minimum length {0} must be between {1} and {2}.",
                        MinFragmentLength, MIN_MIN_FRAGMENT_LENGTH, MAX_MIN_FRAGMENT_LENGTH));
                }
            }
            else
            {
                if (MonoisotopicMass == 0 || AverageMass == 0)
                    throw new InvalidDataException("Reporter ions must specify a formula or valid monoisotopic and average masses.");
                if (MonoisotopicMass < MIN_REPORTER_MASS || AverageMass < MIN_REPORTER_MASS)
                    throw new InvalidDataException(string.Format("Reporter ion masses must be greater than or equal to {0}.", MIN_REPORTER_MASS));
                if (MonoisotopicMass > MAX_REPORTER_MASS || AverageMass > MAX_REPORTER_MASS)
                    throw new InvalidDataException(string.Format("Reporter ion masses must be less than or equal to {0}.", MAX_REPORTER_MASS));
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
                MonoisotopicMass = reader.GetNullableDoubleAttribute(ATTR.mass_monoisotopic) ?? 0;
                AverageMass = reader.GetNullableDoubleAttribute(ATTR.mass_average) ?? 0;
                Formula = reader.GetAttribute(ATTR.formula);
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
                writer.WriteAttributeIfString(ATTR.formula, Formula);
                if (Formula == null)
                {
                    writer.WriteAttribute(ATTR.mass_monoisotopic, MonoisotopicMass);
                    writer.WriteAttribute(ATTR.mass_average, AverageMass);
                }                
            }
        }

        #endregion

        #region object overrides

        public bool Equals(MeasuredIon other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                Equals(other._formula, _formula) &&
                Equals(other.Fragment, Fragment) &&
                Equals(other.Restrict, Restrict) &&
                other.Terminus.Equals(Terminus) &&
                other.MinFragmentLength.Equals(MinFragmentLength) &&
                other.MonoisotopicMass.Equals(MonoisotopicMass) &&
                other.AverageMass.Equals(AverageMass);
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
                result = (result*397) ^ (_formula != null ? _formula.GetHashCode() : 0);
                result = (result*397) ^ (Fragment != null ? Fragment.GetHashCode() : 0);
                result = (result*397) ^ (Restrict != null ? Restrict.GetHashCode() : 0);
                result = (result*397) ^ (Terminus.HasValue ? Terminus.Value.GetHashCode() : 0);
                result = (result*397) ^ (MinFragmentLength.HasValue ? MinFragmentLength.Value : 0);
                result = (result*397) ^ (MonoisotopicMass.HasValue ? MonoisotopicMass.Value.GetHashCode() : 0);
                result = (result*397) ^ (AverageMass.HasValue ? AverageMass.Value.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

    public enum MeasuredIonType { fragment, reporter }
}
