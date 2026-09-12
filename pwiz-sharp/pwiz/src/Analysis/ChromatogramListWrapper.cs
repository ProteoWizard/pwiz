using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis;

/// <summary>
/// Base class for <see cref="IChromatogramList"/> decorators that wrap an inner list.
/// Default behavior is pass-through — subclasses override just the parts they transform.
/// </summary>
/// <remarks>
/// Port of <c>pwiz::analysis::ChromatogramListWrapper</c>. Like the cpp version, the
/// constructor seeds a <c>pwiz_Chromatogram_Processing</c> DataProcessing (or clones the
/// inner one) so subclasses can append their own ProcessingMethods to <see cref="Dp"/>.
/// </remarks>
public abstract class ChromatogramListWrapper : ChromatogramListBase
{
    /// <summary>The wrapped inner list.</summary>
    protected IChromatogramList Inner { get; }

    /// <summary>The wrapper's data-processing chain. Subclasses append their own
    /// <see cref="ProcessingMethod"/> entries here.</summary>
    protected DataProcessing Dp { get; }

    /// <summary>Creates a wrapper around <paramref name="inner"/>.</summary>
    /// <remarks>
    /// Disposing this wrapper cascades disposal to <see cref="Inner"/>. When you need
    /// independent parallel wrappers around the SAME inner list, share an inner-list
    /// instance via <see cref="Pwiz.Util.Misc.ReferenceCounted{T}"/> — each wrapper's
    /// Dispose drops one share and the inner list is only released when the last
    /// share is dropped.
    /// </remarks>
    protected ChromatogramListWrapper(IChromatogramList inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;

        // Clone the inner data-processing chain (so wrappers can append without
        // mutating the original), or seed a fresh chain. Matches cpp:
        // setDataProcessingPtr(inner->dataProcessingPtr() ? copy(...) : "pwiz_Chromatogram_Processing")
        var innerDp = inner.DataProcessing;
        if (innerDp is not null)
        {
            Dp = new DataProcessing(innerDp.Id);
            foreach (var pm in innerDp.ProcessingMethods)
                Dp.ProcessingMethods.Add(pm);
        }
        else
        {
            Dp = new DataProcessing("pwiz_Chromatogram_Processing");
        }
    }

    /// <inheritdoc/>
    public override int Count => Inner.Count;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => Inner.ChromatogramIdentity(index);

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false) =>
        Inner.GetChromatogram(index, getBinaryData);

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        Inner.Dispose();
        base.DisposeCore();
    }
}
