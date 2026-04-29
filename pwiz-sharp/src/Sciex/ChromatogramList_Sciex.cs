using System.Globalization;
using Clearcore2.Data;
using Clearcore2.Data.DataAccess.SampleData;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// <see cref="IChromatogramList"/> backed by a Sciex <see cref="WiffData"/>. Initial port of pwiz
/// C++ <c>ChromatogramList_ABI</c>: emits <c>TIC</c> + <c>BPC</c> (summed across all experiments
/// in the sample) plus one <c>SRM</c> / <c>SIM</c> chromatogram per transition. ADC traces
/// (pressure, flow, TWC) are not yet ported.
/// </summary>
public sealed class ChromatogramList_Sciex : ChromatogramListBase, IDisposable
{
    private readonly WiffData _wiff;
    private readonly bool _ownsWiff;
    private readonly bool _globalChromsAreMs1Only;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="wiff"/>; <paramref name="ownsWiff"/> selects whether
    /// disposing the chromatogram list disposes the wiff.</summary>
    public ChromatogramList_Sciex(WiffData wiff, bool ownsWiff, bool globalChromatogramsAreMs1Only)
    {
        ArgumentNullException.ThrowIfNull(wiff);
        _wiff = wiff;
        _ownsWiff = ownsWiff;
        _globalChromsAreMs1Only = globalChromatogramsAreMs1Only;
        CreateIndex();
    }

