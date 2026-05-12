using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

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
/// this assembly at installer-build time by <c>installer/Refresh-VendorPins.ps1</c>.
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
    private const string ArchivePassword = "i-agree-to-the-vendor-licenses";

    /// <summary>Hooks the default load context's <see cref="AssemblyLoadContext.Resolving"/>
    /// event. Safe to call multiple times — only the first registers the handler.</summary>
    public static void RegisterAssemblyResolver()
    {
        lock (_registerLock)
        {
            if (_registered) return;
            AssemblyLoadContext.Default.Resolving += OnAssemblyResolving;
            _registered = true;
        }
    }

    private static Assembly? OnAssemblyResolving(AssemblyLoadContext context, AssemblyName name)
    {
        // Pick the vendor whose AssemblyPrefixes match the requested simple-name. The first
        // match wins; the pin table keeps prefix sets disjoint.
        string requested = name.Name ?? string.Empty;
        var entry = VendorSdkPins.All.FirstOrDefault(v =>
            v.AssemblyPrefixes.Any(p => requested.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
        if (entry is null) return null;

        try
        {
            string extractDir = EnsureExtracted(entry);
            string candidate = Path.Combine(extractDir, requested + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        }
        catch (Exception ex)
        {
            // Don't throw from the resolver — that surfaces as a confusing CLR-level
            // "loader exception" without context. Log + return null so the original
            // assembly-load failure propagates with its normal message.
            Trace.TraceError($"[VendorSdkLoader] failed to resolve {requested} from {entry.Name}: {ex}");
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
        // 7za.exe sits next to Pwiz.Vendor.Common.dll (csproj <None Include> at build time).
        string sevenZip = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        if (!File.Exists(sevenZip))
            throw new FileNotFoundException(
                $"[VendorSdkLoader] 7za.exe not found alongside Pwiz.Vendor.Common.dll. " +
                $"Looked at: {sevenZip}");

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
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            string err = proc.StandardError.ReadToEnd();
            string @out = proc.StandardOutput.ReadToEnd();
            throw new InvalidOperationException(
                $"[VendorSdkLoader] 7za.exe failed extracting {archivePath} (exit {proc.ExitCode}). " +
                $"stdout: {@out.Trim()}, stderr: {err.Trim()}");
        }

        // The vendor archives typically extract into a nested vendor_api/<Vendor>/ structure.
        // Flatten it so subsequent Assembly.LoadFromAssemblyPath sees the DLLs at the top level
        // of extractDir.
        FlattenVendorArchiveLayout(extractDir);
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
public sealed record VendorSdkPin(
    string Name,
    string Version,
    string Url,
    string Sha256,
    string[] AssemblyPrefixes);
