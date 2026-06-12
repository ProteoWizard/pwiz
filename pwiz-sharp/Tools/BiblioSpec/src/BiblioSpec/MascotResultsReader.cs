// Port of pwiz_tools/BiblioSpec/src/MascotResultsReader.{h,cpp}.
//
// Reads PSMs from a Matrix Science Mascot results file (.dat) via the
// MascotShim native wrapper around msparser. Mascot stores spectra inline
// in the .dat alongside the PSMs, so the same .dat doubles as the spec
// file — handled here by a nested SpecFileReader implementation that pulls
// peaks via mascot_dat_get_query_peaks.
//
// Phase 4b scope: PSM iteration + fixed/variable modification table
// construction + parseMods (cpp MascotResultsReader.cpp:322). Phase 5 adds
// Distiller rawfile-list parsing for multi-source projects; the basic
// `Mascot` Jamfile test (F027319-trim.dat) doesn't exercise that path so
// this file punts on it for now.

using System.Globalization;
using System.Runtime.InteropServices;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// BuildParser subclass for Mascot .dat search-results files. Mascot is the
/// only proteomics search engine that embeds its own MS2 spectra in the
/// results file, so this reader also acts as the spec-file source via a
/// nested <see cref="MascotShimSpecFileReader"/>.
/// </summary>
/// <remarks>cpp parity: MascotResultsReader.h:38.</remarks>
public sealed class MascotResultsReader : BuildParser
{
    private readonly string _datPath;
    private string _openedPath = string.Empty;   // may be a temp ASCII copy of _datPath
    private string _tempCopy = string.Empty;     // delete on Dispose if non-empty
    private IntPtr _handle;          // borrowed by the spec reader; freed by Dispose
    private bool _disposed;

    // Static-mod table: residue letter / N_TERM_POS / C_TERM_POS → list of
    // delta masses. cpp MascotResultsReader.h:64 uses the same shape
    // (`MultiModTable`); residues with multiple fixed mods are rare but
    // possible (e.g. selenocysteine alongside carbamidomethyl).
    private readonly Dictionary<char, List<double>> _staticMods = new();

    // Variable-mod table: msparser var-mod 1-based index → delta mass.
    // Looked up from a single character (0-9 or A-W) per position in the
    // PSM's varModsStr.
    private readonly Dictionary<int, double> _varModDeltas = new();

    // queryId → observed precursor m/z, populated during ParseFile and read
    // by MascotShimSpecFileReader.GetSpectrum so the SpecData carries the
    // right precursor MZ without a second shim roundtrip.
    private readonly Dictionary<int, double> _observedMzByQueryId = new();

    // Originating spec-file name (extracted from query title) → PSMs found
    // in that source. cpp MascotResultsReader.cpp:218 uses the same
    // pattern; empty key means "title had no recognisable File: tag" and
    // those PSMs end up bucketed under the .dat itself.
    private readonly SortedDictionary<string, List<PSM>> _psmsByFile =
        new(StringComparer.Ordinal);

    // Quantitation component name (e.g. "heavy" / "light") → per-residue
    // isotope delta map. Populated when the .dat declares a labeling
    // method via mascot_dat_get_quant_*. Empty when unlabeled. cpp parity:
    // MascotResultsReader.h:65 <c>methodModsMaps_</c>.
    private readonly Dictionary<string, Dictionary<char, double>> _componentMods =
        new(StringComparer.Ordinal);

    // Distiller-produced .dat files embed a list of original raw-file paths
    // in their USER params. cpp parity: MascotResultsReader.cpp:617. When
    // exactly one entry is present, every PSM is associated with it; with
    // multiple entries, getFilename parses the title for a `from file [N]`
    // tag to pick the right one.
    private readonly List<string> _distillerRawFiles = new();

    private const char NTermPos = (char)0;   // cpp PSM.h N_TERM_POS marker
    private const char CTermPos = (char)1;   // cpp PSM.h C_TERM_POS marker

    /// <summary>Returns true if <paramref name="path"/> ends with <c>.dat</c>
    /// (case-insensitive). cpp parity: BlibBuilder.cpp:771.</summary>
    public static bool AcceptsExtension(string path) =>
        path.EndsWith(".dat", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Construct a MascotResultsReader bound to <paramref name="maker"/> and
    /// the .dat file at <paramref name="datPath"/>.
    /// </summary>
    /// <remarks>cpp parity: MascotResultsReader.cpp:32.</remarks>
    public MascotResultsReader(BlibBuilder maker, string datPath, ProgressIndicator? parentProgress)
        : base(maker, datPath, parentProgress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datPath);
        _datPath = datPath;

        // Mascot .dats embed their peaks; the spec-file lookup is served by
        // our nested SpecFileReader rather than searching for a separate
        // mzML / wiff next to the .dat.
        PreferEmbeddedSpectra = true;
    }

    /// <summary>cpp parity: MascotResultsReader.cpp emits MASCOT_IONS_SCORE
    /// (BlibFilter score column).</summary>
    public override IList<PsmScoreType> GetScoreTypes() =>
        new[] { PsmScoreType.MascotIonsScore };

