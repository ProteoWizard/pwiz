using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Tools.MsConvert;

namespace Pwiz.Tools.MsConvert.Tests;

[TestClass]
public class ConverterTests
{
    private string _tempDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "msconvert-sharp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ---- Build a small mzML on disk to act as input ----

    private string WriteSmallMzmlInput()
    {
        var msd = new MSData { Id = "roundtrip_src" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        var list = new SpectrumListSimple();

        // MS1 spectrum
        var ms1 = new Spectrum { Index = 0, Id = "scan=1", DefaultArrayLength = 3 };
        ms1.Params.Set(CVID.MS_ms_level, 1);
        ms1.Params.Set(CVID.MS_positive_scan);
        ms1.Params.Set(CVID.MS_spectrum_title, "ms1");
        var s1 = new Scan();
        s1.Set(CVID.MS_scan_start_time, 10.0, CVID.UO_second);
        ms1.ScanList.Scans.Add(s1);
        ms1.SetMZIntensityArrays(new[] { 100.0, 200.0, 300.0 }, new[] { 10.0, 100.0, 20.0 }, CVID.MS_number_of_detector_counts);
        list.Spectra.Add(ms1);

        // MS2 spectrum with precursor
        var ms2 = new Spectrum { Index = 1, Id = "scan=2", DefaultArrayLength = 3 };
        ms2.Params.Set(CVID.MS_ms_level, 2);
        ms2.Params.Set(CVID.MS_positive_scan);
        ms2.Params.Set(CVID.MS_spectrum_title, "ms2");
        var s2 = new Scan();
        s2.Set(CVID.MS_scan_start_time, 20.0, CVID.UO_second);
        ms2.ScanList.Scans.Add(s2);
        var p = new Precursor();
        var si = new SelectedIon();
        si.Set(CVID.MS_selected_ion_m_z, 500.0, CVID.MS_m_z);
        si.Set(CVID.MS_charge_state, 2);
        p.SelectedIons.Add(si);
        ms2.Precursors.Add(p);
        ms2.SetMZIntensityArrays(new[] { 150.0, 250.0, 350.0 }, new[] { 50.0, 500.0, 150.0 }, CVID.MS_number_of_detector_counts);
        list.Spectra.Add(ms2);

        msd.Run.Id = "run";
        msd.Run.SpectrumList = list;

        string path = Path.Combine(_tempDir, "input.mzML");
        File.WriteAllText(path, new MzmlWriter().Write(msd));
        return path;
    }

    // ---- end-to-end cases ----

    [TestMethod]
    public void Run_MzmlPassthrough_PreservesSpectra()
    {
        string input = WriteSmallMzmlInput();
        string outDir = Path.Combine(_tempDir, "out");

        var cfg = new MsConvertConfig { OutputPath = outDir };
        cfg.InputFiles.Add(input);

        int converted = new Converter(cfg).Run();
        Assert.AreEqual(1, converted);

        string outputPath = Path.Combine(outDir, "input.mzML");
        Assert.IsTrue(File.Exists(outputPath));

        var reparsed = new MzmlReader().Read(File.ReadAllText(outputPath));
        Assert.AreEqual(2, reparsed.Run.SpectrumList!.Count);
    }

    [TestMethod]
    public void Run_MzmlToMgf_EmitsMgf()
    {
        string input = WriteSmallMzmlInput();
        string outDir = Path.Combine(_tempDir, "out");

        var cfg = new MsConvertConfig { OutputPath = outDir, Format = OutputFormat.Mgf };
        cfg.InputFiles.Add(input);

        new Converter(cfg).Run();

        string outputPath = Path.Combine(outDir, "input.mgf");
        Assert.IsTrue(File.Exists(outputPath));

        string mgf = File.ReadAllText(outputPath);
        Assert.IsTrue(mgf.Contains("BEGIN IONS", StringComparison.Ordinal), "MGF should have BEGIN IONS");
        // MS1 is not writable in MGF; only the MS2 spectrum should show up.
        Assert.AreEqual(1, mgf.Split("BEGIN IONS").Length - 1);
    }

    [TestMethod]
    public void Run_Ms2OnlyFilter_ChainedWithMzmlOutput()
    {
        string input = WriteSmallMzmlInput();
        string outDir = Path.Combine(_tempDir, "out");

        var cfg = new MsConvertConfig { OutputPath = outDir };
        cfg.InputFiles.Add(input);
        cfg.Filters.Add("msLevel 2-");

        new Converter(cfg).Run();

        var output = new MzmlReader().Read(File.ReadAllText(Path.Combine(outDir, "input.mzML")));
        Assert.AreEqual(1, output.Run.SpectrumList!.Count);
        var spec = output.Run.SpectrumList.GetSpectrum(0);
        Assert.AreEqual(2, spec.Params.CvParam(CVID.MS_ms_level).ValueAs<int>());
    }

    [TestMethod]
    public void Run_ThresholdAndMetadataFixer_ChainedInOrder()
    {
        string input = WriteSmallMzmlInput();
        string outDir = Path.Combine(_tempDir, "out");

        var cfg = new MsConvertConfig { OutputPath = outDir };
        cfg.InputFiles.Add(input);
        cfg.Filters.Add("threshold absolute 40"); // drops intensities < 40
        cfg.Filters.Add("metadataFixer");

        new Converter(cfg).Run();

        var output = new MzmlReader().Read(File.ReadAllText(Path.Combine(outDir, "input.mzML")));
        // MS1 peaks after threshold: {100,200,300} intensities {10,100,20} → only {200}.
        // MS2 peaks: {150,250,350} intensities {50,500,150} → all kept.
        var ms1 = output.Run.SpectrumList!.GetSpectrum(0, getBinaryData: true);
        var ms2 = output.Run.SpectrumList.GetSpectrum(1, getBinaryData: true);
        Assert.AreEqual(1, ms1.GetMZArray()!.Data.Count);
        Assert.AreEqual(3, ms2.GetMZArray()!.Data.Count);
        // Base peak intensity recomputed on MS1 → 100.
        Assert.AreEqual(100.0, ms1.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void Run_MissingInput_ReportsErrorButContinues()
    {
        var cfg = new MsConvertConfig { OutputPath = Path.Combine(_tempDir, "out") };
        cfg.InputFiles.Add(Path.Combine(_tempDir, "does-not-exist.mzML"));

        var logBuf = new StringWriter();
        int converted = new Converter(cfg, logBuf).Run();
        Assert.AreEqual(0, converted);
        StringAssert.Contains(logBuf.ToString(), "error converting", StringComparison.Ordinal);
    }
}
