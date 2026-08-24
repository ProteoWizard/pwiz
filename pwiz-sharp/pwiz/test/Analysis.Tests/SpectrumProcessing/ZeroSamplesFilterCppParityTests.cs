using System.Globalization;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Port of cpp's <c>SpectrumList_PeakFilterTest.cpp::testZeroSamplesFilter()</c>, over the same
/// 3026-sample profile spectrum (<c>TestData/zero-samples-profile.txt</c>, extracted verbatim from
/// cpp's RawX/RawY literals).
/// </summary>
/// <remarks>
/// The point of these assertions is that removeExtra does NOT strip every zero from a profile
/// spectrum: it leaves one flanking zero on each side of a non-zero run, which is what lets peak
/// picking see where a peak returns to baseline. That is why cpp checks the filtered array still
/// begins and ends with a zero whose neighbour is non-zero.
/// </remarks>
[TestClass]
public class ZeroSamplesFilterCppParityTests
{
    private static (double[] Mz, double[] Intensity) LoadProfile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "zero-samples-profile.txt");
        var lines = File.ReadAllLines(path);
        Assert.AreEqual(2, lines.Length, "fixture should hold one m/z line and one intensity line");
        return (Parse(lines[0]), Parse(lines[1]));
    }

    private static double[] Parse(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture))
         .ToArray();

    private static SpectrumListSimple BuildList(double[] mz, double[] intensity)
    {
        var inner = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_MSn_spectrum);
        s.Params.Set(CVID.MS_ms_level, 2);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);

        var precursor = new Precursor();
        precursor.Activation.Set(CVID.MS_electron_transfer_dissociation);
        var ion = new SelectedIon();
        ion.Set(CVID.MS_selected_ion_m_z, 1000.0, CVID.MS_m_z);
        ion.Set(CVID.MS_charge_state, 2);
        precursor.SelectedIons.Add(ion);
        s.Precursors.Add(precursor);

        inner.Spectra.Add(s);
        return inner;
    }

    /// <summary>
    /// cpp runs all three stages against ONE list, and that ordering is load-bearing:
    /// <see cref="SpectrumListSimple"/> hands out its stored spectrum rather than a copy, and the
    /// filter rewrites the arrays in place, so each stage sees what the previous one left behind.
    /// Rebuilding the list per stage changes the answers - addMissing over the untrimmed spectrum
    /// puts the first real sample at index 14 rather than cpp's 10.
    /// </summary>
    [TestMethod]
    public void ZeroSamplesFilter_MatchesCppSequence()
    {
        var (mz, intensity) = LoadProfile();
        var list = BuildList(mz, intensity);
        const int nzeros = 10;

        // Restricted to MS3, an MS2 spectrum must come back untouched.
        var unfiltered = new SpectrumListZeroSamplesFilter(list, new IntegerSet(3))
            .GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(intensity[9], unfiltered.GetIntensityArray()!.Data[9], 1e-9,
            "an out-of-scope MS level is left alone");

        // removeExtra on a profile spectrum keeps one flanking zero per non-zero run.
        var filtered = new SpectrumListZeroSamplesFilter(list, new IntegerSet(2))
            .GetSpectrum(0, getBinaryData: true);
        var y = filtered.GetIntensityArray()!.Data;

        Assert.AreEqual(filtered.GetMZArray()!.Data.Count, y.Count, "arrays stay the same length");
        Assert.AreEqual(0.0, y[0], "a flanking zero survives at the front");
        Assert.AreNotEqual(0.0, y[1], "and the sample after it is the first real one");
        Assert.AreEqual(0.0, y[y.Count - 1], "a flanking zero survives at the back");
        Assert.AreNotEqual(0.0, y[y.Count - 2], "and the sample before it is a real one");
        Assert.AreEqual(intensity[9], y[1], 1e-9, "the first real sample is input index 9");

        // addMissing then pads that trimmed spectrum out to the requested flank.
        var filled = new SpectrumListZeroSamplesFilter(list, new IntegerSet(2),
                ZeroSamplesMode.AddMissing, nzeros)
            .GetSpectrum(0, getBinaryData: true);
        var padded = filled.GetIntensityArray()!.Data;

        Assert.AreEqual(0.0, padded[0]);
        Assert.AreEqual(0.0, padded[1]);
        Assert.AreEqual(0.0, padded[padded.Count - 1]);
        Assert.AreEqual(0.0, padded[padded.Count - 2]);
        Assert.AreEqual(intensity[9], padded[nzeros], 1e-9,
            "the first real sample sits just past the requested flank");
    }
}
