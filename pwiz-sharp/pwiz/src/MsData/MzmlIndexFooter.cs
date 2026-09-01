using System.Globalization;
using System.IO;
using SystemEncoding = System.Text.Encoding;

namespace Pwiz.Data.MsData.Mzml;

/// <summary>
/// Parses the <c>&lt;indexList&gt;</c> footer of an indexed mzML stream. cpp's
/// <c>Serializer_mzML</c> emits indexed mzML by default; the footer holds the byte
/// offsets of every spectrum and chromatogram, which lets us implement
/// random-access spectrum reads without parsing the whole stream.
/// </summary>
/// <remarks>
/// <para>Stream shape:</para>
/// <code>
/// &lt;indexedmzML&gt;
///   &lt;mzML&gt;...&lt;/mzML&gt;
///   &lt;indexList count="2"&gt;
///     &lt;index name="spectrum"&gt;
///       &lt;offset idRef="..."&gt;N&lt;/offset&gt; ...
///     &lt;/index&gt;
///     &lt;index name="chromatogram"&gt;...&lt;/index&gt;
///   &lt;/indexList&gt;
///   &lt;indexListOffset&gt;N&lt;/indexListOffset&gt;
///   &lt;fileChecksum&gt;...&lt;/fileChecksum&gt;
/// &lt;/indexedmzML&gt;
/// </code>
/// <para>We look for the <c>&lt;indexListOffset&gt;</c> in the last few KB of the stream
/// (the surrounding text is bounded by the SHA-1 checksum, so the footer is at most ~150
/// bytes after it), seek there, slurp the indexList bytes, and parse them with a
/// hand-rolled byte scanner.</para>
/// <para>The Stream-based overload is what makes this reusable for both plain mzML files
/// AND the mzML XML stream embedded inside an mzMLb HDF5 container.</para>
/// </remarks>
internal static class MzmlIndexFooter
{
    /// <summary>
    /// Result of a successful indexed-mzML footer parse: the per-spectrum
    /// <see cref="SpectrumIds"/> + <see cref="SpectrumOffsets"/>, the byte position right
    /// after the closing <c>&lt;/spectrumList&gt;</c> (so the caller can resume XML parsing
    /// past the spectrum bodies in O(1) seek time, without <see cref="System.Xml.XmlReader"/>
    /// having to walk every spectrum to honor a Skip), and the same data for chromatograms
    /// when present.
    /// </summary>
    public readonly record struct Result(
        string[] SpectrumIds,
        long[] SpectrumOffsets,
        long EndOfSpectrumListByteOffset,
        string[] ChromatogramIds,
        long[] ChromatogramOffsets);

    /// <summary>Tries the file at <paramref name="filename"/>. See <see cref="TryRead(Stream)"/>.</summary>
    public static Result? TryRead(string filename)
    {
        try
        {
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return TryRead(fs);
        }
        catch (IOException) { return null; }
    }

