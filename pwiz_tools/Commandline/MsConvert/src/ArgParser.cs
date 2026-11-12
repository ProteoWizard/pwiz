using System.Globalization;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Tools.MsConvert;

/// <summary>
/// Parses msconvert-sharp command-line args into a <see cref="MsConvertConfig"/>.
/// The switch vocabulary matches pwiz C++ <c>msconvert</c> 1:1.
/// </summary>
/// <remarks>
/// Every option is one entry in <see cref="s_options"/>, which both the parser and
/// <see cref="Usage"/> read, so an option cannot be accepted without being documented.
/// The build writes <c>--doc</c> output to pwiz_tools/Commandline/msconvert-help.txt, which the
/// ProteoWizard website shows.
/// </remarks>
internal static class ArgParser
{
    internal static MsConvertConfig Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var help = new HelpRequest();
        var tokens = Tokenize(args, help, out var error);
        // Help wins over any other problem with the command line, and comes before any file is read.
        if (help.Requested)
            throw new ArgParseHelpRequested(help);
        if (error is not null)
            throw error;

        // Config files are applied first, so an option on the command line overrides the same
        // option in a config file, as in the C++ msconvert (repeatable options such as --filter
        // add to it).
        var ordered = new List<Token>();
        foreach (var token in tokens.Where(t => ReferenceEquals(t.Option, s_configOption)))
            ordered.AddRange(ReadConfigFile(token.Value!));
        ordered.AddRange(tokens.Where(t => !ReferenceEquals(t.Option, s_configOption)));

        var config = new MsConvertConfig();
        for (int i = 0; i < ordered.Count; i++)
        {
            var token = ordered[i];
            if (token.Option is null)
                config.InputFiles.Add(token.Value!);
            // An option turned off (--gzip=false) cancels the same option turned on before it.
            else if (!token.Off && !ordered.Skip(i + 1).Any(t => t.Off && ReferenceEquals(t.Option, token.Option)))
                token.Option.Apply!(config, token.Value);
        }

