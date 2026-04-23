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
    public void Default_RegistersMzmlAndMgf()
    {
        var list = ReaderList.Default;
        Assert.AreEqual("mzML", list.Readers[0].TypeName);
        Assert.AreEqual("MGF", list.Readers[1].TypeName);
    }

    [TestMethod]
    public void Identify_MzmlContent_ReturnsMzmlCvid()
    {
        var list = ReaderList.Default;
        const string head = "<?xml version=\"1.0\"?><indexedmzML><mzML version=\"1.1.0\">";
        Assert.AreEqual(CVID.MS_mzML_format, list.Identify("anything.xml", head));
    }

    [TestMethod]
    public void Identify_MgfContent_ReturnsMgfCvid()
    {
        var list = ReaderList.Default;
        const string head = "# comment\nBEGIN IONS\nTITLE=foo\n";
        Assert.AreEqual(CVID.MS_Mascot_MGF_format, list.Identify("unknown.txt", head));
    }

    [TestMethod]
    public void Identify_MzmlExtension_FallbackWorks()
    {
        var list = ReaderList.Default;
        Assert.AreEqual(CVID.MS_mzML_format, list.Identify("foo.mzML", head: null));
    }

    [TestMethod]
    public void Identify_MgfExtension_FallbackWorks()
    {
        var list = ReaderList.Default;
        Assert.AreEqual(CVID.MS_Mascot_MGF_format, list.Identify("foo.mgf", head: null));
    }

    [TestMethod]
    public void Identify_UnknownFormat_ReturnsUnknown()
    {
        var list = ReaderList.Default;
        Assert.AreEqual(CVID.CVID_Unknown, list.Identify("random.txt", "not mzml or mgf"));
    }

    [TestMethod]
    public void IdentifyReader_ReturnsCorrectAdapter()
    {
        var list = ReaderList.Default;
        Assert.IsInstanceOfType<MzmlReaderAdapter>(list.IdentifyReader("x.mzML", null));
        Assert.IsInstanceOfType<MgfReaderAdapter>(list.IdentifyReader("x.mgf", null));
        Assert.IsNull(list.IdentifyReader("x.bin", "garbage"));
    }

    [TestMethod]
    public void Read_DispatchesToMgfForMgfContent()
    {
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
    }

    [TestMethod]
    public void Read_DispatchesToMzmlForMzmlContent()
    {
        // Round-trip: build a tiny doc, serialize to a temp mzML file, dispatch through ReaderList.
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
    public void Read_UnknownFormat_Throws()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), "reader_list_unknown.bin");
        File.WriteAllText(tmpFile, "not a spectrum format");
        try
        {
            var msd = new MSData();
            Assert.ThrowsException<NotSupportedException>(
                () => ReaderList.Default.Read(tmpFile, msd));
        }
        finally { File.Delete(tmpFile); }
    }

    [TestMethod]
    public void ReaderConfig_UnknownFormatIsError_False_NoThrow()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), "reader_list_silent.bin");
        File.WriteAllText(tmpFile, "garbage");
        try
        {
            var msd = new MSData();
            ReaderList.Default.Read(tmpFile, msd, new ReaderConfig { UnknownFormatIsError = false });
            // msd stays empty; no exception.
            Assert.IsTrue(msd.IsEmpty);
        }
        finally { File.Delete(tmpFile); }
    }
}
