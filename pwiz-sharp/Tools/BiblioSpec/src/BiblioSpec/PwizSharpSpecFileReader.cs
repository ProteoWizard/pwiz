// Port of pwiz_tools/BiblioSpec/src/PwizReader.{h,cpp}
//
// Wraps pwiz-sharp's MsData library (Pwiz.Data.MsData) so BiblioSpec's BuildParser can
// look up reference spectra in mzML / mzXML / MGF / mzMLb (and, when the vendor SDKs are
// wired in, native vendor formats too).

using System.Globalization;
using System.Linq;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

// Disambiguate: BiblioSpec also has a type called Spectrum (PEAK_T-based) in this
// namespace, so we alias the pwiz-sharp MsData one rather than the BiblioSpec one.
using PwizSpectrum = Pwiz.Data.MsData.Spectra.Spectrum;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// pwiz-sharp implementation of <see cref="ISpecFileReader"/>. Opens the file via
/// <see cref="ReaderList.Default"/> and serves spectra out of the resulting
/// <see cref="MSData"/> document.
/// </summary>
/// <remarks>
/// <para>Port of <c>PwizReader</c> (<c>pwiz_tools/BiblioSpec/src/PwizReader.{h,cpp}</c>). The cpp
/// class uses <c>SpectrumInfo</c> as an intermediate; pwiz-sharp doesn't have a sharp port of
/// SpectrumInfo yet so we read directly off the <see cref="PwizSpectrum"/> object using the
/// same CV terms (<see cref="CVID.MS_scan_start_time"/>, <see cref="CVID.MS_ms_level"/>,
/// <see cref="CVID.MS_selected_ion_m_z"/>, <see cref="CVID.MS_charge_state"/>, etc.).</para>
/// <para>Behavior preserved from cpp PwizReader.cpp:</para>
/// <list type="bullet">
/// <item>combineIonMobilitySpectra is set on the reader config (cpp PwizReader.cpp:64).</item>
/// <item>If the native id format is unknown / missing, fall back to
/// <see cref="CVID.MS_scan_number_only_nativeID_format"/> (cpp PwizReader.cpp:79).</item>
/// <item>Empty files raise <see cref="Verbosity.Error(string)"/> (cpp PwizReader.cpp:73).</item>
/// <item>String-id lookup tries the native id first, then falls back to TITLE= / spot-id lookup;
/// remembers the successful strategy for the next call (cpp PwizReader.cpp:185).</item>
/// <item>Scan-number lookup translates the integer through
/// <see cref="Id.TranslateScanNumberToNativeId"/> then calls
/// <c>SpectrumList.Find</c> (cpp PwizReader.cpp:377).</item>
/// <item>retentionTime is converted seconds → minutes (cpp PwizReader.cpp:437).</item>
/// <item>Missing scan number lookups warn once at <see cref="VerbosityLevel.Warn"/>, then
/// silence to <see cref="VerbosityLevel.Debug"/> to avoid spamming (cpp PwizReader.cpp:395).</item>
/// </list>
/// <para>cpp parity gaps:</para>
/// <list type="bullet">
/// <item><c>mzSort</c> sorts the iteration order by precursor m/z. Sharp port stubs this with a
/// warning because the SpectrumList interface doesn't expose a cheap sort hook; reads stay in
/// file order.</item>
/// <item>Per-product-ion mobility (<see cref="SpecData.ProductIonMobilities"/>) requires Waters
/// MSe-aware vendor reading; not populated here (cpp PwizReader.cpp doesn't populate it either —
/// it gets filled in elsewhere by the Waters vendor code path).</item>
/// </list>
/// </remarks>
public sealed class PwizSharpSpecFileReader : SpecFileReaderBase
{
    private string _fileName = string.Empty;
    private MSData? _fileReader;
    private ISpectrumList? _allSpectra;
    private CVID _nativeIdFormat = CVID.CVID_Unknown;
    private SpecIdType _idType = SpecIdType.ScanNumberId;
    private VerbosityLevel _idNotFoundWarnLevel = VerbosityLevel.Warn;
    private int _curPosition;
    private int[]? _mzSortedOrder;
    private bool _lookUpByNative = true; // cpp PwizReader.cpp:185 static
    private bool _disposed;

