using Pwiz.Analysis;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// End-to-end tests for <see cref="SpectrumListDemux"/>: structural integration tests against a
/// synthetic single-precursor overlap-DIA experiment plus numerical-parity tests against cpp's
/// gold-standard fixtures (<c>OverlapTest.mzML</c> and <c>MsxTest.mzML</c> under
/// <c>pwiz/analysis/spectrum_processing/SpectrumList_DemuxTest.data</c>).
/// </summary>
[TestClass]
public class SpectrumListDemuxTests
{
    [TestMethod]
    public void Wrapper_ExpandsCountByOverlapsPerCycle()
    {
        // 5 cycles × (1 MS1 + 50 MS2) = 255 input spectra. With overlap=2 + 1 precursor each,
        // every MS2 expands to 2 demux outputs (minus the 2 partial edges per cycle).
        var inner = BuildSingleOverlapList(numCycles: 5, scansPerHalf: 25, mzStart: 400, mzEnd: 600);
        int innerMs2Count = 0;
        for (int i = 0; i < inner.Count; i++)
            if (inner.GetSpectrum(i).Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 2)
                innerMs2Count++;

        var demux = new SpectrumListDemux(inner);
        // Output count should be at least innerMs2Count (each MS2 produces ≥ 1 demux output) and
        // less than innerMs2Count * 2 + ms1Count (edge spectra get clipped).
        int innerMs1Count = inner.Count - innerMs2Count;
        Assert.IsTrue(demux.Count >= innerMs2Count + innerMs1Count,
            $"output count {demux.Count} below the input MS1 + per-MS2 minimum");
        Assert.IsTrue(demux.Count <= innerMs2Count * 2 + innerMs1Count,
            $"output count {demux.Count} above the per-MS2-doubled maximum");
    }

