// Public surface and orchestration for the DIA-Umpire algorithm. Port of cpp
// pwiz/analysis/dia_umpire/DiaUmpire.cpp + DiaUmpire.hpp. The leaf helpers
// (PeakCurve, PeakCluster, ScanData, IsotopePatternMap, DiaUmpireMath,
// InstrumentParameter, Config) are already ported. This file owns the
// MS1-window / DIA-window orchestration that wires those leaves together
// and emits pseudo-MS/MS spectra.
//
// Differences from cpp documented inline:
//   * Threading: cpp uses boost::asio::thread_pool with a nested pool inside
//     each window. pwiz-sharp uses Parallel.ForEach with a ParallelOptions
//     MaxDegreeOfParallelism cap, no nested pool — the "nested" parallelism
//     in cpp mainly hides per-spectrum preprocessing, which we do inline.
//   * Spill files: cpp writes one mz5 file per DIA window into a TemporaryFile.
//     pwiz-sharp keeps each window's pseudo-MS/MS spectra in-memory inside
//     a SpillFile wrapper (one MSData + SpectrumListSimple per window). When
//     memory becomes a problem we can swap in mzMLb-backed spill (we have a
//     MzMlbWriter); the SpillFileToken shape is opaque so the swap is local.
//   * exportSeparateQualityMGFs: we honour the flag for sorting purposes but
//     don't actually write out separate MGFs (the spill files have a per-
//     spectrum quality-level userParam; the caller can split if needed).

using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// A single per-window spill MSData. cpp uses a temporary mz5 file pointer
/// (<c>pwiz::util::TemporaryFile*</c>); pwiz-sharp keeps the spilled spectra
/// in memory for now (one <see cref="MSData"/> per DIA window). The
/// <see cref="WindowKey"/> is the same string the algorithm uses to key
/// <see cref="DiaUmpire.SpillFileByWindow"/> — e.g. <c>"MS2:[400-425]"</c>.
/// </summary>
/// <remarks>
/// If/when memory pressure forces it, we can replace <see cref="Data"/> with
/// a path + lazy load. The class is intentionally a token-shaped wrapper so
/// the swap is local.
/// </remarks>
public sealed class SpillFile
{
    /// <summary>The MSData that holds this window's pseudo-MS/MS spectra.</summary>
    public MSData Data { get; }

    /// <summary>Key used in <see cref="DiaUmpire.SpillFileByWindow"/> (e.g. <c>"MS2:[400-425]"</c>).</summary>
    public string WindowKey { get; }

    /// <summary>Constructs a spill file from an in-memory MSData. Owned by the caller.</summary>
    public SpillFile(string windowKey, MSData data)
    {
        WindowKey = windowKey ?? throw new System.ArgumentNullException(nameof(windowKey));
        Data = data ?? throw new System.ArgumentNullException(nameof(data));
    }
}

/// <summary>
/// Identity record for a pseudo-MS/MS spectrum produced by <see cref="DiaUmpire"/>.
/// Points back at the per-window <see cref="SpillFileToken"/> + index inside that
/// spill MSData. Port of cpp <c>DiaUmpire::PseudoMsMsKey</c> (which derives from
/// <c>SpectrumIdentity</c>; we do the same).
/// </summary>
public sealed class PseudoMsMsKey : SpectrumIdentity
{
    /// <summary>Creates an empty key (used by the sort/swap dance — cpp parity).</summary>
    public PseudoMsMsKey() { }

    /// <summary>Creates a fully-populated key.</summary>
    /// <param name="scanTime">Scan start time, minutes.</param>
    /// <param name="targetMz">Precursor m/z.</param>
    /// <param name="charge">Precursor charge.</param>
    /// <param name="spillFileToken">Owning <see cref="SpillFile"/>.</param>
    /// <param name="spillFileIndex">Index of the spectrum inside the spill file's spectrum list.</param>
    public PseudoMsMsKey(float scanTime, float targetMz, int charge,
                         SpillFile spillFileToken, int spillFileIndex)
    {
        ScanTime = scanTime;
        TargetMz = targetMz;
        Charge = charge;
        SpillFileToken = spillFileToken;
        SpillFileIndex = spillFileIndex;
    }

    /// <summary>Scan start time of the pseudo-MS/MS spectrum, in minutes.</summary>
    public float ScanTime { get; set; }

    /// <summary>Precursor m/z.</summary>
    public float TargetMz { get; set; }

    /// <summary>Precursor charge.</summary>
    public int Charge { get; set; }

    /// <summary>Spill file (per-DIA-window MSData) that contains the actual spectrum.</summary>
    public SpillFile? SpillFileToken { get; set; }

    /// <summary>Index of the spectrum inside <see cref="SpillFileToken"/>.Data.Run.SpectrumList.</summary>
    public int SpillFileIndex { get; set; }
}

/// <summary>
/// DIA-Umpire pseudo-MS/MS spectrum generator. Port of cpp <c>DiaUmpire::DiaUmpire</c>.
/// Original algorithm by Chih-Chiang Tsou (Java); cpp port by Matt Chambers; this is the
/// C# port of the cpp.
/// </summary>
/// <remarks>
/// Pipeline per DIA window:
/// <list type="number">
///   <item>Pull MS1 + MS2 spectra in RT range from the inner <see cref="ISpectrumList"/>.</item>
///   <item>Build MS1 peak curves (XICs), smooth, cluster into isotope groups.</item>
///   <item>Build MS2 peak curves per window, smooth, cluster.</item>
///   <item>Pair MS1 clusters with MS2 fragments via Pearson cross-correlation.</item>
///   <item>Emit one pseudo-MS/MS spectrum per high-quality precursor cluster into the
///         window's spill file; record a <see cref="PseudoMsMsKey"/>.</item>
/// </list>
/// <see cref="PseudoMsMsKeys"/> is sorted by (scan time, target m/z, charge) after all
/// windows finish.
/// </remarks>
public sealed class DiaUmpire
{
    private readonly Impl _impl;

