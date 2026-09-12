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

    /// <summary>Forwarded to the inner <see cref="MzmlWriter"/> — when true,
    /// per-spectrum/per-chromatogram fetch failures are logged and skipped
    /// rather than aborting. See <see cref="MzmlWriter.ContinueOnError"/>.</summary>
    public bool ContinueOnError { get; set; }

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
        using (var rawStream = conn.OpenMzMlStream())
        // Buffer the HDF5-backed stream: XmlWriter flushes in ~4 KB chunks and each
        // Write on MzMlDatasetStream triggers an HDF5 set_extent + hyperslab + H5Dwrite,
        // which costs ~3 ms each. For a 475 MB inner mzML that's ~120k HDF5 round-trips
        // and ~6 minutes of pure HDF5 overhead. A 4 MiB BufferedStream batches the
        // XmlWriter chunks into ~120 HDF5 writes total.
        using (var stream = new BufferedStream(rawStream, 4 * 1024 * 1024))
        {
            var inner = new MzmlWriter(_encoderConfig)
            {
                // No <indexedmzML> envelope inside the mzML dataset — cpp's
                // Serializer_mzML detects an mzMLb output stream and writes the spectrum
                // byte offsets to separate HDF5 datasets instead. TrackSpectrumOffsets=true
                // tells MzmlWriter to capture the per-spectrum byte positions + ids in
                // CapturedSpectrumOffsets, which we then store in the HDF5 mzML_spectrumIndex
                // / mzML_spectrumIndex_idRef datasets below — matching the format
                // MzMlbReaderAdapter's lazy path consumes.
                Indexed = false,
                TrackSpectrumOffsets = true,
                IterationListenerRegistry = IterationListenerRegistry,
                ExternalBinarySink = conn,
                ContinueOnError = ContinueOnError,
            };
            inner.Write(msd, stream);

            // Emit the spectrum-index HDF5 datasets so MzMlbReaderAdapter can take the
            // lazy path on re-read. Format matches cpp Serializer_mzML.cpp:200-241:
            //   mzML_spectrumIndex      : long[N+1] of byte positions (N spectrum starts + end-of-list)
            //   mzML_spectrumIndex_idRef: byte buffer of null-terminated id strings
            var offsets = inner.CapturedSpectrumOffsets;
            if (offsets.Count > 0)
            {
                var offsetArray = new long[offsets.Count + 1];
                int idByteCount = 0;
                for (int i = 0; i < offsets.Count; i++)
                {
                    offsetArray[i] = offsets[i].Offset;
                    idByteCount += System.Text.Encoding.UTF8.GetByteCount(offsets[i].Id) + 1; // +1 for null terminator
                }
                offsetArray[^1] = inner.CapturedEndOfSpectrumList;
                conn.AppendInt64("mzML_spectrumIndex", offsetArray);

                var idBytes = new byte[idByteCount];
                int p = 0;
                for (int i = 0; i < offsets.Count; i++)
                {
                    int n = System.Text.Encoding.UTF8.GetBytes(offsets[i].Id, 0, offsets[i].Id.Length, idBytes, p);
                    p += n;
                    idBytes[p++] = 0; // null terminator
                }
                conn.AppendBytes("mzML_spectrumIndex_idRef", idBytes);
            }
        }
    }
}
