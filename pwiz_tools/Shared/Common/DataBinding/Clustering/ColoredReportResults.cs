using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Colors;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.DataBinding.Clustering
{
    public class ReportColorScheme
    {
        private Dictionary<string, ColorManager> _colorManagers;
        private Dictionary<string, Color> _columnColors;

        private ReportColorScheme(ClusteredProperties clusteredProperties,
            Dictionary<string, ColorManager> managers, Dictionary<string, Color> columnColors)
        {
            ClusteredProperties = clusteredProperties;
            _colorManagers = managers;
            _columnColors = columnColors;
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

        public static ReportColorScheme FromClusteredResults(ClusteredReportResults clusteredReportResults)
        {
            var discreteColorScheme = new DiscreteColorScheme();
            var numericColorScheme = new NumericColorScheme();
            var reportResults = clusteredReportResults;
            var distinctValues = new HashSet<object>();
            var numericValues = new HashSet<double>();
            var colorManagers = new Dictionary<string, ColorManager>();
            var columnHeaderValues = new Dictionary<string, object>();
            foreach (var property in reportResults.ClusteredProperties.RowHeaders)
            {
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
                    var propertyDescriptors = series.PropertyIndexes
                        .Select(index => reportResults.ItemProperties[index]).ToList();
                    IColorScheme colorScheme = null;
                    var role = reportResults.ClusteredProperties.GetColumnRole(series);
                    if (role == ClusterRole.COLUMNHEADER)
                    {
                        var values = new HashSet<object>();
                        foreach (var property in propertyDescriptors)
                        {
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
                                transform.TransformRow(propertyDescriptors.Select(pd => pd.GetValue(row))))
                            .OfType<double>());
                    }

                    if (colorScheme != null)
                    {
                        var colorManager = new ColorManager(colorScheme, propertyDescriptors, role as ClusterRole.Transform);
                        foreach (var pd in propertyDescriptors)
                        {
                            colorManagers.Add(pd.Name, colorManager);
                        }
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
            return new ReportColorScheme(reportResults.ClusteredProperties, colorManagers, columnColors);
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
