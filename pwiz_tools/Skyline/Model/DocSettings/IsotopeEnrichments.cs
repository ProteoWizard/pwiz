/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("isotope_enrichments")]
    public sealed class IsotopeEnrichments : XmlNamedElement, IValidating
    {
        public static readonly IsotopeEnrichments DEFAULT = new IsotopeEnrichments(Resources.IsotopeEnrichments_DEFAULT_Default,
            BioMassCalc.HeavySymbols.Select(sym => new IsotopeEnrichmentItem(sym)).ToArray());

        private ImmutableList<IsotopeEnrichmentItem> _isotopeEnrichments;

        public IsotopeEnrichments(string name, IList<IsotopeEnrichmentItem> isotopeEnrichments)
            : base(name)
        {
            _isotopeEnrichments = MakeReadOnly(isotopeEnrichments);

            DoValidate();
        }

        public ImmutableList<IsotopeEnrichmentItem> Enrichments
        {
            get { return _isotopeEnrichments; }
        }

        public IsotopeAbundances IsotopeAbundances { get; private set; }

        #region Property change methods

        public IsotopeEnrichments ChangeEnrichment(IsotopeEnrichmentItem item)
        {
            return ChangeProp(ImClone(this), im => im._isotopeEnrichments = MakeReadOnly(im.Enrichments.Select(e =>
                Equals(e.IsotopeSymbol, item.IsotopeSymbol) ? item : e)));
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IsotopeEnrichments()
        {
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            var isotopes = BioMassCalc.DEFAULT_ABUNDANCES;
            var dictSymDist = _isotopeEnrichments.ToDictionary(e => e.IsotopeSymbol,
                                                               e => e.CalcDistribution(isotopes));

            // Make sure all heavy symbols used in Skyline are represented.
            foreach (string symbol in BioMassCalc.HeavySymbols.Where(symbol => !dictSymDist.ContainsKey(symbol)))
            {
                dictSymDist.Add(symbol, new IsotopeEnrichmentItem(symbol, 1.0).CalcDistribution(isotopes));
            }

            IsotopeAbundances = isotopes.SetAbundances(dictSymDist);
        }

        public static IsotopeEnrichments Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new IsotopeEnrichments());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);

            // Consume tag
            reader.ReadStartElement();

            var list = new List<IsotopeEnrichmentItem>();
            reader.ReadElements(list);
            _isotopeEnrichments = MakeReadOnly(list);

            reader.ReadEndElement();

            DoValidate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);

            writer.WriteElements(_isotopeEnrichments);
        }

        #endregion

        #region object overrides

        public bool Equals(IsotopeEnrichments other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                ArrayUtil.EqualsDeep(other._isotopeEnrichments, _isotopeEnrichments);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as IsotopeEnrichments);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ _isotopeEnrichments.GetHashCodeDeep();
            }
        }

        #endregion
    }

    /// <summary>
    /// Describes a single isotope enrichment to be applied to a <see cref="IsotopeAbundances"/>
    /// object.
    /// </summary>
    [XmlRoot("atom_percent_enrichment")]
    public sealed class IsotopeEnrichmentItem : Immutable, IValidating, IXmlSerializable
    {
        public const double MIN_ATOM_PERCENT_ENRICHMENT = 0;
        public const double MAX_ATOM_PERCENT_ENRICHMENT = 1.0;

        public IsotopeEnrichmentItem(string isotopeSymbol)
            : this(isotopeSymbol, BioMassCalc.GetIsotopeEnrichmentDefault(isotopeSymbol))
        {
        }

        public IsotopeEnrichmentItem(string isotopeSymbol, double atomPercentEnrichment)
        {
            IsotopeSymbol = isotopeSymbol;
            AtomPercentEnrichment = atomPercentEnrichment;

            DoValidate();
        }

        public string IsotopeSymbol { get; private set; }
        public double AtomPercentEnrichment { get; private set; }
        public string Symbol { get { return BioMassCalc.GetMonoisotopicSymbol(IsotopeSymbol); } }
        private int IsotopeIndex { get { return BioMassCalc.GetIsotopeDistributionIndex(IsotopeSymbol); } }

        public MassDistribution CalcDistribution(IsotopeAbundances isotopeAbundances)
        {
            var massDistribution = isotopeAbundances[Symbol];
            double mass = massDistribution.ToArray()[IsotopeIndex].Key;
            massDistribution = massDistribution.SetAbundance(mass, AtomPercentEnrichment);
            return massDistribution;            
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IsotopeEnrichmentItem()
        {
        }

        private enum ATTR
        {
            symbol
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            if (Equals(Symbol, IsotopeSymbol))
                throw new InvalidDataException(string.Format(Resources.IsotopeEnrichmentItem_DoValidate_Isotope_enrichment_is_not_supported_for_the_symbol__0__, IsotopeSymbol));
            if (MIN_ATOM_PERCENT_ENRICHMENT > AtomPercentEnrichment ||
                    AtomPercentEnrichment > MAX_ATOM_PERCENT_ENRICHMENT)
            {
                throw new InvalidDataException(string.Format(Resources.IsotopeEnrichmentItem_DoValidate_Atom_percent_enrichment__0__must_be_between__1__and__2__,
                    AtomPercentEnrichment, MIN_ATOM_PERCENT_ENRICHMENT, MAX_ATOM_PERCENT_ENRICHMENT));
            }
        }

        public static IsotopeEnrichmentItem Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new IsotopeEnrichmentItem());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            IsotopeSymbol = reader.GetAttribute(ATTR.symbol);

            // Consume tag
            AtomPercentEnrichment = reader.ReadElementContentAsDoubleInvariant();

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.symbol, IsotopeSymbol);
            // Write element string
            writer.WriteString(AtomPercentEnrichment.ToString(CultureInfo.InvariantCulture));
        }

        #endregion

        #region object overrides

        public bool Equals(IsotopeEnrichmentItem other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.IsotopeSymbol, IsotopeSymbol) &&
                other.AtomPercentEnrichment.Equals(AtomPercentEnrichment);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IsotopeEnrichmentItem)) return false;
            return Equals((IsotopeEnrichmentItem) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = IsotopeSymbol.GetHashCode();
                result = (result*397) ^ AtomPercentEnrichment.GetHashCode();
                return result;
            }
        }

        public override string ToString()
        {
            return string.Format(Resources.IsotopeEnrichmentItem_ToString__0__1__Percent, IsotopeSymbol, AtomPercentEnrichment*100);
        }

        #endregion
    }
}
