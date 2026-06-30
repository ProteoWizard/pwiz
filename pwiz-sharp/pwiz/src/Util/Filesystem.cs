using System.Runtime.InteropServices;
using System.Text;

namespace Pwiz.Util.Misc;

/// <summary>Cross-platform filesystem helpers used by vendor readers + msconvert.</summary>
public static class Filesystem
{
    // CA1838 suggests char* but our project doesn't enable /unsafe; StringBuilder is fine for
    // a one-shot non-hot-path P/Invoke that runs at file-open time only.
#pragma warning disable CA1838
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(string lpszLongPath,
        [Out] StringBuilder? lpszShortPath, uint cchBuffer);
#pragma warning restore CA1838

    /// <summary>
    /// Returns a non-Unicode (ASCII-printable) version of <paramref name="path"/> by replacing
    /// any path component containing characters outside <c>0x20..0x7E</c> with its Windows 8.3
    /// short name. Used to feed paths to vendor SDKs (Waters MassLynx, Bruker CompassXtract,
    /// etc.) whose ANSI APIs reject UTF-8 bytes outside the current code page. Mirrors
    /// pwiz C++ <c>get_non_unicode_path</c> in <c>pwiz/utility/misc/Filesystem.cpp</c>.
    /// </summary>
    /// <remarks>
    /// On non-Windows the input is returned unchanged. On Windows, if the path contains no
    /// non-ASCII characters or no component can be resolved (e.g. doesn't exist on disk yet),
    /// the original path is returned. The conversion only kicks in for components that exist
    /// — <c>GetShortPathNameW</c> requires the file/directory to be present.
    /// </remarks>
    public static string GetNonUnicodePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!OperatingSystem.IsWindows()) return path;
        if (path.Length == 0 || IsAllPrintableAscii(path)) return path;

        // Walk path components from the root, replacing any component with non-ASCII chars by
        // its 8.3 short name. We must accumulate the path because GetShortPathName needs an
        // existing path to resolve.
        string root = Path.GetPathRoot(path) ?? "";
        string remainder = path.Substring(root.Length);
        var components = remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        string current = root;
        var result = new StringBuilder(root);
        bool stopShortening = false;
        for (int i = 0; i < components.Length; i++)
        {
            string part = components[i];
            current = string.IsNullOrEmpty(current)
                ? part
                : Path.Combine(current, part);

            string usePart = part;
            if (!stopShortening && !IsAllPrintableAscii(part) && Directory.Exists(current))
            {
                if (TryGetShortPathName(current, out string? shortPath) && !string.IsNullOrEmpty(shortPath))
                    usePart = Path.GetFileName(shortPath);
            }

            // GetShortPathName requires the path to exist on disk. Once we hit a non-existent
            // component, we stop trying and append the rest verbatim.
            if (!Directory.Exists(current) && !File.Exists(current))
                stopShortening = true;

            if (result.Length > 0
                && result[result.Length - 1] != Path.DirectorySeparatorChar
                && result[result.Length - 1] != Path.AltDirectorySeparatorChar)
                result.Append(Path.DirectorySeparatorChar);
            result.Append(usePart);
        }
        return result.ToString();
    }

    private static bool TryGetShortPathName(string longPath, out string? shortPath)
    {
        shortPath = null;
        if (!OperatingSystem.IsWindows()) return false;
        // Two-call pattern: query length first, then fill the buffer. GetShortPathNameW
        // returns the size including the null terminator on the length query, and the
        // length excluding the null on the fill call.
        uint len = GetShortPathNameW(longPath, null, 0);
        if (len == 0) return false;
        var buf = new StringBuilder((int)len);
        uint copied = GetShortPathNameW(longPath, buf, len);
        if (copied == 0 || copied >= len) return false;
        shortPath = buf.ToString();
        return true;
    }

    private static bool IsAllPrintableAscii(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // Match the cpp predicate: bytes in [0x20, 0x7E]. Anything else (control chars,
            // chars above 127) triggers the short-path fallback.
            if (c < 32 || c > 126) return false;
        }
        return true;
    }
}
