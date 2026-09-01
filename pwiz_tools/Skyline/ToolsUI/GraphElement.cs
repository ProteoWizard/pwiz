/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using ZedGraph;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// The element for a <see cref="ZedGraphControl"/> on a form, holding the work behind the graph verbs:
    /// GetGraphData, GetGraphImage / GetGraphImageBytes, GetGraphZoom, ZoomGraphTo and ClickGraph. Each
    /// resolves a formId to its form and calls the matching method here.
    ///
    /// <para>Every method runs on the form's UI thread.</para>
    /// </summary>
    internal class GraphElement : ControlElement<ZedGraphControl>
    {
        private const string GRAPH_FILE_PREFIX = @"skyline-graph";

        public GraphElement(ZedGraphControl control, CancellationToken cancellationToken)
            : base(control, cancellationToken)
        {
        }

        /// <summary>Exports the graph's data - the same content as its "Copy Data" clipboard format - to a
        /// TSV file and returns the path, or the empty string when the graph has no data. Writes to a temp
        /// path when <paramref name="filePath"/> is null.</summary>
        public string GetData(string filePath)
        {
            var graphData = CopyGraphDataToolStripMenuItem.GetGraphData(Control.MasterPane);
            if (graphData.Panes.Count == 0)
                return string.Empty;
            filePath = filePath ?? JsonUiService.GetMcpTmpFilePath(GRAPH_FILE_PREFIX, FormTitle, TextUtil.EXT_TSV);
            DirectoryEx.CreateForFilePath(filePath);
            using (var saver = new FileSaver(filePath))
            {
                File.WriteAllText(saver.SafeName, graphData.ToString());
                saver.Commit();
            }
            return filePath.ToForwardSlashPath();
        }

        /// <summary>Renders the graph to a PNG file and returns the path. Writes to a temp path when
        /// <paramref name="filePath"/> is null.</summary>
        public string GetImage(string filePath)
        {
            using (var bitmap = RenderBitmap())
            {
                filePath = filePath ?? JsonUiService.GetMcpTmpFilePath(GRAPH_FILE_PREFIX, FormTitle, JsonUiService.EXT_PNG);
                DirectoryEx.CreateForFilePath(filePath);
                using (var saver = new FileSaver(filePath))
                {
                    bitmap.Save(saver.SafeName, ImageFormat.Png);
                    saver.Commit();
                }
            }
            return filePath.ToForwardSlashPath();
        }

        /// <summary>Renders the graph to PNG bytes returned inline, with a server-suggested fallback file path
        /// (which this call does NOT write). Companion to <see cref="GetImage"/>.</summary>
        public ImageBytesMetadata GetImageBytes()
        {
            using (var bitmap = RenderBitmap())
            {
                return new ImageBytesMetadata
                {
                    Data = JsonUiService.BitmapToPngBytes(bitmap),
                    FilePath = JsonUiService.GetMcpTmpFilePath(GRAPH_FILE_PREFIX, FormTitle, JsonUiService.EXT_PNG).ToForwardSlashPath(),
                    MimeType = JsonUiService.MIME_TYPE_PNG
                };
            }
        }

        /// <summary>The region of DATA coordinates the graph is currently zoomed to - the first pane's X and
        /// Y axis ranges - as a <see cref="SkylineTool.Rectangle"/>.</summary>
        public SkylineTool.Rectangle GetZoom()
        {
            return PaneRectangle(FirstPane());
        }

        /// <summary>Zooms the first pane to the DATA-coordinate rectangle <paramref name="bounds"/>, returning
        /// the zoom actually applied (the graph may clamp it to the data range). A zoom has no direction, so
        /// the edges are normalized into the Min &lt; Max ranges ZedGraph expects.
        ///
        /// <para>An EQUAL edge pair asks for no zoom in that direction and leaves that axis exactly as it was:
        /// equal left and right zoom vertically only, equal top and bottom horizontally only, and a rectangle
        /// with no width and no height changes nothing. That is also what keeps a zoom from writing
        /// Min == Max, which nothing repairs once the auto flags are off.</para></summary>
        public SkylineTool.Rectangle ZoomTo(SkylineTool.Rectangle bounds)
        {
            RequireRectangle(bounds);
            RequireFinite(bounds);
            var pane = FirstPane();
            bool wantsX = bounds.Left != bounds.Right;
            bool wantsY = bounds.Top != bounds.Bottom;
            if (!wantsX && !wantsY)
                return PaneRectangle(pane);

            // An axis with nothing asked of it is passed no range, which leaves its auto flags alone as well
            // as its scale. The rectangle returned is read back off the pane, so it shows what a direction
            // the graph refused to move actually did.
            Control.ZoomPaneToScale(pane,
                wantsX ? Math.Min(bounds.Left, bounds.Right) : (double?) null,
                wantsX ? Math.Max(bounds.Left, bounds.Right) : (double?) null,
                wantsY ? Math.Min(bounds.Top, bounds.Bottom) : (double?) null,
                wantsY ? Math.Max(bounds.Top, bounds.Bottom) : (double?) null);
            return PaneRectangle(pane);
        }


        /// <summary>Clicks or drags on the graph in DATA coordinates: the mouse goes down at the Left/Top
        /// corner of <paramref name="bounds"/> and is released at the Right/Bottom corner. A zero-size
        /// rectangle is a stationary click - down, up, then the click, since some panes select on mouse-down
        /// (the RT regression graph) and others only on click (the CV histogram). Anything larger is a drag,
        /// which raises no click and needs a move for listeners that track one (the chromatogram
        /// peak-boundary adjust).</summary>
        public void Click(SkylineTool.Rectangle bounds)
        {
            RequireRectangle(bounds);
            var pane = FirstPane();
            // GeneralTransform extrapolates past the axes, so a Y below the axis line maps below the chart,
            // into the peak-boundary band.
            var down = pane.GeneralTransform(new PointF((float) bounds.Left, (float) bounds.Top), CoordType.AxisXYScale);
            var up = pane.GeneralTransform(new PointF((float) bounds.Right, (float) bounds.Bottom), CoordType.AxisXYScale);
            bool isDrag = bounds.Left != bounds.Right || bounds.Top != bounds.Bottom;
            Control.PerformMouseDown(LeftClickArgs(down));
            if (isDrag)
                Control.PerformMouseMove(LeftClickArgs(up));
            Control.PerformMouseUp(LeftClickArgs(up));
            if (!isDrag)
                Control.PerformMouseClick(LeftClickArgs(down));
        }

        private Bitmap RenderBitmap()
        {
            return Control.MasterPane.GetImage(Control.MasterPane.IsAntiAlias);
        }

        private GraphPane FirstPane()
        {
            var panes = Control.MasterPane.PaneList;
            if (panes == null || panes.Count == 0)
                throw new ArgumentException(new LlmInstruction(@"Graph has no panes to act on."));
            return panes[0];
        }

        private string FormTitle => FormElement.Form.Text;

        private static void RequireRectangle(SkylineTool.Rectangle bounds)
        {
            if (bounds == null)
                throw new ArgumentException(new LlmInstruction(@"A rectangle is required."));
        }

        // "NaN" and "Infinity" parse as doubles, and an axis given one draws nothing.
        private static void RequireFinite(SkylineTool.Rectangle bounds)
        {
            foreach (var edge in new[] { bounds.Left, bounds.Top, bounds.Right, bounds.Bottom })
            {
                if (double.IsNaN(edge) || double.IsInfinity(edge))
                    throw new ArgumentException(new LlmInstruction(
                        @"Graph coordinates have to be real numbers; NaN and Infinity are not points on an axis."));
            }
        }

        // Left/Right are the X-axis range and Top/Bottom the Y-axis range, Top being the larger Y.
        private static SkylineTool.Rectangle PaneRectangle(GraphPane pane)
        {
            return new SkylineTool.Rectangle
            {
                Left = pane.XAxis.Scale.Min,
                Right = pane.XAxis.Scale.Max,
                Top = pane.YAxis.Scale.Max,
                Bottom = pane.YAxis.Scale.Min
            };
        }

        private static MouseEventArgs LeftClickArgs(PointF pt)
        {
            return new MouseEventArgs(MouseButtons.Left, 1, (int) Math.Round(pt.X), (int) Math.Round(pt.Y), 0);
        }
    }
}
