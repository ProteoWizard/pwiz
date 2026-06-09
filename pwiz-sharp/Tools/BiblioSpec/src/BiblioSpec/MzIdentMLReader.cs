// Port of pwiz_tools/BiblioSpec/src/MzIdentMLReader.{h,cpp}
//
// BuildParser subclass that reads .mzid / .mzid.gz files via the pwiz-sharp IdentData
// layer (Pwiz.Data.IdentData.IdentDataFile, the C# equivalent of cpp's
// pwiz::identdata::IdentDataFile) and populates PSMs. Mirrors the cpp class structure
// 1:1 — see MzIdentMLReader.cpp for the line-level references.

using System.Globalization;
using System.IO.Compression;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.IdentData;
using Pwiz.Data.IdentData.Mzid;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Class for parsing mzIdentML (<c>.mzid</c> / <c>.mzid.gz</c>) files produced by Scaffold,
/// Byonic, MSGF+, PeptideShaker, Mascot, PEAKS, ProteinPilot, etc.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::MzIdentMLReader</c> (MzIdentMLReader.h:33). The cpp version
/// parses with <c>pwiz::identdata::IdentDataFile</c>; this port uses the equivalent
/// <see cref="Pwiz.Data.IdentData.IdentDataFile"/>, which gives us the same in-memory
/// schema-shaped tree without re-implementing XmlReader parsing here.</para>
/// <para>Gzipped input is decompressed via <see cref="GZipStream"/> before being handed
/// to <see cref="MzidReader.ReadInto(Stream, IdentData)"/>. The cpp version's
/// <c>boost::iostreams</c> gz wrapping is the equivalent.</para>
/// </remarks>
public class MzIdentMLReader : BuildParser
{
    // cpp parity: MzIdentMLReader.h:45 ANALYSIS enum — used to decide which CVParam carries
    // the score and how to interpret it.
    private enum Analysis
    {
        Unknown = 0,
        Scaffold,
        Byonic,
        Msgf,
        PeptideShaker,
        Mascot,
        Peaks,
        ProtPilot,
        GenericQvalue,
    }

    // cpp parity: MzIdentMLReader.cpp:132 — SAXHandler::EndEarlyException is used to short-
    // circuit parseFile() during a score-type lookup. We use a private exception type as the
    // C# equivalent; only this class catches it.
    private sealed class EndEarlyException : Exception { }

    // cpp parity: MzIdentMLReader.h:55-65 — pwizReader_ + analysisType_ + fileMap_ +
    // scoreThreshold_ + isScoreLookup_.
    private Analysis _analysisType = Analysis.Unknown;
    private IdentDataFile _pwizReader = null!;
    private readonly Dictionary<string, List<PSM?>> _fileMap = new(StringComparer.Ordinal);
    private double _scoreThreshold;
    private bool _isScoreLookup;

    // cpp parity: VENDOR_READERS is conditionally compiled in cpp (MzIdentMLReader.cpp:94).
    // C# always includes them; spectrum-file resolution is best-effort either way.
    private static readonly string[] _specExtensions =
    {
        ".MGF",
        ".mzXML",
        ".mzML",
        ".mz5",
        ".raw",   // Waters/Thermo
        ".wiff",  // Sciex
        ".wiff2", // Sciex
        ".d",     // Bruker/Agilent
        ".lcd",   // Shimadzu
    };

    /// <summary>
    /// Returns true if <paramref name="path"/> is an mzIdentML file (<c>.mzid</c> or
    /// <c>.mzid.gz</c>). Search-engine detection (Scaffold / Byonic / PeptideShaker /
    /// MSGF+ / Mascot / PEAKS / ProteinPilot / generic q-value) is done at parse time
    /// from CV terms inside the file. Used by <see cref="BlibBuilder"/>'s reader-factory
    /// dispatch — each reader declares its own accepted extensions in one place.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".mzid", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mzid.gz", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Constructor: opens <paramref name="mzidFileName"/> via <see cref="IdentDataFile"/> and
    /// stages the file iterator at the first SpectrumIdentificationList.
    /// </summary>
    /// <param name="maker">BlibBuilder writing the .blib library.</param>
    /// <param name="mzidFileName">Path to <c>.mzid</c> or <c>.mzid.gz</c>.</param>
    /// <param name="parentProgress">Optional caller-supplied progress indicator.</param>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:36.</remarks>
    public MzIdentMLReader(BlibBuilder maker, string mzidFileName, ProgressIndicator? parentProgress)
        : base(maker, mzidFileName, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mzidFileName);

