/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that the "Detection - Histogram" and "Detection - Replicate Comparison" windows
    /// display errors correctly.
    /// </summary>
    [TestClass]
    public class DetectionsPlotErrorTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestDetectionsPlotError()
        {
            TestFilesZip = @"TestFunctional/DetectionsPlotTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"DIA-TTOF-tutorial.sky"));
            WaitForDocumentLoaded();
            // Delete all the peptides from the document so that the detections plots will show errors
            RunUI(()=>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.EditDelete();
            });
            // Verify that the Detections Histogram Graph displays 
            RunUI(()=>
            {
                SkylineWindow.ShowDetectionsHistogramGraph();
            });
            WaitForGraphs();
            var graphHistogram = SkylineWindow.DetectionsPlot;
            Assert.IsTrue(graphHistogram.TryGetGraphPane(out DetectionsHistogramPane paneHistogram));
            RunUI(() =>
            {
                Assert.AreEqual(GraphsResources.DetectionPlotPane_EmptyPlotError_Label, paneHistogram.Title.Text);
                var subTitle = paneHistogram.GraphObjList.FirstOrDefault() as TextObj;
                Assert.IsNotNull(subTitle, "Unable to find SubTitle text object");
                Assert.AreEqual(GraphsResources.DetectionPlotData_NoResults_Label, subTitle.Text);
            });
            OkDialog(graphHistogram, graphHistogram.Close);

            RunUI(()=>SkylineWindow.ShowDetectionsReplicateComparisonGraph());
            WaitForGraphs();
            GraphSummary graphReplicateComparison = SkylineWindow.DetectionsPlot;
            Assert.IsTrue(graphReplicateComparison.TryGetGraphPane(out DetectionsByReplicatePane detectionsByReplicatePane));
            RunUI(() =>
            {
                Assert.AreEqual(GraphsResources.DetectionPlotPane_EmptyPlotError_Label, detectionsByReplicatePane.Title.Text);
                var subTitle = detectionsByReplicatePane.GraphObjList.FirstOrDefault() as TextObj;
                Assert.IsNotNull(subTitle, "Unable to find SubTitle text object");
                Assert.AreEqual(GraphsResources.DetectionPlotData_NoResults_Label, subTitle.Text);
            });
        }
    }
}
