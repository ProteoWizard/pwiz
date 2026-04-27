using System.Globalization;
using Pwiz.Analysis;
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
public sealed class SpectrumList_Waters : SpectrumListBase, IDisposable, IVendorCentroidingSpectrumList
{
    private readonly WatersRawFile _data;
    private readonly bool _owns;
    private readonly int _preferOnlyMsLevel;
    private readonly bool _srmAsSpectra;
    private readonly bool _ddaProcessing;
    private readonly List<IndexEntry> _index = new();
    private readonly Dictionary<string, int> _idToIndex = new(StringComparer.Ordinal);
    private readonly Lazy<(float Lower, float Upper)?> _ddaIsolationOffsets;

    /// <summary>DataProcessing emitted as the <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int Function;
        public int Scan;        // 0-based scan within function (or starting scan when DDA-merged)
        public int MsLevel;
        public CVID SpectrumType;
        // DDA-only: when DDA is true, this entry was produced by the MassLynx DDA processor.
        // The id and isolation-window emission take a different path; the underlying scan-stat
        // calls don't apply.
        public bool Dda;
        public int DdaIndex;
        public int DdaStartScan;
        public int DdaEndScan;
        public float DdaSetMass;
        public float DdaPrecursorMass;
        public bool DdaIsMs1;
    }

    internal SpectrumList_Waters(WatersRawFile data, bool owns, int preferOnlyMsLevel,
        bool srmAsSpectra, bool ddaProcessing = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _owns = owns;
        _preferOnlyMsLevel = preferOnlyMsLevel;
        _srmAsSpectra = srmAsSpectra;
        _ddaProcessing = ddaProcessing;
        _ddaIsolationOffsets = new Lazy<(float, float)?>(_data.GetDdaIsolationWindowOffsets);
        if (ddaProcessing)
            BuildDdaIndex();
        else
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

    private void BuildDdaIndex()
    {
        // pwiz C++ createDDAIndex iterates GetDDAScanCount entries; each one has its own
        // (function, startScan, endScan) plus precursor info. ID format depends on whether
        // it's a single scan or a merged range:
        //   start==end → "function=F process=0 scan=S+1"
        //   else       → "merged=N function=F process=0 scans=S+1-E+1"
        int n = _data.GetDdaScanCount();
        for (int i = 0; i < n; i++)
        {
            var info = _data.GetDdaScanInfo(i);
            int msLevel = info.IsMs1 ? 1 : 2;
            CVID specType = info.IsMs1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum;
            string id = info.StartScan == info.EndScan
                ? "function=" + (info.Function + 1).ToString(CultureInfo.InvariantCulture)
                  + " process=0 scan=" + (info.StartScan + 1).ToString(CultureInfo.InvariantCulture)
                : "merged=" + i.ToString(CultureInfo.InvariantCulture)
                  + " function=" + (info.Function + 1).ToString(CultureInfo.InvariantCulture)
                  + " process=0 scans=" + (info.StartScan + 1).ToString(CultureInfo.InvariantCulture)
                  + "-" + (info.EndScan + 1).ToString(CultureInfo.InvariantCulture);

            var entry = new IndexEntry
            {
                Index = i,
                Id = id,
                Function = info.Function,
                Scan = info.StartScan,
                MsLevel = msLevel,
                SpectrumType = specType,
                Dda = true,
                DdaIndex = i,
                DdaStartScan = info.StartScan,
                DdaEndScan = info.EndScan,
                DdaSetMass = info.SetMass,
                DdaPrecursorMass = info.PrecursorMass,
                DdaIsMs1 = info.IsMs1,
            };
            _index.Add(entry);
            _idToIndex[id] = i;
        }
    }

    /// <inheritdoc/>
    public string VendorCentroidName => "Waters/MassLynx peak picking";

    /// <inheritdoc/>
    public Spectrum GetCentroidSpectrum(int index, bool getBinaryData) =>
        GetSpectrumImpl(index, getBinaryData, doCentroid: true);

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        GetSpectrumImpl(index, getBinaryData, doCentroid: false);

    private Spectrum GetSpectrumImpl(int index, bool getBinaryData, bool doCentroid)
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
        // pwiz C++ always tags the *source* spectrum type up front (so SpectrumList_PeakPicker
        // can decide whether to re-pick) and then overrides to centroid when doCentroid is on
        // and we actually centroided. Mirror that order.
        spec.Params.Set(isProfile ? CVID.MS_profile_spectrum : CVID.MS_centroid_spectrum);
        bool willCentroid = doCentroid && isProfile;
        if (willCentroid)
        {
            // Replace MS_profile_spectrum with MS_centroid_spectrum.
            for (int i = spec.Params.CVParams.Count - 1; i >= 0; i--)
                if (spec.Params.CVParams[i].Cvid == CVID.MS_profile_spectrum) spec.Params.CVParams.RemoveAt(i);
            spec.Params.Set(CVID.MS_centroid_spectrum);
        }

        // Base peak / TIC / peak count come from per-scan stats (not from the binary data),
        // matching pwiz C++ which fills these from MassLynxScanItem entries directly. Reading
        // a single item via the parameters round-trip (createParameters → getScanItemValue →
        // getParameterValue) is the same pattern pwiz C++ wraps in GetScanStat.
        // When we actually centroid (willCentroid==true) we instead recompute from the peaks
        // via calculatePeakMetadata (matches pwiz C++).
        if (isMs && !willCentroid)
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
        // target = setMass (mid-range fallback when SET_MASS is missing). DDA-processed scans
        // carry the precursor info from the DDA processor (SET_MASS in the index, plus per-file
        // isolation window offsets); the selected ion's m/z is the precursor (monoisotopic)
        // mass when set, distinct from the isolation-window target.
        if (ie.MsLevel > 1 && isMs)
        {
            double collisionEnergy = 0;
            string ceStr = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.CollisionEnergy);
            if (TryParseDouble(ceStr, out double ce)) collisionEnergy = Math.Abs(ce);

            double setMass;
            double precursorMass;
            (float Lower, float Upper)? offsets = null;
            if (ie.Dda)
            {
                setMass = ie.DdaSetMass;
                precursorMass = ie.DdaPrecursorMass;
                offsets = _ddaIsolationOffsets.Value;
            }
            else
            {
                string setMassStr = _data.GetScanItem(ie.Function, ie.Scan, WatersScanItem.SetMass);
                setMass = TryParseDouble(setMassStr, out double sm) ? sm : 0;
                precursorMass = setMass;
            }

            var precursor = new Precursor();
            var (lo, hi) = _data.GetAcquisitionMassRange(ie.Function);
            if (offsets is { } o)
            {
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, o.Lower, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, o.Upper, CVID.MS_m_z);
            }
            else if (setMass == 0)
            {
                setMass = (lo + hi) / 2.0;
                precursorMass = setMass;
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, hi - setMass, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, hi - setMass, CVID.MS_m_z);
            }
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, setMass, CVID.MS_m_z);

            precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation);
            if (collisionEnergy > 0)
                precursor.Activation.Set(CVID.MS_collision_energy, collisionEnergy, CVID.UO_electronvolt);

            precursor.SelectedIons.Add(new SelectedIon(precursorMass));
            spec.Precursors.Add(precursor);
        }

        // Read peaks. Centroid request goes through MassLynx ScanProcessor (or, for DDA, the
        // DDA processor's centroid path). Otherwise we read the raw scan as-is. We always read
        // peaks when we centroid (even on metadata-only requests) because base peak / TIC /
        // m/z range have to be recomputed from them.
        if (isMs && (getBinaryData || willCentroid))
        {
            float[] mz, intensity;
            if (ie.Dda)
                (mz, intensity) = _data.GetDdaScan(ie.DdaIndex, willCentroid);
            else if (willCentroid)
                (mz, intensity) = _data.ReadCentroidScan(ie.Function, ie.Scan);
            else
                (mz, intensity) = _data.ReadScan(ie.Function, ie.Scan);
            spec.DefaultArrayLength = mz.Length;

            if (willCentroid)
                CalculatePeakMetadata(spec, mz, intensity);

            if (getBinaryData)
            {
                var mzD = new double[mz.Length];
                var intD = new double[intensity.Length];
                for (int i = 0; i < mz.Length; i++) { mzD[i] = mz[i]; intD[i] = intensity[i]; }
                spec.SetMZIntensityArrays(mzD, intD, CVID.MS_number_of_detector_counts);
            }
        }

        return spec;
    }

    /// <summary>
    /// Recomputes base peak / lowest+highest m/z / TIC from a centroided peak list. Mirrors
    /// pwiz C++ <c>SpectrumList_MetadataFixer::calculatePeakMetadata</c> with single-pass O(n)
    /// folding (the ref expects sample max-by-intensity tie-break ordering on equal intensities,
    /// matching <c>std::max_element</c> over the peaks; we keep the first occurrence too).
    /// </summary>
    private static void CalculatePeakMetadata(Spectrum spec, float[] mz, float[] intensity)
    {
        if (mz.Length == 0)
        {
            spec.Params.Set(CVID.MS_total_ion_current, 0.0, CVID.MS_number_of_detector_counts);
            return;
        }
        double total = 0;
        double basePeakX = mz[0];
        double basePeakY = intensity[0];
        double lowestX = mz[0];
        double highestX = mz[0];
        for (int i = 0; i < mz.Length; i++)
        {
            total += intensity[i];
            if (intensity[i] > basePeakY) { basePeakY = intensity[i]; basePeakX = mz[i]; }
            if (mz[i] < lowestX) lowestX = mz[i];
            if (mz[i] > highestX) highestX = mz[i];
        }
        spec.Params.Set(CVID.MS_base_peak_intensity, basePeakY, CVID.MS_number_of_detector_counts);
        spec.Params.Set(CVID.MS_base_peak_m_z, basePeakX, CVID.MS_m_z);
        spec.Params.Set(CVID.MS_lowest_observed_m_z, lowestX, CVID.MS_m_z);
        spec.Params.Set(CVID.MS_highest_observed_m_z, highestX, CVID.MS_m_z);
        spec.Params.Set(CVID.MS_total_ion_current, total, CVID.MS_number_of_detector_counts);
    }

    private static bool TryParseDouble(string s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_owns) _data.Dispose();
    }
}
