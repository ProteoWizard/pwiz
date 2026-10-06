namespace Pwiz.Analysis.PeakPicking;

/// <summary>
/// Whittaker smoother: the series z that minimises |y - z|² + λ|Dz|², D being the first-difference
/// operator, i.e. the solution of (I + λDᵀD) z = y. Port of <c>pwiz::analysis::WhittakerSmoother</c>
/// (<c>pwiz/analysis/common/WhittakerSmoother.cpp</c>), after Eilers, "A Perfect Smoother",
/// Anal. Chem. 2003, 75, 3631.
/// </summary>
/// <remarks>
/// cpp inverts I + λDᵀD as a dense matrix (Gauss-Jordan, O(n³) time and O(n²) memory). The matrix
/// is symmetric tridiagonal (1 + λ at both ends of the diagonal, 1 + 2λ inside, -λ beside it), so
/// this solves the same system directly in O(n); the result is the same to rounding. x passes
/// through unchanged, and a smoothed series keeps the sum of the original.
/// </remarks>
public sealed class WhittakerSmoother : ISmoother
{
    private readonly double _lambda;

    /// <summary>Constructs a smoother with the given roughness penalty.</summary>
    /// <param name="lambdaCoefficient">Weight of the roughness penalty; must be at least 2.</param>
    public WhittakerSmoother(double lambdaCoefficient)
    {
        if (lambdaCoefficient < 2.0)
            throw new ArgumentException(
                "[WhittakerSmoother] Invalid value for lambda coefficient; valid range is [2, infinity)",
                nameof(lambdaCoefficient));
        _lambda = lambdaCoefficient;
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
            throw new ArgumentException("[WhittakerSmoother.Smooth] x and y arrays must be the same size");

        int n = y.Count;
        var xCopy = new double[n];
        for (int i = 0; i < n; i++) xCopy[i] = x[i];
        var z = new double[n];
        if (n == 1)
            z[0] = y[0];
        else if (n > 1)
        {
            // Thomas algorithm: forward elimination, then back substitution.
            var cPrime = new double[n];
            double denom = 1 + _lambda;
            cPrime[0] = -_lambda / denom;
            z[0] = y[0] / denom;
            for (int i = 1; i < n; i++)
            {
                double diagonal = i == n - 1 ? 1 + _lambda : 1 + 2 * _lambda;
                denom = diagonal + _lambda * cPrime[i - 1];
                cPrime[i] = -_lambda / denom;
                z[i] = (y[i] + _lambda * z[i - 1]) / denom;
            }
            for (int i = n - 2; i >= 0; i--)
                z[i] -= cPrime[i] * z[i + 1];
        }

        // Written last, so a caller may pass the same lists as input and output.
        xSmoothed.Clear();
        xSmoothed.AddRange(xCopy);
        ySmoothed.Clear();
        ySmoothed.AddRange(z);
    }
}
