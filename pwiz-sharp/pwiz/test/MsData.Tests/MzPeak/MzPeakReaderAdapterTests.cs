using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.MzPeak;
using Pwiz.Data.MsData.Readers;

namespace Pwiz.Data.MsData.Tests;

/// <summary>
/// IReader-level tests for the mzPeak adapter: write a synthetic file, open it
/// via the adapter, and check that the spectra surface through pwiz's MSData
/// model (SpectrumList / GetSpectrum / Precursors / Scan / BinaryDataArrays).
/// Round-trip behaviour at the column level is covered by
/// <see cref="RoundTripTests"/>; this layer asserts the column→MSData
/// translation.
/// </summary>
[TestClass]
public class MzPeakReaderAdapterTests
{
    private static string s_fixturePath = string.Empty;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        s_fixturePath = Path.Combine(Path.GetTempPath(), $"mzpeak-adapter-{Guid.NewGuid():N}.mzpeak");
        var spectra = SyntheticData.Spectra();
        var chroms = SyntheticData.Chromatograms();
        MzPeakWriter.Write(s_fixturePath, spectra, SyntheticData.FileMetadata(), chroms);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (File.Exists(s_fixturePath)) File.Delete(s_fixturePath);
    }

    [TestMethod]
    public void Identify_RecognisesMzPeakExtension()
    {
        var adapter = new MzPeakReaderAdapter();
        Assert.AreEqual(CVID.MS_mzPeak_format, adapter.Identify("foo.mzpeak", null));
        Assert.AreEqual(CVID.MS_mzPeak_format, adapter.Identify("foo.MzPeak", null));
    }

    [TestMethod]
    public void Identify_RejectsArbitraryFile()
    {
        var adapter = new MzPeakReaderAdapter();
        Assert.AreEqual(CVID.CVID_Unknown, adapter.Identify("foo.txt", "hello world"));
    }

    [TestMethod]
    public void Identify_RecognisesMzPeakByMagicWhenExtensionMissing()
    {
        // ZIP magic prefix forces the secondary archive-content check.
        var path = Path.Combine(Path.GetTempPath(), $"mzpeak-magic-{Guid.NewGuid():N}");
        File.Copy(s_fixturePath, path, overwrite: true);
        try
        {
            var head = ReadHead(path, 16);
            Assert.AreEqual(CVID.MS_mzPeak_format, new MzPeakReaderAdapter().Identify(path, head));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void Read_PopulatesSpectrumList()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);

        Assert.IsNotNull(msd.Run.SpectrumList);
        Assert.AreEqual(3, msd.Run.SpectrumList!.Count);
        Assert.AreEqual("synthetic=1 scan=1", msd.Run.SpectrumList.SpectrumIdentity(0).Id);
        Assert.AreEqual("synthetic=1 scan=2", msd.Run.SpectrumList.SpectrumIdentity(1).Id);
    }

    [TestMethod]
    public void Read_PopulatesChromatogramList()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);

        Assert.IsNotNull(msd.Run.ChromatogramList);
        Assert.AreEqual(3, msd.Run.ChromatogramList!.Count);
        Assert.AreEqual("TIC", msd.Run.ChromatogramList.ChromatogramIdentity(0).Id);
    }

    [TestMethod]
    public void Read_ProfileSpectrum_HasMsLevelAndProfileCv()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);
        var s = msd.Run.SpectrumList!.GetSpectrum(0, getBinaryData: false);

        Assert.AreEqual(1, s.CvParam(CVID.MS_ms_level).ValueAs<int>());
        Assert.IsTrue(s.HasCVParam(CVID.MS_profile_spectrum));
        Assert.IsFalse(s.HasCVParam(CVID.MS_centroid_spectrum));
        Assert.IsTrue(s.HasCVParam(CVID.MS_positive_scan));
        Assert.AreEqual(400.0, s.CvParam(CVID.MS_base_peak_m_z).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void Read_Ms2Spectrum_ExposesBothPrecursors()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);
        var s = msd.Run.SpectrumList!.GetSpectrum(1, getBinaryData: false);

        Assert.AreEqual(2, s.CvParam(CVID.MS_ms_level).ValueAs<int>());
        Assert.AreEqual(2, s.Precursors.Count);

        var p0 = s.Precursors[0];
        Assert.AreEqual(810.79, p0.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(35.0, p0.Activation.CvParam(CVID.MS_collision_energy).ValueAs<double>(), 1e-9);
        Assert.IsTrue(p0.Activation.HasCVParam(CVID.MS_collision_induced_dissociation));
        Assert.AreEqual(1, p0.SelectedIons.Count);
        Assert.AreEqual(810.7894, p0.SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(2, p0.SelectedIons[0].CvParam(CVID.MS_charge_state).ValueAs<int>());

        var p1 = s.Precursors[1];
        Assert.AreEqual(542.21, p1.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>(), 1e-9);
        Assert.AreEqual(3, p1.SelectedIons[0].CvParam(CVID.MS_charge_state).ValueAs<int>());
    }

    [TestMethod]
    public void Read_ScanInfo_ScanStartTimeAndScanWindows()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);
        var s = msd.Run.SpectrumList!.GetSpectrum(0, getBinaryData: false);

        Assert.AreEqual(1, s.ScanList.Scans.Count);
        var scan = s.ScanList.Scans[0];
        Assert.AreEqual(0.005, scan.CvParam(CVID.MS_scan_start_time).ValueAs<double>(), 1e-9);
        Assert.AreEqual("FTMS + p ESI Full ms", scan.CvParam(CVID.MS_filter_string).Value);
        Assert.AreEqual(2, scan.ScanWindows.Count);
        Assert.AreEqual(200.0, scan.ScanWindows[0].CvParam(CVID.MS_scan_window_lower_limit).ValueAs<double>(), 1e-9);
        Assert.AreEqual(600.0, scan.ScanWindows[0].CvParam(CVID.MS_scan_window_upper_limit).ValueAs<double>(), 1e-9);
    }

    [TestMethod]
    public void Read_BinaryData_RoundTripsMZIntensity()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);
        var s = msd.Run.SpectrumList!.GetSpectrum(0, getBinaryData: true);

        var mz = s.GetMZArray();
        var inten = s.GetIntensityArray();
        Assert.IsNotNull(mz); Assert.IsNotNull(inten);
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0, 400.0, 500.0 }, mz!.Data);
        // Intensities were float on the way in; assert with tolerance to absorb the f→d widen.
        Assert.AreEqual(5, inten!.Data.Count);
        Assert.AreEqual(5e3, inten.Data[3], 1e-6);
        Assert.AreEqual(5, s.DefaultArrayLength);
    }

    [TestMethod]
    public void Read_MetadataOnly_SkipsBinaryData()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);
        var s = msd.Run.SpectrumList!.GetSpectrum(0, getBinaryData: false);

        // Without binary data the BinaryDataArrays list is empty; the cached
        // NumberOfDataPoints from the spectrum metadata still populates
        // DefaultArrayLength so downstream code can pre-size buffers.
        Assert.AreEqual(0, s.BinaryDataArrays.Count);
        Assert.AreEqual(5, s.DefaultArrayLength);
    }

    [TestMethod]
    public void Read_FillInCommonMetadata_AppendsSourceFile()
    {
        var msd = new MSData();
        new MzPeakReaderAdapter().Read(s_fixturePath, msd);

        // FillInCommonMetadata always appends one source-file entry pointing
        // at the input. Plus we don't write any source files in our synthetic
        // fixture, so this should be exactly one.
        Assert.AreEqual(1, msd.FileDescription.SourceFiles.Count);
        Assert.AreEqual(Path.GetFileName(s_fixturePath), msd.FileDescription.SourceFiles[0].Name);

        Assert.IsTrue(msd.Software.Any(sw => sw.HasCVParam(CVID.MS_pwiz)), "pwiz software entry should be present");
    }

    private static string ReadHead(string path, int bytes)
    {
        using var fs = File.OpenRead(path);
        byte[] buf = new byte[bytes];
        int n = fs.Read(buf, 0, bytes);
        // Latin1 is round-trippable byte→char without transformation for the
        // 0x00–0xFF magic bytes we feed into Identify().
        return System.Text.Encoding.Latin1.GetString(buf, 0, n);
    }
}

