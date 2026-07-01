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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    /// <summary>
    /// Verifies that a friendly waters_connect path (waters_connect:&lt;account alias&gt;/Path/To/Injection)
    /// can be imported through the command-line interface, resolving against a Waters_Connect account
    /// saved in the settings. Enabled only when the WC_PASSWORD environment variable is set (see
    /// WatersConnectTestUtil).
    /// </summary>
    [TestClass]
    public class WatersConnectCommandLineTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void TestWatersConnectCommandLineImport()
        {
            if (!WatersConnectTestUtil.EnableWatersConnectTests)
                return;

            const string accountAlias = "wc-test";
            var account = (WatersConnectAccount) WatersConnectTestUtil.GetTestAccount().ChangeAccountAlias(accountAlias);

            var savedAccountList = Settings.Default.RemoteAccountList;
            var savedStorage = RemoteUrl.RemoteAccountStorage;
            try
            {
                // Make the aliased account resolvable in headless mode, the same way Program.Main
                // wires it up for the real command-line interface.
                var accountList = new RemoteAccountList();
                accountList.Add(account);
                Settings.Default.RemoteAccountList = accountList;
                RemoteUrl.RemoteAccountStorage = SkylineRemoteAccountServices.INSTANCE;

                TestFilesDir = new TestFilesDir(TestContext, @"TestConnected\RemoteApiFunctionalTest.data");
                string docPath = TestFilesDir.GetTestPath("SmallMolOptimization.sky");
                const string replicateName = "wc_cli";

                // The friendly path identifies the account by its alias and the injection by its
                // folder/sample-set/injection names; the command-line interface resolves it on the server.
                string friendlyUrl = WatersConnectUrl.UrlPrefix + accountAlias +
                    @"/Company/Skyline/SmallMolOptimization/Scheduled/ID33140_03a_WAA253_4814_092017";

                RunCommand(true,
                           "--in=" + docPath,
                           "--import-file=" + friendlyUrl,
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
    }
}
