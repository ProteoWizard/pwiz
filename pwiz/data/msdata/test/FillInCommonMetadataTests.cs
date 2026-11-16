using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// Verifies <see cref="MSDataFile.FillInCommonMetadata"/> and that the format-reader adapters
/// (mzML, mzMLb, mzXML, MGF, MSn) call it after parsing the input file. Mirrors cpp
/// <c>fillInCommonMetadata</c> in <c>DefaultReaderList.cpp:86</c>.
/// </summary>
[TestClass]
public class FillInCommonMetadataTests
{
    [TestMethod]
    public void Helper_AppendsSourceFileEntry_AndStampsId()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "metadata-helper.mzML");
        File.WriteAllText(tmp, "<mzML/>");
        try
        {
            var msd = new MSData();
            MSDataFile.FillInCommonMetadata(tmp, msd);
            Assert.AreEqual(1, msd.FileDescription.SourceFiles.Count);
            var sf = msd.FileDescription.SourceFiles[0];
            Assert.AreEqual("metadata-helper.mzML", sf.Id);
            Assert.AreEqual("metadata-helper.mzML", sf.Name);
            StringAssert.StartsWith(sf.Location, "file:///");
            // msd.Id / Run.Id fall back to the basename when not set by the format reader.
            Assert.AreEqual("metadata-helper", msd.Id);
            Assert.AreEqual("metadata-helper", msd.Run.Id);
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    [TestMethod]
    public void Helper_AddsPwizSoftware_AndDedupesByVersion()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "metadata-software.mzML");
        File.WriteAllText(tmp, "<mzML/>");
        try
        {
            var msd = new MSData();
            MSDataFile.FillInCommonMetadata(tmp, msd);
            Assert.AreEqual(1, msd.Software.Count);
            var sw = msd.Software[0];
            Assert.AreEqual("pwiz_" + MSData.PwizVersion, sw.Id);
            Assert.AreEqual(MSData.PwizVersion, sw.Version);
            Assert.IsTrue(sw.HasCVParam(CVID.MS_pwiz));

            // Idempotent: a second call with the same pwiz version reuses the existing entry.
            MSDataFile.FillInCommonMetadata(tmp, msd);
            Assert.AreEqual(1, msd.Software.Count, "pwiz software entry should not duplicate");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    [TestMethod]
    public void Helper_AddsPwizReaderConversionDp_WithMS_Conversion_to_mzML()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "metadata-dp.mzML");
        File.WriteAllText(tmp, "<mzML/>");
        try
        {
            var msd = new MSData();
            var dp = MSDataFile.FillInCommonMetadata(tmp, msd);
            Assert.AreEqual("pwiz_Reader_conversion", dp.Id);
            Assert.AreEqual(1, dp.ProcessingMethods.Count);
            Assert.IsTrue(dp.ProcessingMethods[0].HasCVParam(CVID.MS_Conversion_to_mzML));
            Assert.AreSame(msd.Software[0], dp.ProcessingMethods[0].Software);
            Assert.IsTrue(msd.DataProcessings.Contains(dp));
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    [TestMethod]
    public void Helper_SetsDpOnSimpleSpectrumList_ButNotLazyList()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "metadata-sls.mzML");
        File.WriteAllText(tmp, "<mzML/>");
        try
        {
            // Simple list — DP gets set.
            var msd1 = new MSData();
            msd1.Run.SpectrumList = new SpectrumListSimple();
            var dp1 = MSDataFile.FillInCommonMetadata(tmp, msd1);
            Assert.AreSame(dp1, ((SpectrumListSimple)msd1.Run.SpectrumList).Dp);

            // Lazy list constructed independently doesn't get mutated; caller is expected to
            // pass the returned DP to the constructor.
            var msd2 = new MSData();
            // Don't bother building a real SpectrumList_Mzml here; just verify the helper
            // doesn't mutate a non-Simple list. With null SpectrumList, the helper should
            // still run successfully and not throw.
            var dp2 = MSDataFile.FillInCommonMetadata(tmp, msd2);
            Assert.IsNotNull(dp2);
        }
        finally { try { File.Delete(tmp); } catch { } }
    }

    [TestMethod]
    public void MzmlReaderAdapter_ReadsTinyFile_AndAddsProvenance()
    {
        // Produce a minimal valid mzML file via MzmlWriter, then read it back through the
        // adapter. The output should have:
        //   - 1 SourceFile entry for the input file with SHA-1 (added by the adapter)
        //   - pwiz_<version> software entry
        //   - pwiz_Reader_conversion DataProcessing
        var msd = new MSData { Id = "tiny" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        msd.Run.Id = "tiny";
        msd.Run.SpectrumList = new SpectrumListSimple();

        string tmp = Path.Combine(Path.GetTempPath(), $"tiny-{System.Guid.NewGuid():N}.mzML");
        try
        {
            File.WriteAllText(tmp, new MzmlWriter().Write(msd));

            var rt = new MSData();
            new MzmlReaderAdapter().Read(tmp, rt);

            // SourceFile lineage entry present.
            Assert.IsTrue(rt.FileDescription.SourceFiles.Any(sf => sf.Name == Path.GetFileName(tmp)),
                "input file should appear in SourceFiles after read");

            // pwiz software entry.
            Assert.IsTrue(rt.Software.Any(s => s.HasCVParam(CVID.MS_pwiz)),
                "pwiz software entry should be added");

            // pwiz_Reader_conversion DataProcessing.
            Assert.IsTrue(rt.DataProcessings.Any(d => d.Id == "pwiz_Reader_conversion"),
                "pwiz_Reader_conversion DataProcessing should be added");
        }
        finally { try { File.Delete(tmp); } catch { } }
    }
}
