using System.Globalization;
using Clearcore2.Data;
using Clearcore2.Data.DataAccess.SampleData;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// <see cref="ISpectrumList"/> backed by a Sciex <see cref="WiffData"/>. Initial port of pwiz
/// C++ <c>SpectrumList_ABI</c>: enumerates (experiment, cycle) pairs in the selected sample and
/// emits one mzML spectrum per cycle. SRM/SIM-only experiments stay as chromatogram-centric
/// unless the matching <c>...AsSpectra</c> flag is set.
/// </summary>
public sealed class SpectrumList_Sciex : SpectrumListBase, IDisposable
{
    private readonly WiffData _wiff;
    private readonly bool _ownsWiff;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _simAsSpectra;
    private readonly bool _srmAsSpectra;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="wiff"/>. Caller chooses if disposing the list disposes the wiff.</summary>
    public SpectrumList_Sciex(WiffData wiff, bool ownsWiff,
        InstrumentConfiguration? defaultInstrumentConfiguration,
        bool simAsSpectra, bool srmAsSpectra)
    {
        ArgumentNullException.ThrowIfNull(wiff);
        _wiff = wiff;
        _ownsWiff = ownsWiff;
        _defaultIc = defaultInstrumentConfiguration;
        _simAsSpectra = simAsSpectra;
        _srmAsSpectra = srmAsSpectra;
        CreateIndex();
    }

    private sealed class IndexEntry : SpectrumIdentity
    {
        public int ExperimentIndex;
        public int Cycle;            // 1-based cycle within experiment
        public ExperimentType ExperimentType;
        public int MsLevel;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        // Mirrors cpp SpectrumList_ABI::createIndex: walks each experiment, drops empty cycles
        // by checking BPC (fall back to TIC) intensity > 0, then sorts the survivors by RT
        // across all experiments. The native id includes period/cycle/experiment in the same
        // order the cpp reader emits.
        var sortedByTime = new SortedDictionary<double, List<(int Experiment, int Cycle, ExperimentType Type, int MsLevel)>>();

        for (int e = 0; e < _wiff.ExperimentCount; e++)
        {
            MSExperiment? exp;
            try { exp = _wiff.GetExperiment(e); }
            catch { continue; }

            var info = exp.Details;
            var expType = info.ExperimentType;
            int msLevel = exp.GetMsLevelForCycle(1);

            if (expType == ExperimentType.MRM && !_srmAsSpectra) continue;
            if (expType == ExperimentType.SIM && !_simAsSpectra) continue;

            // Pull BPC (preferred — narrower; cpp falls back to TIC when BPC is empty).
            double[] times = Array.Empty<double>();
            double[] intensities = Array.Empty<double>();
            try
            {
                var bpc = exp.GetBasePeakChromatogram(new BasePeakChromatogramSettings(0, null, null));
                times = bpc.GetActualXValues() ?? Array.Empty<double>();
                intensities = bpc.GetActualYValues() ?? Array.Empty<double>();
            }
            catch { /* BPC not always available */ }
            if (times.Length == 0)
            {
                try
                {
                    var tic = exp.GetTotalIonChromatogram();
                    times = tic.GetActualXValues() ?? Array.Empty<double>();
                    intensities = tic.GetActualYValues() ?? Array.Empty<double>();
                }
                catch { /* leave empty */ }
            }

            int n = Math.Min(times.Length, intensities.Length);
            for (int i = 0; i < n; i++)
            {
                if (intensities[i] <= 0) continue;

                // Cpp also requires the spectrum to have non-zero peaks (ignoreZeroIntensityPoints=true).
                // For now we approximate with a per-cycle peak-count check via GetMassSpectrum.
                // Slightly heavier than cpp (we load the spectrum twice) but much simpler.
                int peaks = 0;
                try
                {
                    var ms = exp.GetMassSpectrum(i);
                    var ys = ms.GetActualYValues();
                    if (ys is not null)
                        for (int k = 0; k < ys.Length; k++) if (ys[k] > 0) { peaks++; break; }
                }
                catch { peaks = 0; }
                if (peaks == 0) continue;

                if (!sortedByTime.TryGetValue(times[i], out var list))
                {
                    list = new List<(int, int, ExperimentType, int)>();
                    sortedByTime[times[i]] = list;
                }
                list.Add((e, i + 1, expType, msLevel));
            }
        }

