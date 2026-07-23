namespace Pwiz.Data.IdentData;

/// <summary>One search-engine run that produced a <see cref="SpectrumIdentificationList"/>.
/// Port of <c>pwiz::identdata::SpectrumIdentification</c>. Disambiguates from
/// <see cref="SpectrumIdentificationItem"/> by namespace + class shape.</summary>
public sealed class SpectrumIdentification : Identifiable
{
    /// <summary>Protocol that drove this analysis.</summary>
    public SpectrumIdentificationProtocol? SpectrumIdentificationProtocolPtr { get; set; }

    /// <summary>Result list produced by the analysis.</summary>
    public SpectrumIdentificationList? SpectrumIdentificationListPtr { get; set; }

    /// <summary>ISO-8601 date the analysis ran.</summary>
    public string ActivityDate { get; set; } = string.Empty;

    /// <summary>Spectra files queried.</summary>
    public List<SpectraData> InputSpectra { get; } = new();

    /// <summary>Search databases consulted.</summary>
    public List<SearchDatabase> SearchDatabase { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && SpectrumIdentificationProtocolPtr is null
        && SpectrumIdentificationListPtr is null
        && string.IsNullOrEmpty(ActivityDate)
        && InputSpectra.Count == 0
        && SearchDatabase.Count == 0;
}
