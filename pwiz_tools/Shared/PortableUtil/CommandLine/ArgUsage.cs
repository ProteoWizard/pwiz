/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
    }

    /// <summary>
    /// Static seams that decouple the generic CLI-argument framework from its host
    /// application. Defaults keep PortableUtil a pure-BCL leaf usable on its own; a host
    /// overrides them at startup (Skyline does so in the CommandArgs static constructor).
    /// </summary>
    public static class ArgUsage
    {
        // Usage rendering format identifiers (also surfaced to users via Skyline's --help values).
        public const string FORMAT_ASCII = "ascii";
        public const string FORMAT_NO_BORDERS = "no-borders";

        /// <summary>
        /// Host-supplied descriptions, headers and value-error messages. Defaults to a
        /// no-op provider (empty strings/null descriptions) so the framework never NREs
        /// before a host installs its own.
        /// </summary>
        public static IArgUsageProvider Provider { get; set; } = new NullArgUsageProvider();

        /// <summary>
        /// Classifies a value as a remote URL (left untouched by <see cref="NameValuePair.ValueFullPath"/>
        /// instead of being run through <see cref="System.IO.Path.GetFullPath(string)"/>). Default: never remote.
        /// </summary>
        public static Func<string, bool> IsRemoteUrl { get; set; } = _ => false;

        /// <summary>
        /// Identifies value-example delegates whose argument text should be allowed to wrap
        /// in HTML output (spaces left as-is rather than replaced with &amp;nbsp;). Default: none.
        /// </summary>
        public static Func<Func<string>, bool> IsWrappableListType { get; set; } = _ => false;

        /// <summary>
        /// HTML-encodes a string fragment. Defaults to <see cref="WebUtility.HtmlEncode(string)"/> (BCL on
        /// all targets); a host may override to match a specific encoder's byte-for-byte output.
        /// </summary>
        public static Func<string, string> HtmlEncode { get; set; } = WebUtility.HtmlEncode;

        private class NullArgUsageProvider : IArgUsageProvider
        {
            public string GetDescription(string argName) { return null; }
            public string AppliesToHeader { get { return string.Empty; } }
            public string ArgumentHeader { get { return string.Empty; } }
            public string DescriptionHeader { get { return string.Empty; } }

            public string ValueMissingMessage(string argText) { return string.Empty; }
            public string ValueUnexpectedMessage(string argText) { return string.Empty; }
            public string ValueInvalidMessage(string argText, string value, string[] argValues) { return string.Empty; }
            public string ValueInvalidBoolMessage(string argText, string value) { return string.Empty; }
            public string ValueInvalidIntMessage(string argText, string value) { return string.Empty; }
            public string ValueOutOfRangeIntMessage(string argText, int value, int minVal, int maxVal) { return string.Empty; }
            public string ValueInvalidDoubleMessage(string argText, string value) { return string.Empty; }
            public string ValueOutOfRangeDoubleMessage(string argText, double value, double minVal, double maxVal) { return string.Empty; }
            public string ValueInvalidDateMessage(string argText, string value) { return string.Empty; }
            public string ValueInvalidPathMessage(string argText, string value) { return string.Empty; }
        }
    }
}
