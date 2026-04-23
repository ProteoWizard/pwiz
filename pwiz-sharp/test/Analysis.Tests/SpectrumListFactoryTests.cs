using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests;

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

    // ---- identity-level ----

    [TestMethod]
    public void Wrap_Index_KeepsSpecifiedIndices()
    {
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "index 1 3-4");
        Assert.AreEqual(3, filtered.Count);
        CollectionAssert.AreEqual(
            new[] { 1, 3, 4 },
            Enumerable.Range(0, filtered.Count).Select(i => filtered.SpectrumIdentity(i).Index).ToList());
    }

    [TestMethod]
    public void Wrap_ScanNumber_MatchesScanAttribute()
    {
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "scanNumber 2 5");
        Assert.AreEqual(2, filtered.Count);
    }

    [TestMethod]
    public void Wrap_Id_ExactIdMatch()
    {
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "id scan=1,scan=4");
        Assert.AreEqual(2, filtered.Count);
    }

    // ---- loaded-spectrum predicates ----

    [TestMethod]
    public void Wrap_MsLevel_KeepsMatchingLevel()
    {
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "msLevel 2");
        Assert.AreEqual(3, filtered.Count);
    }

    [TestMethod]
    public void Wrap_ScanTime_RangeWorks()
    {
        // Build times 10..15 — range [11,13] keeps 3 spectra.
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "scanTime [11,13]");
        Assert.AreEqual(3, filtered.Count);
    }

    [TestMethod]
    public void Wrap_ScanTime_HyphenSyntax()
    {
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "scanTime 11-13");
        Assert.AreEqual(3, filtered.Count);
    }

    [TestMethod]
    public void Wrap_Polarity_Positive()
    {
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "polarity positive");
        Assert.AreEqual(3, filtered.Count);
    }

    [TestMethod]
    public void Wrap_Polarity_NegativeShorthand()
    {
        var filtered = SpectrumListFactory.Wrap(BuildMixedList(), "polarity neg");
        Assert.AreEqual(3, filtered.Count);
    }

    // ---- threshold chain ----

    [TestMethod]
    public void Wrap_Threshold_Count_MostIntense()
    {
        var list = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1", DefaultArrayLength = 5 };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.SetMZIntensityArrays(
            new[] { 1.0, 2, 3, 4, 5 },
            new[] { 100.0, 90, 80, 70, 60 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(s);

        var filtered = SpectrumListFactory.Wrap(list, "threshold count 3 most-intense");
        var spec = filtered.GetSpectrum(0, getBinaryData: true);
        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, spec.GetMZArray()!.Data);
    }

    [TestMethod]
    public void Wrap_Threshold_Absolute_DefaultOrientation()
    {
        var list = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "x", DefaultArrayLength = 4 };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.SetMZIntensityArrays(new[] { 1.0, 2.0, 3.0, 4.0 }, new[] { 5.0, 50, 500, 5000 }, CVID.MS_number_of_detector_counts);
        list.Spectra.Add(s);

        // "absolute 100" → default most-intense → keep intensity ≥ 100 → 500, 5000.
        var filtered = SpectrumListFactory.Wrap(list, "threshold absolute 100");
        var spec = filtered.GetSpectrum(0, getBinaryData: true);
        CollectionAssert.AreEqual(new[] { 3.0, 4.0 }, spec.GetMZArray()!.Data);
    }

    [TestMethod]
    public void Wrap_MetadataFixer_RecomputesAfterThreshold()
    {
        var list = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "x", DefaultArrayLength = 4 };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.SetMZIntensityArrays(
            new[] { 100.0, 200.0, 300.0, 400.0 },
            new[] { 10.0, 20, 30, 40 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(s);

        // Threshold drops the bottom two, then metadataFixer recomputes TIC on the remainder.
        var chained = SpectrumListFactory.Wrap(list, new[]
        {
            "threshold absolute 25",
            "metadataFixer",
        });

        var spec = chained.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(70.0, spec.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
        Assert.AreEqual(40.0, spec.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
        Assert.AreEqual(400.0, spec.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
    }

    // ---- error handling ----

    [TestMethod]
    public void Wrap_UnknownFilter_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            SpectrumListFactory.Wrap(BuildMixedList(), "nonsense 1-"));
    }

    [TestMethod]
    public void Wrap_Threshold_BadBy_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            SpectrumListFactory.Wrap(BuildMixedList(), "threshold bogus 5"));
    }

    // ---- registration ----

    [TestMethod]
    public void Register_CustomFilter_IsUsable()
    {
        int callCount = 0;
        SpectrumListFactory.Register("rot13", (_, inner) =>
        {
            callCount++;
            return inner;
        });
        try
        {
            SpectrumListFactory.Wrap(BuildMixedList(), "rot13 whatever");
            Assert.AreEqual(1, callCount);
        }
        finally
        {
            // Clean up so other tests don't see the stub — replace with a no-op.
            SpectrumListFactory.Register("rot13", (_, inner) => inner);
        }
    }
}
