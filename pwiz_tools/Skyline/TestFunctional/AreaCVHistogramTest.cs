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
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Controls.Graphs;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AreaCVHistogramTest : AbstractFunctionalTest
    {
        private struct AreaCVGraphDataStatistics
        {
            public int DataCount { get; set; }
            public double MinMeanArea { get; set; } // Smallest mean area
            public double MaxMeanArea { get; set; } // Largest mean area
            public int Total { get; set; } // Total number of CV's
            public double MaxCV { get; set; } // Highest CV
            public double MinCV { get; set; } // Smallest CV
            public int MaxFrequency { get; set; } // Highest count of CV's
            public double MedianCV { get; set; } // Median CV
            public double MeanCV { get; set; } // Mean CV
            public double belowCVCutoff { get; set; } // Fraction/Percentage of CV's below cutoff
        }

        private bool RecordData { get { return false; } }

        #region Histogram Data

        private static readonly AreaCVGraphDataStatistics STATS1 = new AreaCVGraphDataStatistics
        {
            DataCount     = 68,
            MinMeanArea   = 0,
            MaxMeanArea   = 0,
            Total         = 125,
            MaxCV         = 1.96,
            MinCV         = 0.13,
            MaxFrequency  = 7,
            MedianCV      = 0.48281967365197109,
            MeanCV        = 0.56232,
            belowCVCutoff = 0.024
        };


        private static readonly AreaCVGraphDataStatistics STATS2 = new AreaCVGraphDataStatistics
        {
            DataCount     = 46,
            MinMeanArea   = 0,
            MaxMeanArea   = 0,
            Total         = 125,
            MaxCV         = 1.96,
            MinCV         = 0.12,
            MaxFrequency  = 9,
            MedianCV      = 0.48281967365197109,
            MeanCV        = 0.55776,
            belowCVCutoff = 0.024
        };


        private static readonly AreaCVGraphDataStatistics STATS3 = new AreaCVGraphDataStatistics
        {
            DataCount     = 65,
            MinMeanArea   = 0,
            MaxMeanArea   = 0,
            Total         = 1744,
            MaxCV         = 1.54,
            MinCV         = 0,
            MaxFrequency  = 219,
            MedianCV      = 0.17382217742991013,
            MeanCV        = 0.20458715596330274,
            belowCVCutoff = 0.63130733944954132
        };


        private static readonly AreaCVGraphDataStatistics STATS4 = new AreaCVGraphDataStatistics
        {
            DataCount     = 25,
            MinMeanArea   = 0,
            MaxMeanArea   = 0,
            Total         = 124,
            MaxCV         = 1.54,
            MinCV         = 0.02,
            MaxFrequency  = 20,
            MedianCV      = 0.16298477433620745,
            MeanCV        = 0.1970967741935484,
            belowCVCutoff = 0.72580645161290325
        };


        private static readonly AreaCVGraphDataStatistics STATS5 = new AreaCVGraphDataStatistics
        {
            DataCount     = 26,
            MinMeanArea   = 0,
            MaxMeanArea   = 0,
            Total         = 124,
            MaxCV         = 1.5,
            MinCV         = 0,
            MaxFrequency  = 15,
            MedianCV      = 0.1372919480977226,
            MeanCV        = 0.17322580645161287,
            belowCVCutoff = 0.79032258064516125
        };


        private static readonly AreaCVGraphDataStatistics STATS6 = new AreaCVGraphDataStatistics
        {
            DataCount     = 25,
            MinMeanArea   = 0,
            MaxMeanArea   = 0,
            Total         = 124,
            MaxCV         = 1.5,
            MinCV         = 0,
            MaxFrequency  = 19,
            MedianCV      = 0.091041128301750027,
            MeanCV        = 0.13629032258064519,
            belowCVCutoff = 0.83064516129032262
        };


        private static readonly AreaCVGraphDataStatistics STATS7 = new AreaCVGraphDataStatistics
        {
            DataCount     = 121,
            MinMeanArea   = 4.05,
            MaxMeanArea   = 7.9,
            Total         = 125,
            MaxCV         = 1.96,
            MinCV         = 0.13,
            MaxFrequency  = 2,
            MedianCV      = 0.48281967365197109,
            MeanCV        = 0.56232,
            belowCVCutoff = 0.024
        };


        private static readonly AreaCVGraphDataStatistics STATS8 = new AreaCVGraphDataStatistics
        {
            DataCount     = 120,
            MinMeanArea   = 4.05,
            MaxMeanArea   = 7.9,
            Total         = 125,
            MaxCV         = 1.96,
            MinCV         = 0.12,
            MaxFrequency  = 2,
            MedianCV      = 0.48281967365197109,
            MeanCV        = 0.55776,
            belowCVCutoff = 0.024
        };


        private static readonly AreaCVGraphDataStatistics STATS9 = new AreaCVGraphDataStatistics
        {
            DataCount     = 782,
            MinMeanArea   = 3.0500000000000003,
            MaxMeanArea   = 8.1,
            Total         = 1744,
            MaxCV         = 1.54,
            MinCV         = 0,
            MaxFrequency  = 12,
            MedianCV      = 0.17382217742991013,
            MeanCV        = 0.2045871559633029,
            belowCVCutoff = 0.63130733944954132
        };


        private static readonly AreaCVGraphDataStatistics STATS10 = new AreaCVGraphDataStatistics
        {
            DataCount     = 116,
            MinMeanArea   = 4.05,
            MaxMeanArea   = 7.8500000000000005,
            Total         = 125,
            MaxCV         = 0.84,
            MinCV         = 0,
            MaxFrequency  = 3,
            MedianCV      = 0.13919790773338983,
            MeanCV        = 0.16976,
            belowCVCutoff = 0.776
        };


        private static readonly AreaCVGraphDataStatistics STATS11 = new AreaCVGraphDataStatistics
        {
            DataCount     = 110,
            MinMeanArea   = 4.05,
            MaxMeanArea   = 7.8500000000000005,
            Total         = 125,
            MaxCV         = 0.86,
            MinCV         = 0,
            MaxFrequency  = 5,
            MedianCV      = 0.10056060487203196,
            MeanCV        = 0.12944,
            belowCVCutoff = 0.808
        };


        private static readonly AreaCVGraphDataStatistics STATS12 = new AreaCVGraphDataStatistics
        {
            DataCount     = 109,
            MinMeanArea   = 4.05,
            MaxMeanArea   = 7.8500000000000005,
            Total         = 125,
            MaxCV         = 0.84,
            MinCV         = 0,
            MaxFrequency  = 3,
            MedianCV      = 0.090023027892612933,
            MeanCV        = 0.12528,
            belowCVCutoff = 0.84
        };

        #endregion

        [TestMethod]
        public void TestAreaCVHistograms()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"Rat_plasma.sky")));
            WaitForDocumentLoaded();

            TestHistogram();

            // Reset settings
            Settings.Default.AreaCVHistogramBinWidth = 1.0;
            AreaGraphController.PointsType = PointsTypePeakArea.targets;
            AreaGraphController.GroupByGroup = AreaGraphController.GroupByAnnotation = null;
            AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.none;
            OpenAndChangeProperties(f => f.ShowCVCutoff = f.ShowMedianCV = true);

            TestHistogram2D();

            Assert.IsFalse(RecordData, "Successfully recorded data");
        }

        private void TestHistogram()
        {
            RunUI(() =>
            {
                AreaGraphController.GraphType = GraphTypePeakArea.histogram;
                SkylineWindow.ShowGraphPeakArea(true);
            });

            WaitForGraphs();

            var graph = SkylineWindow.GraphPeakArea;
            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            // Test if the toolbar is there and if the displayed data is correct
            AreaCVHistogramGraphPane pane;
            Assert.IsTrue(graph.TryGetGraphPane(out pane));
            Assert.IsTrue(pane.HasToolbar);
            Assert.IsNotNull(graph.Toolbar);
            AssertBarsAreEqual(72, pane);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane.CurrentData, STATS1));

            // Test if the data is correct after changing the bin width and disabling the show cv cutoff option (which affects GetTotalBars)
            Settings.Default.AreaCVHistogramBinWidth = 2.0;
            OpenAndChangeProperties(f => f.ShowCVCutoff = false);
            UpdateAndWait(graph);
            AssertBarsAreEqual(48, pane);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane.CurrentData, STATS2));

            // Make sure that there are no bars when the points type is decoys
            AreaGraphController.PointsType = PointsTypePeakArea.decoys;
            OpenAndChangeProperties(f => f.ShowMedianCV = false);
            UpdateAndWait(graph);
            AssertAllEqual(0, pane.GetTotalBars(), pane.CurrentData.Data.Count);

            // Make sure the toolbar is displaying the annotations correctly and that grouping by "All" works
            AreaGraphController.PointsType = PointsTypePeakArea.targets;
            AreaGraphController.GroupByGroup = "SubjectId";

            UpdateAndWait(graph);
            CollectionAssert.AreEqual(toolbar.Annotations.ToArray(),
                new[]
                {
                    "All",
                    "D102", "D103", "D108", "D138", "D154", "D172", "D196",
                    "H146", "H147", "H148", "H159", "H160", "H161", "H162"
                });
            RunUI(() =>
            {
                Assert.IsTrue(toolbar.GroupsVisible);
                Assert.IsFalse(toolbar.DetectionsVisible);
            });
            AssertAllEqual(65, pane.GetTotalBars(), pane.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane.CurrentData, STATS3));
            WaitForCondition(700, () => pane.Cache.DataCount == 45);

            // Make sure that grouping by an annotation works correctly
            AreaGraphController.GroupByAnnotation = "D102";
            UpdateAndWait(graph);
            AssertAllEqual(25, pane.GetTotalBars(), pane.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane.CurrentData, STATS4));

            // Verify that global standards normalization works
            AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.global_standards;
            UpdateAndWait(graph);
            AssertAllEqual(26, pane.GetTotalBars(), pane.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane.CurrentData, STATS5));

            // Verify that median normalization works
            AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.medians;
            UpdateAndWait(graph);
            AssertAllEqual(25, pane.GetTotalBars(), pane.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane.CurrentData, STATS6));
        }

        private void TestHistogram2D()
        {
            RunUI(() =>
            {
                AreaGraphController.GraphType = GraphTypePeakArea.histogram2d;
                SkylineWindow.ShowGraphPeakArea(true);
            });

            var graph = SkylineWindow.GraphPeakArea;
            UpdateAndWait(graph);

            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            // Test if the toolbar is there and if the displayed data is correct
            AreaCVHistogram2DGraphPane pane2;
            Assert.IsTrue(graph.TryGetGraphPane(out pane2));
            Assert.IsTrue(pane2.HasToolbar);
            Assert.IsNotNull(graph.Toolbar);
            AssertBarsAreEqual(123, pane2);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane2.CurrentData, STATS7));

            // Test if the data is correct after changing the bin width and disabling the show cv cutoff option (which affects the number of GetTotalBars)
            Settings.Default.AreaCVHistogramBinWidth = 2.0;
            OpenAndChangeProperties(f => f.ShowCVCutoff = false);
            UpdateAndWait(graph);
            AssertBarsAreEqual(122, pane2);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane2.CurrentData, STATS8));

            // Make sure that there are no bars when the points type is decoys
            AreaGraphController.PointsType = PointsTypePeakArea.decoys;
            OpenAndChangeProperties(f => f.ShowMedianCV = false);
            UpdateAndWait(graph);
            AssertAllEqual(pane2.GetTotalBars(), pane2.CurrentData.Data.Count, 0);

            // Make sure the toolbar is displaying the annotations correctly and that grouping by "All" works
            AreaGraphController.PointsType = PointsTypePeakArea.targets;
            AreaGraphController.GroupByGroup = "BioReplicate";
            UpdateAndWait(graph);
            CollectionAssert.AreEqual(toolbar.Annotations.ToArray(),
                new[]
                {
                    "All",
                    "102", "103", "108", "138", "154", "172", "196", "146", "147", "148", "159", "160", "161", "162"      
                });
            RunUI(() =>
            {
                Assert.IsTrue(toolbar.GroupsVisible);
                Assert.IsFalse(toolbar.DetectionsVisible);
            });
            Assert.IsTrue(toolbar.Visible);
            AssertAllEqual(782, pane2.GetTotalBars(), pane2.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane2.CurrentData, STATS9));
            WaitForCondition(700, () => pane2.Cache.DataCount == 45);

            // Make sure that grouping by an annotation works correctly
            AreaGraphController.GroupByAnnotation = "160";
            UpdateAndWait(graph);
            Assert.IsTrue(toolbar.Visible);
            AssertAllEqual(116, pane2.GetTotalBars(), pane2.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane2.CurrentData, STATS10));

            // Verify that global standards normalization works
            AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.global_standards;
            UpdateAndWait(graph);
            AssertAllEqual(110, pane2.GetTotalBars(), pane2.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane2.CurrentData, STATS11));

            // Verify that median normalization works
            AreaGraphController.NormalizationMethod = AreaCVNormalizationMethod.medians;
            UpdateAndWait(graph);
            AssertAllEqual(109, pane2.GetTotalBars(), pane2.CurrentData.Data.Count);
            Assert.IsTrue(AreaCVGraphDataStatisticsEqual(pane2.CurrentData, STATS12));
        }

        private void OpenAndChangeProperties(Action<AreaCVToolbarProperties> action)
        {
            using (var form = ShowDialog<AreaCVToolbarProperties>(() => SkylineWindow.ShowAreaCVPropertyDlg()))
            {
                RunUI(() =>
                {
                    action(form);
                    form.OK();
                });

                UpdateAndWait(SkylineWindow.GraphPeakArea);
            }
        }

        private void AssertBarsAreEqual(int expected, SummaryGraphPane pane)
        {
            if (RecordData)
                return;

            Assert.AreEqual(expected, pane.GetTotalBars());
        }

        private void AssertAllEqual<T>(params T[] values)
        {
            if (RecordData)
                return;

            for (var i = 1; i < values.Length; ++i)
                Assert.AreEqual(values[0], values[i]);
        }

        private void UpdateAndWait(GraphSummary graph)
        {
            RunUI(() => { graph.UpdateUI(); });
            WaitForGraphs();
        }

        private int _currentStatsIndex = 1;
        private bool AreaCVGraphDataStatisticsEqual(AreaCVGraphData a, AreaCVGraphDataStatistics b)
        {
            if (!RecordData)
            {
                return a.Data.Count == b.DataCount && a.MinMeanArea == b.MinMeanArea && a.MaxMeanArea == b.MaxMeanArea && a.Total == b.Total &&
                       a.MaxCV == b.MaxCV && a.MinCV == b.MinCV && a.MaxFrequency == b.MaxFrequency &&
                       a.MedianCV == b.MedianCV && a.MeanCV == b.MeanCV && a.belowCVCutoff == b.belowCVCutoff;
            }
            
            Console.WriteLine(
@"private static readonly AreaCVGraphDataStatistics STATS{0} = new AreaCVGraphDataStatistics
{{
    DataCount     = {1},
    MinMeanArea   = {2:R},
    MaxMeanArea   = {3:R},
    Total         = {4},
    MaxCV         = {5:R},
    MinCV         = {6:R},
    MaxFrequency  = {7},
    MedianCV      = {8:R},
    MeanCV        = {9:R},
    belowCVCutoff = {10:R}
}};

", _currentStatsIndex++, a.Data.Count, a.MinMeanArea, a.MaxMeanArea, a.Total, a.MaxCV, a.MinCV, a.MaxFrequency, a.MedianCV, a.MeanCV, a.belowCVCutoff);

            return true;
        }
    }
}