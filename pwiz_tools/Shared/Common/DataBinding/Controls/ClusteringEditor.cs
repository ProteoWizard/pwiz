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
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Clustering;

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
            colTransform.ValueMember = nameof(ClusterRole.Name);
            colTransform.DisplayMember = nameof(ClusterRole.Label);
            colTransform.Items.AddRange(ClusterRole.All.Cast<object>().ToArray());
            columnsDataGridView.DataSource = _clusterSpecRows;
        }

        public void SetData(DataSchema dataSchema, ReportResults reportResults, ClusteringSpec clusteringSpec)
        {
            _clusterSpecRows.Clear();
            _clusterSpecRows.AddRange(MakeRows(dataSchema, reportResults, clusteringSpec));
            comboDistanceMetric.SelectedItem =
                ClusterMetricType.FromName(clusteringSpec?.DistanceMetric) ?? ClusterMetricType.DEFAULT;
        }

        public IEnumerable<ClusterSpecRow> MakeRows(DataSchema dataSchema, ReportResults reportResults, ClusteringSpec clusteringSpec)
        {
            var pivotedProperties = new PivotedProperties(reportResults.ItemProperties);
            pivotedProperties = pivotedProperties.ChangeSeriesGroups(pivotedProperties.CreateSeriesGroups());
            if (clusteringSpec == null || clusteringSpec.Values.Count == 0)
            {
                clusteringSpec = ClusteringSpec.GetDefaultClusteringSpec(CancellationToken.None, reportResults, pivotedProperties);
            }

            var transforms = clusteringSpec?.ToValueTransformDictionary() ?? new Dictionary<ClusteringSpec.ColumnRef, ClusterRole>();
            foreach (var p in pivotedProperties.UngroupedProperties)
            {
                var columnRef = ClusteringSpec.ColumnRef.FromPropertyDescriptor(p);
                if (columnRef == null)
                {
                    continue;
                }

                var row = new ClusterSpecRow(columnRef, null,
                    p.ColumnCaption.GetCaption(dataSchema.DataSchemaLocalizer), false, p.PropertyType);
                if (transforms.TryGetValue(columnRef, out ClusterRole transform))
                {
                    row.Transform = transform.Name;
                }

                yield return row;
            }

            for (int columnGroupIndex = 0; columnGroupIndex < pivotedProperties.SeriesGroups.Count; columnGroupIndex++)
            {
                var group = pivotedProperties.SeriesGroups[columnGroupIndex];
                foreach (var series in group.SeriesList)
                {
                    var columnRef = ClusteringSpec.ColumnRef.FromPivotedPropertySeries(series);
                    if (columnRef == null)
                    {
                        continue;
                    }

                    bool equalInAllRows = true;
                    foreach (var propertyDescriptor in series.PropertyDescriptors)
                    {
                        if (reportResults.RowItems.Select(propertyDescriptor.GetValue)
                            .Where(value => null != value).Distinct().Skip(1).Any())
                        {
                            equalInAllRows = false;
                            break;
                        }
                    }
                    var row = new ClusterSpecRow(columnRef, columnGroupIndex,
                        series.SeriesCaption.GetCaption(dataSchema.DataSchemaLocalizer), equalInAllRows,
                        series.PropertyType);
                    if (transforms.TryGetValue(columnRef, out ClusterRole transform))
                    {
                        row.Transform = transform.Name;
                    }

                    yield return row;
                }
            }
        }

        public class ClusterSpecRow
        {
            public ClusterSpecRow(ClusteringSpec.ColumnRef columnRef, int? columnGroupIndex, string columnCaption,
                bool equalInAllRows, Type propertyType)
            {
                ColumnRef = columnRef;
                Transform = ClusterRole.IGNORED.Name;
                Column = columnCaption;
                ColumnGroupIndex = columnGroupIndex;
                EqualInAllRows = equalInAllRows;
                PropertyType = propertyType;
            }

            public ClusteringSpec.ColumnRef ColumnRef { get;  }

            public int? ColumnGroupIndex { get; }

            public bool EqualInAllRows { get; }

            public Type PropertyType { get; }
            public string Column { get; }

            public string Transform
            {
                get; set;
            }
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
            if (columnIndex == colTransform.Index)
            {
                var newItems = new List<ClusterRole>();
                newItems.Add(ClusterRole.IGNORED);
                var transforms = ClusterRole.All.OfType<ClusterRole.Transform>()
                    .Where(t => t.CanHandleDataType(rowValue.PropertyType));
                if (rowValue.ColumnGroupIndex.HasValue)
                {
                    if (rowValue.EqualInAllRows)
                    {
                        newItems.Add(ClusterRole.COLUMNHEADER);
                    }
                }
                else
                {
                    newItems.Add(ClusterRole.ROWHEADER);
                    transforms = transforms.Where(t => t != ClusterRole.ZSCORE);
                }

                newItems.AddRange(transforms);
                ReplaceItems(comboBoxControl, newItems);
            }
        }

        private void columnsDataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            columnsDataGridView.Invalidate();
        }

        private void ReplaceItems(ComboBox comboBox, IEnumerable newItems)
        {
            var oldSelectedItem = comboBox.SelectedItem;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(newItems.Cast<object>().ToArray());
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

        public ClusteringSpec GetClusteringSpec()
        {
            var clusteringSpec = new ClusteringSpec(_clusterSpecRows.Select(row => new ClusteringSpec.ValueSpec(row.ColumnRef, row.Transform)));
            clusteringSpec =
                clusteringSpec.ChangeDistanceMetric((comboDistanceMetric.SelectedItem as ClusterMetricType)?.Name);
            return clusteringSpec;
        }

        public ClusterMetricType DistanceMetric
        {
            get
            {
                return comboDistanceMetric.SelectedItem as ClusterMetricType;
            }
            set
            {
                comboDistanceMetric.SelectedItem = value;
            }
        }
    }
}