    /// <inheritdoc/>
    public override SpecIdType IdType
    {
        set => _idType = value;
    }

    /// <inheritdoc/>
    public override void OpenFile(string path, bool mzSort = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        Verbosity.Comment(VerbosityLevel.Detail, $"PwizSharpSpecFileReader preparing file '{path}'.");

        try
        {
            _fileName = path;
            _fileReader?.Dispose();
            _fileReader = new MSData();

            // cpp PwizReader.cpp:63-65: combineIonMobilitySpectra is the one knob the cpp
            // version flips on to make IMS files surface as combined frames (BiblioSpec only
            // cares about precursor + peaks; per-IMS-bin spectra aren't useful here).
            var config = new ReaderConfig { CombineIonMobilitySpectra = true };
            ReaderList.Default.Read(_fileName, _fileReader, config);

            _allSpectra = _fileReader.Run.SpectrumList;
            if (_allSpectra is null || _allSpectra.Count == 0)
            {
                // cpp PwizReader.cpp:73 calls Verbosity::error which throws via BlibException.
                Verbosity.Error($"No spectra found in {path}.");
                return;
            }

            Verbosity.Debug($"Found {_allSpectra.Count} spectra in {path}.");

            // cpp PwizReader.cpp:78 — id::getDefaultNativeIDFormat(MSData&) pulls the
            // CVID off the first SourceFile. Mirror that logic here (sharp doesn't have a
            // standalone helper for this).
            _nativeIdFormat = GetDefaultNativeIdFormat(_fileReader);
            if (_nativeIdFormat == CVID.MS_no_nativeID_format || _nativeIdFormat == CVID.CVID_Unknown)
            {
                _nativeIdFormat = CVID.MS_scan_number_only_nativeID_format;
            }
            // The source-file CV records the UPSTREAM vendor format (e.g. mzXML converted from
            // a Thermo .raw still reports MS_Thermo_nativeID_format on its SourceFile). But the
            // mzXML itself stores spectra by scan-number-only IDs. Verify the detected format
            // matches the actual ID shape of the first spectrum; if not, infer from the ID.
            if (_allSpectra is not null && _allSpectra.Count > 0)
            {
                var probedFormat = InferNativeIdFormatFromSpectrumId(_allSpectra.GetSpectrum(0, false).Id);
                if (probedFormat != CVID.CVID_Unknown && probedFormat != _nativeIdFormat)
                {
                    Verbosity.Debug(
                        $"PwizSharpSpecFileReader: source-file CV reports {_nativeIdFormat} but first " +
                        $"spectrum ID looks like {probedFormat}; using {probedFormat} for lookups.");
                    _nativeIdFormat = probedFormat;
                }
            }

            Verbosity.Debug(
                $"PwizSharpSpecFileReader lookup method is {BlibUtils.SpecIdTypeToString(_idType)}, " +
                $"nativeIdFormat is {_nativeIdFormat}");

            if (mzSort)
            {
                // cpp PwizReader.cpp:90-122 builds a (scanIndex, precursorMz) list and sorts
                // by mz ascending. BlibSearch uses this by default (only --preserve-order opts
                // out); without it the search-results goldens diverge from cpp's.
                var pairs = new List<(int Index, double Mz)>(_allSpectra!.Count);
                for (var i = 0; i < _allSpectra.Count; i++)
                {
                    var s = _allSpectra.GetSpectrum(i, false);
                    double m = 0;
                    if (s.Precursors.Count > 0 && s.Precursors[0].SelectedIons.Count > 0)
                        m = s.Precursors[0].SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();
                    pairs.Add((i, m));
                }
                // cpp uses std::stable_sort which preserves file order for equal-mz rows. .NET
                // List<T>.Sort is unstable; use LINQ OrderBy (stable) so tied-mz tiebreaks match.
                _mzSortedOrder = pairs.OrderBy(p => p.Mz).Select(p => p.Index).ToArray();
            }
        }
        catch (BlibException)
        {
            // Verbosity.Error already wrapped; propagate as-is.
            throw;
        }
        catch (Exception ex)
        {
            // cpp PwizReader.cpp:124-133 catches std::exception and re-emits via Verbosity::error.
            Console.Error.WriteLine($"ERROR: {ex.Message}.");
            Verbosity.Error($"PwizSharpSpecFileReader could not parse {_fileName}");
        }
    }