        foreach (var (rt, entries) in sortedByTime)
        {
            foreach (var (e, c, expType, msLevel) in entries)
            {
                _index.Add(new IndexEntry
                {
                    Index = _index.Count,
                    Id = $"sample={_wiff.SampleNumber} period=1 cycle={c} experiment={e + 1}",
                    ExperimentIndex = e,
                    Cycle = c,
                    ExperimentType = expType,
                    MsLevel = msLevel,
                });
            }
        }
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        if (index < 0 || index >= _index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var ie = _index[index];
        var exp = _wiff.GetExperiment(ie.ExperimentIndex);
        var info = exp.Details;
        int msLevel = exp.GetMsLevelForCycle(ie.Cycle);

        var spec = new Spectrum
        {
            Index = index,
            Id = ie.Id,
        };

        spec.Params.Set(CVID.MS_ms_level, msLevel);
        spec.Params.Set(TranslateAsSpectrumType(info.ExperimentType, msLevel));
        if (info.Polarity == MSExperimentInfo.PolarityEnum.Positive) spec.Params.Set(CVID.MS_positive_scan);
        else if (info.Polarity == MSExperimentInfo.PolarityEnum.Negative) spec.Params.Set(CVID.MS_negative_scan);

        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Scans.Add(scan);

        // RT comes from the experiment; cpp uses convertCycleToRetentionTime which is the same.
        try
        {
            double rtMin = exp.GetRTFromExperimentCycle(ie.Cycle);
            scan.Set(CVID.MS_scan_start_time, rtMin, CVID.UO_minute);
        }
        catch { /* not all experiments have RT */ }

        // Mass range — MSExperimentInfo exposes StartMass/EndMass directly. (The MassRange[]
        // collection on MassRangeInfo only carries dwell-time / per-transition parameters.)
        if (info.StartMass < info.EndMass)
            scan.ScanWindows.Add(new ScanWindow(info.StartMass, info.EndMass, CVID.MS_m_z));

        MassSpectrum? ms = null;
        try { ms = exp.GetMassSpectrum(ie.Cycle - 1); } // experimentScanIndex is 0-based
        catch { /* corrupt cycle */ }

        if (ms is not null)
        {
            var msInfo = ms.Info;
            spec.Params.Set(msInfo.CentroidMode ? CVID.MS_centroid_spectrum : CVID.MS_profile_spectrum);

            // Cpp WiffFile.cpp:872-873 calls msExperiment->AddZeros(spectrum, 1) for profile
            // (continuous) data. AddZeros pads each peak with single zero-intensity flanking
            // points so consumers see explicit baseline. Centroided spectra and the special
            // SIM/MRM x-axis paths (handled below for chromatogram-only experiments) skip it.
            if (!msInfo.CentroidMode)
            {
                try { exp.AddZeros(ms, 1); }
                catch { /* AddZeros isn't always applicable */ }
            }

            if (msLevel > 1 && msInfo.ParentMZ > 0)
            {
                var precursor = new Precursor();
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, msInfo.ParentMZ, CVID.MS_m_z);
                var selected = new SelectedIon();
                selected.Set(CVID.MS_selected_ion_m_z, msInfo.ParentMZ, CVID.MS_m_z);
                if (msInfo.ParentChargeState > 0)
                    selected.Set(CVID.MS_charge_state, msInfo.ParentChargeState);
                precursor.SelectedIons.Add(selected);

                precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation);
                if (msInfo.CollisionEnergy != 0)
                    precursor.Activation.Set(CVID.MS_collision_energy, Math.Abs(msInfo.CollisionEnergy), CVID.UO_electronvolt);
                spec.Precursors.Add(precursor);
            }

            if (getBinaryData)
            {
                double[] x = ms.GetActualXValues() ?? Array.Empty<double>();
                double[] y = ms.GetActualYValues() ?? Array.Empty<double>();
                int n = Math.Min(x.Length, y.Length);
                spec.DefaultArrayLength = n;
                if (n > 0) spec.SetMZIntensityArrays(SliceDouble(x, n), SliceDouble(y, n), CVID.MS_number_of_detector_counts);
            }
            else
            {
                spec.DefaultArrayLength = ms.GetActualXValues()?.Length ?? 0;
            }
        }
        else
        {
            spec.Params.Set(CVID.MS_centroid_spectrum);
        }

        return spec;
    }

    private static double[] SliceDouble(double[] src, int len)
    {
        if (len == src.Length) return src;
        var dst = new double[len];
        Array.Copy(src, dst, len);
        return dst;
    }

    /// <summary>
    /// Maps Sciex <see cref="ExperimentType"/> + msLevel to a mzML spectrum-type CVID. Cpp
    /// <c>Reader_ABI_Detail::translateAsSpectrumType</c> equivalent.
    /// </summary>
    public static CVID TranslateAsSpectrumType(ExperimentType expType, int msLevel) => expType switch
    {
        ExperimentType.MS => msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum,
        ExperimentType.Product => CVID.MS_MSn_spectrum,
        ExperimentType.Precursor => CVID.MS_precursor_ion_spectrum,
        ExperimentType.NeutralGainOrLoss => CVID.MS_constant_neutral_loss_spectrum,
        ExperimentType.SIM => CVID.MS_SIM_spectrum,
        ExperimentType.MRM => CVID.MS_SRM_spectrum,
        _ => CVID.MS_MSn_spectrum,
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsWiff) _wiff.Dispose();
    }
}

/// <summary>Extension to fetch a per-cycle ms level from an experiment, with a fallback when
/// the SDK doesn't expose the variant we need on the public surface.</summary>
internal static class MSExperimentExtensions
{
    /// <summary>Per-cycle ms level. Mirrors cpp <c>ExperimentImpl::getMsLevel</c>: ask the SDK
    /// via <see cref="MSExperiment.GetMassSpectrumInfo"/> so MRM/SIM cycles report whatever ms
    /// level the instrument actually used (typically 1 for MRM, 2 for IDA Product). Falls back
    /// to an experiment-type heuristic when the per-cycle info isn't available.</summary>
    public static int GetMsLevelForCycle(this MSExperiment exp, int cycle)
    {
        try
        {
            int level = exp.GetMassSpectrumInfo(cycle - 1).MSLevel;
            if (level > 0) return level;
        }
        catch { /* ignore — fall back to experiment-type default */ }
        var info = exp.Details;
        return info.ExperimentType switch
        {
            ExperimentType.MS => 1,
            ExperimentType.SIM => 1,
            ExperimentType.Product => 2,
            ExperimentType.Precursor => 2,
            ExperimentType.NeutralGainOrLoss => 2,
            ExperimentType.MRM => 1,   // cpp reports msLevel=1 for MRM cycles via the SDK
            _ => 1,
        };
    }
}