    /// <summary>
    /// Attempts to parse the indexList footer of the indexed-mzML content in
    /// <paramref name="stream"/>. The stream must be seekable. Returns the offsets and
    /// the position of <c>&lt;/spectrumList&gt;</c>'s end, or <c>null</c> if the content
    /// isn't indexed mzML (no <c>&lt;indexListOffset&gt;</c> tail marker or malformed).
    /// Best-effort — falling back to eager reading is the caller's responsibility.
    /// </summary>
    public static Result? TryRead(Stream stream)
    {
        if (!stream.CanSeek) return null;

        long indexListOffset;
        try
        {
            indexListOffset = FindIndexListOffset(stream);
            if (indexListOffset < 0) return null;
        }
        catch (IOException) { return null; }

        // Slurp the indexList bytes (small relative to the stream — typically a few MB even
        // for 90k spectra) and parse with a hand-rolled byte scanner. XmlReader can chew
        // through this too, but it's ~10× slower per entry due to per-element string
        // allocations and conformance bookkeeping — and the format is regular enough that
        // we don't need its generality:
        //   <index name="spectrum">
        //     <offset idRef="ID">NUMBER</offset>...
        //   </index>
        //   <index name="chromatogram">...</index>
        (string[] Ids, long[] Offsets) spec, chrom;
        try
        {
            long streamLen = stream.Length;
            int len = (int)System.Math.Min(int.MaxValue, streamLen - indexListOffset);
            var bytes = new byte[len];
            stream.Position = indexListOffset;
            int total = 0;
            while (total < len)
            {
                int got = stream.Read(bytes, total, len - total);
                if (got <= 0) break;
                total += got;
            }
            var parsed = ParseIndexListBytes(bytes.AsSpan(0, total));
            if (parsed is null) return null;
            spec = (parsed.Value.SpectrumIds, parsed.Value.SpectrumOffsets);
            chrom = (parsed.Value.ChromatogramIds, parsed.Value.ChromatogramOffsets);
        }
        catch (IOException) { return null; }

        // Locate "</spectrumList>" by scanning backwards from a known-close upper bound.
        // If there are chromatograms, scan from the first chromatogram's offset (which is
        // a few bytes inside <chromatogramList>, so </spectrumList> is just before).
        // Without that, the chromatogramList content (e.g. a TIC with thousands of points)
        // can be megabytes — far past any reasonable lookback window from indexListOffset.
        long upperBound = chrom.Offsets.Length > 0 ? chrom.Offsets[0] : indexListOffset;
        long endOfSpectrumList;
        try { endOfSpectrumList = FindEndOfSpectrumList(stream, upperBound); }
        catch (IOException) { return null; }
        if (endOfSpectrumList < 0) return null;

        return new Result(spec.Ids, spec.Offsets, endOfSpectrumList, chrom.Ids, chrom.Offsets);
    }

    /// <summary>Back-compat shim: returns just the spectrum (id, offset) pairs.
    /// Prefer <see cref="TryRead(string)"/> or <see cref="TryRead(Stream)"/>.</summary>
    public static (string[] Ids, long[] Offsets)? TryReadSpectrumOffsets(string filename)
    {
        var r = TryRead(filename);
        return r is null ? null : (r.Value.SpectrumIds, r.Value.SpectrumOffsets);
    }

    private static long FindEndOfSpectrumList(Stream stream, long upperBound)
    {
        // </spectrumList> lives somewhere between the spectrum bodies and the indexList
        // footer. The intervening bytes are at most a few KB:
        //   ...</spectrum></spectrumList>[<chromatogramList>...</chromatogramList>]</run></mzML>\n<indexList>
        // Scan back from upperBound for the closing tag; we cap the look-back at 64 KiB
        // which generously covers even a sizeable chromatogramList header.
        const long lookbackBytes = 65536;
        long start = System.Math.Max(0, upperBound - lookbackBytes);
        int len = (int)(upperBound - start);
        var buf = new byte[len];
        stream.Position = start;
        int total = 0;
        while (total < len)
        {
            int got = stream.Read(buf, total, len - total);
            if (got <= 0) break;
            total += got;
        }
        string window = SystemEncoding.ASCII.GetString(buf, 0, total);
        const string closeTag = "</spectrumList>";
        int idx = window.LastIndexOf(closeTag, System.StringComparison.Ordinal);
        if (idx < 0) return -1;
        return start + idx + closeTag.Length;
    }

