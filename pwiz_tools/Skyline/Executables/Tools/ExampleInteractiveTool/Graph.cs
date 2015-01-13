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
using System.Linq;
using System.Windows.Forms;
using SkylineTool;
using ZedGraph;

namespace ExampleInteractiveTool
{
    /// <summary>
    /// This class abstracts the details of dealing with Zedgraph to
    /// keep the main program focused on the API for Skyline tools
    /// rather than the nuts and bolts of using Zedgraph.
    /// </summary>
    public class Graph
    {
        public event EventHandler<ClickEventArgs> Click;
        private readonly ZedGraphControl _graphControl;

        /// <summary>
        /// Create an empty graph with the appearance we want.
        /// </summary>
        public Graph(ZedGraphControl graphControl, string xLabel, string yLabel)
        {
            _graphControl = graphControl;

            var pane = _graphControl.GraphPane;
            pane.Title.IsVisible = false;
            pane.Border.IsVisible = false;
            pane.Chart.Border.IsVisible = false;

            pane.XAxis.Title.Text = xLabel;
            pane.XAxis.MinorTic.IsOpposite = false;
            pane.XAxis.MajorTic.IsOpposite = false;
            pane.XAxis.MajorTic.IsAllTics = false;
            pane.XAxis.Scale.FontSpec.Angle = 90;
            pane.XAxis.Scale.Align = AlignP.Inside;

            pane.YAxis.Title.Text = yLabel;
            pane.YAxis.MinorTic.IsOpposite = false;
            pane.YAxis.MajorTic.IsOpposite = false;

            _graphControl.IsZoomOnMouseCenter = true;
            _graphControl.IsEnableVZoom = _graphControl.IsEnableVPan = false;
            _graphControl.SizeChanged += GraphSizeChanged;
            _graphControl.ZoomEvent += GraphZoomEvent;
            _graphControl.MouseClick += GraphMouseClick;
            _graphControl.MouseMove += GraphMouseMove;
        }

        /// <summary>
        /// Change to a hand when the cursor goes over a clickable bar in the bar graph.
        /// </summary>
        private void GraphMouseMove(object sender, MouseEventArgs e)
        {
            _graphControl.Cursor = FindBarIndex(e) >= 0 ? Cursors.Hand : Cursors.Cross;
        }

        /// <summary>
        /// Call Click handler if user clicked on a bar.
        /// </summary>
        private void GraphMouseClick(object sender, MouseEventArgs e)
        {
            if (Click == null)
                return;
            int barIndex = FindBarIndex(e);
            if (barIndex < 0)
                return;
            Click(this, new ClickEventArgs(barIndex));
        }

        /// <summary>
        /// Find which bar (if any) contains the coordinates of a mouse event.
        /// </summary>
        /// <param name="e">Mouse event arguments.</param>
        /// <returns>Index of bar or -1 if none contained by mouse point.</returns>
        private int FindBarIndex(MouseEventArgs e)
        {
            object nearestObject;
            int index;
            _graphControl.GraphPane.FindNearestObject(new PointF(e.X, e.Y), _graphControl.CreateGraphics(), out nearestObject, out index);
            return (nearestObject != null && nearestObject.GetType() == typeof (BarItem)) ? index : -1;
        }

        /// <summary>
        /// Rescale axis font size when user zooms.
        /// </summary>
        void GraphZoomEvent(ZedGraphControl sender, ZoomState oldState, ZoomState newState, PointF mousePosition)
        {
            GraphUtilities.ScaleAxisLabels(_graphControl.Width, _graphControl.GraphPane);
        }

        /// <summary>
        /// Rescale axis font size when graph size is changed.
        /// </summary>
        void GraphSizeChanged(object sender, EventArgs e)
        {
            GraphUtilities.ScaleAxisLabels(_graphControl.Width, _graphControl.GraphPane);
        }

        /// <summary>
        /// Create a bar graph for the given labels and values arrays.
        /// </summary>
        public void CreateBars(string[] labels, double[] values)
        {
            var pane = _graphControl.GraphPane;
            pane.CurveList.Clear();

            pane.AddBar(null, null, values, Color.Red);
            pane.XAxis.Scale.TextLabels = labels.ToArray();
            pane.XAxis.Type = AxisType.Text;

            _graphControl.AxisChange();
            GraphUtilities.ScaleAxisLabels(_graphControl.Width, pane);
            _graphControl.Refresh();
        }

        /// <summary>
        /// Create chromatogram graph.
        /// </summary>
        public void CreateChromatograms(IEnumerable<Chromatogram> chromatograms, string name)
        {
            var pane = _graphControl.GraphPane;
            pane.CurveList.Clear();
            pane.Title.IsVisible = true;
            pane.Title.Text = name;
            _graphControl.IsAntiAlias = false;

            foreach (var chromatogram in chromatograms)
            {
                var line = pane.AddCurve("", ToDoubles(chromatogram.Times), ToDoubles(chromatogram.Intensities), chromatogram.Color, SymbolType.None);
                line.Line.Width = 2.0f;
                line.Line.IsAntiAlias = true;
            }

            _graphControl.AxisChange();
            GraphUtilities.ScaleAxisLabels(_graphControl.Width, pane);
            _graphControl.Refresh();
        }

        private static double[] ToDoubles(IList<float> fa)
        {
            var da = new double[fa.Count];
            for (int i = 0; i < fa.Count; i++)
                da[i] = fa[i];
            return da;
        }
    }

    public class ChromatogramGraph : Graph
    {
        public ChromatogramGraph(ZedGraphControl graphControl, string xLabel, string yLabel)
            : base(graphControl, xLabel, yLabel)
        {
            var pane = graphControl.GraphPane;
            pane.XAxis.MajorTic.IsAllTics = true;
            pane.XAxis.MajorTic.IsOpposite = false;
            pane.XAxis.Scale.FontSpec.Angle = 0;
        }
    }

    /// <summary>
    /// Transmit an index value when the mouse is clicked.
    /// </summary>
    public sealed class ClickEventArgs : EventArgs
    {
        public ClickEventArgs(int index)
        {
            Index = index;
        }

        public int Index { get; private set; }
    }
}
