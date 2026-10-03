using Pwiz.Data.Common.Params;

namespace Pwiz.Data.MsData.Samples;

/// <summary>Description of the sample used to generate the dataset.</summary>
/// <remarks>Port of pwiz::msdata::Sample.</remarks>
public sealed class Sample : ParamContainer
{
    /// <summary>Unique sample identifier within this document.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional friendly name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Creates an empty sample.</summary>
    public Sample() { }

    /// <summary>Creates a sample with the given id and optional name.</summary>
    public Sample(string id, string name = "")
    {
        Id = id ?? string.Empty;
        Name = name ?? string.Empty;
    }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Name) && base.IsEmpty;
}
