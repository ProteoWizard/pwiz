using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Data.MsData;

/// <summary>The output format selection for a <see cref="WriteConfig"/>.
/// Mirrors cpp <c>MSDataFile::Format</c>. Formats whose writer isn't yet
/// implemented are still in the enum so CLIs can accept the switch — the
/// chosen writer throws <see cref="System.NotImplementedException"/> at
/// write time.</summary>
public enum WriteFormat
{
    /// <summary>mzML 1.1 — primary output format.</summary>
    Mzml,
    /// <summary>mzXML 3.2 — flat-attribute predecessor to mzML.</summary>
    MzXml,
    /// <summary>Mascot Generic Format — MS/MS peak lists only.</summary>
    Mgf,
    /// <summary>mz5 HDF5 format (unimplemented).</summary>
    Mz5,
    /// <summary>mzMLb HDF5 format (unimplemented).</summary>
    MzMLb,
    /// <summary>pwiz internal text format (unimplemented).</summary>
    Text,
    /// <summary>Legacy MS1 ASCII format (unimplemented).</summary>
    Ms1,
    /// <summary>Legacy CMS1 binary format (unimplemented).</summary>
    Cms1,
    /// <summary>Legacy MS2 ASCII format (unimplemented).</summary>
    Ms2,
    /// <summary>Legacy CMS2 binary format (unimplemented).</summary>
    Cms2,
}

/// <summary>
/// Output-side configuration consumed by every pwiz-sharp writer. Mirrors cpp
/// <c>MSDataFile::WriteConfig</c>.
/// </summary>
/// <remarks>
/// <para>This is the configuration any tool — msconvert, Skyline, a future
/// pipeline — passes when asking pwiz-sharp to write an <see cref="MSData"/>.
/// It carries:</para>
/// <list type="bullet">
///   <item>Which format to write (<see cref="Format"/>).</item>
///   <item>How binary arrays are encoded (<see cref="EncoderConfig"/> — precision,
///         zlib, numpress).</item>
///   <item>Stream-shape toggles (<see cref="Indexed"/>, <see cref="Gzip"/>).</item>
///   <item>mzMLb HDF5 dataset knobs (<see cref="MzMLbChunkSize"/>,
///         <see cref="MzMLbCompressionLevel"/>) — meaningful only when
///         <see cref="Format"/> is <see cref="WriteFormat.MzMLb"/>.</item>
/// </list>
/// <para>Format-specific concerns that aren't naturally part of this struct
/// (e.g. an msconvert <c>--outdir</c> path) belong to the caller's own config.</para>
/// </remarks>
public sealed class WriteConfig
{
    /// <summary>Output format selection. Defaults to <see cref="WriteFormat.Mzml"/>.</summary>
    public WriteFormat Format { get; set; } = WriteFormat.Mzml;

    /// <summary>Binary-array encoder settings (precision, compression, numpress).</summary>
    public BinaryEncoderConfig EncoderConfig { get; set; } = new();

    /// <summary>When true, write an <c>indexedmzML</c> wrapper (default). False means a raw mzML element.
    /// Ignored by formats that don't have an index concept (MGF / text).</summary>
    public bool Indexed { get; set; } = true;

    /// <summary>When true, gzip the final output file (caller appends <c>.gz</c> to the filename).</summary>
    public bool Gzip { get; set; }

    /// <summary>mzMLb dataset chunk size in bytes. Default matches pwiz C++ msconvert (1 MiB).
    /// Only consulted by the mzMLb writer.</summary>
    public int MzMLbChunkSize { get; set; } = 1_048_576;

    /// <summary>mzMLb GZIP compression level 0..9. Default matches pwiz C++ msconvert (4).
    /// Only consulted by the mzMLb writer.</summary>
    public int MzMLbCompressionLevel { get; set; } = 4;

    /// <summary>When true, the writer should log + continue on a per-spectrum write error
    /// rather than aborting. Matches cpp <c>WriteConfig::continueOnError</c>. pwiz-sharp's
    /// writers don't all honor this yet — they stop on the first failed spectrum — but
    /// callers (e.g. MSConvertGUI) can set it for forward compatibility.</summary>
    public bool ContinueOnError { get; set; }

    /// <summary>Shallow copy with a cloned <see cref="EncoderConfig"/>; lets callers tweak
    /// without mutating a shared instance.</summary>
    public WriteConfig Clone() => new()
    {
        Format = Format,
        EncoderConfig = EncoderConfig.Clone(),
        Indexed = Indexed,
        Gzip = Gzip,
        MzMLbChunkSize = MzMLbChunkSize,
        MzMLbCompressionLevel = MzMLbCompressionLevel,
        ContinueOnError = ContinueOnError,
    };
}
