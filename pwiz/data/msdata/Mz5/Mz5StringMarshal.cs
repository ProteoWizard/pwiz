using System;
using System.Runtime.InteropServices;

#pragma warning disable CS1591

namespace Pwiz.Data.MsData.Mz5;

/// <summary>
/// String marshalling helpers for the mz5 read path. mz5 strings come in two
/// flavors: HDF5 variable-length (cpp <c>char*</c>) and HDF5 fixed-length
/// arrays (cpp <c>char[N]</c>). After <c>H5D.read</c>, vlen fields hold an
/// <see cref="IntPtr"/> to HDF5-allocated memory (call <c>H5D.vlen_reclaim</c>
/// to free), while fixed-length fields are inline bytes in the record struct.
/// </summary>
public static class Mz5StringMarshal
{
    /// <summary>Convert an HDF5 variable-length string pointer to a managed
    /// string. Returns empty string for null. Caller is responsible for
    /// arranging the eventual <c>vlen_reclaim</c> on the source array.</summary>
    public static string FromVlen(IntPtr value) =>
        value == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringAnsi(value) ?? string.Empty);

    /// <summary>Read an inline fixed-length NUL-terminated string out of a
    /// pinned-to-record byte buffer. Used for CVParam.value /
    /// UserParam.name/value/type. The cpp writer NUL-pads to the field
    /// capacity, so we stop at the first NUL.</summary>
    public static unsafe string FromFixed(byte* fieldBase, int maxLen)
    {
        int end = 0;
        while (end < maxLen && fieldBase[end] != 0) end++;
        return end == 0 ? string.Empty : System.Text.Encoding.ASCII.GetString(fieldBase, end);
    }
}
