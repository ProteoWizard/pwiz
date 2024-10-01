/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Base class for GraphPanes that are shown on the RetentionTime graph
    /// </summary>
    public abstract class SummaryGraphPane : GraphPane, ITipDisplayer
    {
        protected SummaryGraphPane(GraphSummary graphSummary)
        {
            GraphSummary = graphSummary;
            PaneKey = PaneKey.DEFAULT;
            Border.IsVisible = false;
            Title.IsVisible = true;

            Chart.Border.IsVisible = false;
            XAxis.Scale.MaxAuto = true;
            YAxis.Scale.MaxAuto = true;
            Y2Axis.IsVisible = false;
            X2Axis.IsVisible = false;
            XAxis.MajorTic.IsOpposite = false;
            YAxis.MajorTic.IsOpposite = false;
            XAxis.MinorTic.IsOpposite = false;
            YAxis.MinorTic.IsOpposite = false;
            IsFontsScaled = false;
            YAxis.Scale.MaxGrace = 0.1;
        }

        public GraphSummary GraphSummary { get; private set; }
        public PaneKey PaneKey { get; protected set; }

        Rectangle ITipDisplayer.RectToScreen(Rectangle r)
        {
            if (!GraphSummary.GraphControl.IsDisposed)
                return GraphSummary.GraphControl.RectangleToScreen(r);
            return new Rectangle();
        }

        Rectangle ITipDisplayer.ScreenRect
        {
            get
            {
                if (!GraphSummary.GraphControl.IsDisposed)
                    return Screen.GetBounds(GraphSummary.GraphControl);
                return new Rectangle();
            }
        }

        bool ITipDisplayer.AllowDisplayTip => true;

        /// <summary>
        /// Sets a fixed minimum y value, which the graph control will maintain through
        /// zooming and panning.
        /// </summary>
        public double? FixedYMin { get; set; }

        public void EnsureYMin()
        {
            if (FixedYMin.HasValue && YAxis.Scale.Min != FixedYMin.Value)
            {
                YAxis.Scale.Min = FixedYMin.Value;
                AxisChange(GraphSummary.GraphControl.CreateGraphics());
            }  
            else if (EnableLabelLayout)
                AxisChange();
        }

        /// <summary>
        /// Update the graph pane.
        /// </summary>
        /// <param name="selectionChanged">True when the selection has changed, otherwise false</param>
        public abstract void UpdateGraph(bool selectionChanged);

        public virtual bool HasToolbar { get { return false; } }

        public virtual void OnClose(EventArgs e)
        {
            
        }

        public virtual bool HandleMouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return false;
        }

        public virtual void HandleMouseOutEvent(object sender, EventArgs e){}

        public virtual bool HandleMouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return false;
        }

        public virtual void HandleMouseClick(object sender, MouseEventArgs e)
        {
        }

        public virtual bool HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            return false;
        }

        public virtual void HandleResizeEvent()
        {            
        }

        #region Functional test support

        public int GetTransitionCount()
        {
            return CurveList.Count;
        }

        public int GetTotalBars()
        {
            int count = 0;
            foreach (var curve in CurveList) {
                count += curve.Points.Count;
            }
            return count;
        }

        public int GetBoxObjCount()
        {
            return GraphObjList.Count(o => o is BoxObj);
        }


        public double GetMin()
        {
            double min = double.MaxValue;
            foreach (var curve in CurveList) {
                for (int i = 0; i < curve.Points.Count; i++) {
                    var point = curve.Points[i];
                    min = Math.Min(point.Y, min);
                }
            }
            return min;
        }

        public double GetMax()
        {
            double max = double.MinValue;
            foreach (var curve in CurveList) {
                for (int i = 0; i < curve.Points.Count; i++) {
                    var point = curve.Points[i];
                    if (point.Y < double.MaxValue)
                        max = Math.Max(point.Y, max);
                }
            }
            return max;
        }

        #endregion
    }
}