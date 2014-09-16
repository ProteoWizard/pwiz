/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ArrangeGraphsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestArrangeGraphs()
        {
            TestFilesZip = @"TestFunctional\ArrangeGraphsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string documentPath = TestFilesDir.GetTestPath("11-16-10mz_manual.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));

            // Tests that elements show up in correct groups and order as type "tiled" in grouped window.
            RunGroupDlg(9, DisplayGraphsType.Tiled, GroupGraphsType.separated, GroupGraphsOrder.Acquired_Time);
            List<GraphChromatogram> graphChromatograms = SkylineWindow.GraphChromatograms.ToList();
            var beginningWidth = 0;
            var beginningHeight = 0;
            RunUI(() =>
            {
                beginningWidth = graphChromatograms.First().Parent.Parent.Parent.Width;
                beginningHeight = graphChromatograms.First().Parent.Parent.Parent.Height;
                // Need to set dimensions of panel containing chromatograms so dimensions get set the same each run.
                // Dimension are set to those I had in my settings when I build the test. (Yuval)
                graphChromatograms.First().Parent.Parent.Parent.Height = 443;
                graphChromatograms.First().Parent.Parent.Parent.Width = 736;
            });
            Assert.IsTrue(graphChromatograms.Count == 27);
            var dictGraphPositions = GetDictionaryGraphChromatogram();
            foreach (KeyValuePair<Point, List<GraphChromatogram>> dictGraphGroup in dictGraphPositions)
            {
                Assert.IsTrue(dictGraphGroup.Value.Count == 3);
            }
            List<int> order = new List<int>();
            for (var i = 0; i < beginningWidth; i++)
            {
                if (dictGraphPositions.ContainsKey(new Point(i, 1)))
                {
                    foreach (GraphChromatogram graph in dictGraphPositions[new Point(i, 1)])
                    {
                        order.Add(int.Parse(graph.NameSet));
                    }
                }
            }
            for (var i = 0; i < beginningWidth; i++)
            {
                if (dictGraphPositions.ContainsKey(new Point(i,128)))
                {
                    foreach (GraphChromatogram graph in dictGraphPositions[new Point(i,128)])
                    {
                        order.Add(int.Parse(graph.NameSet));
                    }
                }
            }
            var sortedList = order;
            sortedList.Sort();
            Assert.IsTrue(sortedList == order);
            Assert.IsTrue(order.Count == 27);

            // Test distributed feature in grouped window.
            RunGroupDlg(9, DisplayGraphsType.Column, GroupGraphsType.distributed, GroupGraphsOrder.Acquired_Time);
            dictGraphPositions = GetDictionaryGraphChromatogram();
            var orderedByPosition = new Dictionary<Point, List<GraphChromatogram>>();
            for (int i = 0; i < beginningWidth; i++)
            {
                if (dictGraphPositions.ContainsKey(new Point(i, 1)))
                {
                    orderedByPosition.Add(new Point(i, 1), dictGraphPositions[new Point(i, 1)]);
                }
            }
            int val1 = 0;
            int val2 = 0;
            int val3 = 0;
            foreach (KeyValuePair<Point, List<GraphChromatogram>> dictGraphGroup in orderedByPosition)
            {
                var values = new List<int> { int.Parse(dictGraphGroup.Value[0].NameSet), int.Parse(dictGraphGroup.Value[1].NameSet), int.Parse(dictGraphGroup.Value[2].NameSet) };
                values.Sort();
                if (val1 != 0)
                {
                    Assert.IsTrue(val1 < values[0]);
                    Assert.IsTrue(val2 < values[1]);
                    Assert.IsTrue(val3 < values[2]);
                }
                val1 = values[0];
                val2 = values[1];
                val3 = values[2];
            }

            // Test row functionality through grouped window.
            RunGroupDlg(9, DisplayGraphsType.Row, GroupGraphsType.separated, GroupGraphsOrder.Acquired_Time);
            dictGraphPositions = GetDictionaryGraphChromatogram();
            foreach (KeyValuePair<Point, List<GraphChromatogram>> dictGraphGroup in dictGraphPositions)
            {
                Assert.IsTrue(dictGraphGroup.Value.Count == 3);
                Assert.IsTrue(dictGraphGroup.Key.Y == 1);
            }

            // Tests column functionality through grouped window.
            RunGroupDlg(9, DisplayGraphsType.Column, GroupGraphsType.separated, GroupGraphsOrder.Acquired_Time);
            dictGraphPositions = GetDictionaryGraphChromatogram();
            foreach (KeyValuePair<Point, List<GraphChromatogram>> dictGraphGroup in dictGraphPositions)
            {
                Assert.IsTrue(dictGraphGroup.Value.Count == 3);
                Assert.IsTrue(dictGraphGroup.Key.X == 1);
            }

            // Test View > Arange Graphs > Row
            RunUI(() => SkylineWindow.ArrangeGraphs(DisplayGraphsType.Row));
            dictGraphPositions = GetDictionaryGraphChromatogram();
            foreach (KeyValuePair<Point, List<GraphChromatogram>> dictGraphGroup in dictGraphPositions)
            {
                Assert.IsTrue(dictGraphGroup.Value.Count == 1);
                Assert.IsTrue(dictGraphGroup.Key.Y == 1);
            }

            // Test View > Arange Graphs > Column
            RunUI(() => SkylineWindow.ArrangeGraphs(DisplayGraphsType.Column));
            dictGraphPositions = GetDictionaryGraphChromatogram();
            foreach (KeyValuePair<Point, List<GraphChromatogram>> dictGraphGroup in dictGraphPositions)
            {
                Assert.IsTrue(dictGraphGroup.Value.Count == 1);
                Assert.IsTrue(dictGraphGroup.Key.X == 1);
            }
            RunUI(() =>
            {
                // Sets dimesnions back to what they were at the beginning;
                graphChromatograms.First().Parent.Parent.Parent.Height = beginningHeight;
                graphChromatograms.First().Parent.Parent.Parent.Width = beginningWidth;
            });
        }

        // Returns a dictionary of all groups and their containing chromatograms.  Key is top-left point of window.
        private Dictionary<Point, List<GraphChromatogram>> GetDictionaryGraphChromatogram()
        {
            List<GraphChromatogram> graphChromatograms = SkylineWindow.GraphChromatograms.ToList();
            var dictGraphPositions = new Dictionary<Point, List<GraphChromatogram>>();
            foreach (var graphChrom in graphChromatograms)
            {
                Point ptLeftTop = GetTopLeft(graphChrom.Parent);
                if (dictGraphPositions.ContainsKey(ptLeftTop))
                {
                    List<GraphChromatogram> temp = dictGraphPositions[ptLeftTop];
                    temp.Add(graphChrom);
                    dictGraphPositions.Remove(ptLeftTop);
                    dictGraphPositions.Add(ptLeftTop, temp);
                }
                else
                {
                    dictGraphPositions.Add(ptLeftTop, new List<GraphChromatogram> { graphChrom });
                }
            }
            
            return dictGraphPositions;
        }
         
        private static Point GetTopLeft(Control control)
        {
            return new Point(control.Left, control.Top);
        }

        private void RunGroupDlg(int numberOfGroups, DisplayGraphsType displayType, GroupGraphsType groupType, GroupGraphsOrder orderGraphs)
        {
            RunDlg<ArrangeGraphsGroupedDlg>(SkylineWindow.ArrangeGraphsGrouped, dlg =>
            {
                dlg.Groups = numberOfGroups;
                dlg.DisplayType = displayType;
                dlg.GroupType = groupType;
                dlg.GroupOrder = orderGraphs;
                dlg.OkDialog();
            });
        }
    }
}
