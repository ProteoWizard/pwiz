using System.Globalization;
using Agilent.MassSpectrometry.DataAnalysis;
using Agilent.MassSpectrometry.MIDAC;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Agilent;

/// <summary>
/// Thin managed wrapper around Agilent's <see cref="IMsdrDataReader"/>. C# equivalent of pwiz
/// C++ <c>MassHunterDataImpl</c> in <c>MassHunterData.cpp</c>; the Agilent SDK is already a
/// .NET assembly so the wrapping is much thinner than for SDKs reached via P/Invoke.
/// </summary>
/// <remarks>
/// Scope (initial port): non-IMS MS spectra only — Reader_Agilent / SpectrumList_Agilent expose
/// the scan records and per-row peak data that mzML conversion needs. IMS frames (MIDAC),
/// MRM/SIM transition chromatograms, and non-MS UV/DAD spectra are not yet ported.
/// </remarks>
public sealed class AgilentRawData : IDisposable
{
    // The SDK exposes its API via IMsdrDataReader (the concrete MassSpecDataReader uses
    // explicit interface implementations for most methods). Holding the interface lets us call
    // OpenDataFile / GetSpectrum / etc. without casts.
    private readonly IMsdrDataReader _reader;
    private bool _disposed;

    /// <summary>Path to the .d directory.</summary>
    public string Path { get; }

    /// <summary>Underlying SDK handle. Avoid using outside the Agilent vendor module.</summary>
    public IMsdrDataReader Reader => _reader;

    /// <summary>File-level info, populated lazily.</summary>
    public IBDAFileInformation FileInformation => _reader.FileInformation;

    /// <summary>Per-MS-scan-collection info: types, ranges, polarity, etc.</summary>
    public IBDAMSScanFileInformation MSScanFileInformation => _reader.MSScanFileInformation;

    /// <summary>Number of scan records (mass spectra) in the file.</summary>
    public long TotalScansPresent => MSScanFileInformation.TotalScansPresent;

    /// <summary>True if the file has any MS profile data — checks for AcqData/MSProfile.bin
    /// directly, mirroring cpp <c>MassHunterDataImpl</c>'s <c>hasProfileData_</c>. Necessary
    /// because <c>MSScanFileInformation.SpectraFormat</c> flips to <c>Mixed</c> after a DAD
    /// chromatogram fetch (the SDK reports any device's profile data as profile content),
    /// which would otherwise fool the MS-vs-DAD branch into requesting MSProfile.bin from a
    /// file that doesn't have it.</summary>
    public bool HasProfileData => File.Exists(System.IO.Path.Combine(Path, "AcqData", "MSProfile.bin"));

    /// <summary>Top-level instrument family / device type from the file (Q-TOF / TQ / etc.).</summary>
    public DeviceType DeviceType => MSScanFileInformation.DeviceType;

    private IMidacImsReader? _imsReader;
    private bool? _hasImsData;

    /// <summary>True when the .d directory contains Agilent ion-mobility data (IMSFrame.bin
    /// etc.). Mirrors cpp <c>MassHunterData::hasIonMobilityData</c>.</summary>
    public bool HasIonMobilityData
    {
        get
        {
            if (_hasImsData is bool v) return v;
            try { v = MidacFileAccess.FileHasImsData(Path); }
            catch { v = false; }
            _hasImsData = v;
            return v;
        }
    }

    /// <summary>MIDAC reader for IM data (lazy). Returns null when the file isn't IM. The
    /// reader is owned by this <see cref="AgilentRawData"/> and disposed in <see cref="Dispose"/>.</summary>
    public IMidacImsReader? ImsReader
    {
        get
        {
            if (_imsReader is not null) return _imsReader;
            if (!HasIonMobilityData) return null;
            try { _imsReader = MidacFileAccess.ImsDataReader(Path); }
            catch { _imsReader = null; }
            return _imsReader;
        }
    }

    private int[]? _imsFrameNumbersCache;

