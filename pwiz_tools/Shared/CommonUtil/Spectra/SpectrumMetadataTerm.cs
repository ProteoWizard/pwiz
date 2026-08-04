/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

namespace pwiz.Common.Spectra
{
    /// <summary>
    /// A single mzML controlled-vocabulary (or user) parameter that Skyline reads
    /// from a spectrum but does not interpret into one of its own typed fields.
    /// These otherwise-dropped terms are carried on <see cref="SpectrumMetadata"/>
    /// so they can be shown to the user (and, eventually, filtered on).
    /// </summary>
    public class SpectrumMetadataTerm
    {
        public SpectrumMetadataTerm(string accession, string name, string value, string unit,
            string unitAccession = null, string definition = null)
        {
            Accession = accession;
            Name = name;
            Value = value;
            Unit = unit;
            UnitAccession = unitAccession;
            Definition = definition;
        }

        /// <summary>
        /// The CV accession (e.g. "MS:1000505"), or the user-param name when the
        /// term has no controlled-vocabulary accession.
        /// </summary>
        public string Accession { get; }

        /// <summary>
        /// Human-readable term name resolved from the controlled vocabulary (e.g.
        /// "base peak intensity"). Falls back to <see cref="Accession"/> for user params.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The raw value as reported in the file, kept as text (numeric vs string
        /// interpretation happens at filter time).
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Human-readable unit label (e.g. "number of detector counts"), or null
        /// when the term carries no unit.
        /// </summary>
        public string Unit { get; }

        /// <summary>
        /// The unit's CV accession (e.g. "MS:1000040" for m/z), or null when the term
        /// carries no unit. This, rather than the English <see cref="Unit"/> label, is what
        /// display code keys on to recognize a unit it has a formatting convention for.
        /// </summary>
        public string UnitAccession { get; }

        /// <summary>
        /// The controlled-vocabulary definition of the term (e.g. "The intensity of the
        /// greatest peak in the mass spectrum."), shown as explanatory help text. Null for
        /// user params, which have no ontology definition.
        /// </summary>
        public string Definition { get; }

        protected bool Equals(SpectrumMetadataTerm other)
        {
            return Accession == other.Accession && Name == other.Name && Value == other.Value &&
                   Unit == other.Unit && UnitAccession == other.UnitAccession && Definition == other.Definition;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SpectrumMetadataTerm) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Accession != null ? Accession.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Value != null ? Value.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Unit != null ? Unit.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (UnitAccession != null ? UnitAccession.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Definition != null ? Definition.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return Unit == null ? Name + @": " + Value : Name + @": " + Value + @" " + Unit;
        }
    }
}
