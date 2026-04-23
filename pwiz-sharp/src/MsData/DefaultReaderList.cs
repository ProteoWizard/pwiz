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

    /// <summary>Returns the first reader that identifies the file, or null.</summary>
    public IReader? IdentifyReader(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        foreach (var r in _readers)
            if (r.Identify(filename, head) != CVID.CVID_Unknown) return r;
        return null;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        string? head = Directory.Exists(filename) ? null : ReadHead(filename);
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
        using var stream = File.OpenRead(filename);
        byte[] buffer = new byte[maxBytes];
        int read = stream.ReadAtLeast(buffer, maxBytes, throwOnEndOfStream: false);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// A reader list pre-populated with the built-in format readers in priority order:
    /// mzML → MGF.
    /// </summary>
    public static ReaderList Default
    {
        get
        {
            var list = new ReaderList();
            list.Add(new MzmlReaderAdapter());
            list.Add(new MgfReaderAdapter());
            return list;
        }
    }
}
