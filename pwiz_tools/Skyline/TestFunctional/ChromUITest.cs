/*
 * Original author: Yuval Boss <yuval .at. uw.edu>,
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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ChromUIest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChromUI()
        {
            TestFilesZip = @"TestFunctional\ChromUITest.zip"; // Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestPointsAcrossPeak();
            TestOriginalPeak();
        }

        private void TestPointsAcrossPeak()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("WormUnrefined.sky"));
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.all);
                SkylineWindow.ArrangeGraphs(DisplayGraphsType.Tiled);
                SkylineWindow.ActivateReplicate("1");
            });
            WaitForDocumentLoaded();
            WaitForGraphs();

            var doc = SkylineWindow.Document;
            var pathToGroup = doc.GetPathTo((int) SrmDocument.Level.TransitionGroups, 0);
            RunUI(() => SkylineWindow.SelectPath(pathToGroup));
            WaitForGraphs();

            var nodeGroup = (TransitionGroupDocNode) doc.FindNode(pathToGroup);
            VerifyGraphItemsCorrect(nodeGroup, pathToGroup, true);
            RunUI(() => SkylineWindow.ToggleRawTimesMenuItem());
            WaitForGraphs();
            VerifyGraphItemsCorrect(nodeGroup, pathToGroup, false);
        }

        private void VerifyGraphItemsCorrect(TransitionGroupDocNode nodeGroup, IdentityPath pathToGroup, bool showing)
        {
            foreach (var nodeTran in nodeGroup.Transitions)
            {
                var tranId = nodeTran.Id;
                RunUI(() => SkylineWindow.SelectedPath = new IdentityPath(pathToGroup, tranId));
                WaitForGraphs();
                VerifyRawTimesCount("Unrefined", nodeTran, showing);
                VerifyRawTimesCount("1", nodeTran, showing);
                VerifyRawTimesCount("2", nodeTran, showing);
                VerifyRawTimesCount("3", nodeTran, showing);
                VerifyRawTimesCount("4", nodeTran, showing);
                VerifyRawTimesCount("5", nodeTran, showing);
            }
        }

        private void VerifyRawTimesCount(string chromName, TransitionDocNode transition, bool showing)
        {
            int resultIndex;
            ChromatogramSet chromSet;
            Assert.IsTrue(SkylineWindow.Document.Settings.MeasuredResults.TryGetChromatogramSet(chromName, out chromSet, out resultIndex));

            RunUI(() =>
            {
                int count = GetRawTimeCount(chromName);
                foreach (var tranChromInfo in transition.Results[resultIndex])
                {
                    int pointsCount = showing 
                        ? tranChromInfo.PointsAcrossPeak ?? 0
                        : 0;
                    Assert.AreEqual(pointsCount, count);
                }
            });
        }

        private static int GetRawTimeCount(string chromName)
        {
            var graphChrom = SkylineWindow.GetGraphChrom(chromName);
            // Graph objects will not be present when the program is off screen
            if (!graphChrom.GraphItem.GraphObjList.Any() && Program.SkylineOffscreen)
                return graphChrom.GraphItems.Sum(g => g.RawTimesCount);

            int count = 0;
            foreach (var graphObj in graphChrom.GraphItem.GraphObjList)
            {
                var objTag = graphObj.Tag as ChromGraphItem.GraphObjTag;
                if (objTag != null && objTag.GraphObjType == ChromGraphItem.GraphObjType.raw_time)
                    count++;
            }
            return count;
        }

        private void TestOriginalPeak()
        {
            RunUI(() =>
            {
                SkylineWindow.ActivateReplicate("1");
            });
            WaitForGraphs();
            var graphChrom = SkylineWindow.GetGraphChrom("1");
            // If the entire graph object list may be empty when the graph is offscreen
            // It is not worth it to make this work.
            if (!graphChrom.GraphItem.GraphObjList.Any() && Program.SkylineOffscreen)
                return;

            var norect = GetChromRect(graphChrom);
            Assert.IsNull(norect);
            RunUI(() =>
            {
                var pathPep = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                var nodeGroup = SkylineWindow.DocumentUI.Peptides.ElementAt(0).TransitionGroups.First();
                var listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(new IdentityPath(pathPep, nodeGroup.TransitionGroup),
                        null,
                        graphChrom.NameSet,
                        graphChrom.ChromGroupInfos[0].FilePath,
                        graphChrom.GraphItems.First().GetNearestDisplayTime(40.0),
                        graphChrom.GraphItems.First().GetNearestDisplayTime(42.0),
                        PeakIdentification.ALIGNED,
                        PeakBoundsChangeType.both)
                };
                graphChrom.SimulateChangedPeakBounds(listChanges);
            });

            WaitForGraphs();
            var rect = GetChromRect(graphChrom);
            Assert.IsNotNull(rect);
            var tag = (ChromGraphItem.GraphObjTag)rect.Tag;
            Assert.AreEqual(tag.StartTime.DisplayTime.ToString("N2"), 40.31.ToString(CultureInfo.CurrentCulture));
            Assert.AreEqual(tag.EndTime.DisplayTime.ToString("N2"), 41.21.ToString(CultureInfo.CurrentCulture));
            Assert.IsNull(GetChromRect(SkylineWindow.GetGraphChrom("2")));
            Assert.IsNull(GetChromRect(SkylineWindow.GetGraphChrom("3")));
            Assert.IsNull(GetChromRect(SkylineWindow.GetGraphChrom("4")));
        }

        private static GraphObj GetChromRect(GraphChromatogram graphChrom)
        {
            return graphChrom.GraphItem.GraphObjList.FirstOrDefault(obj =>
            {
                var objTag = obj.Tag as ChromGraphItem.GraphObjTag;
                return objTag != null && objTag.GraphObjType == ChromGraphItem.GraphObjType.original_peak_shading;
            });
        }
    }
}
