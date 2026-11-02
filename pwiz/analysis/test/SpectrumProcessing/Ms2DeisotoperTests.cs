using System.Globalization;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Port of pwiz cpp's <c>pwiz/analysis/spectrum_processing/MS2DeisotoperTest.cpp</c>: 3-case
/// Poisson-mode table-driven test driving <see cref="Ms2Deisotoper"/> through
/// <see cref="SpectrumListPeakFilter"/>.
/// </summary>
[TestClass]
public class Ms2DeisotoperTests
{
    private sealed record DeisotopeCase(
        string Label,
        string InputMz, string InputIntensity,
        string ExpectedMz, string ExpectedIntensity);

    private static readonly DeisotopeCase[] DeisotopeCases =
    {
        new("standard isotope chain",
            InputMz:           "300.0 302.1 303.11 304.12 305.20",
            InputIntensity:    "1.0   85.0  15.0   3.0    3.0",
            ExpectedMz:        "300.0 302.1 305.20",
            ExpectedIntensity: "1.0   85.0  3.0"),

        new("low-mass cluster",
            InputMz:           "299.5 300.01 300.52 301.03",
            InputIntensity:    "10.0  75.0   25.0   40.0",
            ExpectedMz:        "299.5 300.01 301.03",
            ExpectedIntensity: "10.0  75.0   40.0"),

        new("partially-overlapping chains",
            InputMz:           "302.1 302.435 302.77 302.94 303.11",
            InputIntensity:    "61.0  31.0    8.0    45.0   40.0",
            ExpectedMz:        "302.1 302.94 303.11",
            ExpectedIntensity: "61.0  45.0   40.0"),
    };

    [TestMethod]
    public void Poisson_MatchesCppGoldStandard()
    {
        // Mirror cpp ctor args: Poisson mode, mzTol=0.5, hires=false, minCharge=1, maxCharge=3.
        foreach (var c in DeisotopeCases)
        {
            var inner = new SpectrumListSimple();
            inner.Spectra.Add(BuildMs2Spectrum(precursorMz: 100.0,
                ParseDoubles(c.InputMz), ParseDoubles(c.InputIntensity)));

            var wrapped = new SpectrumListPeakFilter(inner,
                new Ms2Deisotoper(matchingTolerance: new MZTolerance(0.5),
                    hiRes: false, poisson: true, minCharge: 1, maxCharge: 3));

            var spec = wrapped.GetSpectrum(0, getBinaryData: true);
            AssertPeakArrays(c.Label, c.ExpectedMz, c.ExpectedIntensity, spec);
        }
    }

    private static double[] ParseDoubles(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture))
         .ToArray();

    private static Spectrum BuildMs2Spectrum(double precursorMz, double[] mz, double[] intensity)
    {
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_ms_level, 2);
        s.Params.Set(CVID.MS_MSn_spectrum);
        s.Precursors.Add(new Precursor(precursorMz));
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
