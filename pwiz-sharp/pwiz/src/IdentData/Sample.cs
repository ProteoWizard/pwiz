namespace Pwiz.Data.IdentData;

#pragma warning disable CA1711 // schema-type names ending in Collection match cpp / mzIdentML

/// <summary>A sample analyzed by mass spectrometry. Port of <c>pwiz::identdata::Sample</c>.</summary>
public sealed class Sample : IdentifiableParamContainer
{
    /// <summary>Contacts associated with this sample (and their roles).</summary>
    public List<ContactRole> ContactRole { get; } = new();

    /// <summary>Composite samples may reference subsamples (e.g. fractions, replicates).</summary>
    public List<Sample> SubSamples { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && ContactRole.Count == 0
        && SubSamples.Count == 0;
}

/// <summary>Top-level sample list for the document. Port of <c>AnalysisSampleCollection</c>.</summary>
public sealed class AnalysisSampleCollection
{
    /// <summary>Samples processed in this analysis.</summary>
    public List<Sample> Samples { get; } = new();

    /// <summary>True when no samples have been recorded.</summary>
    public bool IsEmpty => Samples.Count == 0;
}

/// <summary>Document provider — the contact and software that produced the mzIdentML file.
/// Port of <c>pwiz::identdata::Provider</c>.</summary>
public sealed class Provider : Identifiable
{
    /// <summary>Contact role (typically the primary uploader).</summary>
    public ContactRole? ContactRolePtr { get; set; }

    /// <summary>Software that produced the document.</summary>
    public AnalysisSoftware? AnalysisSoftwarePtr { get; set; }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && ContactRolePtr is null
        && AnalysisSoftwarePtr is null;
}
