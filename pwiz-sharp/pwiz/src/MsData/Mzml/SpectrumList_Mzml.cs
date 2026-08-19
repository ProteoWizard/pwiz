using System.IO;
using System.Xml;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Mzml;

/// <summary>
/// Lazy <see cref="ISpectrumList"/> backed by an indexed mzML stream. cpp's
/// <c>SpectrumList_mzML</c> uses the same approach: parse <c>&lt;indexList&gt;</c>
/// once at construction to learn each spectrum's byte offset, then seek + parse
/// one <c>&lt;spectrum&gt;</c> element on demand. Only the spectrum being read is
/// in memory at any one time, which is the difference between handling a 20 GB
/// mzML and OOMing on a 200 MB one.
/// </summary>
/// <remarks>
/// Constructed by <c>MzmlReaderAdapter</c> (for plain mzML files) or
/// <c>MzMlbReaderAdapter</c> (for the mzML stream embedded in an mzMLb HDF5
/// container — same XML, same indexList footer, just sourced from a different
/// underlying stream). The shared <see cref="MzmlReader"/> instance must outlive
/// this list — it holds the document-level ref maps (param groups, instrument
/// configurations, data processings, source files, samples) that per-spectrum
/// parses resolve against, plus the optional
/// <see cref="MzmlReader.ExternalBinarySource"/> for mzMLb's HDF5-backed
/// binary arrays.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "Matches cpp pwiz::msdata::SpectrumList_mzML class name; pwiz-sharp convention preserves cpp class names verbatim.")]
public sealed class SpectrumList_Mzml : SpectrumListBase
{
    private readonly System.Func<Stream> _openStream;
    private readonly System.IDisposable? _ownedResource;
    private readonly MzmlReader _context;
    private readonly long[] _offsets;
    private readonly string[] _ids;
    private SpectrumIdentity[]? _identities;
    private System.Collections.Generic.Dictionary<string, int>? _idMap;
    private readonly DataProcessing? _dp;
    private readonly string _source;

    // Stream is created lazily on first read so an unused SpectrumList_Mzml doesn't
    // hold an OS / HDF5 file handle. A lock guards stream seeks across threads.
    private Stream? _stream;
    private readonly object _streamLock = new();
    private bool _disposed;

    // Parallel-decode batch. Sequential readers (every search engine, and Skyline's
    // chromatogram extraction) walk indices in order, so when one asks for spectrum i we
    // parse i..i+BatchSize-1 in one pass - cheap, the XML scan is not the expensive part -
    // deferring every base64/zlib decode, then run those decodes on the thread pool and
    // hand out the batch from here.
    //
    // Parsing stays single-threaded and keeps using the one stream, so this needs no
    // reentrancy work in MzmlReader and no second file handle; only the pure decode goes
    // wide. Results are identical and order is unchanged - decode is a pure function of
    // payload plus config, and each spectrum is keyed by its own index.
    //
    /// <summary>
    /// Maximum threads used to decode binary arrays. 1 (the default) keeps the original
    /// fully sequential behaviour, so this changes nothing until a host opts in.
    ///
    /// The host decides, not this class: a caller that already parallelises across FILES -
    /// Osprey scores several at once - would oversubscribe the machine if the library went
    /// wide on its own. Seeded from PWIZ_SHARP_MZML_DECODE_THREADS so it can be set for a
    /// process that cannot easily be recompiled (benchmarks, msconvert runs), but the
    /// property is the real interface.
    ///
    /// A static rather than a per-read option deliberately: it is a machine-level resource
    /// decision, set once at startup, and threading it through ReaderConfig would touch
    /// every reader for a setting only this one honours. Easy to move if that is preferred.
    /// </summary>
    public static int DecodeThreads { get; set; } = ReadThreadSetting();
    private const int BatchSize = 64;
    private readonly System.Collections.Generic.Dictionary<int, Spectrum> _batch = new();

    // The index a forward-walking caller would ask for next; -1 until the first read.
    // Read-ahead only engages when the request matches, so a random-access caller pays
    // nothing beyond the original per-spectrum path.
    private int _nextSequential = -1;

