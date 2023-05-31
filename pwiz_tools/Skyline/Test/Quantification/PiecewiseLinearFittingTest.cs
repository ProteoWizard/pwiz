using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTest.Quantification
{
    [TestClass]
    public class PiecewiseLinearFittingTest : AbstractUnitTest
    {
        Tuple<double, double>[] points = new[]
        {
            Tuple.Create(0.005, 1.64867E-06),
            Tuple.Create(0.005, 3.46991E-06),
            Tuple.Create(0.005, 3.11628E-07),
            Tuple.Create(0.01, 3.52311E-06),
            Tuple.Create(0.01, 2.98539E-06),
            Tuple.Create(0.01, 1.69306E-06),
            Tuple.Create(0.03, 1.32789E-06),
            Tuple.Create(0.03, 2.93961E-06),
            Tuple.Create(0.03, 2.02407E-06),
            Tuple.Create(0.05, 1.40259E-06),
            Tuple.Create(0.05, 3.29722E-06),
            Tuple.Create(0.05, 2.17824E-06),
            Tuple.Create(0.07, 3.71022E-06),
            Tuple.Create(0.07, 2.78271E-06),
            Tuple.Create(0.07, 4.32119E-07),
            Tuple.Create(0.1, 3.18625E-06),
            Tuple.Create(0.1, 3.29426E-06),
            Tuple.Create(0.1, 1.89346E-06),
            Tuple.Create(0.3, 5.48088E-06),
            Tuple.Create(0.3, 5.73241E-06),
            Tuple.Create(0.3, 3.5879E-06),
            Tuple.Create(0.5, 7.44818E-06),
            Tuple.Create(0.5, 7.14181E-06),
            Tuple.Create(0.5, 5.34057E-06),
            Tuple.Create(0.7, 1.10748E-05),
            Tuple.Create(0.7, 1.20063E-05),
            Tuple.Create(0.7, 9.19692E-06),
            Tuple.Create(1.0, 1.28049E-05),
            Tuple.Create(1.0, 1.10805E-05),
            Tuple.Create(1.0, 1.03991E-05)
        };


        [TestMethod]
        public void TestPiecewiseLinearFitting()
        {
            var weightedPoints = points.Select(p => new WeightedPoint(p.Item1, p.Item2, 1 / p.Item1 / p.Item1)).ToList();
            foreach (var xOffset in weightedPoints.Select(pt => pt.X).Distinct().OrderBy(x => x))
            {
                var scoredBilinearCurve = ScoredBilinearCurve.WithOffset(xOffset, weightedPoints);
                var image = DisplayCurve(xOffset, weightedPoints, scoredBilinearCurve);
                var path = Path.Combine("d:\\test\\poster", "PiecewiseLinearFittingTest_" + (xOffset  * 1000).ToString("0000") + ".png");
                image.Save(path, ImageFormat.Png);
                Console.Out.WriteLine("XOffset: {0} Curve: {1} Error: {2}", xOffset, scoredBilinearCurve.CalibrationCurve, scoredBilinearCurve.Error);
            }
        }

        private Image DisplayCurve(double xOffset, IList<WeightedPoint> weightedPoints, ScoredBilinearCurve scoredBilinearCurve)
        {
            var xMin = weightedPoints.Min(pt => pt.X);
            var xMax = weightedPoints.Max(pt => pt.X);
            var symbolSize = 16;
            var lineWidth = 6;

            var metrics = scoredBilinearCurve.CalibrationCurve.GetMetrics(weightedPoints);
            var zedGraphControl = new ZedGraphControl();
            zedGraphControl.MasterPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;
            zedGraphControl.GraphPane.Title.Text = "Candidate Calibration Curve: Linear portion is where x > " + xOffset;
            zedGraphControl.GraphPane.Title.FontSpec.Size = 24f;
            zedGraphControl.GraphPane.IsFontsScaled = false;
            zedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.Legend.FontSpec.Size = 18f;

            var linearPoints = new PointPairList(weightedPoints.Where(pt => pt.X > xOffset)
                .Select(pt => new PointPair(pt.X, pt.Y)).ToList());
            if (linearPoints.Count > 0)
            {
                var linearPointsCurve =
                    new LineItem("Points in linear range", linearPoints, Color.Green, SymbolType.Circle, lineWidth);
                linearPointsCurve.Symbol.Fill = new Fill(Color.Green);
                linearPointsCurve.Symbol.Size = symbolSize;
                linearPointsCurve.Line.IsVisible = false;
                zedGraphControl.GraphPane.CurveList.Add(linearPointsCurve);
            }
            var noisePoints = new PointPairList(weightedPoints.Where(pt => pt.X <= xOffset)
                .Select(pt => new PointPair(pt.X, pt.Y)).ToList());
            if (noisePoints.Count > 0)
            {
                var noisePointsCurve =
                    new LineItem("Points in noise range", noisePoints, Color.DarkOrange, SymbolType.Diamond, lineWidth);
                noisePointsCurve.Symbol.Fill = new Fill(Color.DarkOrange);
                noisePointsCurve.Symbol.Size = symbolSize;
                noisePointsCurve.Line.IsVisible = false;
                zedGraphControl.GraphPane.CurveList.Add(noisePointsCurve);
            }

            if (scoredBilinearCurve.CalibrationCurve is CalibrationCurve.Bilinear bilinear)
            {
                var turningPointCurve = new LineItem("Turning point of calibration curve",
                    new PointPairList(new[] { new PointPair(bilinear.TurningPoint, bilinear.GetY(bilinear.TurningPoint)) }), Color.Blue,
                    SymbolType.Square, lineWidth);
                turningPointCurve.Symbol.Fill = new Fill(Color.Blue);
                turningPointCurve.Symbol.Size = symbolSize;
                turningPointCurve.Line.IsVisible = false;
                zedGraphControl.GraphPane.CurveList.Add(turningPointCurve);

                var height = bilinear.GetY(bilinear.TurningPoint);


                var linearCurvePoints = new PointPairList();
                const int nPts = 10000;
                var xLinearMin = linearPoints.Select(pt => pt.X).Append(bilinear.TurningPoint).Min();
                for (int iPt = 0; iPt < nPts; iPt++)
                {
                    double x = (xLinearMin * (nPts - iPt) + xMax * iPt) / nPts;
                    double y = bilinear.GetLinearCalibrationCurve().GetY(x);
                    linearCurvePoints.Add(x, y);
                }

                var linearCurve = new LineItem("Linear part of calibration curve", linearCurvePoints, Color.Black,
                    SymbolType.None, lineWidth);
                linearCurve.Line.Style = DashStyle.Solid;
                zedGraphControl.GraphPane.CurveList.Add(linearCurve);

                var horizontalCurve = new LineItem("Horizontal part of calibration curve",
                    new PointPairList(new[]
                        { new PointPair(xMin, height), new PointPair(bilinear.TurningPoint, height) }), Color.Black,
                    SymbolType.None, lineWidth);
                horizontalCurve.Line.Style = DashStyle.Dot;
                zedGraphControl.GraphPane.CurveList.Add(horizontalCurve);
            }

            // var points = new PointPairList(weightedPoints.Select(pt => pt.X).ToList(),
            //     weightedPoints.Select(pt => pt.Y).ToList());
            // var pointsCurve = new LineItem(null, points, SampleType.STANDARD.Color, SampleType.STANDARD.SymbolType);
            // pointsCurve.Symbol.Fill = new Fill(SampleType.STANDARD.Color);
            // pointsCurve.Symbol.Size = 20;
            // pointsCurve.Line.IsVisible = false;
            // zedGraphControl.GraphPane.CurveList.Add(pointsCurve);
            // var calibrationPoints = new PointPairList();
            // // const int nPts = 10000;
            // // for (int iPt = 0; iPt < nPts; iPt++)
            // // {
            // //     double x = (xMin * (nPts - iPt) + xMax * iPt) / nPts;
            // //     double y = scoredBilinearCurve.CalibrationCurve.GetY(x);
            // //     calibrationPoints.Add(x, y);
            // // }
            //
            // var calibrationCurve = new LineItem(null, calibrationPoints, Color.Black, SymbolType.None, 2);
            // zedGraphControl.GraphPane.CurveList.Add(calibrationCurve);
            zedGraphControl.GraphPane.XAxis.Scale.Min = xMin * .9;
            zedGraphControl.GraphPane.XAxis.Scale.Max = xMax * 1.1;
            zedGraphControl.GraphPane.XAxis.Type = AxisType.Log;
            zedGraphControl.GraphPane.XAxis.Title.Text = "Analyte Concentration";
            zedGraphControl.GraphPane.XAxis.Title.FontSpec.Size = 24f;
            var allPoints =
                zedGraphControl.GraphPane.CurveList.SelectMany(curve =>
                    Enumerable.Range(0, curve.Points.Count).Select(index => curve.Points[index])).ToList();

            double yMin = allPoints.Min(pt => pt.Y);
            double yMax = allPoints.Max(pt => pt.Y);
            zedGraphControl.GraphPane.YAxis.Scale.Min = yMin * .9;
            zedGraphControl.GraphPane.YAxis.Scale.Max = yMax * 1.1;
            zedGraphControl.GraphPane.YAxis.Type = AxisType.Log;
            zedGraphControl.GraphPane.YAxis.Title.Text = "Peak Area";
            zedGraphControl.GraphPane.YAxis.Title.FontSpec.Size = 24f;
            List<string> labelLines = new List<string>
            {
                metrics.ToString(),
                "Weighted sum of squares deviation from observed points: " + (scoredBilinearCurve.Error * 1e12).ToString("0.0000")
            };
            TextObj text = new TextObj(TextUtil.LineSeparate(labelLines), .01, 0,
                CoordType.ChartFraction, AlignH.Left, AlignV.Top)
            {
                IsClippedToChartRect = true,
                ZOrder = ZOrder.E_BehindCurves,
            };
            text.FontSpec.Border.IsVisible = false;
            text.FontSpec.StringAlignment = StringAlignment.Near;
            text.FontSpec.Size = 24f;

            zedGraphControl.GraphPane.GraphObjList.Add(text);
            zedGraphControl.AxisChange();
            return zedGraphControl.MasterPane.GetImage(1000, 1000, 96);
        }

        private ZedGraphControl CreateZedGraphControl()
        {
            var zedGraphControl = new ZedGraphControl();
            zedGraphControl.MasterPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;
            zedGraphControl.GraphPane.Title.FontSpec.Size = 24f;
            zedGraphControl.GraphPane.IsFontsScaled = false;
            zedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.Legend.FontSpec.Size = 18f;
            zedGraphControl.GraphPane.XAxis.Type = AxisType.Log;
            zedGraphControl.GraphPane.XAxis.Title.Text = "Analyte Concentration";

        }
    }
}
