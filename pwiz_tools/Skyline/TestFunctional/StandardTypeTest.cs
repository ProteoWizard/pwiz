/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test of peptide standard types (iRT, QC and Normalization)
    /// </summary>
    [TestClass]
    public class StandardTypeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStandardType()
        {
            TestFilesZip = @"TestFunctional\StandardTypeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Open the SRMCourse.sky file
            string documentPath1 = TestFilesDir.GetTestPath("SRMCourse.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath1));
            WaitForDocumentLoaded();

            {
                var peps = SkylineWindow.Document.Peptides.ToArray();
                Assert.IsNull(peps[0].GlobalStandardType);
                Assert.IsTrue(peps.Skip(1).Take(10).All(nodePep =>
                    Equals(nodePep.GlobalStandardType, PeptideDocNode.STANDARD_TYPE_IRT)));
                Assert.IsTrue(peps.Skip(11).Take(3).All(nodePep => nodePep.GlobalStandardType == null));
            }
           
            // Test Set Standard Type menus
            // Non-peptide selected (CONSIDER: Should this allow setting standard type on all peptides?)
            SelectNode(SrmDocument.Level.PeptideGroups, 0);
            RunUI(() => SkylineWindow.ShowTreeNodeContextMenu(new Point(0, 0)));
            RunUI(() =>
                {
                    Assert.IsFalse(SkylineWindow.SetStandardTypeConextMenuItem.Visible);
                    SkylineWindow.ContextMenuTreeNode.Close();
                });
            // Select first peptide, which is not actually an iRT standard
            SelectNode(SrmDocument.Level.Peptides, 0);
            RunUI(() => SkylineWindow.ShowTreeNodeContextMenu(new Point(0, 0)));
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.SetStandardTypeConextMenuItem.Visible);
                SkylineWindow.ContextMenuTreeNode.Close();
            });
            // CONSIDER: Would have been nice to validate the contents of the Set Standard Type menu
            //           but it was difficult to get it showing with the Opening event handler firing

            // Set QC type
            RunUI(() => SetPeptideStandardType(1, 0, 3, PeptideDocNode.STANDARD_TYPE_QC));

            // Set back to none
            RunUI(() => SetPeptideStandardType(1, 0, 3, null));

            // Set to decoys should fail
            RunUI(() => SetPeptideStandardType(SkylineWindow.DocumentUI.PeptideGroupCount - 1, 0, 5,
                PeptideDocNode.STANDARD_TYPE_NORMALIZAITON, false));

            // Set Normalization type
            RunUI(() => SetPeptideStandardType(1, 0, 3, PeptideDocNode.STANDARD_TYPE_NORMALIZAITON));

            RunUI(() =>
            {
                AreaReplicateGraphPane pane;
                Assert.IsTrue(SkylineWindow.GraphPeakArea.TryGetGraphPane(out pane));
                Assert.AreEqual(5e+6, pane.YAxis.Scale.Max, 1);
                Assert.IsTrue(pane.YAxis.Title.Text.StartsWith(Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area));

                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_global_standard_view);
            });
            WaitForGraphs();

            // Select peptide index 16
            const int SELECTED_PEPTIDE_INDEX = 16;
            var peptidePath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Peptides, SELECTED_PEPTIDE_INDEX);
            RunUI(() =>
            {
                AreaReplicateGraphPane pane;
                Assert.IsTrue(SkylineWindow.GraphPeakArea.TryGetGraphPane(out pane));
                Assert.AreEqual(0.1, pane.YAxis.Scale.Max, 1e-5);
                Assert.IsTrue(pane.YAxis.Title.Text.StartsWith(Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area_Ratio_To_Global_Standards));
                Assert.AreEqual(5, SkylineWindow.GraphPeakArea.CurveCount);

                // Select a peptide with both light and heavy precursors
                SkylineWindow.SelectedPath = peptidePath;
                // Make it a QC peptide to have one in the StandardType report column later
                SkylineWindow.SetStandardType(PeptideDocNode.STANDARD_TYPE_QC);
            });
            WaitForGraphs();

            RunUI(() =>
            {
                // Should stay ratio to global standards
                AreaReplicateGraphPane pane;
                Assert.IsTrue(SkylineWindow.GraphPeakArea.TryGetGraphPane(out pane));
                Assert.AreEqual(0.2, pane.YAxis.Scale.Max, 1e-5);
                Assert.IsTrue(pane.YAxis.Title.Text.StartsWith(Resources.AreaReplicateGraphPane_UpdateGraph_Peak_Area_Ratio_To_Global_Standards));
                Assert.AreEqual(2, SkylineWindow.GraphPeakArea.CurveCount);
            });

            var ratioColumnLight = RatioPropertyAccessor.PeptideRatioProperty(IsotopeLabelType.light, null);
            var ratioColumnHeavy = RatioPropertyAccessor.PeptideRatioProperty(IsotopeLabelType.heavy, null);
            var ratioColumnPrec = RatioPropertyAccessor.PrecursorRatioProperty(null);
            var ratioColumnTran = RatioPropertyAccessor.TransitionRatioProperty(null);

            if (IsEnableLiveReports)
            {
                var resultsGridForm = ShowDialog<LiveResultsGrid>(() => SkylineWindow.ShowResultsGrid(true));
                var resultsGrid = resultsGridForm.DataGridView;

                // TODO: Implement live report support
            }
            else
            {
                var resultsGridForm = ShowDialog<ResultsGridForm>(() => SkylineWindow.ShowResultsGrid(true));
                var resultsGrid = resultsGridForm.ResultsGrid;
                AddResultGridColumns(resultsGridForm, ratioColumnLight, ratioColumnHeavy);

                var lightValues = new List<float>();
                var heavyValues = new List<float>();
                RunUI(() =>
                {
                    // Make sure cells have values
                    foreach (DataGridViewRow row in resultsGrid.Rows)
                    {
                        float lightValue = (float)row.Cells[ratioColumnLight.ColumnName].Value;
                        Assert.IsFalse(string.IsNullOrEmpty(lightValue.ToString(CultureInfo.CurrentCulture)));
                        lightValues.Add(lightValue);
                        float heavyValue = (float)row.Cells[ratioColumnHeavy.ColumnName].Value;
                        Assert.IsFalse(string.IsNullOrEmpty(heavyValue.ToString(CultureInfo.CurrentCulture)));
                        heavyValues.Add(heavyValue);
                    }

                    // Select the first precursor
                    SkylineWindow.SelectedNode.Expand();
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.NextVisibleNode;
                });
                WaitForGraphs();

                AddResultGridColumns(resultsGridForm, ratioColumnPrec);

                RunUI(() =>
                {
                    // Make sure precursor ratios match the values shown for the peptide
                    for (int i = 0; i < resultsGrid.Rows.Count; i++)
                        Assert.AreEqual(lightValues[i], resultsGrid.Rows[i].Cells[ratioColumnLight.ColumnName].Value);
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.NextVisibleNode;
                    for (int i = 0; i < resultsGrid.Rows.Count; i++)
                        Assert.AreEqual(heavyValues[i], resultsGrid.Rows[i].Cells[ratioColumnHeavy.ColumnName].Value);

                    // Select the first transtion of the heavy precursor
                    SkylineWindow.SelectedNode.Expand();
                    SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.NextVisibleNode;
                });
                WaitForGraphs();

                AddResultGridColumns(resultsGridForm, ratioColumnTran);

                RunUI(() =>
                {
                    // Make sure transition ratios are present
                    foreach (DataGridViewRow row in resultsGrid.Rows)
                    {
                        float lightValue = (float)row.Cells[ratioColumnTran.ColumnName].Value;
                        Assert.IsFalse(string.IsNullOrEmpty(lightValue.ToString(CultureInfo.CurrentCulture)));
                    }
                });

                var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
                var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
                var pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.AddItem);

                var columnsToAdd = new[]
                        {
                            // Not L10N
                            new Identifier("Peptides", "Sequence"),
                            new Identifier("Peptides", "StandardType"),
                            new Identifier("Peptides", "Precursors", "Charge"),
                            new Identifier("Peptides", "Precursors", "IsotopeLabelType"),
                            new Identifier("Peptides", "PeptideResults", ratioColumnLight.DisplayName),
                            new Identifier("Peptides", "PeptideResults", ratioColumnHeavy.DisplayName),
                            new Identifier("Peptides", "Precursors", "PrecursorResults", ratioColumnPrec.DisplayName),
                            new Identifier("Peptides", "Precursors", "Transitions", "FragmentIon"),
                            new Identifier("Peptides", "Precursors", "Transitions", "TransitionResults", ratioColumnTran.DisplayName),
                        };
                RunUI(() =>
                {
                    foreach (Identifier id in columnsToAdd)
                    {
                        Assert.IsTrue(pivotReportDlg.TrySelect(id), string.Format("Failed attempting to add {0} to a report", id));
                        pivotReportDlg.AddSelectedColumn();
                    }
                });
                var previewReportDlg = ShowDialog<PreviewReportDlg>(pivotReportDlg.ShowPreview);

                RunUI(() =>
                {
                    var doc = SkylineWindow.DocumentUI;
                    int countReplicates = doc.Settings.MeasuredResults.Chromatograms.Count;
                    Assert.AreEqual(doc.TransitionCount*countReplicates, previewReportDlg.RowCount);
                    Assert.AreEqual(columnsToAdd.Length, previewReportDlg.ColumnCount);
                    var headerNames = previewReportDlg.ColumnHeaderNames.ToArray();
                    int iStandardType = headerNames.IndexOf(name => Equals(name, "StandardType"));
                    int iLabelType = headerNames.IndexOf(name => Equals(name, "IsotopeLabelType"));
                    int iLightRatio = headerNames.IndexOf(name => Equals(name, ratioColumnLight.DisplayName));
                    int iHeavyRatio = headerNames.IndexOf(name => Equals(name, ratioColumnHeavy.DisplayName));
                    int iPrecRatio = headerNames.IndexOf(name => Equals(name, ratioColumnPrec.DisplayName));
                    int iTranRatio = headerNames.IndexOf(name => Equals(name, ratioColumnTran.DisplayName));
                    int iRow = 0, iPeptide = 0;
                    foreach (var nodePep in doc.Peptides)
                    {
                        foreach (var nodeGroup in nodePep.TransitionGroups)
                        {
                            for (int t = 0; t < nodeGroup.TransitionCount; t++)
                            {
                                for (int i = 0; i < countReplicates; i++)
                                {
                                    var row = previewReportDlg.DataGridView.Rows[iRow++];
                                    string standardTypeExpected = nodePep.GlobalStandardType;
                                    var standardTypeActual = row.Cells[iStandardType].Value;
                                    if (standardTypeExpected == null)
                                    {
                                        if (standardTypeActual != null)
                                            Assert.IsNull(standardTypeActual, string.Format("Non-null standard type {0} found when not expected", standardTypeActual));
                                    }
                                    else
                                        Assert.AreEqual(standardTypeExpected, standardTypeActual);
                                    float peptideLightRatio = (float)(double)row.Cells[iLightRatio].Value;
                                    // Light ration never empty
                                    Assert.IsTrue(peptideLightRatio > 0);
                                    float peptideHeavyRatio = (float)(double)row.Cells[iHeavyRatio].Value;
                                    // Heavy ratio empty when peptide has only a light precursor
                                    Assert.IsTrue(nodePep.TransitionGroupCount == 1
                                        ? peptideHeavyRatio == 0
                                        : peptideHeavyRatio > 0 && peptideHeavyRatio != peptideLightRatio);
                                    string labelType = row.Cells[iLabelType].Value.ToString();
                                    float precursorRatio = (float)(double)row.Cells[iPrecRatio].Value;
                                    if (string.Equals(labelType, IsotopeLabelType.light.Name))
                                        Assert.AreEqual(peptideLightRatio, precursorRatio);
                                    else
                                        Assert.AreEqual(peptideHeavyRatio, precursorRatio);
                                    if (iPeptide == SELECTED_PEPTIDE_INDEX)
                                    {
                                        Assert.AreEqual(lightValues[i], peptideLightRatio);
                                        Assert.AreEqual(heavyValues[i], peptideHeavyRatio);
                                    }

                                    float transitionRatio = (float)(double)row.Cells[iTranRatio].Value;
                                    Assert.IsTrue(transitionRatio < precursorRatio);
                                }
                            }
                        }
                        iPeptide++;
                    }
                });

                OkDialog(previewReportDlg, previewReportDlg.OkDialog);
                OkDialog(pivotReportDlg, pivotReportDlg.CancelButton.PerformClick);
                OkDialog(editReportListDlg, editReportListDlg.OkDialog);
                OkDialog(exportReportDlg, exportReportDlg.CancelClick);
            }

            // Open the MultiLabel.sky file
            string documentPath2 = TestFilesDir.GetTestPath("MultiLabel.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath2));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SetStandardType(PeptideDocNode.STANDARD_TYPE_NORMALIZAITON));

//            PauseAndContinue();
        }

        private static void AddResultGridColumns(ResultsGridForm resultsGridForm, params RatioPropertyAccessor.RatioPropertyName[] ratioPropertyNames)
        {
            RunDlg<ColumnChooser>(resultsGridForm.ChooseColumns, chooseColumnsDlg =>
                {
                    foreach (var ratioPropertyName in ratioPropertyNames)
                    {
                        chooseColumnsDlg.CheckedListBox.SetItemChecked(
                            chooseColumnsDlg.CheckedListBox.Items.IndexOf(ratioPropertyName.HeaderText), true);
                    }
                    chooseColumnsDlg.AcceptButton.PerformClick();
                });
        }

        private void SetPeptideStandardType(int protindex, int pepStartIndex, int pepCount, string standardType, bool success = true)
        {
            var qcPeps = SkylineWindow.SequenceTree.Nodes[protindex];
            SelectRange(qcPeps.Nodes[pepStartIndex], qcPeps.Nodes[pepStartIndex + pepCount - 1]);
            SkylineWindow.SetStandardType(standardType);

            var docChanged = SkylineWindow.DocumentUI;
            ValidateStandardType(docChanged, protindex, pepStartIndex, pepCount, standardType, success);

            var docRound = AssertEx.RoundTrip(docChanged);
            ValidateStandardType(docRound, protindex, pepStartIndex, pepCount, standardType, success);
        }

        private static void ValidateStandardType(SrmDocument docChanged, int protindex, int pepStartIndex, int pepCount,
                                                 string standardType, bool success)
        {
            var pepsChanged = docChanged.PeptideGroups.ElementAt(protindex).Peptides;
            Assert.IsTrue(pepsChanged.Skip(pepStartIndex).Take(pepCount).All(nodePep =>
                Equals(nodePep.GlobalStandardType, standardType)) == success);
        }

        private void SelectRange(TreeNode nodeStart, TreeNode nodeEnd)
        {
            var tree = SkylineWindow.SequenceTree;
            tree.KeysOverride = Keys.None;
            tree.SelectedNode = nodeStart;
            tree.KeysOverride = Keys.Shift;
            tree.SelectedNode = nodeEnd;
            tree.KeysOverride = Keys.None;
        }
    }
}