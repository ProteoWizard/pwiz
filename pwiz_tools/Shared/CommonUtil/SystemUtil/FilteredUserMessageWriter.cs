/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pwiz.Common.SystemUtil
{
    public class FilteredUserMessageWriter : TextWriter
    {
        private readonly IList<string> _filterStrings;
        private readonly IList<(string Pattern, string Replacement)> _replacements;

        private const string REPLACEMENT_START = "s/";
        public FilteredUserMessageWriter(IList<string> filterStrings)
        {
            _filterStrings = filterStrings.Where(s => !s.StartsWith(REPLACEMENT_START)).ToArray();
            _replacements = filterStrings.Where(s => s.StartsWith(REPLACEMENT_START)).Select(GetReplacement).ToArray();
        }

        private static (string Pattern, string Replacement) GetReplacement(string s)
        {
            string replacementTrimmed = s.Substring(REPLACEMENT_START.Length);
            if (replacementTrimmed.EndsWith(@"/"))
                replacementTrimmed = replacementTrimmed.Substring(0, replacementTrimmed.Length - 1);
            
            var parts = SplitReplacement(replacementTrimmed);
            if (parts.Count != 2)
                throw new ArgumentException(@"Invalid substitution string {0}. Ensure it follows the format s/regex/replacement/.", s);
            return (parts[0], parts[1]);
        }

        private static IList<string> SplitReplacement(string s)
        {
            var parts = new List<string>();
            bool isEscaped = false;
            var currentBuilder = new StringBuilder();
            foreach (char c in s)
            {
                if (isEscaped)
                {
                    // Add the escaped character to the current part
                    currentBuilder.Append(c);
                    isEscaped = false;
                }
                else if (c == '\\')
                {
                    // Mark the next character as escaped
                    isEscaped = true;
                }
                else if (c == '/')
                {
                    // Split on unescaped slash
                    parts.Add(currentBuilder.ToString());
                    currentBuilder.Clear();
                }
                else
                {
                    // Add the character to the current part
                    currentBuilder.Append(c);
                }
            }
            // Add the last part
            parts.Add(currentBuilder.ToString());
            return parts;
        }

        public override void WriteLine(string line)
        {
            // Skip lines that contain any of the filter strings
            if (_filterStrings.Any(line.Contains))
                return;
            foreach (var replacement in _replacements)
                line = Regex.Replace(line, replacement.Pattern, replacement.Replacement);
            Messages.WriteAsyncUserMessage(line);
        }

        public override Encoding Encoding => Encoding.Unicode;  // In memory only
    }
}
