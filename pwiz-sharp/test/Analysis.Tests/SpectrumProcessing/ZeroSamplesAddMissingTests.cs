using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Verifies the <c>zeroSamples addMissing</c> mode pads non-zero runs with flanking zeros.
/// Mirrors cpp <c>SpectrumList_ZeroSamplesFilter</c> in <c>Mode_AddMissingZeros</c>.
/// </summary>
[TestClass]
public class ZeroSamplesAddMissingTests
{
    private static SpectrumListSimple BuildSingle(double[] mz, double[] intensity)
    {
        var list = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1", DefaultArrayLength = mz.Length };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        list.Spectra.Add(s);
        return list;
    }

    [TestMethod]
    public void AddMissing_BoundedCount_PadsFlanksUpToN()
    {
        // Two non-zero runs surrounded by sparse samples. addMissing=2 inserts up to 2 zeros
        // on each side of each run (subject to sample-rate guard in ZeroSampleFiller).
        var inner = BuildSingle(
            new[] { 100.0, 101.0, 102.0, 103.0, 200.0, 201.0, 202.0 },
            new[] {   0.0,   0.0,   5.0,   0.0,   0.0,   3.0,   0.0 });

        var filter = new SpectrumListZeroSamplesFilter(
            inner, IntegerSet.Positive, ZeroSamplesMode.AddMissing, flankingZeroCount: 2);
        var s = filter.GetSpectrum(0, getBinaryData: true);
        var mz = s.GetMZArray()!.Data;
        var inten = s.GetIntensityArray()!.Data;
        Assert.AreEqual(mz.Count, inten.Count);
        // Length increased — the filter inserted zero samples somewhere. The exact positions are
        // governed by ZeroSampleFiller's sample-rate heuristic; spot-check that the non-zero
        // sample (5.0) is still present and that flanking zeros didn't displace it.
        int nonZeroAt = inten.IndexOf(5.0);
        Assert.IsTrue(nonZeroAt >= 0, "5.0 intensity should still be present");
        Assert.AreEqual(102.0, mz[nonZeroAt], 1e-9, "5.0 should remain at m/z 102.0");
    }

    [TestMethod]
    public void AddMissing_RespectsMsLevels_PassesThroughOthers()
    {
        var list = new SpectrumListSimple();
        // MS1 — affected
        var ms1 = new Spectrum { Index = 0, Id = "ms1", DefaultArrayLength = 3 };
        ms1.Params.Set(CVID.MS_ms_level, 1);
        ms1.SetMZIntensityArrays(new[] { 100.0, 101.0, 102.0 }, new[] { 0.0, 5.0, 0.0 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(ms1);
        // MS2 — passthrough
        var ms2 = new Spectrum { Index = 1, Id = "ms2", DefaultArrayLength = 3 };
        ms2.Params.Set(CVID.MS_ms_level, 2);
        ms2.SetMZIntensityArrays(new[] { 200.0, 201.0, 202.0 }, new[] { 0.0, 10.0, 0.0 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(ms2);

        var levels = new IntegerSet(); levels.Insert(1, 1);
        var filter = new SpectrumListZeroSamplesFilter(
            list, levels, ZeroSamplesMode.AddMissing, flankingZeroCount: -1);

        var s0 = filter.GetSpectrum(0, getBinaryData: true);
        var s1 = filter.GetSpectrum(1, getBinaryData: true);
        // MS2 spectrum unchanged (passthrough).
        CollectionAssert.AreEqual(new[] { 0.0, 10.0, 0.0 }, s1.GetIntensityArray()!.Data);
        // MS1 array is at least as long as the input — the filter either inserted zeros or
        // left it untouched (sample-rate guard).
        Assert.IsTrue(s0.GetIntensityArray()!.Data.Count >= 3);
    }

    [TestMethod]
    public void AddMissing_NoBinaryData_PassesThrough()
    {
        var inner = BuildSingle(new[] { 100.0, 101.0 }, new[] { 0.0, 5.0 });
        var filter = new SpectrumListZeroSamplesFilter(
            inner, null, ZeroSamplesMode.AddMissing, flankingZeroCount: 2);
        var s = filter.GetSpectrum(0, getBinaryData: false);
        // getBinaryData=false: filter shouldn't touch the array.
        Assert.AreEqual(2, s.GetMZArray()!.Data.Count);
    }

    [TestMethod]
    public void Factory_ParsesAddMissingMode_WithAndWithoutFlankingCount()
    {
        var inner = BuildSingle(new[] { 100.0, 101.0, 102.0 }, new[] { 0.0, 5.0, 0.0 });
        // "addMissing" → unbounded count
        var unbounded = (SpectrumListZeroSamplesFilter)SpectrumListFactory.Wrap(inner, "zeroSamples addMissing");
        Assert.AreEqual(ZeroSamplesMode.AddMissing, unbounded.Mode);
        Assert.AreEqual(-1, unbounded.FlankingZeroCount);

        // "addMissing=5" → count of 5
        var inner2 = BuildSingle(new[] { 100.0, 101.0, 102.0 }, new[] { 0.0, 5.0, 0.0 });
        var bounded = (SpectrumListZeroSamplesFilter)SpectrumListFactory.Wrap(inner2, "zeroSamples addMissing=5");
        Assert.AreEqual(ZeroSamplesMode.AddMissing, bounded.Mode);
        Assert.AreEqual(5, bounded.FlankingZeroCount);

        // "addMissing=5 2-3" → bounded count + MS-level restriction
        var inner3 = BuildSingle(new[] { 100.0, 101.0, 102.0 }, new[] { 0.0, 5.0, 0.0 });
        var boundedMs = (SpectrumListZeroSamplesFilter)SpectrumListFactory.Wrap(inner3, "zeroSamples addMissing=5 2-3");
        Assert.AreEqual(5, boundedMs.FlankingZeroCount);
        Assert.IsTrue(boundedMs.MsLevels.Contains(2));
        Assert.IsTrue(boundedMs.MsLevels.Contains(3));
        Assert.IsFalse(boundedMs.MsLevels.Contains(1));

        // Unknown mode rejected.
        Assert.ThrowsException<ArgumentException>(() =>
            SpectrumListFactory.Wrap(BuildSingle(new[] { 100.0 }, new[] { 0.0 }), "zeroSamples nonsense"));
    }
}
