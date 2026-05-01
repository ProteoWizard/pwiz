using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.MsData.Encoding;

/// <summary>Precision of a binary data array on disk.</summary>
public enum BinaryPrecision
{
    /// <summary>32-bit IEEE-754 float per value.</summary>
    Bits32,
    /// <summary>64-bit IEEE-754 double per value.</summary>
    Bits64,
}

/// <summary>Byte-order choice. mzML requires little-endian; big-endian is here for completeness.</summary>
public enum BinaryByteOrder
{
    /// <summary>Little-endian (x86/ARM/etc.). mzML default.</summary>
    LittleEndian,
    /// <summary>Big-endian.</summary>
    BigEndian,
}

/// <summary>Compression applied to the raw binary bytes before base64.</summary>
public enum BinaryCompression
{
    /// <summary>No compression — raw bytes go straight into base64.</summary>
    None,
    /// <summary>zlib format (deflate + zlib header + adler32). mzML's <c>MS:1000574</c>.</summary>
    Zlib,
}

/// <summary>Lossy "numpress" numerical compressions. See <see cref="MsNumpress"/> for algorithm details.</summary>
public enum BinaryNumpress
{
    /// <summary>No numpress; use normal <see cref="BinaryPrecision"/> + <see cref="BinaryCompression"/>.</summary>
    None,
    /// <summary>Numpress linear (MS:1002312): fixed-point + linear-prediction residuals. For m/z / retention time.</summary>
    Linear,
    /// <summary>Numpress positive-integer (MS:1002313): nearest-integer with variable-width encoding. For ion counts.</summary>
    Pic,
    /// <summary>Numpress short-logged-float (MS:1002314): 16-bit log-scaled fixed-point. For ion counts with wide range.</summary>
    Slof,
}

/// <summary>Configuration for <see cref="BinaryDataEncoder"/>.</summary>
public sealed class BinaryEncoderConfig
{
    /// <summary>Precision. Defaults to 64-bit.</summary>
    public BinaryPrecision Precision { get; set; } = BinaryPrecision.Bits64;

    /// <summary>Byte order. Defaults to little-endian (mzML requirement).</summary>
    public BinaryByteOrder ByteOrder { get; set; } = BinaryByteOrder.LittleEndian;

    /// <summary>Compression algorithm. Defaults to none.</summary>
    public BinaryCompression Compression { get; set; } = BinaryCompression.None;

    /// <summary>Numpress algorithm. Defaults to none.</summary>
    public BinaryNumpress Numpress { get; set; } = BinaryNumpress.None;

    /// <summary>
    /// Fixed-point multiplier for numpress Linear/Slof. 0 means "choose automatically via
    /// <see cref="MsNumpress.OptimalLinearFixedPoint"/> or <see cref="MsNumpress.OptimalSlofFixedPoint"/>".
    /// Ignored for <see cref="BinaryNumpress.Pic"/>.
    /// </summary>
    public double NumpressFixedPoint { get; set; }

    /// <summary>
    /// Relative tolerance for numpress Linear (default 2e-9). If any round-tripped value exceeds
    /// this bound, the encoder falls back to <see cref="BinaryNumpress.None"/> for that array.
    /// 0 disables the tolerance check.
    /// </summary>
    public double NumpressLinearErrorTolerance { get; set; } = 2e-9;

    /// <summary>
    /// Relative tolerance for numpress Slof (default 2e-4). If any round-tripped value exceeds
    /// this bound, the encoder falls back to <see cref="BinaryNumpress.None"/> for that array.
    /// 0 disables the tolerance check.
    /// </summary>
    public double NumpressSlofErrorTolerance { get; set; } = 2e-4;

    /// <summary>Per-CVID overrides for <see cref="Compression"/>. Lets callers use zlib for m/z but none for intensity, etc.</summary>
    public Dictionary<CVID, BinaryCompression> CompressionOverrides { get; } = new();

