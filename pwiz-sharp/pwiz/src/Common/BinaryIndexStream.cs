using System.Text;

#pragma warning disable CA1711 // "Stream" suffix kept for parity with C++ pwiz::data::BinaryIndexStream

namespace Pwiz.Data.Common.Index;

/// <summary>
/// <see cref="IIndex"/> implementation backed by a seekable stream.
/// Memory footprint is negligible; entries are read from the stream on demand.
/// Find(id) is O(log N) via binary search on a sorted-by-id block.
/// Find(ordinal) is O(1) — direct seek.
/// </summary>
/// <remarks>
/// Port of pwiz/data/common/BinaryIndexStream.cpp. Byte-identical to the C++ format:
/// the 48-byte prelude reserved for source-file metadata, the layout of the
/// streamLength + maxIdLength header, and the per-entry record (space-padded id +
/// uint64 index + int64 offset) all match. A sidecar written by C++ pwiz is readable
/// by this class and vice versa.
/// <code>
///   [0..7]   int64  source file size (zero if not populated; C++ leaves these zero)
///   [8..47]  40-byte lowercase ASCII hex SHA-1 of the source file (zero-padded if not populated)
///   [48..55] int64  streamLength  (total payload bytes including streamLength + maxIdLength)
///   [56..63] uint64 maxIdLength   (space-padded id width — i.e. longest id + 1)
///   [64..]   N entries sorted by ordinal index
///   [...]    N entries sorted by id (for binary search)
/// Each entry: maxIdLength bytes of ASCII id (space-padded) + uint64 index + int64 offset.
/// </code>
/// <para>
/// The 48-byte prelude (file size + SHA-1) is C++-compat reserved space. C++ does not
/// validate it; sharp's <c>Create(entries, sourceFileSize, sourceFileSha1Hex)</c>
/// overload populates it so callers (e.g. <c>FastaProteinList</c>) can do their own
/// staleness check by comparing <see cref="SourceFileSize"/> / <see cref="SourceFileSha1Hex"/>
/// against the current FASTA. The header-less <c>Create(entries)</c> overload writes
/// zeros (matches C++).
/// </para>
/// </remarks>
public sealed class BinaryIndexStream : IIndex, IDisposable
{
    private const int FileSizeSize = sizeof(long);
    private const int Sha1HexSize = 40;
    private const int HeaderSize = FileSizeSize + Sha1HexSize;   // 48 — matches indexedMetadataHeaderSize_ in C++
    private const int StreamLengthSize = sizeof(long);
    private const int MaxIdLengthSize = sizeof(ulong);
    private const int EntryTailSize = sizeof(ulong) + sizeof(long); // index + offset

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly object _ioLock = new();

    private long _streamLength;
    private ulong _maxIdLength;
    private int _count;
    private int _entrySize;

    /// <summary>
    /// Opens an existing index or prepares an empty one for one of the <c>Create</c> overloads.
    /// The stream must be seekable and readable; it must be writable for <c>Create</c>.
    /// </summary>
    public BinaryIndexStream(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
            throw new ArgumentException("BinaryIndexStream requires a seekable stream.", nameof(stream));

        _stream = stream;
        _leaveOpen = leaveOpen;

        TryReadHeader();
    }

    /// <summary>Source file size as recorded in the 48-byte prelude, or 0 if the prelude
    /// is unpopulated (C++-written sidecars, or sharp sidecars created via the
    /// header-less Create overload).</summary>
    public long SourceFileSize { get; private set; }

    /// <summary>Source file SHA-1 (40 lowercase hex chars) as recorded in the 48-byte
    /// prelude, or empty if the prelude is unpopulated. Use together with
    /// <see cref="SourceFileSize"/> to detect stale sidecars without re-scanning.</summary>
    public string SourceFileSha1Hex { get; private set; } = string.Empty;

