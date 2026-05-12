using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pwiz.Vendor.Common;

/// <summary>
/// Runtime resolver for vendor SDK assemblies. The installer MSI ships only pwiz-sharp's
/// own DLLs; vendor SDK archives (vendor_api_Thermo.7z, vendor_api_Bruker.7z, …) live in
/// the pwiz GitHub release for the matching pwiz version and are downloaded on first use.
/// </summary>
/// <remarks>
/// <para>Wire-up: each entry-point EXE (<c>msconvert-sharp</c>, <c>MSConvertGUI-sharp</c>,
/// <c>seems-sharp</c>) calls <see cref="RegisterAssemblyResolver"/> once at startup. When
/// pwiz-sharp's reader code first runs and the JIT tries to bind a ThermoFisher.* / Clearcore2.*
/// / MIDAC.* / EDAL.* / etc. assembly, the resolver fires, picks the matching vendor manifest
/// entry by simple-name prefix, downloads + extracts the 7z if needed, and returns the loaded
/// <see cref="Assembly"/>. Subsequent loads of any DLL in the same archive hit the cache.</para>
/// <para>Manifest format: see <c>installer/vendor-manifest.json</c>. The cache lives at
/// <c>%LOCALAPPDATA%\ProteoWizard\vendor\&lt;Vendor&gt;-&lt;Version&gt;\</c> per-user (or
/// <c>%PROGRAMDATA%\ProteoWizard\vendor\</c> when the installer chose per-machine scope —
/// look at the install-scope marker file).</para>
/// <para>Vendor archive password ("i-agree-to-the-vendor-licenses") is a fixed legal
/// "agreement" gate — same posture as the build flag. The user's responsibility to comply
/// with each vendor's EULA. We do not display any EULA at runtime.</para>
/// </remarks>
public static class VendorSdkLoader
{
    private static readonly object _registerLock = new();
    private static bool _registered;
    private static VendorManifest? _manifest;
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
        var manifest = GetManifest();
        if (manifest is null) return null;

        // Pick the vendor whose AssemblyPrefixes match the requested simple-name. The first
        // match wins; vendor manifest entries should keep their prefix sets disjoint.
        string requested = name.Name ?? string.Empty;
        var entry = manifest.Vendors.FirstOrDefault(v =>
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
    /// the per-user cache. Returns the extraction directory.</summary>
    public static string EnsureExtracted(VendorManifestEntry entry)
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

    private static void DownloadIfMissing(VendorManifestEntry entry, string dest)
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

    private static void VerifyHash(VendorManifestEntry entry, string archivePath)
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

    private static VendorManifest? GetManifest()
    {
        if (_manifest is not null) return _manifest;
        // The manifest ships alongside the EXE (installer/vendor-manifest.json → output dir).
        string path = Path.Combine(AppContext.BaseDirectory, "vendor-manifest.json");
        if (!File.Exists(path))
        {
            Trace.TraceWarning($"[VendorSdkLoader] vendor-manifest.json not found at {path}; vendor SDKs won't be resolved on demand.");
            return null;
        }
        try
        {
            string json = File.ReadAllText(path);
            _manifest = JsonSerializer.Deserialize<VendorManifest>(json, ManifestJsonOptions);
            return _manifest;
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[VendorSdkLoader] failed to parse {path}: {ex.Message}");
            return null;
        }
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

/// <summary>Top-level shape of <c>vendor-manifest.json</c>.</summary>
public sealed class VendorManifest
{
    /// <summary>Manifest format version (currently always 1).</summary>
    [JsonPropertyName("schema")] public int Schema { get; set; } = 1;

    /// <summary>One entry per vendor SDK archive available for on-demand download.</summary>
    [JsonPropertyName("vendors")] public List<VendorManifestEntry> Vendors { get; set; } = new();
}

/// <summary>One vendor's archive descriptor inside the manifest.</summary>
public sealed class VendorManifestEntry
{
    /// <summary>Short vendor identifier (e.g. "Thermo", "Bruker", "Waters", "ABI", "Agilent",
    /// "Shimadzu", "Mobilion"). Used as cache subdir name.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    /// <summary>Archive version, opaque to the loader — used to pick the cache subdir and
    /// invalidate older extractions. Typically matches the pwiz tag or vendor SDK version.</summary>
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;

    /// <summary>HTTPS URL the loader fetches when this vendor's SDK is requested. Typically
    /// a GitHub Releases asset URL.</summary>
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;

    /// <summary>Hex-encoded SHA-256 of the archive. Optional but recommended; loader deletes +
    /// re-downloads on mismatch.</summary>
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;

    /// <summary>Assembly simple-name prefixes that should trigger this vendor's load. E.g.
    /// "ThermoFisher." for Thermo, "Clearcore2." for Sciex, "MIDAC" for Agilent.</summary>
    [JsonPropertyName("assemblyPrefixes")] public List<string> AssemblyPrefixes { get; set; } = new();
}