    /// <summary>Constructs the DIA-Umpire processor and runs the pipeline to completion.</summary>
    /// <param name="msd">The source MSData (used for run id + instrument config copy into spill files).</param>
    /// <param name="spectrumList">The source spectrum list (centroided DIA spectra; must contain MS1 + MS2 with isolation windows).</param>
    /// <param name="config">Configuration (instrument params + threading + windowing).</param>
    /// <param name="ilr">Optional iteration listener for progress / cancellation.</param>
    public DiaUmpire(MSData msd, ISpectrumList spectrumList, Config config,
                     IterationListenerRegistry? ilr = null)
    {
        System.ArgumentNullException.ThrowIfNull(msd);
        System.ArgumentNullException.ThrowIfNull(spectrumList);
        System.ArgumentNullException.ThrowIfNull(config);
        _impl = new Impl(msd, spectrumList, config, ilr);
        _impl.Run();
    }

    /// <summary>Sorted (by scan time, target m/z, charge) list of pseudo-MS/MS identities.</summary>
    public IReadOnlyList<PseudoMsMsKey> PseudoMsMsKeys => _impl.OutputScanKeys;

    /// <summary>Map from window-key string (e.g. <c>"MS2:[400-425]"</c>) to the spill file holding that window's spectra.</summary>
    public IReadOnlyDictionary<string, SpillFile> SpillFileByWindow => _impl.SpillFiles;

    // ----------------------------------------------------------------------
    // Internal step enum (matches cpp). Used only for progress messages.
    // ----------------------------------------------------------------------
    internal enum DiaUmpireStep
    {
        InlineStep = 0,
        AssignSpectraToWindows = 1,
        ReadAllSpectra,
        BuildPeakCurves,
        SmoothPeakCurves,
        ClusterPeakCurves,
        ReadMs2Spectra,
        ProcessDiaWindows,
        Count,
    }

    // ----------------------------------------------------------------------
    // Impl — owns the entire run state.
    // ----------------------------------------------------------------------
    internal sealed class Impl
    {
        internal MSData Msd { get; }
        internal ISpectrumList Sl { get; }
        internal Config Config { get; }
        internal IsotopePatternMap IsotopePatternMap { get; }
        internal List<DiaWindow> DiaWindows { get; } = new();
        internal List<PeakCurve> Ms1PeakCurves { get; } = new();
        internal List<PeakCluster> Ms1PeakClusters { get; } = new();
        internal int Ms1Count { get; private set; }
        internal int Ms2Count { get; private set; }
        internal float Ms1CycleTime { get; private set; }
        internal SortedDictionary<float, int> IndexByScanTime { get; } = new();
        internal List<(int MsLevel, float ScanTime)> MsLevelAndScanTimeByIndex { get; } = new();
        internal IterationListenerRegistry? Ilr { get; }
        internal readonly object IlrLock = new();
        internal volatile bool Canceled;
        internal int WindowsProcessed;
        internal List<PseudoMsMsKey> OutputScanKeys { get; } = new();
        internal SortedDictionary<string, SpillFile> SpillFiles { get; } = new(System.StringComparer.Ordinal);

        private readonly List<TargetWindow> _diaTargetWindows = new();

        public Impl(MSData msd, ISpectrumList spectrumList, Config config, IterationListenerRegistry? ilr)
        {
            Msd = msd;
            Sl = spectrumList;
            Config = config;
            Ilr = ilr;
            IsotopePatternMap = new IsotopePatternMap(config.InstrumentParameters);
        }

        public void Run()
        {
            if (!BuildDIAWindows()) { if (Canceled) return; throw new System.InvalidOperationException("error in BuildDIAWindows"); }
            if (!MS1PeakDetection()) { if (Canceled) return; throw new System.InvalidOperationException("error in MS1PeakDetection"); }
            if (!DIAMS2PeakDetection()) { if (Canceled) return; throw new System.InvalidOperationException("error in DIAMS2PeakDetection"); }

            // cpp posts the clear() onto a background thread so it doesn't block the destructor.
            // pwiz-sharp: let the GC handle it — the underlying lists are no longer referenced
            // outside Ms1PeakCurves_/Ms1PeakClusters_ once Run() returns.
            Ms1PeakClusters.Clear();
            Ms1PeakCurves.Clear();
        }

        // ------------------------------------------------------------------
        // Progress / cancellation broadcast. Returns true if cancelled.
        // ------------------------------------------------------------------
        internal bool IterateAndCheckCancellation(int index, int size, string msg, DiaUmpireStep step)
        {
            if (Ilr is null) return false;

            int stepNum = (int)step;
            int stepCount = (int)DiaUmpireStep.Count - 1;

            if (!Config.MultithreadOverWindows)
            {
                int windowsDone = System.Threading.Volatile.Read(ref WindowsProcessed);
                stepNum += windowsDone * 3;
                stepCount += DiaWindows.Count * 3;
            }
            string msgWithStep = $"[step {stepNum} of {stepCount}] {msg}";

            lock (IlrLock)
            {
                var status = Ilr.Broadcast(new IterationUpdate(index, size, msgWithStep));
                if (status == IterationStatus.Cancel) Canceled = true;
                return Canceled;
            }
        }

