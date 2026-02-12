/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// A collection of extensions to the <see cref="Bitmap"/> class in support
    /// of screenshot processing for tutorial screenshots.
    /// </summary>
    public static class ScreenshotProcessingExtensions
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

        /// <summary>
        /// Determines if the operating system is Windows 11.
        /// </summary>
        private static bool IsWindows11()
        {
            var osVersion = Environment.OSVersion;
            if (osVersion.Platform == PlatformID.Win32NT && osVersion.Version.Major == 10)
            {
                // Windows 11 has version 10.0 with a build number >= 22000
                return osVersion.Version.Build >= 22000;
            }
            return false;
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

        public static readonly Color ANNOTATION_COLOR = Color.FromArgb(192, 0, 0);

        public static Bitmap DrawArrowOnBitmap(this Bitmap bmp, PointF startPointF, PointF endPointF, int tailWidth = 6, int arrowHeadWidth = 16, int arrowHeadHeight = 16)
        {
            using var g = Graphics.FromImage(bmp);

            var startPoint = startPointF.ToPoint(bmp.Size);
            var endPoint = endPointF.ToPoint(bmp.Size);

            // Set high quality for smoother drawing
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Define direction vector for the arrow head
            double angle = Math.Atan2(endPoint.Y - startPoint.Y, endPoint.X - startPoint.X);
            double sinAngle = Math.Sin(angle);
            double cosAngle = Math.Cos(angle);

            // Create a pen for the arrow tail with specified tail width
            using var pen = new Pen(ANNOTATION_COLOR, tailWidth);

            pen.StartCap = LineCap.Flat;
            pen.EndCap = LineCap.Flat;

            // Calculate the point where the tail should end (start of the arrowhead)
            var tailEndPoint = new Point(
                (int)(endPoint.X - arrowHeadWidth * cosAngle),
                (int)(endPoint.Y - arrowHeadWidth * sinAngle)
            );

            // Draw the tail of the arrow
            g.DrawLine(pen, startPoint, tailEndPoint);

            // Calculate arrowhead points
            using SolidBrush brush = new SolidBrush(ANNOTATION_COLOR);

            Point[] arrowHead = 
            {
                endPoint, // Tip of the arrow
                new Point(
                    (int)(endPoint.X - arrowHeadWidth * cosAngle + arrowHeadHeight * sinAngle / 2),
                    (int)(endPoint.Y - arrowHeadWidth * sinAngle - arrowHeadHeight * cosAngle / 2)
                ),
                new Point(
                    (int)(endPoint.X - arrowHeadWidth * cosAngle - arrowHeadHeight * sinAngle / 2),
                    (int)(endPoint.Y - arrowHeadWidth * sinAngle + arrowHeadHeight * cosAngle / 2)
                )
            };

            // Draw the arrow head
            g.FillPolygon(brush, arrowHead);

            return bmp;
        }

        public static Bitmap DrawAnnotationRectOnBitmap(this Bitmap bmp, RectangleF rectF, int lineWidth = 3)
        {
            using var g = Graphics.FromImage(bmp);
            using var pen = new Pen(ANNOTATION_COLOR, lineWidth);
            var rect = rectF.ToRect(bmp.Size);
            // Shrink the rectangle to be drawn entirely inside the give rectangle.
            rect.X += 1;
            rect.Y += 1;
            rect.Width -= lineWidth;
            rect.Height -= lineWidth;
            g.DrawRectangle(pen, rect);
            return bmp;
        }

        public static Bitmap DrawAnnotationTextOnBitmap(this Bitmap bmp, PointF anchorPointF, string text, Color? color = null)
        {
            var backgroundColor = color ?? Color.White;

            using var g = Graphics.FromImage(bmp);
            var font = new Font(@"Tahoma", 16);
            var size = TextRenderer.MeasureText(g, text, font);

            TextRenderer.DrawText(g,
                text,
                font,
                new Rectangle(ToPoint(anchorPointF, bmp.Size), size),
                ANNOTATION_COLOR,
                backgroundColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            return bmp;
        }

        /// <summary>
        /// Draws a vertical forward bracket:
        ///  ┌
        ///  -
        ///  └
        /// and a center line extending horizontally at the midpoint, same length as the arms.
        /// </summary>
        public static Bitmap DrawVerticalForwardBracket(this Bitmap bmp, PointF locationF, float lengthF, float armLengthF, int lineWidth = 6)
        {
            return bmp.DrawBracket(Orientation.Vertical, BracketDirection.forward, locationF, lengthF, armLengthF, lineWidth);
        }

        /// <summary>
        /// Draws a vertical backward bracket:
        ///  ┐
        ///  -
        ///  ┘
        /// and a center line extending horizontally at the midpoint, same length as the arms.
        /// </summary>
        public static Bitmap DrawVerticalBackwardBracket(this Bitmap bmp, PointF locationF, float lengthF, float armLengthF, int lineWidth = 6)
        {
            return bmp.DrawBracket(Orientation.Vertical, BracketDirection.backward, locationF, lengthF, armLengthF, lineWidth);
        }

        /// <summary>
        /// Draws a horizontal forward bracket:
        ///  ┌─|-┐
        /// 
        /// and a center line extending vertically at the midpoint, same length as the arms.
        /// </summary>
        public static Bitmap DrawHorizontalForwardBracket(this Bitmap bmp, PointF locationF, float lengthF, float armLengthF, int lineWidth = 6)
        {
            return bmp.DrawBracket(Orientation.Horizontal, BracketDirection.forward, locationF, lengthF, armLengthF, lineWidth);
        }

        /// <summary>
        /// Draws a horizontal backward bracket:
        /// 
        ///  └─|-┘
        /// and a center line extending vertically at the midpoint, same length as the arms.
        /// </summary>
        public static Bitmap DrawHorizontalBackwardBracket(this Bitmap bmp, PointF locationF, float lengthF, float armLengthF, int lineWidth = 6)
        {
            return bmp.DrawBracket(Orientation.Horizontal, BracketDirection.forward, locationF, lengthF, armLengthF, lineWidth);
        }

        enum BracketDirection { forward, backward }

        private static Bitmap DrawBracket(this Bitmap bmp, Orientation orientation, BracketDirection bracketDirection,
            PointF locationF, float lengthF, float armLengthF, int lineWidth)
        {
            using var g = Graphics.FromImage(bmp);
            var location = locationF.ToPoint(bmp.Size);
            int length = (int)(lengthF * bmp.Height);
            int armLength = (int)(armLengthF * bmp.Width);
            armLength *= bracketDirection == BracketDirection.forward ? 1 : -1;
            using var pen = new Pen(ANNOTATION_COLOR, lineWidth);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;

            if (orientation == Orientation.Vertical)
            {
                // Main vertical line
                g.DrawLine(pen, location.X, location.Y, location.X, location.Y + length);

                // Arms
                g.DrawLine(pen, location.X, location.Y, location.X + armLength, location.Y);
                g.DrawLine(pen, location.X, location.Y + length, location.X + armLength, location.Y + length);
                // Center line
                int center = location.Y + length / 2;
                g.DrawLine(pen, location.X, center, location.X - armLength, center);
            }
            else
            {
                // Main horizontal line
                g.DrawLine(pen, location.X, location.Y, location.X + length, location.Y);

                // Arms
                g.DrawLine(pen, location.X, location.Y, location.X, location.Y + armLength);
                g.DrawLine(pen, location.X + length, location.Y, location.X + length, location.Y + armLength);
                // Center line
                int center = location.X + length / 2;
                g.DrawLine(pen, center, location.Y, center, location.Y - armLength);
            }

            return bmp;
        }

        public static Bitmap Expand(this Bitmap bmp, float left = 0, float right = 0, float top = 0, float bottom = 0, Color? color = null)
        {
            var backgroundColor = color ?? Color.White;
            int newWidth = (int)(bmp.Width + left * bmp.Width + right * bmp.Width);
            int newHeight = (int)(bmp.Height + top * bmp.Height + bottom * bmp.Height);
            var newBitmap = new Bitmap(newWidth, newHeight);
            using var g = Graphics.FromImage(newBitmap);
            g.Clear(backgroundColor);
            g.DrawImage(bmp, new PointF(left, top).ToPoint(bmp.Size));
            return newBitmap;
        }

        public static Bitmap Inflate(this Bitmap bmp, float scalingFactor)
        {
            return new Bitmap(bmp, (int)(bmp.Width * scalingFactor), (int)(bmp.Height * scalingFactor));
        }

        private static Point ToPoint(this PointF pointF, Size size)
        {
            return new Point((int)(pointF.X * size.Width), (int)(pointF.Y * size.Height));
        }

        private static Rectangle ToRect(this RectangleF rectF, Size size)
        {
            return new Rectangle(rectF.Location.ToPoint(size),
                new Size((int)(rectF.Width * size.Width), (int)(rectF.Height * size.Height)));
        }

        // Display a placeholder message on a tutorial screenshot. Use when screenshots aren't correct yet and need to be updated.
        public static Bitmap DrawPlaceholderTextOnBitmap(this Bitmap bmp)
        {
            // CONSIDER(ekoneil): support wrapping text on narrow images
            using var g = Graphics.FromImage(bmp);
            var font = new Font(@"Tahoma", 12);
            var text = "Placeholder screenshot, see test for more info";
            var size = TextRenderer.MeasureText(g, text, font);

            TextRenderer.DrawText(g,
                text,
                font,
                new Rectangle(25, 25, size.Width, size.Height),
                Color.Black,
                Color.Yellow,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.GlyphOverhangPadding);

            return bmp;
        }

        public static void DrawBoxOnColumn(this Graphics g, DocumentGridForm documentGridForm, int column, double rows, Color? color = null, int lineWidth = 3)
        {
            var rect = GetBitmapCellRectangle(documentGridForm, 0, column); // column's top data cell
            rect.Height = (int)(rect.Height * rows); // draw rectangle around all rows
            g.DrawRectangle(new Pen(color ?? ANNOTATION_COLOR, lineWidth), rect);
        }

        public static void DrawEllipseOnCell(this Graphics g, DocumentGridForm documentGridForm, int row, int column, Color? color = null, int lineWidth = 3)
        {
            var dataGridView = documentGridForm.DataGridView;
            var text = dataGridView.Rows[row].Cells[column].FormattedValue?.ToString();
            var stringSize = g.MeasureString(text, dataGridView.Font);

            var rect = GetBitmapCellRectangle(documentGridForm, row, column); // column's top data cell

            rect.Width = Convert.ToInt16(stringSize.Width * 1.1); // scale-up ellipse size so shape isn't too tight around text

            g.DrawEllipse(new Pen(color ?? ANNOTATION_COLOR, lineWidth), rect);
        }

        private static Rectangle GetBitmapCellRectangle(DocumentGridForm documentGridForm, int row, int column)
        {
            var rectBitmap = ScreenshotManager.GetFramedWindowBounds(documentGridForm);
            var rect = documentGridForm.DataGridView.GetCellDisplayRectangle(column, row, true); // column's top data cell
            rect = documentGridForm.DataGridView.RectangleToScreen(rect);
            rect.X -= rectBitmap.X;
            rect.Y -= rectBitmap.Y;
            return rect;
        }

        /// <summary>
        /// Draws the state of multiple ProgressBars to cover up
        /// any animation that Windows may have drawn on the completed progress
        /// </summary>
        public static Bitmap FillProgressBars(this Bitmap bmp, IEnumerable<ProgressBar> progressBars)
        {
            var result = bmp;
            foreach (var progressBar in progressBars)
                result = result.FillProgressBar(progressBar);
            return result;
        }

        /// <summary>
        /// Draws the state of a ProgressBar on the ProgressBar to cover up
        /// any animation that Windows may have drawn on the completed progress
        /// </summary>
        public static Bitmap FillProgressBar(this Bitmap bmp, ProgressBar progressBar)
        {
            var bitmapForm = FormEx.GetParentForm(progressBar);
            var formRect = ScreenshotManager.GetFramedWindowBounds(bitmapForm);
            var progressControlRect = progressBar.RectangleToScreen(progressBar.ClientRectangle);
            progressControlRect.Offset(-formRect.Left, -formRect.Top);  // Into bitmap coordinates
            var progressRect = progressControlRect; // Copy values for percent complete bar
            progressRect.Inflate(-1, -1);   // Exclude the border

            // Do the necessary drawing
            return bmp.RenderAsControl(progressBar, g =>
            {
                using var brushControl = new SolidBrush(Color.FromArgb(230, 230, 230)); // Slightly darker than Control
                g.FillRectangle(brushControl, progressRect);

                progressRect.Width = (int)Math.Round(progressRect.Width * progressBar.Value / 100.0);
                using var brush = new SolidBrush(Color.FromArgb(6, 176, 37));   // The color that gets used
                g.FillRectangle(brush, progressRect);

                if (progressBar is CustomTextProgressBar customProgressBar)
                {
                    customProgressBar.DrawText(g, progressControlRect);
                }

                using var pen = new Pen(Color.FromArgb(188, 188, 188));
                progressControlRect.Width -= 1; // Pens draw to the right
                progressControlRect.Height -= 1; // Pens draw below
                g.DrawRectangle(pen, progressControlRect);
            });
        }

        /// <summary>
        /// Creates a MemoryDC based on a control and renders with it using the
        /// rendering function argument.
        /// </summary>
        /// <param name="bmp">The Bitmap to draw on</param>
        /// <param name="control">The control to match as closely as possible</param>
        /// <param name="render">The function that does the actual drawing</param>
        private static Bitmap RenderAsControl(this Bitmap bmp, Control control, Action<Graphics> render)
        {
            var hdc = User32.GetDC(control.Handle);
            try
            {
                var memDc = Gdi32.CreateCompatibleDC(hdc);
                try
                {
                    var hBmp = bmp.GetHbitmap();
                    var oldBmp = Gdi32.SelectObject(memDc, hBmp);

                    using (var g = Graphics.FromHdc(memDc))
                    {
                        render(g);
                    }

                    Gdi32.SelectObject(memDc, oldBmp);
                    
                    var result = Image.FromHbitmap(hBmp);
                    
                    Gdi32.DeleteObject(hBmp);

                    return result;
                }
                finally
                {
                    Gdi32.DeleteDC(memDc);
                }
            }
            finally
            {
                User32.ReleaseDC(control.Handle, hdc);
            }
        }
    }
}
