using System.Globalization;

namespace Pwiz.Analysis.PeakPicking;

/// <summary>
/// Continuous-wavelet-transform peak detector ("CantWaiT"). Convolves the spectrum with a
/// ricker (Mexican-hat) wavelet at a sweep of scales, builds a correlation matrix, finds
/// the maximum across scales for each m/z bin, walks ridge lines through the matrix, and
/// keeps lines that exceed an SNR threshold within their m/z neighbourhood. Port of
/// <c>pwiz::analysis::CwtPeakDetector</c>.
/// </summary>
/// <remarks>
/// Default constructor parameters mirror pwiz C++:
/// <list type="bullet">
///   <item><c>minSnr = 1.0</c> — minimum signal-to-noise ratio for a ridge line to count.</item>
///   <item><c>fixedPeaksKeep = 0</c> — when &gt;0, retain only the top-N peaks by SNR.</item>
///   <item><c>mzTol = 0.1</c> — minimum m/z spacing between adjacent peaks.</item>
///   <item><c>centroid = false</c> — when true, peak m/z is the intensity-weighted centroid
///     across the wavelet's support; when false, peak m/z is the highest sample in the
///     support (with ties broken by max m/z).</item>
/// </list>
/// </remarks>
public sealed class CwtPeakDetector : IPeakDetector
{
    private readonly double _minSnr;
    private readonly int _fixedPeaksKeep;
    private readonly double _mzTol;
    private readonly bool _centroid;
    private const int NScales = 10;
    private readonly double[] _scalings;

    /// <summary>Ridge-line position in the correlation matrix.</summary>
    private struct RidgeLine
    {
        public int Col;
        public int Row;
    }

    /// <summary>Constructs a CWT detector with the given parameters.</summary>
    public CwtPeakDetector(double minSnr, int fixedPeaksKeep, double mzTol, bool centroid = false)
    {
        _minSnr = minSnr;
        _fixedPeaksKeep = fixedPeaksKeep;
        _mzTol = mzTol;
        _centroid = centroid;

        // Wavelet scale sweep — ten linearly-spaced scalings between 1.0 and 7.0. The
        // narrowest scale defines the noise threshold in wavelet space; the widest is
        // tuned to roughly twice the expected number of points per peak (≈12 → 7.0).
        const double initialWidthScaling = 1.0;
        const double finalWidthScaling = 7.0;
        double increment = (finalWidthScaling - initialWidthScaling) / (NScales - 1);
        _scalings = new double[NScales];
        for (int i = 0; i < NScales; i++)
            _scalings[i] = initialWidthScaling + i * increment;
    }

    /// <inheritdoc/>
    public string Name => "CantWaiT (continuous wavelet transform) peak picker";

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
        if (x.Count <= 2) return;

        // Local copies, sorted-together by m/z.
        var xLocal = new double[x.Count];
        var yLocal = new double[x.Count];
        for (int i = 0; i < x.Count; i++) { xLocal[i] = x[i]; yLocal[i] = y[i]; }
        SortTogether(xLocal, yLocal);

        // Bin identical m/z values (within 1e-6) by summing intensities.
        var binnedX = new List<double>(xLocal.Length);
        var binnedY = new List<double>(xLocal.Length);
        binnedX.Add(xLocal[0]);
        binnedY.Add(yLocal[0]);
        for (int i = 1; i < xLocal.Length; i++)
        {
            if (Math.Abs(binnedX[^1] - xLocal[i]) < 1e-6)
            {
                while (i < xLocal.Length && Math.Abs(binnedX[^1] - xLocal[i]) < 1e-6)
                {
                    binnedY[^1] += yLocal[i];
                    i++;
                }
                i--;
            }
            else
            {
                binnedX.Add(xLocal[i]);
                binnedY.Add(yLocal[i]);
            }
        }

        var xs = binnedX;
        var ys = binnedY;
        int mzLength = xs.Count;
        if (mzLength <= 2) return;

        // The correlation matrix has columns at every original m/z point AND at the midpoint
        // between adjacent points (so it can resolve peaks that fall between samples).
        int corrMatrixLength = 2 * mzLength - 1;

