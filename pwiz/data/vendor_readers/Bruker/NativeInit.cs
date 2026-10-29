using System.Runtime.InteropServices;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Loads the OpenMP runtime that <c>libbaf2sql_c.so</c> depends on, before anything P/Invokes
/// into it.
/// </summary>
/// <remarks>
/// <para><c>libbaf2sql_c.so</c> lists <c>libgomp.so.1</c> in <c>DT_NEEDED</c>;
/// <c>libtimsdata.so</c> does not. Not every distribution or container image ships libgomp, and
/// the failure mode is misleading: <c>dlopen</c> reports the *dependency* as
/// "libgomp.so.1: cannot open shared object file", which reads as if our own library were the
/// one missing.</para>
/// <para>Shipping libgomp beside the managed output is not enough on its own. The vendor built
/// <c>libbaf2sql_c.so</c> with an <c>RPATH</c> pointing at their build machine's Jenkins
/// workspace and no <c>$ORIGIN</c> entry, so the loader never searches the application directory
/// for its dependencies. Loading libgomp explicitly by absolute path first puts it in the
/// process under its soname, which the later <c>dlopen</c> of <c>libbaf2sql_c.so</c> then
/// resolves against. Confirmed by hiding the system copy: bundling alone still fails, bundling
/// plus this preload succeeds.</para>
/// <para>Best-effort by design, for two reasons. The bundled build needs GLIBC 2.34 - higher
/// than either Bruker library asks for - so on an older distribution it will not load, and the
/// system copy (if present) should be used instead. And where libgomp is already installed,
/// nothing here is needed at all. Failing loudly would turn working configurations into hard
/// errors; if the dependency genuinely cannot be satisfied, the subsequent P/Invoke reports it.
/// </para>
/// </remarks>
internal static class NativeInit
{
    /// <summary>
    /// Idempotent; the runtime keeps the handle alive for the process. Invoked from
    /// <see cref="NativeMethods"/>'s static constructor rather than a
    /// <c>[ModuleInitializer]</c>: a module initializer runs on any first use of the assembly
    /// (which CA2255 rightly flags for a library), whereas the CLR guarantees a static
    /// constructor runs before the first call into that type - which is exactly the moment the
    /// dependency has to be in place, and no earlier.
    /// </summary>
    internal static void PreloadOpenMpRuntime()
    {
        if (OperatingSystem.IsWindows()) return;

        string bundled = Path.Combine(AppContext.BaseDirectory, "libgomp.so.1");
        if (File.Exists(bundled))
            NativeLibrary.TryLoad(bundled, out _);
    }
}
