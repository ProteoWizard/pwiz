namespace Pwiz.Data.MsData.Encoding;

/// <summary>
/// Numerical compressions for mass-spectrum binary arrays.
/// Port of <c>pwiz::msdata::MSNumpress</c> (Johan Teleman, 2013).
/// </summary>
/// <remarks>
/// Three lossy codecs that produce smaller payloads than raw float/double:
/// <list type="bullet">
///   <item><see cref="EncodeLinear(double[], double)"/> — fixed-point + linear prediction (m/z, retention time).</item>
///   <item><see cref="EncodePic(double[])"/> — nearest-integer compression (ion counts).</item>
///   <item><see cref="EncodeSlof(double[], double)"/> — short-logged-float (ion counts, more dynamic range).</item>
/// </list>
/// Integer values are packed using <see cref="EncodeInt(int, byte[], ref int)"/>, which stores a 4-byte
/// int as a variable-length sequence of half-bytes (nibbles).
/// </remarks>
public static class MsNumpress
{
    // ---------- encodeFixedPoint / decodeFixedPoint ----------

    /// <summary>Writes the 8 bytes of <paramref name="fixedPoint"/> in big-endian order into <paramref name="result"/>.</summary>
    /// <remarks>
    /// The ms-numpress reference implementation writes the double in big-endian byte order on every
    /// platform (<c>MSNumpress.cpp</c>: <c>result[i] = fp[IS_LITTLE_ENDIAN ? (7-i) : i]</c>, which
    /// reverses the bytes on little-endian hosts). Earlier versions called this <c>is_big_endian()</c>
    /// — the upstream has since been renamed but the wire format is unchanged. Byte-exact interop
    /// with pwiz-encoded streams requires matching this behavior.
    /// </remarks>
    public static void EncodeFixedPoint(double fixedPoint, byte[] result)
    {
        ArgumentNullException.ThrowIfNull(result);
        System.Buffers.Binary.BinaryPrimitives.WriteDoubleBigEndian(result.AsSpan(0, 8), fixedPoint);
    }

    /// <summary>Reads the 8-byte big-endian fixed-point double from <paramref name="data"/>.</summary>
    public static double DecodeFixedPoint(ReadOnlySpan<byte> data)
        => System.Buffers.Binary.BinaryPrimitives.ReadDoubleBigEndian(data[..8]);

    // ---------- encodeInt / decodeInt ----------

    /// <summary>
    /// Packs <paramref name="x"/> (a 32-bit signed int) as a variable-length sequence of half-bytes into
    /// <paramref name="res"/>, advancing <paramref name="resLength"/> by the number of half-bytes written.
    /// </summary>
    /// <remarks>
    /// The first half-byte <c>c</c> encodes how many leading zero/one half-bytes the int has:
    /// <c>0..8</c> = leading zeros, <c>9..15</c> = leading ones. Then the remaining half-bytes follow in
    /// little-endian order. Writes at most 9 half-bytes.
    /// </remarks>
    public static void EncodeInt(int x, byte[] res, ref int resLength)
    {
        ArgumentNullException.ThrowIfNull(res);
        const uint mask = 0xf0000000u;
        uint ux = (uint)x;
        uint init = ux & mask;

        if (init == 0)
        {
            int l = 8;
            for (int i = 0; i < 8; i++)
            {
                uint m = mask >> (4 * i);
                if ((ux & m) != 0) { l = i; break; }
            }
            res[resLength] = (byte)l;
            for (int i = l; i < 8; i++)
                res[resLength + 1 + i - l] = (byte)(ux >> (4 * (i - l)));
            resLength += 1 + 8 - l;
        }
        else if (init == mask)
        {
            int l = 7;
            for (int i = 0; i < 8; i++)
            {
                uint m = mask >> (4 * i);
                if ((ux & m) != m) { l = i; break; }
            }
            res[resLength] = (byte)(l + 8);
            for (int i = l; i < 8; i++)
                res[resLength + 1 + i - l] = (byte)(ux >> (4 * (i - l)));
            resLength += 1 + 8 - l;
        }
        else
        {
            res[resLength] = 0;
            for (int i = 0; i < 8; i++)
                res[resLength + 1 + i] = (byte)(ux >> (4 * i));
            resLength += 9;
        }
    }

    /// <summary>
    /// Decodes a single int from the nibble stream <paramref name="data"/>. <paramref name="di"/> is the
    /// current byte index; <paramref name="half"/> is 0 when we're reading the high nibble of that byte,
    /// 1 for the low nibble. Both are advanced as the int is consumed.
    /// </summary>
    public static void DecodeInt(ReadOnlySpan<byte> data, ref int di, ref int half, out int result)
    {
        byte head;
        if (half == 0) head = (byte)(data[di] >> 4);
        else { head = (byte)(data[di] & 0xf); di++; }

        half = 1 - half;
        uint res = 0;
        int n;
        if (head <= 8) n = head;
        else
        {
            n = head - 8;
            uint mask = 0xf0000000u;
            for (int i = 0; i < n; i++)
            {
                uint m = mask >> (4 * i);
                res |= m;
            }
        }

        if (n == 8) { result = (int)res; return; }

        for (int i = n; i < 8; i++)
        {
            byte hb;
            if (half == 0) hb = (byte)(data[di] >> 4);
            else { hb = (byte)(data[di] & 0xf); di++; }
            res |= (uint)hb << ((i - n) * 4);
            half = 1 - half;
        }
        result = (int)res;
    }

