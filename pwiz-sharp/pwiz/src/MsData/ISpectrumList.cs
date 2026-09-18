using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Processing;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// Read-only access to a (possibly file-backed, possibly lazy) collection of spectra.
/// Port of pwiz::msdata::SpectrumList. Implementations that hold native handles (vendor
/// readers) override <see cref="IDisposable.Dispose"/> to release them.
/// </summary>
public interface ISpectrumList : IDisposable
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

    /// <summary>
    /// Returns true iff <paramref name="searchedId"/> has the same native-id key set (format) as
    /// <paramref name="firstIdInList"/>. Port of pwiz::msdata::SpectrumList::checkNativeIdMatch.
    /// </summary>
    bool CheckNativeIdMatch(string firstIdInList, string searchedId);

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
    // A spectrum needs more peaks than this before finding it in m/z order is taken as evidence
    // about the writer rather than as coincidence.
    private const int MIN_PEAK_COUNT_FOR_MZ_SORT_CHECK = 10;

    // What this file has shown so far about the way its writer orders peaks.
    private const int MZ_ORDER_UNSETTLED = 0;
    private const int MZ_ORDER_WRITER_SORTS_BY_MZ = 1;
    private const int MZ_ORDER_WRITER_DOES_NOT_SORT_BY_MZ = 2;

    private readonly HashSet<int> _warned = new();

    // Interlocked because GetSpectrum is called from worker threads. Condemnation is an
    // unconditional store while the good verdict is a compare-exchange from unsettled, so no
    // late-arriving good spectrum can un-condemn a file.
    private int _mzOrderVerdict = MZ_ORDER_UNSETTLED;

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
        return CheckNativeIdFindResult(Count, id);
    }

    /// <summary>
    /// cpp's <c>SpectrumListBase::checkNativeIdFindResult</c>: when a lookup misses, a caller
    /// asking in the other of the two interchangeable id spellings is given the answer it meant.
    /// A list whose ids are <c>scan=N</c> answers an <c>index=N</c> lookup as <c>scan=N+1</c>, and
    /// one whose ids are <c>index=N</c> - MGF, which has no scan numbers - answers <c>scan=N</c>
    /// as <c>index=N-1</c>. Without it, anything holding a scan-based id cannot locate a spectrum
    /// in an MGF-derived list at all.
    /// </summary>
    /// <remarks>
    /// Recursion terminates: the retried id is in the same spelling as the list's own ids, so a
    /// second miss matches neither branch and returns <see cref="Count"/>. cpp additionally warns
    /// once about an id-format mismatch before giving up; the port just reports not-found.
    /// </remarks>
    protected int CheckNativeIdFindResult(int result, string id)
    {
        if (result < Count || Count == 0) return result;
        if (id.Length == 0) return Count;

        string firstId = SpectrumIdentity(0).Id;
        bool triedToFindScanByIndex = firstId.StartsWith("scan=", StringComparison.Ordinal)
                                      && id.StartsWith("index=", StringComparison.Ordinal);
        bool triedToFindIndexByScan = firstId.StartsWith("index=", StringComparison.Ordinal)
                                      && id.StartsWith("scan=", StringComparison.Ordinal);

        if (triedToFindScanByIndex
            && int.TryParse(Id.Value(id, "index"), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int indexValue))
            return Find("scan=" + (indexValue + 1).ToString(CultureInfo.InvariantCulture));

        if (triedToFindIndexByScan
            && int.TryParse(Id.Value(id, "scan"), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int scanValue))
            return Find("index=" + (scanValue - 1).ToString(CultureInfo.InvariantCulture));

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
    public virtual bool CheckNativeIdMatch(string firstIdInList, string searchedId)
    {
        ArgumentNullException.ThrowIfNull(firstIdInList);
        ArgumentNullException.ThrowIfNull(searchedId);
        // cpp parity: MSData.cpp:1170 - the id formats match iff both ids parse to the same set
        // of keys (e.g. one "scan" vs the other "scanId" is a mismatch).
        var actualKeys = new HashSet<string>(Id.Parse(firstIdInList).Keys);
        return actualKeys.SetEquals(Id.Parse(searchedId).Keys);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Locked because GetSpectrum runs on worker threads and HashSet is not safe for concurrent
    /// mutation - two threads inside Add can corrupt the bucket chain and spin there forever.
    /// cpp guards the same book-keeping with a boost::mutex (SpectrumListBase.cpp's ListBase).
    /// </remarks>
    public virtual void WarnOnce(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        int hash = message.GetHashCode(StringComparison.Ordinal);
        bool firstTime;
        lock (_warned)
            firstTime = _warned.Add(hash);
        if (firstTime)
            Console.Error.WriteLine(message);
    }

    /// <summary>
    /// Whether the peaks run along some axis other than m/z, in which case they must be left
    /// exactly as they are and say nothing about the writer.
    /// </summary>
    /// <remarks>
    /// <para>Three ways that happens. The mobility axis of a combined ion mobility scan, or a
    /// scanning quadrupole position: m/z then ascends only within each block, and a global sort
    /// would destroy the structure rather than repair it. The x-axis is not m/z at all -
    /// <see cref="Spectrum.GetMZArray"/> returns a wavelength array too, which the Agilent,
    /// Thermo and Bruker readers use for a diode-array trace, and judging a UV trace as if it
    /// were m/z would let it settle the verdict for every real spectrum in the file. Or each
    /// point is a transition rather than a peak - an SRM or CRM spectrum lists one point per
    /// monitored reaction in the order the method defined them, which is the order that matters,
    /// and the x-axis values are just those transitions' target m/z, not a scan across a
    /// continuum; nothing about that order is wrong, so there is nothing to repair.</para>
    /// <para><see cref="CVID.MS_SIM_spectrum"/> is deliberately NOT in that list, even though a
    /// SIM experiment rendered as spectra is transition-ordered in exactly the same way. The term
    /// is overloaded: the readers use it far more often for an ordinary scan than for a
    /// transition list - Thermo tags every ScanType_SIM scan with it, and Agilent maps both
    /// MSScanType_SelectedIon and MSScanType_TotalIon to it, the latter being a full-range MS1.
    /// Those are continuum scans carrying hundreds of points, so exempting the term would switch
    /// the repair off for the common case in order to protect the rare one. The trade is
    /// deliberate: an SRM-as-spectra file is protected by its own term, while --simAsSpectra
    /// output is not.</para>
    /// <para>Asked by name, not by counting arrays: counting cannot tell an ordering axis from an
    /// ordinary per-peak extra like signal-to-noise, and would refuse to repair any spectrum
    /// carrying one. Port of pwiz::msdata::hasNonMzOrderingAxis; public because the same question
    /// is asked again by tests checking that a round trip preserved ascending m/z order
    /// everywhere it is expected to hold.</para>
    /// </remarks>
    public static bool HasNonMzOrderingAxis(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);

        // The ion mobility term is asked for with its children, which covers the whole family -
        // mean, raw, inverse reduced, deconvoluted. GetArrayByCvid resolves through
        // referenceableParamGroups too, where mzML writers commonly factor out the repeated
        // binaryDataArray terms.
        return spectrum.GetArrayByCvid(CVID.MS_ion_mobility_array, true) is not null ||
               spectrum.GetArrayByCvid(CVID.MS_scanning_quadrupole_position_lower_bound_m_z_array) is not null ||
               spectrum.GetArrayByCvid(CVID.MS_scanning_quadrupole_position_upper_bound_m_z_array) is not null ||
               spectrum.GetArrayByCvid(CVID.MS_wavelength_array) is not null ||
               spectrum.HasCVParam(CVID.MS_SRM_spectrum) ||
               spectrum.HasCVParam(CVID.MS_CRM_spectrum);
    }

    /// <summary>
    /// Puts a spectrum's peaks in ascending m/z order if its writer did not, carrying every array
    /// that holds one value per peak along with them.
    /// </summary>
    /// <remarks>
    /// <para>Call this on the way out of GetSpectrum, from a list that reads a format some other
    /// tool wrote. Ascending m/z is nowhere required by any of those specifications, but it is
    /// what every consumer assumes: extraction binary searches the m/z axis, so a spectrum
    /// presented in another order makes the search land nowhere useful and the chromatogram comes
    /// out empty with no error at all. Writers that use another order do exist - one shipped
    /// peaks in ascending intensity - so the order is checked rather than trusted.</para>
    /// <para>The vendor lists do not call this, on the grounds that peaks arriving through a
    /// vendor API are already ascending; they live in other assemblies, which is what the
    /// internal visibility here records. That reasoning is weakest for the readers whose input is
    /// a file some other desktop tool wrote rather than an instrument stream - ABI T2D, UIMF,
    /// Mobilion, waters_connect - and nothing enforces the ordering for them; none has been
    /// observed to produce anything else.</para>
    /// <para>The question is settled from the first few spectra of a file rather than re-asked
    /// for every one, since walking every m/z array of every file to catch a rare writer is a
    /// cost the whole world would pay for the few. The first spectrum alone will not do: early
    /// scans can precede the sample and carry almost no peaks, and a spectrum with two of them
    /// ascends half the time by chance. So the two verdicts are not symmetric - one spectrum out
    /// of order proves the writer does not sort however few peaks it holds, while peaks found in
    /// order are only believed from a spectrum with enough of them to mean it.</para>
    /// </remarks>
    internal void EnsureMzAscending(Spectrum? spectrum)
    {
        if (spectrum is null || // Empty
            Volatile.Read(ref _mzOrderVerdict) == MZ_ORDER_WRITER_SORTS_BY_MZ) // Already established this file is fine
            return;

        // Nothing to reorder, and nothing to learn, until the binary data is actually here: a
        // metadata-only read still carries the array objects with their cvParams, since parsing
        // builds those and skips only the base64 decode. Fewer than two peaks is indeterminate
        // sortedness and settles nothing either way.
        var mzArray = spectrum.GetMZArray();
        if (mzArray is null || mzArray.Data.Count < 2)
            return;

        if (HasNonMzOrderingAxis(spectrum))
            return;

        var intensityArray = spectrum.GetIntensityArray();
        if (intensityArray is null)
            return;

        var mzs = mzArray.Data;
        if (mzs.Count != intensityArray.Data.Count) // Sanity check, flagged elsewhere if wrong
            return;

        if (IsAscending(mzs))
        {
            // Seems fine - but a short list can be in order by chance, so it does not settle
            // anything.
            if (mzs.Count > MIN_PEAK_COUNT_FOR_MZ_SORT_CHECK)
                Interlocked.CompareExchange(ref _mzOrderVerdict, MZ_ORDER_WRITER_SORTS_BY_MZ, MZ_ORDER_UNSETTLED);
            return;
        }

        // One spectrum out of order means any others may also be out of order. The exchange also
        // tells us whether this is the first such spectrum, which the warning below is keyed on.
        bool isFirstFoundSpectrumOutOfOrder =
            Interlocked.Exchange(ref _mzOrderVerdict, MZ_ORDER_WRITER_DOES_NOT_SORT_BY_MZ) !=
            MZ_ORDER_WRITER_DOES_NOT_SORT_BY_MZ;

        // A spectrum may carry other values like signal-to-noise, baseline, resolution or a charge
        // array alongside m/z and intensity, and every one of them has to be permuted the same
        // way. Stable, so peaks sharing an m/z keep the order the writer gave them.
        var order = StableSortOrder(mzs);
        ApplyPermutation(mzs, order);
        foreach (var array in spectrum.BinaryDataArrays)
        {
            if (!ReferenceEquals(array, mzArray) && array.Data.Count == order.Length)
                ApplyPermutation(array.Data, order);
        }
        foreach (var array in spectrum.IntegerDataArrays)
        {
            if (array.Data.Count == order.Length)
                ApplyPermutation(array.Data, order);
        }

        if (isFirstFoundSpectrumOutOfOrder)
        {
            WarnOnce($@"[SpectrumListBase] peaks were not written in ascending m/z order (first seen at ""{spectrum.Id}""). Reordering them in memory before use.");
        }
    }

    /// <summary>
    /// Applies <see cref="EnsureMzAscending"/> to every spectrum of a list that was read eagerly
    /// rather than served on demand.
    /// </summary>
    /// <remarks>
    /// cpp has no equivalent because its mzML and mzXML lists build their own index when a file
    /// has none, so a single lazy list covers both cases and one call inside it is enough. The
    /// port instead falls back to parsing the whole file into a <see cref="SpectrumListSimple"/>,
    /// which never reaches that call - and a file with no index is exactly the kind a third-party
    /// writer emits, so leaving the fallback out would miss the writers this repair exists for.
    /// Reading each spectrum here is free: they are already in memory.
    /// </remarks>
    internal static void EnsureMzAscendingThroughout(ISpectrumList? list)
    {
        // Deliberately narrower than SpectrumListBase: on a lazy list this loop would read every
        // spectrum of the file, which is the cost those lists exist to avoid. They repair on the
        // way out of GetSpectrum instead, and none of them reaches here.
        if (list is not SpectrumListSimple spectra)
            return;
        for (int i = 0; i < spectra.Count; i++)
            spectra.EnsureMzAscending(spectra.GetSpectrum(i, true));
    }

    /// <summary>True iff <paramref name="values"/> never descends.</summary>
    private static bool IsAscending(List<double> values)
    {
        for (int i = 1; i < values.Count; i++)
        {
            if (values[i] < values[i - 1])
                return false;
        }
        return true;
    }

    /// <summary>
    /// The permutation that sorting <paramref name="keys"/> would apply, without applying it.
    /// Ties break on the original index, which is what makes the sort stable.
    /// </summary>
    private static int[] StableSortOrder(List<double> keys)
    {
        var order = new int[keys.Count];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(order, (x, y) =>
        {
            int compared = keys[x].CompareTo(keys[y]);
            return compared != 0 ? compared : x.CompareTo(y);
        });
        return order;
    }

    /// <summary>
    /// Reorders <paramref name="data"/> in place so that its element i becomes the one that was
    /// at <paramref name="order"/>[i]. Gathers into a temporary before writing any of it back,
    /// since a permutation applied in place would overwrite entries it still has to read.
    /// </summary>
    private static void ApplyPermutation<T>(List<T> data, int[] order)
    {
        var reordered = new T[order.Length];
        for (int i = 0; i < order.Length; i++)
            reordered[i] = data[order[i]];
        for (int i = 0; i < order.Length; i++)
            data[i] = reordered[i];
    }

    // ----- Disposal -----
    //
    // Idempotent: multiple Dispose calls are safe. The real cleanup runs once, in
    // <see cref="DisposeCore"/>; subsequent calls are no-ops. Vendor lists holding native
    // handles override <see cref="DisposeCore"/> to release them.

    private bool _disposed;

    /// <summary>
    /// Idempotent disposal. Runs <see cref="DisposeCore"/> exactly once on the first call.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Subclasses that hold native handles (vendor lists) override this to release them.
    /// Default: no-op (in-memory lists have nothing to release).
    /// </summary>
    protected virtual void DisposeCore() { }
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
