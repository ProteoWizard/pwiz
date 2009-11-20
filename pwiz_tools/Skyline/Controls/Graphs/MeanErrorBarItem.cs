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

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// BarItem with an error bar at the top indicating meand and standard deviation.
    /// </summary>
    public class MeanErrorBarItem : BarItem
    {
        public static PointPair MakePointPair(double xValue, double yValue, double errorValue)
        {
            if (double.IsNaN(errorValue))
                errorValue = 0;
            return new PointPair(xValue, yValue)
                       {Tag = new ErrorTag(errorValue)};
        }

        public static PointPairList MakePointPairList(double[] xValues, double[] yValues, double[] errorValues)
        {
            PointPairList pointPairList = new PointPairList(xValues, yValues);
            for (int i = 0; i < xValues.Length; i++)
            {
                pointPairList[i].Tag = new ErrorTag(errorValues[i]);
            }
            return pointPairList;
        }

        public static double GetYTotal(PointPair pointPair)
        {
            return pointPair.Y + ((ErrorTag) pointPair.Tag).Error/2;
        }

        public MeanErrorBarItem(String label, 
                               double[] xValues, double[] yValues, double[] errorValues,
                               Color color, Color errorColor) 
            : this(label, MakePointPairList(xValues, yValues, errorValues), color, errorColor)
        {
        }

        public MeanErrorBarItem(String label, IPointList pointPairList, Color color, Color middleColor) : base(label, pointPairList, color)
        {
            _bar = new MeanErrorBar(color, middleColor);
        }
    }

    public class MeanErrorBar : Bar
    {
        private const float PIX_TERM_WIDTH = 5;

        public MeanErrorBar(Color color, Color errorColor)
            : base(color)
        {
            ErrorFill = new Fill(errorColor);
        }

        public Fill ErrorFill { get; private set; }

        protected override void DrawSingleBar(Graphics g, GraphPane pane, CurveItem curve,
                                              int index, int pos, Axis baseAxis, Axis valueAxis, float barWidth, float scaleFactor)
        {
            base.DrawSingleBar(g, pane, curve, index, pos, baseAxis, valueAxis, barWidth, scaleFactor);
            PointPair pointPair = curve.Points[index];
            ErrorTag errorTag = pointPair.Tag as ErrorTag;
            if (pointPair.IsInvalid || errorTag == null)
            {
                return;
            }

            double curBase, curLowVal, curHiVal;
            ValueHandler valueHandler = new ValueHandler(pane, false);
            valueHandler.GetValues(curve, index, out curBase, out curLowVal, out curHiVal);

            float pixBase = baseAxis.Scale.Transform(curve.IsOverrideOrdinal, index, curBase);
            double lowError = curHiVal - errorTag.Error/2;
            float pixLowError = valueAxis.Scale.Transform(lowError);
            float pixHiError = valueAxis.Scale.Transform(lowError + errorTag.Error);

            float clusterWidth = pane.BarSettings.GetClusterWidth();
            //float barWidth = curve.GetBarWidth( pane );
            float clusterGap = pane.BarSettings.MinClusterGap * barWidth;
            float barGap = barWidth * pane.BarSettings.MinBarGap;

            // Calculate the pixel location for the side of the bar (on the base axis)
            float pixSide = pixBase - clusterWidth / 2.0F + clusterGap / 2.0F +
                            pos * (barWidth + barGap);

            // Draw the bar
            RectangleF rect;
            if (pane.BarSettings.Base == BarBase.X)
            {
                if (barWidth >= 3 && errorTag.Error > 0)
                {
                    // Draw whiskers
                    float pixMidX = (float)Math.Round(pixSide + barWidth / 2);

                    // Line
                    rect = new RectangleF(pixMidX, pixHiError, 1, pixLowError - pixHiError);
                    ErrorFill.Draw(g, rect);
                    if (barWidth >= PIX_TERM_WIDTH)
                    {
                        // Ends
                        rect = new RectangleF(pixMidX - (float)Math.Round(PIX_TERM_WIDTH / 2), pixHiError, PIX_TERM_WIDTH, 1);
                        ErrorFill.Draw(g, rect);
                        rect = new RectangleF(pixMidX - (float)Math.Round(PIX_TERM_WIDTH / 2), pixLowError, PIX_TERM_WIDTH, 1);
                        ErrorFill.Draw(g, rect);
                    }
                }
            }
            else
            {
                if (barWidth >= 3 && errorTag.Error > 0)
                {
                    // Draw whiskers
                    float pixMidY = (float)Math.Round(pixSide + barWidth / 2);

                    // Line
                    rect = new RectangleF(pixLowError, pixMidY, pixHiError - pixLowError, 1);
                    ErrorFill.Draw(g, rect);
                    if (barWidth >= PIX_TERM_WIDTH)
                    {
                        // Ends
                        rect = new RectangleF(pixHiError, pixMidY - (float)Math.Round(PIX_TERM_WIDTH / 2), 1, PIX_TERM_WIDTH);
                        ErrorFill.Draw(g, rect);
                        rect = new RectangleF(pixLowError, pixMidY - (float)Math.Round(PIX_TERM_WIDTH / 2), 1, PIX_TERM_WIDTH);
                        ErrorFill.Draw(g, rect);                        
                    }
                }
            }
        }
    }

    internal class ErrorTag
    {
        public ErrorTag(double error)
        {
            Error = error;
        }

        public double Error { get; private set; }
    }
}