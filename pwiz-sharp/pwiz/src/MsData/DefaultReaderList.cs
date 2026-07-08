using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.MsData.Readers;

/// <summary>
/// Composite reader that dispatches to the first registered <see cref="IReader"/> that identifies
/// the input. Port of pwiz::msdata::ReaderList + DefaultReaderList.
/// </summary>
/// <remarks>
/// Readers are probed in registration order, so if two readers might match (e.g. both extension
/// and content), put the stricter one first. The built-in <see cref="Default"/> registers mzML
/// ahead of MGF since mzML's content sniff is more specific.
/// </remarks>
public sealed class ReaderList : IReader
{
    private readonly List<IReader> _readers = new();

    /// <summary>Registers an additional reader at the end of the priority list.</summary>
    public void Add(IReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _readers.Add(reader);
    }

    /// <summary>All registered readers in priority order.</summary>
    public IReadOnlyList<IReader> Readers => _readers;

    /// <inheritdoc/>
    public string TypeName => "ReaderList";

    /// <inheritdoc/>
    public CVID CvType => CVID.CVID_Unknown;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions
    {
        get
        {
            var all = new List<string>();
            foreach (var r in _readers) all.AddRange(r.FileExtensions);
            return all;
        }
    }

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        foreach (var r in _readers)
        {
            var cvid = r.Identify(filename, head);
            if (cvid != CVID.CVID_Unknown) return cvid;
        }
        return CVID.CVID_Unknown;
    }

    /// <summary>
    /// Returns file extensions grouped by CV type (vendor). One entry per registered reader,
    /// keyed by the reader's <see cref="IReader.CvType"/>. Ports pwiz.CLI's getFileExtensionsByType
    /// so Skyline's file-open dialog can show separate vendor entries.
    /// </summary>
    public IReadOnlyDictionary<string, IList<string>> FileExtensionsByType()
    {
        var result = new Dictionary<string, IList<string>>();
        foreach (var r in _readers)
        {
            var key = r.TypeName;
            if (!result.ContainsKey(key))
                result[key] = new List<string>(r.FileExtensions);
        }
        return result;
    }

    /// <summary>
    /// Fast id-only read: opens the file, iterates its spectrum list, and yields native
    /// spectrum ids without loading binary payload. Ports pwiz.CLI's ReaderList.readIds.
    /// </summary>
    public string[] ReadIds(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);

        // Multi-sample containers (Sciex WIFF, Shimadzu multi-run .lcd, etc.) enumerate
        // sample names - Skyline uses these to discover the sample list without doing a
        // full per-sample open. Non-multi-sample readers fall through to spectrum-id
        // enumeration on sample 0.
        var reader = IdentifyReader(filename, null);
        if (reader is IMultiSampleReader multi)
            return multi.EnumerateSampleNames(filename);

        using var msd = new MSData();
        Read(filename, msd);
        var sl = msd.Run.SpectrumList;
        if (sl is null) return System.Array.Empty<string>();
        var ids = new string[sl.Count];
        for (int i = 0; i < sl.Count; i++)
            ids[i] = sl.SpectrumIdentity(i).Id;
        return ids;
    }

    /// <summary>Returns the first reader that identifies the file, or null.</summary>
    public IReader? IdentifyReader(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        foreach (var r in _readers)
            if (r.Identify(filename, head) != CVID.CVID_Unknown) return r;
        return null;
    }

    /// <summary>
    /// Legacy pwiz.CLI 4-arg Read overload: pass sampleIndex separately from config.
    /// The sampleIndex is folded into <c>config.RunIndex</c> before dispatching.
    /// </summary>
    public void Read(string filename, MSData result, int sampleIndex, ReaderConfig? config = null)
    {
        // Mutation is intentional: Skyline creates a fresh ReaderConfig per
        // MsDataFileImpl ctor and calls Read once, so overwriting RunIndex has no
        // cross-call leakage. Cloning risked dropping fields not enumerated here.
        var effective = config ?? new ReaderConfig();
        effective.RunIndex = sampleIndex;
        Read(filename, result, effective);
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        // Remote / URL inputs (UNIFI sample-result endpoints, waters_connect) skip the
        // file-system probe — Directory.Exists / File.OpenRead would normalize the URL as a
        // local path and throw. cpp ReaderList does the same: identify by string shape first.
        bool isUrl = filename.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                  || filename.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        string? head = isUrl || Directory.Exists(filename) ? null : ReadHead(filename);
        var reader = IdentifyReader(filename, head);
        if (reader is null)
        {
            if ((config ?? ReaderConfig.Default).UnknownFormatIsError)
                throw new NotSupportedException($"No registered reader recognized the file: {filename}");
            return;
        }
        reader.Read(filename, result, config);
    }

    /// <summary>Reads up to 8 KB of the file head so <see cref="Identify(string, string?)"/> can sniff content.</summary>
    private static string ReadHead(string filename, int maxBytes = 8192)
    {
        try
        {
            return ReadHeadOnce(filename, maxBytes);
        }
        catch (IOException)
        {
            // A vendor reader can leave the previous sample's native file handle alive past Dispose:
            // the Sciex SDK on .NET 8 releases a SIM/SRM sample's .wiff handle only when the reader is
            // finalized AND its SDK-internal async native close completes, briefly locking the file and
            // breaking the next per-sample import's format sniff. Force finalization of the now-
            // unreachable prior reader, then poll for the async release. cpp releases synchronously.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var start = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                System.Threading.Thread.Sleep(50);
                try
                {
                    return ReadHeadOnce(filename, maxBytes);
                }
                catch (IOException) when (start.ElapsedMilliseconds < 5000)
                {
                }
            }
        }
    }

    private static string ReadHeadOnce(string filename, int maxBytes)
    {
        using var stream = File.OpenRead(filename);
        byte[] buffer = new byte[maxBytes];
        int read = stream.ReadAtLeast(buffer, maxBytes, throwOnEndOfStream: false);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// A reader list pre-populated with the built-in format readers in priority order:
    /// mzML → mzMLb → MGF.
    /// </summary>
    /// <remarks>
    /// mzMLb comes after mzML in this list, but Identify is content-driven (HDF5
    /// magic bytes for mzMLb, <c>&lt;mzML&gt;</c> XML tag for mzML), so the order only
    /// matters for ambiguous inputs — and the two formats can't be confused with
    /// each other.
    /// </remarks>
    /// <summary>
    /// Additional readers registered at startup (typically vendor-SDK-backed readers from
    /// <c>Pwiz.Vendor.*</c>). The vendor projects can't be referenced from
    /// <c>Pwiz.Data.MsData</c> directly without dragging the native SDKs into every consumer,
    /// so each vendor exposes a <c>ReaderRegistration.AddTo</c> helper that the host
    /// application calls at startup. Appended after the built-in readers so the built-in
    /// format detection still has priority where formats can be ambiguous.
    /// </summary>
    public static List<IReader> AdditionalReaders { get; } = new();

    public static ReaderList Default
    {
        get
        {
            var list = new ReaderList();
            list.Add(new MzmlReaderAdapter());
            list.Add(new MzMlbReaderAdapter());
            list.Add(new Mz5ReaderAdapter());
            list.Add(new MzxmlReaderAdapter());
            list.Add(new MSnReaderAdapter());
            list.Add(new BtdxReaderAdapter());
            list.Add(new MgfReaderAdapter());
            foreach (var r in AdditionalReaders)
                list.Add(r);
            return list;
        }
    }
}
