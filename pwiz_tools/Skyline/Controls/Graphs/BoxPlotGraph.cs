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
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class BoxPlotGraph : DataboundGraph
    {
        private ReplicateValue _groupByValue;
        private Normalization? _normalizationValue;
        private static Color DefaultBarColor = Color.LightGreen;

        public BoxPlotGraph()
        {
            InitializeComponent();
            var graphPane = zedGraphControl1.GraphPane;
            graphPane.YAxis.Title.IsVisible = false;
            graphPane.Legend.IsVisible = true;
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
            graphPane.IsFontsScaled = false;

            graphPane.BarSettings.MinClusterGap = 3f;
            graphPane.BarSettings.Type = BarType.Overlay;

            graphPane.Title.IsVisible = false;
            graphPane.XAxis.Title.Text = "Replicate"; // TODO Localize
            graphPane.XAxis.Title.IsVisible = true;
            graphPane.YAxis.Title.Text = "Log2 Peak Area"; // TODO Localize
            graphPane.YAxis.Title.IsVisible = true;
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
            var dataSet = buildBoxPlotDataList();

            if (dataSet.IsNullOrEmpty())
                return;

            var pane = zedGraphControl1.GraphPane;
            pane.CurveList.Clear();

            bool isGrouped = _groupByValue != null;
            pane.XAxis.Type = AxisType.Text;
            pane.Legend.IsVisible = isGrouped;
            var boxPlotData = new List<BoxPlotData>(); // yeah idk theres probably a better way to do this

            if (!isGrouped)
            {
                var points2 = new List<PointPair>();

                var allLabels = dataSet
                    .Select(d => SkylineWindow.Document.Settings.MeasuredResults?.Chromatograms[d.Key].Name)
                    .Distinct()
                    .ToArray();

                var labelToIndex = allLabels
                    .Select((label, i) => new { label, i })
                    .ToDictionary(x => x.label, x => x.i);

                foreach (var kvp in dataSet)
                {
                    var replicateName = SkylineWindow.Document.Settings.MeasuredResults?.Chromatograms[kvp.Key].Name;
                    var x = labelToIndex[replicateName];
                    var d = BoxPlotDataUtil.buildBoxPlotData(kvp.Value.ToArray(), replicateName, dataSet.Values.Select(list => list.ToArray()).ToArray(), _normalizationValue);
                    boxPlotData.Add(d);
                    var pointPair = BoxPlotBarItem.MakePointPair(x, d.Q3, d.Q1, d.Median, d.Max, d.Min, d.Outliers);
                    points2.Add(pointPair);
                }
                var pointPairList = new PointPairList(points2);
                var bar = new BoxPlotBarItem(string.Empty, pointPairList, DefaultBarColor, Color.Black);

                pane.CurveList.Add(bar);
                pane.XAxis.Scale.TextLabels = allLabels;

            }
            else
            {
                var groupedDataSet = dataSet
                    .GroupBy(kvp =>
                        _groupByValue
                            .GetValue(new AnnotationCalculator(SkylineWindow.Document), SkylineWindow.Document.Settings.MeasuredResults?.Chromatograms[kvp.Key])
                            .ToString())
                    .ToDictionary(
                        g => g.Key,
                        g => g.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    );

                var allLabels = groupedDataSet
                    .SelectMany(kvp => kvp.Value)
                    .Select(kvp => SkylineWindow.Document.Settings.MeasuredResults?.Chromatograms[kvp.Key].Name)
                    .Distinct()
                    .ToArray();


                var labelToIndex = allLabels
                    .Select((label, i) => new { label, i })
                    .ToDictionary(x => x.label, x => x.i);

                var usedColors = new List<Color>();
                int slots = allLabels.Length;
                foreach (var group in groupedDataSet)
                {
                    var groupByValue = group.Key;
                    var groupDataSet = group.Value;
                    var pointsArray = new PointPair[slots];
                    for (var i = 0; i < slots; i++)
                        pointsArray[i] = new PointPair(i, PointPairBase.Missing);

                    foreach (var kvp in groupDataSet)
                    {
                        var replicateIndex = kvp.Key;
                        var chromatogramSet = SkylineWindow.Document.Settings.MeasuredResults?.Chromatograms[replicateIndex];
                        var replicateName = chromatogramSet.Name;
                        var x = labelToIndex[replicateName];
                        var d = BoxPlotDataUtil.buildBoxPlotData(kvp.Value.ToArray(), replicateName, dataSet.Values.Select(list => list.ToArray()).ToArray(), _normalizationValue);
                        boxPlotData.Add(d);
                        pointsArray[x] = BoxPlotBarItem.MakePointPair(x, d.Q3, d.Q1, d.Median, d.Max, d.Min, d.Outliers);
                    }
                    var points = new PointPairList(pointsArray);
                    var color = ColorGenerator.GetColor(groupByValue, usedColors);
                    usedColors.Add(color);

                    var bar = new BoxPlotBarItem(groupByValue, points, color, Color.Black);
                    pane.CurveList.Add(bar);
                    pane.XAxis.Scale.TextLabels = allLabels;

                }
            }

            double yMin = boxPlotData.Min(bpd => bpd.Outliers.Length > 0 ? Math.Min(bpd.Min, bpd.Outliers.Min()) : bpd.Min);
            double yMax = boxPlotData.Max(bpd => bpd.Outliers.Length > 0 ? Math.Max(bpd.Max, bpd.Outliers.Max()) : bpd.Max);
            zedGraphControl1.GraphPane.YAxis.Scale.Min = yMin - 0.5;
            zedGraphControl1.GraphPane.YAxis.Scale.Max = yMax + 0.5;

            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();
        }

        private Dictionary<int, List<double>> buildBoxPlotDataList()
        {
            var document = SkylineWindow.Document;
            var dataSchema = new SkylineWindowDataSchema(SkylineWindow);
            var moleculeGroups = document.MoleculeGroups.ToList();
            var replicateDataPoints = new Dictionary<int, List<double>>();
            for (int iMoleculeGroup = 0; iMoleculeGroup < moleculeGroups.Count; iMoleculeGroup++)
            {
                var moleculeGroup = moleculeGroups[iMoleculeGroup];
                var path = new IdentityPath(IdentityPath.ROOT, moleculeGroup.PeptideGroup);
                var protein = new Protein(dataSchema, path);
                var abundances = protein.GetProteinAbundances();

                foreach (var kvp in abundances)
                {
                    int replicateIndex = kvp.Key;
                    double abundance = kvp.Value.Raw;
                    if (!replicateDataPoints.TryGetValue(replicateIndex, out var values))
                    {
                        values = new List<double>();
                        replicateDataPoints[replicateIndex] = values;
                    }
                    values.Add(abundance);
                }
            }

            return replicateDataPoints;
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
                    UpdateGraph();
                };
            }

            var normalizationMenuName = "normalizationMenuName";
            var normalizationMenu = menuStrip.Items
                .OfType<ToolStripMenuItem>()
                .FirstOrDefault(i => i.Name == normalizationMenuName);

            if (normalizationMenu == null)
            {
                normalizationMenu = new ToolStripMenuItem("Normalized To");
                menuStrip.Items.Add(normalizationMenu);
            }
            else
            {
                normalizationMenu.DropDownItems.Clear();
            }

            foreach (var normalization in Enum.GetValues(typeof(Normalization)))
            {
                var normalizeItem = new ToolStripMenuItem(normalization.ToString())
                {
                    CheckOnClick = true,
                    Checked = normalization.Equals(_normalizationValue)
                };
                normalizationMenu.DropDownItems.Add(normalizeItem);
                normalizeItem.Click += (s, e) =>
                {
                    foreach (ToolStripMenuItem item in groupingMenu.DropDownItems)
                    {
                        item.Checked = false;
                    }

                    if (normalization.Equals(_normalizationValue))
                    {
                        normalizeItem.Checked = false;
                        _normalizationValue = null;
                    }
                    else
                    {
                        normalizeItem.Checked = true;
                        _normalizationValue = (Normalization)normalization;
                    }
                    UpdateGraph();
                };
            }


            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);
        }

        public override void RefreshData()
        {
            UpdateGraph();
        }

        public enum Normalization
        {
            Median
        }
    }
}
