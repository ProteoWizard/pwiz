namespace Pwiz.Analysis.PeakPicking;

/// <summary>
/// Fills missing zero samples around signal profiles so peak-picking algorithms see a well-formed
/// flanking context. Port of <c>pwiz::analysis::ZeroSampleFiller</c>.
/// </summary>
/// <remarks>
/// The pre-condition: the sample rate may change but must change gradually, and there is at
/// least one zero sample on each side of every non-zero run (so the first-order delta can be
/// read off existing samples without extrapolation).
/// </remarks>
public static class ZeroSampleFiller
{
    /// <summary>
    /// Populates <paramref name="xFilled"/> / <paramref name="yFilled"/> with the input samples
    /// plus up to <paramref name="zeroSampleCount"/> zero samples on each flank of every
    /// non-zero run. Existing samples are preserved.
    /// </summary>
    public static void Fill(
        IReadOnlyList<double> x,
        IReadOnlyList<double> y,
        List<double> xFilled,
        List<double> yFilled,
        int zeroSampleCount)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(xFilled);
        ArgumentNullException.ThrowIfNull(yFilled);
        if (x.Count != y.Count)
            throw new ArgumentException("x and y arrays must be the same size.");

        xFilled.Clear();
        yFilled.Clear();
        xFilled.AddRange(x);
        yFilled.AddRange(y);

        // Scan backward, inserting flanking zeros to the RIGHT of the last non-zero sample of
        // each run (where the run transitions from data → zero as we walk from right to left).
        bool wasInData = false;
        for (int i = yFilled.Count - 1; i >= 0; i--)
        {
            bool nowInData = yFilled[i] > 0.0;
            if (nowInData && !wasInData)
            {
                // at i == 0, fudge the first-order delta
                double firstOrderDelta = i < 1 ? xFilled[i + 1] - xFilled[i]
                                                : xFilled[i] - xFilled[i - 1];
                double secondOrderDelta = i < 2 || yFilled[i - 1] == 0
                    ? 0
                    : firstOrderDelta - (xFilled[i - 1] - xFilled[i - 2]);
                double totalDelta = 0;
                for (int j = 1; j <= zeroSampleCount; j++)
                {
                    totalDelta += secondOrderDelta;
                    double newX = xFilled[i + j - 1] + firstOrderDelta + totalDelta;
                    bool oob = i + j >= yFilled.Count;
                    double sampleDelta = oob ? 0 : xFilled[i + j] - newX;
                    if (sampleDelta < -firstOrderDelta) break;
                    if (oob || sampleDelta > firstOrderDelta)
                    {
                        xFilled.Insert(i + j, newX);
                        yFilled.Insert(i + j, 0.0);
                    }
                }
            }
            wasInData = nowInData;
        }

        // Scan forward, inserting flanking zeros to the LEFT of the first non-zero sample of
        // each run (transition from zero → data).
        wasInData = false;
        for (int i = 0; i < yFilled.Count; i++)
        {
            bool nowInData = yFilled[i] > 0.0;
            if (nowInData && !wasInData)
            {
                int end = yFilled.Count;
                double firstOrderDelta = i == end - 1
                    ? xFilled[i] - xFilled[i - 1]
                    : xFilled[i + 1] - xFilled[i];
                double secondOrderDelta = i == end - 2 || yFilled[i + 1] == 0
                    ? 0
                    : (xFilled[i + 2] - xFilled[i + 1]) - firstOrderDelta;
                double totalDelta = 0;
                for (int j = 1; j <= zeroSampleCount; j++)
                {
                    totalDelta += secondOrderDelta;
                    double newX = (xFilled[i - j + 1] - firstOrderDelta) - totalDelta;
                    bool oob = i - j < 0;
                    double sampleDelta = oob ? 0 : xFilled[i - j] - newX;
                    if (sampleDelta > firstOrderDelta) break;
                    if (oob || sampleDelta <= -firstOrderDelta)
                    {
                        xFilled.Insert(i - j + 1, newX);
                        yFilled.Insert(i - j + 1, 0.0);
                        i++;
                    }
                }
            }
            wasInData = nowInData;
        }
    }
}
