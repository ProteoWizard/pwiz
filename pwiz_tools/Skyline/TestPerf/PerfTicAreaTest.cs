/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfTicAreaTest : AbstractFunctionalTest
    {
        /// <summary>
        /// Tests that the Total Ion Current area is calculated correctly in various types of files.
        /// <see cref="GlobalChromatogramExtractor.IsTicChromatogramUsable"/> is responsible for deciding whether
        /// the TIC chromatogram that is present in the data file can be used to calculate the TicArea. If that chromatogram
        /// is not usable, then Skyline falls back to the technique of summing the spectrum intensities in each MS1 spectrum.
        /// </summary>
        [TestMethod]
        public void TestPerfTicArea()
        {
            TestFilesZipPaths = new[]
            {
                "https://skyline.gs.washington.edu/perftests/PerfTicAreaTest.zip"
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var ticAreas = new Dictionary<string, double>();

            // TIC chromatogram includes MS2 scans
            ticAreas.Add("DemuxedMzML.mzML", 231215169536);
            // TIC chromatogram has zero points because MS1 scans were marked as SIM
            ticAreas.Add("SIMRawFile.raw", 231215169536);
            // TIC chromatogram is usable
            ticAreas.Add("XlinkRawFile.raw", 1021147086848);
            // TIC chromatogram includes MS2 scans, but is otherwise the same as XlinkRawFile.raw
            ticAreas.Add("XlinkRawFile.mzML", 1007182479360);

            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PerfTicAreaTest.sky")));
            ImportResultsFiles(ticAreas.Keys.Select(fileName=> new MsDataFilePath(TestFilesDir.GetTestPath(fileName))).ToArray());

            Assert.IsNotNull(SkylineWindow.Document.Settings.MeasuredResults);
            foreach (var chromatogramSet in SkylineWindow.Document.Settings.MeasuredResults.Chromatograms)
            {
                foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    string filename = msDataFileInfo.FilePath.GetFileName();
                    Assert.IsNotNull(msDataFileInfo.TicArea, filename);
                    double expectedTicArea;
                    Assert.IsTrue(ticAreas.TryGetValue(filename, out expectedTicArea), filename);
                    Assert.AreEqual(expectedTicArea, msDataFileInfo.TicArea.Value, 1.0, filename);
                }
            }
        }
    }
}
