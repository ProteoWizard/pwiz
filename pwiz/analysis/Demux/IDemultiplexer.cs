using MathNet.Numerics.LinearAlgebra;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Builds the design matrix + signal matrix that an <see cref="IDemuxSolver"/> consumes for a
/// given multiplexed spectrum. Port of cpp's <c>IDemultiplexer</c>.
/// </summary>
public interface IDemultiplexer
{
    /// <summary>Wires the demultiplexer up to the source <see cref="ISpectrumList"/> and the
    /// inferred <see cref="IPrecursorMaskCodec"/>. Must be called before
    /// <see cref="BuildDeconvBlock"/>.</summary>
    void Initialize(ISpectrumList spectrumList, IPrecursorMaskCodec maskCodec);

    /// <summary>Builds the masks (design) and signal matrices for demultiplexing the spectrum
    /// at <paramref name="spectrumIndex"/>, using the multiplexed spectra at
    /// <paramref name="muxIndices"/> as input rows.</summary>
    void BuildDeconvBlock(int spectrumIndex, IReadOnlyList<int> muxIndices,
        out Matrix<double> masks, out Matrix<double> signal);

    /// <summary>Picks the indices of multiplexed MS2 spectra near <paramref name="indexToDemux"/>
    /// to assemble the demux block. Skips MS1 spectra; if not enough are available on one side,
    /// pulls extras from the other.</summary>
    void GetMatrixBlockIndices(int indexToDemux, List<int> muxIndices, double demuxBlockExtra = 0.0);

    /// <summary>The demux-window indices (in the solution matrix) corresponding to the most
    /// recent <see cref="BuildDeconvBlock"/>'s spectrum.</summary>
    IReadOnlyList<int> SpectrumIndices { get; }
}
