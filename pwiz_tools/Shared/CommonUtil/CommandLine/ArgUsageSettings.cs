/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System;
using System.Net;

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// Everything one argument model installs on the <see cref="ArgUsage"/> seams, declared
    /// together so <see cref="ArgUsage.Install"/> sets and restores all of it at once: the
    /// grammar, how a token joins an argument to its value, the culture a value is formatted
    /// in, and the host's text and behavior. The defaults are Skyline's, which is also the
    /// framework's: strict <c>--name=value</c>, the current culture, and a provider that
    /// throws until a host installs its own.
    /// </summary>
    public sealed class ArgUsageSettings
    {
        /// <summary>The grammar a parser parses with and an argument's text is spelled with.</summary>
        public CommandLineGrammar Grammar { get; init; } = CommandLineGrammar.STRICT;

        /// <summary>
        /// Separator between an argument and its value, both in the usage help
        /// (<c>--name=&lt;value&gt;</c>) and in a token built by
        /// <see cref="ArgumentBase.GetArgumentTextWithValue"/> or the <c>+</c> operator.
        /// A host with space-separated values (Osprey, CarafeSharp) uses a space, so the
        /// generated help and the tokens its tests build both match what its parser accepts.
        /// </summary>
        public string ArgumentValueSeparator { get; init; } = @"=";

        /// <summary>
        /// Culture a non-string value is formatted with by the <c>+</c> operator, so that
        /// <c>ARG_THRESHOLD + 0.05</c> renders the way the host parses. Null means the current
        /// culture at the time of the call (Skyline); a host that parses numbers invariantly
        /// (Osprey, CarafeSharp) sets the invariant culture.
        /// </summary>
        public IFormatProvider ValueFormatProvider { get; init; }

        /// <summary>Host-supplied descriptions, headers and value-error messages.</summary>
        public IArgUsageProvider Provider { get; init; } = ArgUsage.NOT_INSTALLED_PROVIDER;

        /// <summary>
        /// Classifies a value as a remote URL, left untouched by <see cref="NameValuePair.ValueFullPath"/>
        /// instead of being run through <see cref="System.IO.Path.GetFullPath(string)"/>.
        /// </summary>
        public Func<string, bool> IsRemoteUrl { get; init; } = _ => false;

        /// <summary>
        /// Identifies value-example delegates whose argument text may wrap in HTML output
        /// (spaces left as they are rather than replaced with <c>&amp;nbsp;</c>).
        /// </summary>
        public Func<Func<string>, bool> IsWrappableListType { get; init; } = _ => false;

        /// <summary>HTML-encodes a string fragment; a host may match a specific encoder's output.</summary>
        public Func<string, string> HtmlEncode { get; init; } = WebUtility.HtmlEncode;
    }
}
