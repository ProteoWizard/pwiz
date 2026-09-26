using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Data.MsData.Tests.Encoding;

/// <summary>
/// Round-trip and byte-shape tests for <see cref="BinaryDataEncoder"/>. Tests are grouped
/// by encoding pathway (uncompressed vs compressed vs numpress vs error/overload surface)
/// rather than per-input — each method exercises several inputs that share the pathway.
/// </summary>
[TestClass]
public class BinaryDataEncoderTests
{
    [TestMethod]
    public void DoubleEncoding_64bitAnd32bit_RoundTripsAtAppropriatePrecision()
    {
        // 64-bit: bit-exact round-trip across mixed magnitudes, empty array, large array.
        var enc64 = new BinaryDataEncoder();
        double[] mixed = { 1.0, 2.5, -3.14159, 1e-20, 1e20 };
        CollectionAssert.AreEqual(mixed, enc64.DecodeDoubles(enc64.Encode(mixed)));
        Assert.AreEqual(0, enc64.DecodeDoubles(enc64.Encode(Array.Empty<double>())).Length);
        double[] large = Enumerable.Range(0, 10_000).Select(i => i * 0.123456789).ToArray();
        CollectionAssert.AreEqual(large, enc64.DecodeDoubles(enc64.Encode(large)));

        // 32-bit: round-trip at float precision (1e-5 absolute).
        var enc32 = new BinaryDataEncoder(new BinaryEncoderConfig { Precision = BinaryPrecision.Bits32 });
        double[] input = { 1.0, 2.5, 3.14159f, 1000.0 };
        double[] output = enc32.DecodeDoubles(enc32.Encode(input));
        Assert.AreEqual(input.Length, output.Length);
        for (int i = 0; i < input.Length; i++)
            Assert.AreEqual(input[i], output[i], 1e-5, $"index {i}");
    }

