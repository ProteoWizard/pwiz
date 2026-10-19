namespace Pwiz.Data.IdentData;

/// <summary>
/// Container for all data sets produced by analyses. Port of <c>pwiz::identdata::AnalysisData</c>.
/// </summary>
public sealed class AnalysisData
{
    /// <summary>Per-search results (one list per search-engine run).</summary>
    public List<SpectrumIdentificationList> SpectrumIdentificationList { get; } = new();

    /// <summary>Optional protein-detection output (assembled protein list).</summary>
    public ProteinDetectionList? ProteinDetectionListPtr { get; set; }

    /// <summary>True when no analyses have been recorded.</summary>
    public bool IsEmpty => SpectrumIdentificationList.Count == 0 && ProteinDetectionListPtr is null;
}
