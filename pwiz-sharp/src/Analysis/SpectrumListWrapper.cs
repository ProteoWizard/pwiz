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
    /// <remarks>
    /// Disposing this wrapper cascades disposal to <see cref="Inner"/>. When you need
    /// independent parallel wrappers around the SAME inner list, wrap the inner in a
    /// <see cref="Pwiz.Util.Misc.ReferenceCounted{T}"/> and hand each wrapper its own share
    /// (<see cref="Pwiz.Util.Misc.ReferenceCounted{T}.AddRef"/>) — each wrapper's Dispose
    /// drops one share, and the inner list is only released when the last share is dropped.
    /// </remarks>
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

    /// <summary>
    /// Drops this wrapper's share of <see cref="Inner"/>. The inner list isn't actually
    /// freed until ALL its holders have disposed — sibling wrappers and the document
    /// Run can keep using it after one wrapper is gone.
    /// </summary>
    protected override void DisposeCore()
    {
        Inner.Dispose();
        base.DisposeCore();
    }
}
