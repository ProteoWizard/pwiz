// Port of pwiz_tools/BiblioSpec/src/BlibFilter.cpp
//
// Faithful C# port of BiblioSpec::BlibFilter. Reads a redundant .blib library and produces
// a non-redundant .blib: for each peptide+charge group, picks the "best" representative
// spectrum and writes only the survivors to the output library.
//
// "Best" is either:
//   * the spectrum with the highest average normalised-peak dot-product against the other
//     members of its peptide+charge group (default), or
//   * (under -b / useBestScoring) the spectrum with the best search-engine score for its
//     ScoreType, with TIC as a tiebreaker.
//
// Numerical behaviour (peak processing, dot product, scoring) is preserved byte-for-byte
// with the cpp original — comments tag the key parity points with cpp file:line refs.

using System.Data.SQLite;
using System.Globalization;
using pwiz.BiblioSpec.IO;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Filters a redundant <c>.blib</c> library down to one representative spectrum per
/// peptide+charge group, producing a non-redundant <c>.blib</c>.
/// Port of <c>BiblioSpec::BlibFilter</c> at <c>pwiz_tools/BiblioSpec/src/BlibFilter.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="BlibMaker"/> directly — BlibFilter does NOT go through the
/// reader-based ingestion pipeline of <see cref="BlibBuilder"/>. The redundant library is read
/// through a <see cref="BlibReader"/> of its own (a library of any older schema reads through the
/// same calls), its spectra grouped by precursor; one representative per group is chosen and
/// copied, with everything that hangs off it, through a <see cref="BlibCopier"/> into the new
/// library, under its redundant id.
/// </para>
/// <para>
/// CLI parity: cpp BlibFilter uses boost::program_options. The C# port hooks into
/// <see cref="BlibMaker.ParseCommandArgs"/> / <see cref="BlibMaker.ParseNextSwitch"/> so that
/// it can support boost-style positional args: <c>BlibFilter [options] redundant.blib
/// filtered.blib</c>.
/// </para>
/// </remarks>
public class BlibFilter : BlibMaker
{
    // cpp BlibFilter.cpp:124-134 — defaults from the cpp constructor.
    private const string DefaultRedundantDbName = "redundant";

    // cpp parity: BlibFilter.cpp:126 — minPeaks_ default is 20 (the constructor sets 20, but
    // the boost::program_options default at cpp:163 is 1; the program_options default wins).
    private const int DefaultMinPeaks = 1;

    // cpp parity: BlibFilter.cpp:126 — minAverageScore_ default 0.
    private const double DefaultMinAverageScore = 0.0;

    // cpp BlibFilter.cpp:727-729 — peak processor defaults used inside compAndInsert.
    private const int PeakProcessorNumTopPeaks = 100;

    private BlibReader? _redundant;
    private BlibCopier? _copier;
    private string _redundantFileName = string.Empty;
    private int _minPeaks = DefaultMinPeaks;
    private double _minAverageScore = DefaultMinAverageScore;
    private bool _useBestScoring;

    private readonly Dictionary<int, bool> _higherIsBetter = new();

    /// <summary>
    /// The <c>LibMetadata</c> key under which BlibFilter records the command line it was run
    /// with (see <see cref="BlibMaker.RecordCommandLine"/>).
    /// </summary>
    public const string CommandMetadataKey = "bibliospec.blibfilter.command";

    /// <summary>
    /// Constructs a BlibFilter with cpp-default options. cpp BlibFilter.cpp:124-134:
    /// <c>setOverwrite(true)</c> (never append to a non-redundant library) and
    /// <c>setRedundant(false)</c> (the output is non-redundant by definition).
    /// </summary>
    public BlibFilter()
    {
        // cpp parity: BlibFilter.cpp:132-133 — never append to a non-redundant library,
        // and the output library is non-redundant.
        Overwrite = true;
        Redundant = false;
    }

    /// <summary>Path to the redundant input library. Set by <see cref="ParseCommandArgs"/>.</summary>
    public string RedundantFileName
    {
        get => _redundantFileName;
        set => _redundantFileName = value ?? string.Empty;
    }

    /// <summary>
    /// Minimum number of peaks a spectrum must have to be considered. cpp <c>-n</c> /
    /// <c>--min-peaks</c>. Default 1 (matches the boost::program_options default at
    /// BlibFilter.cpp:163).
    /// </summary>
    public int MinPeaks
    {
        get => _minPeaks;
        set => _minPeaks = value;
    }