/// <summary>Shared synthetic dataset used by both adapter and round-trip tests.</summary>
internal static class SyntheticData
{
    public static MzPeakWriter.SpectrumToWrite[] Spectra() => new[]
    {
        new MzPeakWriter.SpectrumToWrite(
            Index: 0, Id: "synthetic=1 scan=1", Time: 0.0050, MsLevel: 1, IsProfile: true,
            Mz: new[] { 100.0, 200.0, 300.0, 400.0, 500.0 },
            Intensity: new[] { 1e3f, 2e3f, 3e3f, 5e3f, 4e3f },
            ScanStartTime: 0.0050, FilterString: "FTMS + p ESI Full ms",
            InstrumentConfigurationRef: 0, IonInjectionTime: 68.227,
            ScanWindowLowerLimits: new double?[] { 200.0, 800.0 },
            ScanWindowUpperLimits: new double?[] { 600.0, 2000.0 },
            ScanPolarity: 1,
            BasePeakMz: 400.0, BasePeakIntensity: 5e3,
            TotalIonCurrent: 15e3,
            LowestObservedMz: 100.0, HighestObservedMz: 500.0,
            SpectrumDataProcessingRef: "DP01"),
        new MzPeakWriter.SpectrumToWrite(
            Index: 1, Id: "synthetic=1 scan=2", Time: 0.0100, MsLevel: 2, IsProfile: false,
            Mz: new[] { 150.5, 250.5, 350.5 },
            Intensity: new[] { 100f, 250f, 80f },
            ScanStartTime: 0.0100,
            Precursors: new[]
            {
                new MzPeakWriter.PrecursorToWrite(
                    IsolationTargetMz: 810.79, IsolationLowerOffset: 2.0, IsolationUpperOffset: 2.0,
                    CollisionEnergy: 35.0, DissociationMethodCurie: "MS:1000133",
                    SelectedIonMz: 810.7894, SelectedIonPeakIntensity: 1234.5, SelectedIonChargeState: 2),
                new MzPeakWriter.PrecursorToWrite(
                    IsolationTargetMz: 542.21, IsolationLowerOffset: 1.5, IsolationUpperOffset: 1.5,
                    CollisionEnergy: 28.0, DissociationMethodCurie: "MS:1000133",
                    SelectedIonMz: 542.2105, SelectedIonChargeState: 3),
            }),
        new MzPeakWriter.SpectrumToWrite(
            Index: 2, Id: "synthetic=1 scan=3", Time: 0.0150, MsLevel: 1, IsProfile: true,
            Mz: new[] { 110.0, 210.0, 310.0, 410.0 },
            Intensity: new[] { 5e2f, 1.5e3f, 2.5e3f, 1e3f }),
    };

