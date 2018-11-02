﻿/*
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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AreaCVHistogramTest : AbstractFunctionalTestEx
    {
        private bool RecordData { get { return false; } }

        private static int HISTOGRAM_DATA_START = 0;
        private static int HISTOGRAM2D_DATA_START = 6;

        private static readonly AreaCVGraphDataStatistics[] STATS =
        {
            new AreaCVGraphDataStatistics(68, 73, 0, 0, 125, 1.96, 0.13, 7, 0.48281964548670575, 0.56232, 0.024),
            new AreaCVGraphDataStatistics(46, 49, 0, 0, 125, 1.96, 0.12, 9, 0.48281964548670575, 0.55776, 0.024),
            new AreaCVGraphDataStatistics(65, 73, 0, 0, 1744, 1.54, 0, 219, 0.17382216557004743, 0.20458715596330274, 0.63130733944954132),
            new AreaCVGraphDataStatistics(25, 26, 0, 0, 124, 1.54, 0.02, 20, 0.16298478671652405, 0.1970967741935484, 0.72580645161290325),
            new AreaCVGraphDataStatistics(26, 27, 0, 0, 124, 1.5, 0, 15, 0.13729195302811725, 0.17322580645161287, 0.79032258064516125),
            new AreaCVGraphDataStatistics(25, 26, 0, 0, 124, 1.5, 0, 19, 0.0910411123260385, 0.13629032258064519, 0.83064516129032262),
            new AreaCVGraphDataStatistics(121, 121, 4.05, 7.9, 125, 1.96, 0.13, 2, 0.48281964548670575, 0.56232, 0.024),
            new AreaCVGraphDataStatistics(120, 120, 4.05, 7.9, 125, 1.96, 0.12, 2, 0.48281964548670575, 0.55776, 0.024),
            new AreaCVGraphDataStatistics(782, 782, 3.0500000000000003, 8.1, 1744, 1.54, 0, 12, 0.17382216557004743, 0.2045871559633029, 0.63130733944954132),
            new AreaCVGraphDataStatistics(118, 118, 3.6500000000000004, 7.95, 124, 1.54, 0.02, 3, 0.16298478671652405, 0.19709677419354832, 0.72580645161290325),
            new AreaCVGraphDataStatistics(115, 115, 3.6500000000000004, 7.95, 124, 1.5, 0, 2, 0.13729195302811725, 0.1732258064516129, 0.79032258064516125),
            new AreaCVGraphDataStatistics(104, 104, 3.6500000000000004, 7.95, 124, 1.5, 0, 4, 0.0910411123260385, 0.13629032258064522, 0.83064516129032262),
        };

        private static readonly string[] HISTOGRAM_FINDRESULTS = 
        {
            "K.DFATVYVDAVK.D [35, 45]",
            "R.ASGIIDTLFQDR.F [181, 192]",
            "R.HTNNGMICLTSLLR.I [279, 292]",
            "K.ENSSNILDNLLSR.M [802, 814]",
            "R.ALIHCLHMS.- [436, 444]",
            "R.LTHGDFTWTTK.K [90, 100]",
            "K.YGLDLGSLLVR.L [1066, 1076]",
            "K.AGSWQITMK.G [258, 266]",
            "TLNSINIAVFSK",
            "ENEGTYYGPDGR",
            "ENLSPPLGECLLER",
            "ELLDSYIDGR",
            "EEPSADALLPIDCR",
            "AIEDYVNEFSAR",
            "MHPELGSFYDSR",
            "VTSAAFPSPIEK",
            "AEQGAYLGPLPYK",
            "TDEDVPSGPPR",
            "ETGLMAFTNLK",
            "LQTEGDGIYTLNSEK",
        };

        private static readonly string[] HISTOGRAM2D_FINDRESULTS = 
        {
            "K.DFATVYVDAVK.D [35, 45]",
            "R.ASGIIDTLFQDR.F [181, 192]",
            "R.HTNNGMICLTSLLR.I [279, 292]",
            "K.ENSSNILDNLLSR.M [802, 814]",
            "R.ALIHCLHMS.- [436, 444]",
            "R.LTHGDFTWTTK.K [90, 100]",
            "K.YGLDLGSLLVR.L [1066, 1076]",
            "K.AGSWQITMK.G [258, 266]",
            "TLNSINIAVFSK",
            "ENEGTYYGPDGR",
            "ENLSPPLGECLLER",
            "ELLDSYIDGR",
            "EEPSADALLPIDCR",
            "AIEDYVNEFSAR",
            "MHPELGSFYDSR",
            "VTSAAFPSPIEK",
            "AEQGAYLGPLPYK",
            "TDEDVPSGPPR",
            "ETGLMAFTNLK",
            "LQTEGDGIYTLNSEK",
        };

        private static int GetItemsAboveCutoff(int statsIndex)
        {
            return statsIndex < HISTOGRAM2D_DATA_START ? 66 : 118;
        }

        private readonly List<string[][]> RecordedFindResults = new List<string[][]>();

        [TestMethod]
        public void TestAreaCVHistograms()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));

            TestHistogram<AreaCVHistogramGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram, HISTOGRAM_DATA_START);
            TestHistogram<AreaCVHistogram2DGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram2D, HISTOGRAM2D_DATA_START);

            if (RecordData)
            {
                foreach (var recordedFindResults in RecordedFindResults)
                {
                    foreach (var str in recordedFindResults)
                    {
                        Console.WriteLine(@"new[] { " + string.Join(", ", str.Select(s => "\"" + s + "\"")) + @" },");
                    }
                    Console.WriteLine();
                }
                Assert.Fail("Successfully recorded data");
            }
        }
        
        private void TestHistogram<T>(Action showHistogram, int statsStartIndex) where T : SummaryGraphPane
        {
            RunUI(showHistogram);

            WaitForGraphs();

            // Reset settings
            RunUI(() =>
            {
                SkylineWindow.SetAreaCVBinWidth(1.0);
                SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets);
                SkylineWindow.SetAreaCVGroup(null);
                SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.none);
            });

            WaitForGraphs();

            var graph = SkylineWindow.GraphPeakArea;
            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            OpenAndChangeAreaCVProperties(graph, f => f.ShowCVCutoff = f.ShowMedianCV = true);

            // Test if the toolbar is there and if the displayed data is correct
            T pane;
            Assert.IsTrue(graph.TryGetGraphPane(out pane));
            Assert.IsInstanceOfType(pane, typeof(IAreaCVHistogramInfo));
            Assert.IsTrue(pane.HasToolbar);
            AssertDataCorrect(pane, statsStartIndex++);

            var histogramInfo = (IAreaCVHistogramInfo) pane;
            var itemCount = 0;
            // Verify that removing CV's above cutoff works
            RunUI(() =>
            {
                itemCount = histogramInfo.Items;
                SkylineWindow.RemoveAboveCVCutoff(graph);
            });

            int expectedBars = itemCount - GetItemsAboveCutoff(statsStartIndex);
            WaitForHistogramBarCount(histogramInfo, expectedBars);

            RunUI(SkylineWindow.Undo);

            WaitForHistogramBarCount(histogramInfo, itemCount);

            // Test if the data is correct after changing the bin width and disabling the show cv cutoff option (which affects GetTotalBars)
            RunUI(() => SkylineWindow.SetAreaCVBinWidth(2.0));
            OpenAndChangeAreaCVProperties(graph, f => f.ShowCVCutoff = false);
            AssertDataCorrect(pane, statsStartIndex++);

            // Make sure that there are no bars when the points type is decoys
            RunUI(() => SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.decoys));
            OpenAndChangeAreaCVProperties(graph, f => f.ShowMedianCV = false);
            WaitForConditionUI(() => GetCurrentData(pane) != null);
            RunUI(() => Assert.AreEqual(0, pane.GetBoxObjCount()));

            // Make sure the toolbar is displaying the annotations correctly and that grouping by "All" works
            RunUI(() => SkylineWindow.SetAreaCVGroup("SubjectId"));
            RunUI(() => SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets));
            WaitForGraphs();

            string[] annotations = null;
            RunUI(() => annotations = toolbar.Annotations.ToArray());

            CollectionAssert.AreEqual(annotations,
                new[]
                {
                    Resources.GraphSummary_UpdateToolbar_All,
                    "D102", "D103", "D108", "D138", "D154", "D172", "D196",
                    "H146", "H147", "H148", "H159", "H160", "H161", "H162"
                });
            RunUI(() =>
            {
                Assert.IsTrue(toolbar.GroupsVisible);
                Assert.IsFalse(toolbar.DetectionsVisible);
            });
            AssertDataCorrect(pane, statsStartIndex++);
            WaitForCondition(700, () => GetCache(pane).DataCount == 45);
            
            // Make sure that grouping by an annotation works correctly
            RunUI(() => SkylineWindow.SetAreaCVAnnotation("D102"));
            UpdateGraphAndWait(graph);
            AssertDataCorrect(pane, statsStartIndex++);

            // Verify that clicking a bar/point opens the find resuls and correctly shows the peptides
            var p = new PointF(15.0f, 10.0f); // Center of 14 CV bar
            p = pane.GeneralTransform(p, CoordType.AxisXYScale);
            var mouseEventArgs = new MouseEventArgs(MouseButtons.Left, 1, (int)p.X, (int)p.Y, 0);
            string[][] items = null;
            RunUI(() =>
            {
                pane.HandleMouseMoveEvent(pane.GraphSummary.GraphControl, mouseEventArgs);
                pane.HandleMouseClick(pane.GraphSummary.GraphControl, mouseEventArgs);
            });

            var form = WaitForOpenForm<FindResultsForm>();
            RunUI(() =>
            {
                items = form.ListView.Items.OfType<ListViewItem>().Select(l => l.SubItems
                    .OfType<ListViewItem.ListViewSubItem>().Where((item, index) => index != 1).Select(i => i.Text)
                    .ToArray()).ToArray();
            });

            if (RecordData)
            {
                RecordedFindResults.Add(items);
            }
            else
            {
                // Verify that the peptides are displayed correctly in the find results window
                string[][] expected = null;
                if (typeof(T) == typeof(AreaCVHistogramGraphPane))
                    expected = GetExpected(HISTOGRAM_FINDRESULTS);
                else if (typeof(T) == typeof(AreaCVHistogram2DGraphPane))
                    expected = GetExpected(HISTOGRAM2D_FINDRESULTS);
                else
                    Assert.Fail("T not a Histogram graph pane");

                CollectionAssert.AreEqual(expected, items, Comparer<string[]>.Create((a, b) =>
                {
                    for (var i = 0; i < Math.Min(a.Length, b.Length); ++i)
                    {
                        if (a[i] != b[i])
                            return string.Compare(a[i], b[i], StringComparison.CurrentCulture);
                    }

                    return a.Length.CompareTo(b.Length);
                }));
            }


            // Verify that global standards normalization works
            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.global_standards));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++);

            // Verify that median normalization works
            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.medians));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++);
        }

        private string[][] GetExpected(string[] foundText)
        {
            return foundText.Select(t => new[] {t, PeptideAnnotationPairFinder.GetDisplayText(0.14, "D102")}).ToArray();
        }

        private void WaitForHistogramBarCount(IAreaCVHistogramInfo histogramInfo, int expectedBars)
        {
            WaitForConditionUI(() => histogramInfo.Items == expectedBars,
                string.Format("Expecting {0} bars", expectedBars));
        }

        private static AreaCVGraphData.AreaCVGraphDataCache GetCache(SummaryGraphPane pane)
        {
            return GetPaneValue(pane, support => support.Cache);
        }

        private static AreaCVGraphData GetCurrentData(SummaryGraphPane pane)
        {
            return GetPaneValue(pane, support => support.CurrentData);
        }

        private static TVal GetPaneValue<TVal>(SummaryGraphPane pane, Func<IAreaCVHistogramInfo, TVal> getValue)
        {
            var info = pane as IAreaCVHistogramInfo;
            if (info != null)
                return getValue(info);

            Assert.Fail("Graph pane is not a histogram/histogram2d graph pane");
            return default(TVal);
        }

        private void AssertDataCorrect(SummaryGraphPane pane, int statsIndex)
        {
            AreaCVGraphData data = null;
            WaitForConditionUI(() => (data = GetCurrentData(pane)) != null);
            WaitForGraphs();

            RunUI(() =>
            {
                var info = pane as IAreaCVHistogramInfo;
                int items = info != null ? info.Items : 0;
                var graphDataStatistics = new AreaCVGraphDataStatistics(data, items);

                if (!RecordData)
                    Assert.AreEqual(STATS[statsIndex], new AreaCVGraphDataStatistics(data, items));
                else
                    Console.WriteLine(graphDataStatistics.ToCode());
            });
        }
    }
}