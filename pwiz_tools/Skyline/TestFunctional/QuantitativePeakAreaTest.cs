/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA *
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that the AreaReplicateGraphPane correctly displays or hides non-quantitative transitions
    /// </summary>
    [TestClass]
    public class QuantitativePeakAreaTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestQuantitativePeakAreas()
        {
            TestFilesZip = @"TestFunctional\QuantitativePeakAreaTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("QuantitativePeakAreaTest.sky"));
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.TransitionGroups, 0);
                SkylineWindow.ShowOnlyQuantitative(false);
            });
            WaitForGraphs();
            AreaReplicateGraphPane areaReplicateGraphPane;
            Assert.IsTrue(FindOpenForm<GraphSummary>().TryGetGraphPane(out areaReplicateGraphPane));
            var transitionGroup = SkylineWindow.Document.MoleculeTransitionGroups.FirstOrDefault();
            Assert.IsNotNull(transitionGroup);

            Assert.IsFalse(Settings.Default.ShowQuantitativeOnly);
            VerifyPeakAreas(transitionGroup.Transitions.ToList(), areaReplicateGraphPane.CurveList);

            RunUI(()=>SkylineWindow.ShowOnlyQuantitative(true));
            WaitForGraphs();
            VerifyPeakAreas(
                transitionGroup.Transitions.Where(t => t.IsQuantitative(SkylineWindow.Document.Settings)).ToList(),
                areaReplicateGraphPane.CurveList);
        }

        private void VerifyPeakAreas(IList<TransitionDocNode> transitions, CurveList curveList)
        {
            Assert.AreEqual(transitions.Count, curveList.Count);
            foreach (var transition in transitions)
            {
                var matchingCurve = curveList.FirstOrDefault(curve =>
                    ReferenceEquals(transition.Id, ((IdentityPath) curve.Tag).Child));
                Assert.IsNotNull(matchingCurve);
                var transitionChromInfo = transition.Results[0][0];
                Assert.AreEqual(transitionChromInfo.Area, matchingCurve.Points[0].Y);
            }
        }
    }
}
