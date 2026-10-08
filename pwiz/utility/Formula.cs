using System.Globalization;
using System.Text;

namespace Pwiz.Util.Chemistry;

/// <summary>
/// A chemical formula represented as a multiset of <see cref="ElementType"/> counts.
/// Supports parse from standard formula strings ("H2 O", "C6H12O6", "_13C6 H12 O6")
/// and arithmetic (+, -, scalar ×).
/// </summary>
/// <remarks>
/// Port of pwiz/chemistry::Formula.
///
/// Counts for the hot CHONSP + labeled-isotope elements (indices 0..<c>_15N</c>) are stored in
/// a dense array for perf; all other elements use a dictionary. This matches the C++ layout.
/// </remarks>
public sealed class Formula : IEquatable<Formula>
{
    // Count of CHONSP-range elements (C, H, O, N, S, P, _13C, _2H, _18O, _15N = indices 0..9).
    private const int CHONSPSize = (int)ElementType._15N + 1;

    private readonly int[] _chonsp = new int[CHONSPSize];
    private readonly Dictionary<ElementType, int> _other = new();

    /// <summary>Creates an empty formula.</summary>
    public Formula() { }

    /// <summary>Parses a formula string (e.g. "H2 O", "C6H12O6", "_13C6 H12 O6"). Whitespace is optional.</summary>
    /// <exception cref="FormatException">Thrown when the input is malformed.</exception>
    public Formula(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        Parse(formula);
    }

    /// <summary>Copy constructor.</summary>
    public Formula(Formula other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Array.Copy(other._chonsp, _chonsp, CHONSPSize);
        foreach (var kv in other._other) _other[kv.Key] = kv.Value;
    }

    /// <summary>Count of element <paramref name="e"/>.</summary>
    public int this[ElementType e]
    {
        get => IsChonsp(e) ? _chonsp[(int)e] : _other.TryGetValue(e, out var v) ? v : 0;
        set
        {
            if (IsChonsp(e)) _chonsp[(int)e] = value;
            else if (value == 0) _other.Remove(e);
            else _other[e] = value;
        }
    }

    /// <summary>Returns a dictionary view of all non-zero element counts.</summary>
    public IReadOnlyDictionary<ElementType, int> Data
    {
        get
        {
            var dict = new Dictionary<ElementType, int>(_other);
            for (int i = 0; i < CHONSPSize; i++)
                if (_chonsp[i] != 0) dict[(ElementType)i] = _chonsp[i];
            return dict;
        }
    }

    /// <summary>Monoisotopic mass (sum of most-abundant isotope mass × count).</summary>
    public double MonoisotopicMass
    {
        get
        {
            double sum = 0;
            for (int i = 0; i < CHONSPSize; i++)
            {
                int c = _chonsp[i];
                if (c == 0) continue;
                var r = ElementInfo.Record((ElementType)i);
                if (r.Isotopes.Count > 0) sum += r.Monoisotope.Mass * c;
            }
            foreach (var kv in _other)
            {
                if (kv.Value == 0) continue;
                var r = ElementInfo.Record(kv.Key);
                if (r.Isotopes.Count > 0) sum += r.Monoisotope.Mass * kv.Value;
            }
            return sum;
        }
    }

    /// <summary>Average molecular weight (sum of IUPAC atomic weight × count).</summary>
    public double MolecularWeight
    {
        get
        {
            double sum = 0;
            for (int i = 0; i < CHONSPSize; i++)
            {
                int c = _chonsp[i];
                if (c != 0) sum += ElementInfo.Record((ElementType)i).AtomicWeight * c;
            }
            foreach (var kv in _other)
            {
                if (kv.Value != 0) sum += ElementInfo.Record(kv.Key).AtomicWeight * kv.Value;
            }
            return sum;
        }
    }

    /// <summary>Canonical formula string, alphabetized by element symbol (matches pwiz's output).</summary>
    public override string ToString()
    {
        var terms = new List<string>();
        for (int i = 0; i < CHONSPSize; i++)
        {
            int c = _chonsp[i];
            if (c != 0) terms.Add(ElementInfo.Record((ElementType)i).Symbol + c.ToString(CultureInfo.InvariantCulture));
        }
        foreach (var kv in _other)
        {
            if (kv.Value != 0)
                terms.Add(ElementInfo.Record(kv.Key).Symbol + kv.Value.ToString(CultureInfo.InvariantCulture));
        }
        terms.Sort(StringComparer.Ordinal);
        return string.Concat(terms);
    }

    // ---- arithmetic ----

