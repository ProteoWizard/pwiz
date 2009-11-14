using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphRegression : Form
    {
        public static readonly Color COLOR_REGRESSION = Color.DarkBlue;
        public static readonly Color COLOR_LINE_REGRESSION = Color.Black;
        public static readonly Color COLOR_LINE_REGRESSION_CURRENT = Color.DarkGray;

        public GraphRegression(IEnumerable<RegressionGraphData> regressionGraphDatas)
        {
            InitializeComponent();

            var masterPane = graphControl.MasterPane;
            masterPane.PaneList.Clear();

            foreach (var graphData in regressionGraphDatas)
            {
                var regressionPane = new GraphPane();
                regressionPane.Title.Text = graphData.Title;
                regressionPane.XAxis.Title.Text = graphData.LabelX;
                regressionPane.YAxis.Title.Text = graphData.LabelY;
                regressionPane.Border.IsVisible = false;
                regressionPane.Title.IsVisible = true;
                regressionPane.Chart.Border.IsVisible = false;
                regressionPane.XAxis.Scale.MaxAuto = true;
                regressionPane.XAxis.Scale.MinAuto = true;
                regressionPane.YAxis.Scale.MaxAuto = true;
                regressionPane.YAxis.Scale.MinAuto = true;
                regressionPane.Y2Axis.IsVisible = false;
                regressionPane.X2Axis.IsVisible = false;
                regressionPane.XAxis.MajorTic.IsOpposite = false;
                regressionPane.YAxis.MajorTic.IsOpposite = false;
                regressionPane.XAxis.MinorTic.IsOpposite = false;
                regressionPane.YAxis.MinorTic.IsOpposite = false;
                regressionPane.IsFontsScaled = false;
                regressionPane.YAxis.Scale.MaxGrace = 0.1;

                var curve = regressionPane.AddCurve("Values", graphData.XValues, graphData.YValues,
                                               Color.Black, SymbolType.Diamond);
                curve.Line.IsVisible = false;
                curve.Symbol.Border.IsVisible = false;
                curve.Symbol.Fill = new Fill(COLOR_REGRESSION);

                // Find maximum points for drawing the regression line
                var lineX = new[] { double.MaxValue, double.MinValue };
                var lineY = new[] { double.MaxValue, double.MinValue };

                for (int i = 0; i < graphData.XValues.Length; i++)
                {
                    double xValue = graphData.XValues[i];
                    double yValue = graphData.YValues[i];
                    if (xValue < lineX[0])
                    {
                        lineX[0] = xValue;
                        lineY[0] = yValue;
                    }
                    if (xValue > lineX[1])
                    {
                        lineX[1] = xValue;
                        lineY[1] = yValue;
                    }
                }

                // Recalculate the y values based on the maximum x values
                // and the regression.
                lineY[0] = graphData.RegressionLine.GetY(lineX[0]);
                lineY[1] = graphData.RegressionLine.GetY(lineX[1]);

                curve = regressionPane.AddCurve("Regression", lineX, lineY, COLOR_LINE_REGRESSION);
                curve.Line.IsAntiAlias = true;
                curve.Line.IsOptimizedDraw = true;

                if (graphData.RegressionLineCurrent != null)
                {
                    lineY[0] = graphData.RegressionLineCurrent.GetY(lineX[0]);
                    lineY[1] = graphData.RegressionLineCurrent.GetY(lineX[1]);

                    curve = regressionPane.AddCurve("Current", lineX, lineY, COLOR_LINE_REGRESSION_CURRENT);
                    curve.Line.IsAntiAlias = true;
                    curve.Line.IsOptimizedDraw = true;
                }

                Statistics statsX = new Statistics(graphData.XValues);
                Statistics statsY = new Statistics(graphData.YValues);
                double slope = statsY.Slope(statsX);
                double intercept = statsY.Intercept(statsX);

                var label = string.Format("slope = {0:F04}, intercept = {1:F04}\n" +
                                          "r = {2:F02}",
                                          slope,
                                          intercept,
                                          statsY.R(statsX));

                // Setup axes scales to enable the ReverseTransform method
                double xLabel = lineX[0] - 0.1*(lineX[1] - lineX[0]);
                double yLabel = lineY[1] + 0.5*(lineY[1] - lineY[0]);
                TextObj text = new TextObj(label, xLabel, yLabel,
                                           CoordType.AxisXYScale, AlignH.Left, AlignV.Top)
                                   {
                                       IsClippedToChartRect = true,
                                       ZOrder = ZOrder.E_BehindCurves,
                                       FontSpec = GraphSummary.CreateFontSpec(COLOR_LINE_REGRESSION),
                                   };
                regressionPane.GraphObjList.Add(text);

                masterPane.PaneList.Add(regressionPane);
            }

            // Tell ZedGraph to auto layout all the panes
            using (Graphics g = CreateGraphics())
            {
                masterPane.SetLayout(g, PaneLayout.SingleColumn);
                graphControl.AxisChange();
            }
        }
    }

    public class RegressionGraphData
    {
        public string Title { get; set; }
        public string LabelX { get; set; }
        public string LabelY { get; set; }
        public double[] XValues { get; set; }
        public double[] YValues { get; set; }
        public RegressionLine RegressionLine { get; set; }
        public RegressionLine RegressionLineCurrent { get; set; }
    }
}
