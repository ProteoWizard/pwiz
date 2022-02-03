using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TestPeakAreaDotpGraph : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void PeakAreaDotpGraphTest()
        {
            TestFilesZip = @"TestFunctional\PeakAreaDotpGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(
                TestFilesDir.GetTestPath("ABSciex4000_Study9-1_Site19_CalCurves only.sky")));
            var replicates = new[]
            {
                "A1_CalCurve_run_063", "J1_CalCurve_run_074", "9-1_Site19_B2_CalCurve_run_106",
                "9-1_Site19_D2_CalCurve_run_108"
            };

            RunUI(() => { NormalizeGraphToHeavy(); });
            RunDlg<AreaChartPropertyDlg>(SkylineWindow.ShowAreaPropertyDlg, propertyDlg =>
            {
                propertyDlg.SetShowCutoffProperty(false);
                propertyDlg.SetDotpDisplayProperty(DotProductDisplayOption.line);
                propertyDlg.OkDialog();
            });

            RunUI(SkylineWindow.UpdatePeakAreaGraph);
            FindNode((600.8278).ToString(LocalizationHelper.CurrentCulture) + "++");
            WaitForGraphs();
            var expectedDotp1 = new[] {0.62, 1.00, 0.61, 0.53};
            RunUI(() =>
            {
                VerifyDotpLine(replicates, expectedDotp1);
                AssertCutoffLine(false);
            });

            var propertyDialog = ShowDialog<AreaChartPropertyDlg>(SkylineWindow.ShowAreaPropertyDlg);
            RunUI(() =>
            {
                propertyDialog.SetRdotpCutoffValue("asdf");
                propertyDialog.OkDialog();
                Assert.IsNotNull(propertyDialog.GetRdotpErrorText());
                propertyDialog.SetShowCutoffProperty(true);
                propertyDialog.SetRdotpCutoffValue("0.9");
                propertyDialog.OkDialog();
            });

            FindNode((529.2855).ToString(LocalizationHelper.CurrentCulture) + "++");
            WaitForGraphs();
            var expectedDotp2 = new[] {0.72, 0.98, 0.88, 0.92};
            RunUI(() =>
            {
                VerifyDotpLine(replicates, expectedDotp2);
                AssertCutoffLine(true, 2);
            });
            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.label.ToString();
            RunUI(SkylineWindow.UpdatePeakAreaGraph);
            RunUI(() => { VerifyRdotPLabels(replicates, expectedDotp2); });

            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();
            RunUI(SkylineWindow.UpdatePeakAreaGraph);
            RunUI(AssertNoDotp);
            
        }

        private static void NormalizeGraphToHeavy()
        {
            SkylineWindow.AreaNormalizeOption = NormalizeOption.FromIsotopeLabelType(IsotopeLabelType.heavy);
            Settings.Default.AreaLogScale = false;
            SkylineWindow.UpdatePeakAreaGraph();
        }

        private void VerifyRdotPLabels(string[] replicates, double[] rdotps)
        {
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.GraphPane;
            if (!Program.SkylineOffscreen)
            {
                var rdotpLabels = pane.GraphObjList.OfType<TextObj>().ToList()
                    .FindAll(txt => txt.Text.StartsWith(@"rdotp")).Select((obj) => obj.Text).ToArray();

                for (var i = 0; i < replicates.Length; i++)
                {
                    var repIndex = pane.GetOriginalXAxisLabels().ToList()
                        .FindIndex(label => replicates[i].Equals(label));
                    Assert.IsTrue(repIndex >= 0, "Replicate labels of the peak area graph are incorrect.");
                    var expectedLabel = TextUtil.LineSeparate("rdotp",
                        string.Format(CultureInfo.CurrentCulture, "{0:F02}", rdotps[i]));
                    Assert.AreEqual(expectedLabel, rdotpLabels[repIndex],
                        "Dotp labels of the peak area graph are incorrect.");
                }
            }
        }

        private void VerifyDotpLine(string[] replicates, double[] dotps)
        {
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.GraphPane;
            var dotpLine = pane.CurveList.OfType<LineItem>().First(line => line.Label.Text.Equals("rdotp"));
            for (var i = 0; i < replicates.Length; i++)
            {
                var repIndex = pane.GetOriginalXAxisLabels().ToList().FindIndex(label => replicates[i].Equals(label));
                Assert.IsTrue(repIndex >= 0, "Replicate labels of the peak area graph are incorrect.");
                Assert.AreEqual(dotps[i], Math.Round(dotpLine.Points[repIndex].Y, 2));
            }
        }

        private void AssertCutoffLine(bool isPresent, int pointsBelowCutoff = 0)
        {
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.GraphPane;
            var dotpLine = pane.CurveList.OfType<LineItem>().FirstOrDefault(line => line.Label.Text.IsNullOrEmpty());
            if (isPresent)
            {
                Assert.IsNotNull(dotpLine);
                Assert.AreEqual(pointsBelowCutoff, 
                    Enumerable.Range(0, dotpLine.Points.Count).ToList()
                        .FindAll(i => !double.IsNaN(dotpLine.Points[i].Y)).Count);
            }
            else
            {
                Assert.IsNull(dotpLine);
            }
        }

        private void AssertNoDotp()
        {
            //Make sure there is no labels and no line
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.GraphPane;
            if (!Program.SkylineOffscreen)
                Assert.IsFalse(pane.GraphObjList.OfType<TextObj>().Any(txt => txt.Text.StartsWith(@"rdotp")));
            Assert.IsFalse(pane.CurveList.OfType<LineItem>().ToList().Any(line => line.Label.Text.Equals("rdotp")));
        }
    }
}