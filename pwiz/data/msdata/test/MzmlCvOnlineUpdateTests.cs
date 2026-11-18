using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Tests.Cv;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Spectra;
using static Pwiz.Data.Common.Tests.Cv.CvOnlineUpdateFixtures;

namespace Pwiz.Data.MsData.Tests.Mzml;

[TestClass]
public class MzmlCvOnlineUpdateTests
{
    private string _cacheDir = null!;

    [TestInitialize]
    public void Initialize() =>
        _cacheDir = Path.Combine(Path.GetTempPath(), "pwiz-cv-update-test-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        CvLookup.ResetOnlineUpdateForTests();
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [TestMethod]
    public void TermFromANewerCv_SurvivesRoundTrip()
    {
        // An mzML written against psi-ms 99.0.0, using a term this build's CV lacks.
        string embeddedVersion = CvLookup.GetCv("MS").Version;
        string mzml = new MzmlWriter().Write(BuildOneSpectrum())
            .Replace($"version=\"{embeddedVersion}\"", "version=\"99.0.0\"", StringComparison.Ordinal)
            .Replace("accession=\"MS:1000130\" name=\"positive scan\"", "accession=\"MS:9999999\" name=\"new widget\"",
                StringComparison.Ordinal);
        StringAssert.Contains(mzml, "MS:9999999");

        var server = new FakeCvServer();
        server.Serve(MsTagUrl("99.0.0"), MsObo("99.0.0",
            Term("MS:9999999", "new widget", isA: "MS:1000465 ! scan polarity")));
        StartProcess(server, _cacheDir);

        // The reader registers the file's cvList; the unknown term then loads 99.0.0.
        var reparsed = new MzmlReader().Read(mzml);
        var spectrum = reparsed.Run.SpectrumList!.GetSpectrum(0);
        var param = spectrum.Params.CVParams.Single(p => p.Cvid == (CVID)9999999);
        Assert.IsTrue(CvLookup.CvIsA(param.Cvid, CVID.MS_scan_polarity));
        CollectionAssert.AreEqual(new[] { MsTagUrl("99.0.0") }, server.Requests.ToArray());

        string rewritten = new MzmlWriter().Write(reparsed);
        StringAssert.Contains(rewritten, "accession=\"MS:9999999\" name=\"new widget\"");
        Assert.IsFalse(rewritten.Contains("??:0000000", StringComparison.Ordinal), "no unknown terms written");
    }

    private static MSData BuildOneSpectrum()
    {
        var msd = new MSData { Id = "cv_update" };
        msd.CVs.AddRange(MSData.DefaultCVList);
        msd.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);

        var sw = new Software("pwiz-sharp") { Version = "0.1" };
        sw.Set(CVID.MS_pwiz);
        msd.Software.Add(sw);

        var dp = new DataProcessing("dp1");
        dp.ProcessingMethods.Add(new ProcessingMethod { Order = 1, Software = sw });
        dp.ProcessingMethods[0].Set(CVID.MS_Conversion_to_mzML);
        msd.DataProcessings.Add(dp);

        var spectrum = new Spectrum { Index = 0, Id = "scan=1", DefaultArrayLength = 2 };
        spectrum.Params.Set(CVID.MS_ms_level, 1);
        spectrum.Params.Set(CVID.MS_positive_scan);
        spectrum.SetMZIntensityArrays(new[] { 100.0, 200.0 }, new[] { 1.0, 2.0 }, CVID.MS_number_of_detector_counts);

        var list = new SpectrumListSimple { Dp = dp };
        list.Spectra.Add(spectrum);
        msd.Run.Id = "run1";
        msd.Run.SpectrumList = list;
        return msd;
    }
}
