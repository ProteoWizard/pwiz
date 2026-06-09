using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HDF.PInvoke;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1806 // HDF5 close() ints

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Tests for <see cref="Mz5ReaderAdapter"/> against a tiny pre-built mz5 fixture
/// (cpp-msconvert output committed at <c>pwiz-sharp/example_data/tiny.pwiz.1.1.mz5</c>).
/// Covers Identify (HDF5-magic + FileInformation + version) and Read (CV list,
/// source files, software, run id, spectrum / chromatogram arrays).
/// </summary>
[TestClass]
public class Mz5IdentifyTests
{
    private static string FixturePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "example_data", filename);

    [TestMethod]
    public void Identify_CppWrittenMz5_RecognizedAsMz5Format()
    {
        string mz5Path = FixturePath("tiny.pwiz.1.1.mz5");
        Assert.IsTrue(File.Exists(mz5Path), $"fixture missing: {mz5Path}");

        // Identify path: HDF5 magic + FileInformation dataset present + version match.
        byte[] head = new byte[32];
        using (var fs = File.OpenRead(mz5Path))
            fs.Read(head, 0, head.Length);
        string headStr = new(Array.ConvertAll(head, b => (char)b));
        var reader = new Mz5ReaderAdapter();
        Assert.AreEqual(CVID.MS_mz5_format, reader.Identify(mz5Path, headStr));
    }

    [TestMethod]
    public void Read_CppWrittenMz5_PopulatesDocumentLevelMetadata()
    {
        // Both fixtures are produced by the same cpp msconvert invocation on the same
        // source mzML — the .mz5 should round-trip to the same MSData shape as the .mzML.
        string sourceMzML = FixturePath("tiny.pwiz.1.1.mzML");
        string mz5Path = FixturePath("tiny.pwiz.1.1.mz5");
        Assert.IsTrue(File.Exists(sourceMzML), $"fixture missing: {sourceMzML}");
        Assert.IsTrue(File.Exists(mz5Path), $"fixture missing: {mz5Path}");

        var refMsd = new MSData();
        new MzmlReaderAdapter().Read(sourceMzML, refMsd);

        var mz5Msd = new MSData();
        new Mz5ReaderAdapter().Read(mz5Path, mz5Msd);

        // CV list
        Assert.IsTrue(mz5Msd.CVs.Count >= 2,
            $"expected CV list to have MS + UO, got {mz5Msd.CVs.Count}");
        Assert.IsTrue(mz5Msd.CVs.Any(cv => cv.Id == "MS"));
        Assert.IsTrue(mz5Msd.CVs.Any(cv => cv.Id == "UO"));

        // Source files
        Assert.IsTrue(mz5Msd.FileDescription.SourceFiles.Count >= 1,
            "no source files in mz5 output");

        // Software (must include 'pwiz' for the conversion record)
        Assert.IsTrue(mz5Msd.Software.Any(sw => sw.Id.StartsWith("pwiz", StringComparison.Ordinal)),
            "no pwiz software entry");

        // Run id should match the reference
        Assert.IsFalse(string.IsNullOrEmpty(mz5Msd.Run.Id), "Run.Id is empty");
        Assert.AreEqual(refMsd.Run.Id, mz5Msd.Run.Id, "Run.Id mismatch");

        // Spectrum list: counts match, first spectrum's m/z + intensity arrays match.
        Assert.IsNotNull(mz5Msd.Run.SpectrumList);
        Assert.AreEqual(refMsd.Run.SpectrumList!.Count, mz5Msd.Run.SpectrumList!.Count,
            "spectrum count mismatch");

        var refSpec = refMsd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        var mz5Spec = mz5Msd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(refSpec.Id, mz5Spec.Id, "spectrum[0].Id mismatch");

        var refPairs = new List<MZIntensityPair>();
        refSpec.GetMZIntensityPairs(refPairs);
        var mz5Pairs = new List<MZIntensityPair>();
        mz5Spec.GetMZIntensityPairs(mz5Pairs);
        Assert.AreEqual(refPairs.Count, mz5Pairs.Count, "peak count mismatch");
        for (int i = 0; i < refPairs.Count; i++)
        {
            Assert.AreEqual(refPairs[i].Mz, mz5Pairs[i].Mz, 1e-9, $"m/z mismatch at peak {i}");
            Assert.AreEqual(refPairs[i].Intensity, mz5Pairs[i].Intensity, 1e-9, $"intensity mismatch at peak {i}");
        }

        // Chromatogram list: counts match, first chromatogram's time + intensity arrays match.
        int refChromCount = refMsd.Run.ChromatogramList?.Count ?? 0;
        int mz5ChromCount = mz5Msd.Run.ChromatogramList?.Count ?? 0;
        Assert.AreEqual(refChromCount, mz5ChromCount, "chromatogram count mismatch");
        if (refChromCount > 0)
        {
            var refChrom = refMsd.Run.ChromatogramList!.GetChromatogram(0, getBinaryData: true);
            var mz5Chrom = mz5Msd.Run.ChromatogramList!.GetChromatogram(0, getBinaryData: true);
            Assert.AreEqual(refChrom.Id, mz5Chrom.Id, "chromatogram[0].Id mismatch");

            var refTime = refChrom.GetTimeArray()?.Data;
            var mz5Time = mz5Chrom.GetTimeArray()?.Data;
            Assert.IsNotNull(refTime); Assert.IsNotNull(mz5Time);
            Assert.AreEqual(refTime.Count, mz5Time.Count, "chromatogram time-array count mismatch");
            for (int i = 0; i < Math.Min(refTime.Count, 10); i++)
                Assert.AreEqual(refTime[i], mz5Time[i], 1e-9, $"time[{i}] mismatch");

            var refInt = refChrom.GetIntensityArray()?.Data;
            var mz5Int = mz5Chrom.GetIntensityArray()?.Data;
            Assert.IsNotNull(refInt); Assert.IsNotNull(mz5Int);
            Assert.AreEqual(refInt.Count, mz5Int.Count, "chromatogram intensity-array count mismatch");
            for (int i = 0; i < Math.Min(refInt.Count, 10); i++)
                Assert.AreEqual(refInt[i], mz5Int[i], 1e-9, $"intensity[{i}] mismatch");
        }
    }

    [TestMethod]
    public void Identify_HdfFileWithoutFileInformation_ReturnsUnknown()
    {
        // Plain HDF5 file with no mz5 FileInformation dataset; Mz5ReaderAdapter
        // must NOT claim it (mzMLb shares the magic bytes, so we'd otherwise
        // mis-identify any HDF5 file as mz5).
        string path = Path.Combine(Path.GetTempPath(), $"plain-{Guid.NewGuid():N}.h5");
        H5E.set_auto(H5E.DEFAULT, null, IntPtr.Zero);
        long f = H5F.create(path, H5F.ACC_TRUNC);
        H5F.close(f);
        try
        {
            byte[] head = new byte[32];
            using (var fs = File.OpenRead(path))
                fs.Read(head, 0, head.Length);
            string headStr = new(Array.ConvertAll(head, b => (char)b));
            var reader = new Mz5ReaderAdapter();
            Assert.AreEqual(CVID.CVID_Unknown, reader.Identify(path, headStr));
        }
        finally { File.Delete(path); }
    }
}
