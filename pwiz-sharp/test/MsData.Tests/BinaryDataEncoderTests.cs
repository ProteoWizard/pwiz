using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Data.MsData.Tests.Encoding;

[TestClass]
public class BinaryDataEncoderTests
{
    // ---- 64-bit precision, no compression ----

    [TestMethod]
    public void Encode64_NoCompression_RoundTrips()
    {
        var enc = new BinaryDataEncoder();
        double[] input = { 1.0, 2.5, -3.14159, 1e-20, 1e20 };
        string text = enc.Encode(input);
        double[] output = enc.DecodeDoubles(text);
        CollectionAssert.AreEqual(input, output);
    }

    [TestMethod]
    public void Encode64_EmptyArray_RoundTrips()
    {
        var enc = new BinaryDataEncoder();
        Assert.AreEqual(0, enc.DecodeDoubles(enc.Encode(Array.Empty<double>())).Length);
    }

    [TestMethod]
    public void Encode64_LargeArray_RoundTrips()
    {
        var enc = new BinaryDataEncoder();
        double[] input = Enumerable.Range(0, 10_000).Select(i => i * 0.123456789).ToArray();
        double[] output = enc.DecodeDoubles(enc.Encode(input));
        CollectionAssert.AreEqual(input, output);
    }

    // ---- 32-bit precision (lossy round-trip) ----

    [TestMethod]
    public void Encode32_NoCompression_RoundTripsWithFloatPrecision()
    {
        var config = new BinaryEncoderConfig { Precision = BinaryPrecision.Bits32 };
        var enc = new BinaryDataEncoder(config);

        double[] input = { 1.0, 2.5, 3.14159f, 1000.0 };
        double[] output = enc.DecodeDoubles(enc.Encode(input));

        Assert.AreEqual(input.Length, output.Length);
        for (int i = 0; i < input.Length; i++)
            Assert.AreEqual(input[i], output[i], 1e-5, $"index {i}");
    }

    // ---- 64-bit with zlib compression ----

    [TestMethod]
    public void Encode64_Zlib_RoundTrips()
    {
        var config = new BinaryEncoderConfig { Compression = BinaryCompression.Zlib };
        var enc = new BinaryDataEncoder(config);

        double[] input = Enumerable.Range(0, 1000).Select(i => (double)i).ToArray();
        string text = enc.Encode(input);
        double[] output = enc.DecodeDoubles(text);
        CollectionAssert.AreEqual(input, output);
    }

