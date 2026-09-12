// Port of pwiz_tools/BiblioSpec/src/BlibSearch.cpp (and its embedded SearchLibrary class).
//
// Faithful C# port of the BlibSearch tool's main logic. CLI parsing is intentionally kept
// here (not on BlibMaker) because cpp BlibSearch does not extend BlibMaker — it's its own
// program with its own boost::program_options switches.

using System.Globalization;
using System.Text;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Spectral-library search: load a query spectrum file and one or more <c>.blib</c> libraries,
/// score each query spectrum against the library candidates in its m/z window via dot product,
/// and write top-N matches to a <c>.report</c> file.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::BlibSearch</c> (BlibSearch.cpp) plus the embedded
/// <c>SearchLibrary</c> class (SearchLibrary.{h,cpp}).</para>
/// <para>The cpp side splits the work between <c>BlibSearch.cpp::main</c> (CLI parsing +
/// orchestration) and the reusable <c>SearchLibrary</c> class. The C# port collapses those
/// into one class because BlibSearch is the only consumer of SearchLibrary, and the Weibull
/// p-value path is not currently used (cpp default is false and our port has no
/// <c>WeibullPvalue</c> dependency).</para>
/// <para>cpp parity notes:</para>
/// <list type="bullet">
///   <item>Switches and defaults mirror BlibSearch.cpp:234-330 exactly.</item>
///   <item>Search flow mirrors SearchLibrary.cpp:98-124 (searchSpectrum) +
///         SearchLibrary.cpp:324-375 (runSearch).</item>
///   <item>Dot-product comparator + tie-break by lib-spec-id descending matches
///         SearchLibrary.cpp:462.</item>
///   <item>Charge-state filter mirrors SearchLibrary.cpp:517 — if the query has no charge
///         candidates, every library charge is accepted.</item>
///   <item>cachedSpectra_ — we always clear before each query, because BlibSearch never
///         sets <c>mz-sort</c> in its option table (so cpp <c>querySorted_</c> is always
///         false, SearchLibrary.cpp:44, and updateSpectrumCache clears the cache every
///         call, SearchLibrary.cpp:142-145).</item>
///   <item>Decoy generation (<c>--decoys-per-target</c>, <c>--circ-shift</c>,
///         <c>--shift-raw-spectrum</c>) IS ported — drives the <c>search-decoy</c> golden
///         test. When enabled, each target spectrum gets <c>DecoysPerTarget</c> circular-
///         shifted decoys; targets and decoys are scored and reported separately. The decoy
///         report file path is derived by replacing the target report's extension with
///         <c>decoy.report</c> (cpp BlibSearch.cpp:111-112).</item>
///   <item>Weibull p-values and the <c>--psm-result-file</c> output are NOT ported. cpp
///         defaults <c>compute-p-values</c> to false, and even when enabled the computed
///         Weibull params / p-values are not written to the <c>.report</c> file — the
///         report's column set is fixed (Reportfile.cpp:74-101) and contains no p-value
///         column. So the decoy-report test passes without WeibullPvalue. If a future test
///         consumes p-values, port WeibullPvalue.cpp + PsmFile.cpp then.</item>
/// </list>
/// </remarks>
public sealed class BlibSearch : IDisposable
{
    /// <summary>cpp <c>SearchLibrary::MIN_PEAK_SIZE</c> at SearchLibrary.h:49 — minimum
    /// library-spec peak count to consider a library candidate.</summary>
    public const int MinPeakSize = 5;

    // --- Options (cpp BlibSearch.cpp:234-330) ----------------------------------------------

    /// <summary>cpp <c>clear-precursor,c</c>. Default true.</summary>
    public bool ClearPrecursor { get; set; } = true;

    /// <summary>cpp <c>topPeaksForSearch</c>. Default 100.</summary>
    public int TopPeaksForSearch { get; set; } = 100;

    /// <summary>cpp <c>mz-window,w</c>. Default 3.</summary>
    public double MzWindow { get; set; } = 3;

    /// <summary>cpp <c>min-peaks,n</c>. Default 20.</summary>
    public int MinPeaks { get; set; } = 20;

    /// <summary>cpp <c>low-charge,L</c>. Default 1.</summary>
    public int LowCharge { get; set; } = 1;

    /// <summary>cpp <c>high-charge,H</c>. Default 5.</summary>
    public int HighCharge { get; set; } = 5;

    /// <summary>cpp <c>report-matches,m</c>. Default 5. -1 means all.</summary>
    public int ReportMatches { get; set; } = 5;

    /// <summary>cpp <c>report-file,R</c>. Default null → use <c>&lt;spectrum-file&gt;.report</c>.</summary>
    public string? ReportFile { get; set; }

