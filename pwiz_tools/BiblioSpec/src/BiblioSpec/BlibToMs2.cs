// Port of pwiz_tools/BiblioSpec/src/BlibToMs2.cpp + Ms2Writer.h.
//
// BlibToMs2 walks a .blib library via LibReader and writes each RefSpectrum
// to a Crawford-lab .ms2 text file.

using System.Globalization;
using System.Text;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Options for <see cref="BlibToMs2Runner"/>. Mirrors the boost::program_options
/// table populated by cpp <c>ParseCommandline</c> at BlibToMs2.cpp:85.
/// </summary>
/// <remarks>
/// <para>Defaults match cpp BlibToMs2.cpp:99-112 verbatim — mzPrecision=2,
/// intensityPrecision=1, modPrecision=-1 (use the <c>peptideModSeq</c> column
/// as-is rather than rebuilding from the Modifications table).</para>
/// </remarks>
public sealed class BlibToMs2Options
{
    /// <summary>Required positional argument — path to the <c>.blib</c> library.</summary>
    public string Library { get; set; } = string.Empty;

    /// <summary>
    /// Optional <c>-f / --file-name</c>: output filename. When empty, BlibToMs2 derives the
    /// output by replacing the library's extension with <c>ms2</c>. cpp BlibToMs2.cpp:56-60.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// <c>-m / --mz-precision</c>: decimal digits for peak m/z values and the Z-line mass.
    /// cpp default 2. cpp BlibToMs2.cpp:99-102.
    /// </summary>
    public int MzPrecision { get; set; } = 2;

    /// <summary>
    /// <c>-i / --intensity-precision</c>: decimal digits for peak intensities. cpp default 1.
    /// cpp BlibToMs2.cpp:104-107.
    /// </summary>
    public int IntensityPrecision { get; set; } = 1;

    /// <summary>
    /// <c>-p / --mod-precision</c>: when &gt;= 0, rebuild the modified peptide sequence from
    /// the <c>Modifications</c> table with this many decimals per mod-mass. When -1 (default),
    /// use the <c>peptideModSeq</c> column as stored. cpp BlibToMs2.cpp:109-112 and
    /// LibReader.cpp:472-502.
    /// </summary>
    public int ModPrecision { get; set; } = -1;
}

/// <summary>
/// Driver for the BlibToMs2 tool: open a <c>.blib</c>, iterate every <see cref="RefSpectrum"/>
/// via <see cref="LibReader.GetNextSpectrum"/>, and write each one as a Crawford-lab
/// <c>.ms2</c> record. Port of cpp <c>main()</c> in BlibToMs2.cpp:44 + cpp <c>Ms2Writer</c>
/// in Ms2Writer.h:39 (header-only class).
/// </summary>
/// <remarks>
/// <para>cpp Ms2Writer is a tiny header-only class — we fold it into this driver rather
/// than spinning up a separate <c>Ms2Writer.cs</c> file, since the runner is the sole
/// consumer.</para>
/// <para>Format reference: an <c>.ms2</c> record is line-oriented. Each spectrum starts with
/// an <c>S</c> line (scan-low scan-high precursor-mz), optional <c>I</c> RTime line, a
/// <c>Z</c> line (charge peptide-mass), two <c>D</c> lines (seq + modified seq), then
/// peak rows of <c>mz \t intensity</c>. The whole file is preceded by header <c>H</c>
/// lines for CreationDate, Extractor, and Library. See cpp Ms2Writer.h:73-77, 88, 102-133.</para>
/// </remarks>
public sealed class BlibToMs2Runner
{
    private readonly BlibToMs2Options _options;
    private readonly double[] _masses = new double[128];

    /// <summary>Construct a runner with the given parsed options.</summary>
    public BlibToMs2Runner(BlibToMs2Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        // cpp Ms2Writer.h:47 — `AminoAcidMasses::initializeMass(masses_, 0)` (average).
        AminoAcidMasses.InitializeMass(_masses, monoisotopic: false);
    }

