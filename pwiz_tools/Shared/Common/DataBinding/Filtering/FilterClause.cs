using pwiz.Common.Collections;
using System;
using System.Collections.Generic;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Xml;

namespace pwiz.Common.DataBinding.Filtering
{
    public class FilterClause : IXmlSerializable, IComparable<FilterClause>, IComparable
    {
        public static readonly FilterClause EMPTY = new FilterClause(ImmutableList<FilterSpec>.EMPTY);
        public FilterClause(IEnumerable<FilterSpec> filterSpecs)
        {
            FilterSpecs = ImmutableList.ValueOf(filterSpecs);
        }

        public ImmutableList<FilterSpec> FilterSpecs { get; private set; }

        public Predicate<T> MakePredicate<T>(DataSchema dataSchema)
        {
            var rootColumn = ColumnDescriptor.RootColumn(dataSchema, typeof(T));
            var clauses = new List<Predicate<RowItem>>();
            foreach (var filterSpec in FilterSpecs)
            {
                var column = FindColumn(rootColumn, filterSpec.ColumnId);
                if (column == null)
                {
                    throw new InvalidOperationException(string.Format("Invalid filter column '{0}'",
                        filterSpec.ColumnId));
                }

                var filterPredicate = filterSpec.Predicate.MakePredicate(dataSchema, column.PropertyType);
                clauses.Add(rowItem=> filterPredicate(column.GetPropertyValue(rowItem, null)));
            }

            return row =>
            {
                var rowItem = new RowItem(row);
                for (int i = 0; i < clauses.Count; i++)
                {
                    if (!clauses[i](rowItem))
                    {
                        return false;
                    }
                }
                return true;
            };
        }

        public bool Equals(FilterClause other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(FilterSpecs, other.FilterSpecs);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FilterClause)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return FilterSpecs.GetHashCode() * 397;
            }
        }

        private FilterClause()
        {
        }
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        private const string EL_FILTER = "filter";

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
                    if (reader.IsStartElement(EL_FILTER))
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
                writer.WriteStartElement(EL_FILTER);
                filterSpec.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        public int CompareTo(FilterClause other)
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

            return 0;
        }

        int IComparable.CompareTo(object obj)
        {
            return CompareTo((FilterClause)obj);
        }

        public static ColumnDescriptor FindColumn(ColumnDescriptor root, PropertyPath propertyPath)
        {
            if (propertyPath.IsRoot)
            {
                return root;
            }

            var parent = FindColumn(root, propertyPath.Parent);
            if (parent == null)
            {
                return null;
            }

            if (propertyPath.IsProperty)
            {
                return parent.ResolveChild(propertyPath.Name);
            }

            return null;
        }

        public static FilterClause Deserialize(XmlReader reader)
        {
            var filterClause = new FilterClause();
            ((IXmlSerializable) filterClause).ReadXml(reader);
            return filterClause;
        }

        public bool IsEmpty
        {
            get
            {
                return FilterSpecs.Count == 0;
            }
        }
    }
}
