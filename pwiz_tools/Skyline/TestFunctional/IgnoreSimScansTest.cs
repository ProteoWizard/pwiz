/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test Transition Settings to ignore SIM scans in data files
    /// </summary>
    [TestClass]
    public class IgnoreSimScansTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestIgnoreSimScans()
        {
            TestFilesZip = @"TestFunctional\IgnoreSimScansTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test importing raw data with SIM scans that should be ignored
        /// </summary>
        protected override void DoTest()
        {
            const string replicateName = "MA-0451_01_HT_Run_0002_eLC06_20210615";

            using (new WaitDocumentChange(null, true))
            {
                OpenDocument(TestFilesDir.GetTestPath("IgnoreSim.sky"));
                // Originally a Thermo RAW file containing SIM scans for all of the PRTC peptides filtered
                // to just the first PRTC peptide in time and m/z ranges
                ImportResults(TestFilesDir.GetTestPath(replicateName + ".mzML"));
            }

            SelectNode(SrmDocument.Level.Molecules, 0); // First and only peptide
            WaitForGraphs();

            ValidateChromatograms(replicateName, "21.1\n+0.4 ppm");

            ClickChromatogram(21.14, 5.49545E+07);

            ValidateFullScan(ChromSource.sim, 1, 1, 0, 3, 494.5);

            using (new WaitDocumentChange())
            {
                RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.IgnoreSimScans = true;
                    transitionSettingsUI.OkDialog();
                });
            }

            using (new WaitDocumentChange(null, true))
            {
                RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResults =>
                {
                    manageResults.ReimportResults();
                    manageResults.OkDialog();
                });
            }
            WaitForGraphs();

            ValidateChromatograms(replicateName, "21.2\n+0.1 ppm");

            ClickChromatogram(21.18, 6.06743E+07);

            ValidateFullScan(ChromSource.ms1, 1, 1, 1, 9, 495);
        }

        private static void ValidateFullScan(ChromSource source, int monoPoints, int mplus1Points, int mplus2Points, int totalPoints, double maxMz)
        {
            var fullScanGraph = WaitForOpenForm<GraphFullScan>();
            RunUI(() =>
            {
                Assert.IsTrue(fullScanGraph.IsScanTypeSelected(source));
                var expectedPoints = new[] { monoPoints, mplus1Points, mplus2Points, totalPoints };
                var curveList = fullScanGraph.ZedGraphControl.GraphPane.CurveList;
                Assert.AreEqual(expectedPoints.Length, curveList.Count);
                for (int i = 0; i < expectedPoints.Length; i++)
                    Assert.AreEqual(expectedPoints[i], curveList[i].Points.Count);
                var spectrumCurve = curveList.Last();
                double maxMzFound = Mzs(spectrumCurve.Points).Max();
                Assert.IsTrue(maxMzFound < maxMz, string.Format("Found m/z {0} greater than {1}", maxMzFound, maxMz));
            });
        }

        private static IEnumerable<double> Mzs(IPointList points)
        {
            for (int i = 0; i < points.Count; i++)
                yield return points[i].X;
        }

        private static void ValidateChromatograms(string replicateName, string annotationText)
        {
            RunUI(() =>
            {
                AssertResult.IsDocumentResultsState(SkylineWindow.DocumentUI, replicateName, 1, 0, 1, 0, 3);
                string peakAnnotationText = SkylineWindow.GraphChromatograms.First().GetAnnotationLabelStrings().First()
                    .Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
                Assert.AreEqual(annotationText, peakAnnotationText);
            });
        }
    }
}