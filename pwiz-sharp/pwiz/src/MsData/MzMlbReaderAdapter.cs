using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.MzMlb;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads mzMLb files (mzML wrapped in HDF5).</summary>
/// <remarks>
/// Port of <c>pwiz::msdata::Reader_mzMLb</c>. An mzMLb file is an HDF5
/// container holding the mzML XML in a chunked <c>mzML</c> dataset plus one
/// named HDF5 dataset per binary array (m/z, intensity, time, etc.). We
/// recognize it by the leading HDF5 magic bytes (<c>89 48 44 46 0D 0A 1A 0A</c>)
/// — same as cpp's identify path.
/// </remarks>
public sealed class MzMlbReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "mzMLb";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_mzMLb_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mzMLb", ".mzmlb" };

    // HDF5 file signature. Every HDF5 file starts with these 8 bytes regardless
    // of compression / chunking settings; the magic is HDF5's format-version
    // hook. An mzMLb file is always an HDF5 file, but not every HDF5 file is
    // mzMLb — the secondary check is the presence of a named "mzML" dataset
    // (validated inside MzMlbConnection's identifyOnly ctor).
    private static readonly byte[] Hdf5Magic = { 0x89, 0x48, 0x44, 0x46, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        if (head is not null && head.Length >= Hdf5Magic.Length)
        {
            bool magicMatches = true;
            for (int i = 0; i < Hdf5Magic.Length; i++)
            {
                if ((byte)head[i] != Hdf5Magic[i]) { magicMatches = false; break; }
            }
            if (magicMatches)
            {
                // Open in identify mode to confirm there's an actual "mzML"
                // dataset (else this is just a generic HDF5 file). Swallow
                // failures — the cpp identify path is silent on non-mzMLb.
                try
                {
                    using var _ = new MzMlbConnection(filename, identifyOnly: true);
                    return CvType;
                }
                catch
                {
                    return CVID.CVID_Unknown;
                }
            }
        }

        foreach (var ext in FileExtensions)
            if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return CvType;

        return CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        // The MzMlbConnection owns the HDF5 handles for the file (the mzML
        // dataset + every binary dataset we touch during this read). For the
        // lazy path it has to outlive the Read call entirely — SpectrumList_Mzml
        // holds it and pulls binary arrays out of it on each GetSpectrum. For
        // the eager fallback we dispose it at end-of-Read like before.

        // Fast path: cpp's Serializer_mzML special-cases mzMLb — instead of writing
        // an <indexedmzML> envelope into the mzML stream, it writes the spectrum byte
        // offsets to separate HDF5 datasets named `mzML_spectrumIndex` (long[N+1] of
        // byte positions, last one is end-of-spectrumList) + `mzML_spectrumIndex_idRef`
        // (null-terminated id strings packed back-to-back), and similar for
        // chromatograms. See pwiz/data/msdata/Serializer_mzML.cpp around lines 200-241.
        // If those datasets are present, use them as our lazy index without touching
        // the mzML stream's tail (which has no </indexedmzML> wrapper for mzMLb).
        var conn = MzMlbConnection.OpenForRead(filename);
        try
        {
            var idx = TryReadMzMlbIndex(conn);
            if (idx is not null)
            {
                var lazyReader = new MzmlReader { ExternalBinarySource = conn, LazyMode = true };
                using (var headerStream = conn.OpenMzMlStream())
                {
                    var headerOnly = lazyReader.Read(headerStream);
                    MzmlReaderAdapter.CopyInto(headerOnly, result);
                }
                using (var tailStream = conn.OpenMzMlStream())
                {
                    tailStream.Position = idx.Value.EndOfSpectrumList;
                    lazyReader.ResumeAfterSpectrumList(tailStream, result);
                }
                // Add input file as SourceFile + pwiz software + pwiz_Reader_conversion DP
                // BEFORE installing the lazy list so its defaultDataProcessingRef points to the
                // new DP (cpp parity — DefaultReaderList.cpp:563).
                var dpPwiz = MSDataFile.FillInCommonMetadata(filename, result);
                result.Run.SpectrumList = new Pwiz.Data.MsData.Mzml.SpectrumList_Mzml(
                    openStream: conn.OpenMzMlStream,
                    ownedResource: conn,
                    context: lazyReader,
                    ids: idx.Value.SpectrumIds,
                    offsets: idx.Value.SpectrumOffsets,
                    dp: dpPwiz,
                    source: filename);
                conn = null!; // ownership transferred to SpectrumList_Mzml
                return;
            }

            // Fallback: no HDF5 index datasets (older cpp writes, or writes that didn't
            // pass the offsets through). Eager parse — same as the pre-lazy behavior.
            using var stream = conn.OpenMzMlStream();
            var reader = new MzmlReader { ExternalBinarySource = conn };
            var parsed = reader.Read(stream);
            MzmlReaderAdapter.CopyInto(parsed, result);
            MSDataFile.FillInCommonMetadata(filename, result);
        }
        finally
        {
            conn?.Dispose();
        }
    }

    /// <summary>mzMLb spectrum-byte-offset index loaded from the HDF5
    /// <c>mzML_spectrumIndex</c> + <c>mzML_spectrumIndex_idRef</c> datasets.</summary>
    private readonly record struct MzMlbIndex(string[] SpectrumIds, long[] SpectrumOffsets,
                                               long EndOfSpectrumList);

    private static MzMlbIndex? TryReadMzMlbIndex(MzMlbConnection conn)
    {
        const string OFFSETS = "mzML_spectrumIndex";
        const string IDREFS = "mzML_spectrumIndex_idRef";
        if (!conn.Exists(OFFSETS) || !conn.Exists(IDREFS)) return null;

        long count = conn.GetDatasetElementCount(OFFSETS);
        // cpp writes N+1 offsets: [0..N-1] are spectrum starts, [N] is end-of-spectrumList
        // (writer.positionNext()-2 in Serializer_mzML.cpp's spectrumPositions push). Need
        // at least 2 entries (one spectrum + the end marker).
        if (count < 2) return null;
        var rawOffsets = new long[count];
        conn.ReadInt64(OFFSETS, 0, rawOffsets);
        var offsets = new long[count - 1];
        System.Array.Copy(rawOffsets, offsets, offsets.Length);
        long endOfSpectrumList = rawOffsets[^1];

        long idRefBytes = conn.GetDatasetElementCount(IDREFS);
        var idBytes = new byte[idRefBytes];
        conn.ReadBytes(IDREFS, 0, idBytes);
        var ids = SplitNullTerminated(idBytes, offsets.Length);
        if (ids is null) return null;

        return new MzMlbIndex(ids, offsets, endOfSpectrumList);
    }

    private static string[]? SplitNullTerminated(byte[] buf, int expectedCount)
    {
        var result = new string[expectedCount];
        int start = 0;
        int n = 0;
        for (int i = 0; i < buf.Length && n < expectedCount; i++)
        {
            if (buf[i] == 0)
            {
                result[n++] = System.Text.Encoding.UTF8.GetString(buf, start, i - start);
                start = i + 1;
            }
        }
        if (n != expectedCount) return null;
        return result;
    }
}
