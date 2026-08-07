using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.MsData.NativeAot;

/// <summary>
/// Flat C API surfaced via .NET 8 Native AOT. Each [UnmanagedCallersOnly] wrapper
/// here forwards to a logic method in <see cref="ExportsImpl"/>; the split lets the
/// MsData.NativeAot.Tests project drive the logic from MSTest (managed callers
/// cannot invoke [UnmanagedCallersOnly] methods directly), while the AOT-compiled
/// shared library still exposes the unmangled <c>pwiz_msdata_*</c> entry points by
/// name — see <c>examples/cpp-aot-reader/pwiz_msdata.h</c> for the matching C
/// signatures.
/// </summary>
/// <remarks>
/// <para>Handles are <see cref="GCHandle"/>s wrapping the managed <see cref="MSData"/>
/// instance. Pinning isn't needed (we never hand a managed pointer to C); the GCHandle
/// just keeps the document rooted until the caller closes it.</para>
/// <para>Strings cross the boundary as UTF-8 null-terminated <c>byte*</c>. Output
/// strings use a "return required length, caller retries with bigger buffer" idiom
/// so callers can size their buffers without a separate length probe.</para>
/// <para>Errors set a thread-local last-error string retrievable via
/// <c>pwiz_msdata_last_error</c>; success returns 0, failure returns a negative error
/// code (mirroring the rest of pwiz-sharp's native-binding conventions).</para>
/// </remarks>
public static class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "pwiz_msdata_open")]
    public static unsafe int Open(byte* path, IntPtr* outHandle) =>
        ExportsImpl.Open(path, outHandle);

    [UnmanagedCallersOnly(EntryPoint = "pwiz_msdata_spectrum_count")]
    public static int SpectrumCount(IntPtr handle) =>
        ExportsImpl.SpectrumCount(handle);

    [UnmanagedCallersOnly(EntryPoint = "pwiz_msdata_spectrum_id")]
    public static unsafe int SpectrumId(IntPtr handle, int index, byte* idBuf, int idBufLen) =>
        ExportsImpl.SpectrumId(handle, index, idBuf, idBufLen);

    [UnmanagedCallersOnly(EntryPoint = "pwiz_msdata_spectrum_peak_count")]
    public static int SpectrumPeakCount(IntPtr handle, int index) =>
        ExportsImpl.SpectrumPeakCount(handle, index);

    [UnmanagedCallersOnly(EntryPoint = "pwiz_msdata_source_id")]
    public static unsafe int SourceId(IntPtr handle, byte* buf, int bufLen) =>
        ExportsImpl.SourceId(handle, buf, bufLen);

    [UnmanagedCallersOnly(EntryPoint = "pwiz_msdata_close")]
    public static void Close(IntPtr handle) =>
        ExportsImpl.Close(handle);

    [UnmanagedCallersOnly(EntryPoint = "pwiz_msdata_last_error")]
    public static unsafe int GetLastError(byte* buf, int bufLen) =>
        ExportsImpl.GetLastError(buf, bufLen);
}

/// <summary>
/// Logic methods backing <see cref="Exports"/>. Split out so MsData.NativeAot.Tests
/// can call them — [UnmanagedCallersOnly] methods can only be invoked via function
/// pointer, which is awkward in MSTest. This split keeps the AOT-export layer
/// trivially thin (attribute + forward) while making the marshaling logic
/// straightforwardly testable.
/// </summary>
public static class ExportsImpl
{
    // Error codes mirror the simple POSIX-style convention: 0 = success, negative = failure.
    public const int Ok = 0;
    public const int ErrInvalidHandle = -1;
    public const int ErrInvalidArg = -2;
    public const int ErrIndexOutOfRange = -3;
    public const int ErrIoFailure = -4;

    [ThreadStatic] private static string? _lastError;

    /// <summary>Opens an MS data file. <c>path</c> is a UTF-8 null-terminated C string;
    /// on success, <c>*outHandle</c> receives an opaque handle that must be released via
    /// <see cref="Close"/>. Supported formats: mzML, mzXML, MGF. Returns 0 on success or
    /// a negative error code; on error, the failure reason is available via
    /// <see cref="GetLastError"/>.</summary>
    public static unsafe int Open(byte* path, IntPtr* outHandle)
    {
        if (path == null || outHandle == null) return ErrInvalidArg;
        *outHandle = IntPtr.Zero;

        string managedPath = Utf8ToString(path);
        try
        {
            var msd = new MSData();
            ReaderList.Default.Read(managedPath, msd, new ReaderConfig());
            // GCHandle.Normal keeps the document rooted until the caller closes it.
            var handle = GCHandle.Alloc(msd, GCHandleType.Normal);
            *outHandle = GCHandle.ToIntPtr(handle);
            return Ok;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return ErrIoFailure;
        }
    }

    /// <summary>Returns the number of spectra in the file's spectrum list, or a
    /// negative error code if the handle is invalid.</summary>
    public static int SpectrumCount(IntPtr handle)
    {
        if (!TryGetMsData(handle, out var msd)) return ErrInvalidHandle;
        return msd.Run?.SpectrumList?.Count ?? 0;
    }

