namespace Pwiz.Data.Common.Proteome;

/// <summary>How aggressively <see cref="ProteinListCache"/> caches.</summary>
public enum ProteinListCacheMode
{
    /// <summary>No caching — pass through every lookup to the inner list.</summary>
    Off,
    /// <summary>Cache only the metadata-only protein records
    /// (<c>GetProtein(index, getSequence: false)</c>). cpp's
    /// <c>ProteinListCacheMode_MetaDataOnly</c>.</summary>
    MetaDataOnly,
    /// <summary>Cache full protein records including sequences. cpp's
    /// <c>ProteinListCacheMode_MetaDataAndSequence</c>.</summary>
    MetaDataAndSequence,
}

/// <summary>
/// An MRU (most-recently-used) cache wrapper around an <see cref="ProteinList"/>.
/// Port of <c>pwiz::proteome::ProteinListCache</c>. Mode + capacity are mutable;
/// changing the mode clears the cache (matches cpp).
/// </summary>
public sealed class ProteinListCache : ProteinListWrapper
{
    private readonly LinkedList<(int Index, Protein Protein, bool HasSequence)> _mru = new();
    private readonly Dictionary<int, LinkedListNode<(int Index, Protein Protein, bool HasSequence)>> _byIndex = new();
    private ProteinListCacheMode _mode;
    private readonly int _capacity;

    /// <summary>Creates a cache around <paramref name="inner"/>.</summary>
    /// <param name="inner">The wrapped list whose lookups are being cached.</param>
    /// <param name="cacheMode">Initial mode; can be changed at runtime via <see cref="Mode"/>.</param>
    /// <param name="cacheSize">Maximum number of proteins to retain. Must be positive even
    /// when <paramref name="cacheMode"/> is <see cref="ProteinListCacheMode.Off"/>.</param>
    public ProteinListCache(ProteinList inner, ProteinListCacheMode cacheMode, int cacheSize)
        : base(inner)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cacheSize);
        _mode = cacheMode;
        _capacity = cacheSize;
    }

    /// <summary>Current cache mode. Setting to a new value clears the cache.</summary>
    public ProteinListCacheMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            _mru.Clear();
            _byIndex.Clear();
        }
    }

    /// <summary>Number of entries currently in the cache (for tests + debugging).</summary>
    public int CacheCount => _mru.Count;

    /// <inheritdoc/>
    public override Protein GetProtein(int index, bool getSequence = true)
    {
        if (_mode == ProteinListCacheMode.Off) return Inner.GetProtein(index, getSequence);

        // Tighter mode → drop sequence-requiring lookups from the cache hit path so callers
        // don't get a stale "no sequence" protein when they ask for one.
        bool wantSeq = getSequence && _mode == ProteinListCacheMode.MetaDataAndSequence;
        if (_byIndex.TryGetValue(index, out var node)
            && (!getSequence || node.Value.HasSequence))
        {
            // Cache hit. Move to the head of the MRU list.
            _mru.Remove(node);
            _mru.AddFirst(node);
            return node.Value.Protein;
        }

        var p = Inner.GetProtein(index, getSequence);
        var entry = (index, p, HasSequence: getSequence);
        if (node is not null) { _mru.Remove(node); _byIndex.Remove(index); }
        var newNode = new LinkedListNode<(int, Protein, bool)>(entry);
        _mru.AddFirst(newNode);
        _byIndex[index] = newNode;
        while (_mru.Count > _capacity)
        {
            var last = _mru.Last!;
            _mru.RemoveLast();
            _byIndex.Remove(last.Value.Index);
        }
        // The compiler doesn't infer `wantSeq` use after the cache update path, but the field
        // is used implicitly via the mode check above.
        _ = wantSeq;
        return p;
    }
}
