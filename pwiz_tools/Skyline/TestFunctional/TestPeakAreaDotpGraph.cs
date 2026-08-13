using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
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

            // Bug (skyline.ms support thread 75064): the rdotp line must follow the replicate
            // ordering, not stay in document order. Add a numeric replicate annotation whose
            // values reverse the document order, order by it, and re-verify that each replicate
            // still maps to its own rdotp value at its displayed position.
            var sortKeys = replicates
                .Select((name, i) => (name, key: (replicates.Length - i).ToString(CultureInfo.InvariantCulture)))
                .ToDictionary(t => t.name, t => t.key);
            AddReplicateAnnotation(@"SortKey", AnnotationDef.AnnotationType.number, sortKeys);
            OrderPeakAreaByReplicateAnnotation(@"SortKey");
            RunUI(() =>
            {
                var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.MasterPane[0];
                Assert.IsFalse(replicates.SequenceEqual(pane.GetOriginalXAxisLabels()),
                    "Test setup: ordering by SortKey did not change the replicate order.");
                VerifyDotpLine(replicates, expectedRDotp1, @"rdotp");
            });

            // Reset ordering so the rest of the test runs in document order.
            RunUI(() =>
            {
                SummaryReplicateGraphPane.OrderByReplicateAnnotation = null;
                SkylineWindow.UpdatePeakAreaGraph();
            });
            WaitForGraphs();

            // Grouping the replicates into two pairs must aggregate the rdotp line to the group
            // means, matching the grouped bars (Nick's note on the same thread). Compute the
            // expected means from the ungrouped line so display rounding matches exactly.
            double[] ungroupedRdotp = null;
            RunUI(() => ungroupedRdotp = replicates.Select(r => GetRdotpLineValue(r)).ToArray());
            var pairGroups = new Dictionary<string, string>
            {
                {replicates[0], @"G1"}, {replicates[1], @"G1"},
                {replicates[2], @"G2"}, {replicates[3], @"G2"},
            };
            AddReplicateAnnotation(@"PairGroup", AnnotationDef.AnnotationType.text, pairGroups);
            RunUI(() => SkylineWindow.GroupByReplicateAnnotation(@"PairGroup"));
            WaitForGraphs();
            RunUI(() =>
            {
                var expectedG1 = Math.Round((ungroupedRdotp[0] + ungroupedRdotp[1]) / 2, 2);
                var expectedG2 = Math.Round((ungroupedRdotp[2] + ungroupedRdotp[3]) / 2, 2);
                VerifyDotpLine(new[] {@"G1", @"G2"}, new[] {expectedG1, expectedG2}, @"rdotp");
            });

            // Reset grouping so the rest of the test runs ungrouped, in document order.
            RunUI(() =>
            {
                SummaryReplicateGraphPane.GroupByReplicateAnnotation = null;
                SkylineWindow.UpdatePeakAreaGraph();
            });
            WaitForGraphs();

            var propertyDialog = ShowDialog<AreaChartPropertyDlg>(SkylineWindow.ShowAreaPropertyDlg);
            RunUI(() =>
            {
                propertyDialog.SetDotpCutoffValue(AreaExpectedValue.ratio_to_label, "asdf");
                propertyDialog.OkDialog();  // Does not dismiss the form
                Assert.IsNotNull(propertyDialog.GetRdotpErrorText());
                propertyDialog.SetShowCutoffProperty(true);
                propertyDialog.SetDotpCutoffValue(AreaExpectedValue.ratio_to_label, (0.9).ToString(CultureInfo.CurrentCulture));
            });
            OkDialog(propertyDialog, propertyDialog.OkDialog);

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
            });
            OkDialog(propertyDialog, propertyDialog.OkDialog);
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

        // Adds a replicate annotation of the given type with an explicit value per replicate
        // (keyed by replicate name), so ordering or grouping by it is fully deterministic.
        private void AddReplicateAnnotation(string name, AnnotationDef.AnnotationType type,
            IDictionary<string, string> valuesByReplicate)
        {
            RunUI(() => SkylineWindow.ModifyDocument(@"Add replicate annotation", doc =>
            {
                var annotationDef = new AnnotationDef(name,
                    AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate),
                    type, new string[0]);
                var dataSettings = doc.Settings.DataSettings.ChangeAnnotationDefs(
                    ImmutableList.ValueOf(doc.Settings.DataSettings.AnnotationDefs.Append(annotationDef)));
                doc = doc.ChangeSettings(doc.Settings.ChangeDataSettings(dataSettings));
                var measuredResults = doc.MeasuredResults;
                var chromatograms = measuredResults.Chromatograms
                    .Select(c => c.ChangeAnnotations(c.Annotations.ChangeAnnotation(name, valuesByReplicate[c.Name])))
                    .ToArray();
                return doc.ChangeMeasuredResults(measuredResults.ChangeChromatograms(chromatograms));
            }));
        }

        private void OrderPeakAreaByReplicateAnnotation(string annotationTitle)
        {
            RunUI(() =>
            {
                var replicateValue = ReplicateValue.GetGroupableReplicateValues(SkylineWindow.Document)
                    .First(v => v.Title == annotationTitle);
                SummaryReplicateGraphPane.OrderByReplicateAnnotation = replicateValue.ToPersistedString();
                SkylineWindow.UpdatePeakAreaGraph();
            });
            WaitForGraphs();
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
            var dotpLabels = pane.DotProductStrings.ToArray();

            for (var i = 0; i < replicates.Length; i++)
            {
                var repIndex = pane.GetOriginalXAxisLabels().ToList()
                    .FindIndex(label => replicates[i].Equals(label));
                Assert.IsTrue(repIndex >= 0, "Replicate labels of the peak area graph are incorrect.");
                var expectedLabel = TextUtil.LineSeparate(dotpLabel,
                    string.Format(CultureInfo.CurrentCulture, "{0:F02}", dotps[i]));
                Assert.AreEqual(expectedLabel, dotpLabels[repIndex],
                    "Dotp labels of the peak area graph are incorrect.");
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

        // Returns the unrounded rdotp line value at the given replicate's displayed position.
        // Must be called on the UI thread.
        private double GetRdotpLineValue(string replicate, int paneIndex = 0)
        {
            var pane = (AreaReplicateGraphPane) SkylineWindow.GraphPeakArea.GraphControl.MasterPane[paneIndex];
            var dotpLine = pane.CurveList.OfType<LineItem>().First(line => line.Label.Text.Equals(@"rdotp"));
            var repIndex = pane.GetOriginalXAxisLabels().ToList().FindIndex(label => replicate.Equals(label));
            Assert.IsTrue(repIndex >= 0, "Replicate labels of the peak area graph are incorrect.");
            return dotpLine.Points[repIndex].Y;
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