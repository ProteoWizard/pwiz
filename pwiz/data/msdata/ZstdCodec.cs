using System.Buffers.Binary;
using ZstdSharp;

namespace Pwiz.Data.MsData.Encoding;

/// <summary>
/// The three Zstandard codecs mzML defines for binary data arrays. Port of the codecs in pwiz
/// C++ <c>pwiz/utility/misc/mzd.hpp</c>.
/// </summary>
/// <remarks>
/// Every codec here works on the array's raw little-endian bytes, whatever byte order the
/// encoder config asks for: the CV terms define the stored layout as little-endian, and cpp's
/// <c>mzd</c> codecs force it on big-endian hosts.
/// <list type="bullet">
///   <item><c>MS:1003780</c> zstd compression - the raw bytes, zstd-compressed.</item>
///   <item><c>MS:1003781</c> byte-shuffled zstd compression - byte 0 of every value, then byte 1
///         of every value, and so on, zstd-compressed. Neighbouring m/z values share their high
///         bytes, so the shuffle hands zstd long runs.</item>
///   <item><c>MS:1003782</c> dictionary-encoded zstd compression - a sorted table of the distinct
///         values, then each value's index into it, both byte-shuffled, zstd-compressed. Wins on
///         arrays with few distinct values, such as ion mobility.</item>
/// </list>
/// </remarks>
internal static class ZstdCodec
{
    /// <summary>cpp compresses at <c>ZSTD_defaultCLevel()</c>, which is 3.</summary>
    private const int COMPRESSION_LEVEL = 3;

    /// <summary>Dictionary header: the byte offset of the index block, then the number of
    /// distinct values, both little-endian uint64.</summary>
    private const int DICT_HEADER_SIZE = 16;

    // A compression context costs far more to create than to reuse, and the writer compresses
    // every array of every spectrum, so each thread keeps one of each.
    [ThreadStatic] private static Compressor? _compressor;
    [ThreadStatic] private static Decompressor? _decompressor;

    /// <summary>Zstd-compresses <paramref name="raw"/> as is (<c>MS:1003780</c>).</summary>
    public static byte[] Compress(ReadOnlySpan<byte> raw)
    {
        var compressor = _compressor ??= new Compressor(COMPRESSION_LEVEL);
        var output = new byte[Compressor.GetCompressBound(raw.Length)];
        int written = compressor.Wrap(raw, output);
        Array.Resize(ref output, written);
        return output;
    }

    /// <summary>Inverse of <see cref="Compress"/>.</summary>
    /// <remarks>
    /// Also reads frames from streaming compressors, which do not record their content size.
    /// For those <see cref="Decompressor.GetDecompressedSize(ReadOnlySpan{byte})"/> returns an
    /// upper bound rather than the size, so the output is trimmed to what was written. (cpp
    /// rejects such frames.)
    /// </remarks>
    public static byte[] Decompress(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<byte>();
        ulong bound = Decompressor.GetDecompressedSize(compressed);
        if (bound > (ulong)Array.MaxLength)
            throw new InvalidDataException($"Zstd data decompresses to as many as {bound} bytes, more than an array can hold.");
        var decompressor = _decompressor ??= new Decompressor();
        var output = new byte[bound];
        int written = decompressor.Unwrap(compressed, output);
        if (written < output.Length)
            Array.Resize(ref output, written);
        return output;
    }

    /// <summary>Byte-shuffles <paramref name="raw"/>, an array of <paramref name="valueSize"/>-byte
    /// little-endian values, then zstd-compresses it (<c>MS:1003781</c>).</summary>
    public static byte[] ByteShuffleCompress(ReadOnlySpan<byte> raw, int valueSize)
    {
        return Compress(Transpose(raw, valueSize));
    }

    /// <summary>Inverse of <see cref="ByteShuffleCompress"/>.</summary>
    public static byte[] ByteShuffleDecompress(ReadOnlySpan<byte> compressed, int valueSize)
    {
        return ReverseTranspose(Decompress(compressed), valueSize);
    }