        // ------------------------------------------------------------------
        // Step 1 — sort spectra into DIA windows.
        // ------------------------------------------------------------------
        internal bool BuildDIAWindows()
        {
            // Copy variable windows from config to local target list.
            var targetWindowByMzRange = new Dictionary<MzRange, TargetWindow>();
            if (Config.DiaTargetWindowScheme == TargetWindowScheme.SwathVariable)
            {
                foreach (var window in Config.DiaVariableWindows)
                {
                    _diaTargetWindows.Add(window);
                    targetWindowByMzRange[window.MzRange] = window;
                }
            }

            Ms1Count = 0;
            Ms2Count = 0;
            string progressMessage = "assigning spectra to DIA windows";
            bool sawAnyMs2WithIsolation = false;

            int total = Sl.Count;
            for (int i = 0; i < total; ++i)
            {
                var s = Sl.GetSpectrum(i, DetailLevel.FastMetadata);
                if (s.HasCVParam(CVID.MS_profile_spectrum))
                    throw new System.InvalidOperationException(
                        "[DiaUmpire.BuildDIAWindows] DIA Umpire requires centroided spectra; use the peakPicking filter");

                if (IterateAndCheckCancellation(i, total, progressMessage, DiaUmpireStep.AssignSpectraToWindows))
                    return false;

                int msLevel = s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
                if (s.ScanList.Scans.Count == 0)
                    continue;

                // cpp: float scanTime = scans[0].cvParam(MS_scan_start_time).timeInSeconds() / 60;
                var scanStartParam = s.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
                float scanTime = scanStartParam.IsEmpty ? 0f : (float)(scanStartParam.TimeInSeconds() / 60.0);

                MsLevelAndScanTimeByIndex.Add((msLevel, scanTime));
                IndexByScanTime[scanTime] = i;

                if (msLevel == 1) ++Ms1Count;
                if (scanTime > Config.InstrumentParameters.EndRT) break;
                if (msLevel < 2) continue;
                if (s.Precursors.Count == 0) continue;

                float isoTarget = s.Precursors[0].IsolationWindow.CvParamValueOrDefault<float>(CVID.MS_isolation_window_target_m_z, 0);
                if (isoTarget == 0 && s.Precursors[0].SelectedIons.Count > 0)
                {
                    isoTarget = s.Precursors[0].SelectedIons[0].CvParamValueOrDefault<float>(CVID.MS_selected_ion_m_z, 0);
                    if (isoTarget == 0) continue;
                }
                if (isoTarget == 0) continue;
                sawAnyMs2WithIsolation = true;

                ++Ms2Count;

                float loOffset = s.Precursors[0].IsolationWindow.CvParamValueOrDefault<float>(CVID.MS_isolation_window_lower_offset, 0);
                float upOffset = s.Precursors[0].IsolationWindow.CvParamValueOrDefault<float>(CVID.MS_isolation_window_upper_offset, 0);

                if (Config.DiaTargetWindowScheme == TargetWindowScheme.SwathVariable)
                {
                    foreach (var window in _diaTargetWindows)
                    {
                        if (window.MzRange.Begin <= isoTarget && window.MzRange.End >= isoTarget)
                        {
                            window.SpectraInRange.Add(i);
                            break;
                        }
                    }
                }
                else
                {
                    if (loOffset == 0 || upOffset == 0)
                    {
                        // cpp parity: when offsets are missing, synthesize a window of size diaFixedWindowSize+1 split 20/80.
                        loOffset = (Config.DiaFixedWindowSize + 1) * 0.2f;
                        upOffset = (Config.DiaFixedWindowSize + 1) * 0.8f;
                    }

                    var mzRange = new MzRange(isoTarget - loOffset, isoTarget + upOffset);
                    if (!targetWindowByMzRange.TryGetValue(mzRange, out var w))
                    {
                        w = new TargetWindow(mzRange);
                        _diaTargetWindows.Add(w);
                        targetWindowByMzRange[mzRange] = w;
                    }
                    w.SpectraInRange.Add(i);
                }
            }

            if (IterateAndCheckCancellation(total, total, progressMessage, DiaUmpireStep.AssignSpectraToWindows))
                return false;

            if (!sawAnyMs2WithIsolation)
                throw new System.InvalidOperationException(
                    "[DiaUmpire.BuildDIAWindows] no MS2 spectra with isolation window target m/z");

            if (Ms1Count == 0)
                throw new System.InvalidOperationException(
                    "[DiaUmpire.BuildDIAWindows] no MS1 scans detected; they are required for DIA Umpire to work");

            // cpp parity: ms1CycleTime = (lastScanTime - firstScanTime) / ms1Count.
            float firstTime = 0, lastTime = 0;
            foreach (var kv in IndexByScanTime) { firstTime = kv.Key; break; }
            foreach (var kv in IndexByScanTime) lastTime = kv.Key;
            Ms1CycleTime = (lastTime - firstTime) / Ms1Count;

            if (_diaTargetWindows.Count == 0)
                throw new System.InvalidOperationException("[DiaUmpire.BuildDIAWindows] no target windows");

            // cpp: process windows in descending order of m/z (sort by mzRange.begin DESC).
            _diaTargetWindows.Sort((a, b) => b.MzRange.Begin.CompareTo(a.MzRange.Begin));
            for (int i = 0; i + 1 < _diaTargetWindows.Count; ++i)
            {
                var w = _diaTargetWindows[i];
                if (w.SpectraInRange.Count == 0)
                {
                    if (Config.DiaTargetWindowScheme == TargetWindowScheme.SwathVariable)
                        System.Console.Error.WriteLine(
                            $"Warning: DIA window [{w.MzRange.Begin}-{w.MzRange.End}] has no spectra assigned to it; are the variable windows set correctly?");
                    continue;
                }
                DiaWindows.Add(new DiaWindow(w, _diaTargetWindows[i + 1]));
            }
            DiaWindows.Add(new DiaWindow(_diaTargetWindows[^1], null));

            return true;
        }

