using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// P/Invoke surface for Bruker's <c>timsdata.dll</c> — the C "mini-API" distributed with the
/// timsTOF SDK. Structure and signatures match <c>timsdata.h</c> from the SDK.
/// </summary>
/// <remarks>
/// Every function returns <c>0</c> on failure; the thread-local error string is fetched via
/// <see cref="tims_get_last_error_string(byte[], uint)"/>. Multi-threaded use is allowed only
/// when each handle is confined to a single thread.
/// </remarks>
internal static class NativeMethods
{
    /// <summary>DLL name. The x64 timsdata.dll ships alongside our managed output.</summary>
    public const string TimsDataDll = "timsdata";

    /// <summary>Open a .d analysis directory. Returns 0 on error.</summary>
    [DllImport(TimsDataDll, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern ulong tims_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string analysis_directory_name,
        uint use_recalibrated_state);

    /// <summary>Open with explicit pressure-compensation strategy.</summary>
    [DllImport(TimsDataDll, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern ulong tims_open_v2(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string analysis_directory_name,
        uint use_recalibrated_state,
        PressureCompensationStrategy pressure_compensation_strategy);

    /// <summary>Close a handle returned by <see cref="tims_open"/>. Passing 0 is a no-op.</summary>
    [DllImport(TimsDataDll)]
    public static extern void tims_close(ulong handle);

    /// <summary>Copy the last error string into <paramref name="buf"/>. Returns required length.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_get_last_error_string(byte[]? buf, uint length);

    /// <summary>Returns 1 if the analysis has a recalibrated state.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_has_recalibrated_state(ulong handle);

    /// <summary>
    /// Read a range of scans from a frame. Output layout: N uint32 peak counts, then N pairs of
    /// (indices[], intensities[]) uint32 arrays. Returns required byte count (0 on error).
    /// </summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_read_scans_v2(
        ulong handle,
        long frame_id,
        uint scan_begin,
        uint scan_end,
        IntPtr buf,
        uint length);

    /// <summary>Sets the number of threads used internally by timsdata.dll (OpenMP).</summary>
    [DllImport(TimsDataDll)]
    public static extern void tims_set_num_threads(uint n);

    /// <summary>Convert (possibly non-integer) indices to m/z for a frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_index_to_mz(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Convert m/z back to (possibly non-integer) indices for a frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_mz_to_index(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Convert scan numbers to 1/K0 values for a frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_scannum_to_oneoverk0(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Convert 1/K0 values to scan numbers for a frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_oneoverk0_to_scannum(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Convert scan numbers to TIMS voltages for a frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_scannum_to_voltage(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Convert TIMS voltages to scan numbers for a frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tims_voltage_to_scannum(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Converts 1/K0 → CCS (Å²) via the Mason-Shamp equation.</summary>
    [DllImport(TimsDataDll)]
    public static extern double tims_oneoverk0_to_ccs_for_mz(double ook0, int charge, double mz);

    /// <summary>Converts CCS (Å²) → 1/K0 via the Mason-Shamp equation.</summary>
    [DllImport(TimsDataDll)]
    public static extern double tims_ccs_to_oneoverk0_for_mz(double ccs, int charge, double mz);

    // ---------- TSF API (timsdata.dll also exports tsf_* entry points) ----------

    /// <summary>Open a .d TSF analysis directory. Returns 0 on error.</summary>
    [DllImport(TimsDataDll, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern ulong tsf_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string analysis_directory_name,
        uint use_recalibrated_state);

    /// <summary>Close a handle returned by <see cref="tsf_open"/>. Passing 0 is a no-op.</summary>
    [DllImport(TimsDataDll)]
    public static extern void tsf_close(ulong handle);

    /// <summary>Copy the last TSF error string into <paramref name="buf"/>. Returns required length.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tsf_get_last_error_string(byte[]? buf, uint length);

    /// <summary>Returns 1 if the TSF analysis has a recalibrated state.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tsf_has_recalibrated_state(ulong handle);

    /// <summary>
    /// Read a centroided line spectrum for a frame. Returns -1 on error, or the required
    /// output length (may be larger than <paramref name="length"/>; caller must re-call with
    /// a grown buffer).
    /// </summary>
    [DllImport(TimsDataDll)]
    public static extern int tsf_read_line_spectrum_v2(
        ulong handle, long spectrum_id,
        [Out] double[] index_array, [Out] float[] intensity_array, int length);

    /// <summary>Read a profile spectrum for a frame. Returns -1 on error or the required length.</summary>
    [DllImport(TimsDataDll)]
    public static extern int tsf_read_profile_spectrum_v2(
        ulong handle, long spectrum_id,
        [Out] uint[] profile_array, int length);

    /// <summary>Convert (possibly non-integer) indices to m/z for a TSF frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tsf_index_to_mz(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Convert m/z back to (possibly non-integer) indices for a TSF frame.</summary>
    [DllImport(TimsDataDll)]
    public static extern uint tsf_mz_to_index(
        ulong handle, long frame_id,
        [In] double[] input, [Out] double[] output, uint count);

    /// <summary>Sets the number of threads used internally by the TSF half of timsdata.dll.</summary>
    [DllImport(TimsDataDll)]
    public static extern void tsf_set_num_threads(uint n);
}

/// <summary>Pressure compensation strategies recognized by <c>tims_open_v2</c>.</summary>
public enum PressureCompensationStrategy : uint
{
    /// <summary>Apply no pressure compensation.</summary>
    None = 0,
    /// <summary>Use a single reference point from the data.</summary>
    AnalysisGlobal = 1,
    /// <summary>Use per-frame pressure; each frame gets its own transformation.</summary>
    PerFrame = 2,
    /// <summary>Deprecated; identical to <see cref="PerFrame"/>.</summary>
    PerFrameWithMissingReference = 3,
}
