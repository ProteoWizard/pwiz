using System.Globalization;
using Pwiz.Analysis.Filters;
using Pwiz.Analysis.PeakFilters;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis;

/// <summary>
/// Signature of a filter-string parser — receives the argument portion (the text after the
/// filter name), the current SpectrumList, and the document's <see cref="MSData"/> if
/// available. Most filters ignore the MSData argument; <c>titleMaker</c> uses it for the
/// <c>RunId</c> / <c>SourcePath</c> placeholders, and a few cpp filters use it for source-file
/// metadata too. The simple <see cref="SpectrumListFactory.Wrap(ISpectrumList, string)"/>
/// entry point passes <c>null</c>; filters that require MSData must throw if it isn't supplied.
/// </summary>
public delegate ISpectrumList SpectrumListFilterBuilder(string args, ISpectrumList inner, MSData? msd);

/// <summary>
/// Parses msconvert-style filter strings like <c>"msLevel 2-"</c> or
/// <c>"threshold count 50 most-intense"</c> into <see cref="ISpectrumList"/> wrappers.
/// Port of pwiz::analysis::SpectrumListFactory (slimmed — registers the filters we've ported so far).
/// </summary>
public static class SpectrumListFactory
{
    private static readonly Dictionary<string, SpectrumListFilterBuilder> s_builders = BuildDefaultRegistry();

    /// <summary>All known filter names (case-insensitive).</summary>
    public static IEnumerable<string> RegisteredNames => s_builders.Keys;

    /// <summary>Registers a custom filter parser.</summary>
    /// <remarks>Existing entries with the same name are overwritten.</remarks>
    public static void Register(string name, SpectrumListFilterBuilder builder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(builder);
        s_builders[NormalizeName(name)] = builder;
    }

    /// <summary>Applies a single filter specification to <paramref name="inner"/>.</summary>
    /// <param name="inner">The list to wrap.</param>
    /// <param name="filterSpec">Filter name followed by whitespace-separated args, e.g. <c>"msLevel 2-"</c>.</param>
    public static ISpectrumList Wrap(ISpectrumList inner, string filterSpec) =>
        Wrap(inner, filterSpec, msd: null);

