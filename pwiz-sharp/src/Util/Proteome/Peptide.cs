using System.Globalization;
using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Proteome;

/// <summary>How inline modifications in a peptide sequence are parsed.</summary>
/// <remarks>Port of <c>pwiz::proteome::ModificationParsing</c>.</remarks>
public enum ModificationParsing
{
    /// <summary>Any non-AA character causes an exception.</summary>
    Off,
    /// <summary>Inline mods are chemical formulas, e.g. <c>PEP(O)TIDE</c>.</summary>
    ByFormula,
    /// <summary>Inline mods are masses, e.g. <c>PEP(15.94)TIDE</c> or <c>PEP(15.94,15.99)TIDE</c>.</summary>
    ByMass,
    /// <summary>Either formula or mass; tries formula first, falls back to mass.</summary>
    Auto,
}

/// <summary>The delimiter pair used to bracket inline modifications.</summary>
/// <remarks>Port of <c>pwiz::proteome::ModificationDelimiter</c>.</remarks>
public enum ModificationDelimiter
{
    /// <summary><c>(</c> and <c>)</c></summary>
    Parentheses,
    /// <summary><c>[</c> and <c>]</c></summary>
    Brackets,
    /// <summary><c>{</c> and <c>}</c></summary>
    Braces,
}

/// <summary>A peptide / polypeptide: an amino acid sequence plus optional modifications.</summary>
/// <remarks>Port of <c>pwiz::proteome::Peptide</c>. Provides mass + fragmentation helpers
/// for SeeMS-style peptide annotation.</remarks>
public sealed class Peptide : IEquatable<Peptide>, IComparable<Peptide>
{
    private readonly string _sequence;
    private readonly ModificationMap _mods = new();
    private readonly double _monoMass;
    private readonly double _avgMass;
    private readonly bool _valid;

