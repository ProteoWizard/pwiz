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
using pwiz.Skyline.Controls.Databinding;

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

        public static Bitmap CleanupBorder(this Bitmap bmp, bool titleBarOnly = false)
        {
            // Floating dockable forms have only a transparent border at the top
            if (titleBarOnly)
                return bmp.CleanupBorder(new Rectangle(0, 0, bmp.Width, 1));
            else
                return bmp.CleanupBorder(new Rectangle(0, 0, bmp.Width, bmp.Height));
        }

        public static Bitmap CleanupBorder(this Bitmap bmp, Rectangle rectWindow)
        {
            return bmp.CleanupBorder(STANDARD_BORDER_COLOR, rectWindow);
        }

        private static Bitmap CleanupBorder(this Bitmap bmp, Color? color, Rectangle rect)
        {
            var colorCounts = new Dictionary<Color, int>();
            foreach (var point in RectPoints(rect))
                AddPixel(point, bmp, colorCounts);
            var maxColorCount = colorCounts.Values.Max();
            var bestBorderColor = colorCounts.FirstOrDefault(kvp => kvp.Value == maxColorCount).Key;

            // If no color is specified, use the most common color in the border.
            // This is dependent on the screen background color. So, it should not
            // be used in general, but only to figure out the best color to make
            // the standard for all saved screenshots. Currently: #FF707070
            // Also, use the best color if it is white as it is for stand-alone graphs
            if (!color.HasValue || bestBorderColor.ToArgb() == Color.White.ToArgb())
            {
                color = bestBorderColor;
            }

            foreach (var point in RectPoints(rect))
                bmp.SetPixel(point.X, point.Y, color.Value);
            return bmp;
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

        public static void DrawBoxOnColumn(this Graphics g, DocumentGridForm documentGridForm, int column, int rows, Color? color = null, int lineWidth = 3)
        {
            var rect = documentGridForm.DataGridView.GetCellDisplayRectangle(column, 0, true); // column's top data cell
            rect.Y += GetGridViewYOffset(documentGridForm);
            rect.Height *= rows; // draw rectangle around all rows
            g.DrawRectangle(new Pen(color ?? ANNOTATION_COLOR, lineWidth), rect);
        }

        public static void DrawEllipseOnCell(this Graphics g, DocumentGridForm documentGridForm, int row, int column, Color? color = null, int lineWidth = 3)
        {
            var dataGridView = documentGridForm.DataGridView;
            var text = dataGridView.Rows[row].Cells[column].FormattedValue?.ToString();
            var stringSize = g.MeasureString(text, dataGridView.Font);

            var rect = dataGridView.GetCellDisplayRectangle(column, row, true);
            rect.Y += GetGridViewYOffset(documentGridForm);
            rect.Width = Convert.ToInt16(stringSize.Width * 1.1); // scale-up ellipse size so shape isn't too tight around text

            g.DrawEllipse(new Pen(color ?? ANNOTATION_COLOR, lineWidth), rect);
        }
        private static int GetGridViewYOffset(DocumentGridForm documentGridForm)
        {
            // compute top-left corner of data grid's cells, 4px offset puts shapes in the correct place
            return documentGridForm.NavBar.Height + documentGridForm.DataGridView.ColumnHeadersHeight - 4;
        }
    }
}
