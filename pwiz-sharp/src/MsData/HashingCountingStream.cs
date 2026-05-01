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

    public HashingCountingStream(Stream inner) => _inner = inner;

    /// <summary>Returns the SHA-1 digest of everything written while hashing was active.</summary>
    public byte[] GetCurrentHash() => _hash.GetCurrentHash();

    /// <summary>Stops appending to the hash. Any bytes written after this are excluded from the digest.</summary>
    public void StopHashing() => _hashActive = false;

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inner.Write(buffer, offset, count);
        if (_hashActive) _hash.AppendData(buffer, offset, count);
        BytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _inner.Write(buffer);
        if (_hashActive) _hash.AppendData(buffer);
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