    /// <summary>Constructs a peptide; inline modifications are parsed per the parameters.</summary>
    public Peptide(string sequence,
        ModificationParsing parsing = ModificationParsing.Off,
        ModificationDelimiter delimiter = ModificationDelimiter.Parentheses)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        var working = new System.Text.StringBuilder(sequence);
        try
        {
            (char start, char end) = delimiter switch
            {
                ModificationDelimiter.Brackets => ('[', ']'),
                ModificationDelimiter.Braces => ('{', '}'),
                _ => ('(', ')'),
            };

            switch (parsing)
            {
                case ModificationParsing.Off:
                    foreach (char c in working.ToString())
                        if (!AminoAcidInfo.IsKnownSymbol(c))
                            throw new FormatException($"Invalid amino acid in sequence {sequence}");
                    break;

                case ModificationParsing.ByFormula:
                    ParseInlineMods(working, sequence, start, end, allowFormula: true, allowMass: false);
                    break;

                case ModificationParsing.ByMass:
                    ParseInlineMods(working, sequence, start, end, allowFormula: false, allowMass: true);
                    break;

                case ModificationParsing.Auto:
                    ParseInlineMods(working, sequence, start, end, allowFormula: true, allowMass: true);
                    break;
            }

            _sequence = working.ToString();
            _valid = true;
            var unmodified = ComputeFormula(modified: false);
            _monoMass = unmodified.MonoisotopicMass;
            _avgMass = unmodified.MolecularWeight;
        }
        catch
        {
            _sequence = working.ToString();
            _monoMass = 0;
            _avgMass = 0;
        }
    }

    /// <summary>The sequence of amino acid letters making up the peptide (mods stripped).</summary>
    public string Sequence => _sequence;

    /// <summary>Modification map keyed by 0-based residue offset (or <see cref="ModificationMap.NTerminus"/> /
    /// <see cref="ModificationMap.CTerminus"/>). Mutable.</summary>
    public ModificationMap Modifications => _mods;

    /// <summary>
    /// Sum-of-residues + H2O formula for the peptide. With <paramref name="modified"/>=true,
    /// adds the formulas of all attached modifications (which must each have a formula).
    /// </summary>
    public Formula GetFormula(bool modified = false) => ComputeFormula(modified);

    /// <summary>
    /// Returns the peptide mass at the given charge. <paramref name="charge"/>=0 returns the
    /// neutral mass; charge &gt; 0 returns m/z. <paramref name="modified"/> controls whether
    /// modification delta masses are included.
    /// </summary>
    public double MonoisotopicMass(int charge = 0, bool modified = true)
    {
        if (_monoMass == 0) return 0;
        double mass = modified ? _monoMass + _mods.MonoisotopicDeltaMass : _monoMass;
        return charge == 0 ? mass : (mass + PhysicalConstants.Proton * charge) / charge;
    }

    /// <summary>Average-mass version of <see cref="MonoisotopicMass"/>.</summary>
    public double MolecularWeight(int charge = 0, bool modified = true)
    {
        if (_avgMass == 0) return 0;
        double mass = modified ? _avgMass + _mods.AverageDeltaMass : _avgMass;
        return charge == 0 ? mass : (mass + PhysicalConstants.Proton * charge) / charge;
    }

    /// <summary>Builds a fragmentation model for this peptide.</summary>
    public Fragmentation Fragmentation(bool monoisotopic = true, bool modified = true) =>
        new(this, monoisotopic, modified);

    /// <inheritdoc/>
    public bool Equals(Peptide? other) =>
        other is not null && _sequence == other._sequence && _mods.CompareTo(other._mods) == 0;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Peptide);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_sequence, _mods.Count);

    /// <inheritdoc/>
    public int CompareTo(Peptide? other)
    {
        if (other is null) return 1;
        if (_sequence.Length != other._sequence.Length)
            return _sequence.Length.CompareTo(other._sequence.Length);
        int cmp = string.Compare(_sequence, other._sequence, StringComparison.Ordinal);
        return cmp != 0 ? cmp : _mods.CompareTo(other._mods);
    }

    /// <summary>Equality.</summary>
    public static bool operator ==(Peptide? a, Peptide? b) => Equals(a, b);
    /// <summary>Inequality.</summary>
    public static bool operator !=(Peptide? a, Peptide? b) => !Equals(a, b);
    /// <summary>Less-than (length, then sequence, then mods).</summary>
    public static bool operator <(Peptide? a, Peptide? b) => Compare(a, b) < 0;
    /// <summary>Less-than-or-equal.</summary>
    public static bool operator <=(Peptide? a, Peptide? b) => Compare(a, b) <= 0;
    /// <summary>Greater-than.</summary>
    public static bool operator >(Peptide? a, Peptide? b) => Compare(a, b) > 0;
    /// <summary>Greater-than-or-equal.</summary>
    public static bool operator >=(Peptide? a, Peptide? b) => Compare(a, b) >= 0;
    private static int Compare(Peptide? a, Peptide? b) =>
        a is null ? (b is null ? 0 : -1) : a.CompareTo(b);

    private Formula ComputeFormula(bool modified)
    {
        if (string.IsNullOrEmpty(_sequence) || !_valid) return new Formula();

        var formula = new Formula();
        var modItr = _mods.GetEnumerator();
        bool hasMod = modItr.MoveNext();

        // N-terminal H
        formula[ElementType.H] += 1;
        if (modified && hasMod && modItr.Current.Key == ModificationMap.NTerminus)
        {
            foreach (var m in modItr.Current.Value)
                formula = formula + RequireFormula(m);
            hasMod = modItr.MoveNext();
        }

        // residues + per-residue mods
        for (int i = 0; i < _sequence.Length; i++)
        {
            formula = formula + AminoAcidInfo.Record(_sequence[i]).ResidueFormula;
            if (modified && hasMod && modItr.Current.Key == i)
            {
                foreach (var m in modItr.Current.Value)
                    formula = formula + RequireFormula(m);
                hasMod = modItr.MoveNext();
            }
        }

        // C-terminal OH
        formula[ElementType.O] += 1;
        formula[ElementType.H] += 1;
        if (modified && hasMod && modItr.Current.Key == ModificationMap.CTerminus)
        {
            foreach (var m in modItr.Current.Value)
                formula = formula + RequireFormula(m);
        }

        return formula;
    }

    private static Formula RequireFormula(Modification mod)
    {
        if (!mod.HasFormula)
            throw new InvalidOperationException(
                "peptide formula cannot be generated when any modification has no formula info");
        return mod.Formula;
    }

    private void ParseInlineMods(System.Text.StringBuilder sb, string original,
        char startDelim, char endDelim, bool allowFormula, bool allowMass)
    {
        int i = 0;
        while (i < sb.Length)
        {
            if (sb[i] != startDelim) { i++; continue; }

            int closeIdx = -1;
            for (int j = i + 1; j < sb.Length; j++)
            {
                if (sb[j] == endDelim) { closeIdx = j; break; }
            }
            if (closeIdx < 0)
                throw new FormatException("Modification started but not ended in sequence " + original);

            string body = sb.ToString(i + 1, closeIdx - i - 1);
            int offset = i == 0 ? ModificationMap.NTerminus
                : (closeIdx + 1 == sb.Length ? ModificationMap.CTerminus : i - 1);

            bool ok = false;
            if (allowFormula && TryParseFormulaMod(body, out var fmod))
            {
                _mods[offset].Add(fmod);
                ok = true;
            }
            else if (allowMass && TryParseMassMod(body, out var mmod))
            {
                _mods[offset].Add(mmod);
                ok = true;
            }
            if (!ok)
                throw new FormatException(
                    "Modification not parseable as " +
                    (allowFormula && allowMass ? "either a formula or a mass" :
                     allowFormula ? "a chemical formula" : "one or two comma-separated numbers") +
                    " in sequence " + original);

            sb.Remove(i, closeIdx - i + 1);
            // Don't advance i — we consumed delimited content and need to re-check for back-to-back mods.
        }
    }

    private static bool TryParseFormulaMod(string body, out Modification mod)
    {
        try { mod = new Modification(new Formula(body)); return true; }
        catch { mod = new Modification(); return false; }
    }

    private static bool TryParseMassMod(string body, out Modification mod)
    {
        var parts = body.Split(',');
        if (parts.Length is < 1 or > 2) { mod = new Modification(); return false; }
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double mono))
        { mod = new Modification(); return false; }
        double avg = mono;
        if (parts.Length == 2 &&
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out avg))
        { mod = new Modification(); return false; }
        mod = new Modification(mono, avg);
        return true;
    }
}
