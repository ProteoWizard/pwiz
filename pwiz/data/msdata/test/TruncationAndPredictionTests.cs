using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.MzMlb;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Verifies the lossy-encoding paths (mzMLb-only): mantissa truncation, delta prediction, and
/// linear prediction. Mirrors cpp <c>writeMzMLbExtra</c> (IO.cpp:1622-1820). Each test writes
/// a small mzMLb with one of the lossy modes engaged, then re-reads it and checks the round-
/// trip is exact (for prediction with no truncation) or within tolerance (for truncation).
/// </summary>
[TestClass]
public class TruncationAndPredictionTests
{
    private static (string path, MSData orig) WriteOne(BinaryEncoderConfig cfg, double[] mzs, double[] ints)
    {
        var msd = new MSData { Id = "lossy" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        msd.Run.Id = msd.Id;
        var sl = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1", DefaultArrayLength = mzs.Length };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.Params.Set(CVID.MS_centroid_spectrum);
        s.SetMZIntensityArrays(mzs, ints, CVID.MS_number_of_detector_counts);
        sl.Spectra.Add(s);
        msd.Run.SpectrumList = sl;

        string path = Path.Combine(Path.GetTempPath(), $"lossy-{System.Guid.NewGuid():N}.mzMLb");
        new MzMlbWriter(cfg).Write(msd, path);
        return (path, msd);
    }

    private static Spectrum ReadOneSpectrum(string path)
    {
        var rt = new MSData();
        new MzMlbReaderAdapter().Read(path, rt);
        return rt.Run.SpectrumList!.GetSpectrum(0, getBinaryData: true);
    }

    [TestMethod]
    public void Delta_RoundTrip_PreservesArray()
    {
        var cfg = new BinaryEncoderConfig
        {
            Compression = BinaryCompression.Zlib,
            PredictionOverrides = { [CVID.MS_m_z_array] = BinaryPrediction.Delta },
        };
        double[] mz = { 100.0, 100.1, 100.2, 100.3, 100.4 };
        double[] inten = { 5.0, 10.0, 15.0, 20.0, 25.0 };
        var (path, _) = WriteOne(cfg, mz, inten);
        try
        {
            var s = ReadOneSpectrum(path);
            var got = s.GetMZArray()!.Data;
            Assert.AreEqual(mz.Length, got.Count);
            // Delta prediction is exact algebraically but accumulates floating-point
            // round-off across the encode/decode loop. Allow 1e-12 absolute tolerance —
            // well below the lossy-encoding regime this filter is used in.
            for (int i = 0; i < mz.Length; i++)
                Assert.AreEqual(mz[i], got[i], 1e-12, $"m/z[{i}] outside round-trip tolerance");
            CollectionAssert.AreEqual(inten, s.GetIntensityArray()!.Data, "intensity untouched");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void Linear_RoundTrip_PreservesArray()
    {
        var cfg = new BinaryEncoderConfig
        {
            Compression = BinaryCompression.Zlib,
            PredictionOverrides = { [CVID.MS_m_z_array] = BinaryPrediction.Linear },
        };
        // Linear prediction is exact for arrays where each value is a linear function of its
        // index (constant first-order delta). Use a uniformly-spaced m/z grid.
        double[] mz = { 200.0, 200.5, 201.0, 201.5, 202.0, 202.5 };
        double[] inten = { 1.0, 2.0, 4.0, 8.0, 16.0, 32.0 };
        var (path, _) = WriteOne(cfg, mz, inten);
        try
        {
            var s = ReadOneSpectrum(path);
            var got = s.GetMZArray()!.Data;
            Assert.AreEqual(mz.Length, got.Count);
            for (int i = 0; i < mz.Length; i++)
                Assert.AreEqual(mz[i], got[i], 1e-12,
                    $"m/z[{i}] outside linear-prediction round-trip tolerance");
            CollectionAssert.AreEqual(inten, s.GetIntensityArray()!.Data,
                "intensity untouched (no prediction override)");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void Truncation_ZerosMantissaBits_WithinTolerance()
    {
        // Truncating the bottom 30 bits of a 52-bit double mantissa leaves ~22 bits of precision,
        // i.e. ~1e-7 relative error on a 100 m/z value. Verify the round-trip lands within that.
        var cfg = new BinaryEncoderConfig
        {
            Compression = BinaryCompression.Zlib,
            TruncationOverrides = { [CVID.MS_m_z_array] = 30 },
        };
        double[] mz = { 100.123456789, 200.987654321 };
        double[] inten = { 50.0, 75.0 };
        var (path, _) = WriteOne(cfg, mz, inten);
        try
        {
            var s = ReadOneSpectrum(path);
            var roundMz = s.GetMZArray()!.Data;
            Assert.AreEqual(mz.Length, roundMz.Count);
            for (int i = 0; i < mz.Length; i++)
                Assert.AreEqual(mz[i], roundMz[i], mz[i] * 1e-6, $"m/z[{i}] beyond truncation tolerance");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void Truncation_RoundToInteger_ProducesIntegerValues()
    {
        // Truncation == -1 means "round to nearest integer" — useful for intensity counts.
        var cfg = new BinaryEncoderConfig
        {
            Compression = BinaryCompression.Zlib,
            TruncationOverrides = { [CVID.MS_intensity_array] = -1 },
        };
        double[] mz = { 100.0, 101.0, 102.0 };
        double[] inten = { 12.4, 17.6, 23.5 };
        var (path, _) = WriteOne(cfg, mz, inten);
        try
        {
            var s = ReadOneSpectrum(path);
            CollectionAssert.AreEqual(new[] { 12.0, 18.0, 24.0 }, s.GetIntensityArray()!.Data,
                "intensities should round to nearest integer (12.4→12, 17.6→18, 23.5→24)");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void EmitsTruncationCvParam_OnPredictionMode()
    {
        // The cvParam should be present on the binaryDataArray when prediction is set, so
        // downstream readers know to invert it.
        var cfg = new BinaryEncoderConfig
        {
            Compression = BinaryCompression.Zlib,
            PredictionOverrides = { [CVID.MS_m_z_array] = BinaryPrediction.Delta },
        };
        var (path, _) = WriteOne(cfg, new[] { 100.0, 100.1, 100.2 }, new[] { 1.0, 2.0, 3.0 });
        try
        {
            var rt = new MSData();
            new MzMlbReaderAdapter().Read(path, rt);
            var s = rt.Run.SpectrumList!.GetSpectrum(0, getBinaryData: true);
            var mzArr = s.GetMZArray()!;
            Assert.IsTrue(mzArr.HasCVParam(CVID.MS_truncation__delta_prediction_and_zlib_compression),
                "MS_truncation_delta_prediction_and_zlib_compression cvParam should be on the m/z array");
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
