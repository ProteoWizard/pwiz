using Pwiz.Data.MsData.Encoding;
using Pwiz.Tools.MsConvert;

namespace Pwiz.Tools.MsConvert.Tests;

[TestClass]
public class ArgParserTests
{
    [TestMethod]
    public void SingleInput_DefaultsToMzmlWith64Bit()
    {
        var c = Invoke("in.mzML");
        Assert.AreEqual(1, c.InputFiles.Count);
        Assert.AreEqual(OutputFormat.Mzml, c.Format);
        Assert.AreEqual(BinaryPrecision.Bits64, c.EncoderConfig.Precision);
        Assert.AreEqual(".", c.OutputPath);
    }

    [TestMethod]
    public void Filters_Accumulate()
    {
        // Both use --filter; -f is reserved for --filelist (matches pwiz C++ msconvert).
        var c = Invoke("in.mzML", "--filter", "msLevel 2-", "--filter", "scanTime 10-20");
        CollectionAssert.AreEqual(new[] { "msLevel 2-", "scanTime 10-20" }, c.Filters);
    }

    [TestMethod]
    public void OutputOptions_Apply()
    {
        var c = Invoke("in.mzML", "-o", "/tmp/out", "--mgf", "-z", "--32-bit");
        Assert.AreEqual("/tmp/out", c.OutputPath);
        Assert.AreEqual(OutputFormat.Mgf, c.Format);
        Assert.AreEqual(BinaryCompression.Zlib, c.EncoderConfig.Compression);
        Assert.AreEqual(BinaryPrecision.Bits32, c.EncoderConfig.Precision);
    }

    [TestMethod]
    public void Verbose_TurnsOnLogging()
    {
        var c = Invoke("in.mzML", "-v");
        Assert.IsTrue(c.Verbose);
    }

    [TestMethod]
    public void MultipleInputs_AllCaptured()
    {
        var c = Invoke("a.mzML", "b.mzML", "c.mgf");
        CollectionAssert.AreEqual(new[] { "a.mzML", "b.mzML", "c.mgf" }, c.InputFiles);
    }

