using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>The parameters and settings of a search-engine analysis. Port of
/// <c>pwiz::identdata::SpectrumIdentificationProtocol</c>.</summary>
public sealed class SpectrumIdentificationProtocol : Identifiable
{
    /// <summary>Software that runs this protocol.</summary>
    public AnalysisSoftware? AnalysisSoftwarePtr { get; set; }

    /// <summary>Type of search being performed (CV term, e.g. <c>MS_ms_ms_search</c>).</summary>
    public CVParam SearchType { get; set; } = new(CVID.CVID_Unknown);

    /// <summary>Free-form search parameters.</summary>
    public ParamContainer AdditionalSearchParams { get; } = new();

    /// <summary>Variable / fixed modification specifications.</summary>
    public List<SearchModification> ModificationParams { get; } = new();

    /// <summary>Cleavage enzyme(s).</summary>
    public Enzymes Enzymes { get; } = new();

    /// <summary>Mass tables used for residue masses.</summary>
    public List<MassTable> MassTable { get; } = new();

    /// <summary>Fragment-ion mass tolerance.</summary>
    public ParamContainer FragmentTolerance { get; } = new();

    /// <summary>Precursor (parent) mass tolerance.</summary>
    public ParamContainer ParentTolerance { get; } = new();

    /// <summary>Score threshold(s) for accepting a PSM.</summary>
    public ParamContainer Threshold { get; } = new();

    /// <summary>Database filters applied during the search.</summary>
    public List<Filter> DatabaseFilters { get; } = new();

    /// <summary>Translation specification for nucleic-acid databases.</summary>
    public DatabaseTranslation? DatabaseTranslation { get; set; }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && AnalysisSoftwarePtr is null
        && SearchType.IsEmpty
        && AdditionalSearchParams.IsEmpty
        && ModificationParams.Count == 0
        && Enzymes.IsEmpty
        && MassTable.Count == 0
        && FragmentTolerance.IsEmpty
        && ParentTolerance.IsEmpty
        && Threshold.IsEmpty
        && DatabaseFilters.Count == 0
        && DatabaseTranslation is null;
}
