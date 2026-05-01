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
    public override SpectrumIdentity SpectrumIdentity(int index) =>
        Inner.SpectrumIdentity(_acceptedIndices[index]);

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) =>
        Inner.GetSpectrum(_acceptedIndices[index], getBinaryData);
}
