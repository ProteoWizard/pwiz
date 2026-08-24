using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.Btdx;

[TestClass]
public class BtdxTests
{
    private const string MinimalRoot = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <compounds>
          </compounds>
        </root>
        """;

    private const string TwoCompoundsXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <compounds>
            <cmpd cmpdnr="1" rt="12.5" rt_unit="m">
              <title>compound one</title>
              <ms_spectrum>
                <ms_peaks>
                  <pk mz="100.1" i="500" />
                  <pk mz="200.2" i="1000" />
                  <pk mz="300.3" i="250" />
                </ms_peaks>
              </ms_spectrum>
            </cmpd>
            <cmpd cmpdnr="2" rt="30" rt_unit="s">
              <title>compound two</title>
              <precursor mz="500.5" i="9000" z="2" />
              <ms_spectrum msms_stage="2">
                <ms_peaks>
                  <pk mz="150.5" i="20" />
                  <pk mz="450.7" i="800" />
                </ms_peaks>
              </ms_spectrum>
            </cmpd>
          </compounds>
        </root>
        """;

    [TestMethod]
    public void Identify_RecognizesBtdxRootElement()
    {
        var reader = new BtdxReaderAdapter();
        Assert.AreEqual(CVID.MS_Bruker_XML_format, reader.Identify("anything.xml", MinimalRoot),
            "BTDX root element should be recognized");
        Assert.AreEqual(CVID.CVID_Unknown, reader.Identify("anything.xml", "<?xml version=\"1.0\"?><mzML/>"),
            "non-BTDX XML should not be recognized");
        Assert.AreEqual(CVID.CVID_Unknown, reader.Identify("anything.xml", null),
            "no head means no identification (extension is .xml — too generic)");
    }

    [TestMethod]
    public void Read_ParsesCompoundsAndPeaks()
    {
        // Synthesize the document on disk so the adapter's Read(filename, ...) path is exercised.
        string tmp = Path.GetTempFileName();
        string xmlPath = Path.ChangeExtension(tmp, ".xml");
        File.Move(tmp, xmlPath);
        try
        {
            File.WriteAllText(xmlPath, TwoCompoundsXml, System.Text.Encoding.UTF8);

            var msd = new MSData();
            new BtdxReaderAdapter().Read(xmlPath, msd);

            Assert.IsNotNull(msd.Run.SpectrumList);
            Assert.AreEqual(2, msd.Run.SpectrumList.Count);

            // First compound: MS1, no precursor, RT 12.5 minutes.
            var s1 = msd.Run.SpectrumList.GetSpectrum(0, getBinaryData: true);
            Assert.AreEqual("1", s1.Id);
            Assert.AreEqual(1, s1.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0));
            Assert.AreEqual(0, s1.Precursors.Count, "MS1 should have no precursor");
            Assert.IsTrue(s1.Params.HasCVParam(CVID.MS_MSn_spectrum));
            Assert.IsTrue(s1.Params.HasCVParam(CVID.MS_centroid_spectrum));
            var rt1 = s1.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
            Assert.AreEqual(12.5, rt1.ValueAs<double>(), 1e-9);
            Assert.AreEqual(CVID.UO_minute, rt1.Units);

            CollectionAssert.AreEqual(new[] { 100.1, 200.2, 300.3 }, s1.GetMZArray()!.Data);
            CollectionAssert.AreEqual(new[] { 500.0, 1000.0, 250.0 }, s1.GetIntensityArray()!.Data);
            Assert.AreEqual(1750.0, s1.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 1e-9);
            Assert.AreEqual(200.2, s1.Params.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
            Assert.AreEqual(1000.0, s1.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 1e-9);

            // Second compound: MS2 with precursor, RT 30 seconds.
            var s2 = msd.Run.SpectrumList.GetSpectrum(1, getBinaryData: true);
            Assert.AreEqual("2", s2.Id);
            Assert.AreEqual(2, s2.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0));
            Assert.AreEqual(1, s2.Precursors.Count);
            var si = s2.Precursors[0].SelectedIons[0];
            Assert.AreEqual(500.5, si.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(), 1e-9);
            Assert.AreEqual(9000.0, si.CvParam(CVID.MS_peak_intensity).ValueAs<double>(), 1e-9);
            Assert.AreEqual(2, si.CvParam(CVID.MS_charge_state).ValueAs<int>());
            var rt2 = s2.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time);
            Assert.AreEqual(30.0, rt2.ValueAs<double>(), 1e-9);
            Assert.AreEqual(CVID.UO_second, rt2.Units);

            CollectionAssert.AreEqual(new[] { 150.5, 450.7 }, s2.GetMZArray()!.Data);
            CollectionAssert.AreEqual(new[] { 20.0, 800.0 }, s2.GetIntensityArray()!.Data);

            // File content flags from BTDX reader.
            Assert.IsTrue(msd.FileDescription.FileContent.HasCVParam(CVID.MS_MSn_spectrum));
            Assert.IsTrue(msd.FileDescription.FileContent.HasCVParam(CVID.MS_centroid_spectrum));
        }
        finally
        {
            if (File.Exists(xmlPath)) File.Delete(xmlPath);
        }
    }
}
