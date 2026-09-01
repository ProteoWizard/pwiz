namespace Pwiz.Data.IdentData;

#pragma warning disable CA1711 // schema-type names ending in Collection match cpp / mzIdentML

/// <summary>
/// All peptide / protein sequences and their evidence relationships used by the analyses.
/// Port of <c>pwiz::identdata::SequenceCollection</c>.
/// </summary>
public sealed class SequenceCollection
{
    /// <summary>Database protein sequences.</summary>
    public List<DBSequence> DBSequences { get; } = new();

    /// <summary>Identified peptides.</summary>
    public List<Peptide> Peptides { get; } = new();

    /// <summary>Peptide-to-protein evidence entries.</summary>
    public List<PeptideEvidence> PeptideEvidence { get; } = new();

    /// <summary>True when no sequences have been recorded.</summary>
    public bool IsEmpty => DBSequences.Count == 0 && Peptides.Count == 0 && PeptideEvidence.Count == 0;
}
