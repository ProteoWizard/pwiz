/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing.Drawing2D;
using System.Linq;
using pwiz.Skyline.Model;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class PaneProgressBar : IProgressBar
    {
        readonly LineObj _left = new LineObj()
        {
            IsClippedToChartRect = true,
            Line = new Line() { Width = 4, Color = Color.Green, Style = DashStyle.Solid },
            Location = new Location(0, 0, CoordType.PaneFraction),
            ZOrder = ZOrder.A_InFront
        };
        readonly LineObj _right = new LineObj()
        {
            IsClippedToChartRect = true,
            Line = new Line() { Width = 4, Color = Color.LightGreen, Style = DashStyle.Solid },
            Location = new Location(0, 0, CoordType.PaneFraction),
            ZOrder = ZOrder.A_InFront
        };
        private PointF _barLocation;
        private float _barWidth;
        private readonly SummaryGraphPane _parent;
        
        public SummaryGraphPane Parent => _parent;
        public bool IsDisposed { get; private set; }

        public PaneProgressBar(SummaryGraphPane parent)
        {
            SizeF _titleSize;
            _parent = parent;
            var scaleFactor = parent.CalcScaleFactor();
            using (var g = parent.GraphSummary.CreateGraphics())
            {
                _titleSize = parent.Title.FontSpec.BoundingBox(g, @" ", scaleFactor);
            }

            _barWidth = parent.Rect.Width / 3;
            _barLocation = new PointF(
                (parent.Rect.Left + parent.Rect.Right - _barWidth) / (2 * parent.Rect.Width),
                (parent.Rect.Top + parent.Margin.Top * (1 + scaleFactor) + _titleSize.Height) / parent.Rect.Height);

            _left.Location.X = _barLocation.X;
            _left.Location.Y = _barLocation.Y;
            _left.Location.Width = 0;
            _left.Location.Height = 0;
            _right.Location.X = _barLocation.X;
            _right.Location.Y = _barLocation.Y;
            _right.Location.Width = _barWidth / parent.Rect.Width;
            _right.Location.Height = 0;
            parent.GraphObjList.Add(_left);
            parent.GraphObjList.Add(_right);
            IsDisposed = false;
        }

        public void Dispose()
        {
            _parent.GraphObjList.Remove(_left);
            _parent.GraphObjList.Remove(_right);
            IsDisposed = true;
        }

        private void DrawBar(int progress)
        {
            if (_parent.GraphObjList.FirstOrDefault((obj) => ReferenceEquals(obj, _left)) == null)
                _parent.GraphObjList.Add(_left);
            if (_parent.GraphObjList.FirstOrDefault((obj) => ReferenceEquals(obj, _right)) == null)
                _parent.GraphObjList.Add(_right);

            var len1 = _barWidth * progress / 100 / _parent.Rect.Width;

            _left.Location.X = _barLocation.X;
            _left.Location.Y = _barLocation.Y;
            _left.Location.Width = len1;
            _left.Location.Height = 0;
            _right.Location.X = _barLocation.X + len1;
            _right.Location.Y = _barLocation.Y;
            _right.Location.Width = _barWidth / _parent.Rect.Width - len1;
            _right.Location.Height = 0;

            //CONSIDER: Update the progress bar rectangle only
            //  instead of the whole control
            _parent.GraphSummary.GraphControl.Invalidate();
            _parent.GraphSummary.GraphControl.Update();
        }

        //Thread-safe method to update the progress bar
        public void UpdateProgress(int progress)
        {
            var graph = _parent.GraphSummary.GraphControl;
            graph.Invoke((Action) (() => { this.UpdateProgressUI(progress); }));
        }

        //must be called on the UI thread
        public void UpdateProgressUI(int progress)
        {
            var graph = _parent.GraphSummary.GraphControl;
            if (graph != null && !graph.IsDisposed && graph.IsHandleCreated)
                graph.Invoke((Action) (() => { this.DrawBar(progress); }));
        }

        bool IProgressBar.IsDisposed()
        {
            var graph = _parent.GraphSummary.GraphControl;
            return IsDisposed || graph == null || !graph.IsHandleCreated || graph.IsDisposed;
        }

        void IProgressBar.UpdateProgress(int progress)
        {
            this.UpdateProgress(progress);
        }

        void IProgressBar.UIInvoke(Action act)
        {
            var graph = _parent.GraphSummary.GraphControl;
            graph.Invoke(act);
        }
    }
}