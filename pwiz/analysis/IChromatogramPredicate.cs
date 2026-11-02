using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Filters;

/// <summary>
/// A predicate that decides whether a chromatogram passes a filter. Mirrors
/// <see cref="ISpectrumPredicate"/>: two overloads — the cheap
/// <see cref="ChromatogramIdentity"/> check and the deep <see cref="Chromatogram"/> check
/// used only when the identity overload returns <see cref="PredicateDecision.NeedSpectrum"/>.
/// </summary>
/// <remarks>Port of <c>pwiz::analysis::ChromatogramList_Filter::Predicate</c>
/// (tribool → <see cref="PredicateDecision"/>).</remarks>
public interface IChromatogramPredicate
{
    /// <summary>
    /// True when the predicate must inspect the full chromatogram payload (binary arrays) to
    /// decide. Most predicates (index, id) decide from the identity alone — defaults to false.
    /// </summary>
    bool SuggestedDetailLevelNeedsBinary => false;

    /// <summary>Cheap metadata check. Return <see cref="PredicateDecision.NeedSpectrum"/> to
    /// defer to the deep <see cref="Accept(Chromatogram)"/> overload.</summary>
    PredicateDecision Accept(ChromatogramIdentity identity);

    /// <summary>Deep check — only invoked when <see cref="Accept(ChromatogramIdentity)"/>
    /// returned <see cref="PredicateDecision.NeedSpectrum"/>.</summary>
    bool Accept(Chromatogram chromatogram) => false;

    /// <summary>Returns true when the predicate knows it won't accept anything further and
    /// iteration can stop early (e.g. an index-set predicate past its upper bound).</summary>
    bool Done => false;

    /// <summary>Human-readable description for error messages and logs.</summary>
    string Describe();
}