    /// <summary>Applies a single filter specification, providing the document's <see cref="MSData"/>
    /// for filters that need run-level metadata (e.g. <c>titleMaker</c>).</summary>
    public static ISpectrumList Wrap(ISpectrumList inner, string filterSpec, MSData? msd)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterSpec);

        string trimmed = filterSpec.TrimStart();
        int spaceIndex = trimmed.IndexOf(' ');
        string name = spaceIndex < 0 ? trimmed : trimmed[..spaceIndex];
        string args = spaceIndex < 0 ? string.Empty : trimmed[(spaceIndex + 1)..].Trim();

        if (!s_builders.TryGetValue(NormalizeName(name), out var builder))
            throw new ArgumentException($"Unknown filter '{name}'. Known: {string.Join(", ", s_builders.Keys)}");

        return builder(args, inner, msd);
    }

    /// <summary>Applies a sequence of filters in order.</summary>
    public static ISpectrumList Wrap(ISpectrumList inner, IEnumerable<string> filterSpecs)
        => Wrap(inner, filterSpecs, msd: null);

    /// <summary>Applies a sequence of filters in order, with MSData context.</summary>
    public static ISpectrumList Wrap(ISpectrumList inner, IEnumerable<string> filterSpecs, MSData? msd)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        foreach (var spec in filterSpecs)
            inner = Wrap(inner, spec, msd);
        return inner;
    }

    /// <summary>
    /// MSData-shaped overload that mirrors pwiz cpp's <c>SpectrumListFactory::wrap(MSData&amp;,
    /// const vector&lt;string&gt;&amp;)</c>. Wraps <see cref="MSData.Run"/>'s spectrum list
    /// in-place and promotes any new <c>DataProcessing</c> from filter wrappers to the
    /// document-level <see cref="MSData.DataProcessings"/> list. Returns the (possibly new)
    /// inner spectrum list — same return as the cpp factory.
    /// </summary>
    public static ISpectrumList Wrap(MSData msd, IList<string> filterSpecs)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentNullException.ThrowIfNull(filterSpecs);

        var sl = msd.Run.SpectrumList;
        if (sl is null) return null!;

        sl = Wrap(sl, filterSpecs, msd);
        msd.Run.SpectrumList = sl;

        // Promote any new DataProcessing record on the wrapped list to the top-level
        // DataProcessings list (matches Converter.ReadAndProcess in pwiz-sharp's msconvert).
        var wrappedDp = sl.DataProcessing;
        if (wrappedDp is not null)
        {
            int existing = msd.DataProcessings.FindIndex(d => d.Id == wrappedDp.Id);
            if (existing >= 0) msd.DataProcessings[existing] = wrappedDp;
            else msd.DataProcessings.Add(wrappedDp);
        }
        return sl;
    }

    private static string NormalizeName(string s) => s.Trim().ToLowerInvariant();

    private static Dictionary<string, SpectrumListFilterBuilder> BuildDefaultRegistry()
    {
        var map = new Dictionary<string, SpectrumListFilterBuilder>(StringComparer.Ordinal);
        // Identity-level predicates (cheap accept/reject without loading the spectrum).
        map["index"] = (args, inner, _) =>
            new SpectrumListFilter(inner, new IndexSetPredicate(ParseIntegerSet(args)));

        map["scannumber"] = (args, inner, _) =>
            new SpectrumListFilter(inner, new ScanNumberSetPredicate(ParseIntegerSet(args)));

        map["id"] = (args, inner, _) =>
            new SpectrumListFilter(inner, new IdSetPredicate(args.Split(',', StringSplitOptions.RemoveEmptyEntries)));

        // Predicates that need the loaded spectrum.
        map["mslevel"] = (args, inner, _) =>
            new SpectrumListFilter(inner, new MsLevelPredicate(ParseIntegerSet(args)));

        map["scantime"] = (args, inner, _) =>
        {
            var (low, high) = ParseRange(args);
            return new SpectrumListFilter(inner, new ScanTimeRangePredicate(low, high));
        };

        map["polarity"] = (args, inner, _) =>
        {
            CVID polarity = args.Trim().ToLowerInvariant() switch
            {
                "positive" or "pos" or "+" => CVID.MS_positive_scan,
                "negative" or "neg" or "-" => CVID.MS_negative_scan,
                _ => throw new ArgumentException($"polarity filter expected 'positive' or 'negative', got '{args}'"),
            };
            return new SpectrumListFilter(inner, new PolarityPredicate(polarity));
        };

        map["defaultarraylength"] = (args, inner, _) =>
            new SpectrumListFilter(inner, new DefaultArrayLengthPredicate(ParseIntegerSet(args)));

        map["chargestate"] = (args, inner, _) =>
            new SpectrumListFilter(inner, new ChargeStatePredicate(ParseIntegerSet(args)));

        map["scanevent"] = (args, inner, _) =>
            new SpectrumListFilter(inner, new ScanEventPredicate(ParseIntegerSet(args)));

        // Peak-level transforms.
        map["threshold"] = (args, inner, _) =>
        {
            var filter = ParseThresholdFilter(args);
            return new SpectrumListPeakFilter(inner, filter);
        };

        map["zerosamples"] = (args, inner, _) =>
        {
            // "zeroSamples remove"       — default behavior
            // "zeroSamples remove 1-"    — restrict to given MS levels (argument unused when "remove" alone)
            string trimmed = args.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("remove", StringComparison.OrdinalIgnoreCase))
            {
                IntegerSet? msLevels = null;
                int sp = trimmed.IndexOf(' ');
                if (sp > 0)
                {
                    msLevels = new IntegerSet();
                    msLevels.Parse(trimmed[(sp + 1)..]);
                }
                return new SpectrumListZeroSamplesFilter(inner, msLevels);
            }
            throw new ArgumentException($"zeroSamples filter expected 'remove' (add-missing mode is not yet ported); got '{args}'");
        };

        map["metadatafixer"] = (_, inner, _) => new SpectrumListMetadataFixer(inner);

        map["peakpicking"] = (args, inner, _) => ParsePeakPicking(args, inner);

        // Tier 1 ports — see cpp SpectrumListFactory.cpp jumpTable_ around line 1689.
        map["sortbyscantime"] = (_, inner, _) =>
            new SpectrumListSorter(inner, SpectrumListSorter.ByScanStartTimeKey);

        map["stripit"] = (_, inner, _) =>
            new SpectrumListFilter(inner, new StripIonTrapMs1Predicate());

        map["mzwindow"] = (args, inner, _) =>
        {
            var (low, high) = ParseRange(args);
            return new SpectrumListMzWindow(inner, low, high);
        };

        map["mzshift"] = (args, inner, _) => ParseMzShift(args, inner);

        map["mzprecursors"] = (args, inner, _) => ParseMzPrecursors(args, inner);
        map["isolationwindows"] = (args, inner, _) => ParseIsolationWindows(args, inner);
        map["isolationwidth"] = (args, inner, _) => ParseIsolationWidth(args, inner);
        map["mzpresent"] = (args, inner, _) => ParseMzPresent(args, inner);

        map["activation"] = (args, inner, _) => ParseActivation(args, inner);
        map["collisionenergy"] = (args, inner, _) => ParseCollisionEnergy(args, inner);
        // cpp registers analyzer + analyzerType as separate jump-table entries pointing at the
        // same creator (the latter is the deprecated form). Same dispatch here.
        map["analyzer"] = (args, inner, _) => ParseAnalyzerType(args, inner);
        map["analyzertype"] = (args, inner, _) => ParseAnalyzerType(args, inner);
        map["thermoscanfilter"] = (args, inner, _) => ParseThermoScanFilter(args, inner);

        map["titlemaker"] = (args, inner, msd) =>
        {
            if (msd is null)
                throw new InvalidOperationException(
                    "titleMaker filter requires MSData (run id and source path); "
                    + "invoke through SpectrumListFactory.Wrap(msd, filters) or the (inner, spec, msd) overload.");
            return new SpectrumListTitleMaker(msd, inner, args);
        };