    /// <summary>
    /// Run the conversion. Throws <see cref="BlibException"/> on configuration / IO error.
    /// </summary>
    /// <remarks>cpp main() at BlibToMs2.cpp:44.</remarks>
    public void Run()
    {
        // cpp parity: BlibToMs2.cpp:55 — library is required.
        if (string.IsNullOrEmpty(_options.Library))
            throw new BlibException(false, "Missing required argument 'library'.");

        // cpp parity: BlibToMs2.cpp:56-60 — derive output filename if not given.
        var ms2Name = _options.FileName;
        if (string.IsNullOrEmpty(ms2Name))
        {
            ms2Name = BlibUtils.ReplaceExtension(_options.Library, "ms2");
        }

        // cpp parity: BlibToMs2.cpp:63 status line.
        Verbosity.Status($"Opening library {_options.Library}.");
        using var library = new LibReader(_options.Library, _options.ModPrecision);

        // cpp parity: BlibToMs2.cpp:67 status line.
        Verbosity.Status($"Writing spectra to {ms2Name}.");

        // cpp parity: BlibToMs2.cpp:69-73 — open writer + write header + write library name.
        // The cpp <c>ofstream</c> is opened with default mode (truncate-on-open), no UTF8
        // BOM, LF line endings. We mirror that with a StreamWriter pinned to ASCII so we
        // don't accidentally drift to UTF-8-with-BOM on Windows.
        // cpp parity: Ms2Writer.h:67 opens with `file_.open(filename)` — default truncate.
        using var stream = new FileStream(ms2Name, FileMode.Create, FileAccess.Write, FileShare.Read);
        // cpp parity: ofstream emits LF on Linux, CRLF on Windows depending on text-mode.
        // The cpp build on Windows opens in text mode and writes \n, which becomes \r\n
        // on disk. .NET StreamWriter follows Environment.NewLine by default which is
        // \r\n on Windows and \n on Unix — matches cpp text-mode behaviour.
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        WriteFileHeader(writer);

        // cpp parity: BlibToMs2.cpp:72-73 — absolute path of library to the Comment line.
        var fullLibName = BlibUtils.GetAbsoluteFilePath(_options.Library);
        WriteLibName(writer, fullLibName);

        // cpp parity: BlibToMs2.cpp:76-80 — getNextSpectrum loop. Note cpp's GetNextSpectrum
        // returns false on the last (empty) call but still mutates spec; LibReader.cs mirrors
        // the same semantics, so the loop terminates correctly.
        while (library.GetNextSpectrum(out var spec))
        {
            WriteSpectrum(writer, spec);
        }
    }

    /// <summary>
    /// Write the two-line file header. Mirrors cpp Ms2Writer.h:73-77.
    /// </summary>
    /// <remarks>
    /// <para>cpp parity: Ms2Writer.h:74-77 uses <c>ctime(&amp;t)</c> which returns a 25-character
    /// asctime-style string with a trailing newline — e.g. <c>"Thu Aug 30 20:25:54 2018\n"</c>.
    /// The cpp stream emits <c>"H\tCreationDate\t&lt;ctime&gt;H\tExtractor\tBlibToMs2\n"</c> —
    /// because the ctime() string already ends in \n there is no <c>endl</c> between the two
    /// fields. We preserve that exact byte shape (single newline between the two header lines).</para>
    /// </remarks>
    private static void WriteFileHeader(StreamWriter writer)
    {
        // cpp parity: Ms2Writer.h:74 — ctime(&t) emits e.g. "Thu Aug 30 20:25:54 2018\n".
        // Replicate the asctime format exactly: "%a %b %e %H:%M:%S %Y" — `%e` is a
        // space-padded day-of-month (1..31). En-US invariant gets us the month/day names.
        var now = DateTime.Now;
        var asctime = string.Format(
            CultureInfo.InvariantCulture,
            "{0:ddd} {1:MMM} {2,2} {0:HH:mm:ss yyyy}",
            now,
            now,
            now.Day);

        // cpp emits: H\tCreationDate\t<ctime-with-trailing-LF>H\tExtractor\tBlibToMs2\n
        // The two H lines share the LF that ctime() included. .NET-equivalent: write the
        // CreationDate line WITHOUT a trailing newline-pair (StreamWriter.Write), then the
        // Extractor line WITH WriteLine.
        writer.Write("H\tCreationDate\t");
        writer.Write(asctime);
        // ctime's trailing \n + endl combined in cpp: a single newline between, then \n at end.
        writer.WriteLine();
        writer.WriteLine("H\tExtractor\tBlibToMs2");
    }

