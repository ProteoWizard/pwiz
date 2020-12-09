using System;
using System.Collections.Generic;
using System.Diagnostics.PerformanceData;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public partial class PcaPlot : Form
    {
        private List<Tuple<string, PivotedProperties.SeriesGroup>> _datasetOptions;
        private bool _inUpdateControls;
        public PcaPlot()
        {
            InitializeComponent();
        }

        public Clusterer Clusterer { get; private set; }

        public ReportColorScheme ColorScheme { get; private set; }


        public void SetData(Clusterer clusterer, ReportColorScheme colorScheme)
        {
            Clusterer = clusterer;
            ColorScheme = colorScheme;
            UpdateControls();
        }

        public void UpdateControls()
        {
            bool wasInUpdateControls = _inUpdateControls;
            try
            {
                _inUpdateControls = true;
                var newDatasetOptions = new List<Tuple<string, PivotedProperties.SeriesGroup>>();
                if (Clusterer.RowHeaderLevels.Any())
                {
                    newDatasetOptions.Add(Tuple.Create(
                        TextUtil.SpaceSeparate(Clusterer.RowHeaderLevels.Select(pd => pd.ColumnCaption.ToString())),
                        (PivotedProperties.SeriesGroup) null));
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
                        caption = TextUtil.SpaceSeparate(columnHeaders.Select(c => c.SeriesCaption.ToString()));
                    }
                    else
                    {
                        var values = seriesGroup.SeriesList.Where(series =>
                            Clusterer.Properties.GetColumnRole(series) is ClusterRole.Transform).ToList();
                        if (values.Any())
                        {
                            caption = TextUtil.SpaceSeparate(values.Select(v => v.SeriesCaption.ToString()));
                        }
                        else
                        {
                            continue;
                        }
                    }
                    newDatasetOptions.Add(Tuple.Create(caption, seriesGroup));
                }

                int oldSelectedIndex = comboDataset.SelectedIndex;
                comboDataset.Items.Clear();
                foreach (var tuple in newDatasetOptions)
                {
                    comboDataset.Items.Add(tuple.Item1);
                }

                if (newDatasetOptions.Any())
                {
                    if (oldSelectedIndex < newDatasetOptions.Count)
                    {
                        comboDataset.SelectedIndex = oldSelectedIndex;
                    }
                    else
                    {
                        comboDataset.SelectedIndex = 0;
                    }
                }
                _datasetOptions = newDatasetOptions;
            }
            finally
            {
                {
                    _inUpdateControls = wasInUpdateControls;
                }
            }

            UpdateGraph();

        }

        public void UpdateGraph()
        {
            if (Clusterer == null || comboDataset.SelectedIndex < 0 || comboDataset.SelectedIndex >= _datasetOptions.Count)
            {
                return;
            }

            var seriesGroup = _datasetOptions[comboDataset.SelectedIndex].Item2;
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
            int xAxisIndex = (int) numericUpDownXAxis.Value - 1;
            int yAxisIndex = (int) numericUpDownYAxis.Value - 1;
            var clusteredProperties = Clusterer.Properties;
            var pointLists = new Dictionary<ImmutableList<HeaderLevel>, PointPairList>();
            int pcVectorsRequired = Math.Max(xAxisIndex, yAxisIndex) + 1;
            Color missingColor = Color.Black;
            int numberOfDimensions;
            string graphTitle;
            if (seriesGroup == null)
            {
                numberOfDimensions = clusteredProperties.RowValues.Count() +
                                     clusteredProperties.PivotedProperties.SeriesGroups.SelectMany(group =>
                                             group.SeriesList.Where(series =>
                                                 clusteredProperties.GetColumnRole(series) is ClusterRole.Transform))
                                         .Sum(series => series.PropertyIndexes.Count);
                var results = Clusterer.PerformPcaOnRows(pcVectorsRequired);
                for (int iRow = 0; iRow < results.ItemLabels.Count; iRow++)
                {
                    var rowItem = results.ItemLabels[iRow];
                    var headers = new List<HeaderLevel>();
                    foreach (var pdHeader in clusteredProperties.RowHeaders)
                    {
                        var objectValue = pdHeader.GetValue(rowItem);
                        headers.Add(new HeaderLevel(pdHeader.ColumnCaption, objectValue,
                            ColorScheme.GetColor(pdHeader, rowItem) ?? missingColor));
                    }

                    var key = ImmutableList.ValueOf(headers);
                    PointPairList pointPairList;
                    if (!pointLists.TryGetValue(key, out pointPairList))
                    {
                        pointPairList = new PointPairList();
                        pointLists.Add(key, pointPairList);
                    }
                    pointPairList.Add(results.ItemComponents[iRow][xAxisIndex], results.ItemComponents[iRow][yAxisIndex]);
                }

                graphTitle = "PCA on rows";
            }
            else
            {
                var valueSeriesList = seriesGroup.SeriesList.Where(series =>
                    Clusterer.Properties.GetColumnRole(series) is ClusterRole.Transform).ToList();
                numberOfDimensions = Clusterer.RowItems.Count * valueSeriesList.Count;

                var results = Clusterer.PerformPca(seriesGroup, pcVectorsRequired);
                var headerLevels = Clusterer.Properties.GetColumnHeaders(seriesGroup).ToList();


                for (int iColumn = 0; iColumn < results.ItemComponents.Count; iColumn++)
                {
                    var headers = new List<HeaderLevel>();
                    foreach (var series in headerLevels)
                    {
                        var pd = series.PropertyDescriptors[iColumn];
                        var objectValue = Clusterer.RowItems.Select(pd.GetValue).FirstOrDefault(value => null != value);
                        headers.Add(new HeaderLevel(series.SeriesCaption, objectValue, ColorScheme.GetColor(series, objectValue) ?? missingColor));
                    }
                    var key = ImmutableList.ValueOf(headers);
                    PointPairList pointPairList;
                    if (!pointLists.TryGetValue(key, out pointPairList))
                    {
                        pointPairList = new PointPairList();
                        pointLists.Add(key, pointPairList);
                    }
                    pointPairList.Add(results.ItemComponents[iColumn][xAxisIndex], results.ItemComponents[iColumn][yAxisIndex]);
                }
                graphTitle = TextUtil.SpaceSeparate(
                    valueSeriesList.Select(series => series.SeriesCaption.GetCaption(DataSchemaLocalizer.INVARIANT)));
                if (clusteredProperties.RowHeaders.Any())
                {
                    graphTitle += " Across " +
                                  TextUtil.SpaceSeparate(
                                      clusteredProperties.RowHeaders.Select(pd => pd.ColumnCaption.ToString()));
                }

            }
            numericUpDownYAxis.Maximum = numericUpDownXAxis.Maximum = numberOfDimensions;

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

                var lineItem = new LineItem(label, entry.Value, color, symbolType);
                lineItem.Symbol.Fill = new Fill(color);
                lineItem.Line.IsVisible = false;
                zedGraphControl1.GraphPane.CurveList.Add(lineItem);
            }

            zedGraphControl1.GraphPane.Title.Text = graphTitle;
            zedGraphControl1.GraphPane.XAxis.Title.Text = "Principle Component " + (xAxisIndex + 1);
            zedGraphControl1.GraphPane.YAxis.Title.Text = "Principle Component " + (yAxisIndex + 1);
            zedGraphControl1.GraphPane.Legend.IsVisible = zedGraphControl1.GraphPane.CurveList.Count < 16;
            zedGraphControl1.GraphPane.AxisChange();
            zedGraphControl1.Invalidate();
        }

        private void numericUpDown_ValueChanged(object sender, EventArgs e)
        {
            UpdateGraph();
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

        private void comboDataset_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inUpdateControls)
            {
                return;
            }
            UpdateGraph();
        }
    }
}
