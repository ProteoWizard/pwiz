using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis;

/// <summary>
/// Base class for SpectrumList decorators that wrap an inner <see cref="ISpectrumList"/>.
/// Default behavior is pass-through — subclasses override just the parts they transform.
/// </summary>
/// <remarks>Port of pwiz::msdata::SpectrumListWrapper.</remarks>
public abstract class SpectrumListWrapper : SpectrumListBase
{
    /// <summary>The wrapped inner list.</summary>
    protected ISpectrumList Inner { get; }

    /// <summary>Creates a wrapper around <paramref name="inner"/>.</summary>
    protected SpectrumListWrapper(ISpectrumList inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
    }

    /// <inheritdoc/>
    public override int Count => Inner.Count;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => Inner.SpectrumIdentity(index);

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        Inner.GetSpectrum(index, getBinaryData);

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Inner.DataProcessing;

    /// <inheritdoc/>
    public override bool CalibrationSpectraAreOmitted => Inner.CalibrationSpectraAreOmitted;
}
