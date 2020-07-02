/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Text;

namespace pwiz.Skyline.Util
{
    class ConsoleTable
    {
        /// <summary>
        /// This will hold the header of the table.
        /// </summary>
        private string[] _header;

        /// <summary>
        /// This will hold the rows (lines) in the table, not including the
        /// header. I'm using a List of lists because it's easier to deal with...
        /// </summary>
        private readonly List<List<string>> _rows;

        // Unicode borders
        private const char U_HORIZONTAL = '─';
        private const char U_DOWN_RIGHT = '┌';
        private const char U_DOWN_LEFT = '┐';
        private const char U_VERTICAL = '│';
        private const char U_UP_RIGHT = '└';
        private const char U_UP_LEFT = '┘';
        private const char U_VERT_RIGHT = '├';
        private const char U_VERT_LEFT = '┤';
        private const char U_HORZ_DOWN = '┬';
        private const char U_HORZ_UP = '┴';
        private const char U_VERT_HORZ = '┼';

        // ASCII borders
        private const char A_HORIZONTAL = '-';
        private const char A_DOWN_RIGHT = '+';
        private const char A_DOWN_LEFT = '+';
        private const char A_VERTICAL = '|';
        private const char A_UP_RIGHT = '+';
        private const char A_UP_LEFT = '+';
        private const char A_VERT_RIGHT = '+';
        private const char A_VERT_LEFT = '+';
        private const char A_HORZ_DOWN = '+';
        private const char A_HORZ_UP = '+';
        private const char A_VERT_HORZ = '+';

        private char HORIZONTAL { get { return Ascii ? A_HORIZONTAL : U_HORIZONTAL; } }
        private char DOWN_RIGHT { get { return Ascii ? A_DOWN_RIGHT : U_DOWN_RIGHT; } }
        private char DOWN_LEFT { get { return Ascii ? A_DOWN_LEFT : U_DOWN_LEFT; } }
        private char VERTICAL { get { return Ascii ? A_VERTICAL : U_VERTICAL; } }
        private char UP_RIGHT { get { return Ascii ? A_UP_RIGHT : U_UP_RIGHT; } }
        private char UP_LEFT { get { return Ascii ? A_UP_LEFT : U_UP_LEFT; } }
        private char VERT_RIGHT { get { return Ascii ? A_VERT_RIGHT : U_VERT_RIGHT; } }
        private char VERT_LEFT { get { return Ascii ? A_VERT_LEFT : U_VERT_LEFT; } }
        private char HORZ_DOWN { get { return Ascii ? A_HORZ_DOWN : U_HORZ_DOWN; } }
        private char HORZ_UP { get { return Ascii ? A_HORZ_UP : U_HORZ_UP; } }
        private char VERT_HORZ { get { return Ascii ? A_VERT_HORZ : U_VERT_HORZ; } }

        public enum AlignText
        {
            ALIGN_LEFT,
            ALIGN_RIGHT,
        }

        private enum DIVIDER_POS
        {
            ABOVE,
            MIDDLE,
            BELOW,
        }

        public ConsoleTable()
        {
            _header = null;
            _rows = new List<List<string>>();

            Borders = true; // Show borders by default
            SpaceAfter = true; // Empty line after by default
        }

        public string Title { get; set; }
        public string Preamble { get; set; }
        public string Postamble { get; set; }

        /// <summary>
        /// Set text alignment in table cells, either RIGHT or LEFT.
        /// </summary>
        public AlignText TextAlignment { get; set; }

        public bool Borders { get; set; }
        public bool Ascii { get; set; }
        public bool SpaceBefor { get; set; }
        public bool SpaceAfter { get; set; }

        public int[] Widths { get; set; }

        /// <summary>
        /// Total width assuming the last column consumes all excess width
        /// </summary>
        public int? Width { get; set; }

        public void SetHeaders(params string[] h)
        {
            _header = h;
        }

        public void AddRow(params string[] row)
        {
            _rows.Add(new List<string>(row));
        }

        public override string ToString()
        {
            int[] widths = GetWidths();
            string rowFormat = BuildRowFormat(widths);
            int fullWidth = widths.Sum() + widths.Length + 1;

            var sb = new StringBuilder();
            if (SpaceBefor)
                sb.AppendLine();
            if (!string.IsNullOrEmpty(Title))
                sb.AppendLine(Title);
            if (Preamble != null)
                sb.Append(ParaToString(fullWidth, Preamble));
            AppendBorders(sb, widths, DIVIDER_POS.ABOVE); // Top of table border
            if (_header != null)
                sb.AppendFormat(rowFormat, _header.Cast<object>().ToArray());

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_header != null || i > 0)
                {
                    if (Borders)
                        AppendBorders(sb, widths, DIVIDER_POS.MIDDLE);
                    else
                        sb.AppendLine();    // If no boarders separate the rows by a blank line
                }

                // Build cell contents with appropriate text wrapping
                var cells = _rows[i].ToArray();
                while (cells.Any(c => c.Length > 0))
                {
                    var remaining = new string[cells.Length];
                    for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                    {
                        var cellWidth = widths[cellIndex];
                        var cellText = cells[cellIndex];
                        int wrapChars = 0;
                        int wrapIndex = GetWrapIndex(cellText, cellWidth, ref wrapChars);
                        if (wrapIndex == -1)
                            remaining[cellIndex] = string.Empty;
                        else
                        {
                            remaining[cellIndex] = cellText.Substring(wrapIndex);
                            cells[cellIndex] = cells[cellIndex].Substring(0, wrapIndex - wrapChars);
                        }
                    }
                    sb.AppendFormat(rowFormat, cells.Cast<object>().ToArray());
                    cells = remaining;
                }
            }
            AppendBorders(sb, widths, DIVIDER_POS.BELOW);
            if (Postamble != null)
                sb.Append(ParaToString(fullWidth, Postamble));
            if (SpaceAfter)
                sb.AppendLine();

