namespace Pwiz.Data.IdentData;

/// <summary>
/// Evidence for a specific position of a <see cref="Peptide"/> within a <see cref="DBSequence"/>.
/// Port of <c>pwiz::identdata::PeptideEvidence</c>.
/// </summary>
public sealed class PeptideEvidence : IdentifiableParamContainer
{
    /// <summary>The peptide referenced by this evidence.</summary>
    public Peptide? PeptidePtr { get; set; }

    /// <summary>The protein sequence the peptide maps into.</summary>
    public DBSequence? DBSequencePtr { get; set; }

    /// <summary>1-based start position of the peptide within the protein.</summary>
    public int Start { get; set; }

    /// <summary>1-based end position (inclusive) of the peptide within the protein.</summary>
    public int End { get; set; }

    /// <summary>Residue immediately before <see cref="Start"/> in the protein, or '-' for N-term.</summary>
    public char Pre { get; set; }

    /// <summary>Residue immediately after <see cref="End"/> in the protein, or '-' for C-term.</summary>
    public char Post { get; set; }

    /// <summary>Reading frame (only for nucleotide databases).</summary>
    public int Frame { get; set; }

    /// <summary>True if this peptide evidence comes from a decoy database entry.</summary>
    public bool IsDecoy { get; set; }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && PeptidePtr is null
        && DBSequencePtr is null
        && Start == 0 && End == 0
        && Pre == '\0' && Post == '\0'
        && Frame == 0 && !IsDecoy;
}
