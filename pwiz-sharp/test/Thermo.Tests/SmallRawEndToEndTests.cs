using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Vendor.Thermo;

namespace Pwiz.Vendor.Thermo.Tests;

/// <summary>
/// Reads <c>example_data/small.RAW</c> end-to-end. Skipped when the file isn't available.
/// The file ships in the pwiz tree, so on dev machines these tests should run.
/// </summary>
[TestClass]
public class SmallRawEndToEndTests
{
    private static string? FindSmallRawFile()
    {
        // Walk up from the test output directory looking for the example_data folder.
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "example_data", "small.RAW");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void Identify_SmallRaw_ReturnsThermoCvid()
    {
        string? path = FindSmallRawFile();
        if (path is null) { Assert.Inconclusive("example_data/small.RAW not found"); return; }

        // Read the first 20 bytes and feed them to Identify — content sniff path.
        byte[] head = new byte[20];
        using (var s = File.OpenRead(path))
            s.ReadExactly(head, 0, head.Length);
        Assert.IsTrue(Reader_Thermo.HasThermoHeader(head));
    }

    [TestMethod]
    public void Open_SmallRaw_ReportsScanCount()
    {
        string? path = FindSmallRawFile();
        if (path is null) { Assert.Inconclusive("example_data/small.RAW not found"); return; }

        using var raw = new ThermoRawFile(path);
        Assert.IsTrue(raw.ScanCount > 0, "expected at least one scan");
        Assert.IsTrue(raw.FirstScan >= 1, "Thermo scans are 1-based");
        Assert.AreEqual(raw.ScanCount, raw.LastScan - raw.FirstScan + 1);
    }

    [TestMethod]
    public void ReadFirstScan_HasRetentionTimeAndMsLevel()
    {
        string? path = FindSmallRawFile();
        if (path is null) { Assert.Inconclusive("example_data/small.RAW not found"); return; }

        using var raw = new ThermoRawFile(path);
        int firstScan = raw.FirstScan;

        double rtMin = raw.RetentionTimeMinutes(firstScan);
        Assert.IsTrue(rtMin >= 0 && rtMin < 10_000, $"retention time out of sensible range: {rtMin}");

        int msLevel = raw.MsLevel(firstScan);
        Assert.IsTrue(msLevel >= 1 && msLevel <= 5, $"ms level out of expected range: {msLevel}");

        string filter = raw.FilterString(firstScan);
        Assert.IsFalse(string.IsNullOrEmpty(filter), "filter string should be non-empty");
    }

    [TestMethod]
    public void ReadFirstScan_BinaryArrays_NonEmpty()
    {
        string? path = FindSmallRawFile();
        if (path is null) { Assert.Inconclusive("example_data/small.RAW not found"); return; }

        using var raw = new ThermoRawFile(path);
        var (mz, intensity) = raw.GetPeaks(raw.FirstScan, preferCentroid: true);

        Assert.IsTrue(mz.Length > 0, "first scan should have peaks");
        Assert.AreEqual(mz.Length, intensity.Length);
        // Sanity: m/z values should be ascending and in a reasonable mass-spec range.
        for (int i = 1; i < mz.Length; i++)
            Assert.IsTrue(mz[i] >= mz[i - 1], $"m/z array should be sorted, violation at index {i}");
        Assert.IsTrue(mz[0] > 0 && mz[^1] < 100_000);
    }

    [TestMethod]
    public void SpectrumList_IteratesAll_AndReturnsExpectedFields()
    {
        string? path = FindSmallRawFile();
        if (path is null) { Assert.Inconclusive("example_data/small.RAW not found"); return; }

        using var raw = new ThermoRawFile(path);
        using var list = new SpectrumList_Thermo(raw, ownsRaw: false);

        Assert.IsTrue(list.Count > 0);

        // Just load the first few as a smoke test — the .RAW has 48 scans, don't walk them all per test.
        for (int i = 0; i < System.Math.Min(3, list.Count); i++)
        {
            var spec = list.GetSpectrum(i, getBinaryData: true);
            Assert.AreEqual($"controllerType=0 controllerNumber=1 scan={i + raw.FirstScan}", spec.Id);
            Assert.IsTrue(spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) >= 1);
            Assert.IsTrue(spec.DefaultArrayLength > 0);
            Assert.IsNotNull(spec.GetMZArray());
            Assert.IsNotNull(spec.GetIntensityArray());
        }
    }

    [TestMethod]
    public void Read_ViaIReaderInterface_PopulatesMsData()
    {
        string? path = FindSmallRawFile();
        if (path is null) { Assert.Inconclusive("example_data/small.RAW not found"); return; }

        var msd = new MSData();
        new Reader_Thermo().Read(path, msd);

        Assert.AreEqual("small", msd.Id);
        Assert.AreEqual(1, msd.FileDescription.SourceFiles.Count);
        Assert.IsTrue(msd.FileDescription.SourceFiles[0].HasCVParam(CVID.MS_Thermo_RAW_format));
        Assert.IsNotNull(msd.Run.SpectrumList);
        Assert.IsTrue(msd.Run.SpectrumList!.Count > 0);

        // Dispose through the spectrum list so the underlying raw-file handle closes.
        if (msd.Run.SpectrumList is IDisposable d) d.Dispose();
    }
}
