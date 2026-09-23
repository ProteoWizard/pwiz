namespace Pwiz.Data.IdentData;

#pragma warning disable CA1711 // schema-type names ending in Collection match cpp / mzIdentML

/// <summary>
/// Top-level container for input data sets and analysis output sets. Port of
/// <c>pwiz::identdata::DataCollection</c>.
/// </summary>
public sealed class DataCollection
{
    /// <summary>Input files the analyses worked on.</summary>
    public Inputs Inputs { get; } = new();

    /// <summary>Output of the analyses (PSM lists).</summary>
    public AnalysisData AnalysisData { get; } = new();

    /// <summary>True when neither inputs nor analyses are recorded.</summary>
    public bool IsEmpty => Inputs.IsEmpty && AnalysisData.IsEmpty;
}
