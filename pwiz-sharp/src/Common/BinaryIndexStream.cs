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
/// Port of pwiz/data/common/BinaryIndexStream.cpp.
/// On-disk layout (little-endian, compatible with the C++ format):
/// <code>
///   [0..47]  48 bytes reserved header (8-byte file size + 40-byte SHA1 — written as zeros by Create)
///   [48..55] int64  streamLength  (total bytes of the index payload including this header)
///   [56..63] uint64 maxIdLength   (space-padded id width, including the trailing null padding byte)
///   [64..]   N entries sorted by ordinal index
///   [...]    N entries sorted by id (for binary search)
/// Each entry: maxIdLength bytes of ASCII id (space-padded) + uint64 index + int64 offset.
/// </code>
/// </remarks>
public sealed class BinaryIndexStream : IIndex, IDisposable
{
    private const int HeaderSize = 40 + sizeof(long);      // matches indexedMetadataHeaderSize_ in C++
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
    /// Opens an existing index or prepares an empty one for <see cref="Create"/>.
    /// The stream must be seekable and readable; it must be writable for <see cref="Create"/>.
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

            _stream.Seek(HeaderSize, SeekOrigin.Begin);
            Span<byte> buf = stackalloc byte[StreamLengthSize + MaxIdLengthSize];
            int read = _stream.ReadAtLeast(buf, buf.Length, throwOnEndOfStream: false);
            if (read < buf.Length)
            {
                _streamLength = 0;
                _maxIdLength = 0;
                _count = 0;
                _entrySize = 0;
                return;
            }

            _streamLength = BitConverter.ToInt64(buf[..StreamLengthSize]);
            _maxIdLength = BitConverter.ToUInt64(buf.Slice(StreamLengthSize, MaxIdLengthSize));

            _entrySize = checked((int)_maxIdLength + EntryTailSize);
            int headerTail = StreamLengthSize + MaxIdLengthSize;
            long payload = _streamLength - headerTail;
            _count = payload > 0 && _entrySize > 0 ? (int)(payload / (_entrySize * 2L)) : 0;
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