        // ------------------------------------------------------------------
        // Pull a ScanCollection by MS-level filter and RT range. cpp parallelizes
        // per-spectrum preprocessing into a nested thread pool; pwiz-sharp does it
        // inline (Parallel.ForEach over the in-range indices).
        // ------------------------------------------------------------------
        internal ScanCollection? GetAllScanCollectionByMSLabel(bool ms1Included, bool ms2Included,
            bool _ms1Peak, bool _ms2Peak, float startTime, float endTime, DiaUmpireStep step)
        {
            // _ms1Peak / _ms2Peak unused: cpp uses them only to gate centroiding; we run
            // Preprocessing unconditionally on every scan (matches cpp's actual behavior
            // in MS1PeakDetection / DIAMS2PeakDetection which always pass true here).
            int startIndex = LowerBoundByKeyOr(IndexByScanTime, startTime, 0);
            int endIndex = LowerBoundByKeyOr(IndexByScanTime, endTime,
                IndexByScanTime.Count == 0 ? 0 : IndexByScanTime.Reverse().First().Value);

            if (startIndex > endIndex) (startIndex, endIndex) = (endIndex, startIndex);

            var result = new ScanCollection();

            var msLevels = new List<string>();
            if (ms1Included) msLevels.Add("MS1");
            if (ms2Included) msLevels.Add("MS2");
            string progressMessage = $"reading {string.Join("/", msLevels)} spectra into scan collection";

            int totalScans = endIndex + 1 - startIndex;
            var includedScans = new List<ScanData>(totalScans);
            int scansRead = 0;

            // Sequential read from the (single-reader) spectrum list, then parallel preprocessing.
            for (int index = startIndex; index <= endIndex; ++index)
            {
                if (index >= MsLevelAndScanTimeByIndex.Count) break;
                int msLevel = MsLevelAndScanTimeByIndex[index].MsLevel;
                if ((ms1Included && msLevel == 1) || (ms2Included && msLevel == 2))
                {
                    var spec = Sl.GetSpectrum(index, getBinaryData: true);
                    var scan = BuildScanData(spec, index);
                    if (scan is not null)
                    {
                        includedScans.Add(scan);
                        result.AddScan(scan);
                    }
                }
                ++scansRead;
                if (IterateAndCheckCancellation(scansRead, endIndex + 1, progressMessage, step))
                    return null;
            }

            // Preprocess (centroid, background, denoise) in parallel.
            var pool = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = System.Math.Max(1, Config.MaxNestedThreads),
            };
            System.Threading.Tasks.Parallel.ForEach(includedScans, pool, scan =>
                scan.Preprocessing(Config.InstrumentParameters));

            result.SortIndices();

            if (IterateAndCheckCancellation(endIndex, endIndex + 1, progressMessage, step))
                return null;
            return result;
        }

        // ------------------------------------------------------------------
        // Convert a pwiz-sharp Spectrum into a ScanData record.
        // ------------------------------------------------------------------
        private static ScanData? BuildScanData(Spectrum spec, int index)
        {
            int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
            float scanTime = 0f;
            if (spec.ScanList.Scans.Count > 0)
            {
                var p = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
                if (!p.IsEmpty) scanTime = (float)(p.TimeInSeconds() / 60.0);
            }

            var scan = new ScanData
            {
                ScanNum = index,
                MsLevel = msLevel,
                RetentionTime = scanTime,
                Centroided = spec.HasCVParam(CVID.MS_centroid_spectrum),
            };

            var mzArr = spec.GetMZArray();
            var intArr = spec.GetIntensityArray();
            if (mzArr is null || intArr is null) return scan;

            int n = System.Math.Min(mzArr.Data.Count, intArr.Data.Count);
            for (int i = 0; i < n; ++i)
                scan.AddPoint((float)mzArr.Data[i], (float)intArr.Data[i]);

            return scan;
        }

        // ------------------------------------------------------------------
        // Step 2 — MS1 peak detection (build + smooth + cluster).
        // ------------------------------------------------------------------
        internal bool MS1PeakDetection()
        {
            // Calculate how many points per minute for B-spline peak smoothing.
            if (Ms1CycleTime > 0)
                Config.InstrumentParameters.NoPeakPerMin = (int)(Config.InstrumentParameters.SmoothFactor / Ms1CycleTime);

            var scanCollection = GetAllScanCollectionByMSLabel(true, true, true, false,
                Config.InstrumentParameters.StartRT, Config.InstrumentParameters.EndRT,
                DiaUmpireStep.ReadAllSpectra);
            if (scanCollection is null) return false;

            try
            {
                FindAllMzTracePeakCurves(scanCollection, Ms1PeakCurves,
                    Config.InstrumentParameters.MS1PPM, 1, 0, 0,
                    DiaUmpireStep.BuildPeakCurves, multithreaded: false);
            }
            catch (System.Exception e) when (e is not System.OperationCanceledException)
            {
                throw new System.InvalidOperationException("[DiaUmpire.MS1PeakDetection] " + e.Message, e);
            }

            PeakCurveSmoothing(Ms1PeakCurves, 0, 0, multithreaded: true);
            PeakCurveCorrClustering(new MzRange(-1e30f, 1e30f), Ms1PeakCurves, Ms1PeakClusters, 1, 0, 0, multithreaded: true);

            return true;
        }

        // ------------------------------------------------------------------
        // Step 3 — DIA MS2 peak detection + pseudo-MS/MS emission per window.
        // ------------------------------------------------------------------
        internal bool DIAMS2PeakDetection()
        {
            var scanCollectionAllMs2 = GetAllScanCollectionByMSLabel(false, true, true, false,
                Config.InstrumentParameters.StartRT, Config.InstrumentParameters.EndRT,
                DiaUmpireStep.ReadMs2Spectra);
            if (scanCollectionAllMs2 is null) return false;

            bool multithreadWindows = Config.MultithreadOverWindows;
            string progressMessage = "processing DIA window";
            var unsortedScanKeys = new List<PseudoMsMsKey>();
            var keysLock = new object();

            var pool = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = multithreadWindows
                    ? System.Math.Max(1, Config.MaxThreads)
                    : 1,
            };

