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
using pwiz.Common.Graph;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
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

            RunDlg<MassErrorChartPropertyDlg>(SkylineWindow.ShowMassErrorPropertyDlg, dlg => dlg.OkDialog());

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

            // Verify Copy Data output for 2D histogram produces clean 3-column format
            RunUI(() =>
            {
                var graphData = GraphData.GetGraphData(SkylineWindow.GraphMassError.GraphControl.MasterPane);
                AssertEx.AreEqual(1, graphData.Panes.Count, "Expected 1 pane in heatmap");
                AssertEx.AreEqual(1, graphData.Panes[0].DataFrames.Count, "Expected 1 DataFrame in heatmap");
                var dataFrame = graphData.Panes[0].DataFrames[0];
                AssertEx.AreEqual(3, dataFrame.ColumnCount, "Heatmap Copy Data should have 3 columns");
                AssertEx.AreEqual(458, dataFrame.RowCount, "Row count should match heatmap point count");

                // Verify column headers match expected axis labels
                var headers = dataFrame.GetColumnHeaders();
                AssertEx.AreEqual(GraphsResources.MassErrorHistogram2DGraphPane_Graph_Retention_Time, headers[0, 0]?.ToString());
                AssertEx.AreEqual(GraphsResources.MassErrorReplicateGraphPane_UpdateGraph_Mass_Error, headers[0, 1]?.ToString());
                AssertEx.AreEqual(GraphsResources.MassErrorHistogramGraphPane_UpdateGraph_Count, headers[0, 2]?.ToString());

                // Spot-check that mass error values span a reasonable range
                double minY = double.MaxValue, maxY = double.MinValue;
                foreach (var i in new[] { 0, dataFrame.RowCount / 2, dataFrame.RowCount - 1 })
                {
                    var row = dataFrame.GetRow(i);
                    AssertEx.AreEqual(3, row.Length);
                    var yVal = (double)row[1];
                    minY = System.Math.Min(minY, yVal);
                    maxY = System.Math.Max(maxY, yVal);
                    AssertEx.IsTrue((double)row[2] >= 1, "Count should be at least 1");
                }
                AssertEx.IsTrue(maxY - minY > 1, "Mass error values should span a reasonable range");
                var row31 = dataFrame.GetRow(31);
                AssertEx.AreEqual(20.8418, (double)row31[0], 0.0001);
                AssertEx.AreEqual(-11.2000, (double)row31[1], 0.0001);
                AssertEx.AreEqual(2, (double)row31[2]);
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

            // Test degenerate case: single precursor in single replicate (issue #3909).
            // All product ions share the same retention time, so _maxX == _minX.
            // Before the fix, this caused division by zero in the binning calculation.
            RunUI(() =>
            {
                SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.targets);
                SkylineWindow.UpdateXAxis(Histogram2DXAxis.retention_time);
                SkylineWindow.ChangeMassErrorDisplayType(DisplayTypeMassError.products);
            });
            // Strip document to a single precursor - only iRT peptide list
            while (SkylineWindow.Document.MoleculeGroupCount > 1)
            {
                RunUI(() =>
                {
                    SkylineWindow.SequenceTree.SelectedPath =
                        SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.MoleculeGroups,
                            SkylineWindow.Document.MoleculeGroupCount - 1);
                    SkylineWindow.EditDelete();
                });
            }
            // Only first peptide in iRTs
            while (SkylineWindow.Document.MoleculeCount > 1)
            {
                RunUI(() =>
                {
                    SkylineWindow.SequenceTree.SelectedPath =
                        SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules,
                            SkylineWindow.Document.MoleculeCount - 1);
                    SkylineWindow.EditDelete();
                });
            }
            WaitForGraphs();
            RunUI(() =>
            {
                var pane = GetHistogram2DGraphPane();
                Assert.IsTrue(pane.GetPoints() > 0);
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
