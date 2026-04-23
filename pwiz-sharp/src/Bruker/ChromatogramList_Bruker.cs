using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// <see cref="IChromatogramList"/> backed by any Bruker <c>.d</c> directory. Emits TIC + BPC
/// from the vendor handle (with an <c>ms level</c> integer array attached), plus any LC traces
/// found in <c>chromatography-data.sqlite</c>.
/// </summary>
/// <remarks>Thin wrapper over <see cref="IBrukerData"/>; no format-specific code.</remarks>
public sealed class ChromatogramList_Bruker : ChromatogramListBase
{
    private readonly IBrukerData _data;
    private readonly int _preferOnlyMsLevel;
    private readonly List<IndexEntry> _index = new();
    private readonly List<LcTrace> _lcTraces;

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public CVID Kind;
        public int LcTraceIndex = -1;
    }

    /// <summary>DataProcessing emitted as the <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>
    /// Creates a chromatogram list over the given Bruker analysis handle. LC traces (from
    /// <c>chromatography-data.sqlite</c>) are appended after the global TIC + BPC.
    /// </summary>
    public ChromatogramList_Bruker(IBrukerData data, int preferOnlyMsLevel = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _preferOnlyMsLevel = preferOnlyMsLevel;

        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });
        _index.Add(new IndexEntry { Index = 1, Id = "BPC", Kind = CVID.MS_basepeak_chromatogram });

        _lcTraces = data.ReadLcTraces();
        for (int i = 0; i < _lcTraces.Count; i++)
        {
            var lc = _lcTraces[i];
            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = lc.Description,
                Kind = lc.ChromatogramCvid,
                LcTraceIndex = i,
            });
        }
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        var entry = _index[index];
        var chrom = new Chromatogram { Index = entry.Index, Id = entry.Id };
        chrom.Params.Set(entry.Kind);

        if (!getBinaryData) return chrom;

        // pwiz C++ dispatches by chromatogramType: TIC/BPC entries (even LC-sourced ones) are
        // filled from the vendor API, not the LC chunks. Other LC types use the LC payload.
        if (entry.Kind is CVID.MS_TIC_chromatogram or CVID.MS_basepeak_chromatogram)
            return FillGlobalChromatogram(chrom, entry.Kind);
        if (entry.LcTraceIndex >= 0)
            return FillLcTrace(chrom, _lcTraces[entry.LcTraceIndex]);

        return FillGlobalChromatogram(chrom, entry.Kind);
    }

    private Chromatogram FillGlobalChromatogram(Chromatogram chrom, CVID kind)
    {
        var times = new List<double>();
        var intensities = new List<double>();
        var msLevels = new List<long>();
        foreach (var p in _data.EnumerateChromatogramPoints(_preferOnlyMsLevel))
        {
            times.Add(p.RetentionTimeSeconds);
            intensities.Add(kind == CVID.MS_TIC_chromatogram ? p.TotalIonCurrent : p.BasePeakIntensity);
            msLevels.Add(p.MsLevel);
        }

        chrom.DefaultArrayLength = times.Count;

        var timeArr = new BinaryDataArray();
        timeArr.Set(CVID.MS_time_array, "", CVID.UO_second);
        timeArr.Data.AddRange(times);
        chrom.BinaryDataArrays.Add(timeArr);

        var intArr = new BinaryDataArray();
        intArr.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
        intArr.Data.AddRange(intensities);
        chrom.BinaryDataArrays.Add(intArr);

        var msLevelArr = new IntegerDataArray();
        msLevelArr.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);
        msLevelArr.Data.AddRange(msLevels);
        chrom.IntegerDataArrays.Add(msLevelArr);
        return chrom;
    }

    private static Chromatogram FillLcTrace(Chromatogram chrom, LcTrace trace)
    {
        chrom.DefaultArrayLength = trace.Times.Count;

        var timeArr = new BinaryDataArray();
        timeArr.Set(CVID.MS_time_array, "", CVID.UO_second);
        timeArr.Data.AddRange(trace.Times);
        chrom.BinaryDataArrays.Add(timeArr);

        var intArr = new BinaryDataArray();
        intArr.Set(CVID.MS_intensity_array, "", trace.UnitCvid);
        intArr.Data.AddRange(trace.Intensities);
        chrom.BinaryDataArrays.Add(intArr);

        if (!string.IsNullOrEmpty(trace.Description))
            chrom.Params.Set(CVID.MS_chromatogram_title, trace.Description);
        if (!string.IsNullOrEmpty(trace.Instrument))
            chrom.Params.UserParams.Add(new UserParam("Instrument", trace.Instrument));

        return chrom;
    }
}