    /// <summary>1-based frame numbers for all IM frames in the file (in acquisition order).
    /// Mirrors cpp <c>imsReader_-&gt;FilteredFrameNumbers(nullptr)</c> — passing null means
    /// "no filter, all frames".</summary>
    public int[] ImsFrameNumbers
    {
        get
        {
            if (_imsFrameNumbersCache is not null) return _imsFrameNumbersCache;
            var reader = ImsReader;
            if (reader is null) { _imsFrameNumbersCache = Array.Empty<int>(); return _imsFrameNumbersCache; }
            try { _imsFrameNumbersCache = reader.FilteredFrameNumbers((IMidacMsFiltersSpec?)null) ?? Array.Empty<int>(); }
            catch { _imsFrameNumbersCache = Array.Empty<int>(); }
            return _imsFrameNumbersCache;
        }
    }

    /// <summary>Number of IM frames in the file. 0 when not an IM file.</summary>
    public int ImsFrameCount => ImsFrameNumbers.Length;

    /// <summary>1-based frame number for the i-th IM frame (0-based <paramref name="i"/>).
    /// Returns the value MIDAC expects for <c>FrameInfo</c> / <c>FrameMs</c> calls.</summary>
    public int ImsFrameNumber(int i)
    {
        var nums = ImsFrameNumbers;
        return i >= 0 && i < nums.Length ? nums[i] : 0;
    }

    /// <summary>Friendly device name reported by the SDK (e.g. <c>"TandemQuadrupole"</c>).
    /// Mirrors cpp <c>MassHunterDataImpl::getDeviceName</c>.</summary>
    public string GetDeviceName(DeviceType deviceType)
    {
        try { return FileInformation.GetDeviceName(deviceType) ?? string.Empty; }
        catch { return string.Empty; }
    }

    private List<AgilentDeviceInfo>? _devicesCache;

    /// <summary>Devices listed in <c>AcqData/Devices.xml</c>. cpp <c>XmlMetadataParser</c>
    /// parses the same file to populate <c>devices_</c> for serial number lookup; the SDK
    /// itself doesn't expose serial numbers.</summary>
    public IReadOnlyList<AgilentDeviceInfo> Devices
    {
        get
        {
            if (_devicesCache is not null) return _devicesCache;
            _devicesCache = new List<AgilentDeviceInfo>();
            try
            {
                var devicesXmlPath = System.IO.Path.Combine(Path, "AcqData", "Devices.xml");
                if (!File.Exists(devicesXmlPath)) return _devicesCache;
                var doc = System.Xml.Linq.XDocument.Load(devicesXmlPath);
                foreach (var dev in doc.Descendants("Device"))
                {
                    _devicesCache.Add(new AgilentDeviceInfo(
                        Name: (string?)dev.Element("Name") ?? string.Empty,
                        ModelNumber: (string?)dev.Element("ModelNumber") ?? string.Empty,
                        SerialNumber: ((string?)dev.Element("SerialNumber") ?? string.Empty).Trim(),
                        TypeRaw: (string?)dev.Element("Type") ?? string.Empty));
                }
            }
            catch { /* best-effort */ }
            return _devicesCache;
        }
    }

    /// <summary>Per-device serial number lookup (cpp <c>MassHunterData::getDeviceSerialNumber</c>).
    /// Returns empty when the SDK doesn't report one.</summary>
    public string GetDeviceSerialNumber(DeviceType deviceType)
    {
        foreach (var d in Devices)
            if (int.TryParse(d.TypeRaw, out int t) && t == (int)deviceType)
                return d.SerialNumber;
        return string.Empty;
    }

    /// <summary>Acquisition timestamp (local clock). For IM files, prefers the MIDAC
    /// <c>FileInfo.AcquisitionDate</c> (cpp <c>MidacDataImpl::getAcquisitionTime</c>) since
    /// it reports the timestamp the reference mzMLs were generated against — the MassSpec
    /// SDK's <c>FileInformation.AcquisitionTime</c> for the same .d directory comes back
    /// with a different value.</summary>
    public DateTime AcquisitionTime
    {
        get
        {
            if (HasIonMobilityData)
            {
                var ims = ImsReader;
                if (ims is not null)
                {
                    try
                    {
                        var fi = ims.FileInfo;
                        if (fi is not null) return fi.AcquisitionDate;
                    }
                    catch { /* fall through to MassSpec SDK */ }
                }
            }
            return FileInformation.AcquisitionTime;
        }
    }