    /// <summary>Dictionary-encodes <paramref name="raw"/>, an array of
    /// <paramref name="valueSize"/>-byte little-endian values, then zstd-compresses it
    /// (<c>MS:1003782</c>).</summary>
    /// <remarks>
    /// The dictionary is sorted by each value's bit pattern read as an unsigned integer, as cpp
    /// sorts it, so the bytes match cpp's for the same input. Index width is the narrowest of 1, 2
    /// or 4 bytes that can hold the dictionary size.
    /// <para>The one departure from cpp: a dictionary of exactly 255 or 65535 values gets one
    /// unused value added, so its index width is the same under both of cpp's width rules (see
    /// <see cref="IndexSize"/>). cpp's encoder wrote such an array with indices its own decoder
    /// misreads; padded, every reader decodes it, at the cost of one dictionary entry and wider
    /// indices for an array that lands exactly on the boundary.</para>
    /// </remarks>
    public static byte[] DictCompress(ReadOnlySpan<byte> raw, int valueSize)
    {
        int count = CountValues(raw.Length, valueSize);
        var values = new ulong[count];
        for (int i = 0; i < count; i++)
            values[i] = ReadValue(raw.Slice(i * valueSize, valueSize));

        var dictionary = (ulong[])values.Clone();
        Array.Sort(dictionary);
        int distinct = 0;
        for (int i = 0; i < dictionary.Length; i++)
        {
            if (distinct == 0 || dictionary[i] != dictionary[distinct - 1])
                dictionary[distinct++] = dictionary[i];
        }
        if (distinct is byte.MaxValue or ushort.MaxValue)
        {
            dictionary = WithUnusedValue(dictionary.AsSpan(0, distinct));
            distinct++;
        }

        var indexOf = new Dictionary<ulong, int>(distinct);
        for (int i = 0; i < distinct; i++)
            indexOf[dictionary[i]] = i;

        int indexSize = IndexSize((ulong)distinct);
        int valuesOffset = DICT_HEADER_SIZE + distinct * valueSize;
        var encoded = new byte[valuesOffset + count * indexSize];
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(0, 8), (ulong)valuesOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(8, 8), (ulong)distinct);

        var buffer = new byte[Math.Max(distinct * valueSize, count * indexSize)];
        for (int i = 0; i < distinct; i++)
            WriteValue(buffer.AsSpan(i * valueSize, valueSize), dictionary[i]);
        Transpose(buffer.AsSpan(0, distinct * valueSize), valueSize, encoded.AsSpan(DICT_HEADER_SIZE));

        for (int i = 0; i < count; i++)
            WriteValue(buffer.AsSpan(i * indexSize, indexSize), (ulong)indexOf[values[i]]);
        Transpose(buffer.AsSpan(0, count * indexSize), indexSize, encoded.AsSpan(valuesOffset));