        _analysisType = Analysis.Unknown;
        try
        {
            _pwizReader = OpenIdentDataFile(mzidFileName);
        }
        catch (Exception ex) when (ex is not BlibException)
        {
            throw new BlibException(true, $"Failed to parse mzIdentML file '{mzidFileName}': {ex.Message}");
        }

        // cpp parity: MzIdentMLReader.cpp:47 — default lookUpBy_ to NAME_ID.
        LookUpBy = SpecIdType.NameId;
        _scoreThreshold = 0;
        _isScoreLookup = false;
    }

    // cpp parity: MzIdentMLReader.cpp:43 — `new IdentDataFile(mzidFileName)`. The cpp wrapper
    // handles .gz transparently via boost::iostreams; pwiz-sharp's IdentDataFile takes the raw
    // file stream and expects an already-decompressed XML payload, so we decompress here.
    private static IdentDataFile OpenIdentDataFile(string filename)
    {
        // cpp parity: MzIdentMLReader.h-/cpp- accept .mzid.gz; the pwiz-sharp library does
        // not auto-decompress. We strip the .gz suffix when dispatching by extension so the
        // reader chooses the mzid path.
        bool isGz = filename.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        if (!isGz)
        {
            return new IdentDataFile(filename);
        }

        // .mzid.gz — wrap with GZipStream and synthesise a .mzid-named handle so
        // IdentDataFile dispatches to the mzid reader by extension.
        var unzippedName = filename.Substring(0, filename.Length - 3); // strip ".gz"
        using var fs = File.OpenRead(filename);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        return new IdentDataFile(unzippedName, gz);
    }

    /// <summary>
    /// Implementation of <see cref="BuildParser.ParseFile"/>. Reads the .mzid file, stores
    /// PSMs organised by spectrum file, and imports all spectra.
    /// </summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:61.</remarks>
    public override bool ParseFile()
    {
        var proteins = new Dictionary<DBSequence, Protein>();
        Verbosity.Debug("Reading psms from the file.");
        CollectPsms(proteins);

        // cpp parity: MzIdentMLReader.cpp:67 — register multi-file progress.
        if (_fileMap.Count > 1)
        {
            InitSpecFileProgress(_fileMap.Count);
        }

        // cpp parity: MzIdentMLReader.cpp:71 — build a basename → location lookup so the
        // recorded sourceFile is the full URI / path, not just the bare key.
        var mapSourceFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var sf in _pwizReader.DataCollection.Inputs.SourceFile)
        {
            var location = sf.Location;
            if (string.IsNullOrEmpty(location)) continue;

            // cpp parity: substr between last separator and last dot when both present.
            int dot = location.LastIndexOf('.');
            int slash = Math.Max(location.LastIndexOf('\\'), location.LastIndexOf('/'));
            var key = (dot >= 0 && slash >= 0 && slash < dot)
                ? location.Substring(slash + 1, dot - slash - 1)
                : location;
            mapSourceFiles[key] = location;
        }

        // cpp parity: MzIdentMLReader.cpp:86 — pick the BiblioSpec score type from the
        // analysis type discovered while reading.
        var scoreType = AnalysisToScoreType(_analysisType);

        foreach (var kvp in _fileMap)
        {
            // cpp parity: MzIdentMLReader.cpp:103 — split key on ';' into [location;sourceFileKey].
            var pathParts = kvp.Key.Split(';');
            var specFileroot = BlibUtils.GetFileRoot(pathParts[0]);
            SetSpecFileName(specFileroot, _specExtensions);
            var filename = GetSpecFileName();

            // cpp parity: MzIdentMLReader.cpp:107 — mzXML names use scan-number lookup.
            if (filename.Length >= 6 &&
                filename.EndsWith(".mzXML", StringComparison.OrdinalIgnoreCase))
            {
                LookUpBy = SpecIdType.ScanNumberId;
            }

            // cpp parity: MzIdentMLReader.cpp:111 — translate the bare basename back into the
            // full location stored in <SourceFile>.
            var sourceFile = pathParts.Length > 1 ? pathParts[1] : string.Empty;
            if (mapSourceFiles.TryGetValue(sourceFile, out var fullLoc) && !string.IsNullOrEmpty(fullLoc))
                sourceFile = fullLoc;

            // cpp parity: MzIdentMLReader.cpp:116 — install our PSMs for this file and flush
            // them with buildTables. Score-lookup mode never reaches this branch because the
            // outer EndEarlyException short-circuits collectPsms() first.
            Psms.Clear();
            foreach (var psm in kvp.Value)
                Psms.Add(psm);

            if (!_isScoreLookup)
            {
                if (string.IsNullOrEmpty(sourceFile))
                    BuildTables(scoreType);
                else
                    BuildTables(scoreType, sourceFile);
            }
        }

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// cpp parity: MzIdentMLReader.cpp:128 — score-lookup mode runs the parse once with a
    /// short-circuit exception so we only have to scan as much of the file as it takes to
    /// find a recognisable score CVParam.
    /// </remarks>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        _isScoreLookup = true;
        try
        {
            ParseFile();
        }
        catch (EndEarlyException)
        {
            // cpp parity: caught and swallowed; the analysis type is already set.
        }
        return new List<PsmScoreType> { AnalysisToScoreType(_analysisType) };
    }

    /// <summary>
    /// Walk every <c>SpectrumIdentificationList</c> / <c>SpectrumIdentificationResult</c> /
    /// <c>SpectrumIdentificationItem</c> in the file and accumulate the top-ranked,
    /// non-decoy, threshold-passing PSMs into <see cref="_fileMap"/>.
    /// </summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:146.</remarks>
    private void CollectPsms(Dictionary<DBSequence, Protein> proteins)
    {
        // cpp parity: MzIdentMLReader.cpp:148 — 1 SpectrumIdentificationList = 1 .MGF (or other)
        foreach (var sil in _pwizReader.DataCollection.AnalysisData.SpectrumIdentificationList)
        {
            // 1 SpectrumIdentificationResult = 1 spectrum
            foreach (var result in sil.SpectrumIdentificationResult)
            {
                // cpp parity: MzIdentMLReader.cpp:158 — ProteinPilot mzid points spectraData
                // at SD_1 with a file=<idx> attribute carrying the actual file index.
                if (result.Id.StartsWith("file=", StringComparison.Ordinal))
                {
                    var fileIndex = Id.ValueAs<int>(result.Id, "file") - 1;
                    var spectraDataList = _pwizReader.DataCollection.Inputs.SpectraData;
                    if (fileIndex >= 0 && fileIndex < spectraDataList.Count)
                        result.SpectraDataPtr = spectraDataList[fileIndex];
                }

                var filename = (result.SpectraDataPtr?.Location ?? string.Empty);
                var idStr = result.SpectrumID;
                filename = filename + ";" + GetFilenameFromId(idStr);

                // 1 SpectrumIdentificationItem = 1 psm
                foreach (var item in result.SpectrumIdentificationItem)
                {
                    if (item.PeptideEvidencePtr.Count == 0)
                    {
                        Verbosity.Warn($"{result.Id} does not have any PeptideEvidenceRefs");
                        continue;
                    }

                    // cpp parity: MzIdentMLReader.cpp:183 — only top-ranked PSMs, no decoys.
                    if (item.Rank > 1 || item.PeptideEvidencePtr[0].IsDecoy)
                        continue;

                    // cpp parity: MzIdentMLReader.cpp:188 — pick the score (and side-effect:
                    // sets analysisType_ + scoreThreshold_).
                    double score = GetScore(item);

                    PSM? curPsm;
                    if (!PassThreshold(score))
                    {
                        FilteredOutPsmCount++;
                        curPsm = null;
                    }
                    else
                    {
                        // cpp parity: MzIdentMLReader.cpp:194 — build the PSM.
                        curPsm = new PSM();

                        switch (_analysisType)
                        {
                            case Analysis.Byonic:
                                // cpp parity: MzIdentMLReader.cpp:199 — Byonic ships spec title.
                                curPsm.SpecName = result.CvParam(CVID.MS_spectrum_title).Value;
                                break;
                            case Analysis.Msgf:
                                // cpp parity: MzIdentMLReader.cpp:202 — MSGF+ ships scan numbers.
                                if (result.HasCVParam(CVID.MS_scan_number_s__OBSOLETE))
                                {
                                    curPsm.SpecKey = result.CvParam(CVID.MS_scan_number_s__OBSOLETE).ValueAs<int>();
                                    LookUpBy = SpecIdType.ScanNumberId;
                                }
                                else
                                {
                                    curPsm.SpecName = idStr;
                                }
                                break;
                            default:
                                curPsm.SpecName = idStr;
                                break;
                        }

                        // cpp parity: MzIdentMLReader.cpp:214 — if no scan key set yet, try to
                        // pull it from the SpecName string.
                        if (curPsm.SpecKey < 0)
                            StringToScan(curPsm.SpecName, curPsm);

                        curPsm.Score = score;
                        curPsm.Charge = item.ChargeState;
                        ExtractIonMobility(result, item, curPsm);

                        // cpp parity: MzIdentMLReader.cpp:221 — walk every PeptideEvidence to
                        // accumulate the protein list, and grab a Peptide pointer from any
                        // evidence that has one.
                        var peptidePtr = item.PeptidePtr;
                        foreach (var pe in item.PeptideEvidencePtr)
                        {
                            if (pe.DBSequencePtr is null)
                            {
                                Verbosity.Error(
                                    $"peptideEvidenceRef {pe.Id} has null dbSequenceRef");
                                continue;
                            }
                            var dbSeq = pe.DBSequencePtr;
                            if (peptidePtr is null) peptidePtr = pe.PeptidePtr;

                            if (!proteins.TryGetValue(dbSeq, out var protein))
                            {
                                protein = new Protein(dbSeq.Accession);
                                proteins[dbSeq] = protein;
                            }
                            curPsm.Proteins.Add(protein);
                        }

                        if (peptidePtr != null)
                            ExtractModifications(peptidePtr, curPsm);

                        Verbosity.Comment(VerbosityLevel.Detail,
                            $"For file {filename} adding PSM: scan '{curPsm.SpecName}', " +
                            $"charge {curPsm.Charge}, sequence '{curPsm.UnmodSeq}'.");
                    }

                    // cpp parity: MzIdentMLReader.cpp:246 — append the PSM (or a null
                    // placeholder for filtered-out items) to the file's vector.
                    if (!_fileMap.TryGetValue(filename, out var bucket))
                    {
                        bucket = new List<PSM?>();
                        _fileMap[filename] = bucket;
                    }
                    if (curPsm != null)
                        bucket.Add(curPsm);
                    // If curPsm is null, cpp still inserts an empty entry to register the
                    // filename; we just register it via the bucket creation above. cpp's exact
                    // behaviour was: when curPSM is null AND the file is new, insert an empty
                    // vector. When curPSM is null and the file is already known, push nothing.
                    // We've preserved both branches naturally.
                }
            }
        }
    }

    /// <summary>
    /// Using the modification list on the parsed peptide, populate <see cref="PSM.Mods"/>
    /// and <see cref="PSM.UnmodSeq"/>.
    /// </summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:267.</remarks>
    private static void ExtractModifications(Peptide peptide, PSM psm)
    {
        int itMod = 0;
        int itSubst = 0;

        // cpp parity: MzIdentMLReader.cpp:271 — interleave the two sorted lists by location.
        while (itMod < peptide.Modifications.Count || itSubst < peptide.SubstitutionModifications.Count)
        {
            int location;
            double massDelta;

            if (itMod < peptide.Modifications.Count &&
                (itSubst >= peptide.SubstitutionModifications.Count ||
                 peptide.Modifications[itMod].Location < peptide.SubstitutionModifications[itSubst].Location))
            {
                var mod = peptide.Modifications[itMod++];
                location = mod.Location;
                massDelta = mod.MonoisotopicMassDelta != 0 ? mod.MonoisotopicMassDelta : mod.AvgMassDelta;
            }
            else
            {
                var mod = peptide.SubstitutionModifications[itSubst++];
                location = mod.Location;
                massDelta = mod.MonoisotopicMassDelta != 0 ? mod.MonoisotopicMassDelta : mod.AvgMassDelta;
            }

            // cpp parity: MzIdentMLReader.cpp:286 — N-terminal (location=0) folds to position 1,
            // C-terminal past end folds to last residue.
            location = Math.Max(location, 1);
            location = Math.Min(location, peptide.PeptideSequence.Length);
            psm.Mods.Add(new SeqMod(location, massDelta));
        }

        psm.UnmodSeq = peptide.PeptideSequence;
    }

    /// <summary>
    /// Set <see cref="PSM.IonMobility"/> / <see cref="PSM.IonMobilityType"/> based on which
    /// ion-mobility CV term is present on the result (or on the item, for PEAKS files that
    /// misplace the term).
    /// </summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:296.</remarks>
    private static void ExtractIonMobility(
        SpectrumIdentificationResult result,
        SpectrumIdentificationItem item,
        PSM psm)
    {
        var imParam = result.CvParamChild(CVID.MS_ion_mobility_attribute);
        if (imParam.IsEmpty)
        {
            // cpp parity: MzIdentMLReader.cpp:300 — PEAKS sometimes puts the IM term on the SII.
            imParam = item.CvParamChild(CVID.MS_ion_mobility_attribute);
        }
        if (imParam.IsEmpty) return;

        psm.IonMobility = imParam.ValueAs<double>();
        switch (imParam.Cvid)
        {
            case CVID.MS_ion_mobility_drift_time:
                psm.IonMobilityType = IonMobilityType.DriftTimeMsec;
                break;
            case CVID.MS_inverse_reduced_ion_mobility:
                psm.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
                break;
            case CVID.MS_FAIMS_CV:
                psm.IonMobilityType = IonMobilityType.CompensationV;
                break;
            default:
                Verbosity.Warn($"unsupported ion mobility type: {imParam.Name}");
                break;
        }
    }

    /// <summary>
    /// Look through the CVParams of <paramref name="item"/> and return the score for the
    /// peptide probability. Also sets <see cref="_analysisType"/> and
    /// <see cref="_scoreThreshold"/> as a side effect on the first recognised CV term.
    /// </summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:324.</remarks>
    private double GetScore(SpectrumIdentificationItem item)
    {
        // cpp parity: MzIdentMLReader.cpp:327 — two-pass approach. Primary scores first.
        foreach (var cvParam in item.CVParams)
        {
            switch (cvParam.Cvid)
            {
                case CVID.MS_PeptideShaker_PSM_confidence:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.PeptideShaker);
                        _scoreThreshold = GetScoreThreshold(BuildInput.PeptideShaker);
                    }
                    if (_analysisType == Analysis.PeptideShaker)
                        return cvParam.ValueAs<double>() / 100.0;
                    break;

                case CVID.MS_Scaffold_Peptide_Probability:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.Scaffold);
                        _scoreThreshold = GetScoreThreshold(BuildInput.Scaffold);
                    }
                    if (_analysisType == Analysis.Scaffold)
                        return cvParam.ValueAs<double>();
                    break;

                case CVID.MS_Byonic__Peptide_AbsLogProb:
                case CVID.MS_Byonic__Peptide_AbsLogProb2D:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.Byonic);
                        _scoreThreshold = GetScoreThreshold(BuildInput.Byonic);
                    }
                    if (_analysisType == Analysis.Byonic)
                        return Math.Pow(10, -1 * cvParam.ValueAs<double>());
                    break;

                case CVID.MS_MS_GF_QValue:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.Msgf);
                        _scoreThreshold = GetScoreThreshold(BuildInput.Msgf);
                    }
                    if (_analysisType == Analysis.Msgf)
                        return cvParam.ValueAs<double>();
                    break;

                case CVID.MS_Mascot_expectation_value:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.Mascot);
                        _scoreThreshold = GetScoreThreshold(BuildInput.Mascot);
                    }
                    if (_analysisType == Analysis.Mascot)
                        return cvParam.ValueAs<double>();
                    break;

                case CVID.MS_PEAKS_peptideScore:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.Peaks);
                        _scoreThreshold = GetScoreThreshold(BuildInput.Peaks);
                    }
                    if (_analysisType == Analysis.Peaks)
                        return Math.Pow(10, cvParam.ValueAs<double>() / -10);
                    break;

                case CVID.MS_Paragon_confidence:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.ProtPilot);
                        _scoreThreshold = GetScoreThreshold(BuildInput.ProtPilot);
                    }
                    if (_analysisType == Analysis.ProtPilot)
                        return cvParam.ValueAs<double>();
                    break;

                case CVID.MS_PSM_level_q_value:
                case CVID.MS_percolator_Q_value:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.GenericQvalue);
                        _scoreThreshold = GetScoreThreshold(BuildInput.GenericQValueInput);
                    }
                    if (_analysisType == Analysis.GenericQvalue)
                        return cvParam.ValueAs<double>();
                    break;
            }
        }

        // cpp parity: MzIdentMLReader.cpp:411 — second pass for fallback scores when no
        // primary score is found.
        foreach (var cvParam in item.CVParams)
        {
            switch (cvParam.Cvid)
            {
                case CVID.MS_MS_GF_EValue:
                    if (_analysisType == Analysis.Unknown)
                    {
                        SetAnalysisType(Analysis.Msgf);
                        _scoreThreshold = GetScoreThreshold(BuildInput.Msgf);
                    }
                    if (_analysisType == Analysis.Msgf)
                        return cvParam.ValueAs<double>();
                    break;
            }
        }

        // cpp parity: MzIdentMLReader.cpp:428 — Verbosity::error throws via BlibException.
        Verbosity.Error(".mzid file contains an unsupported score type");
        return 0; // unreachable: Verbosity.Error always throws.
    }

    /// <summary>
    /// Setter that also short-circuits parseFile when running in score-lookup mode.
    /// </summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:432.</remarks>
    private void SetAnalysisType(Analysis analysisType)
    {
        _analysisType = analysisType;
        if (_isScoreLookup)
            throw new EndEarlyException();
    }

    /// <summary>Map an internal analysis type to the corresponding BiblioSpec score type.</summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:439.</remarks>
    private static PsmScoreType AnalysisToScoreType(Analysis analysisType) => analysisType switch
    {
        Analysis.Scaffold => PsmScoreType.ScaffoldSomething,
        Analysis.Byonic => PsmScoreType.ByonicPep,
        Analysis.Msgf => PsmScoreType.MsgfScore,
        Analysis.PeptideShaker => PsmScoreType.PeptideShakerConfidence,
        Analysis.Mascot => PsmScoreType.MascotIonsScore,
        Analysis.Peaks => PsmScoreType.PeaksConfidenceScore,
        Analysis.ProtPilot => PsmScoreType.ProteinPilotConfidence,
        Analysis.GenericQvalue => PsmScoreType.GenericQValue,
        _ => PsmScoreType.UnknownScoreType,
    };

    /// <summary>True if <paramref name="score"/> passes the threshold for the current analysis type.</summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:462.</remarks>
    private bool PassThreshold(double score)
    {
        switch (_analysisType)
        {
            // Scores where lower is better.
            case Analysis.Byonic:
            case Analysis.Mascot:
            case Analysis.Msgf:
            case Analysis.Peaks:
            case Analysis.GenericQvalue:
                return score <= _scoreThreshold;
            // Scores where higher is better.
            case Analysis.Scaffold:
            case Analysis.PeptideShaker:
            case Analysis.ProtPilot:
                return score >= _scoreThreshold;
        }
        // cpp parity: MzIdentMLReader.cpp:478 — Verbosity::error throws BlibException.
        Verbosity.Error("Can't determine cutoff score, unknown analysis type");
        return false; // unreachable: Verbosity.Error always throws.
    }

    /// <summary>
    /// Pull a scan number out of a name string with one of the recognised conventions
    /// (<c>"scan=N ..."</c> or <c>"prefix.NNN.NNN.charge[.dta]"</c>).
    /// </summary>
    /// <remarks>cpp parity: MzIdentMLReader.cpp:482.</remarks>
    private static bool StringToScan(string name, PSM psm)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(psm);

        // cpp parity: split on spaces and look for "scan=" prefix.
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in parts)
        {
            if (token.StartsWith("scan=", StringComparison.Ordinal))
            {
                var raw = token.Substring(5);
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scan))
                {
                    psm.SpecKey = scan;
                    return true;
                }
                // cpp parity: lexical_cast would throw bad_lexical_cast on a non-int token;
                // we fall through to the dotted-format check.
            }
        }

        // cpp parity: check for <scan>.<scan> — same number twice in a dotted name.
        var dotParts = name.Split('.');
        for (int i = 0; i < dotParts.Length - 1; i++)
        {
            if (string.Equals(dotParts[i], dotParts[i + 1], StringComparison.Ordinal) &&
                IsAllDigits(dotParts[i]))
            {
                if (int.TryParse(dotParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var scan))
                {
                    psm.SpecKey = scan;
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsAllDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (var c in s)
            if (c < '0' || c > '9') return false;
        return true;
    }
}
