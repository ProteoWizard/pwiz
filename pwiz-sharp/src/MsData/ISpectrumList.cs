using Pwiz.Data.MsData.Processing;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Read-only access to a (possibly file-backed, possibly lazy) collection of spectra.
/// Port of pwiz::msdata::SpectrumList.
/// </summary>
public interface ISpectrumList
{
    /// <summary>Number of spectra in the list.</summary>
    int Count { get; }

    /// <summary>True iff the list is empty and has no data-processing reference.</summary>
    bool IsEmpty { get; }

    /// <summary>Returns just the identity (index/id/spot/offset) for spectrum at <paramref name="index"/>.</summary>
    SpectrumIdentity SpectrumIdentity(int index);

    /// <summary>Returns the ordinal of the spectrum with the given id, or <see cref="Count"/> if not found.</summary>
    int Find(string id);

    /// <summary>
    /// Returns the ordinal for an abbreviated id like <c>"1.1.123.2"</c>
    /// (equivalent to <c>"sample=1 period=1 cycle=123 experiment=2"</c>), or <see cref="Count"/>.
    /// </summary>
    int FindAbbreviated(string abbreviatedId, char delimiter = '.');

    /// <summary>Returns ordinals of all spectra matching a given name/value pair in their id.</summary>
    IReadOnlyList<int> FindNameValue(string name, string value);

    /// <summary>Returns ordinals of all spectra with the given MALDI spot id.</summary>
    IReadOnlyList<int> FindSpotId(string spotId);

    /// <summary>Retrieves a spectrum by index, optionally with binary data populated.</summary>
    Spectrum GetSpectrum(int index, bool getBinaryData = false);

    /// <summary>Retrieves a spectrum at the requested detail level.</summary>
    Spectrum GetSpectrum(int index, DetailLevel detailLevel);

    /// <summary>Data processing applied by this list (may be null).</summary>
    DataProcessing? DataProcessing { get; }

    /// <summary>True iff the source reader deliberately skipped calibration spectra (e.g. Waters lockmass).</summary>
    bool CalibrationSpectraAreOmitted { get; }

    /// <summary>Writes a warning once per list instance (deduplicates by message hash).</summary>
    void WarnOnce(string message);
}

/// <summary>
/// Helpful base class for <see cref="ISpectrumList"/> implementations: provides the default
/// linear <see cref="Find(string)"/> + ordinary id parsing and the warn-once book-keeping.
/// Port of pwiz::msdata::SpectrumListBase.
/// </summary>
public abstract class SpectrumListBase : ISpectrumList
{
    private readonly HashSet<int> _warned = new();

    /// <inheritdoc/>
    public abstract int Count { get; }

    /// <inheritdoc/>
    public virtual bool IsEmpty => Count == 0 && DataProcessing is null;

    /// <inheritdoc/>
    public abstract SpectrumIdentity SpectrumIdentity(int index);

    /// <inheritdoc/>
    public abstract Spectrum GetSpectrum(int index, bool getBinaryData = false);

    /// <inheritdoc/>
    public virtual Spectrum GetSpectrum(int index, DetailLevel detailLevel) =>
        GetSpectrum(index, detailLevel >= DetailLevel.FullData);

    /// <inheritdoc/>
    public virtual DataProcessing? DataProcessing => null;

    /// <inheritdoc/>
    public virtual bool CalibrationSpectraAreOmitted => false;

    /// <inheritdoc/>
    public virtual int Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        for (int i = 0; i < Count; i++)
            if (SpectrumIdentity(i).Id == id) return i;
        return Count;
    }

    /// <inheritdoc/>
    public virtual int FindAbbreviated(string abbreviatedId, char delimiter = '.')
    {
        ArgumentNullException.ThrowIfNull(abbreviatedId);
        for (int i = 0; i < Count; i++)
            if (Id.Abbreviate(SpectrumIdentity(i).Id, delimiter) == abbreviatedId) return i;
        return Count;
    }

    /// <inheritdoc/>
    public virtual IReadOnlyList<int> FindNameValue(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        var results = new List<int>();
        for (int i = 0; i < Count; i++)
        {
            if (Id.Value(SpectrumIdentity(i).Id, name) == value)
                results.Add(i);
        }
        return results;
    }

    /// <inheritdoc/>
    public virtual IReadOnlyList<int> FindSpotId(string spotId)
    {
        ArgumentNullException.ThrowIfNull(spotId);
        var results = new List<int>();
        for (int i = 0; i < Count; i++)
            if (SpectrumIdentity(i).SpotId == spotId) results.Add(i);
        return results;
    }

    /// <inheritdoc/>
    public virtual void WarnOnce(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        int hash = message.GetHashCode(StringComparison.Ordinal);
        if (_warned.Add(hash))
            Console.Error.WriteLine(message);
    }
}

/// <summary>
/// In-memory <see cref="ISpectrumList"/>. Port of pwiz::msdata::SpectrumListSimple.
/// </summary>
public sealed class SpectrumListSimple : SpectrumListBase
{
    /// <summary>The spectra.</summary>
    public List<Spectrum> Spectra { get; } = new();

    /// <summary>Data processing applied by this list.</summary>
    public DataProcessing? Dp { get; set; }

    /// <inheritdoc/>
    public override int Count => Spectra.Count;

    /// <inheritdoc/>
    public override bool IsEmpty => Spectra.Count == 0 && Dp is null;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index) => Spectra[index];

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false) => Spectra[index];

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => Dp;
}
