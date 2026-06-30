// P/Invoke surface for MascotShim — the native C wrapper around Matrix
// Science's msparser library that lives in
// pwiz-sharp/Tools/BiblioSpec/native/MascotShim/. See MascotShim.h for the C
// contract; this file mirrors it one-to-one. Built only when the project
// property <MascotSupport> is true (the default).

using System.Runtime.InteropServices;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Status codes returned by every <see cref="MascotShimInterop"/> entry point.
/// Mirrors the <c>MASCOT_OK</c> / <c>MASCOT_ERR_*</c> macros in
/// <c>MascotShim.h</c>.
/// </summary>
internal enum MascotResult
{
    Ok = 0,
    InvalidHandle = -1,
    NullBuffer = -2,
    NotEnoughSpace = -3,
    SdkException = -4,
    NoData = -5,
    NotImplemented = -6,
}

/// <summary>
/// One PSM row returned by the shim's iterator. Layout MUST match
/// <c>mascot_psm_record_t</c> in <c>MascotShim.h</c>: same field order, same
/// fixed buffer sizes. Strings live in inline UTF-8 buffers so a single
/// P/Invoke call delivers everything — no per-field roundtrips.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct MascotPsmRecord
{
    public int QueryId;
    public int Rank;
    public int Charge;
    public double IonsScore;
    public double ExpectationValue;
    public double ObservedMz;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] Peptide;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] ReadableMods;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] VarModsStr;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] PrevAa;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] NextAa;

    /// <summary>Quantitation component label (e.g. "heavy" / "light"), or
    /// empty when the .dat declares no labeling.</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] ComponentStr;
}

/// <summary>
/// One per-residue isotope mass difference within a quantitation component.
/// Layout matches <c>mascot_isotope_diff_t</c>: a single ASCII residue byte
/// (with 7 bytes of padding so the struct is 16-byte aligned for the
/// trailing double) and the delta mass.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct MascotIsotopeDiff
{
    public byte Residue;        // 'A'..'Z'
    // Pad to align the double on an 8-byte boundary (matches the natural
    // C-side struct padding under MSVC's default layout rules).
    private readonly byte _pad1;
    private readonly byte _pad2;
    private readonly byte _pad3;
    private readonly byte _pad4;
    private readonly byte _pad5;
    private readonly byte _pad6;
    private readonly byte _pad7;
    public double Delta;
}

/// <summary>
/// One fixed or variable modification row enumerated from the .dat search
/// parameters. Layout matches <c>mascot_mod_t</c> in <c>MascotShim.h</c>:
/// 128-byte UTF-8 name buffer, 64-byte residue-spec buffer (empty for
/// variable mods — the residue position lives in the PSM's
/// <c>VarModsStr</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct MascotMod
{
    public double Delta;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
    public byte[] Name;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] Residues;
}

/// <summary>
/// Callback signature for <see cref="MascotShimInterop.EnumerateDistillerRawFiles"/>.
/// The native side guarantees the pointer is valid only for the duration of
/// the call — copy out via <see cref="Marshal.PtrToStringUTF8(IntPtr)"/>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void MascotStringCallback(IntPtr utf8String, IntPtr userdata);

/// <summary>
/// Flat P/Invoke surface for <c>MascotShim.dll</c> / <c>libMascotShim.so</c>.
/// One-to-one with <c>MascotShim.h</c>. Every call follows either
/// &quot;return value out via int*/double*&quot; or the two-call array pattern
/// (call once with <c>null</c>/0 to query the required size, again with the
/// allocated buffer). On failure, a negative <see cref="MascotResult"/> is
/// returned and a thread-local message is stashed; retrieve it via
/// <see cref="LastError"/>.
/// </summary>
internal static class MascotShimInterop
{
    private const string DllName = "MascotShim";