    /// <summary>cpp <c>preserve-order</c>. When false, spectra are processed sorted by m/z.</summary>
    public bool PreserveOrder { get; set; }

    /// <summary>cpp <c>bin-size</c>. Default 1.0.</summary>
    public double BinSize { get; set; } = 1.0;

    /// <summary>cpp <c>bin-offset</c>. Default 0.</summary>
    public double BinOffset { get; set; }

    /// <summary>cpp <c>remove-noise-first</c>. Default true.</summary>
    public bool RemoveNoiseFirst { get; set; } = true;

    /// <summary>cpp <c>decoys-per-target</c>. Default 0 (decoy generation disabled).</summary>
    public int DecoysPerTarget { get; set; }

    /// <summary>cpp <c>circ-shift</c>. Default 3 m/z. Increment between successive decoys.</summary>
    public double CircShift { get; set; } = 3;

    /// <summary>cpp <c>shift-raw-spectrum</c>. Default true. When true, shift raw peaks (the
    /// processed peak list is rebuilt afterwards); when false, shift the already-processed peaks.</summary>
    public bool ShiftRawSpectrum { get; set; } = true;

    /// <summary>The query spectrum file path (mzML/mzXML/ms2/etc.).</summary>
    public string SpectrumFile { get; set; } = string.Empty;

    /// <summary>One or more library paths (<c>.blib</c>).</summary>
    public List<string> LibraryNames { get; } = new();

    // --- State -----------------------------------------------------------------------------

    private readonly List<LibReader> _libraries = new();
    private readonly List<Match> _targetMatches = new();
    private readonly List<Match> _decoyMatches = new();
    private readonly List<RefSpectrum> _cachedSpectra = new();
    private readonly List<RefSpectrum> _cachedDecoySpectra = new();
    private bool _disposed;

    /// <summary>Read-only view of the currently-cached target matches (last <see cref="SearchSpectrum"/> call).</summary>
    public IReadOnlyList<Match> TargetMatches => _targetMatches;

    /// <summary>Read-only view of the currently-cached decoy matches (last <see cref="SearchSpectrum"/> call).</summary>
    public IReadOnlyList<Match> DecoyMatches => _decoyMatches;

    /// <summary>
    /// cpp BlibSearch.cpp:362-387 — verify the spec file has a recognised extension and
    /// every library ends in <c>.blib</c>.
    /// </summary>
    /// <remarks>cpp parity: BlibSearch.cpp:365-377 — the recognised query extensions are
    /// .ms2, .cms2, .bms2, .mzML, .mzXML, .MGF, .mz5, .wiff, .wiff2.</remarks>
    public static void CheckFileExtensions(string specFileName, IReadOnlyList<string> libraryNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specFileName);
        ArgumentNullException.ThrowIfNull(libraryNames);

        if (!BlibUtils.HasExtension(specFileName, ".ms2") &&
            !BlibUtils.HasExtension(specFileName, ".cms2") &&
            !BlibUtils.HasExtension(specFileName, ".bms2") &&
            !BlibUtils.HasExtension(specFileName, ".mzML") &&
            !BlibUtils.HasExtension(specFileName, ".mzXML") &&
            !BlibUtils.HasExtension(specFileName, ".MGF") &&
            !BlibUtils.HasExtension(specFileName, ".mz5") &&
            !BlibUtils.HasExtension(specFileName, ".wiff") &&
            !BlibUtils.HasExtension(specFileName, ".wiff2"))
        {
            Verbosity.Error(
                $"Spectrum file '{specFileName}' must be of type .ms2, .cms2, .bms2, " +
                ".mzML, .mzXML, .MGF, .mz5, or .wiff/.wiff2.");
        }

