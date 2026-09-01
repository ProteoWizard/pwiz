using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Port of pwiz cpp's <c>pwiz/analysis/spectrum_processing/SpectrumList_ScanSummerTest.cpp</c>:
/// 8-input → 5-output table-driven test driving <see cref="SpectrumListScanSummer"/>. Verifies
/// MS1 sort+bin (sumMs1=true), MS2 grouping by precursor m/z + scan time + ion mobility,
/// median CV-param patching, and auxiliary parallel-array drop before summing.
/// </summary>
[TestClass]
public class SpectrumListScanSummerTests
{
    private sealed record ScanSummerInput(
        string InputMz, string InputIntensity,
        double PrecursorMz, double ScanTimeSec, double IonMobility, int MsLevel);

    private sealed record ScanSummerExpected(
        string Label,
        string ExpectedMz, string ExpectedIntensity,
        double ExpectedPrecursorMz, double ExpectedScanTimeSec, double ExpectedIonMobility,
        int ExpectedMsLevel);

    private static readonly ScanSummerInput[] Inputs =
    {
        // MS1 spectrum with near-duplicate peaks (sumMs1=true should bin within 1e-6 Da)
        new("112 112.0000001 112.1 120 121 123 124 128 129 112 112.0000001 112.1 120 121 123 124 128 129",
            "  3           2     6   0   1   4   2   1   7   3           2     6   0   1   4   2   1   7",
            PrecursorMz: 0, ScanTimeSec: 20.0, IonMobility: 0, MsLevel: 1),

        new("112.0001 119 120 121 122 123 124 127 128 129",
            "       1   4   5   6   7   8   9  10  11  12",
            PrecursorMz: 0, ScanTimeSec: 20.1, IonMobility: 0, MsLevel: 1),

        // MS2 group around precursor 120 (3 spectra below: indices 2, 3, 5 — same precursor ±0.04, time ±1.05)
        new("112 112.0000001 112.1 120 121 123 124 128 129",
            "  3           2     5   0   1   4   2   1   7",
            PrecursorMz: 120.0, ScanTimeSec: 20.0, IonMobility: 0, MsLevel: 2),

        new("112.0001 119 120 121 122 123 124 127 128 129",
            "       1   4   5   6   7   8   9  10  11  12",
            PrecursorMz: 120.01, ScanTimeSec: 20.1, IonMobility: 0, MsLevel: 2),

        // MS2 group around precursor 401, IM ~1.0 (indices 4 and 6 group; index 7 is standalone IM=2)
        new("200 200.1 200.2 200.9 202",
            "1.0 3.0 1.0 0.0 3.0",
            PrecursorMz: 401.23, ScanTimeSec: 21.1, IonMobility: 1.0, MsLevel: 2),

        new("120 126 127",
            "7 7 7",
            PrecursorMz: 119.96, ScanTimeSec: 21.05, IonMobility: 0, MsLevel: 2),

        new("200.1 200.2 200.3 200.8 200.9",
            "1.0 3.0 1.0 1.0 4.0",
            PrecursorMz: 401.19, ScanTimeSec: 21.2, IonMobility: 1.01, MsLevel: 2),

        new("200.1 200.2 200.3 200.8 200.9",
            "1.0 3.0 1.0 1.0 4.0",
            PrecursorMz: 401.21, ScanTimeSec: 21.3, IonMobility: 2.0, MsLevel: 2),
    };

    private static readonly ScanSummerExpected[] Expected =
    {
        new("MS1 binned within 1e-6 Da",
            ExpectedMz:           "112 112.1 120 121 123 124 128 129",
            ExpectedIntensity:    " 10    12   0   2   8   4   2  14",
            ExpectedPrecursorMz: 0, ExpectedScanTimeSec: 20.0, ExpectedIonMobility: 0, ExpectedMsLevel: 1),

        new("MS1 untouched (no near-duplicates)",
            ExpectedMz:           "112.0001 119 120 121 122 123 124 127 128 129",
            ExpectedIntensity:    "       1   4   5   6   7   8   9  10  11  12",
            ExpectedPrecursorMz: 0, ExpectedScanTimeSec: 20.1, ExpectedIonMobility: 0, ExpectedMsLevel: 1),

        new("MS2 grouped around precursor 120 (3 sub-scans)",
            ExpectedMz:           "112 112.1 119 120 121 122 123 124 126 127 128 129",
            ExpectedIntensity:    "6       5   4  12   7   7  12  11   7  17  12  19",
            ExpectedPrecursorMz: 120.0, ExpectedScanTimeSec: 20.1, // median of 20.0, 20.1, 21.05
            ExpectedIonMobility: 0, ExpectedMsLevel: 2),

        new("MS2 grouped around precursor 401, IM ~1 (2 sub-scans)",
            ExpectedMz:           "200 200.1 200.2 200.3 200.8 200.9 202",
            ExpectedIntensity:    "1.0 4.0 4.0 1.0 1.0 4.0 3.0",
            ExpectedPrecursorMz: 401.21, // median of 401.19, 401.23
            ExpectedScanTimeSec: 21.15,  // median of 21.1, 21.2
            ExpectedIonMobility: 1.005,  // median of 1.0, 1.01
            ExpectedMsLevel: 2),

        new("MS2 standalone (IM=2 differs from cases 4,6)",
            ExpectedMz:           "200.1 200.2 200.3 200.8 200.9",
            ExpectedIntensity:    "1.0 3.0 1.0 1.0 4.0",
            ExpectedPrecursorMz: 401.21, ExpectedScanTimeSec: 21.3, ExpectedIonMobility: 2.0, ExpectedMsLevel: 2),
    };

