/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that the chosen normalization method changes to an appropriate value if
    /// the current option is not supported by the document.
    /// </summary>
    [TestClass]
    public class ConstrainNormalizationMethodTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestConstrainNormalizationMethod()
        {
            TestFilesZip = @"TestFunctional\ConstrainNormalizationMethodTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string pathGlobalStandardsNoHeavy = TestFilesDir.GetTestPath("Rat_plasma.sky");
            RunUI(()=>SkylineWindow.OpenFile(pathGlobalStandardsNoHeavy));
            var globalStandardPaths = SkylineWindow.Document.GetMoleculeGroupPairs()
                .Where(pair => pair.NodeMolecule.GlobalStandardType == StandardType.GLOBAL_STANDARD).Select(pair =>
                    new IdentityPath(pair.NodeMoleculeGroup.PeptideGroup, pair.NodeMolecule.Peptide)).ToList();
            Assert.AreNotEqual(0, globalStandardPaths.Count);
            Assert.IsTrue(SkylineWindow.Document.Settings.HasGlobalStandardArea);

            // Modify the current document so that it has no global standards, and save the document as "NoGlobalStandards.sky"
            RunUI(()=>
            {
                SkylineWindow.SequenceTree.SelectedPaths = globalStandardPaths;
                SkylineWindow.SetStandardType(null);
            });
            Assert.IsFalse(SkylineWindow.Document.Settings.HasGlobalStandardArea);
            string pathNoGlobalStandardsNoHeavy = TestFilesDir.GetTestPath("NoGlobalStandards.sky");
            RunUI(()=>SkylineWindow.SaveDocument(pathNoGlobalStandardsNoHeavy));

            // Open a document which has heavy standards and choose "Normalize To Heavy"
            string pathHasHeavy = TestFilesDir.GetTestPath("Human_plasma.sky");
            var heavyNormalizeOption = NormalizeOption.FromIsotopeLabelType(IsotopeLabelType.heavy);
            RunUI(()=>
            {
                SkylineWindow.OpenFile(pathHasHeavy);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                // Choose "Normalize To Heavy" normalization method
                SkylineWindow.SetNormalizationMethod(heavyNormalizeOption);
            });
            WaitForGraphs();
            var areaReplicateGraphPane = FindAreaReplicateGraphPane();
            var options = GetNormalizationOptionMenuItems(areaReplicateGraphPane, heavyNormalizeOption);
            CollectionAssert.Contains(options, heavyNormalizeOption);
            CollectionAssert.DoesNotContain(options, NormalizeOption.GLOBAL_STANDARDS);

            // Open a document which has no heavy standards
            RunUI(() =>
            {
                SkylineWindow.OpenFile(pathGlobalStandardsNoHeavy);
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();
            areaReplicateGraphPane = FindAreaReplicateGraphPane();

            // The currently selected normalization option should have changed to "DEFAULT" since there
            // are no global standards
            options = GetNormalizationOptionMenuItems(areaReplicateGraphPane, NormalizeOption.DEFAULT);
            CollectionAssert.DoesNotContain(options, heavyNormalizeOption);
            CollectionAssert.Contains(options, NormalizeOption.GLOBAL_STANDARDS);

            // Choose Global Standard normalization
            RunUI(()=>SkylineWindow.SetNormalizationMethod(NormalizeOption.GLOBAL_STANDARDS));
            WaitForGraphs();
            GetNormalizationOptionMenuItems(areaReplicateGraphPane, NormalizeOption.GLOBAL_STANDARDS);

            // Open a document which does not have global standards
            RunUI(()=>
            {
                SkylineWindow.OpenFile(pathNoGlobalStandardsNoHeavy);
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();
            areaReplicateGraphPane = FindAreaReplicateGraphPane();

            // Verify that normalization option has changed to "DEFAULT".
            options = GetNormalizationOptionMenuItems(areaReplicateGraphPane, NormalizeOption.DEFAULT);
            CollectionAssert.DoesNotContain(options, NormalizeOption.GLOBAL_STANDARDS);
            CollectionAssert.DoesNotContain(options, heavyNormalizeOption);
        }

        /// <summary>
        /// Returns the set of normalization menu items that were found on the graph pane's context menu.
        /// </summary>
        /// <param name="graphPane">Graph pane to </param>
        /// <param name="expectedSelected">The NormalizeOption which is expected to have a checkmark next to it</param>
        private List<NormalizeOption> GetNormalizationOptionMenuItems(SummaryGraphPane graphPane, NormalizeOption expectedSelected)
        {
            List<NormalizeOption> list = null;
            bool foundSelected = false;
            List<ToolStripMenuItem> menuItems = null;
            RunUI(() =>
            {
                var graphControl = graphPane.GraphSummary.GraphControl;
                graphControl.ContextMenuStrip.Show(graphControl, new Point());
                menuItems = graphControl.ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                    .SelectMany(EnumerateMenuItems).ToList();
                graphControl.ContextMenuStrip.Hide();
            });

            var groups = menuItems.Where(item => item.Tag is NormalizeOption)
                .GroupBy(item => (NormalizeOption) item.Tag).ToList();
            list = groups.Select(grouping => grouping.Key).ToList();
            foreach (var grouping in groups)
            {
                var normalizeOption = grouping.Key;
                bool expectedChecked = expectedSelected.Equals(normalizeOption);
                foundSelected = foundSelected || expectedChecked;
                foreach (var menuItem in grouping)
                {
                    if (menuItem.Checked != expectedChecked)
                    {
                        Assert.Fail("Unexpected checked state on {0} expected {1} selected", menuItem.Text, expectedSelected);
                    }
                }
            }
            Assert.IsTrue(foundSelected, "{0} not found", expectedSelected);
            return list;
        }

        private IEnumerable<ToolStripMenuItem> EnumerateMenuItems(ToolStripMenuItem menuItem)
        {
            var list = new List<ToolStripMenuItem> {menuItem};
            if (menuItem.DropDownItems.Count > 0)
            {
                menuItem.DropDown.Show();
                list.AddRange(menuItem.DropDownItems.OfType<ToolStripMenuItem>().SelectMany(EnumerateMenuItems));
            }

            return list;
        }

        private AreaReplicateGraphPane FindAreaReplicateGraphPane()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().Select(summary => summary.GraphControl.GraphPane)
                .OfType<AreaReplicateGraphPane>().FirstOrDefault();
        }
    }
}
