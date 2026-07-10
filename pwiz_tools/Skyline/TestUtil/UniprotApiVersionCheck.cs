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
    /// UniProt states, in the headers of every response, the date its API code was deployed and the
    /// release of the data it serves. Our recorded web responses are only as good as the pair they
    /// were recorded against, so a change in either is the earliest warning we get that a recording,
    /// or the code reading it, may no longer describe UniProt.
    ///
    /// This warns rather than fails. UniProt moves on its own schedule, and a deployment there is not
    /// a defect here - but it is the moment to run the live tests and see whether they still agree.
    /// The alternative is what happened in July 2026: the stream endpoint began answering with an
    /// error message in place of the data, and nothing noticed until the perf tests spent 360 seconds
    /// timing out, four weeks after the deployment that caused it.
    ///
    /// UniProt offers no version endpoint. Its published OpenAPI document is frozen at 2022 and does
    /// not mention the fields parameter this code depends on, so these headers are the only signal.
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
        /// Update these whenever the recorded responses in the various WebData.json files are
        /// refreshed, so that the next change UniProt makes is the one that gets reported.
        /// </summary>
        public const string RECORDED_API_DEPLOYMENT_DATE = @"12-June-2026";
        public const string RECORDED_DATA_RELEASE = @"2026_02";

        private const string API_DEPLOYMENT_DATE_HEADER = @"X-API-Deployment-Date";
        private const string DATA_RELEASE_HEADER = @"X-UniProt-Release";

        // Cheapest request that still gets us the headers - one field of one known-good entry
        private const string PROBE_URL =
            @"https://rest.uniprot.org/uniprotkb/search?query=Q08641&format=tsv&size=1&fields=accession";

        // A single long-lived client, so this check never contributes to socket exhaustion
        private static readonly HttpClient CLIENT = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private static bool _alreadyChecked;

        /// <summary>
        /// Reports to the console when UniProt has moved since the recordings were made. Call only
        /// when internet access is allowed. Never throws, and never fails a test: an unreachable
        /// UniProt says nothing about whether its API has changed.
        /// </summary>
        public static void WarnIfChanged()
        {
            if (_alreadyChecked)
                return; // Once per test run is plenty
            _alreadyChecked = true;

            string deploymentDate, dataRelease;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, PROBE_URL);
                using var response = CLIENT.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).Result;
                deploymentDate = GetHeader(response, API_DEPLOYMENT_DATE_HEADER);
                dataRelease = GetHeader(response, DATA_RELEASE_HEADER);
            }
            catch (Exception e)
            {
                Console.WriteLine(@"NOTE: could not ask UniProt which API version it is serving ({0}). " +
                                  @"This says nothing about whether the recorded responses are still accurate.",
                    e.Message);
                return;
            }

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

            if (!Equals(dataRelease, RECORDED_DATA_RELEASE))
            {
                Console.WriteLine();
                Console.WriteLine(@"WARNING: UniProt is now serving data release {0}; the recordings were made " +
                                  @"against {1}. Entries the tests expect may have been revised or withdrawn.",
                    dataRelease ?? @"an unreported release", RECORDED_DATA_RELEASE);
                Console.WriteLine();
            }
        }

        private static string GetHeader(HttpResponseMessage response, string name)
        {
            return response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
        }
    }
}
