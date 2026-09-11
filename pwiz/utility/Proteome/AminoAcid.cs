using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Proteome;

/// <summary>The 20 canonical amino acids plus selenocysteine, ambiguous codes, and Unknown.</summary>
/// <remarks>Port of <c>pwiz::proteome::AminoAcid::Type</c>.</remarks>
public enum AminoAcid
{
    /// <summary>A — Ala</summary>
    Alanine,
    /// <summary>C — Cys</summary>
    Cysteine,
    /// <summary>D — Asp</summary>
    AsparticAcid,
    /// <summary>E — Glu</summary>
    GlutamicAcid,
    /// <summary>F — Phe</summary>
    Phenylalanine,
    /// <summary>G — Gly</summary>
    Glycine,
    /// <summary>H — His</summary>
    Histidine,
    /// <summary>I — Ile</summary>
    Isoleucine,
    /// <summary>K — Lys</summary>
    Lysine,
    /// <summary>L — Leu</summary>
    Leucine,
    /// <summary>M — Met</summary>
    Methionine,
    /// <summary>N — Asn</summary>
    Asparagine,
    /// <summary>P — Pro</summary>
    Proline,
    /// <summary>Q — Gln</summary>
    Glutamine,
    /// <summary>R — Arg</summary>
    Arginine,
    /// <summary>S — Ser</summary>
    Serine,
    /// <summary>T — Thr</summary>
    Threonine,
    /// <summary>V — Val</summary>
    Valine,
    /// <summary>W — Trp</summary>
    Tryptophan,
    /// <summary>Y — Tyr</summary>
    Tyrosine,
    /// <summary>U — Sec (selenocysteine)</summary>
    Selenocysteine,
    /// <summary>B — Asx (Asn or Asp)</summary>
    AspX,
    /// <summary>Z — Glx (Gln or Glu)</summary>
    GlutX,
    /// <summary>X — Unk</summary>
    Unknown,
}

/// <summary>Per-amino-acid metadata: name, abbreviation, single-letter symbol, residue
/// formula (peptide-bonded), full formula (residue + H2O), natural abundance.</summary>
/// <remarks>Port of <c>pwiz::proteome::AminoAcid::Info::Record</c>.</remarks>
public sealed record AminoAcidRecord(
    string Name,
    string Abbreviation,
    char Symbol,
    Formula ResidueFormula,
    Formula FullFormula,
    double Abundance);

/// <summary>Lookup helpers for <see cref="AminoAcid"/>.</summary>
/// <remarks>Port of <c>pwiz::proteome::AminoAcid::Info</c>.</remarks>
public static class AminoAcidInfo
{
    private static readonly AminoAcidRecord[] s_byEnum;
    private static readonly Dictionary<char, AminoAcidRecord> s_bySymbol;

    static AminoAcidInfo()
    {
        // Residue formulas (sequence formula = sum(residueFormula) + H2O) match cpp
        // AminoAcid.cpp:83-107 exactly. Abundances are natural prevalence in human proteins.
        var defs = new (AminoAcid Aa, string Name, string Abbrev, char Symbol, string Res, double Abundance)[]
        {
            (AminoAcid.Alanine,        "Alanine",        "Ala", 'A', "C3 H5 N1 O1 S0",      .078),
            (AminoAcid.Cysteine,       "Cysteine",       "Cys", 'C', "C3 H5 N1 O1 S1",      .019),
            (AminoAcid.AsparticAcid,   "Aspartic Acid",  "Asp", 'D', "C4 H5 N1 O3 S0",      .053),
            (AminoAcid.GlutamicAcid,   "Glutamic Acid",  "Glu", 'E', "C5 H7 N1 O3 S0",      .063),
            (AminoAcid.Phenylalanine,  "Phenylalanine",  "Phe", 'F', "C9 H9 N1 O1 S0",      .039),
            (AminoAcid.Glycine,        "Glycine",        "Gly", 'G', "C2 H3 N1 O1 S0",      .072),
            (AminoAcid.Histidine,      "Histidine",      "His", 'H', "C6 H7 N3 O1 S0",      .023),
            (AminoAcid.Isoleucine,     "Isoleucine",     "Ile", 'I', "C6 H11 N1 O1 S0",     .053),
            (AminoAcid.Lysine,         "Lysine",         "Lys", 'K', "C6 H12 N2 O1 S0",     .059),
            (AminoAcid.Leucine,        "Leucine",        "Leu", 'L', "C6 H11 N1 O1 S0",     .091),
            (AminoAcid.Methionine,     "Methionine",     "Met", 'M', "C5 H9 N1 O1 S1",      .023),
            (AminoAcid.Asparagine,     "Asparagine",     "Asn", 'N', "C4 H6 N2 O2 S0",      .043),
            (AminoAcid.Proline,        "Proline",        "Pro", 'P', "C5 H7 N1 O1 S0",      .052),
            (AminoAcid.Glutamine,      "Glutamine",      "Gln", 'Q', "C5 H8 N2 O2 S0",      .042),
            (AminoAcid.Arginine,       "Arginine",       "Arg", 'R', "C6 H12 N4 O1 S0",     .051),
            (AminoAcid.Serine,         "Serine",         "Ser", 'S', "C3 H5 N1 O2 S0",      .068),
            (AminoAcid.Threonine,      "Threonine",      "Thr", 'T', "C4 H7 N1 O2 S0",      .059),
            (AminoAcid.Valine,         "Valine",         "Val", 'V', "C5 H9 N1 O1 S0",      .066),
            (AminoAcid.Tryptophan,     "Tryptophan",     "Trp", 'W', "C11 H10 N2 O1 S0",    .014),
            (AminoAcid.Tyrosine,       "Tyrosine",       "Tyr", 'Y', "C9 H9 N1 O2 S0",      .032),
            (AminoAcid.Selenocysteine, "Selenocysteine", "Sec", 'U', "C3 H5 N1 O1 Se1",     .00),
            (AminoAcid.AspX,           "AspX",           "Asx", 'B', "C4 H6 N2 O2 S0",      .00),
            (AminoAcid.GlutX,          "GlutX",          "Glx", 'Z', "C5 H8 N2 O2 S0",      .00),
            (AminoAcid.Unknown,        "Unknown",        "Unk", 'X', "C5 H6 N1 O1 S0",      .00),
        };

        s_byEnum = new AminoAcidRecord[defs.Length];
        s_bySymbol = new Dictionary<char, AminoAcidRecord>(defs.Length);
        var water = new Formula("H2 O1");
        foreach (var d in defs)
        {
            var residue = new Formula(d.Res);
            var full = residue + water;
            var record = new AminoAcidRecord(d.Name, d.Abbrev, d.Symbol, residue, full, d.Abundance);
            s_byEnum[(int)d.Aa] = record;
            s_bySymbol[d.Symbol] = record;
        }
    }

    /// <summary>Returns the record for an enum value.</summary>
    public static AminoAcidRecord Record(AminoAcid type) => s_byEnum[(int)type];

    /// <summary>Returns the record for a single-letter symbol; throws on unknown symbols.</summary>
    public static AminoAcidRecord Record(char symbol) =>
        s_bySymbol.TryGetValue(symbol, out var rec)
            ? rec
            : throw new ArgumentException($"Invalid amino acid symbol: {symbol}", nameof(symbol));

    /// <summary>True iff <paramref name="symbol"/> is a known amino acid letter.</summary>
    public static bool IsKnownSymbol(char symbol) => s_bySymbol.ContainsKey(symbol);
}
