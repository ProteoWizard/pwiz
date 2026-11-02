using System.Runtime.CompilerServices;
using System.Runtime.Loader;

#pragma warning disable CA1707
#pragma warning disable CA2255 // ModuleInitializer is intentional here

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// Module initializer that hooks <see cref="AssemblyLoadContext.Default"/> resolution before any
/// other code in this assembly runs. The legacy <c>.wiff</c> path's transitive dependencies
/// (<c>Sciex.Data.Processing.dll</c>, <c>Clearcore2.Data.dll</c>, etc.) are shipped as Content
/// next to the executable but aren't listed in <c>.deps.json</c>; without this hook the runtime
/// fails to load them when the JIT first compiles a method that touches the SDK metadata.
/// The <c>.wiff2</c> path uses its own <see cref="Wiff2LoadContext"/> and is unaffected by this
/// hook.
/// </summary>
internal static class SciexAssemblyResolver
{
    private static readonly object s_lock = new();
    private static bool s_installed;

    [ModuleInitializer]
    internal static void Install()
    {
        if (s_installed) return;
        lock (s_lock)
        {
            if (s_installed) return;
            AssemblyLoadContext.Default.Resolving += (ctx, name) =>
            {
                string baseDir = AppContext.BaseDirectory;
                foreach (var ext in new[] { ".dll", ".DLL" })
                {
                    string p = Path.Combine(baseDir, name.Name + ext);
                    if (File.Exists(p)) return ctx.LoadFromAssemblyPath(p);
                }
                return null;
            };
            s_installed = true;
        }
    }
}
