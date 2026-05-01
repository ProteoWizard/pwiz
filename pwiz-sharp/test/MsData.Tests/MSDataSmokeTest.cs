using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Samples;
using Pwiz.Data.MsData.Sources;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

[TestClass]
public class MSDataSmokeTest
{
    [TestMethod]
    public void Empty_AndDefaultCVList()
    {
        Assert.IsTrue(new MSData().IsEmpty, "fresh document is empty");

        var defaultCvs = MSData.DefaultCVList;
        Assert.AreEqual(2, defaultCvs.Count);
        Assert.IsTrue(defaultCvs.Any(cv => cv.Id == "MS"));
        Assert.IsTrue(defaultCvs.Any(cv => cv.Id == "UO"));
    }

    [TestMethod]
    public void BuildSyntheticDocument_EndToEnd()
    {
        // Construct a minimal document end-to-end and verify the binary peak arrays round-trip.
        var msd = new MSData { Id = "synthetic" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        msd.FileDescription.SourceFiles.Add(new SourceFile("sf1", "data.raw", "file:///data/data.raw"));
        msd.Samples.Add(new Sample("s1", "sample-1"));

        var sw = new Software("pwiz-sharp") { Version = "0.0.1" };
        sw.Set(CVID.MS_pwiz);
        msd.Software.Add(sw);

        var instr = new InstrumentConfiguration("inst1");
        instr.ComponentList.Add(new Component(CVID.MS_electrospray_ionization, 1));
        instr.ComponentList.Add(new Component(CVID.MS_orbitrap, 2));
        instr.ComponentList.Add(new Component(CVID.MS_inductive_detector, 3));
        instr.Software = sw;
        msd.InstrumentConfigurations.Add(instr);

        var dp = new DataProcessing("dp1");
        dp.ProcessingMethods.Add(new ProcessingMethod { Order = 1, Software = sw });
        msd.DataProcessings.Add(dp);

        var spectrumList = new SpectrumListSimple { Dp = dp };
        var spec = new Spectrum
        {
            Index = 0,
            Id = "controllerType=0 controllerNumber=1 scan=1",
            DefaultArrayLength = 3,
        };
        spec.Params.Set(CVID.MS_ms_level, 1);
        spec.SetMZIntensityArrays(
            new[] { 100.0, 200.0, 300.0 },
            new[] { 1000.0, 2500.0, 700.0 },
            CVID.MS_number_of_detector_counts);
        spectrumList.Spectra.Add(spec);

        msd.Run.Id = "run-1";
        msd.Run.DefaultInstrumentConfiguration = instr;
        msd.Run.SpectrumList = spectrumList;

        Assert.IsFalse(msd.IsEmpty);
        Assert.AreEqual(1, msd.Run.SpectrumList.Count);

        var pairs = new List<MZIntensityPair>();
        ((Spectrum)msd.Run.SpectrumList.GetSpectrum(0)).GetMZIntensityPairs(pairs);
        Assert.AreEqual(3, pairs.Count);
        Assert.AreEqual(200.0, pairs[1].Mz, 1e-9);
        Assert.AreEqual(2500.0, pairs[1].Intensity, 1e-9);
    }

    [TestMethod]
    public void Id_ParseAbbreviateAndTranslate()
    {
        // Parse splits "key=value key=value" into a dictionary; Value() looks up a single key.
        var map = Id.Parse("controllerType=0 controllerNumber=1 scan=123");
        Assert.AreEqual("0", map["controllerType"]);
        Assert.AreEqual("123", map["scan"]);
        Assert.AreEqual("123", Id.Value("controllerType=0 scan=123 foo=bar", "scan"));
        Assert.AreEqual(string.Empty, Id.Value("a=1", "missing"), "missing key returns empty");

        // Abbreviate keeps only the values, joined with '.'.
        Assert.AreEqual("1.1.123.2", Id.Abbreviate("sample=1 period=1 cycle=123 experiment=2"));

        // Thermo native ID translation round-trips between scan number ↔ native ID.
        string native = Id.TranslateScanNumberToNativeId(CVID.MS_Thermo_nativeID_format, "42");
        Assert.AreEqual("controllerType=0 controllerNumber=1 scan=42", native);
        Assert.AreEqual("42", Id.TranslateNativeIdToScanNumber(CVID.MS_Thermo_nativeID_format, native));
    }

    [TestMethod]
    public void SpectrumList_AndComponentClassification()
    {
        // SpectrumListSimple.IsEmpty considers Dp population.
        var sl = new SpectrumListSimple();
        Assert.IsTrue(sl.IsEmpty, "empty list with no Dp");
        sl.Dp = new DataProcessing("dp");
        Assert.IsFalse(sl.IsEmpty, "list with Dp");

        // Component CVID auto-classifies as Source / Analyzer / Detector; ComponentList exposes
        // Nth-of-type accessors.
        Assert.AreEqual(ComponentType.Analyzer, new Component(CVID.MS_orbitrap, 2).Type);
        var list = new ComponentList
        {
            new(CVID.MS_electrospray_ionization, 1),
            new(CVID.MS_orbitrap, 2),
            new(CVID.MS_inductive_detector, 3),
        };
        Assert.AreEqual(ComponentType.Source, list.Source(0).Type);
        Assert.AreEqual(ComponentType.Analyzer, list.Analyzer(0).Type);
        Assert.AreEqual(ComponentType.Detector, list.Detector(0).Type);
    }
}
