using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.FilterEnums;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Thermo;

/// <summary>
/// <see cref="IChromatogramList"/> backed by a Thermo <see cref="ThermoRawFile"/>. Emits the
/// document-level TIC plus one SIM chromatogram per unique (polarity, Q1) SIM filter when
/// <c>simAsSpectra</c> is false.
/// </summary>
/// <remarks>Port of pwiz::msdata::ChromatogramList_Thermo.</remarks>
public sealed class ChromatogramList_Thermo : ChromatogramListBase
{
    private readonly ThermoRawFile _raw;
    private readonly bool _globalChromatogramsAreMs1Only;

    /// <summary>cpp's scan filter for an MS1-only global chromatogram (ChromatogramList_Thermo.cpp:400).</summary>
    private const string GLOBAL_MS1_FILTER = "Full ms";
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing id emitted as the <c>defaultDataProcessingRef</c>. Set by <see cref="Reader_Thermo"/>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>True when this list contains at least one SIM chromatogram.</summary>
    /// <remarks>Not what drives the fileContent CV any more: cpp keys
    /// <c>MS:1000472 selected ion monitoring chromatogram</c> off the SPECTRUM list's SIM scan
    /// census (Reader_Thermo.cpp:232-235), which is non-zero even when no chromatogram could be
    /// built from those scans. See <c>Reader_Thermo.FillFileContent</c>.</remarks>
    public bool HasSimChromatograms { get; }

