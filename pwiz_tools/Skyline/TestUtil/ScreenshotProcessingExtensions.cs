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
