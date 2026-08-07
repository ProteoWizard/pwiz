using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

namespace Pwiz.Vendor.Common;

/// <summary>
/// Runtime resolver for vendor SDK assemblies. The installer MSI ships only pwiz-sharp's
/// own DLLs; vendor SDK archives (vendor_api_Thermo.7z, vendor_api_Bruker.7z, …) are
/// pinned to specific pwiz commit SHAs and downloaded on first use from
/// raw.githubusercontent.com.
/// </summary>
/// <remarks>
/// <para>Wire-up: each entry-point EXE (<c>msconvert-sharp</c>, <c>MSConvertGUI-sharp</c>,
/// <c>seems-sharp</c>) calls <see cref="RegisterAssemblyResolver"/> once at startup. When
/// pwiz-sharp's reader code first runs and the JIT tries to bind a ThermoFisher.* /
/// Clearcore2.* / MIDAC.* / EDAL.* / etc. assembly, the resolver fires, picks the matching
/// pin entry by simple-name prefix, downloads + extracts the 7z if not yet cached, and
/// returns the loaded <see cref="Assembly"/>. Subsequent loads of any DLL in the same
/// archive hit the cache.</para>
/// <para>Pins come from <see cref="VendorSdkPins.All"/> — a generated array baked into
/// this assembly at installer-build time by <c>build/VendorPinsGenerator</c>.
/// Each pin's URL contains its commit SHA, so GitHub serves byte-immutable content
/// forever; the recorded SHA-256 is defense-in-depth.</para>
/// <para>Cache: per-user at <c>%LOCALAPPDATA%\ProteoWizard\vendor\&lt;Vendor&gt;-&lt;ShortSha&gt;\</c>.
/// Per-machine deployments can override by writing a path into
/// <c>%PROGRAMDATA%\ProteoWizard\vendor-cache-root.txt</c>; admins use this to
/// pre-populate the cache for shared workstations.</para>
/// <para>The archive password ("i-agree-to-the-vendor-licenses") is a fixed legal
/// "agreement" gate — same posture as the build-time <c>-p:IAgreeToVendorLicenses=true</c>
/// flag. We do not display any EULA at runtime; vendor SDK license compliance is the
/// user's responsibility.</para>
/// </remarks>
public static class VendorSdkLoader
{
    private static readonly object _registerLock = new();
    private static bool _registered;
    private static string? _cacheRoot;
    private static readonly Dictionary<string, string> _vendorExtractDir = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Assembly> _nativeRegistered = new();
    private static readonly HashSet<string> _reportedFailures = new(StringComparer.OrdinalIgnoreCase);
    private const string ArchivePassword = "i-agree-to-the-vendor-licenses";

    /// <summary>Hooks the default load context's <see cref="AssemblyLoadContext.Resolving"/>
    /// event, and native (<c>DllImport</c>) resolution for every vendor assembly. Safe to call
    /// multiple times — only the first registers the handlers.</summary>
    public static void RegisterAssemblyResolver()
    {
        lock (_registerLock)
        {
            if (_registered) return;
            AssemblyLoadContext.Default.Resolving += OnAssemblyResolving;

            // Native resolution has to be hooked per-assembly, and it must be hooked before that
            // assembly's first DllImport runs. Doing it on AssemblyLoad (plus a sweep of what is
            // already loaded) means a new vendor reader is covered the day it is added -- nobody
            // has to remember to register it. That matters here: this whole hook exists because
            // the native half of vendor loading was overlooked for years.
            AppDomain.CurrentDomain.AssemblyLoad += (_, e) => TryRegisterNativeResolver(e.LoadedAssembly);
            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
                TryRegisterNativeResolver(loaded);

            _registered = true;
        }
    }

