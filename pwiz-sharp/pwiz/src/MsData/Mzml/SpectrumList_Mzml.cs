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
            _stream?.Dispose();
            _stream = null;
            _ownedResource?.Dispose();
        }
    }
}
