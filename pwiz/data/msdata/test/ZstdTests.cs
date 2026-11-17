using System.Buffers.Binary;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.Encoding;

/// <summary>
/// The mzML zstd compressions (MS:1003780-1003785) beyond the encoder's byte-level cpp regression
/// in <see cref="BinaryDataEncoderCppRegressionTests"/>: the cpp IOTest fixture, the cvParams the
/// writer emits and the reader honors, and the codec edge cases.
/// </summary>
[TestClass]
public class ZstdTests
{
    /// <summary>cpp IOTest.cpp's testBinaryArraysWithZstd, on the same two binaryDataArrays.</summary>
    [TestMethod]
    public void ReadsCppIOTestArrays()
    {
        var spectrum = ReadFixture().Run.SpectrumList!.GetSpectrum(0);

        var mz = spectrum.GetMZArray()!.Data;
        Assert.AreEqual(1006, mz.Count, "byte-shuffled zstd m/z");
        Assert.AreEqual(236.047, mz[0], 1e-2);
        Assert.AreEqual(1636.43, mz[^1], 1e-2);

        var intensity = spectrum.GetIntensityArray()!.Data;
        Assert.AreEqual(1006, intensity.Count, "zstd intensity");
        Assert.AreEqual(11.6745, intensity[0], 1e-2);
        Assert.AreEqual(19.6446, intensity[^1], 1e-2);
    }

    /// <summary>
    /// What msconvert's <c>--zstd --dictZstd --numpressSlof</c> asks for: each array gets its own
    /// zstd cvParam, the numpress array gets the combined "followed by zstd" term, and every array
    /// reads back. Re-writing the result as zlib must not carry a stale zstd cvParam forward,
    /// which would make readers decode zlib bytes as zstd.
    /// </summary>
    [TestMethod]
    public void MzmlRoundTrip_EmitsAndHonorsZstdCvParams()
    {
        var msd = ReadFixture();
        var spectrum = msd.Run.SpectrumList!.GetSpectrum(0);
        var mobility = new BinaryDataArray();
        mobility.Set(CVID.MS_mean_inverse_reduced_ion_mobility_array);
        for (int i = 0; i < spectrum.DefaultArrayLength; i++)
            mobility.Data.Add(0.6 + (i % 7) * 0.05);
        spectrum.BinaryDataArrays.Add(mobility);
        var charge = new IntegerDataArray();
        charge.Set(CVID.MS_charge_array);
        charge.Data.AddRange(Enumerable.Range(0, spectrum.DefaultArrayLength).Select(i => (long)(i % 4)));
        spectrum.IntegerDataArrays.Add(charge);
        var expected = CloneArrays(spectrum);

        var config = new BinaryEncoderConfig { Compression = BinaryCompression.Zstd };
        config.CompressionOverrides[CVID.MS_m_z_array] = BinaryCompression.DictZstd;
        config.CompressionOverrides[CVID.MS_mean_inverse_reduced_ion_mobility_array] = BinaryCompression.ByteShuffleZstd;
        config.NumpressOverrides[CVID.MS_intensity_array] = BinaryNumpress.Slof;
        config.NumpressSlofErrorTolerance = 0;
        var zstd = new MzmlReader().Read(new MzmlWriter(config).Write(msd)).Run.SpectrumList!.GetSpectrum(0);

        AssertOnlyEncoding(zstd.GetMZArray()!, CVID.MS_dictionary_encoded_zstd_compression);
        AssertOnlyEncoding(zstd.BinaryDataArrays[2], CVID.MS_byte_shuffled_zstd_compression);
        AssertOnlyEncoding(zstd.GetIntensityArray()!, CVID.MS_MS_Numpress_short_logged_float_compression_followed_by_zstd_compression);
        AssertOnlyEncoding(zstd.IntegerDataArrays[0], CVID.MS_zstd_compression);
        CollectionAssert.AreEqual(expected.Mz, zstd.GetMZArray()!.Data, "dictionary-encoded m/z is lossless");
        CollectionAssert.AreEqual(expected.Mobility, zstd.BinaryDataArrays[2].Data, "byte-shuffled ion mobility is lossless");
        CollectionAssert.AreEqual(expected.Charge, zstd.IntegerDataArrays[0].Data, "zstd charge is lossless");
        for (int i = 0; i < expected.Intensity.Count; i++)
            Assert.AreEqual(expected.Intensity[i], zstd.GetIntensityArray()!.Data[i], Math.Max(1, expected.Intensity[i]) * 2e-4, "numpress slof + zstd");

        var zstdMsd = new MzmlReader().Read(new MzmlWriter(config).Write(msd));
        var zlib = new MzmlReader().Read(new MzmlWriter(new BinaryEncoderConfig { Compression = BinaryCompression.Zlib }).Write(zstdMsd))
            .Run.SpectrumList!.GetSpectrum(0);
        AssertOnlyEncoding(zlib.GetMZArray()!, CVID.MS_zlib_compression);
        AssertOnlyEncoding(zlib.GetIntensityArray()!, CVID.MS_zlib_compression);
        AssertOnlyEncoding(zlib.IntegerDataArrays[0], CVID.MS_zlib_compression);
        CollectionAssert.AreEqual(expected.Mz, zlib.GetMZArray()!.Data, "zstd m/z re-written as zlib");
    }