        if (config.InputFiles.Count == 0)
            throw new ArgumentException("No input files specified. Use --help for usage.");
        ApplyZstd(config);
        return config;
    }

    /// <summary>One option and its value, or, with no option, an input file. <paramref name="Off"/> is an option turned off.</summary>
    private readonly record struct Token(Option? Option, string? Value, bool Off = false);

    /// <summary>
    /// Splits <paramref name="args"/> into options and input files, collecting help options into
    /// <paramref name="help"/>. The first error is returned rather than thrown, so a help option
    /// anywhere on the command line still gets help.
    /// </summary>
    private static List<Token> Tokenize(IReadOnlyList<string> args, HelpRequest help, out ArgumentException? error)
    {
        var tokens = new List<Token>();
        error = null;
        for (int i = 0; i < args.Count; i++)
        {
            string a = args[i];
            string? inlineValue = null;
            if (!s_optionsByName.TryGetValue(a, out var option))
            {
                // --name=value, as boost::program_options accepted it
                int eq = a.StartsWith("--", StringComparison.Ordinal) ? a.IndexOf('=') : -1;
                if (eq > 0 && s_optionsByName.TryGetValue(a[..eq], out option))
                {
                    inlineValue = a[(eq + 1)..];
                }
                else if (a.StartsWith('-'))
                {
                    error ??= new ArgumentException($"Unknown option: {a}");
                    continue;
                }
                else
                {
                    tokens.Add(new Token(null, a));
                    continue;
                }
            }

            try
            {
                if (!TryTakeValue(option, a, inlineValue, args, ref i, out string? value))
                {
                    if (option.Help == HelpFlag.None)
                        tokens.Add(new Token(option, null, Off: true)); // e.g. mzXML=false
                }
                else if (option.Help != HelpFlag.None)
                    help.Add(option.Help, value);
                else
                    tokens.Add(new Token(option, value));
            }
            catch (ArgumentException e)
            {
                error ??= e;
            }
        }
        return tokens;
    }

    /// <summary>
    /// The value <paramref name="option"/> takes, from <paramref name="inlineValue"/> (the text after
    /// '=') or the argument after it. Returns false when the value turns the option off.
    /// </summary>
    private static bool TryTakeValue(Option option, string name, string? inlineValue,
                                     IReadOnlyList<string> args, ref int i, out string? value)
    {
        value = null;
        switch (option.Value)
        {
            case OptionValue.Required:
                value = inlineValue ?? RequireNext(args, ref i, name);
                return true;
            case OptionValue.OptionalNumber:
                if (inlineValue is null)
                {
                    // The value is optional, so only a number that follows is taken as one.
                    if (PeekNumeric(args, i + 1, out _))
                        value = args[++i];
                    return true;
                }
                // A C++ msconvert config file could also give true or false (singleThreaded=true).
                if (IsBooleanWord(inlineValue, out bool on))
                    return on;
                value = inlineValue;
                return true;
            case OptionValue.OptionalBool:
                // Never taken from the next argument, as boost::program_options did for an implicit value.
                value = inlineValue is null || ParseBool(inlineValue, name) ? bool.TrueString : bool.FalseString;
                return true;
            default:
                return inlineValue is null || ParseBool(inlineValue, name);
        }
    }

    // ---------- option table ----------

    /// <summary>
    /// What follows an option: nothing; a value, required; a number, optional; or a boolean,
    /// optional and given only with '=' (--zlib=off).
    /// </summary>
    private enum OptionValue { None, Required, OptionalNumber, OptionalBool }

    internal enum HelpFlag { None, Help, HelpFilter, ShowExamples, Doc }

    /// <summary>One command-line option: the names it answers to, the value it takes, its help line, and its effect.</summary>
    private sealed record Option(string? Short, string Long, OptionValue Value, string Description,
                                 Action<MsConvertConfig, string?>? Apply)
    {
        /// <summary>Default shown as <c>arg (=default)</c> for an option that requires a value.</summary>
        public string? Default { get; init; }

        /// <summary>Value an <see cref="OptionValue.OptionalNumber"/> option takes when none follows it.</summary>
        public string? Implicit { get; init; }

        /// <summary>Other spellings that are accepted but not shown in help.</summary>
        public string[] Aliases { get; init; } = Array.Empty<string>();

        public HelpFlag Help { get; init; }

        /// <summary>True for an option that is accepted, but not shown in help.</summary>
        public bool Hidden { get; init; }

        /// <summary>The option's first help column, in boost::program_options style as the C++ msconvert printed it.</summary>
        public string Synopsis
        {
            get
            {
                string names = Short is null ? Long : $"{Short} [ {Long} ]";
                return Value switch
                {
                    OptionValue.Required => names + " arg" + (Default is null ? "" : $" (={Default})"),
                    OptionValue.OptionalNumber => names + $" [arg(={Implicit})]",
                    OptionValue.OptionalBool => names + $" [=arg(={Implicit})]",
                    _ => names,
                };
            }
        }
    }

    /// <summary>Names are given as "long" or "long,s", as with boost::program_options.</summary>
    private static (string? Short, string Long) SplitNames(string names)
    {
        int comma = names.IndexOf(',');
        return comma < 0 ? (null, "--" + names) : ("-" + names[(comma + 1)..], "--" + names[..comma]);
    }

    private static Option Flag(string names, string description, Action<MsConvertConfig> apply)
    {
        var (s, l) = SplitNames(names);
        return new Option(s, l, OptionValue.None, description, (c, _) => apply(c));
    }

    private static Option Arg(string names, string description, Action<MsConvertConfig, string> apply,
                              string? defaultValue = null)
    {
        var (s, l) = SplitNames(names);
        return new Option(s, l, OptionValue.Required, description, (c, v) => apply(c, v!)) { Default = defaultValue };
    }

    private static Option OptionalNumber(string names, string implicitValue, string description,
                                         Action<MsConvertConfig, string?> apply)
    {
        var (s, l) = SplitNames(names);
        return new Option(s, l, OptionValue.OptionalNumber, description, apply) { Implicit = implicitValue };
    }

    private static Option OptionalBool(string names, string description, Action<MsConvertConfig, bool> apply)
    {
        var (s, l) = SplitNames(names);
        return new Option(s, l, OptionValue.OptionalBool, description, (c, v) => apply(c, bool.Parse(v!))) { Implicit = "1" };
    }

    /// <summary>An output format the C++ msconvert wrote and this one cannot: accepted only to say so.</summary>
    private static Option Unsupported(string names)
    {
        var (s, l) = SplitNames(names);
        string reason = UnsupportedWriteFormats.TryGetReason(l, out string? r)
            ? r : throw new InvalidOperationException($"{l} is not an unsupported write format.");
        return new Option(s, l, OptionValue.None, string.Empty,
            (_, _) => throw new ArgumentException($"{l}: {reason}")) { Hidden = true };
    }

    private static Option HelpOption(string names, HelpFlag flag, string description, bool takesValue = false)
    {
        var (s, l) = SplitNames(names);
        return new Option(s, l, takesValue ? OptionValue.Required : OptionValue.None, description, null) { Help = flag };
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A tolerance as the C++ msconvert's help printed it, e.g. 2.0e-09.</summary>
    private static string Invariant(double value) => value.ToString("0.0e-00", CultureInfo.InvariantCulture);

    /// <summary>Where the help's default values come from, so they follow the code's defaults.</summary>
    private static readonly MsConvertConfig s_defaults = new();

    /// <summary>The config file option, which <see cref="Parse"/> handles itself.</summary>
    private static readonly Option s_configOption = Arg("config,c", "configuration file (optionName=value)", (_, _) => { });

    /// <summary>Every option, in the order the help lists them (the C++ msconvert's order).</summary>
    private static readonly Option[] s_options =
    {
        Arg("filelist,f", "specify text file containing filenames",
            (c, v) => LoadFilelist(v, c.InputFiles)),
        Arg("outdir,o", "set output directory ('.' for current directory)",
            (c, v) => c.OutputPath = v, defaultValue: s_defaults.OutputPath) with { Aliases = new[] { "--output-path" } },
        s_configOption,
        Arg("outfile", "Override the name of output file.",
            (c, v) => c.OutFile = v),
        Arg("ext,e", "set extension for output files [mzML|mzXML|mgf|mzMLb]",
            (c, v) => c.OutputExtension = v),

        // -------- output format --------
        Flag("mzML", "write mzML format [default]",
            c => c.WriteConfig.Format = WriteFormat.Mzml) with { Aliases = new[] { "--mzml" } },
        Flag("mzXML", "write mzXML format",
            c => c.WriteConfig.Format = WriteFormat.MzXml),
        Unsupported("mz5"),
        Flag("mzMLb", "write mzMLb format",
            c => c.WriteConfig.Format = WriteFormat.MzMLb),
        Arg("mzMLbChunkSize", "mzMLb dataset chunk size in bytes",
            (c, v) => c.WriteConfig.MzMLbChunkSize = ParseInt(v, "--mzMLbChunkSize"),
            defaultValue: Invariant(s_defaults.WriteConfig.MzMLbChunkSize)),
        Arg("mzMLbCompressionLevel", "mzMLb GZIP compression level (0-9)",
            (c, v) => c.WriteConfig.MzMLbCompressionLevel = ParseInt(v, "--mzMLbCompressionLevel"),
            defaultValue: Invariant(s_defaults.WriteConfig.MzMLbCompressionLevel)),
        Flag("mgf", "write Mascot generic format",
            c => c.WriteConfig.Format = WriteFormat.Mgf) with { Aliases = new[] { "--MGF" } },
        Unsupported("text"),
        Flag("ms1", "write MS1 format",
            c => c.WriteConfig.Format = WriteFormat.Ms1),
        Flag("bms1", "write BMS1 format",
            c => c.WriteConfig.Format = WriteFormat.Bms1),
        Flag("cms1", "write CMS1 format",
            c => c.WriteConfig.Format = WriteFormat.Cms1),
        Flag("ms2", "write MS2 format",
            c => c.WriteConfig.Format = WriteFormat.Ms2),
        Flag("bms2", "write BMS2 format",
            c => c.WriteConfig.Format = WriteFormat.Bms2),
        Flag("cms2", "write CMS2 format",
            c => c.WriteConfig.Format = WriteFormat.Cms2),
        Flag("verbose,v", "display detailed progress information",
            c => c.Verbose = true),
        Arg("verboseProgressPeriod", "number of spectra or chromatograms between progress updates with --verbose",
            (c, v) => c.VerboseProgressPeriod = ParseInt(v, "--verboseProgressPeriod"),
            defaultValue: Invariant(s_defaults.VerboseProgressPeriod)),

        // -------- binary precision --------
        Flag("64", "set default binary encoding to 64-bit precision [default]",
            c => c.WriteConfig.EncoderConfig.Precision = BinaryPrecision.Bits64) with { Aliases = new[] { "--64-bit", "--64bit" } },
        Flag("32", "set default binary encoding to 32-bit precision",
            c => c.WriteConfig.EncoderConfig.Precision = BinaryPrecision.Bits32) with { Aliases = new[] { "--32-bit", "--32bit" } },
        Flag("mz64", "encode m/z values in 64-bit precision [default]", c =>
        {
            c.WriteConfig.EncoderConfig.PrecisionOverrides[CVID.MS_m_z_array] = BinaryPrecision.Bits64;
            c.WriteConfig.EncoderConfig.PrecisionOverrides[CVID.MS_time_array] = BinaryPrecision.Bits64;
        }),
        Flag("mz32", "encode m/z values in 32-bit precision", c =>
        {
            c.WriteConfig.EncoderConfig.PrecisionOverrides[CVID.MS_m_z_array] = BinaryPrecision.Bits32;
            c.WriteConfig.EncoderConfig.PrecisionOverrides[CVID.MS_time_array] = BinaryPrecision.Bits32;
        }),
        Flag("inten64", "encode intensity values in 64-bit precision",
            c => c.WriteConfig.EncoderConfig.PrecisionOverrides[CVID.MS_intensity_array] = BinaryPrecision.Bits64),
        Flag("inten32", "encode intensity values in 32-bit precision [default]",
            c => c.WriteConfig.EncoderConfig.PrecisionOverrides[CVID.MS_intensity_array] = BinaryPrecision.Bits32),

        // -------- lossy encoding tweaks (mzMLb only; cpp's BinaryDataEncoder doesn't
        //          honor these for regular mzML output, and sharp follows suit) --------
        Arg("mzTruncation", "Number of mantissa precision bits to truncate for mz and rt, or if -1 will store integers only (mzMLb output only)", (c, v) =>
        {
            int n = ParseInt(v, "--mzTruncation");
            c.MzTruncation = n;
            c.WriteConfig.EncoderConfig.TruncationOverrides[CVID.MS_m_z_array] = n;
            c.WriteConfig.EncoderConfig.TruncationOverrides[CVID.MS_time_array] = n;
        }, defaultValue: Invariant(s_defaults.MzTruncation)),
        Arg("intenTruncation", "Number of mantissa precision bits to truncate for intensities, or if -1 will store integers only (mzMLb output only)", (c, v) =>
        {
            int n = ParseInt(v, "--intenTruncation");
            c.IntenTruncation = n;
            c.WriteConfig.EncoderConfig.TruncationOverrides[CVID.MS_intensity_array] = n;
        }, defaultValue: Invariant(s_defaults.IntenTruncation)),
        Flag("mzDelta", "apply delta prediction to mz and rt values (mzMLb output only)", c =>
        {
            c.MzDelta = true;
            c.WriteConfig.EncoderConfig.PredictionOverrides[CVID.MS_m_z_array] = BinaryPrediction.Delta;
            c.WriteConfig.EncoderConfig.PredictionOverrides[CVID.MS_time_array] = BinaryPrediction.Delta;
        }),
        Flag("intenDelta", "apply delta prediction to intensity values (mzMLb output only)", c =>
        {
            c.IntenDelta = true;
            c.WriteConfig.EncoderConfig.PredictionOverrides[CVID.MS_intensity_array] = BinaryPrediction.Delta;
        }),
        Flag("mzLinear", "apply linear prediction to mz and rt values (mzMLb output only)", c =>
        {
            c.MzLinear = true;
            c.WriteConfig.EncoderConfig.PredictionOverrides[CVID.MS_m_z_array] = BinaryPrediction.Linear;
            c.WriteConfig.EncoderConfig.PredictionOverrides[CVID.MS_time_array] = BinaryPrediction.Linear;
        }),
        Flag("intenLinear", "apply linear prediction to intensity values (mzMLb output only)", c =>
        {
            c.IntenLinear = true;
            c.WriteConfig.EncoderConfig.PredictionOverrides[CVID.MS_intensity_array] = BinaryPrediction.Linear;
        }),

        // -------- output layout and compression --------
        Flag("noindex", "do not write index",
            c => c.WriteConfig.Indexed = false),
        Arg("contactInfo,i", "filename for contact info",
            (c, v) => c.ContactInfo = v),
        OptionalBool("zlib,z", "use zlib compression for binary data [default] (add =off to disable compression)",
            (c, on) => c.WriteConfig.EncoderConfig.Compression = on ? BinaryCompression.Zlib : BinaryCompression.None),
        // Applied after parsing by ApplyZstd, so they win over --zlib wherever they appear on
        // the command line, as in cpp.
        Flag("zstd", "use Zstandard compression for binary data. This applies to all data arrays, instead of zlib, even if zlib=off is specified",
            c => c.Zstd = true),
        Flag("mzShuffleZstd", "use byte-shuffling Zstandard compression for m/z and time values",
            c => c.MzShuffleZstd = true),
        Flag("dictZstd", "use dictionary encoding followed by Zstandard compression for m/z and ion mobility values. This overrides --mzShuffleZstd",
            c => c.DictZstd = true),
        // pwiz C++ numpress flags set per-array overrides; --numpressAll combines Linear+Slof.
        OptionalNumber("numpressLinear", Invariant(s_defaults.WriteConfig.EncoderConfig.NumpressLinearErrorTolerance),
            "use numpress linear prediction compression for binary mz and rt data (relative accuracy loss will not exceed given tolerance arg, unless set to 0)", (c, v) =>
        {
            SetNumpress(c, BinaryNumpress.Linear, CVID.MS_m_z_array, CVID.MS_time_array);
            if (v is not null)
                c.WriteConfig.EncoderConfig.NumpressLinearErrorTolerance = ParseDouble(v, "--numpressLinear");
        }),
        Arg("numpressLinearAbsTol", "desired absolute tolerance for linear numpress prediction (e.g. use 1e-4 for a mass accuracy of 0.2 ppm at 500 m/z, default uses -1.0 for maximal accuracy). Note: setting this value may substantially reduce file size, this overrides relative accuracy tolerance.", (c, v) =>
        {
            double tolerance = ParseDouble(v, "--numpressLinearAbsTol");
            if (tolerance < 0)
                return; // maximal accuracy, the default
            SetNumpress(c, BinaryNumpress.Linear, CVID.MS_m_z_array, CVID.MS_time_array);
            c.WriteConfig.EncoderConfig.NumpressLinearAbsMassAcc = tolerance;
            c.WriteConfig.EncoderConfig.NumpressLinearErrorTolerance = 0;
        }),
        Flag("numpressPic", "use numpress positive integer compression for binary intensities (absolute accuracy loss will not exceed 0.5)",
            c => SetNumpress(c, BinaryNumpress.Pic, CVID.MS_intensity_array)),
        OptionalNumber("numpressSlof", Invariant(s_defaults.WriteConfig.EncoderConfig.NumpressSlofErrorTolerance),
            "use numpress short logged float compression for binary intensities (relative accuracy loss will not exceed given tolerance arg, unless set to 0)", (c, v) =>
        {
            SetNumpress(c, BinaryNumpress.Slof, CVID.MS_intensity_array);
            if (v is not null)
                c.WriteConfig.EncoderConfig.NumpressSlofErrorTolerance = ParseDouble(v, "--numpressSlof");
        }),
        Flag("numpressAll,n", "same as --numpressLinear --numpressSlof (see https://github.com/fickludd/ms-numpress for more info)", c =>
        {
            SetNumpress(c, BinaryNumpress.Linear, CVID.MS_m_z_array, CVID.MS_time_array);
            SetNumpress(c, BinaryNumpress.Slof, CVID.MS_intensity_array);
        }),
        Flag("gzip,g", "gzip entire output file (adds .gz to filename)",
            c => c.WriteConfig.Gzip = true),

        // -------- filters --------
        Arg("filter", "add a spectrum list filter",
            (c, v) => c.Filters.Add(v)),
        Arg("chromatogramFilter", "add a chromatogram list filter",
            (c, v) => c.ChromatogramFilters.Add(v)),

        // -------- run / vendor-level toggles --------
        Flag("merge", "create a single output file from multiple input files by merging file-level metadata and concatenating spectrum lists",
            c => c.Merge = true),
        // Native msconvert (boost::program_options) accepts "--runIndex" as an unambiguous
        // prefix abbreviation of "--runIndexSet"; callers such as Skyline's EncyclopeDIA path
        // pass the abbreviated form. Accept it explicitly so msconvert-sharp stays a drop-in.
        Arg("runIndexSet", "for multi-run sources, select only the specified run indices",
            (c, v) => c.RunIndexSet = v) with { Aliases = new[] { "--runIndex" } },
        Flag("simAsSpectra", "write selected ion monitoring as spectra, not chromatograms",
            c => c.SimAsSpectra = true),
        Flag("srmAsSpectra", "write selected reaction monitoring as spectra, not chromatograms",
            c => c.SrmAsSpectra = true),
        Flag("combineIonMobilitySpectra", "write all ion mobility or Waters SONAR bins/scans in a frame/block as one spectrum instead of individual spectra",
            c => c.CombineIonMobilitySpectra = true),
        Flag("ddaProcessing", "if supported by vendor; combine MS2 spectra referring to the same precursor and survey scan, and calculate accurate precursor masses.",
            c => c.DdaProcessing = true),
        Flag("ignoreCalibrationScans", "do not process calibration scans (currently only applies to Waters lockmass function)",
            c => c.IgnoreCalibrationScans = true),
        Flag("acceptZeroLengthSpectra", "some vendor readers have an efficient way of filtering out empty spectra, but it takes more time to open the file",
            c => c.AcceptZeroLengthSpectra = true),
        Flag("ignoreMissingZeroSamples", "some vendor readers do not include zero samples in their profile data; the default behavior is to add the zero samples but this option disables that",
            c => c.IgnoreMissingZeroSamples = true),
        Flag("ignoreUnknownInstrumentError", "if true, if an instrument cannot be determined from a vendor file, it will not be an error",
            c => c.IgnoreUnknownInstrumentError = true),
        Flag("stripLocationFromSourceFiles", "if true, sourceFile elements will be stripped of location information, so the same file converted from different locations will produce the same mzML",
            c => c.StripLocationFromSourceFiles = true),
        Flag("stripVersionFromSoftware", "if true, software elements will be stripped of version information, so the same file converted with different versions will produce the same mzML",
            c => c.StripVersionFromSoftware = true),
        // Argument is optional; msconvert treats bare "--singleThreaded" as =1.
        OptionalNumber("singleThreaded", "1", "if true, reading and writing spectra will be done on a single thread",
            (c, v) => c.SingleThreaded = v is null ? 1 : (int)ParseDouble(v, "--singleThreaded")),
        Flag("continueOnError", "if true, if an error is seen when enumerating a spectrum or chromatogram, it is skipped and enumeration will attempt to continue (but keep in mind a crash may follow, or the remaining data might be obviously or subtly incorrect)",
            c => c.ContinueOnError = true),

        // -------- help / docs --------
        HelpOption("help", HelpFlag.Help, "show this message, with extra detail on filter options") with { Aliases = new[] { "-h" } },
        HelpOption("help-filter", HelpFlag.HelpFilter, "name of a single filter to get detailed help for", takesValue: true),
        HelpOption("show-examples", HelpFlag.ShowExamples, "show examples of how to run msconvert.exe"),
        HelpOption("doc", HelpFlag.Doc, "writes output of --help and --show-examples, without version info"),
    };

    private static readonly Dictionary<string, Option> s_optionsByName = IndexOptions();

    private static Dictionary<string, Option> IndexOptions()
    {
        var byName = new Dictionary<string, Option>(StringComparer.Ordinal);
        foreach (var option in s_options)
        {
            foreach (string name in option.Aliases.Prepend(option.Long).Concat(option.Short is null ? Array.Empty<string>() : new[] { option.Short }))
                byName.Add(name, option); // throws on a name two options share
        }
        return byName;
    }

    /// <summary>All the option names the parser accepts, including aliases.</summary>
    internal static IEnumerable<string> OptionNames => s_optionsByName.Keys;


    /// <summary>
    /// Turns the zstd switches into encoder settings, as cpp msconvert.cpp does after parsing.
    /// </summary>
    private static void ApplyZstd(MsConvertConfig config)
    {
        var encoder = config.WriteConfig.EncoderConfig;
        // Numpress arrays take the global compression, so under --zstd they get the
        // "followed by zstd" encodings.
        if (config.Zstd)
            encoder.Compression = BinaryCompression.Zstd;

        if (config.DictZstd)
        {
            encoder.CompressionOverrides[CVID.MS_m_z_array] = BinaryCompression.DictZstd;
            encoder.CompressionOverrides[CVID.MS_time_array] = BinaryCompression.ByteShuffleZstd;
            foreach (var type in s_ionMobilityArrays)
                encoder.CompressionOverrides[type] = BinaryCompression.DictZstd;
        }
        else if (config.MzShuffleZstd)
        {
            encoder.CompressionOverrides[CVID.MS_m_z_array] = BinaryCompression.ByteShuffleZstd;
            encoder.CompressionOverrides[CVID.MS_time_array] = BinaryCompression.ByteShuffleZstd;
        }

        if (!config.DictZstd && !config.MzShuffleZstd)
            return;

        // cpp accepts both conflicts below at parse time and then fails, writing the first
        // spectrum (numpress) or reading the file back (mzMLb); report them here instead.
        if (config.WriteConfig.Format == WriteFormat.MzMLb)
            throw new ArgumentException("--mzShuffleZstd and --dictZstd are not supported for mzMLb output.");
        foreach (var (type, compression) in encoder.CompressionOverrides)
        {
            if (!compression.IsValueWise())
                continue;
            var numpress = encoder.NumpressOverrides.GetValueOrDefault(type, encoder.Numpress);
            if (numpress != BinaryNumpress.None)
            {
                throw new ArgumentException(
                    $"--mzShuffleZstd and --dictZstd cannot be combined with numpress on the same array ({CvLookup.CvTermInfo(type).Name} has {numpress}).");
            }
        }
    }

    /// <summary>The arrays cpp msconvert's --dictZstd dictionary-encodes besides m/z.</summary>
    private static readonly CVID[] s_ionMobilityArrays =
    {
        CVID.MS_ion_mobility_array,
        CVID.MS_raw_ion_mobility_drift_time_array,
        CVID.MS_mean_inverse_reduced_ion_mobility_array,
        CVID.MS_mean_ion_mobility_drift_time_array,
        CVID.MS_deconvoluted_ion_mobility_drift_time_array,
    };

    private static void SetNumpress(MsConvertConfig config, BinaryNumpress kind, params CVID[] arrayTypes)
    {
        foreach (var type in arrayTypes)
        {
            config.WriteConfig.EncoderConfig.NumpressOverrides[type] = kind;
            // Compression follows --zlib, as in pwiz C++ ("followed_by_zlib" by default); numpress
            // arrays are marked as 32-bit precision semantically.
            config.WriteConfig.EncoderConfig.PrecisionOverrides[type] = BinaryPrecision.Bits32;
        }
    }

    private static void LoadFilelist(string path, List<string> destination)
    {
        foreach (string raw in ReadLines(path, "file list"))
        {
            string trimmed = raw.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                destination.Add(trimmed);
        }
    }

    /// <summary>
    /// The options in a config file, one <c>optionName=value</c> per line, as the C++ msconvert
    /// read them. Lines starting with <c>#</c> are comments, and a name without <c>=</c> is a
    /// switch turned on.
    /// </summary>
    private static List<Token> ReadConfigFile(string path)
    {
        var args = new List<string>();
        foreach (string raw in ReadLines(path, "configuration file"))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            int eq = line.IndexOf('=');
            args.Add(eq < 0 ? "--" + line : "--" + line[..eq].Trim() + "=" + line[(eq + 1)..].Trim());
        }

        var tokens = Tokenize(args, new HelpRequest(), out var error);
        if (error is not null)
            throw new ArgumentException($"{path}: {error.Message}");
        if (tokens.Any(t => ReferenceEquals(t.Option, s_configOption)))
            throw new ArgumentException($"{path}: a configuration file cannot name another one.");
        return tokens;
    }

    private static string[] ReadLines(string path, string description)
    {
        try
        {
            return File.ReadAllLines(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException($"Unable to read {description} {path}: {e.Message}", e);
        }
    }

    /// <summary>The words boost::program_options read as a boolean.</summary>
    private static bool IsBooleanWord(string value, out bool on)
    {
        on = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
             || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        return on || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase)
               || value.Equals("off", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ParseBool(string value, string option)
    {
        if (IsBooleanWord(value, out bool on))
            return on;
        if (value is "1" or "0")
            return value == "1";
        throw new ArgumentException($"{option}: expected true or false, got '{value}'.");
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
        // An option's name is never taken as a value, so "-o --help" is -o missing its value, as in boost.
        if (i + 1 >= args.Count || IsOptionName(args[i + 1]))
            throw new ArgumentException($"{option} requires a value.");
        return args[++i];
    }

    private static bool IsOptionName(string arg)
    {
        if (s_optionsByName.ContainsKey(arg))
            return true;
        int eq = arg.StartsWith("--", StringComparison.Ordinal) ? arg.IndexOf('=') : -1;
        return eq > 0 && s_optionsByName.ContainsKey(arg[..eq]);
    }


    // ---------- help ----------

    /// <summary>Which help the command line asked for.</summary>
    internal sealed class HelpRequest
    {
        internal bool Detailed { get; private set; }
        internal bool Doc { get; private set; }
        // ReSharper disable once MemberHidesStaticFromOuterClass
        internal bool Examples { get; private set; }
        internal string? FilterName { get; private set; }

        internal bool Requested => Detailed || Doc || Examples || FilterName is not null;

        internal void Add(HelpFlag flag, string? value)
        {
            switch (flag)
            {
                case HelpFlag.Help: Detailed = true; break;
                case HelpFlag.HelpFilter: FilterName = value; break;
                case HelpFlag.ShowExamples: Examples = true; break;
                case HelpFlag.Doc: Doc = true; break;
            }
        }
    }

    /// <summary>
    /// The text printed for <paramref name="request"/>, choosing among several requests as the
    /// C++ msconvert did: --help-filter, then --show-examples (without --doc), then --doc or --help.
    /// </summary>
    /// <param name="request">The help the command line asked for.</param>
    /// <param name="maxLineLength">Wrap width for --help; zero or less means no wrapping. --doc never wraps.</param>
    internal static string HelpText(HelpRequest request, int maxLineLength = 0)
    {
        if (request.FilterName is not null)
            return SpectrumListFactory.Usage(request.FilterName) + "\n";
        if (request.Examples && !request.Doc)
            return Examples;
        if (request.Doc)
            return Usage(detailedHelp: true, writeDoc: true);
        return Usage(detailedHelp: true, maxLineLength: maxLineLength) + "\n";
    }

    /// <summary>
    /// The usage text: options, then the spectrum and chromatogram filters (named only, unless
    /// <paramref name="detailedHelp"/>), then the version block the C++ msconvert's usage ended
    /// with. <paramref name="writeDoc"/> is the --doc form: no wrapping, examples in place of the
    /// version block, so the text changes only when msconvert's options or filters do.
    /// </summary>
    internal static string Usage(bool detailedHelp = false, bool writeDoc = false, int maxLineLength = 0)
    {
        if (writeDoc)
        {
            detailedHelp = true;
            maxLineLength = 0;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("Usage: msconvert [options] <input files>\n")
          .Append("Convert mass spec data file formats.\n\n")
          .Append("Return value: 0 if every input file converted, 1 if any failed, 2 for a command-line error.\n\n");
        AppendOptions(sb, maxLineLength);
        sb.Append(SpectrumListFactory.Usage(detailedHelp,
            detailedHelp ? null : "(run this program with --help to see details for all filters)", maxLineLength));
        sb.Append(ChromatogramListFactory.Usage(detailedHelp, null, maxLineLength)).Append('\n');
        if (writeDoc)
            sb.Append('\n').Append(Examples);
        sb.Append("Questions, comments, and bug reports:\n")
          .Append("https://github.com/ProteoWizard\n")
          .Append("support@proteowizard.org\n");
        if (!writeDoc)
            sb.Append('\n').Append(VersionInfo.Block);
        return sb.ToString();
    }

    /// <summary>
    /// Width to wrap help at for a console stream, or zero (no wrapping) when the stream is
    /// redirected or there is no console.
    /// </summary>
    internal static int ConsoleWidth(bool redirected)
    {
        if (redirected)
            return 0;
        try
        {
            return Math.Max(0, Console.WindowWidth - 1);
        }
        catch (Exception e) when (e is IOException or PlatformNotSupportedException)
        {
            return 0;
        }
    }

    /// <summary>One line per option, descriptions aligned in a second column as boost::program_options printed them.</summary>
    private static void AppendOptions(System.Text.StringBuilder sb, int maxLineLength)
    {
        sb.Append("Options:\n");
        var shown = s_options.Where(o => !o.Hidden).ToList();
        int column = shown.Max(o => o.Synopsis.Length) + 3; // two-space indent, synopsis, one space
        if (maxLineLength > 0)
            column = Math.Min(column, maxLineLength / 2);
        foreach (var option in shown)
        {
            string synopsis = "  " + option.Synopsis;
            sb.Append(synopsis);
            if (synopsis.Length < column)
                sb.Append(' ', column - synopsis.Length);
            else
                sb.Append('\n').Append(' ', column);
            AppendWrapped(sb, ": " + option.Description, column, maxLineLength);
        }
    }

    /// <summary>Appends <paramref name="text"/> and a newline, continuing wrapped lines at <paramref name="indent"/>.</summary>
    private static void AppendWrapped(System.Text.StringBuilder sb, string text, int indent, int maxLineLength)
    {
        int width = maxLineLength - indent;
        if (maxLineLength <= 0 || width < 20 || text.Length <= width)
        {
            sb.Append(text).Append('\n');
            return;
        }

        int lineLength = 0;
        foreach (string word in text.Split(' '))
        {
            if (lineLength > 0 && lineLength + 1 + word.Length > width)
            {
                sb.Append('\n').Append(' ', indent);
                lineLength = 0;
            }
            if (lineLength > 0)
            {
                sb.Append(' ');
                lineLength++;
            }
            sb.Append(word);
            lineLength += word.Length;
        }
        sb.Append('\n');
    }

    /// <summary>The --show-examples text.</summary>
    private const string Examples =
        "Examples:\n" +
        "\n" +
        "# convert data.RAW to data.mzML\n" +
        "msconvert data.RAW\n" +
        "\n" +
        "# convert data.RAW to data.mzXML\n" +
        "msconvert data.RAW --mzXML\n" +
        "\n" +
        "# put output file in my_output_dir\n" +
        "msconvert data.RAW -o my_output_dir\n" +
        "\n" +
        "# combining options to create a smaller mzML file, much like the old ReAdW converter program\n" +
        "msconvert data.RAW --32 --zlib --filter \"peakPicking true 1-\" --filter \"zeroSamples removeExtra\"\n" +
        "\n" +
        "# extract scan indices 5...10 and 20...25\n" +
        "msconvert data.RAW --filter \"index [5,10] [20,25]\"\n" +
        "\n" +
        "# extract MS1 scans only\n" +
        "msconvert data.RAW --filter \"msLevel 1\"\n" +
        "\n" +
        "# extract MS2 and MS3 scans only\n" +
        "msconvert data.RAW --filter \"msLevel 2-3\"\n" +
        "\n" +
        "# extract MSn scans for n>1\n" +
        "msconvert data.RAW --filter \"msLevel 2-\"\n" +
        "\n" +
        "# apply ETD precursor mass filter\n" +
        "msconvert data.RAW --filter ETDFilter\n" +
        "\n" +
        "# remove non-flanking zero value samples\n" +
        "msconvert data.RAW --filter \"zeroSamples removeExtra\"\n" +
        "\n" +
        "# remove non-flanking zero value samples in MS2 and MS3 only\n" +
        "msconvert data.RAW --filter \"zeroSamples removeExtra 2 3\"\n" +
        "\n" +
        "# add missing zero value samples (with 5 flanking zeros) in MS2 and MS3 only\n" +
        "msconvert data.RAW --filter \"zeroSamples addMissing=5 2 3\"\n" +
        "\n" +
        "# keep only HCD spectra from a decision tree data file\n" +
        "msconvert data.RAW --filter \"activation HCD\"\n" +
        "\n" +
        "# keep the top 42 peaks or samples (depending on whether spectra are centroid or profile):\n" +
        "msconvert data.RAW --filter \"threshold count 42 most-intense\"\n" +
        "\n" +
        "# multiple filters: select scan numbers and recalculate precursors\n" +
        "msconvert data.RAW --filter \"scanNumber [500,1000]\" --filter \"precursorRefine\"\n" +
        "\n" +
        "# multiple filters: apply peak picking and then keep the bottom 100 peaks:\n" +
        "msconvert data.RAW --filter \"peakPicking true 1-\" --filter \"threshold count 100 least-intense\"\n" +
        "\n" +
        "# multiple filters: apply peak picking and then keep all peaks that are at least 50% of the intensity of the base peak:\n" +
        "msconvert data.RAW --filter \"peakPicking true 1-\" --filter \"threshold bpi-relative .5 most-intense\"\n" +
        "\n" +
        "# use a configuration file\n" +
        "msconvert data.RAW -c config.txt\n" +
        "\n" +
        "# example configuration file\n" +
        "mzXML=true\n" +
        "zlib=true\n" +
        "filter=index [3,7]\n" +
        "filter=precursorRefine\n" +
        "\n" +
        "\n";
}

/// <summary>Thrown by the parser when help is requested; the caller prints the help and exits.</summary>
internal sealed class ArgParseHelpRequested : Exception
{
    internal ArgParseHelpRequested(ArgParser.HelpRequest request) => Request = request;

    internal ArgParser.HelpRequest Request { get; }
}
