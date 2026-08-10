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
    /// Honored by the same readers that consume it in cpp, and only on the paths cpp consumes
    /// it on — msconvert exposes it as <c>--ignoreMissingZeroSamples</c>:
    /// <list type="bullet">
    /// <item>Sciex (<c>SpectrumList_ABI.cpp:254</c>/<c>:261</c>) — suppresses the SDK's
    /// synthetic framing zeros, changing both the arrays and <c>defaultArrayLength</c>.</item>
    /// <item>Agilent (<c>SpectrumList_Agilent.cpp:434</c>/<c>:541</c>) — combine-IMS only.</item>
    /// <item>Waters (<c>SpectrumList_Waters.cpp:581</c>) — combine-IMS only.</item>
    /// <item>Mobilion (<c>SpectrumList_Mobilion.cpp:182</c>/<c>:305</c>) and
    /// UIMF (<c>SpectrumList_UIMF.cpp:141</c>).</item>
    /// </list>
    /// Bruker ignores it in cpp too (its uses are commented out). For the vendors that never
    /// look at it, the downstream filter <c>SpectrumList_ZeroSamplesFilter</c> (in
    /// <c>Pwiz.Analysis</c>) covers the common case via <c>--filter "zeroSamples removeExtra"</c>.
    /// </remarks>
    public bool IgnoreZeroIntensityPoints { get; set; }

    /// <summary>
    /// When true, spectra with zero peaks survive into the output instead of being dropped.
    /// Default false matches pwiz cpp's behavior of suppressing the empty-payload spectra
    /// vendor SDKs sometimes emit at the start/end of an acquisition. Port of
    /// <c>pwiz::msdata::Reader::Config::acceptZeroLengthSpectra</c>.
    /// </summary>
    /// <remarks>
    /// Its documented purpose in cpp is "skip expensive checking for empty spectra when opening
    /// a file", so it is a performance knob as much as a content one. Honored by the same
    /// readers that consume it in cpp, and only on the paths cpp consumes it on — msconvert
    /// exposes it as <c>--acceptZeroLengthSpectra</c>:
    /// <list type="bullet">
    /// <item>Sciex (<c>SpectrumList_ABI.cpp:298</c>) — the index is built off the TIC instead of
    /// the BPC and the per-cycle <c>getDataSize</c> probe is skipped, so empty cycles survive
    /// (on wiff2 zero-intensity cycles are still dropped, and Product experiments still require
    /// precursor info). Also (<c>SpectrumList_ABI.cpp:240</c>) suppresses the
    /// <c>base peak m/z</c> / <c>base peak intensity</c> cvParams, whose lookup is what forces
    /// the SDK to build the base-peak chromatogram.</item>
    /// <item>Agilent (<c>SpectrumList_Agilent.cpp:678</c>) — ion-mobility, non-combined only:
    /// every drift bin is indexed instead of only MIDAC's non-empty ones.</item>
    /// </list>
    /// UIMF (<c>SpectrumList_UIMF.cpp:236</c>) and UNIFI (<c>SpectrumList_UNIFI.cpp:195</c>) have
    /// their uses commented out in cpp, so those readers deliberately ignore it here too. No
    /// other vendor consumes it.
    /// </remarks>
    public bool AcceptZeroLengthSpectra { get; set; }

    /// <summary>
    /// When true, MS2+ spectra without precursor info are kept rather than dropped. The
    /// default (false) drops them, matching pwiz cpp. Port of
    /// <c>pwiz::msdata::Reader::Config::allowMsMsWithoutPrecursor</c>.
    /// </summary>
    /// <remarks>
    /// Advisory like <see cref="AcceptZeroLengthSpectra"/> — round-tripped through the API so
    /// callers (SeeMS, msconvert-sharp) can pass it without the option silently disappearing.
    /// </remarks>
    public bool AllowMsMsWithoutPrecursor { get; set; }

    /// <summary>
    /// When true, the Bruker TIMS reader emits one combined spectrum per diaPASEF frame
    /// (with the entire isolation-window range as a single chunk) instead of breaking the
    /// frame into per-window spectra. Used by Skyline's Bruker diaPASEF import path when
    /// the file declares a window-group table; harmless on non-diaPASEF Bruker data.
    /// Port of <c>pwiz::msdata::Reader::Config::passEntireDiaPasefFrame</c>.
    /// </summary>
    /// <remarks>
    /// Currently advisory: the C# Bruker reader does not yet implement the frame-combination
    /// path. When set, callers see one spectrum per isolation window as today. Wiring the
    /// behavior through <c>SpectrumList_Bruker.GetSpectrum</c> is a follow-up that will
    /// match cpp <c>SpectrumList_Bruker.cpp:286-442</c>.
    /// </remarks>
    public bool PassEntireDiaPasefFrame { get; set; }

    /// <summary>
    /// When true (default, matches cpp), if the vendor file's acquisition timestamp doesn't
    /// carry a time zone the reader treats the wall-clock value as if it had been recorded in
    /// the host machine's local time and converts to UTC for the emitted <c>startTimeStamp</c>.
    /// When false, the reader treats the unknown-TZ timestamp as already being UTC.
    /// Port of <c>pwiz::msdata::Reader::Config::adjustUnknownTimeZonesToHostTimeZone</c>
    /// (default <c>true</c> in <c>Reader.cpp:48</c>). The vendor harness flips this off so
    /// reference mzMLs don't drift with the build agent's time zone.
    /// </summary>
    public bool AdjustUnknownTimeZonesToHostTimeZone { get; set; } = true;

    /// <summary>
    /// Encodes <paramref name="sdkDate"/> as an mzML <c>startTimeStamp</c> attribute string
    /// (<c>YYYY-MM-DDTHH:MM:SSZ</c>), applying the cpp-equivalent host-tz adjustment when
    /// <paramref name="adjustToHostTimeZone"/> is set. Returns <c>null</c> for the default
    /// <see cref="DateTime"/>.
    /// </summary>
    /// <param name="sdkDate">The vendor SDK's acquisition time, unmodified.</param>
    /// <param name="adjustToHostTimeZone">
    /// Passed per call rather than read from <see cref="AdjustUnknownTimeZonesToHostTimeZone"/>,
    /// because cpp decides per READER, not globally: only Thermo, ABI and Shimadzu honour the
    /// config flag (their SDKs return a zone-less timestamp). Agilent passes a literal
    /// <c>false</c> - "all but the oldest Agilent formats state their time zones" - and Bruker,
    /// Mobilion, Waters, UIMF and UNIFI never adjust at all. Readers that adjusted here when
    /// cpp did not were the reason msconvert-sharp's timestamps drifted from msconvert's.
    /// </param>
    /// <remarks>
    /// Mirrors cpp <c>ShimadzuReader.cpp:365-388</c> / <c>WiffFile.cpp::getSampleAcquisitionTime</c>:
    /// take the SDK's <see cref="DateTime"/> (typically <c>Kind=Local</c> on the host), convert
    /// to UTC via <see cref="DateTime.ToUniversalTime"/>, and — if the flag is set — add
    /// <c>universal_time - local_time</c> back, producing the local wall-clock value tagged
    /// with a <c>Z</c> suffix. cpp's logic is questionable as a "real" UTC value but it's the
    /// canonical msconvert behavior, so we match byte-for-byte.
    /// </remarks>
    public static string? FormatStartTimeStamp(DateTime sdkDate, bool adjustToHostTimeZone)
    {
        if (sdkDate == default) return null;

        // cpp works from the SDK's NAIVE wall-clock reading (a boost ptime, which carries no
        // zone) and adds the offset to it. Converting first with ToUniversalTime would apply
        // the offset a second time whenever the SDK hands back Kind=Local/Unspecified - which
        // is what made Mobilion timestamps land 8 hours out instead of 4.
        DateTime stamp = DateTime.SpecifyKind(sdkDate, DateTimeKind.Unspecified);
        if (adjustToHostTimeZone)
        {
            // cpp: pt + (universal_time() - local_time()), i.e. plus the host's current offset
            // from UTC. GetUtcOffset returns local-minus-UTC (-04:00 on EDT), so subtracting it
            // adds the same amount. Both use "now" rather than the file's date, so a file
            // acquired under the other side of DST gets today's offset - matching cpp, quirk
            // and all.
            stamp -= TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        }
        return $"{stamp.Year:D4}-{stamp.Month:D2}-{stamp.Day:D2}T{stamp.Hour:D2}:{stamp.Minute:D2}:{stamp.Second:D2}Z";
    }

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

    /// <summary>
    /// When true, Waters SONAR data reports the drift-time array as SONAR bin numbers
    /// rather than actual drift times. Advisory; the C# Waters reader doesn't yet emit
    /// SONAR-specific arrays. Round-tripped for Skyline's pwiz.CLI-parity Config.
    /// </summary>
    public bool ReportSonarBins { get; set; }

    /// <summary>
    /// When true, Bruker isolation-window arrays are included in the emitted spectra.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>, matching cpp <c>Reader.cpp:56</c>. It has exactly one effect in
    /// either tree: <c>SpectrumList_Bruker.cpp:442</c> gates the scanning-quadrupole lower/upper
    /// bound m/z arrays on <c>isPassEntireDiaPasefFrame() &amp;&amp; includeIsolationArrays</c>, so it
    /// reaches nothing outside whole-frame diaPASEF Bruker MS2 spectra.
    /// This defaulted to <c>false</c> while the whole-frame path was unreachable in pwiz-sharp,
    /// so the old "Skyline infers isolation from WindowGroup/IM cvParams" rationale was never
    /// exercised - and Skyline, reading Bruker through the cpp reader, has always received these
    /// arrays. Emitting 3 arrays where msconvert emits 5 was the divergence, not the parity.
    /// </remarks>
    public bool IncludeIsolationArrays { get; set; } = true;

    /// <summary>
    /// When true (Waters lockmass), spectra flagged as calibration/lockmass are omitted
    /// from the output. Advisory; readers apply the filter downstream.
    /// </summary>
    public bool CalibrationSpectraAreOmitted { get; set; }

    /// <summary>Default configuration.</summary>
    public static ReaderConfig Default { get; } = new();
}

/// <summary>
/// Optional capability interface for vendor readers that support enumerating multiple
/// runs/samples within a single container file (Sciex .wiff, Shimadzu .lcd, etc.).
/// <see cref="Pwiz.Data.MsData.Readers.ReaderList.ReadIds"/> dispatches to this interface
/// when the identified reader implements it, returning sample-level identifiers instead
/// of spectrum ids.
/// </summary>
public interface IMultiSampleReader
{
    /// <summary>
    /// Enumerates the sample names available inside <paramref name="filename"/>.
    /// One entry per sample; index into this array is the <c>sampleIndex</c> parameter
    /// consumed by <see cref="Readers.ReaderList.Read(string, MSData, int, ReaderConfig?)"/>.
    /// </summary>
    string[] EnumerateSampleNames(string filename);
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
