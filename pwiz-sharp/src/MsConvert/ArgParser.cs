using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Tools.MsConvert;

/// <summary>
/// Parses msconvert-sharp command-line args into a <see cref="MsConvertConfig"/>.
/// The switch vocabulary matches pwiz C++ <c>msconvert</c> 1:1.
/// </summary>
internal static class ArgParser
{
    internal static MsConvertConfig Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var config = new MsConvertConfig();

        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            switch (a)
            {
                // -------- help / docs --------
                case "-h":
                case "--help":
                case "--help-filter":
                case "--show-examples":
                case "--doc":
                    throw new ArgParseHelpRequested();

                // -------- input selection --------
                case "-f":
                case "--filelist":
                    LoadFilelist(RequireNext(args, ref i, a), config.InputFiles);
                    break;

                // -------- output location / naming --------
                case "-o":
                case "--outdir":
                case "--output-path":
                    config.OutputPath = RequireNext(args, ref i, a);
                    break;

                case "--outfile":
                    config.OutFile = RequireNext(args, ref i, a);
                    break;

                case "-e":
                case "--ext":
                    config.OutputExtension = RequireNext(args, ref i, a);
                    break;

                case "-c":
                case "--config":
                    LoadConfigFile(RequireNext(args, ref i, a), config);
                    break;

                // -------- output format --------
                case "--mzML":
                case "--mzml":
                    config.Format = OutputFormat.Mzml;
                    break;
                case "--mzXML":
                    config.Format = OutputFormat.MzXml;
                    break;
                case "--mz5":
                    config.Format = OutputFormat.Mz5;
                    break;
                case "--mzMLb":
                    config.Format = OutputFormat.MzMLb;
                    break;
                case "--mgf":
                case "--MGF":
                    config.Format = OutputFormat.Mgf;
                    break;
                case "--text":
                    config.Format = OutputFormat.Text;
                    break;
                case "--ms1":
                    config.Format = OutputFormat.Ms1;
                    break;
                case "--cms1":
                    config.Format = OutputFormat.Cms1;
                    break;
                case "--ms2":
                    config.Format = OutputFormat.Ms2;
                    break;
                case "--cms2":
                    config.Format = OutputFormat.Cms2;
                    break;

                case "--mzMLbChunkSize":
                    config.MzMLbChunkSize = ParseInt(RequireNext(args, ref i, a), a);
                    break;
                case "--mzMLbCompressionLevel":
                    config.MzMLbCompressionLevel = ParseInt(RequireNext(args, ref i, a), a);
                    break;

                // -------- filters --------
                case "--filter":
                    config.Filters.Add(RequireNext(args, ref i, a));
                    break;

                case "--chromatogramFilter":
                    config.ChromatogramFilters.Add(RequireNext(args, ref i, a));
                    break;

                // -------- binary precision --------
                case "--32":
                case "--32-bit":
                case "--32bit":
                    config.EncoderConfig.Precision = BinaryPrecision.Bits32;
                    break;

                case "--64":
                case "--64-bit":
                case "--64bit":
                    config.EncoderConfig.Precision = BinaryPrecision.Bits64;
                    break;

                case "--mz32":
                    config.EncoderConfig.PrecisionOverrides[CVID.MS_m_z_array] = BinaryPrecision.Bits32;
                    config.EncoderConfig.PrecisionOverrides[CVID.MS_time_array] = BinaryPrecision.Bits32;
                    break;

                case "--mz64":
                    config.EncoderConfig.PrecisionOverrides[CVID.MS_m_z_array] = BinaryPrecision.Bits64;
                    config.EncoderConfig.PrecisionOverrides[CVID.MS_time_array] = BinaryPrecision.Bits64;
                    break;

                case "--inten32":
                    config.EncoderConfig.PrecisionOverrides[CVID.MS_intensity_array] = BinaryPrecision.Bits32;
                    break;

                case "--inten64":
                    config.EncoderConfig.PrecisionOverrides[CVID.MS_intensity_array] = BinaryPrecision.Bits64;
                    break;

                // -------- lossy encoding tweaks (unimplemented, config-only) --------
                case "--mzTruncation":
                    config.MzTruncation = ParseInt(RequireNext(args, ref i, a), a);
                    break;
                case "--intenTruncation":
                    config.IntenTruncation = ParseInt(RequireNext(args, ref i, a), a);
                    break;
                case "--mzDelta":
                    config.MzDelta = true;
                    break;
                case "--intenDelta":
                    config.IntenDelta = true;
                    break;
                case "--mzLinear":
                    config.MzLinear = true;
                    break;
                case "--intenLinear":
                    config.IntenLinear = true;
                    break;

                // -------- compression --------
                case "-z":
                case "--zlib":
                    config.EncoderConfig.Compression = BinaryCompression.Zlib;
                    break;

                // pwiz C++ numpress flags set per-array overrides; --numpressAll combines Linear+Slof.
                case "--numpressLinear":
                    SetNumpress(config, BinaryNumpress.Linear, CVID.MS_m_z_array, CVID.MS_time_array);
                    // Optional tolerance value attached to the switch.
                    if (PeekNumeric(args, i + 1, out double linTol)) { config.EncoderConfig.NumpressLinearErrorTolerance = linTol; i++; }
                    break;

                case "--numpressLinearAbsTol":
                    // Absolute tolerance is an alternative way to specify linear numpress precision.
                    // We stash it as the linear tolerance; the encoder fallback still compares relatively.
                    // This mirrors pwiz's msconvert flag which is really an input to optimalLinearFixedPointMass.
                    config.EncoderConfig.NumpressLinearErrorTolerance = ParseDouble(RequireNext(args, ref i, a), a);
                    break;

                case "--numpressPic":
                    SetNumpress(config, BinaryNumpress.Pic, CVID.MS_intensity_array);
                    break;

                case "--numpressSlof":
                    SetNumpress(config, BinaryNumpress.Slof, CVID.MS_intensity_array);
                    if (PeekNumeric(args, i + 1, out double slofTol)) { config.EncoderConfig.NumpressSlofErrorTolerance = slofTol; i++; }
                    break;

                case "-n":
                case "--numpressAll":
                    SetNumpress(config, BinaryNumpress.Linear, CVID.MS_m_z_array, CVID.MS_time_array);
                    SetNumpress(config, BinaryNumpress.Slof, CVID.MS_intensity_array);
                    break;

                // -------- output layout --------
                case "--noindex":
                    config.NoIndex = true;
                    break;

                case "-g":
                case "--gzip":
                    config.Gzip = true;
                    break;

                case "-i":
                case "--contactInfo":
                    config.ContactInfo = RequireNext(args, ref i, a);
                    break;

                // -------- run / vendor-level toggles --------
                case "--merge":
                    config.Merge = true;
                    break;

                case "--runIndexSet":
                    config.RunIndexSet = RequireNext(args, ref i, a);
                    break;

                case "--simAsSpectra":
                    config.SimAsSpectra = true;
                    break;
                case "--srmAsSpectra":
                    config.SrmAsSpectra = true;
                    break;
                case "--combineIonMobilitySpectra":
                    config.CombineIonMobilitySpectra = true;
                    break;
                case "--ddaProcessing":
                    config.DdaProcessing = true;
                    break;
                case "--ignoreCalibrationScans":
                    config.IgnoreCalibrationScans = true;
                    break;
                case "--acceptZeroLengthSpectra":
                    config.AcceptZeroLengthSpectra = true;
                    break;
                case "--ignoreMissingZeroSamples":
                    config.IgnoreMissingZeroSamples = true;
                    break;
                case "--ignoreUnknownInstrumentError":
                    config.IgnoreUnknownInstrumentError = true;
                    break;
                case "--stripLocationFromSourceFiles":
                    config.StripLocationFromSourceFiles = true;
                    break;
                case "--stripVersionFromSoftware":
                    config.StripVersionFromSoftware = true;
                    break;

                case "--singleThreaded":
                    // Argument is optional; msconvert treats bare "--singleThreaded" as =1.
                    if (PeekNumeric(args, i + 1, out double st))
                    { config.SingleThreaded = (int)st; i++; }
                    else config.SingleThreaded = 1;
                    break;

                case "--continueOnError":
                    config.ContinueOnError = true;
                    break;

                case "-v":
                case "--verbose":
                    config.Verbose = true;
                    break;

                default:
                    if (a.StartsWith('-'))
                        throw new ArgumentException($"Unknown option: {a}");
                    config.InputFiles.Add(a);
                    break;
            }
        }

        if (config.InputFiles.Count == 0)
            throw new ArgumentException("No input files specified. Use --help for usage.");

        return config;
    }

    private static void SetNumpress(MsConvertConfig config, BinaryNumpress kind, params CVID[] arrayTypes)
    {
        foreach (var type in arrayTypes)
        {
            config.EncoderConfig.NumpressOverrides[type] = kind;
            // pwiz C++ stacks numpress with zlib by default (the combined "followed_by_zlib" CV term)
            // and marks numpress arrays as 32-bit precision semantically.
            config.EncoderConfig.CompressionOverrides[type] = BinaryCompression.Zlib;
            config.EncoderConfig.PrecisionOverrides[type] = BinaryPrecision.Bits32;
        }
    }

    private static void LoadFilelist(string path, List<string> destination)
    {
        foreach (string raw in File.ReadAllLines(path))
        {
            string trimmed = raw.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                destination.Add(trimmed);
        }
    }

    /// <summary>
    /// Reads a <c>key=value</c> config file and replays each entry through <see cref="Parse"/>.
    /// Lines starting with <c>#</c> are comments; bare switches (without <c>=</c>) are allowed.
    /// </summary>
    private static void LoadConfigFile(string path, MsConvertConfig target)
    {
        var replay = new List<string>();
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) { replay.Add("--" + line); continue; }
            string key = line[..eq].Trim();
            string val = line[(eq + 1)..].Trim();
            replay.Add("--" + key);
            if (val.Length > 0) replay.Add(val);
        }
        // Add a sentinel input so nested Parse doesn't error on "no inputs"; discard after.
        replay.Add("__configfile_placeholder__");
        var parsed = Parse(replay);
        foreach (var f in parsed.InputFiles)
            if (f != "__configfile_placeholder__") target.InputFiles.Add(f);
        target.Filters.AddRange(parsed.Filters);
        target.ChromatogramFilters.AddRange(parsed.ChromatogramFilters);
        // Scalar option merge: copy fields set by the nested parse onto target.
        MergeScalar(parsed, target);
    }

    private static void MergeScalar(MsConvertConfig src, MsConvertConfig dest)
    {
        // Paths and format.
        if (src.OutputPath != ".") dest.OutputPath = src.OutputPath;
        if (src.OutFile is not null) dest.OutFile = src.OutFile;
        if (src.OutputExtension is not null) dest.OutputExtension = src.OutputExtension;
        if (src.Format != OutputFormat.Mzml) dest.Format = src.Format;

        // Binary encoding — the nested Parse recreated a fresh BinaryEncoderConfig, so we only
        // copy non-default values over.
        var s = src.EncoderConfig; var d = dest.EncoderConfig;
        if (s.Precision != BinaryPrecision.Bits64) d.Precision = s.Precision;
        if (s.Compression != BinaryCompression.None) d.Compression = s.Compression;
        if (s.Numpress != BinaryNumpress.None) d.Numpress = s.Numpress;
        foreach (var kv in s.PrecisionOverrides) d.PrecisionOverrides[kv.Key] = kv.Value;
        foreach (var kv in s.CompressionOverrides) d.CompressionOverrides[kv.Key] = kv.Value;
        foreach (var kv in s.NumpressOverrides) d.NumpressOverrides[kv.Key] = kv.Value;

        // Boolean toggles copy through directly.
        dest.Gzip |= src.Gzip;
        dest.NoIndex |= src.NoIndex;
        dest.Merge |= src.Merge;
        dest.SimAsSpectra |= src.SimAsSpectra;
        dest.SrmAsSpectra |= src.SrmAsSpectra;
        dest.CombineIonMobilitySpectra |= src.CombineIonMobilitySpectra;
        dest.DdaProcessing |= src.DdaProcessing;
        dest.IgnoreCalibrationScans |= src.IgnoreCalibrationScans;
        dest.AcceptZeroLengthSpectra |= src.AcceptZeroLengthSpectra;
        dest.IgnoreMissingZeroSamples |= src.IgnoreMissingZeroSamples;
        dest.IgnoreUnknownInstrumentError |= src.IgnoreUnknownInstrumentError;
        dest.StripLocationFromSourceFiles |= src.StripLocationFromSourceFiles;
        dest.StripVersionFromSoftware |= src.StripVersionFromSoftware;
        dest.ContinueOnError |= src.ContinueOnError;
        dest.Verbose |= src.Verbose;
        if (src.SingleThreaded > 0) dest.SingleThreaded = src.SingleThreaded;
        if (src.RunIndexSet is not null) dest.RunIndexSet = src.RunIndexSet;
        if (src.ContactInfo is not null) dest.ContactInfo = src.ContactInfo;
    }

    private static bool PeekNumeric(IReadOnlyList<string> args, int idx, out double value)
    {
        value = 0;
        if (idx >= args.Count) return false;
        return double.TryParse(args[idx], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static int ParseInt(string s, string option)
    {
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            throw new ArgumentException($"{option}: expected integer, got '{s}'.");
        return v;
    }

    private static double ParseDouble(string s, string option)
    {
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            throw new ArgumentException($"{option}: expected floating-point, got '{s}'.");
        return v;
    }

    private static string RequireNext(IReadOnlyList<string> args, ref int i, string option)
    {
        if (i + 1 >= args.Count)
            throw new ArgumentException($"{option} requires a value.");
        return args[++i];
    }

    private static readonly string[] s_usageLines =
    {
        "Usage: msconvert-sharp [options] <input files...>",
        "",
        "Input / output:",
        "  -f, --filelist FILE      Read input file paths from FILE (one per line)",
        "  -o, --outdir DIR         Output directory (default: current)",
        "  --outfile FILE           Output filename (overrides derived name)",
        "  -e, --ext EXT            Output extension (default derived from format)",
        "  -c, --config FILE        Read key=value options from FILE",
        "",
        "Output format:",
        "  --mzML                   mzML 1.1 (default)",
        "  --mgf                    Mascot Generic Format",
        "  --mzXML, --mz5, --mzMLb, --text, --ms1, --cms1, --ms2, --cms2 (unimplemented)",
        "",
        "Filters:",
        "  --filter \"SPEC\"          Add spectrum filter (repeatable)",
        "  --chromatogramFilter S   Add chromatogram filter (repeatable)",
        "",
        "Binary encoding:",
        "  --32 / --64              Default precision (default: 64-bit)",
        "  --mz32 / --mz64          Per-array precision for m/z + retention-time",
        "  --inten32 / --inten64    Per-array precision for intensity",
        "  -z, --zlib               zlib-compress binary arrays",
        "  --numpressLinear[=tol]   Numpress Linear (m/z / retention time)",
        "  --numpressPic            Numpress Pic (integer intensities)",
        "  --numpressSlof[=tol]     Numpress Slof (log-scaled intensities)",
        "  -n, --numpressAll        --numpressLinear + --numpressSlof",
        "",
        "Output options:",
        "  -g, --gzip               gzip final output file",
        "  --noindex                Don't write indexedmzML wrapper",
        "  -i, --contactInfo FILE   Contact info file",
        "  --merge                  Combine all inputs into a single output",
        "",
        "Vendor toggles:",
        "  --simAsSpectra           Emit SIM scans as spectra",
        "  --srmAsSpectra           Emit SRM scans as spectra",
        "  --combineIonMobilitySpectra",
        "  --ddaProcessing",
        "  --ignoreCalibrationScans",
        "  --acceptZeroLengthSpectra",
        "  --ignoreMissingZeroSamples",
        "  --ignoreUnknownInstrumentError",
        "  --stripLocationFromSourceFiles",
        "  --stripVersionFromSoftware",
        "  --runIndexSet SET",
        "",
        "Misc:",
        "  --singleThreaded [=N]    Force single-threaded read+write",
        "  --continueOnError        Keep going when a file errors out",
        "  -v, --verbose            Verbose progress logging",
        "  -h, --help               Show this help",
    };

    /// <summary>Generates the usage text shown for --help / on parse failure.</summary>
    internal static string Usage() => string.Join('\n', s_usageLines);
}

/// <summary>Thrown by the parser when <c>--help</c> is encountered; the caller prints usage and exits.</summary>
internal sealed class ArgParseHelpRequested : Exception { }
