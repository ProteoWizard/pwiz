using System.Globalization;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using ThermoFisher.CommonCore.Data.FilterEnums;
using ThermoFisher.CommonCore.Data.Interfaces;
using Scan = Pwiz.Data.MsData.Spectra.Scan;
using Precursor = Pwiz.Data.MsData.Spectra.Precursor;
using SelectedIon = Pwiz.Data.MsData.Spectra.SelectedIon;
using Activation = Pwiz.Data.MsData.Spectra.Activation;
using ScanWindow = Pwiz.Data.MsData.Spectra.ScanWindow;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Thermo;

/// <summary>
/// <see cref="ISpectrumList"/> backed by a Thermo <see cref="ThermoRawFile"/>.
/// </summary>
/// <remarks>
/// Port of pwiz::msdata::SpectrumList_Thermo. Mirrors the C++ structure so metadata parity
/// with the C++ msconvert output can be verified via msdiff. Current coverage: scan type,
/// ms level, polarity, profile/centroid, filter string, base peak / TIC, scan window,
/// precursor isolation window + selected ion + charge + activation, ion injection time,
/// mass resolving power, FAIMS CV, lowest/highest observed m/z, binary arrays.
/// </remarks>
public sealed class SpectrumList_Thermo : SpectrumListBase, IDisposable, IVendorCentroidingSpectrumList
{
    private readonly ThermoRawFile _raw;
    private readonly bool _ownsRaw;
    private readonly List<IndexEntry> _index = new();
    private readonly Dictionary<MassAnalyzerType, Pwiz.Data.MsData.Instruments.InstrumentConfiguration> _icByAnalyzer = new();
    private readonly Pwiz.Data.MsData.Instruments.InstrumentConfiguration? _defaultIc;
    private readonly Dictionary<string, int> _trailerIndexByLabel = new(StringComparer.Ordinal);

    // Small LRU-ish cache so an MS1 that is the precursor for many MS2s only gets peak-decoded once.
    // Keyed by (scan, preferCentroid) because the same scan is accessed both ways when the
    // harness asks for centroided output — profile peaks for the regular harness, centroided
    // peaks for the peakPicking-wrapped harness.
    private const int PrecursorCacheSize = 10;
    private readonly LinkedList<(int Scan, bool Centroid, double[] Mz, double[] Intensity)> _precursorCache = new();

    private (double[] Mz, double[] Intensity) GetCachedPeaks(int scanNumber, bool preferCentroid)
    {
        for (var node = _precursorCache.First; node is not null; node = node.Next)
        {
            if (node.Value.Scan == scanNumber && node.Value.Centroid == preferCentroid)
            {
                _precursorCache.Remove(node);
                _precursorCache.AddFirst(node);
                return (node.Value.Mz, node.Value.Intensity);
            }
        }
        var (mz, intensity) = _raw.GetPeaks(scanNumber, preferCentroid);
        _precursorCache.AddFirst((scanNumber, preferCentroid, mz, intensity));
        if (_precursorCache.Count > PrecursorCacheSize)
            _precursorCache.RemoveLast();
        return (mz, intensity);
    }

    private double SumIntensityInWindow(int scanNumber, double centerMz, double halfWidth, bool preferCentroid)
    {
        var (mz, intensity) = GetCachedPeaks(scanNumber, preferCentroid);
        if (mz.Length == 0) return 0;
        double lo = centerMz - halfWidth;
        double hi = centerMz + halfWidth;
        // Binary-search for the first m/z >= lo
        int idx = Array.BinarySearch(mz, lo);
        if (idx < 0) idx = ~idx;
        double sum = 0;
        for (int i = idx; i < mz.Length && mz[i] < hi; i++)
            sum += intensity[i];
        return sum;
    }

    private readonly bool _simAsSpectra;

    /// <summary>Creates a spectrum list over <paramref name="raw"/> with no IC binding.</summary>
    public SpectrumList_Thermo(ThermoRawFile raw, bool ownsRaw = true, bool simAsSpectra = false)
        : this(raw, ownsRaw, null, null, simAsSpectra) { }

