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
    [TestClass]
    public class ConstrainNormalizationMethodTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestContrainNormalizationMethod()
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
            RunUI(()=>
            {
                SkylineWindow.SequenceTree.SelectedPaths = globalStandardPaths;
                SkylineWindow.SetStandardType(null);
            });
            Assert.IsFalse(SkylineWindow.Document.Settings.HasGlobalStandardArea);
            string pathNoGlobalStandardsNoHeavy = TestFilesDir.GetTestPath("NoGlobalStandards.sky");
            RunUI(()=>SkylineWindow.SaveDocument(pathNoGlobalStandardsNoHeavy));
            string pathHasHeavy = TestFilesDir.GetTestPath("Human_plasma.sky");
            var heavyNormalizeOption = NormalizeOption.FromIsotopeLabelType(IsotopeLabelType.heavy);
            RunUI(()=>
            {
                SkylineWindow.OpenFile(pathHasHeavy);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.SetNormalizationMethod(heavyNormalizeOption);
            });
            WaitForGraphs();
            var areaReplicateGraphPane = GetAreaReplicateGraphPane();
            var options = GetNormalizationOptionMenuItems(areaReplicateGraphPane, heavyNormalizeOption);
            CollectionAssert.DoesNotContain(options, NormalizeOption.GLOBAL_STANDARDS);
            RunUI(() =>
            {
                SkylineWindow.OpenFile(pathGlobalStandardsNoHeavy);
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();
            areaReplicateGraphPane = GetAreaReplicateGraphPane();
            options = GetNormalizationOptionMenuItems(areaReplicateGraphPane, NormalizeOption.DEFAULT);
            CollectionAssert.DoesNotContain(options, heavyNormalizeOption);
            RunUI(()=>SkylineWindow.SetNormalizationMethod(NormalizeOption.GLOBAL_STANDARDS));
            WaitForGraphs();
            GetNormalizationOptionMenuItems(areaReplicateGraphPane, NormalizeOption.GLOBAL_STANDARDS);

            RunUI(()=>
            {
                SkylineWindow.OpenFile(pathNoGlobalStandardsNoHeavy);
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();
            areaReplicateGraphPane = GetAreaReplicateGraphPane();
            options = GetNormalizationOptionMenuItems(areaReplicateGraphPane, NormalizeOption.DEFAULT);
            CollectionAssert.DoesNotContain(options, NormalizeOption.GLOBAL_STANDARDS);
            CollectionAssert.DoesNotContain(options, heavyNormalizeOption);
        }

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

        private AreaReplicateGraphPane GetAreaReplicateGraphPane()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().Select(summary => summary.GraphControl.GraphPane)
                .OfType<AreaReplicateGraphPane>().FirstOrDefault();
        }
    }
}
