using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.PeakFilters;

[TestClass]
public class MetadataAndZeroSamplesTests
{
    private static Spectrum MakeSpectrum(double[] mz, double[] intensity)
    {
        var s = new Spectrum { Index = 0, Id = "test", DefaultArrayLength = mz.Length };
        s.Params.Set(CVID.MS_ms_level, 1);
        // Populate with stale/wrong metadata so we can verify the fixer replaces it.
        s.Params.Set(CVID.MS_base_peak_intensity, 999999.0, CVID.MS_number_of_detector_counts);
        s.Params.Set(CVID.MS_total_ion_current, 999999.0, CVID.MS_number_of_detector_counts);
        s.Params.Set(CVID.MS_lowest_observed_m_z, 0.0, CVID.MS_m_z);
        s.Params.Set(CVID.MS_highest_observed_m_z, 9999.0, CVID.MS_m_z);
        s.Params.Set(CVID.MS_base_peak_m_z, 0.0, CVID.MS_m_z);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        return s;
    }

    // ---- MetadataFixer ----

    [TestMethod]
    public void MetadataFixer_RecomputesAllFields()
    {
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(
            new[] { 100.0, 200.0, 300.0, 400.0 },
            new[] { 50.0, 200.0, 150.0, 75.0 }));

        var fixer = new SpectrumListMetadataFixer(inner);
        var fixedSpec = fixer.GetSpectrum(0, getBinaryData: true);

        Assert.AreEqual(200.0, fixedSpec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.0, fixedSpec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(475.0, fixedSpec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(100.0, fixedSpec.Params.CvParam(CVID.MS_lowest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(400.0, fixedSpec.Params.CvParam(CVID.MS_highest_observed_m_z).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void MetadataFixer_IgnoresZeroIntensityPeaks()
    {
        // A zero-intensity peak should not count toward min/max m/z or TIC.
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(
            new[] { 50.0, 100.0, 200.0, 500.0 },
            new[] { 0.0, 50.0, 200.0, 0.0 }));

        var fixed_ = new SpectrumListMetadataFixer(inner).GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(100.0, fixed_.Params.CvParam(CVID.MS_lowest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.0, fixed_.Params.CvParam(CVID.MS_highest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(250.0, fixed_.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void MetadataFixer_EmptySpectrum_ZerosOut()
    {
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(Array.Empty<double>(), Array.Empty<double>()));

        var fixed_ = new SpectrumListMetadataFixer(inner).GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(0.0, fixed_.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(0.0, fixed_.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void MetadataFixer_Calculate_StandaloneHelper()
    {
        var m = SpectrumListMetadataFixer.Calculate(
            new[] { 100.0, 200.0, 300.0 },
            new[] { 10.0, 100.0, 50.0 });
        Assert.AreEqual(200.0, m.BasePeakMz, 1e-9);
        Assert.AreEqual(100.0, m.BasePeakIntensity, 1e-9);
        Assert.AreEqual(160.0, m.TotalIntensity, 1e-9);
        Assert.AreEqual(100.0, m.LowestMz, 1e-9);
        Assert.AreEqual(300.0, m.HighestMz, 1e-9);
    }

    // ---- ZeroSamplesFilter ----

    [TestMethod]
    public void ZeroSamplesFilter_RemovesZeroIntensityPeaks()
    {
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(
            new[] { 50.0, 100.0, 150.0, 200.0, 250.0 },
            new[] { 0.0, 50.0, 0.0, 200.0, 0.0 }));

        var filtered = new SpectrumListZeroSamplesFilter(inner);
        var spec = filtered.GetSpectrum(0, getBinaryData: true);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0 }, spec.GetMZArray()!.Data);
        CollectionAssert.AreEqual(new[] { 50.0, 200.0 }, spec.GetIntensityArray()!.Data);
    }

    [TestMethod]
    public void ZeroSamplesFilter_RespectsMsLevelGate()
    {
        // Filter limited to MS level 2; this spectrum is MS1 — no-op.
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(
            new[] { 50.0, 100.0, 150.0 },
            new[] { 0.0, 50.0, 0.0 }));

        var filter = new SpectrumListZeroSamplesFilter(inner, new Pwiz.Util.Misc.IntegerSet(2));
        var spec = filter.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(3, spec.GetMZArray()!.Data.Count);
    }

    [TestMethod]
    public void ZeroSamplesFilter_NoBinaryDataRequest_Passthrough()
    {
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(
            new[] { 50.0, 100.0 },
            new[] { 0.0, 50.0 }));

        var filter = new SpectrumListZeroSamplesFilter(inner);
        // When binary data isn't requested we shouldn't modify anything (fast-path).
        var spec = filter.GetSpectrum(0, getBinaryData: false);
        // The zero sample is still there since filter didn't run.
        Assert.AreEqual(2, spec.GetMZArray()!.Data.Count);
    }
}
