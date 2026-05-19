using System.Text;
using System.Text.RegularExpressions;

namespace Pwiz.Data.Common.Proteome;

/// <summary>
/// Reads / writes FASTA-format protein databases.
/// Port of <c>pwiz::proteome::Serializer_FASTA</c> + <c>Reader_FASTA</c>, slimmed to
/// the eager-read path (cpp's lazy BinaryIndexStream-backed lookup can be added later
/// if a single-protein-lookup workload appears).
/// </summary>
/// <remarks>
/// <para>Format: each protein is one record. The first line starts with <c>&gt;</c> and
/// holds the id + description (separated by whitespace). Subsequent lines are the
/// amino-acid sequence until the next <c>&gt;</c> or EOF. Blank lines are ignored;
/// trailing carriage returns and spaces are trimmed.</para>
/// <para>cpp uses two id-parsing regexes (an IPI-specific one and a generic
/// <c>"&gt;id description"</c>). We mirror that exactly so a sharp-read FASTA produces
/// the same Protein.Id values as cpp on the same file.</para>
/// </remarks>
public static class Fasta
{
    // cpp regex order (Serializer_FASTA.cpp:254-256). Try IPI-specific first so files
    // containing IPI accessions don't get truncated at the first '|'.
    private static readonly Regex s_idIpiRegex = new(
        @">\s*(\S*?IPI\d+?\.\d+?)(?:\s|\|)(.*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex s_idGenericRegex = new(
        @">\s*(\S+)\s?(.*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>True iff the first non-blank line of <paramref name="head"/> starts
    /// with <c>&gt;</c>. Used by <see cref="ProteomeDataFile"/> for extension-less
    /// format detection.</summary>
    public static bool LooksLikeFasta(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        foreach (string raw in head.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            return line[0] == '>';
        }
        return false;
    }

    /// <summary>Reads a FASTA stream into a fresh <see cref="ProteomeData"/>. The
    /// stream must be seekable only insofar as <paramref name="stream"/> doesn't
    /// need to be — we do a single forward pass.</summary>
    public static ProteomeData Read(Stream stream, string id = "")
    {
        ArgumentNullException.ThrowIfNull(stream);
        var pd = new ProteomeData { Id = id };
        var list = new ProteinListSimple();
        pd.ProteinList = list;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        string? defline = null;
        var sequence = new StringBuilder();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            // StreamReader.ReadLine already strips trailing \n; mzMS-format FASTAs
            // can still carry a stray \r on Windows-LF-stripped lines.
            string trimmed = line.TrimEnd(' ', '\r');
            if (trimmed.Length == 0) continue;

            if (trimmed[0] == '>')
            {
                if (defline is not null)
                    Flush(list, defline, sequence, seenIds);
                defline = trimmed;
                sequence.Clear();
            }
            else if (defline is not null)
            {
                sequence.Append(trimmed);
            }
        }
        if (defline is not null)
            Flush(list, defline, sequence, seenIds);

        return pd;
    }

