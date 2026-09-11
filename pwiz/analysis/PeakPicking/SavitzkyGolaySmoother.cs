using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Pwiz.Analysis.PeakPicking;

/// <summary>
/// Savitzky-Golay smoother — convolves y values with a sliding window of polynomial-fit
/// coefficients. Port of <c>pwiz::analysis::SavitzkyGolaySmoother</c>
/// (<c>pwiz/analysis/common/SavitzkyGolaySmoother.cpp</c>).
/// </summary>
/// <remarks>
/// <para>The polynomial-fit coefficients are derived per (order, windowSize) pair via a least-
/// squares solve (Numerical Recipes §14.8) and cached. Pre-condition: samples within the
/// smoothing window must be approximately equally spaced — sparse profile spectra are padded
/// via <see cref="ZeroSampleFiller"/> before convolution to give the kernel a well-formed flank.</para>
/// <para>Output is clamped at zero (negative ringing from the polynomial fit is suppressed).</para>
/// </remarks>
public sealed class SavitzkyGolaySmoother : ISmoother
{
    private readonly int _order;
    private readonly int _window;

    /// <summary>Constructs a smoother with the given polynomial fit and window size.</summary>
    /// <param name="polynomialOrder">Polynomial order; must be in [2, 20].</param>
    /// <param name="windowSize">Window size; must be odd and ≥ 5.</param>
    public SavitzkyGolaySmoother(int polynomialOrder, int windowSize)
    {
        if (polynomialOrder < 2 || polynomialOrder > 20)
            throw new ArgumentException(
                "[SavitzkyGolaySmoother] Invalid value for polynomial order: valid range is [2, 20]",
                nameof(polynomialOrder));
        if (windowSize < 5 || (windowSize % 2) == 0)
            throw new ArgumentException(
                "[SavitzkyGolaySmoother] Invalid value for window size: value must be odd and ≥ 5",
                nameof(windowSize));
        if (polynomialOrder > windowSize)
            throw new ArgumentException(
                "[SavitzkyGolaySmoother] Invalid values for polynomial order and window size: window size must be greater than polynomial order.");

        _order = polynomialOrder;
        _window = windowSize;
    }

    /// <inheritdoc/>
    public void Smooth(IReadOnlyList<double> x, IReadOnlyList<double> y,
                       List<double> xSmoothed, List<double> ySmoothed)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        ArgumentNullException.ThrowIfNull(xSmoothed);
        ArgumentNullException.ThrowIfNull(ySmoothed);
        if (x.Count != y.Count)
            throw new ArgumentException("[SavitzkyGolaySmoother.Smooth] x and y arrays must be the same size");

        var c = CoefficientCache.Get(_order, _window);
        int flank = (_window - 1) / 2;

        // Pad sparse profile data with flanking zeros so the convolution window has well-defined
        // samples on both sides of every non-zero run. Cpp passes flank+1 to leave one extra
        // zero past the kernel reach — preserves edge behavior at the boundary of each run.
        var xFilled = new List<double>(x.Count + 2 * flank);
        var yFilled = new List<double>(x.Count + 2 * flank);
        ZeroSampleFiller.Fill(x, y, xFilled, yFilled, flank + 1);
        xSmoothed.Clear();
        xSmoothed.AddRange(xFilled);

        // Output mirrors input on the extreme flanks (kernel doesn't fit there).
        ySmoothed.Clear();
        ySmoothed.Capacity = yFilled.Count;
        for (int i = 0; i < flank && i < yFilled.Count; i++) ySmoothed.Add(yFilled[i]);
        for (int i = flank, end = yFilled.Count - flank; i < end; i++)
        {
            // Symmetric kernel: c[flank] is the center weight; c[flank-offset] applies to both
            // i-offset and i+offset (Gram polynomial coefficients are symmetric for symmetric windows).
            double smoothY = c[flank] * yFilled[i];
            for (int offset = 1; offset <= flank; offset++)
            {
                smoothY += c[flank - offset] * yFilled[i - offset];
                smoothY += c[flank - offset] * yFilled[i + offset];
            }
            ySmoothed.Add(System.Math.Max(0.0, smoothY));
        }
        // Cpp duplicates the *first* flank elements of yCopy at the end (subtle; appears to be
        // a stylistic choice rather than a meaningful boundary handling — preserves total array
        // length to match xSmoothed). Mirror it for byte-level parity.
        for (int i = 0; i < flank && i < yFilled.Count; i++) ySmoothed.Add(yFilled[i]);
    }

    /// <summary>Generates the Savitzky-Golay coefficients for the given polynomial order and
    /// half-window sizes via the normal-equations solve from Numerical Recipes §14.8. Public so
    /// callers can inspect the kernel directly (also exercised by the unit tests).</summary>
    public static double[] GenerateCoefficients(int leftWindow, int rightWindow, int order)
    {
        int size = leftWindow + rightWindow + 1;
        var a = DenseMatrix.Create(order + 1, order + 1, 0);
        var b = DenseVector.Create(order + 1, 0);
        b[0] = 1.0;

        for (int ipj = 0; ipj <= (order << 1); ipj++)
        {
            double sum = ipj == 0 ? 1.0 : 0.0;
            for (int k = 1; k <= rightWindow; k++) sum += System.Math.Pow(k, ipj);
            for (int k = 1; k <= leftWindow; k++) sum += System.Math.Pow(-k, ipj);
            int mm = System.Math.Min(ipj, 2 * order - ipj);
            for (int imj = -mm; imj <= mm; imj += 2)
                a[(ipj + imj) / 2, (ipj - imj) / 2] = sum;
        }

        var soln = a.Solve(b);
        var c = new double[size];
        for (int k = -leftWindow; k <= rightWindow; k++)
        {
            double sum = soln[0];
            double fac = 1.0;
            for (int mm = 0; mm < order; mm++)
            {
                fac *= k;
                sum += soln[mm + 1] * fac;
            }
            c[k + leftWindow] = sum;
        }
        return c;
    }

    /// <summary>Process-wide cache of generated SG coefficients, keyed by (order, window).</summary>
    private static class CoefficientCache
    {
        private static readonly Dictionary<(int Order, int Window), double[]> _cache = new();
        private static readonly object _lock = new();

        public static double[] Get(int order, int window)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue((order, window), out var cached)) return cached;
                int half = (window - 1) / 2;
                var coeffs = GenerateCoefficients(half, half, order);
                _cache[(order, window)] = coeffs;
                return coeffs;
            }
        }
    }
}
