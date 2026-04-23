using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Vendor.Thermo;

namespace Pwiz.Vendor.Thermo.Tests;

[TestClass]
public class Reader_ThermoTests
{
    [TestMethod]
    public void Identify_NonThermoContent_ReturnsUnknown()
    {
        var r = new Reader_Thermo();
        Assert.AreEqual(CVID.CVID_Unknown, r.Identify("foo.txt", "hello world"));
    }

    [TestMethod]
    public void Identify_MagicBytes_ReturnsThermoCvid()
    {
        // 0x01 0xA1 then "Finnigan" in UTF-16 — reproduce as a Latin-1-ish string
        // (one byte per char so the span mapping matches the reader's expectation).
        byte[] bytes = new byte[]
        {
            0x01, 0xA1,
            (byte)'F', 0, (byte)'i', 0, (byte)'n', 0, (byte)'n', 0,
            (byte)'i', 0, (byte)'g', 0, (byte)'a', 0, (byte)'n', 0,
            // trailing content doesn't matter
            0x42, 0x42, 0x42,
        };
        string head = System.Text.Encoding.Latin1.GetString(bytes);

        Assert.AreEqual(CVID.MS_Thermo_RAW_format, new Reader_Thermo().Identify("irrelevant.bin", head));
    }

    [TestMethod]
    public void HasThermoHeader_ByteSpanOverload_Works()
    {
        byte[] bytes =
        {
            0x01, 0xA1,
            (byte)'F', 0, (byte)'i', 0, (byte)'n', 0, (byte)'n', 0,
            (byte)'i', 0, (byte)'g', 0, (byte)'a', 0, (byte)'n', 0,
        };
        Assert.IsTrue(Reader_Thermo.HasThermoHeader(bytes));
        bytes[0] = 0xFF;
        Assert.IsFalse(Reader_Thermo.HasThermoHeader(bytes));
    }

    [TestMethod]
    public void HasThermoHeader_ShortInput_ReturnsFalse()
    {
        Assert.IsFalse(Reader_Thermo.HasThermoHeader(new byte[] { 0x01, 0xA1 }));
    }

    [TestMethod]
    public void Identify_RawExtension_NonExistentPath_ReturnsUnknown()
    {
        // No file at that path → shouldn't claim to recognize it.
        var r = new Reader_Thermo();
        Assert.AreEqual(CVID.CVID_Unknown, r.Identify("/does/not/exist.raw", null));
    }

    [TestMethod]
    public void Identify_RawExtension_Directory_ReturnsUnknown()
    {
        // Waters .raw files are directories. Our reader should not claim them.
        string dir = Path.Combine(Path.GetTempPath(), "waters-looking-" + Guid.NewGuid().ToString("N")[..8] + ".raw");
        Directory.CreateDirectory(dir);
        try
        {
            Assert.AreEqual(CVID.CVID_Unknown, new Reader_Thermo().Identify(dir, null));
        }
        finally { Directory.Delete(dir); }
    }

    [TestMethod]
    public void Read_NonExistentFile_Throws()
    {
        // Confirm we surface a clear error instead of silently producing an empty MSData.
        var msd = new MSData();
        Assert.ThrowsException<FileNotFoundException>(
            () => new Reader_Thermo().Read("/does/not/exist.raw", msd));
    }

    [TestMethod]
    public void Assemblies_ResolveAtRuntime()
    {
        // Sanity: instantiating Reader_Thermo forces JIT to resolve the Thermo types. If the
        // extraction step skipped or the hint paths are wrong, the test will fail with a
        // TypeLoadException / FileNotFoundException rather than reaching here.
        _ = new Reader_Thermo();
        Assert.IsTrue(true);
    }
}