    [TestMethod]
    public void DoubleEncoding_Zlib_RoundTripsAndCompressesRepetitiveData()
    {
        var zipped = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.Zlib });

        // Round-trip a 1k-element ramp.
        double[] ramp = Enumerable.Range(0, 1000).Select(i => (double)i).ToArray();
        CollectionAssert.AreEqual(ramp, zipped.DecodeDoubles(zipped.Encode(ramp)));

        // Repetitive data (1000 copies of 42.0) compresses smaller than uncompressed.
        double[] repetitive = new double[1000];
        Array.Fill(repetitive, 42.0);
        var plain = new BinaryDataEncoder();
        plain.Encode(repetitive, out int plainBytes);
        zipped.Encode(repetitive, out int zippedBytes);
        Assert.IsTrue(zippedBytes < plainBytes,
            $"zlib bytes ({zippedBytes}) should be smaller than uncompressed ({plainBytes})");
    }

    [TestMethod]
    public void IntegerEncoding_RoundTripsUncompressedAndZlib()
    {
        // 64-bit integers: full int64 range round-trips uncompressed.
        var plain = new BinaryDataEncoder();
        long[] mixedInts = { 0, 1, -1, long.MaxValue, long.MinValue, 42 };
        CollectionAssert.AreEqual(mixedInts, plain.DecodeInt64(plain.EncodeInt64(mixedInts)));

        // Same path under zlib.
        var zipped = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.Zlib });
        long[] near = { 1_000_000, 1_000_001, 1_000_002, 1_000_003 };
        CollectionAssert.AreEqual(near, zipped.DecodeInt64(zipped.EncodeInt64(near)));
    }

    [TestMethod]
    public void ByteShape_EndianessAndByteStableEncoding()
    {
        // Big-endian wire bytes differ from little-endian for the same input, but BE round-trips
        // through its own decoder.
        var le = new BinaryDataEncoder();
        var be = new BinaryDataEncoder(new BinaryEncoderConfig { ByteOrder = BinaryByteOrder.BigEndian });
        double[] input = { 1.0, 2.0 };
        Assert.AreNotEqual(le.Encode(input), be.Encode(input));
        CollectionAssert.AreEqual(input, be.DecodeDoubles(be.Encode(input)));

        // Byte-stable check: 1.0 (double, little-endian) = 00 00 00 00 00 00 F0 3F → base64
        // "AAAAAAAA8D8=". This is the canonical mzML wire encoding; if it ever changes, every
        // mzML reference fixture in the world will diverge from us.
        Assert.AreEqual("AAAAAAAA8D8=", le.Encode(new[] { 1.0 }), "encode 1.0");
        CollectionAssert.AreEqual(new[] { 1.0 }, le.DecodeDoubles("AAAAAAAA8D8="));
    }

    [TestMethod]
    public void DecodeOverloadsAndErrorPaths()
    {
        var enc = new BinaryDataEncoder();

        // Span overload of DecodeDoubles works the same as the string overload.
        string text = enc.Encode(new[] { 1.0, 2.0 });
        CollectionAssert.AreEqual(new[] { 1.0, 2.0 }, enc.DecodeDoubles(text.AsSpan()));

        // Malformed input (3 bytes after base64 decode is not a multiple of 8) → InvalidDataException.
        Assert.ThrowsException<InvalidDataException>(() => enc.DecodeDoubles("AAAB"));
    }

    [TestMethod]
    public void Numpress_Linear_RoundTripsAndChainsWithZlib()
    {
        // Linear: lossy compression for monotonic m/z arrays at ~5e-6 precision.
        var linear = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Linear });

        double[] mz = { 100.1234, 200.5678, 300.9012, 401.2345, 500.5678 };
        double[] decodedMz = linear.DecodeDoubles(linear.Encode(mz));
        Assert.AreEqual(mz.Length, decodedMz.Length);
        for (int i = 0; i < mz.Length; i++)
            Assert.AreEqual(mz[i], decodedMz[i], 5e-6, $"mz[{i}]");

        // Edge cases: empty + single element.
        Assert.AreEqual(0, linear.DecodeDoubles(linear.Encode(Array.Empty<double>())).Length);
        var single = linear.DecodeDoubles(linear.Encode(new[] { 123.456 }));
        Assert.AreEqual(1, single.Length);
        Assert.AreEqual(123.456, single[0], 5e-6);

        // Numpress Linear chained with zlib (typical config for compressed mzML).
        var chained = new BinaryDataEncoder(new BinaryEncoderConfig
        {
            Numpress = BinaryNumpress.Linear,
            Compression = BinaryCompression.Zlib,
        });
        double[] decodedChained = chained.DecodeDoubles(chained.Encode(mz));
        for (int i = 0; i < mz.Length; i++)
            Assert.AreEqual(mz[i], decodedChained[i], 5e-6, $"linear+zlib mz[{i}]");
    }

    [TestMethod]
    public void Numpress_PicAndSlof_RoundTripsAtAppropriatePrecision()
    {
        // Pic rounds to nearest int on encode, so exact equality holds for integer inputs.
        var pic = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Pic });
        double[] picData = { 0, 15, 123, 9999, 42, 0, 1, 1_000_000 };
        CollectionAssert.AreEqual(picData, pic.DecodeDoubles(pic.Encode(picData)));

        // Slof is lossy in log space — ~0.04% relative precision is the documented target.
        var slof = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Slof });
        double[] slofData = { 0.0, 100.0, 10_000.0, 1_000_000.0, 42.5 };
        double[] slofDecoded = slof.DecodeDoubles(slof.Encode(slofData));
        Assert.AreEqual(slofData.Length, slofDecoded.Length);
        for (int i = 0; i < slofData.Length; i++)
            Assert.AreEqual(slofData[i], slofDecoded[i], Math.Max(1e-6, slofData[i] * 5e-4), $"slof[{i}]");
    }
}
