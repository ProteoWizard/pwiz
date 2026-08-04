using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Filters;

/// <summary>
/// A view over another <see cref="ISpectrumList"/> that keeps only the spectra matching a predicate.
/// </summary>
/// <remarks>
/// Port of pwiz::analysis::SpectrumList_Filter. The index of accepted spectra is built once at construction
/// (eagerly walking the inner list's identities, only loading deep spectra for the NeedSpectrum case), then
/// lookups/reads pass through to the inner list.
/// </remarks>
public sealed class SpectrumListFilter : SpectrumListWrapper
{
    private readonly int[] _acceptedIndices;

    /// <summary>Builds a filter view over <paramref name="inner"/> using <paramref name="predicate"/>.</summary>
    public SpectrumListFilter(ISpectrumList inner, ISpectrumPredicate predicate)
        : base(inner)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var indices = new List<int>();
        for (int i = 0; i < inner.Count; i++)
        {
            if (predicate.Done) break;

            var decision = predicate.Accept(inner.SpectrumIdentity(i));
            bool accept = decision switch
            {
                PredicateDecision.Accept => true,
                PredicateDecision.Reject => false,
                _ => predicate.Accept(inner.GetSpectrum(i, predicate.SuggestedDetailLevel >= DetailLevel.FullData)),
            };
            if (accept) indices.Add(i);
        }
        _acceptedIndices = indices.ToArray();
    }

    /// <inheritdoc/>
    public override int Count => _acceptedIndices.Length;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index)
    {
        // Renumber Index to the position in the *filtered* list, matching cpp
        // (SpectrumList_Filter.cpp:112, which rewrites the copied identity's index) and the
        // sibling SpectrumListSorter. Consumers key off it - mzML writes it as
        // <spectrum index="...">, which has to be dense and ascending. Copy rather than mutate:
        // SpectrumIdentity is a reference type, so writing to the inner list's instance would
        // corrupt the unfiltered list.
        var orig = Inner.SpectrumIdentity(_acceptedIndices[index]);
        return new ProxiedSpectrumIdentity
        {
            Index = index,
            Id = orig.Id,
            SpotId = orig.SpotId,
            SourceFilePosition = orig.SourceFilePosition,
        };
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        // cpp copies the spectrum before renumbering (SpectrumList_Filter.cpp:150-151); we set
        // it in place, as SpectrumListSorter does, so a lazy reader's per-call instance is
        // stamped without an extra deep copy.
        var spec = Inner.GetSpectrum(_acceptedIndices[index], getBinaryData);
        spec.Index = index;
        return spec;
    }

    private sealed class ProxiedSpectrumIdentity : SpectrumIdentity { }
}