    /// <inheritdoc/>
    public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
    {
        ArgumentNullException.ThrowIfNull(returnData);
        EnsureOpen();

        Verbosity.Comment(VerbosityLevel.Detail,
            $"PwizSharpSpecFileReader looking for {BlibUtils.SpecIdTypeToString(findBy)} {identifier}.");

        int foundIndex = GetSpecIndex(identifier, findBy);
        if (foundIndex < 0 || foundIndex >= _allSpectra!.Count)
        {
            // already warned in GetSpecIndex
            return false;
        }

        var spec = _allSpectra.GetSpectrum(foundIndex, getPeaks);
        if (spec is null)
        {
            return false;
        }

        // cpp PwizReader.cpp:168 — only enforce ms2-ness when peaks were requested. MS1-only
        // lookups can land on a precursor-only entry that's level 1.
        int msLevel = spec.CvParam(CVID.MS_ms_level).ValueAs<int>();
        if (getPeaks && msLevel != 2)
        {
            Verbosity.Warn($"Spectrum {identifier} is level {msLevel}, not 2.");
            return false;
        }

        TransferSpec(spec, returnData, getPeaks);
        return true;
    }

    /// <inheritdoc/>
    public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(returnData);
        EnsureOpen();

        Verbosity.Comment(VerbosityLevel.Detail,
            $"PwizSharpSpecFileReader looking for id {identifier}.");

        int foundIndex = GetSpecIndex(identifier);
        if (foundIndex < 0)
        {
            return false;
        }

