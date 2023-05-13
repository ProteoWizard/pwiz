/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    /// <summary>
    /// A filter which can be used to determine which spectra should be used
    /// when extracting chromatograms for a particular precursor.
    /// </summary>
    [XmlRoot(XML_ROOT)]
    public class SpectrumClassFilter : Immutable, IXmlSerializable, IComparable, IComparable<SpectrumClassFilter>, IEquatable<SpectrumClassFilter>
    {
        public static SpectrumClassFilter EmptyToNull(SpectrumClassFilter spectrumClassFilter)
        {
            return true == spectrumClassFilter?.IsEmpty ? null : spectrumClassFilter;
        }
        public const string XML_ROOT = "spectrum_filter";
        public SpectrumClassFilter(IEnumerable<FilterSpec> filterSpecs)
        {
            FilterSpecs = ImmutableList.ValueOf(filterSpecs);
        }

        public ImmutableList<FilterSpec> FilterSpecs { get; private set; }
        public SpectrumClassFilter Alternative { get; private set; }

        public SpectrumClassFilter ChangeAlternative(SpectrumClassFilter alternative)
        {
            return ChangeProp(ImClone(this), im => im.Alternative = alternative);
        }

        public bool IsEmpty
        {
            get { return FilterSpecs.Count == 0 && Alternative == null; }
        }

        public Predicate<SpectrumMetadata> MakePredicate()
        {
            var dataSchema = new DataSchema();
            var clauses = new List<Predicate<SpectrumMetadata>>();
            foreach (var filterSpec in FilterSpecs)
            {
                var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                if (spectrumClassColumn == null)
                {
                    throw new InvalidOperationException(string.Format(Resources.SpectrumClassFilter_MakePredicate_No_spectrum_column__0_,
                        filterSpec.ColumnId));
                }

                var filterPredicate = filterSpec.Predicate.MakePredicate(dataSchema, spectrumClassColumn.ValueType);
                clauses.Add(spectrum=>filterPredicate(spectrumClassColumn.GetValue(spectrum)));
            }

            Predicate<SpectrumMetadata> alternative = x=>true;
            if (Alternative != null)
            {
                alternative = Alternative.MakePredicate();
            }

            return spectrum =>
            {
                for (int i = 0; i < clauses.Count; i++)
                {
                    if (!clauses[i](spectrum))
                    {
                        return alternative(spectrum);
                    }
                }
                return true;
            };
        }

        public bool Equals(SpectrumClassFilter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(FilterSpecs, other.FilterSpecs) && Equals(Alternative, other.Alternative);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumClassFilter)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return FilterSpecs.GetHashCode() * 397 ^ (Alternative != null ? Alternative.GetHashCode() : 0);
            }
        }

        private SpectrumClassFilter()
        {

        }
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private enum EL
        {
            filter,
            spectrum_filter,
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (FilterSpecs != null)
            {
                throw new InvalidOperationException();
            }

            SpectrumClassFilter alternative = null;
            var filterSpecs = new List<FilterSpec>();
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (true)
                {
                    if (reader.IsStartElement(EL.filter))
                    {
                        filterSpecs.Add(FilterSpec.ReadXml(reader));
                    }
                    else if (reader.IsStartElement(EL.spectrum_filter))
                    {
                        var clause = Deserialize(reader);
                        clause.Alternative = alternative;
                        alternative = clause;
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        reader.ReadEndElement();
                        break;
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            FilterSpecs = ImmutableList.ValueOf(filterSpecs);
            Alternative = alternative;
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var filterSpec in FilterSpecs)
            {
                writer.WriteStartElement(EL.filter);
                filterSpec.WriteXml(writer);
                writer.WriteEndElement();
            }

            if (Alternative != null)
            {
                writer.WriteStartElement(EL.spectrum_filter);
                Alternative.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public static SpectrumClassFilter Deserialize(XmlReader xmlReader)
        {
            return xmlReader.Deserialize(new SpectrumClassFilter());
        }

        public int CompareTo(SpectrumClassFilter other)
        {
            if (other == null)
            {
                return 1;
            }

            int result = FilterSpecs.Count.CompareTo(other.FilterSpecs.Count);
            if (result != 0)
            {
                return result;
            }
            Assume.AreEqual(FilterSpecs.Count, other.FilterSpecs.Count);
            for (int i = 0; i < FilterSpecs.Count; i++)
            {
                result = FilterSpecs[i].ColumnId.CompareTo(other.FilterSpecs[i].ColumnId);
                if (result == 0)
                {
                    result = StringComparer.Ordinal.Compare(FilterSpecs[i].Operation.OpName,
                        other.FilterSpecs[i].Operation.OpName);
                }

                if (result == 0)
                {
                    result = StringComparer.Ordinal.Compare(FilterSpecs[i].Predicate.InvariantOperandText,
                        other.FilterSpecs[i].Predicate.InvariantOperandText);
                }

                if (result != 0)
                {
                    return result;
                }
            }

            if (Alternative == null)
            {
                return other.Alternative == null ? 0 : -1;
            }
            return Alternative.CompareTo(other.Alternative);
        }

        int IComparable.CompareTo(object obj)
        {
            return CompareTo((SpectrumClassFilter) obj);
        }

        public static string GetOperandDisplayText(DataSchema dataSchema, FilterSpec filterSpec)
        {
            var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
            if (spectrumClassColumn == null)
            {
                return filterSpec.Predicate.InvariantOperandText;
            }

            var operandType = filterSpec.Operation.GetOperandType(dataSchema, spectrumClassColumn.ValueType);
            if (operandType == null)
            {
                return null;
            }

            try
            {
                var value = filterSpec.Predicate.GetOperandValue(dataSchema, spectrumClassColumn.ValueType);
                if (value == null)
                {
                    return string.Empty;
                }

                return spectrumClassColumn.FormatAbbreviatedValue(value);
            }
            catch
            {
                return filterSpec.Predicate.GetOperandDisplayText(dataSchema, spectrumClassColumn.ValueType);
            }
        }

        public string GetAbbreviatedText()
        {
            var dataSchema = new DataSchema(SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var clauses = new List<string>();
            foreach (var filterSpec in FilterSpecs)
            {
                var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                if (spectrumClassColumn == null)
                {
                    clauses.Add(TextUtil.SpaceSeparate(filterSpec.Column, filterSpec.Operation?.DisplayName, filterSpec.Predicate.InvariantOperandText));
                }
                else
                {
                    var clauseText = new StringBuilder(spectrumClassColumn.GetAbbreviatedColumnName());
                    var opText = filterSpec.Operation.ShortDisplayName;
                    if (char.IsLetterOrDigit(opText[0]) || char.IsLetterOrDigit(opText[opText.Length - 1]))
                    {
                        clauseText.Append(@" ");
                        clauseText.Append(opText);
                        clauseText.Append(@" ");
                    }
                    else
                    {
                        clauseText.Append(opText);
                    }

                    clauseText.Append(GetOperandDisplayText(dataSchema, filterSpec));
                    clauses.Add(clauseText.ToString().Trim());
                }
            }

            return string.Join(Resources.SpectrumClassFilter_GetAbbreviatedText__AND_, clauses);
        }
    }
}
