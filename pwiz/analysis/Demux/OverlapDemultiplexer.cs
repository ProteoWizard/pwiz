using MathNet.Numerics.Interpolation;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Demultiplexer for overlap-style DIA experiments. Port of cpp's <c>OverlapDemultiplexer</c>.
/// Builds a smaller, locally-relevant deconv block (only the overlap regions adjacent in m/z to
/// the spectrum being demultiplexed) and optionally interpolates each block row to the
/// retention time of the demuxed spectrum via cubic Hermite splines (MathNet's
/// <c>CubicSpline.InterpolateHermite</c>).
/// </summary>
public sealed class OverlapDemultiplexer : IDemultiplexer
{
    /// <summary>Tunable parameters.</summary>
    public sealed class Params
    {
        /// <summary>Mass tolerance for MS/MS peak extraction (cpp default 10 ppm).</summary>
        public MZTolerance MassError { get; init; } = new(10.0, MZToleranceUnits.Ppm);

        /// <summary>Down-weight nearby spectra by retention-time distance (only used when
        /// <see cref="InterpolateRetentionTime"/> is false).</summary>
        public bool ApplyWeighting { get; init; } = true;

        /// <summary>Interpolate each multiplexed-spectrum row to the demuxed spectrum's RT via
        /// cubic Hermite splines. Cpp default true.</summary>
        public bool InterpolateRetentionTime { get; init; } = true;

        /// <summary>How rows are interpolated when <see cref="InterpolateRetentionTime"/> is true.
        /// Default <see cref="DemuxInterpolation.NaturalSpline"/>, as cpp.</summary>
        public DemuxInterpolation Interpolation { get; init; } = DemuxInterpolation.NaturalSpline;
    }

    private const int OverlapRegionsInApprox = 7;

    private readonly Params _params;
    private ISpectrumList? _spectrumList;
    private IPrecursorMaskCodec? _maskCodec;
    private List<int> _spectrumIndices = new();

    /// <summary>Constructs an overlap demultiplexer with the given parameters.</summary>
    public OverlapDemultiplexer(Params? p = null) => _params = p ?? new Params();

    /// <inheritdoc/>
    public IReadOnlyList<int> SpectrumIndices => _spectrumIndices;

    /// <inheritdoc/>
    public void Initialize(ISpectrumList spectrumList, IPrecursorMaskCodec maskCodec)
    {
        ArgumentNullException.ThrowIfNull(spectrumList);
        ArgumentNullException.ThrowIfNull(maskCodec);
        _spectrumList = spectrumList;
        _maskCodec = maskCodec;
    }

