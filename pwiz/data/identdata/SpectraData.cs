using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>
/// A spectra source file referenced by an mzIdentML document. Port of
/// <c>pwiz::identdata::SpectraData</c>.
/// </summary>
public sealed class SpectraData : Identifiable
{
    /// <summary>File system / URI location of the spectra file.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Format CV term (e.g. <c>MS_mzML_format</c>).</summary>
    public CVParam FileFormat { get; set; } = new(CVID.CVID_Unknown);

    /// <summary>Spectrum-id format CV term (e.g. <c>MS_Thermo_nativeID_format</c>).</summary>
    public CVParam SpectrumIDFormat { get; set; } = new(CVID.CVID_Unknown);

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(Location)
        && FileFormat.IsEmpty
        && SpectrumIDFormat.IsEmpty;
}
