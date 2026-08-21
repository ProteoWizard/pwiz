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
public sealed class SpectrumList_Sciex : SpectrumListBase, IVendorCentroidingSpectrumList
{
    private readonly AbstractWiffFile _wiff;
    private readonly bool _ownsWiff;
    private readonly InstrumentConfiguration? _defaultIc;
    private readonly bool _simAsSpectra;
    private readonly bool _srmAsSpectra;
    private readonly bool _ignoreZeroIntensityPoints;
    private readonly bool _acceptZeroLengthSpectra;
    private readonly bool _verifyNonEmptySpectra;
    private readonly List<IndexEntry> _index = new();

    /// <summary>DataProcessing emitted as the document's <c>defaultDataProcessingRef</c>.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <summary>Wraps <paramref name="wiff"/>; <paramref name="ownsWiff"/> selects whether
    /// disposing the list disposes the wiff. <paramref name="ignoreZeroIntensityPoints"/> is
    /// cpp <c>Reader::Config::ignoreZeroIntensityPoints</c> (msconvert
    /// <c>--ignoreMissingZeroSamples</c>): when set, the SDK is asked NOT to synthesize the
    /// flanking zero samples around profile peaks. <paramref name="acceptZeroLengthSpectra"/>
    /// is cpp <c>Reader::Config::acceptZeroLengthSpectra</c> (msconvert
    /// <c>--acceptZeroLengthSpectra</c>): when set, the index is built from the TIC without
    /// probing each cycle for content, so empty cycles survive into the output.
    /// <paramref name="verifyNonEmptySpectra"/> is
    /// <c>ReaderConfig.VerifyNonEmptySpectraAtIndex</c> - cpp's per-cycle read, which is optional
    /// here because of its cost.</summary>
    public SpectrumList_Sciex(AbstractWiffFile wiff, bool ownsWiff,
        InstrumentConfiguration? defaultInstrumentConfiguration,
        bool simAsSpectra, bool srmAsSpectra, bool ignoreZeroIntensityPoints = false,
        bool acceptZeroLengthSpectra = false, bool verifyNonEmptySpectra = false)
    {
        ArgumentNullException.ThrowIfNull(wiff);
        _wiff = wiff;
        _ownsWiff = ownsWiff;
        _defaultIc = defaultInstrumentConfiguration;
        _simAsSpectra = simAsSpectra;
        _srmAsSpectra = srmAsSpectra;
        _ignoreZeroIntensityPoints = ignoreZeroIntensityPoints;
        _acceptZeroLengthSpectra = acceptZeroLengthSpectra;
        _verifyNonEmptySpectra = verifyNonEmptySpectra;
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

        // cpp SpectrumList_ABI.cpp:283 — the acceptZeroLengthSpectra branch keeps every wiff1
        // TIC cycle regardless of intensity, but still drops zero-intensity cycles on wiff2.
        bool isWiff2 = _wiff.WiffPath.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase);

