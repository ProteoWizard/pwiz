using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Vendor-agnostic wrapper over a Bruker <c>.d</c> analysis. Port of pwiz C++
/// <c>CompassData</c> (renamed to <c>BrukerData</c> here because "Compass" refers to Bruker's
/// older acquisition software, not TDF / TSF): hides the TDF / TSF (and eventually BAF / FID /
/// YEP) differences so <see cref="SpectrumList_Bruker"/> and <see cref="ChromatogramList_Bruker"/>
/// can be format-agnostic.
/// </summary>
/// <remarks>
/// Owns the underlying metadata and binary-data handles and disposes them together. Create
/// instances via <see cref="BrukerData.Create"/>.
/// </remarks>
public interface IBrukerData : IDisposable
{
    /// <summary>Which Bruker sub-format this wrapper is backed by.</summary>
    BrukerFormat Format { get; }

    /// <summary>Absolute path of the opened <c>.d</c> directory.</summary>
    string AnalysisDirectory { get; }

    /// <summary>Raw <c>GlobalMetadata</c> key/value table from the analysis SQLite db.</summary>
    IReadOnlyDictionary<string, string> GlobalMetadata { get; }

    /// <summary>True if the analysis has PASEF data (TDF only).</summary>
    bool HasPasefData { get; }

    /// <summary>True if the analysis has DIA-PASEF data (TDF only).</summary>
    bool HasDiaPasefData { get; }

    /// <summary>True if the source has any MS1 frames.</summary>
    bool HasMs1Frames { get; }

    /// <summary>True if the source has any MS2+ frames.</summary>
    bool HasMsNFrames { get; }

    /// <summary>Acquired m/z range (from <c>MzAcqRangeLower/Upper</c> global params).</summary>
    (double Low, double High) MzAcquisitionRange { get; }

    /// <summary>
    /// True when the first frame's scan mode or instrument source indicates MALDI (used for
    /// the source CV term in <c>Reader_Bruker</c>).
    /// </summary>
    bool IsMaldiSource { get; }

    /// <summary>Per-frame DIA-PASEF isolation windows (TDF DIA only; empty otherwise).</summary>
    IEnumerable<DiaFrameWindow> EnumerateDiaFrameWindows();

    /// <summary>
    /// Builds the flat spectrum index. Each <see cref="BrukerIndexEntry"/> holds the id + an
    /// opaque per-format tag that <see cref="FillSpectrum"/> consumes to materialize the
    /// spectrum's metadata and peaks.
    /// </summary>
    IReadOnlyList<BrukerIndexEntry> BuildSpectrumIndex(
        bool combineIonMobilitySpectra,
        int preferOnlyMsLevel);

    /// <summary>
    /// Populates <paramref name="spec"/> with all metadata for the entry and, if
    /// <paramref name="getBinaryData"/> is true, its peak arrays. The existing <c>Index</c> /
    /// <c>Id</c> on <paramref name="spec"/> are left untouched.
    /// </summary>
    void FillSpectrum(Spectrum spec, BrukerIndexEntry entry, bool getBinaryData, bool preferCentroid);

    /// <summary>
    /// Yields one point per frame for the TIC / BPC chromatograms, honoring
    /// <paramref name="preferOnlyMsLevel"/> (0 = all, 1 = MS1 only, 2 = MS2+ only).
    /// </summary>
    IEnumerable<BrukerChromatogramPoint> EnumerateChromatogramPoints(int preferOnlyMsLevel);

    /// <summary>Reads the LC traces (pressure, flow, UV, etc.) from <c>chromatography-data.sqlite</c>.</summary>
    List<LcTrace> ReadLcTraces();
}

/// <summary>One entry in the flat spectrum index built by <see cref="IBrukerData.BuildSpectrumIndex"/>.</summary>
public sealed class BrukerIndexEntry : SpectrumIdentity
{
    /// <summary>
    /// Format-specific payload interpreted by the concrete <see cref="IBrukerData"/> impl. Public
    /// so derived classes can stash their state; clients treat it as opaque.
    /// </summary>
    public object Tag { get; set; } = null!;

    /// <summary>1 for MS1, 2 for MSn — exposed so chromatogram callers can build a per-point
    /// ms-level array without round-tripping through full spectrum metadata.</summary>
    public int MsLevel { get; set; } = 1;
}

/// <summary>One point in a TIC / BPC chromatogram.</summary>
/// <param name="RetentionTimeSeconds">Frame retention time.</param>
/// <param name="TotalIonCurrent">Summed intensity (frame TIC).</param>
/// <param name="BasePeakIntensity">Max intensity (frame BPI).</param>
/// <param name="MsLevel">1 for MS1, 2 for MS2+ — emitted into the <c>ms level</c> integer array.</param>
public readonly record struct BrukerChromatogramPoint(
    double RetentionTimeSeconds,
    double TotalIonCurrent,
    double BasePeakIntensity,
    int MsLevel);
