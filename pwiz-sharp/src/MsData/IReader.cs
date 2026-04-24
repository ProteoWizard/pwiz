using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.MsData.Readers;

/// <summary>
/// Reader-wide configuration. A subset of pwiz::msdata::Reader::Config — only the fields
/// that mean anything for format-level readers (vendor-specific options live in their own reader classes).
/// </summary>
public sealed class ReaderConfig
{
    /// <summary>If true and <see cref="IReader.Read(string, MSData, ReaderConfig)"/> hits an unknown format, throw instead of silently producing an empty document.</summary>
    public bool UnknownFormatIsError { get; set; } = true;

    /// <summary>
    /// When non-zero, the reader only emits spectra at this MS level (1 or 2). Saves a lot of
    /// work when downstream consumers only need MS1s (deisotoping) or MS2s (identification).
    /// Port of <c>pwiz::msdata::Reader::Config::preferOnlyMsLevel</c>.
    /// </summary>
    public int PreferOnlyMsLevel { get; set; }

    /// <summary>
    /// When true, ion-mobility-aware readers emit one combined spectrum per frame (Bruker TDF)
    /// or per isolation window (Bruker DIA/PASEF), summing across the mobility axis. Port of
    /// <c>pwiz::msdata::Reader::Config::combineIonMobilitySpectra</c>.
    /// </summary>
    public bool CombineIonMobilitySpectra { get; set; }

    /// <summary>
    /// When true, SIM (Selected Ion Monitoring) scans are exposed as individual spectra.
    /// When false (default), SIM scans are grouped by Q1 into SIM chromatograms and removed
    /// from the spectrum list. Port of <c>pwiz::msdata::Reader::Config::simAsSpectra</c>.
    /// </summary>
    public bool SimAsSpectra { get; set; }

    /// <summary>Default configuration.</summary>
    public static ReaderConfig Default { get; } = new();
}

/// <summary>
/// A reader that identifies and parses one or more mass-spec data file formats into an <see cref="MSData"/>.
/// Port of pwiz::msdata::Reader.
/// </summary>
public interface IReader
{
    /// <summary>Stable string identifier for this reader (e.g. "mzML", "MGF").</summary>
    string TypeName { get; }

    /// <summary>
    /// CV accession for the file format this reader produces
    /// (e.g. <see cref="CVID.MS_mzML_format"/> / <see cref="CVID.MS_Mascot_MGF_format"/>).
    /// </summary>
    CVID CvType { get; }

    /// <summary>File extensions (including the leading period) that this reader typically handles.</summary>
    IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Returns <see cref="CvType"/> iff this reader recognizes <paramref name="filename"/> / <paramref name="head"/>,
    /// otherwise <see cref="CVID.CVID_Unknown"/>. <paramref name="head"/> is the first few KB of the file
    /// (can be null if the caller only wants filename-based identification).
    /// </summary>
    CVID Identify(string filename, string? head);

    /// <summary>Reads the file into <paramref name="result"/>. Throws on unrecognized input.</summary>
    void Read(string filename, MSData result, ReaderConfig? config = null);
}

/// <summary>Convenience helpers for <see cref="IReader"/>.</summary>
public static class ReaderExtensions
{
    /// <summary>True iff the reader recognizes the file (identify returns a non-unknown CVID).</summary>
    public static bool Accepts(this IReader reader, string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.Identify(filename, head) != CVID.CVID_Unknown;
    }
}
