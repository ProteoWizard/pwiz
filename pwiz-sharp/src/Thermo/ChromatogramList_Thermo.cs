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
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing id emitted as the <c>defaultDataProcessingRef</c>. Set by <see cref="Reader_Thermo"/>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>True when this list contains at least one SIM chromatogram (callers can set the fileContent CV).</summary>
    public bool HasSimChromatograms { get; }

    /// <summary>True when this list contains at least one SRM chromatogram (callers can set the fileContent CV).</summary>
    public bool HasSrmChromatograms { get; }

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public CVID Kind;
        // SIM/SRM-specific:
        public double Q1;
        public double Q3;          // SRM only — product m/z
        public double HalfWidth;   // SIM: ½ Q1 isolation width; SRM: ½ Q3 product window
        public PolarityType Polarity;
        public List<int> Scans = new();
        // Non-MS-device sources (Pump Pressure / UV / CAD):
        public Device Device = Device.MS;
        public int DeviceChannel;  // 1-based
    }

    /// <summary>Creates a chromatogram list backed by the given Thermo raw file.</summary>
    public ChromatogramList_Thermo(ThermoRawFile raw, bool simAsSpectra = false, bool srmAsSpectra = false)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });

        if (!simAsSpectra)
            HasSimChromatograms = BuildSimIndex();

        if (!srmAsSpectra)
            HasSrmChromatograms = BuildSrmIndex();

        // Analog/UV controllers: LC pump pressure, UV absorbance, CAD, etc. pwiz C++ iterates
        // these and picks a CV term based on the device's Y-axis label.
        BuildNonMsDeviceIndex();

        // Restore MS selection so subsequent spectrum/chromatogram reads see the MS device.
        try { _raw.Raw.SelectInstrument(Device.MS, 1); } catch { }
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
    /// Builds one chromatogram per unique SIM filter the SDK reports (via
    /// <c>GetAutoFilters</c>) and matches pwiz C++ <c>ChromatogramList_Thermo.cpp:404-503</c>
    /// which iterates <c>RawFile::getFilters()</c>. Iterating auto-filters (rather than each
    /// scan) lets the SDK collapse near-overlapping SIM windows that target the same
    /// quadrupole position — scan-iteration mistakenly emits both as separate chromatograms.
    /// </summary>
    /// <remarks>
    /// Matches pwiz C++ <c>polarityStringForFilter</c> — only prepends "- " for negative
    /// polarity; positive mode has an empty prefix for backward-compat (see
    /// ChromatogramListBase.hpp line 53). The bracketed m/z range comes from the filter
    /// STRING (clean 4-decimal doubles); <c>filter.GetMassRange(j).Low/High</c> would return
    /// float-extended doubles that print at 10 sig figs and diverge from the cpp reference.
    /// </remarks>
    private bool BuildSimIndex()
    {
        var byKey = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
        var autoFilters = _raw.Raw.GetAutoFilters();
        // Map filter -> first scan number, so FillSimChromatogram can look up a representative
        // scan to feed back to the SDK's GetChromatogramDataEx (the new RawFileReader rejects
        // the abbreviated "SIM ms [..]" filter, so we hand it the canonical filter from a real
        // SIM scan via Scans[0]).
        var firstScanByFilter = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int scan = _raw.FirstScan; scan <= _raw.LastScan; scan++)
        {
            var f = _raw.Raw.GetFilterForScanNumber(scan);
            if (f.ScanMode != ScanModeType.Sim) continue;
            string fs = f.ToString();
            if (!firstScanByFilter.ContainsKey(fs))
                firstScanByFilter[fs] = scan;
        }

        foreach (var filterString in autoFilters)
        {
            if (filterString is null) continue;
            // GetAutoFilters returns every filter type (Full, SIM, MRM, etc.); pick out SIM
            // entries — both single-window ("SIM ms [a-b]") and multiplexed ("SIM msx ms [a-b,
            // c-d, ...]") variants. cpp classifies structurally via
            // scanInfo->scanType()==ScanType_SIM; the textual check is equivalent and avoids
            // re-parsing the filter through the SDK.
            if (!filterString.Contains(" SIM ms [", StringComparison.Ordinal)
                && !filterString.Contains(" SIM msx ms [", StringComparison.Ordinal)) continue;
            if (!firstScanByFilter.TryGetValue(filterString, out int sampleScan)) continue;

            var stringRanges = ParseSimMassRanges(filterString);
            if (stringRanges.Count == 0) continue;

            // Polarity sign appears as " + " or " - " in the filter; reuse the per-scan filter
            // object since it already exposes Polarity as an enum.
            var sampleFilter = _raw.Raw.GetFilterForScanNumber(sampleScan);
            var pol = sampleFilter.Polarity;
            string polStr = pol == PolarityType.Negative ? "- " : "";

            foreach (var (lo, hi) in stringRanges)
            {
                double q1 = (lo + hi) / 2.0;
                double halfWidth = (hi - lo) / 2.0;
                string key = polStr + q1.ToString("G10", CultureInfo.InvariantCulture);
                if (!byKey.TryGetValue(key, out var entry))
                {
                    string id = polStr + "SIM SIC " + q1.ToString("G10", CultureInfo.InvariantCulture);
                    entry = new IndexEntry
                    {
                        Id = id,
                        Kind = CVID.MS_SIM_chromatogram,
                        Q1 = q1,
                        HalfWidth = halfWidth,
                        Polarity = pol,
                    };
                    byKey.Add(key, entry);
                }
                entry.Scans.Add(sampleScan);
            }
        }
        if (byKey.Count == 0) return false;
        foreach (var entry in byKey.Values)
        {
            entry.Index = _index.Count;
            _index.Add(entry);
        }
        return true;
    }

    /// <summary>
    /// Builds one chromatogram per (Q1, Q3) SRM transition encoded in the SDK's auto filters.
    /// Filter strings look like <c>"+ c NSI SRM ms2 572.792 [724.375-724.377, 837.459-837.461]"</c>;
    /// each bracketed window becomes one chromatogram with id <c>"SRM SIC Q1,Q3midpoint"</c>.
    /// Mirrors pwiz C++ <c>ChromatogramList_Thermo.cpp:413-479</c>.
    /// </summary>
    /// <remarks>
    /// pwiz C++ skips windows wider than <see cref="MaxSrmScanRange"/> (these aren't true SRM
    /// transitions but wide-window scans that would alias multiple ions). The id format uses
    /// G10 for both Q1 and the per-window Q3 midpoint — same precision-trimming as SIM ids.
    /// </remarks>
    private bool BuildSrmIndex()
    {
        var byKey = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
        var autoFilters = _raw.Raw.GetAutoFilters();
        var firstScanByFilter = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int scan = _raw.FirstScan; scan <= _raw.LastScan; scan++)
        {
            var f = _raw.Raw.GetFilterForScanNumber(scan);
            if (f.ScanMode != ScanModeType.Srm) continue;
            string fs = f.ToString();
            if (!firstScanByFilter.ContainsKey(fs))
                firstScanByFilter[fs] = scan;
        }

        foreach (var filterString in autoFilters)
        {
            if (filterString is null) continue;
            // Filter substring "SRM ms" matches "SRM ms2", "SRM ms3", etc. — each MS-order
            // is a valid transition filter.
            if (!filterString.Contains(" SRM ms", StringComparison.Ordinal)) continue;
            if (!firstScanByFilter.TryGetValue(filterString, out int sampleScan)) continue;

            // SRM filter format: "[polarity] [calibrant?] SRM ms<n> <Q1> [<lo>-<hi>, ...]"
            // Parse Q1 from between "SRM ms" and "[", and ranges from inside "[...]"
            double? q1 = ParseSrmQ1(filterString);
            if (q1 is null) continue;
            var stringRanges = ParseSimMassRanges(filterString); // same bracketed-list shape
            if (stringRanges.Count == 0) continue;

            var sampleFilter = _raw.Raw.GetFilterForScanNumber(sampleScan);
            var pol = sampleFilter.Polarity;
            string polStr = pol == PolarityType.Negative ? "- " : "";

            foreach (var (lo, hi) in stringRanges)
            {
                double scanRange = hi - lo;
                if (scanRange > MaxSrmScanRange) continue; // not a real transition
                double filterQ3 = (lo + hi) / 2.0;
                double halfWidth = scanRange / 2.0;
                string q1Str = q1.Value.ToString("G10", CultureInfo.InvariantCulture);
                string q3Str = filterQ3.ToString("G10", CultureInfo.InvariantCulture);
                string key = polStr + q1Str + "," + q3Str;
                if (!byKey.TryGetValue(key, out var entry))
                {
                    string id = polStr + "SRM SIC " + q1Str + "," + q3Str;
                    entry = new IndexEntry
                    {
                        Id = id,
                        Kind = CVID.MS_SRM_chromatogram,
                        Q1 = q1.Value,
                        Q3 = filterQ3,
                        HalfWidth = halfWidth,
                        Polarity = pol,
                    };
                    byKey.Add(key, entry);
                }
                entry.Scans.Add(sampleScan);
            }
        }
        if (byKey.Count == 0) return false;
        foreach (var entry in byKey.Values)
        {
            entry.Index = _index.Count;
            _index.Add(entry);
        }
        return true;
    }

    private const double MaxSrmScanRange = 1.0; // matches Reader_Thermo_Detail.hpp

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

        if (entry.Device != Device.MS)
            return FillNonMsDeviceChromatogram(chrom, entry);

        return entry.Kind switch
        {
            CVID.MS_SIM_chromatogram => FillSimChromatogram(chrom, entry),
            CVID.MS_SRM_chromatogram => FillSrmChromatogram(chrom, entry),
            _ => FillTicChromatogram(chrom),
        };
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
        var settings = new ChromatogramTraceSettings(TraceType.TIC);
        var data = _raw.Raw.GetChromatogramDataEx(new[] { settings }, -1, -1, new MassOptions());
        if (data?.PositionsArray?.Length > 0 && data.PositionsArray[0] is { } times
            && data.IntensitiesArray?[0] is { } intensities)
        {
            chrom.DefaultArrayLength = times.Length;
            chrom.BinaryDataArrays.Add(MakeArray(times, CVID.MS_time_array, CVID.UO_minute));
            chrom.BinaryDataArrays.Add(MakeArray(intensities, CVID.MS_intensity_array, CVID.MS_number_of_detector_counts));

            // Third array: ms level per time point, matches pwiz C++ ChromatogramList_Thermo.
            var msArr = new IntegerDataArray();
            msArr.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);
            for (int i = 0; i < times.Length; i++)
            {
                try
                {
                    int sn = _raw.Raw.ScanNumberFromRetentionTime(times[i]);
                    msArr.Data.Add(_raw.MsLevel(sn));
                }
                catch { msArr.Data.Add(0); }
            }
            chrom.IntegerDataArrays.Add(msArr);
        }
        return chrom;
    }

    private Chromatogram FillSimChromatogram(Chromatogram chrom, IndexEntry entry)
    {
        // Precursor isolation window (matches pwiz C++ ChromatogramList_Thermo.cpp:211-213).
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, entry.Q1, CVID.MS_m_z);
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, entry.HalfWidth, CVID.MS_m_z);
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, entry.HalfWidth, CVID.MS_m_z);

        // Ask Thermo for the chromatogram over the SIM's Q1 ± halfWidth window — mirrors C++
        // getChromatogramData(Type_MassRange, "SIM ms [...]", Q1-hw, Q1+hw, ...). The new
        // RawFileReader API rejects the abbreviated "SIM ms [..]" string with
        // InvalidFilterFormatException; cpp's older Xcalibur API accepts it as a substring
        // match. Pass the full canonical filter from a representative SIM scan instead so
        // RawFileReader's strict parser is happy and the trace still selects only this SIM.
        string canonicalFilter = entry.Scans.Count > 0
            ? _raw.Raw.GetFilterForScanNumber(entry.Scans[0]).ToString()
            : "";
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
        var data = _raw.Raw.GetChromatogramDataEx(new[] { settings }, -1, -1, new MassOptions());
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
    /// (Q1, no offsets), CID activation (cpp adds collision energy 0 — we omit since we'd
    /// need to query the filter for an energy that isn't reliable for SRM), and the product
    /// isolation window (Q3 ± halfWidth). Mirrors pwiz C++
    /// <c>ChromatogramList_Thermo.cpp:194-207</c>.
    /// </summary>
    private Chromatogram FillSrmChromatogram(Chromatogram chrom, IndexEntry entry)
    {
        // Precursor side: just the target m/z + activation. cpp also writes a "collision
        // energy 0.0" cvParam (placeholder) — we emit the same so msdiff stays clean.
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, entry.Q1, CVID.MS_m_z);
        chrom.Precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        chrom.Precursor.Activation.Set(CVID.MS_collision_energy, 0.0, CVID.UO_electronvolt);

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
        var data = _raw.Raw.GetChromatogramDataEx(new[] { settings }, -1, -1, new MassOptions());
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
}
