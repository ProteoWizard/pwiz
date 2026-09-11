using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Filters;

/// <summary>Whether a predicate's accept-result is definite-true, definite-false, or needs the full spectrum.</summary>
public enum PredicateDecision
{
    /// <summary>Reject the spectrum without loading details.</summary>
    Reject,
    /// <summary>Accept the spectrum.</summary>
    Accept,
    /// <summary>Can't decide from identity metadata alone — load the spectrum and call the <see cref="Spectrum"/> overload.</summary>
    NeedSpectrum,
}

/// <summary>Include or exclude matches.</summary>
public enum FilterMode
{
    /// <summary>Keep spectra that match the predicate.</summary>
    Include,
    /// <summary>Keep spectra that do NOT match the predicate.</summary>
    Exclude,
}

/// <summary>
/// A predicate that decides whether a spectrum passes a filter. Two overloads: the cheap
/// <see cref="SpectrumIdentity"/> check and the deep <see cref="Spectrum"/> check used only
/// when the identity overload returns <see cref="PredicateDecision.NeedSpectrum"/>.
/// </summary>
/// <remarks>Port of pwiz::analysis::SpectrumList_Filter::Predicate (tribool → PredicateDecision).</remarks>
public interface ISpectrumPredicate
{
    /// <summary>
    /// Minimum detail level the filter needs when loading the deep spectrum.
    /// Override to <see cref="DetailLevel.FullData"/> for predicates that must inspect peaks.
    /// </summary>
    DetailLevel SuggestedDetailLevel => DetailLevel.InstantMetadata;

    /// <summary>Cheap metadata check. Return <see cref="PredicateDecision.NeedSpectrum"/> to defer to the deep overload.</summary>
    PredicateDecision Accept(SpectrumIdentity identity);

    /// <summary>Deep check — only called if <see cref="Accept(SpectrumIdentity)"/> returned <see cref="PredicateDecision.NeedSpectrum"/>.</summary>
    bool Accept(Spectrum spectrum) => false;

    /// <summary>
    /// Returns true when the predicate knows it won't accept anything further and iteration can stop early
    /// (e.g. a scan-time-range filter over a time-sorted list, past the upper bound).
    /// </summary>
    bool Done => false;

    /// <summary>Human-readable description for error messages and logs.</summary>
    string Describe();
}
