using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Waters;

/// <summary>
/// P/Invoke surface for Waters' <c>MassLynxRaw.dll</c> — the C "mini-API" exported by the
/// MassLynx SDK. Function signatures match the wrappers in <c>MassLynxRawBase.hpp</c>,
/// <c>MassLynxRawInfoReader.hpp</c>, <c>MassLynxRawScanReader.hpp</c>,
/// <c>MassLynxRawChromatogramReader.hpp</c>, and <c>MassLynxParameters.hpp</c>.
/// </summary>
/// <remarks>
/// Functions return an <c>int</c> result code (0 = success). On failure the SDK records the
/// error in a thread-local slot accessed via <see cref="getErrorMessage"/>; arrays returned by
/// out-parameters are owned by the SDK until the caller hands them to <see cref="releaseMemory"/>.
/// We avoid that round-trip in the common case by copying the contents into managed buffers
/// before calling <see cref="releaseMemory"/>.
/// </remarks>
internal static class NativeMethods
{
    /// <summary>DLL name. The x64 MassLynxRaw.dll ships alongside our managed output.</summary>
    public const string MassLynxRawDll = "MassLynxRaw";

    /// <summary>
    /// MassLynxBaseType enum from MassLynxRawDefs.h — selects which kind of reader is created
    /// by <see cref="createRawReaderFromPath"/>.
    /// </summary>
    public enum MassLynxBaseType
    {
        SCAN = 1,
        INFO = 2,
        CHROM = 3,
        ANALOG = 4,
        LOCKMASS = 5,
        CENTROID = 6,
        DDA = 7,
        MSE = 8,
    }

    // ---------- base reader lifecycle ----------

    [DllImport(MassLynxRawDll, CharSet = CharSet.Ansi, BestFitMapping = false)]
    public static extern int createRawReaderFromPath(
        [MarshalAs(UnmanagedType.LPStr)] string path, out IntPtr reader, MassLynxBaseType type);

    [DllImport(MassLynxRawDll)]
    public static extern int createRawReaderFromReader(IntPtr source, out IntPtr reader, MassLynxBaseType type);

    [DllImport(MassLynxRawDll)]
    public static extern int destroyRawReader(IntPtr reader);

    [DllImport(MassLynxRawDll)]
    public static extern int updateRawReader(IntPtr reader);

    [DllImport(MassLynxRawDll)]
    public static extern int getErrorMessage(int code, out IntPtr message);

    [DllImport(MassLynxRawDll)]
    public static extern int releaseMemory(IntPtr block);

    [DllImport(MassLynxRawDll)]
    public static extern int getVersionInfo(out IntPtr version);

    // ---------- info reader ----------

    [DllImport(MassLynxRawDll)]
    public static extern int getFunctionCount(IntPtr reader, out uint count);

    [DllImport(MassLynxRawDll)]
    public static extern int getFunctionType(IntPtr reader, int function, out int type);

    [DllImport(MassLynxRawDll)]
    public static extern int getFunctionTypeString(IntPtr reader, int functionType, out IntPtr str);

    [DllImport(MassLynxRawDll)]
    public static extern int getIonMode(IntPtr reader, int function, out int ionMode);

    [DllImport(MassLynxRawDll)]
    public static extern int getIonModeString(IntPtr reader, int ionMode, out IntPtr str);

    [DllImport(MassLynxRawDll)]
    public static extern int getRetentionTime(IntPtr reader, int function, int scan, out float rt);

    [DllImport(MassLynxRawDll)]
    public static extern int getDriftTime(IntPtr reader, int drift, out float dt);

    [DllImport(MassLynxRawDll)]
    public static extern int getAcquisitionMassRange(IntPtr reader, int function, int mrm, out float startMass, out float endMass);

    [DllImport(MassLynxRawDll)]
    public static extern int getScanCount(IntPtr reader, int function, out uint count);

    [DllImport(MassLynxRawDll)]
    public static extern int getDriftScanCount(IntPtr reader, int function, out uint count);

    [DllImport(MassLynxRawDll)]
    public static extern int isContinuum(IntPtr reader, int function, [MarshalAs(UnmanagedType.U1)] out bool continuum);

    [DllImport(MassLynxRawDll)]
    public static extern int getScanItemsInFunction(IntPtr reader, int function, IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int getScanItemValue(IntPtr reader, int function, int scan, int[] items, int itemCount, IntPtr parameters);

    // ---------- scan reader ----------

    [DllImport(MassLynxRawDll)]
    public static extern int readScan(IntPtr reader, int function, int scan, out IntPtr masses, out IntPtr intensities, out int size);

    [DllImport(MassLynxRawDll)]
    public static extern int readDriftScan(IntPtr reader, int function, int scan, int drift, out IntPtr masses, out IntPtr intensities, out int size);

    // ---------- chromatogram reader ----------

    [DllImport(MassLynxRawDll)]
    public static extern int readTICChromatogram(IntPtr reader, int function, out IntPtr times, out IntPtr intensities, out int size);

    [DllImport(MassLynxRawDll)]
    public static extern int readBPIChromatogram(IntPtr reader, int function, out IntPtr times, out IntPtr intensities, out int size);

    // ---------- parameters ----------

    [DllImport(MassLynxRawDll)]
    public static extern int createParameters(out IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int createParametersFromParameters(IntPtr source, out IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int destroyParameters(IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int getParameterValue(IntPtr parameters, int key, out IntPtr value);

    [DllImport(MassLynxRawDll)]
    public static extern int setParameterValue(IntPtr parameters, int key,
        [MarshalAs(UnmanagedType.LPStr)] string value);

    [DllImport(MassLynxRawDll)]
    public static extern int getParameterKeys(IntPtr parameters, out IntPtr keys, out int size);
}
