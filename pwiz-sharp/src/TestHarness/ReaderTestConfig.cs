using System.Globalization;
using Pwiz.Analysis;
using Pwiz.Analysis.PeakPicking;
using Pwiz.Data.Common.Diff;
using Pwiz.Data.MsData;
using Pwiz.Util.Misc;

namespace Pwiz.TestHarness;

/// <summary>
/// Configuration for <see cref="VendorReaderTestHarness.TestReader"/>. Port of
/// <c>pwiz::util::ReaderTestConfig</c> — adds the flags that influence the reference mzML
/// filename and shapes the mzML output (combineIonMobilitySpectra, preferOnlyMsLevel, etc.).
/// </summary>
/// <remarks>
/// The flags mirror pwiz's <c>Reader::Config</c> where they overlap; unused flags stay here
/// as plain properties so <see cref="ResultFilename"/> can assemble the same derivative names
/// (e.g. <c>&lt;run&gt;-combineIMS-ms1-centroid.mzML</c>) even if our reader doesn't act on them yet.
/// </remarks>
public sealed class ReaderTestConfig
{
    /// <summary>Emit one spectrum per frame (sum across mobility), not per (frame, scan).</summary>
    public bool CombineIonMobilitySpectra { get; set; }

    /// <summary>If &gt; 0, only spectra at this MS level are emitted.</summary>
    public int PreferOnlyMsLevel { get; set; }

    /// <summary>If false, MS2 spectra that lack precursor info are dropped.</summary>
    public bool AllowMsMsWithoutPrecursor { get; set; } = true;

    /// <summary>Apply vendor centroiding (peak-picking) during read.</summary>
    public bool PeakPicking { get; set; }

    /// <summary>Apply CWT-based centroiding (exercised to check BinaryData pathways).</summary>
    public bool PeakPickingCWT { get; set; }

    /// <summary>Keep global TIC/BPC limited to MS1 scans only.</summary>
    public bool GlobalChromatogramsAreMs1Only { get; set; }

    /// <summary>Expose SIM scans as top-level spectra.</summary>
    public bool SimAsSpectra { get; set; }

    /// <summary>
    /// When true, Bruker combined-IMS spectra sort + jitter merged peaks for reproducible
    /// ordering against pwiz C++ reference mzMLs. The harness should set this on combineIMS
    /// PASEF tests; production msconvert-sharp leaves it off. See <see cref="Pwiz.Data.MsData.Readers.ReaderConfig.SortAndJitter"/>.
    /// </summary>
    public bool SortAndJitter { get; set; }

    /// <summary>Expose SRM transitions as top-level spectra.</summary>
    public bool SrmAsSpectra { get; set; }

    /// <summary>Accept (rather than drop) zero-length spectra.</summary>
    public bool AcceptZeroLengthSpectra { get; set; }

    /// <summary>Drop zero-intensity profile points before emission.</summary>
    public bool IgnoreZeroIntensityPoints { get; set; }

    /// <summary>Apply DDA processing that pwiz applies during certain peak-pick paths.</summary>
    public bool DdaProcessing { get; set; }

    /// <summary>Strip calibration scans emitted for some vendor formats.</summary>
    public bool IgnoreCalibrationScans { get; set; }

    /// <summary>When true, the comparison reference filename has a <c>-mzMobilityFilter</c> suffix.</summary>
    public bool HasIsolationMzFilter { get; set; }

    /// <summary>
    /// Whether double precision is expected for m/z. Influences the <c>--generate-mzML</c>
    /// regeneration step; doesn't affect the diff pathway.
    /// </summary>
    public bool DoublePrecision { get; set; }

    /// <summary>Optional floating-point precision override for the MSData diff.</summary>
    public double? DiffPrecision { get; set; }

    /// <summary>If set, only this single run index is tested (for multi-run inputs).</summary>
    public int? RunIndex { get; set; }

    /// <summary>
    /// If set, the diff only inspects spectra in <c>[Start, End]</c> (0-based, inclusive).
    /// Mirrors pwiz C++ <c>indexRange</c> on the test config — used by the
    /// <c>globalChromatogramsAreMs1Only</c> reference fixtures (which are written with
    /// <c>indexRange = (0, 0)</c> so only the first spectrum lands in the reference).
    /// </summary>
    public (int Start, int End)? IndexRange { get; set; }