    private void TryReadHeader()
    {
        // C++ reads past the hash/file-size prelude and then streamLength, maxIdLength.
        // If the stream is too short, silently treat as empty (matches C++ behavior).
        lock (_ioLock)
        {
            if (_stream.Length < HeaderSize + StreamLengthSize + MaxIdLengthSize)
            {
                _streamLength = 0;
                _maxIdLength = 0;
                _count = 0;
                _entrySize = 0;
                return;
            }

            _stream.Seek(0, SeekOrigin.Begin);
            Span<byte> preludeAndHeader = stackalloc byte[HeaderSize + StreamLengthSize + MaxIdLengthSize];
            int read = _stream.ReadAtLeast(preludeAndHeader, preludeAndHeader.Length, throwOnEndOfStream: false);
            if (read < preludeAndHeader.Length)
            {
                _streamLength = 0;
                _maxIdLength = 0;
                _count = 0;
                _entrySize = 0;
                return;
            }

            SourceFileSize = BitConverter.ToInt64(preludeAndHeader[..FileSizeSize]);
            var sha1Bytes = preludeAndHeader.Slice(FileSizeSize, Sha1HexSize);
            // All-zero bytes mean "not populated" (C++ writes zeros for the whole 48-byte prelude).
            bool anyNonZero = false;
            for (int i = 0; i < sha1Bytes.Length; i++) { if (sha1Bytes[i] != 0) { anyNonZero = true; break; } }
            SourceFileSha1Hex = anyNonZero ? Encoding.ASCII.GetString(sha1Bytes) : string.Empty;

            var streamLenBuf = preludeAndHeader.Slice(HeaderSize, StreamLengthSize);
            var maxIdLenBuf = preludeAndHeader.Slice(HeaderSize + StreamLengthSize, MaxIdLengthSize);
            _streamLength = BitConverter.ToInt64(streamLenBuf);
            _maxIdLength = BitConverter.ToUInt64(maxIdLenBuf);

            _entrySize = checked((int)_maxIdLength + EntryTailSize);
            int headerTail = StreamLengthSize + MaxIdLengthSize;
            long payload = _streamLength - headerTail;
            _count = payload > 0 && _entrySize > 0 ? (int)(payload / (_entrySize * 2L)) : 0;
        }
    }

