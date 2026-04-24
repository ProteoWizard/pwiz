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
    }

    /// <summary>Creates a chromatogram list backed by the given Thermo raw file.</summary>
    public ChromatogramList_Thermo(ThermoRawFile raw, bool simAsSpectra = false)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });

        if (!simAsSpectra)
            HasSimChromatograms = BuildSimIndex();
    }

    /// <summary>
    /// Walks the raw file once and groups SIM scans by (polarity, Q1 midpoint). Produces one
    /// chromatogram per group matching pwiz C++ ChromatogramList_Thermo.cpp:481-504.
    /// </summary>
    private bool BuildSimIndex()
    {
        // Group key: "{polarityChar}:{Q1:G10}" — Q1 is the scan range midpoint.
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
                string polStr = pol == PolarityType.Positive ? "+ " : pol == PolarityType.Negative ? "- " : "";
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

        return entry.Kind == CVID.MS_SIM_chromatogram
            ? FillSimChromatogram(chrom, entry)
            : FillTicChromatogram(chrom);
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
        // For each SIM scan in the group, take its retention time and total-ion intensity.
        var times = new double[entry.Scans.Count];
        var intensities = new double[entry.Scans.Count];
        for (int i = 0; i < entry.Scans.Count; i++)
        {
            int scan = entry.Scans[i];
            times[i] = _raw.RetentionTimeMinutes(scan);
            try
            {
                var stats = _raw.Raw.GetScanStatsForScanNumber(scan);
                intensities[i] = stats.TIC;
            }
            catch { intensities[i] = 0; }
        }
        chrom.DefaultArrayLength = times.Length;
        chrom.BinaryDataArrays.Add(MakeArray(times, CVID.MS_time_array, CVID.UO_minute));
        chrom.BinaryDataArrays.Add(MakeArray(intensities, CVID.MS_intensity_array, CVID.MS_number_of_detector_counts));
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
