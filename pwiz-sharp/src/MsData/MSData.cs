using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;

namespace Pwiz.Data.MsData;

/// <summary>Root element: captures the mass-spec run, metadata, and all associated descriptors.</summary>
/// <remarks>Port of pwiz::msdata::MSData.</remarks>
public sealed class MSData
{
    /// <summary>pwiz software version string, emitted into softwareList / dataProcessing entries.</summary>
    public const string PwizVersion = "3.0.26056";

    /// <summary>Default list of controlled vocabularies used in a freshly-constructed document.</summary>
    public static IReadOnlyList<CV> DefaultCVList { get; } = BuildDefaultCVList();

    private static CV[] BuildDefaultCVList() => new[]
    {
        CvLookup.GetCv("MS"),
        CvLookup.GetCv("UO"),
    };

    /// <summary>Optional accession number.</summary>
    public string Accession { get; set; } = string.Empty;

    /// <summary>Optional id (LSID preferred).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Controlled vocabulary definitions referenced from the document.</summary>
    public List<CV> CVs { get; } = new();

    /// <summary>Document-scoped metadata (source files, contacts, content summary).</summary>
    public Sources.FileDescription FileDescription { get; set; } = new();

    /// <summary>Referenceable ParamGroups.</summary>
    public List<ParamGroup> ParamGroups { get; } = new();

    /// <summary>Samples.</summary>
    public List<Sample> Samples { get; } = new();

    /// <summary>Software used to acquire/process the data.</summary>
    public List<Software> Software { get; } = new();

    /// <summary>Scan-settings blocks.</summary>
    public List<ScanSettings> ScanSettings { get; } = new();

    /// <summary>Instrument configurations.</summary>
    public List<InstrumentConfiguration> InstrumentConfigurations { get; } = new();

    /// <summary>Data-processing blocks.</summary>
    public List<DataProcessing> DataProcessings { get; } = new();

    /// <summary>The single run.</summary>
    public Run Run { get; set; } = new();

    /// <summary>mzML schema version of the backing file (if any); blank for programmatic docs.</summary>
    public string Version { get; set; } = string.Empty;

    private int _nFiltersApplied;

    /// <summary>Increments the applied-filter counter (used to detect out-of-order filter chains).</summary>
    public void FilterApplied() => _nFiltersApplied++;

    /// <summary>Number of filters applied to this document.</summary>
    public int CountFiltersApplied() => _nFiltersApplied;

    /// <summary>True iff all metadata and the run are empty.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Accession)
        && string.IsNullOrEmpty(Id)
        && CVs.Count == 0
        && FileDescription.IsEmpty
        && ParamGroups.Count == 0
        && Samples.Count == 0
        && Software.Count == 0
        && ScanSettings.Count == 0
        && InstrumentConfigurations.Count == 0
        && DataProcessings.Count == 0
        && Run.IsEmpty;

    /// <summary>
    /// All <see cref="DataProcessing"/> blocks, including those coming from the spectrum/chromatogram lists.
    /// </summary>
    public IReadOnlyList<DataProcessing> AllDataProcessings
    {
        get
        {
            var combined = new List<DataProcessing>(DataProcessings);
            if (Run.SpectrumList?.DataProcessing is { } sdp && !combined.Contains(sdp))
                combined.Add(sdp);
            if (Run.ChromatogramList?.DataProcessing is { } cdp && !combined.Contains(cdp))
                combined.Add(cdp);
            return combined;
        }
    }
}
