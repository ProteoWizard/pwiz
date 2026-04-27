using System.Globalization;
using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Waters;

/// <summary>
/// Owns the four MassLynx native readers a single <c>.raw</c> directory needs (info, scan,
/// chromatogram, drift) plus the parsed function index and per-function TIC arrays. Mirrors
/// pwiz C++ <c>RawData</c> in <c>WatersRawFile.hpp</c>.
/// </summary>
/// <remarks>
/// Per-function TIC arrays are eagerly cached on construction because pwiz C++ also fills them
/// up front (<c>ChromatogramReader.ReadTICChromatogram(itr.first, ...)</c>) and downstream code
/// (chromatogramList TIC, MSe heuristic) reads them several times.
/// </remarks>
internal sealed class WatersRawFile : IDisposable
{
    private IntPtr _info;
    private IntPtr _scan;
    private IntPtr _chrom;
    private IntPtr _drift; // SCAN reader bound to the same file but used for drift reads (only meaningful when IMS present)
    private IntPtr _centroidProc; // SCAN-type processor used for vendor centroid (Load + Centroid + GetScan).
    private NativeMethods.ProgressCallback? _centroidProgressCb;
    private IntPtr _ddaProc;       // DDA-type processor (lazily created on first DDA request).
    private NativeMethods.ProgressCallback? _ddaProgressCb;
    private bool _disposed;

    /// <summary>0-based function indices present in the raw directory (sorted ascending).</summary>
    public IReadOnlyList<int> FunctionIndices { get; }

    /// <summary>Per-function-index → TIC times (minutes). Indexed by raw function index, may have gaps for missing functions.</summary>
    public IReadOnlyList<float[]> TimesByFunctionIndex { get; }

    /// <summary>Per-function-index → TIC intensities. Same indexing as <see cref="TimesByFunctionIndex"/>.</summary>
    public IReadOnlyList<float[]> TicByFunctionIndex { get; }

    /// <summary>True if any function has a sibling _FUNCnnn.cdt file (ion mobility data).</summary>
    public bool HasIonMobility { get; }

    /// <summary>Per-function-index → has ion mobility (.cdt sibling and DriftScanCount &gt; 0).</summary>
    public IReadOnlyList<bool> IonMobilityByFunctionIndex { get; }

    /// <summary>Properties parsed out of <c>_HEADER.TXT</c> (e.g. "Acquired Date").</summary>
    public IReadOnlyDictionary<string, string> HeaderProps { get; }

    /// <summary>Absolute path to the .raw directory.</summary>
    public string RawPath { get; }

    public WatersRawFile(string rawPath)
    {
        RawPath = rawPath ?? throw new ArgumentNullException(nameof(rawPath));

        // Open the SCAN reader first (it's the one that loads the raw); the others are
        // constructed from it via createRawReaderFromReader. Matches the constructor chain in
        // pwiz C++ RawData.
        Check(NativeMethods.createRawReaderFromPath(rawPath, out _scan, NativeMethods.MassLynxBaseType.SCAN), "open SCAN reader");
        try
        {
            Check(NativeMethods.createRawReaderFromReader(_scan, out _info, NativeMethods.MassLynxBaseType.INFO), "open INFO reader");
            Check(NativeMethods.createRawReaderFromReader(_scan, out _chrom, NativeMethods.MassLynxBaseType.CHROM), "open CHROM reader");
            // The drift reader uses the same MassLynxBaseType as SCAN; we keep a separate handle
            // because pwiz C++ holds a CachedCompressedDataCluster *per function* internally.
            // For now share the SCAN handle — Phase 1 doesn't read drift data.
            _drift = IntPtr.Zero;

            // Build the function index by globbing _FUNC*.DAT (matches pwiz C++).
            var (indices, hasCdt) = ScanFunctionFiles(rawPath);
            FunctionIndices = indices;

            // pwiz C++ also requires Info.GetDriftScanCount(function) > 0 in addition to the
            // .cdt sibling for IMS detection — some files have leftover .cdt artifacts but
            // empty drift dimension. Build the per-function flag here.
            int last = indices.Count == 0 ? -1 : indices[indices.Count - 1];
            var imsByFunc = new bool[last + 1];
            bool anyIms = false;
            foreach (int f in indices)
            {
                if (!hasCdt.TryGetValue(f, out bool cdt) || !cdt) continue;
                if (NativeMethods.getDriftScanCount(_info, f, out uint dc) == 0 && dc > 0)
                {
                    imsByFunc[f] = true;
                    anyIms = true;
                }
            }
            IonMobilityByFunctionIndex = imsByFunc;
            HasIonMobility = anyIms;

            // Pre-cache per-function TIC.
            var times = new float[last + 1][];
            var tic = new float[last + 1][];
            foreach (int f in indices)
            {
                ReadTic(f, out var t, out var i);
                times[f] = t;
                tic[f] = i;
            }
            TimesByFunctionIndex = times;
            TicByFunctionIndex = tic;

            HeaderProps = ParseHeaderTxt(rawPath);
        }
        catch
        {
            DisposeNative();
            throw;
        }
    }

