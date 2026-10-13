namespace Pwiz.Data.TraData;

/// <summary>
/// File-level helpers for <see cref="TraData"/>: format-detecting <see cref="Read"/>
/// and format-selecting <see cref="Write"/>. Port of <c>pwiz::tradata::TraDataFile</c>.
/// </summary>
/// <remarks>
/// Currently only TraML is wired up. The enum + dispatch are in place so future formats
/// (e.g. CSV transition lists, Skyline native) can plug in without changing callers.
/// </remarks>
public static class TraDataFile
{
    /// <summary>Supported output formats.</summary>
    public enum Format
    {
        /// <summary>TraML XML. The only format currently supported.</summary>
        TraML,
    }

    /// <summary>Reads a TraML file. Format is detected by extension + content sniff
    /// (the first non-blank line should contain <c>&lt;TraML</c>).</summary>
    public static TraData Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path)) throw new FileNotFoundException("TraML file not found", path);

        // Sniff to confirm it's TraML; extension fallback when the head lookup misses.
        string head = SniffHead(path, 512);
        bool looksLikeTraml = head.Contains("<TraML", StringComparison.Ordinal);
        if (!looksLikeTraml)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".traml" or ".tramL" or ".xml"))
                throw new InvalidDataException($"Unable to identify TraML format for {path}");
        }

        var td = TraDataIO.ReadFile(path);
        if (string.IsNullOrEmpty(td.Id))
            td.Id = Path.GetFileNameWithoutExtension(path);
        return td;
    }

    /// <summary>Writes <paramref name="td"/> to <paramref name="path"/> in the chosen
    /// <paramref name="format"/>. Currently only TraML is implemented.</summary>
    public static void Write(TraData td, string path, Format format = Format.TraML)
    {
        ArgumentNullException.ThrowIfNull(td);
        ArgumentException.ThrowIfNullOrEmpty(path);
        switch (format)
        {
            case Format.TraML:
                TraDataIO.WriteFile(path, td);
                break;
            default:
                throw new NotSupportedException($"TraData format {format} is not supported");
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
