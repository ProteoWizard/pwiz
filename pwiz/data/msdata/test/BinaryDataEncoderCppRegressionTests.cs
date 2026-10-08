using Pwiz.Data.MsData.Encoding;

namespace Pwiz.Data.MsData.Tests.Encoding;

/// <summary>
/// Byte-for-byte regression against pwiz C++. Every expected string below is copied verbatim from
/// cpp's <c>BinaryDataEncoderTest.cpp</c>, and the input is cpp's own <c>sampleData_</c>, so these
/// assert that the port writes exactly the bytes msconvert writes for each configuration.
/// </summary>
/// <remarks>
/// The existing encoder tests round-trip the port against itself. That catches an encoder that
/// disagrees with its own decoder, but not one that agrees with itself while disagreeing with
/// C++ - a different zlib level or a different numpress fixed-point exponent would round-trip
/// perfectly and still produce mzML that does not match pwiz byte for byte.
/// </remarks>
[TestClass]
public class BinaryDataEncoderCppRegressionTests
{
    /// <summary>cpp's sampleData_: interleaved m/z and intensity from a real scan, plus a short
    /// synthetic tail.</summary>
    private static readonly double[] SampleData =
    {
        200.00018816645022000000, 0.00000000000000000000,
        200.00043034083151000000, 0.00000000000000000000,
        200.00067251579924000000, 0.00000000000000000000,
        200.00091469135347000000, 0.00000000000000000000,
        201.10647068550810000000, 0.00000000000000000000,
        201.10671554643099000000, 0.00000000000000000000,
        201.10696040795017000000, 0.00000000000000000000,
        201.10720527006566000000, 0.00000000000000000000,
        201.10745013277739000000, 908.68475341796875000000,
        201.10769499608537000000, 1266.26928710937500000000,
        201.10793985998967000000, 1258.11450195312500000000,
        201.10818472449023000000, 848.79339599609375000000,
        201.10842958958708000000, 0.00000000000000000000,
        201.10867445528024000000, 0.00000000000000000000,
        201.10891932156963000000, 0.0000000000000000000,
        200, 0,
        300, 1,
        400, 10,
        500, 100,
        600, 1000,
    };

    private static readonly (string Label, BinaryEncoderConfig Config, string Expected)[] Cases =
    {
        ("Bits32 LittleEndian", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits32,
                ByteOrder = BinaryByteOrder.LittleEndian,
                Compression = BinaryCompression.None,
                Numpress = BinaryNumpress.None,
            },
            "DABIQwAAAAAcAEhDAAAAACwASEMAAAAAPABIQwAAAABCG0lDAAAAAFIbSUMAAAAAYhtJQwAAAAByG0lDAAAAAIIbSUPTK2NEkhtJQ55InkSiG0lDqkOdRLIbSUPHMlREwhtJQwAAAADSG0lDAAAAAOIbSUMAAAAAAABIQwAAAAAAAJZDAACAPwAAyEMAACBBAAD6QwAAyEIAABZEAAB6RA=="),

