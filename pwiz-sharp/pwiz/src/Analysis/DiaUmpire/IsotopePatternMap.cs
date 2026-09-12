namespace Pwiz.Analysis.DiaUmpire;

/// <summary>
/// Lookup table from anchor-mass to per-isotope <see cref="MzRange"/>. Used by the
/// DIA-Umpire algorithm to score isotope-cluster consistency for a precursor candidate
/// of a given mass. Port of cpp <c>DiaUmpire::IsotopePatternMap</c>
/// (<c>typedef vector&lt;map&lt;double, MzRange&gt;&gt;</c>).
/// </summary>
/// <remarks>
/// Indexed as <c>[isotopeIndex][mass] -&gt; range</c>. <c>isotopeIndex</c> ranges from
/// 0 (mono) to <see cref="InstrumentParameter.MaxNoPeakCluster"/> - 2 inclusive.
/// The mass keys come from <see cref="IsotopePatternRange.Table"/> in steps of
/// ~50 Da from 300 to 8650.
/// </remarks>
public sealed class IsotopePatternMap
{
    private readonly SortedDictionary<double, MzRange>[] _byIsotope;

    /// <summary>Builds the map from the static <see cref="IsotopePatternRange.Table"/>.</summary>
    /// <param name="param">Driver for the isotope-count budget
    /// (<c>max(2, MaxNoPeakCluster - 1)</c>).</param>
    public IsotopePatternMap(InstrumentParameter param)
    {
        int isotopeCount = System.Math.Max(2, param.MaxNoPeakCluster - 1);
        _byIsotope = new SortedDictionary<double, MzRange>[isotopeCount];
        for (int i = 0; i < isotopeCount; i++)
        {
            var map = new SortedDictionary<double, MzRange>();
            foreach (var entry in IsotopePatternRange.Table)
                map[entry.Mass] = entry.Ranges[i];
            _byIsotope[i] = map;
        }
    }

    /// <summary>Number of isotopes covered by this map.</summary>
    public int IsotopeCount => _byIsotope.Length;

    /// <summary>Returns the per-mass map for the given isotope index.</summary>
    public SortedDictionary<double, MzRange> this[int isotopeIndex] => _byIsotope[isotopeIndex];
}
