using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace TestPerf
{
    [TestClass]
    public class DetectionsPlotTest : AbstractFunctionalTestEx
    {
        private static readonly int[][] REF_DATA =
        {
            new[] {132, 131, 131, 130, 130, 132},       //q = 0.003, Peptides
            new[] { 142, 141, 141, 140, 140, 141},      //q = 0.003, Precursors
            new[] { 129, 127, 128, 127, 127, 128 },      //q = 0.001, Peptides
            new[] { 139, 137, 138, 137, 136, 136 },      //q = 0.001, Precursors
            new[] { 128, 126, 127, 126, 126, 127 },      //q = 0.001, Peptides, after update
            new[] { 138, 136, 137, 136, 135, 135 }      //q = 0.001, Precursors, after update
        };

        private static readonly string[] TIP_TEXT =
        {
            Resources.DetectionPlotPane_Tooltip_Replicate + TextUtil.SEPARATOR_TSV_STR + @"2_SW-B",
            string.Format(Resources.DetectionPlotPane_Tooltip_Count, DetectionsGraphController.TargetType.PRECURSOR) + 
            TextUtil.SEPARATOR_TSV_STR + 137.ToString( CultureInfo.CurrentCulture),
            Resources.DetectionPlotPane_Tooltip_CumulativeCount + TextUtil.SEPARATOR_TSV_STR + 
            143.ToString( CultureInfo.CurrentCulture),
            Resources.DetectionPlotPane_Tooltip_AllCount + TextUtil.SEPARATOR_TSV_STR + 
            133.ToString( CultureInfo.CurrentCulture),
            Resources.DetectionPlotPane_Tooltip_QMedian + TextUtil.SEPARATOR_TSV_STR + 
            6.0f.ToString(@"F1",CultureInfo.CurrentCulture)
        };
        private static readonly string[] HISTOGRAM_TIP_TEXT =
        {
            Resources.DetectionHistogramPane_Tooltip_ReplicateCount + TextUtil.SEPARATOR_TSV_STR + 
            5.ToString( CultureInfo.CurrentCulture),
            String.Format(Resources.DetectionHistogramPane_Tooltip_Count, DetectionsGraphController.TargetType.PRECURSOR) + 
            TextUtil.SEPARATOR_TSV_STR + 119.ToString( CultureInfo.CurrentCulture),
        };

        [TestMethod]
        public void TestDetectionsPlot()
        {
            RunPerfTests = true;
            TestFilesPersistent = new[] {"."}; // All persistent. No saving
            TestFilesZip = @"http://proteome.gs.washington.edu/software/test/skyline-perf/DetectionPlotTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"DIA-TTOF-tutorial.sky"));

            Trace.WriteLine(this.GetType().Name + ": Test started.");

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
            RunUI(() =>
            {
                propDialog.SetQValueTo(0.003f);
                Trace.WriteLine(this.GetType().Name + ": set Q-Value to 0.003");
            });
            OkDialog(propDialog, propDialog.OkDialog);
            Trace.WriteLine(this.GetType().Name + ": properties dialog for Q-Value to 0.003 has been processed.");
            WaitForCondition(() => (DetectionsGraphController.Settings.QValueCutoff == 0.003f));
            AssertDataCorrect(pane, 0, 0.003f);

            //use properties dialog to update the q-value
            propDialog = ShowDialog<DetectionToolbarProperties>(() =>
            {
                toolbar.pbProperties_Click(graph.GraphControl, new EventArgs());
            });
            RunUI(() =>
            {
                propDialog.SetQValueTo(0.001f);
                Trace.WriteLine(this.GetType().Name + ": set Q-Value to 0.001");
            });
            OkDialog(propDialog, propDialog.OkDialog);
            WaitForCondition(() => (DetectionsGraphController.Settings.QValueCutoff == 0.001f));
            AssertDataCorrect(pane, 2, 0.001f);

            //verify the number of the bars on the plot
            RunUI(() =>
            {
            Assert.IsTrue(
                pane.CurveList[0].IsBar && pane.CurveList[0].Points.Count == REF_DATA[0].Length);
            });

            Trace.WriteLine(this.GetType().Name + ": Display and hide tooltip");
            //display and hide tooltip
            var evt = FindBarPoint(pane, graph, 1);
            RunUI(() =>
            {
                pane.HandleMouseMoveEvent(graph.GraphControl, evt);
                Assert.IsNotNull(pane.ToolTip);
                WaitForConditionUI(() => pane.ToolTip.IsVisible);
                //verify the tooltip text
                CollectionAssert.AreEqual(TIP_TEXT, pane.ToolTip.TipLines);
                pane.HandleMouseMoveEvent(graph.GraphControl, new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0));
                Assert.IsFalse(pane.ToolTip.IsVisible);
            });

            Trace.WriteLine(this.GetType().Name + ": deleting a peptide");
            //test the data correct after a doc change (delete peptide)
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 12);
                SkylineWindow.EditDelete();
            });
            WaitForGraphs();
            WaitForConditionUI(() => DetectionPlotData.DataCache.Datas.Any((dat) =>
                    ReferenceEquals(SkylineWindow.DocumentUI, dat.Document) &&
                    DetectionsGraphController.Settings.QValueCutoff == dat.QValueCutoff),
                "Cache is not updated on document change.");

            //verify that the cache is purged after the document update
            RunUI(() =>
            {
                Assert.IsTrue(DetectionPlotData.DataCache.Datas.All((dat) =>
                    ReferenceEquals(SkylineWindow.DocumentUI, dat.Document)));
            });
            AssertDataCorrect(pane, 4, 0.001f);

            Trace.WriteLine(this.GetType().Name + ": showing a histogram pane.");
            RunUI(() => { SkylineWindow.ShowDetectionsHistogramGraph(); });
            WaitForGraphs();
            DetectionsHistogramPane paneHistogram;
            var graphHistogram = SkylineWindow.DetectionsPlot;
            Assert.IsTrue(graphHistogram.TryGetGraphPane(out paneHistogram), "Cannot get histogram pane.");
            //display and hide tooltip
            evt = FindBarPoint(paneHistogram, graphHistogram, 5);
            Trace.WriteLine(this.GetType().Name + ": showing histogram tooltip.");
            RunUI(() =>
            {
                paneHistogram.HandleMouseMoveEvent(graphHistogram.GraphControl, evt);
                Assert.IsNotNull(paneHistogram.ToolTip, "No tooltip found.");
                WaitForConditionUI(() => paneHistogram.ToolTip.IsVisible, "Tooltip is not visible.");
                //verify the tooltip text
                CollectionAssert.AreEqual(HISTOGRAM_TIP_TEXT, paneHistogram.ToolTip.TipLines);
                
                paneHistogram.HandleMouseMoveEvent(graphHistogram.GraphControl, new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0));
                Assert.IsFalse(paneHistogram.ToolTip.IsVisible);
            });
            RunUI(() =>
            {
                graph.Close();
                graphHistogram.Close();
            });
            WaitForGraphs();
            Trace.WriteLine(this.GetType().Name + ": Test complete.");
        }

        private void AssertDataCorrect(DetectionsPlotPane pane, int refIndex, float qValue, bool record = false)
        {
            DetectionPlotData data = null;
            Trace.WriteLine(this.GetType().Name + $": Waiting for data for qValue {qValue} .");
            WaitForConditionUI(() => (data = pane.CurrentData) != null 
                                           && pane.CurrentData.QValueCutoff == qValue
                                           && DetectionPlotData.DataCache.Status == DetectionPlotData.DetectionDataCache.CacheStatus.idle,
                () => $"Retrieving data for qValue {qValue}, refIndex {refIndex} took too long.");
            WaitForGraphs();
            Assert.IsTrue(data.IsValid);

            if (record)
            {
                Debug.WriteLine("Peptides");
                pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PEPTIDE).TargetsCount
                    .ForEach((cnt) => { Debug.Write($"{cnt}, "); });
                Debug.WriteLine("\nPrecursors");
                pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PRECURSOR).TargetsCount
                    .ForEach((cnt) => { Debug.Write($"{cnt}, "); });
            }

            Assert.IsTrue(
                REF_DATA[refIndex].SequenceEqual(
                    pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PEPTIDE).TargetsCount));
            Assert.IsTrue(
                REF_DATA[refIndex + 1].SequenceEqual(
                    pane.CurrentData.GetTargetData(DetectionsGraphController.TargetType.PRECURSOR).TargetsCount));
        }

        private static MouseEventArgs FindBarPoint(GraphPane pane, Control graph, int index)
        {
            using (var g = graph.CreateGraphics())
            {
                var iterationCount = 0;
                var randomInt = new Random();
                var evt = new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0);
                while (!(pane.FindNearestObject(evt.Location, g, out var nearestObject, out var objectIndex) &&
                         nearestObject is BarItem && index == objectIndex))
                {
                    evt = new MouseEventArgs(MouseButtons.None, 0,
                        randomInt.Next((int) pane.Rect.Width), randomInt.Next((int) pane.Rect.Height), 0);
                    iterationCount++;
                }
                Debug.WriteLine($"Iterations to bar: {iterationCount}");
                return evt;
            }
        }
    }
}