    /// <summary>Writes the spectrum id at <paramref name="index"/> to <paramref name="idBuf"/>
    /// as a UTF-8 null-terminated string, truncating if the buffer is smaller than the id +
    /// terminator. Returns the FULL UTF-8 byte length of the id (excluding the terminator) so
    /// callers can detect truncation and retry with a larger buffer; returns a negative error
    /// code if the handle is invalid or the index is out of range.</summary>
    public static unsafe int SpectrumId(IntPtr handle, int index, byte* idBuf, int idBufLen)
    {
        if (!TryGetMsData(handle, out var msd)) return ErrInvalidHandle;
        var sl = msd.Run?.SpectrumList;
        if (sl is null) return ErrInvalidHandle;
        if ((uint)index >= (uint)sl.Count) return ErrIndexOutOfRange;

        return WriteUtf8(sl.SpectrumIdentity(index).Id, idBuf, idBufLen);
    }

    /// <summary>Returns the number of (m/z, intensity) peaks in the spectrum at
    /// <paramref name="index"/>, reading binary data lazily. Returns a negative error
    /// code if the handle is invalid or the index is out of range.</summary>
    public static int SpectrumPeakCount(IntPtr handle, int index)
    {
        if (!TryGetMsData(handle, out var msd)) return ErrInvalidHandle;
        var sl = msd.Run?.SpectrumList;
        if (sl is null) return ErrInvalidHandle;
        if ((uint)index >= (uint)sl.Count) return ErrIndexOutOfRange;

        try
        {
            var spectrum = sl.GetSpectrum(index, getBinaryData: true);
            return spectrum.DefaultArrayLength;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            return ErrIoFailure;
        }
    }

    /// <summary>Returns the file's source-id (typically the basename without extension)
    /// — same length-probe / truncate convention as <see cref="SpectrumId"/>.</summary>
    public static unsafe int SourceId(IntPtr handle, byte* buf, int bufLen)
    {
        if (!TryGetMsData(handle, out var msd)) return ErrInvalidHandle;
        return WriteUtf8(msd.Id, buf, bufLen);
    }

    /// <summary>Releases the MS data document referenced by <paramref name="handle"/>.
    /// After this returns, the handle is invalid and must not be reused. Safe to call
    /// with <see cref="IntPtr.Zero"/> (no-op).</summary>
    public static void Close(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        try
        {
            var gcHandle = GCHandle.FromIntPtr(handle);
            if (gcHandle.IsAllocated)
            {
                if (gcHandle.Target is IDisposable d) d.Dispose();
                gcHandle.Free();
            }
        }
        catch
        {
            // FromIntPtr can throw if the caller passed garbage — swallow so this looks
            // like a no-op to misbehaved consumers rather than aborting the process.
        }
    }

    /// <summary>Returns the last error message set on this thread (UTF-8, null-terminated,
    /// truncated to <paramref name="bufLen"/> - 1 bytes) and returns the FULL UTF-8 byte
    /// length. Returns 0 when no error is pending.</summary>
    public static unsafe int GetLastError(byte* buf, int bufLen)
    {
        string err = _lastError ?? string.Empty;
        return WriteUtf8(err, buf, bufLen);
    }

    // ------ Helpers ------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetMsData(IntPtr handle, out MSData msd)
    {
        msd = null!;
        if (handle == IntPtr.Zero) return false;
        try
        {
            var gc = GCHandle.FromIntPtr(handle);
            if (!gc.IsAllocated) return false;
            if (gc.Target is not MSData m) return false;
            msd = m;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static unsafe string Utf8ToString(byte* p)
    {
        int len = 0;
        while (p[len] != 0) len++;
        return len == 0 ? string.Empty : Encoding.UTF8.GetString(p, len);
    }

    private static unsafe int WriteUtf8(string s, byte* buf, int bufLen)
    {
        int byteLen = Encoding.UTF8.GetByteCount(s);
        if (buf != null && bufLen > 0)
        {
            int copyLen = System.Math.Min(byteLen, bufLen - 1);
            if (copyLen > 0)
            {
                // Encoding.UTF8.GetBytes throws if the dest can't fit the FULL encoded
                // output — it won't auto-truncate. Encode the whole string to a temp
                // buffer first (cheap for typical mzML ids which are <50 ASCII bytes),
                // then memcpy the prefix. For UTF-8 multi-byte chars this also avoids
                // splitting a codepoint mid-sequence — bytes before copyLen may end on
                // an invalid boundary, but at least we never write a partial char.
                byte[] full = byteLen <= 256 ? s_scratch.Value! : new byte[byteLen];
                if (full.Length < byteLen) full = new byte[byteLen];
                Encoding.UTF8.GetBytes(s, full);
                new Span<byte>(full, 0, copyLen).CopyTo(new Span<byte>(buf, copyLen));
            }
            buf[copyLen] = 0;
        }
        return byteLen;
    }

    // Per-thread reusable encode buffer — keeps the common "id <= 256 bytes" path
    // allocation-free across calls without tripping AOT's no-thread-static-types rule.
    private static readonly System.Threading.ThreadLocal<byte[]> s_scratch =
        new(() => new byte[256]);
}
