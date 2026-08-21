//
// Port of pwiz_tools/MSConvertGUI/Test/MainLogicTest.cs (PR #4099 lineage).
//
// Round-trips an mzML through both the CLI (Pwiz.Tools.MsConvert.Converter) and the GUI
// (MainLogic.QueueWork + MainLogic.Work) and asserts the two outputs MSData-diff equal —
// i.e. clicking "Start" in MSConvertGUI-sharp produces the same file as running
// msconvert-sharp from the command line with equivalent switches.
//
// The cpp test walks pwiz/data/vendor_readers/*.data/* for real vendor inputs and uses
// msconvert.exe / msdiff.exe subprocesses. We use Examples.InitializeTiny as the canonical
// input (no vendor SDK needed), Pwiz.Tools.MsConvert.Converter for the CLI side (no
// subprocess), and Pwiz.Data.MsData.Diff.MSDataDiff for the comparison.

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSConvertGUI;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Tools.MsConvert;

namespace MSConvertGUI.Tests;

/// <summary>
/// Verifies that the GUI's <see cref="MainLogic"/> conversion pipeline produces the same
/// output as <see cref="Pwiz.Tools.MsConvert.Converter"/> when given equivalent options —
/// i.e. the GUI is a thin wrapper around the same writer plumbing the CLI exercises.
/// </summary>
[TestClass]
public class MainLogicTest
{
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MsConvertGuiTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    /// <summary>Writes the <see cref="Examples.InitializeTiny"/> document to a temp .mzML and
    /// returns the path. Used as the canonical test input by every CLI-vs-GUI parity test.</summary>
    private string WriteTinyInput()
    {
        var msd = new MSData();
        Examples.InitializeTiny(msd);
        string path = Path.Combine(_tempDir, "input.mzML");
        using var fs = File.Create(path);
        new MzmlWriter().Write(msd, fs);
        return path;
    }

    /// <summary>Runs an input file through msconvert-sharp's CLI pipeline (no subprocess —
    /// uses <see cref="Converter"/> directly, the same code msconvert-sharp.exe runs).</summary>
    private string RunCli(string inputPath, params string[] extraArgs)
    {
        string outDir = Path.Combine(_tempDir, "cli");
        Directory.CreateDirectory(outDir);
        var args = new List<string> { inputPath, "--outdir", outDir, "--64" };
        args.AddRange(extraArgs);
        var cfg = ArgParser.Parse(args);
        new Converter(cfg).Run();
        return Directory.GetFiles(outDir).Single();
    }

    /// <summary>Runs an input file through the GUI's <see cref="MainLogic"/> pipeline by
    /// queuing a single job and calling <see cref="MainLogic.Work"/> synchronously (drains
    /// the queue on the current thread — same code path the GUI's worker thread runs).</summary>
    private string RunGui(string inputPath, params string[] extraArgs)
    {
        string outDir = Path.Combine(_tempDir, "gui");
        Directory.CreateDirectory(outDir);
        var logic = new MainLogic(new ProgressForm.JobInfo(),
            new Map<string, int>(),
            calculateSHA1Mutex: new object());
        // ParseCommandLine takes pipe-delimited args (matches the cpp test's --64 baseline).
        string argString = string.Join("|",
            new[] { "--64" }
                .Concat(extraArgs)
                .Concat(new[] { inputPath })
                .Where(s => !string.IsNullOrEmpty(s)));
        var config = logic.ParseCommandLine(outDir, argString);
        logic.QueueWork(config);
        // Drain the shared queue on this thread; processFile runs synchronously and
        // releases the work item.
        MainLogic.Work();
        return Directory.GetFiles(outDir).Single();
    }

    /// <summary>Diffs two mzML files; asserts MSData-level equivalence (binary arrays,
    /// CV terms, refs).</summary>
    private static void AssertEquivalentMzml(string a, string b)
    {
        var msdA = new MzmlReader().Read(File.ReadAllText(a));
        var msdB = new MzmlReader().Read(File.ReadAllText(b));
        string diff = MSDataDiff.Describe(msdA, msdB);
        Assert.AreEqual(string.Empty, diff,
            $"CLI vs GUI mzML differ:\n  cli: {a}\n  gui: {b}\n\n{diff}");
    }

    /// <summary>The bedrock parity test: GUI mzML→mzML equals CLI mzML→mzML.</summary>
    [TestMethod]
    public void MzML_To_MzML_GuiMatchesCli()
    {
        string input = WriteTinyInput();
        string cliOut = RunCli(input);
        string guiOut = RunGui(input);
        AssertEquivalentMzml(cliOut, guiOut);
    }

    /// <summary>mzML→mzXML through both paths must agree (file-name extension + format flag).</summary>
    [TestMethod]
    public void MzML_To_MzXML_GuiMatchesCli()
    {
        string input = WriteTinyInput();
        string cliOut = RunCli(input, "--mzXML");
        string guiOut = RunGui(input, "--mzXML");
        // mzXML diff via re-read into MSData: Diff covers what survives the lossy mzXML model.
        var msdA = new MSData();
        using (var fa = File.OpenRead(cliOut))
            Pwiz.Data.MsData.MzXml.MzxmlReader.Read(fa, msdA);
        var msdB = new MSData();
        using (var fb = File.OpenRead(guiOut))
            Pwiz.Data.MsData.MzXml.MzxmlReader.Read(fb, msdB);
        string diff = MSDataDiff.Describe(msdA, msdB);
        Assert.AreEqual(string.Empty, diff,
            $"CLI vs GUI mzXML differ:\n  cli: {cliOut}\n  gui: {guiOut}\n\n{diff}");
    }

    /// <summary>mzML→MGF: GUI and CLI should emit the same byte stream (MGF is line-oriented
    /// text; no XML re-shuffling between the two paths).</summary>
    [TestMethod]
    public void MzML_To_MGF_GuiMatchesCli()
    {
        string input = WriteTinyInput();
        string cliOut = RunCli(input, "--mgf");
        string guiOut = RunGui(input, "--mgf");
        Assert.AreEqual(File.ReadAllText(cliOut, System.Text.Encoding.UTF8),
                        File.ReadAllText(guiOut, System.Text.Encoding.UTF8),
                        "MGF byte streams differ between CLI and GUI");
    }
}
