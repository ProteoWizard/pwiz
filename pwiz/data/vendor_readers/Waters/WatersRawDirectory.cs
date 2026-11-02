namespace Pwiz.Vendor.Waters;

/// <summary>
/// Case-insensitive filename lookup inside a Waters <c>.raw</c> directory.
/// </summary>
/// <remarks>
/// MassLynx writes a single .raw directory with mixed casing: the per-function binary frames
/// are <c>_FUNC001.DAT</c> while the ion-mobility sibling sitting right beside them is
/// <c>_func001.cdt</c>. Windows resolves both spellings against either casing, Linux does not.
/// A literal <c>File.Exists</c> or a case-sensitive glob therefore reports "no .cdt" on Linux
/// and the reader concludes the file has no ion mobility -- which switches off IMS and SONAR
/// silently, with no error, and every mobility fixture reads back as ordinary MS data.
/// Every filename lookup inside a .raw directory goes through here so that cannot recur.
/// </remarks>
internal static class WatersRawDirectory
{
    /// <summary>
    /// Full paths of the per-function binary frame files (<c>_FUNCnnn.DAT</c>), matched
    /// regardless of casing. Sorted so callers get a stable order across filesystems --
    /// directory enumeration order is not guaranteed and differs between Windows and Linux.
    /// </summary>
    public static List<string> FunctionDataFiles(string rawPath)
    {
        var result = new List<string>();
        if (!Directory.Exists(rawPath)) return result;
        foreach (string path in Directory.EnumerateFiles(rawPath))
        {
            string name = Path.GetFileName(path);
            if (name.StartsWith("_FUNC", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".DAT", StringComparison.OrdinalIgnoreCase))
                result.Add(path);
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Full path of <paramref name="fileName"/> directly inside <paramref name="rawPath"/>,
    /// or null when absent. Matches regardless of casing.
    /// </summary>
    public static string? Find(string rawPath, string fileName)
    {
        if (!Directory.Exists(rawPath)) return null;
        foreach (string path in Directory.EnumerateFiles(rawPath))
        {
            if (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                return path;
        }
        return null;
    }

    /// <summary>True iff a file of that name exists in the directory, ignoring casing.</summary>
    public static bool Exists(string rawPath, string fileName) => Find(rawPath, fileName) is not null;
}
