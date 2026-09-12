using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>Database filter applied during a search. Port of <c>pwiz::identdata::Filter</c>.</summary>
public sealed class Filter
{
    /// <summary>Filter type CV terms (e.g. <c>MS_DB_filter_taxonomy</c>).</summary>
    public ParamContainer FilterType { get; } = new();

    /// <summary>Inclusion criteria.</summary>
    public ParamContainer Include { get; } = new();

    /// <summary>Exclusion criteria.</summary>
    public ParamContainer Exclude { get; } = new();

    /// <summary>True when no filter has been recorded.</summary>
    public bool IsEmpty => FilterType.IsEmpty && Include.IsEmpty && Exclude.IsEmpty;
}

/// <summary>Codon → amino acid translation table (e.g. NCBI table 1). Port of
/// <c>pwiz::identdata::TranslationTable</c>.</summary>
public sealed class TranslationTable : IdentifiableParamContainer { }

/// <summary>Specifies how a nucleic-acid database was translated for searching. Port of
/// <c>pwiz::identdata::DatabaseTranslation</c>.</summary>
public sealed class DatabaseTranslation
{
    /// <summary>Reading frames considered (-3..-1, 1..3).</summary>
    public List<int> Frames { get; } = new();

    /// <summary>Translation tables used for each frame.</summary>
    public List<TranslationTable> TranslationTables { get; } = new();

    /// <summary>True when no frames or tables have been recorded.</summary>
    public bool IsEmpty => Frames.Count == 0 && TranslationTables.Count == 0;
}
