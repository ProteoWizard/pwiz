/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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

using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.MSGraph;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using System.Collections.Generic;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FullScanGraphTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestFullScanGraph()
        {
            Run(@"TestData\Results\BlibDriftTimeTest.zip");
        }

        protected override void DoTest()
        {
            var y4 = "y4";
            var y4Annotated = "y4" + TextUtil.SEPARATOR_SPACE + string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, 9));

            var ionsAnnotated = new[]
            {
                "y1" + TextUtil.SEPARATOR_SPACE + string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, 1)),
                "y1++" + TextUtil.SEPARATOR_SPACE + string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, 2)),
                "y5++" + TextUtil.SEPARATOR_SPACE + string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, 3)),
                "y7" + TextUtil.SEPARATOR_SPACE + string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, 6)),
                "y11" + TextUtil.SEPARATOR_SPACE + string.Format(@"({0})",string.Format(Resources.AbstractSpectrumGraphItem_GetLabel_rank__0__, 21))
            };


            Settings.Default.TransformTypeChromatogram = TransformChrom.interpolated.ToString();
            OpenDocument("BlibDriftTimeTest.sky");
            ImportResults("ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5);
            FindNode("453");
            WaitForGraphs();

            CloseSpectrumGraph();

            // Check ion mobility details display
            var expectedIonMobility =
                IonMobilityFilter.GetIonMobilityFilter(3.48, eIonMobilityUnits.drift_time_msec, 0, null);
            for (var loop = 0; loop < 4; loop++)
            {
                bool wantCCS = loop < 2;
                bool wantIM = loop % 2 == 0;
                RunUI(() => SkylineWindow.ShowIonMobility = wantIM);
                RunUI(() => SkylineWindow.ShowCollisionCrossSection = wantCCS);
                WaitForGraphs();
                var graphChrom = SkylineWindow.GetGraphChrom("ID12692_01_UCA168_3727_040714");
                var pane = graphChrom.GraphPane as MSGraphPane;
                var annotation = pane?.GetAnnotationLabelStrings().First() ?? string.Empty;
                AssertEx.AreEqual(wantIM, annotation.Contains(ChromGraphItem.FormatIonMobilityValue(expectedIonMobility)),
                    "did not find expected IMS information display");
                AssertEx.AreEqual(wantCCS, annotation.Contains(ChromGraphItem.FormatCollisionCrossSectionValue(expectedIonMobility)),
                    " did not find expected CCS information display");
            }

            // Simulate click on a peak in GraphChromatogram form.
            ClickChromatogram(32.95, 134.6);
            TestScale(452, 456, 0, 250);
            ClickChromatogram(33.23, 27.9);
            TestScale(452, 456, 0, 400);
            WaitForOpenForm<GraphFullScan>();   // For localization testing

            // Check arrow navigation.
            ClickForward(33.25, 0);
            TestScale(452, 456, 0, 200);
            ClickBackward(33.23, 27.9);
            TestScale(452, 456, 0, 400);

            // Check scan type selection.
            SetScanType(ChromSource.fragment, 33.24, 27.9);
            TestScale(453, 457, 0, 80);
            SetScanType(ChromSource.ms1, 33.23, 27.9);
            TestScale(452, 456, 0, 400);

            // Check filtered spectrum.
            SetFilter(true);
            TestScale(452, 456, 0, 40);
            SetFilter(false);
            TestScale(452, 456, 0, 400);

            // Check zoomed spectrum.
            SetZoom(false);
            TestScale(0, 2000, 0, 5100);
            SetZoom(true);
            TestScale(452, 456, 0, 400);

            // Check zoomed heatmap.
            SetSpectrum(false);
            TestScale(452, 456, 2.61, 4.34);
            WaitForOpenForm<GraphFullScan>();   // For localization testing

            // Check filtered heatmap.
            SetFilter(true);
            TestScale(452, 456, 3.2, 3.8);
            SetZoom(false);
            TestScale(0, 2000, 3.2, 3.8);
            SetFilter(false);
            TestScale(0, 2000, 0, 15);
            SetZoom(true);
            TestScale(452, 456, 2.61, 4.34);

            // Check click on ion label.
            SetSpectrum(true);
            SetZoom(false);
            SetScanType(ChromSource.fragment, 33.23, 27.9);
            ClickFullScan(517, 1000);

            //test mass error in un-annotated spectrum
            TestAnnotations(new[] {y4});
            RunUI(() => SkylineWindow.SetShowMassError(true));
            TestAnnotations((new [] { new StringBuilder(y4).AppendLine().Append(string.Format(Resources.GraphSpectrum_MassErrorFormat_ppm,
                "", -43.5)).ToString() }));
            RunUI(() => SkylineWindow.SetShowMassError(false));
            TestAnnotations(new[] { y4 });

            TestScale(516, 520, 0, 80);

            var noVendorCentroidedMessage = ShowDialog<MessageDlg>(() =>
                SkylineWindow.GraphFullScan.SetPeakTypeSelection(MsDataFileScanHelper.PeakType.centroided));
            OkDialog(noVendorCentroidedMessage, noVendorCentroidedMessage.OkDialog);

            SetShowAnnotations(true);
            TestAnnotations(new []{ y4Annotated });
            RunUI(() => SkylineWindow.SetShowMassError(true));
            TestAnnotations(new []{new StringBuilder(y4Annotated).AppendLine().Append(
                        string.Format(Resources.GraphSpectrum_MassErrorFormat_ppm,"+", 155.5)).ToString()});
            RunUI(() => SkylineWindow.SetShowMassError(false));
            SetZoom(false);
            TestAnnotations(ionsAnnotated);
            ClickFullScan(618, 120);
            RunUI(() => SkylineWindow.GraphFullScan.SetMzScale(new MzRange(500, 700)));
            TestScale(500, 700, 0, 1050.5);
            SetShowAnnotations(false);
            TestAnnotations(new[] { "y5" });

            // Check split graph
            ShowSplitChromatogramGraph(true);
            SetZoom(true);

            ClickChromatogram(33.11, 15.055, PaneKey.PRODUCTS);
            TestScale(529, 533, 0, 50);
            ClickChromatogram(33.06, 68.8, PaneKey.PRECURSORS);
            TestScale(452, 456, 0, 300);

            //test sync m/z scale
            RunUI(() => SkylineWindow.ShowGraphSpectrum(true));
            WaitForGraphs();
            RunUI(() => SkylineWindow.SynchMzScale(SkylineWindow.GraphFullScan));  // Sync from the full scan viewer to the library match
            WaitForGraphs();
            Assert.AreEqual(SkylineWindow.GraphFullScan.Range, SkylineWindow.GraphSpectrum.Range);
            var testRange = new MzRange(100, 200);
            RunUI(() => SkylineWindow.GraphSpectrum.SetMzScale(testRange));
            RunUI(() => SkylineWindow.SynchMzScale(SkylineWindow.GraphSpectrum)); // Sync from the library match to the full scan viewer
            WaitForGraphs();
            Assert.AreEqual(SkylineWindow.GraphSpectrum.Range, SkylineWindow.GraphFullScan.Range);
            Assert.AreEqual(testRange, SkylineWindow.GraphFullScan.Range);

            RunUI(() => SkylineWindow.SynchMzScale(SkylineWindow.GraphSpectrum, false)); // Sync from the library match to the full scan viewer
            //annotations are not shown in the offscreen mode
            TestSpecialIonsAnnotations();
            TestIonMatchToleranceUnitSetting();
        }

        private static void ClickFullScan(double x, double y)
        {
            RunUI(() => SkylineWindow.GraphFullScan.TestMouseClick(x, y));
        }

        private static void TestScale(double xMin, double xMax, double yMin, double yMax)
        {
            RunUI(() =>
            {
                double xAxisMin = SkylineWindow.GraphFullScan.XAxisMin;
                double xAxisMax = SkylineWindow.GraphFullScan.XAxisMax;
                double yAxisMin = SkylineWindow.GraphFullScan.YAxisMin;
                double yAxisMax = SkylineWindow.GraphFullScan.YAxisMax;

                Assert.IsTrue(xMin - xAxisMin >= 0 &&
                              xMin - xAxisMin < (xMax - xMin)/4,
                              "Expected x minimum {0}, got {1}", xMin, xAxisMin);
                Assert.IsTrue(xAxisMax - xMax >= 0 &&
                              xAxisMax - xMax < (xMax - xMin)/4,
                              "Expected x maximum {0}, got {1}", xMax, xAxisMax);
                Assert.IsTrue(yMin - yAxisMin >= 0 &&
                              yMin - yAxisMin < (yMax - yMin)/4,
                              "Expected y minimum {0}, got {1}", yMin, yAxisMin);
                Assert.IsTrue(yAxisMax - yMax >= 0 &&
                              yAxisMax - yMax < (yMax - yMin)/4,
                              "Expected y maximum {0}, got {1}", yMax, yAxisMax);
            });
        }

        private static void ClickForward(double x, double y)
        {
            RunUI(() => SkylineWindow.GraphFullScan.ChangeScan(1));
            CheckFullScanSelection(x, y);
        }

        private static void ClickBackward(double x, double y)
        {
            RunUI(() => SkylineWindow.GraphFullScan.ChangeScan(-1));
            CheckFullScanSelection(x, y);
        }

        private static void SetScanType(ChromSource source, double x, double y)
        {
            RunUI(() => SkylineWindow.GraphFullScan.SelectScanType(source));
            CheckFullScanSelection(x, y);
        }

        private static void SetShowAnnotations(bool isChecked)
        {
            RunUI(() => SkylineWindow.GraphFullScan.SetShowAnnotations(isChecked));
        }

        private static void TestAnnotations(string[] annotationText)
        {
            var graphLabels = SkylineWindow.GraphFullScan.IonLabels;
            Assert.IsTrue(annotationText.All(txt => graphLabels.Contains(txt)));
        }

        private void TestLibraryMatchAnnotations(string[] annotationText)
        {
            var ionLabels = SkylineWindow.GraphSpectrum.IonLabels.ToHashSet();
            Assert.IsTrue(annotationText.All(txt => ionLabels.Contains(txt)));
        }
        private static void SetFilter(bool isChecked)
        {
            RunUI(() => SkylineWindow.GraphFullScan.SetFilter(isChecked));
        }

        private static void SetZoom(bool isChecked)
        {
            RunUI(() => SkylineWindow.GraphFullScan.SetZoom(isChecked));
        }

        private static void SetSpectrum(bool isChecked)
        {
            RunUI(() => SkylineWindow.GraphFullScan.SetSpectrum(isChecked));
        }

        private void TestSpecialIonsAnnotations()
        {
            var testIon = new MeasuredIon("Reporter_Test", "C31H47N14O4", 679.3899, 679.3899, Adduct.M_PLUS, true);
            //Add special ion to the document settings
            RunUI(() =>
            {
                var settings = SkylineWindow.DocumentUI.Settings;
                var newFilter = settings.TransitionSettings.Filter.ChangeMeasuredIons(new List<MeasuredIon>(new[] { testIon }));
                var newSettings = settings.ChangeTransitionSettings(settings.TransitionSettings.ChangeFilter(newFilter));
                SkylineWindow.ModifyDocument("Set test settings",
                    doc => doc.ChangeSettings(newSettings));
            });


            WaitForDocumentLoaded();
            WaitForGraphs();
            RunUI(SkylineWindow.HideFullScanGraph);
            Settings.Default.ShowSpecialIons = true;
            SetShowAnnotations(true);
            SetZoom(true);
            FindNode("679");
            ClickChromatogram(33.11, 15.055, PaneKey.PRODUCTS);
            RunUI(() => SkylineWindow.GraphFullScan.SetMzScale(new MzRange(670, 680)));
            WaitForGraphs();
            //check that the special ion annotation shows in both library and full scan viewers
            TestAnnotations(new [] {"Reporter_Test+"});
            TestLibraryMatchAnnotations(new[] { "Reporter_Test+" });
            Settings.Default.ShowSpecialIons = false;
        }

        private void TestIonMatchToleranceUnitSetting()
        {
            RunUI(() => {
                SkylineWindow.ShowBIons(true);
                SkylineWindow.ShowCIons(true);
            });

            SetZoom(false);
            ClickChromatogram(33.11, 15.055, PaneKey.PRODUCTS);
            WaitForGraphs();
            Assert.AreEqual(Skyline.Program.SkylineOffscreen? 70 : 20, SkylineWindow.GraphFullScan.IonLabels.Count());

            RunUI(() =>
            {
                var settings = SkylineWindow.DocumentUI.Settings;
                var newLibs = settings.TransitionSettings.Libraries.ChangeIonMatchMzTolerance(new MzTolerance(10.0, MzTolerance.Units.ppm));
                var newSettings = settings.ChangeTransitionSettings(settings.TransitionSettings.ChangeLibraries(newLibs));
                SkylineWindow.ModifyDocument("Set test settings",
                    doc => doc.ChangeSettings(newSettings));
            });
            WaitForDocumentLoaded();
            ClickChromatogram(33.11, 15.055, PaneKey.PRODUCTS);
            RunUI(() =>
            {
                SkylineWindow.GraphFullScan.SetMzScale(new MzRange(100, 600));
                SkylineWindow.GraphFullScan.SetIntensityScale(400);
            });
            WaitForGraphs();
            var graphLabels = SkylineWindow.GraphFullScan.IonLabels;
            Assert.AreEqual(Skyline.Program.SkylineOffscreen ? 49 : 1, graphLabels.Count());
        }
    }
}
