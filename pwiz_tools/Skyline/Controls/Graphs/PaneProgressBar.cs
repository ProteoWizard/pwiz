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
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class PaneProgressBar : IDisposable
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
        private SizeF _titleSize;
        private PointF _barLocation;
        private readonly SummaryGraphPane _parent;

        public PaneProgressBar(SummaryGraphPane parent)
        {
            _parent = parent;
            var scaleFactor = parent.CalcScaleFactor();
            using (var g = parent.GraphSummary.CreateGraphics())
            {
                _titleSize = parent.Title.FontSpec.BoundingBox(g, parent.Title.Text, scaleFactor);
            }
            _barLocation = new PointF(
                (parent.Rect.Left + parent.Rect.Right - _titleSize.Width) / (2 * parent.Rect.Width),
                (parent.Rect.Top + parent.Margin.Top * (1 + scaleFactor) + _titleSize.Height) / parent.Rect.Height);

            _left.Location.X = _barLocation.X;
            _left.Location.Y = _barLocation.Y;
            _left.Location.Width = 0;
            _left.Location.Height = 0;
            _right.Location.X = _barLocation.X;
            _right.Location.Y = _barLocation.Y;
            _right.Location.Width = _titleSize.Width / parent.Rect.Width;
            _right.Location.Height = 0;
            parent.GraphObjList.Add(_left);
            parent.GraphObjList.Add(_right);
        }

        public void Dispose()
        {
            _parent.GraphObjList.Remove(_left);
            _parent.GraphObjList.Remove(_right);
        }

        private void DrawBar(int progress)
        {
            if (_parent.GraphObjList.FirstOrDefault((obj) => ReferenceEquals(obj, _left)) == null)
                _parent.GraphObjList.Add(_left);
            if (_parent.GraphObjList.FirstOrDefault((obj) => ReferenceEquals(obj, _right)) == null)
                _parent.GraphObjList.Add(_right);

            var len1 = _titleSize.Width * progress / 100 / _parent.Rect.Width;

            _left.Location.X = _barLocation.X;
            _left.Location.Y = _barLocation.Y;
            _left.Location.Width = len1;
            _left.Location.Height = 0;
            _right.Location.X = _barLocation.X + len1;
            _right.Location.Y = _barLocation.Y;
            _right.Location.Width = _titleSize.Width / _parent.Rect.Width - len1;
            _right.Location.Height = 0;


            _parent.GraphSummary.GraphControl.Invalidate();
            _parent.GraphSummary.GraphControl.Update();
        }

        //Thread-safe method to update the progress bar
        public void UpdateProgress(int progress)
        {
            var graph = _parent.GraphSummary.GraphControl;
            if (graph != null && !graph.IsDisposed && graph.IsHandleCreated)
                try
                {   //It is possible that the main thread disposes the graph object
                    //during the Invoke method. No additional action is required in such a case
                    graph.Invoke((Action) (() => { this.DrawBar(progress); }));
                }
                catch (ObjectDisposedException)
                {}
        }

    }
}