    /// <summary>
    /// cpp picks the dictionary's index width with <c>distinct &lt;= max</c> when encoding and
    /// <c>distinct &lt; max</c> when decoding, so the rules disagree at exactly 255 and 65535
    /// values. The writer pads those dictionaries by one unused value so every reader agrees on
    /// the width. Each boundary is also tried with 0.0 among the values, so the pad lands
    /// mid-dictionary rather than at the front.
    /// </summary>
    [TestMethod]
    public void Dictionary_WritesNoWidthTheCppRulesDisagreeOn()
    {
        var cases = new[] { (1, 1, 1), (254, 254, 1), (255, 256, 2), (256, 256, 2), (65534, 65534, 2), (65535, 65536, 4), (65536, 65536, 4) };
        foreach (var (distinct, written, indexSize) in cases)
        {
            foreach (double first in new[] { 100.0, 0.0 })
            {
                foreach (var precision in new[] { BinaryPrecision.Bits64, BinaryPrecision.Bits32 })
                {
                    int valueSize = precision == BinaryPrecision.Bits64 ? 8 : 4;
                    int count = distinct + 3;
                    var data = Enumerable.Range(0, count).Select(i => i % distinct == 0 ? first : 100.0 + (i % distinct) * 0.5).ToArray();
                    var encoder = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.DictZstd, Precision = precision });
                    byte[] compressed = encoder.EncodeToBytes(data, out _);
                    byte[] encoded = ZstdCodec.Decompress(compressed);

                    string label = $"{distinct} distinct starting {first}, {precision}";
                    ulong header = BinaryPrimitives.ReadUInt64LittleEndian(encoded.AsSpan(8, 8));
                    Assert.AreEqual((ulong)written, header, label);
                    Assert.AreEqual(CppEncoderIndexSize(header), CppDecoderIndexSize(header), label);
                    Assert.AreEqual(16 + written * valueSize + count * indexSize, encoded.Length, label);
                    CollectionAssert.AreEqual(data, encoder.DecodeDoublesFromRawBytes(compressed), label);
                }
            }
        }
    }

    /// <summary>
    /// cpp's encoder wrote a 255-value dictionary with 1-byte indices, which cpp's own decoder
    /// misread. The port decodes by the encoder's rule, so those files read correctly.
    /// </summary>
    [TestMethod]
    public void Dictionary_ReadsCppBoundaryOutput()
    {
        const int distinct = 255;
        var data = Enumerable.Range(0, distinct + 2).Select(i => 100.0 + (i % distinct) * 0.5).ToArray();
        var dictionary = data.Take(distinct).Select(BitConverter.DoubleToUInt64Bits).Order().ToArray();
        var encoded = new byte[16 + distinct * 8 + data.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(0, 8), 16 + distinct * 8);
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(8, 8), distinct);
        for (int i = 0; i < distinct; i++)
        {
            for (int b = 0; b < 8; b++)
                encoded[16 + b * distinct + i] = (byte)(dictionary[i] >> (8 * b));
        }
        for (int i = 0; i < data.Length; i++)
            encoded[16 + distinct * 8 + i] = (byte)Array.IndexOf(dictionary, BitConverter.DoubleToUInt64Bits(data[i]));

        var encoder = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.DictZstd });
        CollectionAssert.AreEqual(data, encoder.DecodeDoublesFromRawBytes(ZstdCodec.Compress(encoded)));
    }

    /// <summary>Empty arrays, int64 arrays and 32-bit integer decoding through every zstd codec.</summary>
    [TestMethod]
    public void RoundTrips_EmptyAndIntegerArrays()
    {
        long[] integers = { 0, 1, -1, long.MaxValue, long.MinValue, 42, 42, 7 };
        foreach (var compression in new[] { BinaryCompression.Zstd, BinaryCompression.ByteShuffleZstd, BinaryCompression.DictZstd })
        {
            var encoder = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = compression });
            Assert.AreEqual(0, encoder.DecodeDoubles(encoder.Encode(Array.Empty<double>())).Length, $"{compression} empty doubles");
            Assert.AreEqual(0, encoder.DecodeInt64(encoder.EncodeInt64(Array.Empty<long>())).Length, $"{compression} empty int64");
            CollectionAssert.AreEqual(integers, encoder.DecodeInt64(encoder.EncodeInt64(integers)), $"{compression} int64");

            var bits32 = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = compression, Precision = BinaryPrecision.Bits32 });
            string int32Encoded = bits32.Encode(Array.Empty<double>());
            Assert.AreEqual(0, bits32.DecodeIntegers(int32Encoded).Length, $"{compression} empty int32");
        }
    }

    /// <summary>
    /// Prediction is an mzMLb transform whose cvParam names zlib. The mzML writer must not stamp
    /// it on arrays it did not predict - with zstd that sent the reader to zlib - and the reader
    /// must let an explicit zstd cvParam win over the zlib its name implies.
    /// </summary>
    [TestMethod]
    public void Prediction_DoesNotTurnZstdIntoZlib()
    {
        var msd = ReadFixture();
        var expectedMz = msd.Run.SpectrumList!.GetSpectrum(0).GetMZArray()!.Data.ToList();
        var config = new BinaryEncoderConfig { Compression = BinaryCompression.Zstd };
        config.CompressionOverrides[CVID.MS_m_z_array] = BinaryCompression.ByteShuffleZstd;
        config.PredictionOverrides[CVID.MS_m_z_array] = BinaryPrediction.Linear;
        config.PredictionOverrides[CVID.MS_intensity_array] = BinaryPrediction.Delta;
        string xml = new MzmlWriter(config).Write(msd);

        var spectrum = new MzmlReader().Read(xml).Run.SpectrumList!.GetSpectrum(0);
        AssertOnlyEncoding(spectrum.GetMZArray()!, CVID.MS_byte_shuffled_zstd_compression);
        CollectionAssert.AreEqual(expectedMz, spectrum.GetMZArray()!.Data, "m/z written with a prediction override");

        // A prediction cvParam next to an explicit zstd one, as an mzMLb source carries it.
        string predicted = xml.Replace(
            "<cvParam cvRef=\"MS\" accession=\"MS:1003780\" name=\"zstd compression\" value=\"\" />",
            "<cvParam cvRef=\"MS\" accession=\"MS:1003780\" name=\"zstd compression\" value=\"\" />" +
            "<cvParam cvRef=\"MS\" accession=\"MS:1003089\" name=\"truncation, delta prediction and zlib compression\" value=\"\" />");
        Assert.AreNotEqual(xml, predicted, "the intensity array's zstd cvParam was found");
        var intensity = new MzmlReader().Read(predicted).Run.SpectrumList!.GetSpectrum(0).GetIntensityArray()!;
        Assert.AreEqual(1006, intensity.Data.Count);
        Assert.AreEqual(11.6745, intensity.Data[0], 1e-2);
    }

    /// <summary>
    /// Streaming zstd compressors do not record the content size in the frame, so the size the
    /// decompressor reports is only a bound. Other mzML writers may produce such frames.
    /// </summary>
    [TestMethod]
    public void Decompress_ReadsFramesWithoutContentSize()
    {
        var data = Enumerable.Range(0, 5000).Select(i => 100.0 + i * 0.25).ToArray();
        var raw = new byte[data.Length * 8];
        for (int i = 0; i < data.Length; i++)
            BinaryPrimitives.WriteDoubleLittleEndian(raw.AsSpan(i * 8), data[i]);
        using var streamed = new MemoryStream();
        using (var zstd = new ZstdSharp.CompressionStream(streamed, 3, leaveOpen: true))
            zstd.Write(raw);

        var encoder = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.Zstd });
        CollectionAssert.AreEqual(data, encoder.DecodeDoubles(Convert.ToBase64String(streamed.ToArray())));
    }

    /// <summary>A corrupt dictionary header whose value count overflows count * width must be
    /// reported as bad data, not crash on an out-of-range slice.</summary>
    [TestMethod]
    public void Dictionary_RejectsAnOverflowingHeader()
    {
        var encoded = new byte[24 + 4];
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(0, 8), 24);
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(8, 8), (1UL << 61) + 1);
        byte[] compressed = ZstdCodec.Compress(encoded);
        Assert.ThrowsException<InvalidDataException>(() => ZstdCodec.DictDecompress(compressed, 8));
    }

    /// <summary>Numpress output is a byte stream, not values, so only plain zstd can follow it.</summary>
    [TestMethod]
    public void Numpress_CombinesOnlyWithPlainZstd()
    {
        double[] data = { 100.1, 200.2, 300.3, 400.4 };
        var plain = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = BinaryCompression.Zstd, Numpress = BinaryNumpress.Linear });
        string encoded = plain.Encode(data, out _, out var actualNumpress);
        Assert.AreEqual(BinaryNumpress.Linear, actualNumpress);
        double[] decoded = plain.DecodeDoubles(encoded);
        for (int i = 0; i < data.Length; i++)
            Assert.AreEqual(data[i], decoded[i], 1e-6);

        foreach (var compression in new[] { BinaryCompression.ByteShuffleZstd, BinaryCompression.DictZstd })
        {
            var encoder = new BinaryDataEncoder(new BinaryEncoderConfig { Compression = compression, Numpress = BinaryNumpress.Linear });
            Assert.ThrowsException<InvalidOperationException>(() => encoder.Encode(data), $"{compression} encode");
            Assert.ThrowsException<InvalidOperationException>(() => encoder.DecodeDoubles(encoded), $"{compression} decode");
        }
    }

    /// <summary>cpp mzd.hpp encode_values: <c>distinct &lt;= max</c>.</summary>
    private static int CppEncoderIndexSize(ulong distinct) =>
        distinct <= byte.MaxValue ? 1 : distinct <= ushort.MaxValue ? 2 : 4;

    /// <summary>cpp mzd.hpp dictionary_decode: <c>distinct &lt; max</c>.</summary>
    private static int CppDecoderIndexSize(ulong distinct) =>
        distinct < byte.MaxValue ? 1 : distinct < ushort.MaxValue ? 2 : 4;

    private static MSData ReadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "mzml", "zstdIOTest.mzML");
        return new MzmlReader().Read(File.ReadAllText(path));
    }

    private static (List<double> Mz, List<double> Intensity, List<double> Mobility, List<long> Charge) CloneArrays(Spectrum spectrum)
    {
        return (spectrum.GetMZArray()!.Data.ToList(), spectrum.GetIntensityArray()!.Data.ToList(),
            spectrum.BinaryDataArrays[2].Data.ToList(), spectrum.IntegerDataArrays[0].Data.ToList());
    }

    /// <summary>Asserts <paramref name="array"/> carries <paramref name="expected"/> and no other
    /// compression cvParam.</summary>
    private static void AssertOnlyEncoding(ParamContainer array, CVID expected)
    {
        var compressions = array.CVParams.Select(p => p.Cvid).Where(IsCompressionCv).ToList();
        CollectionAssert.AreEqual(new[] { expected }, compressions,
            $"compression cvParams: {string.Join(", ", compressions)}");
    }

    private static bool IsCompressionCv(CVID cvid) =>
        cvid != CVID.MS_binary_data_compression_type && CvLookup.CvIsA(cvid, CVID.MS_binary_data_compression_type);
}
