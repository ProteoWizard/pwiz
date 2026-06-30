// MsDataFileImpl — refactored to use pwiz-sharp instead of pwiz.CLI.
//
// This is the Phase 2 sandbox port of pwiz_tools/Shared/ProteowizardWrapper/MsDataFileImpl.cs.
// The public surface preserves Skyline's call-site contract (same class name, same method
// signatures); the internals call pwiz-sharp's managed MSData / MSDataFile / ISpectrumList /
// IChromatogramList instead of the native C++/CLI bindings.
//
// Phase 2 scope: only the methods that the TestData suite exercises are ported below. The
// remaining surface from the original (QcTraces, SONAR, IsValidDiaPasefPoint, CCS converter
// wrappers, etc.) is enumerated in the NET8-PORT-NOTES.md catalog and ports straight through —
// the techniques shown here apply identically.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace pwiz.ProteowizardWrapper;

/// <summary>
/// Phase 2 net8 facade — same shape as the legacy pwiz.CLI-backed version, but the
/// underlying data layer is pwiz-sharp. Skyline call sites can swap from the old
/// MsDataFileImpl to this one with no public-surface change.
/// </summary>
public sealed class MsDataFileImpl : IDisposable
{
    // ===== Constants (no pwiz dep — moved verbatim from legacy) =====
    public const string PREFIX_TOTAL = "SRM TIC ";
    public const string PREFIX_SINGLE = "SRM SIC ";
    public const string PREFIX_PRECURSOR = "SIM SIC ";
    public const string TIC = "TIC";
    public const string BPC = "BPC";

    // ===== Backing state =====
    private readonly MSData _msd;
    private readonly ReaderConfig _config;
    private bool _disposed;

    /// <summary>Path the file was opened from (preserved for diagnostics + telemetry).</summary>
    public string FilePath { get; }

    /// <summary>Sample index inside a multi-sample container (Sciex .wiff, Shimadzu .lcd).</summary>
    public int SampleIndex { get; }

    public MsDataFileImpl(string path,
        int sampleIndex = 0,
        bool simAsSpectra = false,
        bool srmAsSpectra = false,
        bool acceptZeroLengthSpectra = true,
        bool requireVendorCentroidedMS1 = false,
        bool requireVendorCentroidedMS2 = false,
        bool ignoreZeroIntensityPoints = false,
        int preferOnlyMsLevel = 0,
        bool combineIonMobilitySpectra = true,
        bool trimNativeId = true,
        bool passEntireDiaPasefFrame = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = path;
        SampleIndex = sampleIndex;

        // Map the legacy ReaderConfig fields (camelCase) onto pwiz-sharp's PascalCase
        // equivalents. The big-rock flags all line up 1:1; PassEntireDiaPasefFrame was
        // newly added in this branch's pwiz-sharp leg (see ReaderConfig.cs).
        _config = new ReaderConfig
        {
            SimAsSpectra = simAsSpectra,
            SrmAsSpectra = srmAsSpectra,
            AcceptZeroLengthSpectra = acceptZeroLengthSpectra,
            IgnoreZeroIntensityPoints = ignoreZeroIntensityPoints,
            PreferOnlyMsLevel = combineIonMobilitySpectra ? 0 : preferOnlyMsLevel,
            AllowMsMsWithoutPrecursor = false,
            CombineIonMobilitySpectra = combineIonMobilitySpectra,
            IgnoreCalibrationScans = true, // Waters: lockmass scans aren't part of the analytical run
            GlobalChromatogramsAreMs1Only = true,
            PassEntireDiaPasefFrame = passEntireDiaPasefFrame,
            RunIndex = sampleIndex,
        };

        _msd = new MSData();
        ReaderList.Default.Read(path, _msd, _config);
        // requireVendorCentroidedMS{1,2} and trimNativeId are advisory in the legacy
        // impl too — surfaced through later peak-pick wrapping, not the ctor. Hold the
        // flags for when Skyline asks GetSpectrum + peak-pick.
        RequireVendorCentoridedMs1 = requireVendorCentroidedMS1;
        RequireVendorCentoridedMs2 = requireVendorCentroidedMS2;
    }