#if !NO_VENDOR_SUPPORT
        // Vendor-specific: SpectrumList_LockmassRefiner reaches into SpectrumList_Waters'
        // lockmass-aware overloads, so it's only compiled when Waters is available. cpp's
        // jumpTable_ has the same entry; we gate it here instead of #ifdef'ing the whole file.
        map["lockmassrefiner"] = (args, inner, _) => ParseLockmassRefiner(args, inner);
#endif

        return map;
    }

#if !NO_VENDOR_SUPPORT
    private static SpectrumList_LockmassRefiner ParseLockmassRefiner(string args, ISpectrumList inner)
    {
        // cpp filterCreator_lockmassRefiner — three key=value pairs.
        double mz = double.Parse(TakeKeyValue(ref args, "mz=", "0"),
            NumberStyles.Float, CultureInfo.InvariantCulture);
        double mzNeg = double.Parse(TakeKeyValue(ref args, "mzNegIons=", "0"),
            NumberStyles.Float, CultureInfo.InvariantCulture);
        double tol = double.Parse(TakeKeyValue(ref args, "tol=", "1.0"),
            NumberStyles.Float, CultureInfo.InvariantCulture);

        args = args.Trim();
        if (!string.IsNullOrEmpty(args))
            throw new ArgumentException(
                $"lockmassRefiner: unhandled text remaining in argument string: \"{args}\"");

        if ((mz <= 0 && mzNeg <= 0) || tol <= 0)
            throw new ArgumentException("lockmassRefiner: lockmassMz and lockmassTolerance must be positive real numbers");

        if (mzNeg <= 0) mzNeg = mz;

        return new SpectrumList_LockmassRefiner(inner, mz, mzNeg, tol);
    }
