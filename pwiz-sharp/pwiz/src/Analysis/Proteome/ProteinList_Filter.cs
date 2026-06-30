using Pwiz.Data.Common.Proteome;
using Pwiz.Util.Misc;

#pragma warning disable CA1707 // underscored type names match cpp pwiz (ProteinList_Filter etc.)

namespace Pwiz.Analysis.Proteome;

/// <summary>
/// Predicate-driven sub-list of an existing <see cref="ProteinList"/>. Port of cpp's
/// <c>pwiz::analysis::ProteinList_Filter</c>.
/// </summary>
/// <remarks>
/// Walks the wrapped list once at construction time, calling
/// <see cref="IProteinFilterPredicate.Accept"/> on each protein. Iteration stops early
/// when <see cref="IProteinFilterPredicate.Done"/> returns true — predicates that key
/// off a sorted-by-index criterion can short-circuit.
/// <para>The predicate returns a nullable bool (cpp's <c>boost::logic::tribool</c>):
/// <c>true</c>/<c>false</c> are kept/dropped; <c>null</c> means "I need the sequence to
/// decide", and the filter retries with <c>getSequence=true</c>. If still null, the
/// protein is kept (cpp's default-accept-on-indeterminate behavior).</para>
/// </remarks>
public sealed class ProteinList_Filter : ProteinListWrapper
{
    private readonly List<int> _indexMap = new();

    /// <summary>Creates a filtered sub-list over <paramref name="inner"/>.</summary>
    public ProteinList_Filter(ProteinList inner, IProteinFilterPredicate predicate) : base(inner)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        // Walk the original list. We may need to flip getSequence to true mid-iteration
        // when a predicate returns null (indeterminate); revisit the same index in that case.
        bool getSequence = false;
        int n = inner.Count;
        for (int i = 0; i < n; i++)
        {
            if (predicate.Done) break;

            var protein = inner.GetProtein(i, getSequence);
            bool? accepted = predicate.Accept(protein);

            if (accepted == true || (accepted is null && getSequence))
            {
                _indexMap.Add(protein.Index);
            }
            else if (accepted == false)
            {
                // drop
            }
            else // indeterminate, sequence not yet loaded — retry this index with the sequence
            {
                getSequence = true;
                i--;
            }
        }
    }

    /// <inheritdoc/>
    public override int Count => _indexMap.Count;

    /// <inheritdoc/>
    public override Protein GetProtein(int index, bool getSequence = true)
    {
        if ((uint)index >= (uint)_indexMap.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        int originalIndex = _indexMap[index];
        var p = Inner.GetProtein(originalIndex, getSequence);
        // cpp clones the protein and rewrites its index to the filtered position;
        // sharp's Protein.Index is init-only, so allocate a new instance with the
        // filtered index. Sequence may be empty when getSequence=false.
        return new Protein(p.Id, index, p.Description, p.Sequence);
    }
}

/// <summary>Filter predicate hook for <see cref="ProteinList_Filter"/>.</summary>
public interface IProteinFilterPredicate
{
    /// <summary>Returns true / false to keep / drop; null when the sequence is needed
    /// to decide (cpp's <c>boost::tribool</c> indeterminate).</summary>
    bool? Accept(Protein protein);

    /// <summary>True iff no more proteins will be accepted — used to short-circuit
    /// iteration for predicates that know the upper bound of their match set.</summary>
    bool Done { get; }
}

/// <summary>Keeps proteins whose <see cref="Protein.Index"/> is in the given
/// <see cref="IntegerSet"/>. Stops iterating once the index is past the set's upper
/// bound. Port of cpp's <c>ProteinList_FilterPredicate_IndexSet</c>.</summary>
public sealed class ProteinFilterPredicate_IndexSet : IProteinFilterPredicate
{
    private readonly IntegerSet _indexSet;
    private bool _done;

    /// <summary>Creates a predicate matching the given <paramref name="indexSet"/>.</summary>
    public ProteinFilterPredicate_IndexSet(IntegerSet indexSet)
    {
        ArgumentNullException.ThrowIfNull(indexSet);
        _indexSet = indexSet;
    }

    /// <inheritdoc/>
    public bool? Accept(Protein protein)
    {
        if (_indexSet.HasUpperBound(protein.Index)) _done = true;
        return _indexSet.Contains(protein.Index);
    }

    /// <inheritdoc/>
    public bool Done => _done;
}

/// <summary>Keeps proteins whose <see cref="Protein.Id"/> is in the given set. The set
/// is consumed as matches are found, so iteration short-circuits once the set is empty.
/// Port of cpp's <c>ProteinList_FilterPredicate_IdSet</c>.</summary>
public sealed class ProteinFilterPredicate_IdSet : IProteinFilterPredicate
{
    private readonly HashSet<string> _ids;

    /// <summary>Creates a predicate matching the given <paramref name="ids"/>.</summary>
    public ProteinFilterPredicate_IdSet(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        _ids = new HashSet<string>(ids, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public bool? Accept(Protein protein) => _ids.Remove(protein.Id);

    /// <inheritdoc/>
    public bool Done => _ids.Count == 0;
}