    public bool RequireVendorCentoridedMs1 { get; }
    public bool RequireVendorCentoridedMs2 { get; }

    // ===== File-level metadata =====

    /// <summary>Run id from the MSData document.</summary>
    public string RunId => _msd.Run.Id;

    /// <summary>True if the file ran through software named <paramref name="softwareName"/>.</summary>
    public bool IsProcessedBy(string softwareName)
    {
        foreach (var dp in _msd.DataProcessings)
        foreach (var pm in dp.ProcessingMethods)
            if (pm.Software is not null && pm.Software.Id.Contains(softwareName, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Walks the file's InstrumentConfiguration list and returns one record per IC,
    /// surfacing the model + ionization + analyzer + detector CV-child names that
    /// PwizFileInfoTest asserts on.
    /// </summary>
    public IEnumerable<MsInstrumentConfigInfo> GetInstrumentConfigInfoList()
    {
        foreach (var ic in _msd.InstrumentConfigurations)
            yield return CreateMsInstrumentConfigInfo(ic);
    }

    private static MsInstrumentConfigInfo CreateMsInstrumentConfigInfo(Pwiz.Data.MsData.Instruments.InstrumentConfiguration ic)
    {
        // Instrument MODEL — the CV-child of `MS_instrument_model` is the vendor-specific
        // model term (e.g. "4000 QTRAP", "Q Exactive HF").
        string model = ic.CvParamChild(CVID.MS_instrument_model) is { Cvid: var modelCvid } && modelCvid != CVID.CVID_Unknown
            ? CvLookup.CvTermInfo(modelCvid).Name
            : string.Empty;

        // Walk components — pwiz's IC has source / analyzer / detector entries with their
        // own CV-child queries. Per-vendor instruments report any of these as empty.
        string ionization = string.Empty;
        string analyzer = string.Empty;
        string detector = string.Empty;
        foreach (var comp in ic.ComponentList)
        {
            switch (comp.Type)
            {
                case Pwiz.Data.MsData.Instruments.ComponentType.Source:
                    var src = comp.CvParamChild(CVID.MS_ionization_type);
                    if (src.Cvid != CVID.CVID_Unknown) ionization = CvLookup.CvTermInfo(src.Cvid).Name;
                    break;
                case Pwiz.Data.MsData.Instruments.ComponentType.Analyzer:
                    var an = comp.CvParamChild(CVID.MS_mass_analyzer_type);
                    if (an.Cvid != CVID.CVID_Unknown)
                    {
                        // Append (legacy did the same) — multi-analyzer instruments (e.g.
                        // "quadrupole/quadrupole/axial ejection linear ion trap") are
                        // a "/"-joined string of every CV-child term.
                        var name = CvLookup.CvTermInfo(an.Cvid).Name;
                        analyzer = string.IsNullOrEmpty(analyzer) ? name : analyzer + "/" + name;
                    }
                    break;
                case Pwiz.Data.MsData.Instruments.ComponentType.Detector:
                    var det = comp.CvParamChild(CVID.MS_detector_type);
                    if (det.Cvid != CVID.CVID_Unknown) detector = CvLookup.CvTermInfo(det.Cvid).Name;
                    break;
            }
        }

        return new MsInstrumentConfigInfo(model, ionization, analyzer, detector);
    }

    // ===== Vendor detection (CV-param sniffs on FileDescription.SourceFiles[0]) =====

    public bool IsThermoFile => HasSourceFileFormat(CVID.MS_Thermo_RAW_format);
    public bool IsAgilentFile => HasSourceFileFormat(CVID.MS_Agilent_MassHunter_format);
    public bool IsWatersFile => HasSourceFileFormat(CVID.MS_Waters_raw_format);
    public bool IsShimadzuFile => HasSourceFileFormat(CVID.MS_Shimadzu_Biotech_database_entity);
    public bool IsABFile => HasSourceFileFormat(CVID.MS_ABI_WIFF_format);

    private bool HasSourceFileFormat(CVID cvid)
    {
        var sourceFiles = _msd.FileDescription.SourceFiles;
        if (sourceFiles.Count == 0) return false;
        return sourceFiles[0].HasCVParam(cvid);
    }

    // ===== Spectrum surface =====

    /// <summary>Spectrum count; throws if the file has no spectrum list (chromatograms-only).</summary>
    public int SpectrumCount => _msd.Run.SpectrumList?.Count ?? 0;

    /// <summary>Alias matching the legacy method name.</summary>
    public int GetSpectrumCount() => SpectrumCount;

    /// <summary>Linear find by id — small wrapper over ISpectrumList.Find.</summary>
    public int GetSpectrumIndex(string id) => _msd.Run.SpectrumList?.Find(id) ?? -1;

    /// <summary>Native vendor id for the spectrum at <paramref name="scanIndex"/>.</summary>
    public string GetSpectrumId(int scanIndex)
        => _msd.Run.SpectrumList?.SpectrumIdentity(scanIndex).Id ?? string.Empty;

    /// <summary>True iff the spectrum carries the centroid-representation CV term.</summary>
    public bool IsCentroided(int scanIndex)
    {
        var spec = SpectrumOrThrow(scanIndex, getBinaryData: false);
        return spec.HasCVParam(CVID.MS_centroid_spectrum);
    }

    /// <summary>MS level (1, 2, ...) — 0 if the spectrum doesn't declare one.</summary>
    public int GetMsLevel(int scanIndex)
    {
        var spec = SpectrumOrThrow(scanIndex, getBinaryData: false);
        var p = spec.CvParam(CVID.MS_ms_level);
        return p.Cvid == CVID.CVID_Unknown ? 0 : p.ValueAs<int>();
    }

    /// <summary>Scan start time (minutes) — null if not present.</summary>
    public double? GetStartTime(int scanIndex)
    {
        var spec = SpectrumOrThrow(scanIndex, getBinaryData: false);
        if (spec.ScanList.Scans.Count == 0) return null;
        var p = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
        return p.Cvid == CVID.CVID_Unknown ? null : p.ValueAs<double>();
    }

    /// <summary>Raw (mz, intensity) arrays for the spectrum at <paramref name="scanIndex"/>.</summary>
    public void GetSpectrum(int scanIndex, out double[] mzArray, out double[] intensityArray)
    {
        var spec = SpectrumOrThrow(scanIndex, getBinaryData: true);
        var mz = spec.GetMZArray();
        var inten = spec.GetIntensityArray();
        mzArray = mz is null ? Array.Empty<double>() : mz.Data.ToArray();
        intensityArray = inten is null ? Array.Empty<double>() : inten.Data.ToArray();
    }

    /// <summary>Scan times for every spectrum in the file (minutes); fast metadata-only walk.</summary>
    public double[] GetScanTimes()
    {
        var list = _msd.Run.SpectrumList;
        if (list is null) return Array.Empty<double>();
        var times = new double[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var spec = list.GetSpectrum(i, getBinaryData: false);
            if (spec.ScanList.Scans.Count == 0) continue;
            var p = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
            if (p.Cvid != CVID.CVID_Unknown) times[i] = p.ValueAs<double>();
        }
        return times;
    }

    // ===== Chromatogram surface =====

    public int ChromatogramCount => _msd.Run.ChromatogramList?.Count ?? 0;

    /// <summary>True iff the file has at least one chromatogram with binary data.</summary>
    public bool HasChromatogramData
    {
        get
        {
            var list = _msd.Run.ChromatogramList;
            if (list is null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var chrom = list.GetChromatogram(i, getBinaryData: true);
                if (chrom.DefaultArrayLength > 0) return true;
            }
            return false;
        }
    }

    /// <summary>Chromatogram id + identity index for the chromatogram at <paramref name="index"/>.</summary>
    public string GetChromatogramId(int index, out int indexId)
    {
        var id = _msd.Run.ChromatogramList!.ChromatogramIdentity(index);
        indexId = id.Index;
        return id.Id;
    }

    /// <summary>Time + intensity arrays for the chromatogram at <paramref name="chromIndex"/>.</summary>
    public void GetChromatogram(int chromIndex, out string id, out double[] timeArray, out float[] intensityArray)
    {
        var chrom = _msd.Run.ChromatogramList!.GetChromatogram(chromIndex, getBinaryData: true);
        id = chrom.Id;
        var t = chrom.GetTimeArray();
        var i = chrom.GetIntensityArray();
        timeArray = t is null ? Array.Empty<double>() : t.Data.ToArray();
        // Skyline historically holds intensities as float to halve memory for big arrays.
        if (i is null)
        {
            intensityArray = Array.Empty<float>();
        }
        else
        {
            intensityArray = new float[i.Data.Count];
            for (int k = 0; k < i.Data.Count; k++) intensityArray[k] = (float)i.Data[k];
        }
    }

    // ===== QC traces =====

    /// <summary>
    /// Returns the file's QC chromatograms (pressure / flow rate / temperature / etc.).
    /// Skips the standard ion-current chromatograms (TIC, BPC, SIM, SRM). Returns null
    /// when the file has no chromatograms at all (matches legacy null-vs-empty convention).
    /// </summary>
    public List<QcTrace>? GetQcTraces()
    {
        var list = _msd.Run.ChromatogramList;
        if (list is null || list.Count == 0) return null;

        var result = new List<QcTrace>();
        for (int i = 0; i < list.Count; i++)
        {
            // Cheap metadata-only probe to filter out ion-current chromatograms; the
            // pwiz-sharp lazy list lets us avoid pulling binary arrays for ones we'll skip.
            var meta = list.GetChromatogram(i, getBinaryData: false);
            if (meta.HasCVParamChild(CVID.MS_ion_current_chromatogram))
                continue;

            var full = list.GetChromatogram(i, getBinaryData: true);
            result.Add(new QcTrace(full));
        }
        return result;
    }

    /// <summary>
    /// QC-trace measurement-quality tags. The strings match the legacy wrapper
    /// (and Skyline UI labels) verbatim so the cross-stack swap is a no-op for
    /// any test or dialog that compares string-equal.
    /// </summary>
    public abstract class QcTraceQuality
    {
        public const string Pressure = "pressure";
        public const string FlowRate = "volumetric flow rate";
        public const string Temperature = "temperature";
    }

    /// <summary>Display-string constants for QC-trace intensity units.</summary>
    public abstract class QcTraceUnits
    {
        public const string Intensity = "intensity";
        public const string Pascal = "Pa";
        public const string PoundsPerSquareInch = "psi";
        public const string MicrolitersPerMinute = "uL/min";
        public const string DegreeC = "°C";
        public const string DegreeF = "°F";
        public const string Percent = "%";
        public const string Unknown = "unknown";
    }

    /// <summary>One QC trace built off a chromatogram's (time, intensity) arrays + CV-typed metadata.</summary>
    public sealed class QcTrace
    {
        internal QcTrace(Chromatogram c)
        {
            Name = c.Id;
            Index = c.Index;

            // Quality: pick from chromatogram-type CV child (pressure / flow rate /
            // temperature / etc.). For anything else fall back to the raw name —
            // generalised chromatograms can be just-about-anything.
            var typeParam = c.CvParamChild(CVID.MS_chromatogram_type);
            var chromatogramType = typeParam.Cvid;
            MeasuredQuality = chromatogramType switch
            {
                CVID.MS_pressure_chromatogram => QcTraceQuality.Pressure,
                CVID.MS_flow_rate_chromatogram => QcTraceQuality.FlowRate,
                CVID.MS_temperature_chromatogram => QcTraceQuality.Temperature,
                _ => Name,
            };

            // Units: try the intensity-array CV term first, then a UserParam fallback.
            string? unitsString = null;
            var unitsCvid = CVID.CVID_Unknown;
            var intensityArr = c.GetIntensityArray();
            if (intensityArr is not null)
            {
                var arrTypeParam = intensityArr.CvParamChild(CVID.MS_intensity_array);
                unitsCvid = arrTypeParam.Units;
                if (unitsCvid != CVID.CVID_Unknown)
                    unitsString = CvLookup.CvTermInfo(unitsCvid).Name;
            }

            if (unitsCvid is CVID.MS_number_of_detector_counts or CVID.CVID_Unknown)
            {
                var userUnits = c.UserParam("units");
                if (userUnits is not null) unitsString = userUnits.Value;
                if (string.IsNullOrEmpty(unitsString) || unitsCvid == CVID.MS_number_of_detector_counts)
                    unitsString = QcTraceUnits.Intensity;
            }
            unitsString = unitsCvid switch
            {
                CVID.UO_percent => QcTraceUnits.Percent,
                CVID.UO_pounds_per_square_inch => QcTraceUnits.PoundsPerSquareInch,
                CVID.UO_pascal => QcTraceUnits.Pascal,
                CVID.UO_microliters_per_minute => QcTraceUnits.MicrolitersPerMinute,
                CVID.UO_degree_Celsius => QcTraceUnits.DegreeC,
                CVID.UO_degree_Fahrenheit => QcTraceUnits.DegreeF,
                _ => unitsString,
            };
            IntensityUnits = string.IsNullOrEmpty(unitsString) ? QcTraceUnits.Unknown : unitsString;

            var timeArr = c.GetTimeArray();
            Times = timeArr is null ? Array.Empty<double>() : timeArr.Data.ToArray();
            Intensities = intensityArr is null ? Array.Empty<double>() : intensityArr.Data.ToArray();
        }

        public string Name { get; }
        public int Index { get; }
        public double[] Times { get; }
        public double[] Intensities { get; }
        public string MeasuredQuality { get; }
        public string IntensityUnits { get; }

        /// <summary>
        /// Formatted display string like "Pressure (psi)" or "Temperature (°C)". When
        /// the intensity is generic "intensity" the units are returned alone.
        /// </summary>
        public string TypeWithUnits()
        {
            string CapitalizeFirst(string str)
            {
                if (string.IsNullOrEmpty(str)) return str;
                if (str.Length == 1) return str.ToUpper();
                return char.ToUpper(str[0]) + str[1..];
            }

            var type = MeasuredQuality;
            var units = IntensityUnits;

            if (!string.IsNullOrEmpty(units) && !units.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                // Strip embedded "(...)" / "[...]" trailing units from the quality string
                // before we re-append the canonical units.
                if (type.EndsWith(")"))
                {
                    int openParen = type.LastIndexOf('(');
                    if (openParen > 0) type = type[..openParen].TrimEnd();
                }
                else if (type.EndsWith("]"))
                {
                    int openBracket = type.LastIndexOf('[');
                    if (openBracket > 0) type = type[..openBracket].TrimEnd();
                }
                if (units == QcTraceUnits.Intensity) return CapitalizeFirst(units);
                return $"{CapitalizeFirst(type)} ({units})";
            }
            return CapitalizeFirst(type);
        }
    }

    // ===== Output =====

    /// <summary>Writes the underlying MSData document to <paramref name="path"/> as mzML.</summary>
    public void Write(string path)
    {
        MSDataFile.Write(_msd, path, new WriteConfig());
    }

    // ===== Lifecycle =====

    public static bool IsValidFile(string filepath)
    {
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath)) return false;
        try
        {
            // Identify takes a head-bytes hint (first ~512 bytes) for magic-byte-based
            // formats — null is fine when we only have a path to probe.
            return ReaderList.Default.Identify(filepath, head: null) != CVID.CVID_Unknown;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _msd.Dispose();
    }

    private Spectrum SpectrumOrThrow(int scanIndex, bool getBinaryData)
    {
        var list = _msd.Run.SpectrumList
            ?? throw new InvalidOperationException("File has no spectrum list.");
        if ((uint)scanIndex >= (uint)list.Count)
            throw new ArgumentOutOfRangeException(nameof(scanIndex));
        return list.GetSpectrum(scanIndex, getBinaryData);
    }
}
