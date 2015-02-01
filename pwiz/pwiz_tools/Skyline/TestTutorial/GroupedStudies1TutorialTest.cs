/*
 * Original author: Brendan MacLean <bendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class GroupedStudies1TutorialTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestGroupedStudies1Tutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/GroupedStudies1-2_6.pdf";

            TestFilesZipPaths = new[]
            {
                CanImportRawData
                    ? @"https://skyline.gs.washington.edu/tutorials/GroupedStudies1.zip"
                    : @"https://skyline.gs.washington.edu/tutorials/GroupedStudies1Mzml.zip",
                @"TestTutorial\GroupedStudies1Views.zip"
            };
            RunFunctionalTest();
        }

        private static bool CanImportRawData
        {
            get
            {
                return false; //ExtensionTestContext.CanImportThermoRaw;
            }
        }

        private static string ExtThermoRaw
        {
            get { return CanImportRawData ? ExtensionTestContext.ExtThermoRaw : ExtensionTestContext.ExtMzml; }
        }

        private string GetTestPath(string relativePath)
        {
            var folderExistGroupedStudies = CanImportRawData ? "GroupedStudies1" : "GroupedStudies1Mzml";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderExistGroupedStudies, relativePath));
        }

        private string GetHfRawTestPath(string fileName = null)
        {
            string dirPath = GetTestPath(@"Heart Failure\raw");
            return !string.IsNullOrEmpty(fileName)
                ? Path.Combine(dirPath, fileName)
                : dirPath;
        }

        private bool IsFullData { get { return IsPauseForScreenShots; } }

        protected override void DoTest()
        {
            OpenImportArrange();

            ExploreTopPeptides();

            ExploreGlobalStandards();

            ExploreBottomPeptides();
        }

        private void OpenImportArrange()
        {
            // Open the file
            RunUI(() => SkylineWindow.OpenFile(GetHfRawTestPath("Rat_plasma.sky")));
            var docInitial = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(docInitial, null, 49, 137, 137, 789);
            PauseForScreenShot("Status bar", 3);

            if (IsEnableLiveReports)
            {
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                var pathLibraryName = PropertyPath.Parse("LibraryName");
                RunUI(() => documentGrid.ChooseView("Precursors"));
                WaitForConditionUI(() => documentGrid.RowCount > 0 &&
                    documentGrid.FindColumn(pathLibraryName) != null); // Let it initialize

                DataGridView gridView = null;
                DataGridViewColumn columnLibraryName = null;
                RunUI(() =>
                {
                    gridView = documentGrid.DataGridView;
                    columnLibraryName = documentGrid.FindColumn(pathLibraryName);
                });
                Assert.IsNotNull(gridView);
                Assert.IsNotNull(columnLibraryName);
                RunUI(() =>
                {
                    gridView.Sort(columnLibraryName, ListSortDirection.Descending);
                    gridView.CurrentCell = gridView.Rows[79].Cells[columnLibraryName.Index];
                });

                WaitForConditionUI(() => gridView.Rows[80].Cells[columnLibraryName.Index].Value == null);

                PauseForScreenShot<DocumentGridForm>("Document grid toolbar", 4);

                RunUI(() =>
                {
                    Assert.AreEqual("Rat (NIST) (Rat_plasma2)", gridView.Rows[48].Cells[columnLibraryName.Index].Value);
                    Assert.AreEqual("Rat (GPM) (Rat_plasma2)", gridView.Rows[49].Cells[columnLibraryName.Index].Value);
                    Assert.AreEqual("Rat (GPM) (Rat_plasma2)", gridView.CurrentCell.Value);
                    SkylineWindow.ShowDocumentGrid(false);
                });
            }

            if (IsFullData)
            {
                ImportResultsFiles(GetHfRawTestPath(), ExtThermoRaw, null, null);
            }
            else
            {
                ImportResultsAsync(GetHfRawTestPath("D_103_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("D_103_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("D_103_REP3" + ExtThermoRaw),
                    GetHfRawTestPath("D_108_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("D_108_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("D_108_REP3" + ExtThermoRaw),
                    GetHfRawTestPath("D_196_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("D_196_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("D_196_REP3" + ExtThermoRaw),
                    GetHfRawTestPath("H_146_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("H_146_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("H_146_REP3" + ExtThermoRaw),
                    GetHfRawTestPath("H_148_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("H_148_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("H_148_REP3" + ExtThermoRaw),
                    GetHfRawTestPath("H_159_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("H_159_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("H_159_REP3" + ExtThermoRaw),
                    GetHfRawTestPath("H_162_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("H_162_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("H_162_REP3" + ExtThermoRaw));
            }

            var allChrom = WaitForOpenForm<AllChromatogramsGraph>();

            PauseForScreenShot<AllChromatogramsGraph>("Loading Chromatograms form", 5);

            RunUI(() =>
            {
                if (allChrom.Visible)
                    allChrom.Hide();
                // Keep all chromatograms graph from popping up on every RestoreViewOnScreen call below
                Skyline.Properties.Settings.Default.AutoShowAllChromatogramsGraph = false;

                SkylineWindow.ShowGraphSpectrum(false);
                SkylineWindow.ShowGraphRetentionTime(true);
                SkylineWindow.Size = new Size(898, 615);
            });

            RestoreViewOnScreen(6);
            PauseForScreenShot("Docking Retention Times view", 6);

            RestoreViewOnScreen(7);
            RunUI(() => SkylineWindow.ShowGraphPeakArea(true));
            PauseForScreenShot("Docking Peak Areas view", 7);

            RestoreViewOnScreen(8);
            PauseForScreenShot("Docking Targets view", 8);

            RestoreViewOnScreen(9);
            var arrangeGraphsDlg = ShowDialog<ArrangeGraphsGroupedDlg>(SkylineWindow.ArrangeGraphsGrouped);
            RunUI(() =>
            {
                arrangeGraphsDlg.Groups = 3;
                arrangeGraphsDlg.GroupType = GroupGraphsType.distributed;
                arrangeGraphsDlg.GroupOrder = GroupGraphsOrder.Document;
            });

            PauseForScreenShot<ArrangeGraphsGroupedDlg>("Arrange Graphs Grouped form", 8);

            OkDialog(arrangeGraphsDlg, arrangeGraphsDlg.OkDialog);

            if (IsPauseForScreenShots)
                RunUI(() => SkylineWindow.WindowState = FormWindowState.Maximized);

            SelectNode(SrmDocument.Level.Peptides, 0);

            RunDlg<ChromChartPropertyDlg>(() => SkylineWindow.ShowChromatogramProperties(), propDlg =>
            {
                propDlg.FontSize = 12;
                propDlg.OkDialog();
            });

            WaitForDocumentLoaded(10 * 60 * 1000); // 10 minutes

            PauseForScreenShot("Skyline window maximized", 9);

            if (IsPauseForScreenShots)
                RunUI(() => SkylineWindow.WindowState = FormWindowState.Normal);
        }

        private void ExploreTopPeptides()
        {
            RestoreViewOnScreen(10);

            PauseForScreenShot("Retention Times graph (zoomed to show only healthy)", 10);

            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            PauseForScreenShot("Retention Times graph with integrat all (zoomed to show only healthy)", 10);

            RunUI(SkylineWindow.EditDelete); // Delete first peptide

            PauseForScreenShot("Retention Times graph for second peptide", 11);

            RestoreViewOnScreen(12);

            PauseForScreenShot("Peak Areas graph", 12);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view));

            PauseForScreenShot("Peak Areas graph (normalized to total)", 12);

            RestoreViewOnScreen(13);

            RunUI(() => SkylineWindow.ActivateReplicate("D_103_REP3"));

            PauseForScreenShot("Chromatogram graph for D_103_REP3", 13);

            ChangePeakBounds("D_103_REP3", 30.11, 30.43);

            RunUI(() => SkylineWindow.ActivateReplicate("H_162_REP1"));

            PauseForScreenShot("Chromatogram graph for H_162_REP1", 13);

            RunUI(() => SkylineWindow.ActivateReplicate("D_108_REP2"));

            ChangePeakBounds("D_108_REP2", 30.11, 30.5);

            RunUI(() => SkylineWindow.ActivateReplicate("D_162_REP3"));

            RestoreViewOnScreen(15);

            {
                var findDlg = ShowDialog<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg);
                RunUI(() =>
                {
                    findDlg.FindOptions = new FindOptions().ChangeForward(true)
                        .ChangeText(string.Empty)
                        .ChangeCustomFinders(Finders.ListAllFinders().Where(f => f is TruncatedPeakFinder));
                });

                PauseForScreenShot<FindNodeDlg>("Find form", 15);

                RunUI(findDlg.FindAll);

                OkDialog(findDlg, findDlg.Close);

                var findView = WaitForOpenForm<FindResultsForm>();
                int expectedItems = IsFullData ? 457 : 284;
                try
                {
                    WaitForConditionUI(() => findView.ItemCount == expectedItems);
                }
                catch (AssertFailedException)
                {
                    RunUI(() => Assert.AreEqual(expectedItems, findView.ItemCount));
                }

                PauseForScreenShot("Find Results view", 15);
            }

            if (IsEnableLiveReports)
            {
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                RunUI(() => documentGrid.ChooseView("Precursors"));
                WaitForCondition(() => (documentGrid.RowCount > 0)); // Let it initialize

                var viewEditor = ShowDialog<ViewEditor>(documentGrid.NavBar.CustomizeView);
                RunUI(() =>
                {
                    viewEditor.ViewName = "Truncated Precursors";
                    viewEditor.ChooseColumnsTab.RemoveColumns(2, viewEditor.ChooseColumnsTab.ColumnCount);
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Parse("Replicates!*")));
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                    viewEditor.ChooseColumnsTab.ExpandPropertyPath(
                        PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*"), true);
                    // Make the view editor bigger so that these expanded nodes can be seen in the next screenshot
                    viewEditor.Height = Math.Max(viewEditor.Height, 529);
                    Assert.IsTrue(
                        viewEditor.ChooseColumnsTab.TrySelect(
                            PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.CountTruncated")));
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                });

                PauseForScreenShot<ViewEditor.ChooseColumnsView>("Customize View form", 16);

                RunUI(() =>
                {
                    viewEditor.ActivatePropertyPath(
                        PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.CountTruncated"));
                    viewEditor.TabControl.SelectTab(1);
                    int iFilter = viewEditor.ViewInfo.Filters.Count;
                    viewEditor.FilterTab.AddSelectedColumn();
                    viewEditor.FilterTab.SetFilterOperation(iFilter, FilterOperations.OP_IS_GREATER_THAN);
                    viewEditor.FilterTab.SetFilterOperand(iFilter, 0.ToString());
                });

                PauseForScreenShot<ViewEditor.ChooseColumnsView>("Customize View - Filter tab", 17);

                OkDialog(viewEditor, viewEditor.OkDialog);

                RunUI(() => documentGrid.Parent.Size = new Size(514, 315));

                PauseForScreenShot<DocumentGridForm>("Document Grid", 18);

                RunUI(() => SkylineWindow.ShowDocumentGrid(false));
            }

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            // TODO: Use link clicks in Document Grid
            FindNode("DFATVYVDAVK");
            RunUI(() => SkylineWindow.ActivateReplicate("D_196_REP3"));

            PauseForScreenShot("Chromatogram graph", 18);

            RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 1);
                SkylineWindow.ActivateReplicate("D_196_REP3");
                Assert.AreEqual("R.LGGEEVSVACK.L [237, 247]", SkylineWindow.SelectedNode.Text);
            });

            PauseForScreenShot("Retention Times graph for LGGEEVSVACK peptide", 19);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 1);
                SkylineWindow.ActivateReplicate("D_196_REP3");
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
            });

            PauseForScreenShot("Peak Areas graph", 20);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 1);
                SkylineWindow.ActivateReplicate("D_172_REP2");
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
                SkylineWindow.AutoZoomBestPeak();
            });

            PauseForScreenShot("Chromatogram graph zoomed", 21);

            RunUI(() => SkylineWindow.ActivateReplicate("D_138_REP1"));

            PauseForScreenShot("Chromatogram graph zoomed - interference", 21);

            RunUI(() => SelectNode(SrmDocument.Level.Peptides, 2));

            PauseForScreenShot("Chromatogram graph zoomed - nice signal", 22);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            RunUI(() => SelectNode(SrmDocument.Level.Peptides, 2));

            PauseForScreenShot("Peak Areas graph - consistent abundances", 22);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 3);
                Assert.AreEqual("R.GSYNLQDLLAQAK.L [378, 390]", SkylineWindow.SelectedNode.Text);
                SkylineWindow.AutoZoomNone();
                SkylineWindow.ActivateReplicate("D_103_REP1");
            });

            PauseForScreenShot("Chromatogram graph - langscape", 23);

            RunUI(() => SkylineWindow.ActivateReplicate("D_103_REP3"));

            PauseForScreenShot("Chromatogram graph - missing peak", 24);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            WaitForActiveReplicate("H_148_REP1");

            TransitionGroupDocNode nodeGroupRemove = null;
            IdentityPath pathGroupRemove = null;
            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 3);

                var nodePepTree = (PeptideTreeNode)SkylineWindow.SelectedNode;
                nodeGroupRemove = (TransitionGroupDocNode)nodePepTree.DocNode.Children[0];
                pathGroupRemove = new IdentityPath(nodePepTree.Path, nodeGroupRemove.Id);
            });

            WaitForGraphs();

            RemovePeak("D_103_REP3", pathGroupRemove, nodeGroupRemove);

            PauseForScreenShot("Peak Areas graph - removed peak", 24);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            RunUI(() => SelectNode(SrmDocument.Level.Peptides, 3));

            RemovePeak("D_108_REP2", pathGroupRemove, nodeGroupRemove);
            if (IsFullData)
                RemovePeak("H_146_REP2", pathGroupRemove, nodeGroupRemove);
            RemovePeak("H_159_REP2", pathGroupRemove, nodeGroupRemove);
            RemovePeak("H_162_REP3", pathGroupRemove, nodeGroupRemove);

            RunUI(() => SkylineWindow.ActivateReplicate("H_148_REP2"));

            PauseForScreenShot("Chromatogram graph - truncated peak", 25);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            RunUI(() => SelectNode(SrmDocument.Level.Peptides, 3));

            ChangePeakBounds("H_148_REP2", 31.8, 32.2);
            ChangePeakBounds("H_159_REP3", 31.8, 32.2);
            if (IsFullData)
            {
                ChangePeakBounds("H_160_REP2", 31.8, 32.2);
                ChangePeakBounds("H_161_REP3", 31.8, 32.2);
            }
            ChangePeakBounds("H_162_REP2", 31.8, 32.2);

            RunUI(() => SkylineWindow.ActivateReplicate("H_162_REP3"));

            PauseForScreenShot("Peak Areas graph - removed peaks", 25);

            RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 3);
                SkylineWindow.ActivateReplicate("D_103_REP3");
            });

            PauseForScreenShot("Retention Times graph - removed peaks", 26);

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 4);
                SkylineWindow.EditDelete();
                SelectNode(SrmDocument.Level.Peptides, 4);
                Assert.IsTrue(SkylineWindow.SelectedNode.Text.Contains("TSDQIHFFFAK"));
            });

            PauseForScreenShot("Retention Times graph - strange variance", 26);

            RunUI(() => SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time));

            PauseForScreenShot("Retention Times graph - acquired time order", 27);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, 5);
                SelectNode(SrmDocument.Level.Peptides, 6);
                SelectNode(SrmDocument.Level.Peptides, 7);
            });

            ChangePeakBounds("D_108_REP2", 26.8, 27.4);

            PauseForScreenShot("Chromatogram graph - peak truncation", 28);

            RunUI(() => SkylineWindow.ActivateReplicate("H_162_REP3"));

            PauseForScreenShot("Chromatogram graph - peak truncation noisy", 28);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            FindNode("FGLYSDQMR");

            PauseForScreenShot("Peak Areas graph - inconsistent ion abundance", 28);

            RunUI(SkylineWindow.EditDelete);
        }

        private void ExploreGlobalStandards()
        {
            var peptideCount = SkylineWindow.Document.PeptideCount;

            // Ensure some settings, in case prior steps did not occur
            RunUI(() =>
            {
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
                SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time);
            });

            RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, peptideCount - 1);
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Text.Contains("AFGLSSPR"),
                    string.Format("{0} does not contain AFGLSSPR", SkylineWindow.SequenceTree.SelectedNode.Text));
                SelectNode(SrmDocument.Level.Peptides, peptideCount - 2);
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Text.Contains("VVLSGSDATLAYSAFK"),
                    string.Format("{0} does not contain VVLSGSDATLAYSAFK", SkylineWindow.SequenceTree.SelectedNode.Text));
            });

            PauseForScreenShot("Retention Times graph for VVLSGSDATLAYSAFK", 29);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            RunUI(() => SelectNode(SrmDocument.Level.Peptides, peptideCount - 2));

            PauseForScreenShot("Peak Areas graph for VVLSGSDATLAYSAFK", 29);

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, peptideCount - 3);
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Text.Contains("HLNGFSVPR"),
                    string.Format("{0} does not contain HLNGFSVPR", SkylineWindow.SequenceTree.SelectedNode.Text));
            });

            PauseForScreenShot("Peak Areas graph for VVLSGSDATLAYSAFK", 30);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Peptides, peptideCount - 3);
                SkylineWindow.ActivateReplicate("H_159_REP2");
                SkylineWindow.AutoZoomBestPeak();
            });

            PauseForScreenShot("Chromatogram graph with interference", 30);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none));

            SelectNode(SrmDocument.Level.PeptideGroups, SkylineWindow.Document.PeptideGroupCount - 1);

            PauseForScreenShot("Multi-peptide chromatogram graph for S", 31);

            RunUI(SkylineWindow.SelectAll);

            PauseForScreenShot("All multi-peptide chromatogram graph", 32);

            RunUI(() =>
            {
                SkylineWindow.ShowRTPeptideGraph();
                SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.time);
            });

            WaitForGraphs();

            RestoreViewOnScreen(33);

            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaPeptideGraph();
                SkylineWindow.ShowTotalTransitions();
                SkylineWindow.ShowCVValues(true);
            });

            PauseForScreenShot("Peak areas peptide comparison graph with CV valuse", 33);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            FindNode("LGPLVEQGR");

            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);

            PauseForScreenShot("Peak area replicate comparison graph for LGPLVEDQGR", 33);

            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
            });

            PauseForScreenShot("Peak area graph for LGPLVEDQR normalized", 34);
        }

        private void ExploreBottomPeptides()
        {
            var peptideCount = SkylineWindow.Document.PeptideCount;

            SelectNode(SrmDocument.Level.Peptides, peptideCount - 4);

            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.SelectedNode.Text.Contains("DVFSQQADLSR"));
                SkylineWindow.ShowRTReplicateGraph();
            });

            {
                bool peptideIfsSelected = false;
                int i = peptideCount - 4;
                do
                {
                    SelectNode(SrmDocument.Level.Peptides, --i);
                    RunUI(() => peptideIfsSelected = SkylineWindow.SelectedNode.Text.Contains("IFSQQADLSR"));
                    WaitForGraphs();
                } while (!peptideIfsSelected);

                RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

                SelectNode(SrmDocument.Level.Peptides, i);
                RunUI(() =>
                {
                    SkylineWindow.ActivateReplicate("H_146_REP1");
                    SkylineWindow.AutoZoomNone();
                });

                PauseForScreenShot("Chromatogram graph - truncated peak", 35);

                RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

                SelectNode(SrmDocument.Level.Peptides, i);
                RunUI(() => SkylineWindow.ActivateReplicate("H_146_REP1"));

                PauseForScreenShot("Retention Times graph - wide peaks", 35);
            }
        }

        private static int GetChromIndex(string name)
        {
            int index;
            ChromatogramSet chromatogramSet;
            Assert.IsTrue(SkylineWindow.Document.Settings.MeasuredResults.TryGetChromatogramSet(name,
                out chromatogramSet, out index));
            return index;
        }

        private static void WaitForActiveReplicate(string name)
        {
            int index = GetChromIndex(name);

            WaitForConditionUI(() => SkylineWindow.SelectedResultsIndex == index);
        }

        private void RemovePeak(string chromName, IdentityPath pathGroupRemove, TransitionGroupDocNode nodeGroupRemove)
        {
            RunUI(() =>
            {
                SkylineWindow.ActivateReplicate(chromName);
                SkylineWindow.RemovePeak(pathGroupRemove, nodeGroupRemove, null);

                // Confirm that peak has been removed from the right replicate
                var nodeGroupAfter = SkylineWindow.DocumentUI.FindNode(pathGroupRemove) as TransitionGroupDocNode;
                Assert.IsNotNull(nodeGroupAfter);
                Assert.AreSame(nodeGroupRemove.Id, nodeGroupAfter.Id);
                int resultsIndex = GetChromIndex(chromName);

                Assert.IsNotNull(nodeGroupRemove.Results[resultsIndex]);
                Assert.IsTrue(nodeGroupRemove.Results[resultsIndex][0].Area.HasValue);
                Assert.IsNotNull(nodeGroupAfter.Results[resultsIndex]);
                Assert.IsFalse(nodeGroupAfter.Results[resultsIndex][0].Area.HasValue);
            });
        }

        private void ChangePeakBounds(string chromName,
                                       double startDisplayTime,
                                       double endDisplayTime)
        {
            Assert.IsTrue(startDisplayTime < endDisplayTime,
                string.Format("Start time {0} must be less than end time {1}.", startDisplayTime, endDisplayTime));

            RunUI(() => SkylineWindow.ActivateReplicate(chromName));

            WaitForGraphs();

            RunUIWithDocumentWait(() => // adjust integration
            {
                var graphChrom = SkylineWindow.GetGraphChrom(chromName);

                var nodeGroupTree = SkylineWindow.SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                IdentityPath pathGroup;
                if (nodeGroupTree != null)
                    pathGroup = nodeGroupTree.Path;
                else
                {
                    var nodePepTree = SkylineWindow.SequenceTree.GetNodeOfType<PeptideTreeNode>();
                    pathGroup = new IdentityPath(nodePepTree.Path, nodePepTree.ChildDocNodes[0].Id);
                }
                var listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(pathGroup,
                        null,
                        graphChrom.NameSet,
                        graphChrom.ChromGroupInfos[0].FilePath,
                        graphChrom.GraphItems.First().GetNearestDisplayTime(startDisplayTime),
                        graphChrom.GraphItems.First().GetNearestDisplayTime(endDisplayTime),
                        PeakIdentification.ALIGNED,
                        PeakBoundsChangeType.both)
                };
                graphChrom.SimulateChangedPeakBounds(listChanges);
            });
            WaitForGraphs();
        }

        private void RunUIWithDocumentWait(Action act)
        {
            var doc = SkylineWindow.Document;
            RunUI(act);
            WaitForDocumentChange(doc); // make sure the action changes the document
        }
    }
}