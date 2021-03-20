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
using System.Drawing;
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.Colors;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.DataBinding.Clustering
{
    public class ReportColorScheme
    {
        private Dictionary<string, ColorManager> _colorManagers;
        private Dictionary<string, Color> _columnColors;
        private Dictionary<object, ColorManager> _seriesColorManagers;

        private ReportColorScheme(ClusteredProperties clusteredProperties,
            Dictionary<string, ColorManager> managers, Dictionary<string, Color> columnColors, Dictionary<object, ColorManager> seriesColorManagers)
        {
            ClusteredProperties = clusteredProperties;
            _colorManagers = managers;
            _columnColors = columnColors;
            _seriesColorManagers = seriesColorManagers;
        }

        public ClusteredProperties ClusteredProperties { get; private set; }

        public Color? GetColor(DataPropertyDescriptor propertyDescriptor, RowItem rowItem)
        {
            if (!_colorManagers.TryGetValue(propertyDescriptor.Name, out ColorManager colorManager))
            {
                return null;
            }

            return colorManager.ColorScheme.GetColor(colorManager.GetRowValue(rowItem, propertyDescriptor));
        }

        public Color? GetColumnColor(DataPropertyDescriptor propertyDescriptor)
        {
            if (_columnColors.TryGetValue(propertyDescriptor.Name, out Color color))
            {
                return color;
            }

            return null;
        }

        public IEnumerable<Color?> GetRowColors(RowItem rowItem)
        {
            return ClusteredProperties.RowHeaders.Select(p => GetColor(p, rowItem));
        }

        public Color? GetColor(PivotedProperties.Series series, object value)
        {
            if (!_colorManagers.TryGetValue(
                ClusteredProperties.PivotedProperties.ItemProperties[series.PropertyIndexes[0]].Name,
                out ColorManager colorManager))
            {
                return null;
            }

            return colorManager.ColorScheme.GetColor(value);
        }

        public IEnumerable<Color?> GetSeriesColors(PivotedProperties.Series series, RowItem rowItem)
        {
            if (!_seriesColorManagers.TryGetValue(series.SeriesId, out ColorManager colorManager))
            {
                return Enumerable.Repeat((Color?) null, series.PropertyIndexes.Count);
            }

            return colorManager.GetRowValues(rowItem).Select(colorManager.ColorScheme.GetColor);
        }

        public static ReportColorScheme FromClusteredResults(CancellationToken cancellationToken, ClusteredReportResults clusteredReportResults)
        {
            var discreteColorScheme = new DiscreteColorScheme();
            var numericColorScheme = new NumericColorScheme();
            var reportResults = clusteredReportResults;
            var distinctValues = new HashSet<object>();
            var numericValues = new HashSet<double>();
            var colorManagers = new Dictionary<string, ColorManager>();
            var columnHeaderValues = new Dictionary<string, object>();
            var seriesColorManagers = new Dictionary<object, ColorManager>();
            foreach (var property in reportResults.ClusteredProperties.RowHeaders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ClusterRole.IsNumericType(property.PropertyType))
                {
                    var colorScheme = new NumericColorScheme();
                    colorScheme.AddValues(reportResults.RowItems.Select(row => property.GetValue(row)));
                    colorManagers[property.Name] = new ColorManager(colorScheme, new[] { property }, null);
                }
                else
                {
                    distinctValues.UnionWith(reportResults.RowItems.Select(property.GetValue).Where(v => null != v));
                    colorManagers[property.Name] = new ColorManager(discreteColorScheme, new[] { property }, null);
                }
            }
            foreach (var property in reportResults.ClusteredProperties.PivotedProperties.UngroupedProperties)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var transform = reportResults.ClusteredProperties.GetRowTransform(property);
                if (transform == null)
                {
                    continue;
                }
                colorManagers[property.Name] = new ColorManager(numericColorScheme, new[] { property }, transform);
                numericValues.UnionWith(reportResults.RowItems
                    .Select(row => transform.TransformRow(new[] { property.GetValue(row) }).First())
                    .OfType<double>());
            }

            foreach (var group in reportResults.PivotedProperties.SeriesGroups)
            {
                foreach (var series in group.SeriesList)
                {
                    IColorScheme colorScheme = null;
                    var role = reportResults.ClusteredProperties.GetColumnRole(series);
                    if (role == ClusterRole.COLUMNHEADER)
                    {
                        var values = new HashSet<object>();
                        foreach (var property in series.PropertyDescriptors)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var columnDistinctValues = reportResults.RowItems.Select(property.GetValue)
                                .Where(v => null != v).Distinct().ToList();
                            if (columnDistinctValues.Count == 1)
                            {

                                columnHeaderValues[property.Name] = columnDistinctValues[0];
                            }
                            values.UnionWith(columnDistinctValues);
                        }
                        if (ClusterRole.IsNumericType(series.PropertyType))
                        {
                            colorScheme = new NumericColorScheme();
                            colorScheme.AddValues(values);
                        }
                        else
                        {
                            distinctValues.UnionWith(values);
                            colorScheme = discreteColorScheme;
                        }
                    }
                    else if (role is ClusterRole.Transform transform)
                    {
                        colorScheme = numericColorScheme;
                        numericValues.UnionWith(reportResults.RowItems.SelectMany(row =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            return transform.TransformRow(
                                series.PropertyDescriptors.Select(pd => pd.GetValue(row)));
                        }).OfType<double>());
                    }

                    if (colorScheme != null)
                    {
                        var colorManager = new ColorManager(colorScheme, series.PropertyDescriptors, role as ClusterRole.Transform);
                        foreach (var pd in series.PropertyDescriptors)
                        {
                            colorManagers.Add(pd.Name, colorManager);
                        }
                        seriesColorManagers.Add(series.SeriesId, colorManager);
                    }
                }
            }
            numericColorScheme.AddValues(numericValues);
            discreteColorScheme.AddValues(distinctValues);
            var columnColors = new Dictionary<string, Color>();
            foreach (var entry in columnHeaderValues)
            {
                if (colorManagers.TryGetValue(entry.Key, out ColorManager manager))
                {
                    var color = manager.ColorScheme.GetColor(entry.Value);
                    if (color.HasValue)
                    {
                        columnColors.Add(entry.Key, color.Value);
                    }
                }
            }
            return new ReportColorScheme(reportResults.ClusteredProperties, colorManagers, columnColors, seriesColorManagers);
        }

        class ColorManager
        {
            public ColorManager(IColorScheme colorScheme, IEnumerable<DataPropertyDescriptor> propertyDescriptors,
                ClusterRole.Transform transform)
            {
                ColorScheme = colorScheme;
                PropertyDescriptors = ImmutableList.ValueOf(propertyDescriptors);
                Transform = transform;
            }

            public IColorScheme ColorScheme { get; }
            public ImmutableList<DataPropertyDescriptor> PropertyDescriptors { get; }
            public ClusterRole.Transform Transform { get; }

            public IEnumerable<object> GetRowValues(RowItem rowItem)
            {
                var rawValues = PropertyDescriptors.Select(pd => pd.GetValue(rowItem));
                if (Transform != null)
                {
                    return Transform.TransformRow(rawValues).Cast<object>();
                }

                return rawValues;
            }

            public object GetRowValue(RowItem rowItem, DataPropertyDescriptor propertyDescriptor)
            {
                if (Transform == null)
                {
                    return propertyDescriptor.GetValue(rowItem);
                }
                var pdIndex = PropertyDescriptors.IndexOf(propertyDescriptor);
                if (pdIndex < 0)
                {
                    return null;
                }

                return GetRowValues(rowItem).Skip(pdIndex).FirstOrDefault();
            }
        }
    }
}
