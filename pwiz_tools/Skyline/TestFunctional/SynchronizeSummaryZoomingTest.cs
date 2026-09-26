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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SynchronizeSummaryZoomingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSynchronizeSummaryZooming()
        {
            TestFilesZip = "TestFunctional/SynchronizeSummaryZoomingTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"Ms1FilterTutorial.sky")));
            WaitForDocumentLoaded();

            Settings.Default.SynchronizeSummaryZooming = false;

            RunUI(() =>
            {
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowMassErrorReplicateComparison();
            });

            WaitForGraphs();

            // In binary floating point the first tic on this range is 0.05 * 174 = 8.700000000000001
            RunUI(() => AssertTicValuesRoundTrip(SkylineWindow.GraphRetentionTime.GraphControl.GraphPane, 8.7, 9.05));

            GraphSummary[] summaries ={ SkylineWindow.GraphPeakArea, SkylineWindow.GraphRetentionTime, SkylineWindow.GraphMassError };
            Settings.Default.SynchronizeSummaryZooming = true;

            // Test if graphs sync correctly with ShowLibraryPeakArea disabled/enabled
            Settings.Default.ShowLibraryPeakArea = false;
            TestGraphSummary(summaries[0], new [] { summaries[1], summaries[2] }, true);

            Settings.Default.ShowLibraryPeakArea = true;
            TestGraphSummary(summaries[0], new[] { summaries[1], summaries[2] }, true);
            TestGraphSummary(summaries[1], new[] { summaries[0], summaries[2] }, true);

            // Test that graphs don't sync when the graph types are different
            RunUI(() =>
            {
                SkylineWindow.ShowGraphRetentionTime(true, GraphTypeSummary.peptide);
            });
            
            WaitForGraphs();
            summaries = new[] { SkylineWindow.GraphPeakArea, SkylineWindow.GraphRetentionTime, SkylineWindow.GraphMassError };
            TestGraphSummary(summaries[1], new[] {summaries[0], summaries[2]}, false);

            //Test that graphs don't sync when they are hidden
            RunUI(() => { summaries[0].Hide(); summaries[1].Hide(); });
            WaitForGraphs();
            TestGraphSummary(summaries[2], new[] { summaries[0], summaries[1] }, false);
        }

        private void TestGraphSummary(GraphSummary active, GraphSummary[] others, bool shouldSync)
        {
            double min = 0.0;
            double max = SkylineWindow.GraphChromatograms.Count();

            double otherMin = -1.0;
            double otherMax = 1.0;
            
            RunUI(() =>
            {
                active.GraphControl.GraphPane.XAxis.Scale.Min = min;
                active.GraphControl.GraphPane.XAxis.Scale.Max = max;

                foreach (var graphSummary in others)
                {
                    graphSummary.GraphControl.GraphPane.XAxis.Scale.Min = otherMin;
                    graphSummary.GraphControl.GraphPane.XAxis.Scale.Max = otherMax;
                }

                active.Activate();
                SkylineWindow.SynchronizeSummaryZooming(active);
            });

            WaitForGraphs();

            RunUI(() =>
            {
                double add = GetExpectedVisible(active) ? -1.0 : 0.0;

                foreach (var graphSummary in others)
                {
                    bool expectedVisible = GetExpectedVisible(graphSummary);
                    add += expectedVisible ? 1.0 : 0.0;
                    Assert.AreEqual(shouldSync ? min + add : otherMin, graphSummary.GraphControl.GraphPane.XAxis.Scale.Min);
                    Assert.AreEqual(shouldSync ? max + add : otherMax, graphSummary.GraphControl.GraphPane.XAxis.Scale.Max);
                    add += expectedVisible ? -1.0 : 0.0;
                }
            });
        }

        /// <summary>
        /// Zooms the Y axis to the given range and asserts that every tic value is the double nearest
        /// its 15-digit decimal value. Tic values with binary floating point noise, such as
        /// 8.850000000000001, get shown in full by .NET Core's shortest round-trip formatting.
        /// </summary>
        private static void AssertTicValuesRoundTrip(GraphPane graphPane, double min, double max)
        {
            var ticValues = new List<double>();
            Axis.ScaleFormatHandler recordTicValue = (pane, axis, val, index) =>
            {
                ticValues.Add(val);
                return null;
            };
            var scale = graphPane.YAxis.Scale;
            scale.Min = min;
            scale.Max = max;
            graphPane.YAxis.ScaleFormatEvent += recordTicValue;
            try
            {
                graphPane.AxisChange();
                // Drawing the pane formats every tic label
                graphPane.GetImage().Dispose();
            }
            finally
            {
                graphPane.YAxis.ScaleFormatEvent -= recordTicValue;
                scale.MinAuto = scale.MaxAuto = true;
                graphPane.AxisChange();
            }
            Assert.AreNotEqual(0, ticValues.Count);
            foreach (var ticValue in ticValues)
            {
                var text = ticValue.ToString(@"G15", CultureInfo.InvariantCulture);
                Assert.AreEqual(double.Parse(text, CultureInfo.InvariantCulture), ticValue,
                    string.Format(@"Tic value {0} has floating point noise", ticValue.ToString(@"R", CultureInfo.InvariantCulture)));
            }
        }

        private bool GetExpectedVisible(GraphSummary g)
        {
            var pane = g.GraphControl.GraphPane as AreaReplicateGraphPane;
            return pane != null && pane.ExpectedVisible.IsVisible();
        }
    }
}