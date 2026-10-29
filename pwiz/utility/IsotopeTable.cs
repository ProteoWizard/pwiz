namespace Pwiz.Util.Chemistry;

/// <summary>
/// A precomputed multinomial-expansion table giving the isotope distribution
/// for 1..<c>maxAtomCount</c> atoms of a single element.
/// </summary>
/// <remarks>
/// Port of pwiz::chemistry::IsotopeTable.
///
/// The table is computed once at construction and queried in O(recordSize) per
/// <see cref="Distribution"/> call, where recordSize is the product of per-isotope
/// cutoff extents. Zero-abundance isotopes are stripped and the remaining distribution
/// is sorted by descending abundance (so isotope 0 is the most-abundant one).
/// </remarks>
public sealed class IsotopeTable
{
    private readonly MassAbundance[] _sorted; // by descending abundance, zero-abundance removed
    private readonly int _maxAtomCount;
    private readonly double _cutoff;
    private readonly int[] _extents;
    private readonly int _recordSize;
    private readonly double[] _table;

    /// <summary>
    /// Builds the table for an element with isotope distribution <paramref name="md"/>,
    /// precomputed up to <paramref name="maxAtomCount"/> atoms at abundance <paramref name="cutoff"/>.
    /// </summary>
    public IsotopeTable(IReadOnlyList<MassAbundance> md, int maxAtomCount, double cutoff)
    {
        ArgumentNullException.ThrowIfNull(md);
        ArgumentOutOfRangeException.ThrowIfNegative(maxAtomCount);

        _maxAtomCount = maxAtomCount;
        _cutoff = cutoff;

        // Strip zero-abundance isotopes and sort by descending abundance.
        var nonZero = md.Where(m => m.Abundance > 0).OrderByDescending(m => m.Abundance).ToArray();
        if (nonZero.Length == 0)
            throw new ArgumentException("Element has no non-zero-abundance isotopes.", nameof(md));
        _sorted = nonZero;

        // Per-dimension extent via binomial-tail cutoff for each less-abundant isotope.
        _extents = new int[_sorted.Length - 1];
        for (int i = 1; i < _sorted.Length; i++)
            _extents[i - 1] = TailCutoffPosition(_maxAtomCount, _sorted[i].Abundance, cutoff);

        _recordSize = 1;
        foreach (var e in _extents) _recordSize *= e;

        _table = new double[checked(_maxAtomCount * _recordSize)];
        ComputeTableValues();
    }

    /// <summary>
    /// Returns the isotope distribution for <paramref name="atomCount"/> atoms of the element.
    /// Zero atoms yields an empty list. Out-of-range counts throw.
    /// </summary>
    public IReadOnlyList<MassAbundance> Distribution(int atomCount)
    {
        if (atomCount < 0 || atomCount > _maxAtomCount)
            throw new ArgumentOutOfRangeException(nameof(atomCount));
        if (atomCount == 0) return Array.Empty<MassAbundance>();

        var result = new List<MassAbundance>();
        int recordIndex = atomCount - 1;
        int[] multiIndex = new int[_extents.Length];

        int start = recordIndex * _recordSize;
        for (int i = 0; i < _recordSize; i++)
        {
            double abundance = _table[start + i];
            if (abundance > _cutoff)
                result.Add(new MassAbundance(MassAt(atomCount, multiIndex), abundance));
            Increment(multiIndex);
        }

        result.Sort((a, b) => a.Mass.CompareTo(b.Mass));
        return result;
    }

    private void Increment(int[] multiIndex)
    {
        for (int i = 0; i < multiIndex.Length; i++)
        {
            if (++multiIndex[i] >= _extents[i]) multiIndex[i] = 0;
            else return;
        }
    }

    private double MassAt(int atomCount, int[] multiIndex)
    {
        // Sum of (mass × count) for each less-abundant isotope, plus the remaining
        // atoms worth of the most-abundant (isotope 0) mass.
        double result = 0;
        int atomsCounted = 0;
        for (int i = 0; i < multiIndex.Length; i++)
        {
            result += _sorted[i + 1].Mass * multiIndex[i];
            atomsCounted += multiIndex[i];
        }
        if (atomsCounted > atomCount)
            return double.NaN; // caller filters by cutoff; impossible states stay at zero abundance
        result += _sorted[0].Mass * (atomCount - atomsCounted);
        return result;
    }

    private void ComputeTableValues()
    {
        double p0 = _sorted[0].Abundance;
        int dims = _extents.Length;
        int[] multiIndex = new int[dims];

        for (int record = 0; record < _maxAtomCount; record++)
        {
            int n = record + 1;
            int start = record * _recordSize;
            Array.Clear(multiIndex);

            _table[start] = Math.Pow(p0, n);
            Increment(multiIndex);

            for (int i = 1; i < _recordSize; i++)
            {
                // Skip impossible states: sum of less-abundant counts can't exceed n.
                int sum = 0;
                for (int d = 0; d < dims; d++) sum += multiIndex[d];
                if (sum > n)
                {
                    _table[start + i] = 0;
                    Increment(multiIndex);
                    continue;
                }

                // Recursion: A(i0+1, ij-1, ...) * (pj/p0) * k0_prev/kj
                int prevDim = FirstNonzeroDimension(multiIndex);
                int delta = 1;
                for (int d = 0; d < prevDim; d++) delta *= _extents[d];
                // Predecessor has one fewer of isotope[prevDim+1], so one more of isotope[0].

                double previous = _table[start + i - delta];
                double pj = _sorted[prevDim + 1].Abundance;
                int k0_previous = n - sum + 1; // count of isotope 0 in predecessor state
                int kj = multiIndex[prevDim];

                _table[start + i] = previous * (pj / p0) * ((double)k0_previous / kj);
                Increment(multiIndex);
            }
        }
    }

    private static int FirstNonzeroDimension(int[] multiIndex)
    {
        for (int i = 0; i < multiIndex.Length; i++)
            if (multiIndex[i] != 0) return i;
        throw new InvalidOperationException("multiIndex is zero");
    }

    /// <summary>
    /// Returns the smallest k at which Pr(K=k) first drops below <paramref name="cutoff"/>,
    /// where K ~ Binomial(n, p). Matches pwiz's <c>tailCutoffPosition</c>.
    /// </summary>
    private static int TailCutoffPosition(int n, double p, double cutoff)
    {
        double probability = Math.Pow(1 - p, n);
        double kExpected = n * p;
        for (int k = 0; k <= n; k++)
        {
            if (k > kExpected && probability < cutoff) return k;
            // pmf recurrence: P(k+1)/P(k) = p/(1-p) * (n-k)/(k+1)
            probability *= p / (1 - p) * (n - k) / (k + 1);
        }
        return n + 1;
    }
}
