using System.IO;
using Pwiz.Data.MsData.Mzml;

namespace Pwiz.Data.MsData.Tests.Mzml;

/// <summary>
/// Smoke tests for <see cref="MzmlIndexFooter"/> against the cpp-generated mzML
/// fixtures that ship with pwiz's DiaUmpire test data.
/// </summary>
[TestClass]
public class MzmlIndexFooterTests
{
    private static string? FindCppTestDataDir()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "analysis", "spectrum_processing",
                "SpectrumList_DiaUmpireTest.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    [TestMethod]
    public void TryReadSpectrumOffsets_RecognizesIndexedMzML()
    {
        string? dataDir = FindCppTestDataDir();
        if (dataDir is null) { Assert.Inconclusive("cpp test data dir not found"); return; }
        string path = Path.Combine(dataDir,
            "Hoofnagle_10xDil_SWATH_01-20130327_Hoofnagle_10xDil_SWATH_1_01-diaumpire.mzML");
        if (!File.Exists(path)) { Assert.Inconclusive($"fixture missing: {path}"); return; }

        var result = MzmlIndexFooter.TryReadSpectrumOffsets(path);
        Assert.IsNotNull(result, "Expected to find an indexList footer in the cpp-generated fixture.");
        var (ids, offsets) = result.Value;
        Assert.IsTrue(ids.Length > 0);
        Assert.AreEqual(ids.Length, offsets.Length);

        // Each offset must point into the file (and must be monotonically increasing —
        // mzML's indexList sorts spectra by index).
        long fileLen = new FileInfo(path).Length;
        for (int i = 0; i < offsets.Length; i++)
        {
            Assert.IsTrue(offsets[i] > 0 && offsets[i] < fileLen,
                $"Offset[{i}] = {offsets[i]} is out of range for file of length {fileLen}.");
            if (i > 0) Assert.IsTrue(offsets[i] > offsets[i - 1],
                $"Offset[{i}] = {offsets[i]} should be > Offset[{i - 1}] = {offsets[i - 1]}.");
            Assert.IsFalse(string.IsNullOrEmpty(ids[i]));
        }

        // Each offset should point at a "<spectrum " element — spot-check the first one.
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Position = offsets[0];
        var buf = new byte[20];
        int n = fs.Read(buf, 0, buf.Length);
        string head = System.Text.Encoding.ASCII.GetString(buf, 0, n);
        Assert.IsTrue(head.StartsWith("<spectrum ", System.StringComparison.Ordinal),
            $"Offset[0] should point at '<spectrum ' but got: '{head}'.");
    }

    [TestMethod]
    public void TryReadSpectrumOffsets_ReturnsNullForNonIndexedFile()
    {
        // Any non-mzML file works — the parser looks for "<indexListOffset>" near EOF.
        string tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
        File.WriteAllText(tmp, "not an mzML at all");
        try
        {
            var result = MzmlIndexFooter.TryReadSpectrumOffsets(tmp);
            Assert.IsNull(result);
        }
        finally { File.Delete(tmp); }
    }
}
