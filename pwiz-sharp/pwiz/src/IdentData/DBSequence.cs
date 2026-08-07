namespace Pwiz.Data.IdentData;

/// <summary>
/// A protein sequence entry from a search database. Port of <c>pwiz::identdata::DBSequence</c>.
/// </summary>
public sealed class DBSequence : IdentifiableParamContainer
{
    /// <summary>Sequence length in residues.</summary>
    public int Length { get; set; }

    /// <summary>Database accession (e.g. "P12345").</summary>
    public string Accession { get; set; } = string.Empty;

    /// <summary>Reference to the search database the sequence came from.</summary>
    public SearchDatabase? SearchDatabasePtr { get; set; }

    /// <summary>Optional one-letter sequence string.</summary>
    public string Seq { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && Length == 0
        && string.IsNullOrEmpty(Accession)
        && SearchDatabasePtr is null
        && string.IsNullOrEmpty(Seq);
}

/// <summary>
/// A search database used during the analysis. Port of <c>pwiz::identdata::SearchDatabase</c>.
/// </summary>
public sealed class SearchDatabase : IdentifiableParamContainer
{
    /// <summary>File location of the database.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Database version string.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Date the search database was last updated.</summary>
    public string ReleaseDate { get; set; } = string.Empty;

    /// <summary>Number of database sequences.</summary>
    public long NumDatabaseSequences { get; set; }

    /// <summary>Number of residues across all sequences.</summary>
    public long NumResidues { get; set; }

    /// <summary>Database file format CV term (e.g. <c>MS_FASTA_format</c>).</summary>
    public Pwiz.Data.Common.Params.CVParam FileFormat { get; set; } = new(Pwiz.Data.Common.Cv.CVID.CVID_Unknown);

    /// <summary>CV-named identity of the database (e.g. UniProt entry).</summary>
    public Pwiz.Data.Common.Params.ParamContainer DatabaseName { get; } = new();
}
