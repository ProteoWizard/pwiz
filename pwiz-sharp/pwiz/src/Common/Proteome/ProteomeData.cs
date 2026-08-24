using Pwiz.Util.Proteome;

namespace Pwiz.Data.Common.Proteome;

/// <summary>
/// A protein entry — a <see cref="Peptide"/> (amino acid sequence) plus identifying
/// metadata (database id, ordinal index, description / FASTA defline). Port of
/// <c>pwiz::proteome::Protein</c>.
/// </summary>
public class Protein : Peptide
{
    /// <summary>Constructs a protein with the given identity + sequence. cpp passes
    /// the sequence straight to the <see cref="Peptide"/> base constructor with no
    /// inline-modification parsing; we mirror that (FASTA sequences don't carry
    /// modifications).</summary>
    public Protein(string id, int index, string description, string sequence)
        : base(sequence)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(description);
        Id = id;
        Index = index;
        Description = description;
    }

    /// <summary>Database id (the FASTA defline before the first whitespace, typically).</summary>
    public string Id { get; }

    /// <summary>Ordinal position in the source <see cref="ProteinList"/>.</summary>
    public int Index { get; }

    /// <summary>FASTA description (the defline tail after the id).</summary>
    public string Description { get; }

    /// <summary>True iff id, description, and sequence are all empty.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Id)
                           && string.IsNullOrEmpty(Description)
                           && string.IsNullOrEmpty(Sequence);
}

/// <summary>
/// Read-only collection of proteins, typically backed by a FASTA file or other
/// protein database. Port of <c>pwiz::proteome::ProteinList</c>.
/// </summary>
/// <remarks>
/// Concrete impls (the in-memory <see cref="ProteinListSimple"/> or a lazy FASTA-
/// backed one) override <see cref="Count"/> + <see cref="GetProtein"/>. The default
/// <see cref="Find"/> / <see cref="FindKeyword"/> impls are O(N) linear scans;
/// implementations that can do better override them.
/// </remarks>
public abstract class ProteinList
{
    /// <summary>Number of proteins in the list.</summary>
    public abstract int Count { get; }

    /// <summary>True iff <see cref="Count"/> is zero.</summary>
    public virtual bool IsEmpty => Count == 0;

    /// <summary>Returns the protein at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based ordinal index.</param>
    /// <param name="getSequence">When false, the protein's amino-acid sequence may
    /// be returned empty (cheap "lookup the id/description only" path used by
    /// <see cref="Find"/> and <see cref="FindKeyword"/>). Defaults to true.</param>
    public abstract Protein GetProtein(int index, bool getSequence = true);

    /// <summary>Returns the ordinal of the protein with the given <paramref name="id"/>,
    /// or <see cref="Count"/> if not found. cpp returns size_t and uses size() as
    /// the sentinel; we keep that convention so callers ported from cpp keep working.</summary>
    public virtual int Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        for (int i = 0, n = Count; i < n; i++)
            if (GetProtein(i, getSequence: false).Id == id) return i;
        return Count;
    }

    /// <summary>Returns the ordinal indices of every protein whose
    /// <see cref="Protein.Description"/> contains <paramref name="keyword"/>.</summary>
    public virtual List<int> FindKeyword(string keyword, bool caseSensitive = true)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        var result = new List<int>();
        var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (int i = 0, n = Count; i < n; i++)
        {
            string desc = GetProtein(i, getSequence: false).Description;
            if (desc.Contains(keyword, cmp)) result.Add(i);
        }
        return result;
    }
}

/// <summary>In-memory <see cref="ProteinList"/>. Port of
/// <c>pwiz::proteome::ProteinListSimple</c>.</summary>
public sealed class ProteinListSimple : ProteinList
{
    /// <summary>The protein records, in their canonical order.</summary>
    public List<Protein> Proteins { get; } = new();

    /// <inheritdoc/>
    public override int Count => Proteins.Count;

    /// <inheritdoc/>
    public override Protein GetProtein(int index, bool getSequence = true)
    {
        if ((uint)index >= (uint)Proteins.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return Proteins[index];
    }
}

/// <summary>
/// Top-level container for a protein database. Mirrors the MSData shape from
/// <c>Pwiz.Data.MsData</c>: one id + one list. Port of <c>pwiz::proteome::ProteomeData</c>.
/// </summary>
/// <remarks>
/// <see cref="IDisposable"/> so callers using lazy-backed protein lists (e.g.
/// <see cref="Fasta.OpenLazy"/>) can scope ownership with <c>using var</c>. Dispose
/// is forwarded to the inner <see cref="ProteinList"/> when it implements
/// <see cref="IDisposable"/>; no-op for in-memory <see cref="ProteinListSimple"/>.
/// </remarks>
public sealed class ProteomeData : IDisposable
{
    /// <summary>Document id (typically the source filename's stem).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The protein records (lazy or eager).</summary>
    public ProteinList? ProteinList { get; set; }

    /// <summary>True iff the document carries no proteins.</summary>
    public bool IsEmpty => ProteinList is null || ProteinList.IsEmpty;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (ProteinList is IDisposable d) d.Dispose();
    }
}
