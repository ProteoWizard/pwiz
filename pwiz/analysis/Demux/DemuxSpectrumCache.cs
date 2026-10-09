using System.Collections.Concurrent;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Demux;

/// <summary>
/// Marker interface for spectrum lists that expose pre-computed MS levels in O(1) — used by
/// <see cref="DemuxHelpers.FindNearbySpectra"/> to avoid re-reading every adjacent spectrum just
/// to inspect its <see cref="CVID.MS_ms_level"/> CV param. <see cref="DemuxSpectrumCache"/>
/// implements this; arbitrary spectrum lists do not.
/// </summary>
internal interface IMsLevelProvider
{
    /// <summary>Returns the MS level of spectrum <paramref name="index"/> without forcing a
    /// full GetSpectrum call.</summary>
    int GetMsLevel(int index);
}

/// <summary>
/// Spectrum-list wrapper that pre-computes MS levels and caches recently-fetched binary spectra,
/// bounded, oldest read evicted first. Drops the demux pipeline's GetSpectrum count from ~500 metadata calls +
/// ~30 binary calls per source spectrum to ~0 metadata calls + ~5 binary calls (the rest hit
/// the cache from the previous source spectrum's demux block).
/// </summary>
internal sealed class DemuxSpectrumCache : SpectrumListWrapper, IMsLevelProvider
{
    /// <summary>A spectrum's metadata as the demultiplexer uses it.</summary>
    internal readonly record struct SpectrumSummary(int MsLevel, int PrecursorCount,
        double PrecursorLowMz, double PrecursorHighMz);

    private readonly SpectrumSummary[] _summaries;
    private SpectrumIdentity[]? _identities; // only until the index mapper has read them
    private int _capacity;

    // Serializes inner reads and cache changes: vendor readers are not thread-safe, and
    // SpectrumListDemux builds blocks on several threads when solving ahead.
    private readonly object _readLock = new();

    // Hits take no lock; a lock on every hit was the contention under solve-ahead. Evicts the
    // oldest read first, which an in-order reader has moved past.
    private readonly ConcurrentDictionary<int, Spectrum> _byIndex = new();
    private readonly Queue<KeyValuePair<int, Spectrum>> _readOrder = new();

    public DemuxSpectrumCache(ISpectrumList inner, int capacity = 256) : base(inner)
    {
        _capacity = capacity;

        // Pre-compute MS levels in one sweep. Each metadata-only GetSpectrum is cheap individually
        // (~10s of µs) but the cumulative cost dominated FindNearbySpectra in the demux pipeline,
        // which makes ~500 such calls per source spectrum.
        _summaries = new SpectrumSummary[inner.Count];
        _identities = new SpectrumIdentity[inner.Count];
        for (int i = 0; i < inner.Count; i++)
        {
            var s = inner.GetSpectrum(i, getBinaryData: false);
            bool hasPrecursor = s.Precursors.Count > 0;
            _summaries[i] = new SpectrumSummary(
                s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0),
                s.Precursors.Count,
                hasPrecursor ? DemuxHelpers.PrecursorMzLow(s.Precursors[0]) : 0,
                hasPrecursor ? DemuxHelpers.PrecursorMzHigh(s.Precursors[0]) : 0);
            _identities[i] = new SpectrumIdentity
            {
                Index = s.Index, Id = s.Id, SpotId = s.SpotId, SourceFilePosition = s.SourceFilePosition,
            };
        }
    }

    /// <inheritdoc/>
    public int GetMsLevel(int index) => _summaries[index].MsLevel;

    /// <summary>Spectrum <paramref name="index"/>'s metadata from the constructor's sweep.</summary>
    public SpectrumSummary GetSummary(int index) => _summaries[index];

    /// <summary>Spectrum <paramref name="index"/>'s identity from the constructor's sweep, until
    /// <see cref="ReleaseIdentities"/>.</summary>
    public SpectrumIdentity GetIdentity(int index) =>
        (_identities ?? throw new InvalidOperationException("identities released"))[index];

    /// <summary>Drops the identities once the index mapper has built its own.</summary>
    public void ReleaseIdentities() => _identities = null;

    /// <summary>Raises the capacity to at least <paramref name="capacity"/> spectra.</summary>
    public void EnsureCapacity(int capacity)
    {
        lock (_readLock)
            _capacity = System.Math.Max(_capacity, capacity);
    }

    /// <summary>Reads spectra <paramref name="first"/> to <paramref name="last"/> into the cache
    /// in file order, so blocks solved afterwards do not queue for the reader.</summary>
    public void Prefetch(int first, int last)
    {
        first = System.Math.Max(first, 0);
        last = System.Math.Min(last, _summaries.Length - 1);
        for (int i = first; i <= last; i++)
            GetSpectrum(i, getBinaryData: true);
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        // We only cache decoded (binary) spectra. Metadata-only calls bypass the cache because
        // most of them are MS-level probes and hit GetMsLevel via FindNearbySpectra anyway.
        if (!getBinaryData)
        {
            lock (_readLock)
                return Inner.GetSpectrum(index, getBinaryData: false);
        }

        if (_byIndex.TryGetValue(index, out var cached))
            return cached;

        lock (_readLock)
        {
            // Another thread may have read it while this one waited.
            if (_byIndex.TryGetValue(index, out cached))
                return cached;

            var spec = Inner.GetSpectrum(index, getBinaryData: true);
            _byIndex[index] = spec;
            _readOrder.Enqueue(new KeyValuePair<int, Spectrum>(index, spec));
            while (_readOrder.Count > _capacity)
                _byIndex.TryRemove(_readOrder.Dequeue());
            return spec;
        }
    }
}
