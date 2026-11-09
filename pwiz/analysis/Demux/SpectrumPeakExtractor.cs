using MathNet.Numerics.LinearAlgebra;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Bins centroided peaks from a spectrum into a fixed grid of m/z windows. Port of cpp's
/// <c>pwiz/analysis/demux/SpectrumPeakExtractor.cpp</c>, except that it searches each bin's full
/// window (cpp drops part of it above half the top m/z) and a snapped shared edge belongs to one bin.
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
        for (int i = 0; i < n; i++)
        {
            double peakMz = peakMzList[i];
            // cpp: deltaMz = peakMz - (peakMz - massError) — the absolute Da equivalent of the
            // tolerance applied at this m/z (handles ppm vs. mz units transparently).
            double deltaMz = peakMz - SubtractTolerance(peakMz, massError);
            _ranges[i] = (peakMz - deltaMz, peakMz + deltaMz);
        }
        _minValue = _ranges[0].Low;
        _maxValue = _ranges[n - 1].High;

        // Snap adjacent overlapping ranges to a shared midpoint so a single peak can't be
        // double-counted across two bins (cpp lines 47-57), using the original edges as cpp does.
        double originalLow = _ranges[0].Low;
        for (int i = 0; i + 1 < n; i++)
        {
            double nextOriginalLow = _ranges[i + 1].Low;
            if (_ranges[i].High > nextOriginalLow)
            {
                double center = (_ranges[i].High + originalLow + _ranges[i + 1].High + nextOriginalLow) / 4.0;
                _ranges[i] = (_ranges[i].Low, center);
                _ranges[i + 1] = (center, _ranges[i + 1].High);
            }
            originalLow = nextOriginalLow;
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

            // Skip only bins that end below the query. Cpp starts at query - maxDelta, the
            // largest HALF-width, which drops the upper part of every bin above half the
            // spectrum's top m/z; Skyline's original binner used the full width.
            while (binStartIndex < _ranges.Length && _ranges[binStartIndex].High < query)
                binStartIndex++;
            // A peak on an edge snapped to the next bin's start belongs to the next bin only.
            for (int b = binStartIndex; b < _ranges.Length; b++)
            {
                if (_ranges[b].Low > query) break;
                bool sharedEdge = b + 1 < _ranges.Length && _ranges[b + 1].Low == _ranges[b].High;
                if (query < _ranges[b].High || (query == _ranges[b].High && !sharedEdge))
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
