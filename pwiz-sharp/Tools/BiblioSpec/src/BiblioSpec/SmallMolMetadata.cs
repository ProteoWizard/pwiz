// Port of pwiz_tools/BiblioSpec/src/SmallMolMetadata.h

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Information peculiar to small molecules: inchi-key (primary identifier), precursor adduct,
/// chemical formula, friendly molecule name, secondary identifiers, and a declared precursor m/z
/// for the case where no chemical formula is available.
/// </summary>
/// <remarks>
/// Port of <c>SmallMolMetadata</c>. cpp <c>otherKeys</c> is a tab-separated list of
/// <c>idType:value</c> tokens (e.g. <c>"CAS:58-08-2\tinchi:1S/..."</c>) — preserved verbatim.
/// </remarks>
public sealed class SmallMolMetadata : IEquatable<SmallMolMetadata>
{
    /// <summary>Primary structural identifier (InChI key).</summary>
    public string InchiKey { get; set; } = string.Empty;

    /// <summary>Ionising adduct (e.g. <c>[M+Na]</c>, <c>[2M-H2O+2H]</c>). Should agree with charge.</summary>
    public string PrecursorAdduct { get; set; } = string.Empty;

    /// <summary>Neutral chemical formula (may include an unexplained-mass suffix, e.g. <c>H84C44N12O13[+3.3122]</c>).</summary>
    public string ChemicalFormula { get; set; } = string.Empty;

    /// <summary>Friendly molecule name.</summary>
    public string MoleculeName { get; set; } = string.Empty;

    /// <summary>Tab-separated <c>idType:value</c> tokens for alternative identifiers.</summary>
    public string OtherKeys { get; set; } = string.Empty;

    /// <summary>Declared precursor m/z; ignored except when no chemical formula is provided.</summary>
    public double PrecursorMzDeclared { get; set; }

    /// <summary>Reset all fields to their default empty / zero values.</summary>
    public void Clear()
    {
        InchiKey = string.Empty;
        PrecursorAdduct = string.Empty;
        ChemicalFormula = string.Empty;
        MoleculeName = string.Empty;
        OtherKeys = string.Empty;
        PrecursorMzDeclared = 0;
    }

    /// <summary>
    /// True when this metadata is rich enough to be useful: needs a name + adduct + (formula OR declared m/z).
    /// </summary>
    public bool IsCompleteEnough() =>
        !string.IsNullOrEmpty(MoleculeName)
        && !string.IsNullOrEmpty(PrecursorAdduct)
        && (PrecursorMzDeclared != 0 || !string.IsNullOrEmpty(ChemicalFormula));

    /// <inheritdoc/>
    public bool Equals(SmallMolMetadata? other)
    {
        if (other is null) return false;
        return InchiKey == other.InchiKey
            && PrecursorAdduct == other.PrecursorAdduct
            && ChemicalFormula == other.ChemicalFormula
            && MoleculeName == other.MoleculeName
            && OtherKeys == other.OtherKeys
            && PrecursorMzDeclared == other.PrecursorMzDeclared;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SmallMolMetadata);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(InchiKey, PrecursorAdduct, ChemicalFormula, MoleculeName, OtherKeys, PrecursorMzDeclared);

    /// <summary>
    /// Schema for the SQL columns used to persist small-molecule metadata. Matches cpp
    /// <c>SmallMolMetadata::DEFINE_SQL_COLS_AND_COMMENTS</c>; order matters because cpp
    /// uses it for sorting.
    /// </summary>
    public static readonly (string Name, string Description)[] SqlColumns =
    {
        ("moleculeName", "precursor molecule's name (not needed for peptides)"),
        ("chemicalFormula", "precursor molecule's neutral formula, may include value for unexplained mass e.g. H84C44N12O13[+3.3122] (not needed for peptides)"),
        ("precursorAdduct", "ionizing adduct e.g. [M+Na], [2M-H2O+2H] etc (not needed for peptides)"),
        ("inchiKey", "molecular identifier for structure retrieval (not needed for peptides)"),
        ("otherKeys", "alternative molecular identifiers for structure retrieval, tab separated name:value pairs e.g. cas:58-08-2\\thmdb:01847 (not needed for peptides)"),
    };

    /// <summary>Column names only, in cpp order.</summary>
    public static IEnumerable<string> SqlColumnNames => SqlColumns.Select(c => c.Name);

    /// <summary>
    /// cpp <c>sql_col_decls()</c> equivalent: emit a CREATE-TABLE column list, one per line,
    /// each line terminated by a comma and the cpp inline-comment.
    /// </summary>
    public static string SqlColumnDeclarations()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (name, description) in SqlColumns)
        {
            sb.Append(name).Append(' ').Append("VARCHAR(128)").Append(", -- ").Append(description).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// cpp <c>sql_col_names_csv()</c> equivalent: leading-comma list of column names usable
    /// after an existing column list (e.g. <c>"id, peptide" + SqlColumnNamesCsv()</c>).
    /// </summary>
    public static string SqlColumnNamesCsv()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (name, _) in SqlColumns)
        {
            sb.Append(", ").Append(name);
        }
        return sb.ToString();
    }
}
