using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>
/// A modification applied to a residue (or terminus) of a peptide. Port of
/// <c>pwiz::identdata::Modification</c>.
/// </summary>
public sealed class Modification : ParamContainer
{
    /// <summary>1-based residue position; 0 means N-terminal, sequence.Length+1 means C-terminal.</summary>
    public int Location { get; set; }

    /// <summary>Residue characters this modification applies to (often a single letter).</summary>
    public List<char> Residues { get; } = new();

    /// <summary>Average mass shift introduced by the modification (Da).</summary>
    public double AvgMassDelta { get; set; }

    /// <summary>Monoisotopic mass shift introduced by the modification (Da).</summary>
    public double MonoisotopicMassDelta { get; set; }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        Location == 0
        && Residues.Count == 0
        && AvgMassDelta == 0
        && MonoisotopicMassDelta == 0
        && base.IsEmpty;
}

/// <summary>A substitution modification (one residue replaced by another). Port of
/// <c>pwiz::identdata::SubstitutionModification</c>.</summary>
public sealed class SubstitutionModification
{
    /// <summary>Original residue at the substitution site.</summary>
    public char OriginalResidue { get; set; }

    /// <summary>Replacement residue at the substitution site.</summary>
    public char ReplacementResidue { get; set; }

    /// <summary>1-based residue position of the substitution.</summary>
    public int Location { get; set; }

    /// <summary>Average mass shift introduced by the substitution (Da).</summary>
    public double AvgMassDelta { get; set; }

    /// <summary>Monoisotopic mass shift introduced by the substitution (Da).</summary>
    public double MonoisotopicMassDelta { get; set; }

    /// <summary>True when no fields are populated.</summary>
    public bool IsEmpty =>
        OriginalResidue == '\0'
        && ReplacementResidue == '\0'
        && Location == 0
        && AvgMassDelta == 0
        && MonoisotopicMassDelta == 0;
}

/// <summary>A modification considered during search-engine evaluation (fixed or variable). Port
/// of <c>pwiz::identdata::SearchModification</c>.</summary>
public sealed class SearchModification : ParamContainer
{
    /// <summary>True for fixed modifications (always applied), false for variable.</summary>
    public bool FixedMod { get; set; }

    /// <summary>Mass shift introduced by the modification (Da).</summary>
    public double MassDelta { get; set; }

    /// <summary>Residue characters this modification can apply to.</summary>
    public List<char> Residues { get; } = new();

    /// <summary>Specificity rules CV term (e.g. <c>MS_modification_specificity_protein_N_term</c>).</summary>
    public CVParam SpecificityRules { get; set; } = new(CVID.CVID_Unknown);

    /// <inheritdoc/>
    public override bool IsEmpty =>
        !FixedMod
        && MassDelta == 0
        && Residues.Count == 0
        && SpecificityRules.IsEmpty
        && base.IsEmpty;
}
