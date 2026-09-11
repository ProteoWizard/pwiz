using Pwiz.Vendor.UIMF;

namespace Pwiz.Vendor.UIMF.Tests;

/// <summary>
/// Quick "does the SDK load and read the test fixture?" check — exercises every
/// <see cref="UimfData"/> entry point on the BSA fixture. Catches integration regressions
/// (UIMFLibrary.dll TFM mismatch, SQLite native interop missing, etc.) before the
/// per-spectrum harness comparison is even meaningful.
/// </summary>
[TestClass]
public class UimfDataSmokeTests
{
    [TestMethod]
    public void OpenBsaFixture_AllEntryPointsRespond()
    {
        string? root = FindTestDataRoot();
        if (root is null) { Assert.Inconclusive("UIMF test data tree not found."); return; }
        string fixture = Path.Combine(root, "BSA_10ugml_CID.UIMF");
        if (!File.Exists(fixture)) { Assert.Inconclusive($"{fixture} not present."); return; }

        using var data = new UimfData(fixture);

        Assert.IsTrue(data.Index.Count > 0, "index should have at least one row");
        Assert.IsTrue(data.FrameCount > 0, "FrameCount should be > 0");
        Assert.IsTrue(data.DriftScansPerFrame > 0, "DriftScansPerFrame should be > 0");
        Assert.IsTrue(data.HasIonMobility, "BSA fixture is IMS data");

        var (low, high) = data.GetScanRange();
        Assert.IsTrue(low > 0 && high > low, $"scan range looks wrong: ({low}, {high})");

        // First scan: ensure we can fetch m/z + intensity arrays without throwing.
        var first = data.Index[0];
        var (mz, intens) = data.GetScan(first.Frame, first.Scan, first.FrameType, ignoreZeroIntensityPoints: true);
        Assert.AreEqual(mz.Length, intens.Length, "m/z and intensity arrays must be parallel");

        // TIC: every UIMF file has at least one frame.
        var (ticTime, ticIntens) = data.GetTic();
        Assert.AreEqual(ticTime.Length, ticIntens.Length);
        Assert.AreEqual(data.FrameCount, ticTime.Length, "TIC should have one entry per frame");
    }

    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "UIMF",
                "Reader_UIMF_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
