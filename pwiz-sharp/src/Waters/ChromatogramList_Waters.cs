using System.Globalization;
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
        public int Function;
        public int Offset;        // transition index within the function (SRM only)
        public float Q1;
        public float Q3;
        public CVID Polarity;
    }

    internal ChromatogramList_Waters(WatersRawFile data, int preferOnlyMsLevel, bool srmAsSpectra = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _preferOnlyMsLevel = preferOnlyMsLevel;

        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });

        // Enumerate SRM transitions per MRM function. pwiz C++ uses readProductScan(scan=1) on
        // each MRM function — the precursor and product m/z arrays come back parallel, one
        // entry per transition. SIM functions go through the same path but with no product;
        // we don't expand those yet (no Phase 1 fixture exercises SIM chromatograms).
        if (srmAsSpectra) return; // SRM-as-spectra mode means SRM transitions appear in the spectrum list, not here.
        AppendSrmIndex();
    }

    private void AppendSrmIndex()
    {
        foreach (int function in _data.FunctionIndices)
        {
            int rawType;
            try { rawType = _data.GetFunctionType(function); }
            catch { continue; }
            var ft = WatersDetail.FromMassLynxFunctionType(rawType);
            if (!WatersDetail.TranslateFunctionType(ft, out _, out CVID spectrumType)) continue;
            if (spectrumType != CVID.MS_SRM_spectrum) continue;

            float[] precs, prods;
            try { (precs, prods) = _data.ReadMrmTransitions(function); }
            catch { continue; }
            if (precs.Length == 0 || prods.Length != precs.Length) continue;

            CVID polarity = WatersDetail.Polarity(WatersDetail.FromMassLynxIonMode(_data.GetIonMode(function)));

            for (int i = 0; i < precs.Length; i++)
            {
                string prefix = polarity == CVID.MS_negative_scan ? "- " : string.Empty;
                string id = prefix
                    + "SRM SIC Q1=" + FormatMz(precs[i])
                    + " Q3=" + FormatMz(prods[i])
                    + " function=" + (function + 1).ToString(CultureInfo.InvariantCulture)
                    + " offset=" + i.ToString(CultureInfo.InvariantCulture);
                _index.Add(new IndexEntry
                {
                    Index = _index.Count,
                    Id = id,
                    Kind = CVID.MS_SRM_chromatogram,
                    Function = function,
                    Offset = i,
                    Q1 = precs[i],
                    Q3 = prods[i],
                    Polarity = polarity,
                });
            }
        }
    }

    private static string FormatMz(float mz) =>
        // C++ ostream default is 6 significant digits ("%g") — G6 in .NET matches: 418.72601f
        // becomes "418.726", 666.34332f becomes "666.343".
        mz.ToString("G6", CultureInfo.InvariantCulture);

    private Chromatogram FillSrmChromatogram(Chromatogram chrom, IndexEntry ie, bool getBinaryData)
    {
        // Polarity tag inside the chromatogram (the id-prefix is set elsewhere).
        if (ie.Polarity != CVID.CVID_Unknown) chrom.Params.Set(ie.Polarity);

        // Precursor / product isolation windows + activation. pwiz C++ adds only the target
        // m/z (no offsets) and a CID activation — we mirror that exactly.
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, ie.Q1, CVID.MS_m_z);
        chrom.Precursor.Activation.Set(CVID.MS_collision_induced_dissociation);
        chrom.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, ie.Q3, CVID.MS_m_z);

        var (times, intensities) = _data.ReadMrmChromatogram(ie.Function, ie.Offset);
        chrom.DefaultArrayLength = times.Length;
        if (!getBinaryData) return chrom;

        var timeArr = new BinaryDataArray();
        timeArr.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var intArr = new BinaryDataArray();
        intArr.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
        timeArr.Data.Capacity = times.Length;
        intArr.Data.Capacity = intensities.Length;
        for (int i = 0; i < times.Length; i++)
        {
            timeArr.Data.Add(times[i]);
            intArr.Data.Add(intensities[i]);
        }
        chrom.BinaryDataArrays.Add(timeArr);
        chrom.BinaryDataArrays.Add(intArr);
        return chrom;
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

        if (ie.Kind == CVID.MS_SRM_chromatogram)
            return FillSrmChromatogram(chrom, ie, getBinaryData);

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
