namespace Pwiz.Analysis;

public static partial class ChromatogramListFactory
{
    /// <summary>
    /// Help for the filters in <see cref="BuildDefaultRegistry"/>, in the order msconvert lists
    /// them. An entry shows only while its filter is registered, as in
    /// <see cref="SpectrumListFactory"/>.
    /// </summary>
    private static readonly FilterUsage[] s_usage =
    {
        new("index", "<index_value_set>",
            "Selects chromatograms by index - an index value 0-based numerical order in which the chromatogram appears in the input.\n" +
            "  <index_value_set> is an int_set of indexes."),
        new("lockmassRefiner", "mz=<real> mzNegIons=<real (mz)> tol=<real (1.0 Daltons)>",
            "For Waters data, adjusts m/z values according to the specified lockmass m/z and tolerance. Distinct m/z value for negative ions is optional and defaults to the given mz value. For other data, currently does nothing."),
    };

    /// <summary>Help for each registered filter that has any, in the order msconvert lists them.</summary>
    public static IEnumerable<FilterUsage> FilterUsages =>
        s_usage.Where(u => s_builders.ContainsKey(NormalizeName(u.Name)));

    /// <summary>
    /// The "Chromatogram List Filters" section of msconvert's usage. Port of cpp's
    /// <c>ChromatogramListFactory::usage(bool, const char*, int)</c>.
    /// </summary>
    /// <param name="detailedHelp">Include each filter's description and the int_set explanation.</param>
    /// <param name="moreHelpPrompt">Line shown under the heading when <paramref name="detailedHelp"/> is false.</param>
    /// <param name="maxLineLength">Wrap width; zero or less means no wrapping.</param>
    public static string Usage(bool detailedHelp, string? moreHelpPrompt = null, int maxLineLength = 0) =>
        FilterUsageFormatter.Format("Chromatogram List Filters",
            "Note: Filters are applied sequentially in the order that you list them, and the sequence order " +
            "can make a large difference in your output.",
            FilterUsages, detailedHelp, moreHelpPrompt, maxLineLength);
}