    // The footer is small (a few hundred bytes max). Scan back from EOS for
    // "<indexListOffset>" — we don't bother with XML parsing for this step since the
    // tag is unambiguous and the surrounding text is bounded by the checksum block.
    private static long FindIndexListOffset(Stream stream)
    {
        const int tailBytes = 4096;
        long streamLen = stream.Length;
        int readLen = (int)System.Math.Min(tailBytes, streamLen);
        stream.Position = streamLen - readLen;
        byte[] buf = new byte[readLen];
        int n = 0;
        while (n < readLen)
        {
            int got = stream.Read(buf, n, readLen - n);
            if (got <= 0) break;
            n += got;
        }
        string tail = SystemEncoding.ASCII.GetString(buf, 0, n);

        const string openTag = "<indexListOffset>";
        const string closeTag = "</indexListOffset>";
        int openIdx = tail.LastIndexOf(openTag, System.StringComparison.Ordinal);
        if (openIdx < 0) return -1;
        int closeIdx = tail.IndexOf(closeTag, openIdx, System.StringComparison.Ordinal);
        if (closeIdx < 0) return -1;
        string number = tail[(openIdx + openTag.Length)..closeIdx].Trim();
        return long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long offset)
            ? offset : -1;
    }

    private record struct IndexListContent(
        string[] SpectrumIds, long[] SpectrumOffsets,
        string[] ChromatogramIds, long[] ChromatogramOffsets);

    // Hand-rolled byte parser for the indexList footer's repetitive
    //   <offset idRef="ID">NUMBER</offset>
    // structure. ~10× faster than XmlReader on a 90k-offset list because we avoid
    // per-element string allocations and ReadElementContentAsString overhead.
    private static IndexListContent? ParseIndexListBytes(System.ReadOnlySpan<byte> bytes)
    {
        var specIds = new System.Collections.Generic.List<string>();
        var specOff = new System.Collections.Generic.List<long>();
        var chrIds = new System.Collections.Generic.List<string>();
        var chrOff = new System.Collections.Generic.List<long>();
        bool sawSpectrumIndex = false;

        int pos = 0;

        System.ReadOnlySpan<byte> tagIndexStart = "<index "u8;
        System.ReadOnlySpan<byte> tagIndexEnd = "</index>"u8;
        System.ReadOnlySpan<byte> tagOffsetStart = "<offset "u8;
        System.ReadOnlySpan<byte> tagOffsetEnd = "</offset>"u8;
        System.ReadOnlySpan<byte> attrName = "name=\""u8;
        System.ReadOnlySpan<byte> attrIdRef = "idRef=\""u8;
        System.ReadOnlySpan<byte> tagIndexListEnd = "</indexList>"u8;

        System.Collections.Generic.List<string>? curIds = null;
        System.Collections.Generic.List<long>? curOff = null;

        while (pos < bytes.Length)
        {
            int next = bytes[pos..].IndexOf((byte)'<');
            if (next < 0) break;
            pos += next;
            var slice = bytes[pos..];

            if (slice.StartsWith(tagIndexListEnd)) break;

            if (slice.StartsWith(tagIndexStart))
            {
                int nameAt = slice.IndexOf(attrName);
                if (nameAt < 0) return null;
                var afterName = slice[(nameAt + attrName.Length)..];
                int closeQuote = afterName.IndexOf((byte)'"');
                if (closeQuote < 0) return null;
                string name = System.Text.Encoding.ASCII.GetString(afterName[..closeQuote]);
                if (name == "spectrum") { curIds = specIds; curOff = specOff; sawSpectrumIndex = true; }
                else if (name == "chromatogram") { curIds = chrIds; curOff = chrOff; }
                else { curIds = null; curOff = null; }
                pos += nameAt + attrName.Length + closeQuote + 1;
                continue;
            }
            if (slice.StartsWith(tagIndexEnd))
            {
                curIds = null;
                curOff = null;
                pos += tagIndexEnd.Length;
                continue;
            }
            if (slice.StartsWith(tagOffsetStart) && curIds is not null && curOff is not null)
            {
                int idAt = slice.IndexOf(attrIdRef);
                if (idAt < 0) return null;
                var afterId = slice[(idAt + attrIdRef.Length)..];
                int closeQuote = afterId.IndexOf((byte)'"');
                if (closeQuote < 0) return null;
                string id = System.Text.Encoding.ASCII.GetString(afterId[..closeQuote]);
                var afterTag = afterId[(closeQuote + 1)..];
                int gt = afterTag.IndexOf((byte)'>');
                if (gt < 0) return null;
                var content = afterTag[(gt + 1)..];
                int end = content.IndexOf(tagOffsetEnd);
                if (end < 0) return null;
                if (!System.Buffers.Text.Utf8Parser.TryParse(content[..end], out long value, out _))
                    return null;
                curIds.Add(id);
                curOff.Add(value);
                pos += idAt + attrIdRef.Length + closeQuote + 1 + gt + 1 + end + tagOffsetEnd.Length;
                continue;
            }
            pos++;
        }

        if (!sawSpectrumIndex) return null;
        return new IndexListContent(
            specIds.ToArray(), specOff.ToArray(),
            chrIds.ToArray(), chrOff.ToArray());
    }
}
