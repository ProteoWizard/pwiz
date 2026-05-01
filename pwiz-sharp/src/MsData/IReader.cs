using Pwiz.Data.Common.Chemistry;
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

    /// <summary>
    /// When true, SRM (Selected Reaction Monitoring) scans are exposed as individual spectra.
    /// When false (default), SRM transitions are grouped by (Q1, Q3) into SRM SIC
    /// chromatograms and removed from the spectrum list. Port of
    /// <c>pwiz::msdata::Reader::Config::srmAsSpectra</c>.
    /// </summary>
    public bool SrmAsSpectra { get; set; }

    /// <summary>
    /// When true (test/reference-parity mode), Bruker combined-IMS spectra sort their merged
    /// peak arrays by m/z and add a 1e-8 jitter to duplicate m/z values so std::sort-style
    /// tie-break ordering is reproducible. Production conversions leave this off — mzML
    /// doesn't mandate m/z ordering and downstream tools handle either layout. Port of
    /// <c>pwiz::msdata::Reader::Config::sortAndJitter</c>.
    /// </summary>
    public bool SortAndJitter { get; set; }

    /// <summary>
    /// Hint to the reader that the caller will apply vendor peak picking downstream. Bruker's
    /// reader currently uses this to keep the global TIC/BPC chromatograms emitted in
    /// combine-ion-mobility mode (pwiz C++ centroid+combineIMS reference mzMLs include them
    /// while non-centroid combineIMS refs omit them). Port of
    /// <c>pwiz::msdata::Reader::Config::peakPicking</c>.
    /// </summary>
    public bool PeakPicking { get; set; }

    /// <summary>
    /// When true, vendor-specific DDA preprocessing kicks in (currently Waters MassLynx
    /// DDA processor only). The reader emits one spectrum per DDA-processed scan with
    /// merged-scan ids when multiple raw scans contribute to a single DDA precursor. Port of
    /// <c>pwiz::msdata::Reader::Config::ddaProcessing</c>.
    /// </summary>
    public bool DdaProcessing { get; set; }

    /// <summary>
    /// When true, the global TIC chromatogram only sums MS1 functions — non-MS1 functions
    /// (and Waters function-1 promoted by the MSe heuristic) are excluded. Port of
    /// <c>pwiz::msdata::Reader::Config::globalChromatogramsAreMs1Only</c>.
    /// </summary>
    public bool GlobalChromatogramsAreMs1Only { get; set; }

    /// <summary>
    /// When true, vendor-flagged calibration scans (e.g. Waters lockmass function) are
    /// excluded from the spectrum list. Port of
    /// <c>pwiz::msdata::Reader::Config::ignoreCalibrationScans</c>.
    /// </summary>
    public bool IgnoreCalibrationScans { get; set; }

    /// <summary>
    /// Optional list of (m/z, mobility-bounds) windows used to filter combine-IMS spectra:
    /// only peaks whose drift time falls inside one of the windows are retained. Port of
    /// <c>pwiz::msdata::Reader::Config::isolationMzAndMobilityFilter</c>.
    /// </summary>
    public List<MzMobilityWindow> IsolationMzAndMobilityFilter { get; set; } = new();

    /// <summary>
    /// When true, profile-mode peaks whose intensity is exactly zero are dropped from the
    /// emitted m/z and intensity arrays. Some vendors emit zero-intensity flanking points
    /// around real peaks; downstream tools (and SeeMS) prefer the trimmed form. Port of
    /// <c>pwiz::msdata::Reader::Config::ignoreZeroIntensityPoints</c>.
    /// </summary>
    /// <remarks>
    /// This flag is currently advisory: it round-trips through <see cref="ReaderConfig"/> but
    /// no pwiz-sharp reader acts on it yet. The downstream filter
    /// <c>SpectrumList_ZeroSamplesFilter</c> (in <c>Pwiz.Analysis</c>) covers the common case
    /// when applied via <c>--filter "zeroSamples removeExtra"</c>.
    /// </remarks>
    public bool IgnoreZeroIntensityPoints { get; set; }

    /// <summary>
    /// When true, spectra with zero peaks survive into the output instead of being dropped.
    /// Default false matches pwiz cpp's behavior of suppressing the empty-payload spectra
    /// vendor SDKs sometimes emit at the start/end of an acquisition. Port of
    /// <c>pwiz::msdata::Reader::Config::acceptZeroLengthSpectra</c>.
    /// </summary>
    /// <remarks>
    /// Advisory like <see cref="IgnoreZeroIntensityPoints"/> — no pwiz-sharp reader currently
    /// applies the filter. Round-tripping ensures SeeMS and msconvert-sharp can request it
    /// today and behavior fills in as readers are upgraded.
    /// </remarks>
    public bool AcceptZeroLengthSpectra { get; set; }

    /// <summary>
    /// When true, MS2+ spectra without precursor info are kept rather than dropped. The
    /// default (false) drops them, matching pwiz cpp. Port of
    /// <c>pwiz::msdata::Reader::Config::allowMsMsWithoutPrecursor</c>.
    /// </summary>
    /// <remarks>
    /// Advisory like the two above — round-tripped through the API so callers (SeeMS,
    /// msconvert-sharp) can pass it without the option silently disappearing.
    /// </remarks>
    public bool AllowMsMsWithoutPrecursor { get; set; }

    /// <summary>
    /// For multi-run input files (multi-sample WIFF, multi-run MGF), the zero-based run index
    /// to load. Default 0 = first run. Port of pwiz cpp's <c>runIndex</c> parameter on
    /// <c>MSDataFile::read</c> (carried as a config field here so it round-trips through
    /// <see cref="IReader.Read"/> alongside the other reader-wide options).
    /// </summary>
    /// <remarks>
    /// Multi-run support isn't wired into the pwiz-sharp readers yet; right now every reader
    /// loads run 0. SeeMS's <c>MSDataRunPath</c> already parses <c>path:N</c> URIs into
    /// (filename, RunIndex) so when multi-run vendor reads land, the SeeMS call sites already
    /// pass through the correct index.
    /// </remarks>
    public int RunIndex { get; set; }

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

/// <summary>
/// Thrown by an <see cref="IReader"/>'s <see cref="IReader.Read"/> when the reader recognizes
/// the format but its vendor SDK isn't compiled into this build (i.e. the project was built
/// without <c>-p:IAgreeToVendorLicenses=true</c>). Distinct from
/// <see cref="NotSupportedException"/>, which a reader still legitimately throws for
/// formats it hasn't ported yet but whose SDK <em>is</em> available.
/// </summary>
public sealed class VendorSupportNotEnabledException : NotSupportedException
{
    /// <summary>Creates the exception with a default message.</summary>
    public VendorSupportNotEnabledException() : base() { }

    /// <summary>Creates the exception with the given message.</summary>
    public VendorSupportNotEnabledException(string? message) : base(message) { }

    /// <summary>Creates the exception with the given message and inner exception.</summary>
    public VendorSupportNotEnabledException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
