using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Mobilion;

/// <summary>
/// P/Invoke surface for <c>MobilionShim.dll</c> — the small native C wrapper around
/// MBISDK that lives in <c>src/Vendor/Mobilion/MobilionShim/</c>. See
/// <c>MobilionShim.h</c> for the C contract; this file mirrors it one-to-one.
/// </summary>
/// <remarks>
/// All functions follow either the "return value out via int*/double*" pattern or the
/// two-call array pattern (call once with NULL/0 to query needed size, again with the
/// allocated buffer). On error, the call returns a non-zero <c>MbiResult</c> and stashes
/// a thread-local message; <see cref="MbiLastErrorMessage"/> retrieves it.
/// </remarks>
internal static class MobilionShimNative
{
    private const string DllName = "MobilionShim";

    /* ---- Result codes ---------------------------------------------------- */
    public const int MBI_OK = 0;
    public const int MBI_ERR_INVALID_HANDLE = -1;
    public const int MBI_ERR_NULL_BUFFER = -2;
    public const int MBI_ERR_NOT_ENOUGH_SPACE = -3;
    public const int MBI_ERR_SDK_EXCEPTION = -4;
    public const int MBI_ERR_NO_DATA = -5;

    /* ---- Diagnostics ----------------------------------------------------- */
    [DllImport(DllName, EntryPoint = "mbi_last_error_message", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mbi_last_error_message();

    /// <summary>Reads the per-thread error string the shim stashed on the most recent
    /// failed call. Empty when the most recent call succeeded.</summary>
    public static string MbiLastErrorMessage()
    {
        IntPtr p = mbi_last_error_message();
        return p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(p) ?? string.Empty;
    }

    /* ---- MBIFile lifecycle ---------------------------------------------- */
    [DllImport(DllName, EntryPoint = "mbi_file_open", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr mbi_file_open([MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport(DllName, EntryPoint = "mbi_file_free", CallingConvention = CallingConvention.Cdecl)]
    public static extern void mbi_file_free(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_file_init", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_file_init(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_file_close", CallingConvention = CallingConvention.Cdecl)]
    public static extern void mbi_file_close(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_file_num_frames", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_file_num_frames(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_file_get_frame", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mbi_file_get_frame(IntPtr handle, int frameIndex1Based);

    /* ---- Global metadata ------------------------------------------------ */
    [DllImport(DllName, EntryPoint = "mbi_file_global_read_string", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mbi_file_global_read_string(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string key,
        [Out] byte[]? outBuf, int outBufSize, out int outRequired);

    [DllImport(DllName, EntryPoint = "mbi_file_global_read_double", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mbi_file_global_read_double(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string key,
        out double outValue);

    /* ---- CCS calibration ------------------------------------------------ */
    [DllImport(DllName, EntryPoint = "mbi_file_can_convert_ccs", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_file_can_convert_ccs(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_file_ccs_min", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_file_ccs_min(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_file_ccs_max", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_file_ccs_max(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_file_arrival_time_to_ccs", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_file_arrival_time_to_ccs(IntPtr handle, double driftTime, double absMzCharge);

    [DllImport(DllName, EntryPoint = "mbi_file_ccs_to_arrival_time", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_file_ccs_to_arrival_time(IntPtr handle, double ccs, double absMzCharge);

    /* ---- Frame ---------------------------------------------------------- */
    [DllImport(DllName, EntryPoint = "mbi_frame_free", CallingConvention = CallingConvention.Cdecl)]
    public static extern void mbi_frame_free(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_frame_get_ce_at", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_frame_get_ce_at(IntPtr handle, long index);

    [DllImport(DllName, EntryPoint = "mbi_frame_collision_energy", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_frame_collision_energy(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_frame_time", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_frame_time(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_frame_total_intensity", CallingConvention = CallingConvention.Cdecl)]
    public static extern long mbi_frame_total_intensity(IntPtr handle);

    [DllImport(DllName, EntryPoint = "mbi_frame_arrival_bin_time_offset", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_frame_arrival_bin_time_offset(IntPtr handle, nuint binIndex);

    [DllImport(DllName, EntryPoint = "mbi_frame_arrival_bin_time_offsets_batch", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_frame_arrival_bin_time_offsets_batch(
        IntPtr handle,
        [In] long[] scanIndices, int count,
        [Out] double[] outDrift);

    [DllImport(DllName, EntryPoint = "mbi_frame_metadata_read_string", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mbi_frame_metadata_read_string(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string key,
        [Out] byte[]? outBuf, int outBufSize, out int outRequired);

    [DllImport(DllName, EntryPoint = "mbi_frame_metadata_read_double", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mbi_frame_metadata_read_double(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string key,
        out double outValue);

    [DllImport(DllName, EntryPoint = "mbi_frame_get_nonzero_scan_indices", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_frame_get_nonzero_scan_indices(
        IntPtr handle,
        [Out] long[]? outBuf, int outBufSize, out int outCount);

    [DllImport(DllName, EntryPoint = "mbi_frame_get_scan_data_mz_sparse", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_frame_get_scan_data_mz_sparse(
        IntPtr handle, nuint scanIndex,
        [Out] double[]? outMz, [Out] long[]? outIntens,
        int outBufSize, out int outCount);

    [DllImport(DllName, EntryPoint = "mbi_frame_get_scan_data_tof_sparse", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_frame_get_scan_data_tof_sparse(
        IntPtr handle, nuint scanIndex,
        [Out] long[]? outTof, [Out] long[]? outIntens,
        int outBufSize, out int outCount);

    [DllImport(DllName, EntryPoint = "mbi_frame_get_coo_array", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_frame_get_coo_array(
        IntPtr handle,
        [Out] long[]? outData, [Out] long[]? outRowScan, [Out] long[]? outColTof,
        int outBufSize, out int outCount);

    [DllImport(DllName, EntryPoint = "mbi_frame_index_to_mz", CallingConvention = CallingConvention.Cdecl)]
    public static extern double mbi_frame_index_to_mz(IntPtr handle, long tofIndex);

    [DllImport(DllName, EntryPoint = "mbi_frame_index_to_mz_batch", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mbi_frame_index_to_mz_batch(
        IntPtr handle,
        [In] long[] tofIndices, int count,
        [Out] double[] outMz);
}
