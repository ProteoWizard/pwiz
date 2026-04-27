using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// <see cref="IBrukerData"/> backed by a TDF (timsTOF with ion mobility) <c>.d</c> directory —
/// wraps <see cref="TimsBinaryData"/> (binary reads) + <see cref="TdfMetadata"/> (SQLite metadata).
/// </summary>
/// <remarks>
/// Per-frame native reads are cached so successive per-scan requests on the same frame don't
/// re-issue <c>tims_read_scans_v2</c> for each of the typically 927 mobility scans.
/// </remarks>
internal sealed class TdfData : IBrukerData
{
    private readonly TimsBinaryData _tims;
    private readonly TdfMetadata _meta;
    private long _cachedFrameId = -1;
    private FrameProxy? _cachedFrame;
    private bool _disposed;

    public TdfData(string analysisDirectory, bool useRecalibratedState)
    {
        AnalysisDirectory = analysisDirectory;
        _meta = new TdfMetadata(analysisDirectory);
        _tims = new TimsBinaryData(analysisDirectory, useRecalibratedState);
    }

    public BrukerFormat Format => BrukerFormat.Tdf;
    public string AnalysisDirectory { get; }
    public IReadOnlyDictionary<string, string> GlobalMetadata => _meta.GlobalMetadata;
    public bool HasPasefData => _meta.HasPasefData;
    public bool HasDiaPasefData => _meta.HasDiaPasefData;
    public bool HasMs1Frames => _meta.HasMs1Frames;
    public bool HasMsNFrames => _meta.HasMsNFrames;
    public (double Low, double High) MzAcquisitionRange => _meta.MzAcquisitionRange;
    public bool IsMaldiSource => false; // no MALDI on TDF

    /// <summary>Exposed so <c>Reader_Bruker</c> can attach per-scan 1/K0 to DIA window groups.</summary>
    internal TimsBinaryData TimsBinaryData => _tims;

    /// <summary>Exposed so <c>Reader_Bruker</c> can enumerate DIA window groups for ParamGroup userParams.</summary>
    internal TdfMetadata Metadata => _meta;

    public IEnumerable<DiaFrameWindow> EnumerateDiaFrameWindows() => _meta.EnumerateDiaFrameWindows();

    public IEnumerable<BrukerChromatogramPoint> EnumerateChromatogramPoints(int preferOnlyMsLevel)
    {
        // When there are no PASEF DDA precursors, each frame contributes a single point —
        // matches pwiz C++ for non-PASEF and DIA-PASEF data.
        if (!HasPasefData || preferOnlyMsLevel == 1)
        {
            foreach (var frame in _meta.EnumerateFrames(preferOnlyMsLevel))
            {
                if (frame.NumPeaks == 0) continue; // skip calibration / warm-up frames (cpp TimsData.cpp:247)
                yield return new BrukerChromatogramPoint(
                    frame.RetentionTimeSeconds,
                    frame.SummedIntensities,
                    frame.MaxIntensity,
                    frame.MsMsType == MsMsType.Ms1 ? 1 : 2);
            }
            yield break;
        }

        // PASEF DDA: interleave MS1 frame TIC points with interpolated MS2 points derived from
        // per-precursor intensities. Mirrors pwiz C++ TimsData.cpp:380-443 (inner loop) and
        // lines 668-689 (final flush) — MS2 intensities accumulated per frame are distributed
        // evenly across the time span from the last MS2 frame to the next.
        foreach (var p in BuildPasefChromatogramPoints(preferOnlyMsLevel))
            yield return p;
    }

