using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>Cleavage specificity of a digestion enzyme. Mirrors
/// <c>pwiz::proteome::Digestion::Specificity</c>.</summary>
public enum DigestionSpecificity
{
    /// <summary>Neither termini must match the digestion motif.</summary>
    NonSpecific = 0,
    /// <summary>Either or both termini must match.</summary>
    SemiSpecific = 1,
    /// <summary>Both termini must match.</summary>
    FullySpecific = 2,
}

/// <summary>An individual cleavage enzyme used in the search. Port of
/// <c>pwiz::identdata::Enzyme</c>.</summary>
public sealed class Enzyme : Identifiable
{
    /// <summary>N-terminal mass gain (free residue terminal contribution).</summary>
    public string NTermGain { get; set; } = string.Empty;

    /// <summary>C-terminal mass gain.</summary>
    public string CTermGain { get; set; } = string.Empty;

    /// <summary>Required terminal specificity for matching peptides.</summary>
    public DigestionSpecificity TerminalSpecificity { get; set; } = DigestionSpecificity.FullySpecific;

    /// <summary>Number of allowed missed cleavages.</summary>
    public int MissedCleavages { get; set; }

    /// <summary>Minimum allowed peptide length (cpp <c>minDistance</c>).</summary>
    public int MinDistance { get; set; }

    /// <summary>PCRE-style regex describing the cleavage site.</summary>
    public string SiteRegexp { get; set; } = string.Empty;

    /// <summary>CV-named identity of the enzyme (e.g. <c>MS_Trypsin</c>).</summary>
    public ParamContainer EnzymeName { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(NTermGain)
        && string.IsNullOrEmpty(CTermGain)
        && MissedCleavages == 0
        && MinDistance == 0
        && string.IsNullOrEmpty(SiteRegexp)
        && EnzymeName.IsEmpty;
}

/// <summary>List of enzymes used in the experiment. Port of <c>pwiz::identdata::Enzymes</c>.</summary>
public sealed class Enzymes
{
    /// <summary>Whether enzymes are independent (true), exclusive (false), or unspecified
    /// (null — cpp uses <c>boost::tribool</c>).</summary>
    public bool? Independent { get; set; }

    /// <summary>The enzyme list.</summary>
    public List<Enzyme> EnzymeList { get; } = new();

    /// <summary>True when no enzymes are recorded and independence is unspecified.</summary>
    public bool IsEmpty => Independent is null && EnzymeList.Count == 0;
}
