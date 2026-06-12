using System.IO;
using System.Xml;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.MzXml;

/// <summary>
/// Lazy <see cref="ISpectrumList"/> backed by an indexed mzXML file. Mirrors
/// cpp's <c>SpectrumList_mzXML</c>: parses the <c>&lt;index&gt;</c> footer
/// once at construction to learn each scan's byte offset, then seeks and
/// parses a single <c>&lt;scan&gt;</c> element on demand. Only the scan
/// being read is in memory at any one time — the difference between
/// streaming a 20 GB mzXML and OOMing on a 200 MB one.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "Matches cpp pwiz::msdata::SpectrumList_mzXML class name; pwiz-sharp convention preserves cpp class names verbatim.")]
public sealed class SpectrumList_Mzxml : SpectrumListBase
{
    private readonly string _filename;
    private readonly MzxmlReader _context;
    private readonly long[] _offsets;
    private readonly string[] _ids;
    private SpectrumIdentity[]? _identities;
    private System.Collections.Generic.Dictionary<string, int>? _idMap;

    // Stream is opened lazily on first read so an unused list doesn't hold a
    // file handle. A lock guards stream seeks across threads.
    private Stream? _stream;
    private readonly object _streamLock = new();
    private bool _disposed;

    internal SpectrumList_Mzxml(string filename, MzxmlReader context,
                                string[] ids, long[] offsets,
                                Pwiz.Data.MsData.Processing.DataProcessing? dp = null)
    {
        _filename = filename;
        _context = context;
        _ids = ids;
        _offsets = offsets;
        _dp = dp;
    }

    private readonly Pwiz.Data.MsData.Processing.DataProcessing? _dp;

    /// <inheritdoc/>
    public override Pwiz.Data.MsData.Processing.DataProcessing? DataProcessing => _dp;

    /// <inheritdoc/>
    public override int Count => _offsets.Length;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index)
    {
        if ((uint)index >= (uint)_offsets.Length)
            throw new System.ArgumentOutOfRangeException(nameof(index));
        var ids = _identities;
        if (ids is null)
        {
            ids = new SpectrumIdentity[_ids.Length];
            for (int i = 0; i < _ids.Length; i++)
                ids[i] = new SpectrumIdentity { Id = _ids[i], Index = i };
            _identities = ids;
        }
        return ids[index];
    }

    /// <inheritdoc/>
    public override int Find(string id)
    {
        System.ArgumentNullException.ThrowIfNull(id);
        var map = _idMap;
        if (map is null)
        {
            map = new System.Collections.Generic.Dictionary<string, int>(_ids.Length, System.StringComparer.Ordinal);
            for (int i = 0; i < _ids.Length; i++) map[_ids[i]] = i;
            _idMap = map;
        }
        return map.TryGetValue(id, out int idx) ? idx : _offsets.Length;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        if ((uint)index >= (uint)_offsets.Length)
            throw new System.ArgumentOutOfRangeException(nameof(index));

        lock (_streamLock)
        {
            System.ObjectDisposedException.ThrowIf(_disposed, this);
            _stream ??= new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read,
                                       bufferSize: 1 << 16, FileOptions.RandomAccess);
            // cpp parity / OLD-mzXML-1.x bug-compat: ReAdW + TPP era indexes record offsets a
            // handful of bytes BEFORE the scan they describe — often the offset is the start of
            // the preceding whitespace, and sometimes it's mid-element (e.g. inside a trailing
            // self-closing "/>" of the prior tag or even the leading "</dataProcessing>" of the
            // header). XmlReader.ReadToFollowing trips on the unmatched close tags. Mirror cpp's
            // SpectrumList_mzXML::seekToScan which scan-forwards the raw byte stream until it
            // finds "<scan" before handing the cursor to the XML parser.
            _stream.Position = SeekToScanTag(_stream, _offsets[index]);

            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                CloseInput = false,
                ConformanceLevel = ConformanceLevel.Fragment,
            };
            using var xr = XmlReader.Create(_stream, settings);
            if (!xr.ReadToFollowing("scan"))
                throw new InvalidDataException($"No <scan> element at offset {_offsets[index]} in {_filename}");

            var spec = _context.ReadOneScan(xr, getBinaryData);
            // The index records id only; index numbers must be stamped from our
            // position in the offset table since mzXML <scan num="..."> values
            // can be non-contiguous (e.g. some Thermo workflows skip numbers).
            spec.Index = index;
            spec.Id = _ids[index];
            return spec;
        }
    }

    /// <summary>
    /// Scan forward from <paramref name="startOffset"/> looking for the literal byte sequence
    /// <c>"&lt;scan"</c> (followed by either whitespace or <c>'&gt;'</c>). The mzXML format only
    /// uses ASCII for markup, so a byte-level search is safe regardless of the file's declared
    /// encoding. Caps the search at 64 KiB past the recorded offset — well beyond any realistic
    /// index-vs-content drift but small enough that a truly broken file fails fast instead of
    /// streaming the whole tail of the file looking for a match that doesn't exist.
    /// </summary>
    private static long SeekToScanTag(Stream stream, long startOffset)
    {
        const int maxLookahead = 64 * 1024;
        stream.Position = startOffset;
        Span<byte> needle = stackalloc byte[5] { (byte)'<', (byte)'s', (byte)'c', (byte)'a', (byte)'n' };
        Span<byte> window = stackalloc byte[256];
        long absolutePos = startOffset;
        long capPos = startOffset + maxLookahead;
        int carry = 0;
        // Sized at needle.Length: we carry exactly the trailing N bytes of one window so a
        // needle that straddles two stream reads still matches.
        Span<byte> carryBuf = stackalloc byte[5];
        while (absolutePos < capPos)
        {
            int wanted = window.Length - carry;
            int got = stream.Read(window[carry..(carry + wanted)]);
            if (got <= 0) break;
            int totalInWindow = carry + got;
            for (int i = 0; i + needle.Length < totalInWindow; i++)
            {
                if (window[i] != needle[0]) continue;
                if (window.Slice(i, needle.Length).SequenceEqual(needle))
                {
                    // Require the next byte to be either '>' (no attributes) or ASCII whitespace
                    // so we don't match "<scan_log_extra" or similar non-scan elements that just
                    // happen to start with the same five characters.
                    byte after = window[i + needle.Length];
                    if (after == (byte)'>' || after == (byte)' ' || after == (byte)'\t'
                        || after == (byte)'\r' || after == (byte)'\n')
                    {
                        return absolutePos + i;
                    }
                }
            }
            // Carry the last few bytes so a needle that straddles two window reads still matches.
            carry = needle.Length;
            int tailStart = totalInWindow - carry;
            window.Slice(tailStart, carry).CopyTo(carryBuf);
            absolutePos += tailStart;
            carryBuf[..carry].CopyTo(window[..carry]);
        }
        // No match — fall back to the recorded offset and let the XML parser surface a useful
        // error message rather than silently advancing past a real problem.
        return startOffset;
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        lock (_streamLock)
        {
            if (_disposed) return;
            _disposed = true;
            _stream?.Dispose();
            _stream = null;
        }
    }
}
