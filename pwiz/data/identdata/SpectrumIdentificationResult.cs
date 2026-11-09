namespace Pwiz.Data.IdentData;

/// <summary>
/// All identifications produced for a single source spectrum. For MS/MS data each
/// <see cref="SpectrumIdentificationItem"/> is a candidate match ranked by score. Port of
/// <c>pwiz::identdata::SpectrumIdentificationResult</c>.
/// </summary>
public sealed class SpectrumIdentificationResult : IdentifiableParamContainer
{
    /// <summary>Native id of the spectrum the search engine queried with (e.g. "scan=1234").</summary>
    public string SpectrumID { get; set; } = string.Empty;

    /// <summary>Reference to the spectra-data file the spectrum came from.</summary>
    public SpectraData? SpectraDataPtr { get; set; }

    /// <summary>Candidate matches for this spectrum, ranked by score (rank=1 is best).</summary>
    public List<SpectrumIdentificationItem> SpectrumIdentificationItem { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(SpectrumID)
        && SpectraDataPtr is null
        && SpectrumIdentificationItem.Count == 0;
}
