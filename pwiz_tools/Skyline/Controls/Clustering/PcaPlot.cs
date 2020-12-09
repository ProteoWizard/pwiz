using System;
using System.Collections.Generic;
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
            UpdateGraph();
        }

        public void UpdateGraph()
        {
            if (Clusterer == null)
            {
                return;
            }
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
            var seriesGroup =
                clusteredProperties.PivotedProperties.SeriesGroups.FirstOrDefault(group => group.PivotKeys.Count > 1);
            if (seriesGroup == null)
            {
                return;
            }
            var results = Clusterer.PerformPca(seriesGroup, Math.Max(xAxisIndex, yAxisIndex) + 1);
            var valueSeriesList = seriesGroup.SeriesList.Where(series =>
                Clusterer.Properties.GetColumnRole(series) is ClusterRole.Transform).ToList();
            var headerLevels = Clusterer.Properties.GetColumnHeaders(seriesGroup).ToList();
            var numberOfDimensions = Clusterer.RowItems.Count * valueSeriesList.Count;
            numericUpDownYAxis.Maximum = numericUpDownXAxis.Maximum = numberOfDimensions;
            
            const int symbolHeaderLevel = 0;
            const int colorHeaderLevel = 1;

            var pointLists = new Dictionary<ImmutableList<object>, PointPairList>();
            for (int iColumn = 0; iColumn < results.ItemComponents.Count; iColumn++)
            {
                var headerValues = new List<object>();
                for (int headerLevel = 0; headerLevel < 2; headerLevel++)
                {
                    object headerValue = null;
                    var series = headerLevels.Skip(headerLevel).FirstOrDefault();
                    if (series != null)
                    {
                        var pd = clusteredProperties.PivotedProperties.ItemProperties[series.PropertyIndexes[iColumn]];
                        headerValue = Clusterer.RowItems.Select(pd.GetValue).FirstOrDefault(value => null != value);
                    }
                    headerValues.Add(headerValue);
                }

                var key = ImmutableList.ValueOf(headerValues);
                PointPairList pointPairList;
                if (!pointLists.TryGetValue(key, out pointPairList))
                {
                    pointPairList = new PointPairList();
                    pointLists.Add(key, pointPairList);
                }
                pointPairList.Add(results.ItemComponents[iColumn][xAxisIndex], results.ItemComponents[iColumn][yAxisIndex]);
            }

            var symbolObjects = pointLists.Keys.Select(key => key[symbolHeaderLevel]).OrderBy(v => v).ToList();
            foreach (var entry in pointLists)
            {
                var label = TextUtil.SpaceSeparate(entry.Key.Select(o => (o ?? string.Empty).ToString()));
                var symbolType = symbolTypes[symbolObjects.IndexOf(entry.Key[symbolHeaderLevel]) % symbolTypes.Count];
                Color color = Color.Black;
                var colorSeries = headerLevels.Skip(colorHeaderLevel).FirstOrDefault();
                if (colorSeries != null)
                {
                    color = ColorScheme.GetColor(colorSeries, entry.Key[colorHeaderLevel]) ?? color;
                }

                var lineItem = new LineItem(label, entry.Value, color, symbolType);
                lineItem.Symbol.Fill = new Fill(color);
                lineItem.Line.IsVisible = false;
                zedGraphControl1.GraphPane.CurveList.Add(lineItem);
            }

            string graphTitle = TextUtil.SpaceSeparate(
                valueSeriesList.Select(series => series.SeriesCaption.GetCaption(DataSchemaLocalizer.INVARIANT)));
            if (clusteredProperties.RowHeaders.Any())
            {
                graphTitle += " Across " +
                              TextUtil.SpaceSeparate(
                                  clusteredProperties.RowHeaders.Select(pd => pd.ColumnCaption.ToString()));
            }
            zedGraphControl1.GraphPane.Title.Text = graphTitle;
            zedGraphControl1.GraphPane.XAxis.Title.Text = "Principle Component " + (xAxisIndex + 1);
            zedGraphControl1.GraphPane.YAxis.Title.Text = "Principle Component " + (yAxisIndex + 1);

            zedGraphControl1.GraphPane.AxisChange();
            zedGraphControl1.Invalidate();
        }

        private void numericUpDown_ValueChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        class HeaderLevel
        {
            public HeaderLevel(Color color, object value)
            {
                Color = color;
                Value = value;
            }

            public Color Color { get; }
            public object Value { get; }

            public string Label
            {
                get { return (Value ?? string.Empty).ToString(); }
            }
        }
    }
}
