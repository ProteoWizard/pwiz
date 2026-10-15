/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Text constants and helpers for delimited files and indented log lines. The names and
    /// signatures mirror Skyline's <c>pwiz.Skyline.Util.Extensions.TextUtil</c>, so that when
    /// that class moves to CommonUtil (which Osprey can reference) this one can be deleted and
    /// its callers changed only by a using directive. Two differences until then:
    /// <see cref="ToDsvLine"/> does not quote fields, because no Osprey file has ever quoted
    /// one and a reader of its files would see changed bytes, and <see cref="TAB_SIZE"/> is 2,
    /// the indent Osprey's log has always used.
    /// </summary>
    public static class TextUtil
    {
        public const string EXT_TSV = @".tsv";
        public const string EXT_CSV = @".csv";

        public const char SEPARATOR_TSV = '\t';
        public static readonly string SEPARATOR_TSV_STR = SEPARATOR_TSV.ToString();

        /// <summary>
        /// The line ending of every file Osprey writes for comparison with its Rust original,
        /// which always writes LF - never <see cref="System.Environment.NewLine"/>, which is
        /// CRLF on Windows.
        /// </summary>
        public const string LF = "\n";
        public const string CRLF = "\r\n";

        /// <summary>
        /// Lowercase boolean text, as Rust and JSON write it, for files and hash keys that
        /// must not depend on .NET's "True"/"False".
        /// </summary>
        public const string TRUE_TEXT = @"true";
        public const string FALSE_TEXT = @"false";

        public const int TAB_SIZE = 2;

        /// <summary>
        /// <see cref="TRUE_TEXT"/> or <see cref="FALSE_TEXT"/>.
        /// </summary>
        public static string ToLowerText(this bool value)
        {
            return value ? TRUE_TEXT : FALSE_TEXT;
        }

        /// <summary>
        /// Joins fields into one line of a delimiter-separated file. Unlike Skyline's version,
        /// fields are written as they are, never quoted.
        /// </summary>
        public static string ToDsvLine(this IEnumerable<string> fields, char separator)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (string field in fields)
            {
                if (!first)
                    sb.Append(separator);
                sb.Append(field);
                first = false;
            }
            return sb.ToString();
        }

        public static string GetIndentation(int indentLevel, int tabSize = TAB_SIZE)
        {
            if (indentLevel <= 0)
                return string.Empty;

            return new string(' ', tabSize * indentLevel);
        }

        public static string Indent(this string s, int indentLevel, int tabSize = TAB_SIZE)
        {
            if (s == null || indentLevel <= 0)
                return s;

            return GetIndentation(indentLevel, tabSize) + s;
        }

        /// <summary>
        /// Utility function for <see cref="string"/> like <see cref="File"/> ReadLines(): the
        /// lines of <paramref name="text"/> without their endings (CRLF, LF or CR), and no empty
        /// last line for text that ends with a line ending.
        /// </summary>
        public static IEnumerable<string> ReadLines(this string text)
        {
            var lines = new List<string>();
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    lines.Add(line);
            }
            return lines;
        }
    }
}
