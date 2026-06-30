using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Vendor.Thermo;

namespace Pwiz.Vendor.Thermo.Tests;

/// <summary>
/// Unit tests for <see cref="Reader_Thermo"/> below the harness layer: identify-by-header,
/// identify-by-extension edge cases, and surfacing of read failures. Grouped by behavior
/// (identify / header-detection / read-errors) rather than per-input so each method exercises
/// a coherent slice of reader behavior.
/// </summary>
[TestClass]
public class Reader_ThermoTests
{
    private static readonly byte[] FinniganMagicBytes =
    {
        0x01, 0xA1,
        (byte)'F', 0, (byte)'i', 0, (byte)'n', 0, (byte)'n', 0,
        (byte)'i', 0, (byte)'g', 0, (byte)'a', 0, (byte)'n', 0,
    };

    [TestMethod]
    public void Identify_DispatchesByHeaderAndExtension()
    {
        var r = new Reader_Thermo();

        // Magic bytes win regardless of extension/path.
        byte[] bytesWithTrailer = FinniganMagicBytes.Concat(new byte[] { 0x42, 0x42, 0x42 }).ToArray();
        string head = System.Text.Encoding.Latin1.GetString(bytesWithTrailer);
        Assert.AreEqual(CVID.MS_Thermo_RAW_format, r.Identify("irrelevant.bin", head),
            "magic bytes should be sufficient to claim the file");

        // No magic, no clue: don't claim.
        Assert.AreEqual(CVID.CVID_Unknown, r.Identify("foo.txt", "hello world"),
            "non-Thermo content shouldn't be claimed");

        // .raw extension alone is not enough — file must exist and not be a directory.
        Assert.AreEqual(CVID.CVID_Unknown, r.Identify("/does/not/exist.raw", null),
            "non-existent .raw path shouldn't be claimed");

        string dir = Path.Combine(Path.GetTempPath(), "waters-looking-" + Guid.NewGuid().ToString("N")[..8] + ".raw");
        Directory.CreateDirectory(dir);
        try
        {
            // Waters .raw files are directories — must not be claimed.
            Assert.AreEqual(CVID.CVID_Unknown, r.Identify(dir, null),
                ".raw directories shouldn't be claimed");
        }
        finally { Directory.Delete(dir); }
    }

    [TestMethod]
    public void HasThermoHeader_ByteSpan_DetectsMagicCorrectly()
    {
        // Positive: full Finnigan magic.
        Assert.IsTrue(Reader_Thermo.HasThermoHeader(FinniganMagicBytes));

        // Wrong leading byte → false.
        var corrupted = (byte[])FinniganMagicBytes.Clone();
        corrupted[0] = 0xFF;
        Assert.IsFalse(Reader_Thermo.HasThermoHeader(corrupted));

        // Too-short input → false (don't read past end of span).
        Assert.IsFalse(Reader_Thermo.HasThermoHeader(new byte[] { 0x01, 0xA1 }));
    }

    [TestMethod]
    public void Read_SurfacesErrors_AndReaderInstantiates()
    {
        // Read should throw, not return empty MSData, when the file doesn't exist.
        var msd = new MSData();
        Assert.ThrowsException<FileNotFoundException>(
            () => new Reader_Thermo().Read("/does/not/exist.raw", msd));

        // Sanity: JITting Reader_Thermo forces resolution of all Thermo SDK types. If the
        // extraction step skipped or hint paths are wrong, this throws TypeLoadException /
        // FileNotFoundException at construction.
        _ = new Reader_Thermo();
    }
}
