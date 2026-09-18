using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Demultiplexing solver — given a design matrix <c>A</c> (precursor masks) and a signal
/// matrix <c>B</c> (per-multiplexed-spectrum binned intensities), returns the matrix <c>X</c>
/// such that <c>A X ≈ B</c> (column-by-column least-squares solve). Port of cpp's
/// <c>DemuxSolver</c> interface.
/// </summary>
public interface IDemuxSolver
{
    /// <summary>Solves <c>A X = B</c> column-wise for <paramref name="X"/>. Each column of
    /// <paramref name="B"/> is one transition's intensities across the multiplexed spectra; the
    /// matching column of <paramref name="X"/> is the deconvolved per-window intensity.</summary>
    void Solve(Matrix<double> A, Matrix<double> B, Matrix<double> X);
}

/// <summary>
/// <see cref="IDemuxSolver"/> implementation that solves each column of B independently as a
/// non-negative least-squares problem via <see cref="Nnls"/>. Port of cpp's <c>NNLSSolver</c>.
/// </summary>
public sealed class NnlsSolver : IDemuxSolver
{
    private readonly int _maxIter;
    private readonly double _epsilon;

    /// <summary>Constructs an NNLS solver.</summary>
    /// <param name="maxIter">Maximum NNLS iterations per column (cpp default 50).</param>
    /// <param name="epsilon">Optimality tolerance (cpp default 1e-10).</param>
    public NnlsSolver(int maxIter = 50, double epsilon = 1e-10)
    {
        _maxIter = maxIter;
        _epsilon = epsilon;
    }

    /// <inheritdoc/>
    public void Solve(Matrix<double> A, Matrix<double> B, Matrix<double> X)
    {
        ArgumentNullException.ThrowIfNull(A);
        ArgumentNullException.ThrowIfNull(B);
        ArgumentNullException.ThrowIfNull(X);
        if (A.RowCount != B.RowCount)
            throw new ArgumentException("A and B must have the same row count");
        if (A.ColumnCount != X.RowCount)
            throw new ArgumentException("X must have row count = A.ColumnCount");
        if (B.ColumnCount != X.ColumnCount)
            throw new ArgumentException("X must have column count = B.ColumnCount");

        // Pre-compute A^T B once. Each per-column NNLS reuses the corresponding column of AtB
        // instead of recomputing A^T b_j. For ~200 transitions/spectrum × 80k spectra × 7 mux
        // rows × 7 cols this saves ~80M scalar mults per spectrum and amortizes much better
        // through MathNet's BLAS path than the per-column hot loop did.
        var AtB = A.TransposeThisAndMultiply(B);

        // One Nnls instance per thread — A^T A is cached at construction, so paying the
        // O(m·n²) construction cost once per thread (instead of once per column) eliminates the
        // dominant redundant work in the demux pipeline. Equivalent to OpenMP firstprivate.
        var localNnls = new System.Threading.ThreadLocal<Nnls>(
            () => new Nnls(A, _maxIter, _epsilon),
            trackAllValues: false);

        try
        {
            int numCols = B.ColumnCount;
            int numRows = A.ColumnCount;
            Parallel.For(0, numCols, fragIndex =>
            {
                var solver = localNnls.Value!;
                if (!solver.Solve(AtB, fragIndex))
                {
                    // Cpp swallows convergence failure silently — match that to keep parity.
                    return;
                }
                var sol = solver.X;
                for (int i = 0; i < numRows; i++) X[i, fragIndex] = sol[i];
            });
        }
        finally
        {
            localNnls.Dispose();
        }
    }

    /// <summary>Convenience helper: allocates the solution matrix and runs the column-wise NNLS solve.</summary>
    public Matrix<double> Solve(Matrix<double> A, Matrix<double> B)
    {
        var X = DenseMatrix.Create(A.ColumnCount, B.ColumnCount, 0);
        Solve(A, B, X);
        return X;
    }
}
