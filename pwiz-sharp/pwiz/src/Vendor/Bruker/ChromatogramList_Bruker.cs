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
    private readonly SpectrumList_Bruker? _spectrumList;
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
        : this(data, null, preferOnlyMsLevel) { }

    /// <summary>
    /// Creates a chromatogram list with a sibling spectrum list, used to populate the
    /// <c>ms level</c> integer-array attached to TIC/BPC chromatograms by indexing
    /// <c>spectrumList[i]</c> for each chromatogram point — matches pwiz C++
    /// ChromatogramList_Bruker.cpp:155-156 which calls <c>getMSSpectrum(i)->getMSMSStage()</c>.
    /// </summary>
    public ChromatogramList_Bruker(IBrukerData data, SpectrumList_Bruker? spectrumList, int preferOnlyMsLevel)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _spectrumList = spectrumList;
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
        foreach (var p in _data.EnumerateChromatogramPoints(_preferOnlyMsLevel))
        {
            times.Add(p.RetentionTimeSeconds);
            intensities.Add(kind == CVID.MS_TIC_chromatogram ? p.TotalIonCurrent : p.BasePeakIntensity);
        }
        // pwiz C++ ChromatogramList_Bruker.cpp:155-156 keys msLevel array by spectrumList[i],
        // not by per-point classification — for PASEF data this means the msLevel array
        // mirrors the FIRST N spectra's ms levels. Without a sibling spectrum list we fall
        // back to per-point classification (same value either way for non-PASEF, where
        // chromatogram[i] corresponds 1:1 to spectrum[i]).
        var msLevels = new List<long>(times.Count);
        if (_spectrumList is not null)
        {
            int specCount = _spectrumList.Count;
            for (int i = 0; i < times.Count; i++)
                msLevels.Add(i < specCount ? _spectrumList.GetMsLevelByIndex(i) : 0);
        }
        else
        {
            int idx = 0;
            foreach (var p in _data.EnumerateChromatogramPoints(_preferOnlyMsLevel))
            {
                msLevels.Add(p.MsLevel);
                if (++idx >= times.Count) break;
            }
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
