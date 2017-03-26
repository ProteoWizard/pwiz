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
                string message = peptide.ModifiedSequence + ' ' + transitionGroup.PrecursorMz + ' ' + Settings.Default.TransformTypeChromatogram;
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
