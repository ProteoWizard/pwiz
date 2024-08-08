/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class GroupComparisonVolcanoPlotTest : AbstractFunctionalTestEx
    {
        private const double ACCURACY = 1E-6;
        [TestMethod]
        public void TestGroupComparisonVolcanoPlot()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(@"Rat_plasma.sky");

            // Create new group comparison
            CreateGroupComparison("Test Group Comparison", "Condition", "Healthy", "Diseased");
            var grid = ShowDialog<FoldChangeGrid>(() => SkylineWindow.ShowGroupComparisonWindow("Test Group Comparison"));

            // Wait for grid and show volcano plot
            var volcanoPlot = ShowDialog<FoldChangeVolcanoPlot>(() => grid.ShowVolcanoPlot());
            volcanoPlot.UseOverridenKeys = true;

            // Check if data is displayed correctly without any bounds set
            OpenVolcanoPlotProperties(volcanoPlot, p => p.TextFoldChangeCutoff.Text = p.TextFoldChangeCutoff.Text = p.TextPValueCutoff.Text = "");
            WaitForVolcanoPlotPointCount(grid, 125);
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, false, 1, 124, 0));

            // Set bounds to p value < 0.05 and fold-change 2
            OpenVolcanoPlotProperties(volcanoPlot, p =>
            {
                p.TextFoldChangeCutoff.Text = (1.0).ToString(CultureInfo.CurrentCulture);
                p.TextPValueCutoff.Text = (-Math.Log10(0.05)).ToString(CultureInfo.CurrentCulture);
            });

            // Check if data is displayed correctly with default settings
            WaitForVolcanoPlotPointCount(grid, 125);
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, true, 1, 42, 82));

            // Verify that checking/unchecking the checkbox correctly converts the values
            OpenVolcanoPlotProperties(volcanoPlot, p =>
            {
                Assert.IsTrue(p.CheckBoxLog.Checked);
                p.CheckBoxLog.Checked = false;
                double value;
                Assert.IsTrue(double.TryParse(p.TextFoldChangeCutoff.Text, out value));
                Assert.AreEqual(2, value, ACCURACY);
                Assert.IsTrue(double.TryParse(p.TextPValueCutoff.Text, out value));
                Assert.AreEqual(0.05, value, ACCURACY);
                p.CheckBoxLog.Checked = true;
                Assert.IsTrue(double.TryParse(p.TextFoldChangeCutoff.Text, out value));
                Assert.AreEqual(1.0, value, ACCURACY);
                Assert.IsTrue(double.TryParse(p.TextPValueCutoff.Text, out value));
                Assert.AreEqual(-Math.Log10(0.05), value, ACCURACY);
            });

            // Remove peptides below cutoffs and restore them
            var count = GetRowCount(grid);
            RunUI(volcanoPlot.RemoveBelowCutoffs);
            WaitForVolcanoPlotPointCount(grid, count - 82); // 83 peptides are below the cutoffs, 1 is a global standard type
            Assert.AreEqual(count - 82, GetPeptideCount());
            RunUI(SkylineWindow.Undo);
            WaitForVolcanoPlotPointCount(grid, count);
            Assert.AreEqual(count, GetPeptideCount());

            OpenVolcanoPlotProperties(volcanoPlot, p =>
            {
                // Enable filtering for next check
                p.CheckBoxFilter.Checked = true;
            });

            // Check if the points that are not within cutoff have been removed -> outCount should stay the same, selectedCount and inCount 0
            WaitForVolcanoPlotPointCount(grid, 42);
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, true, 0, 42, 0));

            // Disable filtering
            OpenVolcanoPlotProperties(volcanoPlot, p =>
            {
                p.CheckBoxFilter.Checked = false;
            });

            // Verify that the filtered points are back
            WaitForVolcanoPlotPointCount(grid, 125);
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, true, 1, 42, 82));

            // Test that unchecking removes the absolute fold-change column
            RunUI(() => CollectionAssert.DoesNotContain(grid.DataboundGridControl.ColumnHeaderNames, ColumnCaptions.AbsLog2FoldChange));

            // Test selection change within the volcano plot
            MoveAndClick(volcanoPlot, -1.4747, 3.4163);
            WaitForConditionUI(() => !SkylineWindow.SequenceTree.IsInUpdate && !volcanoPlot.UpdatePending);

            // Now 1 peptide should be selected
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, true, 1, 41, 83));
            AssertSelectionCorrect(volcanoPlot, "EVLPELGIK", 1);

            MoveAndClick(volcanoPlot, 5.1869, -Math.Log10(1E-6), true); // This peptide's pvalue is too small so it's capped at 1E-6
            WaitForConditionUI(() => !SkylineWindow.SequenceTree.IsInUpdate && !volcanoPlot.UpdatePending);

            // Now 2 peptides should be selected
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, true, 2, 40, 83));
            AssertSelectionCorrect(volcanoPlot, "AIAYLNTGYQR", 2);

            MoveAndClick(volcanoPlot, -1.4747, 3.4163, true); // Deselect the first peptide
            WaitForConditionUI(() => !SkylineWindow.SequenceTree.IsInUpdate && !volcanoPlot.UpdatePending);

            // Now only 1 should be selected again
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, true, 1, 41, 83));
            AssertSelectionCorrect(volcanoPlot, "AIAYLNTGYQR", 1);

            // Test select all
            RunUI(() => SkylineWindow.SelectAll());
            WaitForConditionUI(() => !SkylineWindow.SequenceTree.IsInUpdate && !volcanoPlot.UpdatePending);
            RunUI(() => AssertVolcanoPlotCorrect(volcanoPlot, true, 100, 0, 25)); // Selection limited to 100, all other points are not within the cutoff

            // Verify that the grid and the volcano plot are open after re-opening the document
            RunUI(() => SkylineWindow.SaveDocument());
            OpenDocument(@"Rat_plasma.sky");

            var openForms = FormUtil.OpenForms.OfType<FoldChangeForm>().ToArray();
            Assert.AreEqual(2, openForms.Length);
            Assert.IsTrue(openForms.Any(f => f is FoldChangeVolcanoPlot));
            Assert.IsTrue(openForms.Any(f => f is FoldChangeGrid));
        }

        private int GetPeptideCount()
        {
            var count = -1;
            RunUI(() => count = SkylineWindow.DocumentUI.PeptideCount);
            return count;
        }


        private static void AssertSelectionCorrect(FoldChangeVolcanoPlot volcanoPlot, string selectedSequence, int totalSelected)
        {
            RunUI(() =>
            {
                var row = volcanoPlot.GetSelectedRow();
                Assert.IsNotNull(row);
                Assert.IsNotNull(row.Peptide);
                Assert.AreEqual(selectedSequence, row.Peptide.Sequence);
                Assert.AreEqual(selectedSequence, SkylineWindow.SelectedPeptideSequence);
                Assert.AreEqual(totalSelected, SkylineWindow.SequenceTree.SelectedPaths.Count);
            });
        }

        private static void MoveAndClick(FoldChangeVolcanoPlot volcanoPlot, double x, double y, bool ctrl = false)
        {
            RunUI(() =>
            {
                if (ctrl)
                    volcanoPlot.OverridenModifierKeys = Keys.Control;
                volcanoPlot.MoveMouse(MouseButtons.Left, volcanoPlot.GraphToScreenCoordinates(x, y));
                volcanoPlot.ClickSelectedRow();
                volcanoPlot.OverridenModifierKeys = Keys.None;
            });
        }

        private static void AssertVolcanoPlotCorrect(FoldChangeVolcanoPlot plot, bool showingBounds, int selectedCount, int outCount, int inCount)
        {
            var curveCounts = plot.GetCurveCounts();
            Assert.AreEqual(showingBounds, FoldChangeVolcanoPlot.AnyCutoffSettingsValid);
            Assert.AreEqual(showingBounds ? 5 : 2,  curveCounts.CurveCount);

            if (selectedCount != curveCounts.SelectedCount ||
                outCount != curveCounts.OutCount ||
                inCount != curveCounts.InCount)
            {
                Assert.Fail("Volcano plot expected: selectedCount={0}, outCount={1}, inCount={2}; actual: selectedCount={3}, outCount={4}, inCount={5}",
                    selectedCount, outCount, inCount,
                    curveCounts.SelectedCount, curveCounts.OutCount, curveCounts.InCount);
            }
        }

        private static void OpenVolcanoPlotProperties(FoldChangeVolcanoPlot volcanoPlot, Action<VolcanoPlotPropertiesDlg> action)
        {
            RunDlg<VolcanoPlotPropertiesDlg>(volcanoPlot.ShowProperties, d =>
            {
                action(d);
                d.OkDialog();
            });
        }
    }
}