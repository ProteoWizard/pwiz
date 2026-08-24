using Pwiz.Data.MsData.Processing;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Read-only access to a (possibly file-backed) collection of chromatograms.
/// Port of pwiz::msdata::ChromatogramList. Implementations holding native handles override
/// <see cref="IDisposable.Dispose"/>.
/// </summary>
public interface IChromatogramList : IDisposable
{
    /// <summary>Number of chromatograms in the list.</summary>
    int Count { get; }

    /// <summary>True iff the list is empty and has no data-processing reference.</summary>
    bool IsEmpty { get; }

    /// <summary>Returns just the identity for chromatogram at <paramref name="index"/>.</summary>
    ChromatogramIdentity ChromatogramIdentity(int index);

    /// <summary>Returns the ordinal of the chromatogram with the given id, or <see cref="Count"/> if not found.</summary>
    int Find(string id);

    /// <summary>Retrieves a chromatogram by index, optionally with binary data populated.</summary>
    Chromatogram GetChromatogram(int index, bool getBinaryData = false);

    /// <summary>Retrieves a chromatogram at the requested detail level.</summary>
    Chromatogram GetChromatogram(int index, DetailLevel detailLevel);

    /// <summary>Data processing applied by this list (may be null).</summary>
    DataProcessing? DataProcessing { get; }

    /// <summary>Writes a warning once per list instance.</summary>
    void WarnOnce(string message);
}

/// <summary>
/// Base class with the common linear <see cref="Find(string)"/> and warn-once behavior.
/// Port of pwiz::msdata::ChromatogramListBase.
/// </summary>
public abstract class ChromatogramListBase : IChromatogramList
{
    private readonly HashSet<int> _warned = new();

    /// <inheritdoc/>
    public abstract int Count { get; }

    /// <inheritdoc/>
    public virtual bool IsEmpty => Count == 0 && DataProcessing is null;

    /// <inheritdoc/>
    public abstract ChromatogramIdentity ChromatogramIdentity(int index);

    /// <inheritdoc/>
    public abstract Chromatogram GetChromatogram(int index, bool getBinaryData = false);

    /// <inheritdoc/>
    public virtual Chromatogram GetChromatogram(int index, DetailLevel detailLevel) =>
        GetChromatogram(index, detailLevel >= DetailLevel.FullData);

    /// <inheritdoc/>
    public virtual DataProcessing? DataProcessing => null;

    /// <inheritdoc/>
    public virtual int Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        for (int i = 0; i < Count; i++)
            if (ChromatogramIdentity(i).Id == id) return i;
        return Count;
    }

    /// <inheritdoc/>
    public virtual void WarnOnce(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        int hash = message.GetHashCode(StringComparison.Ordinal);
        if (_warned.Add(hash))
            Console.Error.WriteLine(message);
    }

    // Idempotent disposal — same pattern as SpectrumListBase.
    private bool _disposed;

    /// <summary>Idempotent disposal. Runs <see cref="DisposeCore"/> exactly once.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>Subclasses with native handles override this to release them.</summary>
    protected virtual void DisposeCore() { }
}

/// <summary>
/// In-memory <see cref="IChromatogramList"/>. Port of pwiz::msdata::ChromatogramListSimple.
/// </summary>
public sealed class ChromatogramListSimple : ChromatogramListBase
{
    /// <summary>The chromatograms.</summary>
    public List<Chromatogram> Chromatograms { get; } = new();

    /// <summary>Data processing applied by this list.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override int Count => Chromatograms.Count;

    /// <inheritdoc/>
    public override bool IsEmpty => Chromatograms.Count == 0 && Dp is null;

    /// <inheritdoc/>
    public override ChromatogramIdentity ChromatogramIdentity(int index) => Chromatograms[index];

    /// <inheritdoc/>
    public override Chromatogram GetChromatogram(int index, bool getBinaryData = false) => Chromatograms[index];

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;
}