            return sb.ToString();
        }

        private static int GetWrapIndex(string text, int width, ref int wrapWidth)
        {
            if (text == null)
                return -1;

            int breakIndex = text.IndexOfAny(new []{'\r', '\n'});
            if (breakIndex != -1 && breakIndex < width)
            {
                wrapWidth = text[breakIndex] == '\n' ? 1 : 2;
                return breakIndex + wrapWidth;
            }

            if (width <= 0)
                return -1;
            if (text.Length <= width)
                return -1;

            foreach (var lb in new[] { @" ", @"|"})
            {
                int indexBreak = text.LastIndexOf(lb, width, StringComparison.Ordinal);
                if (indexBreak != -1)
                {
                    if (lb == @" ")
                        wrapWidth = 1;
                    return indexBreak + lb.Length;
                }
            }
            return width;   // No line break sequences found, just return the width itself
        }

        private void AppendBorders(StringBuilder hsb, int[] widths, DIVIDER_POS pos)
        {
            if (!Borders)
                return;

            hsb.Append(pos == DIVIDER_POS.ABOVE ? DOWN_RIGHT : (pos == DIVIDER_POS.MIDDLE ? VERT_RIGHT : UP_RIGHT));
            bool first = true;
            foreach (var width in widths)
            {
                if (!first)
                    hsb.Append(pos == DIVIDER_POS.ABOVE ? HORZ_DOWN : (pos == DIVIDER_POS.MIDDLE ? VERT_HORZ : HORZ_UP));
                first = false;

                if (width > 0)
                    hsb.Append(new string(HORIZONTAL, width));
            }
            hsb.Append(pos == DIVIDER_POS.ABOVE ? DOWN_LEFT : (pos == DIVIDER_POS.MIDDLE ? VERT_LEFT : UP_LEFT));
            hsb.AppendLine();
        }

        /// <summary>
        /// Returns a valid format that is to be passed to AppendFormat
        /// member function of StringBuilder.
        /// General form: "|{i, +/-widths[i]}|", where 0 &lt;= i &lt;= widths.Length - 1
        /// and widths[i] represents the maximum width from column 'i'.
        /// </summary>
        /// <param name="widths">The array of widths presented above.</param>
        private string BuildRowFormat(int[] widths)
        {
            var rowFormat = new StringBuilder();
            if (Borders)
                rowFormat.Append(VERTICAL); // Left edge
            for (int i = 0; i < widths.Length; i++)
            {
                if (i > 0)
                    rowFormat.Append(Borders ? VERTICAL : ' '); // Divider
                if (TextAlignment == AlignText.ALIGN_LEFT)
                    rowFormat.Append('{').Append(i).Append(@",-").Append(widths[i]).Append('}');
                else
                    rowFormat.Append('{').Append(i).Append(',').Append(widths[i]).Append('}');
            }
            if (Borders)
                rowFormat.Append(VERTICAL); // Right edge
            rowFormat.AppendLine();
            return rowFormat.ToString();
        }

        /// <summary>
        /// This function will return an array of integers, an element at
        /// position 'i' will return the maximum length from column 'i'
        /// of the table (if we look at the table as a matrix).
        /// </summary>
        private int[] GetWidths()
        {
            int[] widths;
            if (_header != null)
            {
                // Initially we assume that the maximum length from column 'i'
                // is exactly the length of the header from column 'i'.
                widths = new int[_header.Length];
                for (int i = 0; i < _header.Length; i++)
                    widths[i] = _header[i].Split('\n').Max(s => s.Length);
            }
            else
            {
                int count = GetMaxRowCellCount();
                widths = new int[count];
                for (int i = 0; i < count; i++)
                    widths[i] = -1;
            }

            foreach (List<string> row in _rows)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    if (row[i] == null)
                        continue;
                    row[i] = row[i].Trim();
                    int rowWidth = row[i].Split('\n').Max(s => s.Length);
                    if (rowWidth > widths[i])
                        widths[i] = rowWidth ;
                }
            }

            if (Widths != null)
            {
                for (int i = 0; i < Widths.Length; i++)
                {
                    int fixedLen = Widths[i];
                    if (i < widths.Length && fixedLen > 0)
                        widths[i] = fixedLen;
                }
            }
            else if (Width.HasValue)
            {
                widths[widths.Length - 1] = Width.Value - widths.Take(widths.Length - 1).Sum()  // Width of cell text excluding last cell
                                                        - (widths.Length - 1) // cell separators
                                                        - (Borders ? 2 : 0);  // outside borders if applicable
            }
            Assume.IsTrue(widths.All(w => w > 0));

            return widths;
        }

        /// <summary>
        /// This function returns the maximum possible cell count of an
        /// individual row (line). If a header was supplied,
        /// the maximum length of an individual row should equal the
        /// length of the header.
        /// </summary>
        private int GetMaxRowCellCount()
        {
            return _header != null ? _header.Length : _rows.Max(r => r.Count);
        }

        public static string ParaToString(int width, string text, bool spaceAfter = false)
        {
            var ctPara = new ConsoleTable { Borders = false, Width = width, SpaceAfter = spaceAfter};
            ctPara.AddRow(text);
            return ctPara.ToString();
        }
    }
}
