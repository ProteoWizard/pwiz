/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Drawing.Drawing2D;
using System.Linq;

namespace pwiz.Common.GUI
{
    /// <summary>
    /// Repaints the boundary of a window captured from the screen. Windows 11 rounds a window's
    /// corners and blends its frame with whatever lies behind it, so a screen copy carries that in
    /// its edge pixels. The tutorial screenshots and the forms captured over the Skyline MCP both
    /// get the same standard border instead.
    /// </summary>
    public static class ScreenshotBorder
    {
        /// <summary>
        /// The most common color #FF707070 for a window border on Windows 10 with a white background.
        /// On a red background this becomes #FF68251F
        /// </summary>
        private static readonly Color STANDARD_BORDER_COLOR = Color.FromArgb(0x70, 0x70, 0x70);
        /// <summary>
        /// The interior border color for a docked dockable form.
        /// </summary>
        private static readonly Color INTERIOR_BORDER_COLOR = Color.FromArgb(0xA0, 0xA0, 0xA0);

        public const int CORNER_FORM_WINDOWS11 = 8;
        public const int CORNER_TOOL_WINDOW_WINDOWS11 = 4;

        public static int CornerForm => IsWindows11() ? CORNER_FORM_WINDOWS11 : 0;
        public static int CornerToolWindow => IsWindows11() ? CORNER_TOOL_WINDOW_WINDOWS11 : 0;

        public static Rectangle GetToolWindowBorderRect(Rectangle rectWindow)
        {
            return IsWindows11() ? rectWindow : new Rectangle(rectWindow.Location, new Size(rectWindow.Width, 1));
        }

        /// <summary>
        /// Repaints the border of a captured top-level window (<paramref name="toolWindow"/> false)
        /// or floating tool window. A border of one color, or an all-white one (a graph), is left alone.
        /// </summary>
        public static Bitmap CleanupBorder(this Bitmap bmp, bool toolWindow = false)
        {
            bool isWindows11 = IsWindows11();
            if (!toolWindow)
            {
                return bmp.CleanupBorder(new Rectangle(0, 0, bmp.Width, bmp.Height), isWindows11 ? CORNER_FORM_WINDOWS11 : 0);
            }
            else if (!isWindows11)
            {
                // Floating dockable forms have only a transparent border at the top
                return bmp.CleanupBorder(new Rectangle(0, 0, bmp.Width, 1), 0);
            }
            else
            {
                // Floating dockable forms in Windows 11 have a 4 pixel corner radius
                return bmp.CleanupBorder(new Rectangle(0, 0, bmp.Width, bmp.Height), CORNER_TOOL_WINDOW_WINDOWS11);
            }
        }

        public static Bitmap CleanupBorder(this Bitmap bmp, Rectangle rectWindow, int cornerRadius, Rectangle? excludeRect = null)
        {
            return bmp.CleanupBorder(STANDARD_BORDER_COLOR, rectWindow, cornerRadius, excludeRect);
        }

        /// <summary>
        /// Determines if the operating system is Windows 11, which rounds window corners.
        /// </summary>
        public static bool IsWindows11()
        {
            var osVersion = Environment.OSVersion;
            if (osVersion.Platform == PlatformID.Win32NT && osVersion.Version.Major == 10)
            {
                // Windows 11 has version 10.0 with a build number >= 22000
                return osVersion.Version.Build >= 22000;
            }
            return false;
        }

        private static Bitmap CleanupBorder(this Bitmap bmp, Color? color, Rectangle rect, int cornerRadius, Rectangle? excludeRect)
        {
            var colorCounts = GetColorCounts(bmp, rect);

            // If no color is specified, use the most common color in the border.
            // This is dependent on the screen background color. So, it should not
            // be used in general, but only to figure out the best color to make
            // the standard for all saved screenshots. Currently: #FF707070
            var maxColorCount = colorCounts.Values.Max();
            var bestBorderColor = colorCounts.FirstOrDefault(kvp => kvp.Value == maxColorCount).Key;
            // All white border means it is actually a graph so don't draw anything on it
            // Only when the rectangle is the full area of the bitmap
            if (bestBorderColor.ToArgb() == Color.White.ToArgb() && rect == new Rectangle(0, 0, bmp.Width, bmp.Height))
                return bmp;
            // If there is supposed to be a corner curve but there is just one color, avoid
            // drawing a curved corner on top of the otherwise rectangular border.
            if (cornerRadius != 0 && colorCounts.Count == 1)
                return bmp;
            // If the proposed color is the standard border color and the best color is the
            // interior boarder color, then use the interior color. This is the case for
            // docked DockableForms.
            if (color.HasValue &&
                color.Value == STANDARD_BORDER_COLOR &&
                bestBorderColor == INTERIOR_BORDER_COLOR)
            {
                color = bestBorderColor;
                if (cornerRadius != CORNER_TOOL_WINDOW_WINDOWS11)
                    cornerRadius = 0;   // No arched corners on interior borders
            }
            return bmp.CleanupBorderInternal(color ?? bestBorderColor, rect, cornerRadius, excludeRect);
        }

        private static IDictionary<Color, int> GetColorCounts(Bitmap bmp, Rectangle rect)
        {
            var colorCounts = new Dictionary<Color, int>();
            foreach (var point in RectPoints(rect))
                AddPixel(point, bmp, colorCounts);
            return colorCounts;
        }