            void ProcessWindow(DiaWindow diaWindow)
            {
                if (Canceled) return;
                string diaWindowId = "MS2:[" + diaWindow.MzRange.Begin.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                   + "-" + diaWindow.MzRange.End.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";

                int curWindowsProcessed = System.Threading.Volatile.Read(ref WindowsProcessed);
                if (IterateAndCheckCancellation(curWindowsProcessed, DiaWindows.Count, progressMessage, DiaUmpireStep.ProcessDiaWindows))
                    return;

                try
                {
                    var buildPeakCurvesStep = multithreadWindows ? DiaUmpireStep.InlineStep : DiaUmpireStep.BuildPeakCurves;
                    FindAllMzTracePeakCurves(scanCollectionAllMs2, diaWindow.PeakCurves,
                        Config.InstrumentParameters.MS2PPM, 2, curWindowsProcessed, DiaWindows.Count,
                        buildPeakCurvesStep, multithreaded: !multithreadWindows, scanIndices: diaWindow.SpectraInRange);
                }
                catch (System.Exception e) when (e is not System.OperationCanceledException)
                {
                    throw new System.InvalidOperationException("[DiaUmpire.DIAMS2PeakDetection] " + e.Message, e);
                }

                PeakCurveSmoothing(diaWindow.PeakCurves, curWindowsProcessed, DiaWindows.Count, multithreaded: !multithreadWindows);
                PeakCurveCorrClustering(diaWindow.MzRange, diaWindow.PeakCurves, diaWindow.PeakClusters, 2,
                    curWindowsProcessed, DiaWindows.Count, multithreaded: !multithreadWindows);

                if (IterateAndCheckCancellation(curWindowsProcessed, DiaWindows.Count, progressMessage, DiaUmpireStep.ProcessDiaWindows))
                    return;

                if (diaWindow.PeakCurves.Count == 0)
                {
                    System.Console.Error.WriteLine($"No peak detected for window {diaWindow.MzRange.Begin}-{diaWindow.MzRange.End}");
                    System.Threading.Interlocked.Increment(ref WindowsProcessed);
                    return;
                }

                // Mass-defect filter on MS2 fragment curves.
                if (Config.InstrumentParameters.MassDefectFilter)
                {
                    var md = default(MassDefect);
                    var kept = new List<PeakCurve>(diaWindow.PeakCurves.Count);
                    // cpp parity: iterates the curves in reverse but the order doesn't matter for "keep iff in MD range".
                    for (int i = diaWindow.PeakCurves.Count - 1; i >= 0; --i)
                    {
                        var pc = diaWindow.PeakCurves[i];
                        bool keep = false;
                        for (int charge = 1; charge <= 2; ++charge)
                        {
                            float mass = charge * (pc.TargetMz - (float)Pwiz.Util.Chemistry.PhysicalConstants.Proton);
                            if (md.InMassDefectRange(mass, Config.InstrumentParameters.MassDefectOffset))
                            {
                                keep = true;
                                break;
                            }
                        }
                        if (keep) kept.Add(pc);
                    }
                    diaWindow.PeakCurves.Clear();
                    diaWindow.PeakCurves.AddRange(kept);
                }

                if (IterateAndCheckCancellation(curWindowsProcessed, DiaWindows.Count, progressMessage, DiaUmpireStep.ProcessDiaWindows))
                    return;

                diaWindow.PrecursorFragmentPairBuildingForMS1(this);
                diaWindow.PrecursorFragmentPairBuildingForUnfragmentedIon(this);

                if (IterateAndCheckCancellation(curWindowsProcessed, DiaWindows.Count, progressMessage, DiaUmpireStep.ProcessDiaWindows))
                    return;

                var localScanList = new List<PseudoMSMSProcessing>();

                // MS1-cluster-based pseudo-MS/MS (Q1/Q2).
                foreach (var ms1Cluster in Ms1PeakClusters)
                {
                    if (diaWindow.MzRange.Begin > ms1Cluster.GetMaxMz() || diaWindow.MzRange.End < ms1Cluster.TargetMz())
                        continue;
                    if (!diaWindow.FragmentsClu2Cur.TryGetValue(ms1Cluster.Index, out var frags))
                        continue;
                    if (diaWindow.NextWindowMzRange.Equals(MzRange.Empty) || diaWindow.NextWindowMzRange.End < ms1Cluster.TargetMz())
                    {
                        var quality = ms1Cluster.IsotopeComplete(3) ? QualityLevel.Q1IsotopeComplete : QualityLevel.Q2Ms1Group;
                        var pseudo = new PseudoMSMSProcessing(ms1Cluster, frags, Config.InstrumentParameters, quality);
                        pseudo.Run();
                        localScanList.Add(pseudo);
                    }
                }

                if (IterateAndCheckCancellation(curWindowsProcessed, DiaWindows.Count, progressMessage, DiaUmpireStep.ProcessDiaWindows))
                    return;

                // Unfragmented-precursor pseudo-MS/MS (Q3).
                foreach (var ms2Cluster in diaWindow.PeakClusters)
                {
                    if (diaWindow.MzRange.Begin > ms2Cluster.TargetMz() || diaWindow.MzRange.End < ms2Cluster.TargetMz())
                        continue;
                    if (!diaWindow.UnFragIonClu2Cur.TryGetValue(ms2Cluster.Index, out var frags))
                        continue;
                    var pseudo = new PseudoMSMSProcessing(ms2Cluster, frags, Config.InstrumentParameters, QualityLevel.Q3UnfragmentedPrecursor);
                    pseudo.Run();
                    localScanList.Add(pseudo);
                }

                // Emit pseudo-MS/MS spectra into the window's spill MSData.
                var spillData = new MSData
                {
                    Id = Msd.Id + " DIA window " + diaWindowId,
                };
                spillData.Run.Id = spillData.Id;
                foreach (var ic in Msd.InstrumentConfigurations) spillData.InstrumentConfigurations.Add(ic);
                foreach (var sw in Msd.Software) spillData.Software.Add(sw);
                foreach (var cv in Msd.CVs) spillData.CVs.Add(cv);
                var outputScans = new SpectrumListSimple();
                spillData.Run.SpectrumList = outputScans;
                var spillFile = new SpillFile(diaWindowId, spillData);

                var localPseudoMsMs = new List<PseudoMsMsKey>();
                foreach (var pseudoScan in localScanList)
                {
                    var precursorCluster = pseudoScan.Precursorcluster;

                    var s = new Spectrum();
                    s.Params.Set(CVID.MS_ms_level, 2);
                    s.Params.Set(CVID.MS_MSn_spectrum);
                    s.Params.Set(CVID.MS_centroid_spectrum);
                    string qualityValue = pseudoScan.QualityLevel switch
                    {
                        QualityLevel.Q1IsotopeComplete => "1",
                        QualityLevel.Q2Ms1Group => "2",
                        QualityLevel.Q3UnfragmentedPrecursor => "3",
                        _ => "0",
                    };
                    s.Params.UserParams.Add(new UserParam("DIA-Umpire quality level", qualityValue, "xsd:positiveInteger"));

                    var scan = new Scan();
                    // cpp parity: round(rtMinutes * 10000) / 10000 then store with UO_minute.
                    double rtMin = System.Math.Round(precursorCluster.PeakHeightRT[0] * 10000.0) / 10000.0;
                    scan.Set(CVID.MS_scan_start_time, rtMin, CVID.UO_minute);
                    if (Msd.InstrumentConfigurations.Count > 0)
                        scan.InstrumentConfiguration = Msd.InstrumentConfigurations[0];
                    s.ScanList.Scans.Add(scan);

                    s.Precursors.Add(new Precursor(precursorCluster.TargetMz(),
                        precursorCluster.PeakHeight[0], precursorCluster.Charge, CVID.MS_number_of_detector_counts));

                    pseudoScan.GetScan(out double[] mzArray, out double[] intensityArray);
                    s.SetMZIntensityArrays(mzArray, intensityArray, CVID.MS_number_of_detector_counts);

                    s.Index = outputScans.Spectra.Count;
                    s.Id = "merged=" + s.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    outputScans.Spectra.Add(s);

                    localPseudoMsMs.Add(new PseudoMsMsKey(
                        precursorCluster.PeakHeightRT[0], precursorCluster.TargetMz(),
                        precursorCluster.Charge, spillFile, s.Index));
                }

                lock (keysLock)
                {
                    SpillFiles[diaWindowId] = spillFile;
                    unsortedScanKeys.AddRange(localPseudoMsMs);
                }

                if (IterateAndCheckCancellation(curWindowsProcessed, DiaWindows.Count, progressMessage, DiaUmpireStep.ProcessDiaWindows))
                    return;

                diaWindow.PeakClusters.Clear();
                diaWindow.PeakCurves.Clear();
                diaWindow.FragmentsClu2Cur.Clear();
                diaWindow.UnFragIonClu2Cur.Clear();
                diaWindow.FragmentMS1Ranking.Clear();
                diaWindow.FragmentUnfragRanking.Clear();
                System.Threading.Interlocked.Increment(ref WindowsProcessed);
            }