    public static MzPeakWriter.ChromatogramToWrite[] Chromatograms() => new[]
    {
        new MzPeakWriter.ChromatogramToWrite(
            Index: 0, Id: "TIC", ChromatogramTypeCurie: "MS:1000235", DataProcessingRef: "DP01",
            Time: new[] { 0.001, 0.002, 0.003 },
            Intensity: new[] { 1e5f, 2e5f, 1.5e5f }),
        new MzPeakWriter.ChromatogramToWrite(
            Index: 1, Id: "BPC", ChromatogramTypeCurie: "MS:1000628", DataProcessingRef: "DP01",
            Time: new[] { 0.001, 0.002, 0.003 },
            Intensity: new[] { 5e4f, 1e5f, 8e4f }),
        new MzPeakWriter.ChromatogramToWrite(
            Index: 2, Id: "SIM 200.0", ChromatogramTypeCurie: "MS:1000626", DataProcessingRef: "DP01",
            Time: new[] { 0.001, 0.002 },
            Intensity: new[] { 100f, 200f }),
    };

    public static MzPeak.FileMetadata FileMetadata() => new(
        FileDescription: new MzPeak.FileDescription(
            Contents: Array.Empty<MzPeakCvParam>(),
            SourceFiles: Array.Empty<MzPeak.SourceFile>()),
        InstrumentConfigurations: Array.Empty<MzPeak.InstrumentConfiguration>(),
        DataProcessingMethods: Array.Empty<DataProcessingMethod>(),
        Software: Array.Empty<SoftwareInfo>(),
        Samples: Array.Empty<SampleInfo>(),
        Run: new RunInfo(
            Id: "synthetic-run",
            DefaultDataProcessingId: "DP01",
            DefaultInstrumentId: 0,
            DefaultSourceFileId: null,
            StartTime: null,
            Parameters: Array.Empty<MzPeakCvParam>()),
        SpectrumCount: 3,
        SpectrumDataPointCount: 12);
}
