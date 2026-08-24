namespace Pwiz.Analysis.PeakPicking;

/// <summary>
/// Finds all local maxima: any point whose y value is strictly greater than its
/// <c>flank</c> neighbours on both sides (<c>flank = (windowSize − 1) / 2</c>). Port of
/// <c>pwiz::analysis::LocalMaximumPeakDetector</c>.
/// </summary>
/// <remarks>
/// Input is padded with flanking zero samples via <see cref="ZeroSampleFiller"/>. Because a
/// window full of zeros always smooths to zero, we only need <c>flank+1</c> zeros per flank.
/// </remarks>
public sealed class LocalMaximumPeakDetector : IPeakDetector
{
    private readonly int _window;

    /// <summary>Creates a detector with the given (odd) window size.</summary>
    public LocalMaximumPeakDetector(int windowSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowSize, 1);
        _window = windowSize;
    }

    /// <inheritdoc/>
    public string Name => "local maximum peak picker";

    /// <inheritdoc/>
    public void Detect(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        List<double> xPeakValues,
        List<double> yPeakValues,
        List<Peak>? peaks = null)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(xPeakValues);
        ArgumentNullException.ThrowIfNull(yPeakValues);
        if (x.Count != y.Count)
            throw new ArgumentException("x and y arrays must be the same size.");
        if (x.Count == 0) return;

        int flank = (_window - 1) / 2;

        var xFilled = new List<double>(x.Count);
        var yFilled = new List<double>(y.Count);
        ZeroSampleFiller.Fill(x, y, xFilled, yFilled, flank + 1);

        for (int i = flank, end = yFilled.Count - flank; i < end; i++)
        {
            bool isPeak = true;
            for (int j = 1; j <= flank; j++)
            {
                if (yFilled[i] < yFilled[i - j] || yFilled[i] < yFilled[i + j])
                {
                    isPeak = false;
                    break;
                }
            }
            if (isPeak)
            {
                xPeakValues.Add(xFilled[i]);
                yPeakValues.Add(yFilled[i]);
            }
        }

        if (peaks is null) return;
        peaks.Clear();
        peaks.Capacity = Math.Max(peaks.Capacity, xPeakValues.Count);
        for (int i = 0; i < xPeakValues.Count; i++)
            peaks.Add(new Peak(xPeakValues[i], yPeakValues[i]));
    }
}
