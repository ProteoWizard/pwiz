using System;
using System.IO;

namespace Pwiz.TestHarness;

/// <summary>
/// Anchored filesystem paths for test code that needs to find source-tree fixtures,
/// the vendor test data under pwiz/data, the installer build dir, etc.
///
/// Walks parents of <see cref="AppContext.BaseDirectory"/> at first use looking for a
/// directory that LOOKS like the repo root (contains <c>pwiz/</c> + <c>pwiz_tools/</c> +
/// <c>Pwiz.sln</c>). The discovered root is the anchor for every other property —
/// future tree restructures don't invalidate any consumer code, only this one
/// discovery method.
///
/// Replaces the per-test "../../../../../" walk chains that accumulated through the
/// pwiz/+Tools/ restructure. CodeInspectionTests had its own FindPwizSharpRoot;
/// InstallerTests had two separate walks for the Setup.exe + Thermo fixture;
/// ReaderUnifiHarnessTests had FindOverrideReferenceRoot. All consolidated here.
/// </summary>
public static class PwizSharpPaths
{
    private static readonly Lazy<string> _root = new(FindRoot);

    /// <summary>The repo root (absolute path).</summary>
    public static string Root => _root.Value;

    /// <summary>pwiz/ — the library tree: src/, test/ and the data fixtures beside them.</summary>
    public static string CppRoot => Path.Combine(Root, "pwiz");

    /// <summary>pwiz_tools/ — Skyline, Shared and the pwiz tools (BiblioSpec test corpus lives here).</summary>
    public static string CppToolsRoot => Path.Combine(Root, "pwiz_tools");

    /// <summary>libraries/ — 7za.exe, bsdtar.exe and the msparser archives.</summary>
    public static string LibrariesPath => Path.Combine(Root, "libraries");

    /// <summary>example_data/ — tiny round-trip fixtures (tiny.pwiz.1.1.*).</summary>
    public static string ExampleData => Path.Combine(Root, "example_data");

    /// <summary>scripts/installer/build — Inno Setup output dir.</summary>
    public static string InstallerBuildDir => Path.Combine(Root, "scripts", "installer", "build");

    /// <summary>vendor-assemblies/ — destination of vendor SDK 7z extraction.</summary>
    public static string VendorAssemblies => Path.Combine(Root, "vendor-assemblies");

    /// <summary>
    /// Vendor test data file under
    /// <c>pwiz/data/vendor_readers/&lt;Vendor&gt;/Reader_&lt;Vendor&gt;_Test.data/</c>.
    /// </summary>
    public static string CppVendorTestData(string vendor, string filename) =>
        Path.Combine(CppRoot, "data", "vendor_readers", vendor, $"Reader_{vendor}_Test.data", filename);

    /// <summary>
    /// Per-vendor override Reference/ subdir of a vendor reader's test project:
    /// <c>pwiz/data/vendor_readers/&lt;Vendor&gt;/test/Reference</c>.
    /// </summary>
    public static string VendorTestReferenceDir(string vendorDir) =>
        Path.Combine(CppRoot, "data", "vendor_readers", vendorDir, "test", "Reference");

    /// <summary>
    /// Walk parents of the calling assembly's base directory until we find a dir that
    /// LOOKS like the repo root: contains <c>pwiz/</c> + <c>pwiz_tools/</c> + <c>Pwiz.sln</c>.
    /// All three together rule out any other directory we might accidentally land in.
    /// </summary>
    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Looks(dir)) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "PwizSharpPaths: cannot find the repo root from " +
            $"AppContext.BaseDirectory='{AppContext.BaseDirectory}'. " +
            "Expected an ancestor directory containing pwiz/, pwiz_tools/, and Pwiz.sln.");
    }

    private static bool Looks(string candidate) =>
        Directory.Exists(Path.Combine(candidate, "pwiz"))
        && Directory.Exists(Path.Combine(candidate, "pwiz_tools"))
        && File.Exists(Path.Combine(candidate, "Pwiz.sln"));
}
