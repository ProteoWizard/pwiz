using System.Buffers.Binary;
using System.Text;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Reads a zip container whose entries must all be STORED (uncompressed), exposing each one as a
/// seekable window onto the container. Used for the Spectronaut <c>.sn-report.zip</c>, which holds
/// its two parquet files that way.
/// </summary>
/// <remarks>
/// <para>Why not <see cref="System.IO.Compression.ZipArchive"/>: Parquet is read back-to-front —
/// the footer lives at the end of the file and the reader then seeks to each row group — so it
/// needs a seekable stream. <c>ZipArchiveEntry.Open()</c> returns <c>CanSeek == false</c> even for
/// a stored entry (verified on .NET 10), and .NET does not expose where an entry's bytes begin, so
/// there is no way to build a window from it without reflection.</para>
/// <para>Because the entries are stored, each one is a contiguous byte range of the container, so
/// the central directory gives everything needed to hand Parquet a seekable window straight onto
/// the file — no decompression, no temporary copy, and random access over an entry of any size.
/// That is the whole reason STORED is required, and a deflated entry is rejected rather than
/// silently copied out.</para>
/// </remarks>
public sealed class StoredZipArchive : IDisposable
{
    private const uint EocdSignature = 0x06054b50;
    private const uint Eocd64Signature = 0x06064b50;
    private const uint Eocd64LocatorSignature = 0x07064b50;
    private const uint CentralHeaderSignature = 0x02014b50;
    private const uint LocalHeaderSignature = 0x04034b50;
    private const ushort MethodStored = 0;
    private const uint Zip64Sentinel = 0xFFFFFFFF;

    /// <summary>One stored entry: where its bytes start in the container, and how many.</summary>
    private sealed record Entry(string Name, long DataOffset, long Length);

    private readonly string _path;
    private readonly List<Entry> _entries = new();
    private readonly List<Stream> _open = new();

    /// <summary>Entry names, in central-directory order.</summary>
    public IReadOnlyList<string> EntryNames => _entries.ConvertAll(e => e.Name);

    public StoredZipArchive(string path)
    {
        _path = path;
        using var fs = File.OpenRead(path);
        ReadCentralDirectory(fs);
    }

    /// <summary>
    /// Returns a read-only, seekable stream over <paramref name="name"/>'s bytes. The stream is
    /// owned by this archive and closed by <see cref="Dispose"/>.
    /// </summary>
    public Stream OpenSeekable(string name)
    {
        var entry = _entries.Find(e => string.Equals(e.Name, name, StringComparison.Ordinal))
            ?? throw new BlibException(false, $"'{_path}' has no entry named '{name}'.");
        // A private handle per entry: the windows are independent, so nothing has to coordinate a
        // shared file position.
        var fs = File.OpenRead(_path);
        var window = new WindowStream(fs, entry.DataOffset, entry.Length);
        _open.Add(window);
        return window;
    }

