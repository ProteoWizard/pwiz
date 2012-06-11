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
    internal abstract class SummaryBarGraphPaneBase : SummaryGraphPane
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

        protected void ScaleAxisLabels()
        {
            float dyAvailable = Rect.Height/5;

            // Reset the text labels to their original values.
            if (_reducedTextLabels != null && ArrayUtil.EqualsDeep(XAxis.Scale.TextLabels, _reducedTextLabels))
                Array.Copy(_originalTextLabels, XAxis.Scale.TextLabels, _originalTextLabels.Length);
            //Keep the reduced text.
            else if (XAxis.Scale.TextLabels != null)
                _originalTextLabels = XAxis.Scale.TextLabels.ToArray();
            _reducedTextLabels = null;

            int countLabels = (XAxis.Scale.TextLabels != null ? XAxis.Scale.TextLabels.Length : 0);
            float dxAvailable = Chart.Rect.Width/Math.Max(1, countLabels);

            float dpAvailable;

            var fontSpec = XAxis.Scale.FontSpec;

            var textWidth = MaxWidth(new Font(fontSpec.Family, fontSpec.Size), XAxis.Scale.TextLabels);

            if (textWidth > dxAvailable)
            {
                dpAvailable = dyAvailable;
                XAxis.Scale.FontSpec.Angle = 90;
                XAxis.Scale.Align = AlignP.Inside;
            }
            else
            {
                dpAvailable = dxAvailable;
                XAxis.Scale.FontSpec.Angle = 0;
                XAxis.Scale.Align = AlignP.Center;
            }
            
            int pointSize;
            
            for (pointSize = 12; pointSize > 4; pointSize--)
            {
                using (var font = new Font(fontSpec.Family, pointSize))
                {
                    var maxWidth = MaxWidth(font, XAxis.Scale.TextLabels);
                    if (maxWidth <= dpAvailable)
                    {
                        break;
                    }
                    if (Helpers.RemoveRepeatedLabelText(XAxis.Scale.TextLabels, FirstDataIndex))
                    {
                        maxWidth = MaxWidth(font, XAxis.Scale.TextLabels);
                        if (maxWidth <= dpAvailable)
                            break;
                    }
                }
            }

            if (XAxis.Scale.TextLabels != null && !ArrayUtil.EqualsDeep(XAxis.Scale.TextLabels, _originalTextLabels))
                _reducedTextLabels = XAxis.Scale.TextLabels.ToArray();

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
    }
}