    internal SpectrumList_Thermo(ThermoRawFile raw, bool ownsRaw,
        Pwiz.Data.MsData.Instruments.InstrumentConfiguration? defaultInstrumentConfiguration,
        IReadOnlyDictionary<MassAnalyzerType, Pwiz.Data.MsData.Instruments.InstrumentConfiguration>? icByAnalyzer,
        bool simAsSpectra = false)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
        _simAsSpectra = simAsSpectra;
        _defaultIc = defaultInstrumentConfiguration;
        if (icByAnalyzer is not null)
            foreach (var kv in icByAnalyzer) _icByAnalyzer[kv.Key] = kv.Value;
        try
        {
            var headers = raw.Raw.GetTrailerExtraHeaderInformation();
            for (int i = 0; i < headers.Length; i++)
                _trailerIndexByLabel[headers[i].Label] = i;
        }
        catch { /* some files may not expose trailer info */ }
        CreateIndex();
    }

    private bool TryGetTrailerValue(int scanNumber, string label, out object? value)
    {
        value = null;
        if (!_trailerIndexByLabel.TryGetValue(label, out int idx)) return false;
        try { value = _raw.Raw.GetTrailerExtraValue(scanNumber, idx); return value is not null; }
        catch { return false; }
    }

    private bool TryGetTrailerDouble(int scanNumber, string label, out double value)
    {
        value = 0;
        if (!TryGetTrailerValue(scanNumber, label, out var v) || v is null) return false;
        try { value = Convert.ToDouble(v, CultureInfo.InvariantCulture); return true; }
        catch { return false; }
    }

    private bool TryGetTrailerInt(int scanNumber, string label, out long value)
    {
        value = 0;
        if (!TryGetTrailerValue(scanNumber, label, out var v) || v is null) return false;
        try { value = Convert.ToInt64(v, CultureInfo.InvariantCulture); return true; }
        catch { return false; }
    }

    private bool TryGetTrailerString(int scanNumber, string label, out string value)
    {
        value = string.Empty;
        if (!TryGetTrailerValue(scanNumber, label, out var v) || v is null) return false;
        value = (v.ToString() ?? string.Empty).Trim();
        return value.Length > 0;
    }

    /// <summary>DataProcessing id emitted as the <c>defaultDataProcessingRef</c>. Set by <see cref="Reader_Thermo"/>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int Scan;
        public MSOrderType MsOrder;
        public MassAnalyzerType MassAnalyzer;
        public ScanModeType ScanMode;
        public PolarityType Polarity;
        public double IsolationMz;
    }

    public override int Count => _index.Count;

    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        for (int scan = _raw.FirstScan; scan <= _raw.LastScan; scan++)
        {
            var filter = _raw.Raw.GetFilterForScanNumber(scan);

            // SIM scans are emitted as chromatograms (grouped by Q1) unless simAsSpectra=true,
            // matching pwiz C++ ChromatogramList_Thermo.cpp:481-504.
            if (filter.ScanMode == ScanModeType.Sim && !_simAsSpectra)
                continue;

            var entry = new IndexEntry
            {
                Index = _index.Count,
                Id = ThermoRawFile.NativeId(scan),
                Scan = scan,
                MsOrder = filter.MSOrder,
                MassAnalyzer = filter.MassAnalyzer,
                ScanMode = filter.ScanMode,
                Polarity = filter.Polarity,
            };
            if (entry.MsOrder > MSOrderType.Ms && filter.MassCount > 0)
            {
                try { entry.IsolationMz = filter.GetMass(filter.MassCount - 1); }
                catch { entry.IsolationMz = 0; }
            }
            _index.Add(entry);
        }
    }

    /// <inheritdoc/>
    public string VendorCentroidName => "Thermo/Xcalibur peak picking";

    /// <inheritdoc/>
    public Spectrum GetCentroidSpectrum(int index, bool getBinaryData) =>
        GetSpectrumImpl(index, getBinaryData, preferCentroid: true);

    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        GetSpectrumImpl(index, getBinaryData, preferCentroid: false);

    private Spectrum GetSpectrumImpl(int index, bool getBinaryData, bool preferCentroid)
    {
        var ie = _index[index];
        int scanNumber = ie.Scan;
        var raw = _raw.Raw;

        var spec = new Spectrum
        {
            Index = index,
            Id = ie.Id,
        };

        // ---- scan type & ms level (mirrors C++ switch on scanType/msOrder) ----
        int msLevel = MsOrderToLevel(ie.MsOrder);
        switch (ie.ScanMode)
        {
            case ScanModeType.Sim:
                spec.Params.Set(CVID.MS_SIM_spectrum);
                break;
            case ScanModeType.Srm:
                spec.Params.Set(CVID.MS_SRM_spectrum);
                break;
            default:
                switch (ie.MsOrder)
                {
                    case MSOrderType.Nl:  spec.Params.Set(CVID.MS_constant_neutral_loss_spectrum); msLevel = 2; break;
                    case MSOrderType.Ng:  spec.Params.Set(CVID.MS_constant_neutral_gain_spectrum); msLevel = 2; break;
                    case MSOrderType.Par: spec.Params.Set(CVID.MS_precursor_ion_spectrum); msLevel = 2; break;
                    case MSOrderType.Ms:  spec.Params.Set(CVID.MS_MS1_spectrum); break;
                    default:              spec.Params.Set(CVID.MS_MSn_spectrum); break;
                }
                break;
        }
        spec.Params.Set(CVID.MS_ms_level, msLevel);

        // Zoom scans (narrow m/z window) and instruments flagged as "enhanced resolution" get
        // tagged with MS_enhanced_resolution_scan, matching pwiz C++ SpectrumList_Thermo.cpp:359.
        var rawFilter = _raw.Raw.GetFilterForScanNumber(scanNumber);
        if (ie.ScanMode == ScanModeType.Zoom || rawFilter.Enhanced == TriState.On)
            spec.Params.Set(CVID.MS_enhanced_resolution_scan);

        // ---- scan list ----
        var scan = new Scan();
        // Always set the analyzer-specific IC (falling back to the document default). The
        // MzmlWriter suppresses the redundant instrumentConfigurationRef attribute when it
        // equals the run default, while MzmlReader resolves an omitted ref back to the
        // default — so both in-memory and serialized forms stay consistent.
        scan.InstrumentConfiguration =
            _icByAnalyzer.TryGetValue(ie.MassAnalyzer, out var icForAnalyzer)
                ? icForAnalyzer
                : _defaultIc;
        double rtMin = _raw.RetentionTimeMinutes(scanNumber);
        scan.Set(CVID.MS_scan_start_time, rtMin, CVID.UO_minute);

        // Match pwiz C++ SpectrumList_Thermo cvParam order within the scan element:
        //   mass resolving power, filter string, preset scan configuration, ion injection time.

        long resolvingPower = 0;
        if (TryGetTrailerInt(scanNumber, "Orbitrap Resolution:", out long rp1) && rp1 > 0)
            resolvingPower = rp1;
        else if (TryGetTrailerInt(scanNumber, "FT Resolution:", out long rp2) && rp2 > 0)
            resolvingPower = rp2;
        if (resolvingPower > 0)
            scan.Set(CVID.MS_mass_resolving_power, resolvingPower);

        string filterString = raw.GetFilterForScanNumber(scanNumber)?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(filterString))
            scan.Set(CVID.MS_filter_string, filterString);

        if (TryGetTrailerInt(scanNumber, "Scan Event:", out long scanEvent) && scanEvent > 0)
            scan.Set(CVID.MS_preset_scan_configuration, scanEvent);

        // Scan Description (e.g. "sps" for SPS-MS3 scans) — emitted as a spectrum-level userParam
        // on the outer Spectrum, matching pwiz C++.
        if (TryGetTrailerString(scanNumber, "Scan Description:", out string scanDesc))
            spec.Params.UserParams.Add(new UserParam("scan description", scanDesc, "xsd:string"));

        if (msLevel > 1 && TryGetTrailerDouble(scanNumber, "Monoisotopic M/Z:", out double monoMz))
        {
            // Matches pwiz C++: lexical_cast<string>(double) — no trailing ".0" for integer values.
            scan.UserParams.Add(new UserParam(
                "[Thermo Trailer Extra]Monoisotopic M/Z:",
                monoMz.ToString("G", CultureInfo.InvariantCulture),
                "xsd:float"));
        }

        if (TryGetTrailerDouble(scanNumber, "Ion Injection Time (ms):", out double injMs))
            scan.Set(CVID.MS_ion_injection_time, injMs, CVID.UO_millisecond);

        // Source-induced CID offset voltage. CommonCore's IScanFilter doesn't expose
        // sourceOffsetVoltage() directly like the old XRawfile COM API, but the filter
        // string embeds it as "sid=N.NN" — e.g. "NSI sid=10.00 t Full ms2 ...".
        if (TryParseSid(filterString, out double sid) && sid != 0)
            scan.Set(CVID.MS_offset_voltage, sid, CVID.UO_volt);

        // ---- polarity ----
        if (ie.Polarity == PolarityType.Positive)
            spec.Params.Set(CVID.MS_positive_scan);
        else if (ie.Polarity == PolarityType.Negative)
            spec.Params.Set(CVID.MS_negative_scan);

        // ---- profile / centroid flag ----
        // Honors preferCentroid: when the caller (e.g. SpectrumList_PeakPicker in vendor-prefer
        // mode) asks for centroided data, emit MS_centroid_spectrum regardless of analyzer —
        // ThermoRawFile.GetPeaks uses Scan.ToCentroid for non-FTMS profile scans, so the
        // returned arrays are genuinely centroided.
        var filter = raw.GetFilterForScanNumber(scanNumber);
        bool scanIsProfile = filter.ScanData == ScanDataType.Profile;
        bool emitCentroid = !scanIsProfile || preferCentroid;
        spec.Params.Set(emitCentroid ? CVID.MS_centroid_spectrum : CVID.MS_profile_spectrum);

        // ---- scan stats (base peak, TIC) ----
        try
        {
            var stats = raw.GetScanStatsForScanNumber(scanNumber);
            if (stats.BasePeakMass > 0)
            {
                spec.Params.Set(CVID.MS_base_peak_m_z, stats.BasePeakMass, CVID.MS_m_z);
                spec.Params.Set(CVID.MS_base_peak_intensity, stats.BasePeakIntensity, CVID.MS_number_of_detector_counts);
            }
            spec.Params.Set(CVID.MS_total_ion_current, stats.TIC);

            double low = filter.GetMassRange(0).Low;
            double high = filter.GetMassRange(filter.MassRangeCount - 1).High;
            scan.ScanWindows.Add(new ScanWindow(low, high, CVID.MS_m_z));
        }
        catch { /* ignore — a subset of scans might not expose stats */ }

        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        // ---- precursor (MS2+) ----
        if (msLevel > 1 && ie.MsOrder != MSOrderType.Par)
        {
            // MSX / SPS take the multi-precursor path (all precursors at msLevel-1). Everything
            // else uses the per-level path that emits one precursor per filter mass with
            // spectrumRef / peak_intensity / isolation-width fallback.
            bool isMsx = filter.Multiplex == TriState.On;
            bool isSps = ReadSpsMasses(ie.Scan).Count > 1;
            bool isBigMassCount = filter.MassCount > msLevel - 1;
            if (filter.MassCount > 1 && (isMsx || isSps || isBigMassCount))
                PopulateMultiPrecursor(spec, filter, ie, msLevel);
            else
                PopulatePrecursor(spec, filter, ie, preferCentroid);
        }

        // ---- binary data ----
        // Always retrieve the mass list so defaultArrayLength + lowest/highest observed m/z match
        // the data even when getBinaryData is false. C++ does the same (see SpectrumList_Thermo.cpp).
        var (mz, intensity) = _raw.GetPeaks(scanNumber, preferCentroid);
        spec.DefaultArrayLength = mz.Length;
        if (mz.Length > 0)
        {
            spec.Params.Set(CVID.MS_lowest_observed_m_z, mz[0], CVID.MS_m_z);
            spec.Params.Set(CVID.MS_highest_observed_m_z, mz[^1], CVID.MS_m_z);
        }
        // pwiz C++ always attaches the (possibly empty) m/z + intensity arrays when binary
        // data is requested — so empty spectra still emit two zero-length binaryDataArray
        // elements rather than an empty binaryDataArrayList.
        if (getBinaryData)
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);

        return spec;
    }

    /// <summary>
    /// True when the scan has multiple precursors at the same MS level (MSX) or multiple
    /// precursors selected for a single MSn (SPS). Standard nested MSn
    /// (<c>MassCount == msLevel - 1</c>) returns false.
    /// </summary>
    /// <remarks>
    /// Must check <c>== TriState.On</c> rather than <c>!= TriState.Off</c>: CommonCore's
    /// <c>IScanFilter</c> returns <c>TriState.Any</c> (not Off) when the flag is simply
    /// unset — so the "Any" default would otherwise misroute every nested MSn into the
    /// multi-precursor branch and drop the spectrumRef / peak-intensity fields.
    /// </remarks>
    private static bool HasMultiplePrecursors(IScanFilter filter, int msLevel) =>
        filter.Multiplex == TriState.On
        || filter.MultiNotch == TriState.On
        || filter.MassCount > msLevel - 1;

    private void PopulateMultiPrecursor(Spectrum spec, IScanFilter filter, IndexEntry ie, int msLevel)
    {
        // Parity with pwiz C++ RawFile.cpp parseFilterString + SPS trailer append:
        //   - filter masses i < msLevel-1 are nested precursors at ms level i+1
        //   - additional filter masses (MSX) are all at ms level msLevel-1
        //   - SPS: extra masses come from trailer "SPS Masses:" + "SPS Masses Continued:",
        //     all at ms level msLevel-1, skipping the first (duplicate of last filter mass)
        int filterCount = filter.MassCount;
        var entries = new List<(double Mass, int Level, double HalfWidth, ActivationType Act, double Energy)>();
        for (int i = 0; i < filterCount; i++)
        {
            int lvl = i < msLevel - 1 ? i + 1 : msLevel - 1;
            double hw = 0;
            try { hw = filter.GetIsolationWidth(i) / 2.0; } catch { }
            entries.Add((filter.GetMass(i), lvl, hw, filter.GetActivation(i), filter.GetEnergy(i)));
        }

        // SPS detection: CommonCore's filter.MultiNotch returns TriState.Any rather than On,
        // so follow pwiz C++ which also falls back to the trailer — SPS is whenever non-empty
        // "SPS Masses:" trailer exists.
        var spsMasses = entries.Count > 0 ? ReadSpsMasses(ie.Scan) : new List<double>();
        bool isSps = spsMasses.Count > 0;
        if (isSps)
        {
            if (spsMasses.Count > 1)
            {
                var last = entries[^1]; // inherit isolation width, activation, energy from last filter mass
                for (int i = 1; i < spsMasses.Count; i++)
                    entries.Add((spsMasses[i], msLevel - 1, last.HalfWidth, last.Act, last.Energy));
            }

            // For SPS, trailer "MS<n-1> Isolation Width:" overrides the API isolation width
            // when larger (pwiz C++ comment: "API one isn't always accurate for some reason").
            string widthTag = "MS" + (msLevel - 1).ToString(CultureInfo.InvariantCulture) + " Isolation Width:";
            if (TryGetTrailerDouble(ie.Scan, widthTag, out double trailerWidth) && trailerWidth > 0)
            {
                double trailerHalf = trailerWidth / 2.0;
                for (int k = 0; k < entries.Count; k++)
                    if (entries[k].HalfWidth < trailerHalf)
                    {
                        var e = entries[k];
                        entries[k] = (e.Mass, e.Level, trailerHalf, e.Act, e.Energy);
                    }
            }
        }

        // Emit in reverse so the highest ms level (innermost, closest to fragment scan) comes first.
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];

            var precursor = new Precursor();
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, e.Mass, CVID.MS_m_z);
            if (e.HalfWidth > 0)
            {
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, e.HalfWidth, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, e.HalfWidth, CVID.MS_m_z);
            }
            precursor.IsolationWindow.UserParams.Add(new UserParam(
                "ms level", e.Level.ToString(CultureInfo.InvariantCulture)));

            var selectedIon = new SelectedIon();
            selectedIon.Set(CVID.MS_selected_ion_m_z, e.Mass, CVID.MS_m_z);
            precursor.SelectedIons.Add(selectedIon);

            SetActivationCv(precursor.Activation, e.Act);
            if (e.Energy > 0 && (e.Act == ActivationType.CollisionInducedDissociation
                                 || e.Act == ActivationType.HigherEnergyCollisionalDissociation))
                precursor.Activation.Set(CVID.MS_collision_energy, e.Energy, CVID.UO_electronvolt);

            spec.Precursors.Add(precursor);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex SidRegex =
        new(@"\bsid=([\-+]?\d+(?:\.\d+)?)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool TryParseSid(string filter, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(filter)) return false;
        var m = SidRegex.Match(filter);
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private List<double> ReadSpsMasses(int scanNumber)
    {
        var result = new List<double>();
        if (!TryGetTrailerString(scanNumber, "SPS Masses:", out string s)) return result;
        if (TryGetTrailerString(scanNumber, "SPS Masses Continued:", out string s2))
            s = s + "," + s2;
        foreach (var token in s.Split(','))
        {
            var t = token.Trim();
            if (t.Length == 0) continue;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                result.Add(v);
        }
        return result;
    }

    private static void SetActivationCv(Activation activation, ActivationType activationType)
    {
        switch (activationType)
        {
            case ActivationType.HigherEnergyCollisionalDissociation:
                activation.Set(CVID.MS_beam_type_collision_induced_dissociation); break;
            case ActivationType.CollisionInducedDissociation:
                activation.Set(CVID.MS_collision_induced_dissociation); break;
            case ActivationType.ElectronTransferDissociation:
                activation.Set(CVID.MS_electron_transfer_dissociation); break;
            case ActivationType.ElectronCaptureDissociation:
                activation.Set(CVID.MS_electron_capture_dissociation); break;
            case ActivationType.PQD:
                activation.Set(CVID.MS_pulsed_q_dissociation); break;
            case ActivationType.MultiPhotonDissociation:
                activation.Set(CVID.MS_photodissociation); break;
            default:
                activation.Set(CVID.MS_collision_induced_dissociation); break;
        }
    }

    private void PopulatePrecursor(Spectrum spec, IScanFilter filter, IndexEntry ie, bool preferCentroid)
    {
        // pwiz C++ iterates over filter masses in reverse (innermost first). Each filter mass
        // at index i maps to a precursor at ms level i+1; the innermost (index MassCount-1)
        // gets the full treatment (trailer-width override, spectrumRef lookup by scan-range,
        // peak_intensity, monoisotope adjustment). Outer precursors get a simpler emission.
        int msLevel = MsOrderToLevel(ie.MsOrder);
        int massCount = filter.MassCount;
        if (massCount == 0) return;

        for (int i = massCount - 1; i >= 0; i--)
        {
            int precursorMsLevel = i + 1;
            bool isPrimary = precursorMsLevel == msLevel - 1;
            double isolationMz = 0;
            try { isolationMz = filter.GetMass(i); } catch { }
            if (isolationMz <= 0) continue;

            // Mirror pwiz C++ SpectrumList_Thermo.cpp:552-568 isolation-width logic exactly.
            // trailerExtraValueDouble returns 0 (not throw) when the trailer is missing, so the
            // override is UNCONDITIONAL for primary precursors — an absent trailer zeros out
            // the filter-based width, then the method fallback takes over. This matters for
            // newer DDA files (e.g. TMT MS3) where no "MS{n} Isolation Width:" trailer exists
            // and cpp emits no offsets because the method also has no width for that event.
            double isolationHalfWidth = 0;
            try { isolationHalfWidth = filter.GetIsolationWidth(i) / 2.0; } catch { }

            if (isPrimary)
            {
                string widthTag = "MS" + msLevel.ToString(CultureInfo.InvariantCulture) + " Isolation Width:";
                TryGetTrailerDouble(ie.Scan, widthTag, out double trailerWidth);
                isolationHalfWidth = trailerWidth / 2.0;  // unconditional override, matches cpp
            }

            // Method fallback when the above resolved to 0 (LTQ-class where filter returns 0
            // or trailer absent). Matches pwiz C++ line 563-568.
            if (isolationHalfWidth == 0)
            {
                var (segNum, evtNum) = _raw.GetScanSegmentAndEvent(ie.Scan);
                double methodWidth = isPrimary
                    ? _raw.GetMethodIsolationWidth(segNum, evtNum)
                    : 0;
                if (methodWidth == 0)
                    methodWidth = _raw.GetMethodDefaultIsolationWidth(segNum, isPrimary ? msLevel : precursorMsLevel);
                if (methodWidth > 0) isolationHalfWidth = methodWidth / 2.0;
            }

            var precursor = new Precursor();
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isolationMz, CVID.MS_m_z);
            if (isolationHalfWidth > 0)
            {
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, isolationHalfWidth, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, isolationHalfWidth, CVID.MS_m_z);
            }
            precursor.IsolationWindow.UserParams.Add(new UserParam(
                "ms level", precursorMsLevel.ToString(CultureInfo.InvariantCulture)));

            // ---- selected ion (m/z + charge) ----
            var selectedIon = new SelectedIon();
            double selectedIonMz = isolationMz;

            // Charge state applies to all precursor levels (cpp reads the same trailer for
            // primary and outer precursors — SpectrumList_Thermo.cpp:718-722).
            int precursorCharge = 0;
            if (TryGetTrailerInt(ie.Scan, "Charge State:", out long cs) && cs > 0)
                precursorCharge = (int)cs;
            if (isPrimary && TryGetTrailerDouble(ie.Scan, "Monoisotopic M/Z:", out double mono) && mono > 0)
            {
                // Reject when outside isolation window — guards against a known Thermo firmware
                // bug where the Monoisotopic trailer can report a reference mass well outside
                // the actual isolation. Matches pwiz C++ SpectrumList_Thermo.cpp 617-623.
                const double defaultLowerOffset = 1.5;
                const double defaultUpperOffset = 2.5;
                double lo, hi;
                if (isolationHalfWidth <= 2.0)
                {
                    lo = isolationMz - defaultLowerOffset * 2;
                    hi = isolationMz + defaultUpperOffset;
                }
                else
                {
                    lo = isolationMz - isolationHalfWidth;
                    hi = isolationMz + isolationHalfWidth;
                }
                if (mono >= lo && mono <= hi)
                    selectedIonMz = mono;
            }

            if (selectedIonMz > 0)
                selectedIon.Set(CVID.MS_selected_ion_m_z, selectedIonMz, CVID.MS_m_z);
            if (precursorCharge > 0)
                selectedIon.Set(CVID.MS_charge_state, precursorCharge);

            // ---- precursor spectrum ref (only for the primary precursor): previous scan at
            // msLevel-1 whose scan window brackets our isolation m/z. Matches pwiz C++
            // findPrecursorSpectrumIndex — important for triple-play LTQ zoom-scan patterns.
            if (isPrimary)
            {
                int precursorIndex = FindPrecursorIndex(ie.Index, precursorMsLevel, isolationMz, ie.Scan);
                if (precursorIndex >= 0)
                {
                    precursor.SpectrumId = _index[precursorIndex].Id;
                    // ---- peak intensity at the isolation m/z in the precursor scan ----
                    double queryHalfWidth = isolationHalfWidth > 0 ? 1.5 : 0.0;
                    double peakIntensity = SumIntensityInWindow(_index[precursorIndex].Scan, isolationMz, queryHalfWidth, preferCentroid);
                    if (peakIntensity > 0)
                        selectedIon.Set(CVID.MS_peak_intensity, peakIntensity, CVID.MS_number_of_detector_counts);
                }
            }

            precursor.SelectedIons.Add(selectedIon);

            // ---- activation ----
            try
            {
                var activation = filter.GetActivation(i);
                TranslateActivation(activation, precursor.Activation);
                double energy = filter.GetEnergy(i);
                if (energy > 0 && (activation == ActivationType.CollisionInducedDissociation
                                   || activation == ActivationType.HigherEnergyCollisionalDissociation))
                {
                    precursor.Activation.Set(CVID.MS_collision_energy, energy, CVID.UO_electronvolt);
                }
            }
            catch { }

            spec.Precursors.Add(precursor);
        }
    }

    /// <summary>
    /// Walks <see cref="_index"/> backward from <paramref name="fromIndex"/> to find the spectrum
    /// that produced this MSn's precursor. When the scan's <c>"Master Scan Number:"</c> trailer
    /// is set we prefer that scan (matches the Thermo-native master-scan link for DDA/TMT-style
    /// MS3 trees); otherwise fall back to the first preceding spectrum at
    /// <paramref name="precursorMsLevel"/> whose scan-range covers <paramref name="isolationMz"/>
    /// (rejects narrow-window zoom scans that don't bracket the MSn target). Mirrors pwiz C++
    /// findPrecursorSpectrumIndex in SpectrumList_Thermo.cpp:972+.
    /// </summary>
    private int FindPrecursorIndex(int fromIndex, int precursorMsLevel, double isolationMz, int currentScan)
    {
        long masterScan = TryGetTrailerInt(currentScan, "Master Scan Number:", out long m) && m > 0 ? m : -1;

        for (int j = fromIndex - 1; j >= 0; j--)
        {
            var prev = _index[j];
            if (MsOrderToLevel(prev.MsOrder) < 1) continue;

            if (masterScan > 0)
            {
                if (masterScan == prev.Scan)
                {
                    // Master-scan hit: accept if it's at the right ms level, else keep looking
                    // (master scan can be a non-precursor triggering scan, e.g. ETD→HCD).
                    if (MsOrderToLevel(prev.MsOrder) == precursorMsLevel) return j;
                    masterScan = -1;
                    continue;
                }
                if (masterScan > prev.Scan) return -1; // walked past the master; give up
                continue;
            }

            if (MsOrderToLevel(prev.MsOrder) != precursorMsLevel) continue;

            if (isolationMz <= 0) return j;

            var candFilter = _raw.Raw.GetFilterForScanNumber(prev.Scan);
            bool mzInRange = false;
            int rangeCount = candFilter.MassRangeCount;
            for (int r = 0; r < rangeCount && !mzInRange; r++)
            {
                var range = candFilter.GetMassRange(r);
                if (isolationMz >= range.Low && isolationMz <= range.High)
                    mzInRange = true;
            }
            if (mzInRange) return j;
        }
        return -1;
    }

    private static int MsOrderToLevel(MSOrderType order) => order switch
    {
        MSOrderType.Ms => 1,
        MSOrderType.Ms2 => 2,
        MSOrderType.Ms3 => 3,
        MSOrderType.Ms4 => 4,
        MSOrderType.Ms5 => 5,
        MSOrderType.Ms6 => 6,
        MSOrderType.Ms7 => 7,
        MSOrderType.Ms8 => 8,
        MSOrderType.Ms9 => 9,
        MSOrderType.Ms10 => 10,
        _ => 1,
    };

    private static void TranslateActivation(ActivationType t, Activation a)
    {
        switch (t)
        {
            case ActivationType.CollisionInducedDissociation: a.Set(CVID.MS_collision_induced_dissociation); break;
            case ActivationType.MultiPhotonDissociation: a.Set(CVID.MS_photodissociation); break;
            case ActivationType.ElectronCaptureDissociation: a.Set(CVID.MS_electron_capture_dissociation); break;
            case ActivationType.PQD: a.Set(CVID.MS_pulsed_q_dissociation); break;
            case ActivationType.HigherEnergyCollisionalDissociation: a.Set(CVID.MS_beam_type_collision_induced_dissociation); break;
            case ActivationType.ElectronTransferDissociation: a.Set(CVID.MS_electron_transfer_dissociation); break;
            case ActivationType.UltraVioletPhotoDissociation: a.Set(CVID.MS_photodissociation); break;
            default: a.Set(CVID.MS_collision_induced_dissociation); break;
        }
    }

    public void Dispose()
    {
        if (_ownsRaw) _raw.Dispose();
    }
}