    /// <summary>Bitmask of MS scan types present in the file.</summary>
    public MSScanType ScanTypes => MSScanFileInformation.ScanTypes;

    /// <summary>SDK version string.</summary>
    public string Version => _reader.Version ?? string.Empty;

    /// <summary>Opens <paramref name="dotDPath"/> (a .d directory).</summary>
    /// <remarks>
    /// .NET 8 limitation: <c>DataFileMgr.OpenDataFile</c> in <c>BaseDataAccess.dll</c> uses
    /// <see cref="System.Delegate"/>'s <c>BeginInvoke</c> for asynchronous metadata loading, a
    /// pattern .NET Core 5+ removed. On a .NET (Core/5+/8) host the SDK throws
    /// <see cref="PlatformNotSupportedException"/>; we re-wrap that with a clearer message
    /// pointing at the actual limitation. The C# port itself is correct and runs cleanly under
    /// a .NET Framework 4.8 host (e.g. Skyline).
    /// </remarks>
    public AgilentRawData(string dotDPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dotDPath);
        if (!Directory.Exists(dotDPath))
            throw new DirectoryNotFoundException($"Agilent .d not found: {dotDPath}");
        Path = dotDPath;

        _reader = new MassSpecDataReader();
        try
        {
            if (!_reader.OpenDataFile(dotDPath))
                throw new InvalidDataException($"MassSpecDataReader could not open {dotDPath}");
        }
        catch (PlatformNotSupportedException ex)
        {
            throw new PlatformNotSupportedException(
                "Agilent's MassSpecDataReader uses delegate.BeginInvoke, which .NET 5+ removed; "
                + "this msconvert-sharp build targets .NET 8 and cannot open Agilent .d files. "
                + "Run under a .NET Framework 4.8 host (e.g. Skyline) instead.", ex);
        }
    }

    /// <summary>Returns the lightweight scan record for row <paramref name="rowIndex"/> (0-based).</summary>
    public IMSScanRecord GetScanRecord(int rowIndex) => _reader.GetScanRecord(rowIndex);

    /// <summary>
    /// Returns the full spectrum for row <paramref name="rowIndex"/>. <paramref name="preferProfile"/>
    /// asks for profile data when both formats are stored; otherwise the SDK returns the centroid
    /// representation. Mirrors cpp <c>getProfileSpectrumByRow</c> / <c>getPeakSpectrumByRow</c>.
    /// </summary>
    public IBDASpecData GetSpectrumByRow(int rowIndex, bool preferProfile)
    {
        var storage = preferProfile
            ? DesiredMSStorageType.ProfileElsePeak
            : DesiredMSStorageType.PeakElseProfile;
        // The 3-arg overload takes (scanId, peakFilterMS1, peakFilterMSn, storageType). Passing
        // null for the peak filters means "no filtering". rowIndex here is the row, not scan id —
        // the SDK overloads on int treat the int as a row when called via this signature.
        return _reader.GetSpectrum(rowIndex, null, null, storage);
    }

    // ---------- TIC / BPC helpers ----------

    /// <summary>Run-level total ion chromatogram (times in minutes, intensities in counts).</summary>
    public IBDAChromData? GetTic()
    {
        try { return _reader.GetTIC(); }
        catch { return null; }
    }

    /// <summary>Run-level base peak chromatogram (times in minutes, intensities in counts).</summary>
    public IBDAChromData? GetBpc()
    {
        try { return _reader.GetBPC(); }
        catch { return null; }
    }

    /// <summary>The non-MS data reader interface (UV/DAD/pressure/flow signals), or null when
    /// the underlying SDK reader doesn't implement it. Mirrors cpp <c>MassHunterDataImpl</c>'s
    /// <c>(MHDAC::INonmsDataReader^) reader_</c> downcast.</summary>
    public INonmsDataReader? NonMsDataReader
    {
        get
        {
            try { return _reader as INonmsDataReader; }
            catch { return null; }
        }
    }

    /// <summary>
    /// Cached time grid for non-MS (UV/DAD) spectra, in minutes. Populated lazily by
    /// <see cref="GetNonMsScanCount"/> via the SDK's <c>GetChromatogram</c> with
    /// <c>ChromType.ExtractedWavelength</c> + <c>DeviceName="DAD"</c>; mirrors cpp
    /// <c>MassHunterDataImpl::initNonMsData</c>.
    /// </summary>
    private double[]? _dadTimes;

    /// <summary>
    /// Number of non-MS (UV/DAD) spectra in the file. Returns 0 when the file has no DAD
    /// device. Caches the time grid so subsequent <see cref="GetNonMsSpectrumByRow"/> calls
    /// can map a row index to a scan time.
    /// </summary>
    public int GetNonMsScanCount()
    {
        if (_dadTimes is not null) return _dadTimes.Length;
        try
        {
            // Properties on BDAChromFilter / BDASpecFilter are explicitly implemented on the
            // matching interface, so configuring them requires an interface-typed reference.
            IBDAChromFilter filter = new BDAChromFilter();
            filter.ChromatogramType = ChromType.ExtractedWavelength;
            filter.DeviceName = "DAD";
            var chromatograms = _reader.GetChromatogram(filter);
            if (chromatograms is null || chromatograms.Length == 0)
            {
                _dadTimes = Array.Empty<double>();
                return 0;
            }
            _dadTimes = chromatograms[0].XArray ?? Array.Empty<double>();
            return _dadTimes.Length;
        }
        catch
        {
            _dadTimes = Array.Empty<double>();
            return 0;
        }
    }

    private List<AgilentTransition>? _transitionsCache;
    private List<IBDAChromData?>? _transitionChromCache;

    /// <summary>SRM (MultipleReactionMode) and SIM (SelectedIonMonitoring) transitions
    /// declared by the file. Mirrors cpp <c>MassHunterDataImpl::initTransitions</c> /
    /// <c>getTransitions</c>: queries the SDK with <c>ChromType.MultipleReactionMode</c>
    /// then <c>ChromType.SelectedIonMonitoring</c>, builds a transition record from each
    /// chromatogram's <c>MZOfInterest</c> / <c>MeasuredMassRange</c> / <c>IonPolarity</c>
    /// / <c>AcquiredTimeRange</c>, and caches both the metadata and the chromatogram
    /// data (cpp comments that re-fetching SRM data costs a 50x perf hit on large files).
    /// MRM transitions are emitted before SIM, matching cpp's index ordering.</summary>
    public IReadOnlyList<AgilentTransition> Transitions
    {
        get
        {
            if (_transitionsCache is not null) return _transitionsCache;
            EnsureTransitionsLoaded();
            return _transitionsCache!;
        }
    }

    /// <summary>Returns the cached chromatogram for <paramref name="transitionIndex"/>,
    /// or null when the SDK didn't return one (defensive — shouldn't happen for valid
    /// transitions). Index aligns with <see cref="Transitions"/>.</summary>
    public IBDAChromData? GetTransitionChromatogram(int transitionIndex)
    {
        if (_transitionsCache is null) EnsureTransitionsLoaded();
        if (_transitionChromCache is null || transitionIndex < 0 || transitionIndex >= _transitionChromCache.Count)
            return null;
        return _transitionChromCache[transitionIndex];
    }

    private void EnsureTransitionsLoaded()
    {
        // Build (transition, chrom) pairs, then sort matching cpp's `set<Transition>` order
        // (operator<: type → ionPolarity → Q1 → Q3 → timeStart → timeEnd ascending). Preserving
        // the chromatogram pointer alongside the transition keeps GetTransitionChromatogram
        // aligned with the public Transitions list after sort.
        var staged = new List<(AgilentTransition T, IBDAChromData? C)>();
        try
        {
            // MRM first (cpp does the same ordering — `transitions_.insert(t)` is into a set
            // sorted by Q1/Q3, but the per-type discovery order matches cpp's array layout).
            IBDAChromFilter filter = new BDAChromFilter();
            filter.DoCycleSum = false;
            filter.ExtractOneChromatogramPerScanSegment = true;
            filter.ChromatogramType = ChromType.MultipleReactionMode;
            var mrmChroms = _reader.GetChromatogram(filter);
            if (mrmChroms is not null)
            {
                foreach (var c in mrmChroms)
                {
                    if (c.MZOfInterest is null || c.MZOfInterest.Length == 0) continue;
                    if (c.MeasuredMassRange is null || c.MeasuredMassRange.Length == 0) continue;
                    var mzRange = c.MZOfInterest[0];
                    var prodRange = c.MeasuredMassRange[0];
                    double q1 = mzRange.Start;
                    double q3 = prodRange.Start;
                    var pol = c.IonPolarity switch
                    {
                        IonPolarity.Positive => AgTransitionPolarity.Positive,
                        IonPolarity.Negative => AgTransitionPolarity.Negative,
                        _ => AgTransitionPolarity.Unassigned,
                    };
                    double startTime = 0, endTime = 0;
                    if (c.AcquiredTimeRange is { Length: > 0 })
                    {
                        startTime = c.AcquiredTimeRange[0].Start;
                        endTime = c.AcquiredTimeRange[0].End;
                    }
                    staged.Add((new AgilentTransition(
                        Type: AgTransitionType.Mrm,
                        Q1: q1, Q3: q3,
                        Polarity: pol,
                        TimeStart: startTime, TimeEnd: endTime,
                        CollisionEnergy: c.CollisionEnergy), c));
                }
            }

            // SIM: only Q1 (selection mass) is meaningful; Q3 stays at 0.
            filter.ChromatogramType = ChromType.SelectedIonMonitoring;
            var simChroms = _reader.GetChromatogram(filter);
            if (simChroms is not null)
            {
                foreach (var c in simChroms)
                {
                    if (c.MeasuredMassRange is null || c.MeasuredMassRange.Length == 0) continue;
                    double q1 = c.MeasuredMassRange[0].Start;
                    var pol = c.IonPolarity switch
                    {
                        IonPolarity.Positive => AgTransitionPolarity.Positive,
                        IonPolarity.Negative => AgTransitionPolarity.Negative,
                        _ => AgTransitionPolarity.Unassigned,
                    };
                    double startTime = 0, endTime = 0;
                    if (c.AcquiredTimeRange is { Length: > 0 })
                    {
                        startTime = c.AcquiredTimeRange[0].Start;
                        endTime = c.AcquiredTimeRange[0].End;
                    }
                    staged.Add((new AgilentTransition(
                        Type: AgTransitionType.Sim,
                        Q1: q1, Q3: 0,
                        Polarity: pol,
                        TimeStart: startTime, TimeEnd: endTime,
                        CollisionEnergy: 0), c));
                }
            }
        }
        catch { /* SDK quirks shouldn't take down the whole list */ }

        // cpp Transition::operator< (MassHunterData.cpp:261-279) — type, then polarity, Q1,
        // Q3, time start, time end ascending.
        staged.Sort((a, b) =>
        {
            int cmp = ((int)a.T.Type).CompareTo((int)b.T.Type);
            if (cmp != 0) return cmp;
            cmp = ((int)a.T.Polarity).CompareTo((int)b.T.Polarity);
            if (cmp != 0) return cmp;
            cmp = a.T.Q1.CompareTo(b.T.Q1);
            if (cmp != 0) return cmp;
            cmp = a.T.Q3.CompareTo(b.T.Q3);
            if (cmp != 0) return cmp;
            cmp = a.T.TimeStart.CompareTo(b.T.TimeStart);
            if (cmp != 0) return cmp;
            return a.T.TimeEnd.CompareTo(b.T.TimeEnd);
        });

        var transList = new List<AgilentTransition>(staged.Count);
        var chromList = new List<IBDAChromData?>(staged.Count);
        foreach (var (t, c) in staged)
        {
            transList.Add(t);
            chromList.Add(c);
        }
        _transitionsCache = transList;
        _transitionChromCache = chromList;
    }

    /// <summary>The cached DAD time grid (must call <see cref="GetNonMsScanCount"/> at least
    /// once first). Indexed 0..N-1, in minutes.</summary>
    public double[] NonMsScanTimes
    {
        get
        {
            if (_dadTimes is null) GetNonMsScanCount();
            return _dadTimes ?? Array.Empty<double>();
        }
    }

    /// <summary>
    /// Returns the UV/DAD spectrum at <paramref name="rowIndex"/> (0-based, into
    /// <see cref="NonMsScanTimes"/>) — mirrors cpp <c>MassHunterDataImpl::getNonMsSpectrum</c>.
    /// X = wavelength (nm), Y = absorbance counts.
    /// </summary>
    public IBDASpecData? GetNonMsSpectrumByRow(int rowIndex)
    {
        var times = NonMsScanTimes;
        if (rowIndex < 0 || rowIndex >= times.Length) return null;
        try
        {
            double t = times[rowIndex];
            IBDASpecFilter specFilter = new BDASpecFilter();
            specFilter.SpectrumType = SpecType.UVSpectrum;
            specFilter.ScanRange = new IRange[] { new MinMaxRange(t, t) };
            var spectra = _reader.GetSpectrum(specFilter);
            if (spectra is null || spectra.Length == 0) return null;
            return spectra[0];
        }
        catch { return null; }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _reader?.CloseDataFile(); }
        catch { /* SDK may throw on bogus state — best-effort close */ }
        if (_imsReader is not null)
        {
            try { _imsReader.Close(); } catch { }
            _imsReader = null;
        }
    }

    /// <summary>
    /// Quick sanity check: a path is an Agilent .d directory iff it has an <c>AcqData</c>
    /// subdirectory containing the well-known scan files. Mirrors cpp <c>Reader_Agilent::identify</c>.
    /// </summary>
    public static bool IsAgilentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;
        string acqData = System.IO.Path.Combine(path, "AcqData");
        if (!Directory.Exists(acqData)) return false;
        // MSScan.bin or MSPeak.bin signal "this AcqData has MS data"; some non-MS .d dirs only
        // have signal data and no MS scans.
        return File.Exists(System.IO.Path.Combine(acqData, "MSScan.bin"))
            || File.Exists(System.IO.Path.Combine(acqData, "MSPeak.bin"));
    }
}

