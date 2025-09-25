/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class BoxPlotGraph : DataboundGraph
    {
        private ReplicateValue _groupByValue;

        public BoxPlotGraph()
        {
            InitializeComponent();
            var graphPane = zedGraphControl1.GraphPane;
            graphPane.YAxis.Title.IsVisible = false;
            graphPane.Legend.IsVisible = true;
            graphPane.Margin.All = 0;
            graphPane.Border.IsVisible = false;
            graphPane.Chart.Border.IsVisible = false;

            graphPane.XAxis.Type = AxisType.Text;
            graphPane.XAxis.MinorTic.Size = 0;
            graphPane.XAxis.MajorTic.IsOpposite = false;
            graphPane.XAxis.MajorTic.Size = 2;
            graphPane.YAxis.MinorTic.Size = 0;
            graphPane.YAxis.MajorTic.IsOpposite = false;
            graphPane.YAxis.MajorTic.Size = 2;

            graphPane.X2Axis.MinorTic.Size = 0;
            graphPane.X2Axis.MajorTic.Size = 0;
            graphPane.Y2Axis.MinorTic.Size = 0;
            graphPane.Y2Axis.MajorTic.Size = 0;
            graphPane.X2Axis.IsVisible = false;
            graphPane.Y2Axis.IsVisible = false;

            graphPane.XAxis.Scale.FontSpec = GraphSummary.CreateFontSpec(Color.Black);
            graphPane.XAxis.Scale.FontSpec.Angle = 90;
            graphPane.YAxis.Scale.FontSpec = GraphSummary.CreateFontSpec(Color.Black);
            graphPane.YAxis.Scale.FontSpec.Angle = 90;

            graphPane.BarSettings.MinClusterGap = 3f;
            graphPane.BarSettings.Type = BarType.Cluster;

            graphPane.XAxis.Title.Text = "Replicate"; // TODO How to populate this? Also localize
            graphPane.YAxis.Title.Text = "Log2";
        }

        public static BoxPlotGraph CreateBoxPlotGraph(SkylineWindow skylineWindow)
        {
            var dataSchema = new SkylineWindowDataSchema(skylineWindow);
            var viewContext = new DocumentGridViewContext(dataSchema);
            var container = new Container();
            var bindingListSource = new BindingListSource(container);

            var columnDescriptor = ColumnDescriptor.RootColumn(dataSchema, typeof(Protein));
            var viewSpec = new ViewSpec().SetName("Replicate Pivot").SetRowType(columnDescriptor.PropertyType);
            var columnNames = new List<string>() { "", "Description", @"Results!*.Value.Abundance.TransitionAveraged" };
            var columns = columnNames.Select(c => new ColumnSpec(PropertyPath.Parse(c)));
            viewSpec = viewSpec.SetColumns(columns);

            var viewInfo = new ViewInfo(columnDescriptor, viewSpec).ChangeViewGroup(ViewGroup.BUILT_IN);

            bindingListSource.SetViewContext(viewContext, viewInfo);
            var boxPlot = new BoxPlotGraph
            {
                SkylineWindow = skylineWindow,
                BindingListSource = bindingListSource,
            };
            boxPlot.RefreshData();
            return boxPlot;
        }
        
        public void UpdateGraph()
        {
            bool isClusteredMode = _groupByValue == null;
            var dataSet = buildDataMap();
            if (dataSet.IsNullOrEmpty())
                return;

            var pane = zedGraphControl1.GraphPane;
            pane.CurveList.Clear();

            // 1) Build global label index
            var allLabels = dataSet
                .SelectMany(kvp => kvp.Value)
                .Select(d => d.Label)
                .Distinct()
                .ToArray();

            pane.XAxis.Type = AxisType.Text;
            pane.XAxis.Scale.TextLabels = allLabels;

            var labelToIndex = allLabels
                .Select((label, i) => new { label, i })
                .ToDictionary(x => x.label, x => x.i);

            // 2) Bar drawing mode
            pane.BarSettings.Type = isClusteredMode ? BarType.Cluster : BarType.Overlay;
            //pane.BarSettings.MinBarGap = 0.0f;
            //pane.BarSettings.MinClusterGap = isClusteredMode ? 0.15f : 0.0f;
            //pane.BarSettings.ClusterScaleWidth = 1.0f;

            // Optional: keep one label per tick
            //pane.XAxis.Scale.MajorStep = 1;

            // 3) Build one curve per group, aligned to the global label index
            var usedColors = new List<Color>();
            int slots = allLabels.Length;

            foreach (var kvp in dataSet)
            {
                // Start with all slots as Missing to preserve alignment
                var pointsArray = new PointPair[slots];
                for (int i = 0; i < slots; i++)
                    pointsArray[i] = new PointPair(i, PointPairBase.Missing);

                // Fill only the labels this group actually has
                foreach (var d in kvp.Value)
                {
                    int x = labelToIndex[d.Label];
                    pointsArray[x] = BoxPlotBarItem.MakePointPair(
                        x, d.Q3, d.Q1, d.Median, d.Max, d.Min, d.Outliers);
                }

                var points = new PointPairList(pointsArray);
                var color = ColorGenerator.GetColor(kvp.Key, usedColors);
                usedColors.Add(color);

                var bar = new BoxPlotBarItem(kvp.Key, points, color, Color.Black);
                pane.CurveList.Add(bar);
            }

            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();
        }
        private Dictionary<string, List<BoxPlotData>> buildDataMap()
        {
            if (BindingListSource.ItemProperties.IsNullOrEmpty())
            {
                return new Dictionary<string, List<BoxPlotData>>();
            }
            var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(BindingListSource.ItemProperties);
            var rowItems = BindingListSource.Cast<RowItem>().ToArray();
            var boxPlotDataMap = new Dictionary<string, List<BoxPlotData>> ();

            foreach (var group in replicatePivotColumns.GetReplicateColumnGroups())
            {
                foreach (var column in group)
                {
                    // Filter out replicate constant columns.
                    if (replicatePivotColumns.IsConstantColumn(column))
                    {
                        continue;
                    }
                    // Filter our non replicate constant columns.
                    if (replicatePivotColumns.GetResultKey(column) == null)
                    {
                        continue;
                    }

                    // Determine which key to group by. By default, use property name. 
                    string groupByKey = null;
                    var groupByReplicateValue = _groupByValue;
                    if (groupByReplicateValue != null)
                    {
                        var chromatogramSet =
                            SkylineWindow.Document.Settings.MeasuredResults?.Chromatograms[group.Key.ReplicateIndex];
                        groupByKey = groupByReplicateValue.GetValue(new AnnotationCalculator(SkylineWindow.Document), chromatogramSet).ToString();
                    }
                    else
                    {
                        groupByKey = column.DisplayColumn.ColumnDescriptor?.GetColumnCaption(ColumnCaptionType.localized);
                    }

                    // Initialize List for column
                    if (!boxPlotDataMap.TryGetValue(groupByKey, out var columnDataList))
                    { 
                        columnDataList = new List<BoxPlotData>();
                        boxPlotDataMap.Add(groupByKey, columnDataList);
                    }
                    var replicate = GetReplicate(column.DisplayColumn.DataSchema as SkylineDataSchema, column.PivotKey, replicatePivotColumns);
                    Console.WriteLine($"Replicate {replicate.Name}, Count: {replicate.Files.Count}");
                    // Add data points for column and replicate
                    var dataPoints = rowItems.Select(column.GetValue).Where(v => v != null).Cast<double>().ToArray();
                    Console.WriteLine($"Replicate Name {group.Key.ReplicateName}, Group By Key {groupByKey}, Group By Type {groupByReplicateValue?.Title}");
                    var boxPlotData = BoxPlotDataUtil.buildBoxPlotData(dataPoints, group.Key.ReplicateName);
                    if (boxPlotData != null)
                    {
                        columnDataList.Add(boxPlotData);
                    }
                }
            }
            return boxPlotDataMap;
        }

        private Replicate GetReplicate(SkylineDataSchema skylineDataSchema, PivotKey pivotKey, ReplicatePivotColumns replicatePivotColumns)
        {
            var resultKey = replicatePivotColumns.GetResultKey(pivotKey);
            if (resultKey == null)
            {
                return null;
            }

            var resultKeyWithoutFileIndex = new ResultKey(resultKey.ReplicateName, resultKey.ReplicateIndex, 0);
            Replicate replicate = null;
            skylineDataSchema?.ReplicateList.TryGetValue(resultKeyWithoutFileIndex, out replicate);
            return replicate;
        }

        private void zedGraphControl1_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            var groupingMenuName = "groupingMenu";
            var groupingMenu = menuStrip.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(i => i.Name == groupingMenuName);

            if (groupingMenu == null)
            {
                groupingMenu = new ToolStripMenuItem("Group By");
                menuStrip.Items.Add(groupingMenu);
            }
            else
            {
                groupingMenu.DropDownItems.Clear();
            }


            var groupings = ReplicateValue.GetGroupableReplicateValues(SkylineWindow.Document).ToArray();
            foreach (var replicateValue in groupings)
            {
                var groupByItem = new ToolStripMenuItem(replicateValue.Title)
                {
                    CheckOnClick = true,
                    Checked = replicateValue.Equals(_groupByValue)
                };
                groupingMenu.DropDownItems.Add(groupByItem);
                groupByItem.Click += (s, e) =>
                {
                    foreach (ToolStripMenuItem item in groupingMenu.DropDownItems)
                    {
                        item.Checked = false;
                    }

                    if (replicateValue.Equals(_groupByValue))
                    {
                        groupByItem.Checked = false;
                        _groupByValue = null;
                    }
                    else
                    {
                        groupByItem.Checked = true;
                        _groupByValue = replicateValue;
                    }
                    groupByReplicateValue(replicateValue);
                };
            }
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);
        }
        
        private void groupByReplicateValue(ReplicateValue value)
        {
            UpdateGraph();
        }

        public override void RefreshData()
        {
            UpdateGraph();
        }
    }
}
