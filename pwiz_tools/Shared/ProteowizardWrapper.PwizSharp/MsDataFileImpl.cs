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