    [TestMethod]
    public void Ms1Spectra_PassThroughUnchanged()
    {
        var inner = BuildSingleOverlapList(numCycles: 5, scansPerHalf: 25, mzStart: 400, mzEnd: 600);
        var demux = new SpectrumListDemux(inner);

        // Find a demux entry that maps to an MS1 (msLevel != 2 → numDemuxIndices = 1, so 1:1 with input).
        bool foundMs1 = false;
        for (int i = 0; i < demux.Count; i++)
        {
            var spec = demux.GetSpectrum(i, getBinaryData: true);
            if (spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) != 1) continue;
            // MS1 should retain its original peaks.
            var mz = spec.GetMZArray();
            Assert.IsNotNull(mz, "MS1 spectrum has no m/z array");
            Assert.IsTrue(mz!.Data.Count > 0, "MS1 spectrum has zero peaks");
            foundMs1 = true;
            break;
        }
        Assert.IsTrue(foundMs1, "no MS1 spectrum found in demux output");
    }

    [TestMethod]
    public void Ms2Spectrum_GetsRewrittenIsolationWindow()
    {
        // Each demuxed MS2 should have a NARROWER isolation window than the source mux'd MS2.
        var inner = BuildSingleOverlapList(numCycles: 5, scansPerHalf: 25, mzStart: 400, mzEnd: 600);
        var demux = new SpectrumListDemux(inner);

        for (int i = 0; i < demux.Count; i++)
        {
            var spec = demux.GetSpectrum(i, getBinaryData: true);
            if (spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) != 2) continue;
            Assert.AreEqual(1, spec.Precursors.Count, "demuxed spectrum should keep one precursor");
            var pre = spec.Precursors[0];
            double lower = pre.IsolationWindow.CvParam(CVID.MS_isolation_window_lower_offset).ValueAs<double>();
            double upper = pre.IsolationWindow.CvParam(CVID.MS_isolation_window_upper_offset).ValueAs<double>();
            // Source half-width was (mzEnd - mzStart) / scansPerHalf / 2 = 200 / 25 / 2 = 4.
            // After overlap demux, the demux sub-window halves the source width → ≤ 2.
            Assert.IsTrue(upper > 0, $"demuxed precursor lacks upper offset (got {upper})");
            Assert.IsTrue(lower > 0, $"demuxed precursor lacks lower offset (got {lower})");
            Assert.IsTrue(upper + lower <= 4.0 + 0.01, // half of source width 4 + slack
                $"demuxed isolation width {upper + lower} larger than half of source half-width");
            return;
        }
        Assert.Fail("no demuxed MS2 spectrum found");
    }

    [TestMethod]
    public void Factory_DispatchesDemultiplexFilter()
    {
        var inner = BuildSingleOverlapList(numCycles: 5, scansPerHalf: 25, mzStart: 400, mzEnd: 600);
        var wrapped = SpectrumListFactory.Wrap(inner, "demultiplex massError=10ppm nnlsMaxIter=50");
        Assert.IsInstanceOfType<SpectrumListDemux>(wrapped);
    }

    [TestMethod]
    public void Factory_RejectsBadOptimization()
    {
        var inner = BuildSingleOverlapList(numCycles: 5, scansPerHalf: 25, mzStart: 400, mzEnd: 600);
        Assert.ThrowsException<ArgumentException>(() =>
            SpectrumListFactory.Wrap(inner, "demultiplex optimization=bogus"));
    }

    // ============================================================================
    //   Cpp gold-standard parity (pwiz/analysis/spectrum_processing/SpectrumList_DemuxTest.cpp)
    // ============================================================================

    // Constants from cpp's SpectrumList_DemuxTest.cpp:
    private const int OverlapDemuxIndex = 128;            // index in the demuxed output
    private const int OverlapOriginalIndex = 64;          // index in the source mzML
    private const int OverlapDeconvCount = 2;             // overlap multiplicity
    private const int MsxDemuxIndex = 105;
    private const int MsxOriginalIndex = 21;
    private const int MsxDeconvCount = 5;

    // Cpp's testOverlapOnly() intensities at TEST_SPECTRUM_OVERLAP_DEMUX_INDEX = 128, except [18], [25],
    // [26] and [30] (cpp 46296.34, 44635.87, 28415.64, 43483.74), which the full search span changes.
    private static readonly double[] OverlapGoldStandardIntensities = new[]
    {
        62715.75, 10856.38, 26514.10, 15964.11, 35976.23,
        24815.48, 10131.85, 21044.27, 34393.21, 9127.96,
        50067.90, 10287.26, 11103.65, 19305.24, 9583.66,
        11572.70, 9995.09, 29599.00, 38228.79, 32724.88,
        9292.13, 8167.25, 1111.66, 25497.61, 23860.40,
        28433.71, 26008.71, 9848.89, 18376.83, 24337.12,
        5594.79, 26286.20, 40075.65,
    };

    // Intensities at TEST_SPECTRUM_MSX_DEMUX_INDEX = 105 with the full search span (regression values;
    // the extractor itself is checked by SpectrumPeakExtractorTests). Cpp's testMSXOnly() has 29 peaks
    // here: with the full windows the solve assigns more peaks to one window, zeroing them in others.
    private static readonly double[] MsxGoldStandardIntensities = new[]
    {
        2305.29, 912.86, 480.24, 1870.50, 2006.70,
        2767.20, 4189.77, 2183.44, 720.22, 728.97,
        1421.49, 195.37, 740.24, 25800.27, 8947.44,
        10017.21, 7548.32, 6817.35, 4373.10, 3277.52,
        1562.70, 1951.14, 1392.36, 1483.15, 2411.16,
        4166.85,
    };

    [TestMethod]
    public void Demux_OverlapTest_PerWindowSumsMatchOriginal()
    {
        // Cpp: testOverlapOnly() — verify that the demux'd sub-spectra at index 128..129 (the two
        // halves of the overlap pair from source index 64) sum back to the source spectrum's
        // intensities, peak by peak.
        var (centroided, demuxList) = LoadAndWrap("OverlapTest.mzML",
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.OverlapOnly });

        var originalSpec = centroided.GetSpectrum(OverlapOriginalIndex, getBinaryData: true);
        var originalMz = originalSpec.GetMZArray()!.Data;
        var originalInt = originalSpec.GetIntensityArray()!.Data;

        var peakSums = new double[originalInt.Count];
        for (int i = 0; i < OverlapDeconvCount; i++)
        {
            var ds = demuxList.GetSpectrum(OverlapDemuxIndex + i, getBinaryData: true);
            var dMz = ds.GetMZArray()!.Data;
            var dInt = ds.GetIntensityArray()!.Data;
            var mask = new List<int>();
            BuildMzMask(originalMz, dMz, mask);
            for (int k = 0; k < mask.Count; k++) peakSums[mask[k]] += dInt[k];
        }

        for (int i = 0; i < peakSums.Length; i++)
            Assert.AreEqual(originalInt[i], peakSums[i], 1e-5,
                $"sum mismatch at peak {i} (original m/z {originalMz[i]:F4})");
    }

    [TestMethod]
    public void Demux_OverlapTest_AbsoluteIntensities_MatchGoldStandard()
    {
        var (_, demuxList) = LoadAndWrap("OverlapTest.mzML",
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.OverlapOnly });

        var demuxed = demuxList.GetSpectrum(OverlapDemuxIndex, getBinaryData: true);
        var actual = demuxed.GetIntensityArray()!.Data;

        Assert.AreEqual(OverlapGoldStandardIntensities.Length, actual.Count,
            "gold: demuxed intensity-array length");
        for (int i = 0; i < OverlapGoldStandardIntensities.Length; i++)
            Assert.AreEqual(OverlapGoldStandardIntensities[i], actual[i], 0.1,
                $"gold: intensity[{i}]");
    }

    [TestMethod]
    public void Demux_OverlapTest_OriginalScanIdPreserved()
    {
        var (centroided, demuxList) = LoadAndWrap("OverlapTest.mzML",
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.OverlapOnly });

        var demuxId = demuxList.SpectrumIdentity(OverlapDemuxIndex);
        Assert.IsTrue(TryGetIdToken(demuxId.Id, "originalScan", out var originalScanStr),
            $"demux spectrum at index {OverlapDemuxIndex} should expose an originalScan= token; id was '{demuxId.Id}'");

        var originalId = centroided.SpectrumIdentity(OverlapOriginalIndex);
        Assert.IsTrue(TryGetIdToken(originalId.Id, "scan", out var originalScanFromSource),
            $"source spectrum at index {OverlapOriginalIndex} lacks a scan= token");
        Assert.AreEqual(originalScanFromSource, originalScanStr,
            "cpp parity: originalScan token should match the source scan number");
    }

    [TestMethod]
    public void Demux_MsxTest_PerWindowSumsMatchOriginal()
    {
        var (centroided, demuxList) = LoadAndWrap("MsxTest.mzML",
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.None });

        var originalSpec = centroided.GetSpectrum(MsxOriginalIndex, getBinaryData: true);
        var originalMz = originalSpec.GetMZArray()!.Data;
        var originalInt = originalSpec.GetIntensityArray()!.Data;

        var peakSums = new double[originalInt.Count];
        for (int i = 0; i < MsxDeconvCount; i++)
        {
            var ds = demuxList.GetSpectrum(MsxDemuxIndex + i, getBinaryData: true);
            var dMz = ds.GetMZArray()!.Data;
            var dInt = ds.GetIntensityArray()!.Data;
            var mask = new List<int>();
            BuildMzMask(originalMz, dMz, mask);
            for (int k = 0; k < mask.Count; k++) peakSums[mask[k]] += dInt[k];
        }

        for (int i = 0; i < peakSums.Length; i++)
            Assert.AreEqual(originalInt[i], peakSums[i], 1e-5,
                $"sum mismatch at peak {i} (original m/z {originalMz[i]:F4})");
    }

    [TestMethod]
    public void Demux_MsxTest_AbsoluteIntensities_MatchGoldStandard()
    {
        var (_, demuxList) = LoadAndWrap("MsxTest.mzML",
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.None });

        var demuxed = demuxList.GetSpectrum(MsxDemuxIndex, getBinaryData: true);
        var actual = demuxed.GetIntensityArray()!.Data;

        Assert.AreEqual(MsxGoldStandardIntensities.Length, actual.Count,
            "gold: demuxed intensity-array length");
        for (int i = 0; i < MsxGoldStandardIntensities.Length; i++)
            Assert.AreEqual(MsxGoldStandardIntensities[i], actual[i], 0.1,
                $"gold: intensity[{i}]");
    }

    /// <summary>Reads <paramref name="fixtureName"/> from the cpp test-data dir, runs centroid
    /// peak picking + demux, returns the centroided source list and the demuxed wrapper.</summary>
    private static (ISpectrumList Centroided, SpectrumListDemux Demuxed) LoadAndWrap(
        string fixtureName, SpectrumListDemux.Params demuxParams)
    {
        string path = Path.Combine(FindDemuxTestDataRoot(), fixtureName);
        if (!File.Exists(path)) Assert.Inconclusive($"cpp demux fixture not found: {path}");

        MSData msd;
        using (var fs = File.OpenRead(path))
            msd = new MzmlReader().Read(fs);

        // Centroid via local-maximum peak picker on MS levels 1+2 (matches cpp's
        // SpectrumList_PeakPicker(LocalMaximumPeakDetector(3), preferVendor=true, levels=1-2)).
        var centroided = new SpectrumList_PeakPicker(msd.Run.SpectrumList!,
            algorithm: new LocalMaximumPeakDetector(3),
            preferVendorPeakPicking: true,
            msLevelsToPeakPick: new IntegerSet(1, 2));

        var demuxed = new SpectrumListDemux(centroided, demuxParams);
        return (centroided, demuxed);
    }

    private static string FindDemuxTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string c = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_DemuxTest.data");
            if (Directory.Exists(c)) return c;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("SpectrumList_DemuxTest.data not found");
    }

    /// <summary>Builds a list of indices into <paramref name="originalMz"/> matching each entry
    /// in <paramref name="derivedMz"/> within 1e-5 m/z. Mirrors cpp's <c>DemuxTest::GetMask</c>.</summary>
    private static void BuildMzMask(IReadOnlyList<double> originalMz, IReadOnlyList<double> derivedMz, List<int> mask)
    {
        mask.Clear();
        int origIdx = 0;
        foreach (var mz in derivedMz)
        {
            for (; origIdx < originalMz.Count; origIdx++)
            {
                if (System.Math.Abs(originalMz[origIdx] - mz) < 1e-5)
                {
                    mask.Add(origIdx);
                    break;
                }
                Assert.IsTrue(originalMz[origIdx] < mz,
                    $"derived m/z {mz} can't be matched to originals (next original = {originalMz[origIdx]})");
            }
        }
        Assert.AreEqual(derivedMz.Count, mask.Count, "every derived m/z should match an original");
    }

    private static bool TryGetIdToken(string id, string key, out string value)
    {
        foreach (var token in id.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = token.IndexOf('=');
            if (eq < 0) continue;
            if (token[..eq] == key) { value = token[(eq + 1)..]; return true; }
        }
        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Real staggered-window data cut down to four fragment m/z whose NNLS reaches the iteration
    /// limit in some spectra, against C#'s output with the full search span (see the data folder's README).
    /// </summary>
    [TestMethod]
    public void Demux_EclipseNnlsFixture_MatchesGoldPeakByPeak()
    {
        string root = FindDemuxTestDataRoot();
        var spectra = ReadEclipseFixture(Path.Combine(root, "EclipseNnlsFixture.tsv"), out var notCompared);
        var demuxList = new SpectrumListDemux(spectra,
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.OverlapOnly });

        // Below 1 are rounding-level near-zeros (cpp's QR and C#'s Cholesky resolve them differently),
        // left out of both expected files alike.
        var expected = new Dictionary<(string Scan, string Demux), List<(double Mz, double Intensity)>>();
        foreach (var line in File.ReadLines(Path.Combine(root, "EclipseNnlsFixture.expected-full-span.tsv")))
        {
            if (line.StartsWith('#')) continue;
            var f = line.Split('\t');
            var key = (f[0], f[1]);
            if (!expected.TryGetValue(key, out var peaks)) expected[key] = peaks = new();
            peaks.Add((double.Parse(f[2], Invariant), double.Parse(f[3], Invariant)));
        }

        int compared = 0;
        for (int i = 0; i < demuxList.Count; i++)
        {
            var spectrum = demuxList.GetSpectrum(i, getBinaryData: true);
            Assert.IsTrue(TryGetIdToken(spectrum.Id, "originalScan", out var scan));
            Assert.IsTrue(TryGetIdToken(spectrum.Id, "demux", out var demux));
            var mzs = spectrum.GetMZArray()?.Data ?? new List<double>();
            var intensities = spectrum.GetIntensityArray()?.Data ?? new List<double>();
            var actual = new List<(double Mz, double Intensity)>();
            notCompared.TryGetValue(scan, out double skipMz);
            for (int k = 0; k < mzs.Count; k++)
            {
                if (intensities[k] >= 1 && mzs[k] != skipMz)
                    actual.Add((mzs[k], intensities[k]));
            }
            var want = expected.TryGetValue((scan, demux), out var w) ? w : new();
            Assert.AreEqual(want.Count, actual.Count, $"peak count of originalScan={scan} demux={demux}");
            for (int k = 0; k < want.Count; k++)
            {
                Assert.AreEqual(want[k].Mz, actual[k].Mz, 1e-6, $"m/z in originalScan={scan} demux={demux}");
                Assert.AreEqual(want[k].Intensity, actual[k].Intensity, want[k].Intensity * 1e-5,
                    $"intensity at {want[k].Mz:F4} in originalScan={scan} demux={demux}");
                compared++;
            }
        }
        Assert.AreEqual(expected.Values.Sum(p => p.Count), compared, "every expected peak compared");
    }

    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Reads the Eclipse fixture (format in the data folder's README); <paramref name="notCompared"/>
    /// gets each spectrum's <c>M</c> peak m/z by scan.</summary>
    private static SpectrumListSimple ReadEclipseFixture(string path, out Dictionary<string, double> notCompared)
    {
        var list = new SpectrumListSimple();
        notCompared = new Dictionary<string, double>();
        string currentScan = string.Empty;
        Spectrum? current = null;
        var mzs = new List<double>();
        var intensities = new List<double>();
        void Finish()
        {
            if (current is null) return;
            current.SetMZIntensityArrays(mzs.ToArray(), intensities.ToArray(), CVID.MS_number_of_detector_counts);
            list.Spectra.Add(current);
            mzs.Clear();
            intensities.Clear();
        }
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var f = line.Split('\t');
            if (f[0] == "S")
            {
                Finish();
                currentScan = f[1];
                current = new Spectrum { Index = list.Spectra.Count, Id = $"scan={f[1]}" };
                current.Params.Set(CVID.MS_ms_level, 2);
                var precursor = new Precursor();
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, double.Parse(f[3], Invariant), CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, double.Parse(f[4], Invariant), CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, double.Parse(f[5], Invariant), CVID.MS_m_z);
                current.Precursors.Add(precursor);
                var scan = new Scan();
                scan.Set(CVID.MS_scan_start_time, double.Parse(f[2], Invariant), CVID.UO_minute);
                current.ScanList.Scans.Add(scan);
            }
            else if (current is null)
            {
                Assert.Fail($"peak line before the first spectrum in {Path.GetFileName(path)}: {line}");
            }
            else if (f[0] == "M")
            {
                mzs.Add(double.Parse(f[1], Invariant));
                intensities.Add(double.Parse(f[2], Invariant));
                notCompared[currentScan] = mzs[^1];
            }
            else
            {
                mzs.Add(double.Parse(f[0], Invariant));
                intensities.Add(double.Parse(f[1], Invariant));
            }
        }
        Finish();
        return list;
    }

    // ---------- helpers ----------

    /// <summary>Builds a single-precursor / single-overlap synthetic DIA list. Mirrors the
    /// generator used in <c>PrecursorMaskCodecTests</c>, with peaks added to each MS2 so the
    /// demux solver has something to deconvolve.</summary>
    private static SpectrumListSimple BuildSingleOverlapList(int numCycles, int scansPerHalf,
        double mzStart, double mzEnd)
    {
        var sl = new SpectrumListSimple();
        double width = (mzEnd - mzStart) / scansPerHalf;
        double halfWidth = width / 2.0;
        int idx = 0;
        for (int cycle = 0; cycle < numCycles; cycle++)
        {
            sl.Spectra.Add(MakeMs1(idx++, scanTimeSec: cycle * 60.0));

            for (int s = 0; s < scansPerHalf; s++)
            {
                double center = mzStart + halfWidth + s * width;
                sl.Spectra.Add(MakeMs2(idx++, scanTimeSec: cycle * 60.0 + s * 0.1,
                    isoCenter: center, halfWidth: halfWidth));
            }
            for (int s = 0; s < scansPerHalf; s++)
            {
                double center = mzStart + halfWidth + s * width + halfWidth;
                sl.Spectra.Add(MakeMs2(idx++, scanTimeSec: cycle * 60.0 + 1.0 + s * 0.1,
                    isoCenter: center, halfWidth: halfWidth));
            }
        }
        return sl;
    }

    private static Spectrum MakeMs1(int index, double scanTimeSec)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}" };
        s.Params.Set(CVID.MS_ms_level, 1);
        s.SetMZIntensityArrays(new[] { 200.0, 300.0 }, new[] { 1.0, 1.0 }, CVID.MS_number_of_detector_counts);
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, scanTimeSec, CVID.UO_second);
        s.ScanList.Scans.Add(scan);
        return s;
    }

    private static Spectrum MakeMs2(int index, double scanTimeSec, double isoCenter, double halfWidth)
    {
        var s = new Spectrum { Index = index, Id = $"scan={index + 1}" };
        s.Params.Set(CVID.MS_ms_level, 2);
        var p = new Precursor();
        p.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isoCenter, CVID.MS_m_z);
        p.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, halfWidth, CVID.MS_m_z);
        p.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, halfWidth, CVID.MS_m_z);
        p.SelectedIons.Add(new SelectedIon(isoCenter));
        s.Precursors.Add(p);
        // Peaks at fixed m/z values so all MS2 spectra share columns in the signal matrix.
        s.SetMZIntensityArrays(new[] { 100.0, 150.0, 250.0 }, new[] { 5.0, 7.0, 3.0 },
            CVID.MS_number_of_detector_counts);
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, scanTimeSec, CVID.UO_second);
        s.ScanList.Scans.Add(scan);
        return s;
    }
}
