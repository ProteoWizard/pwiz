﻿/*
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

using System;
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
using System.Globalization;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;

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

            var expectedPropertiesPrecursor = new Dictionary<string, object> {
                {"FileName","ID12692_01_UCA168_3727_040714.mzML"},
                {"ReplicateName","ID12692_01_UCA168_3727_040714"},
                {"RetentionTime",33.05.ToString(CultureInfo.CurrentCulture)},
                {"IonMobility",3.477.ToString(CultureInfo.CurrentCulture) + " msec"},
                {"IsolationWindow","50:2000 (-975:+975)"},
                {"IonMobilityRange",TextUtil.AppendColon(0.069.ToString(CultureInfo.CurrentCulture)) + 13.8.ToString(CultureInfo.CurrentCulture)},
                {"IonMobilityFilterRange",TextUtil.AppendColon(3.152.ToString(CultureInfo.CurrentCulture)) + 3.651.ToString(CultureInfo.CurrentCulture)},
                {"ScanId","1.0.309201 - 1.0.309400"},
                {"MSLevel","1"},
                {"Instrument",new Dictionary<string, object> {
                        {"InstrumentModel","Waters instrument model"},
                        {"InstrumentManufacturer","Waters"}
                    }
                },
                {"DataPoints",105373.ToString(@"N0", CultureInfo.CurrentCulture)},
                {"MzCount",45751.ToString(@"N0", CultureInfo.CurrentCulture)},
                {"IsCentroided","False"},
                {"idotp",0.84.ToString(CultureInfo.CurrentCulture)}
            };
            var expectedPropertiesProduct = new Dictionary<string, object> {
                {"FileName","ID12692_01_UCA168_3727_040714.mzML"},
                {"ReplicateName","ID12692_01_UCA168_3727_040714"},
                {"RetentionTime", (33.1).ToString(CultureInfo.CurrentCulture)},
                {"IonMobility", (3.326).ToString(CultureInfo.CurrentCulture) + " msec"},
                {"IsolationWindow","50:2000 (-975:+975)"},
                {"IonMobilityRange", TextUtil.AppendColon(0.069.ToString(CultureInfo.CurrentCulture)) + 13.8.ToString(CultureInfo.CurrentCulture)},
                {"IonMobilityFilterRange",TextUtil.AppendColon(3.152.ToString(CultureInfo.CurrentCulture)) + 3.651.ToString(CultureInfo.CurrentCulture)},
                {"ScanId","2.0.309601 - 2.0.309800"},
                {"MSLevel","1"},
                {"Instrument",new Dictionary<string, object> {
                        {"InstrumentModel","Waters instrument model"},
                        {"InstrumentManufacturer","Waters"}
                    }
                },
                {"DataPoints",67630.ToString(@"N0", CultureInfo.CurrentCulture)},
                {"MzCount",31378.ToString(@"N0", CultureInfo.CurrentCulture)},
                {"IsCentroided","False"},
                { "dotp", 0.81.ToString(CultureInfo.CurrentCulture) }
            };

            var expectedPropertiesProduct2 = new Dictionary<string, object>
            {
                { "FileName", "ID12692_01_UCA168_3727_040714.mzML" },
                { "ReplicateName", "ID12692_01_UCA168_3727_040714" },
                { "RetentionTime", (32.96).ToString(CultureInfo.CurrentCulture) },
                { "IonMobility", (5.716).ToString(CultureInfo.CurrentCulture) + " msec" },
                { "IsolationWindow", "50:2000 (-975:+975)" },
                {
                    "IonMobilityRange",
                    TextUtil.AppendColon((0.069).ToString(CultureInfo.CurrentCulture)) + (13.8).ToString(CultureInfo.CurrentCulture)
                },
                {
                    "IonMobilityFilterRange",
                    TextUtil.AppendColon((5.423).ToString(CultureInfo.CurrentCulture)) +
                    (6.161).ToString(CultureInfo.CurrentCulture)
                },
                { "ScanId", "2.0.308201 - 2.0.308400" },
                { "MSLevel", "1" },
                {
                    "Instrument", new Dictionary<string, object>
                    {
                        { "InstrumentModel", "Waters instrument model" },
                        { "InstrumentManufacturer", "Waters" }
                    }
                },
                { "DataPoints", 60587.ToString(@"N0", CultureInfo.CurrentCulture) },
                { "MzCount", 29876.ToString(@"N0", CultureInfo.CurrentCulture) },
                { "IsCentroided", "False" },
                { "dotp", 0.51.ToString(CultureInfo.CurrentCulture) }
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
            TestPropertySheet(expectedPropertiesProduct);
            ClickChromatogram(33.06, 68.8, PaneKey.PRECURSORS);
            TestScale(452, 456, 0, 300);
            TestPropertySheet(expectedPropertiesPrecursor);

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

            // we need to increase the ion match tolerance to have sufficient number of peaks for dotp calculation.
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library);

            RunUI(() =>
            {
                transitionSettingsUI.IonMatchToleranceUnits = MzTolerance.Units.ppm;
                transitionSettingsUI.IonMatchTolerance = 50.0;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            WaitForDocumentLoaded();
            FindNode(679.38.ToString(CultureInfo.CurrentCulture));
            WaitForGraphs();
            ClickChromatogram(32.96, 17, PaneKey.PRODUCTS);
            TestPropertySheet(expectedPropertiesProduct2);
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

            //Labels are not created in offscreen mode, so we just validate total number of ions matching the show settings
            Assert.AreEqual(ExpectedLabelCount(70, 20, 15), SkylineWindow.GraphFullScan.IonLabels.Count());

            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library);

            RunUI(() =>
            {
                var oldTolerance = transitionSettingsUI.IonMatchTolerance;
                transitionSettingsUI.IonMatchToleranceUnits = MzTolerance.Units.ppm;
                Assert.IsTrue(Math.Abs(transitionSettingsUI.IonMatchTolerance/oldTolerance - 1000.0d) < 0.001d);
                transitionSettingsUI.IonMatchTolerance = 10.0;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                Assert.AreEqual(new MzTolerance(10, MzTolerance.Units.ppm),
                    SkylineWindow.DocumentUI.Settings.TransitionSettings.Libraries.IonMatchMzTolerance);
            });
            ClickChromatogram(33.11, 15.055, PaneKey.PRODUCTS);
            RunUI(() =>
            {
                SkylineWindow.GraphFullScan.SetMzScale(new MzRange(100, 600));
                SkylineWindow.GraphFullScan.SetIntensityScale(400);
            });
            WaitForGraphs();
            var graphLabels = SkylineWindow.GraphFullScan.IonLabels;
            Assert.AreEqual(ExpectedLabelCount(48, 1, 2), graphLabels.Count());
        }

        private static int ExpectedLabelCount(int offscreenCount, int onscreenEnCount, int onscreenJaCount)
        {
            if (Skyline.Program.SkylineOffscreen)
                return offscreenCount;

            var cultureShort = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            if (Equals(cultureShort, "ja") || Equals(cultureShort, "zh"))
                return onscreenJaCount;

            return onscreenEnCount;
        }

        private void TestPropertySheet(Dictionary<string, object> expectedPropertiesDict)
        {
            var expectedProperties = new FullScanProperties();
            expectedProperties.Deserialize(expectedPropertiesDict);

            Assert.IsTrue(SkylineWindow.GraphFullScan != null && SkylineWindow.GraphFullScan.Visible);
            var msGraph = SkylineWindow.GraphFullScan.MsGraphExtension;

            var propertiesButton = SkylineWindow.GraphFullScan.PropertyButton;
            Assert.IsFalse(propertiesButton.Checked);
            RunUI(() =>
            {
                propertiesButton.PerformClick();

            });
            WaitForConditionUI(() => msGraph.PropertiesVisible);
            WaitForGraphs();
            FullScanProperties currentProperties = null;
            RunUI(() =>
            {
                currentProperties = msGraph.PropertiesSheet.SelectedObject as FullScanProperties;
            });
            Assert.IsNotNull(currentProperties);
            // To write new json string for the expected property values into the output stream uncomment the next line
            //Trace.Write(currentProperties.Serialize());
            AssertEx.NoDiff(expectedProperties.Serialize(), currentProperties.Serialize());
            Assert.IsTrue(expectedProperties.IsSameAs(currentProperties));
            Assert.IsTrue(propertiesButton.Checked);

            // make sure the properties are updated when the spectrum changes
            RunUI(() =>
            {
                SkylineWindow.GraphFullScan.LeftButton?.PerformClick();
            });
            WaitForGraphs();
            WaitForConditionUI(() => SkylineWindow.GraphFullScan.IsLoaded);
            RunUI(() =>
            {
                currentProperties = msGraph.PropertiesSheet.SelectedObject as FullScanProperties;
            });
            
            Assert.IsFalse(currentProperties.IsSameAs(expectedProperties));
            RunUI(() =>
            {
                propertiesButton.PerformClick();

            });
            WaitForConditionUI(() => !msGraph.PropertiesVisible);
            WaitForGraphs();
            Assert.IsFalse(propertiesButton.Checked);
        }
    }
}