    /// <summary>
    /// Hooks native-library (<c>DllImport</c>) resolution for <paramref name="assembly"/> so a
    /// vendor's native SDK is fetched on demand exactly like its managed one. Idempotent.
    /// </summary>
    /// <remarks>
    /// <para><see cref="RegisterAssemblyResolver"/> alone is not enough.
    /// <see cref="AssemblyLoadContext.Resolving"/> fires only for <b>managed</b> binds, while
    /// Bruker's <c>timsdata</c> / <c>baf2sql_c</c> and Waters' <c>MassLynxRaw</c> are native
    /// libraries reached through <c>DllImport</c> — which goes straight to the OS loader and
    /// never passes through <see cref="AssemblyLoadContext"/>. The installer strips those files
    /// from the MSI using the same prefix table this class matches on, so without this hook the
    /// installed product raises <c>DllNotFoundException</c> for every native vendor while the
    /// managed ones (Thermo, Sciex, Agilent) work fine.</para>
    /// </remarks>
    public static void RegisterNativeResolver(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        lock (_registerLock)
        {
            if (!_nativeRegistered.Add(assembly)) return;
            NativeLibrary.SetDllImportResolver(assembly, ResolveNativeLibrary);
        }
    }

    private static void TryRegisterNativeResolver(Assembly assembly)
    {
        if (assembly.IsDynamic) return;
        string name = assembly.GetName().Name ?? string.Empty;
        if (!name.StartsWith("Pwiz.Vendor.", StringComparison.Ordinal)) return;
        try
        {
            RegisterNativeResolver(assembly);
        }
        catch (InvalidOperationException)
        {
            // Something else already set a resolver on this assembly; leave it alone.
        }
    }

    /// <summary>
    /// Reports why a vendor SDK could not be supplied, once per library.
    /// </summary>
    /// <remarks>
    /// Both resolvers deliberately return "not handled" on failure so the runtime's own
    /// <c>DllNotFoundException</c> / <c>FileNotFoundException</c> surfaces with its normal
    /// message - throwing from a resolver produces a confusing loader-level error instead. The
    /// cost is that the message the user actually sees describes the wrong layer: "Unable to
    /// load shared library 'timsdata'" followed by a probe list, when the real cause was a
    /// failed download, a hash mismatch, or an extraction error. Twice during development that
    /// sent the reader hunting for a missing file when the archive had downloaded perfectly and
    /// only the unpack had failed. <see cref="Trace"/> alone does not fix it: console apps have
    /// no listener attached, so the explanation goes nowhere. Write one line to stderr as well,
    /// immediately before the misleading exception, so the two are read together.
    /// </remarks>
    private static void ReportResolveFailure(string requested, VendorSdkPin entry, string detail)
    {
        // Our own exceptions already carry the tag; adding a second one reads like a bug.
        const string tag = "[VendorSdkLoader] ";
        if (detail.StartsWith(tag, StringComparison.Ordinal))
            detail = detail[tag.Length..];

        string message =
            $"[VendorSdkLoader] could not supply '{requested}' from the {entry.Name} vendor SDK: {detail}\n" +
            $"    archive: {entry.Url}\n" +
            $"    cache:   {Path.Combine(GetCacheRoot(), $"{entry.Name}-{entry.Version}")}\n" +
            $"    The load error reported below is a consequence of this, not the cause.";
        Trace.TraceError(message);
        lock (_registerLock)
        {
            // One report per library; the resolver can be consulted more than once for the same
            // name, and repeating this drowns the real error that follows it.
            if (!_reportedFailures.Add(requested)) return;
        }
        try { Console.Error.WriteLine(message); }
        catch (IOException) { /* stderr redirected to something closed; Trace still has it */ }
    }

