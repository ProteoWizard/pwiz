using Pwiz.Data.MsData.Diff;
using Pwiz.Data.MsData.Mzml;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Reads the mzML pwiz C++ wrote (<c>example_data/tiny.pwiz.1.1.mzML</c>) and re-serialises it
/// with the port's writer, so the input side of the comparison is a genuine pwiz document rather
/// than one the port built for itself.
/// </summary>
/// <remarks>
/// The other mzML tests start from <see cref="Examples.InitializeTiny"/>, which means the writer
/// only ever sees constructs the port's own example contains. Starting from cpp's file exercises
/// whatever pwiz actually emits - its param groups, referenceable params, index and checksum, and
/// its spelling of every attribute.
/// </remarks>
[TestClass]
public class TinyExampleCppParityTests
{
    /// <summary>
    /// The ids in cpp's committed file. Note it holds four spectra ending in <c>cycle=22</c> while
    /// cpp's current <c>examples::initializeTiny</c> - which the port's
    /// <see cref="Examples.InitializeTiny"/> mirrors exactly - builds five ending in <c>scan=22</c>
    /// and <c>cycle=23</c>. The committed file simply predates that growth, so it is a fixture for
    /// the READER, not a reference for the example.
    /// </summary>
    private static readonly string[] CppFileSpectrumIds =
    {
        "scan=19", "scan=20", "scan=21", "sample=1 period=1 cycle=22 experiment=1",
    };

    [TestMethod]
    public void CppWrittenMzml_ReadsAndSurvivesOurWriter()
    {
        var fromCpp = new MzmlReader().Read(File.ReadAllText(CppTinyPath()));

        var ids = Enumerable.Range(0, fromCpp.Run.SpectrumList!.Count)
            .Select(i => fromCpp.Run.SpectrumList.SpectrumIdentity(i).Id)
            .ToArray();
        CollectionAssert.AreEqual(CppFileSpectrumIds, ids, "spectra read out of cpp's mzML");

        // Re-serialise and re-read: nothing pwiz put in the file may be lost on the way through.
        using var buffer = new MemoryStream();
        MSDataFile.Write(fromCpp, buffer, new WriteConfig { Format = WriteFormat.Mzml });
        var roundTripped = new MzmlReader().Read(
            System.Text.Encoding.UTF8.GetString(buffer.ToArray()));

        string diff = MSDataDiff.Describe(fromCpp, roundTripped);
        Assert.AreEqual(string.Empty, diff,
            "re-writing pwiz's own mzML lost or changed something:" + Environment.NewLine + diff);
    }

    private static string CppTinyPath()
    {
        // example_data sits beside the test assembly (copied by the csproj) in normal runs.
        string beside = Path.Combine(AppContext.BaseDirectory, "example_data", "tiny.pwiz.1.1.mzML");
        if (File.Exists(beside)) return beside;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "example_data", "tiny.pwiz.1.1.mzML");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("tiny.pwiz.1.1.mzML not found beside " + AppContext.BaseDirectory);
    }
}
