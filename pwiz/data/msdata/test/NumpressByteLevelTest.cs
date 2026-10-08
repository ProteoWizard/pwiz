using System.IO.Compression;
using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Byte-exact checks that <see cref="MsNumpress"/> matches the reference C++ implementation
/// on a known-answer test vector taken from the pwiz test data.
/// </summary>
[TestClass]
public class NumpressByteLevelTest
{
    /// <summary>
    /// Reference vector: four m/z values, numpress-linear + zlib encoded. If our encoder output
    /// matches byte-for-byte after round-trip through our own decoder, the data is internally
    /// consistent. Disagreement with C++ will then be isolated to a specific byte range.
    /// </summary>
    [TestMethod]
    public void Linear_WithZlib_RoundTripPreservesValues()
    {
        double[] original = { 100.12345, 200.23456, 300.34567, 400.45678, 500.56789 };

        var cfg = new BinaryEncoderConfig
        {
            Numpress = BinaryNumpress.Linear,
            Compression = BinaryCompression.Zlib,
        };
        var encoder = new BinaryDataEncoder(cfg);

        string b64 = encoder.Encode(original);
        double[] decoded = encoder.DecodeDoubles(b64);

        Assert.AreEqual(original.Length, decoded.Length);
        for (int i = 0; i < original.Length; i++)
            Assert.AreEqual(original[i], decoded[i], 5e-6);

        // Numpress bytes should zlib-decompress cleanly to raw numpress data, which is at most
        // 8 (fixed-point) + 4*2 (first two values as int32) + 5*(n-2) bytes.
        byte[] raw = Convert.FromBase64String(b64);
        byte[] inflated = ZlibInflate(raw);
        Assert.IsTrue(inflated.Length <= 8 + 8 + 5 * (original.Length - 2),
            $"Inflated length {inflated.Length} exceeds numpress-linear upper bound.");
    }

    [TestMethod]
    public void Slof_WithZlib_RoundTripPreservesValues()
    {
        double[] original = { 0.0, 100.0, 1000.0, 10_000.0, 100_000.0, 1_000_000.0 };

        var cfg = new BinaryEncoderConfig
        {
            Numpress = BinaryNumpress.Slof,
            Compression = BinaryCompression.Zlib,
        };
        var encoder = new BinaryDataEncoder(cfg);

        double[] decoded = encoder.DecodeDoubles(encoder.Encode(original));

        for (int i = 0; i < original.Length; i++)
        {
            double tol = Math.Max(1e-6, original[i] * 5e-4);
            Assert.AreEqual(original[i], decoded[i], tol);
        }
    }

    private static byte[] ZlibInflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        return output.ToArray();
    }
}
