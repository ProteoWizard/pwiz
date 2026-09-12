namespace Pwiz.Analysis.PeakPicking;

/// <summary>Metadata for one detected peak (x, y, and optional profile bounds / area).</summary>
public readonly record struct Peak(double X, double Y, double Start = 0, double Stop = 0, double Area = 0);

/// <summary>
/// Interface for peak detection algorithms that map a profile signal (x[], y[]) to discrete
/// peaks. Port of <c>pwiz::analysis::PeakDetector</c>.
/// </summary>
public interface IPeakDetector
{
    /// <summary>Short human-readable name for this detector (emitted into DataProcessing userParams).</summary>
    string Name { get; }

    /// <summary>
    /// Finds peaks in the signal defined by <paramref name="x"/> / <paramref name="y"/>.
    /// Populates <paramref name="xPeakValues"/> and <paramref name="yPeakValues"/> with one
    /// entry per detected peak; optionally fills <paramref name="peaks"/> with richer metadata.
    /// </summary>
    void Detect(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        List<double> xPeakValues,
        List<double> yPeakValues,
        List<Peak>? peaks = null);
}
