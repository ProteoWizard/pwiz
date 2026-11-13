namespace Pwiz.Data.Common.Index;

/// <summary>
/// Entry in an index: string id, ordinal index, and stream offset.
/// </summary>
public sealed record IndexEntry(string Id, ulong Index, long Offset);

/// <summary>
/// Generic interface for creating and using an index on a stream of serialized objects.
/// </summary>
/// <remarks>Port of pwiz/data/common/Index.hpp (<c>pwiz::data::Index</c>).</remarks>
public interface IIndex
{
    /// <summary>
    /// Creates the index from the specified list of entries.
    /// The list is passed by reference because implementations may resort it in place.
    /// </summary>
    void Create(List<IndexEntry> entries);

    /// <summary>Returns the number of entries in the index.</summary>
    int Count { get; }

    /// <summary>Returns the entry for the specified string id, or null if the id is not in the index.</summary>
    IndexEntry? Find(string id);

    /// <summary>Returns the entry for the specified ordinal index, or null if the ordinal is out of range.</summary>
    IndexEntry? Find(int index);
}
