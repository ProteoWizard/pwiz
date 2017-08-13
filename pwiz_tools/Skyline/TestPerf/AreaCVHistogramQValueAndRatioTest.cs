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
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class AreaCVHistogramQValueAndRatioTest : AbstractFunctionalTestEx
    {
        private static readonly AreaCVGraphDataStatistics[] STATS =
        {
            new AreaCVGraphDataStatistics(117, 117, 0, 0, 12291, 1.56, 0, 806, 0.095164067507682137, 0.13255471483199088, 0.80701326173622978),
            new AreaCVGraphDataStatistics(172, 172, 0, 0, 18608, 1.71, 0, 268, 0.50598026540976015, 0.53897463456577821, 0.13392089423903697),
            new AreaCVGraphDataStatistics(124, 124, 0, 0, 13187, 1.56, 0, 818, 0.10081787157698326, 0.14358231591719123, 0.78357473269128686),
            new AreaCVGraphDataStatistics(115, 115, 0, 0, 11312, 1.56, 0, 758, 0.095865673556778247, 0.13204738330975957, 0.81108557284299854),
            new AreaCVGraphDataStatistics(2269, 1812, 3.85, 8.5, 12291, 1.56, 0, 42, 0.095164067507682137, 0.13255471483199055, 0.80701326173622978),
            new AreaCVGraphDataStatistics(5847, 4352, 3.1, 7.45, 18608, 1.71, 0, 17, 0.50598026540976015, 0.53897463456577754, 0.13392089423903697),
            new AreaCVGraphDataStatistics(2494, 2001, 3.6500000000000004, 8.5, 13187, 1.56, 0, 42, 0.10081787157698326, 0.14358231591719073, 0.78357473269128686),
            new AreaCVGraphDataStatistics(2129, 1691, 3.9000000000000004, 8.5, 11312, 1.56, 0, 40, 0.095865673556778247, 0.13204738330975938, 0.81108557284299854),
            new AreaCVGraphDataStatistics(50, 51, 0, 0, 247, 3.63, 0.28, 22, 2.2389780149590934, 2.2399595141700406, 0),
            new AreaCVGraphDataStatistics(50, 51, 0, 0, 246, 2.1, 0.06, 24, 0.15889171184485773, 0.27004065040650405, 0.64634146341463417),
            new AreaCVGraphDataStatistics(97, 99, 0, 0, 250, 3.63, 0.06, 12, 1.24900414453809, 1.26328, 0.32),
            new AreaCVGraphDataStatistics(201, 154, 3.7, 7, 247, 3.63, 0.28, 4, 2.2389780149590934, 2.239959514170041, 0),
            new AreaCVGraphDataStatistics(203, 183, 3.7, 7, 246, 2.1, 0.06, 4, 0.15889171184485773, 0.2700406504065041, 0.64634146341463417),
            new AreaCVGraphDataStatistics(233, 191, 3.7, 7, 250, 3.63, 0.06, 3, 1.24900414453809, 1.2632800000000002, 0.32),
        };

        private bool RecordData { get { return false; } }

        [TestMethod]
        public void TestAreaCVHistogramQValuesAndRatios()
        {
            RunPerfTests = true;
            TestFilesPersistent = new[] { "." };  // All persistent. No saving
            TestFilesZip = @"http://proteome.gs.washington.edu/software/test/skyline-perf/AreaCVHistogramQValueAndRatioTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Settings.Default.AreaCVShowCVCutoff = Settings.Default.AreaCVShowMedianCV = false;
            AreaGraphController.GroupByAnnotation = AreaGraphController.GroupByGroup = null;

            OpenDocument(TestFilesDir.GetTestPath(@"BrukerDIA3_0.sky"));

            TestHistogramQValues<AreaCVHistogramGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram, 0);
            TestHistogramQValues<AreaCVHistogram2DGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram2D, 4);

            OpenDocument(TestFilesDir.GetTestPath(@"Site54_Study9-1_standardcurves_083011_v1.sky"));

            TestHistogramRatios<AreaCVHistogramGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram, 8);
            TestHistogramRatios<AreaCVHistogram2DGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram2D, 11);

            Assert.IsFalse(RecordData, "Successfully recorded data");
        }

        private void TestHistogramQValues<T>(Action showHistogram, int statsStartIndex) where T : SummaryGraphPane
        {
            RunUI(() =>
            {
                showHistogram();
                SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets);
                SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.none);
            });

            WaitForGraphs();

            AreaGraphController.MinimumDetections = 2;
            OpenAndChangeProperties(p => p.QValueCutoff = 0.01);

            var graph = SkylineWindow.GraphPeakArea;
            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            // Test if the toolbar is there and if the displayed data is correct
            T pane;
            Assert.IsTrue(graph.TryGetGraphPane(out pane));
            Assert.IsTrue(pane.HasToolbar);
            AssertDataCorrect(pane, statsStartIndex++);

            RunUI(() =>
            {
                SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.decoys);
            });
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex); // No ++ here on purpose


            // Make sure the data is not affected by the qvalue cutoff if the points type is decoys
            OpenAndChangeProperties(p => p.QValueCutoff = 0.02);
            AssertDataCorrect(pane, statsStartIndex++, false);

            // Make sure that the data is correct after changing the qvalue cutoff, this time with points type targets
            RunUI(() =>
            {
                SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets);
            });
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++);

            AreaGraphController.MinimumDetections = 3;
            UpdateAndWait(graph);
            AssertDataCorrect(pane, statsStartIndex++);

            // Verify that a qvalue cutoff of 1.0 has the same effect as no qvalue cutoff
            OpenAndChangeProperties(p => p.QValueCutoff = 1.0);
            AreaCVGraphData qvalue1Data = null;
            WaitForConditionUI(() => (qvalue1Data = GetCurrentData(pane)) != null);
            AreaCVGraphDataStatistics qvalue1Statistics = null;
            RunUI(() => qvalue1Statistics = new AreaCVGraphDataStatistics(qvalue1Data, pane.GetTotalBars()));
            OpenAndChangeProperties(p => p.QValueCutoff = double.NaN);
            AreaCVGraphData qvalueNaNData = null;
            WaitForConditionUI(() => (qvalueNaNData = GetCurrentData(pane)) != null);
            AreaCVGraphDataStatistics qvalueNaNStatistics = null;
            RunUI(() => qvalueNaNStatistics = new AreaCVGraphDataStatistics(qvalueNaNData, pane.GetTotalBars()));

            Assert.AreEqual(qvalue1Statistics, qvalueNaNStatistics);
        }

        private void TestHistogramRatios<T>(Action showHistogram, int statsStartIndex) where T : SummaryGraphPane
        {
            RunUI(() =>
            {
                showHistogram();
                SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets);
                SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.ratio, 0);
            });

            WaitForGraphs();

            AreaGraphController.MinimumDetections = 2;
            OpenAndChangeProperties(p => p.QValueCutoff = double.NaN);

            var graph = SkylineWindow.GraphPeakArea;
            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            // Make sure toolbar is there, combo box items are correct and data is correct
            T pane;
            Assert.IsTrue(graph.TryGetGraphPane(out pane));
            Assert.IsTrue(pane.HasToolbar);
            CollectionAssert.AreEqual(new[] { "Light", "Heavy", "All 15N", "Medians", "None" }, toolbar.NormalizationMethods.ToArray());
            AssertDataCorrect(pane, statsStartIndex++); // Light

            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.ratio, 1));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++); // Heavy

            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.ratio, 2));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++); // All 15N
        }

        private static AreaCVGraphData GetCurrentData(SummaryGraphPane pane)
        {
            if (pane is AreaCVHistogramGraphPane)
                return ((AreaCVHistogramGraphPane)pane).CurrentData;
            else if (pane is AreaCVHistogram2DGraphPane)
                return ((AreaCVHistogram2DGraphPane)pane).CurrentData;
            else
                Assert.Fail("Graph pane is not a histogram/histogram2d graph pane");
            return null;
        }

        private class AreaCVGraphDataStatistics
        {
            public AreaCVGraphDataStatistics(int dataCount, int objects, double minMeanArea, double maxMeanArea, int total, double maxCv, double minCv, int maxFrequency, double medianCv, double meanCv, double belowCvCutoff)
            {
                DataCount = dataCount;
                Objects = objects;
                MinMeanArea = minMeanArea;
                MaxMeanArea = maxMeanArea;
                Total = total;
                MaxCV = maxCv;
                MinCV = minCv;
                MaxFrequency = maxFrequency;
                MedianCV = medianCv;
                MeanCV = meanCv;
                BelowCVCutoff = belowCvCutoff;
            }

            public AreaCVGraphDataStatistics(AreaCVGraphData data, int objects)
            {
                DataCount = data.Data.Count;
                Objects = objects;
                MinMeanArea = data.MinMeanArea;
                MaxMeanArea = data.MaxMeanArea;
                Total = data.Total;
                MaxCV = data.MaxCV;
                MinCV = data.MinCV;
                MaxFrequency = data.MaxFrequency;
                MedianCV = data.MedianCV;
                MeanCV = data.MeanCV;
                BelowCVCutoff = data.BelowCVCutoff;
            }

            protected bool Equals(AreaCVGraphDataStatistics other)
            {
                return DataCount == other.DataCount &&
                    Objects == other.Objects &&
                    MinMeanArea.Equals(other.MinMeanArea) &&
                    MaxMeanArea.Equals(other.MaxMeanArea) &&
                    Total == other.Total &&
                    MaxCV.Equals(other.MaxCV) &&
                    MinCV.Equals(other.MinCV) &&
                    MaxFrequency == other.MaxFrequency &&
                    MedianCV.Equals(other.MedianCV) &&
                    MeanCV.Equals(other.MeanCV) &&
                    BelowCVCutoff.Equals(other.BelowCVCutoff);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((AreaCVGraphDataStatistics) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = DataCount;
                    hashCode = (hashCode * 397) ^ Objects.GetHashCode();
                    hashCode = (hashCode * 397) ^ MinMeanArea.GetHashCode();
                    hashCode = (hashCode * 397) ^ MaxMeanArea.GetHashCode();
                    hashCode = (hashCode * 397) ^ Total;
                    hashCode = (hashCode * 397) ^ MaxCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ MinCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ MaxFrequency;
                    hashCode = (hashCode * 397) ^ MedianCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ MeanCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ BelowCVCutoff.GetHashCode();
                    return hashCode;
                }
            }

            public int DataCount { get; private set; }
            public int Objects { get; private set; }
            public double MinMeanArea { get; private set; } // Smallest mean area
            public double MaxMeanArea { get; private set; } // Largest mean area
            public int Total { get; private set; } // Total number of CV's
            public double MaxCV { get; private set; } // Highest CV
            public double MinCV { get; private set; } // Smallest CV
            public int MaxFrequency { get; private set; } // Highest count of CV's
            public double MedianCV { get; private set; } // Median CV
            public double MeanCV { get; private set; } // Mean CV
            public double BelowCVCutoff { get; private set; } // Fraction/Percentage of CV's below cutoff
        }

        private void OpenAndChangeProperties(Action<AreaCVToolbarProperties> action)
        {
            RunDlg<AreaCVToolbarProperties>(SkylineWindow.ShowAreaCVPropertyDlg, d =>
            {
                action(d);
                d.OK();
            });
            UpdateAndWait(SkylineWindow.GraphPeakArea);
        }

        private void UpdateAndWait(GraphSummary graph)
        {
            RunUI(() => { graph.UpdateUI(); });
            WaitForGraphs();
        }

        private void AssertDataCorrect(SummaryGraphPane pane, int statsIndex, bool record = true)
        {
            AreaCVGraphData data = null;
            WaitForConditionUI(() => (data = GetCurrentData(pane)) != null && data.IsValid);
            WaitForGraphs();

            RunUI(() =>
            {
                int objects = 0;
                if (pane is AreaCVHistogramGraphPane)
                    objects = pane.GetBoxObjCount();
                else if (pane is AreaCVHistogram2DGraphPane)
                    objects = pane.GetTotalBars();

                if (!RecordData)
                {
                    Assert.AreEqual(STATS[statsIndex], new AreaCVGraphDataStatistics(data, objects));
                    return;
                }

                if (record)
                {
                    Console.WriteLine(
                        @"new AreaCVGraphDataStatistics({0}, {1}, {2:R}, {3:R}, {4}, {5:R}, {6:R}, {7}, {8:R}, {9:R}, {10:R}),",
                        data.Data.Count, objects, data.MinMeanArea, data.MaxMeanArea, data.Total, data.MaxCV, data.MinCV, data.MaxFrequency, data.MedianCV, data.MeanCV, data.BelowCVCutoff);
                }
            });
        }
    }
}