    /// <summary>Returns <c>a + b</c>.</summary>
    public static Formula operator +(Formula a, Formula b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var r = new Formula(a);
        for (int i = 0; i < CHONSPSize; i++) r._chonsp[i] += b._chonsp[i];
        foreach (var kv in b._other) r._other[kv.Key] = r._other.GetValueOrDefault(kv.Key) + kv.Value;
        return r;
    }

    /// <summary>Returns <c>a - b</c>.</summary>
    public static Formula operator -(Formula a, Formula b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var r = new Formula(a);
        for (int i = 0; i < CHONSPSize; i++) r._chonsp[i] -= b._chonsp[i];
        foreach (var kv in b._other) r._other[kv.Key] = r._other.GetValueOrDefault(kv.Key) - kv.Value;
        return r;
    }

    /// <summary>Returns <c>a × scalar</c>.</summary>
    public static Formula operator *(Formula a, int scalar)
    {
        ArgumentNullException.ThrowIfNull(a);
        var r = new Formula();
        for (int i = 0; i < CHONSPSize; i++) r._chonsp[i] = a._chonsp[i] * scalar;
        foreach (var kv in a._other) r._other[kv.Key] = kv.Value * scalar;
        return r;
    }

    /// <summary>Returns <c>scalar × a</c>.</summary>
    public static Formula operator *(int scalar, Formula a) => a * scalar;

    // ---- equality ----

    /// <inheritdoc/>
    public bool Equals(Formula? other)
    {
        if (other is null) return false;
        for (int i = 0; i < CHONSPSize; i++)
            if (_chonsp[i] != other._chonsp[i]) return false;
        // Compare dictionaries while ignoring zero-valued entries (which shouldn't exist but be safe).
        int myNonZero = 0, theirNonZero = 0;
        foreach (var kv in _other) if (kv.Value != 0) myNonZero++;
        foreach (var kv in other._other) if (kv.Value != 0) theirNonZero++;
        if (myNonZero != theirNonZero) return false;
        foreach (var kv in _other)
        {
            if (kv.Value == 0) continue;
            if (!other._other.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Formula);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int h = 0;
        for (int i = 0; i < CHONSPSize; i++) h ^= _chonsp[i].GetHashCode();
        foreach (var kv in _other)
            if (kv.Value != 0) h ^= HashCode.Combine(kv.Key, kv.Value);
        return h;
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Formula? a, Formula? b) => Equals(a, b);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Formula? a, Formula? b) => !Equals(a, b);

    // ---- parser ----

    private static bool IsChonsp(ElementType e) => (int)e < CHONSPSize;

    private void Parse(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return;

        int i = 0;
        while (i < formula.Length)
        {
            // skip whitespace
            while (i < formula.Length && char.IsWhiteSpace(formula[i])) i++;
            if (i >= formula.Length) break;

            // symbol: starts with uppercase or '_' (for labeled isotopes like _13C, _2H)
            int symStart = i;
            if (formula[i] == '_')
            {
                i++;
                // skip digits that are part of the isotope label (e.g. "_13" in _13C)
                while (i < formula.Length && char.IsDigit(formula[i])) i++;
                // expect exactly one uppercase letter after the digits (C, H, O, N)
                if (i >= formula.Length || !char.IsUpper(formula[i]))
                    throw new FormatException($"Invalid formula (expected element letter after isotope label): '{formula}'");
                i++;
            }
            else if (char.IsUpper(formula[i]))
            {
                i++;
            }
            else
            {
                throw new FormatException($"Invalid formula (expected element symbol at position {i}): '{formula}'");
            }
            // trailing lowercase letters (e.g. "He", "Uuu")
            while (i < formula.Length && char.IsLower(formula[i])) i++;
            string symbol = formula[symStart..i];

            // optional count (allow negative)
            int count;
            // skip whitespace between symbol and count
            int ws = i;
            while (ws < formula.Length && char.IsWhiteSpace(formula[ws])) ws++;

            bool hasSign = ws < formula.Length && formula[ws] == '-';
            int firstDigit = hasSign ? ws + 1 : ws;

            if (firstDigit < formula.Length && char.IsDigit(formula[firstDigit]))
            {
                int countStart = ws;
                i = firstDigit;
                while (i < formula.Length && char.IsDigit(formula[i])) i++;
                if (!int.TryParse(formula[countStart..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                    throw new FormatException($"Invalid count in formula: '{formula}'");
            }
            else
            {
                count = 1;
            }

            var type = ElementInfo.TypeFromSymbol(symbol);
            this[type] += count;
        }
    }
}
