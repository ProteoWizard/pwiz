using System;
using System.Collections.Generic;
using System.IO;
using HDF.PInvoke;

// HDF.PInvoke's close/release functions return an HRESULT-style int. We can't
// usefully react to errors in cleanup paths (we're already disposing), so the
// "did you ignore this return?" rule is too noisy here.
#pragma warning disable CA1806

namespace Pwiz.Data.MsData.MzMlb;

/// <summary>
/// HDF5 file wrapper for mzMLb. Port of <c>pwiz::msdata::mzmlb::Connection_mzMLb</c>.
/// </summary>
/// <remarks>
/// An mzMLb file is an HDF5 container with two kinds of datasets:
/// <list type="bullet">
///   <item>A single <c>mzML</c> chunked dataset holding the mzML XML as bytes. It
///     carries a <c>version</c> attribute (currently <c>"mzMLb 1.0"</c>) used as
///     both a format-version check and a reliability test against arbitrary HDF5
///     files that aren't mzMLb.</item>
///   <item>One or more named binary datasets (m/z, intensity, etc.), referenced
///     from the mzML XML via <c>external HDF5 dataset</c> / <c>external offset</c>
///     / <c>external array length</c> cvParams instead of inline base64 in the
///     <c>&lt;binary&gt;</c> element.</item>
/// </list>
/// This first iteration is read-only and exposes the <c>mzML</c> dataset as a
/// <see cref="Stream"/> for the XML reader plus typed read helpers for the binary
/// datasets. Write support comes in a follow-up.
/// </remarks>
public sealed class MzMlbConnection : IExternalBinarySource, IExternalBinarySink, IDisposable
{
    /// <summary>The on-disk version string written by pwiz cpp into the
    /// <c>mzML</c> dataset's <c>version</c> attribute. We accept files matching
    /// this exact string.</summary>
    public const string CurrentVersion = "mzMLb 1.0";

    private long _fileId = -1L;
    private DatasetHandle _mzML;
    // Cache for the binary datasets we've opened (cheaper than re-resolving by
    // string every time the XML reader hits the next external-array reference).
    private readonly Dictionary<string, DatasetHandle> _binary = new();
    private bool _disposed;
    // Set when the connection was opened for writing; controls Stream.CanWrite
    // semantics and gates the AppendDoubles / AppendInt64 sink methods.
    private readonly bool _writing;
    private readonly ulong _chunkSize;
    private readonly int _compressionLevel;

    /// <summary>Opens an existing mzMLb file for reading. Equivalent to the
    /// 2-arg constructor with <c>identifyOnly=false</c>. Use
    /// <see cref="OpenForWrite"/> to create a new mzMLb file.</summary>
    public static MzMlbConnection OpenForRead(string filename, bool identifyOnly = false)
        => new(filename, identifyOnly);

    /// <summary>Creates a new mzMLb file for writing. Truncates any existing
    /// file. See the writing constructor for the parameter meanings.</summary>
    public static MzMlbConnection OpenForWrite(string filename,
        ulong chunkSize = 1048576, int compressionLevel = 4)
        => new(filename, chunkSize, compressionLevel);