    private static (IReadOnlyList<int> indices, IReadOnlyDictionary<int, bool> hasCdt) ScanFunctionFiles(string rawPath)
    {
        // Native MassLynx stores each function's binary frames in _FUNC###.DAT; ion mobility
        // adds a sibling _FUNC###.cdt with the compressed drift bins. We mirror pwiz C++ here:
        // glob *.DAT, parse the function number, and check for the .cdt sibling. Note that for
        // function numbers >= 100 the filename grows to _FUNC0100.DAT (4 digits), so we strip
        // the leading zeros before parsing.
        var indices = new List<int>();
        var cdtMap = new Dictionary<int, bool>();
        if (!Directory.Exists(rawPath)) return (indices, cdtMap);
        foreach (var path in Directory.EnumerateFiles(rawPath, "_FUNC*.DAT"))
        {
            string name = Path.GetFileName(path);
            // _FUNC<digits>.DAT — strip "_FUNC" and ".DAT", parse remaining as int.
            string digits = name.Substring(5, name.Length - 9).TrimStart('0');
            if (digits.Length == 0) digits = "0";
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n <= 0) continue;
            int idx = n - 1;
            indices.Add(idx);
            string cdt = Path.ChangeExtension(path, ".cdt");
            cdtMap[idx] = File.Exists(cdt);
        }
        indices.Sort();
        return (indices, cdtMap);
    }

    private static Dictionary<string, string> ParseHeaderTxt(string rawPath)
    {
        // Lines in _HEADER.TXT look like "$$ Acquired Date: 04-Dec-2009". pwiz C++ keys the map
        // by the part between "$$ " and ": "; the value is everything after ": ". We tolerate
        // missing files (some test fixtures don't ship one) by returning an empty map.
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        string headerPath = Path.Combine(rawPath, "_HEADER.TXT");
        if (!File.Exists(headerPath)) return map;
        foreach (var raw in File.ReadAllLines(headerPath))
        {
            if (!raw.StartsWith("$$ ", StringComparison.Ordinal)) continue;
            int colon = raw.IndexOf(": ", StringComparison.Ordinal);
            if (colon < 0) continue;
            string name = raw.Substring(3, colon - 3);
            string value = raw.Substring(colon + 2);
            map[name] = value;
        }
        return map;
    }

    public string GetHeaderProp(string name) =>
        HeaderProps.TryGetValue(name, out var v) ? v : string.Empty;

    public int GetFunctionType(int function)
    {
        Check(NativeMethods.getFunctionType(_info, function, out int t), "getFunctionType");
        return t;
    }

    public int GetIonMode(int function)
    {
        Check(NativeMethods.getIonMode(_info, function, out int m), "getIonMode");
        return m;
    }

    public string GetFunctionTypeString(int functionType)
    {
        Check(NativeMethods.getFunctionTypeString(_info, functionType, out IntPtr p), "getFunctionTypeString");
        return MarshalAndRelease(p);
    }

    public int GetScanCount(int function)
    {
        Check(NativeMethods.getScanCount(_info, function, out uint c), "getScanCount");
        return (int)c;
    }

    public float GetRetentionTime(int function, int scan)
    {
        Check(NativeMethods.getRetentionTime(_info, function, scan, out float rt), "getRetentionTime");
        return rt;
    }

    public (float Low, float High) GetAcquisitionMassRange(int function)
    {
        Check(NativeMethods.getAcquisitionMassRange(_info, function, 0, out float lo, out float hi), "getAcquisitionMassRange");
        return (lo, hi);
    }

    public bool IsContinuum(int function)
    {
        Check(NativeMethods.isContinuum(_info, function, out bool cont), "isContinuum");
        return cont;
    }

    /// <summary>Number of drift (ion-mobility) bins per RT block in <paramref name="function"/>.</summary>
    public int GetDriftScanCount(int function)
    {
        Check(NativeMethods.getDriftScanCount(_info, function, out uint c), "getDriftScanCount");
        return (int)c;
    }

    /// <summary>Drift time (milliseconds) for the given drift bin index.</summary>
    public float GetDriftTime(int driftBin)
    {
        Check(NativeMethods.getDriftTime(_info, driftBin, out float dt), "getDriftTime");
        return dt;
    }

    /// <summary>
    /// Reads (mz, intensity) for one drift bin within an IMS block. SDK retains buffer
    /// ownership (matches pwiz C++ ToVector with bRelease=false).
    /// </summary>
    public (float[] Mz, float[] Intensity) ReadDriftScan(int function, int block, int driftBin)
    {
        Check(NativeMethods.readDriftScan(_scan, function, block, driftBin,
            out IntPtr pMass, out IntPtr pInt, out int n), "readDriftScan");
        var mz = new float[n];
        var intensity = new float[n];
        if (n > 0)
        {
            Marshal.Copy(pMass, mz, 0, n);
            Marshal.Copy(pInt, intensity, 0, n);
        }
        return (mz, intensity);
    }

    /// <summary>
    /// Reads the (mz, intensity) arrays for the given (function, scan). The SDK retains
    /// ownership of the returned float buffers (matching pwiz C++ <c>ReadScan</c> which calls
    /// <c>ToVector</c> with <c>bRelease=false</c>) — we copy out and do not free.
    /// </summary>
    public (float[] Mz, float[] Intensity) ReadScan(int function, int scan)
    {
        Check(NativeMethods.readScan(_scan, function, scan, out IntPtr pMass, out IntPtr pInt, out int n), "readScan");
        var mz = new float[n];
        var intensity = new float[n];
        if (n > 0)
        {
            Marshal.Copy(pMass, mz, 0, n);
            Marshal.Copy(pInt, intensity, 0, n);
        }
        return (mz, intensity);
    }

    /// <summary>
    /// Reads a single scan and returns vendor-centroided peaks via MassLynx's scan processor
    /// (createRawProcessor(SCAN) → combineScan(start=end) → centroidScan → getScan). Mirrors
    /// pwiz C++ <c>RawData::ReadScan(..., doCentroid=true, ...)</c>. The processor is lazily
    /// constructed on first centroid request.
    /// </summary>
    public (float[] Mz, float[] Intensity) ReadCentroidScan(int function, int scan)
    {
        EnsureCentroidProcessor();
        Check(NativeMethods.combineScan(_centroidProc, function, scan, scan), "combineScan");
        Check(NativeMethods.centroidScan(_centroidProc), "centroidScan");
        Check(NativeMethods.getScan(_centroidProc, out IntPtr pMass, out IntPtr pInt, out int n), "getScan");
        // ScanProcessor's GetScan uses ToVector with bRelease=false (matches RawData::ReadScan
        // pattern) — SDK retains ownership, we just copy.
        var mz = new float[n];
        var intensity = new float[n];
        if (n > 0)
        {
            Marshal.Copy(pMass, mz, 0, n);
            Marshal.Copy(pInt, intensity, 0, n);
        }
        return (mz, intensity);
    }

    private void EnsureCentroidProcessor()
    {
        if (_centroidProc != IntPtr.Zero) return;
        // Pin a no-op progress callback so the SDK doesn't invoke a freed delegate during a
        // long-running operation (createRawProcessor stores the function pointer).
        _centroidProgressCb = static (IntPtr _, in int _) => { };
        Check(NativeMethods.createRawProcessor(out _centroidProc, NativeMethods.MassLynxBaseType.SCAN,
            _centroidProgressCb, IntPtr.Zero), "createRawProcessor(SCAN)");
        Check(NativeMethods.setRawReader(_centroidProc, _scan), "setRawReader(centroid)");
    }

    private void EnsureDdaProcessor()
    {
        if (_ddaProc != IntPtr.Zero) return;
        _ddaProgressCb = static (IntPtr _, in int _) => { };
        Check(NativeMethods.createRawProcessor(out _ddaProc, NativeMethods.MassLynxBaseType.DDA,
            _ddaProgressCb, IntPtr.Zero), "createRawProcessor(DDA)");
        Check(NativeMethods.setRawReader(_ddaProc, _scan), "setRawReader(DDA)");
    }

    /// <summary>Number of DDA-processed scans this file would emit.</summary>
    public int GetDdaScanCount()
    {
        EnsureDdaProcessor();
        Check(NativeMethods.ddaGetScanCount(_ddaProc, out int n), "ddaGetScanCount");
        return n;
    }

    /// <summary>
    /// Returns the metadata for one DDA-processed scan: retention time, source function,
    /// underlying scan range, MS1-vs-MSn flag, and (for MS2) set/precursor mass. Mirrors
    /// pwiz C++ <c>RawData::GetDDAScanInfo</c>.
    /// </summary>
    public DdaScanInfo GetDdaScanInfo(int index)
    {
        EnsureDdaProcessor();
        Check(NativeMethods.createParameters(out IntPtr p), "createParameters");
        try
        {
            int rc = NativeMethods.ddaGetScanInfo(_ddaProc, index, p);
            if (rc != 0) return default;
            float rt = ReadFloatParam(p, WatersDdaIndex.RT);
            int func = (int)ReadFloatParam(p, WatersDdaIndex.Function);
            int startScan = (int)ReadFloatParam(p, WatersDdaIndex.StartScan);
            int endScan = (int)ReadFloatParam(p, WatersDdaIndex.EndScan);
            int scanType = (int)ReadFloatParam(p, WatersDdaIndex.ScanType);
            bool isMs1 = scanType == WatersScanType.Ms1;
            float setMass = isMs1 ? 0f : ReadFloatParam(p, WatersDdaIndex.SetMass);
            float precursorMass = isMs1 ? 0f : ReadFloatParam(p, WatersDdaIndex.PrecursorMass);
            return new DdaScanInfo(rt, func, startScan, endScan, isMs1, setMass, precursorMass);
        }
        finally
        {
            _ = NativeMethods.destroyParameters(p);
        }
    }

    /// <summary>
    /// Reads the (mz, intensity) arrays for a DDA-processed scan. Optionally centroids via
    /// the DDA processor's CENTROID parameter (mirrors pwiz C++ <c>SetCentroid(true)</c>).
    /// </summary>
    public (float[] Mz, float[] Intensity) GetDdaScan(int index, bool doCentroid)
    {
        EnsureDdaProcessor();
        SetDdaCentroid(doCentroid);
        Check(NativeMethods.createParameters(out IntPtr p), "createParameters");
        try
        {
            Check(NativeMethods.ddaGetScan(_ddaProc, index,
                out IntPtr pMass, out IntPtr pInt, out int n, p), "ddaGetScan");
            // SDK retains ownership (matches pwiz C++ ToVector with bRelease=false).
            var mz = new float[n];
            var intensity = new float[n];
            if (n > 0)
            {
                Marshal.Copy(pMass, mz, 0, n);
                Marshal.Copy(pInt, intensity, 0, n);
            }
            return (mz, intensity);
        }
        finally
        {
            _ = NativeMethods.destroyParameters(p);
        }
    }

    /// <summary>
    /// Returns the per-scan isolation window offsets stored in the file, or null if both are
    /// zero (pwiz C++ treats that case as "no offsets present" and falls back to default).
    /// </summary>
    public (float Lower, float Upper)? GetDdaIsolationWindowOffsets()
    {
        EnsureDdaProcessor();
        Check(NativeMethods.createParameters(out IntPtr p), "createParameters");
        try
        {
            int rc = NativeMethods.getQuadIsolationWindowParameters(_ddaProc, p);
            if (rc != 0) return null;
            float lower = ReadFloatParam(p, WatersDdaIsolation.LowerOffset);
            float upper = ReadFloatParam(p, WatersDdaIsolation.UpperOffset);
            if (lower == 0f && upper == 0f) return null;
            return (lower, upper);
        }
        finally
        {
            _ = NativeMethods.destroyParameters(p);
        }
    }

    private void SetDdaCentroid(bool centroid)
    {
        Check(NativeMethods.createParameters(out IntPtr p), "createParameters");
        try
        {
            Check(NativeMethods.setParameterValue(p, WatersDdaParameter.Centroid,
                centroid ? "1" : "0"), "setParameterValue(CENTROID)");
            Check(NativeMethods.setDDAParameters(_ddaProc, p), "setDDAParameters");
        }
        finally
        {
            _ = NativeMethods.destroyParameters(p);
        }
    }

    private static float ReadFloatParam(IntPtr parameters, int key)
    {
        if (NativeMethods.getParameterValue(parameters, key, out IntPtr v) != 0 || v == IntPtr.Zero)
            return 0f;
        string s = Marshal.PtrToStringAnsi(v) ?? string.Empty;
        return float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;
    }

    /// <summary>DDA scan info fields. <c>StartScan</c>/<c>EndScan</c> are 0-based.</summary>
    public readonly record struct DdaScanInfo(
        float RetentionTime, int Function, int StartScan, int EndScan,
        bool IsMs1, float SetMass, float PrecursorMass);

    /// <summary>
    /// SRM/MRM transitions for the given function. Returns parallel (precursor m/z, product
    /// m/z) arrays — one entry per Q1/Q3 transition. Pwiz C++ enumerates these via
    /// <c>Reader.ReadScan(function, 1, precursorMZs, intensities, productMZs)</c> and discards
    /// intensities here; we preserve the same shape.
    /// </summary>
    public (float[] PrecursorMz, float[] ProductMz) ReadMrmTransitions(int function)
    {
        Check(NativeMethods.readProductScan(_scan, function, 1,
            out IntPtr pPrec, out IntPtr pInt, out IntPtr pProd, out int n, out int nProd),
            "readProductScan");
        // SDK retains ownership of these float* buffers (matches pwiz C++ ToVector with
        // bRelease=false), so just copy out.
        var prec = new float[n];
        var prod = new float[nProd];
        if (n > 0) Marshal.Copy(pPrec, prec, 0, n);
        if (nProd > 0) Marshal.Copy(pProd, prod, 0, nProd);
        _ = pInt;
        return (prec, prod);
    }

    /// <summary>
    /// Reads the (time, intensity) chromatogram for a specific MRM transition (0-based offset
    /// within the function's transition list). Caller-owned float buffers — released here.
    /// </summary>
    public (float[] Times, float[] Intensities) ReadMrmChromatogram(int function, int transition)
    {
        var list = new[] { transition };
        Check(NativeMethods.readMRMChromatograms(_chrom, function, list, 1,
            out IntPtr pTimes, out IntPtr pInts, out int n), "readMRMChromatograms");
        try
        {
            var times = new float[n];
            var intens = new float[n];
            if (n > 0)
            {
                Marshal.Copy(pTimes, times, 0, n);
                Marshal.Copy(pInts, intens, 0, n);
            }
            return (times, intens);
        }
        finally
        {
            if (pTimes != IntPtr.Zero) _ = NativeMethods.releaseMemory(pTimes);
            if (pInts != IntPtr.Zero) _ = NativeMethods.releaseMemory(pInts);
        }
    }

    /// <summary>Reads a single scan item via the parameters-handle round-trip.</summary>
    public string GetScanItem(int function, int scan, int item)
    {
        Check(NativeMethods.createParameters(out IntPtr p), "createParameters");
        try
        {
            int rc = NativeMethods.getScanItemValue(_info, function, scan, new[] { item }, 1, p);
            // pwiz C++ catches MassLynxRawException and returns "" — the corresponding behavior
            // for us is to swallow the error and return empty when the call signals failure.
            if (rc != 0) return string.Empty;
            int gv = NativeMethods.getParameterValue(p, item, out IntPtr v);
            if (gv != 0 || v == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringAnsi(v) ?? string.Empty;
        }
        finally
        {
            _ = NativeMethods.destroyParameters(p);
        }
    }

    private void ReadTic(int function, out float[] times, out float[] intensities)
    {
        Check(NativeMethods.readTICChromatogram(_chrom, function, out IntPtr pTimes, out IntPtr pInts, out int n), "readTICChromatogram");
        try
        {
            times = new float[n];
            intensities = new float[n];
            if (n > 0)
            {
                Marshal.Copy(pTimes, times, 0, n);
                Marshal.Copy(pInts, intensities, 0, n);
            }
        }
        finally
        {
            if (pTimes != IntPtr.Zero) _ = NativeMethods.releaseMemory(pTimes);
            if (pInts != IntPtr.Zero) _ = NativeMethods.releaseMemory(pInts);
        }
    }

    private static string MarshalAndRelease(IntPtr ansiPtr)
    {
        // The MassLynx SDK returns char* strings the caller is expected to release via
        // releaseMemory. pwiz C++ does it via MassLynxStringHandler::ToString(..., bRelease=true);
        // we replicate that explicitly here so leaks aren't possible if a caller forgets.
        if (ansiPtr == IntPtr.Zero) return string.Empty;
        try
        {
            return Marshal.PtrToStringAnsi(ansiPtr) ?? string.Empty;
        }
        finally
        {
            _ = NativeMethods.releaseMemory(ansiPtr);
        }
    }

    private static void Check(int code, string op)
    {
        if (code == 0) return;
        // The native side keeps last-error in a per-handle slot, but the pattern in pwiz C++ is
        // simpler: ask for the message via getErrorMessage(code, ...). We replicate that.
        string message = "<error message unavailable>";
        if (NativeMethods.getErrorMessage(code, out IntPtr p) == 0 && p != IntPtr.Zero)
        {
            message = MarshalAndRelease(p);
        }
        throw new InvalidOperationException("MassLynx " + op + " failed (code " + code + "): " + message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeNative();
        GC.SuppressFinalize(this);
    }

    private void DisposeNative()
    {
        if (_ddaProc != IntPtr.Zero) { _ = NativeMethods.destroyRawProcessor(_ddaProc); _ddaProc = IntPtr.Zero; }
        if (_centroidProc != IntPtr.Zero) { _ = NativeMethods.destroyRawProcessor(_centroidProc); _centroidProc = IntPtr.Zero; }
        if (_drift != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_drift); _drift = IntPtr.Zero; }
        if (_chrom != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_chrom); _chrom = IntPtr.Zero; }
        if (_info != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_info); _info = IntPtr.Zero; }
        if (_scan != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_scan); _scan = IntPtr.Zero; }
    }

    ~WatersRawFile() { DisposeNative(); }
}
