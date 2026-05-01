using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
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
    private readonly bool _globalChromatogramsAreMs1Only;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public CVID Kind;
        public int Function;
        public int Offset;        // transition index within the function (SRM only) or analog channel index
        public float Q1;
        public float Q3;
        public CVID Polarity;
        public bool IsAnalog;
        public CVID AnalogUnit;
        public string? AnalogUnitsUserParam;
    }

    internal ChromatogramList_Waters(WatersRawFile data, int preferOnlyMsLevel,
        bool srmAsSpectra = false, bool globalChromatogramsAreMs1Only = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _preferOnlyMsLevel = preferOnlyMsLevel;
        _globalChromatogramsAreMs1Only = globalChromatogramsAreMs1Only;

        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = CVID.MS_TIC_chromatogram });

        // Enumerate per-channel chromatograms for SRM and SIM functions. pwiz C++ reads scan 1
        // of each function: SRM yields parallel precursor + product m/z lists (one chromatogram
        // per transition); SIM yields just the precursor m/z list (one chromatogram per channel,
        // no product). srmAsSpectra config moves SRM transitions into the spectrum list instead.
        AppendTransitionIndex(srmAsSpectra);
        AppendAnalogIndex();
    }

    private void AppendAnalogIndex()
    {
        // pwiz C++ ChromatogramList_Waters appends one chromatogram per analog channel after
        // the SRM/SIM block. The chromatogram type is inferred from the channel name + units:
        //   - name contains "temp" + units == "C" → MS_temperature_chromatogram
        //   - units == "LSU"                       → MS_electromagnetic_radiation_chromatogram
        //   - name contains "pressure"             → MS_pressure_chromatogram
        //   - everything else                      → MS_electromagnetic_radiation_chromatogram (PDA/UV)
        int channels = _data.GetAnalogChannelCount();
        for (int ch = 0; ch < channels; ch++)
        {
            string rawName = _data.GetAnalogChannelDescription(ch);
            string rawUnits = _data.GetAnalogChannelUnits(ch);
            // pwiz C++ strips "%" from the name and the degree sign (\u00b0) from units, then trims.
            string name = rawName.Replace("%", "", StringComparison.Ordinal).Trim();
            string units = rawUnits.Replace("\u00b0", "", StringComparison.Ordinal).Trim();

            CVID kind;
            if (name.Contains("temp", StringComparison.OrdinalIgnoreCase)
                && string.Equals(units, "C", StringComparison.OrdinalIgnoreCase))
                kind = CVID.MS_temperature_chromatogram;
            else if (string.Equals(units, "LSU", StringComparison.OrdinalIgnoreCase))
                kind = CVID.MS_electromagnetic_radiation_chromatogram;
            else if (name.Contains("pressure", StringComparison.OrdinalIgnoreCase))
                kind = CVID.MS_pressure_chromatogram;
            else
                kind = CVID.MS_electromagnetic_radiation_chromatogram;

            // Map units to a CV term where possible; otherwise emit a raw "units" userParam
            // (pwiz C++ does the same — see ChromatogramList_Waters.cpp:262-266).
            CVID unitCv;
            string? unitsUserParam = null;
            if (kind == CVID.MS_pressure_chromatogram
                && string.Equals(rawUnits, "psi", StringComparison.OrdinalIgnoreCase))
                unitCv = CVID.UO_pounds_per_square_inch;
            else if (kind == CVID.MS_temperature_chromatogram
                && rawUnits.EndsWith("C", StringComparison.OrdinalIgnoreCase))
                unitCv = CVID.UO_degree_Celsius;
            else if (rawUnits == "%")
                unitCv = CVID.UO_percent;
            else
            {
                unitCv = CVID.MS_number_of_detector_counts;
                unitsUserParam = rawUnits;
            }

            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = name,
                Kind = kind,
                Function = -1,
                Offset = ch,
                IsAnalog = true,
                AnalogUnit = unitCv,
                AnalogUnitsUserParam = unitsUserParam,
            });
        }
    }

    private void AppendTransitionIndex(bool srmAsSpectra)
    {
        foreach (int function in _data.FunctionIndices)
        {
            int rawType;
            try { rawType = _data.GetFunctionType(function); }
            catch { continue; }
            var ft = WatersDetail.FromMassLynxFunctionType(rawType);
            if (!WatersDetail.TranslateFunctionType(ft, out _, out CVID spectrumType)) continue;

            bool isSrm = spectrumType == CVID.MS_SRM_spectrum;
            bool isSim = spectrumType == CVID.MS_SIM_spectrum;
            if (!isSrm && !isSim) continue;
            // SRM-as-spectra mode moves SRM transitions into the spectrum list; SIM stays here
            // because it has no product axis to disambiguate.
            if (isSrm && srmAsSpectra) continue;

            float[] precs, prods;
            try
            {
                if (isSrm)
                    (precs, prods) = _data.ReadMrmTransitions(function);
                else
                {
                    // pwiz C++ reads SIM channels via the raw scan reader (not the product-scan
                    // overload) — the SDK's product-scan call rejects SIM functions outright.
                    precs = _data.ReadSimChannelMzs(function, scan: 1);
                    prods = Array.Empty<float>();
                }
            }
            catch { continue; }
            if (precs.Length == 0) continue;
            if (isSrm && prods.Length != precs.Length) continue;

            CVID polarity = WatersDetail.Polarity(WatersDetail.FromMassLynxIonMode(_data.GetIonMode(function)));

            for (int i = 0; i < precs.Length; i++)
            {
                string id;
                CVID kind;
                float q3;
                if (isSrm)
                {
                    string prefix = polarity == CVID.MS_negative_scan ? "- " : string.Empty;
                    id = prefix
                        + "SRM SIC Q1=" + FormatMz(precs[i])
                        + " Q3=" + FormatMz(prods[i])
                        + " function=" + (function + 1).ToString(CultureInfo.InvariantCulture)
                        + " offset=" + i.ToString(CultureInfo.InvariantCulture);
                    kind = CVID.MS_SRM_chromatogram;
                    q3 = prods[i];
                }
                else
                {
                    // pwiz C++ doesn't prefix the polarity for SIM ids — only SRM gets it.
                    id = "SIM SIC Q1=" + FormatMz(precs[i])
                        + " function=" + (function + 1).ToString(CultureInfo.InvariantCulture)
                        + " offset=" + i.ToString(CultureInfo.InvariantCulture);
                    kind = CVID.MS_SIM_chromatogram;
                    q3 = 0;
                }

                _index.Add(new IndexEntry
                {
                    Index = _index.Count,
                    Id = id,
                    Kind = kind,
                    Function = function,
                    Offset = i,
                    Q1 = precs[i],
                    Q3 = q3,
                    Polarity = polarity,
                });
            }
        }
    }

    private static string FormatMz(float mz) =>
        // C++ ostream default is 6 significant digits ("%g") — G6 in .NET matches: 418.72601f
        // becomes "418.726", 666.34332f becomes "666.343".
        mz.ToString("G6", CultureInfo.InvariantCulture);

    private Chromatogram FillAnalogChromatogram(Chromatogram chrom, IndexEntry ie, bool getBinaryData)
    {
        // The ANALOG reader returns parallel time/intensity arrays per channel. pwiz C++
        // attaches a "units" userParam when the units string isn't a known CV term.
        if (ie.AnalogUnitsUserParam is { Length: > 0 } unitsParam)
            chrom.Params.UserParams.Add(new UserParam("units", unitsParam, "xsd:string"));

        var (times, intensities) = _data.ReadAnalogChannel(ie.Offset);
        chrom.DefaultArrayLength = times.Length;
        if (!getBinaryData) return chrom;

        var timeArr = new BinaryDataArray();
        timeArr.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var intArr = new BinaryDataArray();
        intArr.Set(CVID.MS_intensity_array, "", ie.AnalogUnit);
        for (int i = 0; i < times.Length; i++)
        {
            timeArr.Data.Add(times[i]);
            intArr.Data.Add(intensities[i]);
        }
        chrom.BinaryDataArrays.Add(timeArr);
        chrom.BinaryDataArrays.Add(intArr);
        return chrom;
    }

    private Chromatogram FillSimChromatogram(Chromatogram chrom, IndexEntry ie, bool getBinaryData)
    {
        // pwiz C++ sets only the isolation window target + CID activation for SIM chromatograms
        // (no polarity tag, no product side). Mirror that exactly so msdiff stays clean.
        chrom.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, ie.Q1, CVID.MS_m_z);
        chrom.Precursor.Activation.Set(CVID.MS_collision_induced_dissociation);

        // pwiz C++ ChromatogramReader::ReadMassChromatogram(function, q1, ..., 1.0, false): a
        // Da window of 1.0 with bProducts=false (the SIM channels are MS1 ions, not products).
        var (times, intensities) = _data.ReadMassChromatogram(ie.Function, ie.Q1,
            massWindow: 1.0f, products: false);
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

        if (ie.Kind == CVID.MS_SIM_chromatogram)
            return FillSimChromatogram(chrom, ie, getBinaryData);

        if (ie.IsAnalog)
            return FillAnalogChromatogram(chrom, ie, getBinaryData);

        if (ie.Kind != CVID.MS_TIC_chromatogram) return chrom;

        // Aggregate per-function TIC into a single sorted-by-time chromatogram. We tag each
        // point with its source function index in a non-standard integer array (matches
        // pwiz C++ chromatogramList[0] for combined TIC).
        var points = new List<(double Time, double Intensity, long Function)>();
        foreach (int function in _data.FunctionIndices)
        {
            // pwiz C++ ChromatogramList_Waters TIC includes all functions (even DiodeArray)
            // unless globalChromatogramsAreMs1Only is set; preferOnlyMsLevel narrows similarly.
            int rawType;
            try { rawType = _data.GetFunctionType(function); }
            catch { continue; }
            var ft = WatersDetail.FromMassLynxFunctionType(rawType);
            int msLevel = 0;
            CVID spectrumType = CVID.CVID_Unknown;
            try { _ = WatersDetail.TranslateFunctionType(ft, out msLevel, out spectrumType); }
            catch { continue; }

            if (_preferOnlyMsLevel > 0 && msLevel != _preferOnlyMsLevel) continue;

            // GlobalChromatogramsAreMs1Only excludes non-MS1 functions, plus Waters function-1
            // (0-based) when its collision energy is non-zero (the MSe heuristic that promotes
            // the high-energy function to pseudo-MS2). Mirrors pwiz C++ ChromatogramList_Waters.
            if (_globalChromatogramsAreMs1Only)
            {
                if (spectrumType != CVID.MS_MS1_spectrum) continue;
                if (function == 1)
                {
                    string ceStr = _data.GetScanItem(function, 0, WatersScanItem.CollisionEnergy);
                    if (double.TryParse(ceStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double ce) && Math.Abs(ce) > 0)
                        continue;
                }
            }

            var times = _data.TimesByFunctionIndex[function];
            var intens = _data.TicByFunctionIndex[function];
            for (int i = 0; i < times.Length; i++)
                points.Add((times[i], intens[i], function + 1));
        }

        // Sort by time, breaking ties by function number. pwiz C++ uses a multimap which
        // preserves insertion order for equal keys; we iterate functions in ascending order so
        // a function-number tie-break gives the same emit order. Without this, .NET's unstable
        // List.Sort can swap the function-1/function-2 entries at a shared retention time and
        // produce binary diffs against the cpp reference (seen on MRM+Survey-Scan files).
        points.Sort((a, b) =>
        {
            int cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.Function.CompareTo(b.Function);
        });
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
