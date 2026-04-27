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
    private readonly bool _combineIonMobilitySpectra;
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
        // IMS-only: when Block >= 0 the entry refers to an IMS frame (block = original RT
        // index in the function). For non-combine, Scan holds the drift bin (0-based);
        // for combine, Scan is the mid-bin used for default drift time.
        public int Block = -1;
        public bool Combined;
    }

    internal SpectrumList_Waters(WatersRawFile data, bool owns, int preferOnlyMsLevel,
        bool srmAsSpectra, bool ddaProcessing = false, bool combineIonMobilitySpectra = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _owns = owns;
        _preferOnlyMsLevel = preferOnlyMsLevel;
        _srmAsSpectra = srmAsSpectra;
        _ddaProcessing = ddaProcessing;
        _combineIonMobilitySpectra = combineIonMobilitySpectra;
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
        // t=0.05min. For non-IMS functions, "scan" is the RT-axis position. For IMS functions,
        // we still order *blocks* (RT-axis) by RT, but each block expands to drift-bin entries
        // (non-combine) or one combined entry (combine). The expansion happens after the sort
        // so all bins of one block stay adjacent.
        var byRt = new List<(float Rt, int Function, int Scan, int MsLevel, CVID Type, bool Ims)>();
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

            bool ims = function < _data.IonMobilityByFunctionIndex.Count
                && _data.IonMobilityByFunctionIndex[function];

            int scanCount = _data.GetScanCount(function);
            for (int s = 0; s < scanCount; s++)
            {
                float rt = _data.GetRetentionTime(function, s);
                byRt.Add((rt, function, s, msLevel, spectrumType, ims));
            }
        }

        // Stable sort by retention time.
        var sorted = byRt.OrderBy(t => t.Rt).ToList();

        foreach (var e in sorted)
        {
            if (!e.Ims)
            {
                int i = _index.Count;
                string id = "function=" + (e.Function + 1).ToString(CultureInfo.InvariantCulture)
                    + " process=0 scan=" + (e.Scan + 1).ToString(CultureInfo.InvariantCulture);
                var entry = new IndexEntry
                {
                    Index = i, Id = id,
                    Function = e.Function, Scan = e.Scan, Block = -1,
                    MsLevel = e.MsLevel, SpectrumType = e.Type,
                };
                _index.Add(entry);
                _idToIndex[id] = i;
                continue;
            }

            // IMS function. Block = the RT-axis index (e.Scan). Each block has
            // numScansInBlock drift bins.
            int numScansInBlock = _data.GetDriftScanCount(e.Function);

            if (_combineIonMobilitySpectra)
            {
                int i = _index.Count;
                // pwiz C++ id format for combine-IMS: "merged=N function=F block=B+1".
                string id = "merged=" + (i + 1).ToString(CultureInfo.InvariantCulture)
                    + " function=" + (e.Function + 1).ToString(CultureInfo.InvariantCulture)
                    + " block=" + (e.Scan + 1).ToString(CultureInfo.InvariantCulture);
                var entry = new IndexEntry
                {
                    Index = i, Id = id,
                    Function = e.Function, Block = e.Scan,
                    Scan = numScansInBlock / 2, // mid-bin for default drift-time
                    MsLevel = e.MsLevel, SpectrumType = e.Type,
                    Combined = true,
                };
                _index.Add(entry);
                _idToIndex[id] = i;
            }
            else
            {
                // Non-combine: one entry per (block, drift bin). pwiz C++ id is the linear
                // scan number = numScansInBlock * block + driftBin + 1.
                for (int j = 0; j < numScansInBlock; j++)
                {
                    int i = _index.Count;
                    int linearScan = numScansInBlock * e.Scan + j + 1;
                    string id = "function=" + (e.Function + 1).ToString(CultureInfo.InvariantCulture)
                        + " process=0 scan=" + linearScan.ToString(CultureInfo.InvariantCulture);
                    var entry = new IndexEntry
                    {
                        Index = i, Id = id,
                        Function = e.Function, Block = e.Scan, Scan = j,
                        MsLevel = e.MsLevel, SpectrumType = e.Type,
                    };
                    _index.Add(entry);
                    _idToIndex[id] = i;
                }
            }
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

        // MSe heuristic (mirrors pwiz C++): if this MS1 spectrum is from function 1 and has a
        // non-zero collision energy, promote it to MSn. The cpp code has a FIXME-disabled
        // comparison against function 0's collision energy; we keep the same behavior so the
        // reference mzMLs for HDMSe match (where every function-1 spectrum becomes pseudo-MS2).
        int msLevel = ie.MsLevel;
        CVID spectrumType = ie.SpectrumType;
        double promotedCe = 0;
        if (msLevel == 1 && ie.Function == 1 && ie.SpectrumType == CVID.MS_MS1_spectrum)
        {
            int mseStatIndex = ie.Block >= 0 ? ie.Block : ie.Scan;
            string ceStr = _data.GetScanItem(ie.Function, mseStatIndex, WatersScanItem.CollisionEnergy);
            if (TryParseDouble(ceStr, out double ce) && Math.Abs(ce) > 0)
            {
                msLevel = 2;
                spectrumType = CVID.MS_MSn_spectrum;
                promotedCe = Math.Abs(ce);
            }
        }

        spec.Params.Set(spectrumType);
        bool isMs = spectrumType == CVID.MS_MS1_spectrum || spectrumType == CVID.MS_MSn_spectrum
            || spectrumType == CVID.MS_SIM_spectrum || spectrumType == CVID.MS_SRM_spectrum
            || spectrumType == CVID.MS_constant_neutral_loss_spectrum
            || spectrumType == CVID.MS_constant_neutral_gain_spectrum;
        if (isMs) spec.Params.Set(CVID.MS_ms_level, msLevel);

        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan();
        scan.Set(CVID.MS_preset_scan_configuration, ie.Function + 1);
        spec.ScanList.Scans.Add(scan);

        // IMS scans use the block index for RT/scan-stat lookups; non-IMS scans use Scan.
        int statIndex = ie.Block >= 0 ? ie.Block : ie.Scan;
        double scanTimeMin = _data.GetRetentionTime(ie.Function, statIndex);
        scan.Set(CVID.MS_scan_start_time, scanTimeMin, CVID.UO_minute);

        // IMS scans record drift time on the scan element. Non-combine: per-bin drift time.
        // Combine: drift time of the mid-bin (numScansInBlock/2) — pwiz C++ uses ie.scan
        // which BuildIndex sets to numScansInBlock/2 for the combined entry.
        if (ie.Block >= 0)
        {
            double driftTime = _data.GetDriftTime(ie.Scan);
            scan.Set(CVID.MS_ion_mobility_drift_time, driftTime, CVID.UO_millisecond);
        }

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
        // IMS non-combine spectra get only TIC from the per-block array (the scan-stats only
        // describe the block, not individual drift bins).
        if (isMs && !willCentroid)
        {
            if (ie.Block < 0 || ie.Combined)
            {
                int s = ie.Block < 0 ? ie.Scan : ie.Block;
                string bpMass = _data.GetScanItem(ie.Function, s, WatersScanItem.BasePeakMass);
                string bpInt = _data.GetScanItem(ie.Function, s, WatersScanItem.BasePeakIntensity);
                string tic = _data.GetScanItem(ie.Function, s, WatersScanItem.TotalIonCurrent);
                string peaks = _data.GetScanItem(ie.Function, s, WatersScanItem.PeaksInScan);

                if (TryParseDouble(bpMass, out double bpMassD)) spec.Params.Set(CVID.MS_base_peak_m_z, bpMassD);
                if (TryParseDouble(bpInt, out double bpIntD)) spec.Params.Set(CVID.MS_base_peak_intensity, bpIntD);
                if (TryParseDouble(tic, out double ticD)) spec.Params.Set(CVID.MS_total_ion_current, ticD);
                if (int.TryParse(peaks, NumberStyles.Integer, CultureInfo.InvariantCulture, out int peaksI))
                    spec.DefaultArrayLength = peaksI;
            }
            else // non-combine IMS: per-block TIC, no base peak / peak count
            {
                if (ie.Function < _data.TicByFunctionIndex.Count
                    && _data.TicByFunctionIndex[ie.Function] is { } ticArr
                    && ie.Block < ticArr.Length)
                {
                    // Use the float overload — pwiz C++ stores TicByFunctionIndex as float<>
                    // and serializes with default 6-sig-fig precision; widening to double here
                    // would emit 7 digits and diff against the reference.
                    spec.Params.Set(CVID.MS_total_ion_current, ticArr[ie.Block]);
                }
                spec.DefaultArrayLength = 0;
            }
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
        if (msLevel > 1 && isMs)
        {
            // IMS spectra get scan stats from the block index, not the per-bin drift index.
            int scanStatIndex = ie.Block >= 0 ? ie.Block : ie.Scan;
            double collisionEnergy = promotedCe;
            if (collisionEnergy == 0)
            {
                string ceStr = _data.GetScanItem(ie.Function, scanStatIndex, WatersScanItem.CollisionEnergy);
                if (TryParseDouble(ceStr, out double ce)) collisionEnergy = Math.Abs(ce);
            }

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
                string setMassStr = _data.GetScanItem(ie.Function, scanStatIndex, WatersScanItem.SetMass);
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
        // DDA processor's centroid path). IMS uses readDriftScan for individual bins; combine
        // IMS aggregates across all bins. Otherwise we read the raw scan as-is.
        if (isMs && ie.Combined)
        {
            // Combine path: build (mz, intensity, drift_time) arrays across all bins of the
            // block, regardless of getBinaryData (DefaultArrayLength reflects the merged peak
            // count). We always read peaks here because the combined spectrum has no useful
            // scan-stat-driven shortcut.
            int numScansInBlock = _data.GetDriftScanCount(ie.Function);
            var mzList = new List<double>();
            var intList = new List<double>();
            var driftList = new List<double>();
            for (int s = 0; s < numScansInBlock; s++)
            {
                double driftTime = _data.GetDriftTime(s);
                var (binMz, binInt) = _data.ReadDriftScan(ie.Function, ie.Block, s);
                for (int i = 0; i < binMz.Length; i++)
                {
                    mzList.Add(binMz[i]);
                    intList.Add(binInt[i]);
                    driftList.Add(driftTime);
                }
            }
            spec.DefaultArrayLength = mzList.Count;

            if (getBinaryData)
            {
                spec.SetMZIntensityArrays(mzList, intList, CVID.MS_number_of_detector_counts);
                var driftArr = new BinaryDataArray();
                driftArr.Set(CVID.MS_raw_ion_mobility_array, "", CVID.UO_millisecond);
                driftArr.Data.AddRange(driftList);
                spec.BinaryDataArrays.Add(driftArr);
            }
        }
        else if (isMs && (getBinaryData || willCentroid))
        {
            float[] mz, intensity;
            if (ie.Dda)
                (mz, intensity) = _data.GetDdaScan(ie.DdaIndex, willCentroid);
            else if (ie.Block >= 0)
                (mz, intensity) = _data.ReadDriftScan(ie.Function, ie.Block, ie.Scan);
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