    /// <summary>
    /// The pin whose prefixes match <paramref name="requested"/>, narrowed to this OS.
    /// </summary>
    /// <remarks>
    /// An OS-specific entry wins over a generic one, so a vendor can add a per-OS archive
    /// without the existing entry having to be relabelled: on Linux, Bruker's linux_x64 pin
    /// beats the unlabelled one that carries the Windows DLLs, while on Windows the linux entry
    /// is filtered out entirely and the unlabelled one is all that is left.
    /// </remarks>
    private static VendorSdkPin? FindPin(string requested)
    {
        string os = OperatingSystem.IsWindows() ? "windows" : "linux";
        var matches = VendorSdkPins.All
            .Where(v => v.AssemblyPrefixes.Any(p => requested.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Where(v => v.Os is null || v.Os.Equals(os, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.FirstOrDefault(v => v.Os is not null) ?? matches.FirstOrDefault();
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var entry = FindPin(libraryName);
        if (entry is null) return IntPtr.Zero; // not a vendor library — default probing

        // Already staged beside the managed output, which is what every dev and CI build does.
        // Returning zero hands back to default probing instead of loading a second copy by
        // absolute path.
        foreach (string candidate in NativeFileNames(libraryName))
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, candidate)))
                return IntPtr.Zero;

        try
        {
            string dir = EnsureExtracted(entry);
            foreach (string candidate in NativeFileNames(libraryName))
            {
                string path = Path.Combine(dir, candidate);
                if (File.Exists(path))
                    return NativeLibrary.Load(path);
            }
            ReportResolveFailure(libraryName, entry,
                $"the archive extracted cleanly but contains no library matching " +
                $"{string.Join(" / ", NativeFileNames(libraryName))}");
        }
        catch (Exception ex)
        {
            // Don't throw from the resolver — that surfaces as a confusing loader-level failure.
            // Returning zero lets the normal DllNotFoundException carry the original message.
            ReportResolveFailure(libraryName, entry, ex.Message);
        }
        return IntPtr.Zero;
    }

    /// <summary>Platform spellings of a <c>DllImport</c> name, in probe order.</summary>
    private static IEnumerable<string> NativeFileNames(string libraryName)
    {
        if (OperatingSystem.IsWindows())
        {
            yield return libraryName + ".dll";
        }
        else
        {
            // The archives ship the vendor's own spelling, which is normally libfoo.so, but the
            // DllImport name is "foo" — try both rather than assume.
            yield return "lib" + libraryName + ".so";
            yield return libraryName + ".so";
        }
        yield return libraryName;
    }

    private static Assembly? OnAssemblyResolving(AssemblyLoadContext context, AssemblyName name)
    {
        // Pick the vendor whose AssemblyPrefixes match the requested simple-name. The pin table
        // keeps prefix sets disjoint across vendors, so at most one vendor matches; FindPin
        // narrows the remaining per-OS ambiguity within a vendor.
        string requested = name.Name ?? string.Empty;
        var entry = FindPin(requested);
        if (entry is null) return null;

        try
        {
            string extractDir = EnsureExtracted(entry);
            string candidate = Path.Combine(extractDir, requested + ".dll");
            if (File.Exists(candidate))
                return context.LoadFromAssemblyPath(candidate);
            ReportResolveFailure(requested, entry,
                $"the archive extracted cleanly but contains no {requested}.dll");
            return null;
        }
        catch (Exception ex)
        {
            // Don't throw from the resolver — that surfaces as a confusing CLR-level
            // "loader exception" without context. Report + return null so the original
            // assembly-load failure propagates with its normal message.
            ReportResolveFailure(requested, entry, ex.Message);
            return null;
        }
    }

    /// <summary>Ensures <paramref name="entry"/>'s archive is downloaded + extracted into
    /// the cache. Returns the extraction directory.</summary>
    public static string EnsureExtracted(VendorSdkPin entry)
    {
        if (_vendorExtractDir.TryGetValue(entry.Name, out string? cached))
            return cached;

        string root = GetCacheRoot();
        string dest = Path.Combine(root, $"{entry.Name}-{entry.Version}");
        string marker = Path.Combine(dest, ".ok");

        if (!File.Exists(marker))
        {
            Directory.CreateDirectory(dest);
            string archivePath = Path.Combine(root, $"{entry.Name}-{entry.Version}.7z");
            DownloadIfMissing(entry, archivePath);
            VerifyHash(entry, archivePath);
            ExtractArchive(archivePath, dest);
            File.WriteAllText(marker, $"extracted {DateTime.UtcNow:o}\nfrom {entry.Url}");
        }

        _vendorExtractDir[entry.Name] = dest;
        return dest;
    }

    private static string GetCacheRoot()
    {
        if (_cacheRoot is not null) return _cacheRoot;
        // Per-user cache. Installer with per-machine scope drops a marker at
        // %PROGRAMDATA%\ProteoWizard\vendor-cache-root.txt pointing at a shared dir;
        // detect that and use it so admins can pre-populate the cache.
        string sharedMarker = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ProteoWizard", "vendor-cache-root.txt");
        if (File.Exists(sharedMarker))
        {
            string shared = File.ReadAllText(sharedMarker).Trim();
            if (!string.IsNullOrWhiteSpace(shared))
            {
                Directory.CreateDirectory(shared);
                _cacheRoot = shared;
                return _cacheRoot;
            }
        }
        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProteoWizard", "vendor");
        Directory.CreateDirectory(_cacheRoot);
        return _cacheRoot;
    }

    private static void DownloadIfMissing(VendorSdkPin entry, string dest)
    {
        if (File.Exists(dest)) return;
        Trace.TraceInformation($"[VendorSdkLoader] downloading {entry.Name} from {entry.Url}");
        // Synchronous download — vendor SDK loads happen on the worker thread that's
        // about to read the file anyway. HttpClient with no timeout fits ~100 MB archives
        // (the larger vendor SDKs); user-perceived latency is the price of "lazy fetch".
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        using var response = http.GetAsync(entry.Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        string tmp = dest + ".part";
        using (var fs = File.Create(tmp))
        using (var src = response.Content.ReadAsStream())
            src.CopyTo(fs);
        File.Move(tmp, dest, overwrite: true);
    }

    private static void VerifyHash(VendorSdkPin entry, string archivePath)
    {
        if (string.IsNullOrEmpty(entry.Sha256)) return;  // hash check is opt-in per entry
        using var stream = File.OpenRead(archivePath);
        byte[] hash = SHA256.HashData(stream);
        string actual = Convert.ToHexString(hash);
        if (!actual.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(archivePath);
            throw new InvalidDataException(
                $"[VendorSdkLoader] SHA-256 mismatch on {entry.Name} archive. " +
                $"Expected {entry.Sha256}, got {actual}. Archive deleted; loader will re-download next call.");
        }
    }

    private static void ExtractArchive(string archivePath, string extractDir)
    {
        // 7-Zip sits next to Pwiz.Vendor.Common.dll (csproj <None Include> at build time), and
        // which binary that is depends on the platform: 7za.exe is a Windows PE and 7zz is the
        // Linux build. Hardcoding the former made extraction the one step of runtime SDK
        // resolution that could not work off-Windows - and it failed late, after the download
        // and hash check had already succeeded.
        string toolName = OperatingSystem.IsWindows() ? "7za.exe" : "7zz";
        string sevenZip = Path.Combine(AppContext.BaseDirectory, toolName);
        if (!File.Exists(sevenZip))
            throw new FileNotFoundException(
                $"[VendorSdkLoader] {toolName} not found alongside Pwiz.Vendor.Common.dll. " +
                $"Looked at: {sevenZip}");
        EnsureExecutable(sevenZip);

        var psi = new ProcessStartInfo
        {
            FileName = sevenZip,
            ArgumentList = { "x", "-y", $"-p{ArchivePassword}", $"-o{extractDir}", archivePath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;

        // Drain both pipes WHILE the child runs. Waiting first and reading afterwards deadlocks:
        // 7za prints a line per extracted file, and once that fills the OS pipe buffer (a few KB)
        // it blocks on write while we block in WaitForExit, and neither side ever moves. The
        // small archives hide it - Thermo and Waters hold a handful of files each - but Bruker's
        // holds 89 and hangs every time. Event-driven reads keep the pipes empty; no async/await.
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"[VendorSdkLoader] 7za.exe failed extracting {archivePath} (exit {proc.ExitCode}). " +
                $"stdout: {stdout.ToString().Trim()}, stderr: {stderr.ToString().Trim()}");

        // The vendor archives typically extract into a nested vendor_api/<Vendor>/ structure.
        // Flatten it so subsequent Assembly.LoadFromAssemblyPath sees the DLLs at the top level
        // of extractDir.
        FlattenVendorArchiveLayout(extractDir);
    }

    /// <summary>
    /// Adds the owner-execute bit on Unix when it is missing. The bundled 7zz is committed
    /// executable, but a payload can lose the mode in transit - unzipped from a .zip, restored
    /// from a NuGet package, or copied through a filesystem with no permission model - and the
    /// resulting failure ("Permission denied" from Process.Start) reads nothing like the vendor
    /// SDK problem it actually causes. No-op on Windows.
    /// </summary>
    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            var mode = File.GetUnixFileMode(path);
            if ((mode & UnixFileMode.UserExecute) == 0)
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
        }
        catch (Exception ex)
        {
            // Read-only install dir, say. Let Process.Start produce the real error.
            Trace.TraceWarning($"[VendorSdkLoader] could not mark {path} executable: {ex.Message}");
        }
    }

    private static void FlattenVendorArchiveLayout(string extractDir)
    {
        string nested = Path.Combine(extractDir, "vendor_api");
        if (!Directory.Exists(nested)) return;
        foreach (string dir in Directory.GetDirectories(nested))
        {
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                // Drop subfolder hierarchy (x86/x64/etc.) — pwiz-sharp targets x64 only.
                string rel = Path.GetRelativePath(dir, file);
                // Skip the obvious wrong-arch dirs (x86 / mips / etc.).
                string firstSegment = rel.Split(Path.DirectorySeparatorChar, 2)[0];
                if (firstSegment.Equals("x86", StringComparison.OrdinalIgnoreCase) ||
                    firstSegment.Equals("mips", StringComparison.OrdinalIgnoreCase))
                    continue;

                string flatName = Path.GetFileName(rel);
                string dest = Path.Combine(extractDir, flatName);
                if (!File.Exists(dest))
                    File.Move(file, dest);
            }
        }
        try { Directory.Delete(nested, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}

/// <summary>One vendor SDK pin entry. Populated by the generated
/// <see cref="VendorSdkPins.All"/> array.</summary>
/// <param name="Name">Vendor identifier; also names the per-version cache directory.</param>
/// <param name="Version">Short SHA of the commit the archive is pinned to.</param>
/// <param name="Url">Byte-immutable raw.githubusercontent.com URL containing that SHA.</param>
/// <param name="Sha256">Expected hash of the archive; defence in depth over the pinned URL.</param>
/// <param name="AssemblyPrefixes">Simple-name prefixes this archive answers to, for both
/// managed binds and <c>DllImport</c> names.</param>
/// <param name="Os">
/// Which OS this archive is for: <c>"windows"</c>, <c>"linux"</c>, or null when it applies to
/// any. Most vendors need only one archive, because a managed SDK is portable and the
/// Windows-only vendors are never resolved elsewhere; those entries leave this null. Bruker
/// needs two, because its Windows and Linux native libraries live in separate archives and both
/// answer to the same <paramref name="AssemblyPrefixes"/> - so a match has to be narrowed by OS,
/// or Linux would fetch 10.7 MB of Windows DLLs and still fail to find libtimsdata.so.
/// </param>
public sealed record VendorSdkPin(
    string Name,
    string Version,
    string Url,
    string Sha256,
    string[] AssemblyPrefixes,
    string? Os = null);
