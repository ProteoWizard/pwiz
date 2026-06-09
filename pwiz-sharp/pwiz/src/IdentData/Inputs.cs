using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>
/// Input data sets for the analyses (databases, spectra, source files). Port of
/// <c>pwiz::identdata::Inputs</c>.
/// </summary>
public sealed class Inputs
{
    /// <summary>Source files the mzIdentML document was generated from.</summary>
    public List<SourceFile> SourceFile { get; } = new();

    /// <summary>Search databases consulted by the analyses.</summary>
    public List<SearchDatabase> SearchDatabase { get; } = new();

    /// <summary>Spectra files queried by the analyses.</summary>
    public List<SpectraData> SpectraData { get; } = new();

    /// <summary>True when no inputs have been recorded.</summary>
    public bool IsEmpty => SourceFile.Count == 0 && SearchDatabase.Count == 0 && SpectraData.Count == 0;
}

/// <summary>A file from which this mzIdentML instance was created. Lightweight stub. Port of
/// <c>pwiz::identdata::SourceFile</c>.</summary>
public sealed class SourceFile : IdentifiableParamContainer
{
    /// <summary>File system / URI location.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Format CV term (e.g. <c>MS_pepXML_format</c>).</summary>
    public CVParam FileFormat { get; set; } = new(CVID.CVID_Unknown);
}
