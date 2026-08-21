using System;
using System.Collections.Generic;
using System.IO;
using ParquetSharp;

namespace Pwiz.Data.MsData.MzPeak;

/// <summary>
/// Opens the parquet entries of a .mzpeak ZIP <em>in place</em> — without extracting the archive
/// to a temp directory — when every entry is Stored (uncompressed), which is how
/// <see cref="MzPeakWriter"/> writes them. Each entry's data is then a contiguous, seekable byte
/// range, so a <see cref="ParquetFileReader"/> can read it through a length-bounded sub-stream over
/// the archive file. This avoids copying the (often hundreds-of-MB) data parquet to disk just to
/// read a few row groups. Falls back to <c>null</c> (caller extracts) if any entry isn't Stored or
/// the central directory can't be parsed.
/// </summary>
internal sealed class MzPeakArchive : IDisposable
{
    private readonly string _path;
    private readonly Dictionary<string, (long Offset, long Length)> _entries;

    private MzPeakArchive(string path, Dictionary<string, (long, long)> entries)
    {
        _path = path;
        _entries = entries;
    }

    public bool HasEntry(string name) => _entries.ContainsKey(name);

    /// <summary>Open a parquet entry as a ParquetFileReader over a seekable sub-stream; null if absent.</summary>
    public ParquetFileReader? OpenParquet(string name)
    {
        if (!_entries.TryGetValue(name, out var e)) return null;
        var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sub = new BoundedSubStream(fs, e.Offset, e.Length);   // disposes fs when disposed
        return new ParquetFileReader(sub, leaveOpen: false);       // disposes sub on Close
    }

    public void Dispose() { /* OpenParquet streams are owned by their ParquetFileReader */ }

