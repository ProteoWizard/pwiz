/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    /// <summary>
    /// Verifies that a waters_connect injection URL can be imported through the command-line
    /// interface (--import-file), relying on a Waters_Connect account saved in the settings.
    /// Enabled only when the WC_PASSWORD environment variable is set (see WatersConnectTestUtil).
    /// </summary>
    [TestClass]
    public class WatersConnectCommandLineTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void TestWatersConnectCommandLineImport()
        {
            if (!WatersConnectTestUtil.EnableWatersConnectTests)
                return;

            var account = WatersConnectTestUtil.GetTestAccount();

            var savedAccountList = Settings.Default.RemoteAccountList;
            var savedStorage = RemoteUrl.RemoteAccountStorage;
            try
            {
                // Make the test account resolvable by FindMatchingAccount() in headless mode,
                // the same way Program.Main wires it up for the real command-line interface.
                var accountList = new RemoteAccountList();
                accountList.Add(account);
                Settings.Default.RemoteAccountList = accountList;
                RemoteUrl.RemoteAccountStorage = SkylineRemoteAccountServices.INSTANCE;

                // Resolve a concrete injection URL by navigating the remote folder tree, so the
                // test does not depend on server-assigned ids that can change over time.
                var sampleSetUrl = (WatersConnectUrl) account.GetRootUrl()
                    .ChangePathParts(new[] { "Company", "Skyline", "SmallMolOptimization", "Scheduled" });

                const string injectionName = "ID33140_03a_WAA253_4814_092017";
                MsDataFileUri injectionUri;
                using (var session = RemoteSession.CreateSession(account))
                {
                    var items = FetchContents(session, sampleSetUrl);
                    var injection = items.FirstOrDefault(item => Equals(item.Label, injectionName));
                    Assert.IsNotNull(injection);
                    injectionUri = injection.MsDataFileUri;
                }

                Assert.IsTrue(injectionUri.ToString().StartsWith(WatersConnectUrl.UrlPrefix));

                TestFilesDir = new TestFilesDir(TestContext, @"TestConnected\RemoteApiFunctionalTest.data");
                string docPath = TestFilesDir.GetTestPath("SmallMolOptimization.sky");
                const string replicateName = "wc_cli";

                RunCommand(true,
                           "--in=" + docPath,
                           "--import-file=" + injectionUri,
                           "--import-replicate-name=" + replicateName,
                           "--save");

                var doc = ResultsUtil.DeserializeDocument(docPath);
                Assert.IsTrue(doc.Settings.HasResults);
                Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
                Assert.AreEqual(replicateName, doc.Settings.MeasuredResults.Chromatograms[0].Name);
                Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos.Count);
            }
            finally
            {
                RemoteUrl.RemoteAccountStorage = savedStorage;
                Settings.Default.RemoteAccountList = savedAccountList;
            }
        }

        /// <summary>
        /// Drives the asynchronous fetch for a remote URL until its contents are available,
        /// then returns the listed child items.
        /// </summary>
        private static List<RemoteItem> FetchContents(RemoteSession session, RemoteUrl remoteUrl)
        {
            var signal = new object();
            void OnContentsAvailable()
            {
                lock (signal) Monitor.Pulse(signal);
            }

            session.ContentsAvailable += OnContentsAvailable;
            try
            {
                RemoteServerException exception = null;
                lock (signal)
                {
                    // AsyncFetchContents returns false while a background fetch is in progress;
                    // each completed stage pulses ContentsAvailable so we re-check.
                    for (int i = 0; i < 60 && !session.AsyncFetchContents(remoteUrl, out exception); i++)
                    {
                        if (exception != null)
                            break;
                        Monitor.Wait(signal, 1000);
                    }
                }

                if (exception != null)
                    throw exception;

                return session.ListContents(remoteUrl).ToList();
            }
            finally
            {
                session.ContentsAvailable -= OnContentsAvailable;
            }
        }
    }
}