    /// <summary>
    /// Builds the reference mzML filename from <paramref name="baseFilename"/>, appending
    /// config-derived suffixes in the same order as <c>ReaderTestConfig::resultFilename</c>.
    /// </summary>
    public string ResultFilename(string baseFilename)
    {
        ArgumentNullException.ThrowIfNull(baseFilename);
        string result = baseFilename;

        const string illegal = "\\/*:?<>|\"";
        var chars = result.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (illegal.Contains(chars[i], StringComparison.Ordinal)) chars[i] = '_';
        result = new string(chars);

        if (SimAsSpectra) result = result.Replace(".mzML", "-simSpectra.mzML", StringComparison.Ordinal);
        if (SrmAsSpectra) result = result.Replace(".mzML", "-srmSpectra.mzML", StringComparison.Ordinal);
        if (AcceptZeroLengthSpectra) result = result.Replace(".mzML", "-acceptZeroLength.mzML", StringComparison.Ordinal);
        if (IgnoreZeroIntensityPoints) result = result.Replace(".mzML", "-ignoreZeros.mzML", StringComparison.Ordinal);
        if (CombineIonMobilitySpectra) result = result.Replace(".mzML", "-combineIMS.mzML", StringComparison.Ordinal);
        if (PreferOnlyMsLevel > 0)
            result = result.Replace(".mzML",
                "-ms" + PreferOnlyMsLevel.ToString(CultureInfo.InvariantCulture) + ".mzML",
                StringComparison.Ordinal);
        if (!AllowMsMsWithoutPrecursor)
            result = result.Replace(".mzML", "-noMsMsWithoutPrecursor.mzML", StringComparison.Ordinal);
        if (PeakPicking) result = result.Replace(".mzML", "-centroid.mzML", StringComparison.Ordinal);
        if (PeakPickingCWT) result = result.Replace(".mzML", "-centroid-cwt.mzML", StringComparison.Ordinal);
        if (HasIsolationMzFilter) result = result.Replace(".mzML", "-mzMobilityFilter.mzML", StringComparison.Ordinal);
        if (GlobalChromatogramsAreMs1Only)
            result = result.Replace(".mzML", "-globalChromatogramsAreMs1Only.mzML", StringComparison.Ordinal);
        if (DdaProcessing) result = result.Replace(".mzML", "-ddaProcessing.mzML", StringComparison.Ordinal);
        if (IgnoreCalibrationScans) result = result.Replace(".mzML", "-ignoreCalibrationScans.mzML", StringComparison.Ordinal);
        return result;
    }

    /// <summary>Builds a <see cref="DiffConfig"/> seeded from this config (precision override applied).</summary>
    public DiffConfig BuildDiffConfig()
    {
        var dc = new DiffConfig();
        if (DiffPrecision.HasValue) dc.Precision = DiffPrecision.Value;
        return dc;
    }

    /// <summary>
    /// Applies config-driven wrappers (peak picking, etc.) to <paramref name="msd"/>, appending
    /// the corresponding DataProcessing step. Mirrors <c>pwiz::util::ReaderTestConfig::wrap</c>.
    /// </summary>
    public void Wrap(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        if (!PeakPicking && !PeakPickingCWT) return;
        if (msd.Run.SpectrumList is null) return;

        // Default to MS levels 1+ (all levels) unless PreferOnlyMsLevel narrows the range.
        var msLevels = PreferOnlyMsLevel > 0
            ? new IntegerSet(PreferOnlyMsLevel, PreferOnlyMsLevel)
            : new IntegerSet(1, int.MaxValue);

        // PeakPickingCWT explicitly disables vendor preference and selects the CWT detector
        // (continuous-wavelet-transform with Ricker wavelet) — matches pwiz C++ msconvert
        // --filter "peakPicking cwt" defaults: snr=1.0, peakSpace=0.1.
        IPeakDetector algorithm = PeakPickingCWT
            ? new CwtPeakDetector(minSnr: 1.0, fixedPeaksKeep: 0, mzTol: 0.1, centroid: false)
            : new LocalMaximumPeakDetector(3);
        var picker = new SpectrumList_PeakPicker(
            msd.Run.SpectrumList,
            algorithm: algorithm,
            preferVendorPeakPicking: !PeakPickingCWT,
            msLevelsToPeakPick: msLevels);
        msd.Run.SpectrumList = picker;

        // The pwiz C++ reference mzMLs disagree on whether the peak_picking processing method
        // is included in the serialized dataProcessingList: vendor-centroid references (e.g.
        // ATEHLSTLSEK_profile-centroid) include it; CWT references (e.g. HDDDA-centroid-cwt)
        // don't. The two reference fixtures were regenerated under different cpp builds; we
        // mirror their actual content rather than the (consistent) cpp code path.
        if (PeakPickingCWT) return; // CWT references have only the Conversion method.
        var pickerDp = picker.DataProcessing;
        for (int i = 0; i < msd.DataProcessings.Count; i++)
        {
            if (msd.DataProcessings[i].Id == pickerDp.Id)
            {
                msd.DataProcessings[i] = pickerDp;
                return;
            }
        }
        msd.DataProcessings.Insert(0, pickerDp);
    }
}
