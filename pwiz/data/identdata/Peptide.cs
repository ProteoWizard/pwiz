namespace Pwiz.Data.IdentData;

/// <summary>
/// Schema-level peptide entry in mzIdentML — an id + sequence + modifications. Distinct from
/// <see cref="Pwiz.Util.Proteome.Peptide"/> which is the chemistry-side peptide. Port of
/// <c>pwiz::identdata::Peptide</c>.
/// </summary>
public sealed class Peptide : IdentifiableParamContainer
{
    /// <summary>One-letter peptide sequence (no modifications).</summary>
    public string PeptideSequence { get; set; } = string.Empty;

    /// <summary>Modifications applied to specific residues. Position 0 = N-terminus, position
    /// (sequence.Length+1) = C-terminus, otherwise 1-based residue position.</summary>
    public List<Modification> Modifications { get; } = new();

    /// <summary>Substitution modifications (residue replacements).</summary>
    public List<SubstitutionModification> SubstitutionModifications { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(PeptideSequence)
        && Modifications.Count == 0
        && SubstitutionModifications.Count == 0;
}
