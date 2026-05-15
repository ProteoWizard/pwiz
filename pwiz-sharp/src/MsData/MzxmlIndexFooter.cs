using System.Globalization;
using System.IO;
using SystemEncoding = System.Text.Encoding;

namespace Pwiz.Data.MsData.MzXml;

/// <summary>
/// Parses the <c>&lt;index&gt;</c> footer of an indexed mzXML file. cpp's
/// <c>SpectrumList_mzXMLImpl::readIndex</c> uses the same approach: find
/// <c>&lt;indexOffset&gt;</c> in the file tail, seek there, and parse the
/// <c>&lt;index name="scan"&gt;&lt;offset id="..."&gt;...&lt;/offset&gt;...&lt;/index&gt;</c>
/// table for per-scan byte offsets.
/// </summary>
/// <remarks>
/// Footer shape (matches <c>MzxmlWriter</c>):
/// <code>
/// &lt;index name="scan"&gt;
///   &lt;offset id="1"&gt;N&lt;/offset&gt;
///   ...
/// &lt;/index&gt;
/// &lt;indexOffset&gt;N&lt;/indexOffset&gt;
/// &lt;sha1&gt;...&lt;/sha1&gt;
/// &lt;/mzXML&gt;
/// </code>
/// Each offset is the byte position of the corresponding <c>&lt;scan&gt;</c>
/// element's opening <c>&lt;</c>.
/// </remarks>
internal static class MzxmlIndexFooter
{
    /// <summary>Per-scan id ("scan=NN", matching cpp's
    /// <c>id::translateScanNumberToNativeID</c> for mzXML's <c>scan_number_only</c>
    /// nativeID format) + byte offset to the corresponding <c>&lt;scan&gt;</c> element.</summary>
    public readonly record struct Result(string[] ScanIds, long[] ScanOffsets);

    /// <summary>Tries the file at <paramref name="filename"/>. Returns null when the
    /// file isn't an indexed mzXML or the footer is malformed.</summary>
    public static Result? TryRead(string filename)
    {
        try
        {
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            return TryRead(fs);
        }
        catch (IOException) { return null; }
    }

    /// <summary>Stream overload. Stream must be seekable. Best-effort: callers should
    /// fall back to an eager parse on null.</summary>
    public static Result? TryRead(Stream stream)
    {
        if (!stream.CanSeek) return null;

        long indexOffset;
        try
        {
            indexOffset = FindIndexOffset(stream);
            if (indexOffset < 0) return null;
        }
        catch (IOException) { return null; }

        try
        {
            long streamLen = stream.Length;
            // The <index> block runs from indexOffset to the start of <indexOffset>...
            // since we already located the latter, that's a generous upper bound for the
            // slurp. Cap at int.MaxValue so we can use a single byte[] buffer.
            int len = (int)System.Math.Min(int.MaxValue, streamLen - indexOffset);
            var bytes = new byte[len];
            stream.Position = indexOffset;
            int total = 0;
            while (total < len)
            {
                int got = stream.Read(bytes, total, len - total);
                if (got <= 0) break;
                total += got;
            }
            return ParseIndexBytes(bytes.AsSpan(0, total));
        }
        catch (IOException) { return null; }
    }

    private static long FindIndexOffset(Stream stream)
    {
        // <indexOffset>NNNNN</indexOffset> lives near the end of the file, between
        // </index> and <sha1>. Scan back ~4 KiB to cover any reasonable sha1 + closing
        // tag size; the <indexOffset> tag itself is unambiguous so a plain string search
        // is sufficient.
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

        const string openTag = "<indexOffset>";
        const string closeTag = "</indexOffset>";
        int openIdx = tail.LastIndexOf(openTag, System.StringComparison.Ordinal);
        if (openIdx < 0) return -1;
        int closeIdx = tail.IndexOf(closeTag, openIdx, System.StringComparison.Ordinal);
        if (closeIdx < 0) return -1;
        string number = tail[(openIdx + openTag.Length)..closeIdx].Trim();
        return long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long offset)
            ? offset : -1;
    }

    // Hand-rolled byte parser for the repetitive
    //   <offset id="N">byte_offset</offset>
    // table. ~10× faster than XmlReader on a 27k-offset file because we avoid
    // per-element string allocations and ReadElementContentAsString overhead.
    private static Result? ParseIndexBytes(System.ReadOnlySpan<byte> bytes)
    {
        var ids = new System.Collections.Generic.List<string>();
        var offsets = new System.Collections.Generic.List<long>();

        System.ReadOnlySpan<byte> tagIndexStart = "<index "u8;
        System.ReadOnlySpan<byte> tagIndexEnd = "</index>"u8;
        System.ReadOnlySpan<byte> tagOffsetStart = "<offset "u8;
        System.ReadOnlySpan<byte> tagOffsetEnd = "</offset>"u8;
        System.ReadOnlySpan<byte> attrId = "id=\""u8;

        bool sawIndex = false;
        int pos = 0;

        while (pos < bytes.Length)
        {
            int next = bytes[pos..].IndexOf((byte)'<');
            if (next < 0) break;
            pos += next;
            var slice = bytes[pos..];

            if (slice.StartsWith(tagIndexEnd)) break;

            if (slice.StartsWith(tagIndexStart))
            {
                sawIndex = true;
                int gt = slice.IndexOf((byte)'>');
                if (gt < 0) return null;
                pos += gt + 1;
                continue;
            }

            if (slice.StartsWith(tagOffsetStart))
            {
                int idAt = slice.IndexOf(attrId);
                if (idAt < 0) return null;
                var afterId = slice[(idAt + attrId.Length)..];
                int closeQuote = afterId.IndexOf((byte)'"');
                if (closeQuote < 0) return null;
                string scanNumber = SystemEncoding.ASCII.GetString(afterId[..closeQuote]);
                var afterTag = afterId[(closeQuote + 1)..];
                int gt = afterTag.IndexOf((byte)'>');
                if (gt < 0) return null;
                var content = afterTag[(gt + 1)..];
                int end = content.IndexOf(tagOffsetEnd);
                if (end < 0) return null;
                if (!System.Buffers.Text.Utf8Parser.TryParse(content[..end], out long value, out _))
                    return null;
                ids.Add("scan=" + scanNumber);
                offsets.Add(value);
                pos += idAt + attrId.Length + closeQuote + 1 + gt + 1 + end + tagOffsetEnd.Length;
                continue;
            }
            pos++;
        }

        if (!sawIndex || ids.Count == 0) return null;
        return new Result(ids.ToArray(), offsets.ToArray());
    }
}
