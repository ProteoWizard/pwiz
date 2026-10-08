using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Pwiz.Util.Numerics;

/// <summary>
/// Parabola y = a·x² + b·x + c, either from coefficients or fit to sample points.
/// Port of pwiz::math::Parabola.
/// </summary>
public sealed class Parabola
{
    /// <summary>The three coefficients [a, b, c] in y = a·x² + b·x + c.</summary>
    public double[] Coefficients { get; }

    /// <summary>Constructs from explicit coefficients.</summary>
    public Parabola(double a, double b, double c) => Coefficients = new[] { a, b, c };

    /// <summary>Constructs from an array of exactly three coefficients [a, b, c].</summary>
    public Parabola(IReadOnlyList<double> coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);
        if (coefficients.Count != 3)
            throw new ArgumentException("Expected exactly 3 coefficients [a, b, c].", nameof(coefficients));
        Coefficients = new[] { coefficients[0], coefficients[1], coefficients[2] };
    }

    /// <summary>Constructs via (optionally weighted) ordinary least squares from ≥3 sample points.</summary>
    public Parabola(IReadOnlyList<(double X, double Y)> samples, IReadOnlyList<double>? weights = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count < 3)
            throw new ArgumentException("Need at least 3 samples to fit a parabola.", nameof(samples));
        if (weights is not null && weights.Count != samples.Count)
            throw new ArgumentException("Weights length must match samples length.", nameof(weights));

        // Design matrix rows: [x², x, 1]; solve (WA)ᵀ(WA) β = (WA)ᵀ W y.
        int n = samples.Count;
        var a = DenseMatrix.Create(n, 3, 0);
        var y = DenseVector.Create(n, 0.0);
        for (int i = 0; i < n; i++)
        {
            double xi = samples[i].X;
            double yi = samples[i].Y;
            double w = weights is null ? 1.0 : weights[i];
            double sqrtW = System.Math.Sqrt(w);
            a[i, 0] = xi * xi * sqrtW;
            a[i, 1] = xi * sqrtW;
            a[i, 2] = sqrtW;
            y[i] = yi * sqrtW;
        }
        var beta = a.Solve(y);
        Coefficients = new[] { beta[0], beta[1], beta[2] };
    }

    /// <summary>Evaluates the parabola at <paramref name="x"/>.</summary>
    public double Evaluate(double x)
    {
        double a = Coefficients[0], b = Coefficients[1], c = Coefficients[2];
        return a * x * x + b * x + c;
    }

    /// <summary>
    /// X-coordinate of the vertex, −b/(2a). Undefined when a == 0 (returns NaN).
    /// </summary>
    public double Center
    {
        get
        {
            double a = Coefficients[0];
            return a == 0 ? double.NaN : -Coefficients[1] / (2 * a);
        }
    }

    /// <summary>Formats as <c>a*x^2 + b*x + c</c> for debugging.</summary>
    public override string ToString()
    {
        return $"{Coefficients[0]}*x^2 + {Coefficients[1]}*x + {Coefficients[2]}";
    }
}
