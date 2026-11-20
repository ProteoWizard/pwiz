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

    /// <summary>The list at the bottom of any wrapper chain — i.e. the first ancestor
    /// that isn't itself a <see cref="SpectrumListWrapper"/>. Wrappers expose this so
    /// capability dispatch (e.g. <see cref="Pwiz.Data.MsData.Spectra.IIonMobilitySpectrumList"/>
    /// detection) can reach the vendor list under any stack of wrappers. Mirrors cpp
    /// <c>SpectrumListWrapper::innermost()</c>.</summary>
    public ISpectrumList Innermost
    {
        get
        {
            ISpectrumList cur = Inner;
            while (cur is SpectrumListWrapper w)
                cur = w.Inner;
            return cur;
        }
    }

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
    /// The inner spectrum at <paramref name="detailLevel"/>, below full metadata, for a wrapper
    /// that changes the peaks: nothing is changed without the peaks, and the inner peak count,
    /// which the change would make wrong, is dropped. cpp's <c>SpectrumList_PeakFilter</c> does
    /// the same.
    /// </summary>
    protected Spectrum GetSpectrumWithoutPeakCount(int index, DetailLevel detailLevel)
    {
        var spec = Inner.GetSpectrum(index, detailLevel);
        if (spec.DefaultArrayLength == 0)
            return spec;
        // A copy: some lists hand out their stored instance (SpectrumListSimple does).
        spec = spec.ShallowCopy();
        spec.DefaultArrayLength = 0;
        return spec;
    }

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
