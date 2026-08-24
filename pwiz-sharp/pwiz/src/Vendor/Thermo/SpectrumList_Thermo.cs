using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using ThermoFisher.CommonCore.Data.Business;
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
public sealed class SpectrumList_Thermo : SpectrumListBase, IVendorCentroidingSpectrumList, IIonMobilitySpectrumList
{
    /// <summary>True iff the file may carry FAIMS data. cpp parity: returns true
    /// unconditionally because actually checking would require iterating every scan's
    /// trailer params — too expensive at construction time. The per-spectrum
    /// FAIMS-on check happens at <c>GetSpectrum</c> time and sets
    /// <c>MS_FAIMS_compensation_voltage</c> on the spectrum when the scan
    /// filter carries a cv= token.</summary>
    /// <remarks>
    /// TODO: there's probably a way to check this without walking every scan — most
    /// likely the file-level instrument-method text (the Thermo SDK exposes it via
    /// <c>IRawDataPlus.GetInstrumentMethod</c>) mentions "FAIMS" iff the run was
    /// configured with the device, and the run header / sample info tables may carry
    /// a direct "FAIMSCV" indicator. Worth a short investigation when a non-FAIMS-
    /// Thermo caller hits the false-positive Units = CompensationV reporting.
    /// Until then we ship cpp's hardcoded-true behavior so callers see identical
    /// answers between cpp pwiz and pwiz-sharp.
    /// </remarks>
#pragma warning disable CA1822 // public instance property for cross-vendor consistency; cpp returns true unconditionally too
    public bool HasIonMobility => true;
#pragma warning restore CA1822

    /// <inheritdoc cref="IIonMobilitySpectrumList.IonMobilityUnits"/>
    /// <remarks>Thermo's only IM-style data is FAIMS, which reports compensation voltage in V.
    /// Reported as <see cref="IonMobilityUnits.CompensationV"/> for every Thermo file
    /// (cpp parity — see <see cref="HasIonMobility"/>).</remarks>
    public IonMobilityUnits IonMobilityUnits => IonMobilityUnits.CompensationV;

    /// <inheritdoc/>
    /// <remarks>FAIMS reports CV per-spectrum, never as a 3-array.</remarks>
    public bool HasCombinedIonMobility => false;

    /// <inheritdoc/>
    public bool IsWatersSonar => false;

    // No CCS conversion: FAIMS compensation voltage isn't on a CCS calibration curve,
    // so neither cpp nor sharp expose IIonMobilityCcsConversion for Thermo.

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
    private readonly bool _srmAsSpectra;
    private readonly Pwiz.Data.MsData.Instruments.InstrumentConfiguration? _pdaIc;

    // Scan census, mirroring cpp's spectraByScanType / spectraByMSOrder / spectraByController
    // (SpectrumList_Thermo.cpp:832-834, filled in createIndex). Counted over EVERY MS scan,
    // including the SIM/SRM scans that get routed to the chromatogram list. Reader_Thermo uses
    // these for fileContent, and findPrecursorSpectrumIndex uses the ms-order census to bail out
    // early on targeted runs that contain no precursor-level scans at all.
    private readonly int[] _spectraByScanType = new int[(int)ScanModeType.Q3Ms + 1];
    private readonly int[] _spectraByMsOrder = new int[(int)MSOrderType.Ms10 + 3 + 1]; // +3: MSOrderType.Ng == -3
    private int _pdaSpectraCount;

    /// <summary>cpp <c>SpectrumList_Thermo::numSpectraOfScanType</c>.</summary>
    internal int NumSpectraOfScanType(ScanModeType scanType)
    {
        int i = (int)scanType;
        return i >= 0 && i < _spectraByScanType.Length ? _spectraByScanType[i] : 0;
    }

    /// <summary>cpp <c>SpectrumList_Thermo::numSpectraOfMSOrder</c>.</summary>
    internal int NumSpectraOfMsOrder(MSOrderType msOrder)
    {
        int i = (int)msOrder + 3;
        return i >= 0 && i < _spectraByMsOrder.Length ? _spectraByMsOrder[i] : 0;
    }

    /// <summary>cpp <c>SpectrumList_Thermo::numSpectraOfController(Controller_PDA)</c>.</summary>
    internal int NumPdaSpectra => _pdaSpectraCount;

    internal SpectrumList_Thermo(ThermoRawFile raw, bool ownsRaw,
        Pwiz.Data.MsData.Instruments.InstrumentConfiguration? defaultInstrumentConfiguration,
        IReadOnlyDictionary<MassAnalyzerType, Pwiz.Data.MsData.Instruments.InstrumentConfiguration>? icByAnalyzer,
        bool simAsSpectra = false, bool srmAsSpectra = false,
        Pwiz.Data.MsData.Instruments.InstrumentConfiguration? pdaInstrumentConfiguration = null)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
        _simAsSpectra = simAsSpectra;
        _srmAsSpectra = srmAsSpectra;
        _defaultIc = defaultInstrumentConfiguration;
        _pdaIc = pdaInstrumentConfiguration;
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

    /// <summary>
    /// Raw (untrimmed) trailer string, mirroring cpp <c>ScanInfoImpl::trailerExtraValue(name)</c>
    /// (RawFile.cpp:1187) which reads <c>trailerExtraMap_[name]</c> — an absent key yields the
    /// empty string, and a present value is returned exactly as the SDK rendered it.
    /// </summary>
    private string GetTrailerStringRaw(int scanNumber, string label)
    {
        if (!TryGetTrailerValue(scanNumber, label, out var v) || v is null) return string.Empty;
        return v.ToString() ?? string.Empty;
    }

