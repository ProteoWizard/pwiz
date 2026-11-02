/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// A small reference table in usage output - the values an option takes, such as an
    /// enzyme list - kept compact and set apart from the <see cref="ArgumentGroup{TContext}"/>
    /// sections so it does not read as one: a one-line caption, then the rows wrapped into
    /// side-by-side column groups (read down each group), indented and borderless in plain
    /// text and in a small indented table in HTML.
    /// </summary>
    public class TableUsageBlock : IUsageBlock
    {
        private const int INDENT = 4;
        private const string GROUP_GAP = @"   ";

        public TableUsageBlock(string title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
            Title = title;
            Headers = headers;
            Rows = rows.ToList();
            ColumnGroups = 1;
        }

        public string Title { get; private set; }
        public string Preamble { get; set; }
        public IReadOnlyList<string> Headers { get; private set; }
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; private set; }

        /// <summary>
        /// The most side-by-side groups the rows wrap into. HTML uses this many; plain text
        /// uses as many as fit its width.
        /// </summary>
        public int ColumnGroups { get; set; }

        /// <summary>
        /// The columns, by their index in <see cref="Headers"/>, whose values are right-aligned
        /// (numbers, so their digits line up). Empty by default.
        /// </summary>
        public IReadOnlyCollection<int> RightAlignedColumns { get; set; } = Array.Empty<int>();

        public string ToString(int width, string formatType)
        {
            var sb = new StringBuilder(ConsoleTable.ParaToString(width, Caption).TrimEnd());
            string indent = new string(' ', INDENT);
            // The most groups whose widest line fits; one group always does what it can.
            IList<string> lines = null;
            for (int groups = Math.Max(1, ColumnGroups); groups >= 1; groups--)
            {
                lines = PlainTextLines(groups);
                if (groups == 1 || lines.Max(l => l.Length) + INDENT <= width)
                    break;
            }
            foreach (string line in lines)
                sb.AppendLine().Append(indent + line.TrimEnd());
            return sb.AppendLine().AppendLine().ToString();
        }

        public string ToHtmlString()
        {
            // ReSharper disable LocalizableElement
            const string padding = "padding:1px 6px; border:none";
            const string gapPadding = "padding:1px 6px 1px 24px; border:none";
            var sb = new StringBuilder();
            sb.Append("<p style=\"font-size:90%; margin:4px 0 2px 2em\"><b>").Append(UsageHtmlEncoder.Encode(Title))
                .Append("</b>");
            if (Preamble != null)
                sb.Append(": ").Append(UsageHtmlEncoder.Encode(Preamble));
            sb.AppendLine("</p>");
            sb.AppendLine("<table style=\"margin:0 0 16px 2em; font-size:85%; border:none\">");
            int groups = Math.Max(1, ColumnGroups);
            foreach (var row in WrappedRows(groups))
            {
                sb.Append("<tr>");
                for (int i = 0; i < row.Count; i++)
                {
                    bool groupStart = i > 0 && i % Headers.Count == 0;
                    string tag = row.IsHeader ? "th" : "td";
                    string align = IsRightAligned(i) ? "; text-align:right" : string.Empty;
                    sb.Append('<').Append(tag).Append(" style=\"").Append(groupStart ? gapPadding : padding)
                        .Append(align).Append("\">")
                        .Append(UsageHtmlEncoder.Encode(row[i])).Append("</").Append(tag).Append('>');
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
            return sb.ToString();
            // ReSharper restore LocalizableElement
        }

        /// <summary>True when the cell at <paramref name="cellIndex"/> of a wrapped row is in a right-aligned column.</summary>
        private bool IsRightAligned(int cellIndex)
        {
            return RightAlignedColumns.Contains(cellIndex % Headers.Count);
        }

        private string Caption
        {
            get { return Preamble == null ? Title + @":" : Title + @": " + Preamble; }
        }

        /// <summary>The rows wrapped into <paramref name="groups"/> groups, padded to columns, as text lines.</summary>
        private IList<string> PlainTextLines(int groups)
        {
            var rows = WrappedRows(groups).ToList();
            int columns = rows.Max(r => r.Count);
            var widths = new int[columns];
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Count; i++)
                    widths[i] = Math.Max(widths[i], row[i].Length);
            }
            return rows.Select(row =>
            {
                var line = new StringBuilder();
                for (int i = 0; i < row.Count; i++)
                {
                    if (i > 0)
                        line.Append(i % Headers.Count == 0 ? GROUP_GAP + @" " : @" ");
                    line.Append(IsRightAligned(i) ? row[i].PadLeft(widths[i]) : row[i].PadRight(widths[i]));
                }
                return line.ToString();
            }).ToList();
        }

        /// <summary>
        /// The header row (repeated per group) and the data rows wrapped column-major into
        /// <paramref name="groups"/> side-by-side groups, so each group reads top to bottom.
        /// </summary>
        private IEnumerable<WrappedRow> WrappedRows(int groups)
        {
            int perGroup = (Rows.Count + groups - 1) / groups;
            int usedGroups = perGroup == 0 ? 1 : (Rows.Count + perGroup - 1) / perGroup;
            yield return new WrappedRow(Enumerable.Range(0, usedGroups).SelectMany(_ => Headers), true);
            for (int r = 0; r < perGroup; r++)
            {
                var cells = new List<string>();
                for (int g = 0; g < usedGroups; g++)
                {
                    int index = g * perGroup + r;
                    if (index < Rows.Count)
                        cells.AddRange(Rows[index]);
                    else
                        cells.AddRange(Headers.Select(_ => string.Empty));
                }
                yield return new WrappedRow(cells, false);
            }
        }

        private sealed class WrappedRow : List<string>
        {
            public WrappedRow(IEnumerable<string> cells, bool isHeader) : base(cells)
            {
                IsHeader = isHeader;
            }

            public bool IsHeader { get; private set; }
        }
    }
}
