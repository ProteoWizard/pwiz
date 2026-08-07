using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Tests for <see cref="MzMlbReaderAdapter"/> against a tiny pre-built mzMLb fixture
/// (cpp-msconvert output committed at <c>pwiz-sharp/example_data/tiny.pwiz.1.1.mzMLb</c>).
/// MzMlbReaderTests already covers Identify + Read against synthetic HDF5 fixtures;
/// these tests pin behavior against a real cpp-written file so any drift in cpp's
/// mzMLb layout (binary-array naming, mzML XML embedding, etc.) shows up here.
/// </summary>
[TestClass]
public class MzMlbCppFixtureTests
{
    private static string FixturePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "example_data", filename);

    [TestMethod]
    public void Identify_CppWrittenMzMLb_RecognizedAsMzMLbFormat()
    {
        string mzmlbPath = FixturePath("tiny.pwiz.1.1.mzMLb");
        Assert.IsTrue(File.Exists(mzmlbPath), $"fixture missing: {mzmlbPath}");

        byte[] head = new byte[32];
        using (var fs = File.OpenRead(mzmlbPath))
            fs.Read(head, 0, head.Length);
        string headStr = new(Array.ConvertAll(head, b => (char)b));
        var reader = new MzMlbReaderAdapter();
        Assert.AreEqual(CVID.MS_mzMLb_format, reader.Identify(mzmlbPath, headStr));
    }

    [TestMethod]
    public void Read_CppWrittenMzMLb_MatchesSourceMzMLSpectraAndChromatograms()
    {
        // Sibling fixture: same cpp msconvert input, parallel mzML output. Read both
        // and verify the mzMLb path produces matching spectrum + chromatogram data.
        string sourceMzML = FixturePath("tiny.pwiz.1.1.mzML");
        string mzmlbPath = FixturePath("tiny.pwiz.1.1.mzMLb");
        Assert.IsTrue(File.Exists(sourceMzML), $"fixture missing: {sourceMzML}");
        Assert.IsTrue(File.Exists(mzmlbPath), $"fixture missing: {mzmlbPath}");

        var refMsd = new MSData();
        new MzmlReaderAdapter().Read(sourceMzML, refMsd);

        var mzmlbMsd = new MSData();
        new MzMlbReaderAdapter().Read(mzmlbPath, mzmlbMsd);

        Assert.IsNotNull(mzmlbMsd.Run.SpectrumList);
        Assert.AreEqual(refMsd.Run.SpectrumList!.Count, mzmlbMsd.Run.SpectrumList!.Count,
            "spectrum count mismatch");

        var refSpec = refMsd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        var mzmlbSpec = mzmlbMsd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(refSpec.Id, mzmlbSpec.Id, "spectrum[0].Id mismatch");

        var refPairs = new List<MZIntensityPair>();
        refSpec.GetMZIntensityPairs(refPairs);
        var mzmlbPairs = new List<MZIntensityPair>();
        mzmlbSpec.GetMZIntensityPairs(mzmlbPairs);
        Assert.AreEqual(refPairs.Count, mzmlbPairs.Count, "peak count mismatch");
        for (int i = 0; i < refPairs.Count; i++)
        {
            Assert.AreEqual(refPairs[i].Mz, mzmlbPairs[i].Mz, 1e-9, $"m/z mismatch at peak {i}");
            Assert.AreEqual(refPairs[i].Intensity, mzmlbPairs[i].Intensity, 1e-9, $"intensity mismatch at peak {i}");
        }

        int refChromCount = refMsd.Run.ChromatogramList?.Count ?? 0;
        int mzmlbChromCount = mzmlbMsd.Run.ChromatogramList?.Count ?? 0;
        Assert.AreEqual(refChromCount, mzmlbChromCount, "chromatogram count mismatch");
    }
}
