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

using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.CommonMsData.RemoteApi;
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
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(_skyFilepath)));
            //var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            var editAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => openDataSourceDialog.CurrentDirectory = RemoteUrl.EMPTY);
            RunUI(()=>editAccountDlg.SetRemoteAccount(_testAccount));
            OkDialog(editAccountDlg, editAccountDlg.OkDialog);
            //WaitForConditionUI(() => openDataSourceDialog.ListItemNames.Contains(_dataPath[0]));
            //foreach(var pathSegment in _dataPath)
                //OpenFile(openDataSourceDialog, pathSegment);
            RunUI(() =>
            {
                openDataSourceDialog.CurrentDirectory = (openDataSourceDialog.CurrentDirectory as RemoteUrl)!.ChangePathParts(_dataPath);
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

            RunUI(() => SkylineWindow.SelectElement(ElementRefs.FromObjectReference(ElementLocator.Parse(_selectItem))));

            var chromGraph = FindOpenForm<GraphChromatogram>();
            WaitForConditionUI(5000, () => chromGraph.CurveCount == _filenames.Length);
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
