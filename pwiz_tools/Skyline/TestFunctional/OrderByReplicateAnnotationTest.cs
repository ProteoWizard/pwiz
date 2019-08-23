/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class OrderByReplicateAnnotationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOrderByReplicateAnnotation()
        {
            TestFilesZip = @"TestFunctional\OrderByReplicateAnnotationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("OrderByReplicateAnnotation.sky")));
            RunUI(()=>SkylineWindow.ShowPeakAreaReplicateComparison());
            var peakAreaGraph = FormUtil.OpenForms.OfType<GraphSummary>().First(graph =>
                graph.Type == GraphTypeSummary.replicate && graph.Controller is AreaGraphController);
            OrderBy(peakAreaGraph, (index, item)=>item.Text == ColumnCaptions.AnalyteConcentration);
            VerifyOrder(peakAreaGraph, chromSet=>chromSet.AnalyteConcentration);
            OrderBy(peakAreaGraph, (index, item)=>item.Text == @"ReplicateAnnotation");
            VerifyOrder(peakAreaGraph, chromSet => chromSet.Annotations.GetAnnotation("ReplicateAnnotation"));
            OrderBy(peakAreaGraph, (index, item) => item.Text == @"RandomNumber");
            VerifyOrder(peakAreaGraph, chromSet=>Convert.ToDouble(chromSet.Annotations.GetAnnotation("RandomNumber"), CultureInfo.InvariantCulture));
            // Order by document
            OrderBy(peakAreaGraph, (index, item)=>index == 0);
            var measuredResults = SkylineWindow.Document.Settings.MeasuredResults;
            VerifyOrder(peakAreaGraph, chromSet=>measuredResults.Chromatograms.IndexOf(chromSet));
            // Order by acquired time
            OrderBy(peakAreaGraph, (index, item)=>index == 1);
            VerifyOrder(peakAreaGraph, chromSet=>chromSet.MSDataFileInfos.First().RunStartTime);
        }

        private void OrderBy(GraphSummary graphSummary, Func<int, ToolStripMenuItem, bool> menuItemPredicate)
        {
            RunUI(() =>
            {
                graphSummary.GraphControl.ContextMenuStrip.Show(graphSummary.GraphControl, new Point(1, 1));
                var orderByItem = SkylineWindow.ReplicateOrderContextMenuItem;
                for (int index = 0; index < orderByItem.DropDownItems.Count; index++)
                {
                    var item = orderByItem.DropDownItems[index] as ToolStripMenuItem;
                    if (item == null)
                    {
                        continue;
                    }
                    
                    var predicate = menuItemPredicate(index, item);
                    if (predicate)
                    {
                        item.PerformClick();
                        return;
                    }
                }
                Assert.Fail();
            });
        }

        private void VerifyOrder<T>(GraphSummary graphSummary, Func<ChromatogramSet, T> getValueFunc)
        {
            var document = graphSummary.DocumentUIContainer.Document;
            var measuredResults = document.Settings.MeasuredResults;
            Assert.IsNotNull(measuredResults);
            var summaryReplicateGraphPane = graphSummary.GraphControl.GraphPane as SummaryReplicateGraphPane;
            Assert.IsNotNull(summaryReplicateGraphPane);
            Assert.IsNotNull(summaryReplicateGraphPane);
            int replicateCount = measuredResults.Chromatograms.Count;
            Assert.AreEqual(replicateCount, summaryReplicateGraphPane.XAxis.Scale.TextLabels.Length);
            var replicates = new List<ChromatogramSet>();
            for (int i = 0; i < replicateCount; i++)
            {
                var replicateIndexSet = summaryReplicateGraphPane.IndexOfReplicate(i);
                Assert.AreEqual(1, replicateIndexSet.Count);
                replicates.Add(measuredResults.Chromatograms[replicateIndexSet.First()]);
            }
            for (int i = 1; i < replicates.Count; i++)
            {
                T prevValue = getValueFunc(replicates[i - 1]);
                T curValue = getValueFunc(replicates[i]);
                var comparison = Comparer<T>.Default.Compare(prevValue, curValue);
                Assert.IsTrue(comparison <= 0);
            }
        }
    }
}