        ("Bits32 BigEndian", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits32,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.None,
                Numpress = BinaryNumpress.None,
            },
            "Q0gADAAAAABDSAAcAAAAAENIACwAAAAAQ0gAPAAAAABDSRtCAAAAAENJG1IAAAAAQ0kbYgAAAABDSRtyAAAAAENJG4JEYyvTQ0kbkkSeSJ5DSRuiRJ1DqkNJG7JEVDLHQ0kbwgAAAABDSRvSAAAAAENJG+IAAAAAQ0gAAAAAAABDlgAAP4AAAEPIAABBIAAAQ/oAAELIAABEFgAARHoAAA=="),

        ("Bits64 LittleEndian", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.LittleEndian,
                Compression = BinaryCompression.None,
                Numpress = BinaryNumpress.None,
            },
            "/xedigEAaUAAAAAAAAAAAIV5fYYDAGlAAAAAAAAAAACkK16CBQBpQAAAAAAAAAAAXy4/fgcAaUAAAAAAAAAAAK4HNjVoI2lAAAAAAAAAAACrvLg2aiNpQAAAAAAAAAAAnMM7OGwjaUAAAAAAAAAAAIIcvzluI2lAAAAAAAAAAABax0I7cCNpQAAAAGB6ZYxAJcTGPHIjaUAAAADAE8mTQOUSSz50I2lAAAAAQHWok0CYs88/diNpQAAAAOBYhopAP6ZUQXgjaUAAAAAAAAAAANvq2UJ6I2lAAAAAAAAAAABpgV9EfCNpQAAAAAAAAAAAAAAAAAAAaUAAAAAAAAAAAAAAAAAAwHJAAAAAAAAA8D8AAAAAAAB5QAAAAAAAACRAAAAAAABAf0AAAAAAAABZQAAAAAAAwIJAAAAAAABAj0A="),

        ("Bits64 BigEndian", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.None,
                Numpress = BinaryNumpress.None,
            },
            "QGkAAYqdF/8AAAAAAAAAAEBpAAOGfXmFAAAAAAAAAABAaQAFgl4rpAAAAAAAAAAAQGkAB34/Ll8AAAAAAAAAAEBpI2g1NgeuAAAAAAAAAABAaSNqNri8qwAAAAAAAAAAQGkjbDg7w5wAAAAAAAAAAEBpI245vxyCAAAAAAAAAABAaSNwO0LHWkCMZXpgAAAAQGkjcjzGxCVAk8kTwAAAAEBpI3Q+SxLlQJOodUAAAABAaSN2P8+zmECKhljgAAAAQGkjeEFUpj8AAAAAAAAAAEBpI3pC2erbAAAAAAAAAABAaSN8RF+BaQAAAAAAAAAAQGkAAAAAAAAAAAAAAAAAAEBywAAAAAAAP/AAAAAAAABAeQAAAAAAAEAkAAAAAAAAQH9AAAAAAABAWQAAAAAAAECCwAAAAAAAQI9AAAAAAAA="),

        ("Bits32 LittleEndian Zlib", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits32,
                ByteOrder = BinaryByteOrder.LittleEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = BinaryNumpress.None,
            },
            "eJzjYfBwZgACGSitA6VtoLSTtCeYDoLSSVC6CEo3AenL2skuk4D0PI95LouA9CrnuS6bgPRxoxCXQ1B1l6D0IyjNADWfgWEakG6wZ2A4AaQVHBkYfgHpE04MDGIuDAxVLgB7LB3q"),

        ("Bits32 BigEndian Zlib", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits32,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = BinaryNumpress.None,
            },
            "eJxz9mDgYQACZw8GGSitA6VtwLSntBOUDoLSSVC6CEo3uSRrXwbSk1zmecwD0otc5jqvAtKbXEKMjgPpQ1B1l6D0I6j5YOA8jYHBvgFIn2BgcFQA0r8YGJyAbBcxIK5iYAAAfLkd6g=="),

        ("Bits64 LittleEndian Zlib", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.LittleEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = BinaryNumpress.None,
            },
            "eJz7Lz63i5Eh04EBClora9uYkfhLtOOaWJH48Xr2dexI/HXsZqYZygj+6j07zLKQ+HMOW1vkIPGbZPZb5iHxo447WRdA+AlVqT0OqkeO2RRB+AeET052eCrkbVcC4TuUrpjsMGPzefsyCP9BRFuXg/2yEMcKJPNuv7rpVIXEz2yMd6lB4kNFUfkHiqD8D/YQuhLKV4HQDvVQfiSEPtAEFe93AACmsz1+"),

        ("Bits64 BigEndian Zlib", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = BinaryNumpress.None,
            },
            "eJxzyGRg7Jor/p8BChwyGZjbaitbkfisTXHaS5D47HX2evEIvnKGqRn7OiR+ltmOPauR+DkW1ofnIPHzLPfLNCHxC6ydjkc59KRWJUD4RTbHjqg6TD4pfADCL7HzFnrqMHlFqQOEX2Z/fvMMh662iAcQfoVjyDJ7JPOqnG6+uo3Er3GJb8xEcj8KcCg6AKbtP0D5lVBaBUrXO0DoSCi/CaLeoR8iDgC0Qj1+"),

        ("Bits64 BigEndian numpress Linear", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.None,
                Numpress = BinaryNumpress.Linear,
                // cpp sets both tolerances to 0 for these regressions - 0 means do not guarantee
                // anything. At the shared 2e-9 default the linear fit exceeds it and both
                // implementations fall back to plain encoding.
                NumpressLinearErrorTolerance = 0,
                NumpressSlofErrorTolerance = 0,
            },
            "QS69PAAAAAAu7AEMAAAAAA9J0wgQ61LPfgY70wgQbTLPfg4d0wgQ7hLPfgMM1BgQwGKtfgvq1SgQ4UKtfgjc1SgQIyKtfgXO1SgQRAKtfgKw5SgQ78OG4QNVqQugf3Tmpg+6yRCARe2G9wiYdBGAecaFZgs+qjKwizv8oQVa5SgQS0GtfgJM5SgQjCGtfgwC5BgQApLPfgicxA4Q5MmQzQzK9+kgoDYaDQAvNdQwS+AZrAhzqAY5hKD/kA=="),

        ("Bits64 BigEndian Zlib numpress Linear", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = BinaryNumpress.Linear,
                // cpp sets both tolerances to 0 for these regressions - 0 means do not guarantee
                // anything. At the shared 2e-9 default the linear fit exceeds it and both
                // implementations fall back to plain encoding.
                NumpressLinearErrorTolerance = 0,
                NumpressSlofErrorTolerance = 0,
            },
            "eJxz1NtrwwAEem8YeUA0v+dlDoHXQefr2KyBjFyj83V8skDGO6Hzdcw8VyQEDiStreN+dVVD4KHT2jqOO0CGstLaOtZzQIYL09o6pg1PNQTeH257yBy6kntBfcmzZfy7Tgo0uL5t+84xo0SwofJYaxq33SqjDd3WfxayRgEVezsCdfkAGT2Ka+t4mJ5ICDBNOl/HMecIn8CTkxPO8pz6/lJhgZkUL4O+6RUD7weSaziKV7BZtiz4PwEAkp1KXg=="),

        ("Bits64 BigEndian numpress Slof", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.None,
                Numpress = BinaryNumpress.Slof,
                // cpp sets both tolerances to 0 for these regressions - 0 means do not guarantee
                // anything. At the shared 2e-9 default the linear fit exceeds it and both
                // implementations fall back to plain encoding.
                NumpressLinearErrorTolerance = 0,
                NumpressSlofErrorTolerance = 0,
            },
            "QMHqAAAAAAACvgAAAr4AAAK+AAACvgAANL4AADS+AAA0vgAANL4AADS+GvQ0vvr/NL6//zS+qfE0vgAANL4AADS+AAACvgAAeszWGMHW6VW73lqlQOWH9w=="),

        ("Bits64 BigEndian Zlib numpress Slof", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = BinaryNumpress.Slof,
                // cpp sets both tolerances to 0 for these regressions - 0 means do not guarantee
                // anything. At the shared 2e-9 default the linear fit exceeds it and both
                // implementations fall back to plain encoding.
                NumpressLinearErrorTolerance = 0,
                NumpressSlofErrorTolerance = 0,
            },
            "eJxzOPiKAQSY9qFiEwws9cVk36//Jvv2A/HKj8hyIPVVZ65JHLz2MnT3vailDk/bvwMAn1ogtQ=="),

        ("Bits64 BigEndian numpress Pic", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.None,
                Numpress = BinaryNumpress.Pic,
                // cpp sets both tolerances to 0 for these regressions - 0 means do not guarantee
                // anything. At the shared 2e-9 default the linear fit exceeds it and both
                // implementations fall back to plain encoding.
                NumpressLinearErrorTolerance = 0,
                NumpressSlofErrorTolerance = 0,
            },
            "aMhoyGjIaMhpyGnIachpyGnF2DacUvRpxa5GnFFTachpyGnIaMhcIXFQkXpU8WRlhSWOMA=="),

        ("Bits64 BigEndian Zlib numpress Pic", new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = BinaryNumpress.Pic,
                // cpp sets both tolerances to 0 for these regressions - 0 means do not guarantee
                // anything. At the shared 2e-9 default the linear fit exceeds it and both
                // implementations fall back to plain encoding.
                NumpressLinearErrorTolerance = 0,
                NumpressSlofErrorTolerance = 0,
            },
            "eJzLOJEBhpkwePSG2ZygL5lH17nNCQyGiGWciFEsDJhYFfIxJbVVtc8AAAjsG4c="),

    };

    /// <summary>
    /// Uncompressed and numpress output is fully determined by the format, so it has to match
    /// pwiz C++ byte for byte.
    /// </summary>
    [TestMethod]
    public void Encode_Uncompressed_MatchesCppByteForByte()
    {
        var mismatches = new List<string>();
        foreach (var (label, config, expected) in Cases)
        {
            if (config.Compression != BinaryCompression.None) continue;
            string actual = new BinaryDataEncoder(config).Encode(SampleData);
            if (actual != expected)
                mismatches.Add($"  {label}{Environment.NewLine}    cpp:  {expected}{Environment.NewLine}    port: {actual}");
        }

        Assert.AreEqual(0, mismatches.Count,
            $"{mismatches.Count} uncompressed configurations do not match pwiz C++:" +
            Environment.NewLine + string.Join(Environment.NewLine, mismatches));
    }

    /// <summary>
    /// For the zlib configurations the DATA has to match pwiz C++ exactly; the compressed stream
    /// carrying it does not have to be the same bytes.
    /// </summary>
    /// <remarks>
    /// .NET's deflate is not stock zlib and occasionally makes different, equally valid, choices:
    /// for 64-bit big-endian this sample deflates to 173 bytes where zlib emits 171, agreeing for
    /// the first 140. Both carry the identical 320-byte payload, so every mzML reader sees the
    /// same numbers - the difference is invisible above the compression layer. The other zlib
    /// configurations here do happen to match byte for byte, which is why this is asserted on the
    /// payload rather than on the stream: matching exactly would be luck, not a property.
    /// </remarks>
    [TestMethod]
    public void Encode_Compressed_CarriesTheSamePayloadAsCpp()
    {
        var mismatches = new List<string>();
        foreach (var (label, config, expected) in Cases)
        {
            if (config.Compression == BinaryCompression.None) continue;
            string actual = new BinaryDataEncoder(config).Encode(SampleData);
            byte[] fromCpp = Inflate(Convert.FromBase64String(expected));
            byte[] fromPort = Inflate(Convert.FromBase64String(actual));
            if (!fromCpp.AsSpan().SequenceEqual(fromPort))
                mismatches.Add($"  {label}: payload {fromPort.Length} bytes vs cpp's {fromCpp.Length}");
        }

        Assert.AreEqual(0, mismatches.Count,
            $"{mismatches.Count} compressed configurations carry a different payload than pwiz C++:" +
            Environment.NewLine + string.Join(Environment.NewLine, mismatches));
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// cpp's numpress lossiness limiter, from the same test: it deliberately pushes four values
    /// out to the edge of the double range and requires the encoder to REFUSE numpress rather
    /// than silently lose precision, falling back to plain 64-bit big-endian + zlib.
    /// </summary>
    [TestMethod]
    public void Numpress_RefusesToEncodeWhenErrorWouldExceedTolerance()
    {
        // cpp: binary[1] = max-.1, binary[3] = -that, binary[5] and [7] = half of each.
        var blownOut = (double[])SampleData.Clone();
        blownOut[1] = double.MaxValue - .1;
        blownOut[3] = -blownOut[1];
        blownOut[5] = .5 * blownOut[1];
        blownOut[7] = .5 * blownOut[3];

        foreach (var numpress in new[] { BinaryNumpress.Linear, BinaryNumpress.Slof, BinaryNumpress.Pic })
        {
            var config = new BinaryEncoderConfig
            {
                Precision = BinaryPrecision.Bits64,
                ByteOrder = BinaryByteOrder.BigEndian,
                Compression = BinaryCompression.Zlib,
                Numpress = numpress,
                NumpressLinearErrorTolerance = .01,
                NumpressSlofErrorTolerance = .01,
            };
            byte[] payload = Inflate(Convert.FromBase64String(new BinaryDataEncoder(config).Encode(blownOut)));
            byte[] cppPayload = Inflate(Convert.FromBase64String(SuppressedNumpressExpected));
            CollectionAssert.AreEqual(cppPayload, payload,
                $"numpress {numpress} should have been refused, leaving plain 64-bit big-endian");
        }
    }

    /// <summary>cpp's sampleEncodedModified64BigZlib_: what the encoder emits once numpress is
    /// refused for the blown-out input.</summary>
    private const string SuppressedNumpressExpected =
        "eJxzyGRg7Jor/r/+/X8wcMhkYG6rrWz9j+CzNsVpL6m/D+ez19nrxf+H85UzTM3Y1zFAAZCfZbZjz2okfo6F9eE5SPw8y/0yTUj8Amun41EOPalVCRB+kc2xI6oOk08KH4DwS+y8hZ46TF5R6gDhl9mf3zzDoast4gGEX+EYssweybwqp5uvbiPxa1ziGzMRfAYU4FB0AEzbf4DyK6G0CpSud4DQkVB+E0S9Qz9EHACREFv+";
}
