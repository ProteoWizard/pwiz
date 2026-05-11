using System.Security.Cryptography;

namespace Pwiz.Data.MsData.Mzml;

/// <summary>
/// A write-only stream wrapper that tracks bytes written and feeds them through an incremental
/// SHA-1 hasher. Used by the indexedmzML writer to record byte offsets for each spectrum /
/// chromatogram and compute the <c>fileChecksum</c> over the file up to (but not including) the
/// digest text.
/// </summary>
#pragma warning disable CA5350 // SHA-1 is mandated by the mzML indexedmzML spec, not a security hash.
internal sealed class HashingCountingStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
    private bool _hashActive = true;

    /// <summary>Total bytes written through this wrapper since construction.</summary>
    public long BytesWritten { get; private set; }

    /// <summary>Byte offset of the most recently observed <c>&lt;</c> character, or -1 if the
    /// tracker was reset since the last write. Used by <see cref="MzmlWriter"/> to capture the
    /// position of <c>&lt;spectrum&gt;</c> / <c>&lt;chromatogram&gt;</c> start tags for the
    /// indexedmzML offset list without having to inject manual whitespace (which would put the
    /// XmlWriter into mixed-content mode and suppress pretty-printing of element children).
    /// </summary>
    public long LastLtPos { get; private set; } = -1;

    /// <summary>Clear <see cref="LastLtPos"/>; call this right before writing an element whose
    /// start-tag position you want to capture, then read <see cref="LastLtPos"/> after the
    /// XmlWriter has flushed the start tag.</summary>
    public void ResetLastLt() => LastLtPos = -1;

    public HashingCountingStream(Stream inner) => _inner = inner;

    /// <summary>Returns the SHA-1 digest of everything written while hashing was active.</summary>
    public byte[] GetCurrentHash() => _hash.GetCurrentHash();

    /// <summary>Stops appending to the hash. Any bytes written after this are excluded from the digest.</summary>
    public void StopHashing() => _hashActive = false;

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        if (_hashActive) _hash.AppendData(buffer, offset, count);
        if (LastLtPos < 0)
        {
            long basePos = BytesWritten;
            for (int i = 0; i < count; i++)
                if (buffer[offset + i] == (byte)'<') { LastLtPos = basePos + i; break; }
        }
        BytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        if (_hashActive) _hash.AppendData(buffer);
        if (LastLtPos < 0)
        {
            long basePos = BytesWritten;
            for (int i = 0; i < buffer.Length; i++)
                if (buffer[i] == (byte)'<') { LastLtPos = basePos + i; break; }
        }
        BytesWritten += buffer.Length;
    }

    public override void Flush() => _inner.Flush();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => BytesWritten;
    public override long Position
    {
        get => BytesWritten;
        set => throw new NotSupportedException();
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hash.Dispose();
        base.Dispose(disposing);
    }
}