#endif

    /// <summary>
    /// Parses the <c>peakPicking</c> filter argument string and returns a wrapped list.
    /// </summary>
    /// <remarks>
    /// Supported syntax (a subset of pwiz C++ <c>peakPicking</c>):
    /// <list type="bullet">
    ///   <item><c>peakPicking true [msLevels]</c> — vendor-prefer mode with LocalMaximum fallback</item>
    ///   <item><c>peakPicking false [msLevels]</c> — no vendor preference; LocalMaximum only</item>
    ///   <item><c>peakPicking vendor [msLevel=msLevels]</c> — vendor-prefer mode; throws when
    ///     the vendor list can't centroid</item>
    ///   <item><c>peakPicking cwt [msLevel=msLevels]</c> — reserved; CWT isn't ported yet,
    ///     falls back to LocalMaximum with a warning</item>
    /// </list>
    /// The <c>snr=</c> and <c>peakSpace=</c> parameters accepted by pwiz C++ are parsed but
    /// currently ignored (CWT-only tuning knobs).
    /// </remarks>
    private static SpectrumList_PeakPicker ParsePeakPicking(string args, ISpectrumList inner)
    {
        bool preferVendor = true;
        bool preferCwt = false;
        bool vendorOnly = false;
        IntegerSet msLevels = new(1, int.MaxValue);

        string trimmed = args.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            // Default: peakPicking with vendor prefer, all MS levels (C++ default).
        }
        else
        {
            string[] tokens = trimmed.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
            int msLevelsTokenIndex = 1;
            switch (tokens[0].ToLowerInvariant())
            {
                case "true": preferVendor = true; break;
                case "false": preferVendor = false; break;
                case "vendor": preferVendor = true; vendorOnly = true; break;
                case "cwt": preferVendor = false; preferCwt = true; break;
                default:
                    // No mode token; first token might already be msLevels or a key=value pair.
                    msLevelsTokenIndex = 0;
                    break;
            }

            for (int i = msLevelsTokenIndex; i < tokens.Length; i++)
            {
                string tok = tokens[i];
                if (tok.StartsWith("msLevel=", StringComparison.OrdinalIgnoreCase))
                {
                    msLevels = new IntegerSet();
                    msLevels.Parse(tok["msLevel=".Length..]);
                }
                else if (tok.StartsWith("snr=", StringComparison.OrdinalIgnoreCase) ||
                         tok.StartsWith("peakSpace=", StringComparison.OrdinalIgnoreCase))
                {
                    // CWT tuning knobs — parsed but ignored until CwtPeakDetector lands.
                    continue;
                }
                else if (i == msLevelsTokenIndex && !tok.Contains('=', StringComparison.Ordinal))
                {
                    // Bare MS-level range, e.g. "1-" or "2-3".
                    msLevels = new IntegerSet();
                    msLevels.Parse(tok);
                }
                else
                {
                    throw new ArgumentException($"peakPicking filter: unrecognized argument '{tok}'");
                }
            }
        }

        if (preferCwt)
            Console.Error.WriteLine(
                "[SpectrumListFactory] warning: CWT peak picking is not yet ported; " +
                "falling back to local-maximum peak picker.");

        IPeakDetector? algorithm = vendorOnly ? null : new LocalMaximumPeakDetector(3);
        return new SpectrumList_PeakPicker(inner, algorithm, preferVendor, msLevels);
    }

    // ---------- arg parsers ----------

    private static IntegerSet ParseIntegerSet(string args)
    {
        var s = new IntegerSet();
        s.Parse(args);
        return s;
    }

    private static readonly char[] s_rangeSeparators = { ',', '-' };
    private static readonly char[] s_whitespace = { ' ', '\t' };

    private static (double Low, double High) ParseRange(string args)
    {
        // Accepts "1.5,5.0" or "[1.5,5.0]" or "1.5-5.0"; whitespace tolerated.
        string trimmed = args.Trim('[', ']', ' ', '\t');
        string[] parts = trimmed.Split(s_rangeSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new ArgumentException($"Expected 'low,high' or 'low-high', got '{args}'");
        return (
            double.Parse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture),
            double.Parse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    private static ThresholdFilter ParseThresholdFilter(string args)
    {
        // Syntax: "<by> <threshold> [orientation] [msLevels]"
        // Example: "count 50 most-intense 2-"  or  "absolute 100"
        string[] tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            throw new ArgumentException(
                "threshold filter expects '<by> <value> [orientation] [msLevels]'");

        ThresholdingBy by = tokens[0].ToLowerInvariant() switch
        {
            "count" => ThresholdingBy.Count,
            "count-after-ties" or "counts-after-ties" => ThresholdingBy.CountAfterTies,
            "absolute" => ThresholdingBy.AbsoluteIntensity,
            "bpi-relative" or "fraction-of-base-peak" => ThresholdingBy.FractionOfBasePeakIntensity,
            "tic-relative" or "fraction-of-tic" => ThresholdingBy.FractionOfTotalIntensity,
            "tic-cutoff" or "fraction-of-tic-cutoff" => ThresholdingBy.FractionOfTotalIntensityCutoff,
            _ => throw new ArgumentException($"threshold filter unknown 'by' mode: {tokens[0]}"),
        };

        double threshold = double.Parse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture);

        var orientation = ThresholdingOrientation.MostIntense;
        int consumed = 2;
        if (tokens.Length > 2 && !char.IsDigit(tokens[2][0]))
        {
            orientation = tokens[2].ToLowerInvariant() switch
            {
                "most-intense" or "mostintense" => ThresholdingOrientation.MostIntense,
                "least-intense" or "leastintense" => ThresholdingOrientation.LeastIntense,
                _ => throw new ArgumentException($"threshold filter unknown orientation: {tokens[2]}"),
            };
            consumed = 3;
        }

        IntegerSet? msLevels = null;
        if (tokens.Length > consumed)
        {
            msLevels = new IntegerSet();
            msLevels.Parse(string.Join(' ', tokens[consumed..]));
        }

        return new ThresholdFilter(by, threshold, orientation, msLevels);
    }

    // ----- Tier 1 filter parsers (one per cpp filterCreator_*) -----

    /// <summary>Parses key=value pairs out of <paramref name="args"/> in place — strips matching
    /// tokens from the whitespace-separated list and returns the value (or default). Mirrors
    /// cpp's <c>parseKeyValuePair</c> contract used throughout SpectrumListFactory.cpp.</summary>
    private static string TakeKeyValue(ref string args, string key, string fallback)
    {
        var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                string value = tokens[i].Substring(key.Length);
                tokens.RemoveAt(i);
                args = string.Join(' ', tokens);
                return value;
            }
        }
        return fallback;
    }

    private static MZTolerance TakeMzTolerance(ref string args, MZTolerance fallback)
    {
        var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].StartsWith("mzTol=", StringComparison.OrdinalIgnoreCase))
            {
                // cpp accepts forms like "mzTol=10ppm" or "mzTol=10 ppm" — the latter is two tokens.
                string firstHalf = tokens[i].Substring("mzTol=".Length);
                string combined = firstHalf;
                if (i + 1 < tokens.Count && !tokens[i + 1].Contains('=', StringComparison.Ordinal))
                {
                    combined = firstHalf + " " + tokens[i + 1];
                    tokens.RemoveAt(i + 1);
                }
                tokens.RemoveAt(i);
                args = string.Join(' ', tokens);
                return MZTolerance.TryParse(combined, out var t) ? t : fallback;
            }
        }
        return fallback;
    }

    private static FilterMode TakeFilterMode(ref string args, FilterMode fallback)
    {
        string val = TakeKeyValue(ref args, "mode=", "");
        return val.ToLowerInvariant() switch
        {
            "" => fallback,
            "include" => FilterMode.Include,
            "exclude" => FilterMode.Exclude,
            _ => throw new ArgumentException($"mode= must be 'include' or 'exclude', got '{val}'"),
        };
    }

    private static List<double> ParseMzList(string s)
    {
        // "[1.0,2.0,3.0]" or "[ 1.0 , 2.0 , 3.0 ]"
        string trimmed = s.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
            throw new ArgumentException($"expected list of m/z values like '[100,200,300]', got '{s}'");
        var parts = trimmed[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<double>(parts.Length);
        foreach (var p in parts)
            result.Add(double.Parse(p, NumberStyles.Float, CultureInfo.InvariantCulture));
        return result;
    }

    private static SpectrumListMzShift ParseMzShift(string args, ISpectrumList inner)
    {
        // mzShift "10 ppm" [msLevels=int_set]
        string msLevelsText = TakeKeyValue(ref args, "msLevels=", "");
        if (!MZTolerance.TryParse(args, out var shift))
            throw new ArgumentException($"mzShift: unable to parse shift '{args}'");
        IntegerSet msLevels = IntegerSet.Positive;
        if (!string.IsNullOrEmpty(msLevelsText))
        {
            msLevels = new IntegerSet();
            msLevels.Parse(msLevelsText);
        }
        return new SpectrumListMzShift(inner, shift, msLevels);
    }

    private static SpectrumListFilter ParseMzPrecursors(string args, ISpectrumList inner)
    {
        var tol = TakeMzTolerance(ref args, new MZTolerance(10, MZToleranceUnits.Ppm));
        var mode = TakeFilterMode(ref args, FilterMode.Include);
        string targetText = TakeKeyValue(ref args, "target=", "selected");
        var target = targetText.ToLowerInvariant() switch
        {
            "selected" => PrecursorMzTarget.Selected,
            "isolated" => PrecursorMzTarget.Isolated,
            _ => throw new ArgumentException($"mzPrecursors target= must be 'selected' or 'isolated', got '{targetText}'"),
        };
        var mzs = ParseMzList(args);
        return new SpectrumListFilter(inner, new PrecursorMzPredicate(mzs, tol, mode, target));
    }

    private static SpectrumListFilter ParseIsolationWindows(string args, ISpectrumList inner)
    {
        var tol = TakeMzTolerance(ref args, new MZTolerance(10, MZToleranceUnits.Ppm));
        var mode = TakeFilterMode(ref args, FilterMode.Include);
        // Each window is "[low,high]"; cpp space-separates. We match one bracket-pair at a time.
        var windows = new List<(double, double)>();
        string s = args.Trim();
        while (s.Length > 0)
        {
            int open = s.IndexOf('[');
            int close = s.IndexOf(']', open + 1);
            if (open < 0 || close < 0)
                throw new ArgumentException("isolationWindows: expected list like '[123.4,234.5] [345.6,456.7]'");
            var (lo, hi) = ParseRange(s.Substring(open, close - open + 1));
            windows.Add((lo, hi));
            s = s[(close + 1)..].TrimStart();
        }
        if (windows.Count == 0)
            throw new ArgumentException("isolationWindows: expected at least one window");
        return new SpectrumListFilter(inner, new IsolationWindowPredicate(windows, tol, mode));
    }

    private static SpectrumListFilter ParseIsolationWidth(string args, ISpectrumList inner)
    {
        var tol = TakeMzTolerance(ref args, new MZTolerance(10, MZToleranceUnits.Ppm));
        var mode = TakeFilterMode(ref args, FilterMode.Include);
        var widths = ParseMzList(args);
        return new SpectrumListFilter(inner, new IsolationWidthPredicate(widths, tol, mode));
    }

    private static SpectrumListFilter ParseMzPresent(string args, ISpectrumList inner)
    {
        var tol = TakeMzTolerance(ref args, new MZTolerance(0.5, MZToleranceUnits.Mz));
        var mode = TakeFilterMode(ref args, FilterMode.Include);
        string typeArg = TakeKeyValue(ref args, "type=", "count");
        double thresh = double.Parse(TakeKeyValue(ref args, "threshold=", "10000"),
            NumberStyles.Float, CultureInfo.InvariantCulture);
        string orientArg = TakeKeyValue(ref args, "orientation=", "most-intense");
        var by = typeArg.ToLowerInvariant() switch
        {
            "count" => ThresholdingBy.Count,
            "count-after-ties" => ThresholdingBy.CountAfterTies,
            "absolute" => ThresholdingBy.AbsoluteIntensity,
            "bpi-relative" => ThresholdingBy.FractionOfBasePeakIntensity,
            "tic-relative" => ThresholdingBy.FractionOfTotalIntensity,
            "tic-cutoff" => ThresholdingBy.FractionOfTotalIntensityCutoff,
            _ => throw new ArgumentException($"mzPresent type= unknown: {typeArg}"),
        };
        var orientation = orientArg.ToLowerInvariant() switch
        {
            "most-intense" => ThresholdingOrientation.MostIntense,
            "least-intense" => ThresholdingOrientation.LeastIntense,
            _ => throw new ArgumentException($"mzPresent orientation= unknown: {orientArg}"),
        };
        var mzs = ParseMzList(args);
        return new SpectrumListFilter(inner,
            new MzPresentPredicate(tol, mzs, new ThresholdFilter(by, thresh, orientation), mode));
    }

    private static SpectrumListFilter ParseActivation(string args, ISpectrumList inner)
    {
        var mode = TakeFilterMode(ref args, FilterMode.Include);
        bool hasNoneOf = mode == FilterMode.Exclude;
        string activationName = args.Trim().ToUpperInvariant();
        var cvIDs = new HashSet<CVID>();
        // cpp filterCreator_ActivationType — see SpectrumListFactory.cpp:1391-1427.
        if (activationName == "CID")
        {
            // CID matches "neither HCD nor ETD"; achieved by inverting the hasNoneOf flag and
            // configuring the union of HCD/ETD/etc.
            hasNoneOf = !hasNoneOf;
            cvIDs.Add(CVID.MS_higher_energy_beam_type_collision_induced_dissociation);
            cvIDs.Add(CVID.MS_HCD);
            cvIDs.Add(CVID.MS_BIRD);
            cvIDs.Add(CVID.MS_ECD);
            cvIDs.Add(CVID.MS_ETD);
            cvIDs.Add(CVID.MS_IRMPD);
            cvIDs.Add(CVID.MS_PD);
            cvIDs.Add(CVID.MS_PSD);
            cvIDs.Add(CVID.MS_PQD);
            cvIDs.Add(CVID.MS_SID);
            cvIDs.Add(CVID.MS_SORI);
        }
        else if (activationName == "SA")
        {
            cvIDs.Add(CVID.MS_ETD);
            cvIDs.Add(CVID.MS_CID);
        }
        else if (activationName == "HECID") cvIDs.Add(CVID.MS_higher_energy_beam_type_collision_induced_dissociation);
        else if (activationName == "HCD") cvIDs.Add(CVID.MS_HCD);
        else if (activationName == "BIRD") cvIDs.Add(CVID.MS_BIRD);
        else if (activationName == "ECD") cvIDs.Add(CVID.MS_ECD);
        else if (activationName == "ETD") cvIDs.Add(CVID.MS_ETD);
        else if (activationName == "IRMPD") cvIDs.Add(CVID.MS_IRMPD);
        else if (activationName == "PD") cvIDs.Add(CVID.MS_PD);
        else if (activationName == "PSD") cvIDs.Add(CVID.MS_PSD);
        else if (activationName == "PQD") cvIDs.Add(CVID.MS_PQD);
        else if (activationName == "SID") cvIDs.Add(CVID.MS_SID);
        else if (activationName == "SORI") cvIDs.Add(CVID.MS_SORI);
        else throw new ArgumentException($"activation: unknown type '{activationName}'");
        return new SpectrumListFilter(inner, new ActivationTypePredicate(cvIDs, hasNoneOf));
    }

    private static SpectrumListFilter ParseCollisionEnergy(string args, ISpectrumList inner)
    {
        double low = double.Parse(TakeKeyValue(ref args, "low=", "-1"),
            NumberStyles.Float, CultureInfo.InvariantCulture);
        double high = double.Parse(TakeKeyValue(ref args, "high=", "-1"),
            NumberStyles.Float, CultureInfo.InvariantCulture);
        if (low < 0 || high < 0)
            throw new ArgumentException("collisionEnergy: low= and high= must both be specified and ≥ 0");
        bool acceptNonCID = bool.Parse(TakeKeyValue(ref args, "acceptNonCID=", "true"));
        bool acceptMissingCE = bool.Parse(TakeKeyValue(ref args, "acceptMissingCE=", "false"));
        var mode = TakeFilterMode(ref args, FilterMode.Include);
        return new SpectrumListFilter(inner,
            new CollisionEnergyPredicate(Math.Min(low, high), Math.Max(low, high),
                acceptNonCID, acceptMissingCE, mode));
    }

    private static SpectrumListFilter ParseAnalyzerType(string args, ISpectrumList inner)
    {
        // cpp filterCreator_AnalyzerType — first token is the analyzer name, optional rest is
        // an int_set restricting which MS levels the filter applies to.
        var tokens = args.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            throw new ArgumentException("analyzer: expected an analyzer name");
        string name = tokens[0].ToUpperInvariant();
        IntegerSet msLevels = IntegerSet.Positive;
        if (tokens.Length == 2 && !string.IsNullOrWhiteSpace(tokens[1]))
        {
            msLevels = new IntegerSet();
            msLevels.Parse(tokens[1]);
        }
        var cvIDs = new HashSet<CVID>();
        if (name.StartsWith("FT", StringComparison.Ordinal) || name.StartsWith("ORBI", StringComparison.Ordinal))
        {
            cvIDs.Add(CVID.MS_orbitrap);
            cvIDs.Add(CVID.MS_FT_ICR);
        }
        else if (name.StartsWith("IT", StringComparison.Ordinal)) cvIDs.Add(CVID.MS_ion_trap);
        else if (name.StartsWith("QUAD", StringComparison.Ordinal)) cvIDs.Add(CVID.MS_quadrupole);
        else if (name == "TOF") cvIDs.Add(CVID.MS_TOF);
        else throw new ArgumentException($"analyzer: invalid analyzer type '{name}'");
        return new SpectrumListFilter(inner, new AnalyzerTypePredicate(cvIDs, msLevels));
    }

    private static SpectrumListFilter ParseThermoScanFilter(string args, ISpectrumList inner)
    {
        // "<exact|contains> <include|exclude> <match string>"
        var firstSpace = args.IndexOf(' ');
        if (firstSpace < 0) throw new ArgumentException("thermoScanFilter: expected '<exact|contains> <include|exclude> <match string>'");
        string matchExactArg = args[..firstSpace].Trim().ToUpperInvariant();
        string rest = args[(firstSpace + 1)..].TrimStart();
        var secondSpace = rest.IndexOf(' ');
        if (secondSpace < 0) throw new ArgumentException("thermoScanFilter: expected '<exact|contains> <include|exclude> <match string>'");
        string includeArg = rest[..secondSpace].Trim().ToUpperInvariant();
        string matchString = rest[(secondSpace + 1)..].Trim();

        bool matchExact = matchExactArg switch
        {
            "EXACT" => true,
            "CONTAINS" => false,
            _ => throw new ArgumentException("thermoScanFilter: first arg must be 'exact' or 'contains'"),
        };
        bool inverse = includeArg switch
        {
            "INCLUDE" => false,
            "EXCLUDE" => true,
            _ => throw new ArgumentException("thermoScanFilter: second arg must be 'include' or 'exclude'"),
        };
        return new SpectrumListFilter(inner, new ThermoScanFilterPredicate(matchString, matchExact, inverse));
    }
}
