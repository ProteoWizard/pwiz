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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
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

            ForceMzml = true;   // Mzml is faster for this test.

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/GroupedStudies1-2_6.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.gs.washington.edu/tutorials/GroupedStudies1.zip"
                    : @"https://skyline.gs.washington.edu/tutorials/GroupedStudies1Mzml.zip",
                @"TestTutorial\GroupedStudies1Views.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            var folderExistGroupedStudies = UseRawFiles ? "GroupedStudies1" : "GroupedStudies1Mzml";
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

        private const string TRUNCATED_PRECURSORS_VIEW_NAME = "Truncated Precursors";
        private const string MISSING_PEAKS_VIEW_NAME = "Missing Peaks";

        private readonly string _missingDataName = AnnotationDef.GetColumnName("MissingData");

        protected override void DoTest()
        {
            OpenImportArrange();

            ExploreTopPeptides();

            ExploreGlobalStandards();

            ExploreBottomPeptides();

            PrepareForStatistics();

            ReviewStatistics();

            SimpleGroupComparisons();
        }

        private void OpenImportArrange()
        {
            // Open the file
            RunUI(() => SkylineWindow.OpenFile(GetHfRawTestPath("Rat_plasma.sky")));
            var docInitial = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(docInitial, null, 49, 137, 137, 789);
            PauseForScreenShot("Status bar", 3);

            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            var pathLibraryName = PropertyPath.Parse("LibraryName");
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
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
            WaitForConditionUI(() => Equals(gridView.Rows[48].Cells[columnLibraryName.Index].Value, "Rat (NIST) (Rat_plasma2)"));

            RunUI(() => Assert.AreEqual(137, documentGrid.RowCount));

            PauseForScreenShot<DocumentGridForm>("Document grid toolbar", 4);

            RunUI(() =>
            {
                Assert.AreEqual("Rat (NIST) (Rat_plasma2)", gridView.Rows[48].Cells[columnLibraryName.Index].Value);
                Assert.AreEqual("Rat (GPM) (Rat_plasma2)", gridView.Rows[49].Cells[columnLibraryName.Index].Value);
                Assert.AreEqual("Rat (GPM) (Rat_plasma2)", gridView.CurrentCell.Value);
                SkylineWindow.ShowDocumentGrid(false);
            });

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
                    GetHfRawTestPath("D_138_REP1" + ExtThermoRaw),
                    GetHfRawTestPath("D_138_REP2" + ExtThermoRaw),
                    GetHfRawTestPath("D_138_REP3" + ExtThermoRaw),
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
                Settings.Default.AutoShowAllChromatogramsGraph = false;

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

            PauseForScreenShot<ArrangeGraphsGroupedDlg>("Arrange Graphs Grouped form", 9);

            OkDialog(arrangeGraphsDlg, arrangeGraphsDlg.OkDialog);

            if (IsPauseForScreenShots)
                RunUI(() => SkylineWindow.WindowState = FormWindowState.Maximized);

            SelectNode(SrmDocument.Level.Molecules, 0);

            RunDlg<ChromChartPropertyDlg>(() => SkylineWindow.ShowChromatogramProperties(), propDlg =>
            {
                propDlg.FontSize = GraphFontSize.NORMAL;
                propDlg.OkDialog();
            });

            WaitForDocumentLoaded(10 * 60 * 1000); // 10 minutes

            PauseForScreenShot("Skyline window maximized", 9);

            if (!IsPauseForScreenShots)
                TestApplyToAll();

            if (IsPauseForScreenShots)
                RunUI(() => SkylineWindow.WindowState = FormWindowState.Normal);
        }

        private void ExploreTopPeptides()
        {
            RestoreViewOnScreen(10);

            PauseForScreenShot("Retention Times graph (zoomed to show only healthy)", 10);

            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            PauseForScreenShot("Retention Times graph with integrate all (zoomed to show only healthy)", 11);

            SelectNode(SrmDocument.Level.Molecules, 0);
            RunUI(SkylineWindow.EditDelete); // Delete first peptide

            PauseForScreenShot("Retention Times graph for second peptide", 11);

            RestoreViewOnScreen(12);

            PauseForScreenShot("Peak Areas graph", 12);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view));

            PauseForScreenShot("Peak Areas graph (normalized to total)", 13);

            RestoreViewOnScreen(13);

            ActivateReplicate("D_103_REP3");

            PauseForScreenShot("Chromatogram graph for D_103_REP3", 13);

            ChangePeakBounds("D_103_REP3", 30.11, 30.43);

            ActivateReplicate("H_162_REP1");

            PauseForScreenShot("Chromatogram graph for H_162_REP1", 14);

            ActivateReplicate("D_108_REP2");

            ChangePeakBounds("D_108_REP2", 30.11, 30.5);

            ActivateReplicate("D_162_REP3");

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
                int expectedItems = IsFullData ? 457 : 290;
                try
                {
                    WaitForConditionUI(1000, () => findView.ItemCount == expectedItems);
                }
                catch (AssertFailedException)
                {
                    RunUI(() => Assert.AreEqual(expectedItems, findView.ItemCount));
                }

                PauseForScreenShot("Find Results view", 16);
            }

            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));

            AddTruncatedPrecursorsView(documentGrid, true);

            RunUI(() => SkylineWindow.ShowDocumentGrid(false));

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            // TODO: Use link clicks in Document Grid
            FindNode("DFATVYVDAVK");
            ActivateReplicate("D_196_REP3");

            PauseForScreenShot("Chromatogram graph", 19);

            RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

            SelectNode(SrmDocument.Level.Molecules, 1);
            ActivateReplicate("D_196_REP3");

            RunUI(() => Assert.AreEqual("R.LGGEEVSVACK.L [237, 247]", SkylineWindow.SelectedNode.Text));

            PauseForScreenShot("Retention Times graph for LGGEEVSVACK peptide", 20);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            SelectNode(SrmDocument.Level.Molecules, 1);
            ActivateReplicate("D_196_REP3");

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none));

            PauseForScreenShot("Peak Areas graph", 21);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            SelectNode(SrmDocument.Level.Molecules, 1);
            ActivateReplicate("D_172_REP2");

            RunUI(() =>
            {
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
                SkylineWindow.AutoZoomBestPeak();
            });

            PauseForScreenShot("Chromatogram graph zoomed", 22);

            ActivateReplicate("D_138_REP1");

            PauseForScreenShot("Chromatogram graph zoomed - interference", 22);

            SelectNode(SrmDocument.Level.Molecules, 2);
            ActivateReplicate("D_154_REP1");

            PauseForScreenShot("Chromatogram graph zoomed - nice signal", 23);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            SelectNode(SrmDocument.Level.Molecules, 2);
            ActivateReplicate("D_154_REP1");

            PauseForScreenShot("Peak Areas graph - consistent abundances", 23);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            SelectNode(SrmDocument.Level.Molecules, 3);
            ActivateReplicate("D_103_REP1");

            RunUI(() =>
            {
                Assert.AreEqual("R.GSYNLQDLLAQAK.L [378, 390]", SkylineWindow.SelectedNode.Text);
                SkylineWindow.AutoZoomNone();
            });

            PauseForScreenShot("Chromatogram graph - langscape", 24);

            ActivateReplicate("D_103_REP3");

            PauseForScreenShot("Chromatogram graph - missing peak", 25);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            ActivateReplicate("H_148_REP1");
            WaitForActiveReplicate("H_148_REP1");

            TransitionGroupDocNode nodeGroupRemove = null;
            IdentityPath pathGroupRemove = null;
            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Molecules, 3);

                var nodePepTree = (PeptideTreeNode)SkylineWindow.SelectedNode;
                nodeGroupRemove = (TransitionGroupDocNode)nodePepTree.DocNode.Children[0];
                pathGroupRemove = new IdentityPath(nodePepTree.Path, nodeGroupRemove.Id);
            });

            WaitForGraphs();

            RemovePeak("D_103_REP3", pathGroupRemove, nodeGroupRemove);

            PauseForScreenShot("Peak Areas graph - removed peak", 25);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            RunUI(() => SelectNode(SrmDocument.Level.Molecules, 3));

            RemovePeak("D_108_REP2", pathGroupRemove, nodeGroupRemove);
            if (IsFullData)
                RemovePeak("H_146_REP2", pathGroupRemove, nodeGroupRemove);
            RemovePeak("H_159_REP2", pathGroupRemove, nodeGroupRemove);
            RemovePeak("H_162_REP3", pathGroupRemove, nodeGroupRemove);

            RunUI(() => SkylineWindow.ActivateReplicate("H_148_REP2"));

            PauseForScreenShot("Chromatogram graph - truncated peak", 26);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            SelectNode(SrmDocument.Level.Molecules, 3);

            ChangePeakBounds("H_148_REP2", 31.8, 32.2);
            ChangePeakBounds("H_159_REP3", 31.8, 32.2);
            if (IsFullData)
            {
                ChangePeakBounds("H_160_REP2", 31.8, 32.2);
                ChangePeakBounds("H_161_REP3", 31.8, 32.2);
            }
            ChangePeakBounds("H_162_REP2", 31.8, 32.2);

            ActivateReplicate("H_162_REP3");

            PauseForScreenShot("Peak Areas graph - removed peaks", 26);

            RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

            SelectNode(SrmDocument.Level.Molecules, 3);
            ActivateReplicate("D_103_REP3");

            PauseForScreenShot("Retention Times graph - removed peaks", 27);

            RunUI(() =>
            {
                SelectNode(SrmDocument.Level.Molecules, 4);
                SkylineWindow.EditDelete();
                SelectNode(SrmDocument.Level.Molecules, 4);
                Assert.IsTrue(SkylineWindow.SelectedNode.Text.Contains("TSDQIHFFFAK"));
            });

            PauseForScreenShot("Retention Times graph - strange variance", 27);

            RunUI(() => SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time));

            PauseForScreenShot("Retention Times graph - acquired time order", 28);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            SelectNode(SrmDocument.Level.Molecules, 5);
            SelectNode(SrmDocument.Level.Molecules, 6);
            SelectNode(SrmDocument.Level.Molecules, 7);

            ChangePeakBounds("D_108_REP2", 26.8, 27.4);

            PauseForScreenShot("Chromatogram graph - peak truncation", 29);

            ActivateReplicate("H_162_REP3");

            PauseForScreenShot("Chromatogram graph - peak truncation noisy", 29);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            FindNode("FGLYSDQMR");

            PauseForScreenShot("Peak Areas graph - inconsistent ion abundance", 29);

            RunUI(SkylineWindow.EditDelete);
        }

        private void AddTruncatedPrecursorsView(DocumentGridForm documentGrid, bool initialTestExecution)
        {
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            WaitForCondition(() => (documentGrid.RowCount > 0)); // Let it initialize

            var viewEditor = ShowDialog<ViewEditor>(documentGrid.NavBar.CustomizeView);
            RunUI(() =>
            {
                viewEditor.ViewName = TRUNCATED_PRECURSORS_VIEW_NAME;
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

            if (initialTestExecution)
                PauseForScreenShot<ViewEditor.ChooseColumnsView>("Customize View form", 17);

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

            if (initialTestExecution)
                PauseForScreenShot<ViewEditor.ChooseColumnsView>("Customize View - Filter tab", 18);

            OkDialog(viewEditor, viewEditor.OkDialog);

            var pathTruncated = PropertyPath.Parse("Results!*.Value.CountTruncated");
            int expectedItems = 148;
            if (IsFullData)
                expectedItems = initialTestExecution ? 222 : 221;
            try
            {
                WaitForConditionUI(1000, () => documentGrid.RowCount == expectedItems &&
                                         documentGrid.FindColumn(pathTruncated) != null);
            }
            catch (AssertFailedException)
            {
                RunUI(() => Assert.AreEqual(expectedItems, documentGrid.RowCount));
            }

            RunUI(() =>
            {
                var columnTruncated = documentGrid.FindColumn(pathTruncated);
                documentGrid.DataGridView.Sort(columnTruncated, ListSortDirection.Descending);

                FormEx.GetParentForm(documentGrid).Size = new Size(514, 315);
            });

            if (initialTestExecution)
                PauseForScreenShot<DocumentGridForm>("Document Grid", 19);
        }

        private void ExploreGlobalStandards()
        {
            RunUI(() => SkylineWindow.SetIntegrateAll(true));

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
                SelectNode(SrmDocument.Level.Molecules, peptideCount - 1);
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Text.Contains("AFGLSSPR"),
                    string.Format("{0} does not contain AFGLSSPR", SkylineWindow.SequenceTree.SelectedNode.Text));
                SelectNode(SrmDocument.Level.Molecules, peptideCount - 2);
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Text.Contains("VVLSGSDATLAYSAFK"),
                    string.Format("{0} does not contain VVLSGSDATLAYSAFK", SkylineWindow.SequenceTree.SelectedNode.Text));
            });

            PauseForScreenShot("Retention Times graph for VVLSGSDATLAYSAFK", 30);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            SelectNode(SrmDocument.Level.Molecules, peptideCount - 2);

            PauseForScreenShot("Peak Areas graph for VVLSGSDATLAYSAFK", 30);

            SelectNode(SrmDocument.Level.Molecules, peptideCount - 3);
            RunUI(() => Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Text.Contains("HLNGFSVPR"),
                    string.Format("{0} does not contain HLNGFSVPR", SkylineWindow.SequenceTree.SelectedNode.Text)));

            PauseForScreenShot("Peak Areas graph for VVLSGSDATLAYSAFK", 31);

            RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

            SelectNode(SrmDocument.Level.Molecules, peptideCount - 3);

            RunUI(() =>
            {
                SkylineWindow.ActivateReplicate("H_159_REP2");
                SkylineWindow.AutoZoomBestPeak();
            });

            PauseForScreenShot("Chromatogram graph with interference", 31);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none));

            SelectNode(SrmDocument.Level.MoleculeGroups, SkylineWindow.Document.PeptideGroupCount - 1);
            ActivateReplicate("D_102_REP1");

            PauseForScreenShot("Multi-peptide chromatogram graph for S", 32);

            RunUI(SkylineWindow.SelectAll);

            PauseForScreenShot("All multi-peptide chromatogram graph", 33);

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

            PauseForScreenShot("Peak areas peptide comparison graph with CV valuse", 34);

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            FindNode("LGPLVEQGR");

            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);

            PauseForScreenShot("Peak area replicate comparison graph for LGPLVEDQGR", 34);

            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
            });

            PauseForScreenShot("Peak area graph for LGPLVEDQR normalized", 35);
        }

        private void ExploreBottomPeptides()
        {
            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            var peptideCount = SkylineWindow.Document.PeptideCount;

            RunUI(() =>
            {
                // Ensure these important settings are set
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
                SkylineWindow.SetIntegrateAll(true);    
                SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time);
            });

            SelectNode(SrmDocument.Level.Molecules, peptideCount - 4);

            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.SelectedNode.Text.Contains("DVFSQQADLSR"));
                SkylineWindow.ShowRTReplicateGraph();
            });

            {
                int i = SelectPeptidesUpUntil("IFSQQADLSR");

                RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("H_146_REP1");

                RunUI(() =>
                {
                    SkylineWindow.AutoZoomNone();
                });

                PauseForScreenShot("Chromatogram graph - truncated peak", 36);

                RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("H_146_REP1");

                PauseForScreenShot("Retention Times graph - wide peaks", 36);

                RestoreViewOnScreen(36);
                SelectNode(SrmDocument.Level.Molecules, i);

                RunUI(() =>
                {
                    SkylineWindow.SynchronizeZooming(true);
                    SkylineWindow.Size = new Size(1380, 744);
                });

                PauseForScreenShot("Chromatogram graphs - use zoom and pan to set up", 37);

                RunUI(() => SkylineWindow.Size = new Size(974, 640));
                RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12
                SelectNode(SrmDocument.Level.Molecules, i);

                i = SelectPeptidesUpUntil("MLSGFIPLKPTVK");

                PauseForScreenShot("Peak Areas graph - variance", 37);

                RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13
                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("D_138_REP1");

                PauseForScreenShot("Chromatogram graph - y7 with no coeluting", 38);

                RestoreViewOnScreen(36);    // Chromatogram graphs as on page 36

                SelectNode(SrmDocument.Level.Molecules, i);

                RunUI(() =>
                {
                    SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
                    SkylineWindow.Size = new Size(1380, 744);
                });
                
                ActivateReplicate("H_148_REP1");
                ActivateReplicate("H_148_REP2");
                ActivateReplicate("H_148_REP3");

                PauseForScreenShot("Chromatogram graphs - showing coelution", 38);

                RunUI(() => SkylineWindow.Size = new Size(898, 615));

                RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

                SelectNode(SrmDocument.Level.Molecules, i);

                PauseForScreenShot("Retention Times graph - misintegrated peaks", 39);

                RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12
                SelectNode(SrmDocument.Level.Molecules, i);

                PauseForScreenShot("Peak Areas graph - no normalization", 39);

                if (IsFullData)
                {
                RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("D_154_REP3");

                PauseForScreenShot("Chromatogram graph - mispicked peak", 40);

                ChangePeakBounds("D_154_REP3", 23, 23.5);
                }

                RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view));

                SelectPeptidesUpUntil("GMYESLPVVAVK");

                RunUI(SkylineWindow.EditDelete);

                i = SelectPeptidesUpUntil("ETGLMAFTNLK");
                ActivateReplicate("D_103_REP1");

                PauseForScreenShot("Chromatogram graph - interference outside peak", 41);

                RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("D_103_REP1");

                SelectPeptidesUpUntil("YANVIAYDHSR");

                VerifyLowDotProducts(0.6);

                i = SelectPeptidesUpUntil("TDEDVPSGPPR");

                VerifyLowDotProducts(0.35);

                PauseForScreenShot("Peak Areas graph - poor library correlation", 42);

                RestoreViewOnScreen(13); // Same layout for chromatogram graphs as before on page 13

                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("D_196_REP1");

                PauseForScreenShot("Chromatogram graph - poor library correlation", 42);

                if (IsFullData)
                {
                    SelectPeptidesUpUntil("SPQGLGASTAEISAR");
                    ActivateReplicate("D_154_REP1");
                    ChangePeakBounds("D_154_REP1", 17.2, 17.65);
                }

                i = SelectPeptidesUpUntil("CSSLLWAGAAWLR");

                RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("D_154_REP1");

                PauseForScreenShot("Peak Areas graph - poor run-to-run correlation", 43);

                RunUI(() => SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.document));

                PauseForScreenShot("Peak Areas graph - poor run-to-run correlation - logical order", 44);

                RestoreViewOnScreen(10); // Same layout for RT graph as on page 10

                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("D_154_REP1");

                PauseForScreenShot("Retention Times graph - poor run-to-run correlation - logical order", 44);

                RestoreViewOnScreen(44);
                SelectNode(SrmDocument.Level.Molecules, i);
                ActivateReplicate("D_102_REP3");

                PauseForScreenShot("Cromatogram graph (A) - no peak - Formate width 3.2", 45);

                ActivateReplicate("D_108_REP1");

                PauseForScreenShot("Cromatogram graph (B) - no peak - Formate width 3.2", 45);

                RunUI(() =>
                {
                    SkylineWindow.SaveDocument();
                    var filePathFinished = GetTestPath(@"Heart Failure\Rat_plasma.sky.zip");
                    SkylineWindow.OpenSharedFile(filePathFinished);
                });

                RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

                FindNode("CSSLLWAGAAWLR");
                ActivateReplicate("D_154_REP1");

                RunUI(() =>
                {
                    SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
                    SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time);
                });

                PauseForScreenShot("Peak Areas graph - no normalization", 46);

                RunUI(() =>
                {
                    var filePathFinished = GetTestPath(@"Heart Failure\raw\Rat_plasma.sky");
                    SkylineWindow.OpenFile(filePathFinished);
                });
            }
        }

        private void PrepareForStatistics()
        {
            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            {
                Settings.Default.AnnotationDefList.Clear();
                var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);

                AddReplicateAnnotation(documentSettingsDlg, "SubjectId", AnnotationDef.AnnotationType.text, null, 47);

                RunUI(() => documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true));

                PauseForScreenShot<DocumentSettingsDlg>("Annotation Settings form with SubjectId", 48);

                OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            }

            if (IsPauseForScreenShots)
            {
                // Requires internet access
                var toolStoreDlg = ShowDialog<ToolStoreDlg>(SkylineWindow.ShowToolStoreDlg);

                RunUI(() => toolStoreDlg.SelectTool("MSstats"));

                PauseForScreenShot<ToolStoreDlg>("Tool Store form - showing MSstats tool details", 49);

                OkDialog(toolStoreDlg, toolStoreDlg.CancelButton.PerformClick);
            }

            {
                var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);

                AddReplicateAnnotation(documentSettingsDlg, "BioReplicate", AnnotationDef.AnnotationType.text,
                    null, 50);

                AddReplicateAnnotation(documentSettingsDlg, "Condition", AnnotationDef.AnnotationType.value_list,
                    new[] {"Healthy", "Diseased"}, 50);

                RunUI(() =>
                {
                    documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(1, true);
                    documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(2, true);
                });

                PauseForScreenShot<DocumentSettingsDlg>("Annotation Settings form with MSstats annotations", 50);

                OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            }

            {
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                RunUI(() =>
                {
                    documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
                    FormEx.GetParentForm(documentGrid).Size = new Size(591, 283);
                });
                var pathSubjectId = PropertyPath.Root.Property(AnnotationDef.GetColumnName("SubjectId"));
                WaitForConditionUI(() => (documentGrid.RowCount > 0 &&
                    documentGrid.FindColumn(pathSubjectId) != null)); // Let it initialize

                PauseForScreenShot<DocumentGridForm>("Document Grid - replicates", 51);

                RunUI(() =>
                {
                    var columnSubjectId = documentGrid.FindColumn(pathSubjectId);
                    var gridView = documentGrid.DataGridView;
                    gridView.CurrentCell = gridView.Rows[0].Cells[columnSubjectId.Index];
                });

                PauseForScreenShot<DocumentGridForm>("Document Grid - SubjectId column selected", 52);

                var filePath = GetTestPath(@"Heart Failure\raw\Annotations.xlsx");
                SetExcelFileClipboardText(filePath, "Sheet1", 3, true);

                RunUI(() => documentGrid.DataGridView.SendPaste());

                PauseForScreenShot<DocumentGridForm>("Document Grid - filled", 52);

                RunUI(() => SkylineWindow.ShowDocumentGrid(false));
            }

            {
                var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);

                AddAnnotation(documentSettingsDlg, "MissingData", AnnotationDef.AnnotationType.true_false, null,
                    AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.peptide), 53);

                RunUI(() => documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(3, true));

                PauseForScreenShot<DocumentSettingsDlg>("Annotations form with all annotations checked", 54);

                OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            }

            {
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));

                EnsureTruncatedPrecursorsView(documentGrid);

                RunUI(() => documentGrid.ChooseView(TRUNCATED_PRECURSORS_VIEW_NAME));

                var viewEditor = ShowDialog<ViewEditor>(documentGrid.NavBar.CustomizeView);
                RunUI(() =>
                {
                    var pathPeptides = PropertyPath.Parse("Proteins!*.Peptides!*");
                    viewEditor.ChooseColumnsTab.ExpandPropertyPath(pathPeptides, true);
                    viewEditor.Height = 452;
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(pathPeptides.Property(_missingDataName)));
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                });

                PauseForScreenShot<ViewEditor.ChooseColumnsView>("Customize View form with MissingData annotation checked", 55);

                OkDialog(viewEditor, viewEditor.OkDialog);

                RunUI(() => FormEx.GetParentForm(documentGrid).Size = new Size(591, 296));

                var pathMissingData = PropertyPath.Parse("Peptide").Property(_missingDataName);
                WaitForConditionUI(() => (documentGrid.RowCount > 0 &&
                    documentGrid.FindColumn(pathMissingData) != null)); // Let it initialize

                var pathCountTruncated = PropertyPath.Parse("Results!*.Value.CountTruncated");
                RunUI(() =>
                {
                    var columnCountTruncated = documentGrid.FindColumn(pathCountTruncated);
                    documentGrid.DataGridView.Sort(columnCountTruncated,
                        ListSortDirection.Descending);                    
                });

                PauseForScreenShot<DocumentGridForm>("Document Grid with MissingData field", 55);

                int expectedRows = IsFullData ? 223 : 149;
                const int expectedRowsAbbreviated = 221; // When not all of the tests are run
                RunUI(() =>
                {
                    var columnSubjectId = documentGrid.FindColumn(pathMissingData);
                    var gridView = documentGrid.DataGridView;
                    if (IsFullData && expectedRowsAbbreviated == gridView.Rows.Count)
                        expectedRows = expectedRowsAbbreviated;
                    else
                        Assert.AreEqual(expectedRows, gridView.Rows.Count);
                    gridView.CurrentCell = gridView.Rows[0].Cells[columnSubjectId.Index];
                    gridView.CurrentCell.Value = true;
                    gridView.CurrentCell = gridView.Rows[1].Cells[columnSubjectId.Index];
                });

                PauseForScreenShot<DocumentGridForm>("Document Grid with MissingData field checked", 56);

                string linesTrue = TextUtil.LineSeparate(new string[expectedRows].Select(v => "TRUE"));
                RunUI(() =>
                {
                    ClipboardEx.SetText(linesTrue);
                    var gridView = documentGrid.DataGridView;
                    var columnSubjectId = documentGrid.FindColumn(pathMissingData);
                    gridView.CurrentCell = gridView.Rows[0].Cells[columnSubjectId.Index];
                    gridView.SendPaste();

                    for (int i = 0; i < expectedRows; i++)
                    {
                        var value = gridView.Rows[i].Cells[columnSubjectId.Index].Value;
                        Assert.IsTrue((bool)value);
                    }

                    documentGrid.Close();
                });

                FindNode("SQLPGIIAEGR");

                PauseForScreenShot("Targets NP_036870 and peptides", 57);
            }

            {
                var pathPrecursors = PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*");
                var pathCountTruncated = PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.CountTruncated");
                var pathTotalArea = PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Results!*.Value.TotalArea");

                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                var viewManager = ShowDialog<ManageViewsForm>(documentGrid.NavBar.ManageViews);
                RunUI(() => viewManager.SelectView(TRUNCATED_PRECURSORS_VIEW_NAME));
                var viewEditor = ShowDialog<ViewEditor>(viewManager.CopyView);
                RunUI(() =>
                {
                    viewEditor.ViewName = MISSING_PEAKS_VIEW_NAME;
                    viewEditor.ChooseColumnsTab.RemoveColumn(pathPrecursors);
                    viewEditor.ChooseColumnsTab.RemoveColumn(pathCountTruncated);
                    PivotReplicateAndIsotopeLabelWidget.SetPivotReplicate(viewEditor, true);
                    viewEditor.Height = 352;
                });

                PauseForScreenShot<ViewEditor.ChooseColumnsView>("Missing Results view", 57);

                RunUI(() =>
                {
                    viewEditor.TabControl.SelectTab(1);
                    viewEditor.FilterTab.DeleteSelectedFilters();
                    viewEditor.ActivatePropertyPath(pathTotalArea);
                    int iFilter = viewEditor.ViewInfo.Filters.Count;
                    viewEditor.FilterTab.AddSelectedColumn();
                    viewEditor.FilterTab.SetFilterOperation(iFilter, FilterOperations.OP_IS_BLANK);
                });

                PauseForScreenShot<ViewEditor.FilterView>("Filter tab of column editor", 58);

                OkDialog(viewEditor, viewEditor.OkDialog);
                OkDialog(viewManager, viewManager.AcceptButton.PerformClick);

                // Hide the grid or opening the shared ZIP will cause the test to hang with an error message
                RunUI(() => SkylineWindow.ShowDocumentGrid(false));
            }
        }

        private void EnsureTruncatedPrecursorsView(DocumentGridForm documentGrid)
        {
            bool hasTruncatedPrecursorsView = false;
            RunUI(() => hasTruncatedPrecursorsView = null !=
                        documentGrid.BindingListSource.ViewContext.GetViewInfo(
                            PersistedViews.MainGroup.Id.ViewName(TRUNCATED_PRECURSORS_VIEW_NAME)));
            if (!hasTruncatedPrecursorsView)
                AddTruncatedPrecursorsView(documentGrid, false);
        }

        private void ReviewStatistics()
        {
            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            RunUI(() =>
            {
                if (!string.IsNullOrEmpty(SkylineWindow.DocumentFilePath))
                    SkylineWindow.SaveDocument();
                SkylineWindow.OpenSharedFile(GetTestPath(@"Heart Failure\Rat_plasma.sky.zip"));
            });

            {
                // From prepare for statistics section to get expected screenshot
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));

                bool hasMissingPeaksView = false;
                RunUI(() => hasMissingPeaksView = null != documentGrid.BindingListSource.ViewContext.GetViewInfo(PersistedViews.MainGroup.Id.ViewName(MISSING_PEAKS_VIEW_NAME)));
                if (hasMissingPeaksView)
                {
                    RunUI(() => documentGrid.ChooseView(MISSING_PEAKS_VIEW_NAME));

                    WaitForConditionUI(() => documentGrid.IsComplete && documentGrid.RowCount == 10);

                    var pathPeptide = PropertyPath.Parse("Peptide");
                    var pathMissingData = pathPeptide.Property(_missingDataName);
                    RunUI(() =>
                    {
                        var columnSubjectId = documentGrid.FindColumn(pathMissingData);
                        var gridView = documentGrid.DataGridView;
                        gridView.CurrentCell = gridView.Rows[gridView.RowCount - 2].Cells[columnSubjectId.Index];
                        gridView.CurrentCell.Value = false;
                        gridView.CurrentCell = gridView.Rows[gridView.RowCount - 1].Cells[columnSubjectId.Index];
                        gridView.CurrentCell.Value = false;

                        var columnPeptide = documentGrid.FindColumn(pathPeptide);
                        gridView.CurrentCell = gridView.Rows[0].Cells[columnPeptide.Index];
                        FormEx.GetParentForm(documentGrid).Size = new Size(756, 352);
                    });

                    PauseForScreenShot<DocumentGridForm>("Missing Peaks view in document grid", 58);

                    RunUI(() =>
                    {
                        var columnSubjectId = documentGrid.FindColumn(pathMissingData);
                        var gridView = documentGrid.DataGridView;
                        gridView.CurrentCell = gridView.Rows[gridView.RowCount - 2].Cells[columnSubjectId.Index];
                        gridView.CurrentCell.Value = true;
                        gridView.CurrentCell = gridView.Rows[gridView.RowCount - 1].Cells[columnSubjectId.Index];
                        gridView.CurrentCell.Value = true;
                    });
                }

                OkDialog(documentGrid, documentGrid.Close);                
            }

            RestoreViewOnScreen(12); // Same layout for Peak Areas graph as on page 12

            FindNode("DVFSQQADLSR");

            RunUI(() =>
            {
                SkylineWindow.ShowTotalTransitions();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_global_standard_view);
                SkylineWindow.GroupByReplicateAnnotation("SubjectId");
                SkylineWindow.ShowCVValues(true);
            });

            ActivateReplicate("D_102_REP1");

            PauseForScreenShot("By SubjectId CV peak area ratio to global standard", 60);

            RestoreViewOnScreen(60);

            RunUI(() =>
            {
                SkylineWindow.GroupByReplicateAnnotation("Condition");
                SkylineWindow.ShowCVValues(false);
            });

            FindNode("IAELFSDLEER");

            PauseForScreenShot("IAELFSDLEER mean peak area ratio to global standard by condition", 61);

            FindNode("FSISTDYSLK");

            PauseForScreenShot("FSISTDYSLK mean peak area ratio to global standard by condition", 61);

            FindNode("EVLPELGIK");

            PauseForScreenShot("EVLPELGIK mean peak area ratio to global standard by condition", 61);

            FindNode("SVVDIGLIK");

            PauseForScreenShot("SVVDIGLIK mean peak area ratio to global standard by condition", 62);

            FindNode("LQTEGDGIYTLNSEK");

            PauseForScreenShot("LQTEGDGIYTLNSEK mean peak area ratio to global standard by condition", 62);

            FindNode("CSSLLWAGAAWLR");

            PauseForScreenShot("CSSLLWAGAAWLR mean peak area ratio to global standard by condition", 62);

            FindNode("NLGVVVAPHALR");

            PauseForScreenShot("NLGVVVAPHALR mean peak area ratio to global standard by condition", 62);
        }

        private void AddReplicateAnnotation(DocumentSettingsDlg documentSettingsDlg,
                                            string annotationName,
                                            AnnotationDef.AnnotationType annotationType,
                                            IList<string> annotationValues,
                                            int pausePage)
        {
            AddAnnotation(documentSettingsDlg, annotationName, annotationType, annotationValues,                
                    AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate),
                    pausePage);
        }

        private void AddAnnotation(DocumentSettingsDlg documentSettingsDlg,
                                            string annotationName,
                                            AnnotationDef.AnnotationType annotationType,
                                            IList<string> annotationValues,
                                            AnnotationDef.AnnotationTargetSet annotationTargets,
                                            int pausePage)
        {
            var annotationsListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>
                (documentSettingsDlg.EditAnnotationList);
            RunUI(annotationsListDlg.SelectLastItem);
            var annotationDefDlg = ShowDialog<DefineAnnotationDlg>(annotationsListDlg.AddItem);

            RunUI(() =>
            {
                annotationDefDlg.AnnotationName = annotationName;
                annotationDefDlg.AnnotationType = annotationType;
                if (annotationValues != null)
                annotationDefDlg.Items = annotationValues;
                annotationDefDlg.AnnotationTargets = annotationTargets;
            });

            PauseForScreenShot<DefineAnnotationDlg>("Define Annotation form - " + annotationName, pausePage);

            OkDialog(annotationDefDlg, annotationDefDlg.OkDialog);
            OkDialog(annotationsListDlg, annotationsListDlg.OkDialog);
        }

        private static int SelectPeptidesUpUntil(string sequence)
        {
            bool peptideIfsSelected = false;
            int i = 0;
            RunUI(() => i = Array.IndexOf(SkylineWindow.Document.Peptides.ToArray(),
                SkylineWindow.SequenceTree.GetNodeOfType<PeptideTreeNode>().DocNode));
            do
            {
                SelectNode(SrmDocument.Level.Molecules, --i);
                RunUI(() => peptideIfsSelected = SkylineWindow.SelectedNode.Text.Contains(sequence));
                WaitForGraphs();
            }
            while (!peptideIfsSelected);

            return i;
        }

        private static void VerifyLowDotProducts(double dotp)
        {
            RunUI(() =>
            {
                var nodePep = ((PeptideTreeNode)SkylineWindow.SelectedNode).DocNode;
                var nodeGroup = nodePep.TransitionGroups.First();
                foreach (var tranGroupChromInfo in nodeGroup.ChromInfos)
                {
                    Assert.IsTrue(tranGroupChromInfo.LibraryDotProduct <= dotp,
                        string.Format("Found dotp {0} greater than {1}", tranGroupChromInfo.LibraryDotProduct, dotp));
                }
            });
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
                int resultsIndex = GetChromIndex(chromName);
                SkylineWindow.ActivateReplicate(chromName);
                Assert.AreEqual(resultsIndex, SkylineWindow.SelectedResultsIndex);
                SkylineWindow.RemovePeak(pathGroupRemove, nodeGroupRemove, null);

                // Confirm that peak has been removed from the right replicate
                var nodeGroupAfter = SkylineWindow.DocumentUI.FindNode(pathGroupRemove) as TransitionGroupDocNode;
                Assert.IsNotNull(nodeGroupAfter);
                Assert.AreSame(nodeGroupRemove.Id, nodeGroupAfter.Id);

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

            ActivateReplicate(chromName);

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

        private void SimpleGroupComparisons()
        {
            const string comparisonName = "Healthy v. Diseased";
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            RunUI(() => documentSettingsDlg.GetTabControl().SelectedIndex = 1);
            var editGroupComparisonDlg = ShowDialog <EditGroupComparisonDlg>(documentSettingsDlg.AddGroupComparison);
            RunUI(() =>
            {
                editGroupComparisonDlg.TextBoxName.Text = comparisonName;
                editGroupComparisonDlg.ComboControlAnnotation.SelectedItem = "Condition";
            });
            RunUI(() =>
            {
                editGroupComparisonDlg.ComboControlValue.SelectedItem = "Healthy";
                editGroupComparisonDlg.ComboCaseValue.SelectedItem = "Diseased";
                editGroupComparisonDlg.ComboIdentityAnnotation.SelectedItem = "SubjectId";
                editGroupComparisonDlg.ComboNormalizationMethod.SelectedItem = NormalizationMethod.GLOBAL_STANDARDS;
                editGroupComparisonDlg.TextBoxConfidenceLevel.Text = 99.ToString(CultureInfo.CurrentCulture);
                editGroupComparisonDlg.RadioScopePerProtein.Checked = true;
            });
            PauseForScreenShot<EditGroupComparisonDlg>("Edit Group Comparison", 64);
            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.OkDialog);
            PauseForScreenShot<DocumentSettingsDlg>("Document Settings", 65);
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
            RunUI(() => SkylineWindow.ShowGroupComparisonWindow(comparisonName));
            var foldChangeGrid = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete &&
                foldChangeGrid.DataboundGridControl.FindColumn(PropertyPath.Root.Property("FoldChangeResult")) != null);
            RunUI(() =>
            {
                var foldChangeResultColumn =
                    foldChangeGrid.DataboundGridControl.FindColumn(PropertyPath.Root.Property("FoldChangeResult"));
                foldChangeGrid.DataboundGridControl.DataGridView.AutoResizeColumn(foldChangeResultColumn.Index);
            });
            WaitForConditionUI(() => 0 != foldChangeGrid.DataboundGridControl.RowCount,
                "0 != foldChangeGrid.DataboundGridControl.RowCount");
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete,
                "foldChangeGrid.DataboundGridControl.IsComplete");
            PauseForScreenShot<FoldChangeGrid>("Healthy v. Diseased:Grid", 65);
            RunUI(() =>
            {
                foldChangeGrid.ShowGraph();
            });
            PauseForScreenShot<FoldChangeBarGraph>("Healthy v Diseased:Graph", 66);
            var foldChangeGraph = FindOpenForm<FoldChangeBarGraph>();
            RunUI(() =>
            {
                foldChangeGraph.Show(foldChangeGraph.DockPanel, DockState.Floating);
            });
            RunUI(() =>
            {
                var foldChangeResultColumn =
                    foldChangeGrid.DataboundGridControl.FindColumn(PropertyPath.Root.Property("FoldChangeResult"));
                foldChangeGrid.DataboundGridControl.DataGridView.Sort(foldChangeResultColumn, ListSortDirection.Ascending);
            });
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete);
            WaitForConditionUI(() => 48 == foldChangeGrid.DataboundGridControl.RowCount);
            RunUI(() => Assert.AreEqual(48, foldChangeGrid.DataboundGridControl.RowCount));
            {
                var quickFilterForm = ShowDialog<QuickFilterForm>(() =>
                {
                    var pvalueColumn =
                        foldChangeGrid.DataboundGridControl.FindColumn(
                            PropertyPath.Root.Property("FoldChangeResult").Property("AdjustedPValue"));
                    foldChangeGrid.DataboundGridControl.QuickFilter(pvalueColumn);
                });
                RunUI(() =>
                {
                    quickFilterForm.SetFilterOperation(0, FilterOperations.OP_IS_LESS_THAN);
                    quickFilterForm.SetFilterOperand(0, 0.01.ToString(CultureInfo.CurrentCulture));
                });
                OkDialog(quickFilterForm, quickFilterForm.OkDialog);
            }
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete);
            WaitForConditionUI(() => 14 == foldChangeGrid.DataboundGridControl.RowCount);
            RunUI(() => Assert.AreEqual(14, foldChangeGrid.DataboundGridControl.RowCount));
            PauseForScreenShot<FoldChangeBarGraph>("Right click on the graph and choose Copy", 67);
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete);
            var settingsForm = ShowDialog<GroupComparisonSettingsForm>(foldChangeGrid.ShowChangeSettings);
            RunUI(() => settingsForm.ComboIdentityAnnotation.SelectedIndex = 0);
            WaitForConditionUI(() => 37 == foldChangeGrid.DataboundGridControl.RowCount);
            RunUI(() => settingsForm.ComboIdentityAnnotation.SelectedItem = "SubjectId");
            RunUI(() =>
            {
                string folderName = Path.GetDirectoryName(SkylineWindow.DocumentFilePath);
                Assert.IsNotNull(folderName);
                string newFileName = Path.Combine(folderName,
                    "Rat_plasma_diff.sky");
                SkylineWindow.SaveDocument(newFileName);
                settingsForm.RadioScopePerPeptide.Checked = true;
            });
            {
                var quickFilterForm = ShowDialog<QuickFilterForm>(() =>
                {
                    var pvalueColumn =
                        foldChangeGrid.DataboundGridControl.FindColumn(
                            PropertyPath.Root.Property("FoldChangeResult").Property("AdjustedPValue"));
                    foldChangeGrid.DataboundGridControl.QuickFilter(pvalueColumn);
                });
                RunUI(() =>
                {
                    quickFilterForm.SetFilterOperation(0, FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL);
                    quickFilterForm.SetFilterOperand(0, 0.01.ToString(CultureInfo.CurrentCulture));
                });
                OkDialog(quickFilterForm, quickFilterForm.OkDialog);
            }
            WaitForConditionUI(() => 92 == foldChangeGrid.DataboundGridControl.RowCount);
            PauseForScreenShot<FoldChangeBarGraph>("Copy the graph", 68);
            RunUI(() =>
            {
                foldChangeGrid.DataboundGridControl.DataGridView.SelectAll();
                var colPeptide = foldChangeGrid.DataboundGridControl.FindColumn(PropertyPath.Root.Property("Peptide"));
                var rowToDeselect =
                    foldChangeGrid.DataboundGridControl.DataGridView.Rows.OfType<DataGridViewRow>()
                        .First(row => (string) row.Cells[colPeptide.Index].FormattedValue ==  "VVLSGSDATLAYSAFK");
                rowToDeselect.Selected = false;
            });
            PauseForScreenShot<FoldChangeGrid>("Healthy v. Diseased:Grid", 69);
            var messageDlg = ShowDialog<MultiButtonMsgDlg>(foldChangeGrid.FoldChangeBindingSource.ViewContext.Delete);
            PauseForScreenShot<MultiButtonMsgDlg>("Are you sure you want to delete...", 69);
            OkDialog(messageDlg, messageDlg.BtnYesClick);
        }

        private static void TestApplyToAll()
        {
            // Apply to all
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("GILAADESVGSMAK", null, "D_103_REP1", false, 19.0987);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    19.09872, 18.53870, 18.38107, 18.98798, 18.80227, 18.76315,
                    19.10433, 19.03328, 18.83977, 19.10928, 18.65008, 18.45997,
                    19.44880, 18.57200, 18.79917, 18.73012, 18.64647, 18.72783,
                    18.84278, 18.35188, 18.72457, 18.80247, 18.57477, 18.30993));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LNDGSQITFEK", null, "D_138_REP1", false, 23.5299);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    23.45410, 22.77782, 23.11210, 23.19398, 22.88790, 23.00840,
                    23.52992, 23.57400, 23.19233, 23.45998, 22.81207, 22.81960,
                    23.87478, 23.68238, 23.03755, 22.89255, 22.69688, 23.04172,
                    22.85375, 23.04702, 22.85068, 22.88932, 22.70258, 23.19258));
            });
            // Apply to subsequent
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("DLTGFPQGADQR", null, "H_159_REP2", true, 24.7955);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    24.02537, 23.31307, 24.60460, 23.61628, 23.12335, 25.40152,
                    23.99268, 23.95832, 25.69842, 24.07185, 23.27097, 25.12858,
                    24.41210, 26.79822, 25.46880, 23.08690, 26.87400, 25.36023,
                    23.05053, 24.79552, 25.24442, 23.38800, 25.08665, 24.63885));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("GATYAFSGSHYWR", null, "D_103_REP3", true, 16.4223);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    17.38182, 17.08060, 16.42228, 17.19397, 17.21502, 16.66367,
                    17.23210, 17.45838, 16.66472, 17.39735, 17.00417, 16.43715,
                    17.61817, 17.00378, 16.64450, 17.15165, 17.00528, 16.54887,
                    17.20048, 16.88772, 16.51452, 17.14997, 17.04057, 16.47637));
            });

            // Test apply to all on a case with an obvious reference point
            // with two small (sometimes non-existent) peaks on either side
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LGGEEVSVAC[+57.0]K", null, "H_148_REP1", false, 13.1616);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    13.16143, 13.12685, 13.12692, 13.09358, 13.16153, 13.16042,
                    13.12773, 13.26173, 13.19442, 13.12825, 13.09322, 13.12713,
                    13.22888, 13.12702, 13.19365, 13.16158, 13.12768, 13.12695,
                    13.09430, 13.06082, 13.16077, 13.12740, 13.12707, 13.09500));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LGGEEVSVAC[+57.0]K", null, "H_148_REP1", false, 13.4631);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    13.46293, 13.39485, 13.39492, 13.42858, 13.39603, 13.46192,
                    13.42923, 13.46273, 13.46242, 13.42975, 13.42822, 13.42863,
                    13.46338, 13.42852, 13.46165, 13.46308, 13.46268, 13.42845,
                    13.42930, 13.36232, 13.42877, 13.42890, 13.39507, 13.43000));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("LGGEEVSVAC[+57.0]K", null, "H_148_REP1", false, 13.6641);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(
                    14.30043, 13.79685, 13.79692, 13.79708, 14.33403, 14.90242,
                    13.83123, 14.03223, 13.66342, 13.76475, 13.83022, 13.73013,
                    14.33438, 13.83052, 14.70115, 13.66408, 13.63018, 13.69645,
                    13.56330, 13.52982, 13.69677, 13.83090, 13.56257, 13.76500));
            });

            // For each test, a peak was picked and applied - undo two actions per test
            for (int i = 0; i < 2*7; i++)
                RunUI(() => SkylineWindow.Undo());
        }

        private static Dictionary<string, double> MakeVerificationDictionary(params double[] expected)
        {
            Assert.AreEqual(24, expected.Length);
            return new Dictionary<string, double>
            {
                {"D_103_REP1", expected[0]}, {"D_103_REP2", expected[1]}, {"D_103_REP3", expected[2]},
                {"D_108_REP1", expected[3]}, {"D_108_REP2", expected[4]}, {"D_108_REP3", expected[5]},
                {"D_138_REP1", expected[6]}, {"D_138_REP2", expected[7]}, {"D_138_REP3", expected[8]},
                {"D_196_REP1", expected[9]}, {"D_196_REP2", expected[10]}, {"D_196_REP3", expected[11]},
                {"H_146_REP1", expected[12]}, {"H_146_REP2", expected[13]}, {"H_146_REP3", expected[14]},
                {"H_148_REP1", expected[15]}, {"H_148_REP2", expected[16]}, {"H_148_REP3", expected[17]},
                {"H_159_REP1", expected[18]}, {"H_159_REP2", expected[19]}, {"H_159_REP3", expected[20]},
                {"H_162_REP1", expected[21]}, {"H_162_REP2", expected[22]}, {"H_162_REP3", expected[23]},
            };
        }
    }
}