    /// <summary>Opens an mzMLb file for reading.</summary>
    /// <param name="filename">Path to a .mzMLb file. Must exist.</param>
    /// <param name="identifyOnly">When true, validate it's a real mzMLb file
    /// (HDF5 magic + <c>mzML</c> dataset present) but don't enforce the version
    /// string. Used by <c>Reader_MzMlb.Identify</c>.</param>
    /// <exception cref="IOException">Thrown if the file isn't an mzMLb file or
    /// can't be opened.</exception>
    public MzMlbConnection(string filename, bool identifyOnly)
    {
        if (!File.Exists(filename))
            throw new FileNotFoundException("mzMLb file not found", filename);

        // Silence the default error stack auto-print; we surface errors as exceptions.
        H5E.set_auto(H5E.DEFAULT, null, IntPtr.Zero);

        _fileId = H5F.open(filename, H5F.ACC_RDONLY);
        if (_fileId < 0)
            throw new IOException($"Could not open mzMLb file for reading: {filename}");

        try
        {
            _mzML = OpenDataset("mzML");

            string? version = ReadStringAttribute(_mzML.Dataset, "version");
            if (version is null)
            {
                if (identifyOnly) { Dispose(); return; }
                throw new IOException("File is HDF5 but missing the 'version' attribute; not an mzMLb file.");
            }
            if (!identifyOnly && version != CurrentVersion)
                throw new IOException(
                    $"Unsupported mzMLb version: \"{version}\". Only \"{CurrentVersion}\" is supported.");

            if (identifyOnly) Dispose();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Creates a new mzMLb file for writing. Truncates any existing
    /// file at <paramref name="filename"/>. The <c>mzML</c> dataset is created
    /// chunked + UNLIMITED so XML can be appended via the writable stream
    /// returned by <see cref="OpenMzMlStream"/>. Binary datasets are created
    /// on first <see cref="AppendDoubles"/> / <see cref="AppendInt64"/> call,
    /// also chunked + UNLIMITED.</summary>
    /// <param name="filename">Output path. Will be overwritten if it exists.</param>
    /// <param name="chunkSize">HDF5 chunk size (in elements) for the
    /// <c>mzML</c> dataset and any subsequently-created binary datasets.
    /// Pwiz cpp default is 1 MiB; smaller chunks reduce HDF5 overhead at the
    /// cost of worse compression ratios.</param>
    /// <param name="compressionLevel">DEFLATE level 0 (no compression) to 9.
    /// Pwiz cpp default is 4. 0 = uncompressed, fastest write; higher = smaller
    /// file, slower write. Compression is applied to all chunked datasets.</param>
    public MzMlbConnection(string filename, ulong chunkSize = 1048576, int compressionLevel = 4)
    {
        _writing = true;
        _chunkSize = chunkSize;
        _compressionLevel = compressionLevel;

        H5E.set_auto(H5E.DEFAULT, null, IntPtr.Zero);

        // Configure file-level raw-data chunk cache. The HDF5 default cache is
        // only ~1 MiB total — when an active chunk doesn't fit, every write
        // forces a compress + flush, which on deflate level 4 costs ~10ms per
        // call. For a 475 MiB mzMLb that's hours of pointless re-flushing.
        // Match cpp Connection_mzMLb.cpp:138-147: bump the cache to at least
        // chunk_size, set w0=1.0 so freshly-written chunks evict first (pwiz
        // only writes each chunk once — never re-reads during the same write
        // session — so write-once preemption is exactly right).
        long fapl = H5P.create(H5P.FILE_ACCESS);
        try
        {
            int mdcElems = 0;
            IntPtr nslots = IntPtr.Zero;
            IntPtr nbytes = IntPtr.Zero;
            double w0 = 0.0;
            H5P.get_cache(fapl, ref mdcElems, ref nslots, ref nbytes, ref w0);
            // Bump the cache so an active chunk fits without immediate eviction.
            // 4× chunk_size leaves headroom for the mzML chunk + the two binary
            // chunks (mz + intensity) that are simultaneously being filled per
            // spectrum. Comparable to cpp's max(default, chunk_size), but we go
            // higher because cpp's pwiz interleaves XML and binary writes whereas
            // sharp's BufferedStream on the XML side means a long run of binary
            // writes can occur without touching the mzML chunk.
            IntPtr desired = checked((IntPtr)(long)(_chunkSize * 4));
            if ((long)nbytes < (long)desired) nbytes = desired;
            w0 = 1.0;
            H5P.set_cache(fapl, mdcElems, nslots, nbytes, w0);

            _fileId = H5F.create(filename, H5F.ACC_TRUNC, H5P.DEFAULT, fapl);
        }
        finally
        {
            H5P.close(fapl);
        }
        if (_fileId < 0)
            throw new IOException($"Could not create mzMLb file: {filename}");

        try
        {
            // Create the empty extensible "mzML" dataset up front so we can
            // append XML bytes through the writable stream as the XmlWriter
            // emits them. UNLIMITED max + chunked + deflate matches cpp.
            // mzML is a char (1-byte) dataset, so chunk size in elements == chunk size in bytes.
            ulong[] dims = { 0 };
            ulong[] maxdims = { H5S.UNLIMITED };
            long space = H5S.create_simple(1, dims, maxdims);
            long dcpl = H5P.create(H5P.DATASET_CREATE);
            try
            {
                ulong[] chunk = { _chunkSize };
                H5P.set_chunk(dcpl, 1, chunk);
                if (_compressionLevel > 0) H5P.set_deflate(dcpl, (uint)_compressionLevel);
                H5P.set_fletcher32(dcpl);
                long ds = H5D.create(_fileId, "mzML", H5T.NATIVE_UCHAR, space,
                                     lcpl_id: H5P.DEFAULT, dcpl_id: dcpl);
                if (ds < 0) throw new IOException("H5D.create failed for 'mzML' dataset");
                _mzML = new DatasetHandle { Dataset = ds, Space = space, Size = 0 };
                space = -1; // ownership transferred
                WriteVersionAttribute(ds);
            }
            finally
            {
                H5P.close(dcpl);
                if (space >= 0) H5S.close(space);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Returns a seekable stream over the <c>mzML</c> dataset's bytes.
    /// In read mode the stream supports Read + Seek. In write mode it supports
    /// Read + Write + Seek (writes extend the underlying chunked HDF5 dataset).
    /// Closing the returned stream is a no-op; resources are released when this
    /// <see cref="MzMlbConnection"/> is disposed.</summary>
    public Stream OpenMzMlStream()
    {
        EnsureOpen();
        return new MzMlDatasetStream(this);
    }

    /// <summary>True iff a dataset with the given name exists in the file.</summary>
    public bool Exists(string datasetId)
    {
        EnsureOpen();
        return H5L.exists(_fileId, datasetId) > 0;
    }

    /// <summary>Number of elements in the 1-D dataset (e.g. <c>mzML_spectrumIndex</c>
    /// contains N+1 int64 byte-offsets where N is the spectrum count). Throws if the
    /// dataset doesn't exist; callers should check <see cref="Exists"/> first.</summary>
    public long GetDatasetElementCount(string datasetId)
    {
        EnsureOpen();
        using var ds = OpenDataset(datasetId);
        return (long)ds.Size;
    }

    /// <summary>Reads <c>buf.Length</c> doubles from <paramref name="dataset"/>
    /// starting at <paramref name="offset"/>. Used for m/z and intensity
    /// arrays regardless of on-disk precision — HDF5 converts NATIVE_FLOAT
    /// datasets to double on the fly when this request type is double.</summary>
    public int ReadDoubles(string dataset, long offset, Span<double> buf)
        => ReadTyped(dataset, offset, buf, H5T.NATIVE_DOUBLE);

    /// <summary>Reads <c>buf.Length</c> 64-bit integers from
    /// <paramref name="dataset"/> starting at <paramref name="offset"/>. Used
    /// for integer arrays (e.g. ion counts in centroid spectra).</summary>
    public int ReadInt64(string dataset, long offset, Span<long> buf)
        => ReadTyped(dataset, offset, buf, H5T.NATIVE_INT64);

    /// <summary>Reads <c>buf.Length</c> raw bytes from <paramref name="dataset"/>
    /// starting at byte-offset <paramref name="offset"/>. Used to pull
    /// numpress-encoded opaque blobs back from HDF5 for decoding.</summary>
    public int ReadBytes(string dataset, long offset, Span<byte> buf)
        => ReadTyped(dataset, offset, buf, H5T.NATIVE_UCHAR);

    /// <inheritdoc/>
    public long AppendDoubles(string dataset, ReadOnlySpan<double> data)
        => AppendTyped(dataset, data, H5T.NATIVE_DOUBLE, sizeof(double));

    /// <inheritdoc/>
    public long AppendFloats(string dataset, ReadOnlySpan<float> data)
        => AppendTyped(dataset, data, H5T.NATIVE_FLOAT, sizeof(float));

    /// <inheritdoc/>
    public long AppendInt64(string dataset, ReadOnlySpan<long> data)
        => AppendTyped(dataset, data, H5T.NATIVE_INT64, sizeof(long));

    /// <inheritdoc/>
    public long AppendInt32(string dataset, ReadOnlySpan<int> data)
        => AppendTyped(dataset, data, H5T.NATIVE_INT32, sizeof(int));

    /// <inheritdoc/>
    public long AppendBytes(string dataset, ReadOnlySpan<byte> data)
        => AppendTyped(dataset, data, H5T.NATIVE_UCHAR, sizeof(byte));

    /// <summary>Close the HDF5 file and release all dataset / dataspace handles.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var ds in _binary.Values) ds.Dispose();
        _binary.Clear();
        _mzML.Dispose();

        if (_fileId >= 0)
        {
            H5F.close(_fileId);
            _fileId = -1L;
        }
    }

    private void EnsureOpen()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private DatasetHandle OpenDataset(string name)
    {
        long ds = H5D.open(_fileId, name);
        if (ds < 0)
            throw new IOException($"Could not open dataset '{name}' in mzMLb file.");
        long sp = H5D.get_space(ds);
        if (sp < 0)
        {
            H5D.close(ds);
            throw new IOException($"Could not get dataspace for dataset '{name}'.");
        }
        // Simple 1-D datasets — get the extent so callers know how many elements
        // they can read.
        var dims = new ulong[1];
        H5S.get_simple_extent_dims(sp, dims, null);
        return new DatasetHandle { Dataset = ds, Space = sp, Size = dims[0] };
    }

    private DatasetHandle GetBinaryDataset(string id)
    {
        if (_binary.TryGetValue(id, out var existing)) return existing;
        var ds = OpenDataset(id);
        _binary[id] = ds;
        return ds;
    }

    private int ReadTyped<T>(string datasetId, long offset, Span<T> buf, long nativeType)
        where T : unmanaged
    {
        EnsureOpen();
        if (buf.Length == 0) return 0;
        var ds = GetBinaryDataset(datasetId);

        ulong[] start = { (ulong)offset };
        ulong[] count = { (ulong)buf.Length };
        if (start[0] + count[0] > ds.Size)
            count[0] = ds.Size - start[0];
        if (count[0] == 0) return 0;

        if (H5S.select_hyperslab(ds.Space, H5S.seloper_t.SET, start, null, count, null) < 0)
            throw new IOException($"H5S.select_hyperslab failed for '{datasetId}'.");

        long mspace = H5S.create_simple(1, count, count);
        try
        {
            unsafe
            {
                fixed (T* p = buf)
                {
                    if (H5D.read(ds.Dataset, nativeType, mspace, ds.Space,
                                 H5P.DEFAULT, (IntPtr)p) < 0)
                        throw new IOException($"H5D.read failed for '{datasetId}'.");
                }
            }
        }
        finally
        {
            H5S.close(mspace);
        }
        return (int)count[0];
    }

    private long AppendTyped<T>(string datasetId, ReadOnlySpan<T> data, long nativeType, int elementSize)
        where T : unmanaged
    {
        EnsureOpen();
        if (!_writing)
            throw new InvalidOperationException("MzMlbConnection was not opened for writing.");

        if (!_binary.TryGetValue(datasetId, out var ds))
        {
            // Create the dataset on first reference. Chunked + UNLIMITED max so
            // subsequent calls can extend it. Match cpp Connection_mzMLb.cpp:549:
            // chunk size is specified in *bytes*, so the dataset's chunk-in-elements
            // is chunk_size / sizeof(element). For doubles this yields 131072-element
            // chunks (1 MiB), not 1048576-element chunks (8 MiB). Sticking to a 1 MiB
            // chunk keeps each chunk small enough to fit in the HDF5 cache, avoiding
            // a compress + flush per AppendTyped call (the previous bottleneck for
            // multi-MB mzMLb files: ~90k spectra × 2 arrays × per-write compression).
            ulong chunkElems = _chunkSize / (ulong)elementSize;
            if (chunkElems == 0) chunkElems = 1;
            ulong[] dims = { 0 };
            ulong[] maxdims = { H5S.UNLIMITED };
            long space = H5S.create_simple(1, dims, maxdims);
            long dcpl = H5P.create(H5P.DATASET_CREATE);
            try
            {
                ulong[] chunk = { chunkElems };
                H5P.set_chunk(dcpl, 1, chunk);
                if (_compressionLevel > 0)
                {
                    // Byte-shuffle reorganizes multi-byte values so similar bytes
                    // cluster together, materially improving deflate ratios on
                    // floating-point arrays. Matches cpp Connection_mzMLb.cpp:555.
                    if (elementSize > 1) H5P.set_shuffle(dcpl);
                    H5P.set_deflate(dcpl, (uint)_compressionLevel);
                }
                H5P.set_fletcher32(dcpl);
                long ds2 = H5D.create(_fileId, datasetId, nativeType, space,
                                      lcpl_id: H5P.DEFAULT, dcpl_id: dcpl);
                if (ds2 < 0)
                    throw new IOException($"H5D.create failed for binary dataset '{datasetId}'.");
                ds = new DatasetHandle { Dataset = ds2, Space = space, Size = 0 };
                space = -1; // ownership transferred to ds
                _binary[datasetId] = ds;
            }
            finally
            {
                H5P.close(dcpl);
                if (space >= 0) H5S.close(space);
            }
        }

        if (data.Length == 0) return (long)ds.Size;
        long offset = (long)ds.Size;
        ulong newSize = ds.Size + (ulong)data.Length;
        ExtendDataset(ref ds, newSize);
        _binary[datasetId] = ds;

        ulong[] start = { (ulong)offset };
        ulong[] count = { (ulong)data.Length };
        if (H5S.select_hyperslab(ds.Space, H5S.seloper_t.SET, start, null, count, null) < 0)
            throw new IOException($"H5S.select_hyperslab failed appending to '{datasetId}'.");
        long mspace = H5S.create_simple(1, count, count);
        try
        {
            unsafe
            {
                fixed (T* p = data)
                {
                    if (H5D.write(ds.Dataset, nativeType, mspace, ds.Space,
                                  H5P.DEFAULT, (IntPtr)p) < 0)
                        throw new IOException($"H5D.write failed appending to '{datasetId}'.");
                }
            }
        }
        finally
        {
            H5S.close(mspace);
        }
        return offset;
    }

    /// <summary>Extend a 1-D chunked dataset to the new size. Closes + reopens
    /// the dataspace handle since H5Dset_extent invalidates the existing one.</summary>
    private static void ExtendDataset(ref DatasetHandle ds, ulong newSize)
    {
        ulong[] dims = { newSize };
        if (H5D.set_extent(ds.Dataset, dims) < 0)
            throw new IOException("H5D.set_extent failed.");
        H5S.close(ds.Space);
        ds.Space = H5D.get_space(ds.Dataset);
        ds.Size = newSize;
    }

    /// <summary>Write the fixed-length "version" string attribute on the mzML
    /// dataset. Called once during write-mode construction so readers can
    /// recognize the file. Includes a trailing NUL terminator + sets
    /// H5T_STR_NULLTERM, matching cpp Connection_mzMLb.cpp:177 — cpp readers
    /// (and our own) interpret the attribute as a C string and will otherwise
    /// pick up adjacent memory as part of the version string.</summary>
    private static void WriteVersionAttribute(long datasetId)
    {
        long type = H5T.copy(H5T.C_S1);
        byte[] verBytes = System.Text.Encoding.ASCII.GetBytes(CurrentVersion);
        // +1 for the explicit NUL terminator.
        byte[] padded = new byte[verBytes.Length + 1];
        Array.Copy(verBytes, padded, verBytes.Length);
        H5T.set_size(type, (IntPtr)padded.Length);
        H5T.set_strpad(type, H5T.str_t.NULLTERM);
        long scalar = H5S.create(H5S.class_t.SCALAR);
        long attr = H5A.create(datasetId, "version", type, scalar);
        try
        {
            unsafe
            {
                fixed (byte* p = padded)
                    H5A.write(attr, type, (IntPtr)p);
            }
        }
        finally
        {
            H5A.close(attr);
            H5S.close(scalar);
            H5T.close(type);
        }
    }

    /// <summary>Read a fixed-length string attribute from a dataset. mzMLb's
    /// version attribute is the only one we care about right now.</summary>
    private static string? ReadStringAttribute(long datasetId, string name)
    {
        long attr = H5A.open(datasetId, name);
        if (attr < 0) return null;
        try
        {
            long type = H5A.get_type(attr);
            try
            {
                int size = (int)H5T.get_size(type);
                byte[] buf = new byte[size];
                unsafe
                {
                    fixed (byte* p = buf)
                    {
                        if (H5A.read(attr, type, (IntPtr)p) < 0) return null;
                    }
                }
                // Fixed-length string padded with NUL — strip trailing zeros.
                int end = Array.IndexOf(buf, (byte)0);
                if (end < 0) end = buf.Length;
                return System.Text.Encoding.ASCII.GetString(buf, 0, end);
            }
            finally
            {
                H5T.close(type);
            }
        }
        finally
        {
            H5A.close(attr);
        }
    }

    private struct DatasetHandle : IDisposable
    {
        public long Dataset;
        public long Space;
        public ulong Size;

        public void Dispose()
        {
            if (Space >= 0) { H5S.close(Space); Space = -1L; }
            if (Dataset >= 0) { H5D.close(Dataset); Dataset = -1L; }
        }
    }

    /// <summary>
    /// <see cref="Stream"/> view over the connection's <c>mzML</c> chunked
    /// dataset. Supports Read + Seek; Write is throwing for now (the writer
    /// port hasn't landed). The XML reader needs Seek for forward-only
    /// scanning with one-step pushback.
    /// </summary>
    private sealed class MzMlDatasetStream : Stream
    {
        private readonly MzMlbConnection _owner;
        private long _position;
        public MzMlDatasetStream(MzMlbConnection owner) { _owner = owner; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => _owner._writing;
        public override long Length => (long)_owner._mzML.Size;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            _owner.EnsureOpen();
            if (buffer.Length == 0 || _position >= Length) return 0;
            int toRead = (int)Math.Min(buffer.Length, Length - _position);
            ulong[] start = { (ulong)_position };
            ulong[] count = { (ulong)toRead };
            if (H5S.select_hyperslab(_owner._mzML.Space, H5S.seloper_t.SET,
                                     start, null, count, null) < 0)
                throw new IOException("H5S.select_hyperslab failed on mzML dataset.");
            long mspace = H5S.create_simple(1, count, count);
            try
            {
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        if (H5D.read(_owner._mzML.Dataset, H5T.NATIVE_UCHAR,
                                     mspace, _owner._mzML.Space, H5P.DEFAULT, (IntPtr)p) < 0)
                            throw new IOException("H5D.read failed on mzML dataset.");
                    }
                }
            }
            finally
            {
                H5S.close(mspace);
            }
            _position += toRead;
            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            Position = newPos;
            return _position;
        }

        public override void Flush() { /* HDF5 buffers via libhdf5; rely on close */ }
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _owner.EnsureOpen();
            if (!_owner._writing)
                throw new NotSupportedException("MzMlbConnection was not opened for writing.");
            if (buffer.Length == 0) return;

            ulong end = (ulong)_position + (ulong)buffer.Length;
            if (end > _owner._mzML.Size)
                ExtendDataset(ref _owner._mzML, end);

            ulong[] start = { (ulong)_position };
            ulong[] count = { (ulong)buffer.Length };
            if (H5S.select_hyperslab(_owner._mzML.Space, H5S.seloper_t.SET,
                                     start, null, count, null) < 0)
                throw new IOException("H5S.select_hyperslab failed writing mzML dataset.");
            long mspace = H5S.create_simple(1, count, count);
            try
            {
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        if (H5D.write(_owner._mzML.Dataset, H5T.NATIVE_UCHAR,
                                      mspace, _owner._mzML.Space, H5P.DEFAULT, (IntPtr)p) < 0)
                            throw new IOException("H5D.write failed on mzML dataset.");
                    }
                }
            }
            finally
            {
                H5S.close(mspace);
            }
            _position += buffer.Length;
        }
    }
}
