namespace Pwiz.Data.Common.Index;

/// <summary>
/// In-memory <see cref="IIndex"/> implementation.
/// Find(id) is O(1) via hash lookup (C++ used O(logN) std::map — we upgrade for free).
/// Find(ordinal) is O(1).
/// </summary>
/// <remarks>Port of pwiz/data/common/MemoryIndex.hpp/cpp.</remarks>
public sealed class MemoryIndex : IIndex
{
    private readonly Dictionary<string, IndexEntry> _byId = new(StringComparer.Ordinal);
    private readonly List<IndexEntry> _byOrdinal = new();

    /// <inheritdoc/>
    public void Create(List<IndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byId.Clear();
        _byOrdinal.Clear();
        _byOrdinal.Capacity = entries.Count;

        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
            _byOrdinal.Add(entry);
        }
    }

    /// <inheritdoc/>
    public int Count => _byId.Count;

    /// <inheritdoc/>
    public IndexEntry? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.TryGetValue(id, out var entry) ? entry : null;
    }

    /// <inheritdoc/>
    public IndexEntry? Find(int index) =>
        (uint)index < (uint)_byOrdinal.Count ? _byOrdinal[index] : null;
}
