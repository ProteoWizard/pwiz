using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Tools.MsConvert;

/// <summary>
/// Parsed command-line options for msconvert-sharp. The field set mirrors pwiz C++
/// <c>msconvert.exe --help</c> output so the CLIs are drop-in compatible.
/// </summary>
/// <remarks>
/// <para>Writer-side settings (format, encoder config, indexed/gzip, mzMLb knobs) live on
/// <see cref="WriteConfig"/> — any tool calling pwiz-sharp's writers uses that same type.
/// This class adds the msconvert-CLI-specific surface: input file collection, merge mode,
/// per-source-file post-processing, contact info, filter pipelines, etc.</para>
/// <para>Not every option is wired to working behavior yet — switches for unimplemented
/// output formats or encoding modes throw a clear error at convert time. The remaining
/// options that record intent but are no-ops in our simpler pipeline are documented on
/// each property.</para>
/// </remarks>
public sealed class MsConvertConfig
{
    /// <summary>Input file paths (collected positionally and from <c>--filelist</c>).</summary>
    public List<string> InputFiles { get; } = new();

    /// <summary>Output directory (default: current directory).</summary>
    public string OutputPath { get; set; } = ".";

    /// <summary>Explicit output filename (overrides the derived name).</summary>
    public string? OutFile { get; set; }

    /// <summary>Output extension override (derived from <see cref="WriteConfig.Format"/> when null).</summary>
    public string? OutputExtension { get; set; }

    /// <summary>
    /// Writer-side config (format, encoder, indexed/gzip, mzMLb knobs).
    /// Defaults match pwiz C++ msconvert: 64-bit m/z + retention time, 32-bit intensity,
    /// zlib compression. (See <c>msconvert.cpp</c>: <c>bool zlib = true</c> at the option
    /// declaration plus the unconditional 32-bit intensity override around the precision-flag block.)
    /// </summary>
    public WriteConfig WriteConfig { get; } = new()
    {
        EncoderConfig = new BinaryEncoderConfig
        {
            Compression = BinaryCompression.Zlib,
            PrecisionOverrides =
            {
                [CVID.MS_intensity_array] = BinaryPrecision.Bits32,
            },
        },
    };

    /// <summary>Spectrum-list filter specs applied left-to-right.</summary>
    public List<string> Filters { get; } = new();

    /// <summary>Chromatogram-list filter specs applied left-to-right.</summary>
    public List<string> ChromatogramFilters { get; } = new();

    /// <summary>Mantissa-truncation bits for m/z and retention-time arrays. Unimplemented.</summary>
    public int MzTruncation { get; set; }

    /// <summary>Mantissa-truncation bits for intensity arrays. Unimplemented.</summary>
    public int IntenTruncation { get; set; }

    /// <summary>When true, apply delta prediction to m/z and rt arrays. Unimplemented.</summary>
    public bool MzDelta { get; set; }

    /// <summary>When true, apply delta prediction to intensity arrays. Unimplemented.</summary>
    public bool IntenDelta { get; set; }

    /// <summary>When true, apply linear prediction to m/z and rt arrays. Unimplemented.</summary>
    public bool MzLinear { get; set; }

    /// <summary>When true, apply linear prediction to intensity arrays. Unimplemented.</summary>
    public bool IntenLinear { get; set; }

    /// <summary>Optional path to a contact-info file to attach to the output's FileDescription.</summary>
    public string? ContactInfo { get; set; }

    /// <summary>When true, combine every input into a single output file.</summary>
    public bool Merge { get; set; }

    /// <summary>Optional subset of runs to emit when a vendor file has multiple runs.</summary>
    public string? RunIndexSet { get; set; }

    /// <summary>Reconstructed command-line invocation string, captured by
    /// <c>Program.Main</c> before parsing. Threaded into the output's
    /// <c>dataProcessingList</c> as the <see cref="Pwiz.Data.Common.Cv.CVID.MS_command_line_parameters"/>
    /// cvParam on the first DataProcessing's first ProcessingMethod — matches cpp
    /// <c>msconvert.cpp:1059-1060</c>.</summary>
    public string CommandLineParameters { get; set; } = string.Empty;

    /// <summary>Emit SIM scans as spectra instead of chromatograms.</summary>
    public bool SimAsSpectra { get; set; }

    /// <summary>Emit SRM scans as spectra instead of chromatograms.</summary>
    public bool SrmAsSpectra { get; set; }

    /// <summary>Combine ion-mobility / SONAR frames into single spectra.</summary>
    public bool CombineIonMobilitySpectra { get; set; }

    /// <summary>Vendor-specific DDA post-processing flag (if supported).</summary>
    public bool DdaProcessing { get; set; }

    /// <summary>Skip calibration scans from the output.</summary>
    public bool IgnoreCalibrationScans { get; set; }

    /// <summary>Allow zero-length spectra through without re-reading the raw file to verify.</summary>
    public bool AcceptZeroLengthSpectra { get; set; }

    /// <summary>Don't ask vendor readers to re-insert zero samples surrounding profile peaks.</summary>
    public bool IgnoreMissingZeroSamples { get; set; }

    /// <summary>Continue even when a vendor reader can't identify the instrument model.</summary>
    public bool IgnoreUnknownInstrumentError { get; set; }

    /// <summary>Strip the file:// location from every sourceFile on output (repro-friendly).</summary>
    public bool StripLocationFromSourceFiles { get; set; }

    /// <summary>Strip the version from every software entry on output.</summary>
    public bool StripVersionFromSoftware { get; set; }

    /// <summary>Force single-threaded read+write (0 = auto, 1 = single, >1 = multi).</summary>
    public int SingleThreaded { get; set; }

    /// <summary>Continue processing remaining files after a per-file error.</summary>
    public bool ContinueOnError { get; set; }

    /// <summary>When true, log each file + filter applied to stderr.</summary>
    public bool Verbose { get; set; }

    /// <summary>Iteration period for verbose progress reporting (larger = fewer log lines). Default 100.</summary>
    public int VerboseProgressPeriod { get; set; } = 100;
}
