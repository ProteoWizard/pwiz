/*
 * Original author: Alex MacLean <alex.maclean2000 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MassErrorGraphsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestMassErrorGraphs()
        {
            Run(@"TestFunctional\MassErrorGraphsTest.zip");
        }

        protected override void DoTest()
        {
            OpenDocument("BrukerDIATest.sky");

            #region Repicate
            //just test the mass error graph with some normal data
            SelectNode(SrmDocument.Level.Molecules, 0);
            RunUI(SkylineWindow.ShowMassErrorReplicateComparison);
            WaitForGraphs();
            RunUI(() =>
            {           
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(3, pane.GetTransitionCount());
                Assert.AreEqual(9, pane.GetTotalBars());
                Assert.AreEqual(3.6 , pane.GetMin(),0.1);
                Assert.AreEqual(6.8 , pane.GetMax(),0.1);
            });

            //test the mass error graph with negative data
            SelectNode(SrmDocument.Level.Molecules, 10);
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(9, pane.GetTransitionCount());
                Assert.AreEqual(27, pane.GetTotalBars());
                Assert.AreEqual(-22 , pane.GetMin(),0.1);
                Assert.AreEqual(23.3 , pane.GetMax(),0.1);
            });

            //switch to precursors and make sure graph changes
            RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.precursors));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(3, pane.GetTransitionCount());
            });

            //switch to products and make sure graph changes
            RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.products));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(6, pane.GetTransitionCount());
            });

            //select a protein to test unavailible mass errors
            SelectNode(SrmDocument.Level.MoleculeGroups, 0);
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(0, pane.GetTransitionCount());
                Assert.AreEqual(pane.Title.Text, Resources.MassErrorReplicateGraphPane_UpdateGraph_Select_a_peptide_to_see_the_mass_error_graph);
            });

            //select a peptide with 2 charge states
            SelectNode(SrmDocument.Level.Molecules, 44);
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(2, pane.GetTransitionCount());
                Assert.AreEqual(6, pane.GetTotalBars());
            });

            //select an ion
            SelectNode(SrmDocument.Level.Transitions, 0);
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(3, pane.GetTransitionCount());
                Assert.AreEqual(9, pane.GetTotalBars());
            });

            // test switching to singel transition
            RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.single));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.AreEqual(1, pane.GetTransitionCount());
            });

            // test diabeling the legend
            RunUI(() => SkylineWindow.ShowMassErrorLegend(false));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetReplicateGraphPane();
                Assert.IsFalse(pane.Legend.IsVisible);
            });
            #endregion

            #region Peptide
            //switch to peptide comparison
            RunUI(SkylineWindow.ShowMassErrorPeptideGraph);
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetPeptideGraphPane();
                Assert.AreEqual(1575, pane.GetTotalBars());
                Assert.AreEqual(21.86, pane.GetMax(), 0.1);
                Assert.AreEqual(-21.17,pane.GetMin(),0.1);
            });

            //change scope to protein
            RunUI(() => SkylineWindow.AreaScopeTo(AreaScope.protein));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetPeptideGraphPane();
                Assert.AreEqual(90, pane.GetTotalBars());
            });

            // test switching to singel transition
            RunUI(() => SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.total));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetPeptideGraphPane();
                Assert.AreEqual(1, pane.GetTransitionCount());
            });

            // test order by mass error
            RunUI(() => SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.mass_error));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetPeptideGraphPane();
                Assert.AreEqual(pane.GetMin(), pane.CurveList[0][0].Y);
                Assert.AreEqual(pane.GetMax(), pane.CurveList[0][9].Y);
            });

            // test switching replicate
            RunUI(() => SkylineWindow.ShowSingleReplicate());
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetPeptideGraphPane();
                Assert.AreEqual(pane.GetMin(), 3.7, 0.1);
                Assert.AreEqual(pane.GetMax(), 14.5, 0.1);
            });
            #endregion

            #region Histogram
            //switch to histogram
            RunUI(SkylineWindow.ShowMassErrorHistogramGraph);
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogramGraphPane();
                Assert.AreEqual(46, pane.GetTotalBars());
                Assert.AreEqual(7, pane.GetMax());
                Assert.AreEqual(1, pane.GetMin());
            });

            //switch from singel to all
            RunUI(() => SkylineWindow.ShowAverageReplicates());
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogramGraphPane();
                Assert.AreEqual(66, pane.GetTotalBars());
                Assert.AreEqual(pane.GetMin(),1);
                Assert.AreEqual(pane.GetMax(), 15);
            });

            //switch from targets to decoys
            RunUI(() => SkylineWindow. ShowPointsTypeMassError(PointsTypeMassError.decoys));
            WaitForGraphs(); 
            RunUI(() =>
            {
                var pane = GetHistogramGraphPane();
                Assert.AreEqual(83, pane.GetTotalBars());
                Assert.AreEqual(pane.GetMin(), 1);
                Assert.AreEqual(pane.GetMax(), 10);
            });

            //alter the bin size from 0.5 to 2.0
            RunUI(() => SkylineWindow.UpdateBinSize(2));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogramGraphPane();
                Assert.AreEqual(24, pane.GetTotalBars());
                Assert.AreEqual(pane.GetMin(), 1);
                Assert.AreEqual(pane.GetMax(), 23);
            });

            // switch display type
            RunUI(() => SkylineWindow.ChangeMassErrorDisplayType(DisplayTypeMassError.precursors));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogramGraphPane();
                Assert.AreEqual(23, pane.GetTotalBars());
                Assert.AreEqual(pane.GetMin(), 2);
                Assert.AreEqual(pane.GetMax(), 20);
            });

            // switch transition
            RunUI(() => SkylineWindow.ChangeMassErrorTransition(TransitionMassError.all));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogramGraphPane();
                Assert.AreEqual(26, pane.GetTotalBars());
                Assert.AreEqual(pane.GetMin(), 1);
                Assert.AreEqual(pane.GetMax(), 55);
            });
            #endregion
    
            #region Histogram 2D
            //switch to histogram 2D
            RunUI(SkylineWindow.ShowMassErrorHistogramGraph2D);
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogram2DGraphPane();
                Assert.AreEqual(458, pane.GetPoints());
                Assert.AreEqual(24.8, pane.GetMax(), 0.1);
                Assert.AreEqual(-23.2, pane.GetMin(), 0.1);
            });

            //alter the bin size from 2.0 to 0.5
            RunUI(() => SkylineWindow.UpdateBinSize(0.5));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogram2DGraphPane();
                Assert.AreEqual(570, pane.GetPoints());
            });
            
            //switch from singel to all
            RunUI(() => SkylineWindow.ShowSingleReplicate());
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogram2DGraphPane();
                Assert.AreEqual(212, pane.GetPoints());
            });

            //switch from targets to decoys
            RunUI(() => SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.targets_1FDR));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogram2DGraphPane();
                Assert.AreEqual(144, pane.GetPoints());
            });

            //switch to log scale
            RunUI(() => SkylineWindow.SwitchLogScale());
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogram2DGraphPane();
                Assert.AreEqual(144, pane.GetPoints());
            });
          
            //change x-axis display
            RunUI(() => SkylineWindow.UpdateXAxis(Histogram2DXAxis.mass_to_charge));
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogram2DGraphPane();
                Assert.AreEqual(129, pane.GetPoints());
                Assert.AreEqual(12.45, pane.GetMax(), 0.1);
                Assert.AreEqual(-0.05, pane.GetMin(), 0.1);
            });
            #endregion
        }

        private MassErrorReplicateGraphPane GetReplicateGraphPane()
        {
            MassErrorReplicateGraphPane pane;
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out pane));
            return pane;
        }
        private MassErrorPeptideGraphPane GetPeptideGraphPane()
        {
            MassErrorPeptideGraphPane pane;
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out pane));
            return pane;
        }
        private MassErrorHistogramGraphPane GetHistogramGraphPane()
        {
            MassErrorHistogramGraphPane pane;
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out pane));
            return pane;
        }
        private MassErrorHistogram2DGraphPane GetHistogram2DGraphPane()
        {
            MassErrorHistogram2DGraphPane pane;
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out pane));
            return pane;
        }
    }
}