    /// <inheritdoc/>
    public void BuildDeconvBlock(int spectrumIndex, IReadOnlyList<int> muxIndices,
        out Matrix<double> masks, out Matrix<double> signal)
    {
        if (_spectrumList is null || _maskCodec is null)
            throw new InvalidOperationException(
                "BuildDeconvBlock() Null SpectrumList or IPrecursorMaskCodec; OverlapDemultiplexer may not have been initialized.");

        var deconvSpectrum = _spectrumList.GetSpectrum(spectrumIndex, getBinaryData: true);
        var mzArr = deconvSpectrum.GetMZArray() ?? throw new InvalidOperationException("Deconv spectrum has no m/z array");
        var peakExtractor = new SpectrumPeakExtractor(mzArr.Data, _params.MassError);

        int numMuxSpectra = OverlapRegionsInApprox;
        int numDemuxSpectra = OverlapRegionsInApprox;
        int numTransitions = mzArr.Data.Count;
        masks = DenseMatrix.Create(numMuxSpectra, numDemuxSpectra, 0);
        signal = DenseMatrix.Create(numMuxSpectra, numTransitions, 0);

        // Center-of-mass of the demuxed spectrum's window indices.
        var deconvIndices = new List<int>();
        _maskCodec.SpectrumToIndices(deconvSpectrum, deconvIndices);
        double centerOfDeconvIndices = deconvIndices.Average();

        // Choose a contiguous lower-bound demux-window slice of length OverlapRegionsInApprox
        // centered roughly on the demuxed spectrum's m/z position.
        int idealLowerMzBound = (int)System.Math.Round(centerOfDeconvIndices - OverlapRegionsInApprox / 2.0);
        int lowerMzBound = System.Math.Max(idealLowerMzBound, 0);
        lowerMzBound = System.Math.Min(lowerMzBound, _maskCodec.NumDemuxWindows - OverlapRegionsInApprox);

        // Score each candidate mux spectrum by how close its window center-of-mass is to ours.
        var demuxWindowDistances = new List<(double Distance, int ScanIndex)>();
        var offsetIndices = new List<int>();
        foreach (var scanIndex in muxIndices)
        {
            var offsetSpectrum = _spectrumList.GetSpectrum(scanIndex, getBinaryData: true);
            _maskCodec.SpectrumToIndices(offsetSpectrum, offsetIndices);
            double centerOfOffsetIndices = offsetIndices.Average();
            demuxWindowDistances.Add((centerOfOffsetIndices - centerOfDeconvIndices, scanIndex));
        }

        // Sort by absolute distance (cpp uses an explicit |left|<|right| with 1e-3 tie-breaker).
        demuxWindowDistances.Sort((a, b) =>
        {
            double aa = System.Math.Abs(a.Distance);
            double bb = System.Math.Abs(b.Distance);
            if (System.Math.Abs(aa - bb) <= 1e-3) return 0;
            return aa < bb ? -1 : 1;
        });

        // Take the closest OverlapRegionsInApprox, then resort by signed distance for predictable
        // row order in the block.
        var bestMaskAverages = demuxWindowDistances.Take(OverlapRegionsInApprox).ToList();
        bestMaskAverages.Sort((a, b) =>
        {
            if (System.Math.Abs(a.Distance - b.Distance) <= 1e-3) return 0;
            return a.Distance < b.Distance ? -1 : 1;
        });
        var scansInDeconv = bestMaskAverages.Select(p => p.ScanIndex).ToList();

        // Fill the masks matrix from each chosen mux spectrum's m/z slice.
        for (int row = 0; row < scansInDeconv.Count; row++)
        {
            var muxSpectrum = _spectrumList.GetSpectrum(scansInDeconv[row], getBinaryData: true);
            var fullMaskRow = _maskCodec.GetMask(muxSpectrum);
            for (int col = 0; col < numDemuxSpectra; col++)
                masks[row, col] = fullMaskRow[lowerMzBound + col];
        }

        // Fill the signal matrix.
        if (_params.InterpolateRetentionTime)
        {
            double deconvStartTime = TryGetStartTime(deconvSpectrum)
                ?? throw new InvalidOperationException(
                    "BuildDeconvBlock() MS2 spectrum missing scan_start_time required for interpolation.");

            // For each chosen mux spectrum, find the interpolant's cycles' worth of identically-isolated
            // spectra (stride = SpectraPerCycle), record their (RT, binned-intensities) pairs, and
            // interpolate to the demuxed spectrum's RT.
            int specPerCycle = _maskCodec.SpectraPerCycle;
            var binnedIntensitiesCache = new List<Matrix<double>>();
            var scanTimesCache = new List<double[]>();
            var rowInterpolation = new List<DemuxInterpolation>();
            for (int row = 0; row < scansInDeconv.Count; row++)
            {
                var interpolation = _params.Interpolation;
                var interpolationIndices = new List<int>();
                if (!FindInterpolationSpectra(interpolationIndices, scansInDeconv[row], deconvStartTime,
                        RetentionTimeInterpolator.Samples(interpolation), specPerCycle))
                {
                    // Too short a run for a wider interpolant: fall back to the 3-point natural spline.
                    interpolation = DemuxInterpolation.NaturalThreePoint;
                    if (RetentionTimeInterpolator.Samples(_params.Interpolation) <= 3 ||
                        !FindInterpolationSpectra(interpolationIndices, scansInDeconv[row], deconvStartTime,
                            RetentionTimeInterpolator.Samples(interpolation), specPerCycle))
                    {
                        throw new InvalidOperationException(
                            "BuildDeconvBlock() Not enough spectra to interpolate for the overlap.");
                    }
                }

                var binMatrix = DenseMatrix.Create(interpolationIndices.Count, numTransitions, 0);
                var times = new double[interpolationIndices.Count];
                for (int i = 0; i < interpolationIndices.Count; i++)
                {
                    var s = _spectrumList.GetSpectrum(interpolationIndices[i], getBinaryData: true);
                    times[i] = TryGetStartTime(s)
                        ?? throw new InvalidOperationException(
                            "BuildDeconvBlock() MS2 spectrum missing scan_start_time required for interpolation.");
                    peakExtractor.Extract(s, binMatrix, i);
                }
                binnedIntensitiesCache.Add(binMatrix);
                scanTimesCache.Add(times);
                rowInterpolation.Add(interpolation);
            }

            for (int row = 0; row < OverlapRegionsInApprox; row++)
                InterpolateMuxRegion(signal, row, deconvStartTime,
                    binnedIntensitiesCache[row], scanTimesCache[row], rowInterpolation[row]);
        }
        else
        {
            int specPerCycle = _maskCodec.SpectraPerCycle;
            for (int row = 0; row < scansInDeconv.Count; row++)
            {
                var s = _spectrumList.GetSpectrum(scansInDeconv[row], getBinaryData: true);
                double weight = 1.0;
                if (_params.ApplyWeighting)
                {
                    int scanDiff = spectrumIndex - scansInDeconv[row];
                    double t = 5.0 * scanDiff / specPerCycle;
                    weight = 1.0 / (1.0 + t * t);
                }
                peakExtractor.Extract(s, signal, row, weight);
            }
        }

        // Cache the spectrum's demux-window indices (relative to the slice we picked above).
        _spectrumIndices = new List<int>();
        foreach (var demuxIndex in deconvIndices)
        {
            if (demuxIndex < lowerMzBound)
                throw new InvalidOperationException(
                    "BuildDeconvBlock() Demux index slipped below the chosen lower bound — should not happen.");
            _spectrumIndices.Add(demuxIndex - lowerMzBound);
        }
    }

