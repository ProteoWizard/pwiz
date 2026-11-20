using Pwiz.Util.Misc;

namespace Pwiz.Util.Tests;

/// <summary>
/// The msvcp140 version test behind <see cref="AppLocalCrt.EnsureMsvcp140Compatible"/>, which is
/// what turns a vendor library's access violation into an exception, and the runtime the repo
/// ships for every vendor-reader host to stage beside itself.
/// </summary>
[TestClass]
public class AppLocalCrtTests
{
    [TestMethod]
    public void IsBelowMinimum_SeparatesTooOldRuntimes()
    {
        // 14.40 is where the VS 2022 17.10 STL's constexpr std::mutex became safe.
        var cases = new (string Version, bool ExpectedBelow)[]
        {
            ("14.0.23026.0", true),    // VS 2015, as TortoiseSVN 1.9 ships it
            ("14.29.30157.0", true),
            ("14.39.33523.0", true),
            ("14.40.33810.0", false),
            ("14.44.35211.0", false),
            ("14.51.36247.0", false),
        };
        foreach (var c in cases)
            Assert.AreEqual(c.ExpectedBelow, AppLocalCrt.IsBelowMinimum(Version.Parse(c.Version)), c.Version);
    }

    [TestMethod]
    public void ShippedRuntimeMeetsMinimum()
    {
        if (!OperatingSystem.IsWindows()) return; // Reads the native PE version resource

        string? dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "pwiz_tools")))
            dir = Path.GetDirectoryName(dir);
        Assert.IsNotNull(dir, "pwiz_tools not found above " + AppContext.BaseDirectory);
        string shipped = Path.Combine(dir, "pwiz_tools", "Shared", "Lib", "Microsoft.VC140.CRT", "x64", "msvcp140.dll");
        Assert.IsTrue(File.Exists(shipped), $"Missing {shipped}");
        Assert.IsFalse(AppLocalCrt.IsBelowMinimum(shipped), shipped);
    }
}
