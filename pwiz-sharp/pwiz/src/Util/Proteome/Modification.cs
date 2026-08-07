using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Proteome;

/// <summary>
/// A post-translational modification. Holds either a chemical formula (which determines both
/// monoisotopic and average delta mass) or a pair of explicit delta masses.
/// </summary>
/// <remarks>Port of <c>pwiz::proteome::Modification</c>.</remarks>
public sealed class Modification : IEquatable<Modification>, IComparable<Modification>
{
    private readonly Formula? _formula;
    private readonly double _monoDelta;
    private readonly double _avgDelta;

    /// <summary>Constructs a zero-mass modification (provided for API compatibility).</summary>
    public Modification() { _monoDelta = 0; _avgDelta = 0; }

    /// <summary>Constructs a modification from a chemical formula.</summary>
    public Modification(Formula formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        _formula = formula;
        _monoDelta = formula.MonoisotopicMass;
        _avgDelta = formula.MolecularWeight;
    }

    /// <summary>Constructs a modification from explicit delta masses.</summary>
    public Modification(double monoisotopicDeltaMass, double averageDeltaMass)
    {
        _monoDelta = monoisotopicDeltaMass;
        _avgDelta = averageDeltaMass;
    }

    /// <summary>True iff the modification was constructed with a formula.</summary>
    public bool HasFormula => _formula is not null;

    /// <summary>The difference formula. Throws if <see cref="HasFormula"/> is false.</summary>
    public Formula Formula =>
        _formula ?? throw new InvalidOperationException("This modification has no formula");

    /// <summary>Monoisotopic delta mass.</summary>
    public double MonoisotopicDeltaMass => _monoDelta;

    /// <summary>Average delta mass.</summary>
    public double AverageDeltaMass => _avgDelta;

    /// <inheritdoc/>
    public bool Equals(Modification? other) =>
        other is not null && _monoDelta == other._monoDelta && _avgDelta == other._avgDelta;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Modification);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_monoDelta, _avgDelta);

    /// <inheritdoc/>
    public int CompareTo(Modification? other)
    {
        if (other is null) return 1;
        int cmp = _monoDelta.CompareTo(other._monoDelta);
        return cmp != 0 ? cmp : _avgDelta.CompareTo(other._avgDelta);
    }

    /// <summary>Equality.</summary>
    public static bool operator ==(Modification? a, Modification? b) => Equals(a, b);
    /// <summary>Inequality.</summary>
    public static bool operator !=(Modification? a, Modification? b) => !Equals(a, b);
    /// <summary>Less-than (delta-mass ordering).</summary>
    public static bool operator <(Modification? a, Modification? b) => Compare(a, b) < 0;
    /// <summary>Less-than-or-equal.</summary>
    public static bool operator <=(Modification? a, Modification? b) => Compare(a, b) <= 0;
    /// <summary>Greater-than.</summary>
    public static bool operator >(Modification? a, Modification? b) => Compare(a, b) > 0;
    /// <summary>Greater-than-or-equal.</summary>
    public static bool operator >=(Modification? a, Modification? b) => Compare(a, b) >= 0;
    private static int Compare(Modification? a, Modification? b) =>
        a is null ? (b is null ? 0 : -1) : a.CompareTo(b);
}

/// <summary>An ordered list of modifications applied to a single residue.</summary>
/// <remarks>Port of <c>pwiz::proteome::ModificationList</c>; just <c>List&lt;Modification&gt;</c>
/// with delta-mass roll-ups.</remarks>
public sealed class ModificationList : List<Modification>, IComparable<ModificationList>
{
    /// <summary>Empty list.</summary>
    public ModificationList() { }

    /// <summary>One-element list.</summary>
    public ModificationList(Modification mod) { Add(mod); }

    /// <summary>List from an existing collection.</summary>
    public ModificationList(IEnumerable<Modification> mods) : base(mods) { }

    /// <summary>Sum of monoisotopic delta masses across all modifications in the list.</summary>
    public double MonoisotopicDeltaMass
    {
        get
        {
            double sum = 0;
            foreach (var m in this) sum += m.MonoisotopicDeltaMass;
            return sum;
        }
    }

    /// <summary>Sum of average delta masses across all modifications in the list.</summary>
    public double AverageDeltaMass
    {
        get
        {
            double sum = 0;
            foreach (var m in this) sum += m.AverageDeltaMass;
            return sum;
        }
    }