        private static Bitmap CleanupBorderInternal(this Bitmap bmp, Color color, Rectangle rect, int cornerRadius,
            Rectangle? excludeRect)
        {
            return IsWindows11() && cornerRadius != 0
                ? bmp.CleanupBorder11(color, rect, cornerRadius, excludeRect)
                : bmp.CleanupBorder10(color, rect, excludeRect);
        }

        private static Bitmap CleanupBorder10(this Bitmap bmp, Color color, Rectangle rect, Rectangle? excludeRect)
        {
            using var g = Graphics.FromImage(bmp);
            ExcludeClip(g, excludeRect);

            using var pen = new Pen(color);
            if (rect.Height == 1)
                g.DrawLine(pen, rect.Location, new Point(rect.Right, rect.Top));
            else
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            return bmp;
        }

        // CONSIDER: The Windows API function Gdi32.CreateRoundRectRgn() exists and
        // creates a native rounded rectangle region that should match the exact pixel boundary
        // Windows uses for rounded window corners.
        // This was tested as a replacement for the GraphicsPath approach below but found no
        // meaningful visual difference at normal scale. Left the original implementation, but
        // this API may be useful if future OS changes require revisiting corner rendering.
        private static Bitmap CleanupBorder11(this Bitmap bmp, Color color, Rectangle rect, int cornerRadius,
            Rectangle? excludeRect)
        {
            var result = new Bitmap(bmp.Width, bmp.Height);

            using var g = Graphics.FromImage(result);

            using var backgroundBrush = new SolidBrush(Color.White);
            g.FillRectangle(backgroundBrush, rect);
            using var pathClippingOuter = new GraphicsPath();
            AddRoundedRectangle(pathClippingOuter, rect, cornerRadius);
            using var controlBrush = new SolidBrush(SystemColors.Control);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(pathClippingOuter);
            ExcludeClip(g, excludeRect);
            g.FillRectangle(controlBrush, rect);
            g.ResetClip();

            using var pathDrawing = new GraphicsPath();
            rect.Width--;   // Pens draw right and below the rectangle
            rect.Height--;
            AddRoundedRectangle(pathDrawing, rect, cornerRadius);
            using var pen = new Pen(color);
            ExcludeClip(g, excludeRect);
            g.DrawPath(pen, pathDrawing);
            rect.Width++;
            rect.Height++;

            // Draw the image within the curved shape just drawn
            using var pathClipping = new GraphicsPath();
            rect.Inflate(-1, -1);
            // Corner transparency is very tricky. So, it is necessary to clip the corner
            // areas slightly further in then their true edges.
            AddRoundedRectangle(pathClipping, rect, cornerRadius + cornerRadius/2);
            g.SmoothingMode = SmoothingMode.None;
            g.SetClip(pathClipping);
            ExcludeClip(g, excludeRect);
            g.DrawImage(bmp, rect, rect, GraphicsUnit.Pixel);
            if (excludeRect.HasValue)
            {
                // If a rectangle was excluded from the border drawing, it needs to be copied now.
                g.ResetClip();
                g.DrawImage(bmp, excludeRect.Value, excludeRect.Value, GraphicsUnit.Pixel);
            }

            return result;
        }

        private static void ExcludeClip(Graphics g, Rectangle? excludeRect)
        {
            if (excludeRect.HasValue)
                g.ExcludeClip(excludeRect.Value);
        }

        /// <summary>
        /// Adds a rounded rectangle to a GraphicsPath.
        /// </summary>
        private static void AddRoundedRectangle(GraphicsPath path, Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            int arcWidth = diameter, arcHeight = diameter;
            path.AddArc(rect.Left, rect.Top, arcWidth, arcHeight, 180, 90); // Top-left corner
            path.AddLine(rect.Left + radius, rect.Top, rect.Right - radius, rect.Top); // Top edge
            path.AddArc(rect.Right - arcWidth, rect.Top, arcWidth, arcHeight, 270, 90); // Top-right corner
            path.AddLine(rect.Right, rect.Top + radius, rect.Right, rect.Bottom - radius); // Right edge
            path.AddArc(rect.Right - arcWidth, rect.Bottom - arcHeight, arcWidth, arcHeight, 0, 90); // Bottom-right corner
            path.AddLine(rect.Right - radius, rect.Bottom, rect.Left + radius, rect.Bottom); // Bottom edge
            path.AddArc(rect.Left, rect.Bottom - arcHeight, arcWidth, arcHeight, 90, 90); // Bottom-left corner
            path.AddLine(rect.Left, rect.Bottom - radius, rect.Left, rect.Top + radius); // Left edge
            path.CloseFigure(); // Close the path to ensure it forms a complete shape
        }

        private static void AddPixel(Point point, Bitmap shotPic, IDictionary<Color, int> colorCounts)
        {
            var c = shotPic.GetPixel(point.X, point.Y);
            if (!colorCounts.ContainsKey(c))
                colorCounts.Add(c, 0);
            colorCounts[c]++;
        }

        private static IEnumerable<Point> RectPoints(Rectangle rect)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                yield return new Point(rect.X + x, rect.Y); // upper edge
                yield return new Point(rect.X + x, rect.Y + rect.Height - 1); // lower edge
            }

            for (var y = 0; y < rect.Height; y++)
            {
                yield return new Point(rect.X, rect.Y + y); // left edge
                yield return new Point(rect.X + rect.Width - 1, rect.Y + y); // right edge
            }
        }
    }
}