    private static int ReadThreadSetting()
    {
        var raw = System.Environment.GetEnvironmentVariable("PWIZ_SHARP_MZML_DECODE_THREADS");
        if (string.IsNullOrEmpty(raw))
            return 1;

        // 0 and -1 are the natural spellings of "all cores" and .NET's "unlimited", so
        // silently treating them as "off" would leave someone reporting that the speedup
        // does not reproduce. Map them to the core count instead, and clamp the top end:
        // this work is CPU-bound inflate-plus-convert, so more threads than cores only
        // adds contention - and the host may already be parallelising across files.
        if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                          System.Globalization.CultureInfo.InvariantCulture, out int n))
            return 1;
        if (n <= 0)
            return System.Environment.ProcessorCount;
        return System.Math.Min(n, System.Environment.ProcessorCount);
    }

    /// <summary>Constructs a lazy spectrum list backed by an arbitrary seekable stream.
    /// <paramref name="openStream"/> is invoked once on first access — for plain mzML
    /// it returns a new FileStream; for mzMLb it returns the mzML dataset stream from
    /// the HDF5 connection. <paramref name="ownedResource"/>, if non-null, is disposed
    /// alongside the spectrum list (used by mzMLb to keep the HDF5 connection alive
    /// for the list's lifetime).</summary>
    internal SpectrumList_Mzml(System.Func<Stream> openStream, System.IDisposable? ownedResource,
                                MzmlReader context, string[] ids, long[] offsets,
                                DataProcessing? dp, string source)
    {
        _openStream = openStream;
        _ownedResource = ownedResource;
        _context = context;
        _offsets = offsets;
        _ids = ids;
        _dp = dp;
        _source = source;
    }

    /// <summary>File-backed convenience overload used by <c>MzmlReaderAdapter</c>.</summary>
    internal SpectrumList_Mzml(string filename, MzmlReader context,
                                string[] ids, long[] offsets, DataProcessing? dp)
        : this(
              openStream: () => new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read,
                                                bufferSize: 1 << 16, FileOptions.RandomAccess),
              ownedResource: null,
              context: context, ids: ids, offsets: offsets, dp: dp, source: filename)
    { }

    /// <inheritdoc/>
    public override int Count => _offsets.Length;

    /// <inheritdoc/>
    public override DataProcessing? DataProcessing => _dp;

    /// <inheritdoc/>
    public override SpectrumIdentity SpectrumIdentity(int index)
    {
        if ((uint)index >= (uint)_offsets.Length)
            throw new System.ArgumentOutOfRangeException(nameof(index));
        // Identities array is materialized on first access. The construction cost is
        // ~250 ns/entry; up to a few-hundred-thousand-spectrum file it stays comfortably
        // under a tenth of a second amortized over a full enumeration.
        var ids = _identities;
        if (ids is null)
        {
            ids = new SpectrumIdentity[_ids.Length];
            for (int i = 0; i < _ids.Length; i++)
                ids[i] = new SpectrumIdentity { Id = _ids[i], Index = i };
            _identities = ids;
        }
        return ids[index];
    }

    /// <inheritdoc/>
    public override int Find(string id)
    {
        System.ArgumentNullException.ThrowIfNull(id);
        var map = _idMap;
        if (map is null)
        {
            map = new System.Collections.Generic.Dictionary<string, int>(_ids.Length, System.StringComparer.Ordinal);
            for (int i = 0; i < _ids.Length; i++) map[_ids[i]] = i;
            _idMap = map;
        }
        return map.TryGetValue(id, out int idx) ? idx : _offsets.Length;
    }

    /// <inheritdoc/>
    public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
    {
        if ((uint)index >= (uint)_offsets.Length)
            throw new System.ArgumentOutOfRangeException(nameof(index));

        lock (_streamLock)
        {
            System.ObjectDisposedException.ThrowIf(_disposed, this);

            // Only worth batching when the caller actually wants peaks: a metadata-only
            // read decodes nothing, so there is nothing to parallelise.
            int threads = DecodeThreads;
            if (threads > 1 && getBinaryData)
            {
                if (_batch.Remove(index, out var cached))
                {
                    _nextSequential = index + 1;
                    return cached;
                }

                // Only read ahead when the caller is actually walking forward. Filters that
                // read backward (SpectrumList_PrecursorRefine), by permutation
                // (SpectrumListSorter) or scattered across the run (SpectrumListScanSummer)
                // would otherwise parse and decode a fresh 64-spectrum batch for every
                // single spectrum served - turning the feature into a large slowdown rather
                // than, as an earlier comment here wrongly claimed, merely "not benefiting".
                if (index == _nextSequential || _nextSequential < 0)
                {
                    FillBatch(index, threads);
                    if (_batch.Remove(index, out cached))
                    {
                        _nextSequential = index + 1;
                        return cached;
                    }
                }
            }

            _nextSequential = index + 1;
            return ReadOne(index, getBinaryData);
        }
    }

    /// <summary>Parses [start, start+BatchSize) with decoding deferred, then decodes the
    /// whole batch in parallel. Caller holds <see cref="_streamLock"/>.</summary>
    private void FillBatch(int start, int threads)
    {
        _batch.Clear();

        int end = System.Math.Min(start + BatchSize, _offsets.Length);
        var pending = new System.Collections.Generic.List<MzmlReader.PendingDecode>();
        _context.PendingDecodes = pending;
        try
        {
            for (int i = start; i < end; i++)
                _batch[i] = ReadOne(i, getBinaryData: true);

            if (pending.Count > 0)
                DecodeInParallel(pending, threads);
        }
        catch
        {
            // EVERY entry in _batch is parsed but NOT yet decoded until DecodeInParallel
            // returns, so a failure anywhere above leaves spectra whose binary arrays are
            // empty. Serving those would be silent data loss rather than an error - a host
            // with ContinueOnError would write a whole batch of spectra with no peaks and
            // report only the one failure. Dropping the batch makes the next read take the
            // single-spectrum path, which fails honestly on the spectrum that is actually
            // broken.
            _batch.Clear();
            throw;
        }
        finally
        {
            _context.PendingDecodes = null;
        }
    }

    /// <summary>Runs the deferred decodes, preserving the exception a sequential read would
    /// have thrown.</summary>
    private static void DecodeInParallel(
        System.Collections.Generic.List<MzmlReader.PendingDecode> pending, int threads)
    {
        try
        {
            System.Threading.Tasks.Parallel.ForEach(
                pending,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = threads },
                item => item.Run());
        }
        catch (System.AggregateException ex) when (ex.InnerException is not null)
        {
            // Parallel.ForEach wraps everything in AggregateException, so a caller
            // catching FormatException from a corrupt base64 payload - which is what the
            // sequential path throws - would stop matching the moment decoding went
            // parallel. Rethrow the original with its stack intact so turning threads on
            // cannot change which handler fires.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(ex.InnerException).Throw();
        }
    }

    /// <summary>The original single-spectrum read. Caller holds <see cref="_streamLock"/>.</summary>
    private Spectrum ReadOne(int index, bool getBinaryData)
    {
        {
            _stream ??= _openStream();
            _stream.Position = _offsets[index];

            // Fragment conformance lets XmlReader start mid-document on the next
            // <spectrum> element; CloseInput=false keeps our underlying stream alive
            // across calls. The reader is short-lived (one spectrum) — the cost
            // of recreating it is negligible vs the seek + parse work.
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                CloseInput = false,
                ConformanceLevel = ConformanceLevel.Fragment,
            };
            using var xr = XmlReader.Create(_stream, settings);
            if (!xr.ReadToFollowing("spectrum"))
                throw new InvalidDataException($"No <spectrum> element at offset {_offsets[index]} in {_source}");

            var spec = _context.ReadOneSpectrum(xr, getBinaryData);
            // Belt-and-suspenders: index/id may diverge from the spectrum's own
            // attributes if the indexList was edited; normalize to our identity.
            spec.Index = index;
            spec.Id = _ids[index];
            return spec;
        }
    }

    /// <inheritdoc/>
    protected override void DisposeCore()
    {
        lock (_streamLock)
        {
            if (_disposed) return;
            _disposed = true;
            // A batch of profile MS1 spectra is gigabytes; without this they stay reachable
            // from a disposed list for as long as anything holds a reference to it.
            _batch.Clear();
            _stream?.Dispose();
            _stream = null;
            _ownedResource?.Dispose();
        }
    }
}