    /// <summary>
    /// Parse the ZIP central directory and return Stored entries' (data offset, length). Returns
    /// null if the archive isn't all-Stored or can't be parsed (caller falls back to extraction).
    /// </summary>
    public static MzPeakArchive? TryOpen(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            long fileLen = fs.Length;
            int maxScan = (int)Math.Min(fileLen, 65557);          // 64KB comment + 22-byte EOCD
            var tail = new byte[maxScan];
            fs.Seek(fileLen - maxScan, SeekOrigin.Begin);
            fs.ReadExactly(tail, 0, maxScan);

            int eocd = -1;
            for (int i = maxScan - 22; i >= 0; i--)
                if (tail[i] == 0x50 && tail[i + 1] == 0x4b && tail[i + 2] == 0x05 && tail[i + 3] == 0x06) { eocd = i; break; }
            if (eocd < 0) return null;

            long cdOffset = U32(tail, eocd + 16);
            long cdEntries = U16(tail, eocd + 10);

            // Zip64: when the 32-bit fields are saturated, the real values live in the Zip64 EOCD.
            if (cdOffset == 0xFFFFFFFF || cdEntries == 0xFFFF)
            {
                int loc = -1;
                for (int i = eocd - 20; i >= 0; i--)
                    if (tail[i] == 0x50 && tail[i + 1] == 0x4b && tail[i + 2] == 0x06 && tail[i + 3] == 0x07) { loc = i; break; }
                if (loc < 0) return null;
                long z64Eocd = (long)U64(tail, loc + 8);
                var z64 = new byte[56];
                fs.Seek(z64Eocd, SeekOrigin.Begin);
                fs.ReadExactly(z64, 0, 56);
                if (!(z64[0] == 0x50 && z64[1] == 0x4b && z64[2] == 0x06 && z64[3] == 0x06)) return null;
                cdEntries = (long)U64(z64, 32);
                cdOffset = (long)U64(z64, 48);
            }

            fs.Seek(cdOffset, SeekOrigin.Begin);
            var entries = new Dictionary<string, (long, long)>(StringComparer.Ordinal);
            for (long e = 0; e < cdEntries; e++)
            {
                var hdr = new byte[46];
                fs.ReadExactly(hdr, 0, 46);
                if (!(hdr[0] == 0x50 && hdr[1] == 0x4b && hdr[2] == 0x01 && hdr[3] == 0x02)) return null;
                int method = U16(hdr, 10);
                long uncompSize = U32(hdr, 24);
                int nameLen = U16(hdr, 28);
                int extraLen = U16(hdr, 30);
                int commentLen = U16(hdr, 32);
                long localOffset = U32(hdr, 42);

                var nameBytes = new byte[nameLen];
                fs.ReadExactly(nameBytes, 0, nameLen);
                var extra = new byte[extraLen];
                fs.ReadExactly(extra, 0, extraLen);
                if (commentLen > 0) fs.Seek(commentLen, SeekOrigin.Current);

                if (method != 0) return null;   // not Stored → can't sub-stream; fall back to extraction

                // Zip64 extra field (id 0x0001) supplies any saturated size/offset, in field order.
                if (uncompSize == 0xFFFFFFFF || localOffset == 0xFFFFFFFF)
                {
                    bool ok = ReadZip64Extra(extra, U32(hdr, 20) == 0xFFFFFFFF, uncompSize == 0xFFFFFFFF,
                        localOffset == 0xFFFFFFFF, out long u64Uncomp, out long u64Offset);
                    if (!ok) return null;
                    if (uncompSize == 0xFFFFFFFF) uncompSize = u64Uncomp;
                    if (localOffset == 0xFFFFFFFF) localOffset = u64Offset;
                }

                // Local header: data starts after its own (possibly different) name+extra lengths.
                long savedCdPos = fs.Position;
                var lh = new byte[30];
                fs.Seek(localOffset, SeekOrigin.Begin);
                fs.ReadExactly(lh, 0, 30);
                if (!(lh[0] == 0x50 && lh[1] == 0x4b && lh[2] == 0x03 && lh[3] == 0x04)) return null;
                int lNameLen = U16(lh, 26);
                int lExtraLen = U16(lh, 28);
                long dataOffset = localOffset + 30 + lNameLen + lExtraLen;
                fs.Seek(savedCdPos, SeekOrigin.Begin);

                string name = System.Text.Encoding.UTF8.GetString(nameBytes);
                entries[name] = (dataOffset, uncompSize);
            }
            return entries.Count == 0 ? null : new MzPeakArchive(path, entries);
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadZip64Extra(byte[] extra, bool needComp, bool needUncomp, bool needOffset,
        out long uncomp, out long offset)
    {
        uncomp = 0; offset = 0;
        int p = 0;
        while (p + 4 <= extra.Length)
        {
            int id = U16(extra, p);
            int size = U16(extra, p + 2);
            int body = p + 4;
            if (id == 0x0001)
            {
                int q = body;
                if (needUncomp) { if (q + 8 > extra.Length) return false; uncomp = (long)U64(extra, q); q += 8; }
                if (needComp) { q += 8; }                       // compressed size precedes offset
                if (needOffset) { if (q + 8 > extra.Length) return false; offset = (long)U64(extra, q); }
                return true;
            }
            p = body + size;
        }
        return false;
    }

    private static int U16(byte[] b, int o) => b[o] | (b[o + 1] << 8);
    private static long U32(byte[] b, int o) => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    private static ulong U64(byte[] b, int o)
    {
        ulong v = 0;
        for (int i = 7; i >= 0; i--) v = (v << 8) | b[o + i];
        return v;
    }

    /// <summary>Read-only seekable window [offset, offset+length) over an owned FileStream.</summary>
    private sealed class BoundedSubStream : Stream
    {
        private readonly FileStream _inner;
        private readonly long _offset;
        private readonly long _length;
        private long _pos;

        public BoundedSubStream(FileStream inner, long offset, long length)
        {
            _inner = inner;
            _offset = offset;
            _length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _pos;
            set => _pos = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos >= _length) return 0;
            int toRead = (int)Math.Min(count, _length - _pos);
            _inner.Seek(_offset + _pos, SeekOrigin.Begin);
            int n = _inner.Read(buffer, offset, toRead);
            _pos += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _pos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _pos + offset,
                SeekOrigin.End => _length + offset,
                _ => _pos,
            };
            return _pos;
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