    [TestMethod]
    public void UnknownOption_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => Invoke("--notanoption"));
    }

    [TestMethod]
    public void NoInputFiles_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => Invoke());
    }

    [TestMethod]
    public void OptionRequiringValue_WithoutValue_Throws()
    {
        Assert.ThrowsException<ArgumentException>(() => Invoke("in.mzML", "--filter"));
    }

    [TestMethod]
    public void Filelist_LoadsPathsFromFile()
    {
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tmp, new[] { "one.mzML", "# comment", "two.mzML", "", "three.mzML" });
            var c = Invoke("-f", tmp);
            CollectionAssert.AreEqual(new[] { "one.mzML", "two.mzML", "three.mzML" }, c.InputFiles);
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void PerArrayPrecision_OverridesApplied()
    {
        var c = Invoke("in.mzML", "--mz64", "--inten32");
        Assert.AreEqual(BinaryPrecision.Bits64,
            c.EncoderConfig.PrecisionOverrides[Pwiz.Data.Common.Cv.CVID.MS_m_z_array]);
        Assert.AreEqual(BinaryPrecision.Bits32,
            c.EncoderConfig.PrecisionOverrides[Pwiz.Data.Common.Cv.CVID.MS_intensity_array]);
    }

    [TestMethod]
    public void NumpressAll_SetsLinearAndSlof()
    {
        var c = Invoke("in.mzML", "-n");
        Assert.AreEqual(BinaryNumpress.Linear,
            c.EncoderConfig.NumpressOverrides[Pwiz.Data.Common.Cv.CVID.MS_m_z_array]);
        Assert.AreEqual(BinaryNumpress.Slof,
            c.EncoderConfig.NumpressOverrides[Pwiz.Data.Common.Cv.CVID.MS_intensity_array]);
    }

    [TestMethod]
    public void NumpressLinear_WithInlineTolerance()
    {
        // "--numpressLinear 1e-5" — the numeric argument should override the default tolerance.
        var c = Invoke("in.mzML", "--numpressLinear", "1e-5");
        Assert.AreEqual(1e-5, c.EncoderConfig.NumpressLinearErrorTolerance, 1e-12);
    }

    [TestMethod]
    public void OutputFormats_AllRecognized()
    {
        Assert.AreEqual(OutputFormat.MzXml, Invoke("x", "--mzXML").Format);
        Assert.AreEqual(OutputFormat.Mz5, Invoke("x", "--mz5").Format);
        Assert.AreEqual(OutputFormat.MzMLb, Invoke("x", "--mzMLb").Format);
        Assert.AreEqual(OutputFormat.Text, Invoke("x", "--text").Format);
        Assert.AreEqual(OutputFormat.Ms1, Invoke("x", "--ms1").Format);
        Assert.AreEqual(OutputFormat.Cms1, Invoke("x", "--cms1").Format);
        Assert.AreEqual(OutputFormat.Ms2, Invoke("x", "--ms2").Format);
        Assert.AreEqual(OutputFormat.Cms2, Invoke("x", "--cms2").Format);
    }

    [TestMethod]
    public void BooleanToggles_AllTriggerable()
    {
        var c = Invoke("in.mzML",
            "--noindex", "--gzip", "--merge",
            "--simAsSpectra", "--srmAsSpectra", "--combineIonMobilitySpectra",
            "--ddaProcessing", "--ignoreCalibrationScans",
            "--acceptZeroLengthSpectra", "--ignoreMissingZeroSamples", "--ignoreUnknownInstrumentError",
            "--stripLocationFromSourceFiles", "--stripVersionFromSoftware",
            "--continueOnError");

        Assert.IsTrue(c.NoIndex);
        Assert.IsTrue(c.Gzip);
        Assert.IsTrue(c.Merge);
        Assert.IsTrue(c.SimAsSpectra);
        Assert.IsTrue(c.SrmAsSpectra);
        Assert.IsTrue(c.CombineIonMobilitySpectra);
        Assert.IsTrue(c.DdaProcessing);
        Assert.IsTrue(c.IgnoreCalibrationScans);
        Assert.IsTrue(c.AcceptZeroLengthSpectra);
        Assert.IsTrue(c.IgnoreMissingZeroSamples);
        Assert.IsTrue(c.IgnoreUnknownInstrumentError);
        Assert.IsTrue(c.StripLocationFromSourceFiles);
        Assert.IsTrue(c.StripVersionFromSoftware);
        Assert.IsTrue(c.ContinueOnError);
    }

    [TestMethod]
    public void SingleThreaded_WithAndWithoutArg()
    {
        Assert.AreEqual(1, Invoke("in.mzML", "--singleThreaded").SingleThreaded);
        Assert.AreEqual(4, Invoke("in.mzML", "--singleThreaded", "4").SingleThreaded);
    }

    [TestMethod]
    public void ExtOverride_UsedForOutputFilename()
    {
        var c = Invoke("in.mzML", "-e", "mzml");
        Assert.AreEqual("mzml", c.OutputExtension);
    }

    [TestMethod]
    public void ConfigFile_ReplayedAsOptions()
    {
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tmp, new[]
            {
                "# zlib compression + 32-bit precision via config file",
                "zlib",
                "32-bit",
                "filter=msLevel 1",
            });
            var c = Invoke("in.mzML", "-c", tmp);
            Assert.AreEqual(BinaryCompression.Zlib, c.EncoderConfig.Compression);
            Assert.AreEqual(BinaryPrecision.Bits32, c.EncoderConfig.Precision);
            CollectionAssert.Contains(c.Filters, "msLevel 1");
        }
        finally { File.Delete(tmp); }
    }

    private static MsConvertConfig Invoke(params string[] args) => ArgParser.Parse(args);
}
