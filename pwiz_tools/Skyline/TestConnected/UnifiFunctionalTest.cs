/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class UnifiFunctionalTest : AbstractFunctionalTestEx
    {
        private RemoteAccount _testAccount;
        private string _skyFilepath;
        private string[] _dataPath;
        private string[] _filenames;
        private string _selectItem;
        private PointF? _chromatogramPoint;

        // .NET Framework and .NET 8 surface a failed socket connection with different text: net472's
        // WebException wrapper vs net8's raw socket message. Derive the net8 expectation from the OS socket
        // layer (the same source HttpClient's message comes from) so it follows the machine locale rather
        // than being a hardcoded English literal.
#if NET472
        private static readonly string ConnectionRefusedMessage = "Unable to connect to the remote server";
        private static readonly string DnsResolutionFailedMessage = "The remote name could not be resolved";
#else
        private static readonly string ConnectionRefusedMessage =
            new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused).Message;
        private static readonly string DnsResolutionFailedMessage =
            new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound).Message;
#endif

        [TestMethod]
        public void TestUnifi()
        {
            if (!UnifiTestUtil.EnableUnifiTests)
            {
                return;
            }
            TestFilesZip = @"TestConnected\UnifiFunctionalTest.zip";
            _testAccount = UnifiTestUtil.GetTestAccount();
            _skyFilepath = "test.sky";
            _dataPath = new[] { "Company", "Demo Department", "Peptides",  };
            _filenames = new[] { "Hi3_ClpB_MSe_01" };
            _selectItem = "Molecule:/sp|P0A6A8|ACP_ECOLI/ITTVQAAIDYINGHQA";
            _chromatogramPoint = new PointF(4.0f, 3.25f);
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestWatersConnect()
        {
            if (!WatersConnectTestUtil.EnableWatersConnectTests)
            {
                return;
            }
            TestFilesZip = @"TestConnected\RemoteApiFunctionalTest.data";
            _testAccount = WatersConnectTestUtil.GetTestAccount();
            _skyFilepath = "SmallMolOptimization.sky";
            _dataPath = new[] { "Company", "Skyline", "SmallMolOptimization", "Scheduled",  };
            _filenames = new[] { "ID33140_03a_WAA253_4814_092017", "ID33141_03a_WAA253_4814_092017" };
            _selectItem = "Molecule:/Nucleotide metabolism/UDP";
            _chromatogramPoint = null;
            RunFunctionalTest();

            // test duplicate run renaming
            _dataPath = new[] { "Company", "Skyline", "Replicates - five injections - all Same - 06NOV25" };
            _filenames = new[] { "Sample 1 (1)", "Sample 1 (2)", "Sample 1 (3)" };
            _selectItem = null;
            RunFunctionalTest();
        }

        private void AssertAlertDlgContainsMessage(Action showDlgAction, string expectedMessage)
        {
            RunDlg<AlertDlg>(showDlgAction, dlg =>
            {
                StringAssert.Contains(dlg.DetailedMessage, expectedMessage);
                dlg.OkDialog();
            });
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(_skyFilepath)));
            //var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            var editAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => openDataSourceDialog.SetCurrentDirectory(RemoteUrl.EMPTY));

            // Test invalid server URLs
            RunUI(() => editAccountDlg.SetRemoteAccount(_testAccount.ChangeServerUrl("localhost")));
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), ToolsUIResources.EditRemoteAccountDlg_ValidateValues_Invalid_server_URL_);
            RunUI(() => editAccountDlg.SetRemoteAccount(_testAccount.ChangeServerUrl("https://localhost:12345"))); // resolves, but no server there
            // .NET Framework's WebException said "Unable to connect to the remote server"; .NET 8's HttpClient
            // surfaces the raw Winsock error instead. Both wrap the same socket failure.
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), ConnectionRefusedMessage);
            RunUI(() => editAccountDlg.SetRemoteAccount(_testAccount.ChangeServerUrl("https://asdfdsafads.local"))); // non-resolving hostname
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), DnsResolutionFailedMessage);

            // Test invalid client id, scope, and secret
            RunUI(() => editAccountDlg.SetRemoteAccount((_testAccount as WatersConnectAccount)!.ChangeClientId("foobar")));
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), ToolsUIResources.EditRemoteAccountDlg_TestWatersConnectAccount_invalid_client_id_or_secret);
            RunUI(() => editAccountDlg.SetRemoteAccount((_testAccount as WatersConnectAccount)!.ChangeClientSecret("foobar")));
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), ToolsUIResources.EditRemoteAccountDlg_TestWatersConnectAccount_invalid_client_id_or_secret);
            RunUI(() => editAccountDlg.SetRemoteAccount((_testAccount as WatersConnectAccount)!.ChangeClientScope("foobar")));
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), "invalid_scope"); // not L10N

            // Test invalid password, the error message tested is a non-L10N string from Waters server
            RunUI(() => editAccountDlg.SetRemoteAccount(_testAccount.ChangePassword("wrongpassword")));
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), "password entered for this user is incorrect");

            RunUI(() => editAccountDlg.SetRemoteAccount(_testAccount));
            OkDialog(editAccountDlg, editAccountDlg.OkDialog);

            RunUI(() =>
            {
                openDataSourceDialog.SetCurrentDirectory((openDataSourceDialog.CurrentDirectory as RemoteUrl)!.ChangePathParts(_dataPath));
            });
            foreach (var filename in _filenames)
                OpenFile(openDataSourceDialog, filename, false);
            RunUI(openDataSourceDialog.Open);

            if (_filenames.Length > 1)
            {
                // Remove prefix/suffix dialog pops up; accept default behavior
                var removeSuffix = WaitForOpenForm<ImportResultsNameDlg>();
                OkDialog(removeSuffix, () => removeSuffix.YesDialog());
            }
            WaitForDocumentLoaded();

            if (_selectItem == null)
                return;

            RunUI(() => SkylineWindow.SelectElement(ElementRefs.FromObjectReference(ElementLocator.Parse(_selectItem))));

            // Skyline creates one GraphChromatogram per imported replicate. On net8 the WinForms handle
            // for every one of them is created eagerly (net472 deferred the hidden ones), so
            // FindOpenForm<GraphChromatogram> - which requires a single open form - sees them all. Target
            // the graph for the currently selected replicate, waiting for the async graph update to settle.
            GraphChromatogram chromGraph = null;
            WaitForConditionUI(5000, () =>
            {
                chromGraph = SkylineWindow.GetGraphChrom(SkylineWindow.SelectedGraphChromName);
                return chromGraph != null && chromGraph.CurveCount == _filenames.Length;
            });
            Assert.IsNotNull(chromGraph);
            Assert.AreEqual(_filenames.Length, chromGraph.CurveCount);

            if (_chromatogramPoint != null)
            {
                ClickChromatogram(_chromatogramPoint.Value.X, _chromatogramPoint.Value.Y);
                GraphFullScan graphFullScan = FindOpenForm<GraphFullScan>();
                Assert.IsNotNull(graphFullScan);
            }
        }

        private void OpenFile(OpenDataSourceDialog openDataSourceDialog, string name, bool open = true)
        {
            WaitForConditionUI(() => openDataSourceDialog.ListItemNames.Contains(name));
            RunUI(()=>
            {
                openDataSourceDialog.SelectFile(name);
                if (open)
                    openDataSourceDialog.Open();
            });
            
        }
    }
}
