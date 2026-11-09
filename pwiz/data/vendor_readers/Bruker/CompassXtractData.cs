using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

// This file is only compiled when $(NativeVendorsAvailable) is true, i.e. on Windows with the
// vendor licenses accepted (see Bruker.csproj). CA1416 has nothing to warn about here and its
// interface/implementation checks would only fight the [SupportedOSPlatform] annotations, which
// are kept because they document the constraint for anyone reading the code.
#pragma warning disable CA1416

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// <see cref="IBrukerData"/> backed by Bruker's legacy CompassXtract COM server: YEP
/// (<c>analysis.yep</c>, Esquire / HCT / amaZon ion traps) and FID (flex-series MALDI). Port of
/// <c>CompassDataImpl</c> + <c>MSSpectrumImpl</c> in <c>CompassData.cpp:247-700</c>, plus the
/// CompassXtract-specific branches of <c>SpectrumList_Bruker.cpp</c>.
/// </summary>
/// <remarks>
/// <para>CompassXtract is in-process COM and Windows-only. It is activated registration-free
/// through <see cref="CompassXtractActivationContext"/>, which every method that touches an RCW
/// enters before doing so — activation contexts are per-thread, and this object is routinely
/// created on one thread and read on another.</para>
/// <para>FID is a <b>tree</b> of single-spectrum acquisitions rather than one analysis: the root
/// directory is opened once for run-level metadata (analysis date, instrument description,
/// family), while each spectrum is served by opening the individual <c>…/fid</c> directory on
/// demand and closing it again — the same thing cpp does at
/// <c>SpectrumList_Bruker.cpp:117-121</c>, where <c>compassDataPtr_</c> is replaced per scan.</para>
/// <para>The <c>useRecalibratedState</c> flag <see cref="BrukerData.Create"/> takes is not
/// honored here: cpp's CompassXtract path always calls <c>GetMassIntensityValues</c> and never
/// the <c>IMSSpectrum2.GetRecalibratedMassIntensityValues</c> overload.</para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class CompassXtractData : IBrukerData
{
    private readonly Dictionary<int, CompassXtractParameterCache> _parameterCacheByMsLevel = new();
    private readonly Dictionary<string, string> _globalMetadata = new(StringComparer.Ordinal);

    /// <summary>Absolute directories that directly contain a <c>fid</c> file (FID only).</summary>
    private readonly List<string> _fidDirectories = new();

    // Declared as the base interfaces rather than the coclass interfaces: `new EDAL.MSAnalysis()`
    // is the only place the coclass identity is needed, and everything used below
    // (Open, MSSpectrumCollection, InstrumentFamily, InstrumentDescription, AnalysisDateTime;
    // Count and the 1-based indexer) is declared on IMSAnalysis / IMSSpectrumCollection.
    private EDAL.IMSAnalysis? _analysis;
    private EDAL.IMSSpectrumCollection? _spectra;
    private readonly int _spectrumCount;
    private readonly bool _hasMsData;
    private bool _disposed;

    /// <summary>Opens <paramref name="analysisDirectory"/> through CompassXtract.</summary>
    /// <param name="analysisDirectory">The <c>.d</c> directory (YEP) or the root of a FID tree.</param>
    /// <param name="format">Either <see cref="BrukerFormat.Yep"/> or <see cref="BrukerFormat.Fid"/>.</param>
    public CompassXtractData(string analysisDirectory, BrukerFormat format)
    {
        ArgumentException.ThrowIfNullOrEmpty(analysisDirectory);
        if (format is not (BrukerFormat.Yep or BrukerFormat.Fid))
            throw new ArgumentOutOfRangeException(nameof(format),
                "CompassXtract backs the YEP and FID formats only.");

        AnalysisDirectory = analysisDirectory;
        Format = format;

        // cpp CompassData.cpp:521 converts to the Windows 8.3 short path because CompassXtract
        // cannot open a path holding characters outside the current code page, and
        // Reader_Bruker.cpp:243-244 refuses outright when even the short path is not
        // representable. Same two steps here.
        string openPath = Filesystem.GetNonUnicodePath(analysisDirectory);
        if (!IsAscii(openPath))
            throw new NotSupportedException(
                $"[CompassXtract] the Bruker API does not support Unicode in file paths ('{analysisDirectory}')");

        if (format == BrukerFormat.Fid)
            _fidDirectories.AddRange(BrukerData.EnumerateFidDirectories(analysisDirectory));

        using (CompassXtractActivationContext.Activate())
        {
            try
            {
                EDAL.IMSAnalysis analysis = new EDAL.MSAnalysis();
                _analysis = analysis;
                analysis.Open(openPath);
                _spectra = analysis.MSSpectrumCollection;
                _spectrumCount = _spectra.Count;
                _hasMsData = _spectrumCount > 0;    // CompassData.cpp:525
                LoadGlobalMetadata(analysis);
                DetectMsLevels();
            }
            catch
            {
                ReleaseComObjects();
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public BrukerFormat Format { get; }

    /// <inheritdoc/>
    public string AnalysisDirectory { get; }

    /// <summary>
    /// Run-level metadata in the same key/value shape the SQLite-backed backends expose, so
    /// <c>Reader_Bruker</c> stays format-agnostic. Only the keys CompassXtract can actually
    /// answer are present: <c>AcquisitionDateTime</c> and <c>AcquisitionSoftwareVersion</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately absent, because the corresponding cpp accessors are hardcoded to return
    /// nothing for CompassXtract: <c>AcquisitionSoftware</c> (<c>CompassData.cpp:685</c> returns
    /// <c>""</c>, which is what makes <c>Reader_Bruker</c> fall back on the instrument family),
    /// <c>InstrumentSerialNumber</c> (<c>:682</c>) and <c>InstrumentSourceType</c>
    /// (<c>:684</c> returns <c>InstrumentSource_Unknown</c>, which likewise routes the source
    /// term through the family). <c>InstrumentFamily</c> is absent too — CompassXtract reports it
    /// in the SDK enum's numbering rather than the metadata code numbering, so it is surfaced
    /// through <see cref="InstrumentFamily"/> instead of being mis-decoded here.
    /// </remarks>
    public IReadOnlyDictionary<string, string> GlobalMetadata => _globalMetadata;

    /// <inheritdoc/>
    /// <remarks>PASEF is timsTOF-only; CompassXtract predates it.</remarks>
    public bool HasPasefData => false;

    /// <inheritdoc/>
    /// <remarks>diaPASEF is timsTOF-only; CompassXtract predates it.</remarks>
    public bool HasDiaPasefData => false;

    /// <inheritdoc/>
    public bool HasMs1Frames { get; private set; }

    /// <inheritdoc/>
    public bool HasMsNFrames { get; private set; }

    /// <summary>
    /// Always <c>(0, 0)</c>: CompassXtract has no run-level acquisition range. The per-spectrum
    /// range comes from the <c>Scan Begin</c> / <c>Scan End</c> spectrum parameters instead
    /// (<c>CompassData.cpp:404-405</c>), which is where the mzML scan window comes from.
    /// </summary>
    public (double Low, double High) MzAcquisitionRange => (0.0, 0.0);

    /// <inheritdoc/>
    public BrukerInstrumentFamily InstrumentFamily { get; private set; } = BrukerInstrumentFamily.Unknown;

    /// <inheritdoc/>
    public string InstrumentDescription { get; private set; } = string.Empty;

    /// <summary>
    /// Always false: <c>CompassDataImpl::getTIC</c> / <c>getBPC</c> return null pointers
    /// (<c>CompassData.cpp:612-620</c>), so cpp's <c>ChromatogramList_Bruker::createIndex</c>
    /// adds no TIC or BPC entry for YEP / FID.
    /// </summary>
    public bool HasGlobalChromatograms => false;

    /// <summary>
    /// True only for the FTMS / solariX "Mobile Hexapole Position" hack. Everything else routes
    /// through the instrument family, exactly as cpp's <c>createInstrumentConfigurations</c> does
    /// (<c>Reader_Bruker_Detail.cpp:220-230</c>): CompassXtract reports
    /// <c>InstrumentSource_Unknown</c>, so the family decides the source term, and the only case
    /// where the family alone is ambiguous is an FT-ICR that might be MALDI.
    /// </summary>
    public bool IsMaldiSource { get; private set; }

    /// <inheritdoc/>
    /// <remarks>diaPASEF is timsTOF-only.</remarks>
    public IEnumerable<DiaFrameWindow> EnumerateDiaFrameWindows() => Array.Empty<DiaFrameWindow>();

    /// <summary>
    /// Always empty: with <see cref="HasGlobalChromatograms"/> false there is no TIC / BPC to
    /// enumerate, and cpp never asks CompassXtract for chromatogram points.
    /// </summary>
    public IEnumerable<BrukerChromatogramPoint> EnumerateChromatogramPoints(
        int preferOnlyMsLevel, bool passEntireDiaPasefFrame = false)
    {
        _ = preferOnlyMsLevel;
        _ = passEntireDiaPasefFrame;
        return Array.Empty<BrukerChromatogramPoint>();
    }

    /// <summary>
    /// LC traces from <c>chromatography-data.sqlite</c> when the acquisition happens to have one.
    /// </summary>
    /// <remarks>
    /// cpp reads that file for every Bruker format — <c>ChromatogramList_Bruker::createIndex</c>
    /// calls <c>readChromatographyDataSqlite</c> unconditionally
    /// (<c>ChromatogramList_Bruker.cpp:188</c>), outside the TIC / BPC branch that CompassXtract
    /// declines. A 2008-era YEP will not have one, and the reader simply returns nothing.
    /// </remarks>
    public List<LcTrace> ReadLcTraces() => ChromatographyDataSqlite.ReadAll(AnalysisDirectory);

    // ---------- run-level metadata ----------

    private void LoadGlobalMetadata(EDAL.IMSAnalysis analysis)
    {
        if (!_hasMsData) return;

        // CompassData.cpp:665-669 - the SDK enum's numbering is the same as pwiz's for every
        // value CompassXtract can report (Trap=0 .. maXis=7, Unknown=255), so a straight cast is
        // the whole translation; see BrukerInstrumentFamily's remarks.
        InstrumentFamily = (BrukerInstrumentFamily)(int)analysis.InstrumentFamily;
        InstrumentDescription = analysis.InstrumentDescription ?? string.Empty;   // CompassData.cpp:676-680

        // CompassData.cpp:637-651 builds a boost ptime out of AnalysisDateTime's components and
        // Reader_Bruker.cpp:219 renders it with encode_xml_datetime. Round-tripping the DateTime
        // lets Reader_Bruker.ConvertTimestamp produce the same "yyyy-MM-ddTHH:mm:ssZ".
        _globalMetadata["AcquisitionDateTime"] =
            analysis.AnalysisDateTime.ToString("o", CultureInfo.InvariantCulture);

        // CompassData.cpp:686 - the literal string "unknown", which is what lands in the mzML
        // <software version="unknown"> element for YEP / FID.
        _globalMetadata["AcquisitionSoftwareVersion"] = "unknown";

        IsMaldiSource = DetectMaldiSource();
    }

    /// <summary>
    /// Port of the FTMS / solariX branch of <c>createInstrumentConfigurations</c>
    /// (<c>Reader_Bruker_Detail.cpp:220-230</c>): cpp builds a name/value map from the <b>first</b>
    /// spectrum's parameters and promotes the source to MALDI when
    /// <c>Mobile Hexapole Position</c> reads <c>MALDI</c>. Its own comment calls this a hack.
    /// </summary>
    private bool DetectMaldiSource()
    {
        if (InstrumentFamily is not (BrukerInstrumentFamily.Ftms or BrukerInstrumentFamily.SolariX))
            return false;
        if (!_hasMsData || _spectra is null) return false;

        var parameters = new CompassXtractParameterList(_spectra[1].MSSpectrumParameterCollection);
        // cpp builds its name/value map with BOOST_FOREACH (Reader_Bruker_Detail.cpp:146), i.e.
        // an enumeration, so it too stops one short - see CompassXtractParameterList.EnumeratedCount.
        int count = parameters.EnumeratedCount;
        for (int i = 0; i < count; i++)
        {
            var p = parameters[i];
            if (string.Equals(p.Name, "Mobile Hexapole Position", StringComparison.Ordinal))
                return string.Equals(p.Value, "MALDI", StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>
    /// Populates <see cref="HasMs1Frames"/> / <see cref="HasMsNFrames"/> by walking spectra until
    /// both are known. Port of the loop in <c>Reader_Bruker.cpp:176-193</c>.
    /// </summary>
    /// <remarks>
    /// cpp derives the FID loop bound from the <i>root</i> analysis's spectrum count and then
    /// decrements both ends to make it 0-based (<c>Reader_Bruker.cpp:179</c>) — which underflows
    /// to <c>SIZE_MAX</c> if the root open yielded nothing. We bound by the enumerated fid count
    /// as well, so the loop stays in range no matter what the root analysis reports.
    /// </remarks>
    private void DetectMsLevels()
    {
        if (Format == BrukerFormat.Fid)
        {
            int end = _spectrumCount > 0
                ? Math.Min(_spectrumCount, _fidDirectories.Count)
                : _fidDirectories.Count;
            for (int i = 0; i < end && (!HasMs1Frames || !HasMsNFrames); i++)
                RecordMsLevel(OpenFidSpectrumLevel(_fidDirectories[i]));
            return;
        }

        if (!_hasMsData || _spectra is null) return;
        for (int scan = 1; scan <= _spectrumCount && (!HasMs1Frames || !HasMsNFrames); scan++)
            RecordMsLevel(_spectra[scan].MSMSStage);
    }

    private void RecordMsLevel(int msLevel)
    {
        if (msLevel == 1) HasMs1Frames = true;
        else if (msLevel > 1) HasMsNFrames = true;
    }

    private static int OpenFidSpectrumLevel(string fidDirectory)
    {
        EDAL.IMSAnalysis? analysis = null;
        try
        {
            analysis = OpenFid(fidDirectory, out EDAL.IMSSpectrum spectrum);
            return spectrum.MSMSStage;
        }
        finally
        {
            Release(analysis);
        }
    }

    // ---------- index ----------

    /// <summary>
    /// Builds the flat spectrum index. Port of the <c>Reader_Bruker_Format_FID</c> and generic
    /// (<c>scan=N</c>) branches of <c>SpectrumList_Bruker::createIndex</c>
    /// (<c>SpectrumList_Bruker.cpp:756-768</c> and <c>:810-825</c>).
    /// </summary>
    /// <remarks>
    /// <paramref name="preferOnlyMsLevel"/> is intentionally ignored: cpp's
    /// <c>CompassData::create</c> does not forward it to the CompassXtract implementation and
    /// <c>createIndex</c> does no ms-level filtering on these branches, so an <c>--ms1</c>-style
    /// request is served by msconvert's filter chain rather than by the reader. The
    /// ion-mobility flags are meaningless here for the same reason they are for BAF.
    /// </remarks>
    public IReadOnlyList<BrukerIndexEntry> BuildSpectrumIndex(
        bool combineIonMobilitySpectra, int preferOnlyMsLevel, bool passEntireDiaPasefFrame)
    {
        _ = combineIonMobilitySpectra; // CompassXtract data has no mobility dimension.
        _ = preferOnlyMsLevel;         // see remarks: cpp does not filter this path by ms level.
        _ = passEntireDiaPasefFrame;   // TDF diaPASEF only.

        if (Format == BrukerFormat.Fid)
        {
            var fidIndex = new List<BrukerIndexEntry>(_fidDirectories.Count);
            foreach (string fidDirectory in _fidDirectories)
            {
                // "file=" + the xml:ID-encoded source-file id, which is the fid's path relative
                // to the root directory's parent, with forward slashes
                // (SpectrumList_Bruker.cpp:604-608 builds the id, :766 the spectrum id).
                string id = "file=" + EncodeXmlId(BrukerData.FidRelativeId(AnalysisDirectory, fidDirectory));
                fidIndex.Add(new BrukerIndexEntry
                {
                    Index = fidIndex.Count,
                    Id = id,
                    Tag = fidDirectory,
                });
            }
            return fidIndex;
        }

        var index = new List<BrukerIndexEntry>(_spectrumCount);
        for (int scan = 1; scan <= _spectrumCount; scan++)
        {
            index.Add(new BrukerIndexEntry
            {
                Index = index.Count,
                Id = "scan=" + scan.ToString(CultureInfo.InvariantCulture),
                Tag = scan,
            });
        }
        return index;
    }

    // ---------- spectrum ----------

    /// <summary>
    /// Fills <paramref name="spec"/> from the CompassXtract spectrum behind
    /// <paramref name="entry"/>. Port of the non-TDF path of
    /// <c>SpectrumList_Bruker::spectrum</c> (<c>SpectrumList_Bruker.cpp:225-540</c>).
    /// </summary>
    public void FillSpectrum(Spectrum spec, BrukerIndexEntry entry, bool getBinaryData,
        bool preferCentroid, bool sortAndJitter, bool includeIsolationArrays)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(entry);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = sortAndJitter;          // TDF combined-frame only
        _ = includeIsolationArrays; // TDF diaPASEF whole-frame only

        using (CompassXtractActivationContext.Activate())
        {
            if (entry.Tag is string fidDirectory)
            {
                EDAL.IMSAnalysis? analysis = null;
                try
                {
                    analysis = OpenFid(fidDirectory, out EDAL.IMSSpectrum fidSpectrum);
                    FillFromSpectrum(spec, fidSpectrum, getBinaryData, preferCentroid);
                }
                finally
                {
                    Release(analysis);
                }
                return;
            }

            if (_spectra is null)
                throw new InvalidOperationException("[CompassXtract] the analysis has no MS data.");

            int scan = (int)entry.Tag;
            if (scan < 1 || scan > _spectrumCount)
                throw new ArgumentOutOfRangeException(nameof(entry),
                    $"[CompassXtract] scan number {scan} is out of range.");   // CompassData.cpp:569-570
            FillFromSpectrum(spec, _spectra[scan], getBinaryData, preferCentroid);
        }
    }

    private void FillFromSpectrum(Spectrum spec, EDAL.IMSSpectrum spectrum, bool getBinaryData, bool preferCentroid)
    {
        int msLevel = spectrum.MSMSStage;
        spec.Params.Set(CVID.MS_ms_level, msLevel);
        spec.Params.Set(msLevel == 1 ? CVID.MS_MS1_spectrum : CVID.MS_MSn_spectrum);

        var scan = new Scan();
        double scanTime = spectrum.RetentionTime;
        if (scanTime > 0)
            scan.Set(CVID.MS_scan_start_time, scanTime, CVID.UO_second);

        var parameters = new CompassXtractParameterList(spectrum.MSSpectrumParameterCollection);
        var cache = GetParameterCache(msLevel);

        var (scanBegin, scanEnd) = GetScanRange(cache, parameters);
        if (scanBegin > 0 && scanEnd > 0)
            scan.ScanWindows.Add(new ScanWindow(scanBegin, scanEnd, CVID.MS_m_z));

        switch (spectrum.Polarity)
        {
            case EDAL.SpectrumPolarity.IonPolarity_Negative:
                spec.Params.Set(CVID.MS_negative_scan);
                break;
            case EDAL.SpectrumPolarity.IonPolarity_Positive:
                spec.Params.Set(CVID.MS_positive_scan);
                break;
            default:
                break;
        }

        // No base-peak / TIC params: cpp gates them on getTIC() > 0 (SpectrumList_Bruker.cpp:266)
        // and the CompassXtract MSSpectrum returns a hardcoded 0 for both getTIC() and getBPI()
        // (CompassData.cpp:351-352). No ion-mobility params either - oneOverK0() is the base
        // class's 0.0, and no MALDI spotID - getMaldiChip() is the base class's empty optional.

        spec.ScanList.Set(CVID.MS_no_combination);   // SpectrumList_Bruker.cpp:419
        spec.ScanList.Scans.Add(scan);

        if (msLevel > 1)
            AddPrecursor(spec, spectrum, cache, parameters);

        FillPeaks(spec, spectrum, getBinaryData, preferCentroid);
    }

    /// <summary>
    /// Profile-preferred peak selection, port of <c>SpectrumList_Bruker.cpp:478-509</c>: profile
    /// data wins unless the caller asked for centroids or there is no profile data, and a
    /// centroid spectrum produced <i>because</i> the caller asked also gets tagged
    /// <c>profile spectrum</c> so a downstream peak picker knows not to re-centroid it.
    /// </summary>
    /// <remarks>
    /// cpp has a cheaper metadata-only path that asks for the array sizes without the arrays
    /// (<c>:511-540</c>); CompassXtract answers both with the same <c>GetMassIntensityValues</c>
    /// call, so materializing once and only attaching the arrays when
    /// <paramref name="getBinaryData"/> is set produces identical metadata for the same cost.
    /// </remarks>
    private static void FillPeaks(Spectrum spec, EDAL.IMSSpectrum spectrum, bool getBinaryData, bool preferCentroid)
    {
        bool getLineData = preferCentroid;
        double[] mz = Array.Empty<double>();
        double[] intensity = Array.Empty<double>();

        if (!getLineData)
        {
            (mz, intensity) = GetMassIntensityValues(spectrum, EDAL.SpectrumTypes.SpectrumType_Profile);
            if (mz.Length == 0)
                getLineData = true;   // preferred profile, but there isn't any - try centroided
            else
                spec.Params.Set(CVID.MS_profile_spectrum);
        }

        if (getLineData)
        {
            // Declared centroided even when the scan turns out to be empty.
            spec.Params.Set(CVID.MS_centroid_spectrum);
            (mz, intensity) = GetMassIntensityValues(spectrum, EDAL.SpectrumTypes.SpectrumType_Line);
            if (mz.Length > 0 && preferCentroid)
                spec.Params.Set(CVID.MS_profile_spectrum);
        }

        spec.DefaultArrayLength = mz.Length;
        if (getBinaryData)
            spec.SetMZIntensityArrays(mz, intensity, CVID.MS_number_of_detector_counts);
    }

    /// <summary>
    /// Port of the <c>msLevel &gt; 1</c> block of <c>SpectrumList_Bruker::spectrum</c>
    /// (<c>SpectrumList_Bruker.cpp:285-375</c>).
    /// </summary>
    private static void AddPrecursor(Spectrum spec, EDAL.IMSSpectrum spectrum,
        CompassXtractParameterCache cache, CompassXtractParameterList parameters)
    {
        var (fragmentedMzs, fragmentationModes) = GetFragmentationData(spectrum);
        if (fragmentedMzs.Length == 0) return;   // cpp: the whole block is inside `if (!fragMZs.empty())`

        // The isolation modes are decoded but unused - see GetIsolationData's remarks for why
        // they cannot simply be dropped.
        var (isolationMzs, _) = GetIsolationData(spectrum);

        var precursor = new Precursor();
        double isolationWidth = double.NaN;   // cpp re-reads it per iteration; the value cannot change

        for (int i = 0; i < fragmentedMzs.Length; i++)
        {
            if (fragmentedMzs[i] > 0)
            {
                var selectedIon = new SelectedIon(fragmentedMzs[i]);

                int charge = GetChargeState(cache, parameters);
                if (charge > 0)
                    selectedIon.Set(CVID.MS_charge_state, charge);

                // cpp also emits MS_peak_intensity from isolationInfo[i].intensity and a CCS term
                // when ion mobility is available; the CompassXtract IsolationInfo leaves intensity
                // value-initialized to 0 (CompassData.cpp:367) and canConvertOneOverK0AndCCS() is
                // false, so neither ever fires on this path.

                if (i < fragmentationModes.Length)
                    SetActivation(precursor.Activation, fragmentationModes[i]);

                precursor.SelectedIons.Add(selectedIon);
            }

            // cpp indexes isolationInfo with the fragmentation-loop index and would read past the
            // end if the two SAFEARRAYs ever disagreed; treat a missing entry as absent instead.
            double isolationMz = i < isolationMzs.Length ? isolationMzs[i] : 0.0;
            if (isolationMz > 0)
            {
                if (double.IsNaN(isolationWidth))
                    isolationWidth = GetIsolationWidth(cache, parameters);

                precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, isolationMz, CVID.MS_m_z);
                if (isolationWidth > 0)
                {
                    precursor.IsolationWindow.Set(CVID.MS_isolation_window_lower_offset, isolationWidth / 2, CVID.MS_m_z);
                    precursor.IsolationWindow.Set(CVID.MS_isolation_window_upper_offset, isolationWidth / 2, CVID.MS_m_z);
                }

                // MS_collision_energy is likewise gated on isolationInfo[i].collisionEnergy, which
                // the CompassXtract IsolationInfo never populates.
            }
        }

        if (precursor.SelectedIons.Count > 0 || !precursor.IsolationWindow.IsEmpty)
            spec.Precursors.Add(precursor);
    }

    /// <summary>Port of the <c>FragmentationMode</c> switch at <c>SpectrumList_Bruker.cpp:315-342</c>.</summary>
    private static void SetActivation(Activation activation, EDAL.FragmentationModes fragmentationMode)
    {
        switch (fragmentationMode)
        {
            case EDAL.FragmentationModes.FragMode_CID:
                activation.Set(CVID.MS_collision_induced_dissociation);
                break;
            case EDAL.FragmentationModes.FragMode_ETD:
                activation.Set(CVID.MS_electron_transfer_dissociation);
                break;
            case EDAL.FragmentationModes.FragMode_CIDETD_CID:
            case EDAL.FragmentationModes.FragMode_CIDETD_ETD:
                activation.Set(CVID.MS_collision_induced_dissociation);
                activation.Set(CVID.MS_electron_transfer_dissociation);
                break;
            case EDAL.FragmentationModes.FragMode_ISCID:
                activation.Set(CVID.MS_in_source_collision_induced_dissociation);
                break;
            case EDAL.FragmentationModes.FragMode_ECD:
                activation.Set(CVID.MS_electron_capture_dissociation);
                break;
            case EDAL.FragmentationModes.FragMode_IRMPD:
                activation.Set(CVID.MS_infrared_multiphoton_dissociation);
                break;
            // FragMode_PTR has an empty case in cpp; Off / Unknown fall out of the switch.
            default:
                break;
        }
    }

    // ---------- spectrum parameters ----------

    private CompassXtractParameterCache GetParameterCache(int msLevel)
    {
        if (!_parameterCacheByMsLevel.TryGetValue(msLevel, out var cache))
        {
            cache = new CompassXtractParameterCache();
            _parameterCacheByMsLevel[msLevel] = cache;
        }
        return cache;
    }

    /// <summary>Port of <c>MSSpectrumImpl::getScanRange</c> (<c>CompassData.cpp:396-410</c>).</summary>
    /// <remarks>
    /// cpp uses <c>lexical_cast&lt;double&gt;</c>, which throws out of the reader on a value it
    /// cannot parse; an unparsable bound is treated as absent here instead.
    /// </remarks>
    private static (double Begin, double End) GetScanRange(
        CompassXtractParameterCache cache, CompassXtractParameterList parameters)
    {
        string scanBegin = cache.Get("Scan Begin", parameters);
        string scanEnd = cache.Get("Scan End", parameters);
        if (scanBegin.Length > 0 && scanEnd.Length > 0
            && double.TryParse(scanBegin, NumberStyles.Float, CultureInfo.InvariantCulture, out double begin)
            && double.TryParse(scanEnd, NumberStyles.Float, CultureInfo.InvariantCulture, out double end))
            return (begin, end);
        return (0.0, 0.0);
    }

    /// <summary>Port of <c>MSSpectrumImpl::getChargeState</c> (<c>CompassData.cpp:412-440</c>).</summary>
    /// <remarks>
    /// The word forms are deliberately inert. cpp writes
    /// <c>if (chargeState == "single") 1; else if (chargeState == "double") 2; …</c> — expression
    /// statements with no <c>return</c>, so those four spellings fall out of the chain and the
    /// function yields 0. That is the behavior every Bruker CompassXtract reference mzML was
    /// generated with, so it is reproduced rather than fixed; changing it belongs upstream in
    /// C++ first.
    /// </remarks>
    private static int GetChargeState(CompassXtractParameterCache cache, CompassXtractParameterList parameters)
    {
        string chargeState = cache.Get("ChargeState", parameters);
        if (chargeState.Length > 0 && chargeState is not ("single" or "double" or "triple" or "quad"))
        {
            if (int.TryParse(chargeState, NumberStyles.Integer, CultureInfo.InvariantCulture, out int charge)
                && charge > 0)
                return charge;
        }
        return 0;
    }

    /// <summary>Port of <c>MSSpectrumImpl::getIsolationWidth</c> (<c>CompassData.cpp:442-460</c>).</summary>
    /// <remarks>
    /// The truncation is deliberate. cpp parses the parameter as a <c>double</c> and assigns it
    /// into an <c>int</c> (<c>int isolationWidth = lexical_cast&lt;double&gt;(…)</c>, line 453)
    /// before widening it back to the <c>double</c> return type, so a 3.5 Da isolation width is
    /// reported as 3 and anything below 1 Da is suppressed entirely. Reproduced for the same
    /// reason as the charge-state quirk above.
    /// </remarks>
    private static double GetIsolationWidth(CompassXtractParameterCache cache, CompassXtractParameterList parameters)
    {
        string text = cache.Get("IsolationWidth", parameters);
        if (text.Length > 0
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && parsed >= int.MinValue && parsed <= int.MaxValue)
        {
            int truncated = (int)parsed;
            if (truncated > 0)
                return truncated;
        }
        return 0.0;
    }

    // ---------- COM plumbing ----------

    /// <summary>
    /// Opens one <c>fid</c> directory and returns its single spectrum. Port of
    /// <c>SpectrumList_Bruker::getMSSpectrumPtr</c>'s FID branch
    /// (<c>SpectrumList_Bruker.cpp:117-121</c>), which likewise creates a fresh analysis per scan
    /// and asks it for spectrum 1.
    /// </summary>
    /// <remarks>Caller must already hold a <see cref="CompassXtractActivationContext.Activate"/>
    /// scope, and must <see cref="Release"/> the returned analysis.</remarks>
    private static EDAL.IMSAnalysis OpenFid(string fidDirectory, out EDAL.IMSSpectrum spectrum)
    {
        EDAL.IMSAnalysis analysis = new EDAL.MSAnalysis();
        try
        {
            analysis.Open(Filesystem.GetNonUnicodePath(fidDirectory));
            spectrum = analysis.MSSpectrumCollection[1];
            return analysis;
        }
        catch
        {
            Release(analysis);
            throw;
        }
    }

    private static (double[] Mz, double[] Intensity) GetMassIntensityValues(
        EDAL.IMSSpectrum spectrum, EDAL.SpectrumTypes spectrumType)
    {
        spectrum.GetMassIntensityValues(spectrumType, out object massArray, out object intensityArray);
        return (ToDoubleArray(massArray), ToDoubleArray(intensityArray));
    }

    /// <summary>Port of <c>MSSpectrumImpl::getFragmentationData</c> (<c>CompassData.cpp:377-392</c>).</summary>
    private static (double[] Mzs, EDAL.FragmentationModes[] Modes) GetFragmentationData(EDAL.IMSSpectrum spectrum)
    {
        spectrum.GetFragmentationData(out object mzArray, out Array modeArray);
        return (ToDoubleArray(mzArray), ToFragmentationModes(modeArray));
    }

    /// <summary>Port of <c>MSSpectrumImpl::getIsolationData</c> (<c>CompassData.cpp:358-375</c>).</summary>
    /// <remarks>
    /// <para>The mode array is decoded into <see cref="EDAL.IsolationModes"/> rather than left as
    /// a bare <see cref="Array"/>, and that is load-bearing rather than cosmetic. Both mode
    /// parameters carry a <c>FieldMarshal</c> descriptor of the form
    /// <c>SafeArray(VT_I4, SafeArrayUserDefinedSubType = EDAL.&lt;enum&gt;)</c>, so the CLR
    /// marshaller resolves that enum <b>by name, in the assembly holding the signature</b> — which
    /// under <c>EmbedInteropTypes</c> is <c>Pwiz.Vendor.Bruker</c>, not <c>Interop.EDAL</c>. The
    /// compiler only embeds interop types it sees referenced in source, so a version of this
    /// method that discarded the modes without ever naming the type produced
    /// <c>"Could not resolve type 'EDAL.IsolationModes' in assembly 'Pwiz.Vendor.Bruker'"</c> on
    /// the first MS2 spectrum. <see cref="EDAL.FragmentationModes"/> happened to work only because
    /// <see cref="SetActivation"/> switches on it; naming both here stops that from being an
    /// accident of an unrelated method.</para>
    /// <para>Nothing reads an isolation mode back: cpp stores it on <c>IsolationInfo</c>
    /// (<c>CompassData.cpp:371</c>) and <c>SpectrumList_Bruker</c> never consults it. The array is
    /// returned rather than dropped so the reason it has to be decoded stays visible at the one
    /// place that could otherwise "simplify" it away.</para>
    /// </remarks>
    private static (double[] Mzs, EDAL.IsolationModes[] Modes) GetIsolationData(EDAL.IMSSpectrum spectrum)
    {
        spectrum.GetIsolationData(out object mzArray, out Array modeArray);
        return (ToDoubleArray(mzArray), ToIsolationModes(modeArray));
    }

    /// <summary>
    /// Converts a SAFEARRAY handed back through a VARIANT into <c>double[]</c>.
    /// </summary>
    /// <remarks>
    /// <c>GetMassIntensityValues</c> yields a plain <c>double[]</c>, while
    /// <c>GetIsolationData</c> / <c>GetFragmentationData</c> yield a two-dimensional array whose
    /// first column holds the m/z values — cpp casts those to <c>cli::array&lt;double,2&gt;^</c>
    /// and reads <c>[i, 0]</c>. cpp bounds that loop with <c>Length</c> (the total element count),
    /// which is only the row count because the second rank is 1; iterating rows here keeps a wider
    /// array from walking off the end.
    /// </remarks>
    private static double[] ToDoubleArray(object? value)
    {
        switch (value)
        {
            case null:
                return Array.Empty<double>();
            case double[] flat:
                return flat;
            case double[,] grid:
            {
                int rows = grid.GetLength(0);
                if (rows == 0 || grid.GetLength(1) == 0) return Array.Empty<double>();
                var result = new double[rows];
                for (int i = 0; i < rows; i++)
                    result[i] = grid[i, 0];
                return result;
            }
            case Array array when array.Rank == 1:
            {
                var result = new double[array.Length];
                for (int i = 0; i < result.Length; i++)
                    result[i] = Convert.ToDouble(array.GetValue(i), CultureInfo.InvariantCulture);
                return result;
            }
            default:
                return Array.Empty<double>();
        }
    }

    /// <summary>
    /// The marshaller normally hands back an array already typed as the enum, in which case this
    /// is a cast; the element-wise path covers a run where it does not (an <c>int[]</c>, say).
    /// Written out per enum rather than as a generic helper: a generic instantiation over a
    /// type-equivalent (embedded) interop type is exactly the kind of thing that works until it
    /// doesn't, and there are only two of them.
    /// </summary>
    private static EDAL.IsolationModes[] ToIsolationModes(Array? array)
    {
        if (array is null || array.Rank != 1 || array.Length == 0) return Array.Empty<EDAL.IsolationModes>();
        if (array is EDAL.IsolationModes[] typed) return typed;
        var result = new EDAL.IsolationModes[array.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = (EDAL.IsolationModes)Convert.ToInt32(array.GetValue(i), CultureInfo.InvariantCulture);
        return result;
    }

    /// <inheritdoc cref="ToIsolationModes"/>
    private static EDAL.FragmentationModes[] ToFragmentationModes(Array? array)
    {
        if (array is null || array.Rank != 1 || array.Length == 0) return Array.Empty<EDAL.FragmentationModes>();
        if (array is EDAL.FragmentationModes[] typed) return typed;
        var result = new EDAL.FragmentationModes[array.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = (EDAL.FragmentationModes)Convert.ToInt32(array.GetValue(i), CultureInfo.InvariantCulture);
        return result;
    }

    private static bool IsAscii(string value)
    {
        foreach (char c in value)
            if (c > '\x7f') return false;
        return true;
    }

    /// <summary>
    /// pwiz's xml:ID escaping, needed because <c>SpectrumList_Bruker</c> bakes the encoded form
    /// into the FID nativeID itself (<c>"file=" + encode_xml_id_copy(...)</c>,
    /// <c>SpectrumList_Bruker.cpp:766</c>) rather than leaving it to the serializer. Port of
    /// <c>encode_xml_id</c> / <c>insertEncodedChar</c> in <c>XMLWriter.cpp:333-387</c>: every
    /// character outside <c>NCNameChar</c> (and a leading character outside
    /// <c>NCNameStartChar</c>) becomes <c>_x00</c> + two lowercase hex digits + <c>_</c>.
    /// </summary>
    internal static string EncodeXmlId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool ok = i == 0 ? IsNCNameStartChar(c) : IsNCNameChar(c);
            if (ok)
            {
                sb.Append(c);
                continue;
            }
            int b = c & 0xFF;   // cpp encodes a `char`, i.e. the low byte
            sb.Append("_x00")
              .Append(HexDigits[(b & 0xF0) >> 4])
              .Append(HexDigits[b & 0x0F])
              .Append('_');
        }
        return sb.ToString();
    }

    private const string HexDigits = "0123456789abcdef";

    private static bool IsNCNameStartChar(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';

    private static bool IsNCNameChar(char c) =>
        IsNCNameStartChar(c) || (c >= '0' && c <= '9') || c == '.' || c == '-';

    // ---------- lifetime ----------

    /// <summary>
    /// Releases the CompassXtract COM objects.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately <b>no finalizer</b>, unlike <see cref="Baf2SqlData"/> and
    /// <see cref="TimsBinaryData"/>. Those hold a raw native handle in a field — a
    /// <see cref="ulong"/> the GC knows nothing about — so an instance dropped without
    /// <c>Dispose</c> leaks it until the process exits, and only a finalizer can close it. This
    /// class holds no native handle: it holds runtime callable wrappers, which are ordinary
    /// managed objects with finalizers of their own. Drop a <see cref="CompassXtractData"/>
    /// without disposing it and the RCWs become unreachable in the same GC pass and release their
    /// interfaces on that pass's finalization, which is what
    /// <c>VendorReaderTestHarness.ProbeFinalizers</c> observes.</para>
    /// <para>Adding a finalizer here would not improve that and would introduce a hazard: it
    /// would call <see cref="Marshal.FinalReleaseComObject"/> on the finalizer thread, tearing
    /// COM objects down from a thread that never entered the activation context and in an order
    /// the CLR would otherwise pick. <see cref="Dispose()"/> keeps the deterministic release for
    /// the normal path, where it runs on the caller's thread.</para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseComObjects();
    }

    private void ReleaseComObjects()
    {
        Release(_spectra);
        _spectra = null;
        Release(_analysis);
        _analysis = null;
    }

    /// <summary>
    /// Drops an RCW's reference deterministically. cpp's destructor does the same thing with
    /// <c>delete msAnalysis_</c> (<c>CompassData.cpp:546</c>) and notes in the lines right after
    /// it that CompassXtract keeps the underlying file locked regardless — "allow CompassXtract
    /// to keep this file locked because it doesn't seem to interfere with opening the file again".
    /// </summary>
    private static void Release(object? comObject)
    {
        if (comObject is null) return;
        try
        {
            if (Marshal.IsComObject(comObject))
                Marshal.FinalReleaseComObject(comObject);
        }
        catch (ArgumentException)
        {
            // Already released, or never an RCW; nothing left to do.
        }
        catch (InvalidComObjectException)
        {
            // Separated from its underlying interface by an earlier release.
        }
    }
}
