using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Spectra
{
    [XmlRoot(XML_ROOT)]
    public class SpectrumClassFilter : Immutable, IXmlSerializable
    {
        public const string XML_ROOT = "spectrum_filter";
        public SpectrumClassFilter(IEnumerable<FilterSpec> filterSpecs)
        {
            FilterSpecs = ImmutableList.ValueOf(filterSpecs);
        }

        public ImmutableList<FilterSpec> FilterSpecs { get; private set; }

        public Predicate<SpectrumMetadata> MakePredicate()
        {
            var dataSchema = new DataSchema();
            var clauses = new List<Predicate<SpectrumMetadata>>();
            foreach (var filterSpec in FilterSpecs)
            {
                var spectrumClassColumn = SpectrumClassColumn.FindColumn(filterSpec.ColumnId);
                if (spectrumClassColumn == null)
                {
                    throw new InvalidOperationException(string.Format("No such spectrum column {0}",
                        filterSpec.ColumnId));
                }

                var filterPredicate = filterSpec.Predicate.MakePredicate(dataSchema, spectrumClassColumn.ValueType);
                clauses.Add(spectrum=>filterPredicate(spectrumClassColumn.GetValue(spectrum)));
            }

            return spectrum =>
            {
                foreach (var clause in clauses)
                {
                    if (!clause(spectrum))
                    {
                        return false;
                    }
                }

                return true;
            };
        }

        protected bool Equals(SpectrumClassFilter other)
        {
            return FilterSpecs.Equals(other.FilterSpecs);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpectrumClassFilter) obj);
        }

        public override int GetHashCode()
        {
            return FilterSpecs.GetHashCode();
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
            filter
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (FilterSpecs != null)
            {
                throw new InvalidOperationException();
            }

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
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var filterSpec in FilterSpecs)
            {
                writer.WriteStartElement(EL.filter);
                filterSpec.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public static SpectrumClassFilter Deserialize(XmlReader xmlReader)
        {
            return xmlReader.Deserialize(new SpectrumClassFilter());
        }
    }
}
