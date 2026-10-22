using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>
/// Software used for performing the analyses (search engine, post-processor, etc.). Port of
/// <c>pwiz::identdata::AnalysisSoftware</c>.
/// </summary>
public sealed class AnalysisSoftware : Identifiable
{
    /// <summary>Software version string.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>URI for the software (homepage, vendor page, ...).</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Vendor-specific customizations applied to this software instance.</summary>
    public string Customizations { get; set; } = string.Empty;

    /// <summary>Optional contact role for the software's developer / maintainer.</summary>
    public ContactRole? ContactRolePtr { get; set; }

    /// <summary>The software's CV-named identity (a CV term identifying the software product).</summary>
    public ParamContainer SoftwareName { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(Version)
        && string.IsNullOrEmpty(Uri)
        && string.IsNullOrEmpty(Customizations)
        && ContactRolePtr is null
        && SoftwareName.IsEmpty;
}
