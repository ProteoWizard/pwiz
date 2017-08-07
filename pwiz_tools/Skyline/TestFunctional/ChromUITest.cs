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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ChromUIest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChromUI()
        {
            TestFilesZip = @"TestFunctional\ChromUITest.zip"; //Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestPointsAcrossPeak();
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
                VerifyRawTimesCount("Unrefined", 5, nodeTran, showing);
                VerifyRawTimesCount("1", 45, nodeTran, showing);
                VerifyRawTimesCount("2", 47, nodeTran, showing);
                VerifyRawTimesCount("3", 49, nodeTran, showing);
                VerifyRawTimesCount("4", 51, nodeTran, showing);
                VerifyRawTimesCount("5", 53, nodeTran, showing);
            }
        }

        private void VerifyRawTimesCount(string chromName, int fileId, TransitionDocNode transition, bool showing)
        {
            RunUI(() =>
            {
                int count = GetRawTimeCount(chromName);
                foreach (var tranChromInfo in transition.ChromInfos.Where(c => c.FileId.GlobalIndex == fileId))
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

            int count = 0;
            foreach (var graphObj in graphChrom.GraphItem.GraphObjList)
            {
                var objTag = graphObj.Tag as ChromGraphItem.GraphObjTag;
                if (objTag != null && objTag.GraphObjType == ChromGraphItem.GraphObjType.raw_time)
                    count++;
            }
            return count;
        }
    }
}