    /// <summary>Name of the first entry ending with <paramref name="suffix"/>, or null.</summary>
    public string? FindEntry(string suffix) =>
        _entries.Find(e => e.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))?.Name;

    public void Dispose()
    {
        foreach (var s in _open)
            s.Dispose();
        _open.Clear();
    }

    private void ReadCentralDirectory(FileStream fs)
    {
        long eocd = FindEocd(fs);
        var buf = Read(fs, eocd, 22);
        int entryCount = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(10));
        long cdOffset = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(16));

        if (cdOffset == Zip64Sentinel || entryCount == 0xFFFF)
            (entryCount, cdOffset) = ReadZip64Eocd(fs, eocd);

        long p = cdOffset;
        for (int i = 0; i < entryCount; i++)
        {
            var hdr = Read(fs, p, 46);
            if (BinaryPrimitives.ReadUInt32LittleEndian(hdr) != CentralHeaderSignature)
                throw new BlibException(false,
                    $"'{_path}' is not a readable zip: central directory entry {i} has no header.");

            ushort method = BinaryPrimitives.ReadUInt16LittleEndian(hdr.AsSpan(10));
            long size = BinaryPrimitives.ReadUInt32LittleEndian(hdr.AsSpan(20));
            ushort nameLen = BinaryPrimitives.ReadUInt16LittleEndian(hdr.AsSpan(28));
            ushort extraLen = BinaryPrimitives.ReadUInt16LittleEndian(hdr.AsSpan(30));
            ushort commentLen = BinaryPrimitives.ReadUInt16LittleEndian(hdr.AsSpan(32));
            long localHeader = BinaryPrimitives.ReadUInt32LittleEndian(hdr.AsSpan(42));

            string name = Encoding.UTF8.GetString(Read(fs, p + 46, nameLen));
            if (size == Zip64Sentinel || localHeader == Zip64Sentinel)
                (size, localHeader) = ReadZip64Extra(Read(fs, p + 46 + nameLen, extraLen),
                                                     size, localHeader, name);

            if (method != MethodStored)
                throw new BlibException(false,
                    $"'{name}' in '{_path}' is compressed (method {method}). This container must " +
                    "store its entries uncompressed so they can be read in place.");

            // The central directory's extra field and the local header's may differ in length, so
            // the data offset has to come from the local header.
            var local = Read(fs, localHeader, 30);
            if (BinaryPrimitives.ReadUInt32LittleEndian(local) != LocalHeaderSignature)
                throw new BlibException(false, $"'{name}' in '{_path}' has no local header.");
            long dataOffset = localHeader + 30
                + BinaryPrimitives.ReadUInt16LittleEndian(local.AsSpan(26))
                + BinaryPrimitives.ReadUInt16LittleEndian(local.AsSpan(28));

            _entries.Add(new Entry(name, dataOffset, size));
            p += 46 + nameLen + extraLen + commentLen;
        }
    }

    /// <summary>Scans back for the end-of-central-directory record, which a zip comment can push
    /// up to 64 KiB from the end.</summary>
    private long FindEocd(FileStream fs)
    {
        int window = (int)Math.Min(fs.Length, 22 + 0xFFFF);
        var tail = Read(fs, fs.Length - window, window);
        for (int i = tail.Length - 22; i >= 0; i--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) == EocdSignature)
                return fs.Length - window + i;
        throw new BlibException(false, $"'{_path}' is not a zip container (no end-of-central-directory).");
    }

    private (int count, long offset) ReadZip64Eocd(FileStream fs, long eocd)
    {
        var loc = Read(fs, eocd - 20, 20);
        if (BinaryPrimitives.ReadUInt32LittleEndian(loc) != Eocd64LocatorSignature)
            throw new BlibException(false, $"'{_path}' needs zip64 but has no zip64 locator.");
        long z64 = (long)BinaryPrimitives.ReadUInt64LittleEndian(loc.AsSpan(8));
        var rec = Read(fs, z64, 56);
        if (BinaryPrimitives.ReadUInt32LittleEndian(rec) != Eocd64Signature)
            throw new BlibException(false, $"'{_path}' has a zip64 locator but no zip64 record.");
        return ((int)BinaryPrimitives.ReadUInt64LittleEndian(rec.AsSpan(32)),
                (long)BinaryPrimitives.ReadUInt64LittleEndian(rec.AsSpan(48)));
    }

    /// <summary>
    /// Pulls the real size and local-header offset out of the zip64 extended information extra
    /// field (header id 0x0001), whose values appear only for the fields that overflowed.
    /// </summary>
    private (long size, long localHeader) ReadZip64Extra(byte[] extra, long size, long localHeader,
                                                         string name)
    {
        int i = 0;
        while (i + 4 <= extra.Length)
        {
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(i));
            ushort len = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(i + 2));
            if (id == 0x0001)
            {
                int o = i + 4;
                // Order is fixed: uncompressed, compressed, local header offset, disk - each
                // present only if its 32-bit field held the sentinel.
                if (size == Zip64Sentinel && o + 8 <= extra.Length)
                {
                    o += 8;  // uncompressed size, which we do not use for a stored entry
                    if (o + 8 <= extra.Length)
                    {
                        size = (long)BinaryPrimitives.ReadUInt64LittleEndian(extra.AsSpan(o));
                        o += 8;
                    }
                }
                if (localHeader == Zip64Sentinel && o + 8 <= extra.Length)
                    localHeader = (long)BinaryPrimitives.ReadUInt64LittleEndian(extra.AsSpan(o));
                return (size, localHeader);
            }
            i += 4 + len;
        }
        throw new BlibException(false, $"'{name}' in '{_path}' needs zip64 but has no zip64 extra field.");
    }

    private byte[] Read(FileStream fs, long offset, int count)
    {
        if (offset < 0 || offset + count > fs.Length)
            throw new BlibException(false, $"'{_path}' is truncated or not a zip container.");
        var buf = new byte[count];
        fs.Position = offset;
        fs.ReadExactly(buf, 0, count);
        return buf;
    }

    /// <summary>Read-only seekable view of a byte range of an underlying stream.</summary>
    private sealed class WindowStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _offset;
        private long _position;

        public WindowStream(Stream inner, long offset, long length)
        {
            _inner = inner;
            _offset = offset;
            Length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }

        public override long Position
        {
            get => _position;
            set => _position = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= Length) return 0;
            int want = (int)Math.Min(count, Length - _position);
            _inner.Position = _offset + _position;
            int got = _inner.Read(buffer, offset, want);
            _position += got;
            return got;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