    /// <summary>True when this list contains at least one SRM chromatogram.
    /// See the remarks on <see cref="HasSimChromatograms"/> for why fileContent no longer uses it.</summary>
    public bool HasSrmChromatograms { get; }

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public CVID Kind;
        // SIM/SRM-specific:
        public double Q1;
        public double Q3;          // SRM only — product m/z
        public double HalfWidth;   // SIM: ½ Q1 isolation width; SRM: ½ Q3 product window
        public PolarityType Polarity;
        /// <summary>The scan-filter string this entry came from. cpp reconstructs a ScanInfo
        /// from it per SRM chromatogram (<c>getScanInfoFromFilterString</c>).</summary>
        public string Filter = string.Empty;
        // Non-MS-device sources (Pump Pressure / UV / CAD):
        public Device Device = Device.MS;
        public int DeviceChannel;  // 1-based
    }

    /// <summary>Creates a chromatogram list backed by the given Thermo raw file.</summary>
    /// <remarks>
    /// <paramref name="globalChromatogramsAreMs1Only"/> restricts the file-level TIC to MS1. cpp
    /// expresses this as a scan filter - it hands addChromatogram the string "Full ms" instead of
    /// "" (ChromatogramList_Thermo.cpp:400-401) - and the SDK then returns one point per matching
    /// scan rather than one per scan of any level. Without it a 99-scan file reports 99 TIC points
    /// where Skyline, which asks for an MS1-only TIC, expects the 30 MS1 scans.
    /// </remarks>
    public ChromatogramList_Thermo(ThermoRawFile raw, bool simAsSpectra = false, bool srmAsSpectra = false,
                                   bool globalChromatogramsAreMs1Only = false)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _globalChromatogramsAreMs1Only = globalChromatogramsAreMs1Only;
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });

        // Build the index under InvariantCulture. The Thermo SDK renders scan filters
        // (GetFilterForScanNumber(...).ToString()) using the current thread culture, and those
        // strings are matched -- as dictionary keys -- against the period-formatted
        // GetAutoFilters() output. Under a comma-decimal culture such as French the two forms
        // diverge, the lookup misses, and no SIM/SRM chromatograms get built. GetChromatogram
        // wraps extraction for the same reason.
        bool hasSim = false, hasSrm = false;
        RunInvariant(() =>
        {
            (hasSim, hasSrm) = BuildFilterIndex(simAsSpectra, srmAsSpectra);

            // Analog/UV controllers: LC pump pressure, UV absorbance, CAD, etc. pwiz C++ iterates
            // these and picks a CV term based on the device's Y-axis label.
            BuildNonMsDeviceIndex();

            // Restore MS selection so subsequent spectrum/chromatogram reads see the MS device.
            try { _raw.Raw.SelectInstrument(Device.MS, 1); } catch { }
        });
        HasSimChromatograms = hasSim;
        HasSrmChromatograms = hasSrm;
    }

    private void BuildNonMsDeviceIndex()
    {
        foreach (var device in new[] { Device.Analog, Device.UV, Device.Pda, Device.MSAnalog })
        {
            int count = 0;
            try { count = _raw.Raw.GetInstrumentCountOfType(device); } catch { }
            for (int n = 1; n <= count; n++)
            {
                try { _raw.Raw.SelectInstrument(device, n); }
                catch { continue; }

                InstrumentData info;
                try { info = _raw.Raw.GetInstrumentData(); }
                catch { continue; }

                string axisY = info.AxisLabelY ?? string.Empty;
                var units = info.Units;
                (string idPrefix, CVID kind)? classified = null;

                bool isAbsorbance = units == DataUnits.AbsorbanceUnits
                    || units == DataUnits.MilliAbsorbanceUnits
                    || units == DataUnits.MicroAbsorbanceUnits;

                // Order matters: PDA-device first (otherwise its absorbance units would route
                // it through the UV branch and emit a duplicate "UV n" id, missing the PDA
                // chromatogram entirely). Then UV (other absorbance), CAD (pA), pressure.
                if (device == Device.Pda)
                {
                    classified = ("PDA ", CVID.MS_absorption_chromatogram);
                }
                else if (isAbsorbance && (axisY.Length == 0 || axisY.StartsWith("UV", StringComparison.OrdinalIgnoreCase)))
                {
                    classified = ("UV ", CVID.MS_emission_chromatogram);
                }
                else if (axisY.EndsWith("pA", StringComparison.OrdinalIgnoreCase))
                {
                    classified = ("CAD ", CVID.MS_TIC_chromatogram);
                }
                else if (axisY.Contains("Pressure", StringComparison.OrdinalIgnoreCase))
                {
                    classified = ("Pump Pressure ", CVID.MS_pressure_chromatogram);
                }

                if (classified is null) continue;
                _index.Add(new IndexEntry
                {
                    Index = _index.Count,
                    Id = classified.Value.idPrefix + n.ToString(CultureInfo.InvariantCulture),
                    Kind = classified.Value.kind,
                    Device = device,
                    DeviceChannel = n,
                });
            }
        }
    }

    /// <summary>
    /// Builds the SIM and SRM chromatogram index in a single pass over the SDK's auto filters
    /// (<c>GetAutoFilters</c>), mirroring pwiz C++ <c>ChromatogramList_Thermo.cpp:406-518</c>
    /// which loops once over <c>RawFile::getFilters()</c> and switches on the filter's scan type.
    /// One chromatogram is emitted per (filter, bracketed m/z window) pair.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately does NOT collapse windows that repeat across filters. A multiplexed
    /// (msx) acquisition schedules the same quadrupole window in several scan events, so the same
    /// "SIM SIC q1" / "SRM SIC q1,q3" id can be produced more than once; cpp's
    /// <c>addChromatogram</c> push_backs unconditionally, so the reference output contains those
    /// repeats and de-duplicating here desynchronizes every downstream chromatogram index.</para>
    /// <para>Matches pwiz C++ <c>polarityStringForFilter</c> — only prepends "- " for negative
    /// polarity; positive mode has an empty prefix for backward-compat (see
    /// ChromatogramListBase.hpp line 53). The bracketed m/z range comes from the filter
    /// STRING (clean 4-decimal doubles); <c>filter.GetMassRange(j).Low/High</c> would return
    /// float-extended doubles that print at 10 sig figs and diverge from the cpp reference.</para>
    /// </remarks>
    private (bool HasSim, bool HasSrm) BuildFilterIndex(bool simAsSpectra, bool srmAsSpectra)
    {
        bool hasSim = false, hasSrm = false;
        foreach (var filterString in _raw.Raw.GetAutoFilters())
        {
            if (filterString is null) continue;

            // GetAutoFilters returns every filter type (Full, SIM, SRM, ...). cpp classifies
            // structurally via scanInfo->scanType(); the textual check is equivalent and avoids
            // re-parsing the filter through the SDK. SIM appears both as single-window
            // ("SIM ms [a-b]") and multiplexed ("SIM msx ms [a-b, c-d, ...]"); "SRM ms" matches
            // "SRM ms2", "SRM ms3", etc. — each MS-order is a valid transition filter.
            bool isSim = filterString.Contains(" SIM ms [", StringComparison.Ordinal)
                         || filterString.Contains(" SIM msx ms [", StringComparison.Ordinal);
            bool isSrm = !isSim && filterString.Contains(" SRM ms", StringComparison.Ordinal);
            if (!isSim && !isSrm) continue;

            // cpp breaks out of the scan-type switch (adding nothing) when the caller asked for
            // these scans as spectra instead.
            if (isSim && simAsSpectra) continue;
            if (isSrm && srmAsSpectra) continue;

            var stringRanges = ParseSimMassRanges(filterString);
            if (stringRanges.Count == 0) continue;

            var pol = ParsePolarity(filterString);
            string polStr = pol == PolarityType.Negative ? "- " : "";

            if (isSim)
            {
                foreach (var (lo, hi) in stringRanges)
                {
                    double q1 = (lo + hi) / 2.0;
                    string q1Str = q1.ToString("G10", CultureInfo.InvariantCulture);
                    _index.Add(new IndexEntry
                    {
                        Index = _index.Count,
                        Id = polStr + "SIM SIC " + q1Str,
                        Kind = CVID.MS_SIM_chromatogram,
                        Q1 = q1,
                        HalfWidth = (hi - lo) / 2.0,
                        Polarity = pol,
                    });
                    hasSim = true;
                }
            }
            else
            {
                // SRM filter format: "[polarity] [calibrant?] SRM ms<n> <Q1> [<lo>-<hi>, ...]"
                double? q1 = ParseSrmQ1(filterString);
                if (q1 is null) continue;
                string q1Str = q1.Value.ToString("G10", CultureInfo.InvariantCulture);

                foreach (var (lo, hi) in stringRanges)
                {
                    double scanRange = hi - lo;
                    if (scanRange > MaxSrmScanRange) continue; // not a real transition
                    double filterQ3 = (lo + hi) / 2.0;
                    string q3Str = filterQ3.ToString("G10", CultureInfo.InvariantCulture);
                    _index.Add(new IndexEntry
                    {
                        Index = _index.Count,
                        Id = polStr + "SRM SIC " + q1Str + "," + q3Str,
                        Kind = CVID.MS_SRM_chromatogram,
                        Q1 = q1.Value,
                        Q3 = filterQ3,
                        HalfWidth = scanRange / 2.0,
                        Polarity = pol,
                        Filter = filterString,
                    });
                    hasSrm = true;
                }
            }
        }
        return (hasSim, hasSrm);
    }

    /// <summary>
    /// Reads the polarity out of a scan-filter string. cpp reconstructs a ScanInfo from the
    /// filter string (<c>getScanInfoFromFilterString</c>) and reads <c>polarityType()</c> off it;
    /// ask the SDK to do the same parse, falling back to the bare "+"/"-" token the filter
    /// grammar puts between the analyzer and the ionization mode.
    /// </summary>
    private PolarityType ParsePolarity(string filterString)
    {
        try
        {
            var parsed = _raw.Raw.GetFilterFromString(filterString);
            if (parsed is not null)
                return parsed.Polarity;
        }
        catch { /* unparseable filter — fall through to the textual scan */ }

        foreach (var token in filterString.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length != 1) continue;
            if (token[0] == '+') return PolarityType.Positive;
            if (token[0] == '-') return PolarityType.Negative;
        }
        // cpp leaves polarityType CVID_Unknown here: no polarity cvParam and no "- " id prefix.
        return PolarityType.Any;
    }

    /// <summary>
    /// Widest bracketed window still treated as a real SRM transition (matches
    /// <c>MAX_SRM_SCAN_RANGE</c> in cpp Reader_Thermo_Detail.hpp). Anything wider is skipped
    /// here, which is why <see cref="SpectrumList_Thermo"/> has to keep such scans as spectra.
    /// </summary>
    internal const double MaxSrmScanRange = 1.0;

    /// <summary>Parses the precursor m/z (Q1) out of an SRM filter string like
    /// <c>"+ c NSI SRM ms2 572.792 [724.375-724.377, ...]"</c>. Returns null when the
    /// string isn't shaped like an SRM filter.</summary>
    private static double? ParseSrmQ1(string filterString)
    {
        // Find the "SRM ms<n>" token and read the next whitespace-delimited number.
        int srmIdx = filterString.IndexOf(" SRM ms", StringComparison.Ordinal);
        if (srmIdx < 0) return null;
        // Skip past "SRM ms" and the digit(s) following.
        int p = srmIdx + " SRM ms".Length;
        while (p < filterString.Length && char.IsDigit(filterString[p])) p++;
        // Skip whitespace.
        while (p < filterString.Length && char.IsWhiteSpace(filterString[p])) p++;
        // Read the precursor m/z (digits + dot, possibly with sign).
        int start = p;
        while (p < filterString.Length && (char.IsDigit(filterString[p]) || filterString[p] == '.'))
            p++;
        if (p == start) return null;
        if (!double.TryParse(filterString.AsSpan(start, p - start), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double q1))
            return null;
        return q1;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        var entry = _index[index];
        var chrom = new Chromatogram
        {
            Index = entry.Index,
            Id = entry.Id,
        };
        chrom.Params.Set(entry.Kind);

        if (!getBinaryData) return chrom;

        // Polarity cvParam for SIM/SRM chromatograms matches pwiz C++ ref output.
        if (entry.Kind == CVID.MS_SIM_chromatogram || entry.Kind == CVID.MS_SRM_chromatogram)
        {
            if (entry.Polarity == PolarityType.Positive)
                chrom.Params.Set(CVID.MS_positive_scan);
            else if (entry.Polarity == PolarityType.Negative)
                chrom.Params.Set(CVID.MS_negative_scan);
        }

        // The Thermo RawFileReader SDK parses the SRM/SIM filter strings it is handed
        // (via GetChromatogramDataEx) using the current thread culture. Under a comma-decimal
        // culture such as French it rejects the period-formatted filters pwiz builds --
        // "InvalidFilterFormatException: SRM ms2 363.706 [455.239-455.241]". Force
        // InvariantCulture around the SDK calls so extraction is culture-independent.
        return RunInvariant(() =>
        {
            if (entry.Device != Device.MS)
                return FillNonMsDeviceChromatogram(chrom, entry);

            return entry.Kind switch
            {
                CVID.MS_SIM_chromatogram => FillSimChromatogram(chrom, entry),
                CVID.MS_SRM_chromatogram => FillSrmChromatogram(chrom, entry),
                _ => FillTicChromatogram(chrom),
            };
        });
    }

    private Chromatogram FillNonMsDeviceChromatogram(Chromatogram chrom, IndexEntry entry)
    {
        try
        {
            _raw.Raw.SelectInstrument(entry.Device, entry.DeviceChannel);

            // pwiz C++ maps the controller to a specific TraceType:
            //   UV / Analog (pressure, CAD) -> TraceType.ChannelA (pwiz "Type_ECD" = 31)
            //   PDA -> TraceType.TotalAbsorbance (pwiz "Type_TotalScan" = 22)
            TraceType trace = entry.Kind switch
            {
                CVID.MS_absorption_chromatogram => TraceType.TotalAbsorbance,
                _ => TraceType.ChannelA,
            };
            var settings = new ChromatogramTraceSettings(trace);
            ThermoFisher.CommonCore.Data.Interfaces.IChromatogramData? data;
            try
            {
                data = _raw.Raw.GetChromatogramDataEx(new[] { settings }, -1, -1, new MassOptions());
            }
            catch (ArgumentException)
            {
                // The Thermo SDK throws "Unknown UV/PDA packet type" / "Unknown channel" /
                // similar ArgumentException for some legacy non-MS device data formats it
                // can't decode (e.g. older PDA packet layouts). pwiz C++ silently skips these
                // devices and emits an empty chromatogram so the file still converts. Mirror
                // that behavior — surface a lone empty chromatogram rather than aborting the
                // whole conversion.
                return chrom;
            }
            if (!(data?.PositionsArray?.Length > 0) || data.PositionsArray[0] is not { } times
                || data.IntensitiesArray?[0] is not { } intensities)
            {
                return chrom;
            }

            if (entry.Kind == CVID.MS_pressure_chromatogram)
            {
                // Pressure traces repeat the same y value for long runs of x values; dedupe
                // everything except the first/last and transitions. Also convert bar -> pascal
                // because the ontology doesn't have a bar term (pwiz C++ does the same).
                var (dedupTimes, dedupIntensities) = DedupePressureTrace(times, intensities, scaleFactor: 1e5);
                chrom.DefaultArrayLength = dedupTimes.Length;
                chrom.BinaryDataArrays.Add(MakeArray(dedupTimes, CVID.MS_time_array, CVID.UO_minute));
                chrom.BinaryDataArrays.Add(MakeArray(dedupIntensities, CVID.MS_intensity_array, CVID.UO_pascal));
            }
            else
            {
                CVID intensityUnit = entry.Kind switch
                {
                    CVID.MS_absorption_chromatogram => CVID.UO_absorbance_unit,
                    CVID.MS_emission_chromatogram => CVID.UO_absorbance_unit,
                    CVID.MS_TIC_chromatogram => CVID.UO_picoampere, // CAD -> pA
                    _ => CVID.MS_number_of_detector_counts,
                };
                // cpp ChromatogramList_Thermo.cpp:136-140 scales picoampere intensities by 1e-6:
                // "Thermo seems to store CAD intensities as attoAmps but shows them as picoAmps
                // in QualBrowser". Without it a CAD trace is off by exactly 1e6.
                if (intensityUnit == CVID.UO_picoampere)
                {
                    intensities = (double[])intensities.Clone();
                    for (int i = 0; i < intensities.Length; i++) intensities[i] *= 1e-6;
                }
                chrom.DefaultArrayLength = times.Length;
                chrom.BinaryDataArrays.Add(MakeArray(times, CVID.MS_time_array, CVID.UO_minute));
                chrom.BinaryDataArrays.Add(MakeArray(intensities, CVID.MS_intensity_array, intensityUnit));
            }
        }
        finally
        {
            // Always restore the MS instrument so subsequent spectrum reads work.
            try { _raw.Raw.SelectInstrument(Device.MS, 1); } catch { }
        }
        return chrom;
    }

    private static (double[] Times, double[] Intensities) DedupePressureTrace(
        double[] times, double[] intensities, double scaleFactor)
    {
        int n = Math.Min(times.Length, intensities.Length);
        if (n == 0) return (Array.Empty<double>(), Array.Empty<double>());
        if (n <= 2)
        {
            var ti = new double[n];
            var ii = new double[n];
            for (int k = 0; k < n; k++) { ti[k] = times[k]; ii[k] = intensities[k] * scaleFactor; }
            return (ti, ii);
        }
        var outTimes = new List<double>(n);
        var outIntensities = new List<double>(n);
        outTimes.Add(times[0]); outIntensities.Add(intensities[0] * scaleFactor);
        for (int i = 1; i + 1 < n; i++)
        {
            double prev = intensities[i - 1], cur = intensities[i], next = intensities[i + 1];
            if (cur != prev || cur != next)
            {
                outTimes.Add(times[i]);
                outIntensities.Add(cur * scaleFactor);
            }
        }
        outTimes.Add(times[n - 1]); outIntensities.Add(intensities[n - 1] * scaleFactor);
        return (outTimes.ToArray(), outIntensities.ToArray());
    }

    private Chromatogram FillTicChromatogram(Chromatogram chrom)
    {
        // cpp's global filter: "Full ms" for an MS1-only TIC, "" (no filter) for all levels.
        var settings = new ChromatogramTraceSettings(TraceType.TIC);
        if (_globalChromatogramsAreMs1Only)
            settings.Filter = GLOBAL_MS1_FILTER;
        var data = _raw.Raw.GetChromatogramDataEx(new[] { settings }, -1, -1, new MassOptions());
        if (data?.PositionsArray?.Length > 0 && data.PositionsArray[0] is { } times
            && data.IntensitiesArray?[0] is { } intensities)
        {
            chrom.DefaultArrayLength = times.Length;
            chrom.BinaryDataArrays.Add(MakeArray(times, CVID.MS_time_array, CVID.UO_minute));
            chrom.BinaryDataArrays.Add(MakeArray(intensities, CVID.MS_intensity_array, CVID.MS_number_of_detector_counts));

            // Third array: ms level per time point, matches pwiz C++ ChromatogramList_Thermo.
            // cpp writes getMSOrder() RAW (ChromatogramList_Thermo.cpp:133), not the 1..10 ms
            // level - and Thermo's MSOrderType is negative for the scan kinds that are not a
            // plain MSn: Ng = -3, Nl = -2, Par = -1. A neutral-loss run therefore reads -2 in
            // cpp where our translated level said 2.
            var msArr = new IntegerDataArray();
            msArr.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);
            for (int i = 0; i < times.Length; i++)
            {
                try
                {
                    int sn = _raw.Raw.ScanNumberFromRetentionTime(times[i]);
                    msArr.Data.Add((int)_raw.Raw.GetFilterForScanNumber(sn).MSOrder);
                }
                catch { msArr.Data.Add(0); }
            }
            chrom.IntegerDataArrays.Add(msArr);
        }
        return chrom;
    }

    /// <summary>
    /// Activation type + energy for an SRM transition, read back out of its scan-filter string.
    /// Port of cpp <c>ChromatogramList_Thermo.cpp:175-178</c>
    /// (<c>getScanInfoFromFilterString(ci.filter)-&gt;precursorActivationType(0)</c>, with
    /// <c>ActivationType_Unknown</c> assumed to be CID). cpp's ScanInfo-from-filter-string path
    /// goes through the same CommonCore parser we call here (RawFile.cpp:1279-1293).
    /// </summary>
    private (ActivationType Activation, double Energy) ReadSrmActivation(string filterString)
    {
        if (!string.IsNullOrEmpty(filterString))
        {
            try
            {
                var parsed = _raw.Raw.GetFilterFromString(filterString);
                if (parsed is not null && parsed.MassCount > 0)
                {
                    var act = parsed.GetActivation(0);
                    double energy = parsed.GetEnergy(0);
                    return (act == ActivationType.Any ? ActivationType.CollisionInducedDissociation : act, energy);
                }
            }
            catch { /* unparseable filter — fall through to cpp's CID assumption */ }
        }
        return (ActivationType.CollisionInducedDissociation, 0.0);
    }

    private Chromatogram FillSimChromatogram(Chromatogram chrom, IndexEntry entry)
    {
        // Precursor isolation window (matches pwiz C++ ChromatogramList_Thermo.cpp:211-213).
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, entry.Q1, CVID.MS_m_z);
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, entry.HalfWidth, CVID.MS_m_z);
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, entry.HalfWidth, CVID.MS_m_z);

        // Ask Thermo for the chromatogram over the SIM's Q1 ± halfWidth window — mirrors C++
        // getChromatogramData(Type_MassRange, "SIM ms [...]", Q1-hw, Q1+hw, ...).
        // Pass the abbreviated "SIM ms [LO-HI]" filter via the (filter, ranges) constructor —
        // this is what cpp's RawFileThreadImpl::getChromatogramData does. The constructor
        // accepts the abbreviated form (the property setter rejects it as
        // InvalidFilterFormatException). The SDK uses substring matching against scan
        // filters, so single-window SIM, multi-window SIM (msx), and any overlapping window
        // contribute data — matches the cpp reference output.
        string lo = (entry.Q1 - entry.HalfWidth).ToString("G10", CultureInfo.InvariantCulture);
        string hi = (entry.Q1 + entry.HalfWidth).ToString("G10", CultureInfo.InvariantCulture);
        string abbreviatedFilter = $"SIM ms [{lo}-{hi}]";
        var ranges = new[] { new ThermoFisher.CommonCore.Data.Business.Range(entry.Q1 - entry.HalfWidth, entry.Q1 + entry.HalfWidth) };
        var settings = new ChromatogramTraceSettings(abbreviatedFilter, ranges)
        {
            Trace = TraceType.MassRange,
        };
        var data = GetFilteredChromatogramData(settings);
        if (data?.PositionsArray?.Length > 0 && data.PositionsArray[0] is { } times
            && data.IntensitiesArray?[0] is { } intensities)
        {
            chrom.DefaultArrayLength = times.Length;
            chrom.BinaryDataArrays.Add(MakeArray(times, CVID.MS_time_array, CVID.UO_minute));
            chrom.BinaryDataArrays.Add(MakeArray(intensities, CVID.MS_intensity_array, CVID.MS_number_of_detector_counts));
        }
        return chrom;
    }

    /// <summary>
    /// Pulls the (time, intensity) trace for one SRM transition. Sets the precursor isolation
    /// (Q1, no offsets), the activation carried by the transition's scan filter, and the product
    /// isolation window (Q3 ± halfWidth). Mirrors pwiz C++
    /// <c>ChromatogramList_Thermo.cpp:172-206</c>.
    /// </summary>
    private Chromatogram FillSrmChromatogram(Chromatogram chrom, IndexEntry entry)
    {
        // Precursor side: the target m/z + activation. cpp rebuilds a ScanInfo from the
        // transition's filter string (getScanInfoFromFilterString) and reads the activation type
        // and energy off it; an unknown activation is assumed to be CID.
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, entry.Q1, CVID.MS_m_z);

        var (srmActivation, srmEnergy) = ReadSrmActivation(entry.Filter);
        switch (srmActivation)
        {
            case ActivationType.HigherEnergyCollisionalDissociation:
                chrom.Precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation); break;
            case ActivationType.ElectronTransferDissociation:
                chrom.Precursor.Activation.Set(CVID.MS_electron_transfer_dissociation); break;
            case ActivationType.ElectronCaptureDissociation:
                chrom.Precursor.Activation.Set(CVID.MS_electron_capture_dissociation); break;
            case ActivationType.PQD:
                chrom.Precursor.Activation.Set(CVID.MS_pulsed_q_dissociation); break;
            case ActivationType.MultiPhotonDissociation:
            case ActivationType.UltraVioletPhotoDissociation:
                chrom.Precursor.Activation.Set(CVID.MS_photodissociation); break;
            default:
                chrom.Precursor.Activation.Set(CVID.MS_collision_induced_dissociation); break;
        }

        // cpp ChromatogramList_Thermo.cpp:184-185 writes this with NO unit argument, so the
        // cvParam carries no unitAccession — unlike the spectrum-level collision energy, which
        // cpp does tag with UO_electronvolt. Emitting eV here made every SRM transition in
        // every TSQ file differ from the cpp reference.
        if (srmActivation == ActivationType.CollisionInducedDissociation)
            chrom.Precursor.Activation.Set(CVID.MS_collision_energy, srmEnergy);

        // Product side: the Q3 transition target plus the SDK-reported half-width on each
        // side. cpp stores the original filter half-width as the offset (called q3Offset).
        chrom.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, entry.Q3, CVID.MS_m_z);
        chrom.Product.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, entry.HalfWidth, CVID.MS_m_z);
        chrom.Product.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, entry.HalfWidth, CVID.MS_m_z);

        // Same trick as FillSimChromatogram: pass the abbreviated filter via the
        // (filter, ranges) constructor — RawFileReader's strict property setter rejects this
        // form but the constructor accepts it for substring-matching against scan filters.
        string lo = (entry.Q3 - entry.HalfWidth).ToString("G10", CultureInfo.InvariantCulture);
        string hi = (entry.Q3 + entry.HalfWidth).ToString("G10", CultureInfo.InvariantCulture);
        string q1Str = entry.Q1.ToString("G10", CultureInfo.InvariantCulture);
        string polarity = entry.Polarity == PolarityType.Negative ? "- " : "";
        string abbreviatedFilter = $"{polarity}SRM ms2 {q1Str} [{lo}-{hi}]";
        var ranges = new[] { new ThermoFisher.CommonCore.Data.Business.Range(entry.Q3 - entry.HalfWidth, entry.Q3 + entry.HalfWidth) };
        var settings = new ChromatogramTraceSettings(abbreviatedFilter, ranges)
        {
            Trace = TraceType.MassRange,
        };
        var data = GetFilteredChromatogramData(settings);
        if (data?.PositionsArray?.Length > 0 && data.PositionsArray[0] is { } times
            && data.IntensitiesArray?[0] is { } intensities)
        {
            chrom.DefaultArrayLength = times.Length;
            chrom.BinaryDataArrays.Add(MakeArray(times, CVID.MS_time_array, CVID.UO_minute));
            chrom.BinaryDataArrays.Add(MakeArray(intensities, CVID.MS_intensity_array, CVID.MS_number_of_detector_counts));
        }
        return chrom;
    }

    /// <summary>
    /// Runs one mass-range trace, honoring the scan filter carried by <paramref name="settings"/>.
    /// </summary>
    /// <remarks>
    /// Must be <c>GetChromatogramData</c>, not <c>GetChromatogramDataEx</c>: the Ex overload
    /// silently ignores the settings' Filter and extracts the mass range from every scan in the
    /// file. For SRM that merges transitions that share a Q3 but differ in Q1 (e.g.
    /// "SRM SIC 112.039,68.049" and "SRM SIC 112.087,68.049" both came back with the union's
    /// 236 points instead of 119 and 117). cpp calls the same non-Ex overload —
    /// RawFile.cpp:2858 <c>raw_-&gt;GetChromatogramData(settings, firstScan, lastScan)</c>.
    /// (-1, -1 is the SDK's documented "all data", equivalent to cpp's first/last scan.)
    /// </remarks>
    private ThermoFisher.CommonCore.Data.Interfaces.IChromatogramData GetFilteredChromatogramData(
        ChromatogramTraceSettings settings) =>
        _raw.Raw.GetChromatogramData(
            new ThermoFisher.CommonCore.Data.Interfaces.IChromatogramSettings[] { settings }, -1, -1);

    /// <summary>
    /// Parses bracketed m/z ranges from a Thermo SIM filter string. Examples:
    /// <c>"FTMS + p NSI SIM ms [337.9372-339.4372]"</c> → one range (337.9372, 339.4372);
    /// <c>"FTMS + p NSI SIM ms [310.5-311.5, 400.0-401.0]"</c> → two ranges (multiplexed SIM).
    /// Returns clean doubles parsed from the textual digits, avoiding the float-extension
    /// noise that <c>filter.GetMassRange(j).Low/High</c> introduces.
    /// </summary>
    private static List<(double Low, double High)> ParseSimMassRanges(string filterString)
    {
        var ranges = new List<(double, double)>();
        int open = filterString.IndexOf('[', StringComparison.Ordinal);
        int close = filterString.IndexOf(']', StringComparison.Ordinal);
        if (open < 0 || close < 0 || close < open) return ranges;
        string inner = filterString.Substring(open + 1, close - open - 1);
        foreach (var part in inner.Split(','))
        {
            int dash = part.IndexOf('-', StringComparison.Ordinal);
            if (dash < 0) continue;
            string lo = part.Substring(0, dash).Trim();
            string hi = part.Substring(dash + 1).Trim();
            if (double.TryParse(lo, NumberStyles.Float, CultureInfo.InvariantCulture, out double loVal)
                && double.TryParse(hi, NumberStyles.Float, CultureInfo.InvariantCulture, out double hiVal))
                ranges.Add((loVal, hiVal));
        }
        return ranges;
    }

    private static BinaryDataArray MakeArray(double[] values, CVID kind, CVID units)
    {
        var arr = new BinaryDataArray();
        arr.Set(kind, "", units);
        arr.Data.AddRange(values);
        return arr;
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the thread culture forced to InvariantCulture,
    /// restoring the caller's culture afterward. The Thermo RawFileReader SDK renders and
    /// parses scan-filter strings (e.g. "SRM ms2 363.706 [455.239-455.241]") using the current
    /// thread culture; under a comma-decimal culture such as French those strings use commas and
    /// no longer round-trip against the period-formatted filters pwiz builds and matches on.
    /// </summary>
    private static void RunInvariant(Action action)
    {
        var savedCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            action();
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = savedCulture;
        }
    }

    private static T RunInvariant<T>(Func<T> func)
    {
        T result = default!;
        RunInvariant(() => { result = func(); });
        return result;
    }
}
