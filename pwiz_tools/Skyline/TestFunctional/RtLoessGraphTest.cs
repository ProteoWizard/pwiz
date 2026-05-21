/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the RT LOESS curves graph "Show" menu additions: the independent Legend and
    /// Peptides toggles, and selecting a peptide by clicking one of its points.
    /// </summary>
    [TestClass]
    public class RtLoessGraphTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRtLoessGraph()
        {
            TestFilesZip = @"TestFunctional\MedianPolishSmallTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var skyFile = Directory.GetFiles(TestFilesDir.FullPath, "*.sky").First();
            RunUI(() => SkylineWindow.OpenFile(skyFile));
            WaitForDocumentLoaded();

            RunUI(() =>
            {
                SkylineWindow.SetRtLoessShowValue(RtLoessShowValue.Median);
                SkylineWindow.ShowPeakAreaRtLoessGraph();
            });
            // Wait until the RT LOESS curves have been computed and drawn (replicate curves are
            // tagged with their replicate index).
            WaitForConditionUI(() => TryGetRtLoessPane(out var pane) && pane.CurveList.Any(c => c.Tag is int));

            VerifyLegendToggle();
            VerifyPeptidesToggleAndSelection();
        }

        private void VerifyLegendToggle()
        {
            RunUI(() => SkylineWindow.SetRtLoessShowLegend(false));
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(TryGetRtLoessPane(out var pane));
                Assert.IsFalse(pane.Legend.IsVisible);
            });

            RunUI(() => SkylineWindow.SetRtLoessShowLegend(true));
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(TryGetRtLoessPane(out var pane));
                Assert.IsTrue(pane.Legend.IsVisible);
            });
        }

        private void VerifyPeptidesToggleAndSelection()
        {
            // With Peptides off there is no Peptides curve.
            RunUI(() => SkylineWindow.SetRtLoessShowPeptides(false));
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(TryGetRtLoessPane(out var pane));
                Assert.IsNull(FindPeptidesCurve(pane));
            });

            // Turning Peptides on adds a scatter curve whose points are tagged with the
            // IdentityPath of the peptide they belong to.
            RunUI(() => SkylineWindow.SetRtLoessShowPeptides(true));
            WaitForGraphs();
            WaitForConditionUI(() => TryGetRtLoessPane(out var pane) && FindPeptidesCurve(pane) != null);

            RunUI(() =>
            {
                Assert.IsTrue(TryGetRtLoessPane(out var pane));
                var peptidesCurve = FindPeptidesCurve(pane);
                Assert.IsNotNull(peptidesCurve);
                Assert.IsTrue(peptidesCurve.Points.Count > 0);

                var document = SkylineWindow.Document;
                for (int i = 0; i < peptidesCurve.Points.Count; i++)
                {
                    var identityPath = peptidesCurve.Points[i].Tag as IdentityPath;
                    Assert.IsNotNull(identityPath);
                    Assert.IsInstanceOfType(document.FindNode(identityPath), typeof(PeptideDocNode));
                }

                // Clicking a peptide point selects that peptide in the Targets tree.
                var point = peptidesCurve.Points[0];
                var expectedPath = (IdentityPath) point.Tag;
                var screenPt = pane.GeneralTransform(new PointF((float) point.X, (float) point.Y),
                    CoordType.AxisXYScale);
                pane.HandleMouseClick(SkylineWindow.GraphPeakArea.GraphControl,
                    new MouseEventArgs(MouseButtons.Left, 1, (int) screenPt.X, (int) screenPt.Y, 0));
                Assert.AreEqual(expectedPath, SkylineWindow.SelectedPath);
            });
        }

        private static bool TryGetRtLoessPane(out AreaRtLoessGraphPane pane)
        {
            pane = null;
            var graph = SkylineWindow.GraphPeakArea;
            return graph != null && graph.TryGetGraphPane(out pane);
        }

        private static CurveItem FindPeptidesCurve(AreaRtLoessGraphPane pane)
        {
            return pane.CurveList.FirstOrDefault(
                curve => Equals(curve.Label.Text, GraphsResources.AreaRtLoessGraphPane_Peptides));
        }
    }
}
