using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results.Imputation;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakImputationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakImputation()
        {
            TestFilesZip = @"TestFunctional\PeakImputationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PeakImputationTest.sky"));
                SkylineWindow.ShowPeakImputation();
            });

            var peakImputationForm = FindOpenForm<PeakImputationForm>();
            RunUI(() =>
            {
                peakImputationForm.RtCalculatorName = RtValueType.PEAK_APEXES.Name;
            });
            WaitForCondition(() => peakImputationForm.IsComplete);
            GraphSummary graphSummaryScoreToRunRegression = null;
            RunUI(()=>
            {
                peakImputationForm.DisplayRetentionTimeRegression();
                graphSummaryScoreToRunRegression =
                    SkylineWindow.ListGraphRetentionTime.FirstOrDefault(graph =>
                        graph.Type == GraphTypeSummary.score_to_run_regression);
            });
            Assert.IsNotNull(graphSummaryScoreToRunRegression);
            WaitForGraphs();
            WaitForConditionUI(() =>
            {
                Assert.IsTrue(graphSummaryScoreToRunRegression.TryGetGraphPane(
                    out RTLinearRegressionGraphPane linearRegressionGraphPane));
                return !linearRegressionGraphPane.IsCalculating;
            });
            RunUI(() =>
            {
                Assert.IsTrue(graphSummaryScoreToRunRegression.TryGetGraphPane(
                    out RTLinearRegressionGraphPane linearRegressionGraphPane));
                Assert.IsFalse(linearRegressionGraphPane.IsCalculating);
                Assert.AreEqual(RtValueType.PEAK_APEXES.ToString(), linearRegressionGraphPane.XAxis.Title.Text);
            });
            RunUI(()=>
            {
                peakImputationForm.AlignAllGraphs = true;
            });
            WaitForGraphs();
        }
    }
}
