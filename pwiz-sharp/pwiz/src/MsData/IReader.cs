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
    /// When true, a vendor file whose instrument metadata cannot be resolved is an error rather
    /// than something to convert with a guessed instrument. Port of
    /// <c>pwiz::msdata::Reader::Config::unknownInstrumentIsError</c> (<c>Reader.cpp:83-88</c>);
    /// msconvert exposes the inverse as <c>--ignoreUnknownInstrumentError</c>.
    /// </summary>
    /// <remarks>
    /// Defaults to FALSE, as cpp does (<c>Reader.cpp:47</c>). Only msconvert turns it on, by
    /// negating its own flag (<c>msconvert.cpp:507-508</c>, mirrored in
    /// <c>Converter.BuildReaderConfig</c>) - a conversion wants the strict behaviour, because a
    /// silently-substituted instrument is worse than a failed conversion there: downstream tools
    /// key off the instrument configuration and a plausible-but-wrong model propagates unnoticed.
    /// Every other consumer - Skyline, the hosted C API - gets cpp's library default, where an
    /// unresolved instrument is a stderr warning and the read continues with whatever the reader
    /// could determine. Defaulting this to true instead would hard-fail those callers on files
    /// cpp and net472 open without complaint.
    /// </remarks>
    public bool UnknownInstrumentIsError { get; set; }

    /// <summary>
    /// Reports a piece of instrument metadata the reader could not resolve: throws when
    /// <see cref="UnknownInstrumentIsError"/> is set, otherwise writes the message to stderr
    /// and returns so the caller can carry on with a fallback.
    /// </summary>
    /// <remarks>
    /// Port of <c>Reader::Config::instrumentMetadataError</c>, including cpp's choice to write
    /// the non-fatal form straight to stderr rather than through a log sink — msconvert-sharp's
    /// diagnostics go to the same place, and matching keeps the two CLIs' output comparable.
    /// The appended hint names the msconvert flag, exactly as cpp's does, because that is the
    /// only actionable thing a user can do with this message.
    /// </remarks>
    public void InstrumentMetadataError(string message)
    {
        if (UnknownInstrumentIsError)
            throw new IOException(message +
                "; if you want to convert the file anyway, use the ignoreUnknownInstrumentError flag");
        Console.Error.WriteLine(message);
    }

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
    /// When true, the Sciex reader confirms at index-build time that each cycle it is about to
    /// index has readable points, and drops the ones that do not. Off by default.
    /// </summary>
    /// <remarks>
    /// Not a cpp <c>Reader::Config</c> field: cpp always does this
    /// (<c>SpectrumList_ABI.cpp:314</c> requires
    /// <c>getSpectrum(...)-&gt;getDataSize(false, true) &gt; 0</c> as well as a positive BPC
    /// intensity). It is a flag here because the probe reads the file's entire spectral payload
    /// on every open, which on .NET 8 made a large TripleTOF <c>.wiff</c> exceed Skyline
    /// GraphFullScan's scan-load reopen timeout. Conversion wants cpp's exact spectrum set and
    /// pays once, so msconvert-sharp turns it on; Skyline leaves it off and keeps the fast open.
    /// <para>
    /// The cheap half of cpp's test - requiring the cycle TIC to be positive, which catches
    /// cycles whose BPC reports a sentinel base peak - is unconditional and needs no flag. This
    /// covers only the residue: cycles whose BPC *and* TIC both look like real data and which
    /// still read back empty (448 of them in <c>20061108_CPTAC_1B468.wiff</c>). Nothing short of
    /// the read distinguishes those.
    /// </para>
    /// </remarks>
    public bool VerifyNonEmptySpectraAtIndex { get; set; }

    /// <summary>
    /// Threads used to decode mzML binary arrays. 1 (the default) decodes inline on the
    /// read thread, which is the original behaviour; higher values parse a batch of spectra
    /// with decoding deferred and then decode them in parallel.
    /// </summary>
    /// <remarks>
    /// Off by default because the host, not this library, knows what else is already running.
    /// The two shapes differ enough that neither default suits both:
    /// <list type="bullet">
    ///   <item>
    ///     Skyline and MsConvertGUI process multiple FILES at once. That is the established
    ///     answer for vendor formats, which are not purely I/O bound and go faster several at
    ///     a time - and where per-file threading is the only safe axis, since a vendor reader
    ///     is thread-safe on its own thread but not necessarily for concurrent spectrum
    ///     retrieval. A host doing that should leave this at 1 rather than go wide underneath
    ///     its own parallelism.
    ///   </item>
    ///   <item>
    ///     A host that reads one file at a time should set it, but modestly: measured on a
    ///     5.99 GB Astral mzML, 1 thread takes 83.4s and 8 takes 54.6s, after which the curve
    ///     is flat - 16, 24 and 32 all land within a second of 8. The floor is the XML parse,
    ///     which stays serial, so past the knee extra threads only add memory pressure (each
    ///     batch holds 64 spectra). 8 is a reasonable default; core count buys nothing.
    ///     Osprey funnels its reads through a one-permit gate, so its read phase has the
    ///     machine to itself and nothing competes with the decode.
    ///   </item>
    /// </list>
    /// <para>
    /// Note what this does and does not parallelise. The XML parse stays single-threaded on
    /// the one stream; only the base64/zlib decode of already-extracted payloads goes wide. So
    /// no reader is ever asked for spectra concurrently, and this composes with a host's
    /// per-file parallelism rather than contending with it for thread-safety. It is honoured
    /// by the mzML reader alone, in the same way <see cref="VerifyNonEmptySpectraAtIndex"/> is
    /// read by one reader. Values below 1 are treated as 1, and the count is clamped to the
    /// processor count.
    /// </para>
    /// </remarks>
    public int MzmlDecodeThreads { get; set; } = 1;

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
    /// take the SDK's <see cref="DateTime"/> as a naive wall clock and - if the flag is set - add
    /// <c>universal_time - local_time</c>, producing the local wall-clock value tagged with a
    /// <c>Z</c> suffix. cpp's logic is questionable as a "real" UTC value but it's the canonical
    /// msconvert behavior, so we match byte-for-byte. Note the conversion to UTC is NOT done
    /// here: <c>ShimadzuReader.cpp:370</c> is the only cpp vendor reader that calls
    /// <c>ToUniversalTime</c>, and it does so before this point, so only Shimadzu's caller does.
    /// </remarks>
    public static string? FormatStartTimeStamp(DateTime sdkDate, bool adjustToHostTimeZone)
    {
        if (sdkDate == default) return null;

        // cpp works from the SDK's NAIVE wall-clock reading (a boost ptime, which carries no
        // zone) and adds the offset to it. Converting first with ToUniversalTime would apply
        // the offset a second time whenever the SDK hands back Kind=Local/Unspecified - which
        // is what made Mobilion timestamps land 8 hours out instead of 4.
        // cpp clamps before it builds the ptime, i.e. before the host-zone shift below.
        DateTime stamp = ClampToBoostDateRange(DateTime.SpecifyKind(sdkDate, DateTimeKind.Unspecified));
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
    /// Floors a vendor timestamp at the year 1400, preserving month, day and time of day.
    /// </summary>
    /// <param name="value">A vendor acquisition time, possibly corrupt.</param>
    /// <remarks>
    /// Five cpp vendor APIs apply exactly this, with the same <c>AddYears(1400 - Year)</c>
    /// arithmetic and the same comment about a corrupt date in a test file:
    /// <c>MassHunterData.cpp:487</c>, <c>MidacData.cpp:217</c>, <c>ShimadzuReader.cpp:375</c>,
    /// <c>UIMFReader.cpp:220</c>, <c>UnifiData.cpp:1055</c>. 1400-01-01 is the earliest
    /// <c>boost::gregorian::date</c> can represent and constructing one below it throws, so cpp
    /// has to clamp before it can encode. A corrupt date therefore surfaces in cpp as
    /// 1400-MM-DD with the rest intact: an Agilent file reading 0412-01-11T05:31:47 comes out as
    /// 1400-01-11T05:31:47, and a Mobilion file whose ACQ_TIMESTAMP was never set (0001-01-01)
    /// as 1400-01-01. cpp also caps the year at 10000; <see cref="DateTime"/> stops at 9999, so
    /// that half cannot arise here.
    /// </remarks>
    public static DateTime ClampToBoostDateRange(DateTime value) =>
        value.Year < 1400 ? value.AddYears(1400 - value.Year) : value;

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

    /// <summary>
    /// The reader's own name for the format at <paramref name="filename"/>, or empty when it
    /// does not recognize it. Port of cpp <c>Reader::identify</c>, which returns a type STRING
    /// rather than a CVID. cpp gives each Bruker sub-format its own Reader class with its own
    /// getType(); this port has a single Reader_Bruker that discriminates inside Identify, so
    /// that reader overrides this to report the specific format.
    /// </summary>
    string IdentifyType(string filename, string? head)
        => Identify(filename, head) != CVID.CVID_Unknown ? TypeName : string.Empty;
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
