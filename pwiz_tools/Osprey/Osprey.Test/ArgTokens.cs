/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Common.CommandLine;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Turns the tokens a test builds from the Argument instances into the argv Osprey's
    /// tokenizer takes. <c>ArgumentBase.operator +</c> joins an argument and its value with
    /// the host's <see cref="ArgUsage.ArgumentValueSeparator"/> - a space for Osprey - into
    /// ONE string, <c>"--threads 8"</c>, exactly as the usage text documents the command
    /// line. A shell would deliver that as two argv entries, and this helper does the same:
    /// a dash-prefixed token is split at its FIRST separator, so a value containing spaces
    /// (a path) stays whole, and a token with no separator passes through untouched.
    ///
    /// Test-side on purpose. The production tokenizer must see only what a shell hands it:
    /// <c>Program.Main</c> pre-scans the raw argv for <c>--task</c>, so a production split
    /// would let a quoted <c>"--task PerFileScoring"</c> past that pre-scan and silently run
    /// the whole pipeline, and it would turn a mis-quoted <c>"--no-prefilter false"</c> from
    /// a hard error into a flag plus a warning.
    /// </summary>
    internal static class ArgTokens
    {
        public static string[] Split(params string[] tokens)
        {
            string separator = ArgUsage.ArgumentValueSeparator;
            var argv = new List<string>(tokens.Length);
            foreach (string token in tokens)
            {
                int separatorIndex = token.StartsWith(@"-") ? token.IndexOf(separator, StringComparison.Ordinal) : -1;
                if (separatorIndex > 0)
                {
                    argv.Add(token.Substring(0, separatorIndex));
                    argv.Add(token.Substring(separatorIndex + separator.Length));
                }
                else
                {
                    argv.Add(token);
                }
            }
            return argv.ToArray();
        }
    }
}