            if (multithreadWindows)
                System.Threading.Tasks.Parallel.ForEach(DiaWindows, pool, ProcessWindow);
            else
                foreach (var w in DiaWindows) ProcessWindow(w);

            if (IterateAndCheckCancellation(DiaWindows.Count, DiaWindows.Count, progressMessage, DiaUmpireStep.ProcessDiaWindows))
                return false;

            // Sort by (scanTime, targetMz, charge) — cpp parity.
            unsortedScanKeys.Sort((a, b) =>
            {
                int t = a.ScanTime.CompareTo(b.ScanTime);
                if (t != 0) return t;
                int m = a.TargetMz.CompareTo(b.TargetMz);
                if (m != 0) return m;
                return a.Charge.CompareTo(b.Charge);
            });

            OutputScanKeys.Capacity = unsortedScanKeys.Count;
            for (int i = 0; i < unsortedScanKeys.Count; ++i)
            {
                var k = unsortedScanKeys[i];
                k.Index = i;
                k.Id = "merged=" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                OutputScanKeys.Add(k);
            }

            return true;
        }

        // ------------------------------------------------------------------
        // Step: FindAllMzTracePeakCurves — the core peak-curve builder.
        // Port of cpp Impl::FindAllMzTracePeakCurves.
        // ------------------------------------------------------------------
        internal bool FindAllMzTracePeakCurves(ScanCollection scanCollection, List<PeakCurve> peakCurves,
            float ppmTolerance, int msLevel, int windowsProcessedSnapshot, int windowsTotal,
            DiaUmpireStep step, bool multithreaded = false, IReadOnlyList<int>? scanIndices = null)
        {
            // cpp uses boost::container::flat_set<pair<int,float>>. HashSet<(int,float)>
            // is slightly faster on the same workload for lookup-only.
            var included = new HashSet<(int Scan, float Mz)>();

            float preRT = 0;
            float snr = msLevel == 1 ? Config.InstrumentParameters.SN : Config.InstrumentParameters.MS2SN;
            string progressMessage = "building peak curves";

            IReadOnlyList<int> scansForMsLevel = scanIndices is null || scanIndices.Count == 0
                ? scanCollection.GetScanNoArray(msLevel)
                : scanIndices;

            for (int scanIdx = 0; scanIdx < scansForMsLevel.Count; ++scanIdx)
            {
                if (msLevel == 1)
                {
                    if (IterateAndCheckCancellation(scanIdx, scansForMsLevel.Count, progressMessage, step))
                        return false;
                }
                else if (multithreaded)
                {
                    if (IterateAndCheckCancellation(scanIdx, scansForMsLevel.Count, progressMessage, DiaUmpireStep.BuildPeakCurves))
                        return false;
                }
                else if (IterateAndCheckCancellation(windowsProcessedSnapshot, windowsTotal, "processing DIA window", DiaUmpireStep.ProcessDiaWindows))
                    return false;

                var scanPtr = scanCollection.GetScan(scansForMsLevel[scanIdx]);
                if (scanPtr is null) continue;
                var scan = scanPtr;
                float scanTime = scan.RetentionTime;

                if (preRT == 0) preRT = scanTime - 0.01f;

                for (int peakIdx = 0; peakIdx < scan.Data.Count; ++peakIdx)
                {
                    var peak = scan.Get(peakIdx);
                    if (peak.Mz < Config.InstrumentParameters.MinMZ) continue;

                    if (!included.Add((scan.ScanNum, peak.Mz))) continue;

                    float startmz = peak.Mz;
                    float startint = peak.Intensity;

                    // Find the maximum peak within the ppm window as the starting peak.
                    for (int k = peakIdx + 1; k < scan.Data.Count; ++k)
                    {
                        var nextPeak = scan.Get(k);
                        if (InstrumentParameter.CalcPPM(nextPeak.Mz, startmz) > ppmTolerance) break;
                        if (!included.Add((scan.ScanNum, nextPeak.Mz))) continue;
                        if (nextPeak.Intensity >= startint)
                        {
                            startmz = nextPeak.Mz;
                            startint = nextPeak.Intensity;
                        }
                    }

                    var peakcurve = new PeakCurve(Config.InstrumentParameters)
                    {
                        MsLevel = msLevel,
                        TargetMz = startmz,
                        StartScan = scan.ScanNum,
                    };
                    peakcurve.AddPeak(new XYZData(preRT, startmz, scan.Background));
                    peakcurve.AddPeak(new XYZData(scan.RetentionTime, startmz, startint));

                    int missedScan = 0;
                    float endrt = scan.RetentionTime;
                    int endScan = scan.ScanNum;
                    float bk = 0;

                    for (int scan2Idx = scanIdx + 1; scan2Idx < scansForMsLevel.Count
                                                     && missedScan < Config.InstrumentParameters.NoMissedScan; ++scan2Idx)
                    {
                        var scanData2 = scanCollection.GetScan(scansForMsLevel[scan2Idx]);
                        if (scanData2 is null) break;
                        int scanNo2 = scanData2.ScanNum;
                        endrt = scanData2.RetentionTime;
                        endScan = scanData2.ScanNum;
                        bk = scanData2.Background;
                        float currentmz = 0;
                        float currentint = 0;

                        if (scanData2.PointCount() == 0)
                        {
                            if (Config.InstrumentParameters.FillGapByBK)
                                peakcurve.AddPeak(new XYZData(scanData2.RetentionTime, peakcurve.TargetMz, scanData2.Background));
                            missedScan++;
                            continue;
                        }

                        int mzidx = scanData2.GetLowerIndexOfX(peakcurve.TargetMz);
                        for (int pkidx = mzidx; pkidx < scanData2.Data.Count; ++pkidx)
                        {
                            var currentpeak = scanData2.Get(pkidx);
                            if (currentpeak.GetX() < Config.InstrumentParameters.MinMZ) continue;
                            if (included.Contains((scanNo2, currentpeak.Mz))) continue;

                            if (InstrumentParameter.CalcPPM(currentpeak.GetX(), peakcurve.TargetMz) > ppmTolerance)
                            {
                                if (currentpeak.GetX() > peakcurve.TargetMz) break;
                            }
                            else
                            {
                                included.Add((scanNo2, currentpeak.Mz));
                                if (currentint < currentpeak.GetY())
                                {
                                    currentmz = currentpeak.GetX();
                                    currentint = currentpeak.GetY();
                                }
                            }
                        }

                        if (currentmz == 0)
                        {
                            if (Config.InstrumentParameters.FillGapByBK)
                                peakcurve.AddPeak(new XYZData(scanData2.RetentionTime, peakcurve.TargetMz, scanData2.Background));
                            missedScan++;
                        }
                        else
                        {
                            missedScan = 0;
                            peakcurve.AddPeak(new XYZData(scanData2.RetentionTime, currentmz, currentint));
                        }
                    }

                    peakcurve.AddPeak(new XYZData(endrt, peakcurve.TargetMz, bk));
                    peakcurve.EndScan = endScan;

                    // Apply the cpp accept criteria (no targeted inclusion list yet).
                    if (peakcurve.GetRawSNR() > snr
                        && peakcurve.GetPeakList().Count >= Config.InstrumentParameters.MinPeakPerPeakCurve + 2)
                    {
                        peakCurves.Add(peakcurve);
                    }
                }
                preRT = scan.RetentionTime;
            }

            int idxAssign = 1;
            foreach (var pc in peakCurves) pc.Index = idxAssign++;
            return true;
        }

        // ------------------------------------------------------------------
        // Step: PeakCurveSmoothing — B-spline + optional CWT region split.
        // ------------------------------------------------------------------
        internal bool PeakCurveSmoothing(List<PeakCurve> peakCurves, int windowsProcessedSnapshot, int windowsTotal, bool multithreaded)
        {
            string progressMessage = "smoothing peak curves";
            var resultCurves = new System.Collections.Concurrent.ConcurrentBag<PeakCurve>();

            var pool = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = multithreaded ? System.Math.Max(1, Config.MaxNestedThreads) : 1,
            };
            int processed = 0;
            System.Threading.Tasks.Parallel.ForEach(peakCurves, pool, curve =>
            {
                if (Canceled) return;
                curve.DoBspline();
                if (Config.InstrumentParameters.DetectByCWT)
                {
                    curve.DetectPeakRegion();
                    var separated = curve.SeparatePeakByRegion(Config.InstrumentParameters.SN);
                    foreach (var r in separated) resultCurves.Add(r);
                }
                else
                {
                    resultCurves.Add(curve);
                }
                int cur = System.Threading.Interlocked.Increment(ref processed);
                if (multithreaded)
                {
                    if (IterateAndCheckCancellation(cur, peakCurves.Count, progressMessage, DiaUmpireStep.SmoothPeakCurves))
                        return;
                }
                else if (IterateAndCheckCancellation(windowsProcessedSnapshot, windowsTotal, "processing DIA window", DiaUmpireStep.ProcessDiaWindows))
                    return;
            });

            if (Canceled) return false;

            peakCurves.Clear();
            peakCurves.AddRange(resultCurves);

            // cpp sort: TargetMz asc, ties broken by ApexRT asc.
            peakCurves.Sort((a, b) => a.TargetMz == b.TargetMz
                ? a.ApexRT.CompareTo(b.ApexRT)
                : a.TargetMz.CompareTo(b.TargetMz));

            int i = 1;
            foreach (var pc in peakCurves) pc.Index = i++;
            return true;
        }

        // ------------------------------------------------------------------
        // Step: PeakCurveCorrClustering — build isotope clusters via KDtree clusterer.
        // ------------------------------------------------------------------
        internal bool PeakCurveCorrClustering(MzRange mzRange, List<PeakCurve> peakCurves,
            List<PeakCluster> peakClusters, int msLevel, int windowsProcessedSnapshot, int windowsTotal, bool multithreaded)
        {
            int maxNoPeakCluster, minNoPeakCluster, startCharge, endCharge;
            if (msLevel == 1)
            {
                maxNoPeakCluster = Config.InstrumentParameters.MaxNoPeakCluster;
                minNoPeakCluster = Config.InstrumentParameters.MinNoPeakCluster;
                startCharge = Config.InstrumentParameters.StartCharge;
                endCharge = Config.InstrumentParameters.EndCharge;
            }
            else
            {
                maxNoPeakCluster = Config.InstrumentParameters.MaxMS2NoPeakCluster;
                minNoPeakCluster = Config.InstrumentParameters.MinMS2NoPeakCluster;
                startCharge = Config.InstrumentParameters.MS2StartCharge;
                endCharge = Config.InstrumentParameters.MS2EndCharge;
            }

            // pwiz-sharp PeakCurveClusteringCorrKDtree takes a flat list rather than an R-tree.
            // Slice down to curves whose ApexRT could contribute (cpp's R-tree gives this for free).
            var searchable = peakCurves;
            var chiSquaredGof = new ChiSquareGOF(maxNoPeakCluster);
            var clusterMutex = new object();
            var clusterJobs = new List<PeakCurveClusteringCorrKDtree>();
            int curvesToCluster = 0;
            string progressMessage = "clustering peak curves";

            for (int targetCurveIndex = 0; targetCurveIndex < peakCurves.Count; ++targetCurveIndex)
            {
                var peakcurve = peakCurves[targetCurveIndex];
                if (peakcurve.TargetMz < mzRange.Begin || peakcurve.TargetMz > mzRange.End) continue;
                ++curvesToCluster;
                clusterJobs.Add(new PeakCurveClusteringCorrKDtree(
                    peakCurves, targetCurveIndex, searchable,
                    Config.InstrumentParameters, IsotopePatternMap, chiSquaredGof,
                    startCharge, endCharge, maxNoPeakCluster, minNoPeakCluster, clusterMutex));
            }

            var pool = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = multithreaded ? System.Math.Max(1, Config.MaxNestedThreads) : 1,
            };
            int curvesClustered = 0;
            System.Threading.Tasks.Parallel.ForEach(clusterJobs, pool, job =>
            {
                if (Canceled) return;
                job.Run();
                int cur = System.Threading.Interlocked.Increment(ref curvesClustered);
                if (multithreaded)
                {
                    if (IterateAndCheckCancellation(cur, curvesToCluster, progressMessage, DiaUmpireStep.ClusterPeakCurves))
                        return;
                }
                else if (IterateAndCheckCancellation(windowsProcessedSnapshot, windowsTotal, "processing DIA window", DiaUmpireStep.ProcessDiaWindows))
                    return;
            });

            if (Canceled) return false;

            foreach (var job in clusterJobs)
            {
                foreach (var cluster in job.ResultClusters)
                {
                    // cpp: skip clusters whose mono peak has already been claimed at this charge.
                    if (!Config.InstrumentParameters.RemoveGroupedPeaks
                        || cluster.MonoIsotopePeak is null
                        || !cluster.MonoIsotopePeak.ChargeGrouped.Contains(cluster.Charge))
                    {
                        cluster.Index = peakClusters.Count + 1;
                        _ = cluster.GetConflictCorr();
                        cluster.StartScan = LowerBoundByKeyOr(IndexByScanTime, cluster.StartRT, -1);
                        cluster.EndScan = LowerBoundByKeyOr(IndexByScanTime, cluster.EndRT, -1);
                        peakClusters.Add(cluster);
                    }
                }
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Helpers.
        // ------------------------------------------------------------------
        private static int LowerBoundByKeyOr(SortedDictionary<float, int> dict, float key, int fallback)
        {
            foreach (var kv in dict)
                if (kv.Key >= key) return kv.Value;
            return fallback;
        }
    }
}
