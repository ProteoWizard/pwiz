/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using ZedGraph;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for SplitGraphTest
    /// </summary>
    [TestClass]
    public class SplitGraphTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSplitGraph()
        {
            TestFilesZip = @"TestFunctional\SplitGraphTest.zip";
            RunFunctionalTest();
        }


        protected override void DoTest()
        {
            // IsPauseForScreenShots = true;
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SplitGraphUnitTest.sky")));
            CollectionAssert.AreEqual(new[]{"SplitGraph_rev1.clib"}, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs
                .Select(spec => Path.GetFileName(spec.FilePath)).ToArray());
            WaitForDocumentLoaded();
            // Test that AutoZoomNone and AutoZoomBestPeak work
            var graphChromatogram = FormUtil.OpenForms.OfType<GraphChromatogram>().First();
            var graphChromatogramGraphControl = AllControls(graphChromatogram).OfType<ZedGraphControl>().First();
            RunUI(() =>
                {
                    // Select the first transition group
                    SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(2, 0);
                    SkylineWindow.AutoZoomBestPeak();
                    // Make sure that we are zoomed in to approximately the best peak
                    Assert.AreEqual(graphChromatogramGraphControl.GraphPane.XAxis.Scale.Min, 13.0, 1.0);
                    Assert.AreEqual(graphChromatogramGraphControl.GraphPane.XAxis.Scale.Max, 14.0, 1.0);
                    // Remember the zoom state so that we can pretend to manually zoom later
                    var zoomStateAuto = new ZoomState(graphChromatogramGraphControl.GraphPane, ZoomState.StateType.Zoom);
                    SkylineWindow.AutoZoomNone();
                    Assert.AreEqual(graphChromatogramGraphControl.GraphPane.XAxis.Scale.Min, 0.0, 1.0);
                    Assert.AreEqual(graphChromatogramGraphControl.GraphPane.XAxis.Scale.Max, 35.0, 1.0);
                    // Pretend to manually zoom
                    zoomStateAuto.ApplyState(graphChromatogramGraphControl.GraphPane);
                    Assert.AreEqual(graphChromatogramGraphControl.GraphPane.XAxis.Scale.Min, 13.0, 1.0);
                    // Select some other transition group: 
                    SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(2, 1);
                    SkylineWindow.ShowPeakAreaReplicateComparison();
                });
            WaitForGraphs();
            // Ensure that we zoomed out when the selected transition group changed
            Assert.AreEqual(graphChromatogramGraphControl.GraphPane.XAxis.Scale.Min, 0.0, 1.0);

            var peakAreaSummary = FormUtil.OpenForms.OfType<GraphSummary>().First();
            var graphLibraryMatch = FormUtil.OpenForms.OfType<GraphSpectrum>().First();
            var libraryMatchGraphControl = AllControls(graphLibraryMatch).OfType<ZedGraphControl>().First();
            RunUI(() =>
            {
                Assert.IsTrue(Settings.Default.ShowLibraryChromatograms);
                AssertCurveListsSame(graphChromatogram.CurveList,
                    libraryMatchGraphControl.GraphPane.CurveList);
                AssertCurveListsSame(graphChromatogram.CurveList,
                    peakAreaSummary.GraphControl.GraphPane.CurveList);
                Assert.AreEqual(9, graphChromatogram.CurveList.Count);
                Assert.AreEqual(1, graphChromatogramGraphControl.MasterPane.PaneList.Count);
                Assert.AreEqual(1, peakAreaSummary.GraphControl.MasterPane.PaneList.Count);
                SkylineWindow.ShowPrecursorTransitions();
                Assert.AreEqual(3, graphChromatogram.CurveList.Count);
                // TODO(nicksh): Enable this when libraries filter based on precursor/product
                //AssertCurveListsSame(graphChromatogram.CurveList, libraryMatchGraphControl.GraphPane.CurveList);
                AssertCurveListsSame(graphChromatogram.CurveList,
                    peakAreaSummary.GraphControl.GraphPane.CurveList.FindAll(c => c.Tag is IdentityPath &&
                        !c.IsY2Axis).ToList()); //exclude the dotp graph from the count.
                SkylineWindow.ShowProductTransitions();
            });
            WaitForGraphs();
            RunUI(() =>{
                Assert.AreEqual(6, graphChromatogram.CurveList.Count);
                // TODO(nicksh): Enable this when libraries filter based on precursor/product
                AssertCurveListsSame(graphChromatogram.CurveList,
                    libraryMatchGraphControl.GraphPane.CurveList);
                AssertCurveListsSame(graphChromatogram.CurveList,
                    peakAreaSummary.GraphControl.GraphPane.CurveList.FindAll(c => c.Tag is IdentityPath && !c.IsY2Axis).ToList());
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.ShowSplitChromatogramGraph(true);
            });
            WaitForGraphs();
            Assert.AreEqual(2, graphChromatogramGraphControl.MasterPane.PaneList.Count);
            Assert.AreEqual(2, peakAreaSummary.GraphControl.MasterPane.PaneList.Count);
            AssertCurveListsSame(graphChromatogram.GetCurveList(graphChromatogramGraphControl.MasterPane.PaneList[0]),
                peakAreaSummary.GraphControl.MasterPane.PaneList[0].CurveList.FindAll(c => c.Tag is IdentityPath && !c.IsY2Axis).ToList());
            AssertCurveListsSame(graphChromatogram.GetCurveList(graphChromatogramGraphControl.MasterPane.PaneList[1]),
                peakAreaSummary.GraphControl.MasterPane.PaneList[1].CurveList.FindAll(c => c.Tag is IdentityPath && !c.IsY2Axis).ToList());
        }

        private static void AssertCurveListsSame(List<CurveItem> curveList1, List<CurveItem> curveList2)
        {
            CollectionAssert.AreEqual(curveList1.Select(curve=>curve.Label.Text).ToArray(), curveList2.Select(curve=>curve.Label.Text).ToArray());
            CollectionAssert.AreEqual(curveList1.Select(curve=>curve.Color).ToArray(), curveList2.Select(curve=>curve.Color).ToArray());
        }

        private static IEnumerable<Control> AllControls(Control control)
        {
            return new[] {control}.Concat(control.Controls.Cast<Control>().SelectMany(AllControls));
        }
    }
}