    /// <summary>Write the library Comment header line. cpp Ms2Writer.h:83-89.</summary>
    private static void WriteLibName(StreamWriter writer, string libName)
    {
        writer.Write("H\tComment\tLibrary\t");
        writer.WriteLine(libName);
    }

    /// <summary>
    /// Write a single spectrum: S / I / Z / D / D / peak rows. cpp Ms2Writer.h:94-137.
    /// </summary>
    /// <remarks>
    /// <para>Field separators are tabs throughout (cpp uses <c>"\t"</c> string literals between
    /// every field). Decimal precision rules:</para>
    /// <list type="bullet">
    ///   <item>S line precursor m/z: fixed-point, <c>mzPrecision</c> decimals.</item>
    ///   <item>I line RTime: default-format (non-fixed) at the cpp default precision (6); only
    ///         emitted when <c>retentionTime &gt; 0</c>.</item>
    ///   <item>Z line peptide mass: fixed-point, <c>mzPrecision</c> decimals.</item>
    ///   <item>Peak lines: m/z fixed-point at <c>mzPrecision</c>, intensity fixed-point at
    ///         <c>intensityPrecision</c>, separated by a tab.</item>
    /// </list>
    /// <para>cpp uses <c>std::cout &lt;&lt; fixed</c> for fixed-format. .NET <c>F&lt;n&gt;</c>
    /// format string is the equivalent; we pin to <see cref="CultureInfo.InvariantCulture"/>
    /// because cpp's <c>imbue</c> defaults to "C" locale.</para>
    /// </remarks>
    private void WriteSpectrum(StreamWriter writer, RefSpectrum spec)
    {
        var id = spec.LibSpecId;
        var modSeq = spec.ModifiedSequence;

        var mzFmt = "F" + _options.MzPrecision.ToString(CultureInfo.InvariantCulture);
        var intensityFmt = "F" + _options.IntensityPrecision.ToString(CultureInfo.InvariantCulture);

        // cpp parity: Ms2Writer.h:102-105
        // S\t<id>\t<id>\t<fixed mz at mzPrecision>\n
        writer.Write("S\t");
        writer.Write(id.ToString(CultureInfo.InvariantCulture));
        writer.Write('\t');
        writer.Write(id.ToString(CultureInfo.InvariantCulture));
        writer.Write('\t');
        writer.WriteLine(spec.Mz.ToString(mzFmt, CultureInfo.InvariantCulture));

        // cpp parity: Ms2Writer.h:108-114 — I\tRTime\t<default-format>
        // cpp calls `file_.unsetf(std::ios_base::floatfield)` to drop fixed and revert to
        // the default ostream precision (cpp default = 6). We reproduce by using "G" with
        // no explicit precision argument — but C# "G" default produces full round-trip
        // precision (15-17 digits), so explicitly use "G6" to match cpp's default 6.
        if (spec.RetentionTime > 0)
        {
            writer.Write("I\tRTime\t");
            writer.WriteLine(spec.RetentionTime.ToString("G6", CultureInfo.InvariantCulture));
        }

        // cpp parity: Ms2Writer.h:117-120
        // Z\t<charge>\t<fixed peptide-mass at mzPrecision>\n
        writer.Write("Z\t");
        writer.Write(spec.Charge.ToString(CultureInfo.InvariantCulture));
        writer.Write('\t');
        var pepMass = BlibUtils.GetPeptideMass(modSeq, _masses);
        writer.WriteLine(pepMass.ToString(mzFmt, CultureInfo.InvariantCulture));

        // cpp parity: Ms2Writer.h:123-124 — D\tseq\t<seq>\n and D\tmodified seq\t<modSeq>\n
        writer.Write("D\tseq\t");
        writer.WriteLine(spec.Sequence);
        writer.Write("D\tmodified seq\t");
        writer.WriteLine(modSeq);

        // cpp parity: Ms2Writer.h:127-133 — peak rows: mz\tintensity\n
        var peaks = spec.RawPeaks;
        for (var i = 0; i < peaks.Count; i++)
        {
            writer.Write(peaks[i].Mz.ToString(mzFmt, CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.WriteLine(peaks[i].Intensity.ToString(intensityFmt, CultureInfo.InvariantCulture));
        }
    }

    // ---- argv parsing -----------------------------------------------------

    /// <summary>
    /// Parse argv into a <see cref="BlibToMs2Options"/>. cpp parity: BlibToMs2.cpp:85
    /// (<c>ParseCommandline</c>) + CommandLine.cpp's boost::program_options driver.
    /// </summary>
    /// <remarks>
    /// <para>cpp accepts both long (<c>--file-name</c>) and short (<c>-f</c>) forms. We accept:</para>
    /// <list type="bullet">
    ///   <item><c>-f &lt;name&gt;</c> / <c>--file-name &lt;name&gt;</c> / <c>--file-name=&lt;name&gt;</c></item>
    ///   <item><c>-m &lt;n&gt;</c> / <c>--mz-precision &lt;n&gt;</c> / <c>--mz-precision=&lt;n&gt;</c></item>
    ///   <item><c>-i &lt;n&gt;</c> / <c>--intensity-precision &lt;n&gt;</c> / <c>--intensity-precision=&lt;n&gt;</c></item>
    ///   <item><c>-p &lt;n&gt;</c> / <c>--mod-precision &lt;n&gt;</c> / <c>--mod-precision=&lt;n&gt;</c></item>
    ///   <item><c>-h</c> / <c>--help</c></item>
    /// </list>
    /// <para>cpp also wires <c>--verbosity</c> via CommandLine.cpp:63-65; we accept it but it
    /// only flips <see cref="Verbosity.GlobalLevel"/>.</para>
    /// <para>The last non-switch argv element is the library (matching cpp positional
    /// argument <c>library</c>).</para>
    /// </remarks>
    /// <param name="argv">Argv from Main (not including argv[0]).</param>
    /// <param name="showHelp">Set to true if the caller should print usage and exit.</param>
    public static BlibToMs2Options ParseCommandArgs(string[] argv, out bool showHelp)
    {
        ArgumentNullException.ThrowIfNull(argv);
        showHelp = false;

        var opts = new BlibToMs2Options();
        var positional = new List<string>();

        for (var i = 0; i < argv.Length; i++)
        {
            var arg = argv[i];
            if (string.IsNullOrEmpty(arg))
                continue;

            // Long form with = separator: --name=value
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var eq = arg.IndexOf('=');
                string name;
                string? value;
                if (eq >= 0)
                {
                    name = arg.Substring(2, eq - 2);
                    value = arg.Substring(eq + 1);
                }
                else
                {
                    name = arg.Substring(2);
                    value = null;
                }

                switch (name)
                {
                    case "help":
                        showHelp = true;
                        return opts;

                    case "file-name":
                        opts.FileName = value ?? ConsumeValue(argv, ref i, name);
                        break;

                    case "mz-precision":
                        opts.MzPrecision = ParseInt(value ?? ConsumeValue(argv, ref i, name), name);
                        break;

                    case "intensity-precision":
                        opts.IntensityPrecision = ParseInt(value ?? ConsumeValue(argv, ref i, name), name);
                        break;

                    case "mod-precision":
                        opts.ModPrecision = ParseInt(value ?? ConsumeValue(argv, ref i, name), name);
                        break;

                    case "verbosity":
                        // cpp CommandLine.cpp:63-65 — translate string to V_LEVEL.
                        Verbosity.GlobalLevel = Verbosity.StringToLevel(value ?? ConsumeValue(argv, ref i, name));
                        break;

                    case "out":
                        // The cpp test harness sends `--out=<path>` to mean "the output filename"
                        // (Jamfile.jam:162). For BlibToMs2 this is the .ms2 path. Mirror by routing
                        // it into FileName so the harness works unmodified.
                        opts.FileName = value ?? ConsumeValue(argv, ref i, name);
                        break;

                    default:
                        throw new BlibException(false, $"Unknown option '--{name}'.");
                }
                continue;
            }

            // Short form: -X (with optional next-argv value)
            if (arg.Length >= 2 && arg[0] == '-')
            {
                var flag = arg[1];
                switch (flag)
                {
                    case 'h':
                        showHelp = true;
                        return opts;

                    case 'f':
                        opts.FileName = ConsumeShortValue(arg, argv, ref i, "file-name");
                        break;

                    case 'm':
                        opts.MzPrecision = ParseInt(ConsumeShortValue(arg, argv, ref i, "mz-precision"), "mz-precision");
                        break;

                    case 'i':
                        opts.IntensityPrecision = ParseInt(ConsumeShortValue(arg, argv, ref i, "intensity-precision"), "intensity-precision");
                        break;

                    case 'p':
                        opts.ModPrecision = ParseInt(ConsumeShortValue(arg, argv, ref i, "mod-precision"), "mod-precision");
                        break;

                    case 'v':
                        Verbosity.GlobalLevel = Verbosity.StringToLevel(ConsumeShortValue(arg, argv, ref i, "verbosity"));
                        break;

                    default:
                        throw new BlibException(false, $"Unknown switch '{arg}'.");
                }
                continue;
            }

            // Non-switch — positional argument.
            positional.Add(arg);
        }

        // cpp parity: BlibToMs2.cpp:116-120 — exactly one required positional ("library").
        if (positional.Count == 0)
        {
            throw new BlibException(false, "Missing required argument 'library'.");
        }
        if (positional.Count > 1)
        {
            // cpp boost::program_options rejects extras when `repeatLast=false` (BlibToMs2.cpp:120).
            throw new BlibException(false,
                $"Unexpected extra positional arguments: {string.Join(" ", positional.GetRange(1, positional.Count - 1))}.");
        }

        opts.Library = positional[0];
        return opts;
    }

    private static string ConsumeValue(string[] argv, ref int i, string optName)
    {
        if (i + 1 >= argv.Length)
            throw new BlibException(false, $"Option --{optName} requires a value.");
        return argv[++i];
    }

    private static string ConsumeShortValue(string arg, string[] argv, ref int i, string optName)
    {
        // Allow inline "-mVALUE" form for parity with boost::program_options. cpp accepts
        // both `-m 4` and `-m4`.
        if (arg.Length > 2)
            return arg.Substring(2);
        return ConsumeValue(argv, ref i, optName);
    }

    private static int ParseInt(string raw, string optName)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new BlibException(false, $"Option --{optName} expected an integer, got '{raw}'.");
        return result;
    }

