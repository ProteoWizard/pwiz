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
    private IntPtr _analog;        // ANALOG-type reader (lazy: not all .raw have analog channels).
    private IntPtr _centroidProc; // SCAN-type processor used for vendor centroid (Load + Centroid + GetScan).
    private NativeMethods.ProgressCallback? _centroidProgressCb;
    private IntPtr _ddaProc;       // DDA-type processor (lazily created on first DDA request).
    private NativeMethods.ProgressCallback? _ddaProgressCb;
    private IntPtr _lockMassProc;  // LOCKMASS-type processor (lazy, only when ApplyLockMass is called).
    private NativeMethods.ProgressCallback? _lockMassProgressCb;
    private float _appliedLockMassMz;
    private float _appliedLockMassTolerance;
    private int _sonarFunctionIndex = -1;          // Lazy-resolved by FindSonarFunction.
    private float _sonarMassLowerLimit;            // Cached at SONAR-function discovery.
    private float _sonarMassUpperLimit;            // Cached at SONAR-function discovery.
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

    /// <summary>True if any function has SONAR enabled (scanning-quadrupole binning).</summary>
    public bool HasSonar { get; }

    /// <summary>Per-function-index → SONAR enabled (only IMS functions can be SONAR).</summary>
    public IReadOnlyList<bool> SonarEnabledByFunctionIndex { get; }

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
            var sonarByFunc = new bool[last + 1];
            bool anyIms = false;
            bool anySonar = false;
            foreach (int f in indices)
            {
                if (!hasCdt.TryGetValue(f, out bool cdt) || !cdt) continue;
                if (NativeMethods.getDriftScanCount(_info, f, out uint dc) == 0 && dc > 0)
                {
                    imsByFunc[f] = true;
                    anyIms = true;
                    // SONAR detection: only IMS functions can be SONAR. The flag lives in the
                    // SONAR_ENABLED scan item on the first scan (matches pwiz C++ RawData).
                    string sonar = GetScanItemImpl(f, 0, WatersScanItem.SonarEnabled);
                    if (sonar.Length > 0
                        && (string.Equals(sonar, "1", StringComparison.Ordinal)
                            || string.Equals(sonar, "true", StringComparison.OrdinalIgnoreCase)))
                    {
                        sonarByFunc[f] = true;
                        anySonar = true;
                    }
                }
            }
            IonMobilityByFunctionIndex = imsByFunc;
            HasIonMobility = anyIms;
            SonarEnabledByFunctionIndex = sonarByFunc;
            HasSonar = anySonar;

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

    /// <summary>
    /// Number of analog channels in this raw file (pressure, column temperature, ELSD, etc.).
    /// Lazily opens the ANALOG-type reader on first access.
    /// </summary>
    public int GetAnalogChannelCount()
    {
        EnsureAnalogReader();
        if (_analog == IntPtr.Zero) return 0;
        Check(NativeMethods.getChannelCount(_analog, out int n), "getChannelCount");
        return n;
    }

    /// <summary>Channel description (e.g. "System Pressure"). Encoded in CP1252 by the SDK.</summary>
    public string GetAnalogChannelDescription(int channel)
    {
        EnsureAnalogReader();
        if (_analog == IntPtr.Zero) return string.Empty;
        Check(NativeMethods.getChannelDescription(_analog, channel, out IntPtr p), "getChannelDescription");
        return Cp1252OrAnsiAndRelease(p);
    }

    /// <summary>Channel unit string (e.g. "psi", "°C", "%", "LSU").</summary>
    public string GetAnalogChannelUnits(int channel)
    {
        EnsureAnalogReader();
        if (_analog == IntPtr.Zero) return string.Empty;
        Check(NativeMethods.getChannelUnits(_analog, channel, out IntPtr p), "getChannelUnits");
        return Cp1252OrAnsiAndRelease(p);
    }

    /// <summary>
    /// Reads the (time, intensity) data for an analog channel. SDK retains buffer ownership
    /// (matches pwiz C++ ToVector with bRelease=false) — copy out, do not free.
    /// </summary>
    public (float[] Times, float[] Intensities) ReadAnalogChannel(int channel)
    {
        EnsureAnalogReader();
        if (_analog == IntPtr.Zero) return (Array.Empty<float>(), Array.Empty<float>());
        // Some channels may be empty; the SDK may signal this by failing readChannel. pwiz C++
        // catches the exception and treats it as "empty channel"; we mirror that.
        int rc = NativeMethods.readChannel(_analog, channel,
            out IntPtr pTimes, out IntPtr pInts, out int n);
        if (rc != 0) return (Array.Empty<float>(), Array.Empty<float>());
        var times = new float[n];
        var intensities = new float[n];
        if (n > 0)
        {
            Marshal.Copy(pTimes, times, 0, n);
            Marshal.Copy(pInts, intensities, 0, n);
        }
        return (times, intensities);
    }

    private void EnsureAnalogReader()
    {
        if (_analog != IntPtr.Zero) return;
        // If the file has no analog data, createRawReaderFromReader fails — swallow the error
        // and leave the handle null (matches pwiz C++ which does this lazily inside RawData).
        if (NativeMethods.createRawReaderFromReader(_scan, out IntPtr h, NativeMethods.MassLynxBaseType.ANALOG) == 0)
            _analog = h;
    }

    private static string Cp1252OrAnsiAndRelease(IntPtr ansiPtr)
    {
        // pwiz C++ converts the SDK strings from CP1252 to UTF-8 before storing. We use
        // PtrToStringAnsi which respects the local ANSI code page; for Latin-1 channel names
        // this matches CP1252 in practice. SDK retains ownership of the string buffer.
        if (ansiPtr == IntPtr.Zero) return string.Empty;
        return Marshal.PtrToStringAnsi(ansiPtr) ?? string.Empty;
    }

    /// <summary>
    /// Returns the lockmass function index, or null if the file has no lockmass function.
    /// Cached after the first lookup since the answer doesn't change for an open file.
    /// </summary>
    public int? GetLockMassFunction()
    {
        if (_lockmassChecked) return _lockmassFunction;
        _lockmassChecked = true;
        if (NativeMethods.getLockMassFunction(_info, out bool has, out int which) == 0 && has)
            _lockmassFunction = which;
        return _lockmassFunction;
    }

    private bool _lockmassChecked;
    private int? _lockmassFunction;

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
    /// Reads the profile (mz, intensity) arrays for the given (function, scan). Mirrors
    /// pwiz C++ <c>RawData::ReadScan(..., doCentroid=false, ...)</c> which routes through
    /// the MassLynx scan processor (combineScan(start=end) → getScan), not the raw scan
    /// reader. The processor path applies any pending lockmass correction and matches the
    /// data the cpp peak picker sees.
    /// </summary>
    public (float[] Mz, float[] Intensity) ReadScan(int function, int scan)
    {
        EnsureCentroidProcessor();
        Check(NativeMethods.combineScan(_centroidProc, function, scan, scan), "combineScan");
        Check(NativeMethods.getScan(_centroidProc, out IntPtr pMass, out IntPtr pInt, out int n), "getScan");
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
    /// Reads the SIM channel m/z list for the given function (typically called with scan=1 to
    /// enumerate the channels). Bypasses the scan processor — pwiz C++ uses the raw scan
    /// reader (<c>readScan</c>) for the channel-list query, which is more reliable than the
    /// processor pipeline for non-MS1 scans.
    /// </summary>
    public float[] ReadSimChannelMzs(int function, int scan = 1)
    {
        Check(NativeMethods.readScan(_scan, function, scan,
            out IntPtr pMass, out IntPtr _, out int n), "readScan");
        var mz = new float[n];
        if (n > 0) Marshal.Copy(pMass, mz, 0, n);
        return mz;
    }

    /// <summary>
    /// Reads the (time, intensity) mass chromatogram for one m/z target. <paramref name="massWindow"/>
    /// is the half-width in Da; <paramref name="products"/>=true reads product-ion chromatograms,
    /// false reads MS1/SIM. Mirrors pwiz C++ <c>ChromatogramReader::ReadMassChromatogram</c>.
    /// </summary>
    public (float[] Times, float[] Intensities) ReadMassChromatogram(int function, float mass,
        float massWindow = 1.0f, bool products = false)
    {
        var masses = new[] { mass };
        Check(NativeMethods.readMassChromatograms(_chrom, function, masses, 1,
            out IntPtr pTimes, out IntPtr pInts, massWindow, products, out int n),
            "readMassChromatograms");
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
    public string GetScanItem(int function, int scan, int item) =>
        GetScanItemImpl(function, scan, item);

    private string GetScanItemImpl(int function, int scan, int item)
    {
        if (NativeMethods.createParameters(out IntPtr p) != 0) return string.Empty;
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

    /// <summary>
    /// Returns the (low, high) precursor m/z range for one SONAR bin. Used to attach
    /// scanning_quadrupole_position userParams or arrays to SONAR spectra.
    /// </summary>
    public (float Low, float High) GetSonarBinPrecursorMassRange(int function, int bin)
    {
        Check(NativeMethods.getIndexPrecursorMassRange(_info, function, bin, out float low, out float high),
            "getIndexPrecursorMassRange");
        return (low, high);
    }

    /// <summary>
    /// Returns the SONAR bin range covering <paramref name="precursorMz"/> ± <paramref name="tolerance"/>.
    /// Returns (-1, -1) when the requested window falls outside the calibrated mass range.
    /// Mirrors pwiz C++ <c>RawData::GetSonarRange</c>.
    /// </summary>
    public (int Start, int End) GetSonarBinRange(double precursorMz, double tolerance)
    {
        FindSonarFunction();
        if (precursorMz - tolerance > _sonarMassUpperLimit || precursorMz + tolerance < _sonarMassLowerLimit)
            return (-1, -1);
        // SDK takes a "tolerance" but treats it as half-window — cpp passes tolerance*2.
        if (NativeMethods.getIndexRange(_info, _sonarFunctionIndex, (float)precursorMz,
            (float)tolerance * 2.0f, out int start, out int end) != 0)
            return (-1, -1);
        return (start, end);
    }

    /// <summary>
    /// Returns the nominal precursor m/z at SONAR bin <paramref name="bin"/>. Returns 0 when
    /// the bin is outside the calibrated range. Used for display + bin-to-mass mapping in
    /// SONAR-aware tools. Mirrors pwiz C++ <c>RawData::SonarBinToPrecursorMz</c>.
    /// </summary>
    public double SonarBinToPrecursorMz(int bin)
    {
        FindSonarFunction();
        if (NativeMethods.getPrecursorMass(_info, _sonarFunctionIndex, bin, out float mz) != 0)
            return 0;
        return mz;
    }

    /// <summary>
    /// Lazy-resolves the first SONAR-calibrated function in the file and caches its overall
    /// precursor mass range. Per the Waters SDK team, function index doesn't matter under
    /// normal operation, so we pick the first one that responds to <c>getPrecursorMass</c>.
    /// </summary>
    private void FindSonarFunction()
    {
        if (_sonarFunctionIndex >= 0) return;
        foreach (int function in FunctionIndices)
        {
            if (NativeMethods.getPrecursorMass(_info, function, 1, out float _) != 0)
                continue;
            _sonarFunctionIndex = function;
            // Cache the overall mass range so GetSonarBinRange can short-circuit out-of-range
            // queries before calling the SDK.
            Check(NativeMethods.getFunctionPrecursorMassRange(_info, function,
                out _sonarMassLowerLimit, out _sonarMassUpperLimit),
                "getFunctionPrecursorMassRange");
            return;
        }
        throw new InvalidOperationException(
            "[WatersRawFile.FindSonarFunction] could not identify any function index for SONAR " +
            "mz-to-bin conversion (_sonar.inf calibration file missing or corrupt?)");
    }

    /// <summary>
    /// True if this file ships with a CCS calibration (<c>mob_cal.csv</c>). SONAR files use
    /// IMS hardware but don't have a CCS calibration even if the file is present, so we gate
    /// on <see cref="HasSonar"/> too — matches pwiz C++ <c>RawData::HasCcsCalibration</c>.
    /// </summary>
    public bool HasCcsCalibration =>
        !HasSonar && File.Exists(Path.Combine(RawPath, "mob_cal.csv"));

    /// <summary>
    /// Converts a drift time (ms) + neutral mass (Da) + charge to a collisional cross
    /// section (Å²). Requires <see cref="HasCcsCalibration"/>; throws otherwise.
    /// </summary>
    public float DriftTimeToCcs(float driftTime, float mass, int charge)
    {
        Check(NativeMethods.getCollisionalCrossSection(_info, driftTime, mass, charge, out float ccs),
            "getCollisionalCrossSection");
        return ccs;
    }

    /// <summary>
    /// Inverse of <see cref="DriftTimeToCcs"/> — given a CCS + neutral mass + charge,
    /// returns the predicted drift time (ms).
    /// </summary>
    public float CcsToDriftTime(float ccs, float mass, int charge)
    {
        Check(NativeMethods.getDriftTime_CCS(_info, ccs, mass, charge, out float dt),
            "getDriftTime_CCS");
        return dt;
    }

    // ---------- lockmass correction ----------

    /// <summary>
    /// True if this file CAN have lockmass correction applied (the lockmass function exists
    /// and is acquired). Doesn't apply correction; that's <see cref="ApplyLockMass"/>.
    /// </summary>
    public bool LockMassCanBeApplied()
    {
        if (NativeMethods.canLockMassCorrect(_info, out bool canApply) != 0) return false;
        return canApply;
    }

    /// <summary>True if lockmass correction is currently applied (post-acquisition).</summary>
    public bool LockMassIsApplied()
    {
        if (!LockMassCanBeApplied()) return false;
        return NativeMethods.isLockMassCorrected(_info, out bool applied) == 0 && applied;
    }

    /// <summary>
    /// Applies lockmass correction at <paramref name="mz"/> (Da) with the given tolerance.
    /// No-op if correction is already applied at the same parameters. Returns true if the
    /// correction is now active (or was already active at the requested params).
    /// </summary>
    public bool ApplyLockMass(double mz, double tolerance)
    {
        const float MzEpsilon = 1e-5f;
        const float ToleranceEpsilon = 1e-5f;
        float newMz = (float)mz;
        float newTolerance = (float)tolerance;

        if (LockMassIsApplied()
            && Math.Abs(newMz - _appliedLockMassMz) < MzEpsilon
            && Math.Abs(newTolerance - _appliedLockMassTolerance) < ToleranceEpsilon)
            return true; // unchanged

        EnsureLockMassProcessor();
        if (_lockMassProc == IntPtr.Zero) return false;

        // Build a parameters bag with MASS + TOLERANCE keys, then trigger the correction.
        Check(NativeMethods.createParameters(out IntPtr p), "createParameters");
        try
        {
            Check(NativeMethods.setParameterValue(p, WatersLockMassParameter.Mass,
                newMz.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                "setParameterValue(MASS)");
            Check(NativeMethods.setParameterValue(p, WatersLockMassParameter.Tolerance,
                newTolerance.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                "setParameterValue(TOLERANCE)");
            Check(NativeMethods.setLockMassParameters(_lockMassProc, p), "setLockMassParameters");
            Check(NativeMethods.lockMassCorrect(_lockMassProc, out bool _), "lockMassCorrect");
        }
        finally
        {
            _ = NativeMethods.destroyParameters(p);
        }
        _appliedLockMassMz = newMz;
        _appliedLockMassTolerance = newTolerance;
        return true;
    }

    /// <summary>
    /// Removes any active lockmass correction. Cheap when no correction is active. The native
    /// call is expensive when correction WAS applied (re-reads spectra), so we gate on
    /// <see cref="LockMassIsApplied"/>.
    /// </summary>
    public void RemoveLockMass()
    {
        if (!LockMassIsApplied()) return;
        // Lockmass state can be persisted to disk (lmgt.inf) by a prior LockMassCorrect call,
        // so IsLockMassApplied may be true on first open even though we've never created a
        // processor handle. Ensure one exists before issuing the clear.
        EnsureLockMassProcessor();
        if (_lockMassProc == IntPtr.Zero) return;
        Check(NativeMethods.removeLockMassCorrection(_lockMassProc), "removeLockMassCorrection");
        _appliedLockMassMz = 0;
        _appliedLockMassTolerance = 0;
    }

    /// <summary>
    /// Returns the lockmass-corrected m/z for an arbitrary m/z value at the given scan time.
    /// When correction is active the SDK provides a per-RT gain factor that scales m/z values.
    /// When inactive returns the input unchanged.
    /// </summary>
    public double GetLockMassCorrectedMz(double scanTimeMin, double uncorrectedMz)
    {
        if (!LockMassIsApplied() || _lockMassProc == IntPtr.Zero) return uncorrectedMz;
        if (NativeMethods.getLockMassCorrection(_lockMassProc, (float)scanTimeMin, out float gain) != 0)
            return uncorrectedMz;
        return uncorrectedMz * gain;
    }

    private void EnsureLockMassProcessor()
    {
        if (_lockMassProc != IntPtr.Zero) return;
        _lockMassProgressCb = static (IntPtr _, in int _) => { };
        if (NativeMethods.createRawProcessor(out IntPtr h, NativeMethods.MassLynxBaseType.LOCKMASS,
            _lockMassProgressCb, IntPtr.Zero) != 0) return;
        _lockMassProc = h;
        // Bind the lockmass processor to the same SCAN reader pwiz C++ uses.
        Check(NativeMethods.setRawReader(_lockMassProc, _scan), "setRawReader(lockmass)");
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
        if (_lockMassProc != IntPtr.Zero) { _ = NativeMethods.destroyRawProcessor(_lockMassProc); _lockMassProc = IntPtr.Zero; }
        if (_ddaProc != IntPtr.Zero) { _ = NativeMethods.destroyRawProcessor(_ddaProc); _ddaProc = IntPtr.Zero; }
        if (_centroidProc != IntPtr.Zero) { _ = NativeMethods.destroyRawProcessor(_centroidProc); _centroidProc = IntPtr.Zero; }
        if (_analog != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_analog); _analog = IntPtr.Zero; }
        if (_drift != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_drift); _drift = IntPtr.Zero; }
        if (_chrom != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_chrom); _chrom = IntPtr.Zero; }
        if (_info != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_info); _info = IntPtr.Zero; }
        if (_scan != IntPtr.Zero) { _ = NativeMethods.destroyRawReader(_scan); _scan = IntPtr.Zero; }
    }

    ~WatersRawFile() { DisposeNative(); }
}