    private List<BrukerChromatogramPoint> BuildPasefChromatogramPoints(int preferOnlyMsLevel)
    {
        var output = new List<BrukerChromatogramPoint>();
        var ms1Frames = preferOnlyMsLevel != 2
            ? _meta.EnumerateFrames(1).Where(f => f.NumPeaks > 0).ToList()
            : new List<TdfFrame>();
        var ms2FramesById = _meta.EnumerateFrames(2).Where(f => f.NumPeaks > 0).ToDictionary(f => f.FrameId);
        var precursors = _meta.EnumeratePasefPrecursors().ToList();

        int ms1Idx = 0;
        double lastMs2Time = 0, ms2Time = 0;
        var ms2Intensities = new List<double>();

        void FlushMs2(double toTime)
        {
            double timeDelta = toTime - lastMs2Time;
            if (toTime == lastMs2Time && output.Count > 1)
                timeDelta = toTime - output[^2].RetentionTimeSeconds;
            for (int i = 0; i < ms2Intensities.Count; i++)
            {
                double t = lastMs2Time + (timeDelta / ms2Intensities.Count) * (i + 1);
                output.Add(new BrukerChromatogramPoint(t, ms2Intensities[i], ms2Intensities[i], 2));
            }
            lastMs2Time = toTime;
            ms2Intensities.Clear();
        }

        foreach (var info in precursors)
        {
            if (!ms2FramesById.TryGetValue(info.FrameId, out var frame)) continue;

            // Interleave MS1 points whose time is before this PASEF frame.
            while (ms1Idx < ms1Frames.Count && ms1Frames[ms1Idx].RetentionTimeSeconds < frame.RetentionTimeSeconds)
            {
                var m1 = ms1Frames[ms1Idx++];
                output.Add(new BrukerChromatogramPoint(m1.RetentionTimeSeconds, m1.SummedIntensities, m1.MaxIntensity, 1));
            }

            if (lastMs2Time == 0) lastMs2Time = frame.RetentionTimeSeconds;

            if (ms2Intensities.Count > 0 && ms2Time != frame.RetentionTimeSeconds)
                FlushMs2(frame.RetentionTimeSeconds);

            ms2Time = frame.RetentionTimeSeconds;
            ms2Intensities.Add(info.Intensity);
        }

        // Final flush for the last MS2 frame's accumulated intensities.
        if (ms2Intensities.Count > 0)
            FlushMs2(ms2Time);

        // Emit any remaining MS1 points after the last PASEF frame.
        while (ms1Idx < ms1Frames.Count)
        {
            var m1 = ms1Frames[ms1Idx++];
            output.Add(new BrukerChromatogramPoint(m1.RetentionTimeSeconds, m1.SummedIntensities, m1.MaxIntensity, 1));
        }
        return output;
    }

    public List<LcTrace> ReadLcTraces() => ChromatographyDataSqlite.ReadAll(AnalysisDirectory);

    // ---------- index build ----------

    private sealed class Tag
    {
        public TdfFrame Frame = null!;
        public int ScanBegin;  // 0-based
        public int ScanEnd;    // 0-based inclusive
        public DiaFrameWindow? DiaWindow;
        public PasefPrecursorInfo? PasefPrecursor;
        public bool Combined;  // true when combineIonMobilitySpectra
    }

    private static BrukerIndexEntry MakeCombinedEntry(int idx, TdfFrame frame, int scanBegin, int scanEnd, Tag tag)
    {
        // Matches pwiz C++ SpectrumList_Bruker.cpp native-id format for combineIonMobilitySpectra:
        //   "merged={index} frame={frameId} scanStart={begin+1} scanEnd={end+1}"
        string id = "merged=" + idx.ToString(CultureInfo.InvariantCulture) +
                    " frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture) +
                    " scanStart=" + (scanBegin + 1).ToString(CultureInfo.InvariantCulture) +
                    " scanEnd=" + (scanEnd + 1).ToString(CultureInfo.InvariantCulture);
        return new BrukerIndexEntry { Index = idx, Id = id, Tag = tag };
    }

    public IReadOnlyList<BrukerIndexEntry> BuildSpectrumIndex(bool combineIonMobilitySpectra, int preferOnlyMsLevel)
    {
        var index = new List<BrukerIndexEntry>();
        var diaByFrame = HasDiaPasefData
            ? _meta.EnumerateDiaFrameWindows()
                .GroupBy(w => w.FrameId)
                .ToDictionary(g => g.Key, g => g.ToList())
            : new Dictionary<long, List<DiaFrameWindow>>();
        var pasefByFrame = HasPasefData
            ? _meta.EnumeratePasefPrecursors()
                .GroupBy(p => p.FrameId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.ScanBegin).ToList())
            : new Dictionary<long, List<PasefPrecursorInfo>>();