    /// <summary>
    /// cpp-parity double trailer read. Mirrors <c>RawFileThreadImpl::getTrailerExtraValueDouble</c>
    /// (RawFile.cpp:2628-2642): a MISSING label (or a null value) yields <c>valueIfMissing</c>
    /// (0) and is NOT an error, while a present-but-unconvertible value throws — which the
    /// callers in cpp's SpectrumList_Thermo swallow with <c>catch (RawEgg&amp;)</c>.
    /// </summary>
    /// <returns>
    /// false only for the "present but unconvertible" case, i.e. exactly when cpp's
    /// <c>try { ... } catch (RawEgg&amp;) {}</c> suppresses the emission. Returning true with
    /// <paramref name="value"/> == 0 for a missing label is what makes cpp emit
    /// <c>ion injection time = 0.0</c> / <c>Monoisotopic M/Z: = 0</c> on files whose trailer
    /// lacks those labels entirely.
    /// </returns>
    private bool TryGetTrailerDoubleOrZero(int scanNumber, string label, out double value)
    {
        value = 0;
        if (!_trailerIndexByLabel.TryGetValue(label, out int idx)) return true; // missing label -> 0
        object? v;
        try { v = _raw.Raw.GetTrailerExtraValue(scanNumber, idx); }
        catch { return true; } // cpp's find() miss / null result path -> valueIfMissing
        if (v is null) return true;
        try { value = Convert.ToDouble(v, CultureInfo.InvariantCulture); return true; }
        catch { return false; } // Convert::ToDouble threw -> RawEgg -> cpp emits nothing
    }

    /// <summary>
    /// cpp's <c>lexical_cast&lt;string&gt;(double)</c> rendering: boost streams the value with
    /// <c>std::numeric_limits&lt;double&gt;::max_digits10</c> (17) significant digits, so
    /// 361.1810607910156 prints as "361.18106079101562". .NET's default "G" gives the shortest
    /// round-trip form instead, which is one digit shorter for most trailer values.
    /// </summary>
    private static string CppDoubleToString(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);

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

        // Controller this scan belongs to. MS scans use Device.MS / 1; PDA-as-spectra entries
        // use Device.Pda / N. Cpp emits these as separate native id "controllerType=4 ..." entries.
        public Device Controller = Device.MS;
        public int ControllerNumber = 1;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        for (int scan = _raw.FirstScan; scan <= _raw.LastScan; scan++)
        {
            var filter = _raw.Raw.GetFilterForScanNumber(scan);

            // Census first — cpp counts every MS scan before the SIM/SRM "continue"s
            // (SpectrumList_Thermo.cpp:882-884).
            int msOrderSlot = (int)filter.MSOrder + 3;
            if (msOrderSlot >= 0 && msOrderSlot < _spectraByMsOrder.Length) _spectraByMsOrder[msOrderSlot]++;
            int scanTypeSlot = (int)filter.ScanMode;
            if (scanTypeSlot >= 0 && scanTypeSlot < _spectraByScanType.Length) _spectraByScanType[scanTypeSlot]++;

            // SIM scans are emitted as chromatograms (grouped by Q1) unless simAsSpectra=true,
            // matching pwiz C++ ChromatogramList_Thermo.cpp:481-504.
            if (filter.ScanMode == ScanModeType.Sim && !_simAsSpectra)
                continue;
            // SRM scans become per-transition chromatograms (Q1, Q3) unless srmAsSpectra=true,
            // matching pwiz C++ ChromatogramList_Thermo.cpp:413-479 — but only when the
            // chromatogram list will actually take them. ChromatogramList_Thermo drops windows
            // wider than MaxSrmScanRange (they alias several ions, so they aren't transitions),
            // so an SRM scan with such a window has to stay a spectrum or its data is lost
            // entirely. Port of cpp SpectrumList_Thermo.cpp:894-916 "hasExcessiveSrmScanRange".
            if (filter.ScanMode == ScanModeType.Srm && !_srmAsSpectra
                && !HasExcessiveSrmScanRange(filter))
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

        // PDA-as-spectra: matches cpp SpectrumList_Thermo.cpp:939-961 — append one entry per
        // PDA scan as an EMR (electromagnetic-radiation) spectrum after the MS scans.
        AddPdaIndex();

        // Restore MS as the active controller so subsequent MS-side reads see the right state.
        try { _raw.Raw.SelectInstrument(Device.MS, 1); } catch { }
    }

    /// <summary>
    /// True when any of the scan's bracketed m/z windows is wider than
    /// <see cref="ChromatogramList_Thermo.MaxSrmScanRange"/> — cpp's
    /// <c>SpectrumList_Thermo.cpp:899-911</c> loop over <c>scanInfo->scanRange(i)</c>, which on
    /// the RawFileReader path is exactly <c>filter.GetMassRange(i).Low/High</c>
    /// (RawFile.cpp:1710).
    /// </summary>
    private static bool HasExcessiveSrmScanRange(IScanFilter filter)
    {
        try
        {
            for (int i = 0; i < filter.MassRangeCount; i++)
            {
                var range = filter.GetMassRange(i);
                if (range.High - range.Low > ChromatogramList_Thermo.MaxSrmScanRange)
                    return true;
            }
        }
        catch { /* filter without usable mass ranges — treat as a normal transition */ }
        return false;
    }

    private void AddPdaIndex()
    {
        int pdaCount = _raw.PdaControllerCount;
        for (int n = 1; n <= pdaCount; n++)
        {
            try { _raw.Raw.SelectInstrument(Device.Pda, n); }
            catch { continue; }

            int firstScan, lastScan;
            try
            {
                var hdr = _raw.Raw.RunHeaderEx;
                firstScan = hdr.FirstSpectrum;
                lastScan = hdr.LastSpectrum;
            }
            catch { continue; }

            for (int scan = firstScan; scan <= lastScan; scan++)
            {
                _index.Add(new IndexEntry
                {
                    Index = _index.Count,
                    Id = ThermoRawFile.NativeId(scan, Device.Pda, n),
                    Scan = scan,
                    Controller = Device.Pda,
                    ControllerNumber = n,
                });
                _pdaSpectraCount++;
            }
        }
    }

    /// <inheritdoc/>
    public string VendorCentroidName => "Thermo/Xcalibur peak picking";

