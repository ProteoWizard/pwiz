using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.IdentData;
using Pwiz.Data.IdentData.PepXml;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

#pragma warning disable CA1707

namespace Pwiz.Analysis;

/// <summary>
/// Refines m/z values using a calibration shift derived from peptide identifications.
/// Port of <c>pwiz::analysis::SpectrumList_MZRefiner</c>.
/// </summary>
/// <remarks>
/// <para>Reads an mzIdentML / pepXML file of peptide-spectrum matches, filters them by score
/// (e.g. MS-GF+ SpecEValue ≤ 1e-10), and computes a calibration shift to apply to every m/z
/// value in matching MS levels (plus base peak / lowest / highest observed m/z CV params,
/// precursor selected ion m/z, isolation window target, and the Thermo Trailer Extra
/// "Monoisotopic M/Z" user param).</para>
/// <para>Two shift algorithms are supported per MS level: a single global median ppm shift,
/// and a "dependent" binned shift (m/z-binned or scan-time-binned). Cpp's selection logic
/// picks the dependent shift when its smoothed-MAD percent improvement over global exceeds
/// 3%, preferring the scan-time shift when it beats the m/z one. MS2 calibrates against
/// theoretical fragment-ion (b/y/c/z) m/z values rather than precursor m/z.</para>
/// </remarks>
public sealed class SpectrumList_MZRefiner : SpectrumListWrapper
{
    // cpp constants (SpectrumList_MZRefiner.cpp:48-51, 1410-1412, 1927-1928).
    private const int MinimumResultsForGlobalShift = 100;
    private const int MinimumResultsForDependentShift = 500;
    private const int MinimumResultsForMs2Shift = 100;
    private const double DependentShiftImprovementThreshold = 3.0;
    private const double IsotopeScreenAdj = 0.15;
    private const double IsotopeFilter = 0.20;
    private const double PpmErrorLimit = 50.0;
    private const double Ms2MzErrorThreshold = 0.2;
    private const double Ms2PpmErrorThreshold = 50.0;

    private readonly IntegerSet _msLevelsToRefine;
    private readonly IAdjustment? _ms1Adjust;
    private readonly IAdjustment? _ms2Adjust;
    private readonly bool _allHighRes;
    private readonly string _chosenShiftDescription = string.Empty;
    private readonly double _globalShiftPpm;
    private readonly string _shiftRangeDescription = string.Empty;
    private readonly string _identFilePath;
    private readonly string _filterScoreName;
    private readonly string _filterThreshold;

    /// <summary>Constructs a refiner that calibrates <paramref name="msd"/>'s spectrum list
    /// against the search results in <paramref name="identFilePath"/>.</summary>
    /// <param name="msd">MSData containing the spectra to refine.</param>
    /// <param name="identFilePath">Path to an mzIdentML or pepXML search-result file.</param>
    /// <param name="cvTerm">Score name driving the filter (e.g. <c>"specEValue"</c>); maps via
    /// <see cref="PepXmlTranslator"/> when paired with the search engine's CVID.</param>
    /// <param name="rangeSet">Range expression (e.g. <c>"-1e-10"</c> for ≤ 1e-10, <c>"1-5"</c>
    /// for [1, 5], <c>"5-"</c> for ≥ 5).</param>
    /// <param name="msLevelsToRefine">MS levels whose m/z arrays + metadata get adjusted.</param>
    /// <param name="step">Multiplier applied to the filter thresholds when the initial pass
    /// returns &lt; 500 PSMs (cpp's <c>adjustFilterByStep</c>); 0 disables stepping.</param>
    /// <param name="maxStep">Maximum number of step iterations (cpp's <c>maxSteps</c>); 0
    /// disables stepping.</param>
    /// <param name="assumeHighRes">When true, skip the instrument-config high-res check.</param>
    /// <param name="ilr">Optional progress callback registry (cpp's
    /// <c>IterationListenerRegistry</c>); receives updates while processing identifications
    /// and reading MS2 data.</param>
    public SpectrumList_MZRefiner(MSData msd, string identFilePath, string cvTerm,
        string rangeSet, IntegerSet msLevelsToRefine, double step = 0.0, int maxStep = 0,
        bool assumeHighRes = false, IterationListenerRegistry? ilr = null)
        : base(GetSpectrumList(msd))
    {
        ArgumentException.ThrowIfNullOrEmpty(identFilePath);
        ArgumentException.ThrowIfNullOrEmpty(cvTerm);
        ArgumentException.ThrowIfNullOrEmpty(rangeSet);
        ArgumentNullException.ThrowIfNull(msLevelsToRefine);

        _msLevelsToRefine = msLevelsToRefine;
        _identFilePath = identFilePath;

        if (!ContainsHighResData(msd, assumeHighRes, out _allHighRes))
            throw new InvalidOperationException(
                "[SpectrumList_MZRefiner] No high-resolution data in input file.");

        var ident = new IdentDataFile(identFilePath);
        var filter = new CVConditionalFilter(ident, cvTerm, rangeSet, step, maxStep);
        _filterScoreName = filter.ScoreName;
        _filterThreshold = filter.ThresholdDescription;

        var psms = CollectAcceptedPsmsWithRetry(ident, filter, ilr, out int badByScore, out int badByMassError);
        if (psms.Count < MinimumResultsForGlobalShift)
            throw new InvalidOperationException(
                $"[SpectrumList_MZRefiner] Less than {MinimumResultsForGlobalShift} ({psms.Count}) values in identfile that pass the threshold.");

        var ms2Data = new List<FragmentShiftDatum>();
        if (psms.Count >= MinimumResultsForDependentShift)
        {
            // Read MSData spectra to populate scan times (if SIRs didn't have them) and to
            // collect MS2 fragment-ion shift data.
            EnrichWithMsData(msd, psms, ms2Data, ilr);
        }

        if (msLevelsToRefine.Contains(1))
            _ms1Adjust = ChooseShift(psms, isMs1: true);

        if (msLevelsToRefine.Contains(2))
        {
            _ms2Adjust = ms2Data.Count >= MinimumResultsForMs2Shift
                ? ChooseShift(ms2Data, isMs1: false)
                : _ms1Adjust;
        }

        var primary = _ms1Adjust ?? _ms2Adjust;
        if (primary is not null)
        {
            _chosenShiftDescription = primary.PrettyAdjustment;
            _globalShiftPpm = primary.GlobalShiftPpm;
            _shiftRangeDescription = primary.ShiftRange;
        }

        WriteStatsRow(identFilePath, _filterScoreName, _filterThreshold,
            badByScore, badByMassError,
            psms.Count, _ms1Adjust, ms2Data.Count, _ms2Adjust, _ms1Adjust);
    }

    /// <summary>Description of the chosen MS1 shift (mirrors cpp's <c>Shift dependency</c>
    /// UserParam value).</summary>
    public string ChosenShiftDescription => _chosenShiftDescription;

