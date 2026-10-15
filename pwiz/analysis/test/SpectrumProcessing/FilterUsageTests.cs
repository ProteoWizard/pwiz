using Pwiz.Analysis;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

[TestClass]
public class FilterUsageTests
{
    [TestMethod]
    public void EveryRegisteredFilterHasHelp()
    {
        // msconvert's help is generated from these tables, so a filter registered without help
        // would be missing from it, and help for a filter that is not registered would document
        // one users cannot run.
        AssertSameNames(SpectrumListFactory.RegisteredNames, SpectrumListFactory.FilterUsages, "spectrum");
        AssertSameNames(ChromatogramListFactory.RegisteredNames, ChromatogramListFactory.FilterUsages, "chromatogram");
    }

    private static void AssertSameNames(IEnumerable<string> registered, IEnumerable<FilterUsage> usages, string kind)
    {
        var documented = usages.Select(u => u.Name.ToLowerInvariant()).ToList();
        Assert.AreEqual(documented.Count, documented.Distinct().Count(), $"a {kind} filter is documented twice");
        CollectionAssert.AreEquivalent(registered.OrderBy(n => n).ToList(), documented.OrderBy(n => n).ToList(),
            $"registered {kind} filters and documented {kind} filters differ");
    }

    [TestMethod]
    public void Usage_BriefListsNamesAndDetailedAddsDescriptions()
    {
        string brief = SpectrumListFactory.Usage(detailedHelp: false, moreHelpPrompt: "(more)");
        StringAssert.StartsWith(brief, "\nSpectrum List Filters\n=====================\n(more)\nindex <index_value_set>\nid <id_set>\n");
        Assert.IsFalse(brief.Contains("int_set' is a list", StringComparison.Ordinal), "brief help has no int_set explanation");

        string detailed = SpectrumListFactory.Usage(detailedHelp: true);
        StringAssert.Contains(detailed, "Many filters take 'int_set' arguments.");
        StringAssert.Contains(detailed,
            "msLevel <mslevels>\nThis filter selects only spectra with the indicated <mslevels>, expressed as an int_set.\n\n");
        StringAssert.EndsWith(detailed, "\n\n");

        StringAssert.StartsWith(ChromatogramListFactory.Usage(detailedHelp: false),
            "\nChromatogram List Filters\n=========================\nindex <index_value_set>\n");
    }

    [TestMethod]
    public void Usage_SingleFilterOrTheListOfNames()
    {
        Assert.AreEqual("sortByScanTime \nThis filter reorders spectra, sorting them by ascending scan start time.\n",
            SpectrumListFactory.Usage("sortByScanTime"));

        string unknown = SpectrumListFactory.Usage("noSuchFilter");
        StringAssert.StartsWith(unknown, "Invalid filter name: noSuchFilter\n\nSupported filters are:\nindex\nid\n");
        StringAssert.Contains(unknown, "\ndiaUmpire\n");
    }

    [TestMethod]
    public void Usage_WrapsLongLinesAtSpaces()
    {
        const int width = 60;
        foreach (string line in SpectrumListFactory.Usage(detailedHelp: true, maxLineLength: width).Split('\n'))
        {
            // A line may run long only where it has no space to break at within the width.
            if (line.Length > width)
                Assert.IsFalse(line[..width].Contains(' ', StringComparison.Ordinal), $"line not wrapped: \"{line}\"");
        }

        // No width means no wrapping.
        StringAssert.Contains(SpectrumListFactory.Usage(detailedHelp: true),
            "This filter selects spectra by scan number.  Depending on the input data type, scan number and spectrum index are not always the same thing");
    }
}
