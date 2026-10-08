using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Tools.MsDiff;

namespace Pwiz.Tools.MsDiff.Tests;

[TestClass]
public class DifferTests
{
    private string _tempDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "msdiff-sharp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void SameFileTwice_ReportsNothingAndExitsZero()
    {
        string a = WriteMzml("a", intensity: 100.0);

        var (exit, output) = Run("-p", "1e-5", a, a);

        Assert.AreEqual(0, exit, output);
        StringAssert.Contains(output, "spectrumList (0 spectra)", output);
        StringAssert.Contains(output, "chromatogramList (0 chromatograms)", output);
    }

    /// <summary>
    /// Two files holding the same data under different names still differ in <c>id</c> and in
    /// their sourceFile entries, so the exit code is 1 — the same thing that happens comparing
    /// a reference mzML against a freshly converted one. The counts are what say the DATA
    /// matched, which is why the container harness greps for those rather than for an empty
    /// report.
    /// </summary>
    [TestMethod]
    public void SameDataDifferentNames_DiffersOnlyInMetadata()
    {
        string a = WriteMzml("a", intensity: 100.0);
        string b = WriteMzml("b", intensity: 100.0);

        var (exit, output) = Run("-p", "1e-5", a, b);

        Assert.AreEqual(1, exit, output);
        StringAssert.Contains(output, "spectrumList (0 spectra)", output);
        StringAssert.Contains(output, "chromatogramList (0 chromatograms)", output);

        // -i drops the metadata, leaving nothing at all to report.
        var ignored = Run("-i", "-p", "1e-5", a, b);
        Assert.AreEqual(0, ignored.Exit, ignored.Output);
    }

    [TestMethod]
    public void DifferingIntensity_IsAttributedToTheSpectrum()
    {
        string a = WriteMzml("a", intensity: 100.0);
        string b = WriteMzml("b", intensity: 999.0);

        var (exit, output) = Run(a, b);

        Assert.AreEqual(1, exit, output);
        StringAssert.Contains(output, "spectrumList (1 differing spectra)", output);
        StringAssert.Contains(output, "chromatogramList (0 chromatograms)", output);
        // The detail report still names where it differed.
        StringAssert.Contains(output, "spectrum[0]", output);
    }

    [TestMethod]
    public void PrecisionGovernsWhetherASmallDeltaCounts()
    {
        string a = WriteMzml("a", intensity: 100.0);
        string b = WriteMzml("b", intensity: 100.0 + 1e-4);

        // Asserted on the spectrum count, not the exit code: these two always differ in
        // metadata (see SameDataDifferentNames_DiffersOnlyInMetadata), so only the count moves
        // with the tolerance. -i removes the metadata noise so the exit code tracks the data.
        var loose = Run("-i", "-p", "1e-2", a, b);
        StringAssert.Contains(loose.Output, "spectrumList (0 spectra)", loose.Output);
        Assert.AreEqual(0, loose.Exit, loose.Output);

        var tight = Run("-i", "-p", "1e-9", a, b);
        StringAssert.Contains(tight.Output, "spectrumList (1 differing spectra)", tight.Output);
        Assert.AreEqual(1, tight.Exit, tight.Output);
    }

    /// <summary>
    /// The container's vendor sweep decides pass/fail with
    /// <c>grep -q "0 spectra" &amp;&amp; grep -q "0 chromatograms"</c> over msdiff's stdout, so both
    /// lines have to be present in every run — including a run with no differences, where cpp
    /// prints nothing at all and the grep would fail.
    /// </summary>
    [TestMethod]
    public void SummaryLinesArePresentWhetherOrNotFilesDiffer()
    {
        string a = WriteMzml("a", intensity: 100.0);
        string same = WriteMzml("same", intensity: 100.0);
        string different = WriteMzml("different", intensity: 42.0);

        foreach (var other in new[] { same, different })
        {
            var (_, output) = Run(a, other);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.IsTrue(Array.Exists(lines, l => l.Contains("spectra", StringComparison.Ordinal)),
                "no spectra summary line for " + other);
            Assert.IsTrue(Array.Exists(lines, l => l.Contains("chromatograms", StringComparison.Ordinal)),
                "no chromatograms summary line for " + other);
        }
    }

