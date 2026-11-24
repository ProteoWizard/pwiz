using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pwiz.Util.Misc;

/// <summary>
/// Keeps the process on a VC++ runtime new enough for the native libraries it hosts.
/// </summary>
/// <remarks>
/// <para>Windows resolves an import of <c>msvcp140.dll</c> to whichever module of that name is
/// already loaded, from any directory. Shell extensions loaded by a Windows file dialog can carry
/// their own, much older, app-local copy (TortoiseSVN 1.9 ships VS 2015's 14.0), and if one of
/// them loads first it becomes the process's <c>msvcp140.dll</c>.</para>
/// <para>Native code built with the VS 2022 17.10+ STL needs msvcp140 14.40 or later: its
/// <c>std::mutex</c> has a constexpr constructor, and an older msvcp140 locks it through an
/// uninitialized pointer, an access violation. Bruker's timsdata 5.x is built that way.</para>
/// </remarks>
public static class AppLocalCrt
{
    /// <summary>The first msvcp140 release compatible with the VS 2022 17.10 STL's constexpr std::mutex.</summary>
    public static readonly Version MinimumMsvcp140Version = new Version(14, 40);

    // vcruntime first: msvcp140 imports it. vcomp140 is the OpenMP runtime timsdata also imports.
    private static readonly string[] RUNTIME_DLLS =
        { "vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "vcomp140.dll" };

    private static bool _loadedMsvcp140Verified;

    /// <summary>
    /// Loads the VC++ runtime by full path, so that every DLL loaded later binds to it rather than
    /// to whatever copy a later component brings. For each runtime DLL that the application ships
    /// beside itself, the newer of that copy and the system's is loaded, so a newer installed
    /// redistributable is never downgraded. Call it as the first statement of <c>Main</c> in any
    /// application that can host native vendor readers and show a file dialog. Best effort: a
    /// module that is already loaded is left alone (a second copy of the same name would not change
    /// what later imports bind to), and a build directory without the app-local runtime keeps the
    /// normal search.
    /// </summary>
    public static void Pin()
    {
        if (!OperatingSystem.IsWindows())
            return;

        foreach (var dll in RUNTIME_DLLS)
        {
            if (GetModuleHandleW(dll) != IntPtr.Zero)
                continue;
            var appLocal = Path.Combine(AppContext.BaseDirectory, dll);
            if (!File.Exists(appLocal))
                continue;
            var system = Path.Combine(Environment.SystemDirectory, dll);
            var path = File.Exists(system) && FileVersion(system) > FileVersion(appLocal) ? system : appLocal;
            NativeLibrary.TryLoad(path, out _); // Never unloaded
        }
    }

    /// <summary>
    /// Full path of the <c>msvcp140.dll</c> loaded in this process, or null if none is loaded yet
    /// (or not on Windows).
    /// </summary>
    public static string? LoadedMsvcp140Path()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        var module = GetModuleHandleW("msvcp140.dll");
        if (module == IntPtr.Zero)
            return null;
        var buffer = new char[4096];
        uint length = GetModuleFileNameW(module, buffer, (uint)buffer.Length);
        return length == 0 ? null : new string(buffer, 0, (int)length);
    }

    /// <summary>True if <paramref name="version"/> predates <see cref="MinimumMsvcp140Version"/>.</summary>
    public static bool IsBelowMinimum(Version version) => version < MinimumMsvcp140Version;

    /// <summary>
    /// True if the msvcp140 at <paramref name="path"/> predates <see cref="MinimumMsvcp140Version"/>.
    /// </summary>
    public static bool IsBelowMinimum(string path) => IsBelowMinimum(FileVersion(path));

    /// <summary>
    /// Call before the first call into a native library built with the VS 2022 17.10+ STL, while
    /// a runtime that is too old can still be an exception rather than an access violation. Pins
    /// the runtime first (see <see cref="Pin"/>), which covers hosts that did not, then throws if
    /// the msvcp140 the library will bind to - the loaded one, or the system's when none is loaded
    /// and the application ships none - is older than <see cref="MinimumMsvcp140Version"/>. Cheap
    /// after the first success, since a loaded msvcp140 cannot change.
    /// </summary>
    public static void EnsureMsvcp140Compatible(string consumer)
    {
        if (!OperatingSystem.IsWindows() || _loadedMsvcp140Verified)
            return;

        Pin();
        var loaded = LoadedMsvcp140Path();
        var path = loaded;
        if (path == null)
        {
            var system = Path.Combine(Environment.SystemDirectory, "msvcp140.dll");
            if (!File.Exists(system))
                return; // No runtime at all: the library's own load reports that
            path = system;
        }
        if (IsBelowMinimum(path))
        {
            throw new InvalidOperationException(
                $"{consumer} requires msvcp140.dll {MinimumMsvcp140Version} or later, but this process uses version " +
                $"{FileVersionInfo.GetVersionInfo(path).FileVersion} from {path}. If that copy belongs to another program " +
                "(often a shell extension loaded by a file dialog), updating or removing that program fixes this; " +
                "otherwise install the current Microsoft Visual C++ Redistributable.");
        }
        if (loaded != null)
            _loadedMsvcp140Verified = true;
    }

    private static Version FileVersion(string path)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameW(IntPtr module, [Out] char[] fileName, uint size);
}