    /// <summary>Per-CVID overrides for <see cref="Precision"/> (e.g. 32-bit for intensity).</summary>
    public Dictionary<CVID, BinaryPrecision> PrecisionOverrides { get; } = new();

    /// <summary>Per-CVID overrides for <see cref="Numpress"/>.</summary>
    public Dictionary<CVID, BinaryNumpress> NumpressOverrides { get; } = new();

    /// <summary>Deep-copy constructor so callers can tweak without mutating the shared instance.</summary>
    public BinaryEncoderConfig Clone()
    {
        var c = new BinaryEncoderConfig
        {
            Precision = Precision,
            ByteOrder = ByteOrder,
            Compression = Compression,
            Numpress = Numpress,
            NumpressFixedPoint = NumpressFixedPoint,
            NumpressLinearErrorTolerance = NumpressLinearErrorTolerance,
            NumpressSlofErrorTolerance = NumpressSlofErrorTolerance,
        };
        foreach (var kv in CompressionOverrides) c.CompressionOverrides[kv.Key] = kv.Value;
        foreach (var kv in PrecisionOverrides) c.PrecisionOverrides[kv.Key] = kv.Value;
        foreach (var kv in NumpressOverrides) c.NumpressOverrides[kv.Key] = kv.Value;
        return c;
    }
}

/// <summary>
/// Encodes/decodes mzML <c>&lt;binaryDataArray&gt;</c> payloads.
/// Port of pwiz::msdata::BinaryDataEncoder.
/// </summary>
/// <remarks>
/// Pipeline (encode): <c>double[] → optional numpress → little-endian bytes (32 or 64 bit) → optional zlib → base64 → string</c>.
/// Pipeline (decode): inverse.
/// Numpress compression variants (Linear/Pic/Slof) are stubs — <see cref="BinaryNumpress"/> handling
/// throws <see cref="NotImplementedException"/>. File the follow-up task to finish.
/// </remarks>
public sealed class BinaryDataEncoder
{
    private readonly BinaryEncoderConfig _config;

    /// <summary>Creates an encoder with the given config (copied; safe to mutate the original afterwards).</summary>
    public BinaryDataEncoder(BinaryEncoderConfig? config = null)
    {
        _config = (config ?? new BinaryEncoderConfig()).Clone();
    }

    /// <summary>Returns the effective configuration (the in-use copy).</summary>
    public BinaryEncoderConfig Config => _config;

    // ---------- encode (double) ----------

    /// <summary>Encodes a doubles array to a base64 string per the configured pipeline.</summary>
    public string Encode(ReadOnlySpan<double> data) => Encode(data, out _);

    /// <summary>Encodes a doubles array; <paramref name="binaryByteCount"/> is the size of the raw (post-compression) bytes before base64.</summary>
    public string Encode(ReadOnlySpan<double> data, out int binaryByteCount)
        => Encode(data, out binaryByteCount, out _);