        var corrMatrix = new double[NScales][];
        for (int i = 0; i < NScales; i++) corrMatrix[i] = new double[corrMatrixLength];
        var widths = new double[mzLength];
        // waveletPoints[side][scale][mzIndex] — side 0 = points to the left, side 1 = right.
        var waveletPoints = new int[2][][];
        for (int s = 0; s < 2; s++)
        {
            waveletPoints[s] = new int[NScales][];
            for (int sc = 0; sc < NScales; sc++) waveletPoints[s][sc] = new int[mzLength];
        }

        GetScales(xs, ys, waveletPoints, widths);
        CalcCorrelation(xs, ys, waveletPoints, widths, corrMatrix);

        var allLines = new List<RidgeLine>();
        var snrs = new List<double>();
        GetPeakLines(corrMatrix, xs, allLines, snrs);

        RefinePeaks(xs, ys, allLines, widths, xPeakValues, yPeakValues, snrs);

        if (peaks is null) return;
        peaks.Clear();
        peaks.Capacity = Math.Max(peaks.Capacity, xPeakValues.Count);
        for (int i = 0; i < xPeakValues.Count; i++)
            peaks.Add(new Peak(xPeakValues[i], yPeakValues[i]));
    }

    /// <summary>
    /// Estimates the per-m/z sampling rate (averaged in a 10-point sliding window) and the
    /// number of wavelet sample points needed on each side of every m/z point at every scale.
    /// Skips broader scales when intensity drops sharply (likely between peaks) — that's a
    /// performance optimization that doesn't affect peak counts because the inner correlation
    /// loop applies the same skip.
    /// </summary>
    private void GetScales(List<double> mz, List<double> intensity,
        int[][][] nPoints, double[] widths)
    {
        int mzLength = mz.Count;
        var xSpacing = new double[mzLength];
        double lastXSpacing = 0;

        for (int i = 1; i < mzLength - 1; i++)
        {
            if (intensity[i] != 0.0 || intensity[i - 1] != 0.0)
            {
                xSpacing[i] = mz[i] - mz[i - 1];
                if (xSpacing[i] <= 0)
                    throw new InvalidOperationException(
                        "[CwtPeakDetector::GetScales] m/z profile data are unsorted or contain duplicates");
                if (xSpacing[i] > 10)
                    throw new InvalidOperationException(
                        "[CwtPeakDetector::GetScales] m/z profile data seems to lack flanking zeros between peak profiles");
                lastXSpacing = xSpacing[i];
            }
            else
            {
                xSpacing[i] = lastXSpacing;
            }
        }
        xSpacing[0] = xSpacing[1];
        xSpacing[mzLength - 1] = xSpacing[mzLength - 2];

        // Smooth the spacing across a 10-point window. Sampling rate varies by ~3-4× across
        // a typical TOF/Orbitrap m/z range, so a small averaging window absorbs jitter without
        // blurring real changes.
        int windowSize = 10;
        if (windowSize > mzLength) windowSize = mzLength / 2;
        int hfWindow = windowSize / 2;
        windowSize = 2 * hfWindow;

        for (int i = 1; i < mzLength - 1; i++)
        {
            int scalesToInclude = NScales;
            // If the local intensity drops below 75% of either neighbour we treat the point
            // as inter-peak space and only sample the narrowest scale (saves work on noise).
            if (intensity[i] < 0.75 * intensity[i - 1] || intensity[i] < 0.75 * intensity[i + 1])
                scalesToInclude = 1;

            int windowLow = Math.Max(0, i - hfWindow);
            int windowHigh = Math.Min(mzLength - 1, i + hfWindow);
            int nTot = windowHigh - windowLow;

            double sum = 0;
            for (int j = windowLow; j < windowHigh; j++) sum += xSpacing[j];
            widths[i] = sum / nTot;

            for (int j = 0; j < scalesToInclude; j++)
            {
                // For a wavelet of width w*scale, contributions beyond ±3 widths are
                // negligible — that's the radius we sample.
                double maxMzWindow = widths[i] * _scalings[j] * 3.0;

                int nPointsLeft = 0;
                int counter = i;
                while (--counter >= 0)
                {
                    if (mz[i] - mz[counter] > maxMzWindow) break;
                    nPointsLeft++;
                }
                int nPointsRight = 0;
                counter = i;
                while (++counter < mzLength)
                {
                    if (mz[counter] - mz[i] > maxMzWindow) break;
                    nPointsRight++;
                }

                nPoints[0][j][i] = nPointsLeft;
                nPoints[1][j][i] = nPointsRight;
            }
        }
    }

    /// <summary>
    /// Convolves the spectrum with a Ricker (Mexican-hat) wavelet at every (scale, m/z column),
    /// filling <paramref name="matrix"/> with the correlation values. Columns alternate between
    /// "centered on a sample point" and "centered on the midpoint between two sample points",
    /// hence <c>2*mzLength-1</c> total columns.
    /// </summary>
    private void CalcCorrelation(List<double> mz, List<double> intensity,
        int[][][] waveletPoints, double[] widths, double[][] matrix)
    {
        int mzLength = mz.Count;
        // Pad both arrays so the wavelet can extend safely past the spectrum edges.
        const int paddingPoints = 500;
        var padMz = new double[mzLength + 2 * paddingPoints];
        var padIntensity = new double[mzLength + 2 * paddingPoints];
        for (int j = paddingPoints; j < mzLength + paddingPoints; j++)
        {
            padMz[j] = mz[j - paddingPoints];
            padIntensity[j] = intensity[j - paddingPoints];
        }
        var waveletData = new double[paddingPoints];

        for (int i = 0; i < NScales; i++)
        {
            double currentScaling = _scalings[i];
            int matrixIndex = 2; // depends on the j=1 starting index of the inner loop

            for (int j = 1; j < mzLength - 1; j++)
            {
                if (i > 0)
                {
                    // Skip inter-peak points at higher scales (matches GetScales' skip).
                    if (intensity[j] < 0.75 * intensity[j - 1] || intensity[j] < 0.75 * intensity[j + 1])
                    {
                        matrixIndex += 2;
                        continue;
                    }
                }

                int nPointsLeft = waveletPoints[0][i][j];
                int nPointsRight = waveletPoints[1][i][j];

                int paddedCol = j + paddingPoints;
                double width = widths[j] * currentScaling;
                // Ricker wavelet pre-factor: 2 / (sqrt(3w) * π^(1/4)). Note: cpp uses
                // sqrt(3.141519) — appears to be a typo for sqrt(π). Keeping verbatim for
                // reference parity.
                double param1 = 2.0 / (Math.Sqrt(3.0 * width) * Math.Sqrt(Math.Sqrt(3.141519)));
                double param2 = width * width;

                Ricker2d(padMz, paddedCol, nPointsLeft, nPointsRight, param1, param2, padMz[paddedCol], waveletData);

                int startPoint = paddedCol - nPointsLeft;
                for (int k = 0; k < nPointsLeft + nPointsRight + 1; k++)
                    matrix[i][matrixIndex] += waveletData[k] * padIntensity[startPoint + k];

                matrixIndex++;

                if (j == mzLength - 1) break;

                double mzShift = (padMz[paddedCol] + padMz[paddedCol + 1]) / 2.0;
                Ricker2d(padMz, paddedCol, nPointsLeft, nPointsRight, param1, param2, mzShift, waveletData);

                for (int k = 0; k < nPointsLeft + nPointsRight + 1; k++)
                    matrix[i][matrixIndex] += waveletData[k] * padIntensity[startPoint + k];

                matrixIndex++;
            }
        }
    }

    /// <summary>
    /// Reduces the correlation matrix to ridge lines: the per-column maximum across scales,
    /// surviving an SNR floor estimated as the 95th percentile of the narrowest-scale
    /// correlations within a sliding 300-column window.
    /// </summary>
    private void GetPeakLines(double[][] corrMatrix, List<double> x,
        List<RidgeLine> allLines, List<double> snrs)
    {
        int corrMatrixLength = corrMatrix[0].Length;

        // Step 1: per-column row of maximum correlation.
        var colMaxes = new int[corrMatrixLength];
        for (int i = 0; i < corrMatrixLength; i++)
        {
            double corrMax = 0.0;
            for (int j = 0; j < NScales; j++)
            {
                if (corrMatrix[j][i] > corrMax)
                {
                    corrMax = corrMatrix[j][i];
                    colMaxes[i] = j;
                }
            }
        }

        // Step 2: per-bin noise floor (95th percentile of narrowest-scale correlations).
        const double noisePer = 95.0;
        int windowSize = 300;
        if (windowSize > corrMatrixLength) windowSize = corrMatrixLength / 2;
        int hfWindow = windowSize / 2;
        windowSize = 2 * hfWindow;

        int nNoiseBins = corrMatrixLength / windowSize + 1;
        var noises = new double[nNoiseBins];
        for (int i = 0; i < nNoiseBins; i++)
        {
            int windowLow = i * windowSize;
            int windowHigh = i == nNoiseBins - 1 ? corrMatrixLength : windowLow + windowSize;
            int nTot = windowHigh - windowLow;
            var sortedData = new double[nTot];
            for (int j = 0; j < nTot; j++) sortedData[j] = corrMatrix[0][windowLow + j];
            Array.Sort(sortedData);
            double noise = ScoreAtPercentile(noisePer, sortedData, nTot);
            if (noise < 1.0) noise = 1.0;
            noises[i] = noise;
        }

        // Per-column m/z (alternates between sample m/z and midpoint m/z).
        var interpolatedX = new double[corrMatrixLength];
        for (int i = 0; i < corrMatrixLength; i++)
            interpolatedX[i] = ConvertColToMz(x, i);

        // Step 3: find local maxima at least mzTol_ apart, filter by SNR.
        for (int i = 2; i < corrMatrixLength - 2; i++)
        {
            double correlationVal = corrMatrix[colMaxes[i]][i];

            // Reject unless this is the local peak across a 5-column window.
            if (correlationVal < corrMatrix[colMaxes[i - 1]][i - 1] ||
                correlationVal < corrMatrix[colMaxes[i - 2]][i - 2] ||
                correlationVal < corrMatrix[colMaxes[i + 1]][i + 1] ||
                correlationVal < corrMatrix[colMaxes[i + 2]][i + 2])
                continue;

            double mzCol = ConvertColToMz(x, i);
            double lowTol = mzCol - _mzTol;
            double highTol = mzCol + _mzTol;
            int lowBound = LowerBound(interpolatedX, lowTol);
            int highBound = UpperBound(interpolatedX, highTol) - 1;

            double maxCorr = 0.0;
            int maxCol = 0;
            for (int j = lowBound; j <= highBound; j++)
            {
                int row = colMaxes[j];
                if (corrMatrix[row][j] > maxCorr)
                {
                    maxCorr = corrMatrix[row][j];
                    maxCol = j;
                }
            }

            int noiseBin = Math.Min(maxCol / windowSize, nNoiseBins - 1);
            double snr = maxCorr / noises[noiseBin];
            if (snr < _minSnr) continue;

            int nLines = allLines.Count;
            if (nLines > 0)
            {
                double mzNewLine = ConvertColToMz(x, maxCol);
                double mzPrevLine = ConvertColToMz(x, allLines[nLines - 1].Col);
                double mzDiff = mzNewLine - mzPrevLine;
                double corrPrev = corrMatrix[allLines[nLines - 1].Row][allLines[nLines - 1].Col];
                if (mzDiff > _mzTol)
                {
                    allLines.Add(new RidgeLine { Col = maxCol, Row = colMaxes[maxCol] });
                    snrs.Add(snr);
                }
                else if (maxCorr > corrPrev)
                {
                    allLines.RemoveAt(nLines - 1);
                    snrs.RemoveAt(nLines - 1);
                    allLines.Add(new RidgeLine { Col = maxCol, Row = colMaxes[maxCol] });
                    snrs.Add(snr);
                }
            }
            else
            {
                allLines.Add(new RidgeLine { Col = maxCol, Row = colMaxes[maxCol] });
                snrs.Add(snr);
            }
        }
    }

    /// <summary>
    /// Refines each ridge line into an actual (m/z, intensity) peak by either centroiding
    /// (intensity-weighted m/z) or selecting the highest sample within the wavelet's support.
    /// Drops noise-floor peaks (intensity &lt; 2 at the narrowest scale), de-duplicates peaks
    /// closer than mzTol, and applies <c>fixedPeaksKeep</c> when set.
    /// </summary>
    private void RefinePeaks(List<double> noisyX, List<double> noisyY,
        List<RidgeLine> lines, double[] widths,
        List<double> smoothX, List<double> smoothY, List<double> snrs)
    {
        if (lines.Count == 0) return;

        for (int i = 0; i < lines.Count; i++)
        {
            double mzCol = ConvertColToMz(noisyX, lines[i].Col);
            int row = lines[i].Row;
            double currentScaling = _scalings[row];
            double offset = currentScaling * widths[lines[i].Col / 2];

            int startFittingPoint = LowerBound(noisyX, mzCol - offset);
            int endFittingPoint = UpperBound(noisyX, mzCol + offset) - 1;

            double maxIntensity = 0.0;
            double intensityAccumulator = 0.0;
            double mzCentroid = 0.0;
            double bestMz = 0.0;

            if (_centroid)
            {
                for (int j = startFittingPoint; j <= endFittingPoint; j++)
                {
                    intensityAccumulator += noisyY[j];
                    mzCentroid += noisyY[j] * noisyX[j];
                    if (noisyY[j] >= maxIntensity) maxIntensity = noisyY[j];
                }
                bestMz = mzCentroid / intensityAccumulator;
            }
            else
            {
                for (int j = startFittingPoint; j <= endFittingPoint; j++)
                {
                    intensityAccumulator += noisyY[j];
                    if (noisyY[j] >= maxIntensity)
                    {
                        maxIntensity = noisyY[j];
                        bestMz = noisyX[j];
                    }
                }
            }

            if (smoothX.Count == 0 || bestMz != smoothX[^1])
            {
                smoothX.Add(bestMz);
                smoothY.Add(maxIntensity);
            }
        }

        // Drop low-intensity peaks at the narrowest scale (likely noise).
        for (int k = smoothX.Count - 1; k > 0; k--)
        {
            if (smoothY[k] < 2.0 && lines[k].Row < 1)
            {
                smoothX.RemoveAt(k);
                smoothY.RemoveAt(k);
                snrs.RemoveAt(k);
            }
        }

        // Re-sort by m/z (centroiding can produce out-of-order peaks).
        SortTogether(smoothX, smoothY, snrs);

        // De-duplicate peaks within mzTol of each other, keeping the lower-intensity one
        // (matches pwiz C++ which removes the *lower* of the pair — note the index choice).
        for (int k = smoothX.Count - 1; k > 0; k--)
        {
            if (smoothX[k] - smoothX[k - 1] < _mzTol)
            {
                int removeIndex = smoothY[k] > smoothY[k - 1] ? k - 1 : k;
                smoothX.RemoveAt(removeIndex);
                smoothY.RemoveAt(removeIndex);
                snrs.RemoveAt(removeIndex);
            }
        }

        // Trim to top-N by SNR if fixedPeaksKeep is set (used by the Turbocharger
        // charge-determination path; not exercised by the standard centroid-cwt filter).
        if (_fixedPeaksKeep > 0 && snrs.Count > _fixedPeaksKeep)
        {
            double percentile = 100.0 * (1.0 - (double)_fixedPeaksKeep / snrs.Count);
            var sortedSnrs = snrs.ToArray();
            Array.Sort(sortedSnrs);
            double cutoff = ScoreAtPercentile(percentile, sortedSnrs, sortedSnrs.Length);
            for (int i = snrs.Count - 1; i >= 0; i--)
            {
                if (snrs[i] < cutoff)
                {
                    smoothX.RemoveAt(i);
                    smoothY.RemoveAt(i);
                    snrs.RemoveAt(i);
                }
            }
        }
    }

    // ---------- helpers ----------

    private static void Ricker2d(double[] padMz, int col, int nPointsLeft, int nPointsRight,
        double a, double wsq, double centralMz, double[] total)
    {
        if (nPointsRight + nPointsLeft >= total.Length)
            throw new InvalidOperationException("[CwtPeakDetector::Ricker2d] invalid input parameters");
        int cnt = 0;
        for (int i = col - nPointsLeft, end = col + nPointsRight; i <= end; i++, cnt++)
        {
            double vec = padMz[i] - centralMz;
            double tsq = vec * vec;
            double mod = 1.0 - tsq / wsq;
            double gauss = Math.Exp(-tsq / (2.0 * wsq));
            total[cnt] = a * mod * gauss;
        }
    }

    /// <summary>Returns the index of the first element &gt;= target (std::lower_bound semantics).</summary>
    private static int LowerBound(List<double> sorted, double target) =>
        LowerBoundCore(sorted, sorted.Count, target);

    private static int LowerBound(double[] sorted, double target) =>
        LowerBoundCore(sorted, sorted.Length, target);

    private static int LowerBoundCore(IList<double> sorted, int count, double target)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (sorted[mid] < target) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>Returns the index of the first element &gt; target (std::upper_bound semantics).</summary>
    private static int UpperBound(List<double> sorted, double target) =>
        UpperBoundCore(sorted, sorted.Count, target);

    private static int UpperBound(double[] sorted, double target) =>
        UpperBoundCore(sorted, sorted.Length, target);

    private static int UpperBoundCore(IList<double> sorted, int count, double target)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (sorted[mid] <= target) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static double ScoreAtPercentile(double perc, double[] sorted, int nTot)
    {
        // Matches scipy.stats.scoreatpercentile: linear interpolation between adjacent
        // ranks, denominator (nTot - 1).
        double nBelow = (nTot - 1) * perc / 100.0;
        if (Math.Ceiling(nBelow) == nBelow)
            return sorted[(int)nBelow];
        double loInd = Math.Floor(nBelow);
        double hiInd = Math.Ceiling(nBelow);
        double fraction = nBelow - loInd;
        double lo = sorted[(int)loInd];
        double hi = sorted[(int)hiInd];
        return lo + (hi - lo) * fraction;
    }

    private static double ConvertColToMz(List<double> mzs, int col)
    {
        int mapIndex = col / 2;
        if ((col & 1) == 1) return (mzs[mapIndex] + mzs[mapIndex + 1]) / 2.0;
        return mzs[mapIndex];
    }

    private static void SortTogether(double[] keys, double[] values)
    {
        // Build paired indices, sort by keys, then materialize back into arrays.
        var idx = new int[keys.Length];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        Array.Sort(idx, (a, b) => keys[a].CompareTo(keys[b]));
        var sortedK = new double[keys.Length];
        var sortedV = new double[values.Length];
        for (int i = 0; i < idx.Length; i++) { sortedK[i] = keys[idx[i]]; sortedV[i] = values[idx[i]]; }
        Array.Copy(sortedK, keys, keys.Length);
        Array.Copy(sortedV, values, values.Length);
    }

    private static void SortTogether(List<double> keys, List<double> v1, List<double> v2)
    {
        int n = keys.Count;
        var idx = new int[n];
        for (int i = 0; i < n; i++) idx[i] = i;
        Array.Sort(idx, (a, b) => keys[a].CompareTo(keys[b]));
        var k = new double[n];
        var a1 = new double[n];
        var a2 = new double[n];
        for (int i = 0; i < n; i++) { k[i] = keys[idx[i]]; a1[i] = v1[idx[i]]; a2[i] = v2[idx[i]]; }
        for (int i = 0; i < n; i++) { keys[i] = k[i]; v1[i] = a1[i]; v2[i] = a2[i]; }
    }
}
