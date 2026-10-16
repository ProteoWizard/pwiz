/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Text;

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// A command line as one line of text again, for recording how an artifact was made (a
    /// library's <c>LibMetadata</c> row <c>&lt;tool&gt;.command</c>): the tool's name, then each
    /// argument as a shell would need it typed - quoted when it holds whitespace or a quote,
    /// with an embedded quote escaped by a backslash. Provenance for a reader, not input for a
    /// parser.
    /// </summary>
    public static class CommandLineText
    {
        /// <summary><paramref name="tool"/> followed by <paramref name="args"/>, each quoted as needed.</summary>
        public static string Format(string tool, IEnumerable<string> args)
        {
            var text = new StringBuilder(tool);
            foreach (string arg in args)
                text.Append(' ').Append(Quote(arg));
            return text.ToString();
        }

        /// <summary><paramref name="arg"/> as a shell would need it typed: itself, or in double quotes.</summary>
        public static string Quote(string arg)
        {
            if (!string.IsNullOrEmpty(arg) && arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
                return arg;
            return @"""" + (arg ?? string.Empty).Replace(@"""", @"\""") + @"""";
        }
    }
}
