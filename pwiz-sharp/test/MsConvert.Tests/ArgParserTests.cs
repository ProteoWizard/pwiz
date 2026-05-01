using Pwiz.Data.MsData.Encoding;
using Pwiz.Tools.MsConvert;

namespace Pwiz.Tools.MsConvert.Tests;

[TestClass]
public class ArgParserTests
{
    private static MsConvertConfig Invoke(params string[] args) => ArgParser.Parse(args);

    [TestMethod]
    public void Inputs_DefaultsAndMultiple_AndFilelist()
    {
        // Single input → mzML output, 64-bit precision, output to current directory.
        var single = Invoke("in.mzML");
        Assert.AreEqual(1, single.InputFiles.Count);
        Assert.AreEqual(OutputFormat.Mzml, single.Format);
        Assert.AreEqual(BinaryPrecision.Bits64, single.EncoderConfig.Precision);
        Assert.AreEqual(".", single.OutputPath);

        // Multiple positional inputs are accumulated.
        CollectionAssert.AreEqual(new[] { "a.mzML", "b.mzML", "c.mgf" },
            Invoke("a.mzML", "b.mzML", "c.mgf").InputFiles);

        // -f loads inputs from a file (skips comments + blank lines).
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tmp, new[] { "one.mzML", "# comment", "two.mzML", "", "three.mzML" });
            CollectionAssert.AreEqual(
                new[] { "one.mzML", "two.mzML", "three.mzML" },
                Invoke("-f", tmp).InputFiles);
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void OutputOptions_FormatPathCompressionPrecision()
    {
        // -o sets output dir; --mgf picks format; -z enables zlib; --32-bit drops precision.
        var c = Invoke("in.mzML", "-o", "/tmp/out", "--mgf", "-z", "--32-bit");
        Assert.AreEqual("/tmp/out", c.OutputPath);
        Assert.AreEqual(OutputFormat.Mgf, c.Format);
        Assert.AreEqual(BinaryCompression.Zlib, c.EncoderConfig.Compression);
        Assert.AreEqual(BinaryPrecision.Bits32, c.EncoderConfig.Precision);
        Assert.IsFalse(c.Verbose, "no -v flag");

        // -v turns verbose on.
        Assert.IsTrue(Invoke("in.mzML", "-v").Verbose);

        // -e overrides the output filename extension.
        Assert.AreEqual("mzml", Invoke("in.mzML", "-e", "mzml").OutputExtension);
    }

    [TestMethod]
    public void OutputFormats_AllRecognized()
    {
        // Each format flag selects its OutputFormat enum value.
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
    public void Filters_AccumulateInOrder()
    {
        // --filter is repeatable; -f is reserved for --filelist (matches pwiz cpp msconvert).
        var c = Invoke("in.mzML", "--filter", "msLevel 2-", "--filter", "scanTime 10-20");
        CollectionAssert.AreEqual(new[] { "msLevel 2-", "scanTime 10-20" }, c.Filters);
    }

    [TestMethod]
    public void PrecisionOverrides_PerArrayAndNumpress()
    {
        // --mz64 / --inten32 override precision per array type.
        var perArray = Invoke("in.mzML", "--mz64", "--inten32");
        Assert.AreEqual(BinaryPrecision.Bits64,
            perArray.EncoderConfig.PrecisionOverrides[Pwiz.Data.Common.Cv.CVID.MS_m_z_array]);
        Assert.AreEqual(BinaryPrecision.Bits32,
            perArray.EncoderConfig.PrecisionOverrides[Pwiz.Data.Common.Cv.CVID.MS_intensity_array]);

        // -n is shorthand for "numpress everything": Linear for m/z, Slof for intensity.
        var numpressAll = Invoke("in.mzML", "-n");
        Assert.AreEqual(BinaryNumpress.Linear,
            numpressAll.EncoderConfig.NumpressOverrides[Pwiz.Data.Common.Cv.CVID.MS_m_z_array]);
        Assert.AreEqual(BinaryNumpress.Slof,
            numpressAll.EncoderConfig.NumpressOverrides[Pwiz.Data.Common.Cv.CVID.MS_intensity_array]);

        // --numpressLinear takes an inline tolerance.
        Assert.AreEqual(1e-5,
            Invoke("in.mzML", "--numpressLinear", "1e-5").EncoderConfig.NumpressLinearErrorTolerance, 1e-12);
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
        // No arg → 1 (cpp parity); with int arg → that thread count.
        Assert.AreEqual(1, Invoke("in.mzML", "--singleThreaded").SingleThreaded);
        Assert.AreEqual(4, Invoke("in.mzML", "--singleThreaded", "4").SingleThreaded);
    }

    [TestMethod]
    public void ConfigFile_ReplayedAsOptions()
    {
        // -c FILE replays each line as if it were a command-line option.
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

    [TestMethod]
    public void ErrorPaths()
    {
        // Unknown option, no-input, and value-required-but-missing all throw ArgumentException.
        Assert.ThrowsException<ArgumentException>(() => Invoke("--notanoption"), "unknown option");
        Assert.ThrowsException<ArgumentException>(() => Invoke(), "no input files");
        Assert.ThrowsException<ArgumentException>(() => Invoke("in.mzML", "--filter"), "missing value");
    }
}