    /// <summary>
    /// Minimum average dot-product score required to keep a peptide+charge group's best
    /// representative. cpp <c>-s</c> / <c>--min-score</c>. Default 0.
    /// </summary>
    public double MinAverageScore
    {
        get => _minAverageScore;
        set => _minAverageScore = value;
    }

    /// <summary>
    /// When true, pick the spectrum with the best search-engine score (TIC as tiebreaker)
    /// instead of the spectrum with the highest average dot product. cpp <c>-b</c> /
    /// <c>--best-scoring</c>. Default false.
    /// </summary>
    public bool UseBestScoring
    {
        get => _useBestScoring;
        set => _useBestScoring = value;
    }

    // --- BlibMaker overrides --------------------------------------------------------------

    /// <summary>The redundant library, open from <see cref="Init"/> to <see cref="BlibMaker.Commit"/>.</summary>
    private BlibReader RedundantLibrary => _redundant ?? throw new InvalidOperationException("The redundant library is not open. Call Init first.");

    /// <summary>
    /// cpp <c>BlibFilter::attachAll</c> at BlibFilter.cpp:214-223: opens the redundant library
    /// to read from. cpp attached it to the connection the new library was written on; here a
    /// reader of its own reads it, a library of any older schema through the same calls.
    /// </summary>
    protected override void OpenSources()
    {
        if (string.IsNullOrEmpty(_redundantFileName))
            throw new BlibException(false, "Redundant library name has not been set; call ParseCommandArgs or RedundantFileName first.");

        Verbosity.Status($"Filtering redundant library '{_redundantFileName}'.");
        _redundant = new BlibReader(_redundantFileName);
    }

    /// <summary>
    /// cpp <c>BlibFilter::commit</c> at BlibFilter.cpp:225-232: the redundant library is closed
    /// after LibInfo has taken its revision from it, before the writer completes the new one.
    /// </summary>
    protected override void CloseSources()
    {
        _redundant?.Dispose();
        _redundant = null;
    }

