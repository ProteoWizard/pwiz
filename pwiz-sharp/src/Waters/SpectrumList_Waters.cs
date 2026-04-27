using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Waters;

/// <summary>
/// <see cref="ISpectrumList"/> backed by a Waters <c>.raw</c> directory. Iterates the SDK's
/// (function, scan) grid in retention-time order and emits one mzML spectrum per non-IMS scan
/// (or per drift bin when ion mobility is enabled). Phase 1 of the port: no IMS, no DDA, no
/// lockmass. Mirrors pwiz C++ <c>SpectrumList_Waters</c>.
/// </summary>
public sealed class SpectrumList_Waters : SpectrumListBase, IDisposable
{
    private readonly WatersRawFile _data;
    private readonly bool _owns;
    private readonly int _preferOnlyMsLevel;
    private readonly bool _srmAsSpectra;
    private readonly List<IndexEntry> _index = new();
    private readonly Dictionary<string, int> _idToIndex = new(StringComparer.Ordinal);

    /// <summary>DataProcessing emitted as the <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int Function;
        public int Scan;        // 0-based scan within function
        public int MsLevel;
        public CVID SpectrumType;
    }

    internal SpectrumList_Waters(WatersRawFile data, bool owns, int preferOnlyMsLevel, bool srmAsSpectra)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _owns = owns;
        _preferOnlyMsLevel = preferOnlyMsLevel;
        _srmAsSpectra = srmAsSpectra;
        BuildIndex();
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override int Find(string id) =>
        _idToIndex.TryGetValue(id, out int v) ? v : Count;

    private void BuildIndex()
    {
        // pwiz C++ orders all (function, scan) pairs by retention time across all functions, so
        // a TIC scan from function 10 at t=0.02min sorts before a TIC scan from function 0 at
        // t=0.05min. We keep all pairs in a list of (rt, function, scan, msLevel, type) tuples,
        // sort by rt (stable on insertion order so equal-rt pairs preserve function order), then
        // assign sequential indices.
        var byRt = new List<(float Rt, int Function, int Scan, int MsLevel, CVID Type)>();
        foreach (int function in _data.FunctionIndices)
        {
            int rawType;
            try { rawType = _data.GetFunctionType(function); }
            catch { continue; }

            var ft = WatersDetail.FromMassLynxFunctionType(rawType);
            if (!WatersDetail.TranslateFunctionType(ft, out int msLevel, out CVID spectrumType))
                continue;

            // Skip non-MS function variants we don't expand to spectra in this phase.
            if (spectrumType == CVID.MS_SRM_spectrum && !_srmAsSpectra) continue;
            if (spectrumType == CVID.MS_SIM_spectrum) continue;
            if (spectrumType == CVID.MS_constant_neutral_loss_spectrum) continue;
            if (spectrumType == CVID.MS_constant_neutral_gain_spectrum) continue;

            if (_preferOnlyMsLevel > 0 && msLevel != _preferOnlyMsLevel) continue;

            int scanCount = _data.GetScanCount(function);
            for (int s = 0; s < scanCount; s++)
            {
                float rt = _data.GetRetentionTime(function, s);
                byRt.Add((rt, function, s, msLevel, spectrumType));
            }
        }

        // Stable sort by retention time (LINQ OrderBy preserves original insertion order on
        // ties) — this matches pwiz C++ which uses multimap<float, ...> + insertion order.
        var sorted = byRt.OrderBy(t => t.Rt).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            var e = sorted[i];
            string id = "function=" + (e.Function + 1).ToString(CultureInfo.InvariantCulture)
                + " process=0 scan=" + (e.Scan + 1).ToString(CultureInfo.InvariantCulture);
            var entry = new IndexEntry
            {
                Index = i,
                Id = id,
                Function = e.Function,
                Scan = e.Scan,
                MsLevel = e.MsLevel,
                SpectrumType = e.Type,
            };
            _index.Add(entry);
            _idToIndex[id] = i;
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];
        var spec = new Spectrum { Index = ie.Index, Id = ie.Id };

        spec.Params.Set(ie.SpectrumType);
        bool isMs = ie.SpectrumType == CVID.MS_MS1_spectrum || ie.SpectrumType == CVID.MS_MSn_spectrum
            || ie.SpectrumType == CVID.MS_SIM_spectrum || ie.SpectrumType == CVID.MS_SRM_spectrum
            || ie.SpectrumType == CVID.MS_constant_neutral_loss_spectrum
            || ie.SpectrumType == CVID.MS_constant_neutral_gain_spectrum;
        if (isMs) spec.Params.Set(CVID.MS_ms_level, ie.MsLevel);

        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan();
        scan.Set(CVID.MS_preset_scan_configuration, ie.Function + 1);
        spec.ScanList.Scans.Add(scan);

        double scanTimeMin = _data.GetRetentionTime(ie.Function, ie.Scan);
        scan.Set(CVID.MS_scan_start_time, scanTimeMin, CVID.UO_minute);

        if (isMs)
        {
            CVID polarity = WatersDetail.Polarity(WatersDetail.FromMassLynxIonMode(_data.GetIonMode(ie.Function)));
            if (polarity != CVID.CVID_Unknown) spec.Params.Set(polarity);
        }

        bool isProfile = isMs && _data.IsContinuum(ie.Function);
        spec.Params.Set(isProfile ? CVID.MS_profile_spectrum : CVID.MS_centroid_spectrum);

        // Base peak / TIC / peak count come from per-scan stats (not from the binary data),
        // matching pwiz C++ which fills these from MassLynxScanItem entries directly. Reading
        // a single item via the parameters round-trip (createParameters → getScanItemValue →
        // getParameterValue) is the same pattern pwiz C++ wraps in GetScanStat.
        if (isMs)
        {
            string bpMass = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.BasePeakMass);
            string bpInt = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.BasePeakIntensity);
            string tic = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.TotalIonCurrent);
            string peaks = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.PeaksInScan);

            if (TryParseDouble(bpMass, out double bpMassD)) spec.Params.Set(CVID.MS_base_peak_m_z, bpMassD);
            if (TryParseDouble(bpInt, out double bpIntD)) spec.Params.Set(CVID.MS_base_peak_intensity, bpIntD);
            if (TryParseDouble(tic, out double ticD)) spec.Params.Set(CVID.MS_total_ion_current, ticD);
            if (int.TryParse(peaks, NumberStyles.Integer, CultureInfo.InvariantCulture, out int peaksI))
                spec.DefaultArrayLength = peaksI;
        }

        // Scan window from the function's acquisition mass range.
        if (isMs)
        {
            var (lo, hi) = _data.GetAcquisitionMassRange(ie.Function);
            scan.ScanWindows.Add(new ScanWindow(lo, hi, CVID.MS_m_z));
        }

        // Precursor metadata for MS2 / MSn — pwiz C++ pulls SET_MASS for the isolation target
        // and COLLISION_ENERGY for the activation. We emit a single isolation window with
        // target = setMass (mid-range fallback when SET_MASS is missing).
        if (ie.MsLevel > 1 && isMs)
        {
            double collisionEnergy = 0;
            string ceStr = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.CollisionEnergy);
            if (TryParseDouble(ceStr, out double ce)) collisionEnergy = Math.Abs(ce);

            double setMass = 0;
            string setMassStr = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.SetMass);
            if (TryParseDouble(setMassStr, out double sm)) setMass = sm;

            var precursor = new Precursor();
            var (lo, hi) = _data.GetAcquisitionMassRange(ie.Function);
            if (setMass == 0)
            {
                setMass = (lo + hi) / 2.0;
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, hi - setMass, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, hi - setMass, CVID.MS_m_z);
            }
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, setMass, CVID.MS_m_z);

            precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation);
            if (collisionEnergy > 0)
                precursor.Activation.Set(CVID.MS_collision_energy, collisionEnergy, CVID.UO_electronvolt);

            precursor.SelectedIons.Add(new SelectedIon(setMass));
            spec.Precursors.Add(precursor);
        }

        if (getBinaryData && isMs)
        {
            var (mz, intensity) = _data.ReadScan(ie.Function, ie.Scan);
            spec.DefaultArrayLength = mz.Length;
            // SDK returns float arrays; widen to double on copy and tag the unit.
            var mzD = new double[mz.Length];
            var intD = new double[intensity.Length];
            for (int i = 0; i < mz.Length; i++) { mzD[i] = mz[i]; intD[i] = intensity[i]; }
            spec.SetMZIntensityArrays(mzD, intD, CVID.MS_number_of_detector_counts);
        }

        return spec;
    }

    private static bool TryParseDouble(string s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_owns) _data.Dispose();
    }
}
