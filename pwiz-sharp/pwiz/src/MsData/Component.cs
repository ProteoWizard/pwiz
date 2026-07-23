using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.MsData.Instruments;

/// <summary>Category of an instrument <see cref="Component"/>.</summary>
public enum ComponentType
{
    /// <summary>Unknown / unspecified.</summary>
    Unknown = -1,
    /// <summary>Ion source.</summary>
    Source = 0,
    /// <summary>Mass analyzer.</summary>
    Analyzer,
    /// <summary>Detector.</summary>
    Detector,
}

/// <summary>A source / analyzer / detector component in an instrument configuration.</summary>
/// <remarks>Port of pwiz::msdata::Component.</remarks>
public sealed class Component : ParamContainer
{
    /// <summary>Source / Analyzer / Detector.</summary>
    public ComponentType Type { get; set; } = ComponentType.Unknown;

    /// <summary>Order from source to detector (1-based).</summary>
    public int Order { get; set; }

    /// <summary>Creates an empty component.</summary>
    public Component() { }

    /// <summary>Creates a component with the given type and order.</summary>
    public Component(ComponentType type, int order) { Type = type; Order = order; }

    /// <summary>
    /// Creates a component and classifies it via <paramref name="cvid"/>
    /// (source/analyzer/detector is derived from the CV term hierarchy).
    /// </summary>
    public Component(CVID cvid, int order)
    {
        Order = order;
        Define(cvid);
    }

    /// <summary>
    /// Adds <paramref name="cvid"/> and sets <see cref="Type"/> from the CV hierarchy
    /// (MS_source / MS_mass_analyzer / MS_detector roots).
    /// </summary>
    public void Define(CVID cvid)
    {
        CVParams.Add(new CVParam(cvid));
        if (CvLookup.CvIsA(cvid, CVID.MS_ionization_type))
            Type = ComponentType.Source;
        else if (CvLookup.CvIsA(cvid, CVID.MS_mass_analyzer_type))
            Type = ComponentType.Analyzer;
        else if (CvLookup.CvIsA(cvid, CVID.MS_detector_type))
            Type = ComponentType.Detector;
    }

    /// <summary>Shortcut to set both type and order, then add the CV term.</summary>
    public void Define(CVID cvid, int order)
    {
        Order = order;
        Define(cvid);
    }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        Type == ComponentType.Unknown && Order == 0 && base.IsEmpty;
}

/// <summary>
/// Ordered list of instrument components. Helpers return the n-th source/analyzer/detector.
/// Port of pwiz::msdata::ComponentList.
/// </summary>
public sealed class ComponentList : List<Component>
{
    /// <summary>The n-th source (0-based within the source subset).</summary>
    public Component Source(int index) => NthOfType(ComponentType.Source, index);

    /// <summary>The n-th analyzer (0-based within the analyzer subset).</summary>
    public Component Analyzer(int index) => NthOfType(ComponentType.Analyzer, index);

    /// <summary>The n-th detector (0-based within the detector subset).</summary>
    public Component Detector(int index) => NthOfType(ComponentType.Detector, index);

    private Component NthOfType(ComponentType type, int index)
    {
        int seen = 0;
        foreach (var c in this)
        {
            if (c.Type == type)
            {
                if (seen == index) return c;
                seen++;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(index),
            $"No {type} component at index {index} (found {seen}).");
    }
}

/// <summary>
/// Acquisition settings configured prior to the run (inclusion/exclusion lists etc.).
/// Port of pwiz::msdata::ScanSettings.
/// </summary>
public sealed class ScanSettings
{
    /// <summary>Unique id for this settings block.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Referenced source files.</summary>
    public List<Pwiz.Data.MsData.Sources.SourceFile> SourceFiles { get; } = new();

    /// <summary>Target list (inclusion list) configured prior to the run.</summary>
    public List<Target> Targets { get; } = new();

    /// <summary>Creates an empty scan-settings block.</summary>
    public ScanSettings() { }

    /// <summary>Creates a scan-settings block with the given id.</summary>
    public ScanSettings(string id) => Id = id ?? string.Empty;

    /// <summary>True iff all fields are empty.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Id) && SourceFiles.Count == 0 && Targets.Count == 0;
}

/// <summary>A scan target (inclusion-list entry). Port of pwiz::msdata::Target.</summary>
public sealed class Target : ParamContainer { }

/// <summary>A particular hardware configuration of the mass spectrometer.</summary>
/// <remarks>Port of pwiz::msdata::InstrumentConfiguration.</remarks>
public sealed class InstrumentConfiguration : ParamContainer
{
    /// <summary>Unique id for this instrument configuration.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Ordered list of components (source/analyzer/detector).</summary>
    public ComponentList ComponentList { get; } = new();

    /// <summary>Software that controls this instrument configuration.</summary>
    public Software? Software { get; set; }

    /// <summary>Scan settings used by this instrument configuration.</summary>
    public ScanSettings? ScanSettings { get; set; }

    /// <summary>Creates an empty configuration.</summary>
    public InstrumentConfiguration() { }

    /// <summary>Creates a configuration with the given id.</summary>
    public InstrumentConfiguration(string id) => Id = id ?? string.Empty;

    /// <inheritdoc/>
    public override bool IsEmpty =>
        string.IsNullOrEmpty(Id)
        && ComponentList.Count == 0
        && Software is null
        && ScanSettings is null
        && base.IsEmpty;
}
