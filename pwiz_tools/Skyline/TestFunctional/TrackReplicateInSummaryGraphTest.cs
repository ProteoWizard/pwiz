using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TrackReplicateInSummaryGraphTest : AbstractFunctionalTestEx
    {
        private bool _asSmallMolecules;

        [TestMethod]
        public void TestTrackReplicateInSummaryGraph()
        {
            TestFilesZip = @"TestFunctional\MultiSelectPeakAreaGraphTest.zip";
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestTrackReplicateInSummaryGraphAsSmallMolecules()
        {
            if (SkipSmallMoleculeTestVersions())
            {
                return;
            } 
            TestFilesZip = @"TestFunctional\MultiSelectPeakAreaGraphTest.zip";
            _asSmallMolecules = true;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ABSciex4000_Study9-1_Site19_CalCurves only.sky")));
            if (_asSmallMolecules)
            {
                ConvertDocumentToSmallMolecules(RefinementSettings.ConvertToSmallMoleculesMode.formulas,
                    RefinementSettings.ConvertToSmallMoleculesChargesMode.none, true);
            }

            WaitForDocumentLoaded();

            // Show Peakarea and retention time graphs, deactivate synchronize zoom
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowGraphPeakArea(true);
                SkylineWindow.ShowGraphRetentionTime(true);
                Settings.Default.SynchronizeSummaryZooming = false;
            });
            WaitForGraphs();

            var summaries = new[] { SkylineWindow.GraphPeakArea, SkylineWindow.GraphRetentionTime };


            SetAxes(4, 7, summaries);            
            CheckTrackTarget(2, summaries);
            SetAxes(4, 7, summaries);
            CheckTrackTarget(8, summaries);
            CheckTrackTarget(2, summaries);
            SetAxes(4.3, 12.1, summaries);
            CheckTrackTarget(15, summaries);
            CheckTrackTarget(2, summaries);
        }


        private void CheckTrackTarget(int position, GraphSummary[] summaries)
        {
            // Move to a position
            RunUI(() =>
            {
                SkylineWindow.SelectedResultsIndex = position;
            });

            WaitForGraphs();

            RunUI(() =>
            {
                foreach (var summary in summaries)
                {
                    Assert.IsTrue(summary.GraphControl.GraphPane.XAxis.Scale.Min <= position + 1);
                    Assert.IsTrue(summary.GraphControl.GraphPane.XAxis.Scale.Max >= position + 1);
                }   
            });
            
        }

        private void SetAxes(double min, double max, GraphSummary[] summaries)
        {
            RunUI(() =>
            {
                foreach (var summary in summaries)
                {
                    summary.GraphControl.GraphPane.XAxis.Scale.Min = min;
                    summary.GraphControl.GraphPane.XAxis.Scale.Max = max;
                }
            });

            WaitForGraphs();
        }

    }
}