    [TestMethod]
    public void Encode64_Zlib_SmallerThanUncompressed_ForRepetitiveData()
    {
        // Repetitive data should compress well.
        double[] input = new double[1000];
        Array.Fill(input, 42.0);

        var plain = new BinaryDataEncoder();
        var zipped = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.Zlib });

        string plainText = plain.Encode(input, out int plainBytes);
        string zippedText = zipped.Encode(input, out int zippedBytes);

        Assert.IsTrue(zippedBytes < plainBytes,
            $"zlib-compressed bytes ({zippedBytes}) should be smaller than uncompressed ({plainBytes})");
        _ = plainText;
        _ = zippedText;
    }

    // ---- Int64 ----

    [TestMethod]
    public void EncodeInt64_NoCompression_RoundTrips()
    {
        var enc = new BinaryDataEncoder();
        long[] input = { 0, 1, -1, long.MaxValue, long.MinValue, 42 };
        long[] output = enc.DecodeInt64(enc.EncodeInt64(input));
        CollectionAssert.AreEqual(input, output);
    }

    [TestMethod]
    public void EncodeInt64_Zlib_RoundTrips()
    {
        var enc = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.Zlib });
        long[] input = { 1_000_000, 1_000_001, 1_000_002, 1_000_003 };
        long[] output = enc.DecodeInt64(enc.EncodeInt64(input));
        CollectionAssert.AreEqual(input, output);
    }

    // ---- Endianness ----

    [TestMethod]
    public void BigEndian_RoundTrips_AndDiffersFromLittleEndian()
    {
        var little = new BinaryDataEncoder();
        var big = new BinaryDataEncoder(new BinaryEncoderConfig { ByteOrder = BinaryByteOrder.BigEndian });

        double[] input = { 1.0, 2.0 };
        string le = little.Encode(input);
        string be = big.Encode(input);
        Assert.AreNotEqual(le, be);

        CollectionAssert.AreEqual(input, big.DecodeDoubles(be));
    }

    // ---- Stability of the wire format (not just round-trip) ----

    [TestMethod]
    public void Encode64_KnownValue_MatchesExpectedBase64()
    {
        // 1.0 (double, little-endian) = 00 00 00 00 00 00 F0 3F → base64 "AAAAAAAA8D8="
        var enc = new BinaryDataEncoder();
        string text = enc.Encode(new[] { 1.0 });
        Assert.AreEqual("AAAAAAAA8D8=", text);
    }

    [TestMethod]
    public void Decode64_KnownBase64_MatchesExpectedValue()
    {
        var enc = new BinaryDataEncoder();
        double[] output = enc.DecodeDoubles("AAAAAAAA8D8=");
        CollectionAssert.AreEqual(new[] { 1.0 }, output);
    }

    // ---- Error paths ----

    [TestMethod]
    public void DecodeDoubles_MalformedLength_Throws()
    {
        var enc = new BinaryDataEncoder();
        // 3 bytes = 1-char base64 padding variant decoded. Should fail because 3 % 8 != 0.
        Assert.ThrowsException<InvalidDataException>(() => enc.DecodeDoubles("AAAB"));
    }

    // ---- Numpress round-trips ----

    [TestMethod]
    public void Numpress_Linear_RoundTripsMzArray()
    {
        // A typical m/z array: monotonically increasing, smoothly spaced doubles.
        double[] data = { 100.1234, 200.5678, 300.9012, 401.2345, 500.5678 };
        var enc = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Linear });
        string encoded = enc.Encode(data);
        double[] decoded = enc.DecodeDoubles(encoded);

        Assert.AreEqual(data.Length, decoded.Length);
        for (int i = 0; i < data.Length; i++)
            Assert.AreEqual(data[i], decoded[i], 5e-6, $"index {i}: {data[i]} vs {decoded[i]}");
    }

    [TestMethod]
    public void Numpress_Pic_RoundTripsIonCounts()
    {
        double[] data = { 0, 15, 123, 9999, 42, 0, 1, 1_000_000 };
        var enc = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Pic });
        double[] decoded = enc.DecodeDoubles(enc.Encode(data));

        // Pic rounds to nearest int on encode, so exact equality holds for integer inputs.
        CollectionAssert.AreEqual(data, decoded);
    }

    [TestMethod]
    public void Numpress_Slof_RoundTripsIntensityArray()
    {
        double[] data = { 0.0, 100.0, 10_000.0, 1_000_000.0, 42.5 };
        var enc = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Slof });
        double[] decoded = enc.DecodeDoubles(enc.Encode(data));

        Assert.AreEqual(data.Length, decoded.Length);
        // Slof is lossy in log space — ~0.04% relative precision is the documented target.
        for (int i = 0; i < data.Length; i++)
        {
            double expected = data[i];
            double tol = Math.Max(1e-6, expected * 5e-4);
            Assert.AreEqual(expected, decoded[i], tol, $"index {i}");
        }
    }

    [TestMethod]
    public void Numpress_LinearWithZlib_RoundTrips()
    {
        double[] data = { 100.1234, 200.5678, 300.9012, 401.2345, 500.5678 };
        var enc = new BinaryDataEncoder(new BinaryEncoderConfig
        {
            Numpress = BinaryNumpress.Linear,
            Compression = BinaryCompression.Zlib,
        });
        double[] decoded = enc.DecodeDoubles(enc.Encode(data));
        for (int i = 0; i < data.Length; i++)
            Assert.AreEqual(data[i], decoded[i], 5e-6);
    }

    [TestMethod]
    public void Numpress_Linear_EmptyArray()
    {
        var enc = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Linear });
        double[] decoded = enc.DecodeDoubles(enc.Encode(Array.Empty<double>()));
        Assert.AreEqual(0, decoded.Length);
    }

    [TestMethod]
    public void Numpress_Linear_SingleValue()
    {
        double[] data = { 123.456 };
        var enc = new BinaryDataEncoder(new BinaryEncoderConfig { Numpress = BinaryNumpress.Linear });
        double[] decoded = enc.DecodeDoubles(enc.Encode(data));
        Assert.AreEqual(1, decoded.Length);
        Assert.AreEqual(123.456, decoded[0], 5e-6);
    }

    // ---- Span overloads ----

    [TestMethod]
    public void DecodeDoubles_SpanOverload_Works()
    {
        var enc = new BinaryDataEncoder();
        string text = enc.Encode(new[] { 1.0, 2.0 });
        double[] output = enc.DecodeDoubles(text.AsSpan());
        CollectionAssert.AreEqual(new[] { 1.0, 2.0 }, output);
    }
}
