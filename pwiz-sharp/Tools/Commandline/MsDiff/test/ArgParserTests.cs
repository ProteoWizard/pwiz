using Pwiz.Tools.MsDiff;

namespace Pwiz.Tools.MsDiff.Tests;

[TestClass]
public class ArgParserTests
{
    [TestMethod]
    public void Parse_OptionShapes()
    {
        // (args, expected precision, expected ignoreMetadata). cpp declares -i as zero_tokens,
        // so it never swallows the filename that follows it.
        var cases = new (string[] Args, double Precision, bool Ignore)[]
        {
            (new[] { "a.mzML", "b.mzML" },                            1e-6, false),
            (new[] { "-p", "1e-5", "a.mzML", "b.mzML" },              1e-5, false),
            (new[] { "--precision", "1e-5", "a.mzML", "b.mzML" },     1e-5, false),
            (new[] { "--precision=1e-5", "a.mzML", "b.mzML" },        1e-5, false),
            (new[] { "-i", "a.mzML", "b.mzML" },                      1e-6, true),
            (new[] { "--ignore", "a.mzML", "b.mzML" },                1e-6, true),
            (new[] { "-i", "-p", "0.5", "a.mzML", "b.mzML" },         0.5,  true),
            (new[] { "a.mzML", "-p", "2", "b.mzML" },                 2.0,  false),
        };

        foreach (var (args, precision, ignore) in cases)
        {
            string label = string.Join(' ', args);
            var c = ArgParser.Parse(args);
            Assert.AreEqual(precision, c.DiffConfig.Precision, label);
            Assert.AreEqual(ignore, c.DiffConfig.IgnoreMetadata, label);
            Assert.AreEqual("a.mzML", c.FileA, label);
            Assert.AreEqual("b.mzML", c.FileB, label);
        }
    }

    [TestMethod]
    public void Parse_Rejected()
    {
        // cpp throws its usage string unless it gets exactly two filenames.
        var bad = new[]
        {
            new[] { "only-one.mzML" },
            new[] { "a.mzML", "b.mzML", "c.mzML" },
            Array.Empty<string>(),
            new[] { "-p", "not-a-number", "a.mzML", "b.mzML" },
            new[] { "-p" },
            new[] { "--bogus", "a.mzML", "b.mzML" },
        };

        foreach (var args in bad)
            Assert.ThrowsException<ArgumentException>(() => ArgParser.Parse(args),
                string.Join(' ', args));
    }

    [TestMethod]
    public void Parse_HelpIsNotAnError()
    {
        foreach (var flag in new[] { "-h", "--help", "-?" })
            Assert.ThrowsException<ArgParseHelpRequested>(() => ArgParser.Parse(new[] { flag }), flag);
    }

    [TestMethod]
    public void Parse_PrecisionIsCultureInvariant()
    {
        // A comma-decimal culture must not turn "1e-5" into something else, or change what
        // "0.5" means. The container sets no particular culture.
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");
            Assert.AreEqual(0.5, ArgParser.Parse(new[] { "-p", "0.5", "a", "b" }).DiffConfig.Precision);
            Assert.AreEqual(1e-5, ArgParser.Parse(new[] { "-p", "1e-5", "a", "b" }).DiffConfig.Precision);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
