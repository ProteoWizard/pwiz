/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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
using System.Linq;
using System.Net.Http;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// UniProt states, in the headers of every response, the date its API code was deployed. Our
    /// recorded web responses, and the code that reads them, are only as good as the API they were
    /// written against, so a deployment there is the earliest warning we get that either may no
    /// longer describe UniProt.
    ///
    /// This warns rather than fails. UniProt deploys on its own schedule, and doing so is not a
    /// defect here - but it is the moment to run the live tests and see whether they still agree.
    /// The alternative is what happened in July 2026: the stream endpoint began answering with an
    /// error message in place of the data, and nothing noticed until the perf tests spent 360 seconds
    /// timing out, four weeks after the deployment that caused it.
    ///
    /// The data release is deliberately not watched. UniProt publishes one every few weeks, entries
    /// come and go with it, and a warning that routine would soon be read as noise. Only the API
    /// itself changing is worth interrupting anyone over.
    ///
    /// UniProt offers no version endpoint. Its published OpenAPI document is frozen at 2022 and does
    /// not mention the fields parameter this code depends on, so this header is the only signal.
    ///
    /// Take care which header means what. UniProt's help page for response headers documents
    /// X-UniProt-Release-Date as "the last date that the API was updated", but its own examples pair
    /// that date with a data release (25-July-2021 with 2021_03), and it moves with X-UniProt-Release
    /// rather than with the API. The header that does track the API code, X-API-Deployment-Date, is
    /// not documented at all.
    /// </summary>
    public static class UniprotApiVersionCheck
    {
        /// <summary>
        /// Update this whenever the recorded responses in the various WebData.json files are
        /// refreshed, so that the next API change UniProt makes is the one that gets reported.
        /// </summary>
        public const string RECORDED_API_DEPLOYMENT_DATE = @"12-June-2026";

        private const string API_DEPLOYMENT_DATE_HEADER = @"X-API-Deployment-Date";

        // Cheapest request that still gets us the headers - one field of one known-good entry
        private const string PROBE_URL =
            @"https://rest.uniprot.org/uniprotkb/search?query=Q08641&format=tsv&size=1&fields=accession";

        // A single long-lived client, so this check never contributes to socket exhaustion
        private static readonly HttpClient CLIENT = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private static bool _alreadyChecked;

        /// <summary>
        /// Reports to the console when UniProt has moved since the recordings were made. Pass the
        /// caller's AllowInternetAccess, so that a test which is not permitted to reach the web
        /// cannot ask this question by accident. Never throws, and never fails a test: an unreachable
        /// UniProt says nothing about whether its API has changed.
        /// </summary>
        public static void WarnIfChanged(bool allowInternetAccess)
        {
            if (!allowInternetAccess)
                return; // Asking would mean going to the web, which this caller may not do

            if (_alreadyChecked)
                return; // The answer cannot change during a run, and one report of it is enough

            string deploymentDate;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, PROBE_URL);
                using var response = CLIENT.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result;
                deploymentDate = GetHeader(response, API_DEPLOYMENT_DATE_HEADER);
            }
            catch (Exception e)
            {
                // Do not latch on a failed probe - if UniProt was briefly unreachable, a later web
                // test in the same run should be free to ask again.
                Console.WriteLine(@"NOTE: could not ask UniProt which API version it is serving ({0}). " +
                                  @"This says nothing about whether the recorded responses are still accurate.",
                    e.Message);
                return;
            }
            _alreadyChecked = true; // Got an answer; no need to ask again this run

            if (deploymentDate == null)
            {
                // The header is undocumented - UniProt's help page describes only the release headers,
                // and mislabels the release date as the date the API was updated - so it may be
                // withdrawn without notice. Say that plainly rather than report a phantom deployment.
                Console.WriteLine();
                Console.WriteLine(@"WARNING: UniProt no longer reports {0}, which is how these tests notice that its " +
                                  @"API has changed under them. A new signal is needed; see {1}.",
                    API_DEPLOYMENT_DATE_HEADER, nameof(UniprotApiVersionCheck));
                Console.WriteLine();
            }
            else if (!Equals(deploymentDate, RECORDED_API_DEPLOYMENT_DATE))
            {
                Console.WriteLine();
                Console.WriteLine(@"WARNING: UniProt deployed new API code on {0}. The responses recorded in the " +
                                  @"WebData.json files, and the code that parses them, were last checked against {1}.",
                    deploymentDate, RECORDED_API_DEPLOYMENT_DATE);
                Console.WriteLine(@"         Run the tests that go to the web (TestFastaImportWeb, " +
                                  @"TestOlderProteomeDbWeb, UniquePeptides1PerfTest) and see whether they still agree " +
                                  @"with the recordings. Then update {0} in {1}.",
                    nameof(RECORDED_API_DEPLOYMENT_DATE), nameof(UniprotApiVersionCheck));
                Console.WriteLine();
            }
        }

        private static string GetHeader(HttpResponseMessage response, string name)
        {
            return response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
        }
    }
}
