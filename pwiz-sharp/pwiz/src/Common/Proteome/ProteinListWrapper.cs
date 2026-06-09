namespace Pwiz.Data.Common.Proteome;

/// <summary>
/// Base class for <see cref="ProteinList"/> decorators that wrap an inner list.
/// Default behavior is pure pass-through; subclasses override only the parts they
/// transform (caching, filtering, etc.). Port of <c>pwiz::proteome::ProteinListWrapper</c>.
/// </summary>
public abstract class ProteinListWrapper : ProteinList
{
    /// <summary>The wrapped inner list.</summary>
    protected ProteinList Inner { get; }

    /// <summary>Creates a wrapper around <paramref name="inner"/>.</summary>
    protected ProteinListWrapper(ProteinList inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        Inner = inner;
    }

    /// <inheritdoc/>
    public override int Count => Inner.Count;

    /// <inheritdoc/>
    public override bool IsEmpty => Inner.IsEmpty;

    /// <inheritdoc/>
    public override Protein GetProtein(int index, bool getSequence = true) =>
        Inner.GetProtein(index, getSequence);

    /// <inheritdoc/>
    public override int Find(string id) => Inner.Find(id);

    /// <inheritdoc/>
    public override List<int> FindKeyword(string keyword, bool caseSensitive = true) =>
        Inner.FindKeyword(keyword, caseSensitive);
}
