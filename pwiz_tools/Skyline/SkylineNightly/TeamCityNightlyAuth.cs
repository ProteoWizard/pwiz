/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net>,
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

// Shared TeamCity artifact-download helpers used by both SkylineNightly
// (downloads SkylineTester.zip) and SkylineNightlyShim (downloads
// SkylineNightly.zip). This file is linked into SkylineNightlyShim via
// <Compile Include="..\SkylineNightly\TeamCityNightlyAuth.cs" Link="..." />
// so the auth scheme lives in one place.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SkylineNightly
{
    internal static class TeamCityNightlyAuth
    {
        public const string TokenEnvVar = "TEAMCITY_NIGHTLY_TEST_AUTH_TOKEN";

        /// <summary>
        /// The bt209 branch a machine downloads SkylineNightly itself from, as a TeamCity branch locator
        /// such as "pull/4700". Unset means master. This is how a change to SkylineNightly gets tried on
        /// a machine or two before it merges: set the variable there, and the machine runs the pull
        /// request's SkylineNightly until the variable is unset. It changes nothing else: SkylineTester
        /// still comes from the branch's own build, so SkylineNightly must always be able to drive the
        /// SkylineTester of every branch, current or not.
        ///
        /// Set it as a persistent User-level variable (PowerShell - no admin needed), which the scheduled
        /// task picks up on its next run, as it does the TeamCity token:
        ///   [Environment]::SetEnvironmentVariable("SKYLINE_NIGHTLY_BRANCH", "pull/4700", "User")
        ///
        /// To put the machine back on master:
        ///   [Environment]::SetEnvironmentVariable("SKYLINE_NIGHTLY_BRANCH", $null, "User")
        ///
        /// Use "User" level, NOT "Machine" level: the task runs as the user who scheduled it, and a
        /// User-level variable needs no elevation to set or clear.
        /// </summary>
        public const string BranchEnvVar = "SKYLINE_NIGHTLY_BRANCH";
        private const string MASTER_BRANCH = "master";

        private const string ARTIFACT_URL_TEMPLATE =
            "https://teamcity.labkey.org/repository/download/{0}/{1}/{2}{3}";

        // The historic /guestAuth/ path with hardcoded guest/guest creds is no
        // longer accepted by the server; each nightly test machine must set the
        // env var named by TokenEnvVar.
        public static string GetRequiredToken()
        {
            // Trim because setx and copy/paste commonly introduce a trailing newline or stray whitespace;
            // a value of " " or "abc\r\n" should fail fast (or be cleaned), not be sent as the Bearer credential.
            var token = Environment.GetEnvironmentVariable(TokenEnvVar);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new IOException("Environment variable " + TokenEnvVar + " is not set. " +
                    "A read-only TeamCity token is required to download nightly build artifacts. " +
                    "See https://skyline.ms/home/development/wiki-page.view?name=SkylineNightly to obtain the token and set this env var.");
            }
            return token.Trim();
        }

        /// <summary>
        /// The branch query for the SkylineNightly download, e.g. "?branch=master" or what BranchEnvVar says.
        /// </summary>
        public static string GetSkylineNightlyBranchQuery()
        {
            var branch = Environment.GetEnvironmentVariable(BranchEnvVar);
            return "?branch=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(branch) ? MASTER_BRANCH : branch.Trim());
        }

        // branchQuery is e.g. "?branch=master", or "" for build configs whose
        // VCS root pins the branch. useLastSuccessful selects the most recent successful build instead of
        // the most recent finished build; SkylineNightly switches to that during prolonged TC outages.
        public static string GetArtifactUrl(string buildType, string zipName, string branchQuery, bool useLastSuccessful)
        {
            var status = useLastSuccessful ? ".lastSuccessful" : ".lastFinished";
            return string.Format(ARTIFACT_URL_TEMPLATE, buildType, status, zipName, branchQuery);
        }

        /// <summary>
        /// Downloads a TeamCity artifact to filePath, authenticating with the token. Deletes the partial
        /// file and throws an <see cref="IOException"/> whose message names the underlying cause on failure.
        /// </summary>
        public static void DownloadArtifact(string url, string filePath, string token)
        {
            ConfigureSecurityProtocol();
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    // Headers only, so the default timeout does not cut off a large zip still streaming
                    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();
                        using (var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        using (var target = File.Create(filePath))
                        {
                            source.CopyTo(target);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                try
                {
                    File.Delete(filePath);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                    // The download failure is what matters
                }

                if (e is HttpRequestException || e is TaskCanceledException)
                    throw new IOException(GetFullMessage(e), e);
                throw;
            }
        }

        private static void ConfigureSecurityProtocol()
        {
            // The current recommendation from MSFT for future-proofing HTTPS https://docs.microsoft.com/en-us/dotnet/framework/network-programming/tls
            // is don't specify TLS levels at all, let the OS decide. But we worry that this will mess up Win7 and Win8 installs, so we continue to specify explicitly.
            try
            {
                var Tls13 = (SecurityProtocolType)12288; // From decompiled SecurityProtocolType - compiler has no definition for some reason
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | Tls13;
            }
            catch (NotSupportedException)
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12; // Probably an older Windows Server
            }
        }

        // HttpClient reports "An error occurred while sending the request." and puts the actual
        // cause (DNS, TLS, refused connection) in the inner exceptions, which the logs need to show
        private static string GetFullMessage(Exception e)
        {
            var message = e.Message;
            for (var inner = e.InnerException; inner != null; inner = inner.InnerException)
                message += " " + inner.Message;
            return message;
        }
    }
}