    /// <summary>
    /// Encodes a doubles array. <paramref name="actualNumpress"/> reports the numpress variant the
    /// encoder settled on — it may be <see cref="BinaryNumpress.None"/> even if the config requested
    /// numpress, when the configured tolerance would have been exceeded.
    /// </summary>
    public string Encode(ReadOnlySpan<double> data, out int binaryByteCount, out BinaryNumpress actualNumpress)
    {
        actualNumpress = _config.Numpress;
        byte[] bytes;
        switch (_config.Numpress)
        {
            case BinaryNumpress.None:
                bytes = _config.Precision == BinaryPrecision.Bits64
                    ? WriteDoubles64(data, _config.ByteOrder)
                    : WriteDoubles32(data, _config.ByteOrder);
                break;
            case BinaryNumpress.Linear:
                bytes = MsNumpress.EncodeLinear(data.ToArray(), _config.NumpressFixedPoint);
                if (_config.NumpressLinearErrorTolerance > 0
                    && !WithinTolerance(data, MsNumpress.DecodeLinear(bytes), _config.NumpressLinearErrorTolerance))
                {
                    actualNumpress = BinaryNumpress.None;
                    bytes = EncodeRaw(data);
                }
                break;
            case BinaryNumpress.Pic:
                bytes = MsNumpress.EncodePic(data.ToArray());
                // Pic quantizes to integers, so the tolerance is fixed at +/- 0.5. Still check
                // for overflow-induced NaNs.
                if (!WithinToleranceAbsolute(data, MsNumpress.DecodePic(bytes), 1.0))
                {
                    actualNumpress = BinaryNumpress.None;
                    bytes = EncodeRaw(data);
                }
                break;
            case BinaryNumpress.Slof:
                bytes = MsNumpress.EncodeSlof(data.ToArray(), _config.NumpressFixedPoint);
                if (_config.NumpressSlofErrorTolerance > 0
                    && !WithinTolerance(data, MsNumpress.DecodeSlof(bytes), _config.NumpressSlofErrorTolerance))
                {
                    actualNumpress = BinaryNumpress.None;
                    bytes = EncodeRaw(data);
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported numpress: {_config.Numpress}");
        }

        bytes = Compress(bytes);
        binaryByteCount = bytes.Length;
        return Convert.ToBase64String(bytes);
    }

    private byte[] EncodeRaw(ReadOnlySpan<double> data) =>
        _config.Precision == BinaryPrecision.Bits64
            ? WriteDoubles64(data, _config.ByteOrder)
            : WriteDoubles32(data, _config.ByteOrder);

    /// <summary>Relative-error tolerance check mirroring pwiz BinaryDataEncoder.cpp.</summary>
    private static bool WithinTolerance(ReadOnlySpan<double> original, ReadOnlySpan<double> decoded, double tolerance)
    {
        if (original.Length != decoded.Length) return false;
        for (int i = 0; i < original.Length; i++)
        {
            double o = original[i], d = decoded[i];
            if (!double.IsFinite(o) || !double.IsFinite(d)) return false;
            if (o == 0) { if (Math.Abs(d) > tolerance) return false; }
            else if (d == 0) { if (Math.Abs(o) > tolerance) return false; }
            else if (Math.Abs(1.0 - o / d) > tolerance) return false;
        }
        return true;
    }

    private static bool WithinToleranceAbsolute(ReadOnlySpan<double> original, ReadOnlySpan<double> decoded, double tolerance)
    {
        if (original.Length != decoded.Length) return false;
        for (int i = 0; i < original.Length; i++)
        {
            if (!double.IsFinite(decoded[i]) || Math.Abs(original[i] - decoded[i]) >= tolerance) return false;
        }
        return true;
    }

    // ---------- encode (int64) ----------

    /// <summary>Encodes an int64 array to a base64 string. int64 is always 64-bit precision (<see cref="BinaryPrecision"/> is ignored).</summary>
    public string EncodeInt64(ReadOnlySpan<long> data) => EncodeInt64(data, out _);

    /// <summary>Encodes an int64 array; reports the raw byte count.</summary>
    public string EncodeInt64(ReadOnlySpan<long> data, out int binaryByteCount)
    {
        if (_config.Numpress != BinaryNumpress.None)
            throw new NotImplementedException("Numpress encoding of int64 is not defined. Use doubles for numpress.");

        byte[] bytes = WriteInt64(data, _config.ByteOrder);
        bytes = Compress(bytes);
        binaryByteCount = bytes.Length;
        return Convert.ToBase64String(bytes);
    }

    // ---------- decode (double) ----------

    /// <summary>Decodes a base64 string into a doubles array per the configured pipeline.</summary>
    public double[] DecodeDoubles(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        byte[] bytes = Convert.FromBase64String(encoded);
        return DecodeDoublesFromBytes(bytes);
    }

    /// <summary>Decodes a base64 character slice. Useful for streaming readers that want to avoid a string copy.</summary>
    public double[] DecodeDoubles(ReadOnlySpan<char> encoded)
    {
        byte[] bytes = FromBase64(encoded);
        return DecodeDoublesFromBytes(bytes);
    }

    private double[] DecodeDoublesFromBytes(byte[] bytes)
    {
        bytes = Decompress(bytes);

        return _config.Numpress switch
        {
            BinaryNumpress.None => _config.Precision == BinaryPrecision.Bits64
                ? ReadDoubles64(bytes, _config.ByteOrder)
                : ReadDoubles32(bytes, _config.ByteOrder),
            BinaryNumpress.Linear => MsNumpress.DecodeLinear(bytes),
            BinaryNumpress.Pic => MsNumpress.DecodePic(bytes),
            BinaryNumpress.Slof => MsNumpress.DecodeSlof(bytes),
            _ => throw new InvalidOperationException($"Unsupported numpress: {_config.Numpress}"),
        };
    }

    // ---------- decode (int64) ----------

    /// <summary>Decodes a base64 string into an int64 array.</summary>
    public long[] DecodeInt64(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        byte[] bytes = Convert.FromBase64String(encoded);
        return DecodeInt64FromBytes(bytes);
    }

    /// <summary>Decodes a base64 character slice into an int64 array.</summary>
    public long[] DecodeInt64(ReadOnlySpan<char> encoded)
    {
        byte[] bytes = FromBase64(encoded);
        return DecodeInt64FromBytes(bytes);
    }

    private long[] DecodeInt64FromBytes(byte[] bytes)
    {
        if (_config.Numpress != BinaryNumpress.None)
            throw new NotImplementedException("Numpress decoding of int64 is not defined.");

        bytes = Decompress(bytes);
        return ReadInt64(bytes, _config.ByteOrder);
    }

    /// <summary>
    /// Decodes integer data, picking int32 or int64 based on <see cref="BinaryEncoderConfig.Precision"/>.
    /// 32-bit values are widened to <see cref="long"/> so callers have a single code path.
    /// </summary>
    public long[] DecodeIntegers(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        byte[] bytes = Convert.FromBase64String(encoded);
        if (_config.Numpress != BinaryNumpress.None)
            throw new NotImplementedException("Numpress decoding of integer arrays is not defined.");
        bytes = Decompress(bytes);
        if (_config.Precision == BinaryPrecision.Bits32)
            return ReadInt32AsInt64(bytes, _config.ByteOrder);
        return ReadInt64(bytes, _config.ByteOrder);
    }

    private static long[] ReadInt32AsInt64(byte[] bytes, BinaryByteOrder order)
    {
        if (bytes.Length % 4 != 0)
            throw new InvalidDataException($"Byte count {bytes.Length} is not a multiple of 4 (expected 32-bit ints).");
        int count = bytes.Length / 4;
        var result = new long[count];
        var span = bytes.AsSpan();
        if (order == BinaryByteOrder.LittleEndian)
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(i * 4, 4));
        }
        else
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadInt32BigEndian(span.Slice(i * 4, 4));
        }
        return result;
    }

    // ---------- raw-bytes helpers ----------

    private static byte[] WriteDoubles64(ReadOnlySpan<double> data, BinaryByteOrder order)
    {
        byte[] buf = new byte[data.Length * 8];
        var span = buf.AsSpan();
        if (order == BinaryByteOrder.LittleEndian)
        {
            for (int i = 0; i < data.Length; i++)
                BinaryPrimitives.WriteDoubleLittleEndian(span.Slice(i * 8, 8), data[i]);
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
                BinaryPrimitives.WriteDoubleBigEndian(span.Slice(i * 8, 8), data[i]);
        }
        return buf;
    }

    private static byte[] WriteDoubles32(ReadOnlySpan<double> data, BinaryByteOrder order)
    {
        byte[] buf = new byte[data.Length * 4];
        var span = buf.AsSpan();
        if (order == BinaryByteOrder.LittleEndian)
        {
            for (int i = 0; i < data.Length; i++)
                BinaryPrimitives.WriteSingleLittleEndian(span.Slice(i * 4, 4), (float)data[i]);
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
                BinaryPrimitives.WriteSingleBigEndian(span.Slice(i * 4, 4), (float)data[i]);
        }
        return buf;
    }

    private static byte[] WriteInt64(ReadOnlySpan<long> data, BinaryByteOrder order)
    {
        byte[] buf = new byte[data.Length * 8];
        var span = buf.AsSpan();
        if (order == BinaryByteOrder.LittleEndian)
        {
            for (int i = 0; i < data.Length; i++)
                BinaryPrimitives.WriteInt64LittleEndian(span.Slice(i * 8, 8), data[i]);
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
                BinaryPrimitives.WriteInt64BigEndian(span.Slice(i * 8, 8), data[i]);
        }
        return buf;
    }

    private static double[] ReadDoubles64(byte[] bytes, BinaryByteOrder order)
    {
        if (bytes.Length % 8 != 0)
            throw new InvalidDataException($"Byte count {bytes.Length} is not a multiple of 8 (expected 64-bit doubles).");
        int count = bytes.Length / 8;
        var result = new double[count];
        var span = bytes.AsSpan();
        if (order == BinaryByteOrder.LittleEndian)
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(i * 8, 8));
        }
        else
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadDoubleBigEndian(span.Slice(i * 8, 8));
        }
        return result;
    }

    private static double[] ReadDoubles32(byte[] bytes, BinaryByteOrder order)
    {
        if (bytes.Length % 4 != 0)
            throw new InvalidDataException($"Byte count {bytes.Length} is not a multiple of 4 (expected 32-bit floats).");
        int count = bytes.Length / 4;
        var result = new double[count];
        var span = bytes.AsSpan();
        if (order == BinaryByteOrder.LittleEndian)
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(i * 4, 4));
        }
        else
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadSingleBigEndian(span.Slice(i * 4, 4));
        }
        return result;
    }

    private static long[] ReadInt64(byte[] bytes, BinaryByteOrder order)
    {
        if (bytes.Length % 8 != 0)
            throw new InvalidDataException($"Byte count {bytes.Length} is not a multiple of 8 (expected 64-bit ints).");
        int count = bytes.Length / 8;
        var result = new long[count];
        var span = bytes.AsSpan();
        if (order == BinaryByteOrder.LittleEndian)
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(i * 8, 8));
        }
        else
        {
            for (int i = 0; i < count; i++)
                result[i] = BinaryPrimitives.ReadInt64BigEndian(span.Slice(i * 8, 8));
        }
        return result;
    }

    // ---------- compression ----------

    private byte[] Compress(byte[] raw) => _config.Compression switch
    {
        BinaryCompression.None => raw,
        BinaryCompression.Zlib => ZlibCompress(raw),
        _ => throw new InvalidOperationException($"Unsupported compression: {_config.Compression}"),
    };

    private byte[] Decompress(byte[] compressed) => _config.Compression switch
    {
        BinaryCompression.None => compressed,
        BinaryCompression.Zlib => ZlibDecompress(compressed),
        _ => throw new InvalidOperationException($"Unsupported compression: {_config.Compression}"),
    };

    private static byte[] ZlibCompress(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }
        return output.ToArray();
    }

    private static byte[] ZlibDecompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        return output.ToArray();
    }

    // ---------- base64 from char span ----------

    private static byte[] FromBase64(ReadOnlySpan<char> encoded)
    {
        // System.Convert doesn't expose a ReadOnlySpan<char> overload that allocates;
        // we use the TryFromBase64Chars API with a pooled buffer.
        int maxLen = (encoded.Length * 3) / 4 + 4;
        byte[] rented = ArrayPool<byte>.Shared.Rent(maxLen);
        try
        {
            if (!Convert.TryFromBase64Chars(encoded, rented, out int written))
                throw new FormatException("Input is not valid base64.");
            byte[] result = new byte[written];
            Array.Copy(rented, result, written);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
