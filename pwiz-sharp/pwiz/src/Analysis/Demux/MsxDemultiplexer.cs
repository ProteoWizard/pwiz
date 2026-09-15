using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Chemistry;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// MSX-style demultiplexer. Port of cpp's <c>MSXDemultiplexer</c>. Use this for MSX
/// experiments (with or without overlap); for pure overlap data prefer
/// <see cref="OverlapDemultiplexer"/> which adds chromatographic interpolation.
/// </summary>
public sealed class MsxDemultiplexer : IDemultiplexer
{
    /// <summary>Tunable parameters.</summary>
    public sealed class Params
    {
        /// <summary>Mass tolerance for MS/MS peak extraction (cpp default 10 ppm).</summary>
        public MZTolerance MassError { get; init; } = new(10.0, MZToleranceUnits.Ppm);

        /// <summary>Down-weight nearby spectra by their retention-time distance from the spectrum
        /// being demuxed. Cpp default true.</summary>
        public bool ApplyWeighting { get; init; } = true;

        /// <summary>Set when fill times vary per scan window — multiplies each row's weight by
        /// the precursor's <c>MultiFillTime</c> sum / 1000 (seconds).</summary>
        public bool VariableFill { get; init; }
    }

    private readonly Params _params;
    private ISpectrumList? _spectrumList;
    private IPrecursorMaskCodec? _maskCodec;
    private List<int> _spectrumIndices = new();

    /// <summary>Constructs an MSX demultiplexer with the given parameters.</summary>
    public MsxDemultiplexer(Params? p = null)
    {
        _params = p ?? new Params();
    }

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
                "Null SpectrumList or IPrecursorMaskCodec; MsxDemultiplexer may not have been initialized.");

        var deconvSpectrum = _spectrumList.GetSpectrum(spectrumIndex, getBinaryData: true);
        var mzArr = deconvSpectrum.GetMZArray() ?? throw new InvalidOperationException("Deconv spectrum has no m/z array");
        var peakExtractor = new SpectrumPeakExtractor(mzArr.Data, _params.MassError);

        masks = DenseMatrix.Create(muxIndices.Count, _maskCodec.DemuxBlockSize, 0);
        signal = DenseMatrix.Create(muxIndices.Count, mzArr.Data.Count, 0);

        int specPerCycle = _maskCodec.SpectraPerCycle;
        for (int row = 0; row < muxIndices.Count; row++)
        {
            int currentIndex = muxIndices[row];
            var s = _spectrumList.GetSpectrum(currentIndex, getBinaryData: true);
            double weight = 1.0;
            if (_params.ApplyWeighting)
            {
                // cpp: weight = 1 / (1 + (5 * scanDiff / specPerCycle)^2). Damps far-away spectra
                // assuming a roughly Gaussian elution-peak shape with σ ≈ specPerCycle/5.
                int scanDiff = spectrumIndex - currentIndex;
                double t = 5.0 * scanDiff / specPerCycle;
                weight = 1.0 / (1.0 + t * t);
            }
            _maskCodec.GetMask(s, masks, row, weight);
            if (_params.VariableFill)
            {
                double totalInjectionTime = 0;
                foreach (var p in s.Precursors)
                {
                    var injectParam = p.UserParam("MultiFillTime");
                    if (injectParam.IsEmpty)
                        throw new InvalidOperationException(
                            "[MsxDemultiplexer] MS2 scan missing MultiFillTime user param required by variableFill demux.");
                    totalInjectionTime += injectParam.ValueAs<double>();
                }
                weight *= totalInjectionTime / 1000.0;
            }
            peakExtractor.Extract(s, signal, row, weight);
        }

        _spectrumIndices = new List<int>();
        _maskCodec.SpectrumToIndices(deconvSpectrum, _spectrumIndices);
    }

    /// <inheritdoc/>
    public void GetMatrixBlockIndices(int indexToDemux, List<int> muxIndices, double demuxBlockExtra = 0.0)
    {
        if (_spectrumList is null || _maskCodec is null)
            throw new InvalidOperationException(
                "Null SpectrumList or IPrecursorMaskCodec; MsxDemultiplexer may not have been initialized.");

        demuxBlockExtra = System.Math.Max(0.0, demuxBlockExtra);
        int numSpectraToFind = _maskCodec.DemuxBlockSize +
            (int)System.Math.Round(demuxBlockExtra * _maskCodec.SpectraPerCycle);
        if (!DemuxHelpers.FindNearbySpectra(muxIndices, _spectrumList, indexToDemux, numSpectraToFind))
            throw new InvalidOperationException(
                "GetMatrixBlockIndices() Not enough spectra to demultiplex this block");
    }
}
