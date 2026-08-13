#if !NO_VENDOR_SUPPORT
using Agilent.MassSpectrometry.DataAnalysis;
using Agilent.MassSpectrometry.MIDAC;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Agilent;

/// <summary>
/// <see cref="IChromatogramList"/> for Agilent <c>.d</c> directories. C# port of pwiz cpp
/// <c>ChromatogramList_Agilent</c>: emits a run-level TIC, one SRM / SIM chromatogram per
/// transition, then one absorption / pressure / flow-rate chromatogram per non-MS signal
/// exposed by the file's <c>INonmsDataReader</c>, in that order.
/// </summary>
public sealed class ChromatogramList_Agilent : ChromatogramListBase
{
    private readonly AgilentRawData _raw;
    private readonly bool _ownsRaw;
    private readonly bool _globalChromsAreMs1Only;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="raw"/>; <paramref name="ownsRaw"/> selects whether
    /// disposing the list disposes the raw data handle.</summary>
    public ChromatogramList_Agilent(AgilentRawData raw, bool ownsRaw, bool globalChromatogramsAreMs1Only)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _raw = raw;
        _ownsRaw = ownsRaw;
        _globalChromsAreMs1Only = globalChromatogramsAreMs1Only;
        CreateIndex();
    }

    private enum ChromKind { Tic, Signal, Srm, Sim }

    private sealed class IndexEntry : ChromatogramIdentity
    {
        public ChromKind Kind;
        public CVID ChromatogramType;
        public AgilentSignal? Signal;        // null for TIC / SRM / SIM
        public int TransitionIndex = -1;     // index into AgilentRawData.Transitions for SRM/SIM
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => _index[index];

    private void CreateIndex()
    {
        // TIC always comes first. cpp does the same; the run-level TIC is supported on every
        // Agilent file type with MS data.
        _index.Add(new IndexEntry { Index = 0, Id = "TIC", Kind = ChromKind.Tic, ChromatogramType = CVID.MS_TIC_chromatogram });

        // SRM / SIM transitions next (cpp ChromatogramList_Agilent.cpp:266-294 builds the same
        // order: TIC, then one chromatogram per transition from MassHunterData::getTransitions).
        // Id format mirrors cpp's boost.spirit.karma generator output:
        //   SRM: "<polarity>SRM SIC Q1=<q1> Q3=<q3> start=<t> end=<t>"
        //   SIM: "SIM SIC Q1=<q1> start=<t> end=<t>"
        var transitions = _raw.Transitions;
        for (int i = 0; i < transitions.Count; i++)
        {
            var t = transitions[i];
            string id;
            CVID type;
            if (t.Type == AgTransitionType.Mrm)
            {
                // cpp ChromatogramListBase::polarityStringForFilter — empty for positive (so
                // chromatogram IDs round-trip cleanly with the historical "no polarity prefix
                // means positive" convention), "- " for negative. We previously prepended
                // "+ " for positive which made every positive-mode SRM ID diff vs cpp.
                string pol = t.Polarity switch
                {
                    AgTransitionPolarity.Negative => "- ",
                    _ => string.Empty,
                };
                id = $"{pol}SRM SIC Q1={PwizFloat.ToKarmaNoSci(t.Q1)} Q3={PwizFloat.ToKarmaNoSci(t.Q3)} start={PwizFloat.ToKarmaNoSci(t.TimeStart)} end={PwizFloat.ToKarmaNoSci(t.TimeEnd)}";
                type = CVID.MS_SRM_chromatogram;
            }
            else
            {
                id = $"SIM SIC Q1={PwizFloat.ToKarmaNoSci(t.Q1)} start={PwizFloat.ToKarmaNoSci(t.TimeStart)} end={PwizFloat.ToKarmaNoSci(t.TimeEnd)}";
                type = CVID.MS_SIM_chromatogram;
            }
            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = id,
                Kind = t.Type == AgTransitionType.Mrm ? ChromKind.Srm : ChromKind.Sim,
                ChromatogramType = type,
                TransitionIndex = i,
            });
        }

        // Non-MS signals: UV/DAD absorption, pump pressure and flow curves. AgilentRawData.Signals
        // is the port of cpp MassHunterDataImpl::getSignals - every row of each device's
        // Chromatograms table, then every row of its InstrumentCurves table. cpp
        // ChromatogramList_Agilent.cpp:296-314 walks that list in order and skips whatever
        // translateAsChromatogramType doesn't recognize.
        foreach (var signal in _raw.Signals)
        {
            CVID chromatogramType = TranslateAsChromatogramType(signal);
            if (chromatogramType == CVID.CVID_Unknown || chromatogramType == CVID.MS_chromatogram)
                continue;

            // cpp's id is `signal.deviceName + " " + signal.signalName`, where `signal.deviceName`
            // is the deviceNameAndOrdinal (e.g. "DAD1"), not the bare device name.
            string id = signal.DeviceName + " " + signal.SignalName;
            if (signal.SignalDescription.Length > 0)
                id += ": " + signal.SignalDescription;

            _index.Add(new IndexEntry
            {
                Index = _index.Count,
                Id = id,
                Kind = ChromKind.Signal,
                ChromatogramType = chromatogramType,
                Signal = signal,
            });
        }
    }

    /// <summary>Port of cpp <c>translateAsChromatogramType</c>
    /// (<c>Reader_Agilent_Detail.cpp:231</c>). Pumps and samplers are classified by what their
    /// signal description mentions; detectors give absorption chromatograms unless the signal is
    /// an instrument curve. Anything else is skipped by the indexer.</summary>
    internal static CVID TranslateAsChromatogramType(AgilentSignal signal)
    {
        switch (signal.DeviceType)
        {
            // samplers - cpp falls these through into the pump cases, so they share the
            // description-driven classification below
            case DeviceType.ALS: // automatic liquid sampler
            case DeviceType.WellPlateSampler:
            case DeviceType.MicroWellPlateSampler:
            case DeviceType.CompactLCSampler:
            case DeviceType.CompactLC1220Sampler:
            case DeviceType.CTC: // CTC Analytics

            // pumps
            case DeviceType.IsocraticPump:
            case DeviceType.BinaryPump:
            case DeviceType.QuaternaryPump:
            case DeviceType.CapillaryPump:
            case DeviceType.NanoPump:
            case DeviceType.LowFlowPump:
            case DeviceType.CompactLCIsoPump:
            case DeviceType.CompactLCGradPump:
            case DeviceType.CompactLC1220IsoPump:
            case DeviceType.CompactLC1220GradPump:
            case DeviceType.PumpValveCluster:
            case DeviceType.CANValves:
                // cpp has a third branch here testing "pressure" a second time and returning
                // MS_chromatogram; it is unreachable, and MS_chromatogram would be skipped by
                // the indexer anyway.
                if (signal.SignalDescription.Contains("flow", StringComparison.OrdinalIgnoreCase))
                    return CVID.MS_flow_rate_chromatogram;
                if (signal.SignalDescription.Contains("pressure", StringComparison.OrdinalIgnoreCase))
                    return CVID.MS_pressure_chromatogram;
                return CVID.CVID_Unknown;

            case DeviceType.FlameIonizationDetector:
            case DeviceType.ThermalConductivityDetector:
            case DeviceType.RefractiveIndexDetector:
            case DeviceType.MultiWavelengthDetector:
            case DeviceType.DiodeArrayDetector:
            case DeviceType.VariableWavelengthDetector:
            case DeviceType.AnalogDigitalConverter:
            case DeviceType.ElectronCaptureDetector:
            case DeviceType.FluorescenceDetector:
            case DeviceType.EvaporativeLightScatteringDetector:
            case DeviceType.CompactLCVWD:
            case DeviceType.CompactLC1220VWD:
            case DeviceType.CompactLC1220DAD:
            case DeviceType.NitrogenPhosphorousDetector:
            case DeviceType.FlamePhotometricDetector:
            case DeviceType.GCDetector:
            case DeviceType.UIB2:
                return signal.IsInstrumentCurve ? CVID.CVID_Unknown : CVID.MS_absorption_chromatogram;

            default:
                return CVID.CVID_Unknown;
        }
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
            case ChromKind.Tic:
                FillTic(c, getBinaryData);
                break;

            case ChromKind.Signal:
                FillSignal(c, ie, getBinaryData);
                break;

            case ChromKind.Srm:
            case ChromKind.Sim:
                FillTransition(c, ie, getBinaryData);
                break;
        }

        return c;
    }

    private void FillTransition(Chromatogram c, IndexEntry ie, bool getBinaryData)
    {
        if (ie.TransitionIndex < 0)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var transitions = _raw.Transitions;
        if (ie.TransitionIndex >= transitions.Count)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var t = transitions[ie.TransitionIndex];

        // Polarity tag mirrors cpp ChromatogramList_Agilent.cpp:139-141 / :174-176.
        if (t.Polarity == AgTransitionPolarity.Positive) c.Params.Set(CVID.MS_positive_scan);
        else if (t.Polarity == AgTransitionPolarity.Negative) c.Params.Set(CVID.MS_negative_scan);

        // Precursor / product isolation windows. SRM has both Q1 and Q3 bounds; SIM has Q1
        // only. cpp also emits MS_CID + collision energy on the precursor activation, but the
        // collision energy comes from the chromatogram object; we'll wire that in if/when
        // a fixture surfaces a non-zero CE diff.
        c.Precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, t.Q1, CVID.MS_m_z);
        if (ie.Kind == ChromKind.Srm)
        {
            c.Precursor.Activation.Set(CVID.MS_CID);
            // cpp ChromatogramList_Agilent.cpp:144 — collision energy reads as a unit-less
            // scalar from the SDK (no unit cvParam attached). Match that.
            c.Precursor.Activation.Set(CVID.MS_collision_energy, t.CollisionEnergy);
            c.Product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, t.Q3, CVID.MS_m_z);
        }

        // Binary data — pull from the cached chromatogram the SDK returned during transition
        // discovery. cpp also caches by reference (member array) for the same 50x perf reason.
        var chrom = _raw.GetTransitionChromatogram(ie.TransitionIndex);
        if (chrom is null)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var x = chrom.XArray ?? Array.Empty<double>();
        var y = chrom.YArray ?? Array.Empty<float>();
        int n = Math.Min(x.Length, y.Length);
        c.DefaultArrayLength = n;
        if (getBinaryData && n > 0)
        {
            var times = new BinaryDataArray();
            times.Set(CVID.MS_time_array, "", CVID.UO_minute);
            for (int i = 0; i < n; i++) times.Data.Add(x[i]);
            var inten = new BinaryDataArray();
            inten.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
            for (int i = 0; i < n; i++) inten.Data.Add(y[i]);
            c.BinaryDataArrays.Add(times);
            c.BinaryDataArrays.Add(inten);
        }
    }

    private void FillTic(Chromatogram c, bool getBinaryData)
    {
        // Build TIC by walking scan records (RetentionTime, TIC). cpp does the same in
        // MassHunterDataImpl ctor: SDK's GetTIC() returns an IBDAChromData but populating it
        // is awkward and the per-scan-record path mirrors what cpp emits exactly.
        // For IM files, the per-frame TIC from MIDAC's frameInfo (which is what cpp's
        // MidacDataImpl::getTicIntensities returns) gives NaN for frames with no data; the
        // MassSpec SDK's rec.Tic gives 0.0 for those same frames. Mirror cpp's value.
        int total = (int)_raw.TotalScansPresent;
        bool onlyMs1 = _globalChromsAreMs1Only;
        bool isIms = _raw.HasIonMobilityData;
        var imsReader = isIms ? _raw.ImsReader : null;
        var times = new List<double>(total);
        var intensities = new List<double>(total);
        var msLevels = new List<long>(total);
        if (isIms && imsReader is not null)
        {
            // IM path: one entry per MIDAC frame. Use per-frame Tic (NaN for empty frames).
            int frames = _raw.ImsFrameCount;
            for (int i = 0; i < frames; i++)
            {
                int frameNumber = _raw.ImsFrameNumber(i);
                IMidacFrameInfo? info = null;
                try { info = imsReader.FrameInfo(frameNumber); } catch { }
                if (info is null) continue;
                int level = 1;
                double rt = 0;
                double tic = double.NaN;
                try { rt = info.AcqTimeRange?.Min ?? 0; } catch { }
                try { tic = info.Tic; } catch { }
                try
                {
                    var details = info.SpectrumDetails;
                    if (details is not null)
                    {
                        int lvl = (int)(object)details.MsLevel!;
                        level = lvl == 2 ? 2 : 1;
                    }
                }
                catch { }
                if (onlyMs1 && level != 1) continue;
                times.Add(rt);
                // cpp caches TIC intensities as BinaryData<float> (MassHunterData.cpp:144)
                // and assigns them straight into the mzML double array, so every value it
                // writes is float-rounded. Keeping the SDK double left all eight
                // single-diff Agilent files off by ~5e-08 relative - float32 epsilon.
                intensities.Add((float)tic);
                msLevels.Add(level);
            }
        }
        else
        {
            for (int i = 0; i < total; i++)
            {
                IMSScanRecord rec;
                try { rec = _raw.GetScanRecord(i); }
                catch { continue; }
                int level = rec.MSLevel == MSLevel.MSMS ? 2 : 1;
                if (onlyMs1 && level != 1) continue;
                times.Add(rec.RetentionTime);
                // cpp caches TIC intensities as BinaryData<float> (MassHunterData.cpp:144)
                // and assigns them straight into the mzML double array, so every value it
                // writes is float-rounded. Keeping the SDK double left all eight
                // single-diff Agilent files off by ~5e-08 relative - float32 epsilon.
                intensities.Add((float)rec.Tic);
                msLevels.Add(level);
            }
        }
        c.DefaultArrayLength = times.Count;
        // cpp ChromatogramList_Agilent.cpp:109-128 (MS_TIC_chromatogram) always calls
        // setTimeIntensityArrays and emplaces the "ms level" integer array whenever binary data
        // is requested, so the TIC carries time/intensity/ms-level arrays even when empty. When
        // globalChromatogramsAreMs1Only filters out every scan of an all-MS2 CNL file the TIC is
        // legitimately empty, but the arrays must still exist: pwiz.ProteowizardWrapper
        // .MsDataFileImpl.GetChromatogram dereferences GetTimeArray().Data unconditionally (and
        // has an explicit empty-MS1-TIC placeholder path), so a missing time array throws
        // NullReferenceException on import. Guarding on times.Count > 0 dropped the arrays.
        if (getBinaryData)
        {
            var t = new BinaryDataArray();
            t.Set(CVID.MS_time_array, "", CVID.UO_minute);
            t.Data.AddRange(times);
            var y = new BinaryDataArray();
            y.Set(CVID.MS_intensity_array, "", CVID.MS_number_of_detector_counts);
            y.Data.AddRange(intensities);
            c.BinaryDataArrays.Add(t);
            c.BinaryDataArrays.Add(y);

            // ms-level integer array — cpp emits this on the TIC so consumers can filter by
            // MS level without re-walking the scan list.
            var ms = new IntegerDataArray();
            ms.Set(CVID.MS_non_standard_data_array, "ms level", CVID.UO_dimensionless_unit);
            ms.Data.AddRange(msLevels);
            c.IntegerDataArrays.Add(ms);
        }
    }

    private void FillSignal(Chromatogram c, IndexEntry ie, bool getBinaryData)
    {
        if (ie.Signal is null)
        {
            c.DefaultArrayLength = 0;
            return;
        }
        var signalData = _raw.GetSignal(ie.Signal);
        if (signalData is null) { c.DefaultArrayLength = 0; return; }

        double[] times = signalData.XArray ?? Array.Empty<double>();
        float[] yArray = signalData.YArray ?? Array.Empty<float>();
        int n = Math.Min(times.Length, yArray.Length);
        c.DefaultArrayLength = n;
        if (getBinaryData)
        {
            // cpp ChromatogramList_Agilent.cpp:209-232: the intensity unit and the scale factor
            // are both chosen from the chromatogram type. Neither conversion is dimensionally
            // what its cpp comment claims, but the emitted values are what the reference mzML
            // holds, so reproduce them exactly.
            CVID intensityUnit = CVID.UO_absorbance_unit;
            double unitMultiplier = 1.0;
            if (ie.ChromatogramType == CVID.MS_pressure_chromatogram)
            {
                intensityUnit = CVID.UO_pascal;
                unitMultiplier = 1e5; // cpp: convert bar to pascal (1 bar = 100000 Pa), no bar term in UO
            }
            else if (ie.ChromatogramType == CVID.MS_flow_rate_chromatogram)
            {
                intensityUnit = CVID.UO_microliters_per_minute;
                unitMultiplier = 1e-6; // cpp: convert mL/min to uL/min, no mL/min term in UO
            }
            EmitTimeIntensityArrays(c, times, yArray, n, intensityUnit, unitMultiplier);
        }
    }

    private static void EmitTimeIntensityArrays(Chromatogram c, double[] times, float[] yArray, int n,
                                                CVID intensityUnit, double intensityMultiplier = 1.0)
    {
        var t = new BinaryDataArray();
        t.Set(CVID.MS_time_array, "", CVID.UO_minute);
        var y = new BinaryDataArray();
        y.Set(CVID.MS_intensity_array, "", intensityUnit);
        for (int i = 0; i < n; i++) t.Data.Add(times[i]);
        // cpp widens each float to double first and scales in double, so scale after the widening
        // conversion rather than in single precision.
        for (int i = 0; i < n; i++) y.Data.Add(yArray[i] * intensityMultiplier);
        c.BinaryDataArrays.Add(t);
        c.BinaryDataArrays.Add(y);
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_ownsRaw) _raw.Dispose();
        base.DisposeCore();
    }
}
#endif
