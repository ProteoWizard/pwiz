namespace Pwiz.Data.IdentData;

/// <summary>
/// All identifications produced by one search-engine run. Port of
/// <c>pwiz::identdata::SpectrumIdentificationList</c>.
/// </summary>
public sealed class SpectrumIdentificationList : IdentifiableParamContainer
{
    /// <summary>Number of database sequences searched against (informational).</summary>
    public long NumSequencesSearched { get; set; }

    /// <summary>Per-fragment measurement definitions (referenced by
    /// <c>FragmentArray.MeasurePtr</c> on each <c>SpectrumIdentificationItem.Fragmentation</c>
    /// entry).</summary>
    public List<Measure> FragmentationTable { get; } = new();

    /// <summary>Per-spectrum result groups, one per source spectrum.</summary>
    public List<SpectrumIdentificationResult> SpectrumIdentificationResult { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && NumSequencesSearched == 0
        && FragmentationTable.Count == 0
        && SpectrumIdentificationResult.Count == 0;
}
