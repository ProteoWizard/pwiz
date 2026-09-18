/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net>,
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

// Shared TeamCity artifact-download helpers used by both SkylineNightly
// (downloads SkylineTester.zip) and SkylineNightlyShim (downloads
// SkylineNightly.zip). This file is linked into SkylineNightlyShim via
// <Compile Include="..\SkylineNightly\TeamCityNightlyAuth.cs" Link="..." />
// so the auth scheme lives in one place.

using System;
using System.IO;
using System.Net;

namespace SkylineNightly
{
    internal static class TeamCityNightlyAuth
    {
        public const string TokenEnvVar = "TEAMCITY_NIGHTLY_TEST_AUTH_TOKEN";

        // The bt209 branch a machine downloads SkylineNightly and its master SkylineTester from, as a
        // TeamCity branch locator such as "pull/4700". Unset means master. This is how a change to the
        // nightly tooling gets tried on a machine or two before it merges: set the variable there, and
        // the machine runs the pull request's build as if it were master, until the variable is unset.
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
        /// The branch of the master build config this machine takes as master: what BranchEnvVar says,
        /// or master.
        /// </summary>
        public static string GetMasterBranch()
        {
            var branch = Environment.GetEnvironmentVariable(BranchEnvVar);
            return string.IsNullOrWhiteSpace(branch) ? MASTER_BRANCH : branch.Trim();
        }

        /// <summary>
        /// The branch query for a download from the master build config, e.g. "?branch=master".
        /// </summary>
        public static string GetMasterBranchQuery()
        {
            return "?branch=" + Uri.EscapeDataString(GetMasterBranch());
        }

        // branchQuery is e.g. "?branch=master" (see GetMasterBranchQuery), or "" for build configs whose
        // VCS root pins the branch. useLastSuccessful selects the most recent successful build instead of
        // the most recent finished build; SkylineNightly switches to that during prolonged TC outages.
        public static string GetArtifactUrl(string buildType, string zipName, string branchQuery, bool useLastSuccessful)
        {
            var status = useLastSuccessful ? ".lastSuccessful" : ".lastFinished";
            return string.Format(ARTIFACT_URL_TEMPLATE, buildType, status, zipName, branchQuery);
        }

        public static void ConfigureClient(WebClient client, string token)
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

            client.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
        }
    }
}