    /// <inheritdoc/>
    public int CompareTo(ModificationList? other)
    {
        if (other is null) return 1;
        if (Count != other.Count) return Count.CompareTo(other.Count);
        for (int i = 0; i < Count; i++)
        {
            int cmp = this[i].CompareTo(other[i]);
            if (cmp != 0) return cmp;
        }
        return 0;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is ModificationList other && CompareTo(other) == 0;

    /// <inheritdoc/>
    public override int GetHashCode() => Count;

    /// <summary>Equality (per-element).</summary>
    public static bool operator ==(ModificationList? a, ModificationList? b) =>
        a is null ? b is null : a.Equals(b);
    /// <summary>Inequality.</summary>
    public static bool operator !=(ModificationList? a, ModificationList? b) => !(a == b);
    /// <summary>Less-than.</summary>
    public static bool operator <(ModificationList? a, ModificationList? b) => Compare(a, b) < 0;
    /// <summary>Less-than-or-equal.</summary>
    public static bool operator <=(ModificationList? a, ModificationList? b) => Compare(a, b) <= 0;
    /// <summary>Greater-than.</summary>
    public static bool operator >(ModificationList? a, ModificationList? b) => Compare(a, b) > 0;
    /// <summary>Greater-than-or-equal.</summary>
    public static bool operator >=(ModificationList? a, ModificationList? b) => Compare(a, b) >= 0;
    private static int Compare(ModificationList? a, ModificationList? b) =>
        a is null ? (b is null ? 0 : -1) : a.CompareTo(b);
}

/// <summary>
/// Maps 0-based residue offsets to a list of modifications. Sentinel offsets
/// <see cref="NTerminus"/> and <see cref="CTerminus"/> hold N- and C-terminal mods.
/// </summary>
/// <remarks>Port of <c>pwiz::proteome::ModificationMap</c> (a SortedDictionary in C#).</remarks>
public sealed class ModificationMap : SortedDictionary<int, ModificationList>, IComparable<ModificationMap>
{
    /// <summary>Sentinel offset for N-terminal modifications.</summary>
    public const int NTerminus = int.MinValue;

    /// <summary>Sentinel offset for C-terminal modifications.</summary>
    public const int CTerminus = int.MaxValue;

    /// <summary>
    /// Indexer that auto-creates an empty list on read of a missing key (so
    /// <c>map[3].Add(mod)</c> works the same as in cpp). Hides the base indexer.
    /// </summary>
    public new ModificationList this[int offset]
    {
        get
        {
            if (!TryGetValue(offset, out var list))
            {
                list = new ModificationList();
                Add(offset, list);
            }
            return list;
        }
        set => base[offset] = value;
    }

    /// <summary>Sum of monoisotopic delta masses across all modifications in the map.</summary>
    public double MonoisotopicDeltaMass
    {
        get
        {
            double sum = 0;
            foreach (var kv in this) sum += kv.Value.MonoisotopicDeltaMass;
            return sum;
        }
    }

    /// <summary>Sum of average delta masses across all modifications in the map.</summary>
    public double AverageDeltaMass
    {
        get
        {
            double sum = 0;
            foreach (var kv in this) sum += kv.Value.AverageDeltaMass;
            return sum;
        }
    }

    /// <inheritdoc/>
    public int CompareTo(ModificationMap? other)
    {
        if (other is null) return 1;
        if (Count != other.Count) return Count.CompareTo(other.Count);
        using var a = GetEnumerator();
        using var b = other.GetEnumerator();
        while (a.MoveNext() && b.MoveNext())
        {
            int keyCmp = a.Current.Key.CompareTo(b.Current.Key);
            if (keyCmp != 0) return keyCmp;
            int valCmp = a.Current.Value.CompareTo(b.Current.Value);
            if (valCmp != 0) return valCmp;
        }
        return 0;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is ModificationMap other && CompareTo(other) == 0;

    /// <inheritdoc/>
    public override int GetHashCode() => Count;

    /// <summary>Equality (per-entry).</summary>
    public static bool operator ==(ModificationMap? a, ModificationMap? b) =>
        a is null ? b is null : a.Equals(b);
    /// <summary>Inequality.</summary>
    public static bool operator !=(ModificationMap? a, ModificationMap? b) => !(a == b);
    /// <summary>Less-than.</summary>
    public static bool operator <(ModificationMap? a, ModificationMap? b) => Compare(a, b) < 0;
    /// <summary>Less-than-or-equal.</summary>
    public static bool operator <=(ModificationMap? a, ModificationMap? b) => Compare(a, b) <= 0;
    /// <summary>Greater-than.</summary>
    public static bool operator >(ModificationMap? a, ModificationMap? b) => Compare(a, b) > 0;
    /// <summary>Greater-than-or-equal.</summary>
    public static bool operator >=(ModificationMap? a, ModificationMap? b) => Compare(a, b) >= 0;
    private static int Compare(ModificationMap? a, ModificationMap? b) =>
        a is null ? (b is null ? 0 : -1) : a.CompareTo(b);
}
