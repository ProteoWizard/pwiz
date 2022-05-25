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
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public partial class PcaPlot : DataboundGraph
    {
        private List<Tuple<string, PivotedProperties.SeriesGroup>> _datasetOptions;
        private bool _inUpdateControls;
        private static readonly Color MISSING_COLOR = Color.Black;
        private readonly PcaCalculator _calculator;
        private PcaChoice _pcaChoice = PcaChoice.EMPTY;
        public PcaPlot()
        {
            InitializeComponent();
            _calculator = new PcaCalculator(this);
            Localizer = SkylineDataSchema.GetLocalizedSchemaLocalizer();
        }

        public ClusterInput ClusterInput
        {
            get
            {
                return _calculator.Input;
            }
            set
            {
                _calculator.Input = value;
            }
        }

        public Clusterer Clusterer
        {
            get
            {
                return _calculator.Results?.Item1;
            }
        }

        public ReportColorScheme ColorScheme
        {
            get
            {
                return _calculator.Results.Item2;
            }
        }

        public override ZedGraphControl GraphControl => zedGraphControl1;

        public DataSchemaLocalizer Localizer { get; }

        public void UpdateControls()
        {
            bool wasInUpdateControls = _inUpdateControls;
            try
            {
                _inUpdateControls = true;
                var newDatasetOptions = GetDatasetOptions();
                comboDataset.Items.Clear();
                foreach (var tuple in newDatasetOptions)
                {
                    comboDataset.Items.Add(tuple.Item1);
                }

                if (newDatasetOptions.Any())
                {
                    if (PcaChoiceValue.DataSetIndex < newDatasetOptions.Count)
                    {
                        comboDataset.SelectedIndex = PcaChoiceValue.DataSetIndex;
                    }
                    else
                    {
                        comboDataset.SelectedIndex = 0;
                    }
                }
                _datasetOptions = newDatasetOptions;
                UpdateGraph();
            }
            finally
            {
                _inUpdateControls = wasInUpdateControls;
            }

        }

        private List<Tuple<string, PivotedProperties.SeriesGroup>> GetDatasetOptions()
        {
            var newDatasetOptions = new List<Tuple<string, PivotedProperties.SeriesGroup>>();
            if (Clusterer == null)
            {
                return newDatasetOptions;
            }
            if (Clusterer.RowHeaderLevels.Any())
            {
                newDatasetOptions.Add(Tuple.Create(
                    TextUtil.SpaceSeparate(Clusterer.RowHeaderLevels.Select(pd => pd.ColumnCaption.GetCaption(Localizer))),
                    (PivotedProperties.SeriesGroup)null));
            }

            foreach (var seriesGroup in Clusterer.Properties.PivotedProperties.SeriesGroups)
            {
                if (seriesGroup.PivotKeys.Count <= 1)
                {
                    continue;
                }

                string caption;
                var columnHeaders = Clusterer.Properties.GetColumnHeaders(seriesGroup).ToList();
                if (columnHeaders.Any())
                {
                    caption = TextUtil.SpaceSeparate(columnHeaders.Select(c => c.SeriesCaption.GetCaption(Localizer)));
                }
                else
                {
                    var values = seriesGroup.SeriesList.Where(series =>
                        Clusterer.Properties.GetColumnRole(series) is ClusterRole.Transform).ToList();
                    if (values.Any())
                    {
                        caption = TextUtil.SpaceSeparate(values.Select(v => v.SeriesCaption.GetCaption(Localizer)));
                    }
                    else
                    {
                        continue;
                    }
                }
                newDatasetOptions.Add(Tuple.Create(caption, seriesGroup));
            }

            return newDatasetOptions;
        }

        public void UpdateGraph()
        {
            if (Clusterer == null || _pcaChoice.DataSetIndex < 0 || _pcaChoice.DataSetIndex >= _datasetOptions.Count)
            {
                return;
            }
            UpdateTitle(Resources.PcaPlot_RefreshData_PCA_Plot);
            var seriesGroup = _datasetOptions[_pcaChoice.DataSetIndex].Item2;
            List<SymbolType> symbolTypes = new List<SymbolType>()
            {
                SymbolType.Square,
                SymbolType.Diamond,
                SymbolType.Triangle,
                SymbolType.Circle,
                SymbolType.XCross,
                SymbolType.Plus,
                SymbolType.Star,
                SymbolType.TriangleDown,
                SymbolType.HDash,
                SymbolType.VDash
            };

            zedGraphControl1.GraphPane.CurveList.Clear();
            var clusteredProperties = Clusterer.Properties;
            Dictionary<ImmutableList<HeaderLevel>, PointPairList> pointLists;
            int numberOfDimensions;
            string graphTitle;
            if (seriesGroup == null)
            {
                numberOfDimensions = clusteredProperties.RowValues.Count() +
                                     clusteredProperties.PivotedProperties.SeriesGroups.SelectMany(group =>
                                             group.SeriesList.Where(series =>
                                                 clusteredProperties.GetColumnRole(series) is ClusterRole.Transform))
                                         .Sum(series => series.PropertyIndexes.Count);
                _pcaChoice = _pcaChoice.ConstrainComponents(numberOfDimensions);
                pointLists = GetRowPointPairLists(_pcaChoice.XComponent, _pcaChoice.YComponent);
                graphTitle = Resources.PcaPlot_UpdateGraph_PCA_on_rows;
            }
            else
            {
                var valueSeriesList = seriesGroup.SeriesList.Where(series =>
                    Clusterer.Properties.GetColumnRole(series) is ClusterRole.Transform).ToList();
                numberOfDimensions = Clusterer.RowItems.Count * valueSeriesList.Count;
                _pcaChoice = _pcaChoice.ConstrainComponents(numberOfDimensions);
                pointLists = GetColumnPointPairLists(seriesGroup, _pcaChoice.XComponent, _pcaChoice.YComponent);
                graphTitle = TextUtil.SpaceSeparate(
                    valueSeriesList.Select(series => series.SeriesCaption.GetCaption(Localizer)));
                if (clusteredProperties.RowHeaders.Any())
                {
                    graphTitle += Resources.PcaPlot_UpdateGraph__Across_ +
                                  TextUtil.SpaceSeparate(
                                      clusteredProperties.RowHeaders.Select(pd => pd.ColumnCaption.GetCaption(Localizer)));
                }

            }
            numericUpDownYAxis.Maximum = numericUpDownXAxis.Maximum = numberOfDimensions;
            numericUpDownXAxis.Value = _pcaChoice.XComponent + 1;
            numericUpDownYAxis.Value = _pcaChoice.YComponent + 1;

            const int symbolHeaderLevel = 0;
            const int colorHeaderLevel = 1;
            var symbolObjects = new List<object>();
            foreach (var key in pointLists.Keys)
            {
                if (key.Count > symbolHeaderLevel)
                {
                    symbolObjects.Add(key[symbolHeaderLevel]);
                }
            }
            symbolObjects.Sort(CollectionUtil.ColumnValueComparer);

            foreach (var entry in pointLists)
            {
                var label = TextUtil.SpaceSeparate(entry.Key.Select(headerLevel => (headerLevel.Value ?? string.Empty).ToString()));
                SymbolType symbolType = SymbolType.Circle;
                if (entry.Key.Count > symbolHeaderLevel)
                {
                    symbolType = symbolTypes[symbolObjects.IndexOf(entry.Key[symbolHeaderLevel]) % symbolTypes.Count];
                }
                Color color = Color.Black;
                if (entry.Key.Count > colorHeaderLevel)
                {
                    color = entry.Key[colorHeaderLevel].Color;
                }

                var lineItem = new LineItem(label, entry.Value, color, symbolType)
                {
                    Tag = entry.Key
                };
                lineItem.Symbol.Fill = new Fill(color);
                lineItem.Line.IsVisible = false;
                zedGraphControl1.GraphPane.CurveList.Add(lineItem);
            }

            zedGraphControl1.GraphPane.Title.Text = graphTitle;
            zedGraphControl1.GraphPane.XAxis.Title.Text = string.Format(Resources.PcaPlot_UpdateGraph_Principal_Component__0_, _pcaChoice.XComponent + 1);
            zedGraphControl1.GraphPane.YAxis.Title.Text = string.Format(Resources.PcaPlot_UpdateGraph_Principal_Component__0_, _pcaChoice.YComponent + 1);
            zedGraphControl1.GraphPane.Legend.IsVisible = zedGraphControl1.GraphPane.CurveList.Count < 16;
            zedGraphControl1.GraphPane.AxisChange();
            zedGraphControl1.Invalidate();
        }

        private Dictionary<ImmutableList<HeaderLevel>, PointPairList> GetRowPointPairLists(int xAxisIndex, int yAxisIndex)
        {
            var pointLists = new Dictionary<ImmutableList<HeaderLevel>, PointPairList>();
            var results = Clusterer.PerformPcaOnRows(Math.Max(xAxisIndex, yAxisIndex) + 1);
            var cellLocator = CellLocator.ForRow(Clusterer.Properties.RowHeaders);
            for (int iRow = 0; iRow < results.ItemLabels.Count; iRow++)
            {
                var rowItem = results.ItemLabels[iRow];
                var headers = new List<HeaderLevel>();
                foreach (var pdHeader in Clusterer.Properties.RowHeaders)
                {
                    var objectValue = pdHeader.GetValue(rowItem);
                    headers.Add(new HeaderLevel(pdHeader.ColumnCaption, objectValue,
                        ColorScheme.GetColor(pdHeader, rowItem) ?? MISSING_COLOR));
                }

                var key = ImmutableList.ValueOf(headers);
                PointPairList pointPairList;
                if (!pointLists.TryGetValue(key, out pointPairList))
                {
                    pointPairList = new PointPairList();
                    pointLists.Add(key, pointPairList);
                }
                var pointInfo = new PointInfo(key);
                pointInfo = pointInfo.ChangeIdentityPath(cellLocator.GetSkylineDocNode(rowItem)?.IdentityPath)
                    .ChangeReplicateName(cellLocator.GetReplicate(rowItem)?.Name);
                var point = new PointPair(results.ItemComponents[iRow][xAxisIndex], results.ItemComponents[iRow][yAxisIndex])
                {
                    Tag = pointInfo
                }; 
                pointPairList.Add(point);
            }

            return pointLists;
        }

        private Dictionary<ImmutableList<HeaderLevel>, PointPairList> GetColumnPointPairLists(
            PivotedProperties.SeriesGroup seriesGroup, int xAxisIndex, int yAxisIndex)
        {
            var pointLists = new Dictionary<ImmutableList<HeaderLevel>, PointPairList>();

            var results = Clusterer.PerformPcaOnColumnGroup(seriesGroup, Math.Max(xAxisIndex, yAxisIndex) + 1);
            var headerLevels = Clusterer.Properties.GetColumnHeaders(seriesGroup).ToList();


            for (int iColumn = 0; iColumn < results.ItemComponents.Count; iColumn++)
            {
                var headers = new List<HeaderLevel>();
                foreach (var series in headerLevels)
                {
                    var pd = series.PropertyDescriptors[iColumn];
                    var objectValue = Clusterer.RowItems.Select(pd.GetValue).FirstOrDefault(value => null != value);
                    headers.Add(new HeaderLevel(series.SeriesCaption, objectValue, ColorScheme.GetColor(series, objectValue) ?? MISSING_COLOR));
                }
                var key = ImmutableList.ValueOf(headers);
                PointPairList pointPairList;
                if (!pointLists.TryGetValue(key, out pointPairList))
                {
                    pointPairList = new PointPairList();
                    pointLists.Add(key, pointPairList);
                }
                var pointInfo = new PointInfo(key);
                var cellLocator = CellLocator.ForColumn(headerLevels.Select(series => series.PropertyDescriptors[iColumn]).ToList(),
                    ImmutableList.Empty<DataPropertyDescriptor>());
                var rowItem = Clusterer.RowItems[0];
                pointInfo = pointInfo.ChangeIdentityPath(cellLocator.GetSkylineDocNode(rowItem)?.IdentityPath)
                    .ChangeReplicateName(cellLocator.GetReplicate(rowItem)?.Name);
                var pointPair = new PointPair(results.ItemComponents[iColumn][xAxisIndex],
                    results.ItemComponents[iColumn][yAxisIndex])
                {
                    Tag = pointInfo
                };

                pointPairList.Add(pointPair);
            }

            return pointLists;
        }

        public PcaChoice PcaChoiceValue
        {
            get
            {
                return _pcaChoice;
            }
            set
            {
                _pcaChoice = value;
                UpdateControls();
            }
        }

        private void numericUpDownX_ValueChanged(object sender, EventArgs e)
        {
            if (_inUpdateControls)
            {
                return;
            }

            PcaChoiceValue = PcaChoiceValue.ChangeXComponent((int) numericUpDownXAxis.Value - 1);
        }
        private void numericUpDownY_ValueChanged(object sender, EventArgs e)
        {
            if (_inUpdateControls)
            {
                return;
            }

            PcaChoiceValue = PcaChoiceValue.ChangeYComponent((int) numericUpDownYAxis.Value - 1);
        }

        public class HeaderLevel
        {
            public HeaderLevel(IColumnCaption seriesCaption, object headerValue, Color color)
            {
                Caption = seriesCaption;
                Value = headerValue;
                Color = color;
            }

            public IColumnCaption Caption { get; }
            public object Value { get; }
            public Color Color { get; }

            protected bool Equals(HeaderLevel other)
            {
                return Equals(Value, other.Value);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((HeaderLevel) obj);
            }

            public override int GetHashCode()
            {
                return (Value != null ? Value.GetHashCode() : 0);
            }
        }

        public class PointInfo : Immutable
        {
            public PointInfo(IEnumerable<HeaderLevel> headerLevels)
            {
                HeaderLevels = ImmutableList.ValueOf(headerLevels);
            }

            public ImmutableList<HeaderLevel> HeaderLevels { get; private set; }
            public IdentityPath IdentityPath { get; private set; }

            public PointInfo ChangeIdentityPath(IdentityPath identityPath)
            {
                return ChangeProp(ImClone(this), im => im.IdentityPath = identityPath);
            }
            public string ReplicateName { get; private set; }

            public PointInfo ChangeReplicateName(string replicateName)
            {
                return ChangeProp(ImClone(this), im => im.ReplicateName = replicateName);
            }
        }

        private void comboDataset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inUpdateControls)
            {
                return;
            }

            PcaChoiceValue = PcaChoiceValue.ChangeDataSetIndex(comboDataset.SelectedIndex);
        }

        private bool zedGraphControl1_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var pointInfo = PointInfoFromMousePoint(e.Location);
            if (pointInfo?.IdentityPath == null && pointInfo?.ReplicateName == null)
            {
                return false;
            }
            SkylineWindow?.SelectPathAndReplicate(pointInfo.IdentityPath, pointInfo.ReplicateName);
            return true;
        }

        private PointInfo PointInfoFromMousePoint(Point point)
        {
            if (!zedGraphControl1.GraphPane.FindNearestPoint(point, out CurveItem nearestCurve, out int iNearest))
            {
                return null;
            }

            if (iNearest < 0 || iNearest >= nearestCurve.Points.Count)
            {
                return null;
            }

            var pointInfo = nearestCurve.Points[iNearest].Tag as PointInfo;
            return pointInfo;
        }

        private bool zedGraphControl1_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var pointInfo = PointInfoFromMousePoint(e.Location);
            if (pointInfo?.IdentityPath == null && pointInfo?.ReplicateName == null)
            {
                return false;
            }

            sender.Cursor = Cursors.Hand;
            return true;
        }

        public override void RefreshData()
        {
            ClusterInput = DataboundGridControl?.CreateClusterInput() ?? ClusterInput;
            UpdateTitle(Resources.PcaPlot_RefreshData_PCA_Plot);
        }

        public override bool IsComplete
        {
            get
            {
                return base.IsComplete && _calculator.IsComplete;
            }
        }

        private class PcaCalculator : BoundGraphDataCalculator<ClusterInput, Tuple<Clusterer, ReportColorScheme>>
        {
            public PcaCalculator(PcaPlot pcaPlot) : base(CancellationToken.None, pcaPlot.zedGraphControl1)
            {
                PcaPlot = pcaPlot;
            }

            public PcaPlot PcaPlot
            {
                get;
            }

            protected override Tuple<Clusterer, ReportColorScheme> CalculateDataBoundResults(ClusterInput input, CancellationToken cancellationToken)
            {
                var resultsTuple = input.GetClusterResultsTuple(GetProgressHandler(cancellationToken));
                if (resultsTuple == null)
                {
                    return null;
                }

                var finalColorScheme = ReportColorScheme.FromClusteredResults(cancellationToken, resultsTuple.Item2);
                return Tuple.Create(resultsTuple.Item1, finalColorScheme);
            }

            protected override void ResultsAvailable()
            {
                PcaPlot.UpdateControls();
            }
        }

        public override PersistentString RestoreFromViewFile(SkylineWindow skylineWindow, PersistentString persistentString)
        {
            persistentString = base.RestoreFromViewFile(skylineWindow, persistentString);
            if (persistentString.Parts.Count >= 1)
            {
                PcaChoiceValue = PcaChoice.ParsePersistentString(persistentString.Parts[0]) ?? PcaChoice.EMPTY;
                persistentString = persistentString.Skip(1);
            }

            return persistentString;
        }

        protected override string GetPersistentString()
        {
            return PersistentString.Parse(base.GetPersistentString())
                .Append(PcaChoiceValue.ToPersistentStringPart())
                .ToString();
        }

        public class PcaChoice : Immutable
        {
            public static readonly PcaChoice EMPTY = new PcaChoice(0, 1, -1);

            public PcaChoice(int xComponent, int yComponent, int dataSetIndex)
            {
                XComponent = xComponent;
                YComponent = yComponent;
                DataSetIndex = dataSetIndex;
            }
            public int XComponent { get; private set; }

            public PcaChoice ChangeXComponent(int xComponent)
            {
                return ChangeProp(ImClone(this), im => im.XComponent = xComponent);
            }
            public int YComponent { get; private set; }

            public PcaChoice ChangeYComponent(int yComponent)
            {
                return ChangeProp(ImClone(this), im => im.YComponent = yComponent);
            }
            public int DataSetIndex { get; private set; }

            public PcaChoice ConstrainComponents(int numberOfDimensions)
            {
                if (XComponent < numberOfDimensions && YComponent < numberOfDimensions)
                {
                    return this;
                }

                return ChangeProp(ImClone(this), im =>
                {
                    im.XComponent = Math.Min(im.XComponent, numberOfDimensions - 1);
                    im.YComponent = Math.Min(im.YComponent, numberOfDimensions - 1);
                });
            }

            public PcaChoice ChangeDataSetIndex(int dataSetIndex)
            {
                return ChangeProp(ImClone(this), im => im.DataSetIndex = dataSetIndex);
            }

            public string ToPersistentStringPart()
            {
                return string.Join(@",", XComponent.ToString(CultureInfo.InvariantCulture),
                    YComponent.ToString(CultureInfo.InvariantCulture),
                    DataSetIndex.ToString(CultureInfo.InvariantCulture));
            }

            public static PcaChoice ParsePersistentString(string str)
            {
                var parts = str.Split(',');
                if (parts.Length != 3)
                {
                    return null;
                }

                var intParts = new List<int>();
                foreach (var part in parts)
                {
                    if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intPart))
                    {
                        return null;
                    }
                    intParts.Add(intPart);
                }
                return new PcaChoice(intParts[0], intParts[1], intParts[2]);
            }

            protected bool Equals(PcaChoice other)
            {
                return XComponent == other.XComponent && YComponent == other.YComponent && DataSetIndex == other.DataSetIndex;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((PcaChoice) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = XComponent;
                    hashCode = (hashCode * 397) ^ YComponent;
                    hashCode = (hashCode * 397) ^ DataSetIndex;
                    return hashCode;
                }
            }
        }
    }
}
