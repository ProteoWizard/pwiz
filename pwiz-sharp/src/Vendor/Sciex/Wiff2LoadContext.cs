using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// Side-by-side <see cref="AssemblyLoadContext"/> for the <c>.wiff2</c> SDK
/// (<c>SCIEX.Apis.Data.v1</c>) and its bundled dependencies. Required because the Sciex
/// SmartAssembly bundle ships <c>Clearcore2.*</c> / <c>OFX.*</c> / <c>Unity.*</c> with
/// PublicKeyToken=null, while the legacy <c>.wiff</c> path's <c>AnalystDataProvider</c> binds
/// against on-disk signed copies (PKT=2a79e0d8fd2e4eca etc.) of the same names. Default ALC
/// can serve only one identity per name; this ALC loads the bundled versions in isolation so
/// both paths coexist.
/// </summary>
internal sealed class Wiff2LoadContext : AssemblyLoadContext
{
    private static readonly object s_lock = new();
    private static Wiff2LoadContext? s_instance;

    /// <summary>Lazy singleton. The first wiff2 reader created in the process initializes it.</summary>
    public static Wiff2LoadContext Instance
    {
        get
        {
            if (s_instance is not null) return s_instance;
            lock (s_lock)
            {
                s_instance ??= new Wiff2LoadContext();
                return s_instance;
            }
        }
    }

    /// <summary>Bundled assemblies extracted from <c>SCIEX.Apis.Data.v1.dll</c>'s SmartAssembly
    /// resources, indexed by simple name. Loaded on demand via
    /// <c>AssemblyLoadContext.LoadFromStream</c>.</summary>
    private readonly Dictionary<string, byte[]> _bundle = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Sciex SDK assembly itself. Holding this on the type so the SDK's static
    /// state (license / Unity container / SmartAssembly resolver) survives across reader
    /// instances.</summary>
    public Assembly SciexAssembly { get; }

    /// <summary>Optional path of a wiff2-specific native interop directory. The wiff2 SDK
    /// expects the older <c>SQLite.Interop.dll</c> matching its bundled
    /// <c>System.Data.SQLite 1.0.109</c>; default ALC keeps NuGet's newer interop for the
    /// rest of the app.</summary>
    private static readonly string s_wiff2NativeDir =
        Path.Combine(AppContext.BaseDirectory, "wiff2");

    private Wiff2LoadContext() : base("Wiff2", isCollectible: false)
    {
        // Load SCIEX.Apis.Data.v1 into this ALC. The bundled SmartAssembly resources travel
        // with it; we then read those resource bytes and stash them in _bundle so subsequent
        // Load() calls can serve them into this ALC (instead of letting them leak into default
        // ALC via SmartAssembly's AppDomain.AssemblyResolve hook).
        SciexAssembly = LoadFromAssemblyPath(
            Path.Combine(AppContext.BaseDirectory, "SCIEX.Apis.Data.v1.dll"));
        ExtractBundle();
        // Override Unity.Abstractions with our Cecil-patched copy if present (NOPs the
        // RegisterSerializationHandler that calls Exception.add_SerializeObjectState).
        string patchedUnity = Path.Combine(s_wiff2NativeDir, "Unity.Abstractions.dll");
        if (File.Exists(patchedUnity))
            _bundle["Unity.Abstractions"] = File.ReadAllBytes(patchedUnity);
        // wiff2-specific System.Data.SQLite (1.0.109; no SEE license probe). The on-disk
        // version on the bin root is the NuGet 1.0.119 that Bruker uses; this ALC uses 1.0.109.
        string wiff2Sqlite = Path.Combine(s_wiff2NativeDir, "System.Data.SQLite.dll");
        if (File.Exists(wiff2Sqlite))
            _bundle["System.Data.SQLite"] = File.ReadAllBytes(wiff2Sqlite);

        // Trigger SmartAssembly's resolver so anything we don't have cached can still resolve
        // through it (and so the SDK's licensing / Unity wire-up can run).
        try
        {
            SciexAssembly.GetType("SmartAssembly.AssemblyResolver.AssemblyResolver", throwOnError: false)
                ?.GetMethod("AttachApp", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
        }
        catch { /* best-effort */ }
    }

    private void ExtractBundle()
    {
        // SmartAssembly stores each bundled assembly as a manifest resource named with a GUID,
        // compressed via SmartAssembly.Zip.SimpleZip. Walk the resources, decompress, sniff PE
        // metadata for the assembly name, and cache the bytes by name.
        var unzip = SciexAssembly.GetType("SmartAssembly.Zip.SimpleZip", throwOnError: false)
                    ?.GetMethod("Unzip", BindingFlags.Public | BindingFlags.Static);
        if (unzip is null) return;
        foreach (var resName in SciexAssembly.GetManifestResourceNames())
        {
            using var s = SciexAssembly.GetManifestResourceStream(resName);
            if (s is null) continue;
            byte[] raw;
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                raw = ms.ToArray();
            }
            byte[]? unpacked = null;
            try { unpacked = (byte[]?)unzip.Invoke(null, new object[] { raw }); }
            catch { continue; }
            if (unpacked is null || unpacked.Length < 64) continue;
            if (unpacked[0] != 0x4D || unpacked[1] != 0x5A) continue; // not a PE image

            string? asmName = null;
            try
            {
                using var pe = new PEReader(new MemoryStream(unpacked));
                if (!pe.HasMetadata) continue;
                var mr = pe.GetMetadataReader();
                asmName = mr.GetString(mr.GetAssemblyDefinition().Name);
            }
            catch { continue; }

            // Don't overwrite SCIEX.Apis.Data.v1 (we loaded it from disk above).
            if (asmName == "SCIEX.Apis.Data.v1") continue;
            _bundle[asmName] = unpacked;
        }
    }

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name is null) return null;
        if (_bundle.TryGetValue(name, out var bytes))
            return LoadFromStream(new MemoryStream(bytes));
        // Fall through to default ALC for shared / system / Pwiz.* dependencies.
        return null;
    }

    /// <inheritdoc/>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // Route SQLite.Interop.dll lookups inside this ALC to our wiff2-specific (older)
        // native interop. Default ALC's NuGet System.Data.SQLite is on a different path so
        // Win32 LoadLibrary treats them as separate images — both can coexist.
        if (string.Equals(unmanagedDllName, "SQLite.Interop.dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(unmanagedDllName, "SQLite.Interop", StringComparison.OrdinalIgnoreCase))
        {
            string p = Path.Combine(s_wiff2NativeDir, "SQLite.Interop.dll");
            if (File.Exists(p)) return LoadUnmanagedDllFromPath(p);
        }
        return IntPtr.Zero;
    }
}
