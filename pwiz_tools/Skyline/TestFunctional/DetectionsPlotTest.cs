/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>, 
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DetectionsPlotTest : AbstractFunctionalTestEx
    {
        private static readonly int[][] REF_DATA =
        {
            new[] { 114, 113, 113, 112, 112, 113},       //q = 0.003, Peptides
            new[] { 123, 122, 122, 121, 121, 121},      //q = 0.003, Precursors
            new[] { 111, 109, 110, 110, 109, 110 },      //q = 0.001, Peptides
            new[] { 120, 118, 119, 119, 117, 117 },      //q = 0.001, Precursors
            new[] { 110, 108, 109, 109, 108, 109 },      //q = 0.001, Peptides, after update
            new[] { 119, 117, 118, 118, 116, 116 }      //q = 0.001, Precursors, after update
        };

        [TestMethod]
        public void TestDetectionsPlot()
        {
            TestFilesZip = @"TestFunctional/DetectionsPlotTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"DIA-TTOF-tutorial.sky"));
            WaitForDocumentLoaded();

            RunUI(() => { SkylineWindow.ShowDetectionsReplicateComparisonGraph(); });
            WaitForGraphs();

            GraphSummary graph = SkylineWindow.DetectionsPlot;
            var toolbar = graph.Toolbar as DetectionsToolbar;
            Assert.IsNotNull(toolbar);
            RunUI(() => { toolbar.CbLevel.SelectedItem = DetectionsGraphController.TargetType.PRECURSOR; });
            WaitForGraphs();

            DetectionsPlotPane pane;
            Assert.IsTrue(graph.TryGetGraphPane(out pane));
            Assert.IsTrue(pane.HasToolbar);

            //use properties dialog to update the q-value
            var propDialog = ShowDialog<DetectionToolbarProperties>(() =>
            {
                toolbar.pbProperties_Click(graph.GraphControl, new EventArgs());
            });

            //verify data correct for 2 q-values
            RunUI(() => propDialog.SetQValueTo(0.003f));
            OkDialog(propDialog, propDialog.OkDialog);
            WaitForCondition(() => (DetectionsGraphController.Settings.QValueCutoff == 0.003f));
            AssertDataCorrect(pane, 0, 0.003f);

            //use properties dialog to update the q-value
            propDialog = ShowDialog<DetectionToolbarProperties>(() =>
            {
                toolbar.pbProperties_Click(graph.GraphControl, new EventArgs());
            });
            RunUI(() => propDialog.SetQValueTo(0.001f));
            OkDialog(propDialog, propDialog.OkDialog);
            WaitForCondition(() => (DetectionsGraphController.Settings.QValueCutoff == 0.001f));
            AssertDataCorrect(pane, 2, 0.001f);

            //verify the number of the bars on the plot
            RunUI(() =>
            {
                Assert.IsTrue(
                    pane.CurveList[0].IsBar && pane.CurveList[0].Points.Count == REF_DATA[0].Length);
            });

            string[] tipText =
            {
                Resources.DetectionPlotPane_Tooltip_Replicate + TextUtil.SEPARATOR_TSV_STR + @"2_SW-B",
                string.Format(Resources.DetectionPlotPane_Tooltip_Count, DetectionsGraphController.TargetType.PRECURSOR) +
                TextUtil.SEPARATOR_TSV_STR + 118.ToString( CultureInfo.CurrentCulture),
                Resources.DetectionPlotPane_Tooltip_CumulativeCount + TextUtil.SEPARATOR_TSV_STR +
                123.ToString( CultureInfo.CurrentCulture),
                Resources.DetectionPlotPane_Tooltip_AllCount + TextUtil.SEPARATOR_TSV_STR +
                115.ToString( CultureInfo.CurrentCulture),
                Resources.DetectionPlotPane_Tooltip_QMedian + TextUtil.SEPARATOR_TSV_STR +
                (6.0d).ToString(@"F1",CultureInfo.CurrentCulture)
            };
            RunUI(() =>
            {
                Assert.IsNotNull(pane.ToolTip);
                pane.PopulateTooltip(1, null);
                //verify the tooltip text
                CollectionAssert.AreEqual(tipText, pane.ToolTip.TipLines);
            });

            //test the data correct after a doc change (delete peptide)
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 12);
                SkylineWindow.EditDelete();
            });
            WaitForGraphs();

            //verify that the cache is purged after the document update
            AssertDataCorrect(pane, 4, 0.001f);

            RunUI(() => { SkylineWindow.ShowDetectionsHistogramGraph(); });
            WaitForGraphs();
            DetectionsHistogramPane paneHistogram;
            var graphHistogram = SkylineWindow.DetectionsPlot;
            Assert.IsTrue(graphHistogram.TryGetGraphPane(out paneHistogram), "Cannot get histogram pane.");
            //display and hide tooltip
            string[] histogramTipText =
            {
                Resources.DetectionHistogramPane_Tooltip_ReplicateCount + TextUtil.SEPARATOR_TSV_STR +
                5.ToString( CultureInfo.CurrentCulture),
                String.Format(Resources.DetectionHistogramPane_Tooltip_Count, DetectionsGraphController.TargetType.PRECURSOR) +
                TextUtil.SEPARATOR_TSV_STR + 102.ToString( CultureInfo.CurrentCulture),
            };
            RunUI(() =>
            {
                Assert.IsNotNull(paneHistogram.ToolTip, "No tooltip found.");
                paneHistogram.PopulateTooltip(5, null);
                //verify the tooltip text
                CollectionAssert.AreEqual(histogramTipText, paneHistogram.ToolTip.TipLines);
            });
            RunUI(() =>
            {
                graph.Close();
                graphHistogram.Close();
            });
            WaitForGraphs();
        }

        private void AssertDataCorrect(DetectionsPlotPane pane, int refIndex, float qValue, bool record = false)
        {
            WaitForConditionUI(() => pane.CurrentData != null 
                                           && pane.CurrentData.QValueCutoff == qValue
                                           && pane.CurrentData.TryGetTargetData(DetectionsGraphController.TargetType.PEPTIDE, out _)
                                           && pane.CurrentData.TryGetTargetData(DetectionsGraphController.TargetType.PRECURSOR, out _),
                () => $"Retrieving data for qValue {qValue}, refIndex {refIndex} took too long.");
            WaitForGraphs();
            WaitForCondition(() => pane.CurrentData.IsValid);

            if (record)
            {
                Console.WriteLine(@"Peptides");
                pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PEPTIDE).TargetsCount
                    .ForEach((cnt) => { Console.Write($@"{cnt}, "); });
                Console.WriteLine(@"\nPrecursors");
                pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PRECURSOR).TargetsCount
                    .ForEach((cnt) => { Console.Write($@"{cnt}, "); });
            }

            Assert.IsTrue(
                REF_DATA[refIndex].SequenceEqual(
                    pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PEPTIDE).TargetsCount));
            Assert.IsTrue(
                REF_DATA[refIndex + 1].SequenceEqual(
                    pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PRECURSOR).TargetsCount));
        }
    }
}