        foreach (var frame in _meta.EnumerateFrames(preferOnlyMsLevel))
        {
            if (frame.NumScans <= 0) continue;
            // pwiz C++ TimsData.cpp:247-248 skips frames with NumPeaks == 0 — these are
            // typically calibration / warm-up frames at the start of the run that carry no
            // usable spectra.
            if (frame.NumPeaks == 0) continue;

            if (combineIonMobilitySpectra)
            {
                // PASEF DDA: one combined spectrum per precursor (merge scans in the precursor's range).
                if (frame.MsMsType != MsMsType.Ms1 && pasefByFrame.TryGetValue(frame.FrameId, out var ddaPrecursors))
                {
                    foreach (var p in ddaPrecursors)
                    {
                        int lastScan = Math.Min(frame.NumScans - 1, p.ScanEnd);
                        if (lastScan < p.ScanBegin) continue;
                        index.Add(MakeCombinedEntry(
                            index.Count, frame, p.ScanBegin, lastScan,
                            new Tag { Frame = frame, ScanBegin = p.ScanBegin, ScanEnd = lastScan, PasefPrecursor = p, Combined = true }));
                    }
                    continue;
                }

                // DIA-PASEF: one combined spectrum per isolation window.
                if (frame.MsMsType != MsMsType.Ms1 && diaByFrame.TryGetValue(frame.FrameId, out var diaWindows))
                {
                    foreach (var w in diaWindows)
                    {
                        int lastScan = Math.Min(frame.NumScans - 1, w.ScanEnd);
                        if (lastScan < w.ScanBegin) continue;
                        index.Add(MakeCombinedEntry(
                            index.Count, frame, w.ScanBegin, lastScan,
                            new Tag { Frame = frame, ScanBegin = w.ScanBegin, ScanEnd = lastScan, DiaWindow = w, Combined = true }));
                    }
                    continue;
                }

                // MS1 or non-PASEF MS2: one spectrum for the whole frame.
                if (frame.NumPeaks == 0) continue;
                index.Add(MakeCombinedEntry(
                    index.Count, frame, 0, frame.NumScans - 1,
                    new Tag { Frame = frame, ScanBegin = 0, ScanEnd = frame.NumScans - 1, Combined = true }));
                continue;
            }

            // DIA-PASEF MS2: one spectrum per scan in each isolation window; skip gaps.
            if (frame.MsMsType != MsMsType.Ms1 && diaByFrame.TryGetValue(frame.FrameId, out var windows))
            {
                foreach (var w in windows)
                {
                    int lastScan = Math.Min(frame.NumScans - 1, w.ScanEnd);
                    for (int scan = w.ScanBegin; scan <= lastScan; scan++)
                        index.Add(new BrukerIndexEntry
                        {
                            Index = index.Count,
                            Id = "frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture) +
                                 " scan=" + (scan + 1).ToString(CultureInfo.InvariantCulture),
                            Tag = new Tag { Frame = frame, ScanBegin = scan, ScanEnd = scan, DiaWindow = w },
                        });
                }
                continue;
            }

            // PASEF DDA MS2: emit one spectrum per scan (matching pwiz C++ default
            // allowMsMsWithoutPrecursor=true) and attach the PasefPrecursorInfo to scans
            // whose scan number falls within a precursor's [scanBegin, scanEnd] range.
            // Scans in the gaps between precursors are still emitted, with no precursor.
            if (frame.MsMsType != MsMsType.Ms1 && pasefByFrame.TryGetValue(frame.FrameId, out var precursors))
            {
                int pIdx = 0;
                for (int scan = 0; scan < frame.NumScans; scan++)
                {
                    while (pIdx < precursors.Count && scan > precursors[pIdx].ScanEnd) pIdx++;
                    var attached = pIdx < precursors.Count
                        && scan >= precursors[pIdx].ScanBegin && scan <= precursors[pIdx].ScanEnd
                            ? precursors[pIdx]
                            : null;
                    index.Add(new BrukerIndexEntry
                    {
                        Index = index.Count,
                        Id = "frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture) +
                             " scan=" + (scan + 1).ToString(CultureInfo.InvariantCulture),
                        Tag = new Tag { Frame = frame, ScanBegin = scan, ScanEnd = scan, PasefPrecursor = attached },
                    });
                }
                continue;
            }

            // Default per-(frame, scan) emission for MS1 + non-PASEF MS2.
            for (int scan = 0; scan < frame.NumScans; scan++)
                index.Add(new BrukerIndexEntry
                {
                    Index = index.Count,
                    Id = "frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture) +
                         " scan=" + (scan + 1).ToString(CultureInfo.InvariantCulture),
                    Tag = new Tag { Frame = frame, ScanBegin = scan, ScanEnd = scan },
                });
        }
        return index;
    }

    // ---------- spectrum fill ----------

    public void FillSpectrum(Spectrum spec, BrukerIndexEntry entry, bool getBinaryData, bool preferCentroid)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(entry);
        _ = preferCentroid; // TDF is always centroided at the vendor API level.
        var tag = (Tag)entry.Tag;
        var frame = tag.Frame;

        int msLevel = frame.MsMsType == MsMsType.Ms1 ? 1 : 2;
        spec.Params.Set(CVID.MS_ms_level, msLevel);
        spec.Params.Set(msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);
        spec.Params.Set(frame.Polarity == IonPolarity.Positive
            ? CVID.MS_positive_scan : CVID.MS_negative_scan);
        // pwiz C++ attr order: base peak + TIC before centroid; no unit on base peak intensity.
        spec.Params.Set(CVID.MS_base_peak_intensity, frame.MaxIntensity);
        spec.Params.Set(CVID.MS_total_ion_current, frame.SummedIntensities);
        spec.Params.Set(CVID.MS_centroid_spectrum);

        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, frame.RetentionTimeSeconds, CVID.UO_second);

        // Per-scan 1/K0 for per-scan TDF spectra (matches pwiz C++ — emitted whenever
        // combineIonMobilitySpectra is off and the scan covers a single mobility bin).
        double scanInvK0 = 0;
        if (!tag.Combined && tag.ScanBegin == tag.ScanEnd)
        {
            scanInvK0 = _tims.ScanNumberToOneOverK0(frame.FrameId, new[] { (double)tag.ScanBegin })[0];
            if (scanInvK0 > 0)
                scan.Set(CVID.MS_inverse_reduced_ion_mobility, scanInvK0, CVID.MS_volt_second_per_square_centimeter);
        }
        // windowGroup userParam marks DIA-PASEF MS2 membership.
        if (tag.DiaWindow is not null)
            scan.UserParams.Add(new UserParam(
                "windowGroup",
                tag.DiaWindow.WindowGroup.ToString(CultureInfo.InvariantCulture)));

        var (mzLow, mzHigh) = _meta.MzAcquisitionRange;
        if (mzHigh > 0)
            scan.ScanWindows.Add(new ScanWindow(mzLow, mzHigh, CVID.MS_m_z));

        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        if (tag.DiaWindow is not null)
            AddDiaPrecursor(spec, tag.DiaWindow);
        else if (tag.PasefPrecursor is not null)
            AddPasefPrecursor(spec, tag.PasefPrecursor, scanInvK0);
        else if (frame.MsMsType != MsMsType.Ms1 && frame.PrecursorMz.HasValue)
            AddFrameMsMsInfoPrecursor(spec, frame, scanInvK0);

        // pwiz C++ leaves MS_lowest/highest_observed_m_z commented-out for TDF.
        var (mz, intensity) = ReadScanRangePeaks(frame, tag.ScanBegin, tag.ScanEnd);
        spec.DefaultArrayLength = mz.Length;
        // Always emit (possibly empty) m/z + intensity arrays when binary data is requested.
        if (getBinaryData)
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
    }

    private static void AddDiaPrecursor(Spectrum spec, DiaFrameWindow window)
    {
        var precursor = new Precursor();
        double isolationMz = window.IsolationMz;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isolationMz, CVID.MS_m_z);
        double half = window.IsolationWidth / 2.0;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, half, CVID.MS_m_z);
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, half, CVID.MS_m_z);

        var selected = new SelectedIon();
        selected.Set(CVID.MS_selected_ion_m_z, isolationMz, CVID.MS_m_z);
        precursor.SelectedIons.Add(selected);

        precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        if (Math.Abs(window.CollisionEnergy) > 0)
            precursor.Activation.Set(CVID.MS_collision_energy, Math.Abs(window.CollisionEnergy));
        spec.Precursors.Add(precursor);
    }

    private static void AddPasefPrecursor(Spectrum spec, PasefPrecursorInfo info, double oneOverK0)
    {
        var precursor = new Precursor();
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, info.IsolationMz, CVID.MS_m_z);
        double half = info.IsolationWidth / 2.0;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, half, CVID.MS_m_z);
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, half, CVID.MS_m_z);

        var selected = new SelectedIon();
        double selectedMz = info.MonoisotopicMz > 0 ? info.MonoisotopicMz : info.IsolationMz;
        selected.Set(CVID.MS_selected_ion_m_z, selectedMz, CVID.MS_m_z);
        if (info.Charge > 0)
            selected.Set(CVID.MS_charge_state, info.Charge);
        if (info.Intensity > 0)
            selected.Set(CVID.MS_peak_intensity, info.Intensity, CVID.MS_number_of_detector_counts);
        if (oneOverK0 > 0 && info.Charge > 0 && selectedMz > 0)
        {
            double ccs = TimsBinaryData.OneOverK0ToCcs(oneOverK0, info.Charge, selectedMz);
            if (ccs > 0)
                selected.Set(CVID.MS_collisional_cross_sectional_area, ccs, CVID.UO_square_angstrom);
        }
        precursor.SelectedIons.Add(selected);

        precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        // pwiz C++ emits CE without a unit for Bruker DDA (see SpectrumList_Bruker.cpp:368).
        if (Math.Abs(info.CollisionEnergy) > 0)
            precursor.Activation.Set(CVID.MS_collision_energy, Math.Abs(info.CollisionEnergy));
        spec.Precursors.Add(precursor);
    }

    private static void AddFrameMsMsInfoPrecursor(Spectrum spec, TdfFrame frame, double oneOverK0)
    {
        var precursor = new Precursor();
        double isolationMz = frame.PrecursorMz!.Value;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isolationMz, CVID.MS_m_z);
        if (frame.IsolationWidth.HasValue)
        {
            double half = frame.IsolationWidth.Value / 2.0;
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, half, CVID.MS_m_z);
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, half, CVID.MS_m_z);
        }

        var selected = new SelectedIon();
        selected.Set(CVID.MS_selected_ion_m_z, isolationMz, CVID.MS_m_z);
        if (frame.PrecursorCharge.HasValue)
            selected.Set(CVID.MS_charge_state, frame.PrecursorCharge.Value);
        // Per-scan CCS when 1/K0 + charge are available — pwiz C++ does this for PASEF per-scan
        // emissions (SpectrumList_Bruker.cpp:312-313) even when no PasefPrecursorInfo row
        // attaches to this scan.
        if (oneOverK0 > 0 && frame.PrecursorCharge is int charge && charge > 0 && isolationMz > 0)
        {
            double ccs = TimsBinaryData.OneOverK0ToCcs(oneOverK0, charge, isolationMz);
            if (ccs > 0)
                selected.Set(CVID.MS_collisional_cross_sectional_area, ccs, CVID.UO_square_angstrom);
        }
        precursor.SelectedIons.Add(selected);

        precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        if (frame.CollisionEnergy.HasValue && frame.CollisionEnergy.Value > 0)
            precursor.Activation.Set(CVID.MS_collision_energy, frame.CollisionEnergy.Value, CVID.UO_electronvolt);
        spec.Precursors.Add(precursor);
    }

    /// <summary>
    /// Reads peaks for the scan range <c>[scanBegin, scanEnd]</c> inclusive. Single-scan requests
    /// return the arrays sorted by m/z; multi-scan requests merge duplicate m/z across mobility
    /// scans by summation.
    /// </summary>
    private (double[] Mz, double[] Intensity) ReadScanRangePeaks(TdfFrame frame, int scanBegin, int scanEnd)
    {
        var proxy = GetFrame(frame.FrameId, frame.NumScans);

        int numRequested = scanEnd - scanBegin + 1;
        if (numRequested == 1)
        {
            var mzs = proxy.GetScanMzs(scanBegin);
            var ints = proxy.GetScanIntensities(scanBegin);
            if (mzs.Length == 0) return (Array.Empty<double>(), Array.Empty<double>());
            var mzArr = mzs.ToArray();
            var intArr = new double[ints.Length];
            for (int i = 0; i < ints.Length; i++) intArr[i] = ints[i];
            return (mzArr, intArr);
        }

        int total = 0;
        for (int s = scanBegin; s <= scanEnd; s++) total += proxy.NumPeaks(s);
        if (total == 0) return (Array.Empty<double>(), Array.Empty<double>());

        var combined = new (double mz, double intensity)[total];
        int write = 0;
        for (int s = scanBegin; s <= scanEnd; s++)
        {
            var mzs = proxy.GetScanMzs(s);
            var ints = proxy.GetScanIntensities(s);
            for (int i = 0; i < mzs.Length; i++)
                combined[write++] = (mzs[i], ints[i]);
        }
        Array.Sort(combined, (a, b) => a.mz.CompareTo(b.mz));

        var mzOut = new List<double>(combined.Length);
        var intOut = new List<double>(combined.Length);
        double curMz = combined[0].mz;
        double curI = combined[0].intensity;
        for (int i = 1; i < combined.Length; i++)
        {
            if (combined[i].mz == curMz) { curI += combined[i].intensity; continue; }
            mzOut.Add(curMz); intOut.Add(curI);
            curMz = combined[i].mz; curI = combined[i].intensity;
        }
        mzOut.Add(curMz); intOut.Add(curI);
        return (mzOut.ToArray(), intOut.ToArray());
    }

    private FrameProxy GetFrame(long frameId, int numScans)
    {
        if (_cachedFrame is not null && _cachedFrameId == frameId) return _cachedFrame;
        _cachedFrame = _tims.ReadScans(frameId, 0, (uint)numScans, performMzConversion: true);
        _cachedFrameId = frameId;
        return _cachedFrame;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tims.Dispose();
        _meta.Dispose();
    }
}
