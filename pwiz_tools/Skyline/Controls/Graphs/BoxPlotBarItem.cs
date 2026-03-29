/*
 * Original author: Eduardo Armendariz <wardough .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Graph;
using ZedGraph;

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
                { Tag = new BoxPlotTag(lowValue, middleValue, highValue, minValue, maxValue, outliers) };
        }

        public BoxPlotBarItem(string label, IPointList pointPairList, Color color, Color middleColor)
            : base(label, pointPairList, color)
        {
            _bar = new BoxPlotBar(color, middleColor);
            _bar.Fill.Type = FillType.Solid;
        }
    }

    public class BoxPlotBar : Bar
    {
        public BoxPlotBar(Color color, Color middleColor) : base(color)
        {
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

            // Find X position for whiskers and outliers
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
            float pixBoxLeft = pixX - barWidth / 2;
            float pixBoxRight = pixX + barWidth / 2;
            float pixMin = valueAxis.Scale.Transform(boxPlotTag.Min);
            float pixMax = valueAxis.Scale.Transform(boxPlotTag.Max);
            float pixLow = valueAxis.Scale.Transform(pointPair.Z);
            float pixHigh = valueAxis.Scale.Transform(pointPair.Y);
            float pixMedian = valueAxis.Scale.Transform(boxPlotTag.Median);

            // Draw whisker lines
            g.DrawLine(MiddlePen, pixX, pixMax, pixX, pixHigh);
            g.DrawLine(MiddlePen, pixX, pixMin, pixX, pixLow);

            // Draw caps
            float capHalf = barWidth / 2;
            g.DrawLine(MiddlePen, pixX - capHalf, pixMax, pixX + capHalf, pixMax);
            g.DrawLine(MiddlePen, pixX - capHalf, pixMin, pixX + capHalf, pixMin);

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

    public class BoxPlotTag
    {
        public BoxPlotTag(double q1, double median, double q3, double min, double max, double[] outliers)
        {
            Q1 = q1;
            Median = median;
            Q3 = q3;
            Min = min;
            Max = max;
            Outliers = outliers;
        }

        public double Q1 { get; private set; }
        public double Median { get; private set; }
        public double Q3 { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double[] Outliers { get; private set; }
    }

    public static class BoxPlotStatistics
    {
        /// <summary>
        /// Compute box plot statistics from pre-sorted values.
        /// Quartile lookup is O(1), outlier scan is O(n).
        /// </summary>
        public static BoxPlotTag ComputeBoxPlot(double[] sortedValues)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return null;

            int count = sortedValues.Length;
            double median = GetMedian(sortedValues, 0, count);
            double q1 = GetMedian(sortedValues, 0, count / 2);
            double q3 = GetMedian(sortedValues, (count + 1) / 2, count);
            double iqr = q3 - q1;

            double lowerFence = q1 - 1.5 * iqr;
            double upperFence = q3 + 1.5 * iqr;

            double lowerWhisker = sortedValues.First(x => x >= lowerFence);
            double upperWhisker = sortedValues.Last(x => x <= upperFence);

            var outliers = sortedValues.Where(x => x < lowerWhisker || x > upperWhisker).ToArray();

            return new BoxPlotTag(q1, median, q3, lowerWhisker, upperWhisker, outliers);
        }

        private static double GetMedian(double[] sorted, int start, int end)
        {
            int n = end - start;
            if (n <= 0)
                return 0;
            if (n % 2 == 0)
                return (sorted[start + n / 2 - 1] + sorted[start + n / 2]) / 2.0;
            return sorted[start + n / 2];
        }
    }

    /// <summary>
    /// Data handler for Copy Data functionality on box plot graphs.
    /// </summary>
    public class BoxPlotBarItemDataHandler : CurveDataHandler
    {
        protected override DataFrameBuilder AddColumns(DataFrameBuilder dataFrameBuilder)
        {
            dataFrameBuilder = AddColumnForAxis(dataFrameBuilder, dataFrameBuilder.BaseAxis);
            var points = dataFrameBuilder.Points;
            var medians = new double?[points.Count];
            var q1Values = new double?[points.Count];
            var q3Values = new double?[points.Count];
            var minValues = new double?[points.Count];
            var maxValues = new double?[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point.IsMissing)
                    continue;
                q1Values[i] = point.Z;
                q3Values[i] = point.Y;
                if (point.Tag is BoxPlotTag tag)
                {
                    medians[i] = tag.Median;
                    minValues[i] = tag.Min;
                    maxValues[i] = tag.Max;
                }
            }

            var dataFrame = new DataFrame(dataFrameBuilder.ValueAxis.Title.Text, points.Count);
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"Min", minValues));
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"Q1", q1Values));
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"Median", medians));
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"Q3", q3Values));
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>(@"Max", maxValues));
            dataFrameBuilder = dataFrameBuilder.AddColumn(dataFrame);
            return dataFrameBuilder;
        }
    }
}