    // ---------- linear (m/z, retention time) ----------

    /// <summary>
    /// Returns the largest fixed-point multiplier that lets every linear-predicted residual fit into
    /// a 32-bit signed int. Used when a caller passes 0 for the fixed point.
    /// </summary>
    public static double OptimalLinearFixedPoint(ReadOnlySpan<double> data)
    {
        if (data.Length == 0) return 0;
        if (data.Length == 1) return Math.Floor(0x7FFFFFFFL / data[0]);
        double maxVal = Math.Max(data[0], data[1]);
        for (int i = 2; i < data.Length; i++)
        {
            double extrapol = data[i - 1] + (data[i - 1] - data[i - 2]);
            double diff = data[i] - extrapol;
            maxVal = Math.Max(maxVal, Math.Ceiling(Math.Abs(diff) + 1));
        }
        return Math.Floor(0x7FFFFFFFL / maxVal);
    }

    /// <summary>
    /// Returns a fixed-point multiplier that yields <paramref name="massAccuracy"/> absolute accuracy,
    /// or <c>-1</c> if that accuracy can't be achieved without 32-bit overflow.
    /// </summary>
    public static double OptimalLinearFixedPointMass(ReadOnlySpan<double> data, double massAccuracy)
    {
        if (data.Length < 3) return 0;
        double maxFp = 0.5 / massAccuracy;
        double overflow = OptimalLinearFixedPoint(data);
        return maxFp > overflow ? -1 : maxFp;
    }

    /// <summary>
    /// Encodes <paramref name="data"/> using fixed-point quantization plus linear-prediction residuals.
    /// When <paramref name="fixedPoint"/> is 0, <see cref="OptimalLinearFixedPoint"/> picks it.
    /// </summary>
    public static byte[] EncodeLinear(double[] data, double fixedPoint)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (fixedPoint == 0) fixedPoint = OptimalLinearFixedPoint(data);

        var buf = new byte[8 + data.Length * 5];
        EncodeFixedPoint(fixedPoint, buf);
        if (data.Length == 0) return Trim(buf, 8);

        long i1 = (long)(data[0] * fixedPoint + 0.5);
        for (int i = 0; i < 4; i++) buf[8 + i] = (byte)((i1 >> (i * 8)) & 0xff);
        if (data.Length == 1) return Trim(buf, 12);

        long i2 = (long)(data[1] * fixedPoint + 0.5);
        for (int i = 0; i < 4; i++) buf[12 + i] = (byte)((i2 >> (i * 8)) & 0xff);

        int halfByteCount = 0;
        int ri = 16;
        byte[] halfBytes = new byte[10];
        long prev1 = i1, prev2 = i2;

