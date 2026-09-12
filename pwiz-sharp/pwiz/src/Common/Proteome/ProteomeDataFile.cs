namespace Pwiz.Data.Common.Proteome;

/// <summary>Output format for <see cref="ProteomeDataFile.Write"/>.</summary>
public enum ProteomeFileFormat
{
    /// <summary>FASTA — the format every reader currently supported reads.</summary>
    Fasta,
}

/// <summary>
/// File-level helpers for <see cref="ProteomeData"/>: format-detecting <see cref="Read"/>
/// (currently dispatches only to FASTA) and format-selecting <see cref="Write"/>.
/// Port of <c>pwiz::proteome::ProteomeDataFile</c> + the default <c>Reader_FASTA</c> registration.
/// </summary>
/// <remarks>
/// cpp's full ProteomeDataFile carries a Reader-registration model so additional formats
/// (mzIdentML protein lists, for example) plug in via <c>DefaultReaderList</c>. For now
/// only FASTA is wired up; the format enum + dispatch are in place so future formats can
/// land here without disturbing callers.
/// </remarks>
public static class ProteomeDataFile
{
    /// <summary>Reads a proteome data file. Format is detected from a head-of-file
    /// sniff (currently: a leading <c>&gt;</c> means FASTA) with an extension-based
    /// fallback.</summary>
    public static ProteomeData Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Proteome file not found", path);

        // Sniff the first ~256 bytes; that's enough to spot a FASTA defline.
        string head = SniffHead(path, 256);
        if (Fasta.LooksLikeFasta(head)) return Fasta.ReadFile(path);

        // Extension fallback before giving up.
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".fasta" or ".fa" or ".tfa" or ".faa")
            return Fasta.ReadFile(path);

        throw new InvalidDataException($"Unable to identify proteome format for {path}");
    }

    /// <summary>Writes <paramref name="pd"/> to <paramref name="path"/> in the
    /// chosen <paramref name="format"/>. Currently only FASTA is implemented.</summary>
    public static void Write(ProteomeData pd, string path, ProteomeFileFormat format = ProteomeFileFormat.Fasta)
    {
        ArgumentNullException.ThrowIfNull(pd);
        ArgumentException.ThrowIfNullOrEmpty(path);
        switch (format)
        {
            case ProteomeFileFormat.Fasta:
                Fasta.WriteFile(path, pd);
                break;
            default:
                throw new NotSupportedException($"Proteome format {format} is not supported");
        }
    }

    private static string SniffHead(string path, int byteCount)
    {
        using var fs = File.OpenRead(path);
        byte[] buf = new byte[byteCount];
        int got = fs.Read(buf, 0, buf.Length);
        return System.Text.Encoding.UTF8.GetString(buf, 0, got);
    }
}
