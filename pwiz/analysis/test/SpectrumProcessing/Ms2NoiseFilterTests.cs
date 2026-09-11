using System.Globalization;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Tests for <see cref="Ms2NoiseFilter"/> (the MS2 sliding-window top-N peak retention filter).
/// Cpp has no equivalent test (oversight): pwiz/analysis/spectrum_processing/MS2NoiseFilter.cpp
/// is referenced from <c>SpectrumList_PeakFilterTest.cpp</c> but no test cases exercise it. The
/// 3-case table here is original — exercises (1) wide-window top-N, (2) narrow sliding window,
/// (3) glycine + precursor cuts before windowed selection.
/// </summary>
[TestClass]
public class Ms2NoiseFilterTests
{
    private sealed record Ms2NoiseCase(
        string Label,
        string InputMz, string InputIntensity,
        string ExpectedMz, string ExpectedIntensity,
        double PrecursorMz, int PrecursorCharge,
        int PeaksInWindow, double WindowWidthDa, bool RelaxLowMass);

    private static readonly Ms2NoiseCase[] Ms2NoiseCases =
    {
        new("wide window: top-N kept globally (no precursor / glycine cuts)",
            // precursor 999 ch=1 → glycine cut at 942 (no peak above), precursor ±0.5 (no peak there)
            // window 1000 covers entire spectrum → top-3 by intensity = 400(60), 200(50), 500(30)
            InputMz:           "100 200 300 400 500",
            InputIntensity:    " 10  50  20  60  30",
            ExpectedMz:        "200 400 500",
            ExpectedIntensity: " 50  60  30",
            PrecursorMz: 999.0, PrecursorCharge: 1,
            PeaksInWindow: 3, WindowWidthDa: 1000.0, RelaxLowMass: false),

        new("narrow window: sliding-window top-1",
            // cpp's sliding-window: each kept peak starts a new window.
            // peaks {100,150,300,350,500,550}, intensities {10,50,20,60,30,70}, window=200, top-1:
            //   lb=100, window [100..300]: {100,150,300} top-1=150 → drop 100,300
            //   lb=150, window [150..350]: {150,350} top-1=350 → drop 150
            //   lb=350, window [350..550]: {350,500,550} top-1=550 → drop 350,500
            //   lb=550, window [550..750]: {550} → keep
            InputMz:           "100 150 300 350 500 550",
            InputIntensity:    " 10  50  20  60  30  70",
            ExpectedMz:        "550",
            ExpectedIntensity: " 70",
            PrecursorMz: 999.0, PrecursorCharge: 1,
            PeaksInWindow: 1, WindowWidthDa: 200.0, RelaxLowMass: false),

        new("glycine cut + precursor ±0.5 cut",
            // precursor 500 ch=2 → glycine cut at 500*2-57.0214640 = 942.978; precursor cut [499.5, 500.5]
            // 950 dropped by glycine; 499.6, 500, 500.5 dropped by precursor cut (inclusive)
            // remaining: 100, 200, 600, 800 — kept by top-100 wide window
            InputMz:           "100 200 499.6 500 500.5 600 800 950",
            InputIntensity:    "  1   5    10  50     7   3 100 999",
            ExpectedMz:        "100 200 600 800",
            ExpectedIntensity: "  1   5   3 100",
            PrecursorMz: 500.0, PrecursorCharge: 2,
            PeaksInWindow: 100, WindowWidthDa: 1000.0, RelaxLowMass: false),
    };

    [TestMethod]
    public void Filter_MatchesExpectedTable()
    {
        foreach (var c in Ms2NoiseCases)
        {
            var inner = new SpectrumListSimple();
            inner.Spectra.Add(BuildMs2Spectrum(c.PrecursorMz, c.PrecursorCharge,
                ParseDoubles(c.InputMz), ParseDoubles(c.InputIntensity)));

            var filter = new Ms2NoiseFilter(c.PeaksInWindow, c.WindowWidthDa, c.RelaxLowMass);
            var wrapped = new SpectrumListPeakFilter(inner, filter);
            var spec = wrapped.GetSpectrum(0, getBinaryData: true);

            AssertPeakArrays(c.Label, c.ExpectedMz, c.ExpectedIntensity, spec);
        }
    }

    private static double[] ParseDoubles(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture))
         .ToArray();

    private static Spectrum BuildMs2Spectrum(double precursorMz, int precursorCharge, double[] mz, double[] intensity)
    {
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.Params.Set(CVID.MS_MSn_spectrum);
        s.Precursors.Add(precursorCharge > 0
            ? new Precursor(precursorMz, precursorCharge)
            : new Precursor(precursorMz));
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        return s;
    }

    private static void AssertPeakArrays(string label, string expectedMz, string expectedIntensity, Spectrum actual)
    {
        var expectedMzArr = ParseDoubles(expectedMz);
        var expectedIntArr = ParseDoubles(expectedIntensity);
        var actualMz = actual.GetMZArray()?.Data ?? new List<double>();
        var actualInt = actual.GetIntensityArray()?.Data ?? new List<double>();

        Assert.AreEqual(expectedMzArr.Length, actualMz.Count, $"[{label}] m/z length");
        Assert.AreEqual(expectedIntArr.Length, actualInt.Count, $"[{label}] intensity length");
        for (int i = 0; i < expectedMzArr.Length; i++)
        {
            Assert.AreEqual(expectedMzArr[i], actualMz[i], 1e-5, $"[{label}] m/z[{i}]");
            Assert.AreEqual(expectedIntArr[i], actualInt[i], 1e-5, $"[{label}] intensity[{i}]");
        }
    }
}