    [TestMethod]
    public void Summer_MatchesCppGoldStandard()
    {
        // Mirror cpp's SpectrumList_ScanSummer ctor args: precursorTol=0.05, scanTimeTol=10,
        // ionMobilityTol=0.5, sumMs1=true.
        var inner = new SpectrumListSimple();
        for (int i = 0; i < Inputs.Length; i++)
            inner.Spectra.Add(BuildInputSpectrum(i, Inputs[i]));

        var summer = new SpectrumListScanSummer(inner,
            precursorTol: 0.05, scanTimeTol: 10, ionMobilityTol: 0.5, sumMs1: true);

        Assert.AreEqual(Expected.Length, summer.Count,
            $"output spectrum count: expected {Expected.Length}, got {summer.Count}");

        for (int i = 0; i < Expected.Length; i++)
        {
            var exp = Expected[i];
            var s = summer.GetSpectrum(i, getBinaryData: true);

            // cpp asserts binaryDataArrayPtrs.Count == 2 — the parallel mobility array must drop.
            Assert.AreEqual(2, s.BinaryDataArrays.Count,
                $"[{exp.Label}] binaryDataArrays count (parallel mobility array should be dropped pre-sum)");

            AssertPeakArrays(exp.Label, exp.ExpectedMz, exp.ExpectedIntensity, s);

            double scanTime = s.ScanList.Scans[0].Params.CvParam(CVID.MS_scan_start_time).ValueAs<double>();
            Assert.AreEqual(exp.ExpectedScanTimeSec, scanTime, 1e-8, $"[{exp.Label}] scan_start_time");

            double im = s.ScanList.Scans[0].Params.CvParamValueOrDefault(CVID.MS_inverse_reduced_ion_mobility, 0.0);
            Assert.AreEqual(exp.ExpectedIonMobility, im, 1e-8, $"[{exp.Label}] inverse_reduced_ion_mobility");

            if (exp.ExpectedMsLevel > 1)
            {
                double precMz = s.Precursors[0].SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();
                Assert.AreEqual(exp.ExpectedPrecursorMz, precMz, 1e-8, $"[{exp.Label}] precursor m/z");
            }
        }
    }

    private static double[] ParseDoubles(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture))
         .ToArray();

    private static Spectrum BuildInputSpectrum(int index, ScanSummerInput c)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}" };
        s.Params.Set(CVID.MS_MSn_spectrum);
        s.Params.Set(CVID.MS_ms_level, c.MsLevel);

        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, c.ScanTimeSec, CVID.UO_second);
        if (c.IonMobility > 0)
            scan.Set(CVID.MS_inverse_reduced_ion_mobility, c.IonMobility,
                CVID.MS_volt_second_per_square_centimeter);
        s.ScanList.Scans.Add(scan);

        if (c.MsLevel > 1)
            s.Precursors.Add(new Precursor(c.PrecursorMz));

        var mz = ParseDoubles(c.InputMz);
        var intensity = ParseDoubles(c.InputIntensity);
        s.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);

        // Parallel ion-mobility array — cpp test attaches one to verify ScanSummer drops
        // auxiliary parallel arrays before summing (gold expects binaryDataArrays.Count == 2).
        var mobilityArray = new BinaryDataArray();
        mobilityArray.Set(CVID.MS_raw_ion_mobility_array, string.Empty, CVID.UO_millisecond);
        for (int i = 0; i < mz.Length; i++) mobilityArray.Data.Add(0);
        s.BinaryDataArrays.Add(mobilityArray);

        scan.ScanWindows.Add(new ScanWindow(mz[0], mz[^1], CVID.MS_m_z));
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
