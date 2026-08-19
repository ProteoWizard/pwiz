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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ZeroLengthSpectraTest : AbstractFunctionalTest
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
        }
    }
}