    /// <summary>
    /// A spectrum-count mismatch must not report "0 spectra". The container harness treats
    /// that string as "the data matched", so reporting 0 here would turn the loudest possible
    /// difference into a silent pass.
    /// </summary>
    [TestMethod]
    public void SpectrumCountMismatch_IsNotReportedAsZeroDiffering()
    {
        string one = WriteMzml("one", intensity: 100.0);
        string three = WriteMzml("three", intensity: 100.0, spectra: 3);

        var (exit, output) = Run("-i", one, three);

        Assert.AreEqual(1, exit, output);
        StringAssert.Contains(output, "spectrum count: 1 vs 3", output);
        StringAssert.Contains(output, "spectrumList (3 differing spectra)", output);
        Assert.IsFalse(output.Contains("spectrumList (0 spectra)", StringComparison.Ordinal),
            "a count mismatch must never render as 0 differing spectra: " + output);
    }

    /// <summary>
    /// A differing count that happens to end in zero must not contain the substring
    /// "0 spectra". Consumers grep for that unanchored - the container's vendor sweep does -
    /// so cpp's uniform "(N spectra)" wording reports a PASS for "(19570 spectra)", which is
    /// every spectrum in the file differing. This is the guard on that.
    /// </summary>
    [TestMethod]
    public void CountsEndingInZero_DoNotLookLikeZero()
    {
        // Kept under DiffConfig.MaxDifferencesToReport (50) so the counts are exact; the
        // saturating case is covered separately below.
        foreach (int n in new[] { 10, 20 })
        {
            string a = WriteMzml("a" + n, intensity: 100.0, spectra: n);
            string b = WriteMzml("b" + n, intensity: 999.0, spectra: n);

            var (_, output) = Run("-i", a, b);

            StringAssert.Contains(output, $"({n} differing spectra)", output);
            Assert.IsFalse(output.Contains("0 spectra", StringComparison.Ordinal),
                $"{n} differing spectra must not contain the substring '0 spectra': {output}");
        }
    }

    /// <summary>
    /// Past <see cref="DiffConfig.MaxDifferencesToReport"/> the walk stops, so the count
    /// understates - 100 differing spectra report as 50. That is a floor, not a ceiling, which
    /// is what matters: it stays non-zero, so no consumer reads a saturated diff as equality.
    /// </summary>
    [TestMethod]
    public void SaturatedReport_StillCountsAsDiffering()
    {
        string a = WriteMzml("a100", intensity: 100.0, spectra: 100);
        string b = WriteMzml("b100", intensity: 999.0, spectra: 100);

        var (exit, output) = Run("-i", a, b);

        Assert.AreEqual(1, exit, output);
        StringAssert.Contains(output, "report capped at 50", output);
        StringAssert.Contains(output, "(50 differing spectra)", output);
        Assert.IsFalse(output.Contains("0 spectra", StringComparison.Ordinal), output);
    }

    [TestMethod]
    public void MissingFile_ExitsOneWithoutThrowing()
    {
        string a = WriteMzml("a", intensity: 100.0);
        var (exit, _) = Run(a, Path.Combine(_tempDir, "does-not-exist.mzML"));
        Assert.AreEqual(1, exit);
    }

    // ---- helpers ----

    private static (int Exit, string Output) Run(params string[] args)
    {
        var config = ArgParser.Parse(args);
        var sw = new StringWriter();
        int exit;
        try { exit = new Differ().Run(config, sw); }
        catch (FileNotFoundException) { exit = 1; }
        return (exit, sw.ToString());
    }

    private string WriteMzml(string name, double intensity, int spectra = 1)
    {
        // Fixed document id, varying only the filename: the id is metadata that -i does not
        // cover, and letting it track the name would leave a difference in every case and
        // mask what these tests assert about the spectrum data.
        var msd = new MSData { Id = "doc" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        var list = new SpectrumListSimple();

        for (int i = 0; i < spectra; i++)
        {
            var ms1 = new Spectrum { Index = i, Id = "scan=" + (i + 1), DefaultArrayLength = 3 };
            ms1.Params.Set(CVID.MS_ms_level, 1);
            ms1.Params.Set(CVID.MS_positive_scan);
            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 10.0 * (i + 1), CVID.UO_second);
            ms1.ScanList.Scans.Add(scan);
            ms1.SetMZIntensityArrays(new[] { 100.0, 200.0, 300.0 },
                                     new[] { 10.0, intensity, 20.0 },
                                     CVID.MS_number_of_detector_counts);
            list.Spectra.Add(ms1);
        }

        // Same run id on both sides: the id is metadata, and a mismatch there would show up as
        // a document-level difference and mask what these tests are actually asserting about
        // the spectrum counts.
        msd.Run.Id = "run";
        msd.Run.SpectrumList = list;

        string path = Path.Combine(_tempDir, name + ".mzML");
        File.WriteAllText(path, new MzmlWriter().Write(msd));
        return path;
    }
}
