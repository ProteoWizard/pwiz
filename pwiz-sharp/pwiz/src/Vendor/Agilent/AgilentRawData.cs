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

    // KNOWN C#-SIDE SURPLUS (deliberate): on ion-mobility files this port emits an
    // `MS:1000529 instrument serial number` that the cpp reference mzML does not carry.
    //
    // cpp is not choosing to omit it. `MassHunterData::create` hands an IM .d to
    // `MidacDataImpl`, whose `getDeviceType()` is a hard-coded `DeviceType_Unknown`
    // (MidacData.cpp:198-201) because MIDAC exposes no device table. `getDeviceSerialNumber`
    // then searches AcqData/Devices.xml for a device whose Type equals that, i.e. searches for
    // type 0, never matches, and returns nothing. Keying the lookup on the scan file's real
    // device type instead (6 = QuadrupoleTimeOfFlight) matches the row and yields the serial
    // the file genuinely carries - SG1812C101 on mulATM4, SG1928C201 on the Dorrestein file,
    // and the placeholder SN123456 on the synthetic ImsSynth fixtures.
    //
    // Because these are real values read from the same Devices.xml cpp itself parses, this is
    // extra correct data rather than a porting gap, and it is kept. The cost is that the
    // affected IM files never compare byte-identical to cpp; that is the intended trade.

    /// <summary>
    /// Spectrum storage mode (profile / centroid / mixed) as cpp's
    /// <c>MassHunterDataImpl::getSpectraFormat</c> (<c>MassHunterData.cpp:514-517</c>) or
    /// <c>MidacDataImpl::getSpectraFormat</c> (<c>MidacData.cpp:244-247</c>) reports it.
    /// </summary>
    /// <remarks>
    /// For ion-mobility files cpp reads MIDAC's <c>FileInfo.TfsMsDetails.MsStorageMode</c>, not
    /// the MassSpec SDK's <c>MSScanFileInformation.SpectraFormat</c>; the two disagree, and the
    /// mode is what <c>Reader_Agilent::fillInMetadata</c> turns into the fileContent
    /// <c>centroid spectrum</c> / <c>profile spectrum</c> terms. Reading the MassSpec value on an
    /// IM file dropped <c>MS:1000127 centroid spectrum</c> from mulATM4.d.DeMP.d and the
    /// Dorrestein fixtures and added it, wrongly, to Test_BsaFromUimf.d.
    /// Unspecified when MIDAC won't answer, which emits neither term - closer to cpp (which
    /// throws) than silently substituting the other SDK's opinion.
    /// </remarks>
    public MSStorageMode SpectraFormat
    {
        get
        {
            if (HasIonMobilityData)
            {
                try
                {
                    var details = ImsReader?.FileInfo?.TfsMsDetails;
                    if (details is not null) return (MSStorageMode)(int)details.MsStorageMode;
                }
                catch { /* fall through to Unspecified */ }
                return MSStorageMode.Unspecified;
            }
            return MSScanFileInformation.SpectraFormat;
        }
    }

    private IMidacImsReader? _imsReader;
    private bool? _hasImsData;
    private IImsCcsInfoReader? _imsCcsReader;
    private bool _imsCcsReaderResolved;

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

    /// <summary>MIDAC's CCS-conversion bridge. Returns null when the file isn't IM or
    /// when MIDAC fails to construct an <see cref="ImsCcsInfoReader"/> (older drivers,
    /// missing calibration metadata, etc.). cpp constructs this once at file-open time;
    /// we defer until first use because most callers never ask for CCS.</summary>
    public IImsCcsInfoReader? ImsCcsReader
    {
        get
        {
            if (_imsCcsReaderResolved) return _imsCcsReader;
            _imsCcsReaderResolved = true;
            if (!HasIonMobilityData) return null;
            try { _imsCcsReader = new ImsCcsInfoReader(); _imsCcsReader.Read(Path); }
            catch { _imsCcsReader = null; }
            return _imsCcsReader;
        }
    }

    /// <summary>True iff this .d carries a single-field CCS calibration. Mirrors cpp
    /// <c>MidacDataImpl::canConvertDriftTimeAndCCS</c> -> <c>imsCcsReader_-&gt;HasSingleFieldCcsInformation</c>.</summary>
    public bool CanConvertDriftTimeAndCcs
    {
        get
        {
            var r = ImsCcsReader;
            if (r is null) return false;
            try { return r.HasSingleFieldCcsInformation; }
            catch { return false; }
        }
    }

    /// <summary>Converts a drift time (ms) to a collisional cross section. Returns
    /// <see cref="double.NaN"/> if MIDAC's solver fails (the "cannot solve cubic fit"
    /// path that throws <see cref="System.IO.InvalidDataException"/> in cpp). Throws
    /// <see cref="InvalidOperationException"/> when called on a non-IM file.</summary>
    public double DriftTimeToCcs(double driftTimeMsec, double mz, int charge)
    {
        var r = ImsCcsReader ?? throw new InvalidOperationException(
            "[AgilentRawData.DriftTimeToCcs] file has no IM / CCS calibration");
        try { return r.CcsFromDriftTime(driftTimeMsec, mz, System.Math.Abs(charge)); }
        catch (System.IO.InvalidDataException) { return double.NaN; }
    }

    /// <summary>Inverse of <see cref="DriftTimeToCcs"/>. Same NaN-on-solver-failure /
    /// throw-on-non-IM semantics.</summary>
    public double CcsToDriftTime(double ccs, double mz, int charge)
    {
        var r = ImsCcsReader ?? throw new InvalidOperationException(
            "[AgilentRawData.CcsToDriftTime] file has no IM / CCS calibration");
        try { return r.DriftTimeFromCcs(ccs, mz, System.Math.Abs(charge)); }
        catch (System.IO.InvalidDataException) { return double.NaN; }
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

    /// <summary>
    /// Drift bins per IM frame. cpp reads this off a frame — <c>FrameImpl</c>'s ctor sets
    /// <c>numDriftBins_ = imsReader->FileInfo->MaxNonTfsMsPerFrame</c> (MidacData.cpp:430-434) —
    /// but it is a file-level constant, so no frame needs to be materialized to get it. It scales
    /// the uncombined-IM spectrum ids: <c>scanId = frameIndex * driftBinsPerFrame + driftBin</c>.
    /// </summary>
    public int ImsDriftBinsPerFrame
    {
        get
        {
            var reader = ImsReader;
            if (reader is null) return 0;
            try { return reader.FileInfo?.MaxNonTfsMsPerFrame ?? 0; }
            catch { return 0; }
        }
    }

    /// <summary>1-based frame number for the i-th IM frame (0-based <paramref name="i"/>).
    /// Returns the value MIDAC expects for <c>FrameInfo</c> / <c>FrameMs</c> calls.</summary>
    public int ImsFrameNumber(int i)
    {
        var nums = ImsFrameNumbers;
        return i >= 0 && i < nums.Length ? nums[i] : 0;
    }

    /// <summary>Friendly device name reported by the SDK (e.g. <c>"TandemQuadrupole"</c>).
    /// Mirrors cpp <c>MassHunterDataImpl::getDeviceName</c> for non-IM files.
    /// <para>On ion mobility files cpp goes through <c>MidacDataImpl::getDeviceName</c>
    /// (<c>MidacData.cpp:203</c>), which ignores <paramref name="deviceType"/> entirely and
    /// reports MIDAC's <c>FileInfo.InstrumentName</c> - the name the file actually carries
    /// ("IM-MS QTOF", "Instrument 1", "IMS 11"). The MassSpec SDK answers the same call with
    /// the generic device-TYPE name ("QTOF"), so reading it here is not a formatting
    /// difference but a different source, and it is what the reference mzMLs carry.</para></summary>
    public string GetDeviceName(DeviceType deviceType)
    {
        if (HasIonMobilityData)
        {
            var ims = ImsReader;
            if (ims is not null)
            {
                try
                {
                    var fi = ims.FileInfo;
                    if (fi is not null) return fi.InstrumentName ?? string.Empty;
                }
                catch { /* fall through to MassSpec SDK */ }
            }
        }
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

    private Dictionary<string, string>? _sampleInfoCache;

    /// <summary>
    /// Name/value pairs from <c>AcqData/sample_info.xml</c>, as cpp's <c>XmlMetadataParser</c>
    /// reads them - the SDK does not expose this metadata. Repeated names get a numeric suffix so
    /// no entry is lost, matching cpp's uniquifying rule.
    /// </summary>
    private Dictionary<string, string> SampleInfo
    {
        get
        {
            if (_sampleInfoCache is not null) return _sampleInfoCache;
            _sampleInfoCache = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                var sampleInfoPath = System.IO.Path.Combine(Path, "AcqData", "sample_info.xml");
                if (!File.Exists(sampleInfoPath)) return _sampleInfoCache;
                var doc = System.Xml.Linq.XDocument.Load(sampleInfoPath);
                // Not named `field`: inside a property accessor that is a contextual keyword from
                // C# 14 on, binding to the synthesized backing field rather than the loop variable.
                foreach (var element in doc.Descendants("Field"))
                {
                    string name = ((string?)element.Element("Name") ?? string.Empty).Trim();
                    if (name.Length == 0) continue;
                    string value = ((string?)element.Element("Value") ?? string.Empty).Trim();

                    string uniqueName = name;
                    for (int suffix = 2; _sampleInfoCache.ContainsKey(uniqueName); suffix++)
                        uniqueName = $"{name}_{suffix}";
                    _sampleInfoCache[uniqueName] = value;
                }
            }
            catch { /* best-effort, as with Devices.xml */ }
            return _sampleInfoCache;
        }
    }

    /// <summary>Looks up one sample-info field. cpp <c>MassHunterData::getSampleInfoValue</c>.</summary>
    public string GetSampleInfoValue(string key, string defaultValue = "") =>
        SampleInfo.TryGetValue(key, out var value) ? value : defaultValue;

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
            // pwiz C++ MassHunterData.cpp:322-324 ignores a false return from OpenDataFile: it only
            // flags a possibly-incomplete acquisition (e.g. a stitched multi-CE .d whose name has
            // "[stitch]"), but the reader is still usable. Throwing here rejected valid files that
            // net472 reads fine; a genuinely unopenable file surfaces on later access instead.
            _reader.OpenDataFile(dotDPath);
        }
        catch (PlatformNotSupportedException ex)
        {
            throw new PlatformNotSupportedException(
                "Agilent's MassSpecDataReader uses delegate.BeginInvoke, which .NET 5+ removed; "
                + "this msconvert-sharp build targets .NET 8 and cannot open Agilent .d files. "
                + "Run under a .NET Framework 4.8 host (e.g. Skyline) instead.", ex);
        }

        InitializeChromatograms();
    }

    private double[] _ticTimes = Array.Empty<double>();
    private double[] _ticTimesMs1 = Array.Empty<double>();
    private float[] _ticIntensities = Array.Empty<float>();
    private float[] _ticIntensitiesMs1 = Array.Empty<float>();
    private float[] _bpcIntensities = Array.Empty<float>();

    /// <summary>
    /// Per-row TIC and base-peak intensities: the SDK's TotalIon and BasePeak chromatograms
    /// fetched with <c>DoCycleSum = false</c>, kept as float. NOT interchangeable with
    /// <c>IMSScanRecord.Tic</c> / <c>.BasePeakIntensity</c> - on centroided QTOF runs the two
    /// disagree by as much as 12%. cpp indexes these by row for a spectrum's total ion current
    /// and base peak intensity (SpectrumList_Agilent.cpp:215-216).
    /// </summary>
    public (float[] Tic, float[] Bpc) ChromatogramIntensities => (_ticIntensities, _bpcIntensities);

    /// <summary>
    /// Run-level TIC time axis, in minutes. Port of cpp
    /// <c>MassHunterDataImpl::getTicTimes</c> / <c>MidacDataImpl::getTicTimes</c>
    /// (MassHunterData.cpp:546-549, MidacData.cpp:300-303). <paramref name="ms1Only"/> selects
    /// the MS1-filtered variant that <c>globalChromatogramsAreMs1Only</c> asks for.
    /// </summary>
    public double[] GetTicTimes(bool ms1Only = false) => ms1Only ? _ticTimesMs1 : _ticTimes;

    /// <summary>
    /// Run-level TIC intensities, aligned with <see cref="GetTicTimes"/>. Port of cpp
    /// <c>MassHunterDataImpl::getTicIntensities</c> / <c>MidacDataImpl::getTicIntensities</c>
    /// (MassHunterData.cpp:556-559, MidacData.cpp:310-313). cpp holds these as
    /// <c>BinaryData&lt;float&gt;</c>, so every value the mzML carries is float-rounded.
    /// </summary>
    public float[] GetTicIntensities(bool ms1Only = false) => ms1Only ? _ticIntensitiesMs1 : _ticIntensities;

    /// <summary>
    /// Fetches the TIC/BPC arrays, mirroring cpp <c>MassHunterDataImpl</c>'s constructor
    /// (<c>MassHunterData.cpp:330-357</c>) or, for ion-mobility files, <c>MidacDataImpl</c>'s
    /// (<c>MidacData.cpp:160-177</c>).
    /// </summary>
    /// <remarks>
    /// Done EAGERLY, at open, because it must happen before anything else queries
    /// <c>GetChromatogram</c>. The Agilent SDK carries state across those calls - the same
    /// reason <see cref="HasProfileData"/> reads MSProfile.bin off disk instead of trusting
    /// <c>SpectraFormat</c>, which flips to Mixed once a DAD chromatogram has been fetched. Our
    /// own index build calls <see cref="GetNonMsScanCount"/> (a DAD <c>GetChromatogram</c>), so
    /// a lazy TIC fetch would run after it and return different numbers than cpp's.
    /// </remarks>
    private void InitializeChromatograms()
    {
        if (HasIonMobilityData)
        {
            InitializeImsChromatograms();
            return;
        }

        // Fetched through a SEPARATE reader, not _reader, and that is a deliberate deviation
        // from cpp - which uses one reader for everything.
        //
        // The Agilent SDK carries state across GetChromatogram: whatever calls a reader has
        // seen changes what its later spectrum reads return, down to ~5e-10 in the m/z axis.
        // Fetching on _reader moved every m/z on mix-with-variable.d and
        // pepmix-with-variable-transients.d (highest observed m/z 1821.983406545172 ->
        // ...558446) and took both off byte parity, while fixing AE_30Apr19_negESI_0001.d and
        // BSA050-r001.d, whose cpp TIC values embed that same perturbation. Replicating cpp's
        // full four-fetch sequence exactly (TIC, BPC, then both again under MSLevelFilter = MS)
        // reproduces cpp on those two and still breaks the other two, so there is no single
        // shared-reader state that satisfies both groups.
        //
        // A private reader keeps the spectrum path bit-for-bit as it was before this array
        // existed, which is the safer half of the trade: four files (BSA-ms2-centroid,
        // ST-100fmol-03, mix-std, blank01) drop from 81 diffs to 1 and nothing regresses.
        // AE_30Apr19 and BSA050 keep their TIC/base-peak diffs as a known remainder.
        IMsdrDataReader? chromReader = null;
        try
        {
            chromReader = new MassSpecDataReader();
            chromReader.OpenDataFile(Path);

            IBDAChromFilter filter = new BDAChromFilter();
            // cpp: "cycle summing can make the full file chromatograms have the wrong number of points"
            filter.DoCycleSum = false;

            filter.ChromatogramType = ChromType.TotalIon;
            var tic = chromReader.GetChromatogram(filter);
            if (tic is { Length: > 0 })
            {
                // Copy: the SDK hands back its own buffers and a later fetch on the same reader
                // may recycle them. cpp's ToBinaryData copies too.
                _ticTimes = CopyOrEmpty(tic[0].XArray);
                _ticIntensities = CopyOrEmpty(tic[0].YArray);
            }

            filter.ChromatogramType = ChromType.BasePeak;
            var bpc = chromReader.GetChromatogram(filter);
            if (bpc is { Length: > 0 }) _bpcIntensities = CopyOrEmpty(bpc[0].YArray);

            // MS1-only variants, in cpp's order (MassHunterData.cpp:347-357). Only the TIC pair
            // is kept: nothing in the port consumes an MS1-only BPC, and cpp's own BPC accessor
            // is called without the flag from SpectrumList_Agilent.
            filter.MSLevelFilter = MSLevel.MS;
            filter.ChromatogramType = ChromType.TotalIon;
            var ticMs1 = chromReader.GetChromatogram(filter);
            if (ticMs1 is { Length: > 0 })
            {
                _ticTimesMs1 = CopyOrEmpty(ticMs1[0].XArray);
                _ticIntensitiesMs1 = CopyOrEmpty(ticMs1[0].YArray);
            }
        }
        catch { /* leave empty; callers fall back to the scan record */ }
        finally
        {
            if (chromReader is not null)
            {
                try { chromReader.CloseDataFile(); } catch { }
            }
        }
    }

    private static T[] CopyOrEmpty<T>(T[]? source) => source is null || source.Length == 0
        ? Array.Empty<T>()
        : (T[])source.Clone();

    /// <summary>
    /// Ion-mobility TIC, per cpp <c>MidacDataImpl</c>'s constructor (MidacData.cpp:160-177): one
    /// point per MIDAC frame, time = <c>FrameInfo(i+1).AcqTimeRange.Min</c>, intensity =
    /// <c>FrameInfo(i+1).Tic</c>. The MS1-only variant keeps the frames whose collision energy is
    /// zero - cpp splits on collision energy here, NOT on MS level, because an all-ions IM frame
    /// is stored as MS1 with a non-zero CE and must not count as MS1 for the global chromatogram.
    /// </summary>
    private void InitializeImsChromatograms()
    {
        var reader = ImsReader;
        if (reader is null) return;
        try
        {
            int frames = reader.FileInfo?.NumFrames ?? 0;
            if (frames <= 0) return;
            var times = new double[frames];
            var intensities = new float[frames];
            var timesMs1 = new List<double>(frames);
            var intensitiesMs1 = new List<float>(frames);
            for (int i = 0; i < frames; i++)
            {
                IMidacFrameInfo? info = null;
                try { info = reader.FrameInfo(i + 1); } catch { }
                if (info is null) continue;
                try { times[i] = info.AcqTimeRange?.Min ?? 0; } catch { }
                try { intensities[i] = (float)info.Tic; } catch { }

                // cpp MidacScanRecord::getCollisionEnergy (MidacData.cpp:406-415).
                double collisionEnergy = 0;
                try
                {
                    var energy = info.SpectrumDetails?.FragmentationEnergyRange;
                    if (energy is not null) collisionEnergy = System.Math.Max(energy.Min, energy.Max);
                }
                catch { }
                if (collisionEnergy == 0)
                {
                    timesMs1.Add(times[i]);
                    intensitiesMs1.Add(intensities[i]);
                }
            }
            _ticTimes = times;
            _ticIntensities = intensities;
            _ticTimesMs1 = timesMs1.ToArray();
            _ticIntensitiesMs1 = intensitiesMs1.ToArray();
        }
        catch { /* leave empty; the TIC chromatogram comes out empty rather than wrong */ }
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
        try
        {
            return _reader.GetSpectrum(rowIndex, null, null, storage);
        }
        catch (Exception ex)
        {
            // cpp's CATCH_AND_FORWARD around the same call (MassHunterData.cpp:706-710) rethrows
            // with the failing function's name attached. Keep the SDK exception as InnerException
            // so an SDK-level cause (e.g. a decompression failure) is still diagnosable.
            throw new IOException(
                $"[AgilentRawData.GetSpectrumByRow] row {rowIndex} of {Path} could not be read: {ex.Message}", ex);
        }
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

    private List<AgilentSignal>? _signalsCache;
    private Dictionary<string, ISignalInfo>? _signalInfoMap;

    /// <summary>
    /// Non-MS signals (UV/DAD absorption traces, pump pressure and flow curves, ...) declared by
    /// the file. Mirrors cpp <c>MassHunterDataImpl::getSignals</c>: for each non-MS device, every
    /// row of the device's <c>Chromatograms</c> signal table followed by every row of its
    /// <c>InstrumentCurves</c> signal table. Empty when the file reports no non-MS data.
    /// </summary>
    public IReadOnlyList<AgilentSignal> Signals
    {
        get
        {
            if (_signalsCache is not null) return _signalsCache;
            EnsureSignalsLoaded();
            return _signalsCache!;
        }
    }

    /// <summary>Chromatogram data for one signal returned by <see cref="Signals"/>, or null when
    /// the SDK has no <c>ISignalInfo</c> for it. cpp <c>MassHunterDataImpl::getSignal</c> throws
    /// in that case; callers here treat a null as an empty chromatogram.</summary>
    public IBDAChromData? GetSignal(AgilentSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (_signalsCache is null) EnsureSignalsLoaded();
        if (_signalInfoMap is null) return null;
        if (!_signalInfoMap.TryGetValue(signal.DeviceName + signal.SignalName, out var signalInfo))
            return null;
        try { return NonMsDataReader?.GetSignal(signalInfo); }
        catch { return null; }
    }

    private void EnsureSignalsLoaded()
    {
        _signalsCache = new List<AgilentSignal>();
        _signalInfoMap = new Dictionary<string, ISignalInfo>(StringComparer.Ordinal);

        // Ion-mobility files go through MIDAC in cpp, and MidacDataImpl::getSignals
        // (MidacData.hpp:81) returns a vector that is never populated - so no non-MS signal
        // chromatogram is emitted for an IMS file even when the .d holds pump curves. The
        // BinPump traces in Dorrestein_GnPS_*.d are exactly that case.
        if (HasIonMobilityData) return;

        var nonMsDataReader = NonMsDataReader;
        if (nonMsDataReader is null) return;
        try
        {
            if (!FileInformation.IsNonMSDataPresent()) return;
        }
        catch { return; }

        IDeviceInfo[]? devices;
        try { devices = nonMsDataReader.GetNonmsDevices(); }
        catch { return; }
        if (devices is null) return;

        foreach (var device in devices)
        {
            string deviceNameAndOrdinal = device.DeviceName + device.OrdinalNumber.ToString(CultureInfo.InvariantCulture);

            // Both stored-data types feed the same signal list; the isInstrumentCurve flag is
            // what lets translateAsChromatogramType suppress detector instrument curves while
            // keeping pump ones. cpp catches per table so a device with only one of the two
            // still contributes.
            AddSignalTable(nonMsDataReader, device, deviceNameAndOrdinal, StoredDataType.Chromatograms, false);
            AddSignalTable(nonMsDataReader, device, deviceNameAndOrdinal, StoredDataType.InstrumentCurves, true);
        }
    }

    private void AddSignalTable(INonmsDataReader nonMsDataReader, IDeviceInfo device, string deviceNameAndOrdinal,
                                StoredDataType storedDataType, bool isInstrumentCurve)
    {
        try
        {
            var signalTable = FileInformation.GetSignalTable(deviceNameAndOrdinal, storedDataType);
            if (signalTable is not null)
            {
                foreach (System.Data.DataRow row in signalTable.Rows)
                {
                    // Descriptions are used verbatim: the leading space in Agilent's " Pressure"
                    // is part of the chromatogram id cpp emits ("LowflowPump1 A:  Pressure").
                    _signalsCache!.Add(new AgilentSignal(
                        DeviceName: deviceNameAndOrdinal,
                        SignalName: row["SignalName"]?.ToString() ?? string.Empty,
                        SignalDescription: row["SignalDescription"]?.ToString() ?? string.Empty,
                        IsInstrumentCurve: isInstrumentCurve,
                        DeviceType: device.DeviceType));
                }
            }

            var signalInfos = nonMsDataReader.GetSignalInfo(device, storedDataType);
            if (signalInfos is not null)
            {
                foreach (var signalInfo in signalInfos)
                    _signalInfoMap![deviceNameAndOrdinal + signalInfo.SignalName] = signalInfo;
            }
        }
        catch { /* cpp logs to cerr and moves on to the next table */ }
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

/// <summary>One non-MS signal, i.e. one row of a device's signal table. <see cref="DeviceName"/>
/// is the device name with its ordinal appended ("LowflowPump1"), matching what cpp stores in
/// <c>Signal::deviceName</c> and uses to build chromatogram ids. <see cref="IsInstrumentCurve"/>
/// distinguishes the <c>InstrumentCurves</c> table from the <c>Chromatograms</c> one.</summary>
public sealed record AgilentSignal(
    string DeviceName,
    string SignalName,
    string SignalDescription,
    bool IsInstrumentCurve,
    DeviceType DeviceType);

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
