namespace Pwiz.Analysis.PeakPicking;

/// <summary>
/// Interface for one-dimensional smoothing algorithms over (x, y) sample series. Port of
/// <c>pwiz::analysis::Smoother</c>.
/// </summary>
/// <remarks>
/// In the case of sparse vectors, smoothing may fill in samples that weren't in the original
/// data — callers must read the size of the output vectors after the call rather than assuming
/// it matches the input.
/// </remarks>
public interface ISmoother
{
    /// <summary>Smooths <paramref name="y"/> values over the corresponding <paramref name="x"/>
    /// axis, writing the result into <paramref name="xSmoothed"/> / <paramref name="ySmoothed"/>.</summary>
    void Smooth(IReadOnlyList<double> x, IReadOnlyList<double> y,
                List<double> xSmoothed, List<double> ySmoothed);
}
