/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Controls;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Lightweight cursor-tracking tooltip that uses the same table-based
    /// rendering as <see cref="NodeTip"/> (via <see cref="TableDesc"/>).
    /// Shows instantly on mouse move and auto-hides after a delay.
    /// Use this instead of <see cref="NodeTip"/> when you need responsive,
    /// cursor-following behavior (e.g. showing coordinates as mouse moves).
    /// </summary>
    internal class CursorTrackingTip : IDisposable
    {
        private const int CURSOR_OFFSET_X = 15;
        private const int CURSOR_OFFSET_Y = 15;
        private const int AUTO_HIDE_DELAY_MS = 3000;
        private const int PADDING = 3;

        private readonly Panel _panel;
        private readonly Timer _autoHideTimer;
        private readonly Func<Point, TableDesc> _getTooltipTable;
        private readonly RenderTools _renderTools = new RenderTools();
        private Point _lastPos = new Point(int.MinValue, int.MinValue);
        private TableDesc _table;

        /// <summary>
        /// Creates a cursor-tracking tooltip on the given parent control.
        /// </summary>
        /// <param name="parent">Control to track mouse movement on.</param>
        /// <param name="getTooltipTable">Callback that returns a <see cref="TableDesc"/>
        /// for a cursor position, or null to hide the tooltip.</param>
        public CursorTrackingTip(Control parent, Func<Point, TableDesc> getTooltipTable)
        {
            _getTooltipTable = getTooltipTable;
            _panel = new Panel
            {
                BackColor = SystemColors.Info,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            _panel.Paint += PanelPaint;
            parent.Controls.Add(_panel);

            _autoHideTimer = new Timer { Interval = AUTO_HIDE_DELAY_MS };
            _autoHideTimer.Tick += (s, e) =>
            {
                _autoHideTimer.Stop();
                _panel.Visible = false;
            };

            parent.MouseMove += OnParentMouseMove;
            parent.MouseDown += (s, e) => _panel.Visible = false;
            parent.MouseLeave += (s, e) => _panel.Visible = false;
        }

        public RenderTools RenderTools => _renderTools;

        private void OnParentMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.None)
            {
                _panel.Visible = false;
                return;
            }
            if (e.Location == _lastPos)
                return; // Ignore spurious MouseMove events from layout changes
            _lastPos = e.Location;
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
            var table = _getTooltipTable(e.Location);
            if (table == null)
            {
                _panel.Visible = false;
                return;
            }
            _table = table;
            using (var g = _panel.CreateGraphics())
            {
                var size = _table.CalcDimensions(g);
                _panel.Size = new Size((int)Math.Ceiling(size.Width) + PADDING * 2,
                                       (int)Math.Ceiling(size.Height) + PADDING * 2);
            }
            _panel.Invalidate();
            _panel.Location = new Point(e.X + CURSOR_OFFSET_X, e.Y + CURSOR_OFFSET_Y);
            _panel.Visible = true;
            _panel.BringToFront();
        }

        private void PanelPaint(object sender, PaintEventArgs e)
        {
            if (_table == null)
                return;
            e.Graphics.TranslateTransform(PADDING, PADDING);
            _table.Draw(e.Graphics);
            e.Graphics.ResetTransform();
        }

        public void Dispose()
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Dispose();
            _panel.Dispose();
            _renderTools.Dispose();
        }
    }
}
