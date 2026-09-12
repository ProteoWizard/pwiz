using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.MSn;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.MSn;

[TestClass]
public class MSnRoundTripTests
{
    private static MSData BuildMs1Document(int numSpectra = 2)
    {
        var msd = new MSData();
        var list = new SpectrumListSimple();
        for (int i = 0; i < numSpectra; i++)
        {
            var spec = new Spectrum
            {
                Index = i,
                Id = $"scan={i + 1}",
            };
            spec.Params.Set(CVID.MS_MSn_spectrum);
            spec.Params.Set(CVID.MS_ms_level, 1);
            spec.Params.Set(CVID.MS_centroid_spectrum);

            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 60.0 * (i + 1), CVID.UO_second); // 1, 2 minutes
            spec.ScanList.Scans.Add(scan);

            spec.SetMZIntensityArrays(
                new[] { 100.1, 200.2 + i, 300.3 },
                new[] { 50.0, 200.0, 75.0 },
                CVID.MS_number_of_detector_counts);

            list.Spectra.Add(spec);
        }
        msd.Run.SpectrumList = list;
        return msd;
    }

    private static MSData BuildMs2Document(int numSpectra = 2)
    {
        var msd = new MSData();
        var list = new SpectrumListSimple();
        for (int i = 0; i < numSpectra; i++)
        {
            var spec = new Spectrum
            {
                Index = i,
                Id = $"scan={i + 10}",
            };
            spec.Params.Set(CVID.MS_MSn_spectrum);
            spec.Params.Set(CVID.MS_ms_level, 2);
            spec.Params.Set(CVID.MS_centroid_spectrum);

            var scan = new Scan();
            scan.Set(CVID.MS_scan_start_time, 60.0 * (i + 1), CVID.UO_second);
            spec.ScanList.Scans.Add(scan);

            var precursor = new Precursor();
            precursor.IsolationWindow.Set(CVID.MS_isolation_window_target_m_z, 500.0 + i, CVID.MS_m_z);
            var si = new SelectedIon();
            si.Set(CVID.MS_selected_ion_m_z, 500.0 + i, CVID.MS_m_z);
            si.Set(CVID.MS_charge_state, 2);
            precursor.SelectedIons.Add(si);
            spec.Precursors.Add(precursor);

            spec.SetMZIntensityArrays(
                new[] { 100.5, 200.7 + i, 300.2 },
                new[] { 50.0, 200.0, 80.0 },
                CVID.MS_number_of_detector_counts);

            list.Spectra.Add(spec);
        }
        msd.Run.SpectrumList = list;
        return msd;
    }

    private static (MSData read, byte[] bytes) RoundTrip(MSData src, MSnType type)
    {
        using var ms = new MemoryStream();
        new SerializerMSn(type).Write(src, ms);
        byte[] bytes = ms.ToArray();
        var dst = new MSData();
        using var input = new MemoryStream(bytes);
        new SerializerMSn(type).Read(input, dst);
        return (dst, bytes);
    }

    private static void AssertMs1RoundTripped(MSData src, MSData dst)
    {
        Assert.IsNotNull(dst.Run.SpectrumList);
        Assert.AreEqual(src.Run.SpectrumList!.Count, dst.Run.SpectrumList.Count);
        for (int i = 0; i < src.Run.SpectrumList.Count; i++)
        {
            var s = src.Run.SpectrumList.GetSpectrum(i, true);
            var d = dst.Run.SpectrumList.GetSpectrum(i, true);
            Assert.AreEqual(s.Id, d.Id, $"spectrum {i} id");
            Assert.AreEqual(1, d.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), "ms level");
            Assert.AreEqual(0, d.Precursors.Count, "MS1 should have no precursor");
            CollectionAssert.AreEqual(s.GetMZArray()!.Data, d.GetMZArray()!.Data, $"spectrum {i} mz");
            // Intensities are float on the binary wire; allow a tiny tolerance.
            var sInt = s.GetIntensityArray()!.Data;
            var dInt = d.GetIntensityArray()!.Data;
            Assert.AreEqual(sInt.Count, dInt.Count);
            for (int p = 0; p < sInt.Count; p++)
                Assert.AreEqual(sInt[p], dInt[p], 1e-3, $"spectrum {i} intensity {p}");
        }
    }

    private static void AssertMs2RoundTripped(MSData src, MSData dst)
    {
        Assert.IsNotNull(dst.Run.SpectrumList);
        Assert.AreEqual(src.Run.SpectrumList!.Count, dst.Run.SpectrumList.Count);
        for (int i = 0; i < src.Run.SpectrumList.Count; i++)
        {
            var s = src.Run.SpectrumList.GetSpectrum(i, true);
            var d = dst.Run.SpectrumList.GetSpectrum(i, true);
            Assert.AreEqual(s.Id, d.Id, $"spectrum {i} id");
            Assert.AreEqual(2, d.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), "ms level");
            Assert.AreEqual(1, d.Precursors.Count, "MS2 should have a precursor");

            double srcMz = s.Precursors[0].IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>();
            double dstMz = d.Precursors[0].IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>();
            Assert.AreEqual(srcMz, dstMz, 1e-6, $"spectrum {i} isolation window mz");

            // Charge state comes back as possible_charge_state on the reader side.
            var dsi = d.Precursors[0].SelectedIons[0];
            int dz = dsi.CvParam(CVID.MS_possible_charge_state).IsEmpty
                ? dsi.CvParam(CVID.MS_charge_state).ValueAs<int>()
                : dsi.CvParam(CVID.MS_possible_charge_state).ValueAs<int>();
            Assert.AreEqual(2, dz, $"spectrum {i} charge state");

            CollectionAssert.AreEqual(s.GetMZArray()!.Data, d.GetMZArray()!.Data, $"spectrum {i} mz");
            var sInt = s.GetIntensityArray()!.Data;
            var dInt = d.GetIntensityArray()!.Data;
            Assert.AreEqual(sInt.Count, dInt.Count);
            for (int p = 0; p < sInt.Count; p++)
                Assert.AreEqual(sInt[p], dInt[p], 1e-3, $"spectrum {i} intensity {p}");
        }
    }

    [TestMethod] public void RoundTrip_Ms1Text()
    {
        var src = BuildMs1Document();
        var (dst, bytes) = RoundTrip(src, MSnType.Ms1);
        Assert.IsTrue(System.Text.Encoding.ASCII.GetString(bytes).Contains("S\t1\t1", StringComparison.Ordinal),
            "MS1 text should have an S line for the first spectrum");
        AssertMs1RoundTripped(src, dst);
    }

    [TestMethod] public void RoundTrip_Ms2Text()
    {
        var src = BuildMs2Document();
        var (dst, bytes) = RoundTrip(src, MSnType.Ms2);
        Assert.IsTrue(System.Text.Encoding.ASCII.GetString(bytes).Contains("Z\t", StringComparison.Ordinal),
            "MS2 text should have at least one Z (charge) line");
        AssertMs2RoundTripped(src, dst);
    }

    [TestMethod] public void RoundTrip_Bms1Binary()
    {
        var src = BuildMs1Document();
        var (dst, bytes) = RoundTrip(src, MSnType.Bms1);
        Assert.IsTrue(bytes.Length > MSnHeader.TotalBytes + 8, "BMS1 binary should include header + spectra");
        AssertMs1RoundTripped(src, dst);
    }

    [TestMethod] public void RoundTrip_Bms2Binary()
    {
        var src = BuildMs2Document();
        var (dst, _) = RoundTrip(src, MSnType.Bms2);
        AssertMs2RoundTripped(src, dst);
    }

    [TestMethod] public void RoundTrip_Cms1Compressed()
    {
        var src = BuildMs1Document();
        var (dst, _) = RoundTrip(src, MSnType.Cms1);
        AssertMs1RoundTripped(src, dst);
    }

    [TestMethod] public void RoundTrip_Cms2Compressed()
    {
        var src = BuildMs2Document();
        var (dst, _) = RoundTrip(src, MSnType.Cms2);
        AssertMs2RoundTripped(src, dst);
    }

    [TestMethod] public void Identify_Ms1ByExtension()
    {
        var r = new MSnReaderAdapter();
        Assert.AreEqual(CVID.MS_MS1_format, r.Identify("foo.ms1", null));
        Assert.AreEqual(CVID.MS_MS1_format, r.Identify("foo.cms1", null));
        Assert.AreEqual(CVID.MS_MS1_format, r.Identify("foo.bms1", null));
    }

    [TestMethod] public void Identify_Ms2ByExtension()
    {
        var r = new MSnReaderAdapter();
        Assert.AreEqual(CVID.MS_MS2_format, r.Identify("foo.ms2", null));
        Assert.AreEqual(CVID.MS_MS2_format, r.Identify("foo.cms2", null));
        Assert.AreEqual(CVID.MS_MS2_format, r.Identify("foo.bms2", null));
    }

    [TestMethod] public void Identify_UnknownExtension()
    {
        var r = new MSnReaderAdapter();
        Assert.AreEqual(CVID.CVID_Unknown, r.Identify("foo.mzML", null));
        Assert.AreEqual(CVID.CVID_Unknown, r.Identify("foo.txt", null));
    }
}
