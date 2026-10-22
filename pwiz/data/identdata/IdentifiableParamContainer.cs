using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>
/// Base for mzIdentML schema types that combine an <see cref="Id"/> + <see cref="Name"/> with
/// CV / user param containers. Port of <c>pwiz::identdata::IdentifiableParamContainer</c>.
/// </summary>
public abstract class IdentifiableParamContainer : ParamContainer
{
    /// <summary>Unique identifier within the mzIdentML document scope.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional human-readable name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override bool IsEmpty => string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Name) && base.IsEmpty;
}