    /// <summary>Global median ppm shift across all accepted MS1 PSMs.</summary>
    public double GlobalShiftPpm => _globalShiftPpm;

    private static ISpectrumList GetSpectrumList(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        return msd.Run.SpectrumList ?? throw new InvalidOperationException(
            "[SpectrumList_MZRefiner] MSData has no spectrum list.");
    }

    // ---- High-res guards ----

    private static bool ConfigurationIsHighRes(InstrumentConfiguration ic)
    {
        // cpp walks the components and picks the LAST analyzer encountered.
        Component? lastAnalyzer = null;
        foreach (var c in ic.ComponentList)
            if (c.Type == ComponentType.Analyzer) lastAnalyzer = c;
        if (lastAnalyzer is null) return false;
        return lastAnalyzer.HasCVParam(CVID.MS_orbitrap)
            || lastAnalyzer.HasCVParam(CVID.MS_time_of_flight)
            || lastAnalyzer.HasCVParam(CVID.MS_fourier_transform_ion_cyclotron_resonance)
            || lastAnalyzer.HasCVParam(CVID.MS_stored_waveform_inverse_fourier_transform);
    }

    private static bool ContainsHighResData(MSData msd, bool assumeHighRes, out bool allHighRes)
    {
        if (assumeHighRes) { allHighRes = true; return true; }
        bool hasHighRes = false;
        allHighRes = true;
        foreach (var ic in msd.InstrumentConfigurations)
        {
            if (ConfigurationIsHighRes(ic)) hasHighRes = true;
            else allHighRes = false;
        }
        return hasHighRes;
    }

    private static bool SpectrumIsHighResAndStartTime(Spectrum s, bool allHighRes, out double startTime)
    {
        startTime = 0;
        bool isHighRes = false;
        if (s.ScanList.Scans.Count == 0) return false;
        var scan = s.ScanList.Scans[0];
        if (allHighRes
            || (scan.InstrumentConfiguration is not null
                && ConfigurationIsHighRes(scan.InstrumentConfiguration)))
            isHighRes = true;
        var scanStart = scan.CvParam(CVID.MS_scan_start_time);
        if (!scanStart.IsEmpty) startTime = scanStart.TimeInSeconds();
        return isHighRes;
    }

    // ---- PSM collection ----

    private static List<PsmShiftDatum> CollectAcceptedPsmsWithRetry(IdentData ident,
        CVConditionalFilter filter, IterationListenerRegistry? ilr,
        out int badByScore, out int badByMassError)
    {
        // Replicates cpp's `while (adjustedFilter)` retry loop: collect PSMs; if fewer than
        // MinimumResultsForDependentShift pass, step the filter once and retry, up to maxStep.
        List<PsmShiftDatum> data;
        do { data = CollectAcceptedPsms(ident, filter, ilr, out badByScore, out badByMassError); }
        while (data.Count < MinimumResultsForDependentShift && filter.AdjustByStep());
        return data;
    }

    private static List<PsmShiftDatum> CollectAcceptedPsms(IdentData ident,
        CVConditionalFilter filter, IterationListenerRegistry? ilr,
        out int badByScore, out int badByMassError)
    {
        int totalResults = ident.DataCollection.AnalysisData.SpectrumIdentificationList
            .Sum(sil => sil.SpectrumIdentificationResult.Count);
        var data = new List<PsmShiftDatum>();
        var temp = new List<PsmShiftDatum>();
        badByScore = 0;
        badByMassError = 0;
        int specCounter = 0;
        foreach (var sil in ident.DataCollection.AnalysisData.SpectrumIdentificationList)
        foreach (var sir in sil.SpectrumIdentificationResult)
        {
            if (ilr is not null && ilr.Broadcast(new IterationUpdate(
                    specCounter++, totalResults,
                    "Processing and filtering spectrum identifications...")) == IterationStatus.Cancel)
                return data;
            temp.Clear();
            double sirScanTime = ReadSirScanTime(sir);
            foreach (var sii in sir.SpectrumIdentificationItem)
            {
                if (sii.CalculatedMassToCharge <= 0) continue;
                if (!filter.Passes(sii, out double score)) { badByScore++; continue; }

                var sd = new PsmShiftDatum
                {
                    ExperMz = sii.ExperimentalMassToCharge,
                    CalcMz = sii.CalculatedMassToCharge,
                    Charge = sii.ChargeState,
                    Rank = sii.Rank,
                    Score = score,
                    ScanTime = sirScanTime,
                    NativeId = sir.SpectrumID,
                    PeptidePtr = sii.PeptidePtr,
                };
                sd.MassError = sd.ExperMz - sd.CalcMz;
                sd.PpmError = sd.MassError / sd.CalcMz * 1e6;

                if (IsotopeScreenAdj <= 0.20 && System.Math.Abs(sd.MassError) >= IsotopeScreenAdj)
                    CleanIsotopes(sd);
                if (System.Math.Abs(sd.MassError) >= IsotopeFilter) { badByMassError++; continue; }
                if (System.Math.Abs(sd.PpmError) > PpmErrorLimit) { badByMassError++; continue; }

                if (sir.SpectrumIdentificationItem.Count == 1) data.Add(sd);
                else temp.Add(sd);
            }
            if (temp.Count == 0) continue;
            if (temp.Count == 1) { data.Add(temp[0]); continue; }
            data.Add(temp.OrderByDescending(s => filter.PreferenceKey(s)).First());
        }
        return data;
    }

    private static double ReadSirScanTime(SpectrumIdentificationResult sir)
    {
        var scanTime = sir.CvParam(CVID.MS_scan_start_time);
        if (scanTime.IsEmpty) scanTime = sir.CvParam(CVID.MS_retention_time);
        if (scanTime.IsEmpty) scanTime = sir.CvParam(CVID.MS_retention_time_s__OBSOLETE);
        return scanTime.IsEmpty ? 0.0 : scanTime.TimeInSeconds();
    }

    private static void CleanIsotopes(PsmShiftDatum sd)
    {
        if (sd.Charge == 0) return;
        const double windowAdj = 0.05;
        double chargeWithSign = sd.MassError < 0 ? -sd.Charge : sd.Charge;
        for (int i = 1; i <= 5; i++)
        {
            double adjustment = i / chargeWithSign;
            if (adjustment - windowAdj <= sd.MassError && sd.MassError <= adjustment + windowAdj)
            {
                sd.ExperMz -= adjustment;
                sd.MassError = sd.ExperMz - sd.CalcMz;
                sd.PpmError = sd.MassError / sd.CalcMz * 1e6;
                return;
            }
        }
    }

    // ---- MS2 fragment-ion collection ----

