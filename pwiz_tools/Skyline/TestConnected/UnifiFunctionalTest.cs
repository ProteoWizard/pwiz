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
        private int _curvesPerReplicate;
        private PointF? _chromatogramPoint;

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
            _curvesPerReplicate = 1;
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
            _curvesPerReplicate = 2;
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
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), "Unable to connect to the remote server");
            RunUI(() => editAccountDlg.SetRemoteAccount(_testAccount.ChangeServerUrl("https://asdfdsafads.local"))); // non-resolving hostname
            AssertAlertDlgContainsMessage(() => editAccountDlg.TestSettings(), "The remote name could not be resolved");

            // waters_connect only below this point: hard-cast client id/scope/secret manipulation,
            // and the invalid-password message text, which is the wire text from the Waters server
            // and not something Unifi's server necessarily matches. Added in d1c5c45927 (#3386) for
            // waters_connect and never guarded, so it null-referenced TestUnifi the first time these
            // ran against a real Unifi account (_testAccount as WatersConnectAccount is null there).
            if (_testAccount is WatersConnectAccount)
            {
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
            }

            RunUI(() => editAccountDlg.SetRemoteAccount(_testAccount));
            OkDialog(editAccountDlg, editAccountDlg.OkDialog);

            if (_testAccount is WatersConnectAccount)
            {
                // waters_connect's ListContents resolves a full, multi-level ChangePathParts jump
                // directly.
                RunUI(() =>
                {
                    openDataSourceDialog.SetCurrentDirectory((openDataSourceDialog.CurrentDirectory as RemoteUrl)!.ChangePathParts(_dataPath));
                });
            }
            else
            {
                // Unifi's UnifiSession.ListContents matches children only by the parent folder's
                // real Id (a GUID assigned incrementally as each level is opened - see
                // UnifiUrl.Id/ChangeId), so jumping straight to a multi-level ChangePathParts path
                // never resolves: Id stays empty and ListContents(navUrl) returns nothing, which
                // left openDataSourceDialog.ListItemNames permanently empty and hung the later
                // WaitForConditionUI in OpenFile for the full 720-second timeout. Navigate one
                // level at a time instead, exactly as clicking through the tree would.
                foreach (var pathSegment in _dataPath)
                    OpenFile(openDataSourceDialog, pathSegment);
            }
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

            // Multiple replicates dock their chromatogram graphs as tabs, and a GraphChromatogram
            // that is not showing draws nothing, so tile them to make every replicate's graph
            // visible before counting curves.
            RunUI(SkylineWindow.ArrangeGraphsTiled);
            RunUI(() => SkylineWindow.SelectElement(ElementRefs.FromObjectReference(ElementLocator.Parse(_selectItem))));

            // Skyline creates one GraphChromatogram per replicate (SkylineWindow.GraphChromatograms),
            // so FindOpenForm, which asserts the form is unique, only works while a single file is
            // imported. Look up each replicate's own graph by the name the document ended up with:
            // ImportResultsNameDlg above removes the common prefix and suffix, which leaves names
            // that are nothing like _filenames (ID33140_03a_... and ID33141_03a_... become 0 and 1).
            var replicateNames = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                .Select(chromatogramSet => chromatogramSet.Name).ToArray();
            Assert.AreEqual(_filenames.Length, replicateNames.Length);
            foreach (var replicateName in replicateNames)
            {
                var chromGraph = SkylineWindow.GetGraphChrom(replicateName);
                Assert.IsNotNull(chromGraph, replicateName);
                WaitForConditionUI(5000, () => chromGraph.CurveCount == _curvesPerReplicate);
                RunUI(() => Assert.AreEqual(_curvesPerReplicate, chromGraph.CurveCount, replicateName));
            }

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
