/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class WaitForConditionHangTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestWaitForConditionHang()
        {
            TestFilesZipPaths = new[]
            {
                @"TestFunctional\WaitForConditionHangTest.zip",
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string pepOfInterest1 = "FYNELTEILVR";
            const string pepOfInterest2 = "KADLVNR";
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("WaitForConditionHangTest.sky"));
                var selectedNode = SkylineWindow.SelectedNode;
                Assert.IsInstanceOfType(selectedNode, typeof(PeptideTreeNode));
                Assert.AreEqual(pepOfInterest1, GetSelectedPeptide());
            });
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                SkylineWindow.SetAreaProteinTargets(true);
                SkylineWindow.ShowSingleReplicate();
                SkylineWindow.ShowProductTransitions();
            });
            Assert.AreEqual(pepOfInterest1, CallUI(GetSelectedPeptide));
            SelectPeptide(pepOfInterest2);
            RunUI(()=>
            {
                SkylineWindow.SelectedResultsIndex = 0;
                SkylineWindow.AutoZoomBestPeak();
            });
            WaitForGraphs();
            RunUI(() =>
            {
                // Reduce the number of updates to this graph by setting directly to settings
                // and then showing the graph.
                Settings.Default.RtCalculatorOption = new RtCalculatorOption.Library("ExtracellularVesicalMagNet");
                RTGraphController.RegressionMethod = RegressionMethodRT.loess;
                SkylineWindow.ShowRTRegressionGraphScoreToRun();
            });
            WaitForRTRegressionComplete();
            RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
            WaitForRTRegressionComplete();
            RunUI(() => SkylineWindow.SelectedResultsIndex = 1);
            WaitForRTRegressionComplete();
        }

        private void WaitForRTRegressionComplete()
        {
            var scoreToRunGraphPane = GetScoreToRunGraphPane();
            Assert.IsNotNull(scoreToRunGraphPane);
            bool[] isCompleted = new bool[1];
            var thread = Thread.CurrentThread;
            var allowedTime = TimeSpan.FromMinutes(2);
            CommonActionUtil.RunAsync(() =>
            {
                InterruptThreadAfter(isCompleted, thread, allowedTime);
            });
            try
            {
                WaitForConditionUI(() => !scoreToRunGraphPane.IsCalculating);
                lock (isCompleted)
                {
                    isCompleted[0] = true;
                    Monitor.Pulse(isCompleted);
                }
            }
            catch (ThreadInterruptedException tei)
            {
                throw new AssertFailedException(string.Format("Failed waiting {0} for graph pane", allowedTime), tei);
            }

        }

        private void InterruptThreadAfter(bool[] boolHolder, Thread thread, TimeSpan timeSpan)
        {
            lock (boolHolder)
            {
                if (!boolHolder[0])
                {
                    Monitor.Wait(boolHolder, timeSpan);
                }

                if (!boolHolder[0])
                {
                    thread.Interrupt();
                }
            }
        }

        public static RTLinearRegressionGraphPane GetScoreToRunGraphPane()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().Select(graphSummary => graphSummary.GraphControl.GraphPane)
                .OfType<RTLinearRegressionGraphPane>().FirstOrDefault(graphPane => !graphPane.RunToRun);
        }

        private void WaitForRelativeAbundanceComplete()
        {
            var summary = FindGraphSummaryByGraphType<SummaryRelativeAbundanceGraphPane>();
            Assert.IsNotNull(summary);
            WaitForPaneCondition<SummaryRelativeAbundanceGraphPane>(summary, pane => pane.IsComplete);
        }

        private static void SelectPeptide(string sequence)
        {
            RunUI(() =>
            {
                var proteinNode = SkylineWindow.SequenceTree.GetNodeOfType<PeptideGroupTreeNode>();
                Assert.IsNotNull(proteinNode);
                var peptideNode = proteinNode.Nodes.OfType<PeptideTreeNode>()
                    .First(node => node.DocNode.Peptide.Sequence == sequence);
                SkylineWindow.SelectedPath = peptideNode.Path;
            });
        }

        private string GetSelectedPeptide()
        {
            return SkylineWindow.SequenceTree.GetNodeOfType<PeptideTreeNode>()?.DocNode.Peptide.Sequence;
        }
    }
}