        return Compress(encoded);
    }

    /// <summary>Inverse of <see cref="DictCompress"/>.</summary>
    public static byte[] DictDecompress(ReadOnlySpan<byte> compressed, int valueSize)
    {
        byte[] encoded = Decompress(compressed);
        if (encoded.Length == 0)
            return encoded;
        if (encoded.Length < DICT_HEADER_SIZE)
            throw new InvalidDataException($"Dictionary-encoded array is {encoded.Length} bytes, shorter than its {DICT_HEADER_SIZE}-byte header.");

        ulong valuesOffset = BinaryPrimitives.ReadUInt64LittleEndian(encoded.AsSpan(0, 8));
        ulong distinct = BinaryPrimitives.ReadUInt64LittleEndian(encoded.AsSpan(8, 8));
        if (valuesOffset < DICT_HEADER_SIZE || valuesOffset > (ulong)encoded.Length)
            throw new InvalidDataException($"Dictionary-encoded array puts its indices at byte {valuesOffset} of {encoded.Length}.");
        if (distinct == 0)
            return Array.Empty<byte>();
        // Divide rather than multiply: a corrupt count can overflow distinct * valueSize.
        ulong dictionaryBytes = valuesOffset - DICT_HEADER_SIZE;
        if (dictionaryBytes % (ulong)valueSize != 0 || dictionaryBytes / (ulong)valueSize != distinct)
        {
            throw new InvalidDataException(
                $"Dictionary of {distinct} values occupies {valuesOffset - DICT_HEADER_SIZE} bytes; expected {valueSize} bytes per value.");
        }

        byte[] dictionary = ReverseTranspose(encoded.AsSpan(DICT_HEADER_SIZE, (int)valuesOffset - DICT_HEADER_SIZE), valueSize);
        int indexSize = IndexSize(distinct);
        byte[] indices = ReverseTranspose(encoded.AsSpan((int)valuesOffset), indexSize);
        int count = indices.Length / indexSize;

        var output = new byte[count * valueSize];
        for (int i = 0; i < count; i++)
        {
            ulong index = ReadValue(indices.AsSpan(i * indexSize, indexSize));
            if (index >= distinct)
                throw new InvalidDataException($"Dictionary index {index} is past the end of a {distinct}-value dictionary.");
            dictionary.AsSpan((int)index * valueSize, valueSize).CopyTo(output.AsSpan(i * valueSize));
        }
        return output;
    }

    /// <summary>
    /// Index width for a dictionary of <paramref name="distinct"/> values: cpp's encoder picks
    /// uint8 up to 255 values, uint16 up to 65535, else uint32.
    /// </summary>
    /// <remarks>
    /// cpp's decoder tests <c>distinct &lt; max</c> where its encoder tests <c>distinct &lt;= max</c>,
    /// so the two disagree at exactly 255 and 65535 values. Decoding follows the encoder, so files
    /// cpp wrote at the boundary read correctly; <see cref="DictCompress"/> never writes one.
    /// </remarks>
    private static int IndexSize(ulong distinct)
    {
        if (distinct <= byte.MaxValue)
            return 1;
        if (distinct <= ushort.MaxValue)
            return 2;
        return 4;
    }

    /// <summary>
    /// Returns <paramref name="sorted"/> (distinct, ascending) with the smallest value it lacks
    /// inserted in order. Values 0..i-1 fill the first i slots up to the first slot holding
    /// something other than its own position, so that position is the smallest missing value -
    /// and it fits any value width, because it is at most the dictionary size.
    /// </summary>
    private static ulong[] WithUnusedValue(ReadOnlySpan<ulong> sorted)
    {
        int gap = 0;
        while (gap < sorted.Length && sorted[gap] == (ulong)gap)
            gap++;
        var padded = new ulong[sorted.Length + 1];
        sorted[..gap].CopyTo(padded);
        padded[gap] = (ulong)gap;
        sorted[gap..].CopyTo(padded.AsSpan(gap + 1));
        return padded;
    }

    private static byte[] Transpose(ReadOnlySpan<byte> raw, int valueSize)
    {
        var shuffled = new byte[raw.Length];
        Transpose(raw, valueSize, shuffled);
        return shuffled;
    }

    /// <summary>Writes byte 0 of every value, then byte 1 of every value, and so on.</summary>
    private static void Transpose(ReadOnlySpan<byte> raw, int valueSize, Span<byte> shuffled)
    {
        int count = CountValues(raw.Length, valueSize);
        for (int b = 0; b < valueSize; b++)
        {
            var plane = shuffled.Slice(b * count, count);
            for (int i = 0; i < count; i++)
                plane[i] = raw[i * valueSize + b];
        }
    }

    private static byte[] ReverseTranspose(ReadOnlySpan<byte> shuffled, int valueSize)
    {
        int count = CountValues(shuffled.Length, valueSize);
        var raw = new byte[shuffled.Length];
        for (int b = 0; b < valueSize; b++)
        {
            var plane = shuffled.Slice(b * count, count);
            for (int i = 0; i < count; i++)
                raw[i * valueSize + b] = plane[i];
        }
        return raw;
    }

    private static int CountValues(int byteCount, int valueSize)
    {
        if (byteCount % valueSize != 0)
            throw new InvalidDataException($"Byte count {byteCount} is not a multiple of the {valueSize}-byte value size.");
        return byteCount / valueSize;
    }

    private static ulong ReadValue(ReadOnlySpan<byte> bytes) => bytes.Length switch
    {
        1 => bytes[0],
        2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
        4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        8 => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, "Value size must be 1, 2, 4 or 8 bytes."),
    };

    private static void WriteValue(Span<byte> bytes, ulong value)
    {
        switch (bytes.Length)
        {
            case 1:
                bytes[0] = (byte)value;
                break;
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)value);
                break;
            case 4:
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)value);
                break;
            case 8:
                BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, "Value size must be 1, 2, 4 or 8 bytes.");
        }
    }
}
