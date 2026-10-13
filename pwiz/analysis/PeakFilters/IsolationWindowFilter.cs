using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.PeakFilters;

/// <summary>
/// Keeps only the peaks of a spectrum that fall inside isolation windows: one given window, or the
/// windows its product spectra isolated from it (the spectra that follow it in the list, up to the
/// next one at its own MS level). Port of pwiz cpp's <c>IsolationWindowFilter</c>
/// (<c>pwiz/analysis/spectrum_processing/SpectrumList_PeakFilter.cpp</c>).
/// </summary>
/// <remarks>
/// A window without a lower or upper offset extends <c>defaultWindowWidth</c> to that side of its
/// target m/z. Overlapping windows are merged, so a peak inside two of them is kept once. Spectra
/// without an MS level, or without any window to apply, pass through unchanged.
/// </remarks>
public sealed class IsolationWindowFilter : ISpectrumDataFilter
{
    private readonly double _defaultWindowWidth;
    private readonly ISpectrumList? _spectrumList;
    private readonly IsolationWindow? _window;

    /// <summary>Filters each spectrum to the isolation windows of its product spectra in
    /// <paramref name="spectrumList"/>.</summary>
    public IsolationWindowFilter(double defaultWindowWidth, ISpectrumList spectrumList)
    {
        ArgumentNullException.ThrowIfNull(spectrumList);
        _defaultWindowWidth = defaultWindowWidth;
        _spectrumList = spectrumList;
    }

    /// <summary>Filters every spectrum to <paramref name="window"/>.</summary>
    public IsolationWindowFilter(double defaultWindowWidth, IsolationWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _defaultWindowWidth = defaultWindowWidth;
        _window = window;
    }

    /// <inheritdoc/>
    public void Apply(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        int precursorMsLevel = spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        if (precursorMsLevel == 0)
            return;

        var windows = new List<(double Start, double Stop)>();
        if (_spectrumList is not null)
        {
            for (int i = spectrum.Index + 1; i < _spectrumList.Count; ++i)
            {
                var product = _spectrumList.GetSpectrum(i, DetailLevel.FullMetadata);
                int productMsLevel = product.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
                if (productMsLevel == 0)
                    continue;
                if (productMsLevel == precursorMsLevel)
                    break;
                if (product.Precursors.Count == 0 || product.Precursors[0].IsolationWindow.IsEmpty)
                    continue;
                windows.Add(Bounds(product.Precursors[0].IsolationWindow));
            }
        }
        else
            windows.Add(Bounds(_window!));

        var mzArray = spectrum.GetMZArray();
        var intensityArray = spectrum.GetIntensityArray();
        if (windows.Count == 0 || mzArray is null || intensityArray is null)
            return;

        var mz = mzArray.Data;
        var intensity = intensityArray.Data;
        var filteredMz = new List<double>();
        var filteredIntensity = new List<double>();
        foreach (var (start, stop) in Merge(windows))
        {
            int first = LowerBound(mz, start);
            int last = UpperBound(mz, stop);
            for (int i = first; i < last; ++i)
            {
                filteredMz.Add(mz[i]);
                filteredIntensity.Add(intensity[i]);
            }
        }

        CVID intensityUnits = CVID.MS_number_of_detector_counts;
        foreach (var p in intensityArray.CVParams)
            if (p.Units != CVID.CVID_Unknown) { intensityUnits = p.Units; break; }
        spectrum.SetMZIntensityArrays(filteredMz, filteredIntensity, intensityUnits);
    }

    private (double Start, double Stop) Bounds(IsolationWindow window)
    {
        double target = window.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>();
        return (target - window.CvParamValueOrDefault(CVID.MS_isolation_window_lower_offset, _defaultWindowWidth),
                target + window.CvParamValueOrDefault(CVID.MS_isolation_window_upper_offset, _defaultWindowWidth));
    }

    /// <summary>Sorts closed intervals and joins the ones that overlap or touch.</summary>
    private static List<(double Start, double Stop)> Merge(List<(double Start, double Stop)> windows)
    {
        windows.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<(double Start, double Stop)> { windows[0] };
        for (int i = 1; i < windows.Count; ++i)
        {
            var last = merged[^1];
            if (windows[i].Start <= last.Stop)
                merged[^1] = (last.Start, Math.Max(last.Stop, windows[i].Stop));
            else
                merged.Add(windows[i]);
        }
        return merged;
    }

    /// <summary>First index whose value is not less than <paramref name="value"/>.</summary>
    private static int LowerBound(List<double> sorted, double value)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (sorted[mid] < value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>First index whose value is greater than <paramref name="value"/>.</summary>
    private static int UpperBound(List<double> sorted, double value)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (sorted[mid] <= value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