        for (int i = 2; i < data.Length; i++)
        {
            long cur = (long)(data[i] * fixedPoint + 0.5);
            long extrapol = prev2 + (prev2 - prev1);
            int diff = (int)(cur - extrapol);
            EncodeInt(diff, halfBytes, ref halfByteCount);

            for (int hbi = 1; hbi < halfByteCount; hbi += 2)
            {
                buf[ri++] = (byte)((halfBytes[hbi - 1] << 4) | (halfBytes[hbi] & 0xf));
            }
            if (halfByteCount % 2 != 0)
            {
                halfBytes[0] = halfBytes[halfByteCount - 1];
                halfByteCount = 1;
            }
            else halfByteCount = 0;

            prev1 = prev2;
            prev2 = cur;
        }
        if (halfByteCount == 1)
        {
            buf[ri++] = (byte)(halfBytes[0] << 4);
        }
        return Trim(buf, ri);
    }

    /// <summary>Decodes a byte array produced by <see cref="EncodeLinear(double[], double)"/>.</summary>
    public static double[] DecodeLinear(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8) throw new InvalidDataException("Numpress linear: need at least 8 bytes for fixed point.");
        double fixedPoint = DecodeFixedPoint(data);
        if (data.Length < 12) return Array.Empty<double>();

        var result = new double[(data.Length - 8) * 2];
        long i1 = 0;
        for (int i = 0; i < 4; i++) i1 |= (long)(data[8 + i] & 0xff) << (i * 8);
        // sign-extend from 32 bits
        i1 = (int)i1;
        result[0] = i1 / fixedPoint;
        if (data.Length == 12) return Resize(result, 1);
        if (data.Length < 16) throw new InvalidDataException("Numpress linear: truncated data after first value.");

        long i2 = 0;
        for (int i = 0; i < 4; i++) i2 |= (long)(data[12 + i] & 0xff) << (i * 8);
        i2 = (int)i2;
        result[1] = i2 / fixedPoint;

        int ri = 2;
        int di = 16;
        int half = 0;
        long prev1 = i1, prev2 = i2;

        while (di < data.Length)
        {
            if (di == data.Length - 1 && half == 1)
            {
                if ((data[di] & 0xf) != 0x8) break;
            }
            DecodeInt(data, ref di, ref half, out int diff);
            long extrapol = prev2 + (prev2 - prev1);
            long y = extrapol + diff;
            result[ri++] = y / fixedPoint;
            prev1 = prev2;
            prev2 = y;
        }
        return Resize(result, ri);
    }

    // ---------- pic (ion counts, integer) ----------

    /// <summary>
    /// Encodes <paramref name="data"/> as nearest-integer counts compressed with <see cref="EncodeInt(int, byte[], ref int)"/>.
    /// Handleable value range: <c>0 .. 4294967294</c>.
    /// </summary>
    public static byte[] EncodePic(double[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var buf = new byte[data.Length * 5 + 1]; // +1 slack for odd half-byte trailer
        int halfByteCount = 0;
        int ri = 0;
        byte[] halfBytes = new byte[10];

        for (int i = 0; i < data.Length; i++)
        {
            int count = (int)(data[i] + 0.5);
            EncodeInt(count, halfBytes, ref halfByteCount);
            for (int hbi = 1; hbi < halfByteCount; hbi += 2)
            {
                buf[ri++] = (byte)((halfBytes[hbi - 1] << 4) | (halfBytes[hbi] & 0xf));
            }
            if (halfByteCount % 2 != 0)
            {
                halfBytes[0] = halfBytes[halfByteCount - 1];
                halfByteCount = 1;
            }
            else halfByteCount = 0;
        }
        if (halfByteCount == 1)
        {
            buf[ri++] = (byte)(halfBytes[0] << 4);
        }
        return Trim(buf, ri);
    }

    /// <summary>Decodes a byte array produced by <see cref="EncodePic"/>.</summary>
    public static double[] DecodePic(ReadOnlySpan<byte> data)
    {
        var result = new double[data.Length * 2];
        int ri = 0;
        int di = 0;
        int half = 0;
        while (di < data.Length)
        {
            if (di == data.Length - 1 && half == 1)
            {
                if ((data[di] & 0xf) != 0x8) break;
            }
            DecodeInt(data, ref di, ref half, out int count);
            result[ri++] = count;
        }
        return Resize(result, ri);
    }

    // ---------- slof (short-logged-float, ion counts) ----------

    /// <summary>Returns the fixed-point multiplier that maximizes Slof precision for this data.</summary>
    public static double OptimalSlofFixedPoint(ReadOnlySpan<double> data)
    {
        if (data.Length == 0) return 0;
        double maxVal = 1;
        for (int i = 0; i < data.Length; i++)
            maxVal = Math.Max(maxVal, Math.Log(data[i] + 1));
        return Math.Floor(0xFFFF / maxVal);
    }

    /// <summary>
    /// Encodes <paramref name="data"/> as <c>ushort fp = round(log(x+1) * fixedPoint)</c>.
    /// When <paramref name="fixedPoint"/> is 0, <see cref="OptimalSlofFixedPoint"/> picks it.
    /// </summary>
    public static byte[] EncodeSlof(double[] data, double fixedPoint)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (fixedPoint == 0) fixedPoint = OptimalSlofFixedPoint(data);

        var buf = new byte[8 + data.Length * 2];
        EncodeFixedPoint(fixedPoint, buf);
        int ri = 8;
        for (int i = 0; i < data.Length; i++)
        {
            ushort x = (ushort)(Math.Log(data[i] + 1) * fixedPoint + 0.5);
            buf[ri++] = (byte)(x & 0xff);
            buf[ri++] = (byte)(x >> 8);
        }
        return buf;
    }

    /// <summary>Decodes a byte array produced by <see cref="EncodeSlof"/>.</summary>
    public static double[] DecodeSlof(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8) throw new InvalidDataException("Numpress slof: need at least 8 bytes for fixed point.");
        double fixedPoint = DecodeFixedPoint(data);
        int count = (data.Length - 8) / 2;
        var result = new double[count];
        for (int i = 0; i < count; i++)
        {
            ushort x = (ushort)(data[8 + i * 2] | (data[8 + i * 2 + 1] << 8));
            result[i] = Math.Exp(x / fixedPoint) - 1;
        }
        return result;
    }

    // ---------- helpers ----------

    private static byte[] Trim(byte[] buf, int length)
    {
        if (buf.Length == length) return buf;
        var copy = new byte[length];
        Array.Copy(buf, copy, length);
        return copy;
    }

    private static double[] Resize(double[] arr, int length)
    {
        if (arr.Length == length) return arr;
        var copy = new double[length];
        Array.Copy(arr, copy, length);
        return copy;
    }
}
