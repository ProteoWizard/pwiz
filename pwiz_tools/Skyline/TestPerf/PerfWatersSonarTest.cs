/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    [TestClass]
    public class PerfWatersSonarTest : AbstractFunctionalTestEx
    {
        static string replicateName = @"LFQ_Waters_SynaptXS_SONAR_Standard_AutoQC_01";

        [TestMethod]
        public void WatersSonarPerfTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfWatersSonarTest.zip");
            TestFilesPersistent =  // List of files that we'd like to unzip alongside parent zipFile, and (re)use in place
                new[] 
                {
                    replicateName + @".raw" // The raw SONAR data
                };

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir; // So we can reimport the raw file, which has moved relative to skyd file 
            Settings.Default.TransformTypeChromatogram = TransformChrom.interpolated.ToString();
            OpenDocument("SkylineSonarTiny.sky");
            var doc = WaitForDocumentLoaded();
            // Reimport data for a replicate
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                var chromatograms = SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms;
                dlg.SelectedChromatograms = new[] {chromatograms[0]};
                dlg.ReimportResults();
                dlg.OkDialog();
            });

            var doc1 = WaitForDocumentChangeLoaded(doc);
            FindNode("425"); // This precursor's mz is withing sonar range

            // Verify UI changes in "drift time" viewer that acknowledge SONAR data 
            WaitForGraphs();
            ClickChromatogram(9.5715, 65.5E+3);
            WaitForGraphs();
            // Click the Show 2D Spectrum button  to change the plot to a three-dimensional spectrum with SONAR.
            RunUI(() => SkylineWindow.GraphFullScan.SetSpectrum(false));
            WaitForGraphs();
            // PauseTest(); // Uncomment for a quick manual demo
            string yTitle = null;
            RunUI(()=>
            {
                yTitle = SkylineWindow.GraphFullScan.ZedGraphControl.GraphPane.YAxis.Title.Text;
            });
            var expectedTitle = Resources.GraphFullScan_CreateIonMobilityHeatmap_Quadrupole_Scan_Range__m_z_;
            AssertEx.AreEqual(expectedTitle, yTitle, "expected fullscan graph y axis title to be " + expectedTitle);
            CloseSpectrumGraph();
            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;

            var numPeaks = new[] { 10, 0 }; // 2nd precursor is outside the Sonar selection range
            int npIndex = 0;
            var errmsg = "";
            var expectLoaded = true;
            foreach (var pair in doc1.PeptidePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                AssertEx.AreEqual(expectLoaded, results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, out chromGroupInfo));
                if (expectLoaded)
                {
                    foreach (var chromGroup in chromGroupInfo)
                    {
                        if (numPeaks[npIndex] != chromGroup.NumPeaks)
                            errmsg += string.Format("unexpected peak count {0} instead of {1} in chromatogram {2}\r\n", chromGroup.NumPeaks, numPeaks[npIndex], npIndex);
                        npIndex++;
                        foreach (var tranInfo in chromGroup.TransitionPointSets)
                        {
                            maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                        }
                    }
                }
                expectLoaded = false; // The second peptide has no precursors in the quadrupole range
            }
            Assert.IsTrue(errmsg.Length == 0, errmsg);
            Assert.AreEqual(66631.82, maxHeight, 1);
        }
    }
}