        for (int e = 0; e < _wiff.ExperimentCount; e++)
        {
            AbstractWiffExperiment exp;
            try { exp = _wiff.GetExperiment(e); }
            catch { continue; }

            var expType = exp.ExperimentType;
            int msLevel = exp.GetMsLevelForCycle(1);

            if (expType == WiffExperimentType.MRM && !_srmAsSpectra) continue;
            if (expType == WiffExperimentType.SIM && !_simAsSpectra) continue;

            double[] times, intensities;
            if (_acceptZeroLengthSpectra)
            {
                // cpp SpectrumList_ABI.cpp:298-306: the flag's whole point is to skip the
                // expensive per-cycle emptiness probe, so the index is built off the TIC alone
                // (never the BPC, which some SDK paths compute by scanning the spectra).
                (times, intensities) = exp.GetTic();
            }
            else
            {
                (times, intensities) = exp.GetBpc();
                if (times.Length == 0) (times, intensities) = exp.GetTic();
            }

            int n = Math.Min(times.Length, intensities.Length);
            for (int i = 0; i < n; i++)
            {
                if (_acceptZeroLengthSpectra)
                {
                    // cpp SpectrumList_ABI.cpp:303-305. wiff1 keeps every cycle (that is what
                    // "accept zero length spectra" buys you); wiff2 still needs intensity > 0
                    // because its TIC is padded across experiments. Product (MS2) experiments
                    // additionally require precursor info, which is the one per-cycle read cpp
                    // still pays for on this branch.
                    if (isWiff2 && intensities[i] <= 0) continue;
                    if (expType == WiffExperimentType.Product
                        && exp.GetSpectrum(i + 1, addZeros: false, centroid: false)?.HasPrecursorInfo != true)
                        continue;
                }
                // cpp SpectrumList_ABI.cpp:314 drops a cycle unless BOTH the per-cycle BPC
                // (fallback TIC) intensity is > 0 AND getSpectrum(...)->getDataSize(false, true)
                // is > 0. This is the first half.
                else if (intensities[i] <= 0)
                    continue;
                // The second half, off by default because of what it costs: it reads the file's
                // entire spectral payload at index-build time, on EVERY open including
                // GraphFullScan's scan-load reopen, which made opening a large TripleTOF MS1
                // .wiff exceed the full-scan graph's load timeout on .NET 8. msconvert turns it
                // on; interactive callers leave it off and accept the empty spectra.
                //
                // There is no cheap stand-in for it. A cycle-TIC > 0 test looked equivalent -
                // the TIC is Spectrum::getSumY, a sum over the points getDataSize counts - and
                // it does catch the sentinel cycles (wine yeast sampleA_2.wiff has 834 MS2
                // cycles reporting base peak 1.0 at m/z 2.35e-07 with nothing behind them). But
                // the implication does not hold in the other direction: "checkmix 1.wiff"
                // sample 7 cycle 58 reports a cycle TIC of exactly 0.0 while carrying 9 real
                // points with a base peak of 2.0, and cpp keeps it. Gating on the TIC dropped
                // that cycle and shifted every id after it.
                //
                // addZeros:false is cpp's ignoreZeroIntensityPoints:true, so this counts the
                // same points getDataSize(false, true) counts.
                //
                // Legacy .wiff only. On wiff2 each GetSpectrum is a full SDK read request, so
                // probing every cycle at index-build time cost more than the conversion itself:
                // 250814_ZTScan_100spd_A_3_G1.wiff2 went from 97 s to over 420 s and started
                // timing out in the corpus sweep. Nothing is given up - every file the probe
                // fixes is a legacy .wiff (the 448 unreadable cycles in 20061108_CPTAC_1B468,
                // the 834 sentinel cycles in wine yeast sampleA_2, and five others) - and wiff2
                // is already better guarded, because its GetBpc returns empty so `intensities`
                // above is the TIC, a stricter emptiness test than the base peak.
                else if (_verifyNonEmptySpectra && !isWiff2
                         && (exp.GetSpectrum(i + 1, addZeros: false, centroid: false)?.XValues.Length ?? 0) == 0)
                    continue;

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
        => GetSpectrumImpl(index, getBinaryData, centroid: false);

    /// <inheritdoc/>
    // cpp SpectrumList_PeakPicker.cpp:139 — "ABI/Analyst peak picking" for the Sciex
    // (mode_ == 3) branch. Match exactly so the dataProcessing userParam string diffs out.
    public string VendorCentroidName => "ABI/Analyst peak picking";

    /// <inheritdoc/>
    public Spectrum GetCentroidSpectrum(int index, bool getBinaryData)
        => GetSpectrumImpl(index, getBinaryData, centroid: true);

    private Spectrum GetSpectrumImpl(int index, bool getBinaryData, bool centroid)
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
        // cpp SpectrumList_ABI.cpp:148 — the spectrum-type term is a pure function of the
        // EXPERIMENT type; the ms level (:146-147) is emitted separately and does not feed into
        // it. The two can legitimately disagree: an MRM3 wiff2 acquisition runs a full-scan
        // ("TOFMS") experiment whose cycles report ms level 3, and cpp emits
        // `ms level = 3` + `MS1 spectrum` for it.
        var spectrumType = TranslateAsSpectrumType(ie.ExperimentType);
        spec.Params.Set(spectrumType);
        if (exp.Polarity == WiffPolarity.Positive) spec.Params.Set(CVID.MS_positive_scan);
        else if (exp.Polarity == WiffPolarity.Negative) spec.Params.Set(CVID.MS_negative_scan);

        spec.ScanList.Set(CVID.MS_no_combination);
        var scan = new Scan { InstrumentConfiguration = _defaultIc };
        spec.ScanList.Scans.Add(scan);

        // Profile vs centroid + AddZeros padding for profile data are handled inside the
        // AbstractWiffSpectrum implementation (legacy: AddZeros via Clearcore2; wiff2: AddFramingZeros
        // via the SDK request). cpp SpectrumList_ABI.cpp:254/:261 passes
        // config_.ignoreZeroIntensityPoints into both getData and getDataSize, and both WIFF
        // SDK layers turn it into "don't add zeros": legacy WiffFile.cpp:836/:872 gate
        // AddZeros(spectrum, 1) on !ignoreZeroIntensityPoints, and WiffFile2.ipp:803/:812 pass
        // addZeros = !ignoreZeroIntensityPoints as the request's AddFramingZeros. So the
        // default (flag off) is addZeros=true, which is what makes the swath centroid output
        // match the cpp reference exactly (765 points/spectrum on the swath.api fixture, vs 255
        // with addZeros=false). Fetched here (before scan-window / start-time emission) because
        // the spectrum's StartTimeMinutes is the cpp-equivalent start time and is preferred
        // over the experiment-cycle RT when the SDK reports it.
        var ms = exp.GetSpectrum(ie.Cycle, addZeros: !_ignoreZeroIntensityPoints, centroid: centroid);

        // cpp SpectrumList_ABI.cpp:139-141: scan_start_time is `spectrum->getStartTime()` and is
        // emitted only when that is > 0. It is the SPECTRUM's start time, never the experiment's
        // per-cycle RT: legacy WIFF reports spectrumInfo->StartRT (WiffFile.cpp:810), which is one
        // cycle earlier than the experiment-cycle RT, and wiff2 reports the cycle scan time the
        // read request was issued with (WiffFile2.ipp:194) — both are AbstractWiffSpectrum
        // .StartTimeMinutes here. Falling back to exp.GetRetentionTime when StartRT is 0 would
        // synthesize a scan start time cpp does not emit (legacy WIFF reports StartRT == 0 for the
        // first cycle of some acquisitions, e.g. UV/_Sample_Run_004.wiff cycle 1).
        //
        // The fallback survives only for the case cpp cannot reach: a spectrum the SDK refused to
        // hand us at all (cpp would have thrown out of getSpectrum).
        double rtMin = ms?.StartTimeMinutes ?? 0;
        if (ms is null)
        {
            try { rtMin = exp.GetRetentionTime(ie.Cycle); } catch { /* not all experiments have RT */ }
        }
        if (rtMin > 0) scan.Set(CVID.MS_scan_start_time, rtMin, CVID.UO_minute);

        // 1-based experiment number; matches cpp's `msExperiment->getExperimentNumber()`.
        scan.Set(CVID.MS_preset_scan_configuration, ie.ExperimentIndex + 1);

        // cpp SpectrumList_ABI.cpp:152-154: scan window comes from
        // experiment->getAcquisitionMassRange, which returns (0, 0) for MRM/SIM and
        // (StartMass, EndMass) for full-scan. cpp pushes a ScanWindow unconditionally,
        // so MRM-as-spectra references include a `[0, 0]` scan window (the SDK throws
        // when we'd ask StartMass on an MRM; treat the throw as "(0, 0)").
        double scanLo = 0, scanHi = 0;
        try { scanLo = exp.StartMass; scanHi = exp.EndMass; }
        catch (ArgumentException) { /* MRM / SIM — keep (0, 0) */ }
        scan.ScanWindows.Add(new ScanWindow(scanLo, scanHi, CVID.MS_m_z));

        if (ms is not null)
        {
            // cpp WiffFile.cpp:738: pointsAreContinuous = !CentroidMode && expType != MRM && expType != SIM.
            // Mark MRM/SIM as centroid regardless of the SDK's CentroidMode flag (each
            // transition is a stick, not a continuum).
            bool isTransition = ms.ExperimentType is WiffExperimentType.MRM or WiffExperimentType.SIM;
            spec.Params.Set(centroid || ms.CentroidMode || isTransition
                ? CVID.MS_centroid_spectrum
                : CVID.MS_profile_spectrum);

            if (msLevel > 1 && ms.HasPrecursorInfo)
            {
                // cpp SpectrumList_ABI.cpp:164-229. `centerMz` starts at 0 and is filled in only
                // by getIsolationInfo (:172-176), which is itself gated on getHasIsolationInfo();
                // every isolation-window cvParam below then hangs off `centerMz > 0` (:183, :200).
                // So a cycle that has a precursor m/z but no isolation window emits
                // selectedIonList + activation and NO isolationWindow — which is exactly what an
                // MRM3 wiff2 acquisition looks like (its experiment is "TOFMS", and wiff2's
                // getHasIsolationInfo is `experimentType == Product`, WiffFile2.ipp:732).
                //
                // cpp also does `selectedMz = centerMz` when there is isolation info (:175), but
                // in both SDK paths that is the same number PrecursorMz already returns — legacy
                // WiffFile.cpp:774 sets `centerMz = getHasPrecursorInfo() ? selectedMz : ...`
                // (and this block only runs when there IS precursor info), and wiff2
                // WiffFile2.ipp:744/:790 sources both from IsolationWindow->IsolationWindowTarget.
                // So one property serves both roles here.
                double centerMz = ms.HasIsolationInfo ? ms.PrecursorMz : 0;

                if (spectrumType == CVID.MS_precursor_ion_spectrum)
                {
                    // cpp SpectrumList_ABI.cpp:178-194: a precursor-ion scan (Q3 parked on a
                    // fixed fragment while Q1 scans) is modelled as a PRODUCT — the isolation
                    // window describes the scanned-for product ion, and mzML's <product> element
                    // carries an isolationWindow and nothing else, so no selectedIon and no
                    // activation (and therefore no collision energy) are emitted for these.
                    // cpp pushes the product even when it ends up empty (:193).
                    var product = new Product();
                    if (centerMz > 0)
                    {
                        product.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, centerMz, CVID.MS_m_z);
                        if (ms.IsolationLowerOffset > 0 && ms.IsolationUpperOffset > 0)
                        {
                            product.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, ms.IsolationLowerOffset, CVID.MS_m_z);
                            product.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, ms.IsolationUpperOffset, CVID.MS_m_z);
                        }
                    }
                    spec.Products.Add(product);
                }
                else
                {
                    var precursor = new Precursor();
                    if (centerMz > 0)
                    {
                        precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, centerMz, CVID.MS_m_z);
                        // cpp SpectrumList_ABI.cpp:203-206 emits both offsets or neither, keyed on
                        // `lowerLimit > 0 && upperLimit > 0` (the absolute window bounds, not the
                        // offsets); the offsets themselves are those bounds' distance from centerMz.
                        if (ms.IsolationLowerOffset > 0 && ms.IsolationUpperOffset > 0)
                        {
                            precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, ms.IsolationLowerOffset, CVID.MS_m_z);
                            precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, ms.IsolationUpperOffset, CVID.MS_m_z);
                        }
                    }

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
                    // cpp SpectrumList_ABI.cpp:223-224 — `if (collisionEnergy > 0)`, where
                    // collisionEnergy was filled in only by getIsolationInfo (:172-176). Both halves
                    // matter: the value comes from the isolation info (see
                    // AbstractWiffSpectrum.CollisionEnergy), and a non-positive value is dropped
                    // rather than emitted as 0.
                    if (ms.CollisionEnergy > 0)
                        precursor.Activation.Set(CVID.MS_collision_energy, ms.CollisionEnergy, CVID.UO_electronvolt);
                    spec.Precursors.Add(precursor);
                }
            }

            // TIC: cpp WiffFile2.ipp:718 reads `spectrum->getSumY()` from a precomputed per-cycle
            // intensities array (`experiment->cycleIntensities()`). Summing the centroided
            // YValues here doesn't match — centroiding redistributes intensity across fewer
            // bins so the total ends up smaller than the raw cycle TIC. Use the SDK's per-cycle
            // value instead via AbstractWiffExperiment.GetCycleTic, which the wiff2 path
            // implements by caching GetExperimentTic.
            //
            // Base peak (MS_base_peak_intensity / MS_base_peak_m_z): legacy WIFF surfaces these
            // per-spectrum; wiff2 doesn't. WiffSpectrum.BasePeak returns null on wiff2 so the
            // CV params are emitted only when the SDK actually has them, matching cpp.
            //
            // cpp SpectrumList_ABI.cpp:240 is `if (!config_.acceptZeroLengthSpectra &&
            // spectrum->getBasePeakY() > 0)`: the base-peak lookup forces the SDK to build the
            // whole experiment's base-peak chromatogram, which is exactly the expensive work
            // acceptZeroLengthSpectra exists to avoid, so the flag suppresses both cvParams.
            // The `> 0` half is already inside GetBasePeak (it returns null for y <= 0).
            double[] xs = ms.XValues;
            double[] ys = ms.YValues;
            int len = Math.Min(xs.Length, ys.Length);

            spec.Params.Set(CVID.MS_total_ion_current, exp.GetCycleTic(ie.Cycle), CVID.MS_number_of_detector_counts);
            if (!_acceptZeroLengthSpectra && ms.BasePeak is var (bpMz, bpIntensity))
            {
                spec.Params.Set(CVID.MS_base_peak_m_z, bpMz, CVID.MS_m_z);
                spec.Params.Set(CVID.MS_base_peak_intensity, bpIntensity, CVID.MS_number_of_detector_counts);
            }

            if (getBinaryData)
            {
                spec.DefaultArrayLength = len;
                // Unconditional, as cpp SpectrumList_ABI.cpp:250 is: it calls
                // setMZIntensityArrays() with empty vectors and then fills them, so a spectrum
                // with zero points still carries <binaryDataArrayList count="2"> holding two
                // empty arrays. Guarding on len > 0 dropped the element entirely, which under
                // vendor peak picking left 57 wiff files short a child per zero-peak spectrum
                // versus cpp. Same defect as the one fixed in SpectrumList_Shimadzu.
                spec.SetMZIntensityArrays(SliceDouble(xs, len), SliceDouble(ys, len), CVID.MS_number_of_detector_counts);
            }
            else
            {
                spec.DefaultArrayLength = xs.Length;
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

    /// <summary>Maps a wiff experiment type to an mzML spectrum-type CVID. Port of cpp
    /// <c>Reader_ABI_Detail.cpp:196-209 (translateAsSpectrumType)</c>, which switches on the
    /// experiment type alone — the ms level is deliberately not consulted.</summary>
    public static CVID TranslateAsSpectrumType(WiffExperimentType expType) => expType switch
    {
        WiffExperimentType.MS => CVID.MS_MS1_spectrum,
        WiffExperimentType.Product => CVID.MS_MSn_spectrum,
        WiffExperimentType.Precursor => CVID.MS_precursor_ion_spectrum,
        WiffExperimentType.NeutralGainOrLoss => CVID.MS_constant_neutral_loss_spectrum,
        WiffExperimentType.SIM => CVID.MS_SIM_spectrum,
        WiffExperimentType.MRM => CVID.MS_SRM_spectrum,
        _ => CVID.MS_MSn_spectrum,
    };

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        if (_ownsWiff) _wiff.Dispose();
        base.DisposeCore();
    }
}
