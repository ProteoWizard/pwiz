/*
 * Original author: Eduardo Armendariz <wardough .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Drawing;
using ZedGraph;
using pwiz.Common.Graph;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// HiLowBarItem with a tick mark, whiskers and outliers. 
    /// </summary>
    [CurveDataHandler(typeof(BoxPlotBarItemDataHandler))]
    public class BoxPlotBarItem : HiLowBarItem
    {
        public static PointPair MakePointPair(double xValue, double highValue, double lowValue,
            double middleValue, double maxValue, double minValue, double[] outliers)
        {
            return new PointPair(xValue, highValue, lowValue)
                { Tag = new BoxPlotTag(middleValue, minValue, maxValue, outliers) };
        }

        public static PointPairList MakePointPairList(double[] xValues, double[] highValues, double[] lowValues,
            double[] middleValues, double[] maxValues, double[] minValues, double[][] outliers)
        {
            PointPairList pointPairList = new PointPairList(xValues, highValues, lowValues);
            for (int i = 0; i < middleValues.Length; i++)
            {
                pointPairList[i].Tag = new BoxPlotTag(middleValues[i], minValues[i], maxValues[i], outliers[i]);
            }
            return pointPairList;
        }

        public BoxPlotBarItem(String label,
            double[] xValues, double[] highValues, double[] lowValues,
            double[] middleValues, double[] maxValues, double[] minValues, double[][] outliers, 
            Color color, Color middleColor)
            : this(label, MakePointPairList(xValues, highValues, lowValues, middleValues, maxValues, minValues, outliers), color, middleColor)
        {
        }

        public BoxPlotBarItem(String label, IPointList pointPairList, Color color, Color middleColor) : base(label,
            pointPairList, color)
        {
            _bar = new BoxPlotBar(color, middleColor);
            _bar.Fill.Type = FillType.Solid;
        }
    }

    public class BoxPlotBar : Bar
    {
 
        public BoxPlotBar(Color color, Color middleColor)
            : base(color)
        {
            // Pens scale better in EMF files
            MiddlePen = new Pen(middleColor);
        }

        public Pen MiddlePen { get; private set; }

        protected override void DrawSingleBar(Graphics g, GraphPane pane, CurveItem curve,
            int index, int pos, Axis baseAxis, Axis valueAxis, float barWidth, float scaleFactor)
        {
            base.DrawSingleBar(g, pane, curve, index, pos, baseAxis, valueAxis, barWidth, scaleFactor);

            PointPair pointPair = curve.Points[index];
            if (pointPair.IsInvalid || !(pointPair.Tag is BoxPlotTag boxPlotTag))
                return;

            // Find X where to place to whiskers and outliers
            double curBase;
            ValueHandler valueHandler = new ValueHandler(pane, false);
            valueHandler.GetValues(curve, index, out curBase, out _, out _);
            float pixBase = baseAxis.Scale.Transform(curve.IsOverrideOrdinal, index, curBase);
            float clusterWidth = pane.BarSettings.GetClusterWidth();
            float clusterGap = pane.BarSettings.MinClusterGap * barWidth;
            float barGap = barWidth * pane.BarSettings.MinBarGap;
            float pixSide = pixBase - clusterWidth / 2.0F + clusterGap / 2.0F + pos * (barWidth + barGap);
            float pixX = (float)Math.Round(pixSide + barWidth / 2);

            // Find edges of whiskers
            double median = boxPlotTag.Median;
            double min = boxPlotTag.Min;
            double max = boxPlotTag.Max;
            float pixBoxLeft = pixX - barWidth / 2;
            float pixBoxRight = pixX + barWidth / 2;
            float pixMin = valueAxis.Scale.Transform(min);
            float pixMax = valueAxis.Scale.Transform(max);
            float pixLow = valueAxis.Scale.Transform(pointPair.Z);
            float pixHigh = valueAxis.Scale.Transform(pointPair.Y);
            float pixMedian = valueAxis.Scale.Transform(median);

            // Draw whisker lines
            g.DrawLine(MiddlePen, pixX, pixMax, pixX, pixHigh);
            g.DrawLine(MiddlePen, pixX, pixMin, pixX, pixLow);

            // Draw caps
            float capHalf = barWidth / 2;
            g.DrawLine(MiddlePen, pixX - capHalf, pixMax, pixX + capHalf, pixMax); // top
            g.DrawLine(MiddlePen, pixX - capHalf, pixMin, pixX + capHalf, pixMin); // bottom

            // Draw median line inside the box
            g.DrawLine(MiddlePen, pixBoxLeft, pixMedian, pixBoxRight, pixMedian);

            // Draw outliers
            var symbol = CreateOutlierSymbol(barWidth / pane.CalcScaleFactor());
            foreach (double outlier in boxPlotTag.Outliers)
            {
                float y = valueAxis.Scale.Transform(outlier);
                symbol.DrawSymbol(g, pane, (int)Math.Round(pixX), (int)Math.Round(y), scaleFactor, true, pointPair);
            }
        }

        private Symbol CreateOutlierSymbol(float size)
        {
            return new Symbol(SymbolType.Circle, Color.FromArgb(0, 0, 0, 0))
            {
                Size = size,
                Fill = { Type = FillType.Solid, Color = Color.FromArgb(0, 0, 0, 0) },
                Border = { Color = Color.Black, Width = 1.5f, IsVisible = true }
            };
        }
    }

    internal class BoxPlotTag
    {
        public BoxPlotTag(double median, double min, double max, double[] outliers)
        {
            Median = median;
            Min = min;
            Max = max;
            Outliers = outliers;
        }

        public double Median { get; private set; }

        public double Min { get; private set; }

        public double Max { get; private set; }

        public double[] Outliers { get; private set; }

        public override string ToString()
        {
            return string.Format(@"{0}-{1}-{2}-{3}", Min, Median, Max, Outliers);
        }
    }

    //TODO: This is needed for when you right-click on the graph and choose "Copy Data"
    public class BoxPlotBarItemDataHandler : CurveDataHandler
    {
        protected override DataFrameBuilder AddColumns(DataFrameBuilder dataFrameBuilder)
        {
            dataFrameBuilder = AddColumnForAxis(dataFrameBuilder, dataFrameBuilder.BaseAxis);
            var points = dataFrameBuilder.Points;
            var apexes = new double?[points.Count];
            var starts = new double?[points.Count];
            var fwhms = new double?[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.IsMissing)
                {
                    continue;
                }
                starts[i] = point.Z;
                var middleErrorTag = point.Tag as BoxPlotTag;
                if (middleErrorTag != null)
                {
                    //apexes[i] = middleErrorTag.Middle;
                    //fwhms[i] = middleErrorTag.Error;
                }
            }
            var dataFrame = new DataFrame(dataFrameBuilder.ValueAxis.Title.Text, dataFrameBuilder.Points.Count);
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"Apex", apexes));
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"Start", starts));
            dataFrame = dataFrame.AddColumn(GetColumnForAxis(dataFrameBuilder, dataFrameBuilder.ValueAxis).SetTitle(@"End"));
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"FWHM", fwhms));
            dataFrameBuilder = dataFrameBuilder.AddColumn(dataFrame);
            return dataFrameBuilder;
        }
    }

    public class BoxPlotData
    {
        public BoxPlotData(string label, double min, double q1, double median, double q3, double max, double[] outliers)
        {
            Label = label;
            Min = min;
            Q1 = q1;
            Median = median;
            Q3 = q3;
            Max = max;
            Outliers = outliers;
        }

        public string Label { get; set; }
        public double Min { get; set; }
        public double Q1 { get; set; }
        public double Median { get; set; }
        public double Q3 { get; set; }
        public double Max { get; set; }
        public double[] Outliers { get; set; }
    }

    public class BoxPlotDataUtil
    {
        public static BoxPlotData buildBoxPlotData(double[] dataPoints, string label)
        {
            var log2Values = dataPoints
                .Select(v => Math.Log(v + 1, 2))
                .OrderBy(x => x)
                .ToList();

            if (log2Values.IsNullOrEmpty())
            {
                return null;
            }

            int count = log2Values.Count;

            double Median(List<double> data)
            {
                int n = data.Count;
                if (n % 2 == 0)
                    return (data[n / 2 - 1] + data[n / 2]) / 2.0;
                else
                    return data[n / 2];
            }

            var lowerHalf = log2Values.Take(count / 2).ToList();
            var upperHalf = log2Values.Skip((count + 1) / 2).ToList();

            double q1 = Median(lowerHalf);
            double median = Median(log2Values);
            double q3 = Median(upperHalf);
            double iqr = q3 - q1;

            double lowerWhisker = log2Values.Where(x => x >= q1 - 1.5 * iqr).Min();
            double upperWhisker = log2Values.Where(x => x <= q3 + 1.5 * iqr).Max();

            var outliers = log2Values
                .Where(x => x < lowerWhisker || x > upperWhisker)
                .ToArray();

            return new BoxPlotData(label: label, min: lowerWhisker, q1: q1, median: median, q3: q3, max: upperWhisker, outliers: outliers);
        }
    }

}
