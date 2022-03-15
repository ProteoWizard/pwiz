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
        private string _maxLengthLabel;

        public AxisLabelScaler(GraphPane graphPane) : this (graphPane, graphPane.XAxis)
        {
        }

        public AxisLabelScaler(GraphPane graphPane, Axis axis)
        {
            _graphPane = graphPane;
            Axis = axis;
        }

        public int FirstDataIndex { get; set; }
        public bool IsRepeatRemovalAllowed { get; set; }

        public bool AxisIsHorizontal
        {
            get { return Axis is XAxis || Axis is X2Axis; }
        }

        public Axis Axis { get; private set; }
        public Chart Chart { get { return _graphPane.Chart; } }

        public string[] OriginalTextLabels
        {
            get { return _originalTextLabels; }
            set
            {
                _originalTextLabels = value;
                _maxLengthLabel = null;
            }
        }

        public void ScaleAxisLabels()
        {
            int countLabels = 0;
            if (Axis.Scale.TextLabels != null)
            {
                countLabels = Axis.Scale.TextLabels.Length;

                // Reset the text labels to their original values.
                if (_reducedTextLabels != null && ArrayUtil.ReferencesEqual(Axis.Scale.TextLabels, _reducedTextLabels))
                {
                    Array.Copy(_originalTextLabels, Axis.Scale.TextLabels, _originalTextLabels.Length);
                }
                // Keep the original text.
                else if (!ArrayUtil.ReferencesEqual(Axis.Scale.TextLabels, _originalTextLabels))
                {
                    OriginalTextLabels = Axis.Scale.TextLabels.ToArray();
                }
            }

            _reducedTextLabels = null;

            float depthAvailable = (AxisIsHorizontal ? _graphPane.Rect.Height : _graphPane.Rect.Width) * MAX_HEIGHT_LABEL_PROPORTION;

            var fontSpec = Axis.Scale.FontSpec;

            var originalTextCopy = Axis.Scale.TextLabels != null ? _originalTextLabels.ToArray() : null;
            int pointSize;
            for (pointSize = (int)Settings.Default.AreaFontSize; pointSize > 4; pointSize--)
            {
                // Start over with the original labels and a smaller font
                if (Axis.Scale.TextLabels != null)
                    Axis.Scale.TextLabels = originalTextCopy;

                using (var font = new Font(fontSpec.Family, pointSize))
                {
                    int maxWidth = _maxLengthLabel != null
                        ? MaxWidth(font, new[] { _maxLengthLabel }, out _maxLengthLabel)
                        : MaxWidth(font, Axis.Scale.TextLabels, out _maxLengthLabel);
                    if (AxisIsHorizontal)
                    {
                        float lengthPerLabel = Chart.Rect.Width / Math.Max(1, countLabels);
                        float dpAvailable = Math.Max(lengthPerLabel, depthAvailable);
                        if (maxWidth <= dpAvailable)
                        {
                            ScaleToWidth(maxWidth, lengthPerLabel);
                            break;
                        }

                        if (IsRepeatRemovalAllowed)
                        {
                            // See if they can be shortened to fit horizontally or vertically
                            if (RemoveRepeatedLabelText(font, lengthPerLabel, lengthPerLabel) ||
                                RemoveRepeatedLabelText(font, depthAvailable, lengthPerLabel))
                            {
                                break;
                            }
                            originalTextCopy = _originalTextLabels.ToArray();
                        }
                    }
                    else
                    {
                        if (maxWidth <= depthAvailable)
                        {
                            break;
                        }
                        if (IsRepeatRemovalAllowed)
                        {
                            // See if they can be shortened to fit horizontally or vertically
                            if (RemoveRepeatedLabelText(font, depthAvailable, depthAvailable))
                            {
                                break;
                            }
                            originalTextCopy = _originalTextLabels.ToArray();
                        }
                    }
                }
            }

            if (Axis.Scale.TextLabels != null && !ArrayUtil.ReferencesEqual(Axis.Scale.TextLabels, _originalTextLabels))
            {
                _reducedTextLabels = Axis.Scale.TextLabels.ToArray();
            }

            try
            {
                Axis.Scale.FontSpec.Size = pointSize;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format(@"Unable to set Axis.Scale.FontSpec.Size to {0} for AreaFontSize {1}", pointSize, Settings.Default.AreaFontSize), e);
            }
        }

        private static int MaxWidth(Font font, IEnumerable<String> labels, out string maxString)
        {
            var result = 0;
            var maxLength = 0;
            maxString = null;
            if (labels != null)
            {
                foreach (var label in labels.Where(l => l!=null))
                {
                    // Not strictly guaranteed, but assume that the label with the maximum width
                    // will be within 2 characters of the maximum string length, since actually
                    // measuring everything can be very time consuming
                    if (label.Length + 2 < maxLength)
                        continue;
                    maxLength = Math.Max(label.Length, maxLength);
                    int labelWidth = SystemMetrics.GetTextWidth(font, label);
                    if (labelWidth > result)
                    {
                        result = labelWidth;
                        maxString = label;
                    }
                }
            }
            return result;
        }

        private bool RemoveRepeatedLabelText(Font font, float dpAvailable, float dxAvailable)
        {
            var startLabels = Axis.Scale.TextLabels.ToArray();
            int maxWidth = RemoveRepeatedLabelText(font, dpAvailable);
            if (maxWidth <= dpAvailable)
            {
                ScaleToWidth(maxWidth, dxAvailable);
                return true;
            }
            Axis.Scale.TextLabels = startLabels;
            return false;
        }

        private int RemoveRepeatedLabelText(Font font, float dpAvailable)
        {
            while (Helpers.RemoveRepeatedLabelText(Axis.Scale.TextLabels, FirstDataIndex))
            {
                string maxLabel;
                int maxWidth = MaxWidth(font, Axis.Scale.TextLabels, out maxLabel);
                if (maxWidth <= dpAvailable)
                {
                    return maxWidth;
                }
            }

            return int.MaxValue;
        }

        private void ScaleToWidth(float textWidth, float dxAvailable)
        {
            if (AxisIsHorizontal && textWidth > dxAvailable)
            {
                Axis.Scale.FontSpec.Angle = 90;
                Axis.Scale.Align = AlignP.Inside;
            }
            else
            {
                Axis.Scale.FontSpec.Angle = AxisIsHorizontal ? 0 : 90;
                Axis.Scale.Align = AlignP.Center;
            }
        }
    }
}
