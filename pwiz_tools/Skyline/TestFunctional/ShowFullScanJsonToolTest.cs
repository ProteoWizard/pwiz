/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Exercises the AI Connector's ShowFullScan: the Full-Scan graph shows the scan at a retention time, the
    /// chromatogram's selection circle is on the transition asked for or the most intense one, and the graph
    /// is zoomed to that transition either way.
    /// </summary>
    [TestClass]
    public class ShowFullScanJsonToolTest : AbstractFunctionalTestEx
    {
        private const string REPLICATE_NAME = "ID12692_01_UCA168_3727_040714";
        private const double APEX_TIME = 33.23;

        [TestMethod]
        public void TestShowFullScanJsonTool()
        {
            Run(@"TestData\Results\BlibDriftTimeTest.zip");
        }

        protected override void DoTest()
        {
            Settings.Default.ShowFullScan = false;
            OpenDocument("BlibDriftTimeTest.sky");
            ImportResults(REPLICATE_NAME + ExtensionTestContext.ExtMz5);
            FindNode("453");
            WaitForGraphs();

            using var server = new JsonToolServer(@"test-" + Guid.NewGuid());
            var startLocator = server.GetSelection().Locators.Single();
            IdentityPath precursorPath = null;
            RunUI(() =>
            {
                var selectedPath = SkylineWindow.SelectedPath;
                precursorPath = SkylineWindow.Document.FindNode(selectedPath) is PeptideDocNode peptide
                    ? new IdentityPath(selectedPath, peptide.TransitionGroups.First().Id)
                    : selectedPath;
            });
            Assert.IsInstanceOfType(SkylineWindow.Document.FindNode(precursorPath), typeof(TransitionGroupDocNode));

            // Without a transition: the circle is on the transition most intense at that time, the selection is
            // left alone, and the graph is zoomed to that transition even when the user had turned zooming off.
            RunUI(() => Settings.Default.AutoZoomFullScanGraph = false);
            var expectedTransitionId = GetMostIntenseTransitionId(APEX_TIME);
            ShowFullScan(server, APEX_TIME, null);
            AssertSelectedScan(expectedTransitionId);
            RunUI(() =>
            {
                Assert.AreEqual(APEX_TIME, SkylineWindow.SelectedScanRetentionTime, 0.05);
                Assert.IsTrue(Settings.Default.AutoZoomFullScanGraph);
            });
            Assert.AreEqual(startLocator, server.GetSelection().Locators.Single());

            // With a transition, here one that is not the most intense: the circle is on it, and it is selected.
            var precursor = (TransitionGroupDocNode) SkylineWindow.Document.FindNode(precursorPath);
            var otherTransition = precursor.Transitions.First(t => !ReferenceEquals(t.Id, expectedTransitionId));
            RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(precursorPath, otherTransition.Id));
            string transitionLocator = server.GetSelection().Locators.Single();
            RunUI(() => SkylineWindow.SelectedPath = precursorPath);
            string precursorLocator = server.GetSelection().Locators.Single();
            ShowFullScan(server, APEX_TIME, transitionLocator);
            AssertSelectedScan(otherTransition.Id);
            RunUI(() => Assert.IsTrue(Settings.Default.AutoZoomFullScanGraph));
            Assert.AreEqual(transitionLocator, server.GetSelection().Locators.Single());

            // Errors: a time outside the chromatogram, a locator that is not a transition, an unknown replicate
            AssertEx.ThrowsException<ArgumentException>(() => server.ShowFullScan(1000));
            AssertEx.ThrowsException<ArgumentException>(() => server.ShowFullScan(APEX_TIME, null, precursorLocator));
            AssertEx.ThrowsException<ArgumentException>(() => server.ShowFullScan(APEX_TIME, @"NoSuchReplicate"));
        }

        private void ShowFullScan(JsonToolServer server, double retentionTime, string transitionLocator)
        {
            var result = server.ShowFullScan(retentionTime, null, transitionLocator);
            AssertEx.IsTrue(result.Completed, @"ShowFullScan should complete: " + result.Message);
            // The completed call describes what the graph shows, so the caller needs no image
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        /// <summary>
        /// The chromatogram's selection circle is on <paramref name="transitionId"/>. ShowFullScan returns only
        /// after the spectrum it loads in the background has loaded, so this needs no wait.
        /// </summary>
        private static void AssertSelectedScan(Identity transitionId)
        {
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.GraphFullScan.IsLoaded);
                Assert.AreSame(transitionId, SkylineWindow.SelectedScanTransition);
            });
        }

        /// <summary>
        /// The transition whose displayed chromatogram is most intense at <paramref name="retentionTime"/>.
        /// </summary>
        private static Identity GetMostIntenseTransitionId(double retentionTime)
        {
            Identity mostIntenseId = null;
            RunUI(() =>
            {
                var graphChrom = SkylineWindow.GetGraphChrom(REPLICATE_NAME);
                double maxIntensity = double.MinValue;
                foreach (var pane in graphChrom.GraphControl.MasterPane.PaneList)
                {
                    foreach (var curve in graphChrom.GetCurves(pane))
                    {
                        var graphItem = (ChromGraphItem) curve.Tag;
                        var points = graphItem.Points;
                        if (graphItem.TransitionNode == null || points.Count == 0)
                            continue;
                        double intensity = points[graphItem.GetNearestMeasuredIndex(retentionTime)].Y;
                        if (intensity > maxIntensity)
                        {
                            maxIntensity = intensity;
                            mostIntenseId = graphItem.TransitionNode.Id;
                        }
                    }
                }
            });
            Assert.IsNotNull(mostIntenseId);
            return mostIntenseId;
        }
    }
}
