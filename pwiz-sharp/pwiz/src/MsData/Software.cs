using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.MsData.Instruments;

/// <summary>A piece of software referenced by the mzML document.</summary>
/// <remarks>Port of pwiz::msdata::Software.</remarks>
public sealed class Software : ParamContainer
{
    /// <summary>Unique id across all software in the document.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Software version string.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Creates an empty Software.</summary>
    public Software() { }

    /// <summary>Creates a Software with the given id.</summary>
    public Software(string id) => Id = id ?? string.Empty;

    /// <summary>Creates a Software with id, a CV term, and version.</summary>
    public Software(string id, CVParam param, string version)
    {
        Id = id ?? string.Empty;
        Version = version ?? string.Empty;
        if (param is not null && !param.IsEmpty) CVParams.Add(param);
    }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Version) && base.IsEmpty;
}
