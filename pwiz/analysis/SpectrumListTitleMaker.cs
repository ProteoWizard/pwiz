using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis;

/// <summary>
/// Wraps an inner <see cref="ISpectrumList"/> and on read sets each spectrum's
/// <see cref="CVID.MS_spectrum_title"/> CV param to the result of substituting placeholders in
/// the configured format string with values from the spectrum. Port of pwiz
/// <c>SpectrumList_TitleMaker</c>.
/// </summary>
/// <remarks>
/// Recognized placeholders (case-sensitive, see cpp <c>filterCreator_titleMaker</c>):
/// <c>&lt;RunId&gt;</c>, <c>&lt;Index&gt;</c>, <c>&lt;Id&gt;</c>, <c>&lt;SourcePath&gt;</c>,
/// <c>&lt;ScanNumber&gt;</c>, <c>&lt;IonMobility&gt;</c>, <c>&lt;ActivationType&gt;</c>,
/// <c>&lt;IsolationMz&gt;</c>, <c>&lt;PrecursorSpectrumId&gt;</c>, <c>&lt;SelectedIonMz&gt;</c>,
/// <c>&lt;ChargeState&gt;</c>, <c>&lt;SpectrumType&gt;</c>,
/// <c>&lt;ScanStartTimeInSeconds&gt;</c>, <c>&lt;ScanStartTimeInMinutes&gt;</c>,
/// <c>&lt;BasePeakMz&gt;</c>, <c>&lt;BasePeakIntensity&gt;</c>, <c>&lt;TotalIonCurrent&gt;</c>,
/// <c>&lt;MsLevel&gt;</c>.
/// Unknown placeholders are left as literals.
/// </remarks>
public sealed class SpectrumListTitleMaker : SpectrumListWrapper
{
    private readonly string _format;
    private readonly string _runId;
    private readonly string _sourcePath;

