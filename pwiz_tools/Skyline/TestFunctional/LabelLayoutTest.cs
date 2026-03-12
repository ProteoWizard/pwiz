/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Co-authored: OpenAI Codex
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LabelLayoutTest : AbstractFunctionalTestEx
    {
        private const float LABEL_TOLERANCE_PX = 2f;
        private const int EXPECTED_POINT_COUNT = 32;
        private static readonly ExpectedPointSnapshot[] EXPECTED_RANDOM_POINTS =
        {
            new ExpectedPointSnapshot(9, "HTNNGMICLTSLLR", 53f, 560475.1f),
            new ExpectedPointSnapshot(10, "IFPENNIK", 60f, 471769.7f),
            new ExpectedPointSnapshot(24, "SQLQEGPPEWK", 6f, 11025796f),
            new ExpectedPointSnapshot(30, "VLIVEPEGIK", 17f, 2936371f),
            new ExpectedPointSnapshot(31, "WTNPDGTTSK", 84f, 199644.5f),
        };

        [TestMethod]
        public void TestLabelLayoutDeterminism()
        {
            TestFilesZip = @"TestFunctional/LabelLayoutTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => Settings.Default.GroupComparisonAvoidLabelOverlap = true);

            OpenDocumentAndGraph();
            var firstSnapshot = CaptureLabelLayoutSnapshot();

            LoadNewDocument(true);

            RunUI(() => Settings.Default.GroupComparisonAvoidLabelOverlap = true);
            OpenDocumentAndGraph();
            var secondSnapshot = CaptureLabelLayoutSnapshot();
            CompareSnapshots(firstSnapshot, secondSnapshot);
        }

        private void OpenDocumentAndGraph()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_Plasma.sky")));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ShowPeakAreaRelativeAbundanceGraph());
            WaitForGraphPane();
        }

        private SummaryRelativeAbundanceGraphPane WaitForGraphPane()
        {
            SummaryRelativeAbundanceGraphPane pane = null;
            WaitForConditionUI(() =>
            {
                pane = FindGraphPane();
                return pane != null && pane.IsSuccessfullyComplete;
            });
            return pane;
        }

        private class PointSnapshot
        {
            public string LabelText { get; set; }
            public PointF LabelPosition { get; set; }
        }

        private class ExpectedPointSnapshot
        {
            public ExpectedPointSnapshot(int index, string labelText, float x, float y)
            {
                Index = index;
                LabelText = labelText;
                LabelPosition = new PointF(x, y);
            }

            public int Index { get; private set; }
            public string LabelText { get; private set; }
            public PointF LabelPosition { get; private set; }
        }

        private List<PointSnapshot> CaptureLabelLayoutSnapshot()
        {
            var pane = WaitForGraphPane();
            Assert.IsNotNull(pane, "Missing relative abundance graph pane.");

            WaitForConditionUI(() =>
                pane.EnableLabelLayout &&
                pane.Layout != null &&
                pane.Layout.PointsLayout.Count > 0);

            var snapshot = new List<PointSnapshot>();

            RunUI(() =>
            {
                var layout = pane.GraphSummary.GraphControl.GraphPane.Layout;
                snapshot = layout.LabeledPoints.Select(lp => new PointSnapshot(){ LabelPosition = lp.Value.LabelPosition, LabelText = lp.Value.Label.Text}).ToList();
            });
            snapshot.Sort((ps1, ps2) => String.Compare((ps1.LabelText + ps1.LabelPosition), ps2.LabelText + ps2.LabelPosition, StringComparison.Ordinal));
            Assert.IsNotNull(snapshot, "Layout snapshot was not captured.");
            Assert.IsTrue(snapshot.Count > 0, "No labels captured for layout snapshot.");
            return snapshot;
        }

        private void CompareSnapshots(List<PointSnapshot> first, List<PointSnapshot> second)
        {
            Assert.AreEqual(first.Count, second.Count, "Snapshots have different number of points.");
            Assert.AreEqual(EXPECTED_POINT_COUNT, first.Count, "Plot point count is different from expected.");
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].LabelText, second[i].LabelText, $@"Point {i} label text do not match.");
                AssertPointPositionEqual(first[i].LabelPosition, second[i].LabelPosition, $@"Point {i} position does not match.");
            }

            foreach (var expectedPoint in EXPECTED_RANDOM_POINTS)
            {
                AssertExpectedPoint(first, expectedPoint, "first");
                AssertExpectedPoint(second, expectedPoint, "second");
            }
        }

        private void AssertExpectedPoint(IReadOnlyList<PointSnapshot> snapshot, ExpectedPointSnapshot expected, string snapshotName)
        {
            Assert.IsTrue(expected.Index >= 0 && expected.Index < snapshot.Count,
                string.Format("Expected point index {0} is out of range for {1} snapshot.", expected.Index, snapshotName));
            var actual = snapshot[expected.Index];
            Assert.AreEqual(expected.LabelText, actual.LabelText,
                string.Format("{0} snapshot point {1} label text does not match.", snapshotName, expected.Index));
            AssertPointPositionEqual(expected.LabelPosition, actual.LabelPosition,
                string.Format("{0} snapshot point {1} position does not match expected.", snapshotName, expected.Index));
        }

        private void AssertPointPositionEqual(PointF expected, PointF actual, string message)
        {
            AssertAxisEqual(expected.X, actual.X, "X", message);
            AssertAxisEqual(expected.Y, actual.Y, "Y", message);
        }

        private void AssertAxisEqual(float expected, float actual, string axisName, string message)
        {
            if (Math.Abs(expected - actual) <= LABEL_TOLERANCE_PX)
                return;

            Assert.Fail(string.Format(CultureInfo.InvariantCulture,
                "{0} ({1}) expected {2} but was {3}; delta={4}, tolerance={5}",
                message,
                axisName,
                expected.ToString("R", CultureInfo.InvariantCulture),
                actual.ToString("R", CultureInfo.InvariantCulture),
                Math.Abs(expected - actual).ToString("R", CultureInfo.InvariantCulture),
                LABEL_TOLERANCE_PX.ToString("R", CultureInfo.InvariantCulture)));
        }


        private SummaryRelativeAbundanceGraphPane FindGraphPane()
        {
            foreach (var graphSummary in SkylineWindow.ListGraphPeakArea)
            {
                if (graphSummary.TryGetGraphPane<SummaryRelativeAbundanceGraphPane>(out var pane))
                    return pane;
            }
            return null;
        }
    }
}
