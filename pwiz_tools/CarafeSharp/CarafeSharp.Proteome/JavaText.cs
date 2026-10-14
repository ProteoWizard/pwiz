/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Globalization;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The Java string and number behaviors Carafe's FASTA handling depends on, where .NET's
    /// nearest equivalent differs: <c>String.trim</c>, <c>Character.isWhitespace</c>,
    /// <c>String.split</c> and <c>replaceAll</c> with the specific patterns Carafe uses, and
    /// <c>Math.round</c>. Each is only as general as those call sites need.
    /// </summary>
    public static class JavaText
    {
        /// <summary>
        /// <c>String.trim()</c>: removes every character up to and including U+0020 from both
        /// ends, which is not the Unicode whitespace set <see cref="string.Trim()"/> uses.
        /// </summary>
        public static string Trim(string s)
        {
            int start = 0;
            int end = s.Length;
            while (start < end && s[start] <= ' ')
                start++;
            while (end > start && s[end - 1] <= ' ')
                end--;
            return start == 0 && end == s.Length ? s : s.Substring(start, end - start);
        }

        /// <summary>
        /// <c>Character.isWhitespace(char)</c>: Unicode space, line and paragraph separators
        /// except the three non-breaking spaces, plus the ASCII controls TAB, LF, VT, FF, CR and
        /// FS through US. Unlike <see cref="char.IsWhiteSpace(char)"/> it excludes U+0085 and
        /// U+00A0.
        /// </summary>
        public static bool IsWhitespace(char c)
        {
            if (c <= ' ')
                return c == ' ' || (c >= '\t' && c <= '\r') || (c >= '\u001C' && c <= '\u001F');
            if (c == '\u00A0' || c == '\u2007' || c == '\u202F')
                return false;
            var category = char.GetUnicodeCategory(c);
            return category == UnicodeCategory.SpaceSeparator ||
                   category == UnicodeCategory.LineSeparator ||
                   category == UnicodeCategory.ParagraphSeparator;
        }

        /// <summary>
        /// The first token of <c>s.split("\\s+", 2)</c> for an already trimmed string: everything
        /// before the first ASCII whitespace character (Java's <c>\s</c> is ASCII only).
        /// </summary>
        public static string FirstToken(string trimmed)
        {
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (IsRegexWhitespace(trimmed[i]))
                    return trimmed.Substring(0, i);
            }
            return trimmed;
        }

        /// <summary>
        /// <c>s.split(regex)</c> for a regex that matches one literal character: trailing empty
        /// strings are removed, but a string with no separator at all comes back whole, even
        /// when it is empty.
        /// </summary>
        public static string[] Split(string s, char separator)
        {
            if (s.IndexOf(separator) < 0)
                return new[] { s };
            var parts = new List<string>(s.Split(separator));
            while (parts.Count > 0 && parts[parts.Count - 1].Length == 0)
                parts.RemoveAt(parts.Count - 1);
            return parts.ToArray();
        }

        /// <summary>
        /// <c>s.replaceAll("^\\*", "").replaceAll("\\*$", "")</c>: removes one leading asterisk
        /// and one final one. Java's <c>$</c> also matches just before a final line terminator,
        /// so an asterisk followed only by one is removed too.
        /// </summary>
        public static string StripTerminalAsterisks(string s)
        {
            if (s.Length > 0 && s[0] == '*')
                s = s.Substring(1);
            int terminator = FinalLineTerminatorLength(s);
            int star = s.Length - terminator - 1;
            if (star >= 0 && s[star] == '*')
                s = s.Remove(star, 1);
            return s;
        }

        /// <summary>
        /// <c>String.toUpperCase()</c> as Carafe runs it (an English locale). Identical to the
        /// invariant culture for everything that can survive digestion.
        /// </summary>
        public static string ToUpper(string s)
        {
            return s.ToUpperInvariant();
        }

        /// <summary>
        /// <c>Math.round(double)</c>: the closest long, ties toward positive infinity, NaN to 0,
        /// and out-of-range values clamped.
        /// </summary>
        public static long Round(double value)
        {
            if (double.IsNaN(value))
                return 0;
            double floor = Math.Floor(value);
            // Exact for any double: value and its floor share an exponent range.
            double rounded = value - floor >= 0.5 ? floor + 1 : floor;
            if (rounded >= long.MaxValue)
                return long.MaxValue;
            if (rounded <= long.MinValue)
                return long.MinValue;
            return (long)rounded;
        }

        private static bool IsRegexWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\u000B' || c == '\f' || c == '\r';
        }

        private static int FinalLineTerminatorLength(string s)
        {
            if (s.Length == 0)
                return 0;
            char last = s[s.Length - 1];
            if (last == '\n')
                return s.Length >= 2 && s[s.Length - 2] == '\r' ? 2 : 1;
            return last == '\r' || last == '\u0085' || last == '\u2028' || last == '\u2029' ? 1 : 0;
        }
    }
}
