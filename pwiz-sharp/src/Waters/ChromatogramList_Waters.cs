using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Waters;

/// <summary>
/// <see cref="IChromatogramList"/> backed by a Waters <c>.raw</c> directory. Phase 1 emits a
/// single TIC chromatogram aggregating all functions' per-scan TIC values, sorted by retention
/// time. Mirrors pwiz C++ <c>ChromatogramList_Waters::createIndex</c>.
/// </summary>
public sealed class ChromatogramList_Waters : ChromatogramListBase
{
    private readonly WatersRawFile _data;
    private readonly int _preferOnlyMsLevel;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public CVID Kind;
    }

    internal ChromatogramList_Waters(WatersRawFile data, int preferOnlyMsLevel)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _preferOnlyMsLevel = preferOnlyMsLevel;
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];
        var chrom = new Chromatogram { Index = ie.Index, Id = ie.Id };
        chrom.Params.Set(ie.Kind);

        if (ie.Kind != CVID.MS_TIC_chromatogram) return chrom;

        // Aggregate per-function TIC into a single sorted-by-time chromatogram. We tag each
        // point with its source function index in a non-standard integer array (matches
        // pwiz C++ chromatogramList[0] for combined TIC).
        var points = new List<(double Time, double Intensity, long Function)>();
        foreach (int function in _data.FunctionIndices)
        {
            // Skip functions that don't translate to MS spectra (some test fixtures include
            // diode-array or off functions that we don't want in the global TIC).
            int rawType;
            try { rawType = _data.GetFunctionType(function); }
            catch { continue; }
            var ft = WatersDetail.FromMassLynxFunctionType(rawType);
            if (!WatersDetail.TranslateFunctionType(ft, out int msLevel, out CVID spectrumType)) continue;
            if (spectrumType == CVID.MS_EMR_spectrum) continue; // diode array

            // preferOnlyMsLevel narrows the TIC the same way it narrows the spectrum list, so
            // the chromatogram lines up with the spectrum count.
            if (_preferOnlyMsLevel > 0 && msLevel != _preferOnlyMsLevel) continue;

            var times = _data.TimesByFunctionIndex[function];
            var intens = _data.TicByFunctionIndex[function];
            for (int i = 0; i < times.Length; i++)
                points.Add((times[i], intens[i], function + 1));
        }

        points.Sort((a, b) => a.Time.CompareTo(b.Time));
        chrom.DefaultArrayLength = points.Count;

        if (!getBinaryData) return chrom;

        var timeArr = new BinaryDataArray();
        timeArr.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var intArr = new BinaryDataArray();
        intArr.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
        var funcArr = new IntegerDataArray();
        funcArr.Set(CVID.MS_non_standard_data_array, "function", CVID.UO_dimensionless_unit);

        timeArr.Data.Capacity = points.Count;
        intArr.Data.Capacity = points.Count;
        funcArr.Data.Capacity = points.Count;
        foreach (var p in points)
        {
            timeArr.Data.Add(p.Time);
            intArr.Data.Add(p.Intensity);
            funcArr.Data.Add(p.Function);
        }

        chrom.BinaryDataArrays.Add(timeArr);
        chrom.BinaryDataArrays.Add(intArr);
        chrom.IntegerDataArrays.Add(funcArr);
        return chrom;
    }
}