    /* ---- Diagnostics ---------------------------------------------------- */
    [DllImport(DllName, EntryPoint = "mascot_get_version",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetVersion(out int major, out int minor, out int patch);

    [DllImport(DllName, EntryPoint = "mascot_last_error",
               CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr LastErrorPtr();

    /// <summary>Reads the per-thread error string stashed by the most recent
    /// failed shim call. Empty when the most recent call succeeded.</summary>
    public static string LastError()
    {
        IntPtr p = LastErrorPtr();
        return p == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringUTF8(p) ?? string.Empty);
    }

    /* ---- Lifecycle (Phase 2) ------------------------------------------- */
    [DllImport(DllName, EntryPoint = "mascot_dat_open",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int Open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string utf8Path,
        double scoreCutoff,
        out IntPtr outHandle);

    [DllImport(DllName, EntryPoint = "mascot_dat_close",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern void Close(IntPtr handle);

    /* ---- Metadata (Phase 2) -------------------------------------------- */
    [DllImport(DllName, EntryPoint = "mascot_dat_is_msms",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsMsMs(IntPtr handle, out int outIsMsMs);

    [DllImport(DllName, EntryPoint = "mascot_dat_num_queries",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int NumQueries(IntPtr handle, out int outCount);

    /* ---- Modifications (Phase 4) --------------------------------------- */
    [DllImport(DllName, EntryPoint = "mascot_dat_num_fixed_mods",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int NumFixedMods(IntPtr handle, out int outCount);

    [DllImport(DllName, EntryPoint = "mascot_dat_get_fixed_mod",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetFixedMod(IntPtr handle, int index1Based, out MascotMod outMod);

    [DllImport(DllName, EntryPoint = "mascot_dat_num_var_mods",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int NumVarMods(IntPtr handle, out int outCount);

    [DllImport(DllName, EntryPoint = "mascot_dat_get_var_mod",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetVarMod(IntPtr handle, int index1Based, out MascotMod outMod);

    /* ---- Quantitation / isotope labels (Phase 4c) ---------------------- */
    [DllImport(DllName, EntryPoint = "mascot_dat_set_quant_config_dir",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int SetQuantConfigDir(
        IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string utf8Dir);

    [DllImport(DllName, EntryPoint = "mascot_dat_get_quant_name",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetQuantName(
        IntPtr handle, [Out] byte[]? outBuf, int outBufSize, out int outRequired);

    [DllImport(DllName, EntryPoint = "mascot_dat_num_quant_components",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int NumQuantComponents(IntPtr handle, out int outCount);

    [DllImport(DllName, EntryPoint = "mascot_dat_get_quant_component_name",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetQuantComponentName(
        IntPtr handle, int componentIndex,
        [Out] byte[]? outBuf, int outBufSize, out int outRequired);

    [DllImport(DllName, EntryPoint = "mascot_dat_get_quant_component_diffs",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetQuantComponentDiffs(
        IntPtr handle, int componentIndex,
        [Out] MascotIsotopeDiff[]? outBuf, int outBufSize, out int outCount);

    /// <summary>Which global search-params field to fetch via
    /// <see cref="GetGlobalParam"/>. cpp uses these as filename fallbacks.</summary>
    public enum MascotGlobalParam
    {
        Filename = 1,
        DataUrl = 2,
        Com = 3,
    }

    [DllImport(DllName, EntryPoint = "mascot_dat_get_global_param",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetGlobalParam(
        IntPtr handle, int which,
        [Out] byte[]? outBuf, int outBufSize, out int outRequired);

    /* ---- Distiller (Phase 5) ------------------------------------------- */
    [DllImport(DllName, EntryPoint = "mascot_dat_enumerate_distiller_raw_files",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int EnumerateDistillerRawFiles(
        IntPtr handle, MascotStringCallback callback, IntPtr userdata);

    /* ---- PSM iteration (Phase 3) --------------------------------------- */
    [DllImport(DllName, EntryPoint = "mascot_dat_open_psm_iter",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int OpenPsmIter(IntPtr handle, out IntPtr outIter);

    [DllImport(DllName, EntryPoint = "mascot_dat_close_psm_iter",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClosePsmIter(IntPtr iter);

    [DllImport(DllName, EntryPoint = "mascot_dat_next_psm",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int NextPsm(IntPtr iter, out MascotPsmRecord outRecord);

    /* ---- Spectrum lookup (Phase 6) ------------------------------------- */
    /// <summary>
    /// Two-call pattern: pass <c>null</c>/0 to query the required buffer
    /// size (written to <c>outRequired</c>), then call again with the right
    /// buffer. Empty title returns <see cref="MascotResult.NoData"/>.
    /// </summary>
    [DllImport(DllName, EntryPoint = "mascot_dat_get_query_title",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetQueryTitle(
        IntPtr handle, int queryId,
        [Out] byte[]? outBuf, int outBufSize, out int outRequired);

    [DllImport(DllName, EntryPoint = "mascot_dat_get_query_rt",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetQueryRetentionTime(
        IntPtr handle, int queryId, int rawFileIndex, out double outRt);

    [DllImport(DllName, EntryPoint = "mascot_dat_get_query_peak_count",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetQueryPeakCount(IntPtr handle, int queryId, out int outCount);

    /// <summary>
    /// Fills the caller's buffers and returns the peak count (positive int)
    /// on success, or a negative <see cref="MascotResult"/> on failure.
    /// The two arrays must each be at least <c>bufSize</c> long.
    /// </summary>
    [DllImport(DllName, EntryPoint = "mascot_dat_get_query_peaks",
               CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetQueryPeaks(
        IntPtr handle, int queryId,
        [Out] double[] mzBuf, [Out] double[] intensityBuf, int bufSize);

    /* ---- Helpers ------------------------------------------------------- */
    /// <summary>Decodes a fixed-size inline UTF-8 buffer (one of
    /// <see cref="MascotPsmRecord"/>'s string fields) to a managed string,
    /// trimming at the first NUL.</summary>
    public static string DecodeBuffer(byte[]? buf)
    {
        if (buf is null || buf.Length == 0) return string.Empty;
        int len = Array.IndexOf<byte>(buf, 0);
        if (len < 0) len = buf.Length;
        return len == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(buf, 0, len);
    }
}