    /// <summary>Reads each PSM's source spectrum from <paramref name="msd"/>: populates
    /// missing scan times and, for high-res MS2 spectra, derives per-fragment-ion ppm errors
    /// into <paramref name="ms2Data"/>. Port of cpp's <c>getMSDataData</c>.</summary>
    /// <remarks>Cpp sorts PSMs by extracted scan number and walks the spectrum list once,
    /// matching native ids as it goes — relies on extracting an integer scan number from the
    /// native id format. The C# port avoids the native-id-format dance with a direct
    /// dictionary lookup: <c>native id → PSMs</c>, then walk the spectrum list and look up
    /// each spectrum's id.</remarks>
    private void EnrichWithMsData(MSData msd, List<PsmShiftDatum> psms,
        List<FragmentShiftDatum> ms2Data, IterationListenerRegistry? ilr)
    {
        var sl = msd.Run.SpectrumList!;
        var byNativeId = new Dictionary<string, List<PsmShiftDatum>>(StringComparer.Ordinal);
        foreach (var p in psms)
        {
            if (!byNativeId.TryGetValue(p.NativeId, out var bucket))
                byNativeId[p.NativeId] = bucket = new List<PsmShiftDatum>();
            bucket.Add(p);
        }

        int matched = 0;
        for (int i = 0; i < sl.Count && matched < psms.Count; i++)
        {
            if (ilr is not null && ilr.Broadcast(new IterationUpdate(
                    i, sl.Count, "Reading scan start times and/or data arrays from data file...")) == IterationStatus.Cancel)
                return;
            var s = sl.GetSpectrum(i, false);
            if (s is null) continue;
            if (!byNativeId.TryGetValue(s.Id, out var bucket)) continue;

            bool isHighRes = SpectrumIsHighResAndStartTime(s, _allHighRes, out double scanTime);
            foreach (var p in s.Precursors)
            {
                GetPrecursorHighResAndStartTime(sl, p, ref scanTime);
                break; // cpp: only worried about the first scan start time.
            }
            int msLevel = s.Params.HasCVParam(CVID.MS_MS1_spectrum)
                ? 1
                : s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);

            Spectrum? withMz = null;
            foreach (var p in bucket)
            {
                p.ScanTime = scanTime;
                p.MsLevel = msLevel;
                if (isHighRes && msLevel > 1 && _msLevelsToRefine.Contains(msLevel))
                {
                    withMz ??= s.GetMZArray() is null ? sl.GetSpectrum(i, true) : s;
                    AddFragmentationIonPpmErrors(p, withMz, scanTime, ms2Data);
                }
                matched++;
            }
        }
    }

    /// <summary>For one PSM + its MS2 spectrum, computes theoretical b/y/c/z fragment-ion m/z
    /// values, finds the nearest experimental peak within ±0.2 Da and ±50 ppm, and pushes the
    /// matched ppm errors into <paramref name="ms2Data"/>. Port of cpp's
    /// <c>fragmentationIonPpmErrors</c>.</summary>
    private void AddFragmentationIonPpmErrors(PsmShiftDatum psm, Spectrum s, double scanStartTime,
        List<FragmentShiftDatum> ms2Data)
    {
        if (psm.PeptidePtr is null) return;
        var mzArr = s.GetMZArray();
        if (mzArr is null || mzArr.Data.Count == 0) return;

        // Determine which ion series the dissociation method produces.
        (bool hasB, bool hasY, bool hasC, bool hasZ) = (false, false, false, false);
        foreach (var p in s.Precursors)
            foreach (var cvp in p.Activation.CvParamChildren(CVID.MS_dissociation_method))
                ClassifyDissociation(cvp.Cvid, ref hasB, ref hasY, ref hasC, ref hasZ);
        // No identified dissociation method → no fragments to match.
        if (!hasB && !hasY && !hasC && !hasZ) return;

        // Build the chemistry-side Peptide with modifications attached programmatically.
        var pep = BuildProteomePeptide(psm.PeptidePtr);
        if (pep.Sequence.Length == 0) return;
        var frag = pep.Fragmentation(monoisotopic: true, modified: true);
        int len = pep.Sequence.Length;

        var mzData = mzArr.Data;
        // cpp builds an integer-keyed lookup table to bound the search around each ion's
        // value. We use a binary search instead — m/z arrays are already sorted ascending.
        Span<int> chargeRange = stackalloc int[] { 1, 2, 3 };
        for (int i = 0; i <= len; i++)
        {
            foreach (var charge in chargeRange)
            {
                if (hasB) TryMatchAndPush(frag.B(i, charge), mzData, ms2Data, scanStartTime);
                if (hasC && i != len) TryMatchAndPush(frag.C(i, charge), mzData, ms2Data, scanStartTime);
                if (hasY) TryMatchAndPush(frag.Y(i, charge), mzData, ms2Data, scanStartTime);
                if (hasZ && i != len) TryMatchAndPush(frag.Z(i, charge), mzData, ms2Data, scanStartTime);
            }
        }
    }

    private static void ClassifyDissociation(CVID cvid,
        ref bool hasB, ref bool hasY, ref bool hasC, ref bool hasZ)
    {
        switch (cvid)
        {
            case CVID.MS_collision_induced_dissociation:
            case CVID.MS_trap_type_collision_induced_dissociation:
            case CVID.MS_in_source_collision_induced_dissociation:
            case CVID.MS_beam_type_collision_induced_dissociation:
            case CVID.MS_higher_energy_beam_type_collision_induced_dissociation:
            case CVID.MS_post_source_decay:
                hasB = true; hasY = true; break;
            case CVID.MS_electron_capture_dissociation:
            case CVID.MS_electron_transfer_dissociation:
                hasC = true; hasZ = true; break;
        }
    }

    private static Pwiz.Util.Proteome.Peptide BuildProteomePeptide(Peptide idPep)
    {
        var pep = new Pwiz.Util.Proteome.Peptide(idPep.PeptideSequence, Pwiz.Util.Proteome.ModificationParsing.Off);
        foreach (var m in idPep.Modifications)
        {
            int offset = m.Location switch
            {
                0 => Pwiz.Util.Proteome.ModificationMap.NTerminus,
                int loc when loc == pep.Sequence.Length + 1 => Pwiz.Util.Proteome.ModificationMap.CTerminus,
                _ => m.Location - 1,
            };
            pep.Modifications[offset].Add(new Pwiz.Util.Proteome.Modification(
                m.MonoisotopicMassDelta, m.AvgMassDelta));
        }
        return pep;
    }

    private static void TryMatchAndPush(double ion, List<double> mzData,
        List<FragmentShiftDatum> ms2Data, double scanStartTime)
    {
        if (ion <= 0) return;
        if (ion < mzData[0] - Ms2MzErrorThreshold || ion > mzData[^1] + Ms2MzErrorThreshold) return;

        // Binary-search-bounded linear scan: closest peak within the ±0.2 Da window.
        int low = LowerBound(mzData, ion - Ms2MzErrorThreshold);
        int high = LowerBound(mzData, ion + Ms2MzErrorThreshold);
        if (low >= mzData.Count) return;

        double bestErr = double.MaxValue;
        double experMass = 0;
        for (int i = low; i < high; i++)
        {
            double err = ion - mzData[i];
            if (System.Math.Abs(err) < System.Math.Abs(bestErr))
            {
                bestErr = err;
                experMass = mzData[i];
            }
        }
        if (System.Math.Abs(bestErr) > Ms2MzErrorThreshold) return;
        double ppmErr = (experMass - ion) / ion * 1e6;
        if (System.Math.Abs(ppmErr) > Ms2PpmErrorThreshold) return;
        ms2Data.Add(new FragmentShiftDatum
        {
            CalcMz = ion,
            ExperMz = experMass,
            PpmError = ppmErr,
            ScanTime = scanStartTime,
        });
    }

    private static int LowerBound(List<double> values, double target)
    {
        int lo = 0, hi = values.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (values[mid] < target) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    // ---- Precursor scan-time lookup (port of cpp's getPrecursorHighResAndStartTime) ----

    private bool GetPrecursorHighResAndStartTime(ISpectrumList sl, Precursor p, ref double scanStartTime)
    {
        if (p.SourceFile is not null) return false;
        int idx = sl.Find(p.SpectrumId);
        if (idx >= sl.Count) return _allHighRes;
        var precursorSpec = sl.GetSpectrum(idx, false);
        return SpectrumIsHighResAndStartTime(precursorSpec, _allHighRes, out scanStartTime);
    }

    // ---- Shift selection per MS level ----

    private static IAdjustment ChooseShift(List<PsmShiftDatum> data, bool isMs1)
    {
        var ppmErrors = data.Select(p => p.PpmError).ToList();
        var global = new SimpleGlobalAdjustment(ppmErrors);
        if (isMs1 && !global.HasSignificantPeak())
            throw new InvalidOperationException(
                "[mzRefiner::shiftCalculator] No significant peak (ppm error histogram) found.");

        if (data.Count < MinimumResultsForDependentShift) return global;

        var scanTimeShift = ScanTimeBinnedShift.Build(data, global);
        var mzShift = MzBinnedShift.Build(data, global);
        return SelectBest(global, scanTimeShift, mzShift);
    }

    private static IAdjustment ChooseShift(List<FragmentShiftDatum> data, bool isMs1)
    {
        var ppmErrors = data.Select(p => p.PpmError).ToList();
        var global = new SimpleGlobalAdjustment(ppmErrors);
        if (isMs1 && !global.HasSignificantPeak())
            throw new InvalidOperationException(
                "[mzRefiner::shiftCalculator] No significant peak (ppm error histogram) found.");

        if (data.Count < MinimumResultsForDependentShift) return global;

        var scanTimeShift = ScanTimeBinnedShift.Build(data, global);
        var mzShift = MzBinnedShift.Build(data, global);
        return SelectBest(global, scanTimeShift, mzShift);
    }

    private static IAdjustment SelectBest(SimpleGlobalAdjustment global,
        BinnedAdjustment scanTime, BinnedAdjustment mz)
    {
        // cpp's preference: scan time if pctImp > 3% AND > m/z's pctImp; else m/z if > 3%;
        // else global.
        if (scanTime.PctImp > DependentShiftImprovementThreshold && scanTime.PctImp > mz.PctImp)
            return scanTime;
        if (mz.PctImp > DependentShiftImprovementThreshold)
            return mz;
        return global;
    }

    // ---- DataProcessing decoration ----

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing
    {
        get
        {
            var dp = new Pwiz.Data.MsData.Processing.DataProcessing(Inner.DataProcessing?.Id ?? "pwiz_Reader_conversion");
            if (Inner.DataProcessing is not null)
                foreach (var pm in Inner.DataProcessing.ProcessingMethods)
                    dp.ProcessingMethods.Add(pm);
            var method = new ProcessingMethod
            {
                Order = dp.ProcessingMethods.Count,
                Software = dp.ProcessingMethods.FirstOrDefault()?.Software,
            };
            method.CVParams.Add(new CVParam(CVID.MS_m_z_calibration));
            method.UserParams.Add(new UserParam("Identification File", _identFilePath));
            method.UserParams.Add(new UserParam("Filter score name", _filterScoreName));
            method.UserParams.Add(new UserParam("Filter score threshold", _filterThreshold));
            method.UserParams.Add(new UserParam("Shift dependency", _chosenShiftDescription));
            method.UserParams.Add(new UserParam("Shift range", _shiftRangeDescription));
            method.UserParams.Add(new UserParam("Global Median Mass Measurement Error (PPM)",
                _globalShiftPpm.ToString("R", CultureInfo.InvariantCulture)));
            dp.ProcessingMethods.Add(method);
            return dp;
        }
    }

    // ---- Spectrum override ----

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(index, getBinaryData);
        if (_ms1Adjust is null && _ms2Adjust is null) return spec;

        bool isHighRes = SpectrumIsHighResAndStartTime(spec, _allHighRes, out double scanTime);
        int msLevel = spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 1);

        if (isHighRes && _msLevelsToRefine.Contains(msLevel))
        {
            var adjust = msLevel > 1 ? _ms2Adjust : _ms1Adjust;
            if (adjust is not null)
            {
                ShiftCvIfPresent(spec.Params, CVID.MS_base_peak_m_z, adjust, scanTime);
                ShiftCvIfPresent(spec.Params, CVID.MS_lowest_observed_m_z, adjust, scanTime);
                ShiftCvIfPresent(spec.Params, CVID.MS_highest_observed_m_z, adjust, scanTime);
                var mzArr = spec.GetMZArray();
                if (mzArr is not null)
                    for (int i = 0; i < mzArr.Data.Count; i++)
                        mzArr.Data[i] = adjust.Shift(scanTime, mzArr.Data[i]);
            }
        }

        if (msLevel >= 2 && _msLevelsToRefine.Contains(msLevel - 1) && _ms1Adjust is not null)
        {
            double pScanTime = scanTime;
            foreach (var p in spec.Precursors)
            {
                if (!GetPrecursorHighResAndStartTime(Inner, p, ref pScanTime)) continue;
                ShiftCvIfPresent(p.IsolationWindow, CVID.MS_isolation_window_target_m_z, _ms1Adjust, pScanTime);
                foreach (var ion in p.SelectedIons)
                    ShiftCvIfPresent(ion, CVID.MS_selected_ion_m_z, _ms1Adjust, pScanTime);
            }
            foreach (var scan in spec.ScanList.Scans)
                foreach (var u in scan.UserParams)
                    if (u.Name == "[Thermo Trailer Extra]Monoisotopic M/Z:"
                        && double.TryParse(u.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        u.Value = _ms1Adjust.Shift(pScanTime, v).ToString("R", CultureInfo.InvariantCulture);
        }

        spec.DataProcessing = DataProcessing;
        return spec;
    }

    private static void ShiftCvIfPresent(ParamContainer pc, CVID cvid, IAdjustment adjust, double scanTime)
    {
        foreach (var p in pc.CVParams)
        {
            if (p.Cvid != cvid) continue;
            if (!double.TryParse(p.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return;
            p.Value = adjust.Shift(scanTime, v).ToString("R", CultureInfo.InvariantCulture);
            return;
        }
    }

    // ---- mzRefinement.tsv stats output ----

    /// <summary>Writes one row of per-file refinement stats to <c>&lt;identFile&gt;.mzRefinement.tsv</c>
    /// (appending if the file already exists; writing the header otherwise). Port of cpp's
    /// <c>shiftCalculator</c> output side-effect. Falls back to the cwd / temp dir if the
    /// preferred path isn't writable, matching cpp's <c>openFilestreamIfWritable</c>.</summary>
    private static void WriteStatsRow(string identFilePath, string scoreName, string thresholdDesc,
        int badByScore, int badByMassError,
        int ms1Count, IAdjustment? ms1Adjust, int ms2Count, IAdjustment? ms2Adjust, IAdjustment? ms1AdjustForCompare)
    {
        string? statPath = ResolveStatPath(identFilePath);
        if (statPath is null) return;

        bool fileExists = File.Exists(statPath);
        using var w = new StreamWriter(new FileStream(statPath, FileMode.Append, FileAccess.Write, FileShare.Read));
        if (!fileExists)
        {
            w.WriteLine("ThresholdScore\tThresholdValue\tExcluded (score)\tExcluded (mass error)"
                + "\tMS1 Included\tMS1 Shift method\tMS1 Final stDev\tMS1 Tolerance for 99%\tMS1 Final MAD\tMS1 MAD Tolerance for 99%"
                + "\tMS2 Included\tMS2 Shift method\tMS2 Final stDev\tMS2 Tolerance for 99%\tMS2 Final MAD\tMS2 MAD Tolerance for 99%");
        }

        var row = new System.Text.StringBuilder();
        row.Append(scoreName).Append('\t')
           .Append(thresholdDesc).Append('\t')
           .Append(badByScore).Append('\t')
           .Append(badByMassError).Append('\t');
        AppendShiftStats(row, ms1Count, ms1Adjust, ms1AdjustForCompare, isMs2: false);
        row.Append('\t');
        AppendShiftStats(row, ms2Count, ms2Adjust, ms1AdjustForCompare, isMs2: true);
        w.WriteLine(row.ToString());
    }

    private static void AppendShiftStats(System.Text.StringBuilder row,
        int count, IAdjustment? adjust, IAdjustment? ms1Adjust, bool isMs2)
    {
        row.Append(count).Append('\t');
        if (adjust is null) { row.Append("\t\t\t\t"); return; }
        string method = adjust switch
        {
            SimpleGlobalAdjustment => "global",
            ScanTimeBinnedShift => "scan time",
            MzBinnedShift => "m/z",
            _ => "unknown",
        };
        if (isMs2 && ReferenceEquals(adjust, ms1Adjust)) method = "same as MS1";
        row.Append(method).Append('\t');
        var inv = CultureInfo.InvariantCulture;
        row.Append(adjust.Stdev.ToString("R", inv)).Append('\t');
        row.Append((adjust.Stdev * 3).ToString("R", inv)).Append('\t');
        row.Append(adjust.Mad.ToString("R", inv)).Append('\t');
        row.Append((adjust.Mad * 3 * 1.4826).ToString("R", inv)); // cpp: MAD → stdev for normal distribution
    }

    /// <summary>Resolves a writable path for the TSV. Cpp's order: alongside the ident file →
    /// just the filename (cwd) → temp dir. Returns null if none are writable.</summary>
    private static string? ResolveStatPath(string identFilePath)
    {
        string baseName = Path.GetFileNameWithoutExtension(identFilePath) + ".mzRefinement.tsv";
        string dir = Path.GetDirectoryName(identFilePath) ?? string.Empty;
        foreach (var candidate in new[]
        {
            string.IsNullOrEmpty(dir) ? baseName : Path.Combine(dir, baseName),
            baseName,
            Path.Combine(Path.GetTempPath(), baseName),
        })
        {
            try
            {
                using var test = new FileStream(candidate, FileMode.Append, FileAccess.Write, FileShare.Read);
                return candidate;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }
}

// ---- Per-PSM and per-fragment shift data ----

/// <summary>One MS1 precursor PSM after filtering — calc/exper m/z + ppm + scan time + back-
/// reference to the originating peptide id (for MS2 fragment-ion derivation).</summary>
internal sealed class PsmShiftDatum
{
    public double ExperMz, CalcMz, MassError, PpmError, ScanTime, Score;
    public int Charge, Rank, MsLevel;
    public string NativeId = string.Empty;
    public Pwiz.Data.IdentData.Peptide? PeptidePtr;
}

/// <summary>One matched MS2 fragment-ion peak: theoretical vs observed m/z + ppm + the scan
/// time of the parent MS2 (used by scan-time-binned shift for MS2).</summary>
internal sealed class FragmentShiftDatum
{
    public double ExperMz, CalcMz, PpmError, ScanTime;
}

// ---- Adjustment hierarchy ----

internal interface IAdjustment
{
    string PrettyAdjustment { get; }
    string ShiftRange { get; }
    double GlobalShiftPpm { get; }
    double Stdev { get; }
    double Mad { get; }
    double PctImp { get; }
    double Shift(double scanTime, double mass);
}

/// <summary>Median-ppm global shift across all data points. Computes (median, mean, mode)
/// stats and validates that the mode peak is well-separated from the histogram baseline.</summary>
internal sealed class SimpleGlobalAdjustment : IAdjustment
{
    private const double FreqHistBinSize = 0.5;
    private const int FreqHistBinCount = (int)(100.0 / 0.5) + 1; // -50..50 ppm in 0.5-ppm bins

    private readonly double _shiftErrorPpm;
    private readonly int[] _freqHist = new int[FreqHistBinCount];
    private readonly double _modeError;

    public string PrettyAdjustment { get; }
    public string ShiftRange => _shiftErrorPpm.ToString("R", CultureInfo.InvariantCulture);
    public double GlobalShiftPpm => _shiftErrorPpm;
    public double Stdev { get; }
    public double Mad { get; }
    public double PctImp => 0.0; // global is the baseline

    /// <summary>Average ppm error (un-shifted) — for stats output only.</summary>
    public double AvgError { get; }
    /// <summary>Median ppm error — same as <see cref="GlobalShiftPpm"/>.</summary>
    public double MedianError => _shiftErrorPpm;
    /// <summary>Mode of the ppm-error histogram — for stats output only.</summary>
    public double ModeError => _modeError;
    /// <summary>Population stdev around the avg — for stats output only.</summary>
    public double AvgStDev { get; }
    /// <summary>Population stdev around the mode — for stats output only.</summary>
    public double ModeStDev { get; }
    /// <summary>Population stdev around the median (= <see cref="Stdev"/>).</summary>
    public double MedianStDev => Stdev;

    public SimpleGlobalAdjustment(List<double> ppmErrors)
    {
        ppmErrors.Sort();
        double median = ppmErrors.Count == 0 ? 0 :
            (ppmErrors.Count % 2 == 1
                ? ppmErrors[ppmErrors.Count / 2]
                : 0.5 * (ppmErrors[ppmErrors.Count / 2 - 1] + ppmErrors[ppmErrors.Count / 2]));

        double sumPpm = 0;
        foreach (var e in ppmErrors)
        {
            if (-50.0 <= e && e <= 50.0)
            {
                int bin = (int)((e + 50.0) * (1.0 / FreqHistBinSize) + 0.5);
                _freqHist[bin]++;
                sumPpm += e;
            }
        }
        AvgError = ppmErrors.Count == 0 ? 0 : sumPpm / ppmErrors.Count;

        int highBin = 0;
        for (int i = 0; i < FreqHistBinCount; i++)
            if (_freqHist[i] > _freqHist[highBin]) highBin = i;
        _modeError = highBin * FreqHistBinSize - 50.0;

        double avgVariance = 0;
        double modeVariance = 0;
        double medianVariance = 0;
        var mads = new List<double>(ppmErrors.Count);
        foreach (var e in ppmErrors)
        {
            avgVariance += (e - AvgError) * (e - AvgError);
            modeVariance += (e - _modeError) * (e - _modeError);
            medianVariance += (e - median) * (e - median);
            mads.Add(System.Math.Abs(e - median));
        }
        int n = ppmErrors.Count;
        AvgStDev = n == 0 ? 0 : System.Math.Sqrt(avgVariance / n);
        ModeStDev = n == 0 ? 0 : System.Math.Sqrt(modeVariance / n);
        Stdev = n == 0 ? 0 : System.Math.Sqrt(medianVariance / n);
        Mad = Median(mads);

        _shiftErrorPpm = median;
        PrettyAdjustment = "Global Shift: " + _shiftErrorPpm.ToString("R", CultureInfo.InvariantCulture) + " ppm";
    }

    /// <summary>Cpp's <c>checkForPeak</c>: rejects refinement when the histogram mode isn't
    /// near the median, or when the mode bin's count isn't at least 5× the average count of
    /// bins outside the ±10 ppm window around the median.</summary>
    public bool HasSignificantPeak()
    {
        int medianBin = (int)((_shiftErrorPpm + 50.0) * (1.0 / FreqHistBinSize) + 0.5);
        int tenPpmBins = (int)(10.0 / FreqHistBinSize);
        int medianLessTen = System.Math.Max(0, medianBin - tenPpmBins);
        int medianPlusTen = System.Math.Min(FreqHistBinCount - 1, medianBin + tenPpmBins);
        int maxBin = (int)((_modeError + 50.0) * (1.0 / FreqHistBinSize) + 0.5);
        if (maxBin < medianLessTen || medianPlusTen < maxBin) return false;
        long sum = 0; int count = 0;
        for (int i = 0; i < FreqHistBinCount; i++)
        {
            if (i >= medianLessTen && i <= medianPlusTen) continue;
            if (_freqHist[i] > 0) { sum += _freqHist[i]; count++; }
        }
        if (count == 0) return true;
        double avg = (double)sum / count;
        return _freqHist[maxBin] >= avg * 5;
    }

    public double Shift(double scanTime, double mass) => mass * (1.0 - _shiftErrorPpm * 1e-6);

    internal static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        values.Sort();
        return values.Count % 2 == 1
            ? values[values.Count / 2]
            : 0.5 * (values[values.Count / 2 - 1] + values[values.Count / 2]);
    }
}

/// <summary>Shared bookkeeping for the two binned shifts (m/z-binned and scan-time-binned).
/// Subclasses just pick the dependency variable and the bin size.</summary>
internal abstract class BinnedAdjustment : IAdjustment
{
    protected readonly double _binSize;
    protected readonly double _globalShift;
    protected readonly double[] _shifts;
    protected readonly int _lowestValidBin;
    protected readonly int _highestValidBin;

    public abstract string PrettyAdjustment { get; }
    public string ShiftRange { get; }
    public double GlobalShiftPpm => _globalShift;
    public double Stdev { get; }
    public double Mad { get; }
    public double PctImp { get; }

    protected BinnedAdjustment(double binSize, double globalShift, double globalMad,
        double[] shifts, int lowestValidBin, int highestValidBin,
        double smoothedStdev, double smoothedMad, double pctImpMad, string shiftRange)
    {
        _binSize = binSize;
        _globalShift = globalShift;
        _shifts = shifts;
        _lowestValidBin = lowestValidBin;
        _highestValidBin = highestValidBin;
        Stdev = smoothedStdev;
        Mad = smoothedMad;
        PctImp = pctImpMad;
        ShiftRange = shiftRange;
    }

    /// <summary>Cpp's <c>binShift</c>: nearest-bin shift with linear interpolation between
    /// adjacent bin centers; clamps to the edge bin's shift outside the valid range.</summary>
    protected double BinShift(double dependency, double mass)
    {
        int useBin = (int)(dependency / _binSize);
        if (useBin < _lowestValidBin) return mass * (1.0 - _shifts[_lowestValidBin] * 1e-6);
        if (useBin > _highestValidBin) return mass * (1.0 - _shifts[_highestValidBin] * 1e-6);

        double lowEdge = _lowestValidBin * _binSize + _binSize / 2.0;
        double highEdge = _highestValidBin * _binSize + _binSize / 2.0;
        if (dependency <= lowEdge || dependency >= highEdge)
            return mass * (1.0 - _shifts[useBin] * 1e-6);

        double binCenter = useBin * _binSize + _binSize / 2.0;
        int lowBin, highBin;
        if (dependency < binCenter) { lowBin = useBin - 1; highBin = useBin; }
        else if (dependency > binCenter) { lowBin = useBin; highBin = useBin + 1; }
        else return mass * (1.0 - _shifts[useBin] * 1e-6);

        double lowMid = lowBin * _binSize + _binSize / 2.0;
        double highMid = highBin * _binSize + _binSize / 2.0;
        double pct = (dependency - lowMid) / (highMid - lowMid);
        double newShift = _shifts[lowBin] + pct * (_shifts[highBin] - _shifts[lowBin]);
        return mass * (1.0 - newShift * 1e-6);
    }

    public abstract double Shift(double scanTime, double mass);

    /// <summary>Builds the bin shift table from raw per-bin samples + per-bin counts. Returns
    /// the constructor inputs needed by <see cref="BinnedAdjustment"/>.</summary>
    protected static (double[] shifts, double smoothedStdev, double smoothedMad, double pctImpMad, string range)
        ProcessBins(List<double>[] sortBins, int[] counts, double binSize,
            int lowestValidBin, int highestValidBin, double globalShift, double globalMad)
    {
        int n = sortBins.Length;
        var rough = new double[n];
        for (int i = 0; i < n; i++) rough[i] = sortBins[i].Count > 0 ? SimpleGlobalAdjustment.Median(sortBins[i]) : globalShift;

        var smoothed = new double[n];
        for (int i = 0; i < n; i++) smoothed[i] = globalShift;
        for (int i = lowestValidBin; i <= highestValidBin; i++)
        {
            int count = counts[i];
            double sum = rough[i] * counts[i];
            for (int j = 1; (j < 2 || count < 100) && j < n; j++)
            {
                if (i + j <= highestValidBin) { count += counts[i + j]; sum += rough[i + j] * counts[i + j]; }
                if (i - j > 0 && i - j >= lowestValidBin) { count += counts[i - j]; sum += rough[i - j] * counts[i - j]; }
            }
            smoothed[i] = count > 0 ? sum / count : globalShift;
        }

        // Per-bin smoothed stdev (cpp's getStats): population stdev within each bin, averaged
        // across valid bins. MAD: median of per-bin median absolute deviations.
        int validBins = 0;
        double binStDevSum = 0;
        var binMads = new List<double>();
        var madWorker = new List<double>();
        for (int i = lowestValidBin; i <= highestValidBin; i++)
        {
            if (sortBins[i].Count == 0) continue;
            validBins++;
            double varSum = 0;
            madWorker.Clear();
            foreach (var v in sortBins[i])
            {
                double d = v - smoothed[i];
                varSum += d * d;
                madWorker.Add(System.Math.Abs(d));
            }
            binStDevSum += System.Math.Sqrt(varSum / sortBins[i].Count);
            binMads.Add(SimpleGlobalAdjustment.Median(madWorker));
        }
        double smoothedStDev = validBins == 0 ? 0 : binStDevSum / validBins;
        double smoothedMad = SimpleGlobalAdjustment.Median(binMads);
        double pctImpMad = globalMad == 0 ? 0
            : 100.0 * (System.Math.Abs(globalMad) - System.Math.Abs(smoothedMad)) / System.Math.Abs(globalMad);

        double min = smoothed[lowestValidBin], max = smoothed[lowestValidBin];
        for (int i = lowestValidBin; i <= highestValidBin; i++)
        {
            if (smoothed[i] < min) min = smoothed[i];
            if (smoothed[i] > max) max = smoothed[i];
        }
        string range = min.ToString("R", CultureInfo.InvariantCulture) + " to " + max.ToString("R", CultureInfo.InvariantCulture);

        return (smoothed, smoothedStDev, smoothedMad, pctImpMad, range);
    }
}

/// <summary>m/z-binned shift (25-Da bins). Bins PSMs by experimental m/z, smooths bin shifts
/// with a weighted neighbor window expanded until count ≥ 100, applies via linear-interp
/// between adjacent bin centers. Port of cpp's <c>AdjustByMassToCharge</c>.</summary>
internal sealed class MzBinnedShift : BinnedAdjustment
{
    private const double MzBinSize = 25.0;

    private MzBinnedShift(double globalShift, double globalMad, double[] shifts,
        int lowestValidBin, int highestValidBin,
        double smoothedStdev, double smoothedMad, double pctImpMad, string shiftRange)
        : base(MzBinSize, globalShift, globalMad, shifts, lowestValidBin, highestValidBin,
               smoothedStdev, smoothedMad, pctImpMad, shiftRange) { }

    public override string PrettyAdjustment => "Using mass to charge dependency";

    public override double Shift(double scanTime, double mass) => BinShift(mass, mass);

    public static MzBinnedShift Build<T>(IList<T> data, SimpleGlobalAdjustment global) where T : class
    {
        double maxExper = data.Max(d => GetExperMz(d));
        double minExper = data.Min(d => GetExperMz(d));
        int lowest = (int)(minExper / MzBinSize);
        int highest = (int)(maxExper / MzBinSize);
        int n = (int)((maxExper + MzBinSize * 4) / MzBinSize);

        var sortBins = new List<double>[n];
        for (int i = 0; i < n; i++) sortBins[i] = new List<double>();
        var counts = new int[n];
        foreach (var d in data)
        {
            int b = (int)(GetExperMz(d) / MzBinSize);
            sortBins[b].Add(GetPpmError(d));
            counts[b]++;
        }
        var (shifts, stdev, mad, pctImp, range) = ProcessBins(sortBins, counts, MzBinSize, lowest, highest, global.GlobalShiftPpm, global.Mad);
        return new MzBinnedShift(global.GlobalShiftPpm, global.Mad, shifts, lowest, highest, stdev, mad, pctImp, range);
    }

    private static double GetExperMz<T>(T d) where T : class => d switch
    {
        PsmShiftDatum p => p.ExperMz,
        FragmentShiftDatum f => f.ExperMz,
        _ => throw new InvalidOperationException(),
    };
    private static double GetPpmError<T>(T d) where T : class => d switch
    {
        PsmShiftDatum p => p.PpmError,
        FragmentShiftDatum f => f.PpmError,
        _ => throw new InvalidOperationException(),
    };
}

/// <summary>Scan-time-binned shift (75-second bins). Otherwise identical to
/// <see cref="MzBinnedShift"/>; the dependency variable is the scan start time.</summary>
internal sealed class ScanTimeBinnedShift : BinnedAdjustment
{
    private const double TimeBinSize = 75.0;

    private ScanTimeBinnedShift(double globalShift, double globalMad, double[] shifts,
        int lowestValidBin, int highestValidBin,
        double smoothedStdev, double smoothedMad, double pctImpMad, string shiftRange)
        : base(TimeBinSize, globalShift, globalMad, shifts, lowestValidBin, highestValidBin,
               smoothedStdev, smoothedMad, pctImpMad, shiftRange) { }

    public override string PrettyAdjustment => "Using scan time dependency";

    public override double Shift(double scanTime, double mass) => BinShift(scanTime, mass);

    public static ScanTimeBinnedShift Build<T>(IList<T> data, SimpleGlobalAdjustment global) where T : class
    {
        double maxT = data.Max(d => GetScanTime(d));
        double minT = data.Min(d => GetScanTime(d));
        int lowest = (int)(minT / TimeBinSize);
        int highest = (int)(maxT / TimeBinSize);
        int n = (int)((maxT + TimeBinSize * 4) / TimeBinSize);
        if (n <= 0) n = 1;

        var sortBins = new List<double>[n];
        for (int i = 0; i < n; i++) sortBins[i] = new List<double>();
        var counts = new int[n];
        foreach (var d in data)
        {
            int b = (int)(GetScanTime(d) / TimeBinSize);
            if (b < 0 || b >= n) continue;
            sortBins[b].Add(GetPpmError(d));
            counts[b]++;
        }
        var (shifts, stdev, mad, pctImp, range) = ProcessBins(sortBins, counts, TimeBinSize, lowest, highest, global.GlobalShiftPpm, global.Mad);
        return new ScanTimeBinnedShift(global.GlobalShiftPpm, global.Mad, shifts, lowest, highest, stdev, mad, pctImp, range);
    }

    private static double GetScanTime<T>(T d) where T : class => d switch
    {
        PsmShiftDatum p => p.ScanTime,
        FragmentShiftDatum f => f.ScanTime,
        _ => throw new InvalidOperationException(),
    };
    private static double GetPpmError<T>(T d) where T : class => d switch
    {
        PsmShiftDatum p => p.PpmError,
        FragmentShiftDatum f => f.PpmError,
        _ => throw new InvalidOperationException(),
    };
}

// ---- CVConditionalFilter ----

/// <summary>
/// Filters <see cref="SpectrumIdentificationItem"/> entries by a score threshold derived from
/// a (cv-term, range-set) pair. Port of cpp's <c>CVConditionalFilter</c>.
/// </summary>
internal sealed class CVConditionalFilter
{
    private readonly CVID _scoreCvid;
    private readonly string _scoreName;
    private readonly bool _useNameOnly;
    private double _min;
    private double _max;
    private readonly double _step;
    private readonly int _maxSteps;
    private int _stepCount;
    private readonly bool _isAnd;
    private readonly bool _isMin;
    private readonly bool _isMax;
    private readonly double _center;

    public string ScoreName => _scoreName;
    public string ThresholdDescription
    {
        get
        {
            if (_isMax) return "<= " + _max.ToString("R", CultureInfo.InvariantCulture);
            if (_isMin) return ">= " + _min.ToString("R", CultureInfo.InvariantCulture);
            return _min.ToString("R", CultureInfo.InvariantCulture) + " <= MME <= "
                + _max.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public CVConditionalFilter(IdentData ident, string cvTerm, string rangeSet, double step = 0.0, int maxStep = 0)
    {
        CVID software = ident.AnalysisSoftwareList
            .SelectMany(sw => sw.SoftwareName.CVParams.Select(p => p.Cvid))
            .FirstOrDefault(c => c != CVID.CVID_Unknown);
        _scoreCvid = PepXmlTranslator.PepXmlScoreNameToCVID(software, cvTerm);
        if (_scoreCvid == CVID.CVID_Unknown)
        {
            // Cpp's fallback path: try the full-CV name-based translator before giving up and
            // doing a name-substring search on the SII's params at filter time.
            _scoreCvid = new CVTranslator().Translate(cvTerm);
        }
        if (_scoreCvid == CVID.CVID_Unknown)
        {
            _scoreName = cvTerm;
            _useNameOnly = true;
        }
        else
        {
            _scoreName = CvLookup.CvTermInfo(_scoreCvid).Name;
            _useNameOnly = false;
        }

        var (minValue, maxValue) = ParseDoubleRange(rangeSet);
        _min = minValue;
        _max = maxValue;
        _isAnd = minValue < maxValue;
        _isMin = minValue > double.MinValue && maxValue == double.MaxValue;
        _isMax = maxValue < double.MaxValue && minValue == double.MinValue;
        _center = (_min + _max) / 2.0;
        _step = step;
        _maxSteps = maxStep;
        _stepCount = 0;
    }

    public bool Passes(SpectrumIdentificationItem sii, out double scoreValue)
    {
        scoreValue = 0;
        bool found = false;
        double value = 0;
        if (!_useNameOnly)
        {
            var match = sii.CVParams.FirstOrDefault(p => p.Cvid == _scoreCvid);
            if (match is not null && match.Cvid != CVID.CVID_Unknown
                && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                found = true;
        }
        if (!found)
        {
            foreach (var u in sii.UserParams)
                if (u.Name.EndsWith(_scoreName, StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(u.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    found = true; break;
                }
            if (!found)
            {
                foreach (var cv in sii.CVParams)
                {
                    var name = CvLookup.CvTermInfo(cv.Cvid).Name;
                    if (name.EndsWith(_scoreName, StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(cv.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    {
                        found = true; break;
                    }
                }
            }
        }
        if (!found) return false;
        scoreValue = value;
        return _isAnd
            ? value >= _min && value <= _max
            : value <= _max || value >= _min;
    }

    /// <summary>Cpp's <c>adjustFilterByStep</c>: when a pass returns too few PSMs, relax the
    /// thresholds by multiplying by <see cref="_step"/> up to <see cref="_maxSteps"/> times.
    /// Returns true if the thresholds were relaxed.</summary>
    public bool AdjustByStep()
    {
        if (_step == 0.0 || _stepCount >= _maxSteps) return false;
        if (System.Math.Abs(_max) != double.MaxValue) _max *= _step;
        if (System.Math.Abs(_min) != double.MaxValue) _min *= _step;
        _stepCount++;
        return true;
    }

    /// <summary>Higher-is-better key for ranking PSMs when multiple match the same spectrum
    /// (cpp's <c>sortFilter</c>, which prefers rank before score).</summary>
    public double PreferenceKey(PsmShiftDatum p)
    {
        // Rank-based tie-break: a non-zero rank closer to 1 wins.
        if (p.Rank != 0) return -p.Rank * 1e12 + ScoreKey(p.Score);
        return ScoreKey(p.Score);
    }

    private double ScoreKey(double score)
    {
        if (_isMax) return -score;
        if (_isMin) return score;
        return -System.Math.Abs(score - _center);
    }

    internal static (double Min, double Max) ParseDoubleRange(string rangeSet)
    {
        double min = double.MinValue;
        double max = double.MaxValue;
        if (string.IsNullOrEmpty(rangeSet)) return (min, max);

        if (rangeSet[0] == '[' && rangeSet[^1] == ']')
        {
            string inner = rangeSet[1..^1];
            int comma = inner.LastIndexOf(',');
            string lower = comma < 0 ? inner : inner[..comma];
            string upper = comma < 0 ? "" : inner[(comma + 1)..];
            if (!string.IsNullOrEmpty(lower))
                min = double.Parse(lower, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(upper))
                max = double.Parse(upper, NumberStyles.Float, CultureInfo.InvariantCulture);
            return (min, max);
        }

        bool hasLeadingDash = rangeSet[0] == '-';
        string body = hasLeadingDash ? rangeSet[1..] : rangeSet;

        int splitAt = -1;
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] != '-') continue;
            if (i > 0 && (body[i - 1] == 'e' || body[i - 1] == 'E')) continue;
            splitAt = i; break;
        }

        if (splitAt < 0)
        {
            if (!string.IsNullOrEmpty(body))
                max = double.Parse(body, NumberStyles.Float, CultureInfo.InvariantCulture);
            return (min, max);
        }

        string left = body[..splitAt];
        string right = body[(splitAt + 1)..];
        if (right.Length == 0)
            min = double.Parse(left, NumberStyles.Float, CultureInfo.InvariantCulture);
        else if (left.Length == 0)
            max = double.Parse(right, NumberStyles.Float, CultureInfo.InvariantCulture);
        else
        {
            min = double.Parse(left, NumberStyles.Float, CultureInfo.InvariantCulture);
            max = double.Parse(right, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        return (min, max);
    }
}
