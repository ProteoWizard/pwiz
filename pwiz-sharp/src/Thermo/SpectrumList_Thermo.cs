using System.Globalization;
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
public sealed class SpectrumList_Thermo : SpectrumListBase, IDisposable
{
    private readonly ThermoRawFile _raw;
    private readonly bool _ownsRaw;
    private readonly List<IndexEntry> _index = new();
    private readonly Dictionary<MassAnalyzerType, Pwiz.Data.MsData.Instruments.InstrumentConfiguration> _icByAnalyzer = new();
    private readonly Pwiz.Data.MsData.Instruments.InstrumentConfiguration? _defaultIc;
    private readonly Dictionary<string, int> _trailerIndexByLabel = new(StringComparer.Ordinal);

    // Small LRU-ish cache so an MS1 that is the precursor for many MS2s only gets peak-decoded once.
    private const int PrecursorCacheSize = 10;
    private readonly LinkedList<(int Scan, double[] Mz, double[] Intensity)> _precursorCache = new();

    private (double[] Mz, double[] Intensity) GetCachedPeaks(int scanNumber)
    {
        for (var node = _precursorCache.First; node is not null; node = node.Next)
        {
            if (node.Value.Scan == scanNumber)
            {
                _precursorCache.Remove(node);
                _precursorCache.AddFirst(node);
                return (node.Value.Mz, node.Value.Intensity);
            }
        }
        var (mz, intensity) = _raw.GetPeaks(scanNumber, preferCentroid: false);
        _precursorCache.AddFirst((scanNumber, mz, intensity));
        if (_precursorCache.Count > PrecursorCacheSize)
            _precursorCache.RemoveLast();
        return (mz, intensity);
    }

