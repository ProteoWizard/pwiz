/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;
using System.Linq;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that spectra with no m/z's and intensities are still included in the extracted chromatogram.
    /// </summary>
    [TestClass]
    public class ZeroLengthSpectraTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestZeroLengthSpectra()
        {
            TestFilesZip = @"TestFunctional\ZeroLengthSpectraTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ZeroLengthSpectraTest.sky")));
            var msDataFilePath = new MsDataFilePath(TestFilesDir.GetTestPath("S_1.mzML"));
            ImportResultsFile(msDataFilePath.FilePath);
            using var dataFile = msDataFilePath.OpenMsDataFile(new OpenMsDataFileParams());
            var ms1Spectra = Enumerable.Range(0, dataFile.SpectrumCount).Select(dataFile.GetSpectrum)
                .Where(spectrum => spectrum.Level == 1).ToList();
            var emptyMs1Spectra = ms1Spectra.Where(spectrum => spectrum.Mzs.Length == 0).ToList();
            Assert.AreNotEqual(0, emptyMs1Spectra.Count);
            Assert.AreNotEqual(emptyMs1Spectra.Count, ms1Spectra.Count);
            var document = SkylineWindow.Document;
            var peptideDocNode = document.Molecules.First();
            Assert.IsTrue(document.MeasuredResults.TryLoadChromatogram(0, peptideDocNode, peptideDocNode.TransitionGroups.First(), (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance, out var chromatogramGroupInfos));
            Assert.AreEqual(1, chromatogramGroupInfos.Length);
            var chromatogramInfo = chromatogramGroupInfos[0].GetRawTransitionInfo(0);
            Assert.IsNotNull(chromatogramInfo);
            Assert.AreEqual(ms1Spectra.Count, chromatogramInfo.Times.Count);
            RunUI(() =>
            {
                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
                SkylineWindow.SetTransformChrom(TransformChrom.raw);
            });
            
            // Click on a point in the chromatogram which came from one of the empty spectra
            ClickChromatogram(22.3, 1e4);
            var graphFullScan = WaitForOpenForm<GraphFullScan>();
            RunUI(() =>
            {
                graphFullScan.SetZoom(false);
                AssertAxesNotDegenerate(graphFullScan.ZedGraphControl.GraphPane);

                // The empty spectrum still says which m/z values it was measuring, and the x-axis
                // is supposed to show that range instead of collapsing to nothing
                var scanWindowUpperLimit = emptyMs1Spectra.Max(spectrum => spectrum.Metadata.ScanWindowUpperLimit);
                Assert.IsNotNull(scanWindowUpperLimit);
                AssertEx.IsGreaterThanOrEqual(graphFullScan.ZedGraphControl.GraphPane.XAxis.Scale.Max,
                    scanWindowUpperLimit.Value);
            });
        }

        /// <summary>
        /// Asserts that the graph will actually be drawn. ZedGraph skips the axes, the grid and the
        /// curves when the minimum of any of a pane's axis scales is not less than its maximum,
        /// which leaves nothing but the title, so a degenerate range means a blank graph.
        /// </summary>
        private static void AssertAxesNotDegenerate(GraphPane graphPane)
        {
            AssertEx.IsGreaterThan(graphPane.XAxis.Scale.Max, graphPane.XAxis.Scale.Min);
            AssertEx.IsGreaterThan(graphPane.X2Axis.Scale.Max, graphPane.X2Axis.Scale.Min);
            foreach (var yAxis in graphPane.YAxisList)
                AssertEx.IsGreaterThan(yAxis.Scale.Max, yAxis.Scale.Min);
            foreach (var y2Axis in graphPane.Y2AxisList)
                AssertEx.IsGreaterThan(y2Axis.Scale.Max, y2Axis.Scale.Min);
        }
    }
}
