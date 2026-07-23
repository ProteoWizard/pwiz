using Pwiz.Analysis;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class SpectrumListFactoryTests
{
    private static SpectrumListSimple BuildMixedList()
    {
        var list = new SpectrumListSimple();
        for (int i = 0; i < 6; i++)
        {
            var s = new Spectrum { Index = i, Id = $"scan={i + 1}" };
            int msLevel = (i % 2) + 1; // alternating MS1, MS2
            s.Params.Set(CVID.MS_ms_level, msLevel);
            s.Params.Set(i < 3 ? CVID.MS_positive_scan : CVID.MS_negative_scan);
            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 10.0 + i, CVID.UO_second);
            s.ScanList.Scans.Add(scan);
            list.Spectra.Add(s);
        }
        return list;
    }

    private static SpectrumListSimple BuildProfileList()
    {
        // Two peaks in a 9-point profile (max at 102 and 106).
        var list = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.Params.Set(CVID.MS_profile_spectrum);
        s.SetMZIntensityArrays(
            new[] { 100.0, 101.0, 102.0, 103.0, 104.0, 105.0, 106.0, 107.0, 108.0 },
            new[] {   0.0,   1.0,   5.0,   1.0,   0.0,   1.0,   7.0,   1.0,   0.0 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(s);
        return list;
    }

    [TestMethod]
    public void Wrap_IdentityFilters_IndexScanNumberAndId()
    {
        // index 1 3-4 → keep indices 1, 3, 4 in order.
        var byIndex = SpectrumListFactory.Wrap(BuildMixedList(), "index 1 3-4");
        Assert.AreEqual(3, byIndex.Count);
        CollectionAssert.AreEqual(
            new[] { 1, 3, 4 },
            Enumerable.Range(0, byIndex.Count).Select(i => byIndex.SpectrumIdentity(i).Index).ToList());

        // scanNumber matches the "scan=N" attribute on the spectrum id.
        Assert.AreEqual(2, SpectrumListFactory.Wrap(BuildMixedList(), "scanNumber 2 5").Count);

        // id matches an exact id (or comma-separated set).
        Assert.AreEqual(2, SpectrumListFactory.Wrap(BuildMixedList(), "id scan=1,scan=4").Count);
    }

    [TestMethod]
    public void Wrap_LoadedFilters_MsLevelScanTimePolarity()
    {
        // msLevel keeps only that level (3 of the 6 spectra are MS2).
        Assert.AreEqual(3, SpectrumListFactory.Wrap(BuildMixedList(), "msLevel 2").Count);

        // scanTime accepts both [a,b] and a-b syntax for inclusive bounds.
        Assert.AreEqual(3, SpectrumListFactory.Wrap(BuildMixedList(), "scanTime [11,13]").Count);
        Assert.AreEqual(3, SpectrumListFactory.Wrap(BuildMixedList(), "scanTime 11-13").Count);

        // polarity accepts both full names and the "neg" / "pos" shorthands.
        Assert.AreEqual(3, SpectrumListFactory.Wrap(BuildMixedList(), "polarity positive").Count);
        Assert.AreEqual(3, SpectrumListFactory.Wrap(BuildMixedList(), "polarity neg").Count);
    }

    [TestMethod]
    public void Wrap_Threshold_CountAndAbsolute()
    {
        // threshold count 3 most-intense → keep top 3 peaks by intensity.
        var byCount = new SpectrumListSimple();
        var s1 = new Spectrum { Index = 0, Id = "scan=1", DefaultArrayLength = 5 };
        s1.Params.Set(CVID.MS_ms_level, 2);
        s1.SetMZIntensityArrays(
            new[] { 1.0, 2, 3, 4, 5 }, new[] { 100.0, 90, 80, 70, 60 },
            CVID.MS_number_of_detector_counts);
        byCount.Spectra.Add(s1);
        var threshCount = SpectrumListFactory.Wrap(byCount, "threshold count 3 most-intense");
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 },
            threshCount.GetSpectrum(0, getBinaryData: true).GetMZArray()!.Data);

        // threshold absolute N (default most-intense) keeps intensities >= N.
        var byAbs = new SpectrumListSimple();
        var s2 = new Spectrum { Index = 0, Id = "x", DefaultArrayLength = 4 };
        s2.Params.Set(CVID.MS_ms_level, 2);
        s2.SetMZIntensityArrays(
            new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 50, 500, 5000 },
            CVID.MS_number_of_detector_counts);
        byAbs.Spectra.Add(s2);
        var threshAbs = SpectrumListFactory.Wrap(byAbs, "threshold absolute 100");
        CollectionAssert.AreEqual(new[] { 3.0, 4.0 },
            threshAbs.GetSpectrum(0, getBinaryData: true).GetMZArray()!.Data);
    }

    [TestMethod]
    public void Wrap_Chained_ThresholdThenMetadataFixer()
    {
        // Threshold drops the bottom two; metadataFixer recomputes TIC / base peak afterwards.
        var list = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "x", DefaultArrayLength = 4 };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.SetMZIntensityArrays(
            new[] { 100.0, 200.0, 300.0, 400.0 },
            new[] { 10.0, 20, 30, 40 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(s);

        var chained = SpectrumListFactory.Wrap(list, new[] { "threshold absolute 25", "metadataFixer" });
        var spec = chained.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(70.0, spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(40.0, spec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
        Assert.AreEqual(400.0, spec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void Wrap_PeakPicking_VariantsAndMsLevelSyntax()
    {
        // peakPicking true 1- centroids profile data → 2 peaks at 102 and 106.
        var picked = SpectrumListFactory.Wrap(BuildProfileList(), "peakPicking true 1-")
            .GetSpectrum(0, getBinaryData: true);
        Assert.IsTrue(picked.Params.HasCVParam(CVID.MS_centroid_spectrum));
        Assert.IsFalse(picked.Params.HasCVParam(CVID.MS_profile_spectrum));
        CollectionAssert.AreEqual(new[] { 102.0, 106.0 }, picked.GetMZArray()!.Data);

        // peakPicking false 1- still falls through to LocalMaximum on a SpectrumListSimple input.
        var fallback = SpectrumListFactory.Wrap(BuildProfileList(), "peakPicking false 1-")
            .GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(2, fallback.GetMZArray()!.Data.Count);

        // peakPicking ... msLevel=N- syntax accepted (cpp-style).
        var msLevelSyntax = SpectrumListFactory.Wrap(BuildProfileList(), "peakPicking true msLevel=1-")
            .GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(2, msLevelSyntax.GetMZArray()!.Data.Count);
    }

    [TestMethod]
    public void Wrap_ErrorPaths()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            SpectrumListFactory.Wrap(BuildMixedList(), "nonsense 1-"), "unknown filter name");
        Assert.ThrowsException<ArgumentException>(() =>
            SpectrumListFactory.Wrap(BuildMixedList(), "threshold bogus 5"), "unknown threshold by-mode");
    }

    [TestMethod]
    public void Register_CustomFilter_IsUsable()
    {
        // Register a stub, invoke it via Wrap, then restore a no-op so it doesn't bleed into other tests.
        int callCount = 0;
        SpectrumListFactory.Register("rot13", (_, inner, _) => { callCount++; return inner; });
        try
        {
            SpectrumListFactory.Wrap(BuildMixedList(), "rot13 whatever");
            Assert.AreEqual(1, callCount);
        }
        finally
        {
            SpectrumListFactory.Register("rot13", (_, inner, _) => inner);
        }
    }

    [TestMethod]
    public void Wrap_Tier2Filters_DispatchByName()
    {
        // Per-filter argument-string parse + builder check, mirroring the spirit of cpp's
        // SpectrumListFactoryTest.cpp (cpp covers each filterCreator_* by argument string).
        var inner = new SpectrumListSimple();
        var cases = new (string Filter, Type ExpectedType)[]
        {
            ("ms2denoise 6 30 false",                         typeof(SpectrumListPeakFilter)),
            ("ms2denoise",                                    typeof(SpectrumListPeakFilter)),
            ("ms2deisotope hi_res mzTol=0.02",                typeof(SpectrumListPeakFilter)),
            ("ms2deisotope Poisson minCharge=1 maxCharge=4",  typeof(SpectrumListPeakFilter)),
            ("etdfilter true true true false 3.1 mz",         typeof(SpectrumListPeakFilter)),
            ("etdfilter",                                     typeof(SpectrumListPeakFilter)),
            ("scansumming precursorTol=0.05 scanTimeTol=10",  typeof(SpectrumListScanSummer)),
        };
        foreach (var (filter, expectedType) in cases)
        {
            var wrapped = SpectrumListFactory.Wrap(inner, filter);
            Assert.IsInstanceOfType(wrapped, expectedType, $"filter '{filter}' produced {wrapped.GetType().Name}");
        }
    }

    [TestMethod]
    public void Wrap_ZeroSamples_ParsesBothModesAndArgs()
    {
        // Both modes + arg-string variants in one table. mode/count are validated by casting
        // back to SpectrumListZeroSamplesFilter and reading the properties; msLevels is
        // checked by membership probes on the IntegerSet.
        var inner = new SpectrumListSimple();
        var cases = new (string Spec, ZeroSamplesMode Mode, int Count, int[] MsLevelsIn, int[] MsLevelsOut)[]
        {
            // removeExtra (default + alias + explicit + MS-level tail).
            ("zeroSamples",                ZeroSamplesMode.RemoveExtra, -1, new[] { 1, 5 }, System.Array.Empty<int>()),
            ("zeroSamples remove",         ZeroSamplesMode.RemoveExtra, -1, new[] { 1, 5 }, System.Array.Empty<int>()),
            ("zeroSamples removeExtra",    ZeroSamplesMode.RemoveExtra, -1, new[] { 1, 5 }, System.Array.Empty<int>()),
            ("zeroSamples removeExtra 2-3", ZeroSamplesMode.RemoveExtra, -1, new[] { 2, 3 }, new[] { 1, 4 }),
            // addMissing (no count, with count, with count + MS-level tail).
            ("zeroSamples addMissing",     ZeroSamplesMode.AddMissing,  -1, new[] { 1, 5 }, System.Array.Empty<int>()),
            ("zeroSamples addMissing=5",   ZeroSamplesMode.AddMissing,   5, new[] { 1, 5 }, System.Array.Empty<int>()),
            ("zeroSamples addMissing=5 2-3", ZeroSamplesMode.AddMissing, 5, new[] { 2, 3 }, new[] { 1, 4 }),
        };
        foreach (var (spec, mode, count, levelsIn, levelsOut) in cases)
        {
            var wrapped = (SpectrumListZeroSamplesFilter)SpectrumListFactory.Wrap(inner, spec);
            Assert.AreEqual(mode, wrapped.Mode, $"mode for '{spec}'");
            Assert.AreEqual(count, wrapped.FlankingZeroCount, $"flanking count for '{spec}'");
            foreach (int lvl in levelsIn)
                Assert.IsTrue(wrapped.MsLevels.Contains(lvl), $"'{spec}' should include MS level {lvl}");
            foreach (int lvl in levelsOut)
                Assert.IsFalse(wrapped.MsLevels.Contains(lvl), $"'{spec}' should exclude MS level {lvl}");
        }

        // Unknown mode is rejected.
        Assert.ThrowsException<ArgumentException>(() =>
            SpectrumListFactory.Wrap(inner, "zeroSamples nonsense"));
    }
}
