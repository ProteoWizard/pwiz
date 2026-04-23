using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// <see cref="IBrukerData"/> backed by a TSF (non-ion-mobility timsTOF) <c>.d</c> directory —
/// wraps <see cref="TsfBinaryData"/> (binary reads) + <see cref="TsfMetadata"/> (SQLite metadata).
/// One spectrum per frame, with MALDI spot annotations when present.
/// </summary>
internal sealed class TsfData : IBrukerData
{
    private readonly TsfBinaryData _tsf;
    private readonly TsfMetadata _meta;
    private readonly bool _preferProfile;
    private bool _disposed;

    public TsfData(string analysisDirectory, bool useRecalibratedState)
    {
        AnalysisDirectory = analysisDirectory;
        _meta = new TsfMetadata(analysisDirectory);
        _tsf = new TsfBinaryData(analysisDirectory, useRecalibratedState);
        _preferProfile = _meta.HasProfileSpectra;
    }

    public BrukerFormat Format => BrukerFormat.Tsf;
    public string AnalysisDirectory { get; }
    public IReadOnlyDictionary<string, string> GlobalMetadata => _meta.GlobalMetadata;
    public bool HasPasefData => false;
    public bool HasDiaPasefData => false;
    public bool HasMs1Frames => _meta.HasMs1Frames;
    public bool HasMsNFrames => _meta.HasMsNFrames;
    public (double Low, double High) MzAcquisitionRange => _meta.MzAcquisitionRange;

    /// <summary>
    /// TSF MALDI is detected via the first frame's <see cref="TsfScanMode.Maldi"/> marker
    /// (pwiz C++ does the same — <c>translateScanModeToInstrumentSource</c>).
    /// </summary>
    public bool IsMaldiSource =>
        _meta.EnumerateFrames().FirstOrDefault()?.ScanMode == TsfScanMode.Maldi;

    public IEnumerable<DiaFrameWindow> EnumerateDiaFrameWindows() => Array.Empty<DiaFrameWindow>();

    public IEnumerable<BrukerChromatogramPoint> EnumerateChromatogramPoints(int preferOnlyMsLevel)
    {
        foreach (var frame in _meta.EnumerateFrames(preferOnlyMsLevel))
        {
            yield return new BrukerChromatogramPoint(
                frame.RetentionTimeSeconds,
                frame.SummedIntensities,
                frame.MaxIntensity,
                frame.MsMsType == MsMsType.Ms1 ? 1 : 2);
        }
    }

    public List<LcTrace> ReadLcTraces() => ChromatographyDataSqlite.ReadAll(AnalysisDirectory);

    // ---------- index build ----------

    private sealed class Tag
    {
        public TsfFrame Frame = null!;
    }

    public IReadOnlyList<BrukerIndexEntry> BuildSpectrumIndex(bool combineIonMobilitySpectra, int preferOnlyMsLevel)
    {
        _ = combineIonMobilitySpectra; // TSF has no mobility dimension; flag is a no-op here.
        var index = new List<BrukerIndexEntry>();
        foreach (var frame in _meta.EnumerateFrames(preferOnlyMsLevel))
        {
            if (frame.NumPeaks == 0) continue;
            index.Add(new BrukerIndexEntry
            {
                Index = index.Count,
                Id = "frame=" + frame.FrameId.ToString(CultureInfo.InvariantCulture),
                Tag = new Tag { Frame = frame },
            });
        }
        return index;
    }

    // ---------- spectrum fill ----------

    public void FillSpectrum(Spectrum spec, BrukerIndexEntry entry, bool getBinaryData, bool preferCentroid)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(entry);
        var tag = (Tag)entry.Tag;
        var frame = tag.Frame;

        int msLevel = frame.MsMsType == MsMsType.Ms1 ? 1 : 2;
        spec.Params.Set(CVID.MS_ms_level, msLevel);
        spec.Params.Set(msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);
        spec.Params.Set(frame.Polarity == IonPolarity.Positive
            ? CVID.MS_positive_scan : CVID.MS_negative_scan);

        // pwiz C++ emits base peak + TIC without unit, only when TIC > 0.
        if (frame.SummedIntensities > 0)
        {
            spec.Params.Set(CVID.MS_base_peak_intensity, frame.MaxIntensity);
            spec.Params.Set(CVID.MS_total_ion_current, frame.SummedIntensities);
        }

        var scan = new Scan();
        if (frame.RetentionTimeSeconds > 0)
            scan.Set(CVID.MS_scan_start_time, frame.RetentionTimeSeconds, CVID.UO_second);

        var (mzLow, mzHigh) = _meta.MzAcquisitionRange;
        if (mzHigh > 0)
            scan.ScanWindows.Add(new ScanWindow(mzLow, mzHigh, CVID.MS_m_z));

        if (frame.MaldiChip.HasValue)
        {
            spec.SpotId = "chip=" + frame.MaldiChip.Value.ToString(CultureInfo.InvariantCulture)
                        + " spot=" + (frame.MaldiSpotName ?? string.Empty);
        }

        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        if (msLevel > 1 && frame.PrecursorMz.HasValue)
            AddTsfPrecursor(spec, frame);

        // Read peaks: caller may override via preferCentroid (vendor peak-picking feed).
        double[] mz = Array.Empty<double>();
        double[] intensity = Array.Empty<double>();
        bool isCentroid = false;
        bool useProfile = _preferProfile && !preferCentroid;
        if (useProfile)
            (mz, intensity) = _tsf.ReadProfileSpectrum(frame.FrameId);
        if (mz.Length == 0 && _meta.HasLineSpectra)
        {
            (mz, intensity) = _tsf.ReadLineSpectrum(frame.FrameId);
            isCentroid = true;
        }
        spec.Params.Set(isCentroid ? CVID.MS_centroid_spectrum : CVID.MS_profile_spectrum);
        spec.DefaultArrayLength = mz.Length;

        if (mz.Length > 0 && getBinaryData)
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
    }

    private static void AddTsfPrecursor(Spectrum spec, TsfFrame frame)
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
        precursor.SelectedIons.Add(selected);

        precursor.Activation.Set(TranslateActivation(frame.ScanMode));
        // pwiz C++ TSF always reports CE=0 from its getIsolationData, so we drop it here
        // too (byte-for-byte parity) — the FrameMsMsInfo column is non-empty but unused.
        spec.Precursors.Add(precursor);
    }

    private static CVID TranslateActivation(TsfScanMode scanMode) => scanMode switch
    {
        TsfScanMode.IsCid or TsfScanMode.BbCid => CVID.MS_in_source_collision_induced_dissociation,
        _ => CVID.MS_collision_induced_dissociation,
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tsf.Dispose();
        _meta.Dispose();
    }
}
