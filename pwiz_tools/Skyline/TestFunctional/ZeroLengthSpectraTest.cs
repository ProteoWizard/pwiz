using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.ProteowizardWrapper;
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
