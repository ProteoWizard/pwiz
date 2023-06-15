/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakAreaPeptideGraphTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakAreaPeptideGraph()
        {
            TestFilesZip = @"TestFunctional\PeakAreaPeptideGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PeakAreaPeptideGraphTest.sky")));
            WaitForDocumentLoaded();
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowPeakAreaPeptideGraph();
                SkylineWindow.ShowSplitChromatogramGraph(false);
            });
            var peakAreaGraph = FormUtil.OpenForms.OfType<GraphSummary>().FirstOrDefault(graph =>
                graph.Type == GraphTypeSummary.peptide && graph.Controller is AreaGraphController);
            Assert.IsNotNull(peakAreaGraph);
            WaitForGraphs();
            RunUI(() =>
            {
                // When Split Graph is off, there should be only one pane
                Assert.AreEqual(1, peakAreaGraph.GraphControl.MasterPane.PaneList.Count);
                SkylineWindow.ShowSplitChromatogramGraph(true);

                SkylineWindow.AreaScopeTo(AreaScope.document);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                // When the scope is document, there should be two panes since the document has two
                // label types in in total
                Assert.AreEqual(2, peakAreaGraph.GraphControl.MasterPane.PaneList.Count);
                SkylineWindow.AreaScopeTo(AreaScope.protein);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                // When the scope is protein, there should be only one pane because the protein has
                // only one label type
                Assert.AreEqual(1, peakAreaGraph.GraphControl.MasterPane.PaneList.Count);
            });
        }
    }
}
