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
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.DataBinding.Clustering
{
    public class ClusteredProperties
    {
        public static readonly ClusteredProperties EMPTY = new ClusteredProperties(
            new PivotedProperties(ItemProperties.EMPTY),
            ImmutableList.Empty<DataPropertyDescriptor>(),
            new Dictionary<string, ClusterRole.Transform>(),
            new Dictionary<object, ClusterRole>()
        );
        private Dictionary<string, ClusterRole.Transform> _rowTransforms;
        private Dictionary<object, ClusterRole> _columnRoles;
        private ClusteredProperties(PivotedProperties pivotedProperties, 
            IEnumerable<DataPropertyDescriptor> rowHeaders,
            Dictionary<string, ClusterRole.Transform> rowTransforms, Dictionary<object, ClusterRole> columnRoles)
        {
            PivotedProperties = pivotedProperties;
            RowHeaders = ImmutableList.ValueOf(rowHeaders);
            _rowTransforms = rowTransforms;
            _columnRoles = columnRoles;
        }

        public PivotedProperties PivotedProperties { get; private set; }

        public ImmutableList<DataPropertyDescriptor> RowHeaders
        {
            get;
            set;
        }

        public IEnumerable<DataPropertyDescriptor> RowValues
        {
            get
            {
                return PivotedProperties.UngroupedProperties.Where(p=>null != GetRowTransform(p));
            }
        }

        public IEnumerable<PivotedProperties.Series> GetColumnHeaders(PivotedProperties.SeriesGroup group)
        {
            return group.SeriesList.Where(series=>ClusterRole.COLUMNHEADER == GetColumnRole(series));
        }

        public IEnumerable<DataPropertyDescriptor> GetAllColumnHeaderProperties()
        {
            return PivotedProperties.SeriesGroups.SelectMany(GetColumnHeaders)
                .SelectMany(series => series.PropertyDescriptors);
        }

        public IEnumerable<PivotedProperties.Series> ColumnValues
        {
            get
            {
                return PivotedProperties.SeriesGroups.SelectMany(group =>
                    group.SeriesList.Where(series => GetColumnRole(series) is ClusterRole.Transform));
            }
        }

        public ClusterRole.Transform GetRowTransform(DataPropertyDescriptor propertyDescriptor)
        {
            _rowTransforms.TryGetValue(propertyDescriptor.Name, out ClusterRole.Transform role);
            return role;
        }

        public ClusterRole GetColumnRole(PivotedProperties.Series series)
        {
            _columnRoles.TryGetValue(series.SeriesId, out ClusterRole role);
            return role;
        }

        public static ClusteredProperties FromClusteringSpec(ClusteringSpec clusteringSpec, PivotedProperties pivotedProperties)
        {
            var allRoles = clusteringSpec.ToValueTransformDictionary();
            var rowTransforms = new Dictionary<string, ClusterRole.Transform>();
            var rowHeaders = new List<DataPropertyDescriptor>();
            foreach (var property in pivotedProperties.UngroupedProperties)
            {
                var columnRef = ClusteringSpec.ColumnRef.FromPropertyDescriptor(property);
                if (columnRef != null && allRoles.TryGetValue(columnRef, out ClusterRole role))
                {
                    if (role == ClusterRole.ROWHEADER)
                    {
                        rowHeaders.Add(property);
                    } 
                    else if (role is ClusterRole.Transform transform)
                    {
                        rowTransforms.Add(property.Name, transform);
                    }
                }
            }
            var columnRoles = new Dictionary<object, ClusterRole>();
            foreach (var group in pivotedProperties.SeriesGroups)
            {
                foreach (var series in group.SeriesList)
                {
                    var columnRef = ToColumnRef(series);
                    if (columnRef != null && allRoles.TryGetValue(columnRef, out ClusterRole role))
                    {
                        if (role == ClusterRole.COLUMNHEADER || role is ClusterRole.Transform)
                        {
                            columnRoles.Add(series.SeriesId, role);
                        }
                    }
                }
            }
            return new ClusteredProperties(pivotedProperties, rowHeaders, rowTransforms, columnRoles);
        }

        public ClusteredProperties ReplacePivotedProperties(PivotedProperties pivotedProperties)
        {
            return new ClusteredProperties(pivotedProperties,RowHeaders, _rowTransforms, _columnRoles);
        }

        private static ClusteringSpec.ColumnRef ToColumnRef(PivotedProperties.Series series)
        {
            return ClusteringSpec.ColumnRef.FromPivotedPropertySeries(series);
        }
    }
}