    /// <inheritdoc/>
    public void GetMatrixBlockIndices(int indexToDemux, List<int> muxIndices, double demuxBlockExtra = 0.0)
    {
        if (_spectrumList is null || _maskCodec is null)
            throw new InvalidOperationException(
                "GetMatrixBlockIndices() Null SpectrumList or IPrecursorMaskCodec; OverlapDemultiplexer may not have been initialized.");

        demuxBlockExtra = System.Math.Max(0.0, demuxBlockExtra);
        int numSpectraToFind = _maskCodec.SpectraPerCycle +
            (int)System.Math.Round(demuxBlockExtra * _maskCodec.SpectraPerCycle);
        if (!DemuxHelpers.FindNearbySpectra(muxIndices, _spectrumList, indexToDemux, numSpectraToFind))
            throw new InvalidOperationException(
                "GetMatrixBlockIndices() Not enough spectra to demultiplex this block");
    }

    /// <summary>The <paramref name="samples"/> acquisitions of a mux spectrum's window to interpolate
    /// through. An even count is split evenly around the target time, not around the center scan.</summary>
    private bool FindInterpolationSpectra(List<int> indices, int centerIndex, double targetTime, int samples,
        int specPerCycle)
    {
        int backward = -1;
        if (samples % 2 == 0)
        {
            // With binary data: the cache holds decoded spectra, and this one is read for interpolation anyway.
            double centerTime = TryGetStartTime(_spectrumList!.GetSpectrum(centerIndex, getBinaryData: true)) ?? targetTime;
            backward = centerTime > targetTime ? samples / 2 : samples / 2 - 1;
        }
        return DemuxHelpers.FindNearbySpectra(indices, _spectrumList!, centerIndex, samples, specPerCycle, backward);
    }

    private static void InterpolateMuxRegion(Matrix<double> signal, int rowToFill,
        double timeToInterpolate, Matrix<double> intensities, double[] scanTimes,
        DemuxInterpolation interpolation)
    {
        int numTransitions = intensities.ColumnCount;
        if (interpolation == DemuxInterpolation.NaturalSpline)
        {
            for (int transition = 0; transition < numTransitions; transition++)
            {
                var values = new double[scanTimes.Length];
                for (int i = 0; i < values.Length; i++) values[i] = intensities[i, transition];
                double interp = InterpolateOne(timeToInterpolate, scanTimes, values);
                signal[rowToFill, transition] = System.Math.Max(0.0, interp);
            }
            return;
        }

        // The interpolants need increasing times; FindNearbySpectra returns file order, so this sort
        // is a guard, done once for every transition. Acquisition times are distinct, so it is stable enough.
        int n = scanTimes.Length;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        var sortedTimes = (double[])scanTimes.Clone();
        Array.Sort(sortedTimes, order);
        var sortedValues = new double[n];
        for (int transition = 0; transition < numTransitions; transition++)
        {
            for (int i = 0; i < n; i++) sortedValues[i] = intensities[order[i], transition];
            double interp = RetentionTimeInterpolator.Interpolate(interpolation, sortedTimes, sortedValues, timeToInterpolate);
            signal[rowToFill, transition] = System.Math.Max(0.0, interp);
        }
    }

    private static double InterpolateOne(double pointToInterpolate, double[] points, double[] values)
    {
        // Cpp's "CubicHermiteSpline" wraps James Bremner's CSpline library, which is actually a
        // NATURAL cubic spline (zero 2nd derivative at endpoints), not Hermite — MathNet's
        // InterpolateNatural is the matching algorithm. Both expect strictly-increasing points.
        var spline = CubicSpline.InterpolateNatural(points, values);
        return spline.Interpolate(pointToInterpolate);
    }

    private static double? TryGetStartTime(Spectrum s)
    {
        if (s.ScanList.Scans.Count == 0) return null;
        var p = s.ScanList.Scans[0].Params.CvParam(CVID.MS_scan_start_time);
        if (p.Cvid == CVID.CVID_Unknown) return null;
        return p.ValueAs<double>();
    }
}
