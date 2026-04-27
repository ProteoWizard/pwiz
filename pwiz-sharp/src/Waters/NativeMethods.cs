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

    /// <summary>
    /// Returns whether the file has a lockmass-correction function and, if so, which function
    /// index it occupies. Used by <c>ignoreCalibrationScans</c> to skip the lockmass function
    /// when building the spectrum list.
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int getLockMassFunction(IntPtr reader,
        [MarshalAs(UnmanagedType.U1)] out bool hasLockmass, out int whichFunction);

    /// <summary>
    /// SONAR: returns the precursor (quadrupole) m/z range for one SONAR bin within a function.
    /// pwiz C++ uses this to attach scanning_quadrupole_position lower/upper bound userParams
    /// to non-combine SONAR spectra.
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int getIndexPrecursorMassRange(IntPtr reader, int function, int index,
        out float startMass, out float endMass);

    /// <summary>
    /// Returns the collisional cross section in Å² for the given (drift time, neutral mass,
    /// charge) triple. Requires a CCS calibration (mob_cal.csv) — fails otherwise.
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int getCollisionalCrossSection(IntPtr reader, float driftTime,
        float mass, int charge, out float ccs);

    /// <summary>
    /// Inverse of <see cref="getCollisionalCrossSection"/>: returns the drift time predicted
    /// for a (CCS, neutral mass, charge) triple.
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int getDriftTime_CCS(IntPtr reader, float ccs, float mass, int charge,
        out float driftTime);

    // ---------- scan reader ----------

    [DllImport(MassLynxRawDll)]
    public static extern int readScan(IntPtr reader, int function, int scan, out IntPtr masses, out IntPtr intensities, out int size);

    [DllImport(MassLynxRawDll)]
    public static extern int readDriftScan(IntPtr reader, int function, int scan, int drift, out IntPtr masses, out IntPtr intensities, out int size);

    /// <summary>
    /// Reads (precursor m/z, intensity, product m/z) triplets for an SRM/MRM scan. For MRM
    /// functions, scan 1 returns one entry per transition (precursor and product mass arrays
    /// have the same length).
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int readProductScan(IntPtr reader, int function, int scan,
        out IntPtr masses, out IntPtr intensities, out IntPtr productMasses,
        out int size, out int productSize);

    // ---------- chromatogram reader ----------

    [DllImport(MassLynxRawDll)]
    public static extern int readTICChromatogram(IntPtr reader, int function, out IntPtr times, out IntPtr intensities, out int size);

    [DllImport(MassLynxRawDll)]
    public static extern int readBPIChromatogram(IntPtr reader, int function, out IntPtr times, out IntPtr intensities, out int size);

    // ---------- analog reader ----------

    [DllImport(MassLynxRawDll)]
    public static extern int getChannelCount(IntPtr reader, out int count);

    /// <summary>
    /// Reads (time, intensity) for an analog channel. The native API returns const float**
    /// pointers that the SDK retains ownership of (matches pwiz C++ ToVector with
    /// bRelease=false) — copy out and do not free.
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int readChannel(IntPtr reader, int channel,
        out IntPtr times, out IntPtr intensities, out int size);

    /// <summary>
    /// Returns the channel description (e.g. "System Pressure", "A", "ELSD Signal"). Note the
    /// typo in the export name (<c>getChannelDesciption</c>, missing the 'r') — that's the
    /// actual symbol exported by MassLynxRaw.dll.
    /// </summary>
    [DllImport(MassLynxRawDll, EntryPoint = "getChannelDesciption")]
    public static extern int getChannelDescription(IntPtr reader, int channel, out IntPtr description);

    [DllImport(MassLynxRawDll)]
    public static extern int getChannelUnits(IntPtr reader, int channel, out IntPtr units);

    /// <summary>
    /// Reads MRM-transition chromatograms for the requested function. <paramref name="mrmList"/>
    /// is a 0-based list of transition indices (within the function). The output arrays are
    /// concatenated when the list has multiple entries.
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int readMRMChromatograms(IntPtr reader, int function,
        int[] mrmList, int nMRM, out IntPtr times, out IntPtr intensities, out int size);

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

    // ---------- scan / centroid processor ----------

    /// <summary>
    /// Progress callback type. The C ABI is <c>void __stdcall(void* caller, const int&amp; percent)</c>;
    /// `percent` arrives by reference (i.e. as a pointer) for ABI parity with C++ refs. We
    /// don't actually subscribe to progress, so we register a no-op callback at registration
    /// time and keep it pinned for the processor's lifetime.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ProgressCallback(IntPtr caller, in int percent);

    [DllImport(MassLynxRawDll)]
    public static extern int createRawProcessor(out IntPtr processor, MassLynxBaseType type,
        ProgressCallback? callback, IntPtr caller);

    [DllImport(MassLynxRawDll)]
    public static extern int destroyRawProcessor(IntPtr processor);

    [DllImport(MassLynxRawDll)]
    public static extern int setRawReader(IntPtr processor, IntPtr reader);

    /// <summary>
    /// Loads a (possibly merged) scan range into the SCAN-type processor for downstream
    /// centroid / threshold / smooth operations. <see cref="MassLynxBaseType.SCAN"/> processor
    /// only — pass start==end for a single scan.
    /// </summary>
    [DllImport(MassLynxRawDll)]
    public static extern int combineScan(IntPtr processor, int function, int startScan, int endScan);

    [DllImport(MassLynxRawDll)]
    public static extern int centroidScan(IntPtr processor);

    [DllImport(MassLynxRawDll)]
    public static extern int getScan(IntPtr processor, out IntPtr masses, out IntPtr intensities, out int size);

    // ---------- DDA processor ----------

    [DllImport(MassLynxRawDll)]
    public static extern int ddaGetScanCount(IntPtr processor, out int count);

    [DllImport(MassLynxRawDll)]
    public static extern int ddaGetScanInfo(IntPtr processor, int whichScan, IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int ddaGetScan(IntPtr processor, int whichScan,
        out IntPtr masses, out IntPtr intensities, out int size, IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int setDDAParameters(IntPtr processor, IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int setQuadIsolationWindowParameters(IntPtr processor, IntPtr parameters);

    [DllImport(MassLynxRawDll)]
    public static extern int getQuadIsolationWindowParameters(IntPtr processor, IntPtr parameters);
}