        foreach (var libName in libraryNames)
        {
            if (!BlibUtils.HasExtension(libName, ".blib"))
            {
                Verbosity.Error($"Library '{libName}' must be of type .blib.");
            }
        }
    }

    // --- CLI parsing -----------------------------------------------------------------------

    /// <summary>
    /// Print the BlibSearch usage text to <see cref="Console.Out"/>.
    /// </summary>
    /// <remarks>cpp parity: cpp uses boost::program_options' auto-generated usage from the
    /// option descriptions. We reproduce the option list verbatim from BlibSearch.cpp:234-281.</remarks>
    public static void Usage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Usage: BlibSearch [options] <spectrum-file> <library> [<library>...]");
        sb.AppendLine();
        sb.AppendLine("Options:");
        sb.AppendLine("  -c, --clear-precursor arg (=true)   Remove the peaks in a X m/z window around the precursor from the query and library spectrum.");
        sb.AppendLine("      --topPeaksForSearch arg (=100)  Use ARG of the highest intensity peaks.");
        sb.AppendLine("  -w, --mz-window arg (=3)            Compare query to library spectra with precursor m/z +/- ARG.");
        sb.AppendLine("  -n, --min-peaks arg (=20)           Minimum number of peaks the query spectrum must have.");
        sb.AppendLine("  -L, --low-charge arg (=1)           Search only spectra with charge no less than ARG.");
        sb.AppendLine("  -H, --high-charge arg (=5)          Search only spectra with charge no higher than ARG.");
        sb.AppendLine("  -m, --report-matches arg (=5)       Return ARG of the best matches for each query. Use -1 to report all.");
        sb.AppendLine("  -R, --report-file arg               Return results in report file named ARG. Default is <spectrum file name>.report.");
        sb.AppendLine("      --preserve-order                Search spectra in the order they appear in the file.");
        sb.AppendLine("  -h, --help                          Print this help text.");
        Console.Out.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Parse a BlibSearch-style argv. cpp uses boost::program_options; we re-implement the
    /// subset of flags BlibSearch declares.
    /// </summary>
    /// <param name="argv">Argv from Main (no program name at [0]).</param>
    /// <returns>True if the args parsed successfully; false if <c>-h</c>/<c>--help</c> was
    /// requested or there are no args (caller should print usage and exit 0).</returns>
    /// <remarks>
    /// <para>cpp parity: BlibSearch.cpp:223 <c>ParseCommandline</c> + cpp's CommandLine
    /// framework. We support both short forms (<c>-w 3</c>) and long forms
    /// (<c>--mz-window=3</c> or <c>--mz-window 3</c>) the way cpp boost::program_options
    /// would have accepted them.</para>
    /// <para>The last argv positions are the spectrum file followed by 1+ library names
    /// (cpp BlibSearch.cpp:333-339 — argv has both <c>spectrum-file</c> required and
    /// <c>library</c> as the last-multi-value arg).</para>
    /// </remarks>
    public bool ParseArgs(string[] argv)
    {
        ArgumentNullException.ThrowIfNull(argv);

        if (argv.Length == 0) return false;

        var positionals = new List<string>();
        var i = 0;
        while (i < argv.Length)
        {
            var arg = argv[i];
            if (arg == "-h" || arg == "--help" || arg == "-?")
            {
                return false;
            }

            // Long option: --name or --name=value
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var eq = arg.IndexOf('=', StringComparison.Ordinal);
                string name;
                string? value;
                if (eq >= 0)
                {
                    name = arg.Substring(2, eq - 2);
                    value = arg[(eq + 1)..];
                }
                else
                {
                    name = arg[2..];
                    value = null;
                }
                i = ApplyLongSwitch(name, value, i, argv);
                continue;
            }

            // Short option: -X or -X value
            if (arg.Length == 2 && arg[0] == '-')
            {
                i = ApplyShortSwitch(arg[1], i, argv);
                continue;
            }

            // Positional.
            positionals.Add(arg);
            i++;
        }

        if (positionals.Count < 2)
        {
            return false;
        }

        // cpp parity: BlibSearch.cpp:334-335 — positionals are [spectrum-file, libs...].
        SpectrumFile = positionals[0];
        for (var p = 1; p < positionals.Count; p++)
            LibraryNames.Add(positionals[p]);

        return true;
    }

    private int ApplyShortSwitch(char switchChar, int i, string[] argv)
    {
        switch (switchChar)
        {
            case 'c': // clear-precursor
                ClearPrecursor = ParseBoolArg(switchChar, ref i, argv);
                break;
            case 'w': // mz-window
                MzWindow = ParseDoubleArg(switchChar, ref i, argv);
                break;
            case 'n': // min-peaks
                MinPeaks = ParseIntArg(switchChar, ref i, argv);
                break;
            case 'L': // low-charge
                LowCharge = ParseIntArg(switchChar, ref i, argv);
                break;
            case 'H': // high-charge
                HighCharge = ParseIntArg(switchChar, ref i, argv);
                break;
            case 'm': // report-matches
                ReportMatches = ParseIntArg(switchChar, ref i, argv);
                break;
            case 'R': // report-file
                ReportFile = ParseStringArg(switchChar, ref i, argv);
                break;
            default:
                Verbosity.Error($"Unknown switch '-{switchChar}'.");
                return i + 1; // unreachable
        }
        return i + 1;
    }

    private int ApplyLongSwitch(string name, string? inlineValue, int i, string[] argv)
    {
        switch (name)
        {
            case "clear-precursor":
                ClearPrecursor = ParseBoolLong(name, inlineValue, ref i, argv);
                break;
            case "topPeaksForSearch":
                TopPeaksForSearch = ParseIntLong(name, inlineValue, ref i, argv);
                break;
            case "mz-window":
                MzWindow = ParseDoubleLong(name, inlineValue, ref i, argv);
                break;
            case "min-peaks":
                MinPeaks = ParseIntLong(name, inlineValue, ref i, argv);
                break;
            case "low-charge":
                LowCharge = ParseIntLong(name, inlineValue, ref i, argv);
                break;
            case "high-charge":
                HighCharge = ParseIntLong(name, inlineValue, ref i, argv);
                break;
            case "report-matches":
                ReportMatches = ParseIntLong(name, inlineValue, ref i, argv);
                break;
            case "report-file":
                ReportFile = ParseStringLong(name, inlineValue, ref i, argv);
                break;
            case "preserve-order":
                // cpp parity: BlibSearch.cpp:272 — a switch with no value, presence sets the flag.
                PreserveOrder = true;
                break;
            case "bin-size":
                BinSize = ParseDoubleLong(name, inlineValue, ref i, argv);
                break;
            case "bin-offset":
                BinOffset = ParseDoubleLong(name, inlineValue, ref i, argv);
                break;
            case "remove-noise-first":
                RemoveNoiseFirst = ParseBoolLong(name, inlineValue, ref i, argv);
                break;
            case "decoys-per-target":
                DecoysPerTarget = ParseIntLong(name, inlineValue, ref i, argv);
                break;
            case "circ-shift":
                CircShift = ParseDoubleLong(name, inlineValue, ref i, argv);
                break;
            case "shift-raw-spectrum":
                ShiftRawSpectrum = ParseBoolLong(name, inlineValue, ref i, argv);
                break;
            // cpp BlibSearch.cpp:284-329 dev options. Accept and ignore — the report file
            // doesn't carry their effects (no p-value column), and the test harness never
            // sets a non-default that would change the decoy-report contents. Only consume
            // a value when it's inline (--name=VALUE) — otherwise the parser would greedily
            // eat the NEXT real flag as the value (`--compute-p-values --decoys-per-target=1`
            // → DecoysPerTarget stays 0, decoy report comes out empty).
            case "compute-p-values":
            case "fraction-to-fit":
            case "weibull-param-file":
            case "correlation-tolerance":
            case "print-all-params":
            case "min-weibull-scores":
            case "psm-result-file":
                // Consume the inline value if present; otherwise leave argv[i+1] alone — cpp's
                // boost::program_options would have errored on a missing value, but for
                // accept-and-ignore the safe semantics are "absorb when we can, never steal".
                _ = inlineValue;
                break;
            default:
                // cpp parity: BlibSearch.cpp:345 — boost would exit with an error here.
                Verbosity.Error($"Unknown switch '--{name}'.");
                break;
        }
        return i + 1;
    }

    // --- arg-value helpers (short form: value is the next argv element) --------------------

    private static string ParseStringArg(char sw, ref int i, string[] argv)
    {
        if (++i >= argv.Length)
        {
            Verbosity.Error($"-{sw} requires a value.");
            return string.Empty; // unreachable
        }
        return argv[i];
    }

    private static int ParseIntArg(char sw, ref int i, string[] argv)
    {
        var s = ParseStringArg(sw, ref i, argv);
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            Verbosity.Error($"-{sw} expects an integer, got '{s}'.");
        }
        return v;
    }

    private static double ParseDoubleArg(char sw, ref int i, string[] argv)
    {
        var s = ParseStringArg(sw, ref i, argv);
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            Verbosity.Error($"-{sw} expects a number, got '{s}'.");
        }
        return v;
    }

    private static bool ParseBoolArg(char sw, ref int i, string[] argv)
    {
        var s = ParseStringArg(sw, ref i, argv);
        return ParseBoolValue(s, $"-{sw}");
    }

    // --- arg-value helpers (long form: value may be inline after '=' or in next argv) ------

    private static string ParseStringLong(string name, string? inlineValue, ref int i, string[] argv)
    {
        if (inlineValue is not null) return inlineValue;
        if (++i >= argv.Length)
        {
            Verbosity.Error($"--{name} requires a value.");
            return string.Empty; // unreachable
        }
        return argv[i];
    }

    private static int ParseIntLong(string name, string? inlineValue, ref int i, string[] argv)
    {
        var s = ParseStringLong(name, inlineValue, ref i, argv);
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
        {
            Verbosity.Error($"--{name} expects an integer, got '{s}'.");
        }
        return v;
    }

    private static double ParseDoubleLong(string name, string? inlineValue, ref int i, string[] argv)
    {
        var s = ParseStringLong(name, inlineValue, ref i, argv);
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            Verbosity.Error($"--{name} expects a number, got '{s}'.");
        }
        return v;
    }

    private static bool ParseBoolLong(string name, string? inlineValue, ref int i, string[] argv)
    {
        var s = ParseStringLong(name, inlineValue, ref i, argv);
        return ParseBoolValue(s, $"--{name}");
    }

    private static bool ParseBoolValue(string s, string flag)
    {
        // cpp parity: boost::lexical_cast<bool>(s) accepts "0"/"1" and "true"/"false".
        if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
        if (s == "1") return true;
        if (s == "0") return false;
        Verbosity.Error($"{flag} expects true|false|0|1, got '{s}'.");
        return false; // unreachable
    }

    // --- search orchestration ---------------------------------------------------------------

    /// <summary>
    /// Open each library file and the spectrum file, then iterate spectra and search each one.
    /// Writes matches into a <c>.report</c> file alongside the spectrum file.
    /// </summary>
    /// <remarks>cpp parity: BlibSearch.cpp:75 main() (the orchestration block 88-170).</remarks>
    public void Run()
    {
        if (string.IsNullOrEmpty(SpectrumFile))
            throw new BlibException(false, "BlibSearch.Run: SpectrumFile not set.");
        if (LibraryNames.Count == 0)
            throw new BlibException(false, "BlibSearch.Run: no library files set.");

        CheckFileExtensions(SpectrumFile, LibraryNames);

        // cpp parity: BlibSearch.cpp:93-97 — status line.
        var libNamesJoined = string.Join(", ", LibraryNames);
        Verbosity.Status($"Using library(s) {libNamesJoined}.");

        // cpp parity: BlibSearch.cpp:99-103 — write to a .tmp then rename on success.
        var reportFileName = GetTargetReportName();
        var finalReport = reportFileName;
        var tmpReport = reportFileName + ".tmp";
        // cpp parity: BlibSearch.cpp:111-112 — decoy report path is the target report path
        // with its extension replaced by "decoy.report". The replacement only fires when
        // DecoysPerTarget > 0, mirroring the cpp conditional Open call.
        var finalDecoyReport = DecoysPerTarget > 0
            ? BlibUtils.ReplaceExtension(finalReport, "decoy.report")
            : null;

        // cpp parity: BlibSearch.cpp:106 — Reportfile constructed from the options table.
        var reportOptionsHeader = BuildReportHeader();
        using (var targetReport = new Reportfile(ReportMatches, reportOptionsHeader))
        using (var decoyReport = new Reportfile(ReportMatches, reportOptionsHeader))
        {
            targetReport.Open(tmpReport);
            if (finalDecoyReport is not null)
                decoyReport.Open(finalDecoyReport);

            // cpp parity: BlibSearch.cpp:124 — open library readers. If one throws partway
            // through (corrupt .blib, locked file), already-opened LibReaders would leak their
            // SQLite connections until process exit — the normal cleanup path further down
            // wouldn't run. Dispose any readers we've already added before rethrowing.
            try
            {
                foreach (var libName in LibraryNames)
                {
                    Verbosity.Debug($"Creating reader for library {libName}.");
                    _libraries.Add(new LibReader(libName));
                }
            }
            catch
            {
                foreach (var opened in _libraries) opened.Dispose();
                _libraries.Clear();
                throw;
            }

            // cpp parity: BlibSearch.cpp:128-131 — open the spec file in IndexId mode.
            using var fileReader = new PwizSharpSpecFileReader();
            fileReader.IdType = SpecIdType.IndexId;
            var mzSort = !PreserveOrder;
            fileReader.OpenFile(SpectrumFile, mzSort);

            Verbosity.Status($"Searching spectra in '{SpectrumFile}'.");

            // cpp parity: BlibSearch.cpp:136 — iterate spectra, search each one.
            var data = new SpecData();
            while (fileReader.GetNextSpectrum(data))
            {
                // Convert SpecData → BiblioSpec Spectrum for searching.
                var spec = SpecDataToSpectrum(data);

                SearchSpectrum(spec);

                if (_targetMatches.Count == 0)
                    continue;

                targetReport.WriteMatches(_targetMatches);
                // cpp parity: BlibSearch.cpp:152 — decoy report writes happen alongside the
                // target report, regardless of whether the decoy list is empty for this query.
                if (finalDecoyReport is not null)
                    decoyReport.WriteMatches(_decoyMatches);
            }
        }

        // cpp parity: BlibSearch.cpp:172 — rename tmp -> final. Only the TARGET report goes
        // through the .tmp/rename path; the decoy report is written directly to its final
        // path (cpp BlibSearch.cpp:113 calls decoyReport.open with the final name).
        if (File.Exists(finalReport))
            File.Delete(finalReport);
        File.Move(tmpReport, finalReport);
    }

    /// <summary>cpp BlibSearch.cpp:199 — derive a report filename if not supplied.</summary>
    private string GetTargetReportName()
    {
        if (!string.IsNullOrEmpty(ReportFile)) return ReportFile;
        return BlibUtils.ReplaceExtension(SpectrumFile, "report");
    }

    /// <summary>
    /// Build the comment-header block for the <c>.report</c> file. cpp Reportfile.cpp:121
    /// <c>optionsHeaderString</c>.
    /// </summary>
    /// <remarks>cpp parity: same lines, same labels. The <c>date</c> uses <c>ctime()</c>
    /// which appends a newline; we use the equivalent format. <c>clear-precursor</c> is
    /// emitted as <c>"true"</c> when the option count is non-zero, which cpp evaluates the
    /// same way (boost stores defaults as if explicitly set).</remarks>
    private string BuildReportHeader()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Search results from BilbSearch"); // cpp typo "Bilb" preserved (Reportfile.cpp:131)
        // cpp uses ctime() which produces "Ddd Mmm DD HH:MM:SS YYYY\n". We emit the same shape.
        var now = DateTime.Now;
        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "# Date: {0:ddd} {0:MMM} {1,2} {0:HH:mm:ss yyyy}",
            now, now.Day));
        // cpp Reportfile.cpp:133 emits an empty line after the date — the comparator's skip-lines
        // covers the date itself but not this blank separator. Preserve byte parity.
        sb.AppendLine();
        sb.AppendLine($"# query file: {SpectrumFile}");
        sb.AppendLine("# Library file list:");
        for (var i = 0; i < LibraryNames.Count; i++)
        {
            sb.AppendLine($"# libID{i + 1}\t{LibraryNames[i]}");
        }
        sb.AppendLine("# Options:");
        sb.AppendLine($"# clear-precursor = {(ClearPrecursor ? "true" : "false")}");
        sb.AppendLine($"# topPeaksForSearch = {TopPeaksForSearch.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"# mz-window = {MzWindow.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"# low-charge = {LowCharge.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"# high-charge = {HighCharge.ToString(CultureInfo.InvariantCulture)}");
        sb.Append("# report-matches = ");
        if (ReportMatches == -1) sb.AppendLine("all");
        else sb.AppendLine(ReportMatches.ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>
    /// Copy precursor/peak data from <see cref="SpecData"/> (what
    /// <see cref="PwizSharpSpecFileReader"/> returns) into a BiblioSpec <see cref="Spectrum"/>.
    /// </summary>
    /// <remarks>
    /// cpp doesn't need this glue because cpp's PwizReader populates a Spectrum directly.
    /// Our sharp PwizReader returns the leaner SpecData, so this is the bridge.
    /// </remarks>
    private static Spectrum SpecDataToSpectrum(SpecData data)
    {
        var spec = new Spectrum
        {
            ScanNumber = data.Id,
            Mz = data.Mz,
            RetentionTime = data.RetentionTime,
            StartTime = data.StartTime,
            EndTime = data.EndTime,
            CollisionalCrossSection = data.Ccs,
            TotalIonCurrentRaw = data.TotalIonCurrent,
        };
        spec.SetIonMobility(data.IonMobility, data.IonMobilityType);

        // cpp parity: Spectrum carries a list of possible charges (BlibSearch.cpp:138 calls
        // getPossibleCharges()). MS2 queries can carry multiple Z lines → multiple possible
        // charges; PwizSharpSpecFileReader now collects them all.
        if (data.Charges.Count > 0)
        {
            foreach (var c in data.Charges)
                spec.AddCharge(c);
        }
        else if (data.Charge != 0)
        {
            spec.AddCharge(data.Charge);
        }

        // Hydrate raw peaks.
        if (data.NumPeaks > 0 && data.Mzs is not null && data.Intensities is not null)
        {
            var peaks = new PeakT[data.NumPeaks];
            for (var i = 0; i < data.NumPeaks; i++)
            {
                peaks[i] = new PeakT(data.Mzs[i], data.Intensities[i]);
            }
            spec.SetRawPeaks(peaks);
        }
        return spec;
    }

    /// <summary>
    /// cpp <c>SearchLibrary::searchSpectrum</c> at SearchLibrary.cpp:98. Process the query
    /// spectrum, refresh the spectrum cache for its m/z window, then call <see cref="RunSearch"/>.
    /// </summary>
    public void SearchSpectrum(Spectrum querySpec)
    {
        ArgumentNullException.ThrowIfNull(querySpec);

        _targetMatches.Clear();
        _decoyMatches.Clear();

        // cpp parity: SearchLibrary.cpp:103 — process peaks via PeakProcessor.
        var processor = new PeakProcessor
        {
            ClearPrecursor = ClearPrecursor,
            NoiseFirst = RemoveNoiseFirst,
            NumTopPeaks = TopPeaksForSearch,
            BinSize = BinSize,
            BinOffset = BinOffset,
        };
        processor.ProcessPeaks(querySpec);

        // cpp parity: SearchLibrary.cpp:104-108 — too few peaks → skip.
        if (querySpec.NumProcessedPeaks < MinPeaks)
        {
            Verbosity.Warn(
                $"Spectrum {querySpec.ScanNumber} has {querySpec.NumProcessedPeaks} peaks, " +
                "fewer than the minimum.");
            return;
        }

        // cpp parity: SearchLibrary.cpp:112 updateSpectrumCache — we always rebuild (mz-sort
        // is never enabled in cpp BlibSearch's option table).
        UpdateSpectrumCache(querySpec.Mz, processor);

        if (_cachedSpectra.Count == 0)
        {
            Verbosity.Warn(
                $"No library spectra found for query {querySpec.ScanNumber} " +
                $"(precursor m/z {querySpec.Mz.ToString("F2", CultureInfo.InvariantCulture)}).");
            return;
        }

        RunSearch(querySpec);
    }

    /// <summary>
    /// cpp <c>updateSpectrumCache</c> at SearchLibrary.cpp:135. Because <c>querySorted_</c>
    /// is always false for BlibSearch, the cpp path clears the cache every call. We follow.
    /// </summary>
    private void UpdateSpectrumCache(double queryMz, PeakProcessor processor)
    {
        _cachedSpectra.Clear();
        _cachedDecoySpectra.Clear();

        var minMz = queryMz - MzWindow;
        var maxMz = queryMz + MzWindow;
        GetLibrarySpec(minMz, maxMz, processor);
    }

    /// <summary>
    /// cpp <c>getLibrarySpec</c> at SearchLibrary.cpp:229. Pull all candidates in the m/z
    /// window from each library, assign libId, and process each candidate's peaks.
    /// </summary>
    private void GetLibrarySpec(double minMz, double maxMz, PeakProcessor processor)
    {
        for (var libIdx = 0; libIdx < _libraries.Count; libIdx++)
        {
            // cpp parity: SearchLibrary.cpp:234 — libIndex is 1-based; 0 reserved for decoys.
            var libIndex = libIdx + 1;
            var startIdx = _cachedSpectra.Count;

            // cpp parity: SearchLibrary.cpp:239 — minPeaks param hard-coded to 5 in cpp
            // ("TODO add a min-peaks option"). We use MinPeakSize (5) too.
            var candidates = _libraries[libIdx].GetSpecInMzRange(minMz, maxMz, MinPeakSize);
            _cachedSpectra.AddRange(candidates);

            Verbosity.Comment(VerbosityLevel.Detail,
                $"Found {_cachedSpectra.Count - startIdx} spec between " +
                $"{minMz.ToString("F2", CultureInfo.InvariantCulture)} and " +
                $"{maxMz.ToString("F2", CultureInfo.InvariantCulture)}.");

            // cpp parity: SearchLibrary.cpp:243-247 — for each new candidate, set libId,
            // run PeakProcessor.
            for (var s = startIdx; s < _cachedSpectra.Count; s++)
            {
                var curSpec = _cachedSpectra[s];
                curSpec.LibId = libIndex;
                processor.ProcessPeaks(curSpec);
            }

            // cpp parity: SearchLibrary.cpp:251-261 — generate decoys after the target
            // spectra are added; process their peaks if we shifted the raw peak list.
            if (DecoysPerTarget > 0)
            {
                Verbosity.Debug("Generating decoy spectra.");
                // Capture the decoy-list count BEFORE generation so the peak-processing loop
                // iterates exactly the decoys this library produced. Using `startIdx` (the
                // target-list count) is wrong as soon as NewDecoy returns null on any target
                // (too few raw peaks) or the wrapper is called for a second library, because
                // target-index and decoy-index then diverge.
                var startDecoyIdx = _cachedDecoySpectra.Count;
                GenerateDecoySpectra(startIdx);
                if (ShiftRawSpectrum)
                {
                    for (var s = startDecoyIdx; s < _cachedDecoySpectra.Count; s++)
                        processor.ProcessPeaks(_cachedDecoySpectra[s]);
                }
            }
        }
    }

    /// <summary>
    /// cpp <c>generateDecoySpectra</c> at SearchLibrary.cpp:270. Walk all targets added by the
    /// current library and push <see cref="DecoysPerTarget"/> circular-shifted copies of each.
    /// </summary>
    private void GenerateDecoySpectra(int startIndex)
    {
        var shiftMz = CircShift;
        for (var i = 0; i < DecoysPerTarget; i++)
        {
            for (var specI = startIndex; specI < _cachedSpectra.Count; specI++)
            {
                var decoy = _cachedSpectra[specI].NewDecoy(shiftMz, ShiftRawSpectrum);
                if (decoy is not null)
                    _cachedDecoySpectra.Add(decoy);
            }
            shiftMz += CircShift;
        }
    }

    /// <summary>
    /// cpp <c>runSearch</c> at SearchLibrary.cpp:324. Score every cached candidate, sort
    /// descending by dot product, assign ranks.
    /// </summary>
    private void RunSearch(Spectrum querySpec)
    {
        // cpp parity: SearchLibrary.cpp:326-327 — score targets and decoys against the query.
        ScoreMatches(querySpec, _cachedSpectra, _targetMatches);
        ScoreMatches(querySpec, _cachedDecoySpectra, _decoyMatches);

        if (_targetMatches.Count == 0)
        {
            Verbosity.Warn(
                $"No library spectra found for query {querySpec.ScanNumber} " +
                $"(precursor m/z {querySpec.Mz.ToString("F2", CultureInfo.InvariantCulture)}).");
            return;
        }

        // cpp parity: SearchLibrary.cpp:350-351 — sort targets and decoys descending.
        _targetMatches.Sort(CompareByDotScoreDesc);
        _decoyMatches.Sort(CompareByDotScoreDesc);

        // cpp parity: SearchLibrary.cpp:353 — setRank assigns 1-based ranks with ties.
        AssignRanks(_targetMatches);
        AssignRanks(_decoyMatches);
    }

    /// <summary>
    /// cpp <c>scoreMatches</c> at SearchLibrary.cpp:290. For each library candidate that
    /// (a) has processed peaks and (b) matches the query's charge state, build a Match and
    /// compute the dot product.
    /// </summary>
    private static void ScoreMatches(Spectrum query, List<RefSpectrum> spectra, List<Match> matches)
    {
        var charges = query.PossibleCharges;
        foreach (var ref_ in spectra)
        {
            // cpp parity: SearchLibrary.cpp:299-303 — skip candidates with no processed peaks.
            if (ref_.NumProcessedPeaks == 0)
            {
                Verbosity.Debug($"Skipping library spectrum {ref_.LibSpecId}.  No peaks.");
                continue;
            }
            // cpp parity: SearchLibrary.cpp:305-307 — charge filter.
            if (!CheckCharge(charges, ref_.Charge))
                continue;

            var match = new Match(query, ref_)
            {
                MatchLibId = ref_.LibId,
            };
            DotProduct.Compare(match);
            matches.Add(match);
        }
    }

    /// <summary>cpp <c>checkCharge</c> at SearchLibrary.cpp:517 — query with no charges accepts all.</summary>
    private static bool CheckCharge(IReadOnlyList<int> queryCharges, int libCharge)
    {
        if (queryCharges.Count == 0) return true;
        for (var i = 0; i < queryCharges.Count; i++)
            if (libCharge == queryCharges[i]) return true;
        return false;
    }

    /// <summary>
    /// cpp <c>compMatchDotScore</c> at SearchLibrary.cpp:462 — strict descending by dotp,
    /// ties broken by larger LibSpecId.
    /// </summary>
    private static int CompareByDotScoreDesc(Match a, Match b)
    {
        var sa = a.GetScore(MatchScoreType.Dotp);
        var sb = b.GetScore(MatchScoreType.Dotp);
        if (sa > sb) return -1;
        if (sa < sb) return 1;

        // cpp parity: SearchLibrary.cpp:467 — tie-breaker is RefSpec.LibSpecId descending.
        var la = a.RefSpec?.LibSpecId ?? 0;
        var lb = b.RefSpec?.LibSpecId ?? 0;
        if (la > lb) return -1;
        if (la < lb) return 1;
        return 0;
    }

    /// <summary>
    /// cpp <c>rank</c> at SearchLibrary.cpp:377 — assumes matches are sorted descending.
    /// Equal scores share a rank; the next distinct score gets rank+1.
    /// </summary>
    private static void AssignRanks(List<Match> matches)
    {
        if (matches.Count == 0) return;
        var curScore = matches[0].GetScore(MatchScoreType.Dotp);
        var curRank = 1;
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].GetScore(MatchScoreType.Dotp) != curScore)
            {
                curRank++;
                curScore = matches[i].GetScore(MatchScoreType.Dotp);
            }
            matches[i].Rank = curRank;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var lib in _libraries) lib.Dispose();
        _libraries.Clear();
    }
}
