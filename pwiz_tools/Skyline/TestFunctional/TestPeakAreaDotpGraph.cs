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
            var expectedRDotp1 = new[] {0.62, 1.00, 0.61, 0.53};
            RunUI(() =>
            {
                VerifyDotpLine(replicates, expectedRDotp1, @"rdotp");
                AssertCutoffLine(false);
            });

            var propertyDialog = ShowDialog<AreaChartPropertyDlg>(SkylineWindow.ShowAreaPropertyDlg);
            RunUI(() =>
            {
                propertyDialog.SetDotpCutoffValue(AreaExpectedValue.ratio_to_label, "asdf");
                propertyDialog.OkDialog();
                Assert.IsNotNull(propertyDialog.GetRdotpErrorText());
                propertyDialog.SetShowCutoffProperty(true);
                propertyDialog.SetDotpCutoffValue(AreaExpectedValue.ratio_to_label, (0.9).ToString(CultureInfo.CurrentCulture));
                propertyDialog.OkDialog();
            });

            FindNode((529.2855).ToString(LocalizationHelper.CurrentCulture) + "++");
            WaitForGraphs();
            var expectedRDotp2 = new[] {0.72, 0.98, 0.88, 0.92};
            RunUI(() =>
            {
                VerifyDotpLine(replicates, expectedRDotp2, @"rdotp");
                AssertCutoffLine(true, 2);
            });
            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.label.ToString();
            RunUI(SkylineWindow.UpdatePeakAreaGraph);
            RunUI(() => { VerifydotPLabels(replicates, expectedRDotp2, @"rdotp"); });

            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();
            RunUI(SkylineWindow.UpdatePeakAreaGraph);
            RunUI(() => AssertNoDotp());


            RunUI(() => SkylineWindow.OpenFile(
                TestFilesDir.GetTestPath("DIA-QE-tutorial.sky")));
            SkylineWindow.ShowSplitChromatogramGraph(true);
            replicates = new[]{"1-A", "2-B", "3-A","4-B", "5-A", "6-B"};
            var expectedIDotp = new[] {0.93, 0.82, 0.95, 0.92, 0.95, 0.74};
            var expectedDotp = new[] {0.87, 0.74, 0.89, 0.88, 1.00, 0.83};
            propertyDialog = ShowDialog<AreaChartPropertyDlg>(SkylineWindow.ShowAreaPropertyDlg);
            RunUI(() =>
            {
                propertyDialog.SetDotpDisplayProperty(DotProductDisplayOption.line);
                propertyDialog.SetShowCutoffProperty(true);
                propertyDialog.SetDotpCutoffValue(AreaExpectedValue.isotope_dist, (0.94).ToString(CultureInfo.CurrentCulture));
                propertyDialog.SetDotpCutoffValue(AreaExpectedValue.library, (0.85).ToString(CultureInfo.CurrentCulture));
                propertyDialog.OkDialog();
            });
            FindNode((873.9438).ToString(LocalizationHelper.CurrentCulture) + "++");
            WaitForGraphs();
            RunUI(() =>
            {
                VerifyDotpLine(replicates, expectedIDotp, @"idotp");
                AssertCutoffLine(true, 4);
                VerifyDotpLine(replicates, expectedDotp, @"dotp", 1);
                AssertCutoffLine(true, 2, 1);
            });

        }

        private static void NormalizeGraphToHeavy()
        {
            SkylineWindow.AreaNormalizeOption = NormalizeOption.FromIsotopeLabelType(IsotopeLabelType.heavy);
            Settings.Default.AreaLogScale = false;
            SkylineWindow.UpdatePeakAreaGraph();
        }

        private void VerifydotPLabels(string[] replicates, double[] dotps, string dotpLabel, int paneIndex = 0)
        {
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.MasterPane[paneIndex];
            if (!Program.SkylineOffscreen)
            {
                var rdotpLabels = pane.GraphObjList.OfType<TextObj>().ToList()
                    .FindAll(txt => txt.Text.StartsWith(dotpLabel)).Select((obj) => obj.Text).ToArray();

                for (var i = 0; i < replicates.Length; i++)
                {
                    var repIndex = pane.GetOriginalXAxisLabels().ToList()
                        .FindIndex(label => replicates[i].Equals(label));
                    Assert.IsTrue(repIndex >= 0, "Replicate labels of the peak area graph are incorrect.");
                    var expectedLabel = TextUtil.LineSeparate(dotpLabel,
                        string.Format(CultureInfo.CurrentCulture, "{0:F02}", dotps[i]));
                    Assert.AreEqual(expectedLabel, rdotpLabels[repIndex],
                        "Dotp labels of the peak area graph are incorrect.");
                }
            }
        }

        private void VerifyDotpLine(string[] replicates, double[] dotps, string dotpLabel, int paneIndex = 0)
        {
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.MasterPane[paneIndex];
            var dotpLine = pane.CurveList.OfType<LineItem>().First(line => line.Label.Text.Equals(dotpLabel));
            for (var i = 0; i < replicates.Length; i++)
            {
                var repIndex = pane.GetOriginalXAxisLabels().ToList().FindIndex(label => replicates[i].Equals(label));
                Assert.IsTrue(repIndex >= 0, "Replicate labels of the peak area graph are incorrect.");
                Assert.AreEqual(dotps[i], Math.Round(dotpLine.Points[repIndex].Y, 2));
            }
        }

        private void AssertCutoffLine(bool isPresent, int pointsBelowCutoff = 0, int paneIndex = 0)
        {
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.MasterPane[paneIndex];
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

        private void AssertNoDotp(int paneIndex = 0)
        {
            //Make sure there is no labels and no line
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.MasterPane[paneIndex];
            if (!Program.SkylineOffscreen)
                Assert.IsFalse(pane.GraphObjList.OfType<TextObj>().Any(txt => txt.Text.StartsWith(@"rdotp")));
            Assert.IsFalse(pane.CurveList.OfType<LineItem>().ToList().Any(line => line.Label.Text.Equals("rdotp")));
        }
    }
}