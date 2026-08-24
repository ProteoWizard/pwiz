using System.Globalization;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Port of the ETD/ECD section of pwiz cpp's
/// <c>pwiz/analysis/spectrum_processing/SpectrumList_PeakFilterTest.cpp::testPrecursorMassRemoval()</c>:
/// 7-case table-driven test driving <see cref="EtdPrecursorMassFilter"/> through
/// <see cref="SpectrumListPeakFilter"/>.
/// </summary>
/// <remarks>
/// Cpp's <c>SpectrumList_PeakFilterTest.cpp</c> also exercises <c>ThresholdFilter</c>
/// (<c>testThresholdFilter()</c>); pwiz-sharp keeps that in <c>ThresholdFilterTests.cs</c> for now
/// — folding it into this file matches cpp more closely but is out of scope for the Tier 2 port.
/// </remarks>
[TestClass]
public class SpectrumListPeakFilterTests
{
    private const double EtdPrecursorMz = 445.34;
    private const int EtdPrecursorCharge = 3;

    private sealed record EtdCase(
        string Label,
        string InputMz, string InputIntensity,
        string ExpectedMz, string ExpectedIntensity,
        double Tolerance, bool UsePpm,
        bool HasCharge,
        bool RemovePrecursor, bool RemoveChargeReduced,
        bool RemoveNeutralLoss, bool BlanketRemoval);

    private static readonly EtdCase[] EtdCases =
    {
        new("do nothing (all flags off)",
            InputMz:           "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            InputIntensity:    "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280",
            ExpectedMz:        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            ExpectedIntensity: "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280",
            Tolerance: 0.1234, UsePpm: false, HasCharge: false,
            RemovePrecursor: false, RemoveChargeReduced: false,
            RemoveNeutralLoss: false, BlanketRemoval: false),

        new("remove precursor only (with charge)",
            InputMz:           "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 445.35 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            InputIntensity:    "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280 290",
            ExpectedMz:        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            ExpectedIntensity: "10 20 30 40 50 60 70 80 90 100 110 120 130 140 170 180 190 200 210 220 230 240 250 260 270 280 290",
            Tolerance: 0.1234, UsePpm: false, HasCharge: true,
            RemovePrecursor: true, RemoveChargeReduced: false,
            RemoveNeutralLoss: false, BlanketRemoval: false),

        new("remove precursor without charge state",
            InputMz:           "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 445.35 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            InputIntensity:    "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280 290",
            ExpectedMz:        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            ExpectedIntensity: "10 20 30 40 50 60 70 80 90 100 110 120 130 140 170 180 190 200 210 220 230 240 250 260 270 280 290",
            Tolerance: 0.1234, UsePpm: false, HasCharge: false,
            RemovePrecursor: true, RemoveChargeReduced: false,
            RemoveNeutralLoss: false, BlanketRemoval: false),

        new("remove charge-reduced precursors only",
            InputMz:           "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 668.01 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            InputIntensity:    "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 155 160 170 180 190 200 210 220 230 240 250 260 270 280",
            ExpectedMz:        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            ExpectedIntensity: "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 160 170 180 190 200 210 220 230 240 250 260 270 280",
            Tolerance: 0.1234, UsePpm: false, HasCharge: true,
            RemovePrecursor: false, RemoveChargeReduced: true,
            RemoveNeutralLoss: false, BlanketRemoval: false),

        new("remove precursor + charge-reduced",
            InputMz:           "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 445.34 668.01 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            InputIntensity:    "10 20 30 40 50 60 70 80 90 100 110 120 130 140 150 155 160 170 180 190 200 210 220 230 240 250 260 270 280",
            ExpectedMz:        "100 110 120 130 415.8215 422.337 422.8295 423.3215 427.3295 427.8215 428.3135 429.327 431.3425 436.3345 831.632 844.674 845.659 846.643 854.659 855.643 856.627 858.654 862.685 872.669 873.653 890.68 1000.0",
            ExpectedIntensity: "10 20 30 40 50 60 70 80 90 100 110 120 130 140 160 170 180 190 200 210 220 230 240 250 260 270 280",
            Tolerance: 0.1234, UsePpm: false, HasCharge: true,
            RemovePrecursor: true, RemoveChargeReduced: true,
            RemoveNeutralLoss: false, BlanketRemoval: false),

        new("remove precursor + charge-reduced + neutral losses",
            InputMz:           "100 120 445.34 667.51 668.01 1335.02 1336.02 1400.",
            InputIntensity:    "10 20 30 40 50 60 70 80",
            ExpectedMz:        "100 120",
            ExpectedIntensity: "10 20",
            Tolerance: 0.01, UsePpm: false, HasCharge: true,
            RemovePrecursor: true, RemoveChargeReduced: true,
            RemoveNeutralLoss: true, BlanketRemoval: false),

        new("blanket neutral-loss removal (60-Da window)",
            InputMz:           "100 120 445.34 667.51 668.01 1335.02 1336.02 1400.",
            InputIntensity:    "10 20 30 40 50 60 70 80",
            ExpectedMz:        "100 120",
            ExpectedIntensity: "10 20",
            Tolerance: 0.01, UsePpm: false, HasCharge: true,
            RemovePrecursor: true, RemoveChargeReduced: true,
            RemoveNeutralLoss: true, BlanketRemoval: true),
    };

    [TestMethod]
    public void EtdPrecursorMassRemoval_MatchesCppGoldStandard()
    {
        foreach (var c in EtdCases)
        {
            var inner = new SpectrumListSimple();
            inner.Spectra.Add(BuildEtdSpectrum(c));

            var tol = new MZTolerance(c.Tolerance, c.UsePpm ? MZToleranceUnits.Ppm : MZToleranceUnits.Mz);
            // cpp wires up the filter with NeutralLossSpecies=defaults when removeNL=true,
            // empty when removeNL=false (independent of blanket flag).
            var nlOverride = c.RemoveNeutralLoss ? null : Array.Empty<string>();
            var filter = new EtdPrecursorMassFilter(tol,
                removePrecursor: c.RemovePrecursor,
                removeChargeReducedPrecursors: c.RemoveChargeReduced,
                useBlanketFiltering: c.BlanketRemoval,
                neutralLossFormulae: nlOverride);
            var wrapped = new SpectrumListPeakFilter(inner, filter);

            var spec = wrapped.GetSpectrum(0, getBinaryData: true);
            AssertPeakArrays(c.Label, c.ExpectedMz, c.ExpectedIntensity, spec);
        }
    }

    private static double[] ParseDoubles(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture))
         .ToArray();

    private static Spectrum BuildEtdSpectrum(EtdCase c)
    {
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_MSn_spectrum);
        s.Params.Set(CVID.MS_ms_level, 2);

        s.SetMZIntensityArrays(ParseDoubles(c.InputMz), ParseDoubles(c.InputIntensity),
            CVID.MS_number_of_detector_counts);

        var precursor = new Precursor();
        precursor.Activation.Set(CVID.MS_electron_transfer_dissociation);
        var ion = new SelectedIon();
        ion.Set(CVID.MS_selected_ion_m_z, EtdPrecursorMz, CVID.MS_m_z);
        if (c.HasCharge) ion.Set(CVID.MS_charge_state, EtdPrecursorCharge);
        precursor.SelectedIons.Add(ion);
        s.Precursors.Add(precursor);
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