    /// <summary>Convenience overload — opens <paramref name="path"/> and reads it.</summary>
    public static ProteomeData ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var fs = File.OpenRead(path);
        return Read(fs, id: Path.GetFileNameWithoutExtension(path));
    }

    /// <summary>Opens <paramref name="path"/> for lazy per-protein access. The returned
    /// <see cref="ProteomeData.ProteinList"/> is a <see cref="FastaProteinList"/> that
    /// holds the file handle open until disposed; seek/parse one record per
    /// <see cref="ProteinList.GetProtein"/> call. Suitable for huge FASTA databases
    /// where you only need a few proteins.</summary>
    /// <param name="path">Path to a FASTA file.</param>
    /// <param name="useDiskIndex">When true, persist the index to a <c>.index</c> sidecar
    /// next to the FASTA file (via <see cref="Pwiz.Data.Common.Index.BinaryIndexStream"/>).
    /// Subsequent opens with the same sidecar avoid re-scanning the FASTA. When false
    /// (default), use an in-memory index built once per open.</param>
    public static ProteomeData OpenLazy(string path, bool useDiskIndex = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path)) throw new FileNotFoundException("FASTA file not found", path);

        Pwiz.Data.Common.Index.IIndex index = useDiskIndex
            ? OpenOrCreateDiskIndex(path)
            : BuildMemoryIndex(path);
        // FileStream stays open for the lifetime of the list.
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                bufferSize: 1 << 16, FileOptions.RandomAccess);
        var list = new FastaProteinList(fs, index, ownsFile: true);
        return new ProteomeData
        {
            Id = Path.GetFileNameWithoutExtension(path),
            ProteinList = list,
        };
    }

    private static Pwiz.Data.Common.Index.MemoryIndex BuildMemoryIndex(string path)
    {
        using var fs = File.OpenRead(path);
        var entries = FastaProteinList.BuildIndex(fs);
        var idx = new Pwiz.Data.Common.Index.MemoryIndex();
        idx.Create(entries);
        return idx;
    }

    private static Pwiz.Data.Common.Index.BinaryIndexStream OpenOrCreateDiskIndex(string path)
    {
        string sidecarPath = path + ".index";
        // Existing sidecar — open R/W (so MemoryIndex-or-rebuild logic can decide whether
        // it's stale and replace). For this initial port we assume a sidecar with N>0 entries
        // is fresh; cpp's SHA-1 + file-size staleness check is a follow-up.
        if (File.Exists(sidecarPath))
        {
            var rwStream = new FileStream(sidecarPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var idx = new Pwiz.Data.Common.Index.BinaryIndexStream(rwStream, leaveOpen: false);
            if (idx.Count > 0) return idx;
            // Empty sidecar — fall through to build a fresh one (close + truncate).
            idx.Dispose();
        }
        // Build the index from the FASTA, then write it to the sidecar.
        List<Pwiz.Data.Common.Index.IndexEntry> entries;
        using (var fs = File.OpenRead(path)) entries = FastaProteinList.BuildIndex(fs);
        var sidecar = new FileStream(sidecarPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        var bis = new Pwiz.Data.Common.Index.BinaryIndexStream(sidecar, leaveOpen: false);
        bis.Create(entries);
        return bis;
    }

    /// <summary>Writes <paramref name="pd"/> to <paramref name="stream"/> as FASTA.
    /// Each protein becomes <c>"&gt;id description\nsequence\n"</c> — sequence is
    /// emitted on a single line (cpp does the same; FASTA readers tolerate either
    /// wrapped or unwrapped sequences).</summary>
    public static void Write(Stream stream, ProteomeData pd)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(pd);
        if (pd.ProteinList is null) throw new InvalidOperationException("ProteomeData has no ProteinList");

        using var w = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1 << 16, leaveOpen: true)
        {
            NewLine = "\n", // cpp emits '\n', not "\r\n"
        };
        var list = pd.ProteinList;
        for (int i = 0, n = list.Count; i < n; i++)
        {
            var p = list.GetProtein(i, getSequence: true);
            w.Write('>');
            w.Write(p.Id);
            w.Write(' ');
            w.Write(p.Description);
            w.Write('\n');
            w.Write(p.Sequence);
            w.Write('\n');
        }
        w.Flush();
    }

    /// <summary>Convenience overload — creates / truncates <paramref name="path"/>.</summary>
    public static void WriteFile(string path, ProteomeData pd)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var fs = File.Create(path);
        Write(fs, pd);
    }

    private static void Flush(ProteinListSimple list, string defline,
                              StringBuilder sequence, HashSet<string> seenIds)
    {
        // Parse the defline. Try the IPI-flavored regex first (cpp order), then the
        // generic ">id description" form.
        var m = s_idIpiRegex.Match(defline);
        if (!m.Success) m = s_idGenericRegex.Match(defline);
        if (!m.Success)
            throw new FormatException($"[Fasta.Read] could not parse id from defline \"{defline}\"");

        string id = m.Groups[1].Value;
        string desc = m.Groups.Count > 2 ? m.Groups[2].Value : string.Empty;
        if (id.Length == 0)
            throw new FormatException($"[Fasta.Read] empty id in defline \"{defline}\"");

        // cpp duplicates an explicit throw on duplicate ids — we match. Silently
        // tolerating duplicates would only be reasonable if we cross-checked sequences.
        if (!seenIds.Add(id))
            throw new FormatException($"[Fasta.Read] duplicate protein id \"{id}\"");

        list.Proteins.Add(new Protein(id, list.Proteins.Count, desc, sequence.ToString()));
    }
}
