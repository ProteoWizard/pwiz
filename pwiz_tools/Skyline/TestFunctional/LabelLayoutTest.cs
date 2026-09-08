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
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LabelLayoutTest : AbstractFunctionalTestEx
    {
        private const float LABEL_TOLERANCE_PX = 2f;
        private const int EXPECTED_POINT_COUNT = 13;
        private static readonly ExpectedPointSnapshot[] EXPECTED_RANDOM_POINTS =
        {
            new ExpectedPointSnapshot(1, "EENGDFASFR", 55f, 545123.8f),
            new ExpectedPointSnapshot(4, "HEEEVERPAVEK", 86f, 191400.8f),
            new ExpectedPointSnapshot(7, "MLSGFIPLKPTVK", 98f, 138878.5f),
            new ExpectedPointSnapshot(9, "TSDQIHFFFAK", 113f, 79212.3f),
            new ExpectedPointSnapshot(12, "WTNPDGTTSK", 84f, 199644.5f),
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
            VerifyLayoutInvariants();

            LoadNewDocument(true);

            RunUI(() => Settings.Default.GroupComparisonAvoidLabelOverlap = true);
            OpenDocumentAndGraph();
            var secondSnapshot = CaptureLabelLayoutSnapshot();
            VerifyLayoutInvariants();
            CompareSnapshots(firstSnapshot, secondSnapshot);
        }

        private void OpenDocumentAndGraph()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_Plasma.sky")));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ShowPeakAreaRelativeAbundanceGraph());
            WaitForGraphPane();
            // The saved label layout is static so that label positions survive pane recreation -
            // which also means it survives across runs and language passes in the same process.
            // Toggling the overlap setting with the pane alive clears it, so every capture
            // measures a fresh, deterministic layout instead of restoring the previous run's.
            RunUI(() => Settings.Default.GroupComparisonAvoidLabelOverlap = false);
            RunUI(() => Settings.Default.GroupComparisonAvoidLabelOverlap = true);
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
            // Offscreen rendering produces non-reproducible screen coordinates, making
            // deterministic layout comparison unreliable. Skip comparison but still verify
            // that the layout code ran and produced labels.
            if (Program.SkylineOffscreen)
            {
                Assert.IsTrue(first.Count > 0, "First snapshot has no labels (offscreen).");
                Assert.IsTrue(second.Count > 0, "Second snapshot has no labels (offscreen).");
                return;
            }

            Assert.AreEqual(first.Count, second.Count, "Snapshots have different number of points.");
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].LabelText, second[i].LabelText, $@"Point {i} label text do not match.");
                AssertPointPositionEqual(first[i].LabelPosition, second[i].LabelPosition, $@"Point {i} position does not match.");
            }

            // The pinned snapshot below encodes the label choices and positions produced by
            // English chart geometry. Localized axis titles and fonts change the chart
            // rectangle, which legitimately changes which labels the sampler and pruner keep,
            // so the pinned expectations are only checked when running in English.
            if (!Equals("en", GetFolderNameForLanguage(CultureInfo.CurrentCulture)))
                return;

            Assert.AreEqual(EXPECTED_POINT_COUNT, first.Count, "Plot point count is different from expected.");
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


        // Distance (squared, in pixels) within which a marker center counts as a label's own
        // point. Mirrors LabelLayout.OWN_MARKER_TOLERANCE_SQ.
        private const float OWN_MARKER_TOLERANCE_SQ = 4f;

        /// <summary>
        /// Verifies the invariants the post-annealing pruner guarantees, in any language and
        /// with any chart geometry: no non-selected visible label overlaps any other visible
        /// label, and no non-selected visible label covers the center of a point marker other
        /// than its own. Selected labels are exempt as subjects, exactly as in the pruner: they
        /// are never pruned, so when a saved layout is restored under different geometry (e.g.
        /// a second language pass reusing the static saved layout) a selected label may
        /// legitimately end up covering a marker or overlapping another selected label.
        /// </summary>
        private void VerifyLayoutInvariants()
        {
            RunUI(() =>
            {
                var summaryPane = FindGraphPane();
                Assert.IsNotNull(summaryPane, "Missing relative abundance graph pane.");
                var graphControl = summaryPane.GraphSummary.GraphControl;
                var pane = graphControl.GraphPane;
                var layout = pane.Layout;
                Assert.IsNotNull(layout, "Missing label layout for invariant verification.");
                using (var g = graphControl.CreateGraphics())
                {
                    var entries = new List<KeyValuePair<LabeledPoint, RectangleF>>();
                    foreach (var labeledPoint in layout.LabeledPoints.Values.Where(lp => lp.Label.IsVisible))
                    {
                        var rect = pane.GetRectScreen(labeledPoint.Label, g);
                        if (rect.Width > 0 && rect.Height > 0)
                            entries.Add(new KeyValuePair<LabeledPoint, RectangleF>(labeledPoint, rect));
                    }

                    for (var i = 0; i < entries.Count; i++)
                    {
                        for (var j = i + 1; j < entries.Count; j++)
                        {
                            if (entries[i].Key.IsSelected && entries[j].Key.IsSelected)
                                continue;
                            AssertEx.IsFalse(entries[i].Value.IntersectsWith(entries[j].Value),
                                string.Format("Labels '{0}' and '{1}' overlap after pruning.",
                                    entries[i].Key.Label.Text, entries[j].Key.Label.Text));
                        }
                    }

                    // Marker centers are derived the same way the pruner derives them: from the
                    // integer marker rectangles reported by LineItem.GetCoords.
                    var markerCenters = new List<PointF>();
                    foreach (var line in pane.CurveList.OfType<LineItem>().Where(c => c.Symbol.Type != SymbolType.None))
                    {
                        for (var i = 0; i < line.Points.Count; i++)
                        {
                            if (!line.GetCoords(pane, i, out var coords))
                                continue;
                            var sides = Array.ConvertAll(coords.Split(','), int.Parse);
                            markerCenters.Add(new PointF((sides[0] + sides[2]) / 2f, (sides[1] + sides[3]) / 2f));
                        }
                    }

                    foreach (var entry in entries.Where(e => !e.Key.IsSelected))
                    {
                        var ownCenter = new PointF(pane.XAxis.Scale.Transform(entry.Key.Point.X),
                            pane.YAxis.Scale.Transform(entry.Key.Point.Y));
                        foreach (var center in markerCenters)
                        {
                            if (!entry.Value.Contains(center))
                                continue;
                            var dx = center.X - ownCenter.X;
                            var dy = center.Y - ownCenter.Y;
                            AssertEx.IsTrue(dx * dx + dy * dy <= OWN_MARKER_TOLERANCE_SQ,
                                string.Format("Label '{0}' covers a foreign point marker. " +
                                              "rect={1} center={2} ownCenter={3} chart={4} yScale={5}/{6} xScale={7}/{8}",
                                    entry.Key.Label.Text, entry.Value, center, ownCenter, pane.Chart.Rect,
                                    pane.YAxis.Scale.Min, pane.YAxis.Scale.Max, pane.XAxis.Scale.Min, pane.XAxis.Scale.Max));
                        }
                    }
                }
            });
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
