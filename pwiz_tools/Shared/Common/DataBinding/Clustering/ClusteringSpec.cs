/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Clustering
{
    public class ClusteringSpec : Immutable
    {
        public static readonly ClusteringSpec DEFAULT = new ClusteringSpec(ImmutableList.Empty<ValueSpec>())
            .ChangeDistanceMetric(ClusterMetricType.EUCLIDEAN.Name);

        public ClusteringSpec(IEnumerable<ValueSpec> values)
        {
            Values = ImmutableList.ValueOfOrEmpty(values);
        }

        [Track]
        public ImmutableList<ValueSpec> Values { get; private set; }

        public ClusteringSpec ChangeValues(IEnumerable<ValueSpec> values)
        {
            return ChangeProp(ImClone(this), im => im.Values = ImmutableList.ValueOf(values));
        }
        [Track]
        public string DistanceMetric { get; private set; }

        public ClusteringSpec ChangeDistanceMetric(string distanceMetric)
        {
            return ChangeProp(ImClone(this), im => im.DistanceMetric = distanceMetric);
        }

        public Dictionary<ColumnRef, ClusterRole> ToValueTransformDictionary()
        {
            var dictionary = new Dictionary<ColumnRef, ClusterRole>();
            foreach (var value in Values)
            {
                dictionary[value.ColumnRef] = value.ClusterValueTransform;
            }

            return dictionary;
        }

        public ClusteringSpec RemoveRole(ClusterRole role)
        {
            var newValues = ImmutableList.ValueOf(Values.Where(value => value.Transform != role.Name));
            if (newValues.Count == Values.Count)
            {
                return this;
            }
            return new ClusteringSpec(newValues);
        }

        protected bool Equals(ClusteringSpec other)
        {
            return Equals(Values, other.Values) && DistanceMetric == other.DistanceMetric;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ClusteringSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Values != null ? Values.GetHashCode() : 0) * 397) ^ (DistanceMetric != null ? DistanceMetric.GetHashCode() : 0);
            }
        }

        private const string EL_VALUE = "value";
        private const string ATTR_DISTANCE_METRIC = "distance_metric";
        private const string ATTR_COLUMN_ID = "column_id";
        private const string ATTR_PROPERTY_PATH = "property_path";
        private const string ATTR_ROLE = "role";


        public void WriteXml(XmlWriter writer)
        {
            if (DistanceMetric != null)
            {
                writer.WriteAttributeString(ATTR_DISTANCE_METRIC, DistanceMetric);
            }
            foreach (var value in Values)
            {
                writer.WriteStartElement(EL_VALUE);
                if (null != value.ColumnRef.ColumnId)
                {
                    writer.WriteAttributeString(ATTR_COLUMN_ID, value.ColumnRef.ColumnId.ToPersistedString());
                }

                if (null != value.ColumnRef.PropertyPath)
                {
                    writer.WriteAttributeString(ATTR_PROPERTY_PATH, value.ColumnRef.PropertyPath.ToString());
                }

                writer.WriteAttributeString(ATTR_ROLE, value.Transform);
                writer.WriteEndElement();
            }
        }

        public static ClusteringSpec ReadXml(XmlReader reader)
        {
            var xElement = (XElement)XNode.ReadFrom(reader);
            var values = new List<ValueSpec>();
            foreach (var elValue in xElement.Elements(EL_VALUE))
            {
                var role = elValue.Attribute(ATTR_ROLE)?.Value;
                if (role == null)
                {
                    continue;
                }

                ColumnRef columnRef = null;
                var columnid = elValue.Attribute(ATTR_COLUMN_ID)?.Value;
                if (columnid != null)
                {
                    columnRef = new ColumnRef(new ColumnId(columnid));
                }
                else
                {
                    columnRef = new ColumnRef(PropertyPath.Parse(elValue.Attribute(ATTR_PROPERTY_PATH)?.Value));
                }
                values.Add(new ValueSpec(columnRef, role));
            }

            var spec = new ClusteringSpec(values);
            var attrDistanceMetric = xElement.Attribute(ATTR_DISTANCE_METRIC);
            if (attrDistanceMetric != null)
            {
                spec = spec.ChangeDistanceMetric(attrDistanceMetric.Value);
            }

            return spec;
        }

        public class ColumnRef
        {
            public ColumnRef(ColumnId columnId)
            {
                ColumnId = columnId;
            }

            public ColumnRef(PropertyPath propertyPath)
            {
                PropertyPath = propertyPath;
            }

            public PropertyPath PropertyPath
            {
                get;
                private set;
            }

            public ColumnId ColumnId { get; private set; }
            public static ColumnRef FromPropertyDescriptor(DataPropertyDescriptor dataPropertyDescriptor)
            {
                if (dataPropertyDescriptor is ColumnPropertyDescriptor columnPropertyDescriptor)
                {
                    return new ColumnRef(columnPropertyDescriptor.PropertyPath);
                }
                return new ColumnRef(new ColumnId(dataPropertyDescriptor.ColumnCaption));
            }

            public static ColumnRef FromPivotedPropertySeries(PivotedProperties.Series series)
            {
                if (series.SeriesId is PropertyPath propertyPath)
                {
                    return new ColumnRef(propertyPath);
                }

                if (series.SeriesId is IColumnCaption caption)
                {
                    return new ColumnRef(new ColumnId(caption));
                }

                return null;
            }

            protected bool Equals(ColumnRef other)
            {
                return Equals(PropertyPath, other.PropertyPath) && Equals(ColumnId, other.ColumnId);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ColumnRef) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((PropertyPath != null ? PropertyPath.GetHashCode() : 0) * 397) ^ (ColumnId != null ? ColumnId.GetHashCode() : 0);
                }
            }

            public override string ToString()
            {
                return (ColumnId ?? (object) PropertyPath ?? string.Empty).ToString();
            }
        }

        public class ValueSpec : Immutable
        {
            public ValueSpec(ColumnRef columnRef, ClusterRole transform) : this (columnRef, transform.Name)
            {
            }

            public ValueSpec(ColumnRef columnRef, string transform)
            {
                ColumnRef = columnRef;
                Transform = transform;
            }

            [Track]
            public ColumnRef ColumnRef { get; private set; }

            public string Transform { get; private set; }

            public ValueSpec ChangeTransform(string transform)
            {
                return ChangeProp(ImClone(this), im => im.Transform = transform);
            }

            public ValueSpec ChangeTransform(ClusterRole transform)
            {
                return ChangeTransform((transform ?? ClusterRole.IGNORED).Name);
            }

            public ClusterRole ClusterValueTransform
            {
                get
                {
                    return ClusterRole.FromName(Transform);
                }
            }

            protected bool Equals(ValueSpec other)
            {
                return Equals(ColumnRef, other.ColumnRef) && Transform == other.Transform;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ValueSpec) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = ColumnRef.GetHashCode();
                    hashCode = (hashCode * 397) ^ (Transform != null ? Transform.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        public static bool EqualValuesInAllRows(CancellationToken cancellationToken, ReportResults reportResults, PivotedProperties.Series series)
        {
            foreach (var propertyDescriptor in series.PropertyDescriptors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var distinctValues = reportResults.RowItems.Select(propertyDescriptor.GetValue)
                    .Where(value => null != value).Distinct();
                if (distinctValues.Skip(1).Any())
                {
                    return false;
                }
            }

            return true;
        }

        private static int MIN_ROWS_TO_ASSUME_HEADER = 3;
        public static ClusteringSpec GetDefaultClusteringSpec(CancellationToken cancellationToken, ReportResults reportResults,
            PivotedProperties pivotedProperties)
        {
            var values = new List<ValueSpec>();
            foreach (var seriesGroup in pivotedProperties.SeriesGroups)
            {
                foreach (var series in seriesGroup.SeriesList)
                {
                    var columnRef = ColumnRef.FromPivotedPropertySeries(series);
                    if (columnRef == null)
                    {
                        continue;
                    }
                    if (reportResults.RowCount >= MIN_ROWS_TO_ASSUME_HEADER && EqualValuesInAllRows(cancellationToken, reportResults, series))
                    {
                        values.Add(new ValueSpec(columnRef, ClusterRole.COLUMNHEADER));
                    }
                    else
                    {
                        var transform = ClusterRole.IsNumericType(series.PropertyType)
                            ? ClusterRole.ZSCORE
                            : ClusterRole.BOOLEAN;
                        values.Add(new ValueSpec(columnRef, transform));
                    }
                }
            }

            using (var propertyEnumerator = pivotedProperties.UngroupedProperties.GetEnumerator())
            {
                while (propertyEnumerator.MoveNext())
                {
                    var columnRef = ColumnRef.FromPropertyDescriptor(propertyEnumerator.Current);
                    if (columnRef == null)
                    {
                        continue;
                    }
                    values.Insert(0, new ValueSpec(columnRef, ClusterRole.ROWHEADER));
                    break;
                }

                if (values.Count == 1)
                {
                    while (propertyEnumerator.MoveNext())
                    {
                        var propertyDescriptor = propertyEnumerator.Current;
                        if (!ClusterRole.IsNumericType(propertyDescriptor?.PropertyType))
                        {
                            continue;
                        }

                        var columnRef = ColumnRef.FromPropertyDescriptor(propertyDescriptor);
                        if (columnRef== null)
                        {
                            continue;
                        }
                        values.Add(new ValueSpec(columnRef, ClusterRole.RAW));
                    }

                    if (values.Count == 1)
                    {
                        return null;
                    }
                }
            }

            if (values.Count == 0)
            {
                return null;
            }
            return new ClusteringSpec(values).ChangeDistanceMetric(ClusterMetricType.EUCLIDEAN.Name);
        }
    }
}
