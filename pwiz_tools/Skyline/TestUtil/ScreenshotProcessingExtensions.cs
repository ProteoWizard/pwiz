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
using System.Linq;
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

        public static void DrawBoxOnColumn(this Graphics g, DocumentGridForm documentGridForm, int column, int rows, Color color, int lineWidth = 3)
        {
            var rect = documentGridForm.DataGridView.GetCellDisplayRectangle(column, 0, true); // column's top data cell
            rect.Y += GetGridViewYOffset(documentGridForm);
            rect.Height *= rows; // draw rectangle around all rows
            g.DrawRectangle(new Pen(color, lineWidth), rect);
        }

        public static void DrawEllipseOnCell(this Graphics g, DocumentGridForm documentGridForm, int row, int column, Color color, int lineWidth = 3)
        {
            var dataGridView = documentGridForm.DataGridView;
            var text = dataGridView.Rows[row].Cells[column].FormattedValue?.ToString();
            var stringSize = g.MeasureString(text, dataGridView.Font);

            var rect = dataGridView.GetCellDisplayRectangle(column, row, true);
            rect.Y += GetGridViewYOffset(documentGridForm);
            rect.Width = Convert.ToInt16(stringSize.Width * 1.1); // scale-up ellipse size so shape isn't too tight around text

            g.DrawEllipse(new Pen(color, lineWidth), rect);
        }
        private static int GetGridViewYOffset(DocumentGridForm documentGridForm)
        {
            // compute top-left corner of data grid's cells, 4px offset puts shapes in the correct place
            return documentGridForm.NavBar.Height + documentGridForm.DataGridView.ColumnHeadersHeight - 4;
        }
    }
}
