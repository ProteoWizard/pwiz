/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
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

using System.Text.RegularExpressions;

namespace MSConvertGUI
{
    /// <summary>
    /// A data source URL given with <c>username:password@</c>. The reader gets <see cref="Url"/>;
    /// everything shown to the user (file box, file list, job grid, command line) gets
    /// <see cref="ToString"/>, which leaves the credentials out, as the conversion log does.
    /// </summary>
    public sealed class CredentialUrl
    {
        private static readonly Regex UserInfo = new Regex("^(https?://)[^/@:]+:[^/@]*@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private CredentialUrl(string url) { Url = url; }

        /// <summary>The URL with its credentials.</summary>
        public string Url { get; }

        /// <summary>A <see cref="CredentialUrl"/> when <paramref name="text"/> is a URL with
        /// credentials, otherwise null.</summary>
        public static CredentialUrl TryCreate(string text)
        {
            return text != null && UserInfo.IsMatch(text) ? new CredentialUrl(text) : null;
        }

        public override string ToString() => UserInfo.Replace(Url, "$1");

        public override bool Equals(object obj) => obj is CredentialUrl other && other.Url == Url;

        public override int GetHashCode() => Url.GetHashCode();
    }
}
