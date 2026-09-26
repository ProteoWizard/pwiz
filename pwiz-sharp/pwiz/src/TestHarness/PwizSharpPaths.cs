using System;
using System.IO;

namespace Pwiz.TestHarness;

/// <summary>
/// Anchored filesystem paths for test code that needs to find pwiz-sharp source-tree
/// fixtures, the sibling cpp pwiz checkout's vendor data, the installer build dir, etc.
///
/// Walks parents of <see cref="AppContext.BaseDirectory"/> at first use looking for a
/// directory that LOOKS like pwiz-sharp/ (contains <c>pwiz/</c> + <c>Tools/</c> +
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

    /// <summary>The pwiz-sharp/ checkout root (absolute path).</summary>
    public static string Root => _root.Value;

    /// <summary>The sibling cpp pwiz/ checkout root.</summary>
    public static string CppRoot => Path.GetFullPath(Path.Combine(Root, "..", "pwiz"));

    /// <summary>The sibling cpp pwiz_tools/ checkout root (Skyline + BiblioSpec sources).</summary>
    public static string CppToolsRoot => Path.GetFullPath(Path.Combine(Root, "..", "pwiz_tools"));

    /// <summary>The sibling libraries/ directory (vendor 7z archives, 7za.exe, etc.).</summary>
    public static string LibrariesPath => Path.GetFullPath(Path.Combine(Root, "..", "libraries"));

    /// <summary>pwiz-sharp/example_data — tiny round-trip fixtures (tiny.pwiz.1.1.*).</summary>
    public static string ExampleData => Path.Combine(Root, "example_data");

    /// <summary>pwiz-sharp/installer/build — Inno Setup output dir.</summary>
    public static string InstallerBuildDir => Path.Combine(Root, "installer", "build");

    /// <summary>pwiz-sharp/vendor-assemblies — destination of vendor SDK 7z extraction.</summary>
    public static string VendorAssemblies => Path.Combine(Root, "vendor-assemblies");

    /// <summary>
    /// Vendor test data file from the sibling cpp pwiz/ checkout under
    /// <c>pwiz/data/vendor_readers/&lt;Vendor&gt;/Reader_&lt;Vendor&gt;_Test.data/</c>.
    /// </summary>
    public static string CppVendorTestData(string vendor, string filename) =>
        Path.Combine(CppRoot, "data", "vendor_readers", vendor, $"Reader_{vendor}_Test.data", filename);

    /// <summary>Per-test override Reference/ subdir under a pwiz-sharp test project.</summary>
    public static string TestReferenceDir(string testProjectName) =>
        Path.Combine(Root, "pwiz", "test", testProjectName, "Reference");

    /// <summary>
    /// Walk parents of the calling assembly's base directory until we find a dir that
    /// LOOKS like pwiz-sharp/: contains <c>pwiz/</c> + <c>Tools/</c> + <c>Pwiz.sln</c>.
    /// All three together rule out any other directory we might accidentally land in.
    /// </summary>
    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Looks(dir)) return dir;
            // Also check sibling: handles `dotnet test` invoked from a subdir
            // that's adjacent to (rather than under) pwiz-sharp.
            var sibling = Path.Combine(dir, "pwiz-sharp");
            if (Directory.Exists(sibling) && Looks(sibling)) return Path.GetFullPath(sibling);
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "PwizSharpPaths: cannot find pwiz-sharp root from " +
            $"AppContext.BaseDirectory='{AppContext.BaseDirectory}'. " +
            "Expected an ancestor directory containing pwiz/, Tools/, and Pwiz.sln.");
    }

    private static bool Looks(string candidate) =>
        Directory.Exists(Path.Combine(candidate, "pwiz"))
        && Directory.Exists(Path.Combine(candidate, "Tools"))
        && File.Exists(Path.Combine(candidate, "Pwiz.sln"));
}
