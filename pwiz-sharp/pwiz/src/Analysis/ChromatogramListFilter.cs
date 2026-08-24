using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Filters;

/// <summary>
/// A view over another <see cref="IChromatogramList"/> that keeps only the chromatograms
/// matching a predicate. Port of <c>pwiz::analysis::ChromatogramList_Filter</c>.
/// </summary>
/// <remarks>
/// The accepted-indices vector is built once at construction by iterating the inner list's
/// identities (and loading the full chromatogram only when the predicate's identity overload
/// returned <see cref="PredicateDecision.NeedSpectrum"/>). Subsequent reads pass through.
/// </remarks>
public sealed class ChromatogramListFilter : ChromatogramListWrapper
{
    private readonly int[] _acceptedIndices;

    /// <summary>Builds a filter view over <paramref name="inner"/> using
    /// <paramref name="predicate"/>.</summary>
    public ChromatogramListFilter(IChromatogramList inner, IChromatogramPredicate predicate)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var indices = new List<int>();
        for (int i = 0; i < inner.Count; i++)
        {
            if (predicate.Done) break;

            var decision = predicate.Accept(inner.ChromatogramIdentity(i));
            bool accept = decision switch
            {
                PredicateDecision.Accept => true,
                PredicateDecision.Reject => false,
                _ => predicate.Accept(inner.GetChromatogram(i, predicate.SuggestedDetailLevelNeedsBinary)),
            };
            if (accept) indices.Add(i);
        }
        _acceptedIndices = indices.ToArray();
    }

    /// <inheritdoc/>
    public override int Count => _acceptedIndices.Length;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index)
    {
        // Renumber the identity so callers see consecutive indices [0..Count-1] —
        // matches cpp ChromatogramList_Filter::Impl::pushChromatogram (line 97 of the cpp).
        var inner = Inner.ChromatogramIdentity(_acceptedIndices[index]);
        return new ChromatogramIdentity { Id = inner.Id, Index = index };
    }

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false)
    {
        var c = Inner.GetChromatogram(_acceptedIndices[index], getBinaryData);
        c.Index = index;
        return c;
    }
}
