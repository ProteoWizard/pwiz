// Port of pwiz_tools/BiblioSpec/src/SQTreader.{h,cpp}
//
// Reader for SQT files (SEQUEST output, also produced by Comet and optionally post-processed
// by Percolator). The file is plain-text; lines starting with H are header / metadata, S
// starts a spectrum block, M is a candidate match (top-ranked first), L is the protein-list
// for the previous M. Modifications are encoded with marker characters (*, #, @, ^, $, %)
// after the modified residue; the per-character mass shift comes from DiffMod lines in the
// header, and a StaticMod table provides per-residue static modifications.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// BiblioSpec reader for SEQUEST .sqt files (including Comet-produced and Percolator-processed
/// variants). Inherits from <see cref="BuildParser"/>; <see cref="ParseFile"/> reads the file
/// twice — first to detect the version + modification table from the header, then to extract
/// top-ranked PSMs.
/// </summary>
/// <remarks>
/// Port of <c>BiblioSpec::SQTreader</c> (SQTreader.h:45). The cpp implementation uses
/// <c>std::ifstream::peek()</c> for line-prefix lookahead; the C# port reads the file
/// line-by-line into memory (SQT files are small enough that this is fine) and indexes
/// through them — simpler and avoids the awkwardness of a peeking <c>StreamReader</c>.
/// </remarks>
public class SQTreader : BuildParser
{
    /// <summary>Max size of the static / diff mod arrays. cpp parity: SQTreader.h:38 <c>MAX_MODS</c>.</summary>
    private const int MaxMods = 128;

