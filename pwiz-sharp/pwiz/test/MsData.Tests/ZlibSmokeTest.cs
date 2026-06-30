using System.IO.Compression;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Mzml;

namespace Pwiz.Data.MsData.Tests;

[TestClass]
public class ZlibSmokeTest
{
    [TestMethod]
    public void Numpress_WriterOutput_IsZlibWrapped()
    {
        var msd = new MSData { Id = "smoke" };
        msd.CVs.AddRange(MSData.DefaultCVList);

        var list = new Spectra.SpectrumListSimple();
        var spec = new Spectra.Spectrum { Index = 0, Id = "s1", DefaultArrayLength = 4 };
        spec.Params.Set(CVID.MS_ms_level, 1);
        spec.SetMZIntensityArrays(
            new[] { 100.0, 200.0, 300.0, 400.0 },
            new[] { 50.0, 100.0, 150.0, 200.0 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(spec);
        msd.Run.SpectrumList = list;

        var cfg = new BinaryEncoderConfig();
        cfg.NumpressOverrides[CVID.MS_m_z_array] = BinaryNumpress.Linear;
        cfg.CompressionOverrides[CVID.MS_m_z_array] = BinaryCompression.Zlib;

        string xml = new MzmlWriter(cfg).Write(msd);

        // Grab the first <binary>...</binary> (the m/z array) and verify it zlib-decompresses.
        int start = xml.IndexOf("<binary>", StringComparison.Ordinal) + "<binary>".Length;
        int end = xml.IndexOf("</binary>", start, StringComparison.Ordinal);
        string b64 = xml[start..end];
        byte[] bytes = Convert.FromBase64String(b64);

        // zlib stream starts with 0x78 <flag>. First byte must be 0x78 for any zlib-wrapped payload.
        Assert.AreEqual(0x78, bytes[0], $"Expected zlib header 0x78 as first byte, got 0x{bytes[0]:X2}");

        using var input = new MemoryStream(bytes);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        byte[] inflated = output.ToArray();

        // Inflated bytes are numpress-linear data; first 8 are the fixed-point double.
        Assert.IsTrue(inflated.Length >= 8);
    }
}