    /// <summary>Creates an index with a populated 48-byte source-file prelude (file size +
    /// SHA-1 hex). Use this overload when the caller wants downstream opens to detect
    /// stale sidecars without re-scanning the source. C++ pwiz tolerates this prelude
    /// (it seeks past unconditionally), so the resulting sidecar remains byte-readable
    /// by C++.</summary>
    /// <param name="entries">Index entries to write.</param>
    /// <param name="sourceFileSize">Size in bytes of the indexed file (e.g. the FASTA).</param>
    /// <param name="sourceFileSha1Hex">40-char lowercase hex SHA-1 of the indexed file. Pass
    /// <see cref="string.Empty"/> to leave the SHA-1 portion zero-filled — useful when
    /// the caller wants size-only staleness detection without paying for a full hash.</param>
    public void Create(List<IndexEntry> entries, long sourceFileSize, string sourceFileSha1Hex)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(sourceFileSha1Hex);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceFileSize);
        if (sourceFileSha1Hex.Length != 0 && sourceFileSha1Hex.Length != Sha1HexSize)
            throw new ArgumentException($"SHA-1 hex must be empty or exactly {Sha1HexSize} chars.", nameof(sourceFileSha1Hex));

        Create(entries);

        lock (_ioLock)
        {
            _stream.Seek(0, SeekOrigin.Begin);
            Span<byte> prelude = stackalloc byte[HeaderSize];
            prelude.Clear();
            BitConverter.TryWriteBytes(prelude[..FileSizeSize], sourceFileSize);
            if (sourceFileSha1Hex.Length == Sha1HexSize)
                Encoding.ASCII.GetBytes(sourceFileSha1Hex, prelude.Slice(FileSizeSize, Sha1HexSize));
            _stream.Write(prelude);
            _stream.Flush();
            SourceFileSize = sourceFileSize;
            SourceFileSha1Hex = sourceFileSha1Hex;
        }
    }

    /// <inheritdoc/>
    public void Create(List<IndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (!_stream.CanWrite)
            throw new InvalidOperationException("Stream is not writable.");

        lock (_ioLock)
        {
            // Write placeholder prelude if starting empty; otherwise seek past it.
            if (_stream.Length == 0)
            {
                _stream.SetLength(HeaderSize);
                _stream.Seek(HeaderSize, SeekOrigin.Begin);
            }
            else
            {
                _stream.Seek(HeaderSize, SeekOrigin.Begin);
            }

            _count = entries.Count;

            int maxRawId = 0;
            string longestId = string.Empty;
            foreach (var e in entries)
            {
                int len = Encoding.ASCII.GetByteCount(e.Id);
                if (len > maxRawId) { maxRawId = len; longestId = e.Id; }
            }

            _maxIdLength = (ulong)(maxRawId + 1); // space-terminated, matches C++
            if (_maxIdLength > 2000)
                throw new InvalidDataException(
                    $"Creating index with huge ids ('{longestId}') probably means ids are not being parsed correctly.");

            _entrySize = checked((int)_maxIdLength + EntryTailSize);

            int headerTail = StreamLengthSize + MaxIdLengthSize;
            _streamLength = headerTail + (long)_entrySize * _count * 2L;

            using var bw = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
            bw.Write(_streamLength);
            bw.Write(_maxIdLength);

            // sorted by ordinal index
            entries.Sort((a, b) => a.Index.CompareTo(b.Index));
            WriteEntries(bw, entries);

            // sorted by id
            entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            WriteEntries(bw, entries);

            bw.Flush();
        }
    }

    private void WriteEntries(BinaryWriter bw, List<IndexEntry> entries)
    {
        Span<byte> idBuf = _maxIdLength <= 512
            ? stackalloc byte[(int)_maxIdLength]
            : new byte[_maxIdLength];

        foreach (var e in entries)
        {
            idBuf.Fill((byte)' ');
            int written = Encoding.ASCII.GetBytes(e.Id, idBuf);
            // remaining bytes stay as spaces
            _ = written;
            bw.Write(idBuf);
            bw.Write(e.Index);
            bw.Write(e.Offset);
        }
    }

    /// <inheritdoc/>
    public int Count => _count;

    /// <inheritdoc/>
    public IndexEntry? Find(int index)
    {
        if ((uint)index >= (uint)_count)
            return null;

        long indexBegin = HeaderSize + StreamLengthSize + MaxIdLengthSize;
        long offset = indexBegin + (long)index * _entrySize;

        lock (_ioLock)
        {
            _stream.Seek(offset, SeekOrigin.Begin);
            return ReadEntry();
        }
    }

    /// <inheritdoc/>
    public IndexEntry? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_count == 0)
            return null;

        long indexBegin = HeaderSize + StreamLengthSize + MaxIdLengthSize + (long)_entrySize * _count;

        lock (_ioLock)
        {
            // binary search over the id-sorted block
            int lo = 0, hi = _count;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                _stream.Seek(indexBegin + (long)mid * _entrySize, SeekOrigin.Begin);
                var entry = ReadEntry();
                if (entry is null)
                    return null;
                int cmp = string.CompareOrdinal(entry.Id, id);
                if (cmp < 0) lo = mid + 1;
                else if (cmp > 0) hi = mid;
                else return entry;
            }
        }
        return null;
    }

    private IndexEntry? ReadEntry()
    {
        Span<byte> idBuf = _maxIdLength <= 512
            ? stackalloc byte[(int)_maxIdLength]
            : new byte[_maxIdLength];
        int read = _stream.ReadAtLeast(idBuf, idBuf.Length, throwOnEndOfStream: false);
        if (read < idBuf.Length)
            return null;

        int end = idBuf.IndexOf((byte)' ');
        if (end < 0) end = idBuf.Length;
        string id = Encoding.ASCII.GetString(idBuf[..end]);

        Span<byte> tail = stackalloc byte[EntryTailSize];
        read = _stream.ReadAtLeast(tail, tail.Length, throwOnEndOfStream: false);
        if (read < tail.Length)
            return null;

        ulong index = BitConverter.ToUInt64(tail[..sizeof(ulong)]);
        long offset = BitConverter.ToInt64(tail.Slice(sizeof(ulong), sizeof(long)));
        return new IndexEntry(id, index, offset);
    }

    /// <summary>Disposes the underlying stream unless constructed with leaveOpen=true.</summary>
    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}
