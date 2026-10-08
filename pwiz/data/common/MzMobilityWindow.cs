namespace Pwiz.Data.Common.Chemistry;

/// <summary>
/// (m/z, mobility-bounds) tuple used as a filter on combine-IMS peaks. Port of
/// <c>pwiz::chemistry::MzMobilityWindow</c>. A null <see cref="MobilityBounds"/> means "match
/// any mobility"; a null <see cref="Mz"/> means "match any m/z" (the m/z field is currently
/// unused — pwiz C++ filters strictly on mobility for the Waters combine-IMS path).
/// </summary>
public readonly record struct MzMobilityWindow
{
    /// <summary>Optional m/z target. Currently unused for Waters combine-IMS filtering.</summary>
    public double? Mz { get; }

    /// <summary>Optional inclusive-exclusive (lower, upper) mobility-time bounds.</summary>
    public (double Lower, double Upper)? MobilityBounds { get; }

    /// <summary>Constructs a window from a center mobility + tolerance.</summary>
    public MzMobilityWindow(double mobility, double mobilityTolerance)
    {
        Mz = null;
        MobilityBounds = (mobility - mobilityTolerance, mobility + mobilityTolerance);
    }

    /// <summary>Constructs a window with explicit (lower, upper) mobility bounds.</summary>
    public MzMobilityWindow(double mzTarget, (double Lower, double Upper) mobilityBounds)
    {
        Mz = mzTarget;
        MobilityBounds = mobilityBounds;
    }

    /// <summary>
    /// True if <paramref name="mobilityValue"/> is in this window's bounds. A bounds-less
    /// window matches any mobility. Strict inequalities match pwiz C++ exactly.
    /// </summary>
    public bool MobilityValueInBounds(double mobilityValue) =>
        !MobilityBounds.HasValue
        || (MobilityBounds.Value.Lower < mobilityValue && mobilityValue < MobilityBounds.Value.Upper);

    /// <summary>
    /// True if <paramref name="mobilityValue"/> is in any of <paramref name="windows"/> (or if
    /// <paramref name="windows"/> is empty, matches anything).
    /// </summary>
    public static bool MobilityValueInBounds(IReadOnlyList<MzMobilityWindow> windows, double mobilityValue)
    {
        if (windows.Count == 0) return true;
        foreach (var w in windows)
            if (w.MobilityValueInBounds(mobilityValue)) return true;
        return false;
    }
}
