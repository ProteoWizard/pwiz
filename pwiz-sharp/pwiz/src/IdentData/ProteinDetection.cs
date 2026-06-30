using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

#pragma warning disable CA1711 // schema-type names ending in Collection match cpp / mzIdentML

/// <summary>Parameters and settings for a protein-detection analysis. Port of
/// <c>ProteinDetectionProtocol</c>.</summary>
public sealed class ProteinDetectionProtocol : Identifiable
{
    /// <summary>Software running this protocol.</summary>
    public AnalysisSoftware? AnalysisSoftwarePtr { get; set; }

    /// <summary>Free-form analysis parameters.</summary>
    public ParamContainer AnalysisParams { get; } = new();

    /// <summary>Acceptance threshold(s) for protein detection.</summary>
    public ParamContainer Threshold { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && AnalysisSoftwarePtr is null
        && AnalysisParams.IsEmpty
        && Threshold.IsEmpty;
}

/// <summary>Peptide evidence supporting a protein hypothesis. Port of <c>PeptideHypothesis</c>.</summary>
public sealed class PeptideHypothesis
{
    /// <summary>Peptide-evidence reference.</summary>
    public PeptideEvidence? PeptideEvidencePtr { get; set; }

    /// <summary>SII references that support this peptide for the protein hypothesis.</summary>
    public List<SpectrumIdentificationItem> SpectrumIdentificationItemPtr { get; } = new();

    /// <summary>True when no evidence is recorded.</summary>
    public bool IsEmpty => PeptideEvidencePtr is null && SpectrumIdentificationItemPtr.Count == 0;
}

/// <summary>A protein hypothesis (single result from the protein detection analysis). Port of
/// <c>ProteinDetectionHypothesis</c>.</summary>
public sealed class ProteinDetectionHypothesis : IdentifiableParamContainer
{
    /// <summary>Protein sequence database entry the hypothesis references.</summary>
    public DBSequence? DBSequencePtr { get; set; }

    /// <summary>Whether this hypothesis passes the threshold(s).</summary>
    public bool PassThreshold { get; set; }

    /// <summary>Peptide evidence supporting the hypothesis.</summary>
    public List<PeptideHypothesis> PeptideHypothesis { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && DBSequencePtr is null
        && !PassThreshold
        && PeptideHypothesis.Count == 0;
}

/// <summary>Set of related protein hypotheses (e.g. competing assignments). Port of
/// <c>ProteinAmbiguityGroup</c>.</summary>
public sealed class ProteinAmbiguityGroup : IdentifiableParamContainer
{
    /// <summary>The competing protein hypotheses.</summary>
    public List<ProteinDetectionHypothesis> ProteinDetectionHypothesis { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty => base.IsEmpty && ProteinDetectionHypothesis.Count == 0;
}

/// <summary>Top-level protein list produced by the protein-detection analysis. Port of
/// <c>ProteinDetectionList</c>.</summary>
public sealed class ProteinDetectionList : IdentifiableParamContainer
{
    /// <summary>Ambiguity groups containing the protein hypotheses.</summary>
    public List<ProteinAmbiguityGroup> ProteinAmbiguityGroup { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty => base.IsEmpty && ProteinAmbiguityGroup.Count == 0;
}

/// <summary>An analysis that derives proteins from peptide identifications. Port of
/// <c>ProteinDetection</c>.</summary>
public sealed class ProteinDetection : Identifiable
{
    /// <summary>Protocol driving the analysis.</summary>
    public ProteinDetectionProtocol? ProteinDetectionProtocolPtr { get; set; }

    /// <summary>Output list of proteins produced.</summary>
    public ProteinDetectionList? ProteinDetectionListPtr { get; set; }

    /// <summary>ISO-8601 date the analysis ran.</summary>
    public string ActivityDate { get; set; } = string.Empty;

    /// <summary>Spectrum-identification lists feeding the protein detection.</summary>
    public List<SpectrumIdentificationList> InputSpectrumIdentifications { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && ProteinDetectionProtocolPtr is null
        && ProteinDetectionListPtr is null
        && string.IsNullOrEmpty(ActivityDate)
        && InputSpectrumIdentifications.Count == 0;
}

/// <summary>Top-level analysis collection — what analyses were run. Port of
/// <c>AnalysisCollection</c>.</summary>
public sealed class AnalysisCollection
{
    /// <summary>Search-engine runs that produced PSM lists.</summary>
    public List<SpectrumIdentification> SpectrumIdentification { get; } = new();

    /// <summary>Optional protein-detection analysis.</summary>
    public ProteinDetection ProteinDetection { get; } = new();

    /// <summary>True when no analyses are recorded.</summary>
    public bool IsEmpty => SpectrumIdentification.Count == 0 && ProteinDetection.IsEmpty;
}

/// <summary>The protocols used by all analyses in the document. Port of
/// <c>AnalysisProtocolCollection</c>.</summary>
public sealed class AnalysisProtocolCollection
{
    /// <summary>Search-engine protocols.</summary>
    public List<SpectrumIdentificationProtocol> SpectrumIdentificationProtocol { get; } = new();

    /// <summary>Protein-detection protocols.</summary>
    public List<ProteinDetectionProtocol> ProteinDetectionProtocol { get; } = new();

    /// <summary>True when no protocols are recorded.</summary>
    public bool IsEmpty => SpectrumIdentificationProtocol.Count == 0 && ProteinDetectionProtocol.Count == 0;
}