    /// <summary>Closes the redundant library if the build stopped short of <see cref="BlibMaker.Commit"/>.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            CloseSources();
        base.Dispose(disposing);
    }

    /// <summary>
    /// cpp <c>BlibFilter::getLSID</c> at BlibFilter.cpp:234-260. Reads the redundant
    /// library's LSID and replaces <c>:redundant:</c> with <c>:nr:</c>.
    /// </summary>
    protected override string GetLsid()
    {
        var libLsid = RedundantLibrary.Info.Lsid;
        if (string.IsNullOrEmpty(libLsid))
            throw new BlibException(false, $"Failed to read libLSID from redundant library '{_redundantFileName}'.");

        const string redundantMarker = ":redundant:";
        var idx = libLsid.IndexOf(redundantMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            throw new BlibException(false, string.Format(
                CultureInfo.InvariantCulture,
                "The library {0} does not appear to be a redundant library.\n'{1}' was not found in LSID '{2}'.",
                _redundantFileName, redundantMarker, libLsid));
        }

        return string.Concat(
            libLsid.AsSpan(0, idx),
            ":nr:".AsSpan(),
            libLsid.AsSpan(idx + redundantMarker.Length));
    }

    /// <summary>
    /// cpp <c>BlibFilter::getNextRevision</c> at BlibFilter.cpp:262-278. Reads majorVersion
    /// from the redundant library directly (no +1 bump, unlike <see cref="BlibMaker.GetNextRevision"/>).
    /// </summary>
    protected override void GetNextRevision(out int dataRev)
    {
        dataRev = RedundantLibrary.Info.Revision;
    }

    /// <summary>
    /// cpp <c>BlibFilter::init</c> at BlibFilter.cpp:286-312. Calls base init (which opens
    /// the writer and creates the standard tables after <see cref="OpenSources"/>), then adds
    /// the RetentionTimes table <see cref="CompAndInsert"/> fills.
    /// </summary>
    public override void Init()
    {
        base.Init();

        Writer.EnsureRetentionTimes();
    }

    // --- main algorithm --------------------------------------------------------------------

    /// <summary>
    /// cpp <c>BlibFilter::buildNonRedundantLib</c> at BlibFilter.cpp:314-643. For each
    /// (peptideModSeq, charge) or (small-molecule, charge) group, select one representative
    /// spectrum and transfer it to the output library along with all members' retention
    /// times.
    /// </summary>
    public void BuildNonRedundantLib()
    {
        Verbosity.Debug("Starting buildNonRedundant.");

        // cpp parity: BlibFilter.cpp:317-370 — if useBestScoring, classify each ScoreType in
        // the redundant library as "higher-is-better" or "lower-is-better". If we encounter
        // an unknown ScoreType that actually appears in RefSpectra, revert to dot-product
        // mode.
        if (_useBestScoring)
        {
            ClassifyScoreTypes();
        }

        Message = "ERROR: Failed building library " + (LibName ?? string.Empty);

        // cpp parity: BlibFilter.cpp:377-379 — the source files first, so the observations can
        // name theirs; the proteins come across with the spectra that map to them.
        var redundant = RedundantLibrary;
        _copier = new BlibCopier(redundant, Writer);
        _copier.CopySourceFiles();

        Verbosity.Debug("Counting Spectra.");
        var progress = new ProgressIndicator(redundant.Info.SpectrumCount > 0 ? redundant.Info.SpectrumCount : redundant.CountSpectra());

        Verbosity.Debug("Sorting spectra by sequence and charge.");
        var bestSpectraIdAndCount = new List<(int Id, int Copies)>();

        var oneIon = new List<RefSpectrum>();
        var lastPepModSeq = string.Empty;
        var lastCharge = 0;
        var lastMoleculeAndAdduct = new SmallMolMetadata();

        // cpp parity: BlibFilter.cpp:444-453 — the spectra of one precursor come together, with
        // the numPeaks filter; precursor-only entries (empty SpecIDinFile) bypass the filter.
        // The peaks come with the rows unless useBestScoring, which does not need them.
        foreach (var record in redundant.ReadSpectraByPrecursor(!_useBestScoring, _minPeaks))
        {
            progress.Increment();

            var tmpRef = ToRefSpectrum(record);
            var pepModSeq = record.PeptideModSeq ?? string.Empty;
            var charge = record.PrecursorCharge;

            // cpp parity: BlibFilter.cpp:567-569 — small molecule IonID is appended to the
            // group key. One of pepModSeq / smallMoleculeIonID is always empty.
            pepModSeq += tmpRef.GetSmallMoleculeIonId();

            // cpp parity: BlibFilter.cpp:573-599 — decode the peaks unless useBestScoring.
            if (!_useBestScoring)
            {
                var peaks = PeaksOf(record);
                if (peaks.Count == 0 && record.NumPeaks != 0)
                {
                    Verbosity.Error(string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to read peaks for redundant library spectrum {0}, sequence {1}, charge {2}.",
                        tmpRef.LibSpecId, tmpRef.DisplayName, tmpRef.Charge));
                }
                tmpRef.SetRawPeaks(peaks);
            }

            // cpp parity: BlibFilter.cpp:603-624 — group test: same peptide+charge (or
            // same molecule+adduct for small molecules) means add to the current group;
            // otherwise flush the current group and start a new one.
            bool sameGroup;
            if (!string.IsNullOrEmpty(pepModSeq))
                sameGroup = pepModSeq == lastPepModSeq && lastCharge == charge;
            else
                sameGroup = tmpRef.SmallMolMetadata.Equals(lastMoleculeAndAdduct);

            if (sameGroup)
            {
                oneIon.Add(tmpRef);
            }
            else
            {
                if (oneIon.Count > 0)
                {
                    Verbosity.Comment(VerbosityLevel.Detail, string.Format(
                        CultureInfo.InvariantCulture,
                        "Selecting spec for {0}, charge {1} from {2} spectra.",
                        string.IsNullOrEmpty(lastPepModSeq) ? lastMoleculeAndAdduct.MoleculeName : lastPepModSeq,
                        lastCharge, oneIon.Count));
                    CompAndInsert(oneIon, bestSpectraIdAndCount);
                    oneIon.Clear();
                }

                oneIon.Add(tmpRef);
                lastPepModSeq = pepModSeq;
                lastCharge = charge;
                // cpp parity: BlibFilter.cpp:620 — copy SmallMolMetadata for the new group.
                var src = tmpRef.SmallMolMetadata;
                lastMoleculeAndAdduct = new SmallMolMetadata
                {
                    MoleculeName = src.MoleculeName,
                    ChemicalFormula = src.ChemicalFormula,
                    PrecursorAdduct = src.PrecursorAdduct,
                    InchiKey = src.InchiKey,
                    OtherKeys = src.OtherKeys,
                    PrecursorMzDeclared = src.PrecursorMzDeclared,
                };

                Verbosity.Comment(VerbosityLevel.Detail, string.Format(
                    CultureInfo.InvariantCulture,
                    "Collecting spec for {0}, charge {1},",
                    tmpRef.DisplayName, charge));
            }
        }

        // cpp parity: BlibFilter.cpp:630-636 — flush the trailing group.
        if (oneIon.Count > 0)
        {
            progress.Increment();
            Verbosity.Comment(VerbosityLevel.Detail, string.Format(
                CultureInfo.InvariantCulture,
                "Selecting spec for {0}, charge {1} from {2} spectra.",
                oneIon[0].DisplayName, lastCharge, oneIon.Count));
            CompAndInsert(oneIon, bestSpectraIdAndCount);
            oneIon.Clear();
        }

        // cpp parity: BlibFilter.cpp:639 (transferSpectra) — the chosen spectra, in id order and
        // under their redundant ids, which the retention times written above refer to, each with
        // its peaks, modifications, annotations and proteins.
        bestSpectraIdAndCount.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var (id, copies) in bestSpectraIdAndCount)
        {
            if (_copier.CopySpectrum(id, copies, keepId: true) == null)
                throw new BlibException(false, $"Spectrum {id.ToString(CultureInfo.InvariantCulture)} could not be read back from the redundant library.");
        }

        // cpp parity: BlibFilter.cpp:642 — clean up the progress bar.
        progress.Finish();
        progress.Complete();
    }

    /// <summary>
    /// The <see cref="RefSpectrum"/> of a redundant library row, as cpp hydrated it
    /// (BlibFilter.cpp:510-565): a value the library lacks is 0, the flanking residues are
    /// unknown, and a non-integer <c>SpecIDinFile</c> is scan 0.
    /// </summary>
    private static RefSpectrum ToRefSpectrum(BlibSpectrumRecord record)
    {
        var tmpRef = new RefSpectrum
        {
            LibSpecId = checked((int)record.Id),
            FileId = record.FileId,
            Sequence = record.PeptideSeq ?? string.Empty,
            Mz = record.PrecursorMz,
            Charge = record.PrecursorCharge,
            ModifiedSequence = record.PeptideModSeq ?? string.Empty,
            PrevAa = "-",
            NextAa = "-",
            Score = record.Score,
            ScoreType = record.ScoreType,
            ScanNumber = ScanNumberOf(record.SpecIdInFile),
            MoleculeName = record.MoleculeName ?? string.Empty,
            ChemicalFormula = record.ChemicalFormula ?? string.Empty,
            Adduct = record.PrecursorAdduct ?? string.Empty,
            InchiKey = record.InchiKey ?? string.Empty,
            OtherKeys = record.OtherKeys ?? string.Empty,
            CollisionalCrossSection = record.CollisionalCrossSectionSqA ?? 0,
            IonMobilityHighEnergyOffset = record.IonMobilityHighEnergyOffset ?? 0,
            RetentionTime = record.RetentionTime ?? 0,
            TotalIonCurrentRaw = record.TotalIonCurrent ?? 0,
        };
        tmpRef.SetIonMobility(record.IonMobility ?? 0, (IonMobilityType)record.IonMobilityType);
        // cpp parity: BuildParser.cpp:608 — the start and end times are a pair.
        if (record.StartTime.HasValue && record.EndTime.HasValue)
        {
            tmpRef.StartTime = record.StartTime.Value;
            tmpRef.EndTime = record.EndTime.Value;
        }
        return tmpRef;
    }

    /// <summary>
    /// cpp <c>BlibFilter::getUncompressedPeaks</c> at BlibFilter.cpp:645-691: the spectrum's
    /// peaks decoded from its blobs, or none when the blobs are missing, short, or hold a
    /// corrupted peak (cpp parity: BlibFilter.cpp:675-680, which the caller reports).
    /// </summary>
    private static List<PeakT> PeaksOf(BlibSpectrumRecord record)
    {
        if (record.NumPeaks <= 0 || record.MzBlob == null || record.IntensityBlob == null)
            return new List<PeakT>();

        BlibPeaks decoded;
        try
        {
            decoded = BlibReader.DecodePeaks(record);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            Verbosity.Warn($"Could not decode the peaks of spectrum {record.Id.ToString(CultureInfo.InvariantCulture)}: {ex.Message}");
            return new List<PeakT>();
        }

        var peaks = new List<PeakT>(decoded.Mzs.Length);
        for (var i = 0; i < decoded.Mzs.Length; i++)
        {
            var mz = decoded.Mzs[i];
            var intensity = decoded.Intensities[i];
            if (double.IsNaN(mz) || mz < 0 || mz > 100000 ||
                float.IsNaN(intensity) || intensity < 0)
            {
                return new List<PeakT>();
            }
            peaks.Add(new PeakT(mz, intensity));
        }
        return peaks;
    }

    /// <summary>
    /// cpp parity: BlibFilter.cpp:564 — <c>sqlite3_column_int</c> read a non-integer
    /// <c>SpecIDinFile</c> as 0.
    /// </summary>
    private static int ScanNumberOf(string? specIdInFile)
    {
        return int.TryParse(specIdInFile, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scan) ? scan : 0;
    }

    /// <summary>
    /// cpp <c>BlibFilter::buildNonRedundantLib</c> at BlibFilter.cpp:317-370 — classify the
    /// known ScoreType values into <c>higherIsBetter</c> / <c>lowerIsBetter</c> buckets. If
    /// we encounter an unknown ScoreType that is actually present in RefSpectra, fall back
    /// to dot-product mode by setting <see cref="_useBestScoring"/> back to false.
    /// </summary>
    private void ClassifyScoreTypes()
    {
        // The score types the new library seeds (BlibSchema.ScoreTypes, the id its index), and
        // the ones the redundant library's spectra actually carry.
        var inUse = new HashSet<int>(RedundantLibrary.ReadScoreTypesInUse());
        for (var scoreTypeId = 0; scoreTypeId < BlibSchema.ScoreTypes.Length; scoreTypeId++)
        {
            var scoreTypeName = BlibSchema.ScoreTypes[scoreTypeId].ScoreType;
            // cpp parity: BlibFilter.cpp:330-352 — explicit higher/lower-is-better lists.
            // cpp comment at line 336: SEQUEST XCORR is actually the associated q-value here.
            switch (scoreTypeName)
            {
                case "PERCOLATOR QVALUE":
                case "IDPICKER FDR":
                case "MASCOT IONS SCORE":
                case "TANDEM EXPECTATION VALUE":
                case "OMSSA EXPECTATION SCORE":
                case "PROTEIN PROSPECTOR EXPECTATION SCORE":
                case "SEQUEST XCORR":
                case "MAXQUANT SCORE":
                case "MORPHEUS SCORE":
                case "MSGF+ SCORE":
                case "PEAKS CONFIDENCE SCORE":
                case "BYONIC SCORE":
                case "GENERIC Q-VALUE":
                    _higherIsBetter[scoreTypeId] = false;
                    break;
                case "SPECTRUM MILL":
                case "WATERS MSE PEPTIDE SCORE":
                case "PEPTIDE PROPHET SOMETHING":
                case "PROTEIN PILOT CONFIDENCE":
                case "SCAFFOLD SOMETHING":
                case "HARDKLOR IDOTP":
                case "PEPTIDE SHAKER CONFIDENCE":
                    _higherIsBetter[scoreTypeId] = true;
                    break;
                default:
                    Verbosity.Warn($"Don't know if higher or lower is better: {scoreTypeName}");
                    if (inUse.Contains(scoreTypeId))
                    {
                        // cpp parity: BlibFilter.cpp:360-364 — bail out of best-scoring mode if
                        // a spectrum with an unknown score type would actually be selected.
                        Verbosity.Warn("Cannot filter by score, reverting to normal behavior");
                        _useBestScoring = false;
                        return;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// cpp <c>BlibFilter::compAndInsert</c> at BlibFilter.cpp:707-875. Given a list of
    /// RefSpectrum with the same peptide+charge, pick the best representative and append
    /// its id (+ group size) to <paramref name="bestSpectraIdAndCount"/>. Also inserts a
    /// row into <c>RetentionTimes</c> for every member of the group.
    /// </summary>
    /// <remarks>
    /// Selection rules (cpp parity):
    /// <list type="bullet">
    /// <item>1 spectrum — pick it.</item>
    /// <item>2 spectra (default mode) — pick the one with more peaks; ties go to index 0.</item>
    /// <item>3+ spectra (default mode) — process peaks, compute pairwise dot products,
    ///   pick the spectrum with the highest sum of dot products (== highest average since
    ///   the divisor is the same for all). If the average is &lt; <see cref="MinAverageScore"/>,
    ///   drop the entire group.</item>
    /// <item>useBestScoring mode — group by ScoreType, find best score in each group
    ///   (direction per <see cref="_higherIsBetter"/>); if multiple possible winners,
    ///   highest TIC wins.</item>
    /// </list>
    /// </remarks>
    private void CompAndInsert(List<RefSpectrum> oneIon, List<(int Id, int Copies)> bestSpectraIdAndCount)
    {
        var numSpec = oneIon.Count;
        var specId = 0;
        var bestIndex = 0;

        if (!_useBestScoring)
        {
            if (numSpec == 1)
            {
                bestSpectraIdAndCount.Add((oneIon[0].LibSpecId, 1));
            }
            else if (numSpec == 2)
            {
                // cpp parity: BlibFilter.cpp:719 — in the future, pick the one with the best
                // search score. For now, pick the one with more peaks.
                if (oneIon[0].NumRawPeaks < oneIon[1].NumRawPeaks)
                    bestIndex = 1;
                bestSpectraIdAndCount.Add((oneIon[bestIndex].LibSpecId, 2));
            }
            else
            {
                // cpp parity: BlibFilter.cpp:723-776 — process peaks then compute all-by-all
                // dot products.

                // preprocess all RefSpectrum in oneIon (cpp BlibFilter.cpp:727-736).
                // cpp defaults: clearPrecursor=true, numTopPeaks=100, binSize=1, binOffset=0.
                var proc = new PeakProcessor
                {
                    ClearPrecursor = true,
                    NumTopPeaks = PeakProcessorNumTopPeaks,
                };
                foreach (var rs in oneIon)
                    proc.ProcessPeaks(rs);

                // create an array where we'll sum scores for each spectrum, init to 0.
                var scores = new double[oneIon.Count];

                // cpp parity: BlibFilter.cpp:743-758 — upper-triangular pairwise dot product.
                // cpp builds a Match(tmpRef1, tmpRef2) and calls DotProduct::compare; the C#
                // Match takes (Spectrum, RefSpectrum), so cast the first arg accordingly.
                for (var i = 0; i < oneIon.Count; i++)
                {
                    var refI = oneIon[i];
                    for (var j = i + 1; j < oneIon.Count; j++)
                    {
                        var refJ = oneIon[j];
                        var thisMatch = new Match(refI, refJ);
                        DotProduct.Compare(thisMatch);
                        var dotProduct = thisMatch.GetScore(MatchScoreType.Dotp);
                        scores[i] += dotProduct;
                        scores[j] += dotProduct;
                    }
                }

                // cpp parity: BlibFilter.cpp:761 — find the index of the maximum element. cpp
                // getMaxElementIndex returns the FIRST max in tie cases. Ties show up routinely
                // when redundant spectra come from the same source replicate; relying on the
                // float-precision asymmetry inside DotProduct.Compare (see comment there) is
                // what makes cpp and C# pick the same id when several spectra are equally good.
                bestIndex = GetMaxElementIndex(scores);
                var bestScore = scores[bestIndex];
                var bestAverageScore = bestScore / oneIon.Count;

                // cpp parity: BlibFilter.cpp:766-775 — drop the entire group if the best
                // average score is too low.
                if (bestAverageScore >= _minAverageScore)
                {
                    bestSpectraIdAndCount.Add((oneIon[bestIndex].LibSpecId, oneIon.Count));
                }
                else
                {
                    Verbosity.Warn(string.Format(
                        CultureInfo.InvariantCulture,
                        "Best score is {0} for {1}, charge {2} after comparing {3} spectra.  This sequence will not be included in the filtered library.",
                        bestAverageScore, oneIon[0].Sequence, oneIon[0].Charge, oneIon.Count));
                    return;
                }
            }
        }
        else
        {
            // cpp parity: BlibFilter.cpp:777-835 — useBestScoring mode.
            if (numSpec == 1)
            {
                bestSpectraIdAndCount.Add((oneIon[0].LibSpecId, 1));
            }
            else
            {
                var indices = new Dictionary<RefSpectrum, int>(ReferenceEqualityComparer.Instance);
                var groups = GroupByScoreType(oneIon, indices);
                RefSpectrum? winner = null;

                var possibleWinners = new List<RefSpectrum>();
                foreach (var kv in groups)
                {
                    if (!_higherIsBetter.TryGetValue(kv.Key, out var higherIsBetter))
                    {
                        Verbosity.Error(string.Format(
                            CultureInfo.InvariantCulture,
                            "Don't know if higher or lower is better for score type {0}",
                            kv.Key));
                    }
                    var bestScores = GetBestScores(kv.Value, higherIsBetter);
                    possibleWinners.AddRange(bestScores);
                }

                if (possibleWinners.Count == 1)
                {
                    winner = possibleWinners[0];
                }
                else
                {
                    // cpp parity: BlibFilter.cpp:799-806 — find highest TIC to break the tie.
                    var winningValue = -1.0;
                    foreach (var rs in possibleWinners)
                    {
                        var specValue = rs.TotalIonCurrentRaw;
                        if (specValue > winningValue)
                        {
                            winner = rs;
                            winningValue = specValue;
                        }
                    }
                }

                if (winner is null)
                {
                    // Defensive — cpp asserts via the algorithm above that winner != NULL when
                    // numSpec > 1 and groups is non-empty; we throw rather than null-deref.
                    throw new BlibException(false, "BlibFilter: no winner could be selected from best-scoring group.");
                }

                bestSpectraIdAndCount.Add((winner.LibSpecId, oneIon.Count));
                bestIndex = indices[winner];
            }
        }

        if (bestSpectraIdAndCount.Count > 0)
            specId = bestSpectraIdAndCount[^1].Id;

        // cpp parity: BlibFilter.cpp:841-874 — insert one RetentionTimes row per group member.
        for (var i = 0; i < numSpec; i++)
        {
            var spectrum = oneIon[i];
            var specIdRedundant = spectrum.LibSpecId;
            var retentionTime = spectrum.RetentionTime;
            var startTime = spectrum.StartTime;
            var endTime = spectrum.EndTime;

            // cpp parity: a zero retention time is unknown, and the start and end times are
            // kept only as a pair.
            var hasBounds = startTime != 0 && endTime != 0;
            try
            {
                Writer.AddRetentionTime(specId, _copier!.TargetFileId(spectrum.FileId),
                    retentionTime != 0 ? retentionTime : null,
                    hasBounds ? startTime : null,
                    hasBounds ? endTime : null,
                    spectrum.Score, i == bestIndex,
                    spectrum.IonMobility, spectrum.CollisionalCrossSection, spectrum.IonMobilityHighEnergyOffset,
                    (int)spectrum.IonMobilityType, specIdRedundant);
            }
            catch (SQLiteException ex)
            {
                throw new BlibException(false, "Error inserting row into RetentionTimes: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// cpp <c>BlibFilter::groupByScoreType</c> at BlibFilter.cpp:877-891. Builds a
    /// <c>scoreType → spectra</c> map; <paramref name="outIndices"/> records each
    /// spectrum's position within the input list (used to find <c>bestIndex</c>).
    /// </summary>
    private static Dictionary<int, List<RefSpectrum>> GroupByScoreType(
        List<RefSpectrum> oneIon, Dictionary<RefSpectrum, int> outIndices)
    {
        var groups = new Dictionary<int, List<RefSpectrum>>();
        var index = -1;
        foreach (var rs in oneIon)
        {
            var scoreType = rs.ScoreType;
            if (!groups.TryGetValue(scoreType, out var list))
            {
                list = new List<RefSpectrum>();
                groups[scoreType] = list;
            }
            list.Add(rs);
            outIndices[rs] = ++index;
        }
        return groups;
    }

    /// <summary>
    /// cpp <c>BlibFilter::getBestScores</c> at BlibFilter.cpp:893-916. Return the subset
    /// of <paramref name="group"/> tied for the best score; "best" means greatest when
    /// <paramref name="higherIsBetter"/> is true, otherwise least.
    /// </summary>
    private static List<RefSpectrum> GetBestScores(List<RefSpectrum> group, bool higherIsBetter)
    {
        var best = new List<RefSpectrum>();
        if (group.Count == 0)
            return best;

        best.Add(group[0]);
        if (group.Count == 1)
            return best;

        var bestScore = best[0].Score;

        for (var i = 1; i < group.Count; i++)
        {
            var score = group[i].Score;
            if ((!higherIsBetter && score < bestScore) ||
                (higherIsBetter && score > bestScore))
            {
                best.Clear();
                best.Add(group[i]);
                bestScore = score;
            }
            else if (score == bestScore)
            {
                best.Add(group[i]);
            }
        }
        return best;
    }

    /// <summary>
    /// cpp <c>getMaxElementIndex</c>. Returns the index of the first occurrence of the
    /// maximum value in <paramref name="values"/>; assumes non-empty input.
    /// </summary>
    private static int GetMaxElementIndex(double[] values)
    {
        var bestIdx = 0;
        var bestVal = values[0];
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > bestVal)
            {
                bestVal = values[i];
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    // --- CLI parsing -----------------------------------------------------------------------

    /// <summary>
    /// cpp <c>BlibFilter</c> uses boost::program_options for CLI parsing (BlibFilter.cpp:149).
    /// The C# port hooks into the BlibMaker framework instead: consume short switches via
    /// <see cref="ParseNextSwitch"/>, then take the second-to-last and last positional args
    /// as the redundant + filtered library paths (cpp argNames at BlibFilter.cpp:177-179).
    /// </summary>
    public override int ParseCommandArgs(string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);

        // cpp parity: no positional args at all → print usage and exit 1.
        if (argv.Length == 0)
        {
            Usage();
            // Defensive throw — Usage() exits, but if a test override doesn't, we need to
            // bail before the lib-name logic below.
            throw new BlibException(false, "Missing required arguments.");
        }

        // cpp parity: support `-h` / `--help` even though boost::program_options provides it
        // automatically (BiblioSpec::CommandLine wires it in). We treat any `-h` switch as
        // "print usage + exit 1".
        foreach (var a in argv)
        {
            if (a == "-h" || a == "--help")
            {
                Usage();
                throw new BlibException(false, "Help requested.");
            }
        }

        // Consume short options. Unlike BlibBuilder, BlibFilter requires TWO positional args
        // at the end (redundant + filtered), so we override the base logic.
        var i = 0;
        while (i < argv.Length)
        {
            var arg = argv[i];
            if (arg.Length != 2 || arg[0] != '-')
                break;
            i = ParseNextSwitch(i, argv);
        }

        // cpp parity: BlibFilter.cpp:177-189 — two positional args required.
        var remaining = argv.Length - i;
        if (remaining < 2)
        {
            Usage();
            throw new BlibException(false, "Missing required arguments: redundant-library filtered-library.");
        }

        // Penultimate arg = redundant input; last arg = filtered output.
        var redundant = argv[argv.Length - 2];
        var filtered = argv[argv.Length - 1];
        VerifyFileExists(redundant);
        _redundantFileName = redundant;
        SetLibName(filtered);

        return i;
    }

    /// <summary>
    /// cpp BlibFilter switches (BlibFilter.cpp:157-174) translated into the BlibMaker
    /// short-switch loop.
    /// </summary>
    protected override int ParseNextSwitch(int i, string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        if (i < 0 || i >= argv.Length) throw new ArgumentOutOfRangeException(nameof(i));

        var arg = argv[i];
        var switchName = arg[1];

        switch (switchName)
        {
            case 'm':
                // cpp parity: BlibFilter.cpp:158-160 — `-m <megabytes>` SQLite cache size.
                // Default 250M. This shadows BlibMaker's identically-named -m switch (which
                // does the same thing) and overrides for parity.
                if (++i < argv.Length)
                {
                    if (!int.TryParse(argv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) || mb <= 0)
                        throw new BlibException(false, "Invalid -m cache size specified.");
                    CacheSizeMb = mb;
                }
                break;

            case 'n':
                // cpp parity: BlibFilter.cpp:162-164 — `-n <min-peaks>` filter. Default 1.
                if (++i < argv.Length)
                {
                    if (!int.TryParse(argv[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 0)
                        throw new BlibException(false, "Invalid -n min-peaks value specified.");
                    _minPeaks = n;
                }
                break;

            case 's':
                // cpp parity: BlibFilter.cpp:166-168 — `-s <min-score>`. Default 0.
                if (++i < argv.Length)
                {
                    if (!double.TryParse(argv[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                        throw new BlibException(false, "Invalid -s min-score value specified.");
                    _minAverageScore = s;
                }
                break;

            case 'b':
                // cpp parity: BlibFilter.cpp:170-172 — `-b` (with optional bool arg) toggles
                // best-scoring mode. boost::program_options treats `-b` as `-b true`; here
                // we accept an optional argument that parses as a bool, or default to true.
                if (i + 1 < argv.Length && bool.TryParse(argv[i + 1], out var b))
                {
                    _useBestScoring = b;
                    i++;
                }
                else
                {
                    _useBestScoring = true;
                }
                break;

            default:
                // Fall through to the BlibMaker base implementation for -v, -a, -i, -d, -t, -z.
                // -m is handled above (it overrides the base's -m, but they do the same thing).
                return base.ParseNextSwitch(i, argv);
        }

        return Math.Min(argv.Length, i + 1);
    }

    /// <summary>
    /// cpp <c>BlibFilter</c> has no explicit usage() override; the boost::program_options
    /// CommandLine class generates the help text from the registered options. The C# port
    /// emits the same text by hand for parity.
    /// </summary>
    public override void Usage()
    {
        var usage =
            "Usage: BlibFilter [options] <redundant-library> <filtered-library>\n" +
            "Options:\n" +
            "   -m <megabytes>    SQLite memory cache size in megabytes. Default 250M.\n" +
            "   -n <min-peaks>    Only include spectra with at least this many peaks. Default 1.\n" +
            "   -s <min-score>    Best spectrum must have at least this average score to be included. Default 0.\n" +
            "   -b [<bool>]       Select only the spectrum with the best score for each peptide\n" +
            "                     (spectrum TIC is used as tiebreaker). Default false.\n" +
            "   -v                Verbose output (cpp BlibMaker base option).\n" +
            "   -a <authority>    LSID authority. Default proteome.gs.washington.edu.\n" +
            "   -i <library_id>   LSID library ID. Default uses file name.\n";
        Console.Error.WriteLine(usage);
        Environment.Exit(1);
    }
}
