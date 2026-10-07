using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.Readers;

[TestClass]
public class ReaderListTests
{
    [TestMethod]
    public void Default_Registration_AndIdentifyAndIdentifyReader()
    {
        var list = ReaderList.Default;

        // Default registration order: mzML, mzMLb, mz5, mzXML, MSn, BTDX, MGF.
        Assert.AreEqual("mzML", list.Readers[0].TypeName);
        Assert.AreEqual("mzMLb", list.Readers[1].TypeName);
        Assert.AreEqual("MZ5", list.Readers[2].TypeName);
        Assert.AreEqual("mzXML", list.Readers[3].TypeName);
        Assert.AreEqual("MSn", list.Readers[4].TypeName);
        Assert.AreEqual("Bruker Data Exchange", list.Readers[5].TypeName);
        Assert.AreEqual("Mascot Generic", list.Readers[6].TypeName);

        // Identify: header-sniff wins over filename.
        const string mzmlHead = "<?xml version=\"1.0\"?><indexedmzML><mzML version=\"1.1.0\">";
        Assert.AreEqual(CVID.MS_mzML_format, list.Identify("anything.xml", mzmlHead));
        const string mgfHead = "# comment\nBEGIN IONS\nTITLE=foo\n";
        Assert.AreEqual(CVID.MS_Mascot_MGF_format, list.Identify("unknown.txt", mgfHead));

        // Extension-only fallback when no head is supplied.
        Assert.AreEqual(CVID.MS_mzML_format, list.Identify("foo.mzML", head: null));
        Assert.AreEqual(CVID.MS_Mascot_MGF_format, list.Identify("foo.mgf", head: null));

        // Unrecognized → CVID_Unknown sentinel (not exception).
        Assert.AreEqual(CVID.CVID_Unknown, list.Identify("random.txt", "not mzml or mgf"));

        // IdentifyReader returns the correct adapter, or null when no reader can claim the file.
        Assert.IsInstanceOfType<MzmlReaderAdapter>(list.IdentifyReader("x.mzML", null));
        Assert.IsInstanceOfType<MgfReaderAdapter>(list.IdentifyReader("x.mgf", null));
        Assert.IsNull(list.IdentifyReader("x.bin", "garbage"));
    }

    [TestMethod]
    public void Read_DispatchesByFormat_MgfAndMzml()
    {
        // Dispatch to MGF reader for MGF content.
        string tmpMgf = Path.Combine(Path.GetTempPath(), "reader_list_test.mgf");
        File.WriteAllText(tmpMgf, "BEGIN IONS\nTITLE=test\nPEPMASS=500\nEND IONS\n");
        try
        {
            var msd = new MSData();
            ReaderList.Default.Read(tmpMgf, msd);
            Assert.IsNotNull(msd.Run.SpectrumList);
            Assert.AreEqual(1, msd.Run.SpectrumList.Count);
        }
        finally { File.Delete(tmpMgf); }

        // Dispatch to mzML reader for mzML content (round-trip a synthetic doc).
        var original = new MSData { Id = "dispatch" };
        original.CVs.AddRange(MSData.DefaultCVList);
        original.Run.SpectrumList = new SpectrumListSimple();
        string tmpMzml = Path.Combine(Path.GetTempPath(), "reader_list_test.mzML");
        File.WriteAllText(tmpMzml, new MzmlWriter().Write(original));
        try
        {
            var msd = new MSData();
            ReaderList.Default.Read(tmpMzml, msd);
            Assert.AreEqual("dispatch", msd.Id);
        }
        finally { File.Delete(tmpMzml); }
    }

    [TestMethod]
    public void Read_UnknownFormat_ThrowsOrSilentlyEmpty()
    {
        // Default ReaderConfig: unknown format throws.
        string tmpFile = Path.Combine(Path.GetTempPath(), "reader_list_unknown.bin");
        File.WriteAllText(tmpFile, "not a spectrum format");
        try
        {
            var msd = new MSData();
            Assert.ThrowsException<NotSupportedException>(
                () => ReaderList.Default.Read(tmpFile, msd));

            // UnknownFormatIsError=false: read silently produces an empty document.
            ReaderList.Default.Read(tmpFile, msd, new ReaderConfig { UnknownFormatIsError = false });
            Assert.IsTrue(msd.IsEmpty);
        }
        finally { File.Delete(tmpFile); }
    }

    [TestMethod]
    public void Read_EveryRun_OnePerSampleSkippingOneThatFails()
    {
        string multiFile = Path.Combine(Path.GetTempPath(), "reader_list_multi.fake");
        string singleFile = Path.Combine(Path.GetTempPath(), "reader_list_single.fake1");
        File.WriteAllText(multiFile, "samples");
        File.WriteAllText(singleFile, "sample");
        try
        {
            var list = new ReaderList();
            list.Add(new FakeMultiSampleReader(".fake", sampleCount: 3, failingRun: 1));
            list.Add(new FakeReader(".fake1", failingRun: 0));

            // multi-sample: one MSData per sample, the failing one skipped, the caller's RunIndex kept
            var results = new List<MSData>();
            var config = new ReaderConfig { RunIndex = 7 };
            list.Read(multiFile, results, config);
            CollectionAssert.AreEqual(new[] { "run0", "run2" }, results.Select(m => m.Id).ToArray());
            Assert.AreEqual(7, config.RunIndex);

            // single-run: its failure propagates and nothing is added
            var single = new List<MSData>();
            Assert.ThrowsException<InvalidOperationException>(() => list.Read(singleFile, single));
            Assert.AreEqual(0, single.Count);
        }
        finally
        {
            File.Delete(multiFile);
            File.Delete(singleFile);
        }
    }

    /// <summary>Names each MSData after its run and throws for <c>failingRun</c>.</summary>
    private class FakeReader : IReader
    {
        private readonly string _extension;
        private readonly int _failingRun;

        public FakeReader(string extension, int failingRun)
        {
            _extension = extension;
            _failingRun = failingRun;
        }

        public string TypeName => "fake" + _extension;
        public CVID CvType => CVID.MS_mzML_format;
        public IReadOnlyList<string> FileExtensions => new[] { _extension };

        public CVID Identify(string filename, string? head) =>
            Path.GetExtension(filename) == _extension ? CvType : CVID.CVID_Unknown;

        public void Read(string filename, MSData result, ReaderConfig? config = null)
        {
            int run = config?.RunIndex ?? 0;
            if (run == _failingRun)
                throw new InvalidOperationException("unreadable run " + run);
            result.Id = "run" + run;
        }
    }

    private sealed class FakeMultiSampleReader : FakeReader, IMultiSampleReader
    {
        private readonly int _sampleCount;

        public FakeMultiSampleReader(string extension, int sampleCount, int failingRun)
            : base(extension, failingRun)
        {
            _sampleCount = sampleCount;
        }

        public string[] EnumerateSampleNames(string filename) =>
            Enumerable.Range(0, _sampleCount).Select(i => "sample" + i).ToArray();
    }
}
