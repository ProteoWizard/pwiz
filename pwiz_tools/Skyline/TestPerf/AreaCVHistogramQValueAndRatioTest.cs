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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class AreaCVHistogramQValueAndRatioTest : AbstractFunctionalTestEx
    {
        private static readonly AreaCVGraphDataStatistics[] STATS =
        {
            new AreaCVGraphDataStatistics(117, 117, 0, 0, 12291, 1.56, 0, 806, 0.095164132092103074, 0.13255471483199088, 0.80701326173622978),
            new AreaCVGraphDataStatistics(172, 172, 0, 0, 18608, 1.71, 0, 268, 0.50598024141351039, 0.53897463456577821, 0.13392089423903697),
            new AreaCVGraphDataStatistics(124, 124, 0, 0, 13187, 1.56, 0, 818, 0.10081781341883254, 0.14358231591719123, 0.78357473269128686),
            new AreaCVGraphDataStatistics(115, 115, 0, 0, 11312, 1.56, 0, 758, 0.095865665157771063, 0.13204738330975957, 0.81108557284299854),
            new AreaCVGraphDataStatistics(2269, 2269, 3.85, 8.5, 12291, 1.56, 0, 42, 0.095164132092103074, 0.13255471483199055, 0.80701326173622978),
            new AreaCVGraphDataStatistics(5847, 5847, 3.1, 7.45, 18608, 1.71, 0, 17, 0.50598024141351039, 0.53897463456577754, 0.13392089423903697),
            new AreaCVGraphDataStatistics(2494, 2494, 3.6500000000000004, 8.5, 13187, 1.56, 0, 42, 0.10081781341883254, 0.14358231591719073, 0.78357473269128686),
            new AreaCVGraphDataStatistics(2129, 2129, 3.9000000000000004, 8.5, 11312, 1.56, 0, 40, 0.095865665157771063, 0.13204738330975938, 0.81108557284299854),
            new AreaCVGraphDataStatistics(87, 88, 0, 0, 125, 6.63, 0.53, 4, 1.6731671213240806, 1.908, 0),
            new AreaCVGraphDataStatistics(40, 41, 0, 0, 125, 6.55, 0.57000000000000006, 19, 2.1899170913612949, 2.21816, 0),
            new AreaCVGraphDataStatistics(0, 0, 1.7976931348623157E+308, 0, 0, 0, 0, 0, 0, 0, 0),
            new AreaCVGraphDataStatistics(123, 123, 4.45, 7, 125, 6.63, 0.53, 2, 1.6731671213240806, 1.9080000000000001, 0),
            new AreaCVGraphDataStatistics(111, 111, 3.7, 7, 125, 6.55, 0.57000000000000006, 3, 2.1899170913612949, 2.21816, 0),
            new AreaCVGraphDataStatistics(0, 111, 1.7976931348623157E+308, 0, 0, 0, 0, 0, 0, 0, 0),
        };

        private bool RecordData { get { return false; } }

        [TestMethod]
        public void TestAreaCVHistogramQValuesAndRatios()
        {
            // RunPerfTests = true;
            TestFilesPersistent = new[] { "." };  // All persistent. No saving
            TestFilesZip = @"http://proteome.gs.washington.edu/software/test/skyline-perf/AreaCVHistogramQValueAndRatioTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                Settings.Default.AreaCVShowCVCutoff = Settings.Default.AreaCVShowMedianCV = false;
                AreaGraphController.GroupByAnnotation = AreaGraphController.GroupByGroup = null;
            });

            OpenDocument(TestFilesDir.GetTestPath(@"BrukerDIA3_0.sky"));

            TestHistogramQValues<AreaCVHistogramGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram, 0);
            TestHistogramQValues<AreaCVHistogram2DGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram2D, 4);
            TestRefinementQvalue();

            OpenDocument(TestFilesDir.GetTestPath(@"Site54_Study9-1_standardcurves_083011_v1.sky"));

            TestHistogramRatios<AreaCVHistogramGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram, 8);
            TestHistogramRatios<AreaCVHistogram2DGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram2D, 11);
            TestRefinementRatios();

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

            var graph = SkylineWindow.GraphPeakArea;
            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            RunUI(() => toolbar.SetMinimumDetections(2));
            OpenAndChangeAreaCVProperties(graph, p => p.QValueCutoff = 0.01);

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
            OpenAndChangeAreaCVProperties(graph, p => p.QValueCutoff = 0.02);
            AssertDataCorrect(pane, statsStartIndex++, false);

            // Make sure that the data is correct after changing the qvalue cutoff, this time with points type targets
            RunUI(() =>
            {
                SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets);
            });
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++);

            RunUI(() => toolbar.SetMinimumDetections(3));
            UpdateGraphAndWait(graph);
            AssertDataCorrect(pane, statsStartIndex++);

            // Verify that a qvalue cutoff of 1.0 has the same effect as no qvalue cutoff
            OpenAndChangeAreaCVProperties(graph, p => p.QValueCutoff = 1.0);
            AreaCVGraphData qvalue1Data = null;
            WaitForConditionUI(() => (qvalue1Data = GetCurrentData(pane)) != null);
            AreaCVGraphDataStatistics qvalue1Statistics = null;
            RunUI(() => qvalue1Statistics = new AreaCVGraphDataStatistics(qvalue1Data, pane.GetTotalBars()));
            OpenAndChangeAreaCVProperties(graph, p => p.QValueCutoff = double.NaN);
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

            var graph = SkylineWindow.GraphPeakArea;
            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            RunUI(() => toolbar.SetMinimumDetections(2));
            OpenAndChangeAreaCVProperties(graph, p => p.QValueCutoff = double.NaN);

            // Make sure toolbar is there, combo box items are correct and data is correct
            T pane;
            Assert.IsTrue(graph.TryGetGraphPane(out pane));
            Assert.IsTrue(pane.HasToolbar);
            CollectionAssert.AreEqual(new[] { "Light", "Heavy", "All 15N", Resources.AreaCVToolbar_UpdateUI_Medians, Resources.AreaCVToolbar_UpdateUI_None}, toolbar.NormalizationMethods.ToArray());
            AssertDataCorrect(pane, statsStartIndex++); // Light

            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.ratio, 1));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++); // Heavy

            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.ratio, 2));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++, allowInvalid: true); // All 15N
        }

        private void TestRefinementQvalue()
        {
            var graphStates = new[] { (3, 9791, 10343, 61383), (3, 8658, 9185, 54526) , (3, 9395, 10426, 61914) };

            // Verify refinement statistics are same as graph statistics
            RunUI(SkylineWindow.Undo);
            RefineDlg refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.CVCutoff = 20;
                refineDlg.QValueCutoff = 0.02;
            });
            OkDialog(refineDlg, refineDlg.OkDialog);
            var doc = SkylineWindow.Document;
            var refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(graphStates[0], refineDocState);
            RunUI(SkylineWindow.Undo);

            // Verify that a qvalue cutoff of 1.0 has the same effect as no qvalue cutoff
            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.CVCutoff = 20;
                refineDlg.QValueCutoff = 1.0;
            });
            OkDialog(refineDlg, refineDlg.OkDialog);

            doc = SkylineWindow.Document;
            var qvalue1State = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            RunUI(SkylineWindow.Undo);

            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.CVCutoff = 20;
                refineDlg.QValueCutoff = double.NaN;
            });
            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            var qvalueNanState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(qvalue1State, qvalueNanState);
            RunUI(SkylineWindow.Undo);

            // Verify refinement with minimum detections is same as graph state
            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.CVCutoff = 20;
                refineDlg.QValueCutoff = 0.02;
                refineDlg.MinimumDetections = 3;
            });
            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(graphStates[1], refineDocState);
            RunUI(SkylineWindow.Undo);

            // Verify refinement with only Q-value works.
            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.QValueCutoff = 0.01;
                refineDlg.MinimumDetections = 3;
            });
            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);
            Assert.AreEqual(graphStates[2], refineDocState);
            RunUI(SkylineWindow.Undo);
        }

        private void TestRefinementRatios()
        {
            var graphStates = new[] { (34, 111, 111, 333), (34, 0, 0, 0), (34, 0, 0, 0) };

            // Normalize to light reference type
            var refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.CVCutoff = 250;
                refineDlg.NormalizationMethod = AreaCVNormalizationMethod.ratio;
                refineDlg.CVRefineLabelType = IsotopeLabelType.light;
            });
            OkDialog(refineDlg, refineDlg.OkDialog);
            var doc = SkylineWindow.Document;
            var refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);

            Assert.AreEqual(graphStates[0], refineDocState);

            RunUI(SkylineWindow.Undo);

            // Normalize to heave reference type
            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.CVCutoff = 20;
                refineDlg.NormalizationMethod = AreaCVNormalizationMethod.ratio;
                refineDlg.CVRefineLabelType = IsotopeLabelType.heavy;
            });
            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);

            Assert.AreEqual(graphStates[1], refineDocState);

            RunUI(SkylineWindow.Undo);

            // Normalize to all 15N reference type
            refineDlg = ShowDialog<RefineDlg>(() => SkylineWindow.ShowRefineDlg());
            RunUI(() =>
            {
                refineDlg.CVCutoff = 20;
                refineDlg.NormalizationMethod = AreaCVNormalizationMethod.ratio;
                refineDlg.CVRefineLabelType = new IsotopeLabelType("all 15N", 0);
            });
            OkDialog(refineDlg, refineDlg.OkDialog);
            doc = SkylineWindow.Document;
            refineDocState = (doc.PeptideGroupCount, doc.PeptideCount, doc.PeptideTransitionGroupCount,
                doc.PeptideTransitionCount);

            Assert.AreEqual(graphStates[2], refineDocState);
        }

        private static AreaCVGraphData GetCurrentData(SummaryGraphPane pane)
        {
            var testSupport = pane as IAreaCVHistogramInfo;
            if (testSupport != null)
                return testSupport.CurrentData;

            Assert.Fail("Graph pane is not a histogram/histogram2d graph pane");
            return null;
        }

        private void AssertDataCorrect(SummaryGraphPane pane, int statsIndex, bool record = true, bool allowInvalid = false)
        {
            AreaCVGraphData data = null;
            WaitForConditionUI(() => (data = GetCurrentData(pane)) != null && (allowInvalid || data.IsValid));
            WaitForGraphs();

            RunUI(() =>
            {
                var testSupport = pane as IAreaCVHistogramInfo;
                int items = testSupport != null ? testSupport.Items : 0;
                var graphDataStatistics = new AreaCVGraphDataStatistics(data, items);

                if (!RecordData)
                    Assert.AreEqual(STATS[statsIndex], graphDataStatistics);
                else if (record)
                    Console.WriteLine(graphDataStatistics.ToCode());
            });
        }
    }
}