        return GetSpectrum(foundIndex, returnData, SpecIdType.IndexId, getPeaks);
    }

    /// <inheritdoc/>
    public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
    {
        ArgumentNullException.ThrowIfNull(returnData);
        EnsureOpen();

        // cpp PwizReader.cpp:351 — when no indexMzPairs were built (the common case unless
        // openFile was called in INDEX_ID mode with mzSort), getNextSpecIndex walks
        // sequentially from 0 → size-1. We replicate the simpler path here directly. When
        // mzSort was requested, the iteration walks the sorted index permutation instead.
        int total = _mzSortedOrder?.Length ?? _allSpectra!.Count;
        if (_curPosition >= total)
        {
            return false;
        }

        int index = _mzSortedOrder is null ? _curPosition : _mzSortedOrder[_curPosition];
        _curPosition++;
        if (!GetSpectrum(index, returnData, SpecIdType.IndexId, getPeaks))
        {
            Verbosity.Warn(
                $"Could not fetch spectrum at index {index} even though there should be " +
                $"{_allSpectra!.Count} spec in the file.");
            return false;
        }
        return true;
    }

    // ----- helpers -----

    private void EnsureOpen()
    {
        if (_fileReader is null || _allSpectra is null)
            throw new BlibException(false, "PwizSharpSpecFileReader: OpenFile must be called first.");
    }

    /// <summary>
    /// Translate an integer identifier (scan number or zero-based index) to a zero-based
    /// spectrum-list index. Returns -1 when not found.
    /// </summary>
    /// <remarks>cpp parity: PwizReader.cpp:364 <c>getSpecIndex(int, SPEC_ID_TYPE)</c>.</remarks>
    private int GetSpecIndex(int identifier, SpecIdType findBy)
    {
        if (findBy == SpecIdType.IndexId)
        {
            if (identifier >= _allSpectra!.Count)
            {
                Verbosity.Warn(
                    $"Given index, {identifier}, is out of range ({_allSpectra.Count} spec in file).");
            }
            return identifier;
        }

        // Scan number → native id string → list index.
        string scanStr = identifier.ToString(CultureInfo.InvariantCulture);
        string idString = Id.TranslateScanNumberToNativeId(_nativeIdFormat, scanStr);

        if (string.IsNullOrEmpty(idString))
        {
            Verbosity.Warn(
                $"Could not translate integer {identifier} to native id format {_nativeIdFormat} in {_fileName}");
            return -1;
        }

        int foundIndex = _allSpectra!.Find(idString);
        if (foundIndex == _allSpectra.Count)
        {
            Verbosity.Comment(_idNotFoundWarnLevel,
                $"Could not find scan number {identifier}, native id '{idString}' in {_fileName}.");
            // cpp PwizReader.cpp:395 — squelch the warning once it's been said. Same behavior here.
            if (_idNotFoundWarnLevel == VerbosityLevel.Warn)
                _idNotFoundWarnLevel = VerbosityLevel.Debug;
            return -1;
        }
        return foundIndex;
    }

    /// <summary>
    /// Translate a string identifier (native id or TITLE= / spot id) to a zero-based index.
    /// Returns -1 when not found.
    /// </summary>
    /// <remarks>
    /// cpp parity: PwizReader.cpp:184 <c>getSpecIndex(const string&amp;)</c>. The cpp uses a
    /// static `lookUpByNative` so subsequent calls remember which lookup style worked; we mirror
    /// that with an instance field (per-reader is fine — file format doesn't change mid-read).
    /// </remarks>
    private int GetSpecIndex(string identifier)
    {
        int foundIndex = -1;
        int timesLooked = 0;
        while (timesLooked < 2 && foundIndex == -1)
        {
            if (_lookUpByNative)
            {
                if (identifier.IndexOf('=', StringComparison.Ordinal) < 0)
                {
                    foundIndex = -1;
                    _lookUpByNative = !_lookUpByNative;
                }
                else
                {
                    foundIndex = _allSpectra!.Find(identifier);
                    if (foundIndex == _allSpectra.Count)
                    {
                        foundIndex = -1;
                        _lookUpByNative = !_lookUpByNative;
                    }
                }
            }
            else
            {
                var spots = _allSpectra!.FindSpotId(identifier);
                if (spots.Count == 1)
                {
                    foundIndex = spots[0];
                }
                else if (spots.Count > 1)
                {
                    Verbosity.Error($"Multiple spectra found with TITLE='{identifier}'.");
                }
                else
                {
                    foundIndex = -1;
                    _lookUpByNative = !_lookUpByNative;
                }
            }
            timesLooked++;
        }

        if (foundIndex == -1)
        {
            Verbosity.Comment(_idNotFoundWarnLevel,
                $"Could not find native id or title '{identifier}' in {_fileName}.");
            if (_idNotFoundWarnLevel == VerbosityLevel.Warn)
                _idNotFoundWarnLevel = VerbosityLevel.Debug;
        }
        return foundIndex;
    }

    /// <summary>
    /// Mirror of cpp <c>id::getDefaultNativeIDFormat</c>: pull the native id format off the
    /// first source file in the document.
    /// </summary>
    private static CVID GetDefaultNativeIdFormat(MSData msd)
    {
        var sf = msd.Run.DefaultSourceFile;
        if (sf is null && msd.FileDescription.SourceFiles.Count > 0)
            sf = msd.FileDescription.SourceFiles[0];
        if (sf is null) return CVID.CVID_Unknown;
        return sf.CvParamChild(CVID.MS_nativeID_format).Cvid;
    }

    /// <summary>
    /// Infer the native-id format from the shape of an actual spectrum id string. Used to
    /// correct cases where the source-file CV reports the upstream vendor format (e.g.
    /// MS_Thermo_nativeID_format) but the current file stores spectra under a different
    /// format (e.g. mzXML / MGF use scan-number-only).
    /// </summary>
    private static CVID InferNativeIdFormatFromSpectrumId(string id)
    {
        if (string.IsNullOrEmpty(id)) return CVID.CVID_Unknown;
        // Thermo: "controllerType=0 controllerNumber=1 scan=N"
        if (id.StartsWith("controllerType=", StringComparison.Ordinal))
            return CVID.MS_Thermo_nativeID_format;
        // Waters: "function=N process=N scan=N"
        if (id.StartsWith("function=", StringComparison.Ordinal))
            return CVID.MS_Waters_nativeID_format;
        // mzML PSI scan format: "scan=N"
        if (id.StartsWith("scan=", StringComparison.Ordinal))
            return CVID.MS_scan_number_only_nativeID_format;
        // MGF: "index=N"
        if (id.StartsWith("index=", StringComparison.Ordinal))
            return CVID.MS_multiple_peak_list_nativeID_format;
        // mzXML / simple integer id.
        for (int i = 0; i < id.Length; i++)
            if (!char.IsDigit(id[i])) return CVID.CVID_Unknown;
        return CVID.MS_scan_number_only_nativeID_format;
    }

    /// <summary>
    /// Copy precursor + peak data from a pwiz-sharp <see cref="PwizSpectrum"/> into the
    /// BiblioSpec <see cref="SpecData"/> carrier.
    /// </summary>
    /// <remarks>
    /// cpp parity: PwizReader.cpp:433 <c>transferSpec</c>. The cpp version walks SpectrumInfo;
    /// we walk the spectrum directly using the same CV terms (MS:1000016 retentionTime,
    /// MS:1000744 selectedIonMz, MS:1000041 chargeState, etc.).
    /// </remarks>
    private static void TransferSpec(PwizSpectrum spec, SpecData returnData, bool getPeaks)
    {
        // cpp PwizReader.cpp:436 — id is the integer extracted from the native id ("scan=N" -> N).
        returnData.Id = Id.ValueAs<int>(spec.Id, "scan");

        var scan = spec.ScanList.Scans.Count > 0 ? spec.ScanList.Scans[0] : null;

        // cpp PwizReader.cpp:437 — retentionTime is seconds→minutes. Note timeInSeconds()
        // handles the units lookup; sharp port lives on CVParam.TimeInSeconds().
        double rtSeconds = scan is not null ? scan.CvParam(CVID.MS_scan_start_time).TimeInSeconds() : 0;
        returnData.RetentionTime = rtSeconds / 60.0;

        // Total ion current — sits on the spectrum-level CV, not the scan.
        returnData.TotalIonCurrent = spec.CvParam(CVID.MS_total_ion_current).ValueAs<double>();

        // Precursor m/z + charge — read from the first selected ion of the first precursor when
        // present. MS1 / precursor-only spectra leave the defaults (0 / 0). returnData is reused
        // across calls (BlibSearch.cs:471 keeps one SpecData for every GetNextSpectrum), so the
        // precursor-related state — Mz, Charge, Charges — must be reset unconditionally before
        // any new value is written. Reset BEFORE the guard so MS1 / precursor-less spectra also
        // discard prior call's data.
        returnData.Mz = 0;
        returnData.Charge = 0;
        returnData.Charges.Clear();
        if (spec.Precursors.Count > 0 && spec.Precursors[0].SelectedIons.Count > 0)
        {
            var selectedIon = spec.Precursors[0].SelectedIons[0];
            returnData.Mz = selectedIon.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>();

            // cpp PwizReader.cpp:417 walks ALL CV params and collects MS_charge_state +
            // MS_possible_charge_state into a Spectrum.possibleCharges vector. MS2-format
            // queries with multiple Z lines surface as multiple MS_possible_charge_state params;
            // dropping them silently would cause BlibSearch to miss matches at higher charges
            // (e.g. the demo-negative golden expects charges "2,3" — only the first survives
            // a "first-one-wins" read). Iterate via CollectChargeCvParams which mirrors cpp's
            // recursive cvParam walk through referenceableParamGroupRef indirection — a bare
            // foreach on selectedIon.CVParams would miss charges stashed in a ParamGroup.
            bool negative = spec.HasCVParam(CVID.MS_negative_scan);
            foreach (var cv in CollectChargeCvParams(selectedIon))
            {
                int c = cv.ValueAs<int>();
                if (c == 0) continue;
                if (negative) c = -c;
                returnData.Charges.Add(c);
            }
            if (returnData.Charges.Count > 0)
                returnData.Charge = returnData.Charges[0];
        }

        // Ion mobility — three possible CV slots, in the same precedence cpp BiblioSpec uses
        // (Skyline writes the same enum: drift > inverse_reduced > FAIMS CV).
        if (scan is not null)
        {
            var imDrift = scan.CvParam(CVID.MS_ion_mobility_drift_time);
            if (!imDrift.IsEmpty)
            {
                returnData.IonMobility = (float)imDrift.ValueAs<double>();
                returnData.IonMobilityType = IonMobilityType.DriftTimeMsec;
            }
            else
            {
                var imIrim = scan.CvParam(CVID.MS_inverse_reduced_ion_mobility);
                if (!imIrim.IsEmpty)
                {
                    returnData.IonMobility = (float)imIrim.ValueAs<double>();
                    returnData.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
                }
                else
                {
                    var imFaims = scan.CvParam(CVID.MS_FAIMS_compensation_voltage);
                    if (!imFaims.IsEmpty)
                    {
                        returnData.IonMobility = (float)imFaims.ValueAs<double>();
                        returnData.IonMobilityType = IonMobilityType.CompensationV;
                    }
                }
            }

            // BiblioSpec's RefSpectra.startTime / endTime columns are RETENTION TIME bounds
            // (DIA precursor sweep window in minutes), not the MS_scan_window_lower/upper_limit
            // m/z bounds. cpp's PwizReader doesn't populate these from scan windows — leave
            // them at 0 (rendered as N/A in the .check). A future DIA-specific code path can
            // wire the correct CV terms when needed.
        }

        // CCS — looked up off the spectrum level CV.
        var ccsParam = spec.CvParam(CVID.MS_collisional_cross_sectional_area);
        if (!ccsParam.IsEmpty)
            returnData.Ccs = (float)ccsParam.ValueAs<double>();

        if (!getPeaks)
        {
            returnData.NumPeaks = 0;
            returnData.Mzs = null;
            returnData.Intensities = null;
            return;
        }

        // Peaks: copy mz + intensity arrays. cpp PwizReader.cpp:444 — numPeaks is the array size,
        // not defaultArrayLength (which is sometimes a vendor hint).
        var mzArray = spec.GetMZArray();
        var intArray = spec.GetIntensityArray();
        if (mzArray is null || intArray is null)
        {
            returnData.NumPeaks = 0;
            returnData.Mzs = null;
            returnData.Intensities = null;
            return;
        }

        int n = Math.Min(mzArray.Data.Count, intArray.Data.Count);
        returnData.NumPeaks = n;
        if (n > 0)
        {
            var mzs = new double[n];
            var intensities = new float[n];
            for (int i = 0; i < n; i++)
            {
                mzs[i] = mzArray.Data[i];
                intensities[i] = (float)intArray.Data[i];
            }
            returnData.Mzs = mzs;
            returnData.Intensities = intensities;
        }
        else
        {
            returnData.Mzs = null;
            returnData.Intensities = null;
        }

        // TODO: ProductIonMobilities — populated only for Waters MSe combine-IMS reads, which
        // requires walking the mobility binary data array (MS_raw_ion_mobility_drift_time_array
        // or MS_mean_ion_mobility_drift_time_array). cpp PwizReader.cpp doesn't populate it; the
        // Waters vendor reader fills it in downstream. Left null here to match.
    }

    /// <inheritdoc/>
    /// <summary>
    /// Yield every <see cref="CVParam"/> on <paramref name="container"/> (and its referenced
    /// ParamGroups, recursively) whose Cvid is either <see cref="CVID.MS_charge_state"/> or
    /// <see cref="CVID.MS_possible_charge_state"/>. Mirrors cpp's
    /// <c>cvParams(ParamContainer)</c> iterator which walks <c>referenceableParamGroupRef</c>
    /// indirection — a bare foreach over <see cref="ParamContainer.CVParams"/> would miss
    /// charges stashed in a referenced param group, which is legal per the mzML schema.
    /// </summary>
    private static IEnumerable<CVParam> CollectChargeCvParams(ParamContainer container)
    {
        foreach (var cv in container.CVParams)
        {
            if (cv.Cvid == CVID.MS_charge_state || cv.Cvid == CVID.MS_possible_charge_state)
                yield return cv;
        }
        foreach (var pg in container.ParamGroups)
        {
            if (pg is null) continue;
            foreach (var cv in CollectChargeCvParams(pg))
                yield return cv;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing)
        {
            _fileReader?.Dispose();
            _fileReader = null;
            _allSpectra = null;
        }
        base.Dispose(disposing);
    }
}
