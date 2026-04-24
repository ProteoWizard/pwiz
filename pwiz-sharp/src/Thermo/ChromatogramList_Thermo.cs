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

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public CVID Kind;
        // SIM-specific:
        public double Q1;
        public double HalfWidth;
        public PolarityType Polarity;
        public List<int> Scans = new();
        // Non-MS-device sources (Pump Pressure / UV / CAD):
        public Device Device = Device.MS;
        public int DeviceChannel;  // 1-based
    }

    /// <summary>Creates a chromatogram list backed by the given Thermo raw file.</summary>
    public ChromatogramList_Thermo(ThermoRawFile raw, bool simAsSpectra = false)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });

        if (!simAsSpectra)
            HasSimChromatograms = BuildSimIndex();

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

                if (isAbsorbance && (axisY.Length == 0 || axisY.StartsWith("UV", StringComparison.OrdinalIgnoreCase)))
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
                else if (device == Device.Pda)
                {
                    classified = ("PDA ", CVID.MS_absorption_chromatogram);
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
    /// Walks the raw file once and groups SIM scans by (polarity, Q1 midpoint). Produces one
    /// chromatogram per group matching pwiz C++ ChromatogramList_Thermo.cpp:481-504.
    /// </summary>
    /// <remarks>
    /// Matches pwiz C++ <c>polarityStringForFilter</c> — only prepends "- " for negative
    /// polarity; positive mode has an empty prefix for backward-compat (see
    /// ChromatogramListBase.hpp line 53).
    /// </remarks>
    private bool BuildSimIndex()
    {
        var byKey = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
        for (int scan = _raw.FirstScan; scan <= _raw.LastScan; scan++)
        {
            var filter = _raw.Raw.GetFilterForScanNumber(scan);
            if (filter.ScanMode != ScanModeType.Sim) continue;
            for (int j = 0; j < filter.MassRangeCount; j++)
            {
                var range = filter.GetMassRange(j);
                double lo = range.Low, hi = range.High;
                double q1 = (lo + hi) / 2.0;
                double halfWidth = (hi - lo) / 2.0;
                var pol = filter.Polarity;
                string polStr = pol == PolarityType.Negative ? "- " : "";
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
                entry.Scans.Add(scan);
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

        // Polarity cvParam for SIM chromatograms matches pwiz C++ ref output.
        if (entry.Kind == CVID.MS_SIM_chromatogram)
        {
            if (entry.Polarity == PolarityType.Positive)
                chrom.Params.Set(CVID.MS_positive_scan);
            else if (entry.Polarity == PolarityType.Negative)
                chrom.Params.Set(CVID.MS_negative_scan);
        }

        if (entry.Device != Device.MS)
            return FillNonMsDeviceChromatogram(chrom, entry);

        return entry.Kind == CVID.MS_SIM_chromatogram
            ? FillSimChromatogram(chrom, entry)
            : FillTicChromatogram(chrom);
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
            var data = _raw.Raw.GetChromatogramDataEx(new[] { settings }, -1, -1, new MassOptions());
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
        // getChromatogramData(Type_MassRange, "SIM ms [...]", Q1-hw, Q1+hw, ...).
        var settings = new ChromatogramTraceSettings(TraceType.MassRange)
        {
            Filter = $"SIM ms [{(entry.Q1 - entry.HalfWidth).ToString("G10", CultureInfo.InvariantCulture)}-{(entry.Q1 + entry.HalfWidth).ToString("G10", CultureInfo.InvariantCulture)}]",
            MassRanges = new[] { new ThermoFisher.CommonCore.Data.Business.Range(entry.Q1 - entry.HalfWidth, entry.Q1 + entry.HalfWidth) },
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

    private static BinaryDataArray MakeArray(double[] values, CVID kind, CVID units)
    {
        var arr = new BinaryDataArray();
        arr.Set(kind, "", units);
        arr.Data.AddRange(values);
        return arr;
    }
}