    /// <summary>
    /// Print the cpp-style usage block to <see cref="Console.Error"/>. cpp parity:
    /// CommandLine.cpp:77-80 — usage line + options_description dump.
    /// </summary>
    public static void PrintUsage(TextWriter? writer = null)
    {
        writer ??= Console.Error;
        writer.WriteLine("Usage: BlibToMs2 [options] <library>");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -f [ --file-name ] arg                Name the output ms2 file.  Default is");
        writer.WriteLine("                                        <library name>.ms2.");
        writer.WriteLine("  -m [ --mz-precision ] arg (=2)        Precision for peak m/z printed to ms2.");
        writer.WriteLine("                                        Default 2.");
        writer.WriteLine("  -i [ --intensity-precision ] arg (=1) Precision for peak intensities.  Default");
        writer.WriteLine("                                        1.");
        writer.WriteLine("  -p [ --mod-precision ] arg (=-1)      Precision for modification masses.");
        writer.WriteLine("                                        Default -1 (use value in PeptideModSeq");
        writer.WriteLine("                                        column).");
        writer.WriteLine("  -v [ --verbosity ] arg (=status)      Control the level of output to stderr.");
        writer.WriteLine("                                        (silent, error, status, warn, debug,");
        writer.WriteLine("                                        detail, all)  Default status.");
        writer.WriteLine("  -h [ --help ]                         Print help message.");
    }
}
