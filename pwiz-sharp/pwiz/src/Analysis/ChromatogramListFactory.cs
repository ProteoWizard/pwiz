using System.Globalization;
using Pwiz.Analysis.Filters;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis;

/// <summary>
/// Signature of a chromatogram-filter parser — receives the argument portion (text after the
/// filter name), the current chromatogram list, and the document's <see cref="MSData"/>.
/// Most filters ignore the MSData argument.
/// </summary>
public delegate IChromatogramList ChromatogramListFilterBuilder(string args, IChromatogramList inner, MSData? msd);

/// <summary>
/// Parses msconvert-style chromatogram-filter strings like <c>"index [0,3]"</c> into
/// <see cref="IChromatogramList"/> wrappers. Port of
/// <c>pwiz::analysis::ChromatogramListFactory</c>.
/// </summary>
/// <remarks>
/// Cpp registers two filters: <c>index</c> and <c>lockmassRefiner</c>. We mirror that set.
/// (<c>savitzkyGolay</c> is implemented as a wrapper but is not in cpp's msconvert
/// jump-table — it can still be applied programmatically via
/// <see cref="ChromatogramListSavitzkyGolaySmoother"/>.)
/// </remarks>
public static class ChromatogramListFactory
{
    private static readonly Dictionary<string, ChromatogramListFilterBuilder> s_builders = BuildDefaultRegistry();

    /// <summary>All known filter names (case-insensitive).</summary>
    public static IEnumerable<string> RegisteredNames => s_builders.Keys;

    /// <summary>Registers a custom filter parser.</summary>
    /// <remarks>Existing entries with the same name are overwritten.</remarks>
    public static void Register(string name, ChromatogramListFilterBuilder builder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(builder);
        s_builders[NormalizeName(name)] = builder;
    }

    /// <summary>Applies a single filter specification to <paramref name="inner"/>.</summary>
    public static IChromatogramList Wrap(IChromatogramList inner, string filterSpec) =>
        Wrap(inner, filterSpec, msd: null);

    /// <summary>Applies a single filter specification with MSData context.</summary>
    public static IChromatogramList Wrap(IChromatogramList inner, string filterSpec, MSData? msd)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterSpec);

        string trimmed = filterSpec.TrimStart();
        int spaceIndex = trimmed.IndexOf(' ');
        string name = spaceIndex < 0 ? trimmed : trimmed[..spaceIndex];
        string args = spaceIndex < 0 ? string.Empty : trimmed[(spaceIndex + 1)..].Trim();

        if (!s_builders.TryGetValue(NormalizeName(name), out var builder))
        {
            // cpp logs and ignores (ChromatogramListFactory.cpp:255). We throw — easier to spot
            // a typo, and the cpp behavior leaves the user wondering why the filter didn't fire.
            throw new ArgumentException(
                $"Unknown chromatogram filter '{name}'. Known: {string.Join(", ", s_builders.Keys)}");
        }
        return builder(args, inner, msd);
    }

    /// <summary>Applies a sequence of filters in order.</summary>
    public static IChromatogramList Wrap(IChromatogramList inner, IEnumerable<string> filterSpecs) =>
        Wrap(inner, filterSpecs, msd: null);

    /// <summary>Applies a sequence of filters in order, with MSData context.</summary>
    public static IChromatogramList Wrap(IChromatogramList inner, IEnumerable<string> filterSpecs, MSData? msd)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        foreach (var spec in filterSpecs)
            inner = Wrap(inner, spec, msd);
        return inner;
    }

    /// <summary>MSData-shaped overload that wraps <see cref="MSData.Run"/>'s chromatogram
    /// list in-place. Mirrors cpp <c>ChromatogramListFactory::wrap(MSData&amp;, ...)</c>.</summary>
    public static IChromatogramList? Wrap(MSData msd, IList<string> filterSpecs)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentNullException.ThrowIfNull(filterSpecs);

        var cl = msd.Run.ChromatogramList;
        if (cl is null) return null;

        cl = Wrap(cl, filterSpecs, msd);
        msd.Run.ChromatogramList = cl;

        var wrappedDp = cl.DataProcessing;
        if (wrappedDp is not null)
        {
            int existing = msd.DataProcessings.FindIndex(d => d.Id == wrappedDp.Id);
            if (existing >= 0) msd.DataProcessings[existing] = wrappedDp;
            else msd.DataProcessings.Add(wrappedDp);
        }
        return cl;
    }

    private static string NormalizeName(string s) => s.Trim().ToLowerInvariant();

    private static Dictionary<string, ChromatogramListFilterBuilder> BuildDefaultRegistry()
    {
        var map = new Dictionary<string, ChromatogramListFilterBuilder>(StringComparer.Ordinal);

        // "index [a,b] c-d ..." — same int_set syntax as the spectrum filter.
        map["index"] = (args, inner, _) =>
        {
            var set = new IntegerSet();
            set.Parse(args);
            return new ChromatogramListFilter(inner, new ChromatogramIndexSetPredicate(set));
        };

        // "lockmassRefiner mz=NNN [mzNegIons=NNN] [tol=NNN]" — Waters-only.
        map["lockmassrefiner"] = (args, inner, _) =>
        {
            double mz = ParseKey(ref args, "mz=", 0.0);
            double mzNeg = ParseKey(ref args, "mzNegIons=", mz);
            double tol = ParseKey(ref args, "tol=", 1.0);
            args = args.Trim();
            if (args.Length > 0)
                throw new ArgumentException(
                    $"[lockmassRefiner] unhandled text remaining in argument string: \"{args}\"");
            if (mz <= 0 || tol <= 0 || mzNeg <= 0)
                throw new ArgumentException("lockmassMz and lockmassTolerance must be positive real numbers");
            return new ChromatogramListLockmassRefiner(inner, mz, mzNeg, tol);
        };

        return map;
    }

    /// <summary>Parses <c>tokenName</c><c>VALUE</c> out of <paramref name="args"/>; removes the
    /// key/value substring from <paramref name="args"/> as a side effect. Returns
    /// <paramref name="defaultValue"/> if the token isn't present. Mirrors cpp's
    /// <c>parseKeyValuePair&lt;double&gt;</c>.</summary>
    private static double ParseKey(ref string args, string tokenName, double defaultValue)
    {
        int keyIndex = args.LastIndexOf(tokenName, StringComparison.Ordinal);
        if (keyIndex < 0) return defaultValue;
        int valueStart = keyIndex + tokenName.Length;
        int valueEnd = args.IndexOf(' ', valueStart);
        if (valueEnd < 0) valueEnd = args.Length;
        string valueStr = args[valueStart..valueEnd];
        if (!double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            throw new ArgumentException($"error parsing \"{valueStr}\" as value for \"{tokenName}\"; expected a real number");
        args = args.Remove(keyIndex, valueEnd - keyIndex);
        return value;
    }
}
