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
        // Stale / wrong metadata so we can verify the fixer overwrites it.
        s.Params.Set(CVID.MS_base_peak_intensity, 999999.0, CVID.MS_number_of_detector_counts);
        s.Params.Set(CVID.MS_total_ion_current, 999999.0, CVID.MS_number_of_detector_counts);
        s.Params.Set(CVID.MS_lowest_observed_m_z, 0.0, CVID.MS_m_z);
        s.Params.Set(CVID.MS_highest_observed_m_z, 9999.0, CVID.MS_m_z);
        s.Params.Set(CVID.MS_base_peak_m_z, 0.0, CVID.MS_m_z);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        return s;
    }

    [TestMethod]
    public void MetadataFixer_RecomputesScenarios()
    {
        // Normal peaks: base peak m/z + intensity, TIC, min/max m/z all recomputed from binary.
        var normal = new SpectrumListSimple();
        normal.Spectra.Add(MakeSpectrum(
            new[] { 100.0, 200.0, 300.0, 400.0 },
            new[] { 50.0, 200.0, 150.0, 75.0 }));
        var fixedSpec = new SpectrumListMetadataFixer(normal).GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(200.0, fixedSpec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.0, fixedSpec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(475.0, fixedSpec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(100.0, fixedSpec.Params.CvParam(CVID.MS_lowest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(400.0, fixedSpec.Params.CvParam(CVID.MS_highest_observed_m_z).ValueAs<double>(), 1e-9);

        // Zero-intensity peaks must not influence min/max m/z or TIC.
        var withZeros = new SpectrumListSimple();
        withZeros.Spectra.Add(MakeSpectrum(
            new[] { 50.0, 100.0, 200.0, 500.0 },
            new[] { 0.0, 50.0, 200.0, 0.0 }));
        var zerosFixed = new SpectrumListMetadataFixer(withZeros).GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(100.0, zerosFixed.Params.CvParam(CVID.MS_lowest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(200.0, zerosFixed.Params.CvParam(CVID.MS_highest_observed_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(250.0, zerosFixed.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);

        // Empty spectrum zeros out the recomputed fields.
        var empty = new SpectrumListSimple();
        empty.Spectra.Add(MakeSpectrum(Array.Empty<double>(), Array.Empty<double>()));
        var emptyFixed = new SpectrumListMetadataFixer(empty).GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(0.0, emptyFixed.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(0.0, emptyFixed.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);

        // Calculate() exposes the raw computed values directly without going through a SpectrumList.
        var m = SpectrumListMetadataFixer.Calculate(
            new[] { 100.0, 200.0, 300.0 },
            new[] { 10.0, 100.0, 50.0 });
        Assert.AreEqual(200.0, m.BasePeakMz, 1e-9);
        Assert.AreEqual(100.0, m.BasePeakIntensity, 1e-9);
        Assert.AreEqual(160.0, m.TotalIntensity, 1e-9);
        Assert.AreEqual(100.0, m.LowestMz, 1e-9);
        Assert.AreEqual(300.0, m.HighestMz, 1e-9);
    }

    [TestMethod]
    public void ZeroSamplesFilter_BehaviorVariants()
    {
        // Removes zero-intensity peaks from m/z + intensity arrays.
        var inner = new SpectrumListSimple();
        inner.Spectra.Add(MakeSpectrum(
            new[] { 50.0, 100.0, 150.0, 200.0, 250.0 },
            new[] { 0.0, 50.0, 0.0, 200.0, 0.0 }));
        var filtered = new SpectrumListZeroSamplesFilter(inner).GetSpectrum(0, getBinaryData: true);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0 }, filtered.GetMZArray()!.Data);
        CollectionAssert.AreEqual(new[] { 50.0, 200.0 }, filtered.GetIntensityArray()!.Data);

        // MS-level gate: filter limited to MS2 → MS1 spectrum is untouched.
        var msLevelGated = new SpectrumListSimple();
        msLevelGated.Spectra.Add(MakeSpectrum(
            new[] { 50.0, 100.0, 150.0 }, new[] { 0.0, 50.0, 0.0 }));
        var ms2Filter = new SpectrumListZeroSamplesFilter(msLevelGated, new Pwiz.Util.Misc.IntegerSet(2));
        Assert.AreEqual(3, ms2Filter.GetSpectrum(0, getBinaryData: true).GetMZArray()!.Data.Count);

        // Fast-path: when binary data isn't requested, the filter doesn't re-materialize the spectrum.
        var passthrough = new SpectrumListSimple();
        passthrough.Spectra.Add(MakeSpectrum(new[] { 50.0, 100.0 }, new[] { 0.0, 50.0 }));
        var ptFilter = new SpectrumListZeroSamplesFilter(passthrough);
        Assert.AreEqual(2, ptFilter.GetSpectrum(0, getBinaryData: false).GetMZArray()!.Data.Count,
            "no-binary-data fast path should not rewrite arrays");
    }
}
