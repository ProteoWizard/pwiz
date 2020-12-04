using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Layout;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class ClusteringEditor : CommonFormEx
    {
        private BindingList<ClusterSpecRow> _clusterSpecRows = new BindingList<ClusterSpecRow>();

        public ClusteringEditor()
        {
            InitializeComponent();
            comboDistanceMetric.Items.AddRange(ClusterMetricType.ALL.ToArray());
            columnsDataGridView.AutoGenerateColumns = false;
            colRole.Items.AddRange(ROLE_NONE, ROLE_ROW_LABEL, ROLE_ROW_VALUE, ROLE_COLUMN_LABEL, ROLE_COLUMN_VALUE);
            colRole.ValueMember = nameof(ROLE_NONE.Key);
            colRole.DisplayMember = nameof(ROLE_NONE.Value);
            colTransform.Items.AddRange(ClusterValueTransform.All.ToArray());
            colTransform.ValueMember = nameof(ClusterValueTransform.UNCHANGED.Name);
            colTransform.DisplayMember = nameof(ClusterValueTransform.UNCHANGED.Label);
            columnsDataGridView.DataSource = _clusterSpecRows;
        }

        public void SetData(DataSchema dataSchema, ReportResults reportResults, ClusteringSpec clusteringSpec)
        {
            DataSchema = dataSchema;
            ReportResults = reportResults;
            _clusterSpecRows.Clear();
            _clusterSpecRows.AddRange(MakeRows());
        }

        public DataSchema DataSchema { get; private set; }

        public ReportResults ReportResults { get; private set; }

        public IEnumerable<ClusterSpecRow> MakeRows()
        {
            var pivotedProperties = new PivotedProperties(ReportResults.ItemProperties);
            pivotedProperties = pivotedProperties.ChangeSeriesGroups(pivotedProperties.CreateSeriesGroups());
            foreach (var p in pivotedProperties.UngroupedProperties)
            {
                var columnRef = MakeColumnRef(p.PivotedColumnId);
                if (columnRef == null)
                {
                    continue;
                }
                var row = new ClusterSpecRow(columnRef, null, p.ColumnCaption.GetCaption(DataSchema.DataSchemaLocalizer), false, p.PropertyType)
                {
                    Role = ROLE_NONE.Key,
                    Transform = ClusterValueTransform.BOOLEAN.Name,
                };
                yield return row;
            }

            for (int columnGroupIndex = 0; columnGroupIndex < pivotedProperties.SeriesGroups.Count; columnGroupIndex++)
            {
                var group = pivotedProperties.SeriesGroups[columnGroupIndex];
                foreach (var series in group.SeriesList)
                {
                    var columnRef =
                        MakeColumnRef(pivotedProperties.ItemProperties[series.PropertyIndexes[0]].PivotedColumnId);
                    if (columnRef == null)
                    {
                        continue;
                    }

                    bool equalInAllRows = true;
                    for (int iPivotKey = 0; iPivotKey < group.PivotKeys.Count; iPivotKey++)
                    {
                        var propertyDescriptor = ReportResults.ItemProperties[series.PropertyIndexes[iPivotKey]];
                        if (ReportResults.RowItems.Select(propertyDescriptor.GetValue)
                            .Where(value=>null != value).Distinct().Skip(1).Any())
                        {
                            equalInAllRows = false;
                            break;
                        }
                    }

                    var row = new ClusterSpecRow(columnRef, columnGroupIndex,
                        series.SeriesCaption.GetCaption(DataSchema.DataSchemaLocalizer), equalInAllRows,
                        series.PropertyType)
                    {
                        Role = ROLE_COLUMN_VALUE.Key,
                        Transform = ClusterValueTransform.BOOLEAN.Name
                    };
                    yield return row;
                }
            }
        }

        private readonly KeyValuePair<string, string> ROLE_NONE =
            new KeyValuePair<string, string>(@"none", "None");

        private readonly KeyValuePair<string, string> ROLE_ROW_LABEL =
            new KeyValuePair<string, string>(@"row_label", "Row Label");

        private readonly KeyValuePair<string, string> ROLE_ROW_VALUE =
            new KeyValuePair<string, string>(@"row_value", "Row Value");

        private readonly KeyValuePair<string, string> ROLE_COLUMN_LABEL = new KeyValuePair<string, string>("column_label", "Column Label");
        private readonly KeyValuePair<string, string> ROLE_COLUMN_VALUE =
            new KeyValuePair<string, string>(@"column_value", "Column Value");

        public class ClusterSpecRow
        {
            public ClusterSpecRow(ClusteringSpec.ColumnRef columnRef, int? columnGroupIndex, string columnCaption, bool equalInAllRows, Type propertyType)
            {
                ColumnRef = columnRef;
                Column = columnCaption;
                ColumnGroupIndex = columnGroupIndex;
                EqualInAllRows = equalInAllRows;
                PropertyType = propertyType;
            }

            public ClusteringSpec.ColumnRef ColumnRef { get; private set; }

            public int? ColumnGroupIndex { get; }

            public bool EqualInAllRows { get; }

            public Type PropertyType { get; }
            public string Column { get; }

            public string Role { get; set; }

            public string Transform { get; set; }
        }

        private void columnsDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != colTransform.Index)
            {
                return;
            }
            if (e.RowIndex < 0 || e.RowIndex >= _clusterSpecRows.Count)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("Cell Formatting Row {0}", e.RowIndex);
            var row = _clusterSpecRows[e.RowIndex];
            if (row.Role == ROLE_NONE.Key || row.Role == ROLE_ROW_LABEL.Key)
            {
                e.CellStyle.BackColor = Color.LightGray;
                e.CellStyle.ForeColor = Color.DarkGray;
            }
            else
            {
                e.CellStyle.BackColor = Color.White;
                e.CellStyle.ForeColor = Color.Black;
            }
        }

        private void columnsDataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Error line {0} column {1}: {2}", e.RowIndex, e.ColumnIndex,
                e.Exception);
        }

        private void columnsDataGridView_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            var comboBoxControl = e.Control as ComboBox;
            if (comboBoxControl == null)
            {
                return;
            }

            var rowIndex = columnsDataGridView.CurrentCellAddress.Y;
            if (rowIndex < 0 || rowIndex >= _clusterSpecRows.Count)
            {
                return;
            }


            var rowValue = _clusterSpecRows[rowIndex];
            var columnIndex = columnsDataGridView.CurrentCellAddress.X;
            var newItems = new List<object>();
            if (columnIndex == colRole.Index)
            {
                newItems.Add(ROLE_NONE);
                if (rowValue.ColumnGroupIndex.HasValue)
                {
                    newItems.Add(ROLE_COLUMN_VALUE);
                    if (rowValue.EqualInAllRows)
                    {
                        newItems.Add(ROLE_COLUMN_LABEL);
                    }
                }
                else
                {
                    newItems.Add(ROLE_ROW_LABEL);
                    newItems.Add(ROLE_ROW_VALUE);
                }
                ReplaceItems(comboBoxControl, newItems);
            } else if (columnIndex == colTransform.Index)
            {
                bool isNumeric = ZScores.IsNumericType(rowValue.PropertyType);
                if (isNumeric)
                {
                    newItems.Add(ClusterValueTransform.UNCHANGED);
                    if (rowValue.ColumnGroupIndex.HasValue)
                    {
                        newItems.Add(ClusterValueTransform.ZSCORE);
                    }

                    newItems.Add(ClusterValueTransform.LOGARITHM);
                }

                newItems.Add(ClusterValueTransform.BOOLEAN);
            }
            else
            {
                return;
            }
            ReplaceItems(comboBoxControl, newItems);
        }

        private void columnsDataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            columnsDataGridView.Invalidate();
        }

        private void ReplaceItems(ComboBox comboBox, IEnumerable<object> newItems)
        {
            var oldSelectedItem = comboBox.SelectedItem;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(newItems.ToArray());
            if (oldSelectedItem != null)
            {
                var newSelectedIndex = comboBox.Items.OfType<object>().ToList().IndexOf(oldSelectedItem);
                if (newSelectedIndex >= 0)
                {
                    comboBox.SelectedIndex = newSelectedIndex;
                }
                else 
                {
                    comboBox.Items.Insert(0, oldSelectedItem);
                    comboBox.SelectedIndex = 0;
                }
            }
        }

        private ClusteringSpec.ColumnRef MakeColumnRef(PivotedColumnId pivotedColumnId)
        {
            if (pivotedColumnId == null)
            {
                return null;
            }
            if (pivotedColumnId.SeriesId is PropertyPath propertyPath)
            {
                return new ClusteringSpec.ColumnRef(propertyPath);
            }

            if (pivotedColumnId.SeriesId is ColumnId columnId)
            {
                return new ClusteringSpec.ColumnRef(columnId);
            }

            return null;
        }

        public ClusteringSpec GetClusteringSpec()
        {
            var rowHeaders = new List<ClusteringSpec.ColumnRef>();
            var rowValues = new List<ClusteringSpec.ValueSpec>();
            var groups = new List<ClusteringSpec.GroupSpec>();
            foreach (var row in _clusterSpecRows.Where(row => !row.ColumnGroupIndex.HasValue))
            {
                if (row.Role == ROLE_ROW_LABEL.Key)
                {
                    rowHeaders.Add(row.ColumnRef);
                }
                else if (row.Role == ROLE_ROW_VALUE.Key)
                {
                    rowValues.Add(new ClusteringSpec.ValueSpec(row.ColumnRef, row.Transform));
                }
            }

            foreach (var group in _clusterSpecRows.Where(row => row.ColumnGroupIndex.HasValue)
                .ToLookup(row => row.ColumnGroupIndex))
            {
                var columnHeaders = new List<ClusteringSpec.ColumnRef>();
                var columnValues = new List<ClusteringSpec.ValueSpec>();
                foreach (var row in group)
                {
                    if (row.Role == ROLE_COLUMN_LABEL.Key)
                    {
                        columnHeaders.Add(row.ColumnRef);
                    }
                    else if (row.Role == ROLE_COLUMN_VALUE.Key)
                    {
                        columnValues.Add(new ClusteringSpec.ValueSpec(row.ColumnRef, row.Transform));
                    }
                }
                groups.Add(new ClusteringSpec.GroupSpec().ChangeColumnHeaders(columnHeaders).ChangeColumnValues(columnValues));
            }

            return new ClusteringSpec();
        }
    }
}
