using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Encoding;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

[TestClass]
public class NumpressPerArrayTest
{
    [TestMethod]
    public void PerArrayNumpress_MzGetsLinear_IntensityUntouched()
    {
        var msd = BuildMsd();
        var cfg = new BinaryEncoderConfig();
        cfg.NumpressOverrides[CVID.MS_m_z_array] = BinaryNumpress.Linear;
        cfg.CompressionOverrides[CVID.MS_m_z_array] = BinaryCompression.Zlib;
        cfg.PrecisionOverrides[CVID.MS_m_z_array] = BinaryPrecision.Bits32;

        string xml = new MzmlWriter(cfg).Write(msd);

        // m/z array should have the numpress-linear + zlib CV term.
        Assert.IsTrue(
            xml.Contains("MS-Numpress linear prediction compression followed by zlib compression", StringComparison.Ordinal),
            "Expected MS-Numpress linear CV term in output; it was not found.");

        // Intensity array should NOT have numpress — just standard 64-bit + no-compression.
        // Confirm via a round-trip: the reader should report Numpress=None for intensity.
        var reparsed = new MzmlReader().Read(xml);
        var spec = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var intensity = spec.GetIntensityArray()!;
        Assert.IsFalse(intensity.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression));
        Assert.IsFalse(intensity.HasCVParam(CVID.MS_MS_Numpress_linear_prediction_compression_followed_by_zlib_compression));
    }

    private static MSData BuildMsd()
    {
        var msd = new MSData { Id = "test" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        msd.FileDescription.SourceFiles.Add(new SourceFile("sf1", "x.raw", "file:///."));
        var sw = new Software("sw") { Version = "1" };
        sw.Set(CVID.MS_pwiz);
        msd.Software.Add(sw);
        var ic = new InstrumentConfiguration("IC1");
        ic.ComponentList.Add(new Component(CVID.MS_electrospray_ionization, 1));
        ic.ComponentList.Add(new Component(CVID.MS_orbitrap, 2));
        ic.ComponentList.Add(new Component(CVID.MS_inductive_detector, 3));
        msd.InstrumentConfigurations.Add(ic);
        var dp = new DataProcessing("dp1");
        dp.ProcessingMethods.Add(new ProcessingMethod { Order = 1, Software = sw });
        msd.DataProcessings.Add(dp);
        var list = new SpectrumListSimple { Dp = dp };
        var spec = new Spectrum { Index = 0, Id = "s1", DefaultArrayLength = 4 };
        spec.Params.Set(CVID.MS_ms_level, 1);
        spec.SetMZIntensityArrays(
            new[] { 100.0, 200.0, 300.0, 400.0 },
            new[] { 50.0, 100.0, 150.0, 200.0 },
            CVID.MS_number_of_detector_counts);
        list.Spectra.Add(spec);
        msd.Run.Id = "r1";
        msd.Run.SpectrumList = list;
        return msd;
    }
}
