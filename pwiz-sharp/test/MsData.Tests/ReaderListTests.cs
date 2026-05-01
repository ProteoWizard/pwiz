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

        // Default registration order: mzML, then MGF.
        Assert.AreEqual("mzML", list.Readers[0].TypeName);
        Assert.AreEqual("MGF", list.Readers[1].TypeName);

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
}
