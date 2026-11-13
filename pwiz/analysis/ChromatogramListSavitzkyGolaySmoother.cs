using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis;

/// <summary>
/// <see cref="IChromatogramList"/> wrapper that applies a Savitzky-Golay smoother to the
/// intensity array of each chromatogram. Port of
/// <c>pwiz::analysis::ChromatogramList_SavitzkyGolaySmoother</c>.
/// </summary>
/// <remarks>
/// Uses the same hardcoded quartic 9-point coefficients as cpp
/// <c>SavitzkyGolaySmoother::smooth_copy</c>:
/// <c>[-21, 14, 39, 54, 59, 54, 39, 14, -21] / 231</c>. Chromatograms with fewer than 9
/// points are returned unchanged. The first and last 4 points are passed through
/// unsmoothed (kernel doesn't fit). Note: the chromatogram-flavor smoother is byte-identical
/// to cpp; the spectrum-flavor <see cref="Pwiz.Analysis.PeakPicking.SavitzkyGolaySmoother"/>
/// uses the configurable Gram-polynomial version with zero-sample padding — different
/// algorithm, different output, different use case.
/// </remarks>
public sealed class ChromatogramListSavitzkyGolaySmoother : ChromatogramListWrapper
{
    /// <summary>Creates a smoothing wrapper. The smoother is applied lazily on each
    /// <see cref="GetChromatogram"/> call so wrapping a multi-GB chromatogram list doesn't
    /// pre-materialize anything.</summary>
    public ChromatogramListSavitzkyGolaySmoother(IChromatogramList inner) : base(inner)
    {
        // Append a smoothing ProcessingMethod to the inherited chain. cpp doesn't do this
        // in the chromatogram-SG-smoother class but every other sharp filter does, and the
        // mzML schema benefits from the trace.
        var method = new ProcessingMethod
        {
            Order = Dp.ProcessingMethods.Count,
            Software = Dp.ProcessingMethods.FirstOrDefault()?.Software,
        };
        method.Set(CVID.MS_smoothing);
        Dp.ProcessingMethods.Add(method);
    }

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        if (!getBinaryData)
            return Inner.GetChromatogram(index, getBinaryData: false);

        var c = Inner.GetChromatogram(index, getBinaryData: true);
        // cpp accesses binaryDataArrayPtrs[1]->data — second array is intensity by
        // mzML convention (first is time, second is intensity).
        var intensity = c.GetIntensityArray();
        if (intensity is not null && intensity.Data.Count >= 9)
            Smooth(intensity.Data);
        c.DataProcessing = Dp;
        return c;
    }

    // Quartic 9-point Savitzky-Golay smoother — hardcoded coefficients from cpp's
    // SavitzkyGolaySmoother.hpp:50-53. Edges (first and last 4 samples) are passed through
    // unchanged.
    private static void Smooth(List<double> data)
    {
        int n = data.Count;
        var smoothed = new double[n];
        // Copy edge samples.
        smoothed[0] = data[0]; smoothed[1] = data[1]; smoothed[2] = data[2]; smoothed[3] = data[3];
        smoothed[n - 4] = data[n - 4]; smoothed[n - 3] = data[n - 3];
        smoothed[n - 2] = data[n - 2]; smoothed[n - 1] = data[n - 1];

        for (int i = 4; i <= n - 5; i++)
        {
            double sum = 59 * data[i]
                       + 54 * (data[i - 1] + data[i + 1])
                       + 39 * (data[i - 2] + data[i + 2])
                       + 14 * (data[i - 3] + data[i + 3])
                       - 21 * (data[i - 4] + data[i + 4]);
            smoothed[i] = sum / 231.0;
        }

        for (int i = 0; i < n; i++) data[i] = smoothed[i];
    }
}