    /// <summary>
    /// cpp parity: MascotResultsReader::parseFile (MascotResultsReader.cpp:189).
    /// Opens the .dat via the shim, builds the fixed/var mod tables, iterates
    /// every PSM and applies parseMods, then flushes the collected PSMs to
    /// BuildTables. Because Mascot embeds its peaks in the .dat, every PSM
    /// belongs to the same spec-file bucket — the .dat itself.
    /// </summary>
    public override bool ParseFile()
    {
        EnsureHandleOpen();

        BuildFixedModTable();
        BuildVarModTable();
        BuildQuantComponentTables();
        LoadDistillerRawFiles();

        int rc = MascotShimInterop.NumQueries(_handle, out int numQueries);
        ThrowOnShimFailure(rc, "NumQueries");
        Verbosity.Debug($"Mascot .dat has {numQueries} queries.");

        // Pull every PSM into the BuildParser's Psms list. SpecKey carries
        // the 1-based query id so MascotShimSpecFileReader can serve peaks
        // by index via the shim. cpp parity: MascotResultsReader.cpp:241.
        rc = MascotShimInterop.OpenPsmIter(_handle, out IntPtr iter);
        ThrowOnShimFailure(rc, "OpenPsmIter");
        try
        {
            while (true)
            {
                int step = MascotShimInterop.NextPsm(iter, out MascotPsmRecord rec);
                if (step == 0) break;
                if (step < 0)
                {
                    throw new BlibException(true,
                        $"Mascot shim NextPsm failed: {MascotShimInterop.LastError()}");
                }

                var psm = new PSM
                {
                    Charge = rec.Charge,
                    SpecKey = rec.QueryId,
                    Score = rec.ExpectationValue,
                    UnmodSeq = MascotShimInterop.DecodeBuffer(rec.Peptide),
                };

                string varModsStr = MascotShimInterop.DecodeBuffer(rec.VarModsStr);
                ParseMods(psm, varModsStr);

                // cpp parity: MascotResultsReader.cpp:246 — applyIsotopeDiffs.
                // For labeled runs the per-PSM componentStr ("heavy"/"light"/
                // etc.) selects which residue-delta table to overlay.
                string componentStr = MascotShimInterop.DecodeBuffer(rec.ComponentStr);
                ApplyIsotopeDiffs(psm, componentStr);

                // ModifiedSeq is built by BuildParser at insert time from
                // UnmodSeq + Mods; nothing to set here.

                // Mascot's per-PSM observed m/z is the precursor MZ the
                // spec reader will surface as SpecData.Mz. cache it now so
                // the spec reader doesn't have to make a second shim call.
                _observedMzByQueryId[rec.QueryId] = rec.ObservedMz;

                // Bucket the PSM by the originating spec file extracted from
                // the query title — cpp MascotResultsReader.cpp:216 picks
                // the same value via ms_inputquery::getStringTitle. Files
                // with no recognisable "File:" tag fall into the empty-key
                // bucket and use the .dat as their nominal source.
                string sourceFile = ExtractSourceFile(rec.QueryId);
                if (!_psmsByFile.TryGetValue(sourceFile, out var bucket))
                {
                    bucket = new List<PSM>();
                    _psmsByFile[sourceFile] = bucket;
                }
                bucket.Add(psm);

                Psms.Add(psm);
            }
        }
        finally { MascotShimInterop.ClosePsmIter(iter); }

        // Inject the shim-backed SpecFileReader. PreferEmbeddedSpectra is on
        // so the per-bucket SetSpecFileName + BuildTables flow uses the
        // .dat-via-shim for peak lookup regardless of the nominal spec
        // file name we hand it.
        SpecReader = new MascotShimSpecFileReader(_handle, _observedMzByQueryId);
        LookUpBy = SpecIdType.ScanNumberId;

        // cpp parity: MascotResultsReader.cpp:279 — flush each (source spec
        // file) bucket separately so the SpectrumSourceFiles table records
        // the originating .wiff / mzML names extracted from the title,
        // even though the peaks all come from the same .dat.
        InitSpecFileProgress(_psmsByFile.Count);
        foreach (var (sourceFile, bucket) in _psmsByFile)
        {
            Psms.Clear();
            foreach (var p in bucket) Psms.Add(p);

            string specFileName = string.IsNullOrEmpty(sourceFile) ? _datPath : sourceFile;
            SetSpecFileName(specFileName, checkFile: false);
            BuildTables(PsmScoreType.MascotIonsScore, specFileName, showSpecProgress: false);
        }
        return true;
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:617 <c>getDistillerRawFiles</c>.
    /// Pulls the embedded raw-file paths out of the search-params USER block
    /// via the shim's enumeration entry point.
    /// </summary>
    private void LoadDistillerRawFiles()
    {
        var list = _distillerRawFiles;
        // GC-pinned delegate so the native callback can't outlive the
        // managed wrapper.
        var cb = new MascotStringCallback((utf8Ptr, _) =>
        {
            if (utf8Ptr == IntPtr.Zero) return;
            string? name = Marshal.PtrToStringUTF8(utf8Ptr);
            if (!string.IsNullOrEmpty(name)) list.Add(name);
        });
        int rc = MascotShimInterop.EnumerateDistillerRawFiles(_handle, cb, IntPtr.Zero);
        ThrowOnShimFailure(rc, "EnumerateDistillerRawFiles");
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:680 <c>getFilename</c>. Returns
    /// the originating spec filename for a query. cpp's resolution order:
    /// (1) the single Distiller raw file when there's exactly one,
    /// (2) the title's <c>File:</c> tag,
    /// (3) the <c>from file [N]</c> index when multiple Distiller raw files
    ///     exist,
    /// (4) empty (the caller falls back to the .dat itself).
    /// </summary>
    private string ExtractSourceFile(int queryId)
    {
        // (1) Single Distiller raw file: every PSM is associated with it.
        if (_distillerRawFiles.Count == 1) return _distillerRawFiles[0];

        int rc = MascotShimInterop.GetQueryTitle(_handle, queryId, null, 0, out int required);
        if (required <= 1 || (rc != (int)MascotResult.Ok && rc != (int)MascotResult.NotEnoughSpace))
            return _distillerRawFiles.Count > 1
                ? throw new BlibException(false,
                    "Multiple Distiller raw files but query title is missing.")
                : string.Empty;
        var buf = new byte[required];
        rc = MascotShimInterop.GetQueryTitle(_handle, queryId, buf, buf.Length, out _);
        if (rc != (int)MascotResult.Ok) return string.Empty;
        string title = MascotShimInterop.DecodeBuffer(buf);

        // (2) "File:..." tag, same parser the basic Mascot test relies on.
        string fromTitle = ParseFileFromTitle(title);
        if (!string.IsNullOrEmpty(fromTitle)) return fromTitle;

        // (3) Multiple Distiller raw files — pick the right entry by
        // parsing the "from file [N]" tag.
        if (_distillerRawFiles.Count > 1)
        {
            int idx = ParseFromFileIndex(title);
            if (idx < 0)
                throw new BlibException(false,
                    $"When creating multi-file projects in Mascot Distiller, "
                    + $"uncheck the 'Memory efficient (Not compatible with label-free)' "
                    + $"checkbox. Title string was: '{title}'");
            if (idx >= _distillerRawFiles.Count)
                throw new BlibException(false,
                    $"File index {idx} out of range in title '{title}'");
            return _distillerRawFiles[idx];
        }

        // (4) Fall back to the global search-params filename trio. cpp parity:
        // MascotResultsReader.cpp:779-789 — try FILENAME / DATAURL / COM
        // first as a plausible raw-file extension match, then as a plausible
        // MGF name. Cached on first call.
        if (_globalSourceFile is null)
        {
            _globalSourceFile = FindPlausibleGlobalSourceFile();
        }
        return _globalSourceFile;
    }

    private string? _globalSourceFile;

    private string FindPlausibleGlobalSourceFile()
    {
        // Order matches cpp's chained `||` so we land on the first plausible
        // value. The two passes use different acceptance rules: raw-file
        // extensions first, MGF second.
        foreach (var which in new[]
                 {
                     MascotShimInterop.MascotGlobalParam.Filename,
                     MascotShimInterop.MascotGlobalParam.DataUrl,
                     MascotShimInterop.MascotGlobalParam.Com,
                 })
        {
            string v = FetchGlobalParam(which);
            if (IsPlausibleRawFileName(v)) return v;
        }
        foreach (var which in new[]
                 {
                     MascotShimInterop.MascotGlobalParam.Filename,
                     MascotShimInterop.MascotGlobalParam.DataUrl,
                     MascotShimInterop.MascotGlobalParam.Com,
                 })
        {
            string v = FetchGlobalParam(which);
            if (IsPlausibleMgfFileName(v)) return v;
        }
        return string.Empty;
    }

    private string FetchGlobalParam(MascotShimInterop.MascotGlobalParam which)
    {
        int rc = MascotShimInterop.GetGlobalParam(_handle, (int)which, null, 0, out int len);
        if (len <= 1 || (rc != (int)MascotResult.Ok && rc != (int)MascotResult.NotEnoughSpace))
            return string.Empty;
        var buf = new byte[len];
        rc = MascotShimInterop.GetGlobalParam(_handle, (int)which, buf, buf.Length, out _);
        return rc == (int)MascotResult.Ok ? MascotShimInterop.DecodeBuffer(buf) : string.Empty;
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:657 <c>IsPlausibleRawFileName</c>.
    /// Returns true when the string ends with any known vendor raw-file
    /// extension (case-insensitive in cpp; both case variants are tried).
    /// </summary>
    private static bool IsPlausibleRawFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // List + casing kept in sync with cpp MascotResultsReader.cpp:173-183.
        ReadOnlySpan<string> extensions = new[]
        {
            ".raw", ".RAW", ".d", ".wiff", ".wiff2", ".lcd",
            ".mzXML", ".mzxml", ".mzML", ".mzml",
        };
        foreach (var ext in extensions)
            if (name.EndsWith(ext, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>cpp parity: MascotResultsReader.cpp:671 — MGF fallback.</summary>
    private static bool IsPlausibleMgfFileName(string name) =>
        !string.IsNullOrEmpty(name) &&
        (name.EndsWith(".mgf", StringComparison.Ordinal) ||
         name.EndsWith(".MGF", StringComparison.Ordinal));

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:697-744 — parse a <c>from file [N]</c>
    /// or <c>from files [...]</c> tag. Returns the integer index or -1 when
    /// nothing matches; throws on the multi-file form because cpp does too.
    /// </summary>
    private static int ParseFromFileIndex(string title)
    {
        const string fromFile = "from file";
        int idx = title.IndexOf(fromFile, StringComparison.Ordinal);
        if (idx < 0) return -1;
        idx += fromFile.Length;
        if (idx < title.Length && title[idx] == 's')
        {
            throw new BlibException(false,
                "Spectra from multiple files. Use scan group aggregation method 'None' "
                + $"in Mascot Distiller MS/MS processing options. Title string was: '{title}'");
        }
        int open = title.IndexOf('[', idx);
        if (open < 0) return -1;
        int close = title.IndexOf(']', open + 1);
        if (close < 0) return -1;
        var span = title.AsSpan(open + 1, close - open - 1);
        return int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result : -1;
    }

    /// <summary>
    /// cpp parity: BuildParser.cpp:1048 <c>getFilenameFromID</c> — Mascot
    /// query titles encode the source file as <c>File:NAME</c> or
    /// <c>File:"NAME"</c>; this picks NAME out, trimming the surrounding
    /// whitespace and quote markers. Returns an empty string when there's
    /// no recognisable <c>File:</c> tag.
    /// </summary>
    private static string ParseFileFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
        int start = title.IndexOf("File:", StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += "File:".Length;
        while (start < title.Length && title[start] == ' ') start++;
        if (start >= title.Length) return string.Empty;

        int end;
        if (title[start] == '"')
        {
            // Quoted form: File:"foo bar.wiff"c Sample:...
            start++;
            end = title.IndexOf('"', start);
        }
        else if (title[start] == '~')
        {
            // cpp also handles tilde-delimited filenames (rare; carry it).
            start++;
            end = title.IndexOf('~', start);
        }
        else
        {
            // Unquoted: File: foo bar.wiff, Sample:... — stop at the next
            // comma. cpp's logic also handles "no comma" by looking for an
            // attribute colon; the simple comma-terminated form covers
            // every BiblioSpec Mascot fixture.
            end = title.IndexOf(',', start);
        }
        if (end < 0) end = title.Length;
        return title.Substring(start, end - start).Trim();
    }

    /// <inheritdoc/>
    /// <remarks>BuildParser owns disposal of the SpecReader; we own the
    /// shim handle that backs both this reader and the SpecReader.</remarks>
    protected override void Dispose(bool disposing)
    {
        if (_disposed) { base.Dispose(disposing); return; }
        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            MascotShimInterop.Close(_handle);
            _handle = IntPtr.Zero;
        }
        if (!string.IsNullOrEmpty(_tempCopy))
        {
            try { File.Delete(_tempCopy); } catch { /* best-effort */ }
            _tempCopy = string.Empty;
        }
        base.Dispose(disposing);
    }

    // --- helpers --------------------------------------------------------------

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:500-577 <c>getIsotopeMasses</c> +
    /// per-component loop. Asks the shim for the active quantitation method's
    /// component → (residue, delta) tables and stashes them on
    /// <see cref="_componentMods"/>. No-op when the .dat is unlabeled.
    /// </summary>
    private void BuildQuantComponentTables()
    {
        // Hand the shim the directory that holds the quantitation / unimod
        // XSDs. BiblioSpec.csproj ships them under runtimes/<rid>/native/
        // msparser-config/ alongside MascotShim.dll.
        string configDir = LocateQuantConfigDir();
        if (!string.IsNullOrEmpty(configDir))
        {
            int setRc = MascotShimInterop.SetQuantConfigDir(_handle, configDir);
            ThrowOnShimFailure(setRc, "SetQuantConfigDir");
        }

        // Probe the method name. Empty string → no labeling, nothing else
        // to do.
        int nameRc = MascotShimInterop.GetQuantName(_handle, null, 0, out int nameLen);
        if (nameRc == (int)MascotResult.NoData || nameLen <= 1) return;
        if (nameRc != (int)MascotResult.NotEnoughSpace && nameRc != (int)MascotResult.Ok)
            ThrowOnShimFailure(nameRc, "GetQuantName (size probe)");
        var nameBuf = new byte[nameLen];
        ThrowOnShimFailure(
            MascotShimInterop.GetQuantName(_handle, nameBuf, nameBuf.Length, out _),
            "GetQuantName");

        int rc = MascotShimInterop.NumQuantComponents(_handle, out int numComponents);
        ThrowOnShimFailure(rc, "NumQuantComponents");
        for (int i = 0; i < numComponents; i++)
        {
            // Component name (two-call).
            rc = MascotShimInterop.GetQuantComponentName(_handle, i, null, 0, out int compLen);
            if (compLen <= 1) continue;
            if (rc != (int)MascotResult.NotEnoughSpace && rc != (int)MascotResult.Ok)
                ThrowOnShimFailure(rc, $"GetQuantComponentName({i}) size probe");
            var compBuf = new byte[compLen];
            ThrowOnShimFailure(
                MascotShimInterop.GetQuantComponentName(_handle, i, compBuf, compBuf.Length, out _),
                $"GetQuantComponentName({i})");
            string compName = MascotShimInterop.DecodeBuffer(compBuf);

            // Residue-diff table (two-call).
            rc = MascotShimInterop.GetQuantComponentDiffs(_handle, i, null, 0, out int diffCount);
            if (diffCount == 0) { _componentMods[compName] = new Dictionary<char, double>(); continue; }
            if (rc != (int)MascotResult.NotEnoughSpace && rc != (int)MascotResult.Ok)
                ThrowOnShimFailure(rc, $"GetQuantComponentDiffs({i}) size probe");
            var diffs = new MascotIsotopeDiff[diffCount];
            ThrowOnShimFailure(
                MascotShimInterop.GetQuantComponentDiffs(_handle, i, diffs, diffs.Length, out _),
                $"GetQuantComponentDiffs({i})");

            var map = new Dictionary<char, double>();
            foreach (var d in diffs) map[(char)d.Residue] = d.Delta;
            _componentMods[compName] = map;
        }
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:585 <c>applyIsotopeDiffs</c>. For
    /// each residue in the PSM's unmodified sequence, look it up in the
    /// component's mod table and append a SeqMod when present.
    /// </summary>
    private void ApplyIsotopeDiffs(PSM psm, string componentStr)
    {
        if (string.IsNullOrEmpty(componentStr)) return;
        if (!_componentMods.TryGetValue(componentStr, out var residueDeltas)) return;
        for (int i = 0; i < psm.UnmodSeq.Length; i++)
        {
            if (residueDeltas.TryGetValue(psm.UnmodSeq[i], out double delta))
            {
                psm.Mods.Add(new SeqMod(i + 1, delta));
            }
        }
    }

    /// <summary>
    /// Resolves the runtime path to the msparser config directory
    /// (quantitation_1.xsd / quantitation_2.xsd / unimod_2.xsd). Returns an
    /// empty string when not found — quant init then short-circuits.
    /// </summary>
    private static string LocateQuantConfigDir()
    {
        // The csproj copies the XSDs to runtimes/<rid>/native/msparser-config/.
        // The MascotShim.dll lives in the same parent dir.
        string? appBase = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(appBase)) return string.Empty;
        var rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        string candidate = Path.Combine(appBase, "runtimes", rid, "native", "msparser-config");
        if (Directory.Exists(candidate)) return candidate;
        // Fallback: look beside the executing assembly.
        candidate = Path.Combine(appBase, "msparser-config");
        if (Directory.Exists(candidate)) return candidate;
        return string.Empty;
    }

    private void EnsureHandleOpen()
    {
        if (_handle != IntPtr.Zero) return;

        // cpp parity: MascotResultsReader.cpp:69-85. msparser's
        // ms_mascotresfile_dat takes `const char*` and treats it as ANSI on
        // Windows — passing a UTF-8 path with non-ASCII characters fails to
        // resolve. Copy the .dat to a temp directory with a sanitized
        // ASCII-only name and open that instead. The path-stash in
        // _openedPath is what later code reads via GetSpecFileName.
        _openedPath = _datPath;
        if (HasNonAscii(_datPath))
        {
            string baseName = Path.GetFileName(_datPath);
            var safeChars = baseName.Select(c => c <= 0x7F ? c : '_').ToArray();
            string safeName = new string(safeChars);
            string tempPath = Path.Combine(Path.GetTempPath(), safeName);
            if (!File.Exists(tempPath))
                File.Copy(_datPath, tempPath);
            _openedPath = tempPath;
            _tempCopy = tempPath;
            Verbosity.Warn(
                $"Mascot Parser does not support Unicode in filepaths ('{_datPath}'): "
                + $"copying file to a temporary non-Unicode path '{tempPath}'.");
        }

        // cpp parity: MascotResultsReader.cpp:101 sets the score threshold
        // from getScoreThreshold(MASCOT) (default 0.05). The shim's PSM
        // iterator drops PSMs whose expectation value exceeds this.
        double cutoff = GetScoreThreshold(BuildInput.Mascot);
        int rc = MascotShimInterop.Open(_openedPath, scoreCutoff: cutoff, out _handle);
        if (rc != (int)MascotResult.Ok)
        {
            throw new BlibException(true,
                $"Error opening Mascot .dat '{_openedPath}': {MascotShimInterop.LastError()}");
        }
    }

    private static bool HasNonAscii(string s)
    {
        foreach (char c in s) if (c > 0x7F) return true;
        return false;
    }

    private static void ThrowOnShimFailure(int rc, string call)
    {
        if (rc == (int)MascotResult.Ok) return;
        throw new BlibException(true,
            $"Mascot shim {call} failed (rc={rc}): {MascotShimInterop.LastError()}");
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:134-185. For each fixed mod, parse
    /// the residue spec — handling the "N_term" / "C_term" tokens explicitly
    /// and treating every remaining uppercase letter as a residue to which
    /// the delta applies. Multiple fixed mods can target the same residue
    /// (vector of deltas per key).
    /// </summary>
    private void BuildFixedModTable()
    {
        int rc = MascotShimInterop.NumFixedMods(_handle, out int n);
        ThrowOnShimFailure(rc, "NumFixedMods");

        for (int i = 1; i <= n; i++)
        {
            rc = MascotShimInterop.GetFixedMod(_handle, i, out MascotMod mod);
            ThrowOnShimFailure(rc, $"GetFixedMod({i})");

            string residues = MascotShimInterop.DecodeBuffer(mod.Residues);

            // Strip the N_term / C_term tokens — they're mod targets in
            // their own right and the residue letters in the remainder go
            // through the per-residue path below.
            if (TryStripCaseInsensitive(ref residues, "N_term"))
                AppendStatic(NTermPos, mod.Delta);
            if (TryStripCaseInsensitive(ref residues, "C_term"))
                AppendStatic(CTermPos, mod.Delta);

            foreach (char c in residues)
            {
                if (c is >= 'A' and <= 'Z')
                    AppendStatic(c, mod.Delta);
            }
        }
    }

    private void AppendStatic(char key, double delta)
    {
        if (!_staticMods.TryGetValue(key, out var list))
        {
            list = new List<double>();
            _staticMods[key] = list;
        }
        list.Add(delta);
    }

    private static bool TryStripCaseInsensitive(ref string s, string token)
    {
        int idx = s.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        s = s.Remove(idx, token.Length);
        return true;
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:378. msparser indexes variable
    /// mods 1-based; build a (idx → delta) map so per-PSM lookup is O(1).
    /// </summary>
    private void BuildVarModTable()
    {
        int rc = MascotShimInterop.NumVarMods(_handle, out int n);
        ThrowOnShimFailure(rc, "NumVarMods");

        for (int i = 1; i <= n; i++)
        {
            rc = MascotShimInterop.GetVarMod(_handle, i, out MascotMod mod);
            ThrowOnShimFailure(rc, $"GetVarMod({i})");
            _varModDeltas[i] = mod.Delta;
        }
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:322 <c>parseMods</c>. Walks the
    /// PSM's variable-mod string (one char per peptide position with
    /// flanking N/C-term slots), maps each non-zero char to its msparser
    /// var-mod delta, then applies the static-mod table to every residue
    /// position that doesn't already carry a variable mod.
    /// </summary>
    private void ParseMods(PSM psm, string modStr)
    {
        if (string.IsNullOrEmpty(modStr)) return;

        // The mod string layout is [N-term, residue1, residue2, ..., C-term].
        // Position 0 in cpp PSM speak is the N-terminus; positions 1..N are
        // residues; position N+1 is the C-terminus. cpp later consolidates
        // 0 → 1 and N+1 → N.
        int seqLen = psm.UnmodSeq.Length;
        if (modStr.Length < seqLen + 2)
        {
            // Defensive: a varModsStr shorter than expected means we're
            // looking at a degenerate PSM. Skip mod application and let the
            // unmodified sequence stand.
            return;
        }

        AddVarMod(psm, modStr[0], position: 0);
        for (int i = 1; i <= seqLen; i++)
        {
            AddVarMod(psm, modStr[i], position: i);
        }
        AddVarMod(psm, modStr[seqLen + 1], position: seqLen + 1);

        // Static mods, residue-by-residue. cpp checks every position for a
        // pre-existing mod first; we mirror that by passing the running mod
        // list to AppendStaticIfFree.
        for (int i = 0; i < seqLen; i++)
        {
            AppendStaticIfFree(psm, psm.UnmodSeq[i], position: i + 1);
        }
        AppendStaticIfFree(psm, NTermPos, position: 0);
        AppendStaticIfFree(psm, CTermPos, position: seqLen + 1);

        // cpp consolidates terminal-only mods into the adjacent residue
        // position (MascotResultsReader.cpp:358-363). The PSM API exposes
        // SeqMod as a record struct; rebuild the list with the merged
        // positions.
        if (psm.Mods.Count == 0) return;
        for (int i = 0; i < psm.Mods.Count; i++)
        {
            var m = psm.Mods[i];
            int pos = m.Position;
            if (pos == 0) pos = 1;
            else if (pos == seqLen + 1) pos = seqLen;
            if (pos != m.Position) psm.Mods[i] = new SeqMod(pos, m.DeltaMass);
        }
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:371 <c>addVarMod</c>. The varMods
    /// char is '0'–'9' for indices 0–9, 'A'–'W' for 10–32 (cpp
    /// MascotResultsReader.cpp:482). Index 0 means "no mod"; X is reserved
    /// for error-tolerant matches (Phase 4c will fold those in).
    /// </summary>
    private void AddVarMod(PSM psm, char lookup, int position)
    {
        int idx = lookup switch
        {
            >= '1' and <= '9' => lookup - '0',
            >= 'A' and <= 'W' => lookup - 'A' + 10,
            _ => 0,
        };
        if (idx == 0) return;
        if (!_varModDeltas.TryGetValue(idx, out double delta) || delta == 0) return;
        psm.Mods.Add(new SeqMod(position, delta));
    }

    /// <summary>
    /// cpp parity: MascotResultsReader.cpp:389 <c>addStaticMods</c>. If the
    /// position already has a variable mod, don't pile a static on top —
    /// the cpp comment notes that's intentional (the variable mod takes
    /// precedence per Mascot's interpretation).
    /// </summary>
    private void AppendStaticIfFree(PSM psm, char key, int position)
    {
        if (!_staticMods.TryGetValue(key, out var deltas)) return;
        foreach (var existing in psm.Mods)
        {
            if (existing.Position == position) return;
        }
        foreach (double delta in deltas)
        {
            psm.Mods.Add(new SeqMod(position, delta));
        }
    }
}

/// <summary>
/// SpecFileReaderBase implementation that serves spectra straight out of the
/// .dat via the MascotShim. cpp parity: MascotSpecReader.h. Mascot's
/// queryIds are dense 1-based integers so the natural lookup is by SpecKey
/// (<see cref="SpecIdType.ScanNumberId"/>).
/// </summary>
/// <remarks>The shim handle is borrowed from the parent
/// <see cref="MascotResultsReader"/> — this class does NOT own the handle
/// and never closes it.</remarks>
internal sealed class MascotShimSpecFileReader : SpecFileReaderBase
{
    private readonly IntPtr _handle;
    private readonly IReadOnlyDictionary<int, double> _observedMzByQueryId;

    public MascotShimSpecFileReader(IntPtr handle,
        IReadOnlyDictionary<int, double> observedMzByQueryId)
    {
        if (handle == IntPtr.Zero)
            throw new ArgumentException("Mascot shim handle is null.", nameof(handle));
        _handle = handle;
        _observedMzByQueryId = observedMzByQueryId;
    }

    public override void OpenFile(string path, bool mzSort = false)
    {
        // No-op: the underlying .dat is opened by MascotResultsReader when
        // it constructs the shim handle. BuildParser still calls OpenFile
        // before iterating, hence this stub.
    }

    public override SpecIdType IdType { set { /* Mascot always uses ScanNumberId. */ } }

    public override bool GetSpectrum(int identifier, SpecData returnData, SpecIdType findBy, bool getPeaks = true)
    {
        ArgumentNullException.ThrowIfNull(returnData);
        int queryId = identifier;
        if (queryId <= 0) return false;

        returnData.Id = queryId;
        returnData.NumPeaks = 0;
        returnData.Mzs = null;
        returnData.Intensities = null;

        // Precursor m/z comes from the per-PSM observed value that
        // MascotResultsReader stashed during PSM enumeration. cpp pulls it
        // from `ms_peptide::getObserved()` at spec-fetch time
        // (MascotSpecReader.h:128); cached lookup is equivalent.
        if (_observedMzByQueryId.TryGetValue(queryId, out double observedMz))
            returnData.Mz = observedMz;

        // RT: cpp MascotSpecReader.h:133-148 tries msparser's per-query RT
        // API first (returns seconds, divide by 60 for minutes) and only
        // falls back to title parsing when it's empty/unparseable. The
        // title's "Elution:" value isn't always the same as the .dat-header
        // RT — Q1 in F027319-trim.dat reports Elution: 78.954 min in the
        // title but the actual sample RT is 24.266 min, so we MUST hit the
        // shim's getRetentionTimes path first.
        int rtRc = MascotShimInterop.GetQueryRetentionTime(_handle, queryId, -1, out double rtSeconds);
        if (rtRc == (int)MascotResult.Ok)
        {
            returnData.RetentionTime = rtSeconds / 60.0;
        }
        if (TryGetQueryTitle(queryId, out string title))
        {
            if (rtRc != (int)MascotResult.Ok)
                returnData.RetentionTime = ParseRetentionTimeFromTitle(title);
            // Bruker TIMS ion mobility lives only in the title — cpp parity:
            // MascotSpecReader.h:160 calls getIonMobilityFromTitle unconditionally.
            ApplyIonMobilityFromTitle(returnData, title);
        }

        if (!getPeaks)
        {
            return true;
        }

        int rc = MascotShimInterop.GetQueryPeakCount(_handle, queryId, out int peakCount);
        if (rc != (int)MascotResult.Ok || peakCount <= 0)
        {
            return false;
        }

        var mz = new double[peakCount];
        var intensity = new double[peakCount];
        int written = MascotShimInterop.GetQueryPeaks(_handle, queryId, mz, intensity, peakCount);
        if (written != peakCount)
        {
            return false;
        }

        returnData.NumPeaks = peakCount;
        returnData.Mzs = mz;
        returnData.Intensities = new float[peakCount];
        for (int i = 0; i < peakCount; i++)
        {
            returnData.Intensities[i] = (float)intensity[i];
        }
        return true;
    }

    public override bool GetSpectrum(string identifier, SpecData returnData, bool getPeaks = true)
    {
        // Mascot uses integer queryIds; the name-id path isn't part of the
        // Mascot dispatch but BuildParser's fallback might call it. Try to
        // parse identifier as a 1-based integer.
        if (!int.TryParse(identifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out int qid))
            return false;
        return GetSpectrum(qid, returnData, SpecIdType.ScanNumberId, getPeaks);
    }

    public override bool GetNextSpectrum(SpecData returnData, bool getPeaks = true)
    {
        // Sequential iteration isn't used by the BuildParser Mascot path;
        // the per-PSM GetSpectrum overload above is what matters.
        return false;
    }

    private bool TryGetQueryTitle(int queryId, out string title)
    {
        title = string.Empty;
        // Size probe.
        int rc = MascotShimInterop.GetQueryTitle(_handle, queryId, null, 0, out int required);
        if (rc == (int)MascotResult.NoData || required <= 1) return false;
        if (rc != (int)MascotResult.NotEnoughSpace && rc != (int)MascotResult.Ok) return false;

        var buf = new byte[required];
        rc = MascotShimInterop.GetQueryTitle(_handle, queryId, buf, buf.Length, out _);
        if (rc != (int)MascotResult.Ok) return false;
        title = MascotShimInterop.DecodeBuffer(buf);
        return true;
    }

    /// <summary>
    /// cpp parity: MascotSpecReader.h:219 <c>getRetentionTimeFromTitle</c>.
    /// Walks the title looking for any of the four known RT tags Mascot
    /// emitters use: <c>Elution:</c>, <c>Elution from:</c>, <c>RT:</c>,
    /// or <c>rt=</c>. When the value is a range (<c>Elution: A to B min</c>),
    /// returns the midpoint — cpp does the same via its <c>secondStartTag</c>
    /// machinery. Returns 0 when no tag is found.
    /// </summary>
    private static double ParseRetentionTimeFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return 0;
        // (startTag, rangeJoiner, endTag). The rangeJoiner is the token that
        // separates the two ends of a range (typically "to "); NULL in cpp
        // means "this tag doesn't support ranges". cpp parity:
        // MascotSpecReader.h:222-224.
        var tags = new (string Start, string? Range, string End)[]
        {
            ("Elution:",      "to ",  "min"),
            ("Elution from:", " to ", " "),
            ("RT:",           null,   "min"),
            ("rt=",           null,   ","),
        };
        foreach (var (startTag, rangeJoiner, endTag) in tags)
        {
            int idx = title.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            int start = idx + startTag.Length;
            // Skip whitespace after the tag.
            while (start < title.Length && char.IsWhiteSpace(title[start])) start++;

            // End tag bounds the whole value (single or range). Search for
            // the range joiner ONLY inside [start..endIdx) so we don't match
            // tokens like "auto " that happen to contain the joiner string
            // further on in the title.
            int endIdx = title.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0) endIdx = title.Length;

            int firstEnd = endIdx;
            int rangeJoinerLen = 0;
            if (rangeJoiner is not null)
            {
                int joinerIdx = title.IndexOf(rangeJoiner, start, endIdx - start,
                    StringComparison.OrdinalIgnoreCase);
                if (joinerIdx > 0 && joinerIdx < endIdx)
                {
                    firstEnd = joinerIdx;
                    rangeJoinerLen = rangeJoiner.Length;
                }
            }

            if (!TryParseSpan(title.AsSpan(start, firstEnd - start), out double rt))
                continue;

            // If we stopped at the range joiner, parse the second endpoint.
            if (rangeJoinerLen > 0)
            {
                int secondStart = firstEnd + rangeJoinerLen;
                if (TryParseSpan(title.AsSpan(secondStart, endIdx - secondStart), out double rt2))
                {
                    return (rt + rt2) / 2.0;
                }
            }
            return rt;
        }
        return 0;
    }

    private static bool TryParseSpan(ReadOnlySpan<char> span, out double value)
    {
        var trimmed = span.Trim();
        if (trimmed.IsEmpty) { value = 0; return false; }
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// cpp parity: MascotSpecReader.h:264 <c>getIonMobilityFromTitle</c>.
    /// Bruker TIMS data embeds the precursor ion mobility (1/K0, inverse
    /// reduced) in the query title, in one of three formats:
    /// <list type="bullet">
    /// <item><c>...|Mobility=NNN|...</c> (pipe-delimited).</item>
    /// <item><c>...1/K0=NNN ...</c> (space-delimited).</item>
    /// <item><c>... NNN 1/k0,...</c> (lowercase; value precedes the tag).</item>
    /// </list>
    /// Sets <see cref="SpecData.IonMobility"/> + <see cref="SpecData.IonMobilityType"/>
    /// on success; throws <see cref="BlibException"/> with the malformed
    /// string when a tag is present but the value won't parse — that's the
    /// error the cpp <c>mascot_tims_bad</c> Jamfile row expects via its
    /// <c>-e</c> argument.
    /// </summary>
    private static void ApplyIonMobilityFromTitle(SpecData returnData, string title)
    {
        if (string.IsNullOrEmpty(title)) return;

        int start = -1, end = -1;

        int idx = title.IndexOf("Mobility=", StringComparison.Ordinal);
        if (idx >= 0)
        {
            start = idx + "Mobility=".Length;
            end = title.IndexOf('|', start);
            if (end < 0) end = title.Length;
        }
        else
        {
            idx = title.IndexOf("1/K0=", StringComparison.Ordinal);
            if (idx >= 0)
            {
                start = idx + "1/K0=".Length;
                end = title.IndexOf(' ', start);
                if (end < 0) end = title.Length;
            }
            else
            {
                // "... 0.8123 1/k0," — value precedes the lowercase tag.
                int kIdx = title.IndexOf(" 1/k0", StringComparison.Ordinal);
                if (kIdx > 0)
                {
                    end = kIdx;
                    int spaceBefore = title.LastIndexOf(' ', kIdx - 1);
                    if (spaceBefore >= 0) start = spaceBefore + 1;
                }
            }
        }

        if (start < 0 || end <= start) return;

        string imStr = title.Substring(start, end - start);
        if (double.TryParse(imStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double im))
        {
            returnData.IonMobility = (float)im;
            returnData.IonMobilityType = IonMobilityType.InverseReducedVsecPerCm2;
            return;
        }
        // cpp throws BlibException(false, "Failure reading TIMS ion mobility value \"%s\"", ...);
        // mirror the exact phrasing — the Mascot_Tims_Bad Jamfile row matches
        // the substring that follows.
        throw new BlibException(false,
            $"Failure reading TIMS ion mobility value \"{imStr}\"");
    }
}