    /// <inheritdoc/>
    public Spectrum GetCentroidSpectrum(int index, bool getBinaryData) =>
        GetSpectrumImpl(index, getBinaryData, preferCentroid: true);

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        GetSpectrumImpl(index, getBinaryData, preferCentroid: false);

    /// <summary>
    /// Populates a PDA "scan" as an electromagnetic-radiation spectrum (wavelength vs intensity),
    /// matching cpp SpectrumList_Thermo.cpp:282-317. Switches to the entry's PDA controller for
    /// the duration of the read and restores Device.MS afterward so subsequent MS reads see the
    /// original state.
    /// </summary>
    private Spectrum GetPdaSpectrum(int index, IndexEntry ie, bool getBinaryData)
    {
        var raw = _raw.Raw;
        try
        {
            raw.SelectInstrument(ie.Controller, ie.ControllerNumber);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                $"Error setting PDA controller {(int)ie.Controller}/{ie.ControllerNumber}: {e.Message}", e);
        }

        try
        {
            var spec = new Spectrum
            {
                Index = index,
                Id = ie.Id,
            };

            spec.Params.Set(CVID.MS_EMR_spectrum);
            spec.ScanList.Set(CVID.MS_no_combination);

            var scan = new Scan
            {
                InstrumentConfiguration = _pdaIc,
            };
            double rtMin = raw.RetentionTimeFromScanNumber(ie.Scan);
            scan.Set(CVID.MS_scan_start_time, rtMin, CVID.UO_minute);

            try
            {
                var stats = raw.GetScanStatsForScanNumber(ie.Scan);
                if (stats.BasePeakMass > 0)
                {
                    spec.Params.Set(CVID.MS_base_peak_m_z, stats.BasePeakMass, CVID.UO_nanometer);
                    spec.Params.Set(CVID.MS_base_peak_intensity, stats.BasePeakIntensity);
                }
                spec.Params.Set(CVID.MS_total_ion_current, stats.TIC);
                // Unconditional: cpp SpectrumList_Thermo.cpp:294 pushes the window with no
                // guard. Only the base-peak pair above is gated (on basePeakMass, at cpp:287),
                // so a PDA scan reporting HighMass 0 still gets a 0-0 scan window there.
                scan.ScanWindows.Add(new ScanWindow(stats.LowMass, stats.HighMass, CVID.UO_nanometer));
            }
            catch { /* stats unavailable on some PDA scans */ }

            spec.ScanList.Scans.Add(scan);

            // Read the scan data: GetSegmentedScanFromScanNumber returns wavelength positions on
            // PDA controllers (the same call returns m/z on MS controllers — units come from the
            // selected device). Cpp uses raw->getMassList(scan, ...) which is equivalent.
            double[] mz = Array.Empty<double>();
            double[] intensity = Array.Empty<double>();
            try
            {
                var seg = raw.GetSegmentedScanFromScanNumber(ie.Scan, null);
                mz = seg.Positions ?? Array.Empty<double>();
                intensity = seg.Intensities ?? Array.Empty<double>();
            }
            catch { /* leave empty */ }

            spec.DefaultArrayLength = mz.Length;
            if (mz.Length > 0)
            {
                spec.Params.Set(CVID.MS_lowest_observed_wavelength, mz[0], CVID.UO_nanometer);
                spec.Params.Set(CVID.MS_highest_observed_wavelength, mz[mz.Length - 1], CVID.UO_nanometer);
            }

            if (getBinaryData && mz.Length > 0)
            {
                // Mirror cpp swapMZIntensityArrays + replace m/z term with wavelength term.
                // Build wavelength + intensity arrays directly so the spectrum carries
                // MS_wavelength_array (not MS_m_z_array) — same byte layout, different CV term.
                var wlArray = new BinaryDataArray();
                wlArray.Set(CVID.MS_wavelength_array, "", CVID.UO_nanometer);
                wlArray.Data.AddRange(mz);
                var intArray = new BinaryDataArray();
                intArray.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
                intArray.Data.AddRange(intensity);
                spec.BinaryDataArrays.Add(wlArray);
                spec.BinaryDataArrays.Add(intArray);
            }
            return spec;
        }
        finally
        {
            try { raw.SelectInstrument(Device.MS, 1); } catch { }
        }
    }

    private Spectrum GetSpectrumImpl(int index, bool getBinaryData, bool preferCentroid)
    {
        var ie = _index[index];

        // PDA-as-spectra: separate population path. Cpp SpectrumList_Thermo.cpp:282-317 emits
        // these as MS_EMR_spectrum with wavelength axis (UO_nanometer) and no msLevel.
        if (ie.Controller == Device.Pda)
            return GetPdaSpectrum(index, ie, getBinaryData);

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

        // The scan filter is this method's equivalent of cpp's ScanInfo (SpectrumList_Thermo.cpp:269-275):
        // fetched ONCE per spectrum and reused by every site below (enhanced-resolution flag,
        // analyzer scan offset, filter string, profile/centroid, SIM/SRM scan windows, precursor
        // population). Each GetFilterForScanNumber call is an SDK round-trip that re-parses the
        // filter, so asking three times per scan cost ~200k redundant round-trips on a 100k-scan file.
        //
        // cpp throws ("Error retrieving ScanInfo") when the SDK cannot supply it, and the
        // resulting runtime_error aborts the conversion with the spectrum id in the message -
        // so do the same rather than half-degrading (one site used to guard with ?. while the
        // others dereferenced, i.e. a null filter surfaced as a NullReferenceException).
        var filter = raw.GetFilterForScanNumber(scanNumber)
            ?? throw new InvalidOperationException(
                $"Error retrieving scan filter for spectrum \"{ie.Id}\" (scan {scanNumber}).");

        // Zoom scans (narrow m/z window) and instruments flagged as "enhanced resolution" get
        // tagged with MS_enhanced_resolution_scan, matching pwiz C++ SpectrumList_Thermo.cpp:359.
        if (ie.ScanMode == ScanModeType.Zoom || filter.Enhanced == TriState.On)
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

        // Constant-neutral-loss / -gain scans carry the scan offset. cpp reads it off ScanInfo
        // (SpectrumList_Thermo.cpp:277-280); on the x64 RawFileReader path that value is simply
        // the filter's first mass — RawFile.cpp:1656-1657
        //   constantNeutralLoss_ = msOrder == Ng || msOrder == Nl;
        //   analyzerScanOffset_  = constantNeutralLoss_ ? filter_->GetMass(0) : 0;
        // Emitted here (before mass resolving power) to keep cpp's cvParam order.
        if (ie.MsOrder is MSOrderType.Nl or MSOrderType.Ng)
        {
            double analyzerScanOffset = 0;
            try { if (filter.MassCount > 0) analyzerScanOffset = filter.GetMass(0); } catch { }
            scan.Set(CVID.MS_analyzer_scan_offset, analyzerScanOffset, CVID.MS_m_z);
        }

        // Match pwiz C++ SpectrumList_Thermo cvParam order within the scan element:
        //   mass resolving power, filter string, preset scan configuration, ion injection time.

        long resolvingPower = 0;
        if (TryGetTrailerInt(scanNumber, "Orbitrap Resolution:", out long rp1) && rp1 > 0)
            resolvingPower = rp1;
        else if (TryGetTrailerInt(scanNumber, "FT Resolution:", out long rp2) && rp2 > 0)
            resolvingPower = rp2;
        if (resolvingPower > 0)
            scan.Set(CVID.MS_mass_resolving_power, resolvingPower);

        // Note: cpp's older RawFileReader (5.0.0.93 vs our 8.0.6.0) renders half-way precursor
        // m/z values one ulp higher here - 598.125 becomes "598.13" there and "598.12" for us.
        // Both sides call IScanFilter.ToString(); v8's GetScanEventStringForScanNumber agrees
        // with its ToString(), so the difference is inside the SDK and cannot be closed from
        // this side while the two trees ship different RawFileReader versions.
        string filterString = filter.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(filterString))
            scan.Set(CVID.MS_filter_string, filterString);

        // cpp SpectrumList_Thermo.cpp:352-357 keys this off the raw trailer STRING being
        // non-empty and writes that string verbatim — so a scan event of "0" is emitted, and
        // there is no numeric gate. Parsing to a long and requiring > 0 (as this used to)
        // silently dropped the param on instruments that number scan events from zero.
        string scanEventStr = GetTrailerStringRaw(scanNumber, "Scan Event:");
        if (scanEventStr.Length > 0)
            scan.Set(CVID.MS_preset_scan_configuration, scanEventStr);

        // Scan Description (e.g. "sps" for SPS-MS3 scans) — emitted as a spectrum-level userParam
        // on the outer Spectrum, matching pwiz C++.
        if (TryGetTrailerString(scanNumber, "Scan Description:", out string scanDesc))
            spec.Params.UserParams.Add(new UserParam("scan description", scanDesc, "xsd:string"));

        // cpp SpectrumList_Thermo.cpp:399-411. The try/catch there only fires when the trailer
        // value is present but not convertible to a double; a MISSING label yields 0 from
        // getTrailerExtraValueDouble's valueIfMissing, so cpp emits the userParam with value 0.
        if (msLevel > 1 && TryGetTrailerDoubleOrZero(scanNumber, "Monoisotopic M/Z:", out double monoMz))
        {
            scan.UserParams.Add(new UserParam(
                "[Thermo Trailer Extra]Monoisotopic M/Z:",
                CppDoubleToString(monoMz),
                "xsd:float"));
        }

        // cpp SpectrumList_Thermo.cpp:413-431 — same "missing label reads as 0" rule, which is
        // why cpp emits "ion injection time = 0.0" on instruments (magnetic sector, TSQ) whose
        // trailer has no "Ion Injection Time (ms):" label at all.
        if (TryGetTrailerDoubleOrZero(scanNumber, "Ion Injection Time (ms):", out double injMs))
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
        bool scanIsProfile = filter.ScanData == ScanDataType.Profile;
        bool emitCentroid = !scanIsProfile || preferCentroid;
        spec.Params.Set(emitCentroid ? CVID.MS_centroid_spectrum : CVID.MS_profile_spectrum);

        // ---- scan stats (base peak, TIC) ----
        try
        {
            var stats = raw.GetScanStatsForScanNumber(scanNumber);
            // Unconditional, matching cpp SpectrumList_Thermo.cpp:377-378 — an empty MSn scan
            // reports basePeakMass 0 and cpp still writes both params. (Only the non-MS/PDA
            // branch at cpp:287 gates on basePeakMass() > 0; see GetPdaSpectrum.)
            spec.Params.Set(CVID.MS_base_peak_m_z, stats.BasePeakMass, CVID.MS_m_z);
            spec.Params.Set(CVID.MS_base_peak_intensity, stats.BasePeakIntensity, CVID.MS_number_of_detector_counts);
            spec.Params.Set(CVID.MS_total_ion_current, stats.TIC);

            // cpp SpectrumList_Thermo.cpp:387-397 branches here, and both halves matter:
            //
            //   * a SIM or SRM scan with MORE THAN ONE mass range gets one scanWindow per
            //     range, taken from the filter - a multiplexed SIM legitimately acquires
            //     several narrow windows, and collapsing them into one span would describe a
            //     precursor range the instrument never isolated;
            //   * everything else gets a single window from scanInfo->lowMass()/highMass(),
            //     which on the x64 path are ScanStatistics LowMass/HighMass
            //     (RawFile.cpp:1397-1398), NOT the filter's ranges. The stats report what was
            //     acquired, the filter what was requested, and they differ - e.g. acquired
            //     2000.00005 against a filter 2000.0, or 499.953697763383 against 500.0.
            bool multiRange = (ie.ScanMode == ScanModeType.Sim || ie.ScanMode == ScanModeType.Srm)
                              && filter.MassRangeCount > 1;
            if (multiRange)
            {
                for (int r = 0; r < filter.MassRangeCount; r++)
                {
                    var mr = filter.GetMassRange(r);
                    scan.ScanWindows.Add(new ScanWindow(mr.Low, mr.High, CVID.MS_m_z));
                }
            }
            else
            {
                scan.ScanWindows.Add(new ScanWindow(stats.LowMass, stats.HighMass, CVID.MS_m_z));
            }
        }
        catch { /* ignore — a subset of scans might not expose stats */ }

        // FAIMS compensation voltage. Like the sid= offset voltage above, the filter string embeds
        // it as "cv=N.NN" — e.g. "FTMS + p NSI cv=-50.00 Full ms ...". Emit at SPECTRUM level (not the
        // scan) to match pwiz C++ SpectrumList_Thermo.cpp:381-382 and the Skyline read site
        // (MsDataFileImpl.GetIonMobility reads spectrum.CvParam(MS_FAIMS_compensation_voltage)). Emit
        // whenever the token is present, including cv=0 (C++ keys on FAIMSOn, not value != 0). Without
        // this the reader advertises IonMobilityUnits.CompensationV but every spectrum reports no CV,
        // so FAIMS CV filtering silently drops all data.
        if (TryParseCv(filterString, out double cv))
            spec.Params.Set(CVID.MS_FAIMS_compensation_voltage, cv);

        spec.ScanList.Set(CVID.MS_no_combination);
        spec.ScanList.Scans.Add(scan);

        // ---- precursor (MS2+) ----
        if (msLevel > 1 && ie.MsOrder != MSOrderType.Par)
        {
            // Route to the multi-precursor path for MSX (Multiplex flag) or SPS-style scans.
            // pwiz C++ keys SPS off the "SPS Masses:" trailer being non-empty (RawFile.cpp:1422
            // sets hasMultiplePrecursors_=true and isSPS_=true whenever the trailer string is
            // non-empty, regardless of how many actual mass tokens it parses to). Use the same
            // textual non-emptiness check rather than a parsed-count threshold so files with
            // a single SPS mass + trailing comma route the same way cpp does.
            bool isMsx = filter.Multiplex == TriState.On;
            bool hasSpsTrailer = TryGetTrailerString(ie.Scan, "SPS Masses:", out string spsRaw)
                && !string.IsNullOrWhiteSpace(spsRaw);
            if (isMsx || hasSpsTrailer)
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

        // Supplemental activation is a per-SCAN property in cpp (saType_/saEnergy_), applied to
        // every precursor of the multi-precursor branch — RawFile.cpp:1680-1701. On this branch
        // cpp's `hasMultiplePrecursors_ ||` clause (RawFile.cpp:1666) lets EVERY filter mass into
        // precursorActivationTypes_, so read them all.
        var msxActs = ReadActivations(filter, Math.Max(1, filter.MassCount));

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
            // Charge state on the innermost (primary) precursor only — pwiz C++
            // SpectrumList_Thermo.cpp:506-509 only consults the "Charge State:" trailer when
            // precursorInfo.msLevel == msLevel-1; outer precursors fall back to the
            // zero-initialized precursorInfo.chargeState (no emission). Additionally, the
            // ScanInfo::precursorCharge wrapper returns 0 when the file has >1 SPS masses
            // (RawFile.cpp:1730) — the per-scan trailer in those files refers to the MS2
            // precursor, which is ambiguous for SPS targets, so cpp suppresses it entirely.
            if (e.Level == msLevel - 1 && spsMasses.Count <= 1
                && TryGetTrailerInt(ie.Scan, "Charge State:", out long cs) && cs > 0)
                selectedIon.Set(CVID.MS_charge_state, (int)cs);
            precursor.SelectedIons.Add(selectedIon);

            // cpp SpectrumList_Thermo.cpp:487-498 — same rules as the single-precursor branch:
            // Unknown activation is assumed CID, the supplemental type is applied to every
            // precursor of the scan, and both energies are written without a > 0 gate.
            var eFlags = ToFlags(e.Act);
            if (eFlags == ActivationFlags.None) eFlags = ActivationFlags.Cid;
            SetActivationType(eFlags, msxActs.SaFlags, precursor.Activation);
            if ((eFlags & (ActivationFlags.Cid | ActivationFlags.Hcd)) != 0)
                precursor.Activation.Set(CVID.MS_collision_energy, e.Energy, CVID.UO_electronvolt);
            if (msxActs.SaFlags != ActivationFlags.None)
                precursor.Activation.Set(CVID.MS_supplemental_collision_energy, msxActs.SaEnergy, CVID.UO_electronvolt);

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

    private static readonly System.Text.RegularExpressions.Regex CvRegex =
        new(@"\bcv=([\-+]?\d+(?:\.\d+)?)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool TryParseCv(string filter, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(filter)) return false;
        var m = CvRegex.Match(filter);
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

    /// <summary>
    /// Port of cpp's <c>ActivationType</c> bit field (RawFileTypes.h:980-993). It is a bit MASK,
    /// not a plain enum: an ETD scan acquired with supplemental activation carries
    /// <c>ETD | CID</c> (or <c>ETD | HCD</c>), and cpp's <c>setActivationType</c> emits a cvParam
    /// for every bit that is set. Modelling it as a single-valued enum (as this file used to)
    /// drops the supplemental partner's own term — the missing
    /// <c>MS:1000133 collision-induced dissociation</c> /
    /// <c>MS:1000422 beam-type collision-induced dissociation</c> on EThcD/ETciD spectra.
    /// </summary>
    [Flags]
    private enum ActivationFlags
    {
        None = 0,
        Cid = 1,
        Mpd = 2,
        Ecd = 4,
        Pqd = 8,
        Etd = 16,
        Hcd = 32,
        Ptr = 128,
        Netd = 256,
        Nptr = 512,
    }

    /// <summary>Port of cpp <c>convertRawFileReaderActivationType</c> (RawFile.cpp:1549-1566).</summary>
    private static ActivationFlags ToFlags(ActivationType t) => t switch
    {
        ActivationType.CollisionInducedDissociation => ActivationFlags.Cid,
        ActivationType.ElectronCaptureDissociation => ActivationFlags.Ecd,
        ActivationType.ElectronTransferDissociation => ActivationFlags.Etd,
        ActivationType.HigherEnergyCollisionalDissociation => ActivationFlags.Hcd,
        ActivationType.MultiPhotonDissociation => ActivationFlags.Mpd,
        ActivationType.NegativeElectronTransferDissociation => ActivationFlags.Netd,
        ActivationType.NegativeProtonTransferReaction => ActivationFlags.Nptr,
        ActivationType.PQD => ActivationFlags.Pqd,
        ActivationType.ProtonTransferReaction => ActivationFlags.Ptr,
        ActivationType.SAactivation => ActivationFlags.Cid,
        ActivationType.UltraVioletPhotoDissociation => ActivationFlags.Mpd, // cpp marks this FIXME
        _ => ActivationFlags.None,
    };

    /// <summary>
    /// Port of cpp <c>pwiz::msdata::detail::setActivationType</c>
    /// (Reader_Thermo_Detail.cpp:591-616), including its cvParam order:
    /// CID, ETD, ECD, PQD, HCD, supplemental, MPD.
    /// </summary>
    private static void SetActivationType(ActivationFlags activationType, ActivationFlags supplementalActivationType, Activation activation)
    {
        if ((activationType & ActivationFlags.Cid) != 0) activation.Set(CVID.MS_collision_induced_dissociation);
        if ((activationType & ActivationFlags.Etd) != 0) activation.Set(CVID.MS_electron_transfer_dissociation);
        if ((activationType & ActivationFlags.Ecd) != 0) activation.Set(CVID.MS_electron_capture_dissociation);
        if ((activationType & ActivationFlags.Pqd) != 0) activation.Set(CVID.MS_pulsed_q_dissociation);
        if ((activationType & ActivationFlags.Hcd) != 0) activation.Set(CVID.MS_beam_type_collision_induced_dissociation);

        if (supplementalActivationType != ActivationFlags.None)
        {
            if ((supplementalActivationType & ActivationFlags.Cid) != 0)
                activation.Set(CVID.MS_supplemental_collision_induced_dissociation);
            else if ((supplementalActivationType & ActivationFlags.Hcd) != 0)
                activation.Set(CVID.MS_supplemental_beam_type_collision_induced_dissociation);
        }

        // ActivationType_PTR: what does this map to? (cpp's question, kept unanswered)
        if ((activationType & ActivationFlags.Mpd) != 0) activation.Set(CVID.MS_photodissociation);
    }

    /// <summary>
    /// Per-precursor activation bits + energies for one scan filter, plus the scan's
    /// supplemental activation. Port of cpp <c>ScanInfoImpl::parseFilterString</c>'s x64 branch,
    /// RawFile.cpp:1665-1701.
    /// </summary>
    /// <remarks>
    /// Deliberately omits cpp's FTICR rule at RawFile.cpp:1673-1678 / 1691-1695 (rewrite CID as
    /// HCD when <c>Detector == Valid</c> and the analyzer resolves to FTICR, i.e. an LTQ-FT).
    /// That needs the instrument model plumbed down here, and the 492-file parity corpus shows
    /// no divergence attributable to it.
    /// </remarks>
    private static (ActivationFlags[] Flags, double[] Energies, ActivationFlags SaFlags, double SaEnergy)
        ReadActivations(IScanFilter filter, int precursorCount)
    {
        var flags = new ActivationFlags[precursorCount];
        var energies = new double[precursorCount];
        for (int i = 0; i < precursorCount; i++)
        {
            try
            {
                flags[i] = ToFlags(filter.GetActivation(i));
                energies[i] = filter.GetEnergy(i);
            }
            catch { /* filter without an activation at this index — leave Unknown/0 like cpp */ }
        }

        var saFlags = ActivationFlags.None;
        double saEnergy = 0;
        bool supplemental = filter.SupplementalActivation == TriState.On
                            && precursorCount > 0
                            && (flags[0] & ActivationFlags.Etd) != 0;
        if (supplemental)
        {
            // cpp reads the SECOND filter entry when the "sa" activation was spelled out
            // (Lumos style "613.7694@etd132.10 613.7694@cid35.00"); when the filter carries only
            // the bare "sa" flag it assumes supplemental CID with energy 0.
            if (filter.MassCount > 1)
            {
                try
                {
                    saFlags = ToFlags(filter.GetActivation(1));
                    saEnergy = filter.GetEnergy(1);
                }
                catch { saFlags = ActivationFlags.Cid; saEnergy = 0; }
            }
            else
            {
                saFlags = ActivationFlags.Cid;
                saEnergy = 0;
            }

            // cpp ORs the supplemental bit into precursor 0 only (RawFile.cpp:1700).
            flags[0] |= saFlags;
        }
        return (flags, energies, saFlags, saEnergy);
    }

    private void PopulatePrecursor(Spectrum spec, IScanFilter filter, IndexEntry ie, bool preferCentroid)
    {
        // pwiz C++ iterates over filter masses in reverse (innermost first). Each filter mass
        // at index i maps to a precursor at ms level i+1; the innermost (index MassCount-1)
        // gets the full treatment (trailer-width override, spectrumRef lookup by scan-range,
        // peak_intensity, monoisotope adjustment). Outer precursors get a simpler emission.
        // Only iterate up to msLevel-1 entries — additional filter masses (e.g. an ETD scan
        // with @etd@cid encoding) describe SUPPLEMENTAL ACTIVATION on the same precursor, not
        // additional nested precursors. cpp gates this with `i < msLevel_-1` in
        // RawFile.cpp:1657 when populating precursorMZs_.
        int msLevel = MsOrderToLevel(ie.MsOrder);
        int massCount = filter.MassCount;
        if (massCount == 0) return;
        int precursorMassCount = Math.Min(massCount, msLevel - 1);
        if (precursorMassCount <= 0) return;

        var acts = ReadActivations(filter, precursorMassCount);

        for (int i = precursorMassCount - 1; i >= 0; i--)
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
                bool trailerHit = TryGetTrailerDouble(ie.Scan, widthTag, out double trailerWidth);
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

            // pwiz C++ single-precursor branch reads "Charge State:" trailer for BOTH primary
            // and non-primary precursors (SpectrumList_Thermo.cpp:594 + 720). Multi-precursor
            // branch differs — that path only reads it for primary — and the SPS-trailer
            // routing above sends those scans there.
            int precursorCharge = 0;
            if (TryGetTrailerInt(ie.Scan, "Charge State:", out long cs) && cs > 0)
                precursorCharge = (int)cs;

            // cpp's selectedIonMz seed is scanInfo->precursorMZ(i) with preferMonoisotope=true
            // (RawFile.cpp:1753-1769): the scan's own "Monoisotopic M/Z:" trailer when > 0,
            // otherwise the filter's isolation m/z.
            if (isPrimary && TryGetTrailerDouble(ie.Scan, "Monoisotopic M/Z:", out double mono) && mono > 0)
                selectedIonMz = mono;

            if (isPrimary)
            {
                // Triple-play acquisitions insert a narrow zoom scan between the survey MS1 and
                // the MSn; the monoisotopic m/z and charge live on THAT scan, not on the MSn.
                // cpp SpectrumList_Thermo.cpp:596-614.
                var zoomScan = FindPrecursorZoomScan(precursorMsLevel, isolationMz, ie.Index);
                if (zoomScan >= 0)
                {
                    if (selectedIonMz == isolationMz
                        && TryGetTrailerDouble(zoomScan, "Monoisotopic M/Z:", out double zoomMono) && zoomMono > 0)
                        selectedIonMz = zoomMono;
                    if (precursorCharge == 0
                        && TryGetTrailerInt(zoomScan, "Charge State:", out long zoomCharge) && zoomCharge > 0)
                        precursorCharge = (int)zoomCharge;
                }

                // Reject when outside isolation window — guards against a known Thermo firmware
                // bug where the Monoisotopic trailer can report a reference mass well outside
                // the actual isolation. Matches pwiz C++ SpectrumList_Thermo.cpp 616-623.
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
                if (selectedIonMz < lo || selectedIonMz > hi)
                    selectedIonMz = isolationMz;
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
                // For MS3+ the precursor MS2 must be the one that isolated OUR outer m/z:
                // cpp passes precursorMZ(i-1) so "ms3 180.00@cid35 104.96@cid40" links to the
                // "ms2 180.00@cid35" scan, not to whichever earlier MS2 happens to cover
                // 104.96. cpp SpectrumList_Thermo.cpp:638.
                double precursorIsolationMz = 0;
                if (i > 0) { try { precursorIsolationMz = filter.GetMass(i - 1); } catch { } }

                int precursorIndex = FindPrecursorIndex(ie.Index, precursorMsLevel, isolationMz,
                    precursorIsolationMz, ie.Scan, out int nonPrecursorMasterScanNumber);
                if (precursorIndex >= 0)
                {
                    precursor.SpectrumId = _index[precursorIndex].Id;
                    // ---- peak intensity at the isolation m/z in the precursor scan ----
                    double queryHalfWidth = isolationHalfWidth > 0 ? 1.5 : 0.0;
                    double peakIntensity = SumIntensityInWindow(_index[precursorIndex].Scan, isolationMz, queryHalfWidth, preferCentroid);
                    if (peakIntensity > 0)
                        selectedIon.Set(CVID.MS_peak_intensity, peakIntensity, CVID.MS_number_of_detector_counts);
                }
                // The "Master Scan Number:" trailer can point at a scan of the SAME ms level as
                // this one (e.g. an ETD scan triggering an HCD scan); cpp records that scan
                // number as a spectrum-level userParam instead of a precursor link.
                // SpectrumList_Thermo.cpp:653-654 + 996-999.
                if (nonPrecursorMasterScanNumber > 0)
                    spec.Params.UserParams.Add(new UserParam("master scan number",
                        nonPrecursorMasterScanNumber.ToString(CultureInfo.InvariantCulture),
                        "xsd:positiveInteger"));
            }

            precursor.SelectedIons.Add(selectedIon);

            // ---- activation ----
            // cpp SpectrumList_Thermo.cpp:657-667. The activation bits already carry the
            // supplemental partner (see ReadActivations), so an ETciD scan emits BOTH
            // "electron transfer dissociation" and "collision-induced dissociation" plus the
            // supplemental term — and the collision energy / supplemental collision energy are
            // written unconditionally (cpp has no energy > 0 gate; an "sa" scan with no spelled
            // out supplemental energy legitimately reports supplemental collision energy = 0).
            var activationFlags = acts.Flags[i] == ActivationFlags.None
                ? ActivationFlags.Cid   // cpp: ActivationType_Unknown is assumed to be CID
                : acts.Flags[i];
            SetActivationType(activationFlags, acts.SaFlags, precursor.Activation);

            if ((activationFlags & (ActivationFlags.Cid | ActivationFlags.Hcd)) != 0)
                precursor.Activation.Set(CVID.MS_collision_energy, acts.Energies[i], CVID.UO_electronvolt);

            if (acts.SaFlags != ActivationFlags.None)
                precursor.Activation.Set(CVID.MS_supplemental_collision_energy, acts.SaEnergy, CVID.UO_electronvolt);

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
    private int FindPrecursorIndex(int fromIndex, int precursorMsLevel, double isolationMz,
        double precursorIsolationMz, int currentScan, out int nonPrecursorMasterScanNumber)
    {
        nonPrecursorMasterScanNumber = 0;

        // cpp bails out immediately when the run contains no scans at the precursor ms level
        // at all (targeted MSn runs) — SpectrumList_Thermo.cpp:974-976.
        if (precursorMsLevel >= 1 && precursorMsLevel <= (int)MSOrderType.Ms10
            && NumSpectraOfMsOrder((MSOrderType)precursorMsLevel) == 0)
            return -1;

        // pwiz C++ uses getTrailerExtraValueLong(...,"Master Scan Number:", -1) which returns
        // -1 only when the trailer key is ABSENT. When the key is present but the value is 0
        // (Thermo-default "no master" sentinel), cpp still enters master-scan mode and lets
        // the loop fail to match any preceding scan — effectively returning "not found".
        // Mirror that: only fall back to scan-window matching when the trailer is truly
        // missing.
        long masterScan = TryGetTrailerInt(currentScan, "Master Scan Number:", out long m) ? m : -1;

        for (int j = fromIndex - 1; j >= 0; j--)
        {
            var prev = _index[j];
            if (prev.Controller != Device.MS) continue;
            if (prev.MsOrder < MSOrderType.Ms) continue; // cpp: ie.msOrder < MSOrder_MS

            if (masterScan > -1)
            {
                if (masterScan == prev.Scan)
                {
                    // Master-scan hit: accept if it's at the right ms level, else keep looking
                    // (master scan can be a non-precursor triggering scan, e.g. ETD→HCD).
                    if (MsOrderToLevel(prev.MsOrder) == precursorMsLevel) return j;
                    nonPrecursorMasterScanNumber = (int)masterScan;
                    masterScan = -1;
                    continue;
                }
                if (masterScan > prev.Scan) return -1; // walked past the master; give up
                continue;
            }

            if (MsOrderToLevel(prev.MsOrder) != precursorMsLevel) continue;

            // MS3+ chain matching: the candidate must have isolated the m/z that OUR outer
            // filter mass names. cpp SpectrumList_Thermo.cpp:1009-1011.
            if (precursorIsolationMz != 0 && precursorIsolationMz != prev.IsolationMz) continue;

            // A zoom scan is only a valid precursor for a scan whose OUTER isolation m/z it
            // brackets; for an MS2 (precursorIsolationMz == 0) that test can never pass, which
            // is how cpp skips the narrow triple-play zoom scans and links back to the survey
            // MS1. cpp SpectrumList_Thermo.cpp:1014.
            double isolationMzToFind = prev.ScanMode == ScanModeType.Zoom ? precursorIsolationMz : isolationMz;

            bool mzInRange = false;
            var candFilter = _raw.Raw.GetFilterForScanNumber(prev.Scan);
            int rangeCount = candFilter.MassRangeCount;
            if ((prev.ScanMode == ScanModeType.Sim || prev.ScanMode == ScanModeType.Srm) && rangeCount > 1)
            {
                for (int r = 0; r < rangeCount && !mzInRange; r++)
                {
                    var range = candFilter.GetMassRange(r);
                    if (isolationMzToFind >= range.Low && isolationMzToFind <= range.High)
                        mzInRange = true;
                }
            }
            else
            {
                // cpp uses ScanInfo::lowMass()/highMass(), which on the x64 path come from the
                // scan STATISTICS (RawFile.cpp:1397-1398), not from the filter's mass ranges.
                try
                {
                    var stats = _raw.Raw.GetScanStatsForScanNumber(prev.Scan);
                    mzInRange = isolationMzToFind >= stats.LowMass && isolationMzToFind <= stats.HighMass;
                }
                catch { mzInRange = false; }
            }

            if (!mzInRange) continue;
            return j;
        }
        return -1;
    }

    /// <summary>
    /// Finds the preceding zoom scan (if any) whose m/z window brackets
    /// <paramref name="precursorIsolationMz"/>, so its trailer can supply the monoisotopic m/z
    /// and charge for a triple-play MSn. Port of cpp
    /// <c>SpectrumList_Thermo::findPrecursorZoomScan</c> (SpectrumList_Thermo.cpp:1085-1111).
    /// </summary>
    /// <returns>The scan NUMBER whose trailer supplies the values, or -1 when there is none.</returns>
    /// <remarks>
    /// <para>KNOWN CPP QUIRK, DELIBERATELY REPRODUCED. cpp locates the candidate by walking its
    /// spectrum index, but then loads the ScanInfo with <c>raw->getScanInfo(index+1)</c> — the
    /// zero-based LIST INDEX plus one, not the entry's <c>ie.scan</c>. Those agree only when no
    /// scan was dropped while building the index; as soon as the run contains SIM/SRM scans
    /// (which become chromatograms) the two drift apart by the number of scans skipped so far,
    /// and cpp ends up range-testing and trailer-reading a completely different scan.</para>
    /// <para>It fires on real data: <c>090701-LTQVelos-unittest-01.raw</c> drops 14 scans, so
    /// for its MS2 at scan 94 cpp matches the zoom scan at list index 91 but then reads scan 92
    /// — a Full MS1 whose 300-2000 window trivially contains the 459.21 precursor and whose
    /// "Charge State:" trailer is 0. Using the real scan number instead finds the zoom scan's
    /// charge and emits <c>MS:1000041 charge state = 2</c>, which the cpp reference does not
    /// have. Matching cpp is what parity means here, so this port does the same lookup; the
    /// upstream fix belongs in cpp.</para>
    /// </remarks>
    private int FindPrecursorZoomScan(int precursorMsLevel, double precursorIsolationMz, int fromIndex)
    {
        if (NumSpectraOfScanType(ScanModeType.Zoom) == 0) return -1;

        for (int j = fromIndex - 1; j >= 0; j--)
        {
            var prev = _index[j];
            if (prev.Controller != Device.MS) continue;
            if (prev.ScanMode != ScanModeType.Zoom || MsOrderToLevel(prev.MsOrder) != precursorMsLevel) continue;

            int scanNumberCppReads = j + 1; // see remarks: cpp's getScanInfo(index + 1)
            if (scanNumberCppReads < _raw.FirstScan || scanNumberCppReads > _raw.LastScan) continue;

            try
            {
                var stats = _raw.Raw.GetScanStatsForScanNumber(scanNumberCppReads);
                if (precursorIsolationMz < stats.LowMass || precursorIsolationMz > stats.HighMass) continue;
            }
            catch { continue; }

            return scanNumberCppReads;
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

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_ownsRaw) _raw.Dispose();
        base.DisposeCore();
    }
}
