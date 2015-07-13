/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Util
{
    public class AxisLabelScaler
    {
        /// <summary>
        /// Allow labels to consume 1/3 of the graph height when flipped vertical
        /// </summary>
        private const float MAX_HEIGHT_LABEL_PROPORTION = 0.33f;
        private readonly GraphPane _graphPane;
        private string[] _reducedTextLabels;
        private string[] _originalTextLabels;
        public AxisLabelScaler(GraphPane graphPane)
        {
            _graphPane = graphPane;
        }

        public int FirstDataIndex { get; set; }
        public RectangleF Rect
        {
            get { return _graphPane.Rect; }
        }
        public XAxis XAxis { get { return _graphPane.XAxis; } }
        public Chart Chart { get { return _graphPane.Chart; } }

        public string[] OriginalTextLabels
        {
            get { return _originalTextLabels; }
            set { _originalTextLabels = value; }
        }

        public void ScaleAxisLabels()
        {
            int countLabels = 0;
            if (XAxis.Scale.TextLabels != null)
            {
                countLabels = XAxis.Scale.TextLabels.Length;

                // Reset the text labels to their original values.
                if (_reducedTextLabels != null && ArrayUtil.EqualsDeep(XAxis.Scale.TextLabels, _reducedTextLabels))
                {
                    Array.Copy(_originalTextLabels, XAxis.Scale.TextLabels, _originalTextLabels.Length);
                }
                // Keep the original text.
                else
                {
                    _originalTextLabels = XAxis.Scale.TextLabels.ToArray();
                }
            }

            _reducedTextLabels = null;

            float dyAvailable = Rect.Height * MAX_HEIGHT_LABEL_PROPORTION;
            float dxAvailable = Chart.Rect.Width / Math.Max(1, countLabels);

            float dpAvailable = Math.Max(dxAvailable, dyAvailable);

            var fontSpec = XAxis.Scale.FontSpec;

            int pointSize;
            for (pointSize = (int)Settings.Default.AreaFontSize; pointSize > 4; pointSize--)
            {
                // Start over with the original labels and a smaller font
                if (XAxis.Scale.TextLabels != null)
                    XAxis.Scale.TextLabels = _originalTextLabels.ToArray();

                using (var font = new Font(fontSpec.Family, pointSize))
                {
                    // See if the original labels fit with this font
                    int maxWidth = MaxWidth(font, XAxis.Scale.TextLabels);
                    if (maxWidth <= dpAvailable)
                    {
                        ScaleToWidth(maxWidth, dxAvailable);
                        break;
                    }

                    // See if they can be shortened to fit horizontally or vertically
                    if (RemoveRepeatedLabelText(font, dxAvailable, dxAvailable) ||
                        RemoveRepeatedLabelText(font, dyAvailable, dxAvailable))
                    {
                        break;
                    }
                }
            }

            if (XAxis.Scale.TextLabels != null && !ArrayUtil.EqualsDeep(XAxis.Scale.TextLabels, _originalTextLabels))
            {
                _reducedTextLabels = XAxis.Scale.TextLabels.ToArray();
            }

            XAxis.Scale.FontSpec.Size = pointSize;
        }

        private static int MaxWidth(Font font, IEnumerable<String> labels)
        {
            var result = 0;
            if (labels != null)
            {
                foreach (var label in labels)
                    result = Math.Max(result, SystemMetrics.GetTextWidth(font, label));
            }
            return result;
        }

        private bool RemoveRepeatedLabelText(Font font, float dpAvailable, float dxAvailable)
        {
            var startLabels = XAxis.Scale.TextLabels.ToArray();
            int maxWidth = RemoveRepeatedLabelText(font, dpAvailable);
            if (maxWidth <= dpAvailable)
            {
                ScaleToWidth(maxWidth, dxAvailable);
                return true;
            }
            XAxis.Scale.TextLabels = startLabels;
            return false;
        }

        private int RemoveRepeatedLabelText(Font font, float dpAvailable)
        {
            while (Helpers.RemoveRepeatedLabelText(XAxis.Scale.TextLabels, FirstDataIndex))
            {
                int maxWidth = MaxWidth(font, XAxis.Scale.TextLabels);
                if (maxWidth <= dpAvailable)
                {
                    return maxWidth;
                }
            }

            return int.MaxValue;
        }

        private void ScaleToWidth(float textWidth, float dxAvailable)
        {
            if (textWidth > dxAvailable)
            {
                XAxis.Scale.FontSpec.Angle = 90;
                XAxis.Scale.Align = AlignP.Inside;
            }
            else
            {
                XAxis.Scale.FontSpec.Angle = 0;
                XAxis.Scale.Align = AlignP.Center;
            }
        }
    }
}
