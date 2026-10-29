namespace Pwiz.Util.Chemistry;

/// <summary>Output normalization options for <see cref="IsotopeCalculator"/>.</summary>
[Flags]
#pragma warning disable CA1008
public enum IsotopeNormalization
{
    /// <summary>No normalization.</summary>
    None = 0,
#pragma warning restore CA1008

    /// <summary>Shift all masses so that the first (lowest) mass becomes zero.</summary>
    Mass = 1 << 0,

    /// <summary>Rescale abundances so that ∑(aᵢ²) = 1 (L2 normalization).</summary>
    Abundance = 1 << 1,
}

/// <summary>
/// Computes the isotope distribution (envelope) for a chemical <see cref="Formula"/>.
/// </summary>
/// <remarks>
/// Port of pwiz::chemistry::IsotopeCalculator. Uses pairwise convolution of per-element
/// distributions (produced by <see cref="IsotopeTable"/>) with abundance cutoff + mass precision
/// coalescing. This matches the output of the C++ version for the same parameters.
/// </remarks>
public sealed class IsotopeCalculator
{
    private readonly double _abundanceCutoff;
    private readonly double _massPrecision;
    private readonly Dictionary<ElementType, IsotopeTable> _tables = new();

    // Precomputed-table budget per element, from pwiz/chemistry/IsotopeCalculator.cpp.
    private static readonly (ElementType Element, int MaxAtoms)[] s_tableBudgets =
    {
        (ElementType.C, 5000),
        (ElementType.H, 8000),
        (ElementType.N, 1500),
        (ElementType.O, 1500),
        (ElementType.S, 50),
    };

    /// <summary>
    /// Creates a calculator with the given abundance cutoff (peaks below this contribute nothing)
    /// and mass precision (masses within this distance are coalesced into one).
    /// </summary>
    public IsotopeCalculator(double abundanceCutoff, double massPrecision)
    {
        _abundanceCutoff = abundanceCutoff;
        _massPrecision = massPrecision;
        foreach (var (element, maxAtoms) in s_tableBudgets)
        {
            var record = ElementInfo.Record(element);
            _tables[element] = new IsotopeTable(record.Isotopes, maxAtoms, abundanceCutoff);
        }
    }

    /// <summary>
    /// Returns the isotope distribution for <paramref name="formula"/>.
    /// </summary>
    /// <param name="formula">The chemical composition to expand.</param>
    /// <param name="chargeState">If non-zero, masses are converted to m/z for the given charge.</param>
    /// <param name="normalization">Optional mass/abundance normalization.</param>
    public IReadOnlyList<MassAbundance> Distribution(
        Formula formula,
        int chargeState = 0,
        IsotopeNormalization normalization = IsotopeNormalization.None)
    {
        ArgumentNullException.ThrowIfNull(formula);

        // Gather per-element distributions, then coalesce near-duplicates.
        var perElement = new List<List<MassAbundance>>();
        foreach (var (element, count) in formula.Data)
        {
            if (count <= 0) continue;
            List<MassAbundance> dist;
            if (_tables.TryGetValue(element, out var table))
            {
                dist = new List<MassAbundance>(table.Distribution(count));
            }
            else
            {
                // Fallback for elements without a precomputed table: build one on the fly.
                var record = ElementInfo.Record(element);
                var adhocTable = new IsotopeTable(record.Isotopes, count, _abundanceCutoff);
                dist = new List<MassAbundance>(adhocTable.Distribution(count));
            }
            perElement.Add(Coalesce(dist, _massPrecision));
        }

        // Convolve all element distributions into one combined envelope.
        List<MassAbundance> combined = new();
        foreach (var d in perElement)
            combined = Convolve(combined, d, _abundanceCutoff);

        combined.Sort((a, b) => a.Mass.CompareTo(b.Mass));
        var result = Coalesce(combined, _massPrecision);

        if (chargeState != 0)
        {
            for (int i = 0; i < result.Count; i++)
                result[i] = new MassAbundance(Ion.Mz(result[i].Mass, chargeState), result[i].Abundance);
        }

        if (normalization != IsotopeNormalization.None)
            Normalize(result, normalization);

        return result;
    }

    private static List<MassAbundance> Convolve(
        List<MassAbundance> a, List<MassAbundance> b, double cutoff)
    {
        if (a.Count == 0) return new List<MassAbundance>(b);
        if (b.Count == 0) return new List<MassAbundance>(a);

        var result = new List<MassAbundance>(a.Count * b.Count);
        foreach (var i in a)
        foreach (var j in b)
        {
            double abundance = i.Abundance * j.Abundance;
            if (abundance > cutoff)
                result.Add(new MassAbundance(i.Mass + j.Mass, abundance));
        }
        return result;
    }

    private static List<MassAbundance> Coalesce(List<MassAbundance> sortedByMass, double precision)
    {
        // Input assumed sorted by mass (the callers ensure this via IsotopeTable.Distribution
        // and the final pre-Coalesce sort in Distribution()).
        var result = new List<MassAbundance>();
        int i = 0;
        while (i < sortedByMass.Count)
        {
            double anchor = sortedByMass[i].Mass;
            double massTimesAb = 0, ab = 0;
            int j = i;
            while (j < sortedByMass.Count && Math.Abs(sortedByMass[j].Mass - anchor) <= precision)
            {
                ab += sortedByMass[j].Abundance;
                massTimesAb += sortedByMass[j].Abundance * sortedByMass[j].Mass;
                j++;
            }
            double mass = ab == 0 ? anchor : massTimesAb / ab;
            result.Add(new MassAbundance(mass, ab));
            i = j;
        }
        return result;
    }

    private static void Normalize(List<MassAbundance> md, IsotopeNormalization normalization)
    {
        if (md.Count == 0) return;

        double abundanceScale = 1;
        if ((normalization & IsotopeNormalization.Abundance) != 0)
        {
            double sumSquared = 0;
            foreach (var ma in md) sumSquared += ma.Abundance * ma.Abundance;
            if (sumSquared > 0) abundanceScale = Math.Sqrt(sumSquared);
        }

        double massShift = (normalization & IsotopeNormalization.Mass) != 0 ? md[0].Mass : 0;

        for (int i = 0; i < md.Count; i++)
            md[i] = new MassAbundance(md[i].Mass - massShift, md[i].Abundance / abundanceScale);
    }
}
