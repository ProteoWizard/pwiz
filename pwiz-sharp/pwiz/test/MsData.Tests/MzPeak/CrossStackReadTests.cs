using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pwiz.Data.MsData.MzPeak;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Reads mzPeak files written by the independent mzPeak.NET (Apache-Arrow) stack, not by our own
/// writer. These exercise the cross-stack robustness the reader needs beyond round-tripping its own
/// output: physical-type/width divergences (uint8 ms_level vs our signed int8, float32 scalars vs
/// double, int32/uint32 small-ints vs our int64), row-group discovery from Parquet statistics when
/// the <c>point_row_group_ranges</c> KV is absent, float32 value arrays in the point layer, and the
/// separate <c>wavelength_spectra_*</c> entries mzPeak.NET uses for UV/DAD spectra.
///
/// Expected values were captured from mzPeak.NET's own reader over the same fixtures, so a divergence
/// here means our read of a foreign file disagrees with the writer's read of it.
/// </summary>
[TestClass]
public class CrossStackReadTests
{
    private static string FixtureDir =>
        Path.Combine(AppContext.BaseDirectory, "mzpeak_crossstack");

    private static string Small => Path.Combine(FixtureDir, "mzpeaknet_small.mzpeak");
    private static string HasUv => Path.Combine(FixtureDir, "mzpeaknet_has_uv.mzpeak");

    [TestMethod]
    public void Small_ProfileAndCentroidSpectra_ReadWithCorrectValues()
    {
        using var reader = new MzPeakReader(Small);

        Assert.AreEqual(48, reader.SpectrumCount);

        // Spectrum 0: profile, m/z array stored inline. Values from mzPeak.NET's reader.
        var s0 = reader.GetSpectrumDescription(0);
        Assert.IsTrue(s0.IsProfile, "spectrum 0 should be profile");
        Assert.AreEqual(1, s0.MsLevel, "ms_level is written uint8 by mzPeak.NET; must read back as 1");
        // number_of_data_points is written UInt64 by mzPeak.NET; guard the unsigned->long widening
        // (a too-narrow conversion ladder would silently drop this to null).
        Assert.AreEqual(13589L, s0.NumberOfDataPoints);
        var d0 = reader.GetSpectrumData(0);
        Assert.IsNotNull(d0);
        Assert.AreEqual(13589, d0!.Mz.Length);
        Assert.AreEqual(202.60657, d0.Mz[0], 1e-4);
        Assert.AreEqual(202.60682, d0.Mz[1], 1e-4);
        Assert.AreEqual(0.0, d0.Intensity[0], 1e-3);
        Assert.AreEqual(1938.117, d0.Intensity[1], 1e-2);

        // Spectrum 2: centroid, data stored in the supplementary peaks layer.
        var s2 = reader.GetSpectrumDescription(2);
        Assert.IsTrue(s2.IsCentroid, "spectrum 2 should be centroid");
        var d2 = reader.GetSpectrumData(2);
        Assert.IsNotNull(d2);
        Assert.AreEqual(485, d2!.Mz.Length);
        Assert.AreEqual(231.38884, d2.Mz[0], 1e-4);
    }

    [TestMethod]
    public void HasUv_WavelengthSpectra_AppendedAfterMsSpectraWithCorrectValues()
    {
        using var reader = new MzPeakReader(HasUv);

        // 212 MS spectra + 520 UV/DAD wavelength spectra (separate wavelength_spectra_* entries).
        Assert.AreEqual(732, reader.SpectrumCount);

        // First wavelength spectrum is at global index 212 (right after the MS spectra).
        var uv = reader.GetSpectrumDescription(212);
        Assert.AreEqual("merged=212 row=0", uv.Id);
        Assert.IsTrue(uv.IsProfile);
        // The value array is wavelength (nm), not m/z.
        Assert.AreEqual("MS:1000617", uv.ValueArrayCurie);
        Assert.AreEqual("UO:0000018", uv.ValueArrayUnitCurie);

        var data = reader.GetSpectrumData(212);
        Assert.IsNotNull(data);
        Assert.AreEqual(96, data!.Mz.Length);
        Assert.AreEqual(210.0, data.Mz[0], 1e-4);   // value array carries wavelengths
        Assert.AreEqual(212.0, data.Mz[1], 1e-4);
        Assert.AreEqual(-0.10920, data.Intensity[0], 1e-4);

        // Last wavelength spectrum (global index 731).
        Assert.AreEqual("merged=731 row=519", reader.GetSpectrumId(731));
        var last = reader.GetSpectrumData(731);
        Assert.IsNotNull(last);
        Assert.AreEqual(96, last!.Mz.Length);
        Assert.AreEqual(210.0, last.Mz[0], 1e-4);
    }

    [TestMethod]
    public void HasUv_MsSpectraStillReadable_AlongsideWavelengthSpectra()
    {
        using var reader = new MzPeakReader(HasUv);

        // The MS spectra (indices 0..211) must still read correctly when wavelength spectra are present.
        var s0 = reader.GetSpectrumDescription(0);
        Assert.AreEqual("scanId=639", s0.Id);
        Assert.IsNull(s0.ValueArrayCurie, "MS spectra use m/z (no explicit value-array CURIE)");
        var d0 = reader.GetSpectrumData(0);
        Assert.IsNotNull(d0);
        Assert.IsTrue(d0!.Mz.Length > 0);
    }
}