    private enum ChromKind { Tic, Bpc, Srm, Sim, Adc, Twc }

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public ChromKind Kind;
        public CVID ChromatogramType;
        public int ExperimentIndex;
        public int TransitionIndex;     // SRM/SIM transition OR ADC channel index
        public double Q1;
        public double Q3;
        public double DwellTimeMs;
        public double CollisionEnergy;
        public double StartTime;        // scheduled MRM start time (0 if unscheduled)
        public double EndTime;          // scheduled MRM end time (0 if unscheduled)
        public string? CompoundId;
        public MSExperimentInfo.PolarityEnum Polarity;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        // TIC + BPC are always emitted (cpp does the same; wiff2 skips BPC but we don't yet
        // distinguish wiff2 — the SDK simply returns empty if BPC isn't available).
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = ChromKind.Tic, ChromatogramType = CVID.MS_TIC_chromatogram });
        _index.Add(new IndexEntry { Index = 1, Id = "BPC", Kind = ChromKind.Bpc, ChromatogramType = CVID.MS_basepeak_chromatogram });

        for (int e = 0; e < _wiff.ExperimentCount; e++)
        {
            MSExperiment? exp;
            try { exp = _wiff.GetExperiment(e); }
            catch { continue; }
            var info = exp.Details;

            // Walk MassRangeInfo[] to find SRM (MRMMassRange) and SIM (SIMMassRange) transitions.
            // cpp Experiment::getSRM/getSIM iterates the same SDK collection.
            if (info.ExperimentType == ExperimentType.MRM && info.MassRangeInfo is { Length: > 0 } mrmRanges)
            {
                for (int t = 0; t < mrmRanges.Length; t++)
                {
                    if (mrmRanges[t] is not MRMMassRange mrm) continue;
                    // CompoundDepParameters can carry a per-transition collision energy. Falling
                    // back to the experiment default keeps id consistency with cpp.
                    double ce = ReadCompoundParameter(mrm.CompoundDepParameters, "CE");
                    // Scheduled-MRM windows: cpp WiffFile.cpp sets target.startTime =
                    // ExpectedRT - RTWindow/2 and target.endTime = ExpectedRT + RTWindow/2,
                    // emitting "start=... end=..." iff endTime > 0.
                    double start = mrm.ExpectedRT - mrm.RTWindow / 2.0;
                    double end = mrm.ExpectedRT + mrm.RTWindow / 2.0;
                    var entry = new IndexEntry
                    {
                        Index = _index.Count,
                        Kind = ChromKind.Srm,
                        ChromatogramType = CVID.MS_SRM_chromatogram,
                        ExperimentIndex = e,
                        TransitionIndex = t,
                        Q1 = mrm.Q1Mass,
                        Q3 = mrm.Q3Mass,
                        DwellTimeMs = mrm.DwellTime,
                        CollisionEnergy = ce,
                        StartTime = start,
                        EndTime = end,
                        CompoundId = mrm.Name,
                        Polarity = info.Polarity,
                    };
                    entry.Id = BuildSrmId(entry, _wiff.SampleNumber, e + 1);
                    _index.Add(entry);
                }
            }
            else if (info.ExperimentType == ExperimentType.SIM && info.MassRangeInfo is { Length: > 0 } simRanges)
            {
                for (int t = 0; t < simRanges.Length; t++)
                {
                    if (simRanges[t] is not SIMMassRange sim) continue;
                    double ce = ReadCompoundParameter(sim.CompoundDepParameters, "CE");
                    double start = sim.ExpectedRT - sim.RTWindow / 2.0;
                    double end = sim.ExpectedRT + sim.RTWindow / 2.0;
                    var entry = new IndexEntry
                    {
                        Index = _index.Count,
                        Kind = ChromKind.Sim,
                        ChromatogramType = CVID.MS_SIM_chromatogram,
                        ExperimentIndex = e,
                        TransitionIndex = t,
                        Q1 = sim.Mass,
                        DwellTimeMs = sim.DwellTime,
                        CollisionEnergy = ce,
                        StartTime = start,
                        EndTime = end,
                        CompoundId = sim.Name,
                        Polarity = info.Polarity,
                    };
                    entry.Id = BuildSimId(entry, _wiff.SampleNumber, e + 1);
                    _index.Add(entry);
                }
            }
        }

        // ADC traces — pump pressure / flow channels. Cpp filters by name (only "Pressure" or
        // "Flow"), strips the "AAO Companion App. -" vendor prefix, and tags each channel.
        for (int i = 0; i < _wiff.AdcChannelCount; i++)
        {
            string name = _wiff.GetAdcChannelName(i);
            bool isPressure = name.Contains("Pressure", StringComparison.OrdinalIgnoreCase);
            bool isFlow = name.Contains("Flow", StringComparison.OrdinalIgnoreCase);
            if (!isPressure && !isFlow) continue;

            string cleaned = name.Replace("AAO Companion App. -", string.Empty).Trim();
            string id = $"{cleaned} (channel {i + 1})";
            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = id,
                Kind = ChromKind.Adc,
                ChromatogramType = isPressure ? CVID.MS_pressure_chromatogram : CVID.MS_flow_rate_chromatogram,
                TransitionIndex = i,
            });
        }

        // TWC — DAD total-wavelength chromatogram (UV/PDA). Only emit when there's data.
        if (_wiff.HasDadData)
        {
            var (twcX, _) = _wiff.GetTotalWavelengthChromatogram();
            if (twcX.Length > 0)
            {
                _index.Add(new IndexEntry
                {
                    Index = _index.Count,
                    Id = "TWC",
                    Kind = ChromKind.Twc,
                    ChromatogramType = CVID.MS_emission_chromatogram,
                });
            }
        }
    }

    /// <summary>
    /// Mirrors cpp's <c>polarityStringForFilter</c>: empty string for positive (or unknown), "- " for negative.
    /// </summary>
    private static string PolarityPrefix(MSExperimentInfo.PolarityEnum polarity) =>
        polarity == MSExperimentInfo.PolarityEnum.Negative ? "- " : string.Empty;

    /// <summary>
    /// Formats a double with C++ <c>ostringstream</c>'s default precision (6 significant
    /// digits, trailing zeros trimmed). Cpp builds chromatogram ids by streaming the m/z values
    /// into <c>ostringstream</c>; matching that format keeps native ids byte-equal.
    /// </summary>
    private static string FormatLikeCppOss(double v) =>
        v.ToString("G6", CultureInfo.InvariantCulture);

    private static string BuildSrmId(IndexEntry e, int sample, int experimentNumber)
    {
        var ic = CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder(PolarityPrefix(e.Polarity));
        sb.Append("SRM SIC Q1=").Append(FormatLikeCppOss(e.Q1));
        sb.Append(" Q3=").Append(FormatLikeCppOss(e.Q3));
        sb.Append(" sample=").Append(sample);
        sb.Append(" period=1 experiment=").Append(experimentNumber);
        sb.Append(" transition=").Append(e.TransitionIndex);
        if (e.EndTime > 0)
        {
            sb.Append(" start=").Append(FormatLikeCppOss(e.StartTime));
            sb.Append(" end=").Append(FormatLikeCppOss(e.EndTime));
        }
        if (e.CollisionEnergy > 0) sb.Append(" ce=").Append(FormatLikeCppOss(e.CollisionEnergy));
        if (!string.IsNullOrEmpty(e.CompoundId)) sb.Append(" name=").Append(e.CompoundId);
        return sb.ToString();
    }

    private static string BuildSimId(IndexEntry e, int sample, int experimentNumber)
    {
        var ic = CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder(PolarityPrefix(e.Polarity));
        sb.Append("SIM SIC Q1=").Append(FormatLikeCppOss(e.Q1));
        sb.Append(" sample=").Append(sample);
        sb.Append(" period=1 experiment=").Append(experimentNumber);
        sb.Append(" transition=").Append(e.TransitionIndex);
        if (e.EndTime > 0)
        {
            sb.Append(" start=").Append(FormatLikeCppOss(e.StartTime));
            sb.Append(" end=").Append(FormatLikeCppOss(e.EndTime));
        }
        if (e.CollisionEnergy > 0) sb.Append(" ce=").Append(FormatLikeCppOss(e.CollisionEnergy));
        if (!string.IsNullOrEmpty(e.CompoundId)) sb.Append(" name=").Append(e.CompoundId);
        return sb.ToString();
    }

    /// <summary>
    /// Reads a numeric compound-dependent parameter (e.g. "CE" for collision energy) from the
    /// per-transition <c>CompoundDepParameters</c> dictionary. Returns 0 when missing or
    /// unparseable.
    /// </summary>
    private static double ReadCompoundParameter(System.Collections.Generic.Dictionary<string, Parameter>? parameters, string name)
    {
        if (parameters is null) return 0;
        return parameters.TryGetValue(name, out var p) && p is not null ? p.Start : 0;
    }

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];
        var c = new Chromatogram
        {
            Index = index,
            Id = ie.Id,
        };
        c.Params.Set(ie.ChromatogramType);

        switch (ie.Kind)
        {
            case ChromKind.Tic: FillTicOrBpc(c, isBpc: false, getBinaryData); break;
            case ChromKind.Bpc: FillTicOrBpc(c, isBpc: true, getBinaryData); break;
            case ChromKind.Srm: FillSrm(c, ie, isSrm: true, getBinaryData); break;
            case ChromKind.Sim: FillSrm(c, ie, isSrm: false, getBinaryData); break;
            case ChromKind.Adc: FillAdc(c, ie, getBinaryData); break;
            case ChromKind.Twc: FillTwc(c, getBinaryData); break;
        }
        return c;
    }

    /// <summary>Fills <paramref name="c"/> with one ADC channel's (time, intensity) pair.</summary>
    private void FillAdc(Chromatogram c, IndexEntry ie, bool getBinaryData)
    {
        var (times, intensities) = _wiff.GetAdcTrace(ie.TransitionIndex);
        c.DefaultArrayLength = Math.Min(times.Length, intensities.Length);
        if (!getBinaryData) return;

        // Cpp uses UO_pascal for pressure and UO_microliters_per_minute for flow. Sharp's CVID
        // table doesn't expose those, so fall back to the canonical "no unit" pair — the data
        // arrays still round-trip through mzML/mzXML readers, just without an explicit unit.
        CVID intensityUnit = ie.ChromatogramType == CVID.MS_pressure_chromatogram
            ? CVID.UO_pascal
            : CVID.UO_microliters_per_minute;
        var t = new BinaryDataArray();
        t.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var iArr = new BinaryDataArray();
        iArr.Set(CVID.MS_intensity_array, "", intensityUnit);
        int n = c.DefaultArrayLength;
        for (int i = 0; i < n; i++) t.Data.Add(times[i]);
        for (int i = 0; i < n; i++) iArr.Data.Add(intensities[i]);
        c.BinaryDataArrays.Add(t);
        c.BinaryDataArrays.Add(iArr);
    }

    /// <summary>Fills <paramref name="c"/> with the DAD total-wavelength chromatogram.</summary>
    private void FillTwc(Chromatogram c, bool getBinaryData)
    {
        var (times, intensities) = _wiff.GetTotalWavelengthChromatogram();
        c.DefaultArrayLength = Math.Min(times.Length, intensities.Length);
        if (!getBinaryData) return;

        var t = new BinaryDataArray();
        t.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var iArr = new BinaryDataArray();
        iArr.Set(CVID.MS_intensity_array, "", CVID.UO_absorbance_unit);
        int n = c.DefaultArrayLength;
        for (int i = 0; i < n; i++) t.Data.Add(times[i]);
        for (int i = 0; i < n; i++) iArr.Data.Add(intensities[i]);
        c.BinaryDataArrays.Add(t);
        c.BinaryDataArrays.Add(iArr);
    }

    /// <summary>Fills <paramref name="c"/> with the run-level TIC or BPC, summed over experiments.</summary>
    private void FillTicOrBpc(Chromatogram c, bool isBpc, bool getBinaryData)
    {
        // Cpp ChromatogramList_ABI sums TIC/BPC across experiments by time and tags each point
        // with its ms level. We do the same so MultiMap-of-time → (msLevel, intensity) ordering
        // matches.
        var byTime = new SortedDictionary<double, List<(int MsLevel, double Intensity)>>();

        for (int e = 0; e < _wiff.ExperimentCount; e++)
        {
            MSExperiment? exp;
            try { exp = _wiff.GetExperiment(e); }
            catch { continue; }
            if (_globalChromsAreMs1Only && exp.Details.ExperimentType != ExperimentType.MS) continue;
            int msLevel = exp.GetMsLevelForCycle(1);

            double[] times = Array.Empty<double>();
            double[] intensities = Array.Empty<double>();
            try
            {
                if (isBpc)
                {
                    var bpc = exp.GetBasePeakChromatogram(new BasePeakChromatogramSettings(0, null, null));
                    times = bpc.GetActualXValues() ?? Array.Empty<double>();
                    intensities = bpc.GetActualYValues() ?? Array.Empty<double>();
                }
                else
                {
                    var tic = exp.GetTotalIonChromatogram();
                    times = tic.GetActualXValues() ?? Array.Empty<double>();
                    intensities = tic.GetActualYValues() ?? Array.Empty<double>();
                }
            }
            catch { /* leave empty — see TIC fallback below */ }

            // Cpp WiffFile.cpp:488-530 has a two-tier BPC fetch: first GetBasePeakChromatogram
            // with no time range; if that throws, retry with a constrained time range up to
            // the second-to-last cycle. We mirror the constrained retry, using TIC's cycle
            // times to derive the end. Without this, MRM-only experiments emit an empty BPC.
            if (isBpc && times.Length == 0)
            {
                try
                {
                    var tic = exp.GetTotalIonChromatogram();
                    var ticTimes = tic.GetActualXValues() ?? Array.Empty<double>();
                    if (ticTimes.Length > 0)
                    {
                        int lastUsable = ticTimes.Length > 10 ? ticTimes.Length - 1 : ticTimes.Length;
                        var settings = new BasePeakChromatogramSettings(0, null, null, 0, ticTimes[lastUsable - 1]);
                        var bpc = exp.GetBasePeakChromatogram(settings);
                        times = bpc.GetActualXValues() ?? Array.Empty<double>();
                        intensities = bpc.GetActualYValues() ?? Array.Empty<double>();
                    }
                }
                catch { /* leave empty */ }
            }

            int n = Math.Min(times.Length, intensities.Length);
            for (int i = 0; i < n; i++)
            {
                if (!byTime.TryGetValue(times[i], out var list))
                {
                    list = new List<(int, double)>();
                    byTime[times[i]] = list;
                }
                list.Add((msLevel, intensities[i]));
            }
        }

        c.DefaultArrayLength = byTime.Sum(kv => kv.Value.Count);
        if (!getBinaryData) return;

        var t = new BinaryDataArray();
        t.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var iArr = new BinaryDataArray();
        iArr.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
        var msLevelArr = new IntegerDataArray();
        msLevelArr.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);

        foreach (var (time, points) in byTime)
        {
            foreach (var (lvl, intensity) in points)
            {
                t.Data.Add(time);
                iArr.Data.Add(intensity);
                msLevelArr.Data.Add(lvl);
            }
        }
        c.BinaryDataArrays.Add(t);
        c.BinaryDataArrays.Add(iArr);
        c.IntegerDataArrays.Add(msLevelArr);
    }

    private void FillSrm(Chromatogram c, IndexEntry ie, bool isSrm, bool getBinaryData)
    {
        // Per-transition isolation windows + activation. cpp does the same setup before reading
        // the SIC binary arrays.
        c.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, ie.Q1, CVID.MS_m_z);
        c.Precursor.Activation.Set(CVID.MS_CID);
        if (ie.CollisionEnergy > 0)
            c.Precursor.Activation.Set(CVID.MS_collision_energy, ie.CollisionEnergy, CVID.UO_electronvolt);
        if (isSrm)
            c.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, ie.Q3, CVID.MS_m_z);

        if (ie.Polarity == MSExperimentInfo.PolarityEnum.Positive) c.Params.Set(CVID.MS_positive_scan);
        else if (ie.Polarity == MSExperimentInfo.PolarityEnum.Negative) c.Params.Set(CVID.MS_negative_scan);

        c.Params.UserParams.Add(new UserParam("MS_dwell_time",
            (ie.DwellTimeMs / 1000.0).ToString(CultureInfo.InvariantCulture), "xs:float"));

        // SIC for the transition: GetExtractedIonChromatogram(new ExtractedIonChromatogramSettings(transitionIndex)).
        double[] times = Array.Empty<double>();
        double[] intensities = Array.Empty<double>();
        try
        {
            var exp = _wiff.GetExperiment(ie.ExperimentIndex);
            var sic = exp.GetExtractedIonChromatogram(new ExtractedIonChromatogramSettings(ie.TransitionIndex));
            times = sic.GetActualXValues() ?? Array.Empty<double>();
            intensities = sic.GetActualYValues() ?? Array.Empty<double>();
        }
        catch { /* SDK can throw on certain transition layouts */ }

        c.DefaultArrayLength = Math.Min(times.Length, intensities.Length);
        if (!getBinaryData) return;

        var t = new BinaryDataArray();
        t.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var iArr = new BinaryDataArray();
        iArr.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
        int n = c.DefaultArrayLength;
        if (n > 0)
        {
            for (int i = 0; i < n; i++) t.Data.Add(times[i]);
            for (int i = 0; i < n; i++) iArr.Data.Add(intensities[i]);
        }
        c.BinaryDataArrays.Add(t);
        c.BinaryDataArrays.Add(iArr);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsWiff) _wiff.Dispose();
    }
}
