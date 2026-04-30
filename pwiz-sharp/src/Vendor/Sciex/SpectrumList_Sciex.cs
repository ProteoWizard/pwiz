using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// <see cref="ISpectrumList"/> for both <c>.wiff</c> and <c>.wiff2</c> files. C# port of pwiz
/// cpp <c>SpectrumList_ABI</c>: walks (experiment, cycle) pairs in the selected sample,
/// drops empty cycles by checking BPC (or TIC when BPC isn't available), and emits one mzML
/// spectrum per surviving cycle, sorted by RT across all experiments. Works against the
/// <see cref="AbstractWiffFile"/> abstraction so a single code path covers both SDKs.
/// </summary>
public sealed class SpectrumList_Sciex : SpectrumListBase, IDisposable
{
    private readonly AbstractWiffFile _wiff;
    private readonly bool _ownsWiff;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _simAsSpectra;
    private readonly bool _srmAsSpectra;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="wiff"/>; <paramref name="ownsWiff"/> selects whether
    /// disposing the list disposes the wiff.</summary>
    public SpectrumList_Sciex(AbstractWiffFile wiff, bool ownsWiff,
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
        public WiffExperimentType ExperimentType;
        public int MsLevel;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        // Mirrors cpp SpectrumList_ABI::createIndex: walk each experiment, drop empty cycles by
        // checking BPC (fall back to TIC) intensity > 0, then sort survivors by RT across all
        // experiments. Native id includes period/cycle/experiment in the cpp order.
        var sortedByTime = new SortedDictionary<double, List<(int Experiment, int Cycle, WiffExperimentType Type, int MsLevel)>>();

        for (int e = 0; e < _wiff.ExperimentCount; e++)
        {
            AbstractWiffExperiment exp;
            try { exp = _wiff.GetExperiment(e); }
            catch { continue; }

            var expType = exp.ExperimentType;
            int msLevel = exp.GetMsLevelForCycle(1);

            if (expType == WiffExperimentType.MRM && !_srmAsSpectra) continue;
            if (expType == WiffExperimentType.SIM && !_simAsSpectra) continue;

            var (times, intensities) = exp.GetBpc();
            if (times.Length == 0) (times, intensities) = exp.GetTic();

            int n = Math.Min(times.Length, intensities.Length);
            for (int i = 0; i < n; i++)
            {
                if (intensities[i] <= 0) continue;

                // Cpp also requires the spectrum to have non-zero peaks (ignoreZeroIntensityPoints=true).
                // Approximate with a per-cycle peak presence check via GetSpectrum.
                var probe = exp.GetSpectrum(i + 1, addZeros: false, centroid: false);
                bool hasPeaks = false;
                if (probe is not null)
                {
                    var ys = probe.YValues;
                    for (int k = 0; k < ys.Length; k++) if (ys[k] > 0) { hasPeaks = true; break; }
                }
                if (!hasPeaks) continue;

                if (!sortedByTime.TryGetValue(times[i], out var list))
                {
                    list = new List<(int, int, WiffExperimentType, int)>();
                    sortedByTime[times[i]] = list;
                }
                list.Add((e, i + 1, expType, msLevel));
            }
        }

        foreach (var (_, entries) in sortedByTime)
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
        int msLevel = exp.GetMsLevelForCycle(ie.Cycle);

        var spec = new Spectrum
        {
            Index = index,
            Id = ie.Id,
        };

        spec.Params.Set(CVID.MS_ms_level, msLevel);
        spec.Params.Set(TranslateAsSpectrumType(ie.ExperimentType, msLevel));
        if (exp.Polarity == WiffPolarity.Positive) spec.Params.Set(CVID.MS_positive_scan);
        else if (exp.Polarity == WiffPolarity.Negative) spec.Params.Set(CVID.MS_negative_scan);

        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Scans.Add(scan);

        try
        {
            double rtMin = exp.GetRetentionTime(ie.Cycle);
            scan.Set(CVID.MS_scan_start_time, rtMin, CVID.UO_minute);
        }
        catch { /* not all experiments have RT */ }

        if (exp.StartMass < exp.EndMass)
            scan.ScanWindows.Add(new ScanWindow(exp.StartMass, exp.EndMass, CVID.MS_m_z));

        // Profile vs centroid + AddZeros padding for profile data are handled inside the
        // AbstractWiffSpectrum implementation (legacy: AddZeros via Clearcore2; wiff2: AddFramingZeros
        // via the SDK request).
        var ms = exp.GetSpectrum(ie.Cycle, addZeros: true, centroid: false);

        if (ms is not null)
        {
            spec.Params.Set(ms.CentroidMode ? CVID.MS_centroid_spectrum : CVID.MS_profile_spectrum);

            if (msLevel > 1 && ms.HasPrecursorInfo)
            {
                var precursor = new Precursor();
                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, ms.PrecursorMz, CVID.MS_m_z);
                if (ms.IsolationLowerOffset > 0)
                    precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, ms.IsolationLowerOffset, CVID.MS_m_z);
                if (ms.IsolationUpperOffset > 0)
                    precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, ms.IsolationUpperOffset, CVID.MS_m_z);

                var selected = new SelectedIon();
                selected.Set(CVID.MS_selected_ion_m_z, ms.PrecursorMz, CVID.MS_m_z);
                if (ms.PrecursorCharge > 0)
                    selected.Set(CVID.MS_charge_state, ms.PrecursorCharge);
                precursor.SelectedIons.Add(selected);

                if (ms.Activation == WiffActivation.EAD)
                {
                    precursor.Activation.Set(CVID.MS_electron_activated_dissociation);
                    if (ms.ElectronKineticEnergy > 0)
                        precursor.Activation.Set(CVID.MS_electron_beam_energy, ms.ElectronKineticEnergy, CVID.UO_electronvolt);
                }
                else
                {
                    precursor.Activation.Set(CVID.MS_beam_type_collision_induced_dissociation);
                }
                if (ms.CollisionEnergy != 0)
                    precursor.Activation.Set(CVID.MS_collision_energy, ms.CollisionEnergy, CVID.UO_electronvolt);
                spec.Precursors.Add(precursor);
            }

            if (getBinaryData)
            {
                double[] x = ms.XValues;
                double[] y = ms.YValues;
                int n = Math.Min(x.Length, y.Length);
                spec.DefaultArrayLength = n;
                if (n > 0) spec.SetMZIntensityArrays(SliceDouble(x, n), SliceDouble(y, n), CVID.MS_number_of_detector_counts);
            }
            else
            {
                spec.DefaultArrayLength = ms.XValues.Length;
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

    /// <summary>Maps wiff experiment type + msLevel to a mzML spectrum-type CVID. Equivalent of
    /// cpp <c>Reader_ABI_Detail::translateAsSpectrumType</c>.</summary>
    public static CVID TranslateAsSpectrumType(WiffExperimentType expType, int msLevel) => expType switch
    {
        WiffExperimentType.MS => msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum,
        WiffExperimentType.Product => CVID.MS_MSn_spectrum,
        WiffExperimentType.Precursor => CVID.MS_precursor_ion_spectrum,
        WiffExperimentType.NeutralGainOrLoss => CVID.MS_constant_neutral_loss_spectrum,
        WiffExperimentType.SIM => CVID.MS_SIM_spectrum,
        WiffExperimentType.MRM => CVID.MS_SRM_spectrum,
        _ => CVID.MS_MSn_spectrum,
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsWiff) _wiff.Dispose();
    }
}