/// <summary>One row from <c>AcqData/Devices.xml</c>. <see cref="TypeRaw"/> is the integer
/// device type as a string (matching the underlying SDK <c>DeviceType</c> enum value).</summary>
public sealed record AgilentDeviceInfo(string Name, string ModelNumber, string SerialNumber, string TypeRaw);

/// <summary>SRM (multi-reaction) vs SIM (selected-ion-monitoring) transition kind.</summary>
public enum AgTransitionType
{
    /// <summary>Multi-reaction monitoring (Q1 → fragment Q3).</summary>
    Mrm,

    /// <summary>Selected-ion monitoring (single Q1 isolation, no fragmentation).</summary>
    Sim,
}

/// <summary>Mirrors cpp <c>IonPolarity</c> integer values exactly so transition sort order
/// (which compares polarity numerically per <c>Transition::operator&lt;</c>) matches the
/// reference. cpp: Positive=0, Negative=1, Unassigned=2.</summary>
public enum AgTransitionPolarity
{
    /// <summary>Positive ion mode.</summary>
    Positive = 0,

    /// <summary>Negative ion mode.</summary>
    Negative = 1,

    /// <summary>Polarity not reported by the SDK for this transition.</summary>
    Unassigned = 2,
}

/// <summary>One Agilent SRM/SIM transition. <see cref="Q3"/> is unused for
/// <see cref="AgTransitionType.Sim"/> (always 0). <see cref="TimeStart"/>/<see cref="TimeEnd"/>
/// are the segment time range in minutes (0/0 when the SDK doesn't expose one).
/// <see cref="CollisionEnergy"/> is the activation collision energy in eV (0 for SIM).</summary>
public sealed record AgilentTransition(
    AgTransitionType Type,
    double Q1, double Q3,
    AgTransitionPolarity Polarity,
    double TimeStart, double TimeEnd,
    double CollisionEnergy);
