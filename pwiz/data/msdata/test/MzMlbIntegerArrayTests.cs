using System.IO;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.MzMlb;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// IntegerDataArray round-trip through mzMLb. The vendor harness rarely exercises
/// integer arrays (most readers don't emit them at all — charge arrays show up
/// only in centroided / deconvolved spectra), so the 32-bit int dispatch in
/// <c>MzmlWriter.WriteIntegerDataArrayMzMlb</c> needs explicit coverage.
///
/// One test exercises both precisions in a single pass: an int32-precision
/// global config writes an IntegerDataArray with charge values through the
/// writer, the reader pulls it back, and we assert byte-for-byte recovery
/// (HDF5 promotes int32 -> int64 transparently on the read side).
/// </summary>
[TestClass]
public class MzMlbIntegerArrayTests
{
    [TestMethod]
    public void WriteRead_IntegerDataArray_Int32Precision_RoundTripsExactly()
    {
        var charges = new[] { 1L, 2L, 3L, 4L, -1L, 5L };
        var msd = BuildDocWithChargeArray(charges);

        // Configure global 32-bit precision: WriteIntegerDataArrayMzMlb dispatches
        // on global Precision (per-array overrides are for BinaryDataArray's m/z
        // / intensity CVIDs, not charge / deconvolution_id).
        var cfg = new BinaryEncoderConfig { Precision = BinaryPrecision.Bits32 };

        string path = Path.Combine(Path.GetTempPath(),
            $"mzmlb-int32-{System.Guid.NewGuid():N}.mzMLb");
        try
        {
            new MzMlbWriter(cfg).Write(msd, path);

            using (var rt = new MSData())
            {
                new MzMlbReaderAdapter().Read(path, rt);

                Assert.AreEqual(1, rt.Run.SpectrumList?.Count ?? 0);
                var spec = rt.Run.SpectrumList!.GetSpectrum(0, getBinaryData: true);
                Assert.AreEqual(1, spec.IntegerDataArrays.Count);
                CollectionAssert.AreEqual(charges, spec.IntegerDataArrays[0].Data);

                // The writer should have flagged the data as 32-bit on the binaryDataArray.
                Assert.IsTrue(spec.IntegerDataArrays[0].HasCVParam(CVID.MS_32_bit_integer),
                    "int32 precision config should emit MS_32_bit_integer cvParam");
            }
        }
        finally { File.Delete(path); }
    }

    private static MSData BuildDocWithChargeArray(long[] charges)
    {
        var msd = new MSData { Id = "int-test" };
        msd.CVs.AddRange(MSData.DefaultCVList);

        var sl = new SpectrumListSimple();
        var spec = new Spectrum
        {
            Index = 0,
            Id = "scan=1",
            DefaultArrayLength = charges.Length,
        };
        spec.Params.Set(CVID.MS_ms_level, 1);
        // Bare m/z + intensity for schema sanity (Reader_MzMlb expects them); the
        // assertion target is the IntegerDataArray we add alongside.
        spec.SetMZIntensityArrays(
            mz: new[] { 100.0, 200.0, 300.0, 400.0, 500.0, 600.0 },
            intensity: new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0 },
            CVID.MS_number_of_detector_counts);

        var chargeArr = new IntegerDataArray();
        chargeArr.Params.Set(CVID.MS_charge_array);
        chargeArr.Data.AddRange(charges);
        spec.IntegerDataArrays.Add(chargeArr);

        sl.Spectra.Add(spec);
        msd.Run.Id = "r";
        msd.Run.SpectrumList = sl;
        return msd;
    }
}
