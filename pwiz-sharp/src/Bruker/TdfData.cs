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
        // pwiz C++ SpectrumList_Bruker.cpp:381 emits short combineIMS native id "merged=N";
        // the per-scan list of `<scan spectrumRef="frame=F scan=S">` carries the merged scan
        // membership in scanList. (Earlier we used the longer form derived from
        // SpectrumList_Bruker.cpp:785 which is the *spectrumIdentity index id*, not the
        // emitted native id — our older smoke test pinned to "merged=0 frame=1" because
        // that prefix happened to match.)
        _ = scanBegin; _ = scanEnd;
        string id = "merged=" + idx.ToString(CultureInfo.InvariantCulture);
        return new BrukerIndexEntry { Index = idx, Id = id, Tag = tag, MsLevel = frame.MsMsType == MsMsType.Ms1 ? 1 : 2 };
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
                            MsLevel = 2,
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
                        MsLevel = 2,
                    });
                }
                continue;
            }

            // Default per-(frame, scan) emission for MS1 + non-PASEF MS2.
            int defaultLevel = frame.MsMsType == MsMsType.Ms1 ? 1 : 2;
            for (int scan = 0; scan < frame.NumScans; scan++)
                index.Add(new BrukerIndexEntry
                {
                    Index = index.Count,
                    Id = "frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture) +
                         " scan=" + (scan + 1).ToString(CultureInfo.InvariantCulture),
                    Tag = new Tag { Frame = frame, ScanBegin = scan, ScanEnd = scan },
                    MsLevel = defaultLevel,
                });
        }
        return index;
    }

    // ---------- spectrum fill ----------

    public void FillSpectrum(Spectrum spec, BrukerIndexEntry entry, bool getBinaryData, bool preferCentroid, bool sortAndJitter = false)
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

        // Per-scan emission marks the spectrum as centroid; combined mode does NOT (mirrors
        // pwiz C++ output which doesn't tag combined PASEF spectra with MS_centroid_spectrum).
        if (!tag.Combined)
            spec.Params.Set(CVID.MS_centroid_spectrum);

        // Build the scanList. Per-scan TDF emission has one Scan with full metadata; combined
        // mode has one Scan per merged scan (only the first carries time/invK0/scanWindow per
        // pwiz C++ SpectrumList_Bruker.cpp:398-414).
        var scan = new Scan();
        scan.Set(CVID.MS_scan_start_time, frame.RetentionTimeSeconds, CVID.UO_second);

        double scanInvK0 = 0;
        if (!tag.Combined && tag.ScanBegin == tag.ScanEnd)
        {
            scanInvK0 = _tims.ScanNumberToOneOverK0(frame.FrameId, new[] { (double)tag.ScanBegin })[0];
            if (scanInvK0 > 0)
                scan.Set(CVID.MS_inverse_reduced_ion_mobility, scanInvK0, CVID.MS_volt_second_per_square_centimeter);
        }
        else if (tag.Combined && (tag.ScanEnd - tag.ScanBegin + 1) < 100)
        {
            // PASEF combined (< 100 merged scans, typically per-precursor). pwiz C++ emits
            // a multi-scan list and labels the first scan with the raw 0-based TDF scan
            // number plus an invK0 computed at the precursor's avg-scan-number (fractional
            // centroid mobility from PasefFrameMsMsInfo+Precursors join).
            scan.SpectrumId = "frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture) +
                              " scan=" + tag.ScanBegin.ToString(CultureInfo.InvariantCulture);
            double k0Probe = tag.PasefPrecursor?.AvgScanNumber > 0
                ? tag.PasefPrecursor.AvgScanNumber
                : tag.ScanBegin;
            scanInvK0 = _tims.ScanNumberToOneOverK0(frame.FrameId, new[] { k0Probe })[0];
            if (scanInvK0 > 0)
                scan.Set(CVID.MS_inverse_reduced_ion_mobility, scanInvK0, CVID.MS_volt_second_per_square_centimeter);
        }
        // MS1 combined (>= 100 merged scans): no spectrumRef, no invK0 on the single scan
        // element, no per-scan list (matches ref combineIMS mzML for whole-frame summaries).

        if (tag.DiaWindow is not null)
            scan.UserParams.Add(new UserParam(
                "windowGroup",
                tag.DiaWindow.WindowGroup.ToString(CultureInfo.InvariantCulture)));

        var (mzLow, mzHigh) = _meta.MzAcquisitionRange;
        if (mzHigh > 0)
            scan.ScanWindows.Add(new ScanWindow(mzLow, mzHigh, CVID.MS_m_z));

        // Multi-scan emission only fires when fewer than 100 scans are merged (matches pwiz C++
        // SpectrumList_Bruker.cpp:390 `if (scanNumbers.size() < 100)`); this is the regime for
        // PASEF per-precursor combined spectra (~25 scans) but not for MS1 combined frames
        // (~900 scans), which keep the simpler single-scan + MS_no_combination layout.
        int mergedScanCount = tag.Combined ? (tag.ScanEnd - tag.ScanBegin + 1) : 1;
        bool emitMergedList = tag.Combined && mergedScanCount < 100;
        if (emitMergedList)
            spec.ScanList.Set(CVID.MS_sum_of_spectra);
        else
            spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        if (emitMergedList)
        {
            for (int s = tag.ScanBegin + 1; s <= tag.ScanEnd; s++)
            {
                var extra = new Scan
                {
                    SpectrumId = "frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture) +
                                 " scan=" + s.ToString(CultureInfo.InvariantCulture),
                };
                spec.ScanList.Scans.Add(extra);
            }
        }

        if (tag.DiaWindow is not null)
            AddDiaPrecursor(spec, tag.DiaWindow, omitMsLevelUserParam: tag.Combined);
        else if (tag.PasefPrecursor is not null)
            AddPasefPrecursor(spec, tag.PasefPrecursor, scanInvK0, omitMsLevelUserParam: tag.Combined, omitCcs: tag.Combined);
        else if (frame.MsMsType != MsMsType.Ms1 && frame.PrecursorMz.HasValue)
            AddFrameMsMsInfoPrecursor(spec, frame, scanInvK0);

        // pwiz C++ leaves MS_lowest/highest_observed_m_z commented-out for TDF.
        double[] meanMobility = Array.Empty<double>();
        var (mz, intensity) = tag.Combined
            ? ReadCombinedScanRangePeaks(frame, tag.ScanBegin, tag.ScanEnd, sortAndJitter, out meanMobility)
            : ReadScanRangePeaks(frame, tag.ScanBegin, tag.ScanEnd);
        spec.DefaultArrayLength = mz.Length;
        if (getBinaryData)
        {
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
            if (tag.Combined && meanMobility.Length == mz.Length && mz.Length > 0)
            {
                var mobArr = new BinaryDataArray();
                // pwiz C++ uses MS_mean_inverse_reduced_ion_mobility_array as its enum name
                // but the underlying accession is MS:1002816 — which our generated CV table
                // names MS_mean_ion_mobility_array (newer OBO display name; same accession).
                mobArr.Set(CVID.MS_mean_ion_mobility_array, "", CVID.MS_volt_second_per_square_centimeter);
                mobArr.Data.AddRange(meanMobility);
                spec.BinaryDataArrays.Add(mobArr);
            }
        }
    }

    private static void AddDiaPrecursor(Spectrum spec, DiaFrameWindow window, bool omitMsLevelUserParam = false)
    {
        var precursor = new Precursor();
        double isolationMz = window.IsolationMz;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isolationMz, CVID.MS_m_z);
        double half = window.IsolationWidth / 2.0;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, half, CVID.MS_m_z);
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, half, CVID.MS_m_z);
        _ = omitMsLevelUserParam; // DIA emission doesn't add an "ms level" userParam in either mode.

        var selected = new SelectedIon();
        selected.Set(CVID.MS_selected_ion_m_z, isolationMz, CVID.MS_m_z);
        precursor.SelectedIons.Add(selected);

        precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        if (Math.Abs(window.CollisionEnergy) > 0)
            precursor.Activation.Set(CVID.MS_collision_energy, Math.Abs(window.CollisionEnergy));
        spec.Precursors.Add(precursor);
    }

    private static void AddPasefPrecursor(Spectrum spec, PasefPrecursorInfo info, double oneOverK0, bool omitMsLevelUserParam = false, bool omitCcs = false)
    {
        var precursor = new Precursor();
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, info.IsolationMz, CVID.MS_m_z);
        double half = info.IsolationWidth / 2.0;
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, half, CVID.MS_m_z);
        precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, half, CVID.MS_m_z);
        _ = omitMsLevelUserParam; // we don't add an "ms level" userParam in either mode here.

        var selected = new SelectedIon();
        double selectedMz = info.MonoisotopicMz > 0 ? info.MonoisotopicMz : info.IsolationMz;
        selected.Set(CVID.MS_selected_ion_m_z, selectedMz, CVID.MS_m_z);
        if (info.Charge > 0)
            selected.Set(CVID.MS_charge_state, info.Charge);
        if (!omitCcs && info.Intensity > 0)
            selected.Set(CVID.MS_peak_intensity, info.Intensity, CVID.MS_number_of_detector_counts);
        if (!omitCcs && oneOverK0 > 0 && info.Charge > 0 && selectedMz > 0)
        {
            double ccs = TimsBinaryData.OneOverK0ToCcs(oneOverK0, info.Charge, selectedMz);
            if (ccs > 0)
                selected.Set(CVID.MS_collisional_cross_sectional_area, ccs, CVID.UO_square_angstrom);
        }
        precursor.SelectedIons.Add(selected);

        precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        // Combined-mode PASEF doesn't emit collision energy (matches pwiz C++ — the per-scan
        // path emits CE but the combined path drops it because CE may vary across the merged
        // scan range).
        if (!omitCcs && Math.Abs(info.CollisionEnergy) > 0)
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

    /// <summary>
    /// Combined-mode peak reader: concatenates each scan's peaks (m/z, intensity, 1/K0)
    /// in scan order WITHOUT merging. Mirrors pwiz C++ TimsSpectrum::getCombinedSpectrumData
    /// (TimsData.cpp:1022+) which preserves per-scan peaks one-to-one so the
    /// MS_mean_inverse_reduced_ion_mobility_array carries the actual scan-level 1/K0 of
    /// each emitted peak. Output arrays are aligned: <c>mz[i] / intensity[i] / mobility[i]</c>.
    /// </summary>
    private (double[] Mz, double[] Intensity) ReadCombinedScanRangePeaks(
        TdfFrame frame, int scanBegin, int scanEnd, bool sortAndJitter, out double[] meanMobility)
    {
        var proxy = GetFrame(frame.FrameId, frame.NumScans);

        int total = 0;
        for (int s = scanBegin; s <= scanEnd; s++) total += proxy.NumPeaks(s);
        if (total == 0)
        {
            meanMobility = Array.Empty<double>();
            return (Array.Empty<double>(), Array.Empty<double>());
        }

        // Pre-compute 1/K0 for each scan in the range (single batched native call).
        int rangeLen = scanEnd - scanBegin + 1;
        var scanArgs = new double[rangeLen];
        for (int i = 0; i < rangeLen; i++) scanArgs[i] = scanBegin + i;
        var invK0PerScan = _tims.ScanNumberToOneOverK0(frame.FrameId, scanArgs);

        var mzArr = new double[total];
        var intArr = new double[total];
        var mobArr = new double[total];
        int write = 0;
        for (int s = scanBegin; s <= scanEnd; s++)
        {
            double k0 = invK0PerScan[s - scanBegin];
            var mzs = proxy.GetScanMzs(s);
            var ints = proxy.GetScanIntensities(s);
            for (int i = 0; i < mzs.Length; i++)
            {
                mzArr[write] = mzs[i];
                intArr[write] = ints[i];
                mobArr[write] = k0;
                write++;
            }
        }
        if (!sortAndJitter)
        {
            // Production mode: keep peaks in scan-by-scan order. mzML doesn't mandate m/z
            // ordering and downstream tools handle either layout — skipping the sort + jitter
            // means our output is cheaper to produce and the m/z values are exact (no 1e-8
            // perturbation on duplicates).
            meanMobility = mobArr;
            return (mzArr, intArr);
        }

        // Test/reference-parity mode: sort by m/z across all merged scans, then jitter
        // duplicate m/z values by 1e-8 per row (matches pwiz C++ TimsSpectrum::
        // getCombinedSpectrumData at TimsData.cpp:1112-1147 with sortAndJitter=true).
        // Stable sort by m/z preserves scan order on duplicates. cpp uses std::sort which
        // is unstable, so its tie-break order is implementation-defined; on this fixture's
        // data libstdc++ tends to keep the smaller-scan peak first. The jitter perturbs
        // the m/z to lock the tie-break — but if our tie-break ordering differs from the
        // ref the per-position (intensity, mobility) values will still mismatch on a few
        // duplicate positions.
        var idx = Enumerable.Range(0, total).OrderBy(i => mzArr[i]).ToArray();
        var mzSorted = new double[total];
        var intSorted = new double[total];
        var mobSorted = new double[total];
        for (int i = 0; i < total; i++)
        {
            int src = idx[i];
            mzSorted[i] = mzArr[src];
            intSorted[i] = intArr[src];
            mobSorted[i] = mobArr[src];
        }
        // Jitter duplicate m/z values by 1e-8 per consecutive entry so std::sort-style
        // ordering of same-m/z peaks becomes irrelevant — matches pwiz C++ jitter at
        // TimsData.cpp:1141-1147.
        for (int i = 1; i < total; i++)
        {
            if (mzSorted[i - 1] == mzSorted[i])
            {
                int startI = i - 1;
                for (; i < total && mzSorted[startI] == mzSorted[i]; i++)
                    mzSorted[i] += 1e-8 * (i - startI);
            }
        }
        meanMobility = mobSorted;
        return (mzSorted, intSorted);
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
