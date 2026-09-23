/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.MSGraph;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests various combinations of View>Transitions and View>Transform on a chromatogram
    /// graph.
    /// </summary>
    [TestClass]
    public class ChromGraphTransformTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestChromGraphTotal()
        {
            TestFilesZip = @"TestFunctional\ChromGraphTransformTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument("BSA_Agilent.sky");
            foreach (TransformChrom transformChrom in new[] {TransformChrom.raw, TransformChrom.interpolated})
            {
                Settings.Default.TransformTypeChromatogram = transformChrom.ToString();
                foreach (var peptideGroup in SkylineWindow.Document.MoleculeGroups)
                {
                    foreach (var peptide in peptideGroup.Peptides) // Avoid any small molecule test nodes
                    {
                        TestPeptide(peptideGroup, peptide);
                    }
                }
            }

            TestStaleSelection();
        }

        /// <summary>
        /// Regression test for exception report skyline.ms #75292: a NullReferenceException in
        /// GraphChromatogram.GetColorIndex while displaying peptide totals. While a document change
        /// is being dispatched, the chromatogram graph can update with its cached transition-group
        /// paths still referencing a peptide that is no longer in the current document, so
        /// DocumentUI.FindNode(path.Parent) returns null. The graph must tolerate the transient
        /// stale path rather than crash.
        /// </summary>
        private void TestStaleSelection()
        {
            var doc = SkylineWindow.Document;
            var peptideGroup = doc.MoleculeGroups.First(g => g.Peptides.Any());
            var peptide = peptideGroup.Peptides.First();

            // Show the peptide totals chromatogram - the DisplayTotals path that resolves a color
            // per transition group via DocumentUI.FindNode.
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = new IdentityPath(peptideGroup.Id, peptide.Id);
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.total);
            });
            WaitForGraphs();

            var graphChrom = FindOpenForm<GraphChromatogram>();
            Assert.IsNotNull(graphChrom);

            // The race between the document-change dispatch and the tree selection cannot be
            // triggered deterministically through the public UI, so seed the equivalent stale state
            // directly: leave _nodeGroups untouched (so the UpdateGroups reference-equality
            // short-circuit keeps the graph's cached chromatograms and preserves the paths we seed
            // below) but point the cached paths at a peptide that is absent from the document,
            // exactly as FindNode would fail to resolve after the peptide was removed.
            RunUI(() =>
            {
                var nodeGroupsField = typeof(GraphChromatogram).GetField(@"_nodeGroups",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var groupPathsField = typeof(GraphChromatogram).GetField(@"_groupPaths",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(nodeGroupsField);
                Assert.IsNotNull(groupPathsField);

                var nodeGroups = (TransitionGroupDocNode[]) nodeGroupsField.GetValue(graphChrom);
                Assert.IsNotNull(nodeGroups);
                Assert.IsTrue(nodeGroups.Length > 0);

                var stalePaths = nodeGroups
                    .Select(g => new IdentityPath(peptideGroup.Id, new Peptide(@"ELVISLIVES"), g.Id))
                    .ToArray();
                groupPathsField.SetValue(graphChrom, stalePaths);

                // Before the fix this threw NullReferenceException in GetColorIndex.
                graphChrom.UpdateUI();
            });
            WaitForGraphs();

            // The graph survived the stale selection without throwing. Every group shared the
            // absent peptide, so all were skipped - a deterministic 0 curves. This also guards
            // against a future false-green: if the seeded paths were ever discarded (e.g. the
            // short-circuit stopped preserving them), the curves would reappear and this would fail.
            var graphControl = (MSGraphControl) graphChrom.Controls.Find(@"graphControl", true).First();
            Assert.AreEqual(0, GetChromatogramCount(graphControl.GraphPane));
        }

        /// <summary>
        /// Selects a peptide and then changes chromatogram "Transform" and "Transition" settings.
        /// </summary>
        private void TestPeptide(PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
        {
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(peptideGroup.Id, peptide.Id));
            Settings.Default.SplitChromatogramGraph = false;
            RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.all));
            WaitForGraphs();

            var graphChrom = FindOpenForm<GraphChromatogram>();
            MSGraphControl graphControl = (MSGraphControl)graphChrom.Controls.Find("graphControl", true).First();
            if (peptide.TransitionGroupCount <= 1)
            {
                // When a peptide with one transition group is seleted, then all transitions should be displayed
                Assert.AreEqual(peptide.TransitionCount, GetChromatogramCount(graphControl.GraphPane));
            }
            else
            {
                // If the peptide has more than one transition group, then only transition groups are displayed
                // when the peptide is selected.
                Assert.AreEqual(peptide.TransitionGroupCount, GetChromatogramCount(graphControl.GraphPane));
            }

            // When Display Type is set to "total", there is one curve per transition group.
            RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.total));
            WaitForGraphs();
            Assert.AreEqual(peptide.TransitionGroupCount, GetChromatogramCount(graphControl.GraphPane));

            foreach (var transitionGroup in peptide.TransitionGroups)
            {
                string message = peptide.ModifiedTarget.ToString() + ' ' + transitionGroup.PrecursorMz + ' ' + Settings.Default.TransformTypeChromatogram;
                var transitionGroupPath = new IdentityPath(peptideGroup.Id, peptide.Id, transitionGroup.Id);
                RunUI(() => SkylineWindow.SelectedPath = transitionGroupPath);

                // Verify that "Split Graph" produces the correct number of panes.
                Settings.Default.SplitChromatogramGraph = true;
                RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.all));
                WaitForGraphs();
                if (transitionGroup.GetMsTransitions(true).Any() && transitionGroup.GetMsMsTransitions(true).Any())
                {
                    Assert.AreEqual(2, graphControl.MasterPane.PaneList.Count, message);
                }
                else
                {
                    Assert.AreEqual(1, graphControl.MasterPane.PaneList.Count, message);
                }

                int countTrans = 0;
                foreach (var transition in transitionGroup.Transitions)
                {
                    if (++countTrans >= 5)  // Selecting absolutely everything makes this a pretty long test (this cuts time in half)
                        break;

                    var transitionPath = new IdentityPath(transitionGroupPath, transition.Id);
                    RunUI(() => SkylineWindow.SelectedPath = transitionPath);
                    RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.single));
                    WaitForGraphs();
                    Assert.AreEqual(1, graphControl.MasterPane.PaneList.Count);
                    if (1 != GetChromatogramCount(graphControl.GraphPane))
                    {
                        Assert.AreEqual(1, GetChromatogramCount(graphControl.GraphPane));
                    }
                    RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.total));
                }
            }
        }

        private int GetChromatogramCount(MSGraphPane msGraphPane)
        {
            int count = 0;
            RunUI(() => count = GetChromGraphItems(msGraphPane).Count());
            return count;
        }

        private IEnumerable<ChromGraphItem> GetChromGraphItems(MSGraphPane msGraphPane)
        {
            return msGraphPane.CurveList
                .Where(curve => !string.IsNullOrEmpty(curve.Label.Text))
                .Select(curve => curve.Tag)
                .OfType<ChromGraphItem>();
        }
    }
}
