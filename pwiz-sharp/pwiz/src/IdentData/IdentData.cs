using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.IdentData;

/// <summary>
/// Root of the mzIdentML document tree. Port of <c>pwiz::identdata::IdentData</c>.
/// </summary>
public class IdentData : Identifiable
{
    /// <summary>Document creation date (mzIdentML <c>creationDate</c> attribute).</summary>
    public string CreationDate { get; set; } = string.Empty;

    /// <summary>Controlled vocabularies referenced from this document.</summary>
    public List<CV> Cvs { get; } = new();

    /// <summary>Analysis software products used to produce the document.</summary>
    public List<AnalysisSoftware> AnalysisSoftwareList { get; } = new();

    /// <summary>Document provider (contact + software).</summary>
    public Provider Provider { get; } = new();

    /// <summary>Audit collection (contacts referenced from elsewhere in the document).</summary>
    public List<Contact> AuditCollection { get; } = new();

    /// <summary>Samples processed in the analyses.</summary>
    public AnalysisSampleCollection AnalysisSampleCollection { get; } = new();

    /// <summary>Peptide / protein / evidence sequences referenced by the analyses.</summary>
    public SequenceCollection SequenceCollection { get; } = new();

    /// <summary>What analyses were run.</summary>
    public AnalysisCollection AnalysisCollection { get; } = new();

    /// <summary>The protocols used by all analyses in the document.</summary>
    public AnalysisProtocolCollection AnalysisProtocolCollection { get; } = new();

    /// <summary>Input data sets and analysis output sets.</summary>
    public DataCollection DataCollection { get; } = new();

    /// <summary>Bibliographic references attached to the document.</summary>
    public List<BibliographicReference> BibliographicReferences { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(CreationDate)
        && Cvs.Count == 0
        && AnalysisSoftwareList.Count == 0
        && Provider.IsEmpty
        && AuditCollection.Count == 0
        && AnalysisSampleCollection.IsEmpty
        && SequenceCollection.IsEmpty
        && AnalysisCollection.IsEmpty
        && AnalysisProtocolCollection.IsEmpty
        && DataCollection.IsEmpty
        && BibliographicReferences.Count == 0;
}
