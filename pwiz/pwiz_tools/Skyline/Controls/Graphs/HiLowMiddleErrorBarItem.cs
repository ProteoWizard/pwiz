/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// HiLowBarItem with a tick mark somewhere in the middle that indicates some other value.
    /// </summary>
    [CurveDataHandler(typeof(HiLowMiddleBarErrorDataHandler))]
    public class HiLowMiddleErrorBarItem : HiLowBarItem
    {
        public static bool IsHiLoMiddleErrorList(PointPairList pointPairList)
        {
            return pointPairList.Count > 0 && pointPairList[0].Tag is MiddleErrorTag;
        }

        public static PointPair MakePointPair(double xValue, double highValue, double lowValue,
                                              double middleValue, double errorValue)
        {
            return new PointPair(xValue, highValue, lowValue)
                       {Tag = new MiddleErrorTag(middleValue, errorValue)};
        }

        public static PointPairList MakePointPairList(double[] xValues, double[] highValues, double[] lowValues,
                                                      double[] middleValues, double[] errorValues)
        {
            PointPairList pointPairList = new PointPairList(xValues, highValues, lowValues);
            for (int i = 0; i < middleValues.Length; i++)
            {
                pointPairList[i].Tag = new MiddleErrorTag(middleValues[i], errorValues[i]);
            }
            return pointPairList;
        }

        public HiLowMiddleErrorBarItem(String label, 
                                       double[] xValues, double[] highValues, double[] lowValues,
                                       double[] middleValues, double[] errorValues,
                                       Color color, Color middleColor) 
            : this(label, MakePointPairList(xValues, highValues, lowValues, middleValues, errorValues), color, middleColor)
        {
        }

        public HiLowMiddleErrorBarItem(String label, IPointList pointPairList, Color color, Color middleColor) : base(label, pointPairList, color)
        {
            _bar = new HiLowMiddleErrorBar(color, middleColor);
        }
    }

    public class HiLowMiddleErrorBar : Bar
    {
        private const float PIX_TERM_WIDTH = 5;

        public HiLowMiddleErrorBar(Color color, Color middleColor)
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
            MiddleErrorTag middleError = pointPair.Tag as MiddleErrorTag;
            if (pointPair.IsInvalid || middleError == null)
            {
                return;
            }

            double curBase, curLowVal, curHiVal;
            ValueHandler valueHandler = new ValueHandler(pane, false);
            valueHandler.GetValues(curve, index, out curBase, out curLowVal, out curHiVal);

            double middleValue = middleError.Middle;
            float pixBase = baseAxis.Scale.Transform(curve.IsOverrideOrdinal, index, curBase);
            float pixLowBound = valueAxis.Scale.Transform(curLowVal) - 1;
            float pixHiBound = valueAxis.Scale.Transform(curHiVal);
            float pixError = (float) Math.Abs((pixLowBound - pixHiBound) / (curLowVal - curHiVal) * middleError.Error);

            float clusterWidth = pane.BarSettings.GetClusterWidth();
            //float barWidth = curve.GetBarWidth( pane );
            float clusterGap = pane.BarSettings.MinClusterGap * barWidth;
            float barGap = barWidth * pane.BarSettings.MinBarGap;

            // Calculate the pixel location for the side of the bar (on the base axis)
            float pixSide = pixBase - clusterWidth / 2.0F + clusterGap / 2.0F +
                            pos * (barWidth + barGap);
            float pixMiddleValue = valueAxis.Scale.Transform(curve.IsOverrideOrdinal, index, middleValue);


            // Draw the bar
            if (pane.BarSettings.Base == BarBase.X)
            {
                if (barWidth >= 3 && middleError.Error > 0)
                {
                    // Draw whiskers
                    float pixLowError = Math.Min(pixLowBound, pixMiddleValue + pixError/2);
                    float pixHiError = Math.Max(pixHiBound, pixLowError - pixError);
                    pixLowError = Math.Min(pixLowBound, pixHiError + pixError);

                    float pixMidX = (float)Math.Round(pixSide + barWidth / 2);

                    // Line
                    g.DrawLine(MiddlePen, pixMidX, pixHiError, pixMidX, pixLowError);
                    if (barWidth >= PIX_TERM_WIDTH)
                    {
                        // Ends
                        float pixLeft = pixMidX - (float)Math.Round(PIX_TERM_WIDTH / 2);
                        float pixRight = pixLeft + PIX_TERM_WIDTH - 1;
                        g.DrawLine(MiddlePen, pixLeft, pixHiError, pixRight, pixHiError);
                        g.DrawLine(MiddlePen, pixLeft, pixLowError, pixRight, pixLowError);
                    }
                }

                g.DrawLine(MiddlePen, pixSide, pixMiddleValue, pixSide + barWidth, pixMiddleValue);
            }
            else
            {
                if (barWidth >= 3 && middleError.Error > 0)
                {
                    // Draw whiskers
                    float pixHiError = Math.Min(pixHiBound, pixMiddleValue + pixError / 2);
                    float pixLowError = Math.Max(pixLowBound, pixHiError - pixError);
                    pixHiError = Math.Min(pixHiBound, pixLowError + pixError);

                    float pixMidY = (float)Math.Round(pixSide + barWidth / 2);

                    // Line
                    g.DrawLine(MiddlePen, pixLowError, pixMidY, pixHiError, pixMidY);
                    if (barWidth >= PIX_TERM_WIDTH)
                    {
                        // Ends
                        float pixTop = pixMidY - (float)Math.Round(PIX_TERM_WIDTH / 2);
                        float pixBottom = pixTop + PIX_TERM_WIDTH - 1;
                        g.DrawLine(MiddlePen, pixHiError, pixTop, pixHiError, pixBottom);
                        g.DrawLine(MiddlePen, pixLowError, pixTop, pixLowError, pixBottom);
                    }
                }

                g.DrawLine(MiddlePen, pixMiddleValue, pixSide, pixMiddleValue, pixSide + barWidth);
            }
        }
    }

    internal class MiddleErrorTag
    {
        public MiddleErrorTag(double middle, double error)
        {
            Middle = middle;
            Error = error;
        }

        public double Middle { get; private set; }
        public double Error { get; private set; }
        public override string ToString()
        {
            return string.Format("{0}+/-{1}", Middle, Error); // Not L10N
        }
    }

    public class HiLowMiddleBarErrorDataHandler : CurveDataHandler
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
                var middleErrorTag = point.Tag as MiddleErrorTag;
                if (middleErrorTag != null)
                {
                    apexes[i] = middleErrorTag.Middle;
                    fwhms[i] = middleErrorTag.Error;
                }
            }
            var dataFrame = new DataFrame(dataFrameBuilder.ValueAxis.Title.Text, dataFrameBuilder.Points.Count);
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>("Apex", apexes));  // Not L10N
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>("Start", starts)); // Not L10N
            dataFrame = dataFrame.AddColumn(GetColumnForAxis(dataFrameBuilder, dataFrameBuilder.ValueAxis).SetTitle("End")); // Not L10N
            dataFrame = dataFrame.AddColumn(new DataColumn<double?>("FWHM", fwhms)); // Not L10N
            dataFrameBuilder = dataFrameBuilder.AddColumn(dataFrame);
            return dataFrameBuilder;
        }
    }
}