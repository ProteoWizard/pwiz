using MathNet.Numerics.LinearAlgebra;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Bins centroided peaks from a spectrum into a fixed grid of m/z windows. Port of cpp's
/// <c>pwiz/analysis/demux/SpectrumPeakExtractor.cpp</c>.
/// </summary>
/// <remarks>
/// Used by the demux algorithms to assemble the right-hand side <c>b</c> of the NNLS problem:
/// each row of the output matrix holds one input spectrum's intensities, summed into the
/// pre-defined per-peak bins. Bins straddle peaks within an <see cref="MZTolerance"/> window
/// (cpp's "delta = peakMz - (peakMz - massError)" idiom) and adjacent overlapping bins get
/// snapped to a common midpoint so a single peak can't double-count.
/// </remarks>
public sealed class SpectrumPeakExtractor
{
    private readonly (double Low, double High)[] _ranges;
    private readonly double _maxDelta;
    private readonly double _minValue;
    private readonly double _maxValue;

    /// <summary>Number of peak bins; equals the column count required for the output matrix.</summary>
    public int NumPeaks => _ranges.Length;

    /// <summary>Constructs an extractor with one bin per entry of <paramref name="peakMzList"/>.</summary>
    /// <param name="peakMzList">Sorted ascending peak m/z values; no duplicates.</param>
    /// <param name="massError">Tolerance window around each peak m/z.</param>
    public SpectrumPeakExtractor(IReadOnlyList<double> peakMzList, MZTolerance massError)
    {
        ArgumentNullException.ThrowIfNull(peakMzList);
        if (peakMzList.Count == 0)
            throw new ArgumentException("peakMzList must not be empty");

        int n = peakMzList.Count;
        _ranges = new (double, double)[n];
        double maxDelta = 0;
        for (int i = 0; i < n; i++)
        {
            double peakMz = peakMzList[i];
            // cpp: deltaMz = peakMz - (peakMz - massError) — the absolute Da equivalent of the
            // tolerance applied at this m/z (handles ppm vs. mz units transparently).
            double deltaMz = peakMz - SubtractTolerance(peakMz, massError);
            if (deltaMz > maxDelta) maxDelta = deltaMz;
            _ranges[i] = (peakMz - deltaMz, peakMz + deltaMz);
        }
        _maxDelta = maxDelta;
        _minValue = _ranges[0].Low;
        _maxValue = _ranges[n - 1].High;

        // Snap adjacent overlapping ranges to a shared midpoint so a single peak can't be
        // double-counted across two bins (cpp lines 47-57).
        for (int i = 0; i + 1 < n; i++)
        {
            if (_ranges[i].High > _ranges[i + 1].Low)
            {
                double center = (_ranges[i].High + _ranges[i].Low + _ranges[i + 1].High + _ranges[i + 1].Low) / 4.0;
                _ranges[i] = (_ranges[i].Low, center);
                _ranges[i + 1] = (center, _ranges[i + 1].High);
            }
        }
    }

    /// <summary>Bins <paramref name="spectrum"/>'s peaks into row <paramref name="rowNum"/> of
    /// <paramref name="matrix"/>, scaled by <paramref name="weight"/>. The row must have at
    /// least <see cref="NumPeaks"/> columns; existing values are overwritten.</summary>
    public void Extract(Spectrum spectrum, Matrix<double> matrix, int rowNum, double weight = 1.0)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        ArgumentNullException.ThrowIfNull(matrix);
        var mzArr = spectrum.GetMZArray();
        var intArr = spectrum.GetIntensityArray();
        if (mzArr is null || intArr is null) return;

        // Zero the destination row.
        for (int j = 0; j < matrix.ColumnCount; j++) matrix[rowNum, j] = 0;

        var mz = mzArr.Data;
        var inten = intArr.Data;
        int peakCount = System.Math.Min(mz.Count, inten.Count);

        int binStartIndex = 0;
        for (int q = 0; q < peakCount; q++)
        {
            double query = mz[q];
            if (query < _minValue) continue;
            if (query > _maxValue) break;

            double minStart = query - _maxDelta;
            // Advance binStartIndex past bins whose entire range is below the query window.
            for (; binStartIndex < _ranges.Length; binStartIndex++)
            {
                if (_ranges[binStartIndex].Low >= minStart) break;
            }
            // Add this peak's intensity to every bin whose [Low, High] contains it.
            for (int b = binStartIndex; b < _ranges.Length; b++)
            {
                if (_ranges[b].Low > query) break;
                if (_ranges[b].Low <= query && query <= _ranges[b].High)
                    matrix[rowNum, b] += inten[q];
            }
        }

        // Apply the row weight.
        for (int j = 0; j < NumPeaks; j++) matrix[rowNum, j] *= weight;
    }

    /// <summary>Computes <c>peakMz - massError</c> respecting the tolerance's units.</summary>
    private static double SubtractTolerance(double peakMz, MZTolerance tol) => tol.Units switch
    {
        MZToleranceUnits.Mz => peakMz - tol.Value,
        MZToleranceUnits.Ppm => peakMz - System.Math.Abs(peakMz) * tol.Value * 1e-6,
        _ => peakMz - tol.Value,
    };
}
