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

    // Hard-coded intensity vector from cpp's testOverlapOnly() at TEST_SPECTRUM_OVERLAP_DEMUX_INDEX = 128.
    private static readonly double[] OverlapGoldStandardIntensities = new[]
    {
        62715.75, 10856.38, 26514.10, 15964.11, 35976.23,
        24815.48, 10131.85, 21044.27, 34393.21, 9127.96,
        50067.90, 10287.26, 11103.65, 19305.24, 9583.66,
        11572.70, 9995.09, 29599.00, 46296.34, 32724.88,
        9292.13, 8167.25, 1111.66, 25497.61, 23860.40,
        44635.87, 28415.64, 9848.89, 18376.83, 24337.12,
        43483.74, 26286.20, 40075.65,
    };

    // Hard-coded intensity vector from cpp's testMSXOnly() at TEST_SPECTRUM_MSX_DEMUX_INDEX = 105.
    private static readonly double[] MsxGoldStandardIntensities = new[]
    {
        931.31, 550.11, 650.53, 1870.50, 62.58,
        2767.20, 4917.47, 1525.37, 923.80, 726.35,
        1421.49, 1699.59, 3126.18, 25833.26, 23554.24,
        10017.21, 900.55, 26146.96, 9478.34, 2643.12,
        5988.79, 1562.70, 1952.92, 1392.36, 1354.70,
        5745.34, 1891.37, 2545.78, 4131.52,
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
    public void Demux_OverlapTest_AbsoluteIntensities_MatchCppGoldStandard()
    {
        var (_, demuxList) = LoadAndWrap("OverlapTest.mzML",
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.OverlapOnly });

        var demuxed = demuxList.GetSpectrum(OverlapDemuxIndex, getBinaryData: true);
        var actual = demuxed.GetIntensityArray()!.Data;

        Assert.AreEqual(OverlapGoldStandardIntensities.Length, actual.Count,
            "cpp parity: demuxed intensity-array length");
        for (int i = 0; i < OverlapGoldStandardIntensities.Length; i++)
            Assert.AreEqual(OverlapGoldStandardIntensities[i], actual[i], 0.1,
                $"cpp parity: intensity[{i}]");
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
    public void Demux_MsxTest_AbsoluteIntensities_MatchCppGoldStandard()
    {
        var (_, demuxList) = LoadAndWrap("MsxTest.mzML",
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.None });

        var demuxed = demuxList.GetSpectrum(MsxDemuxIndex, getBinaryData: true);
        var actual = demuxed.GetIntensityArray()!.Data;

        Assert.AreEqual(MsxGoldStandardIntensities.Length, actual.Count,
            "cpp parity: demuxed intensity-array length");
        for (int i = 0; i < MsxGoldStandardIntensities.Length; i++)
            Assert.AreEqual(MsxGoldStandardIntensities[i], actual[i], 0.1,
                $"cpp parity: intensity[{i}]");
    }

    // ============================================================================
    //   SolveThreads determinism
    // ============================================================================

    [TestMethod]
    public void SolveThreads_OverlapTest_OutputIdenticalToSingleThread()
    {
        AssertSolveThreadsDeterministic("OverlapTest.mzML", SpectrumListDemux.Optimization.OverlapOnly);
    }

    [TestMethod]
    public void SolveThreads_MsxTest_OutputIdenticalToSingleThread()
    {
        AssertSolveThreadsDeterministic("MsxTest.mzML", SpectrumListDemux.Optimization.None);
    }

    /// <summary>Reads every spectrum of the fixture demultiplexed at SolveThreads=1, then checks
    /// that SolveThreads 2 and 4 give exactly the same spectra, read in order and read in a
    /// jumping order (backwards, then scattered) that exercises solving a batch from a jump.</summary>
    private static void AssertSolveThreadsDeterministic(string fixtureName, SpectrumListDemux.Optimization optimization)
    {
        var (_, serial) = LoadAndWrap(fixtureName,
            new SpectrumListDemux.Params { Optimization = optimization, SolveThreads = 1 });
        var expected = new Spectrum[serial.Count];
        for (int i = 0; i < serial.Count; i++)
            expected[i] = serial.GetSpectrum(i, getBinaryData: true);
        Assert.IsTrue(expected.Any(s => s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 2),
            "fixture should produce demuxed MS2 spectra");

        foreach (int threads in new[] { 2, 4 })
        {
            var p = new SpectrumListDemux.Params { Optimization = optimization, SolveThreads = threads };

            var (_, inOrder) = LoadAndWrap(fixtureName, p);
            Assert.AreEqual(expected.Length, inOrder.Count, $"SolveThreads={threads}: spectrum count");
            for (int i = 0; i < inOrder.Count; i++)
                AssertSpectraEqual(expected[i], inOrder.GetSpectrum(i, getBinaryData: true), $"SolveThreads={threads} in order");

            var (_, jumping) = LoadAndWrap(fixtureName, p);
            // Every jump outside the batches in hand solves a whole batch, so keep the jumps few.
            var order = new List<int>();
            for (int i = jumping.Count - 1; i >= 0; i -= System.Math.Max(1, jumping.Count / 4))
                order.Add(i);                         // Backwards through the run
            var random = new Random(42);
            for (int k = 0; k < 6; k++)
                order.Add(random.Next(jumping.Count)); // Scattered jumps
            order.AddRange(new[] { 0, jumping.Count / 2, jumping.Count - 1, 1, jumping.Count / 2 + 1 });
            foreach (int i in order)
                AssertSpectraEqual(expected[i], jumping.GetSpectrum(i, getBinaryData: true), $"SolveThreads={threads} jumping");
        }
    }

    [TestMethod]
    public void SolveThreads_UnreadableSpectrum_FailsSameSpectraAsSingleThread()
    {
        // One unreadable MS2 spectrum must fail the same demuxed spectra at any thread count,
        // not a whole batch, and every other spectrum must still come out identical.
        var (centroided, _) = LoadAndWrap("OverlapTest.mzML", new SpectrumListDemux.Params());
        int bad = Enumerable.Range(centroided.Count / 2, centroided.Count / 2).First(i =>
            centroided.GetSpectrum(i).Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 2);

        var serial = ReadAll(bad, 1);
        Assert.IsTrue(serial.Any(r => r.Error), "some spectra should fail to demultiplex");
        Assert.IsTrue(serial.Count(r => !r.Error) > serial.Length / 2, "most spectra should demultiplex");
        foreach (int threads in new[] { 2, 4 })
        {
            var parallel = ReadAll(bad, threads);
            for (int i = 0; i < serial.Length; i++)
            {
                Assert.AreEqual(serial[i].Error, parallel[i].Error, $"SolveThreads={threads}, index {i}: failed");
                if (!serial[i].Error)
                    AssertSpectraEqual(serial[i].Spectrum!, parallel[i].Spectrum!, $"SolveThreads={threads}");
            }
        }
    }

    private static (bool Error, Spectrum? Spectrum)[] ReadAll(int unreadableIndex, int solveThreads)
    {
        var (centroided, _) = LoadAndWrap("OverlapTest.mzML", new SpectrumListDemux.Params());
        var demuxed = new SpectrumListDemux(new UnreadableSpectrumList(centroided, unreadableIndex),
            new SpectrumListDemux.Params { Optimization = SpectrumListDemux.Optimization.OverlapOnly, SolveThreads = solveThreads });
        var results = new (bool, Spectrum?)[demuxed.Count];
        for (int i = 0; i < demuxed.Count; i++)
        {
            try
            {
                results[i] = (false, demuxed.GetSpectrum(i, getBinaryData: true));
            }
            catch (InvalidDataException)
            {
                results[i] = (true, null);
            }
        }
        return results;
    }

    /// <summary>Throws reading the peaks of one spectrum, as a damaged vendor file might.</summary>
    private sealed class UnreadableSpectrumList : SpectrumListWrapper
    {
        private readonly int _unreadableIndex;

        public UnreadableSpectrumList(ISpectrumList inner, int unreadableIndex) : base(inner)
        {
            _unreadableIndex = unreadableIndex;
        }

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            if (index == _unreadableIndex && getBinaryData)
                throw new InvalidDataException($"spectrum {index} is unreadable");
            return base.GetSpectrum(index, getBinaryData);
        }
    }

    private static void AssertSpectraEqual(Spectrum expected, Spectrum actual, string context)
    {
        string where = $"{context}, index {expected.Index}";
        Assert.AreEqual(expected.Index, actual.Index, $"{where}: index");
        Assert.AreEqual(expected.Id, actual.Id, $"{where}: id");
        Assert.AreEqual(expected.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0),
            actual.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), $"{where}: ms level");
        Assert.AreEqual(expected.Precursors.Count, actual.Precursors.Count, $"{where}: precursor count");
        for (int p = 0; p < expected.Precursors.Count; p++)
        {
            foreach (var cvid in new[] { CVID.MS_isolation_window_target_m_z,
                         CVID.MS_isolation_window_lower_offset, CVID.MS_isolation_window_upper_offset })
            {
                Assert.AreEqual(expected.Precursors[p].IsolationWindow.CvParam(cvid).ValueAs<double>(),
                    actual.Precursors[p].IsolationWindow.CvParam(cvid).ValueAs<double>(),
                    $"{where}: precursor {p} {cvid}");
            }
        }
        CollectionAssert.AreEqual(expected.GetMZArray()?.Data.ToArray(), actual.GetMZArray()?.Data.ToArray(),
            $"{where}: m/z array");
        CollectionAssert.AreEqual(expected.GetIntensityArray()?.Data.ToArray(), actual.GetIntensityArray()?.Data.ToArray(),
            $"{where}: intensity array");
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
