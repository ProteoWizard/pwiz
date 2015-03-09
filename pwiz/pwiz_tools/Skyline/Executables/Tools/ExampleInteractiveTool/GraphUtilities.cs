/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using ZedGraph;

namespace ExampleInteractiveTool
{
    /// <summary>
    /// Utilities to perform graph functions.
    /// </summary>
    public static class GraphUtilities
    {
        /// <summary>
        /// Rescale font size of x axis labels when the graph is zoomed or resized.
        /// </summary>
        /// <param name="width">Width of the graph in pixels.</param>
        /// <param name="pane">GraphPane of the graph.</param>
        public static void ScaleAxisLabels(int width, GraphPane pane)
        {
            if (pane.XAxis.Scale.TextLabels == null)
                return;

            pane.XAxis.Scale.IsPreventLabelOverlap = false;
            int countLabels = (int) Math.Ceiling(pane.XAxis.Scale.Max - pane.XAxis.Scale.Min) + 1;

            float dxAvailable = (float) width / countLabels;

            var fontSpec = pane.XAxis.Scale.FontSpec;

            int pointSize;

            for (pointSize = 12; pointSize > 4; pointSize--)
            {
                using (var font = new Font(fontSpec.Family, pointSize))
                {
                    // See if the original labels fit with this font
                    int maxWidth = MaxWidth(font, pane.XAxis.Scale.TextLabels);
                    if (maxWidth <= dxAvailable)
                        break;
                }
            }

            pane.XAxis.Scale.FontSpec.Size = pointSize;
            pane.AxisChange();
        }

        private static int MaxWidth(Font font, IEnumerable<String> labels)
        {
            var result = 0;
            if (labels != null)
            {
                foreach (var label in labels)
                    result = Math.Max(result, SystemMetrics.GetTextHeight(font, label));
            }
            return result;
        }

        private static class SystemMetrics
        {
            // For some reason, TextRenderer.MeasureText does not return the actual width of the text.
            // We multiply by the "FUDGE_FACTOR" so that the ListBox is wide enough that it doesn't need a horizontal scroll bar.
            private const double FudgeFactor = 1.8;

            private static Size MeasureText(Font font, String text, int dxAvailable)
            {
                Size size = TextRenderer.MeasureText(text, font, new Size(dxAvailable, Int16.MaxValue));
                return new Size((int)Math.Ceiling(size.Width * FudgeFactor), (int)Math.Ceiling(size.Height * FudgeFactor));
            }
// ReSharper disable once UnusedMember.Local
            public static int GetTextWidth(Font font, String text)
            {
                return MeasureText(font, text, int.MaxValue).Width;
            }
            public static int GetTextHeight(Font font, String text)
            {
                return MeasureText(font, text, int.MaxValue).Height;
            }
        }
    }
}