    // cpp parity: SQTreader.cpp:46-52 — regex for Comet "H CometParams add_X_<name> = <mass>" lines.
    // boost::xpressive [[:blank:]] is space+tab; .NET regex \s would include newlines, so we hand-write [ \t].
    private static readonly Regex _cometModRegex = new(
        @"^H[ \t]+CometParams[ \t]+add_(?<aa>[A-Z])_[^\s]+[ \t]*=[ \t]*(?<modMass>(\d*[.])?\d+)[ \t]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // cpp parity: SQTreader.h:64-65 — per-character mod tables (0 if unset).
    private readonly double[] _staticMods = new double[MaxMods];
    private readonly double[] _diffMods = new double[MaxMods];

    // cpp parity: SQTreader.h:68 — monoisotopic mass table for residues.
    // Note the cpp ctor passes `0` to initializeMass which means AVERAGE; preserved for parity.
    private readonly double[] _masses = AminoAcidMasses.BuildMassTable(monoisotopic: false);

    // cpp parity: SQTreader.h:66 — set by openRead when a "Percolator" header line is seen.
    private bool _percolated;

    // cpp parity: SQTreader.h:67 — detected SQT dialect (defaults to SequestVersion("2.7")).
    private SQTversion _sqtVersion;

    // All lines from the file, read once in openRead and indexed by the parse loop.
    // cpp parity: stand-in for the cpp std::ifstream + peek() pattern.
    private List<string> _lines = new();

    /// <summary>
    /// Returns true if <paramref name="path"/> is a SEQUEST <c>.sqt</c> file (also covers
    /// Comet's <c>.sqt</c> dialect — version detection is done from header lines at parse
    /// time). Used by <see cref="BlibBuilder"/>'s reader-factory dispatch — each reader
    /// declares its own accepted extensions in one place.
    /// </summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".sqt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a reader for the given SQT file.
    /// </summary>
    /// <param name="maker">The library being built.</param>
    /// <param name="sqtFilename">Path to the .sqt file.</param>
    /// <param name="parentProgress">Optional caller progress indicator.</param>
    /// <remarks>cpp parity: SQTreader.cpp:34.</remarks>
    public SQTreader(BlibBuilder maker, string sqtFilename, ProgressIndicator? parentProgress)
        : base(maker, sqtFilename, parentProgress)
    {
        // cpp parity: SQTreader.cpp:44 — initial assumption is SEQUEST 2.7; openRead replaces
        // this with a CometVersion if it detects a "H CometVersion ..." header.
        _sqtVersion = new SequestVersion("2.7");
    }

    /// <summary>
    /// Implementation of <see cref="BuildParser.ParseFile"/>. Opens the SQT, reads the header,
    /// then walks the spectrum blocks to extract top-ranked PSMs.
    /// </summary>
    /// <remarks>cpp parity: SQTreader.cpp:197.</remarks>
    public override bool ParseFile()
    {
        // cpp parity: SQTreader.cpp:198.
        OpenRead(warnIfNotPercolated: true);

        // cpp parity: SQTreader.cpp:200-206 — register the matching .ms2 / .cms2 / .bms2 / .pms2.
        var extensions = new List<string> { ".ms2", ".cms2", ".bms2", ".pms2" };
        var fileroot = BlibUtils.GetFileRoot(GetFileName());
        SetSpecFileName(fileroot, extensions);

        // cpp parity: SQTreader.cpp:209.
        ExtractPsms();

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>cpp parity: SQTreader.cpp:218 — calls openRead(false) (no warning) then returns
    /// PERCOLATOR_QVALUE if percolated, else SEQUEST_XCORR.</remarks>
    public override IList<PsmScoreType> GetScoreTypes()
    {
        OpenRead(warnIfNotPercolated: false);
        return new List<PsmScoreType>
        {
            _percolated ? PsmScoreType.PercolatorQValue : PsmScoreType.SequestXCorr,
        };
    }

    /// <summary>
    /// Open the SQT file and read through its header section, populating the static-mod /
    /// diff-mod tables and detecting the SQT dialect + version. Leaves the in-memory line
    /// buffer positioned at the first non-header line (the first 'S' line).
    /// </summary>
    /// <param name="warnIfNotPercolated">If true and no Percolator marker is found in the header,
    /// emit a <see cref="Verbosity.Status(string)"/> noting that filtering will use XCorr.</param>
    /// <remarks>cpp parity: SQTreader.cpp:68.</remarks>
    public void OpenRead(bool warnIfNotPercolated)
    {
        var filename = GetFileName();
        try
        {
            _lines = new List<string>(File.ReadAllLines(filename));
        }
        catch (IOException ex)
        {
            // cpp parity: SQTreader.cpp:75 — BlibException with hasFilename=true.
            throw new BlibException(true, $"Couldn't open '{filename}'. {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new BlibException(true, $"Couldn't open '{filename}'. {ex.Message}");
        }

        // Reset header-driven state in case OpenRead is invoked twice (cpp does the same:
        // closes the stream at the top of openRead and re-reads from the start).
        Array.Clear(_staticMods);
        Array.Clear(_diffMods);
        _percolated = false;
        _sqtVersion = new SequestVersion("2.7");

        // cpp parity: SQTreader.cpp:87 — while(file.peek() == 'H'). We consume header lines
        // off the front of the list so the rest of the parser sees only spectrum blocks.
        var headerEnd = 0;
        while (headerEnd < _lines.Count && _lines[headerEnd].Length > 0 && _lines[headerEnd][0] == 'H')
        {
            var buffer = _lines[headerEnd];
            headerEnd++;

            // cpp parity: SQTreader.cpp:92 — Sequest version detection.
            if (string.Equals(_sqtVersion.GeneratorName, "Sequest", StringComparison.Ordinal)
                && buffer.Contains("SQTGeneratorVersion", StringComparison.Ordinal))
            {
                var numPos = IndexOfFirstDigit(buffer);
                if (numPos < 0)
                {
                    throw new BlibException(false,
                        $"Could not get the SEQUEST version from this line in the header: {buffer}.");
                }
                var versionStr = buffer.Substring(numPos);
                if (!_sqtVersion.TrySetVersion(versionStr))
                {
                    throw new BlibException(false,
                        $"Could not get the SEQUEST version from this line in the header: {buffer}.");
                }
            }

            // cpp parity: SQTreader.cpp:111 — Comet version detection (replaces SEQUEST default).
            if (buffer.Contains("CometVersion", StringComparison.Ordinal))
            {
                _sqtVersion = new CometVersion();
                var numPos = IndexOfFirstDigit(buffer);
                if (numPos < 0)
                {
                    throw new BlibException(false,
                        $"Could not get the Comet version from this line in the header: {buffer}.");
                }
                var versionStr = buffer.Substring(numPos);
                if (!_sqtVersion.TrySetVersion(versionStr))
                {
                    throw new BlibException(false,
                        $"Could not get the Comet version from this line in the header: {buffer}.");
                }
            }

            // cpp parity: SQTreader.cpp:133 — "StaticMod" line, e.g. "H StaticMod C=160.165".
            // The cpp uses sscanf with "%*c %*s %s" then parses "<letter>=<float>".
            if (buffer.Contains("StaticMod", StringComparison.Ordinal))
            {
                if (TryParseStaticModLine(buffer, out var modLetter, out var modValue))
                {
                    // cpp parity: SQTreader.cpp:142 — Comet and SEQUEST > 2.7 report
                    // residue+mod; subtract the residue mass to get the mod delta.
                    if (string.Equals(_sqtVersion.GeneratorName, "Comet", StringComparison.Ordinal)
                        || (string.Equals(_sqtVersion.GeneratorName, "Sequest", StringComparison.Ordinal)
                            && _sqtVersion > new SequestVersion("2.7")))
                    {
                        var residue = modLetter.ToString();
                        // cpp parity: SQTreader.cpp:145 — casts residueMass to float before subtraction.
                        var residueMass = (float)BlibUtils.GetPeptideMass(residue, _masses);
                        modValue -= residueMass;
                    }
                    if (modLetter < MaxMods)
                        _staticMods[modLetter] = modValue;
                }
            }

            // cpp parity: SQTreader.cpp:151 — CometParams "add_X_<name> = <mass>" overrides StaticMod
            // because it reports the modification mass directly (not residue + mass).
            var cometMatch = _cometModRegex.Match(buffer);
            if (cometMatch.Success)
            {
                if (cometMatch.Groups["aa"].Length != 1 || cometMatch.Groups["modMass"].Length <= 0)
                {
                    throw new BlibException(false,
                        $"Could not extract the staticMod from this line in the header: {buffer}");
                }
                var modLetter = cometMatch.Groups["aa"].Value[0];
                var modValue = double.Parse(cometMatch.Groups["modMass"].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture);
                if (modLetter < MaxMods)
                    _staticMods[modLetter] = modValue;
            }

            // cpp parity: SQTreader.cpp:168 — DiffMod lines map a marker character to a mass shift.
            // The cpp searches for " DiffMod" or tab+"DiffMod" (note the literal tab in the cpp).
            if (buffer.Contains(" DiffMod", StringComparison.Ordinal)
                || buffer.Contains("\tDiffMod", StringComparison.Ordinal))
            {
                var posEquals = buffer.IndexOf('=');
                if (posEquals < 0)
                {
                    throw new BlibException(false, $"Unexpected static mod format: {buffer}");
                }
                var modSymbol = buffer[posEquals - 1];
                // cpp parity: SQTreader.cpp:176 — atof(buffer.c_str() + posEquals +1).
                // atof reads as much numeric prefix as it can; we replicate by scanning forward.
                var modValue = ParseLeadingDouble(buffer, posEquals + 1);
                if (modSymbol < MaxMods)
                    _diffMods[modSymbol] = modValue;
            }

            // cpp parity: SQTreader.cpp:181 — Percolator marker.
            if (buffer.Contains("Percolator", StringComparison.Ordinal))
            {
                _percolated = true;
            }
        }

        // cpp parity: SQTreader.cpp:186.
        if (warnIfNotPercolated && !_percolated)
        {
            Verbosity.Status("File was not processed by Percolator. Filtering on xcorr.");
        }

        // Drop the consumed header so the body-parser indexes start at 0.
        if (headerEnd > 0)
            _lines.RemoveRange(0, headerEnd);
    }

    /// <summary>
    /// Read the spectrum-block portion of the file (everything past the H header). Each block
    /// starts with an S line giving scan/charge; the first M line gives the top-ranked match,
    /// from which we extract score + sequence. Subsequent M / L lines are skipped — only the
    /// top-ranked hit is kept (cpp parity).
    /// </summary>
    /// <remarks>cpp parity: SQTreader.cpp:228 — populates Psms with passing PSMs and calls
    /// <see cref="BuildParser.BuildTables(PsmScoreType, string, bool, WorkflowType)"/>.</remarks>
    private void ExtractPsms()
    {
        var scoreThreshold = GetScoreThreshold(BuildInput.Sqt);
        Verbosity.Debug(
            $"Using Percolator q-value threshold {scoreThreshold.ToString(CultureInfo.InvariantCulture)}");

        var i = 0;
        while (i < _lines.Count)
        {
            CurPsm = new PSM();

            // cpp parity: SQTreader.cpp:242 — advance to the next 'S' line.
            while (i < _lines.Count && (string.IsNullOrEmpty(_lines[i]) || _lines[i][0] != 'S'))
            {
                i++;
            }
            if (i >= _lines.Count)
                break;

            var sLine = _lines[i++];
            // cpp parity: SQTreader.cpp:256 — sscanf "%*c %d %*d %d %*d %*s %*lf %*f %*f %*d".
            // Columns: S, low_scan, high_scan, charge, process_time, server, exp_mass, total_intensity, lowest_sp, num_matched.
            // We pull scanNumber from column 2 and charge from column 4.
            var sCols = SplitWhitespace(sLine);
            if (sCols.Length < 4
                || !int.TryParse(sCols[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var scanNumber)
                || !int.TryParse(sCols[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var charge))
            {
                throw new BlibException(true,
                    $"Malformed S line at line {i} of '{GetFileName()}': {sLine}");
            }
            CurPsm.Charge = charge;
            CurPsm.SpecKey = scanNumber;

            // cpp parity: SQTreader.cpp:261 — if the next line isn't 'M', discard this PSM.
            if (i >= _lines.Count || string.IsNullOrEmpty(_lines[i]) || _lines[i][0] != 'M')
            {
                CurPsm = null;
                continue;
            }
            var mLine = _lines[i++];

            // cpp parity: SQTreader.cpp:271 — sscanf "%*c %*d %*d %*f %*f %lf %lf %*d %*d %s %*c".
            // Columns: M, rank-by-Xcorr, rank-by-Sp, calc-mass, deltaCn, XCorr, Sp (or q-value), matched, total, sequence, validation.
            var mCols = SplitWhitespace(mLine);
            if (mCols.Length < 10
                || !double.TryParse(mCols[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var xcorr)
                || !double.TryParse(mCols[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var qvalue))
            {
                throw new BlibException(true,
                    $"Malformed M line at line {i} of '{GetFileName()}': {mLine}");
            }
            var wholePepSeq = mCols[9];

            // cpp parity: SQTreader.cpp:273 — percolator negates its q-values.
            CurPsm.Score = _percolated ? -1.0 * qvalue : xcorr;

            // cpp parity: SQTreader.cpp:279 — good matches score 0 to threshold; drop if above.
            if (CurPsm.Score > scoreThreshold)
            {
                CurPsm = null;
                FilteredOutPsmCount++;
                continue;
            }

            // cpp parity: SQTreader.cpp:286 — split modSeq into unmod + mods.
            ParseModifiedSeq(wholePepSeq, out var unmodSeq, CurPsm.Mods, hasFlankingAa: true);
            CurPsm.UnmodSeq = unmodSeq;

            Psms.Add(CurPsm);
            Verbosity.Comment(VerbosityLevel.Detail,
                string.Format(CultureInfo.InvariantCulture,
                    "Saving PSM: scan {0}, charge {1}, qvalue {2:G3}, seq {3}.",
                    CurPsm.SpecKey, CurPsm.Charge, CurPsm.Score, CurPsm.UnmodSeq));
            CurPsm = null;

            // cpp parity: SQTreader.cpp:288 — any subsequent M / L lines for this spectrum are
            // skipped passively when the next iteration's "advance to S" loop runs. We don't
            // populate PSM.Proteins from L lines because the cpp doesn't either.
        }

        // cpp parity: SQTreader.cpp:295 — score type depends on whether Percolator ran.
        var score = _percolated ? PsmScoreType.PercolatorQValue : PsmScoreType.SequestXCorr;
        BuildTables(score);
    }

    /// <summary>
    /// Split an SQT-style sequence (e.g. <c>"K.PEPT*IDE.R"</c>) into the unmodified sequence
    /// and the list of position+delta-mass modifications. Mod marker characters (<c>*^$@#%</c>)
    /// immediately follow the modified residue; their per-character mass shift comes from
    /// the DiffMod lines parsed in <see cref="OpenRead"/>. Static modifications are then
    /// applied for every residue whose <c>_staticMods[c]</c> entry is non-zero.
    /// </summary>
    /// <param name="modSeq">The sequence with flanking AAs (if <paramref name="hasFlankingAa"/>) and
    /// mod marker characters.</param>
    /// <param name="unmodSeq">Output: the unmodified peptide sequence (just the residues).</param>
    /// <param name="mods">Output: list of (1-based position, delta mass) entries, appended in scan order.</param>
    /// <param name="hasFlankingAa">If true (the SQT default), <paramref name="modSeq"/> begins
    /// with "X." for the flanking AA and ends with ".X".</param>
    /// <remarks>cpp parity: SQTreader.cpp:314.</remarks>
    public void ParseModifiedSeq(string modSeq, out string unmodSeq, IList<SeqMod> mods, bool hasFlankingAa = true)
    {
        ArgumentNullException.ThrowIfNull(modSeq);
        ArgumentNullException.ThrowIfNull(mods);

        // cpp parity: SQTreader.cpp:320 — the symbols we may encounter as mod markers.
        const string modSymbols = "*^$@#%";

        // cpp parity: SQTreader.cpp:321 — start at index 2 to skip the "X." flanking residue.
        var startIdx = hasFlankingAa ? 2 : 0;

        var sb = new System.Text.StringBuilder();
        var modCount = 0;
        // cpp parity: SQTreader.cpp:328 — scan until the next '.' (the closing flanking) or EOS.
        for (var i = startIdx; i < modSeq.Length && modSeq[i] != '.'; i++)
        {
            var c = modSeq[i];
            if (modSymbols.Contains(c, StringComparison.Ordinal))
            {
                // cpp parity: SQTreader.cpp:330 — position is 1-based on the unmod seq AFTER the residue.
                // i - startIdx - modCount: index into the unmod seq we've built so far.
                var position = i - startIdx - modCount;
                var deltaMass = c < MaxMods ? _diffMods[c] : 0.0;
                mods.Add(new SeqMod(position, deltaMass));
                modCount++;
            }
            else
            {
                sb.Append(c);
            }
        }
        unmodSeq = sb.ToString();

        // cpp parity: SQTreader.cpp:344 — second pass: apply non-zero static mods for every residue.
        for (var i = 0; i < unmodSeq.Length; i++)
        {
            var c = unmodSeq[i];
            var modMass = c < MaxMods ? _staticMods[c] : 0.0;
            if (modMass > 0)
            {
                // mods are 1-based.
                mods.Add(new SeqMod(i + 1, modMass));
            }
        }
    }

    // --- small parsing helpers ------------------------------------------------------

    // cpp parity: SQTreader.cpp:93 — buffer.find_first_of("0123456789").
    private static int IndexOfFirstDigit(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] >= '0' && s[i] <= '9')
                return i;
        }
        return -1;
    }

    // cpp parity: SQTreader.cpp:133 — sscanf "%*c %*s %s" extracts the third whitespace token,
    // then parses "<letter>=<float>" or "<letter><x><float>" with sscanf "%c%*c%f".
    private static bool TryParseStaticModLine(string buffer, out char modLetter, out float modValue)
    {
        modLetter = '\0';
        modValue = 0;
        var tokens = SplitWhitespace(buffer);
        if (tokens.Length < 3)
            return false;
        // cpp parity: token[2] is the "<letter><sep><value>" string.
        var token = tokens[2];
        if (token.Length < 3)
            return false;
        modLetter = token[0];
        // skip token[1] (the separator), parse rest as float.
        return float.TryParse(token.AsSpan(2), NumberStyles.Float, CultureInfo.InvariantCulture, out modValue);
    }

    // cpp parity: SQTreader.cpp:176 — atof(buffer.c_str() + posEquals +1).
    // atof reads as much of the leading numeric prefix as it can; non-numeric chars stop it
    // and the return is 0.0 if no leading digits.
    private static double ParseLeadingDouble(string s, int start)
    {
        var end = start;
        // optional sign
        if (end < s.Length && (s[end] == '+' || s[end] == '-'))
            end++;
        var startNum = end;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.'))
            end++;
        // optional exponent
        if (end < s.Length && (s[end] == 'e' || s[end] == 'E'))
        {
            end++;
            if (end < s.Length && (s[end] == '+' || s[end] == '-'))
                end++;
            while (end < s.Length && char.IsDigit(s[end]))
                end++;
        }
        if (end == startNum)
            return 0.0;
        return double.TryParse(
            s.AsSpan(start, end - start),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var v) ? v : 0.0;
    }

    private static readonly char[] _whitespaceChars = { ' ', '\t' };

    // cpp uses sscanf "%s" which scans whitespace-delimited tokens. We do the same by splitting
    // on space/tab and dropping empties (consecutive separators).
    private static string[] SplitWhitespace(string s) =>
        s.Split(_whitespaceChars, StringSplitOptions.RemoveEmptyEntries);
}
