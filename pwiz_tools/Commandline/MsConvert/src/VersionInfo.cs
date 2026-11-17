using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Pwiz.Tools.MsConvert;

/// <summary>
/// The version block msconvert prints after its usage text and in its error report, in the form
/// the C++ msconvert printed it, so anything that reads the "ProteoWizard release:" line - the
/// Docker image publish takes its version tag from it - keeps working:
/// <code>
/// ProteoWizard release: 4.0.26281 (92536b2)
/// Build date: Oct 8 2026 15:52:58
/// </code>
/// </summary>
internal static class VersionInfo
{
    /// <summary>"4.0.26281 (92536b2)": the version and short commit hash of this build.</summary>
    internal static string Release { get; } = FormatRelease(
        typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(VersionInfo).Assembly.GetName().Version);

    /// <summary>
    /// Formats the informational version the build stamps ("4.0.26281-92536b2 (automated build)",
    /// see Directory.Build.targets) as "4.0.26281 (92536b2)". Without one in that shape, falls
    /// back to the assembly version with hash "0", which is what the build uses when git is absent.
    /// </summary>
    internal static string FormatRelease(string? informationalVersion, Version? assemblyVersion)
    {
        var match = Regex.Match(informationalVersion ?? string.Empty, @"^(?<version>\d+\.\d+\.\d+)-(?<hash>[0-9A-Za-z]+)");
        if (match.Success)
            return match.Groups["version"].Value + " (" + match.Groups["hash"].Value + ")";
        return (assemblyVersion ?? new Version(0, 0, 0)).ToString(3) + " (0)";
    }

    /// <summary>
    /// When this msconvert was built. .NET has no counterpart to the __DATE__ / __TIME__ the C++
    /// build printed, so this is the assembly file's time, which the installers and the Linux
    /// tarball both keep.
    /// </summary>
    internal static string BuildDate
    {
        get
        {
            string path = typeof(VersionInfo).Assembly.Location;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "unknown";
            return File.GetLastWriteTime(path).ToString("MMM d yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The two-line block, without a trailing newline.</summary>
    internal static string Block => "ProteoWizard release: " + Release + "\n" + "Build date: " + BuildDate;
}
