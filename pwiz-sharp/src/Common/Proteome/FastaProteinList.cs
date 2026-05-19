using System.Text;
using System.Text.RegularExpressions;
using Pwiz.Data.Common.Index;

namespace Pwiz.Data.Common.Proteome;

/// <summary>
/// Lazy <see cref="ProteinList"/> backed by a FASTA file + an <see cref="IIndex"/>
/// of per-protein byte offsets. Port of cpp's <c>ProteinList_FASTA</c>.
/// </summary>
/// <remarks>
/// <para>Each <see cref="GetProtein"/> call seeks the underlying <see cref="FileStream"/>
/// to the recorded offset of the protein's <c>&gt;</c> defline and parses one record. Only the
/// record being read lives in memory at any one time — the difference between handling a
/// human-proteome 200 MB FASTA and OOMing on a Uniprot-everything 50 GB one.</para>
/// <para>Thread-safe via a per-list lock around the shared <see cref="FileStream"/>.
/// For multi-threaded readers, share the <see cref="FastaProteinList"/> instance — don't
/// open multiple <see cref="FileStream"/>s on the same path.</para>
/// </remarks>
public sealed class FastaProteinList : ProteinList, IDisposable
{
    private static readonly Regex s_idIpiRegex = new(
        @">\s*(\S*?IPI\d+?\.\d+?)(?:\s|\|)(.*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex s_idGenericRegex = new(
        @">\s*(\S+)\s?(.*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly FileStream _file;
    private readonly IIndex _index;
    private readonly bool _ownsFile;
    private readonly object _ioLock = new();
    private bool _disposed;

    internal FastaProteinList(FileStream file, IIndex index, bool ownsFile = true)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(index);
        _file = file;
        _index = index;
        _ownsFile = ownsFile;
    }

    /// <inheritdoc/>
    public override int Count => _index.Count;

    /// <inheritdoc/>
    public override Protein GetProtein(int index, bool getSequence = true)
    {
        if ((uint)index >= (uint)_index.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var entry = _index.Find(index) ?? throw new InvalidDataException($"Index missing ordinal {index}");

        lock (_ioLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _file.Seek(entry.Offset, SeekOrigin.Begin);
            return ParseOne(_file, index, getSequence);
        }
    }

    /// <inheritdoc/>
    public override int Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entry = _index.Find(id);
        return entry is null ? Count : (int)entry.Index;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsFile) _file.Dispose();
        if (_index is IDisposable disp) disp.Dispose();
    }

    /// <summary>Walks <paramref name="stream"/> once and emits one <see cref="IndexEntry"/>
    /// per FASTA record (<c>id</c> = the protein's id as parsed by the same regexes
    /// <see cref="Fasta"/> uses; <c>index</c> = ordinal position; <c>offset</c> = byte
    /// position of the leading <c>&gt;</c> on the defline). Stream must be seekable;
    /// position is restored to 0 on return.</summary>
    public static List<IndexEntry> BuildIndex(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
            throw new ArgumentException("BuildIndex requires a seekable stream", nameof(stream));

        stream.Position = 0;
        var entries = new List<IndexEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // Read line-by-line using a byte buffer so we can track exact byte offsets. Avoid
        // StreamReader's internal buffer which makes Position untrackable.
        long offset = 0;
        var line = new List<byte>(256);
        var buf = new byte[4096];
        int bufLen = 0;
        int bufPos = 0;
        ulong index = 0;
        while (true)
        {
            // Fill buf if needed.
            if (bufPos >= bufLen)
            {
                bufLen = stream.Read(buf, 0, buf.Length);
                bufPos = 0;
                if (bufLen == 0) break;
            }
            long lineStart = offset;
            line.Clear();
            // Read one line.
            bool eol = false;
            while (!eol)
            {
                if (bufPos >= bufLen)
                {
                    bufLen = stream.Read(buf, 0, buf.Length);
                    bufPos = 0;
                    if (bufLen == 0) { eol = true; break; }
                }
                byte b = buf[bufPos++];
                offset++;
                if (b == '\n') { eol = true; break; }
                line.Add(b);
            }
            if (line.Count > 0 && line[0] == (byte)'>')
            {
                // Strip trailing \r and spaces.
                int end = line.Count;
                while (end > 0 && (line[end - 1] == (byte)' ' || line[end - 1] == (byte)'\r')) end--;
                string defline = Encoding.UTF8.GetString(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(line)[..end]);
                var m = s_idIpiRegex.Match(defline);
                if (!m.Success) m = s_idGenericRegex.Match(defline);
                if (!m.Success)
                    throw new FormatException($"[FastaProteinList.BuildIndex] could not parse id from defline \"{defline}\"");
                string id = m.Groups[1].Value;
                if (id.Length == 0)
                    throw new FormatException($"[FastaProteinList.BuildIndex] empty id in defline \"{defline}\"");
                if (!seen.Add(id))
                    throw new FormatException($"[FastaProteinList.BuildIndex] duplicate protein id \"{id}\"");
                entries.Add(new IndexEntry(id, index, lineStart));
                index++;
            }
        }
        stream.Position = 0;
        return entries;
    }

    // Parses one FASTA record starting at the current stream position. Stream is expected
    // to be positioned on the leading '>' of the defline. Reads until the next '>' or EOF.
    private static Protein ParseOne(FileStream stream, int recordIndex, bool getSequence)
    {
        var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string? defline = reader.ReadLine();
        if (defline is null || defline.Length == 0 || defline[0] != '>')
            throw new InvalidDataException($"Expected '>' at index {recordIndex}'s offset");
        string trimmed = defline.TrimEnd(' ', '\r');
        var m = s_idIpiRegex.Match(trimmed);
        if (!m.Success) m = s_idGenericRegex.Match(trimmed);
        if (!m.Success)
            throw new FormatException($"[FastaProteinList.ParseOne] could not parse id from defline \"{trimmed}\"");
        string id = m.Groups[1].Value;
        string description = m.Groups.Count > 2 ? m.Groups[2].Value : string.Empty;

        if (!getSequence)
            return new Protein(id, recordIndex, description, string.Empty);

        var seq = new StringBuilder();
        while (reader.Peek() != -1)
        {
            // Peek at the next char; if it's '>' the next record begins.
            if (reader.Peek() == '>') break;
            string? line = reader.ReadLine();
            if (line is null) break;
            if (line.Length == 0) continue;
            seq.Append(line.TrimEnd(' ', '\r'));
        }
        return new Protein(id, recordIndex, description, seq.ToString());
    }
}