    /// <summary>Wraps <paramref name="inner"/>; per-spectrum titles are built from
    /// <paramref name="format"/> using values pulled from <paramref name="msd"/> (run id,
    /// source path) and the spectrum itself.</summary>
    public SpectrumListTitleMaker(MSData msd, ISpectrumList inner, string format)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentException.ThrowIfNullOrEmpty(format);
        _format = format;
        _runId = msd.Run.Id ?? string.Empty;
        _sourcePath = msd.FileDescription.SourceFiles.Count > 0
            ? msd.FileDescription.SourceFiles[0].Location ?? string.Empty
            : string.Empty;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        var spec = Inner.GetSpectrum(index, getBinaryData);
        spec.Params.Set(CVID.MS_spectrum_title, BuildTitle(spec));
        return spec;
    }

    private string BuildTitle(Spectrum spec)
    {
        string title = _format;
        title = title.Replace("<RunId>", _runId, StringComparison.Ordinal);
        title = title.Replace("<Index>", spec.Index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        title = title.Replace("<Id>", spec.Id, StringComparison.Ordinal);
        title = title.Replace("<SourcePath>", _sourcePath, StringComparison.Ordinal);
        title = title.Replace("<ScanNumber>", ScanNumber(spec), StringComparison.Ordinal);
        title = title.Replace("<IonMobility>", FirstScanCvDouble(spec, CVID.MS_ion_mobility_drift_time)
                                                ?? FirstScanCvDouble(spec, CVID.MS_inverse_reduced_ion_mobility)
                                                ?? string.Empty,
                              StringComparison.Ordinal);
        title = title.Replace("<ActivationType>", ActivationType(spec), StringComparison.Ordinal);
        title = title.Replace("<IsolationMz>", IsolationMz(spec), StringComparison.Ordinal);
        title = title.Replace("<PrecursorSpectrumId>", PrecursorSpectrumId(spec), StringComparison.Ordinal);
        title = title.Replace("<SelectedIonMz>", SelectedIonMz(spec), StringComparison.Ordinal);
        title = title.Replace("<ChargeState>", ChargeState(spec), StringComparison.Ordinal);
        title = title.Replace("<SpectrumType>", SpectrumType(spec), StringComparison.Ordinal);
        title = title.Replace("<ScanStartTimeInSeconds>", ScanStartTime(spec, minutes: false), StringComparison.Ordinal);
        title = title.Replace("<ScanStartTimeInMinutes>", ScanStartTime(spec, minutes: true), StringComparison.Ordinal);
        title = title.Replace("<BasePeakMz>", CvDouble(spec.Params.CvParam(CVID.MS_base_peak_m_z)), StringComparison.Ordinal);
        title = title.Replace("<BasePeakIntensity>", CvDouble(spec.Params.CvParam(CVID.MS_base_peak_intensity)), StringComparison.Ordinal);
        title = title.Replace("<TotalIonCurrent>", CvDouble(spec.Params.CvParam(CVID.MS_total_ion_current)), StringComparison.Ordinal);
        title = title.Replace("<MsLevel>",
            spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0).ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
        return title;
    }

    private static string ScanNumber(Spectrum spec)
    {
        // cpp prefers the native "scan=NNN" id when the id parses to a single integer,
        // else falls back to (index+1).
        string scan = Id.Value(spec.Id, "scan");
        if (!string.IsNullOrEmpty(scan)) return scan;
        return (spec.Index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static string? FirstScanCvDouble(Spectrum spec, CVID cvid)
    {
        if (spec.ScanList.Scans.Count == 0) return null;
        var p = spec.ScanList.Scans[0].CvParam(cvid);
        return p.IsEmpty ? null : p.ValueAs<double>().ToString(CultureInfo.InvariantCulture);
    }

    private static string ActivationType(Spectrum spec)
    {
        if (spec.Precursors.Count == 0) return string.Empty;
        var p = spec.Precursors[0].Activation.CvParamChild(CVID.MS_dissociation_method);
        return p.IsEmpty ? string.Empty : CvLookup.CvTermInfo(p.Cvid).Name;
    }

    private static string IsolationMz(Spectrum spec)
    {
        if (spec.Precursors.Count == 0) return string.Empty;
        var p = spec.Precursors[0].IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z);
        return p.IsEmpty ? string.Empty : p.ValueAs<double>().ToString(CultureInfo.InvariantCulture);
    }

    private static string PrecursorSpectrumId(Spectrum spec)
    {
        if (spec.Precursors.Count == 0) return string.Empty;
        return spec.Precursors[0].SpectrumId ?? string.Empty;
    }

    private static string SelectedIonMz(Spectrum spec)
    {
        if (spec.Precursors.Count == 0 || spec.Precursors[0].SelectedIons.Count == 0) return string.Empty;
        var p = spec.Precursors[0].SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z);
        return p.IsEmpty ? string.Empty : p.ValueAs<double>().ToString(CultureInfo.InvariantCulture);
    }

    private static string ChargeState(Spectrum spec)
    {
        if (spec.Precursors.Count == 0 || spec.Precursors[0].SelectedIons.Count == 0) return string.Empty;
        var p = spec.Precursors[0].SelectedIons[0].CvParam(CVID.MS_charge_state);
        return p.IsEmpty ? string.Empty : p.ValueAs<int>().ToString(CultureInfo.InvariantCulture);
    }

    private static string SpectrumType(Spectrum spec)
    {
        var p = spec.Params.CvParamChild(CVID.MS_spectrum_type);
        return p.IsEmpty ? string.Empty : CvLookup.CvTermInfo(p.Cvid).Name;
    }

    private static string ScanStartTime(Spectrum spec, bool minutes)
    {
        if (spec.ScanList.Scans.Count == 0) return string.Empty;
        var p = spec.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
        if (p.IsEmpty) return string.Empty;
        double seconds = p.TimeInSeconds();
        double v = minutes ? seconds / 60.0 : seconds;
        return v.ToString(CultureInfo.InvariantCulture);
    }

    private static string CvDouble(CVParam p) =>
        p.IsEmpty ? string.Empty : p.ValueAs<double>().ToString(CultureInfo.InvariantCulture);
}
