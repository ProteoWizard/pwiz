/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Net;

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// Supplies the host-application text the framework cannot carry itself: localized
    /// argument descriptions, the usage-table column headers, and the localized error
    /// messages for the framework's value exceptions. A host (e.g. Skyline) installs an
    /// implementation on <see cref="ArgUsage.Provider"/> so PortableUtil needs no .resx.
    /// </summary>
    public interface IArgUsageProvider
    {
        string GetDescription(string argName);
        string AppliesToHeader { get; }
        string ArgumentHeader { get; }
        string DescriptionHeader { get; }

        string ValueMissingMessage(string argText);
        string ValueUnexpectedMessage(string argText);
        string ValueInvalidMessage(string argText, string value, string[] argValues);
        string ValueInvalidBoolMessage(string argText, string value);
        string ValueInvalidIntMessage(string argText, string value);
        string ValueOutOfRangeIntMessage(string argText, int value, int minVal, int maxVal);
        string ValueInvalidDoubleMessage(string argText, string value);
        string ValueOutOfRangeDoubleMessage(string argText, double value, double minVal, double maxVal);
        string ValueInvalidDateMessage(string argText, string value);
        string ValueInvalidPathMessage(string argText, string value);
        /// <summary>The error for a token spelled like an option that names no argument, as typed.</summary>
        string UnknownArgumentMessage(string argText);
    }

    /// <summary>
    /// Static seams that decouple the generic CLI-argument framework from its host
    /// application. Defaults keep PortableUtil a pure-BCL leaf usable on its own. They are
    /// process-global, not per parser instance, so the program that owns the process installs
    /// its argument model once at startup, through <see cref="Install"/>: Skyline in the
    /// CommandArgs static constructor, Osprey in its, CarafeSharp.exe in Main. A library's
    /// declarations hosted in another program's process - CarafeSharp's in Skyline and Osprey -
    /// install nothing and run under the host's settings; a library that installed its own when
    /// its type initialized decided how the host parsed its next command line.
    /// </summary>
    public static class ArgUsage
    {
        /// <summary>
        /// Installs an argument model's <see cref="ArgUsageSettings"/> on these seams, the one
        /// way they are set. The program that owns the process calls it once at startup; a test
        /// that parses under another grammar for a while takes <see cref="Current"/> first and
        /// installs it again after.
        /// </summary>
        public static void Install(ArgUsageSettings settings)
        {
            Grammar = settings.Grammar;
            ArgumentValueSeparator = settings.ArgumentValueSeparator;
            ValueFormatProvider = settings.ValueFormatProvider;
            Provider = settings.Provider;
            IsRemoteUrl = settings.IsRemoteUrl;
            IsWrappableListType = settings.IsWrappableListType;
            HtmlEncode = settings.HtmlEncode;
        }

        /// <summary>The settings in force, as <see cref="Install"/> would install them again.</summary>
        public static ArgUsageSettings Current
        {
            get
            {
                return new ArgUsageSettings
                {
                    Grammar = Grammar,
                    ArgumentValueSeparator = ArgumentValueSeparator,
                    ValueFormatProvider = ValueFormatProvider,
                    Provider = Provider,
                    IsRemoteUrl = IsRemoteUrl,
                    IsWrappableListType = IsWrappableListType,
                    HtmlEncode = HtmlEncode
                };
            }
        }

        // Usage rendering format identifiers (also surfaced to users via Skyline's --help values).
        public const string FORMAT_ASCII = "ascii";
        public const string FORMAT_NO_BORDERS = "no-borders";

        /// <summary>The provider in force until a host installs one, which throws on every use.</summary>
        internal static readonly IArgUsageProvider NOT_INSTALLED_PROVIDER = new NotInstalledArgUsageProvider();

        // Set only through Install, so the seams always change together and can be restored
        // together; each is described on its ArgUsageSettings property.

        /// <summary>
        /// The grammar <see cref="CommandLineParser{TContext}"/> parses with and
        /// <see cref="ArgumentBase.ArgumentText"/> spells arguments with.
        /// </summary>
        public static CommandLineGrammar Grammar { get; private set; } = CommandLineGrammar.STRICT;

        /// <summary>
        /// Separator between an argument and its value, both in the usage help and in a token
        /// built by <see cref="ArgumentBase.GetArgumentTextWithValue"/> / the <c>+</c> operator.
        /// </summary>
        public static string ArgumentValueSeparator { get; private set; } = @"=";

        /// <summary>Culture a non-string value is formatted with by the <c>+</c> operator; null for the current culture.</summary>
        public static IFormatProvider ValueFormatProvider { get; private set; }

        /// <summary>
        /// Host-supplied descriptions, headers and value-error messages. Defaults to a provider
        /// that throws: descriptions/headers/error text are inherently host-specific, so a host
        /// that renders usage or reports a value error MUST install one. Failing loudly here turns
        /// a "seam not installed" bug into an immediate exception instead of silently emitting
        /// empty/mis-encoded output the byte-identical golden HTML test cannot catch.
        /// </summary>
        public static IArgUsageProvider Provider { get; private set; } = NOT_INSTALLED_PROVIDER;

        /// <summary>Classifies a value as a remote URL, left untouched by <see cref="NameValuePair.ValueFullPath"/>.</summary>
        public static Func<string, bool> IsRemoteUrl { get; private set; } = _ => false;

        /// <summary>Identifies value-example delegates whose argument text may wrap in HTML output.</summary>
        public static Func<Func<string>, bool> IsWrappableListType { get; private set; } = _ => false;

        /// <summary>HTML-encodes a string fragment; a host may match a specific encoder's byte-for-byte output.</summary>
        public static Func<string, string> HtmlEncode { get; private set; } = WebUtility.HtmlEncode;

        private class NotInstalledArgUsageProvider : IArgUsageProvider
        {
            private static InvalidOperationException NotInstalled()
            {
                return new InvalidOperationException(
                    @"No IArgUsageProvider has been installed on ArgUsage.Provider. The host application must install one before rendering usage or reporting a command-line value error.");
            }

            public string GetDescription(string argName) { throw NotInstalled(); }
            public string AppliesToHeader { get { throw NotInstalled(); } }
            public string ArgumentHeader { get { throw NotInstalled(); } }
            public string DescriptionHeader { get { throw NotInstalled(); } }

            public string ValueMissingMessage(string argText) { throw NotInstalled(); }
            public string ValueUnexpectedMessage(string argText) { throw NotInstalled(); }
            public string ValueInvalidMessage(string argText, string value, string[] argValues) { throw NotInstalled(); }
            public string ValueInvalidBoolMessage(string argText, string value) { throw NotInstalled(); }
            public string ValueInvalidIntMessage(string argText, string value) { throw NotInstalled(); }
            public string ValueOutOfRangeIntMessage(string argText, int value, int minVal, int maxVal) { throw NotInstalled(); }
            public string ValueInvalidDoubleMessage(string argText, string value) { throw NotInstalled(); }
            public string ValueOutOfRangeDoubleMessage(string argText, double value, double minVal, double maxVal) { throw NotInstalled(); }
            public string ValueInvalidDateMessage(string argText, string value) { throw NotInstalled(); }
            public string ValueInvalidPathMessage(string argText, string value) { throw NotInstalled(); }
            public string UnknownArgumentMessage(string argText) { throw NotInstalled(); }
        }
    }
}
