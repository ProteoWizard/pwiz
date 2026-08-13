/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Graph;
using pwiz.MSGraph;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Implemented by Skyline graph panes that display heat map data, so that
    /// <see cref="CopyGraphDataToolStripMenuItem"/> can produce clean 3-column
    /// clipboard output without knowing the concrete pane type.
    /// </summary>
    public interface IHeatMapDataProvider
    {
        HeatMapData HeatMapData { get; }
        string HeatMapZAxisName { get; }
    }

    /// <summary>
    /// Menu item to copy the data from a ZedGraph to the clipboard as tab separated values
    /// </summary>
    public sealed class CopyGraphDataToolStripMenuItem : ToolStripMenuItem
    {
        public CopyGraphDataToolStripMenuItem(ZedGraphControl zedGraphControl)
        {
            ZedGraphControl = zedGraphControl;
            Text = Resources.CopyGraphDataToolStripMenuItem_CopyGraphDataToolStripMenuItem_Copy_Data;
        }

        public ZedGraphControl ZedGraphControl { get; private set; }

        protected override void OnClick(EventArgs e)
        {
            CopyGraphData(ZedGraphControl);
        }

        /// <summary>
        /// Copy the data from the curves in the ZedGraphControl to the clipboard.
        /// Heatmap panes get special handling to produce clean 3-column (x, y, z) output
        /// instead of the default curve-based extraction.
        /// </summary>
        public static void CopyGraphData(ZedGraphControl zedGraphControl)
        {
            var graphData = GetGraphData(zedGraphControl.MasterPane);
            if (graphData.Panes.Count == 0)
                return;
            ClipboardHelper.SetClipboardText(zedGraphControl, graphData.ToString());
        }

        /// <summary>
        /// Build a <see cref="GraphData"/> from the MasterPane, with special handling for
        /// <see cref="IHeatMapDataProvider"/> panes that produce clean 3-column output.
        /// </summary>
        public static GraphData GetGraphData(MasterPane masterPane)
        {
            var paneDataList = new List<GraphPaneData>();
            foreach (var graphPane in masterPane.PaneList)
            {
                var paneData = TryGetHeatMapPaneData(graphPane) ??
                               GraphPaneData.GetGraphPaneData(graphPane);
                if (paneData != null)
                    paneDataList.Add(paneData);
            }
            return new GraphData(null, paneDataList);
        }

        /// <summary>
        /// For panes that implement <see cref="IHeatMapDataProvider"/>, extract the
        /// <see cref="HeatMapData"/> and build a 3-column DataFrame (x, y, z) for clipboard output.
        /// </summary>
        private static GraphPaneData TryGetHeatMapPaneData(GraphPane graphPane)
        {
            if (!(graphPane is IHeatMapDataProvider provider))
                return null;

            var heatMapData = provider.HeatMapData;
            if (heatMapData == null)
                return null;

            var points = heatMapData.GetAllPoints().Select(p => p.Point).ToList();
            if (points.Count == 0)
                return null;

            var xAxisTitle = graphPane.XAxis?.Title?.Text ?? @"X";
            var yAxisTitle = graphPane.YAxis?.Title?.Text ?? @"Y";
            var zAxisTitle = provider.HeatMapZAxisName ?? @"Z";

            var xColumn = new DataColumn<double>(xAxisTitle, points.Select(p => (double)p.X).ToList());
            var yColumn = new DataColumn<double>(yAxisTitle, points.Select(p => (double)p.Y).ToList());
            var zColumn = new DataColumn<double>(zAxisTitle, points.Select(p => (double)p.Z).ToList());

            var dataFrame = new DataFrame(null, points.Count)
                .SetRowHeaders(xColumn)
                .AddColumn(yColumn)
                .AddColumn(zColumn);

            return new GraphPaneData(graphPane.Title?.Text, new[] { dataFrame });
        }
    }
}
