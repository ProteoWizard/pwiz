using Pwiz.Data.MsData;
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
        Assert.AreEqual(WriteFormat.Mzml, single.WriteConfig.Format);
        Assert.AreEqual(BinaryPrecision.Bits64, single.WriteConfig.EncoderConfig.Precision);
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
        Assert.AreEqual(WriteFormat.Mgf, c.WriteConfig.Format);
        Assert.AreEqual(BinaryCompression.Zlib, c.WriteConfig.EncoderConfig.Compression);
        Assert.AreEqual(BinaryPrecision.Bits32, c.WriteConfig.EncoderConfig.Precision);
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
        Assert.AreEqual(WriteFormat.MzXml, Invoke("x", "--mzXML").WriteConfig.Format);
        Assert.AreEqual(WriteFormat.Mz5, Invoke("x", "--mz5").WriteConfig.Format);
        Assert.AreEqual(WriteFormat.MzMLb, Invoke("x", "--mzMLb").WriteConfig.Format);
        Assert.AreEqual(WriteFormat.Text, Invoke("x", "--text").WriteConfig.Format);
        Assert.AreEqual(WriteFormat.Ms1, Invoke("x", "--ms1").WriteConfig.Format);
        Assert.AreEqual(WriteFormat.Cms1, Invoke("x", "--cms1").WriteConfig.Format);
        Assert.AreEqual(WriteFormat.Ms2, Invoke("x", "--ms2").WriteConfig.Format);
        Assert.AreEqual(WriteFormat.Cms2, Invoke("x", "--cms2").WriteConfig.Format);
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
            perArray.WriteConfig.EncoderConfig.PrecisionOverrides[Pwiz.Data.Common.Cv.CVID.MS_m_z_array]);
        Assert.AreEqual(BinaryPrecision.Bits32,
            perArray.WriteConfig.EncoderConfig.PrecisionOverrides[Pwiz.Data.Common.Cv.CVID.MS_intensity_array]);

        // -n is shorthand for "numpress everything": Linear for m/z, Slof for intensity.
        var numpressAll = Invoke("in.mzML", "-n");
        Assert.AreEqual(BinaryNumpress.Linear,
            numpressAll.WriteConfig.EncoderConfig.NumpressOverrides[Pwiz.Data.Common.Cv.CVID.MS_m_z_array]);
        Assert.AreEqual(BinaryNumpress.Slof,
            numpressAll.WriteConfig.EncoderConfig.NumpressOverrides[Pwiz.Data.Common.Cv.CVID.MS_intensity_array]);

        // --numpressLinear takes an inline tolerance.
        Assert.AreEqual(1e-5,
            Invoke("in.mzML", "--numpressLinear", "1e-5").WriteConfig.EncoderConfig.NumpressLinearErrorTolerance, 1e-12);
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

        Assert.IsTrue(!c.WriteConfig.Indexed);
        Assert.IsTrue(c.WriteConfig.Gzip);
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
    public void VerboseProgressPeriod_DefaultsAndOverrides()
    {
        // Default keeps the tight console cadence; a host with a slow progress sink raises it.
        Assert.AreEqual(100, Invoke("in.mzML").VerboseProgressPeriod);
        Assert.AreEqual(2500, Invoke("in.mzML", "--verboseProgressPeriod", "2500").VerboseProgressPeriod);
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
            Assert.AreEqual(BinaryCompression.Zlib, c.WriteConfig.EncoderConfig.Compression);
            Assert.AreEqual(BinaryPrecision.Bits32, c.WriteConfig.EncoderConfig.Precision);
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

    [TestMethod]
    public void RunIndexSet_StoresIntervalString()
    {
        // Stored verbatim; Converter parses it via IntegerSet.Parse() at Run() time.
        Assert.AreEqual("0-3", Invoke("in.wiff", "--runIndexSet", "0-3").RunIndexSet);
        Assert.AreEqual("[1,5] 7", Invoke("in.wiff", "--runIndexSet", "[1,5] 7").RunIndexSet);
        // Default is null (no filtering).
        Assert.IsNull(Invoke("in.wiff").RunIndexSet);
    }

    [TestMethod]
    public void Usage_EndsWithTheCppVersionBlock()
    {
        // informational version (as Directory.Build.targets stamps it), assembly version -> release
        var cases = new (string? Informational, Version? Assembly, string Expected)[]
        {
            ("4.0.26281-92536b2 (automated build)", new Version(4, 0, 26281, 0), "4.0.26281 (92536b2)"),
            ("4.0.26281-92536b2 (developer build)", new Version(4, 0, 26281, 0), "4.0.26281 (92536b2)"),
            ("4.0.26281-0 (developer build)", new Version(4, 0, 26281, 0), "4.0.26281 (0)"),
            ("4.0.0", new Version(4, 0, 0, 0), "4.0.0 (0)"),
            (null, new Version(4, 0, 26281, 0), "4.0.26281 (0)"),
            (null, null, "0.0.0 (0)"),
        };
        foreach (var (informational, assembly, expected) in cases)
            Assert.AreEqual(expected, VersionInfo.FormatRelease(informational, assembly), informational ?? "(none)");

        // The usage text ends with the two lines the C++ msconvert printed, and its release line
        // is in the shape the Docker image publish parses into a version tag.
        string[] lines = ArgParser.Usage().Split('\n');
        Assert.AreEqual(string.Empty, lines[^3]);
        StringAssert.Matches(lines[^2], new System.Text.RegularExpressions.Regex(
            @"^ProteoWizard release: [0-9]+\.[0-9]+\.[0-9]+ \([0-9a-z]+\)$"));
        StringAssert.StartsWith(lines[^1], "Build date: ");
    }

    private static ArgParser.HelpRequest HelpFor(params string[] args) =>
        Assert.ThrowsException<ArgParseHelpRequested>(() => Invoke(args)).Request;

    [TestMethod]
    public void Help_EveryOptionIsDocumented()
    {
        // The parser and the help read the same option table; check the help shows every option
        // under its own name (aliases such as --32-bit and -h are accepted but not shown).
        string usage = ArgParser.Usage();
        string[] undocumented = { "--mzml", "--MGF", "--output-path", "--32-bit", "--32bit", "--64-bit", "--64bit", "--runIndex", "-h" };
        foreach (string name in ArgParser.OptionNames.Except(undocumented))
        {
            // "  --name", or "  -n [ --name ]" for an option with a short name
            bool shown = name.StartsWith("--", StringComparison.Ordinal)
                ? usage.Contains("\n  " + name, StringComparison.Ordinal) || usage.Contains($" [ {name} ]", StringComparison.Ordinal)
                : usage.Contains($"\n  {name} [ --", StringComparison.Ordinal);
            Assert.IsTrue(shown, $"{name} is missing from the help");
        }

        // boost::program_options' synopsis forms, as the C++ msconvert printed them.
        StringAssert.Contains(usage, "\n  -f [ --filelist ] arg ");
        StringAssert.Contains(usage, "\n  -o [ --outdir ] arg (=.) ");
        StringAssert.Contains(usage, "\n  --mzMLbChunkSize arg (=1048576) ");
        StringAssert.Contains(usage, "\n  --numpressLinear [arg(=2.0e-09)] ");
        StringAssert.Contains(usage, "\n  --mzML ");
    }

    [TestMethod]
    public void Help_FlagsSelectTheText()
    {
        Assert.IsTrue(HelpFor("--help").Detailed);
        Assert.IsTrue(HelpFor("-h").Detailed);
        Assert.IsTrue(HelpFor("in.mzML", "--doc").Doc, "inputs alongside a help flag still get help");
        Assert.AreEqual("msLevel", HelpFor("--help-filter", "msLevel").FilterName);

        // --help: details for every filter, ending with the version block.
        string help = ArgParser.HelpText(HelpFor("--help"));
        StringAssert.Contains(help, "Spectrum List Filters\n=====================\n\nNote: Filters are applied sequentially");
        StringAssert.Contains(help, "\nProteoWizard release: ");
        Assert.IsFalse(help.Contains("Examples:", StringComparison.Ordinal));

        // --doc: what the build writes to msconvert-help.txt, so no version or build date.
        string doc = ArgParser.HelpText(HelpFor("--doc"));
        StringAssert.StartsWith(doc, "Usage: msconvert [options] <input files>\n");
        StringAssert.Contains(doc, "\nmzShift <m/z shift as number and units (e.g. 10ppm or 1Da)> [msLevels=int_set (1-)]\n");
        StringAssert.Contains(doc, "\n\nExamples:\n\n# convert data.RAW to data.mzML\n");
        StringAssert.EndsWith(doc, "\nQuestions, comments, and bug reports:\nhttps://github.com/ProteoWizard\nsupport@proteowizard.org\n");
        Assert.IsFalse(doc.Contains("ProteoWizard release:", StringComparison.Ordinal));
        Assert.IsFalse(doc.Contains("Build date:", StringComparison.Ordinal));

        // --show-examples alone prints only the examples; with --doc it is part of --doc.
        StringAssert.StartsWith(ArgParser.HelpText(HelpFor("--show-examples")), "Examples:\n");
        Assert.AreEqual(doc, ArgParser.HelpText(HelpFor("--show-examples", "--doc")));

        // --help-filter: that filter's details, or the filter names for an unknown one.
        StringAssert.StartsWith(ArgParser.HelpText(HelpFor("--help-filter", "msLevel")),
            "msLevel <mslevels>\nThis filter selects only spectra");
        StringAssert.StartsWith(ArgParser.HelpText(HelpFor("--help-filter", "bogus")), "Invalid filter name: bogus\n");
    }

    [TestMethod]
    public void Help_WrapsToTheConsoleWidth()
    {
        // --help wraps descriptions under their column; --doc never wraps.
        string[] lines = ArgParser.Usage(detailedHelp: true, maxLineLength: 80).Split('\n');
        int continuation = Array.FindIndex(lines, l => l.StartsWith("  --stripLocationFromSourceFiles", StringComparison.Ordinal)) + 1;
        Assert.IsTrue(lines[continuation].StartsWith("                    ", StringComparison.Ordinal), lines[continuation]);
        foreach (string line in lines.SkipWhile(l => l != "Options:").TakeWhile(l => l.Length > 0))
            Assert.IsTrue(line.Length <= 80, $"option line not wrapped: \"{line}\"");

        Assert.IsTrue(ArgParser.Usage(writeDoc: true, maxLineLength: 80).Split('\n').Any(l => l.Length > 200));
    }

    [TestMethod]
    public void ConfigFile_SwitchValues()
    {
        // As in the --doc example config file: "mzXML=true" turns a switch on, "=false" leaves it off.
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tmp, new[] { "mzXML=true", "zlib=true", "gzip=false", "filter=index [3,7]" });
            var c = Invoke("in.mzML", "-c", tmp);
            CollectionAssert.AreEqual(new[] { "in.mzML" }, c.InputFiles, "switch values are not input files");
            Assert.AreEqual(WriteFormat.MzXml, c.WriteConfig.Format);
            Assert.AreEqual(BinaryCompression.Zlib, c.WriteConfig.EncoderConfig.Compression);
            Assert.IsFalse(c.WriteConfig.Gzip);
            CollectionAssert.AreEqual(new[] { "index [3,7]" }, c.Filters);
        }
        finally { File.Delete(tmp); }
    }
}
