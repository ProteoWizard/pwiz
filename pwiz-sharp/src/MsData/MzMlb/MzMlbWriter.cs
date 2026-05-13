using System.IO;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData.MzMlb;

/// <summary>
/// Writes an <see cref="MSData"/> as an mzMLb (HDF5-wrapped mzML) file.
/// </summary>
/// <remarks>
/// Port of <c>pwiz::msdata::Serializer_mzMLb</c> (write path). Composes the
/// existing <see cref="MzmlWriter"/> with an <see cref="MzMlbConnection"/>:
/// the writer's mzML XML is streamed into the HDF5 file's <c>mzML</c> dataset,
/// and each spectrum's binary arrays go into named HDF5 datasets via the
/// connection's <see cref="IExternalBinarySink"/> implementation.
/// <para>This first iteration emits every array as 64-bit float / int64 (no
/// 32-bit float, numpress, or compression variants on the array bytes
/// themselves). HDF5 chunk-level deflate compression is still applied via
/// <see cref="ChunkSize"/> / <see cref="CompressionLevel"/>.</para>
/// </remarks>
public sealed class MzMlbWriter
{
    private readonly BinaryEncoderConfig? _encoderConfig;

    /// <summary>HDF5 chunk size (in elements) for the mzML XML dataset and
    /// the binary-array datasets. Defaults to 1 MiB, matching pwiz cpp.</summary>
    public ulong ChunkSize { get; set; } = 1048576;

    /// <summary>DEFLATE level applied to all chunked datasets. 0 = uncompressed,
    /// 9 = max. Defaults to 4 (pwiz cpp's default).</summary>
    public int CompressionLevel { get; set; } = 4;

    /// <summary>Optional progress registry forwarded to the underlying
    /// <see cref="MzmlWriter"/>.</summary>
    public IterationListenerRegistry? IterationListenerRegistry { get; set; }

    /// <summary>Creates an mzMLb writer with the given binary-encoder config.
    /// The encoder config is mostly unused — mzMLb's binary arrays live in
    /// HDF5 datasets, not in the XML, so precision / compression / numpress
    /// settings are advisory — but the config is round-tripped through
    /// <see cref="MzmlWriter"/> for the precision and compression CV params
    /// that still appear on each <c>binaryDataArray</c>.</summary>
    public MzMlbWriter(BinaryEncoderConfig? encoderConfig = null)
    {
        _encoderConfig = encoderConfig;
    }

    /// <summary>Writes <paramref name="msd"/> to <paramref name="path"/> as
    /// an mzMLb file. Truncates the destination if it exists. Throws on I/O
    /// errors.</summary>
    public void Write(MSData msd, string path)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var conn = MzMlbConnection.OpenForWrite(path, ChunkSize, CompressionLevel);
        using var stream = conn.OpenMzMlStream();
        var inner = new MzmlWriter(_encoderConfig)
        {
            // mzMLb files inherently aren't indexed via the standard
            // <indexedmzML> envelope — the HDF5 layer gives random access
            // to spectra via dataset offsets instead. Match cpp here.
            Indexed = false,
            IterationListenerRegistry = IterationListenerRegistry,
            ExternalBinarySink = conn,
        };
        inner.Write(msd, stream);
    }
}
