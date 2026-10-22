namespace Pwiz.Util.Chemistry;

/// <summary>
/// Static reference data for one chemical element (or labeled-isotope variant).
/// Port of pwiz/chemistry::Element::Info::Record.
/// </summary>
public sealed class ElementRecord
{
    /// <summary>The enum identifier.</summary>
    public ElementType Type { get; }

    /// <summary>Chemical symbol ("H", "C", "_2H", etc.).</summary>
    public string Symbol { get; }

    /// <summary>Atomic number (protons).</summary>
    public int AtomicNumber { get; }

    /// <summary>IUPAC average atomic weight (u).</summary>
    public double AtomicWeight { get; }

    /// <summary>All known isotopes for this element.</summary>
    public IReadOnlyList<MassAbundance> Isotopes { get; }

    /// <summary>The most-abundant isotope (highest <see cref="MassAbundance.Abundance"/>). Zero-abundance synthetic elements return the first isotope.</summary>
    public MassAbundance Monoisotope { get; }

    /// <summary>Optional alternate symbol (e.g. "D" for <c>_2H</c>). Empty when not applicable.</summary>
    public string Synonym { get; }

    internal ElementRecord(
        ElementType type,
        string symbol,
        int atomicNumber,
        double atomicWeight,
        IReadOnlyList<MassAbundance> isotopes,
        string synonym = "")
    {
        Type = type;
        Symbol = symbol;
        AtomicNumber = atomicNumber;
        AtomicWeight = atomicWeight;
        Isotopes = isotopes ?? Array.Empty<MassAbundance>();
        Synonym = synonym ?? string.Empty;

        // The monoisotope is the highest-abundance isotope; if all abundances are zero
        // (synthetic elements), fall back to the first entry.
        MassAbundance best = Isotopes.Count > 0 ? Isotopes[0] : default;
        foreach (var iso in Isotopes)
            if (iso.Abundance > best.Abundance) best = iso;
        Monoisotope = best;
    }
}
