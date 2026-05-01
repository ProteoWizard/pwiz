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
/// filter name) and the current SpectrumList, returns the wrapped list.
/// </summary>
public delegate ISpectrumList SpectrumListFilterBuilder(string args, ISpectrumList inner);

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
    public static ISpectrumList Wrap(ISpectrumList inner, string filterSpec)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterSpec);

        string trimmed = filterSpec.TrimStart();
        int spaceIndex = trimmed.IndexOf(' ');
        string name = spaceIndex < 0 ? trimmed : trimmed[..spaceIndex];
        string args = spaceIndex < 0 ? string.Empty : trimmed[(spaceIndex + 1)..].Trim();

        if (!s_builders.TryGetValue(NormalizeName(name), out var builder))
            throw new ArgumentException($"Unknown filter '{name}'. Known: {string.Join(", ", s_builders.Keys)}");

        return builder(args, inner);
    }

    /// <summary>Applies a sequence of filters in order.</summary>
    public static ISpectrumList Wrap(ISpectrumList inner, IEnumerable<string> filterSpecs)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        foreach (var spec in filterSpecs)
            inner = Wrap(inner, spec);
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

        sl = Wrap(sl, filterSpecs);
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
        map["index"] = (args, inner) =>
            new SpectrumListFilter(inner, new IndexSetPredicate(ParseIntegerSet(args)));

        map["scannumber"] = (args, inner) =>
            new SpectrumListFilter(inner, new ScanNumberSetPredicate(ParseIntegerSet(args)));

        map["id"] = (args, inner) =>
            new SpectrumListFilter(inner, new IdSetPredicate(args.Split(',', StringSplitOptions.RemoveEmptyEntries)));

        // Predicates that need the loaded spectrum.
        map["mslevel"] = (args, inner) =>
            new SpectrumListFilter(inner, new MsLevelPredicate(ParseIntegerSet(args)));

        map["scantime"] = (args, inner) =>
        {
            var (low, high) = ParseRange(args);
            return new SpectrumListFilter(inner, new ScanTimeRangePredicate(low, high));
        };

        map["polarity"] = (args, inner) =>
        {
            CVID polarity = args.Trim().ToLowerInvariant() switch
            {
                "positive" or "pos" or "+" => CVID.MS_positive_scan,
                "negative" or "neg" or "-" => CVID.MS_negative_scan,
                _ => throw new ArgumentException($"polarity filter expected 'positive' or 'negative', got '{args}'"),
            };
            return new SpectrumListFilter(inner, new PolarityPredicate(polarity));
        };

        map["defaultarraylength"] = (args, inner) =>
            new SpectrumListFilter(inner, new DefaultArrayLengthPredicate(ParseIntegerSet(args)));

        map["chargestate"] = (args, inner) =>
            new SpectrumListFilter(inner, new ChargeStatePredicate(ParseIntegerSet(args)));

        map["scanevent"] = (args, inner) =>
            new SpectrumListFilter(inner, new ScanEventPredicate(ParseIntegerSet(args)));

        // Peak-level transforms.
        map["threshold"] = (args, inner) =>
        {
            var filter = ParseThresholdFilter(args);
            return new SpectrumListPeakFilter(inner, filter);
        };

        map["zerosamples"] = (args, inner) =>
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

        map["metadatafixer"] = (_, inner) => new SpectrumListMetadataFixer(inner);

        map["peakpicking"] = (args, inner) => ParsePeakPicking(args, inner);

        return map;
    }

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
}
