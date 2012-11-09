/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class SummaryBarGraphPaneBase : SummaryGraphPane
    {
        protected static bool ShowSelection
        {
            get
            {
                return Settings.Default.ShowReplicateSelection;
            }
        }

        protected static readonly Color[] COLORS_TRANSITION = GraphChromatogram.COLORS_LIBRARY;
        protected static readonly Color[] COLORS_GROUPS = GraphChromatogram.COLORS_GROUPS;

        protected static int GetColorIndex(TransitionGroupDocNode nodeGroup, int countLabelTypes, ref int charge, ref int iCharge)
        {
            return GraphChromatogram.GetColorIndex(nodeGroup, countLabelTypes, ref charge, ref iCharge);
        }

        protected SummaryBarGraphPaneBase(GraphSummary graphSummary)
            : base(graphSummary)
        {
        }

        public void Clear()
        {
            CurveList.Clear();
            GraphObjList.Clear();
        }

        protected virtual int FirstDataIndex { get { return 0; } }

        protected abstract int SelectedIndex { get; }

        protected abstract IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex);

        public override bool HandleKeyDownEvent(object sender, KeyEventArgs keyEventArgs)
        {
            switch (keyEventArgs.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                    ChangeSelection(SelectedIndex - 1, null);
                    return true;
                case Keys.Right:
                case Keys.Down:
                    ChangeSelection(SelectedIndex + 1, null);
                    return true;
            }
            return false;
        }

        protected abstract void ChangeSelection(int selectedIndex, IdentityPath identityPath);

        public override bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            if (mouseEventArgs.Button != MouseButtons.None)
                return base.HandleMouseMoveEvent(sender, mouseEventArgs);

            CurveItem nearestCurve;
            int iNearest;
            if (!FindNearestPoint(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
            {
                return false;
            }
            IdentityPath identityPath = GetIdentityPath(nearestCurve, iNearest);
            if (identityPath == null)
            {
                return false;
            }
            GraphSummary.Cursor = Cursors.Hand;
            return true;
        }

        public override bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs mouseEventArgs)
        {
            CurveItem nearestCurve;
            int iNearest;
            if (!FindNearestPoint(new PointF(mouseEventArgs.X, mouseEventArgs.Y), out nearestCurve, out iNearest))
            {
                return false;
            }
            IdentityPath identityPath = GetIdentityPath(nearestCurve, iNearest);
            if (identityPath == null)
            {
                return false;
            }

            ChangeSelection(iNearest, identityPath);
            return true;
        }

        public override void Draw(Graphics g)
        {
            _chartBottom = Chart.Rect.Bottom;
            HandleResizeEvent();
            base.Draw(g);
            if (IsRedrawRequired(g))
                base.Draw(g);

        }

        public override void HandleResizeEvent()
        {
            ScaleAxisLabels();
        }

        protected virtual bool IsRedrawRequired(Graphics g)
        {
            // Have to call HandleResizeEvent twice, since the X-scale may not be up
            // to date before calling Draw.  If nothing changes, this will be a no-op
            HandleResizeEvent();
            return (Chart.Rect.Bottom != _chartBottom);
        }

        private float _chartBottom;
        public string[] _originalTextLabels;
        public string[] _reducedTextLabels;

        protected string[] OriginalXAxisLabels
        {
            get { return _originalTextLabels ?? XAxis.Scale.TextLabels; }
        }

        /// <summary>
        /// Allow labels to consume 1/3 of the graph height when flipped vertical
        /// </summary>
        private const float MAX_HEIGHT_LABEL_PROPORTION = 0.33f;

        protected void ScaleAxisLabels()
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
            
            for (pointSize = 12; pointSize > 4; pointSize--)
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
    }
}