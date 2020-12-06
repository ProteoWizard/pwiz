using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Colors;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Clustering;

namespace pwiz.Common.DataBinding.Controls
{
    public class DataboundColorManager : Component
    {
        private Dictionary<string, ColorManager> _colorManagers;
        private BoundDataGridView _boundDataGridView;
        private IColorScheme _numericColorScheme = new NumericColorScheme();
        private IColorScheme _discreteColorScheme = new DiscreteColorScheme(2);

        public BoundDataGridView BoundDataGridView
        {
            get
            {
                return _boundDataGridView;
            }
            set
            {
                if (ReferenceEquals(BoundDataGridView, value))
                {
                    return;
                }

                if (BoundDataGridView != null)
                {
                    BoundDataGridView.CellFormatting -= BoundDataGridView_OnCellFormatting;
                    BoundDataGridView.DataBindingComplete -= BoundDataGridView_DataBindingComplete;
                }

                _boundDataGridView = value;
                if (BoundDataGridView != null)
                {
                    BoundDataGridView.CellFormatting += BoundDataGridView_OnCellFormatting;
                    BoundDataGridView.DataBindingComplete += BoundDataGridView_DataBindingComplete;
                }
            }
        }

        public BindingListSource BindingListSource
        {
            get
            {
                return BoundDataGridView?.DataSource as BindingListSource;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BoundDataGridView = null;
            }
        }

        private void BoundDataGridView_OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_colorManagers == null)
            {
                return;
            }

            var reportResults = BindingListSource?.ReportResults;
            if (reportResults == null)
            {
                return;
            }

            if (e.ColumnIndex < 0 || e.ColumnIndex >= BoundDataGridView.ColumnCount || e.RowIndex < 0 ||
                e.RowIndex >= reportResults.RowCount)
            {
                return;
            }

            var propertyName = BoundDataGridView.Columns[e.ColumnIndex].DataPropertyName;
            if (propertyName == null || !_colorManagers.TryGetValue(propertyName, out ColorManager colorManager))
            {
                return;
            }

            var propertyDescriptor = reportResults.ItemProperties.FindByName(propertyName);
            if (propertyDescriptor == null)
            {
                return;
            }

            var color = colorManager.ColorScheme.GetColor(colorManager.GetRowValue(reportResults.RowItems[e.RowIndex],
                propertyDescriptor));
            if (color.HasValue)
            {
                e.CellStyle.BackColor = color.Value;
                if (color.Value.R + color.Value.B + color.Value.G < 128 * 3)
                {
                    e.CellStyle.ForeColor = Color.White;
                }
            }

        }

        private void BoundDataGridView_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            InitializeColorSchemes();
        }

        public void InitializeColorSchemes()
        {
            var bindingListSource = BoundDataGridView?.DataSource as BindingListSource;
            var reportResults = bindingListSource?.ReportResults as ClusteredReportResults;
            if (reportResults == null)
            {
                _colorManagers = null;
                return;
            }
            var distinctValues = new HashSet<object>();
            var numericValues = new HashSet<double>();
            var colorManagers = new Dictionary<string, ColorManager>();
            var columnHeaderValues = new Dictionary<string, object>();
            foreach (var property in reportResults.ClusteredProperties.RowHeaders)
            {
                if (ZScores.IsNumericType(property.PropertyType))
                {
                    var colorScheme = new NumericColorScheme();
                    colorScheme.Calibrate(reportResults.RowItems.Select(row => property.GetValue(row)));
                    colorManagers[property.Name] = new ColorManager(colorScheme, new[] {property}, null);
                }
                else
                {
                    distinctValues.UnionWith(reportResults.RowItems.Select(property.GetValue).Where(v => null != v));
                    colorManagers[property.Name] = new ColorManager(_discreteColorScheme, new[] { property }, null);
                }
            }
            foreach (var property in reportResults.ClusteredProperties.PivotedProperties.UngroupedProperties)
            {
                ColorManager colorManager = null;
                var transform = reportResults.ClusteredProperties.GetRowTransform(property);
                if (transform == null)
                {
                    continue;
                }
                colorManagers[property.Name] = new ColorManager(_numericColorScheme, new []{property}, transform);
                numericValues.UnionWith(reportResults.RowItems
                    .Select(row => transform.TransformRow(new[] {property.GetValue(row)}).First())
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
                        if (ZScores.IsNumericType(series.PropertyType))
                        {
                            colorScheme = new NumericColorScheme();
                            colorScheme.Calibrate(values);
                        }
                        else
                        {
                            distinctValues.UnionWith(values);
                            colorScheme = _discreteColorScheme;
                        }
                    }
                    else if (role is ClusterRole.Transform transform)
                    {
                        colorScheme = _numericColorScheme;
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
            _numericColorScheme.Calibrate(numericValues);
            _discreteColorScheme.Calibrate(distinctValues);
            _colorManagers = colorManagers;
            if (columnHeaderValues.Any())
            {
                BoundDataGridView.EnableHeadersVisualStyles = false;
                foreach (var entry in columnHeaderValues)
                {
                    if (!_colorManagers.TryGetValue(entry.Key, out ColorManager colorManager))
                    {
                        continue;
                    }
                    foreach (var col in BoundDataGridView.Columns.OfType<DataGridViewColumn>()
                        .Where(col => col.DataPropertyName == entry.Key))
                    {
                        var color = colorManager.ColorScheme.GetColor(entry.Value);
                        if (color.HasValue)
                        {
                            col.HeaderCell.Style.BackColor = color.Value;
                        }
                    }
                }
            }
            else
            {
                BoundDataGridView.EnableHeadersVisualStyles = true;
            }
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
