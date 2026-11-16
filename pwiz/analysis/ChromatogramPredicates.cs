using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Filters;

/// <summary>
/// Accepts chromatograms whose ordinal index is in the given <see cref="IntegerSet"/>.
/// Port of <c>pwiz::analysis::ChromatogramList_FilterPredicate_IndexSet</c>.
/// </summary>
public sealed class ChromatogramIndexSetPredicate : IChromatogramPredicate
{
    private readonly IntegerSet _set;
    private bool _eos;

    /// <summary>Creates a predicate over the given set of indices.</summary>
    public ChromatogramIndexSetPredicate(IntegerSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    /// <inheritdoc/>
    public PredicateDecision Accept(ChromatogramIdentity identity)
    {
        if (_set.HasUpperBound(identity.Index)) _eos = true;
        return _set.Contains(identity.Index) ? PredicateDecision.Accept : PredicateDecision.Reject;
    }

    /// <inheritdoc/>
    public bool Done => _eos;

    /// <inheritdoc/>
    public string Describe() => "set of chromatogram indices";
}
