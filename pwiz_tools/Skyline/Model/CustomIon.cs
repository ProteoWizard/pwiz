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
using System.Globalization;
using System.IO;
using System.Xml;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class CustomIon : Immutable, IValidating
    {
        public const double MAX_MASS = 100000;
        public const double MIN_MASS = MeasuredIon.MIN_REPORTER_MASS;
        private string _formula;
        private string _unlabledFormula;
        
        /// <summary>
        /// A simple object used to represent any molecule
        /// </summary>
        /// <param name="formula">The molecular formula of the molecule</param>
        /// <param name="monoisotopicMass">The monoisotopic mass of the molecule(can be calculated from formula)</param>
        /// <param name="averageMass">The average mass of the molecule (can be calculated by the formula)</param>
        /// <param name="name">The arbitrary name given to this molecule</param>
        protected CustomIon(string formula, double? monoisotopicMass, double? averageMass, string name)
        {
            MonoisotopicMass = monoisotopicMass ?? averageMass ?? 0;
            AverageMass = averageMass ?? monoisotopicMass ?? 0;
            Formula = formula;
            Name = name;

            Validate();
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected CustomIon()
        {
        }

        public string Formula
        { 
            get {return _formula;} 
            private set
            {
                _formula = string.IsNullOrEmpty(value) ? null : value;
                _unlabledFormula = BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(_formula);
                Helpers.AssignIfEquals(ref _unlabledFormula, _formula); // Save some string space if actually unlableled
            }
        }

        public string UnlabeledFormula { get { return _unlabledFormula; } }

        /// <summary>
        /// For matching heavy/light pairs in small molecule documents
        /// </summary>
        public string PrimaryEquivalenceKey { get { return Name; } }
        public string SecondaryEquivalenceKey { get { return UnlabeledFormula; } }

        public string Name { get; protected set; }
        public double MonoisotopicMass { get; private set; }
        public double AverageMass { get; private set; }

        private const string massFormat = "{0} [{1:F06}/{2:F06}]"; // Not L10N

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name))
                    return Name;
                else if (!string.IsNullOrEmpty(Formula))
                    return Formula;
                else
                    return String.Format(massFormat, Resources.CustomIon_DisplayName_Ion, MonoisotopicMass, AverageMass );  
            }
        }

        public string InvariantName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name))
                    return Name;
                else if (!string.IsNullOrEmpty(Formula))
                    return Formula;
                else
                    return String.Format(CultureInfo.InvariantCulture, massFormat, "Ion", MonoisotopicMass, AverageMass);  // Not L10N
            }
        }

        public double GetMass(MassType massType)
        {
            return (massType == MassType.Average) ? AverageMass : MonoisotopicMass;
        }

        public void Validate()
        {
            if (!string.IsNullOrEmpty(Formula))
            {
                try
                {
                    MonoisotopicMass = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, Formula);
                    AverageMass = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, Formula);
                }
                catch (ArgumentException x)
                {
                    throw new InvalidDataException(x.Message, x);  // Pass original as inner exception
                }
            }
            if (AverageMass == 0 || MonoisotopicMass == 0)
                throw new InvalidDataException(Resources.CustomIon_Validate_Custom_ions_must_specify_a_formula_or_valid_monoisotopic_and_average_masses_);
            if(AverageMass > MAX_MASS || MonoisotopicMass > MAX_MASS)
                throw new InvalidDataException(string.Format(Resources.CustomIon_Validate_The_mass_of_the_custom_ion_exceeeds_the_maximum_of__0_,MAX_MASS));
            if(AverageMass < MIN_MASS || MonoisotopicMass < MIN_MASS)
                throw new InvalidDataException(string.Format(Resources.CustomIon_Validate_The_mass_of_the_custom_ion_is_less_than_the_minimum_of__0__,MIN_MASS));
        }

        private bool Equals(CustomIon other)
        {
            var equal = string.Equals(Formula, other.Formula) &&
                string.Equals(Name, other.Name) &&
                MonoisotopicMass.Equals(other.MonoisotopicMass) &&
                AverageMass.Equals(other.AverageMass);
            return equal; // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CustomIon) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Formula != null ? Formula.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ MonoisotopicMass.GetHashCode();
                hashCode = (hashCode*397) ^ AverageMass.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// For use in heavy/light matching, where formula or name is only reliable match value
        /// Without that we use transition list mz sort order
        /// </summary>
        public static bool Equivalent(CustomIon ionA, CustomIon ionB)
        {
            if (Equals(ionA, ionB))
                return true;
            if (ionA == null || ionB == null)
                return false; // One null, one non-null
            // Name
            if (ionA.PrimaryEquivalenceKey != null || ionB.PrimaryEquivalenceKey != null)
                return Equals(ionA.PrimaryEquivalenceKey, ionB.PrimaryEquivalenceKey);
            // Formula (stripped of labels)
            if (ionA.SecondaryEquivalenceKey != null || ionB.SecondaryEquivalenceKey != null)
                return Equals(ionA.SecondaryEquivalenceKey, ionB.SecondaryEquivalenceKey);
            return true; // Not proven to be unequivalent - it's up to caller to think about mz
        }

        public int GetEquivalentHashCode()
        {
            if (!string.IsNullOrEmpty(PrimaryEquivalenceKey))
                return PrimaryEquivalenceKey.GetHashCode();
            if (!string.IsNullOrEmpty(SecondaryEquivalenceKey))
                return SecondaryEquivalenceKey.GetHashCode();
            return 0;
        }

        protected enum ATTR
        {
            name, //For Measured ion as in reporter ions
            custom_ion_name, // For custom ion as found in molecule list
            formula, // Obsolete - but this is a cue that the formula is missing a hydrogen, since we used to assume M+H ionization
            ion_formula, 
            mass_monoisotopic,
            mass_average
        } 

        protected virtual void ReadAttributes(XmlReader reader)
        {
            Formula = reader.GetAttribute(ATTR.formula);
            if (!string.IsNullOrEmpty(Formula))
            {
                Formula = BioMassCalc.AddH(Formula);  // Update this old style formula to current by adding the hydrogen we formerly left out due to assuming protonation
            }
            else
            {
                Formula = reader.GetAttribute(ATTR.ion_formula);
            }
            if (string.IsNullOrEmpty(Formula))
            {
                AverageMass = reader.GetDoubleAttribute(ATTR.mass_average);
                MonoisotopicMass = reader.GetDoubleAttribute(ATTR.mass_monoisotopic);
            }
            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            if (Formula != null)
            {
                writer.WriteAttribute(ATTR.ion_formula, Formula);
                writer.WriteAttributeNullable(ATTR.mass_average, AverageMass);
                writer.WriteAttributeNullable(ATTR.mass_monoisotopic, MonoisotopicMass);
            }
            else
            {
                // Without a formula we can't rederive masses, so write at higher precision
                writer.WriteAttributeNullableRoundTrip(ATTR.mass_average, AverageMass);
                writer.WriteAttributeNullableRoundTrip(ATTR.mass_monoisotopic, MonoisotopicMass);
            }
            if (Name != null)
                writer.WriteAttribute(ATTR.custom_ion_name, Name);
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public static bool IsValidLibKey(string key)
        {
            try
            {
                SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, key);
            }
            catch
            {
                return false;
            }
            return true;
        }
    }

    public class DocNodeCustomIon : CustomIon
    {
        public DocNodeCustomIon(string formula, string name = null)
            : this(formula, null, null, name)
        {
        }

        public DocNodeCustomIon(double monoisotopicMass, double averageMass, string name = null)
            : this(null, monoisotopicMass, averageMass, name)
        {
        }

        public DocNodeCustomIon(string formula, double? monoisotopicMass, double? averageMass, string name = null)
            : base(formula, monoisotopicMass, averageMass, name)
        {
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected DocNodeCustomIon()
        {
        }

        public static DocNodeCustomIon Deserialize(XmlReader reader)
        {
            DocNodeCustomIon ion = new DocNodeCustomIon();
            ion.ReadAttributes(reader);
            return ion;
        }

        protected override void ReadAttributes(XmlReader reader)
        {
            base.ReadAttributes(reader);

            Name = reader.GetAttribute(ATTR.custom_ion_name);
        }
    }

    /// <summary>
    /// Special subclass of custom ion for use in settings
    /// For use as a reference in a document, and not to be edited.
    /// Returns false for IsEditableInstance.
    /// </summary>

    public class SettingsCustomIon : CustomIon
    {
        public SettingsCustomIon(string formula, double? monoisotopicMass, double? averageMass, string name)
            : base(formula, monoisotopicMass, averageMass, name)
        {
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected SettingsCustomIon()
        {
        }

        public static SettingsCustomIon Deserialize(XmlReader reader)
        {
            SettingsCustomIon ion = new SettingsCustomIon() ;
            ion.ReadAttributes(reader);
            return ion;
        }

        protected override void ReadAttributes(XmlReader reader)
        {
            base.ReadAttributes(reader);

            Name = reader.GetAttribute(ATTR.name);
        }
    }
}