    private double SumIntensityInWindow(int scanNumber, double centerMz, double halfWidth)
    {
        var (mz, intensity) = GetCachedPeaks(scanNumber);
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

    /// <summary>Creates a spectrum list over <paramref name="raw"/> with no IC binding.</summary>
    public SpectrumList_Thermo(ThermoRawFile raw, bool ownsRaw = true)
        : this(raw, ownsRaw, null, null) { }

    internal SpectrumList_Thermo(ThermoRawFile raw, bool ownsRaw,
        Pwiz.Data.MsData.Instruments.InstrumentConfiguration? defaultInstrumentConfiguration,
        IReadOnlyDictionary<MassAnalyzerType, Pwiz.Data.MsData.Instruments.InstrumentConfiguration>? icByAnalyzer)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
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
            var entry = new IndexEntry
            {
                Index = scan - _raw.FirstScan,
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

    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
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

        // ---- scan list ----
        var scan = new Scan();
        // Prefer the analyzer-specific IC when available; fall back to the document default.
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

        // ---- polarity ----
        if (ie.Polarity == PolarityType.Positive)
            spec.Params.Set(CVID.MS_positive_scan);
        else if (ie.Polarity == PolarityType.Negative)
            spec.Params.Set(CVID.MS_negative_scan);

        // ---- profile / centroid flag (from filter) ----
        var filter = raw.GetFilterForScanNumber(scanNumber);
        if (filter.ScanData == ScanDataType.Profile)
            spec.Params.Set(CVID.MS_profile_spectrum);
        else
            spec.Params.Set(CVID.MS_centroid_spectrum);

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
            PopulatePrecursor(spec, filter, ie);

        // ---- binary data ----
        // Always retrieve the mass list so defaultArrayLength + lowest/highest observed m/z match
        // the data even when getBinaryData is false. C++ does the same (see SpectrumList_Thermo.cpp).
        var (mz, intensity) = _raw.GetPeaks(scanNumber, preferCentroid: false);
        spec.DefaultArrayLength = mz.Length;
        if (mz.Length > 0)
        {
            spec.Params.Set(CVID.MS_lowest_observed_m_z, mz[0], CVID.MS_m_z);
            spec.Params.Set(CVID.MS_highest_observed_m_z, mz[^1], CVID.MS_m_z);
            if (getBinaryData)
                spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
        }

        return spec;
    }

    private void PopulatePrecursor(Spectrum spec, IScanFilter filter, IndexEntry ie)
    {
        var precursor = new Precursor();

        double isolationMz = ie.IsolationMz;
        double isolationHalfWidth = 0;
        try
        {
            if (filter.MassCount > 0)
            {
                // Thermo .NET SDK returns the half-width ("offset") already; pwiz C++'s
                // precursorInfo.isolationWidth is full-width and is halved for offsets.
                isolationHalfWidth = filter.GetIsolationWidth(filter.MassCount - 1);
            }
        }
        catch { }

        if (isolationMz > 0)
        {
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isolationMz, CVID.MS_m_z);
            if (isolationHalfWidth > 0)
            {
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, isolationHalfWidth, CVID.MS_m_z);
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, isolationHalfWidth, CVID.MS_m_z);
            }
            int precursorMsLevel = Math.Max(1, MsOrderToLevel(ie.MsOrder) - 1);
            // Matches pwiz C++: userParams.emplace_back("ms level", string) with no xsd type.
            precursor.IsolationWindow.UserParams.Add(new UserParam(
                "ms level", precursorMsLevel.ToString(CultureInfo.InvariantCulture)));
        }

        // ---- selected ion (m/z + charge) ----
        var selectedIon = new SelectedIon();
        double selectedIonMz = isolationMz;

        int precursorCharge = 0;
        if (TryGetTrailerInt(ie.Scan, "Charge State:", out long cs) && cs > 0)
            precursorCharge = (int)cs;
        if (TryGetTrailerDouble(ie.Scan, "Monoisotopic M/Z:", out double mono) && mono > 0)
            selectedIonMz = mono;

        if (selectedIonMz > 0)
            selectedIon.Set(CVID.MS_selected_ion_m_z, selectedIonMz, CVID.MS_m_z);
        if (precursorCharge > 0)
            selectedIon.Set(CVID.MS_charge_state, precursorCharge);
        precursor.SelectedIons.Add(selectedIon);

        // ---- precursor spectrum ref: previous scan at msLevel-1 ----
        int currentLevel = MsOrderToLevel(ie.MsOrder);
        int targetPrecursorLevel = Math.Max(1, currentLevel - 1);
        int precursorIndex = -1;
        for (int j = ie.Index - 1; j >= 0; j--)
        {
            var prev = _index[j];
            if (MsOrderToLevel(prev.MsOrder) == targetPrecursorLevel)
            {
                precursor.SpectrumId = prev.Id;
                precursorIndex = j;
                break;
            }
        }

        // ---- peak intensity at the isolation m/z in the precursor scan ----
        if (precursorIndex >= 0 && isolationMz > 0)
        {
            // pwiz C++ uses 1.5 as the query half-width when any isolation width is known,
            // 0 otherwise — see SpectrumList_Thermo.cpp line ~647.
            double queryHalfWidth = isolationHalfWidth > 0 ? 1.5 : 0.0;
            double peakIntensity = SumIntensityInWindow(_index[precursorIndex].Scan, isolationMz, queryHalfWidth);
            if (peakIntensity > 0)
                selectedIon.Set(CVID.MS_peak_intensity, peakIntensity, CVID.MS_number_of_detector_counts);
        }

        // ---- activation ----
        try
        {
            if (filter.MassCount > 0)
            {
                int last = filter.MassCount - 1;
                var activation = filter.GetActivation(last);
                TranslateActivation(activation, precursor.Activation);
                double energy = filter.GetEnergy(last);
                if (energy > 0 && (activation == ActivationType.CollisionInducedDissociation
                                   || activation == ActivationType.HigherEnergyCollisionalDissociation))
                {
                    precursor.Activation.Set(CVID.MS_collision_energy, energy, CVID.UO_electronvolt);
                }
            }
        }
        catch { }

        spec.Precursors.Add(precursor);
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
