using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Vendor.UNIFI.Tests;

[TestClass]
public class SpectrumListUnifiTests
{
    [TestMethod]
    public void Index_UnifiNoIms_UsesScanIds()
    {
        // cpp SpectrumList_UNIFI.cpp:247-262: legacy UNIFI without IMS or with combined-IMS
        // off → "scan=N" id format. Spectra are 1-based.
        var src = new FakeSource(
            api: RemoteApiType.Unifi,
            hasIms: false,
            spectra: BuildSpectra(3));
        var list = new SpectrumList_UNIFI(src, defaultInstrumentConfiguration: null,
                                          combineIonMobilitySpectra: false);
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual("scan=1", list.SpectrumIdentity(0).Id);
        Assert.AreEqual("scan=2", list.SpectrumIdentity(1).Id);
        Assert.AreEqual("scan=3", list.SpectrumIdentity(2).Id);
        Assert.IsFalse(list.IsWatersConnect);
    }

    [TestMethod]
    public void Index_UnifiCombinedIms_UsesMergedIds()
    {
        var src = new FakeSource(
            api: RemoteApiType.Unifi,
            hasIms: true,
            spectra: BuildSpectra(2));
        var list = new SpectrumList_UNIFI(src, null, combineIonMobilitySpectra: true);
        Assert.AreEqual("merged=1", list.SpectrumIdentity(0).Id);
        Assert.AreEqual("merged=2", list.SpectrumIdentity(1).Id);
    }

    [TestMethod]
    public void Index_WatersConnect_UsesChannelAndScanIds()
    {
        // cpp SpectrumList_UNIFI.cpp:233-246: waters_connect ids include the channel.
        var src = new FakeSource(
            api: RemoteApiType.WatersConnect,
            hasIms: false,
            spectra: BuildSpectra(3));
        // Override the channel/scan mapping: 3 spectra across 2 channels.
        src.ChannelMap = i => i switch
        {
            0 => (0, 0),
            1 => (0, 1),
            _ => (1, 0),
        };
        var list = new SpectrumList_UNIFI(src, null, combineIonMobilitySpectra: false);
        Assert.AreEqual("channelIndex=0 scanIndex=0", list.SpectrumIdentity(0).Id);
        Assert.AreEqual("channelIndex=0 scanIndex=1", list.SpectrumIdentity(1).Id);
        Assert.AreEqual("channelIndex=1 scanIndex=0", list.SpectrumIdentity(2).Id);
        Assert.IsTrue(list.IsWatersConnect);
    }

    [TestMethod]
    public void GetSpectrum_Ms1Profile_PopulatesScanWindowAndArrays()
    {
        var unifi = new UnifiSpectrum
        {
            MsLevel = 1,
            RetentionTime = 0.5,
            ScanPolarity = UnifiPolarity.Positive,
            EnergyLevel = UnifiEnergyLevel.Low,
            DataIsContinuous = true,
            ScanRange = (50.0, 1200.0),
            ArrayLength = 3,
            MzArray = new[] { 100.0, 200.0, 300.0 },
            IntensityArray = new[] { 10.0, 20.0, 30.0 },
        };
        var src = new FakeSource(RemoteApiType.Unifi, hasIms: false, spectra: new[] { unifi });
        var list = new SpectrumList_UNIFI(src, null, combineIonMobilitySpectra: false);
        var spec = list.GetSpectrum(0, getBinaryData: true);

        Assert.AreEqual(0, spec.Index);
        Assert.AreEqual("scan=1", spec.Id);
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_MS1_spectrum));
        Assert.AreEqual(1, spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_positive_scan));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_profile_spectrum));
        Assert.IsFalse(spec.Params.HasCVParam(CVID.MS_centroid_spectrum));

        // Single scan with the configured RT + scan window.
        Assert.AreEqual(1, spec.ScanList.Scans.Count);
        var scan = spec.ScanList.Scans[0];
        Assert.AreEqual(0.5, scan.CvParam(CVID.MS_scan_start_time).ValueAs<double>());
        Assert.AreEqual(1, scan.ScanWindows.Count);
        Assert.AreEqual(50.0, scan.ScanWindows[0].CvParam(CVID.MS_scan_window_lower_limit).ValueAs<double>());
        Assert.AreEqual(1200.0, scan.ScanWindows[0].CvParam(CVID.MS_scan_window_upper_limit).ValueAs<double>());
        // Energy-level → preset_scan_configuration mapping.
        Assert.AreEqual(1, scan.CvParam(CVID.MS_preset_scan_configuration).ValueAs<int>());

        // Binary arrays match what the source returned.
        Assert.AreEqual(2, spec.BinaryDataArrays.Count); // m/z + intensity
        CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0 }, spec.BinaryDataArrays[0].Data);
        CollectionAssert.AreEqual(new[] { 10.0, 20.0, 30.0 }, spec.BinaryDataArrays[1].Data);
        Assert.AreEqual(3, spec.DefaultArrayLength);
        // No precursor on MS1.
        Assert.AreEqual(0, spec.Precursors.Count);
    }

    [TestMethod]
    public void GetSpectrum_Ms2_PopulatesIsolationWindowFromScanRange()
    {
        // cpp SpectrumList_UNIFI.cpp:162-174: for MS2, the isolation window is centered on
        // (scanRange.high - scanRange.low) / 2 with offsets so the lower/upper bounds match
        // the function's acquired range. This is a quirk of UNIFI (no per-scan precursor m/z
        // surfaced) — we mirror it byte-for-byte.
        var unifi = new UnifiSpectrum
        {
            MsLevel = 2,
            RetentionTime = 1.5,
            ScanPolarity = UnifiPolarity.Positive,
            EnergyLevel = UnifiEnergyLevel.High,
            DataIsContinuous = false,
            ScanRange = (100.0, 600.0),
            ArrayLength = 0,
        };
        var src = new FakeSource(RemoteApiType.Unifi, false, new[] { unifi });
        var list = new SpectrumList_UNIFI(src, null, false);
        var spec = list.GetSpectrum(0, getBinaryData: false);

        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_MSn_spectrum));
        Assert.AreEqual(2, spec.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_centroid_spectrum));
        Assert.AreEqual(2, spec.ScanList.Scans[0].CvParam(CVID.MS_preset_scan_configuration).ValueAs<int>());

        Assert.AreEqual(1, spec.Precursors.Count);
        var precursor = spec.Precursors[0];
        // (high-low)/2 = 250; lower offset = 250 - 100 = 150; upper offset = 600 - 250 = 350.
        Assert.AreEqual(250.0, precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>());
        Assert.AreEqual(150.0, precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_lower_offset).ValueAs<double>());
        Assert.AreEqual(350.0, precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_upper_offset).ValueAs<double>());
        Assert.AreEqual(1, precursor.SelectedIons.Count);
        Assert.AreEqual(250.0, precursor.SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>());
        Assert.IsTrue(precursor.Activation.HasCVParam(CVID.MS_beam_type_collision_induced_dissociation));
    }

    [TestMethod]
    public void GetSpectrum_LowEnergyOnMs2_Throws()
    {
        // Sanity check: cpp asserts MSe Low always lands on MS1 (cpp SpectrumList_UNIFI.cpp:145).
        var bad = new UnifiSpectrum
        {
            MsLevel = 2,
            EnergyLevel = UnifiEnergyLevel.Low,
            DataIsContinuous = true,
            ScanRange = (50, 100),
        };
        var src = new FakeSource(RemoteApiType.Unifi, false, new[] { bad });
        var list = new SpectrumList_UNIFI(src, null, false);

        Assert.ThrowsException<InvalidOperationException>(() => list.GetSpectrum(0));
    }

    [TestMethod]
    public void GetSpectrum_CentroidOnContinuousData_AppliesProfileLie()
    {
        // cpp SpectrumList_UNIFI.cpp:213-214: when source is continuum but caller asks for
        // centroid, set MS_profile_spectrum on top of MS_centroid_spectrum so the outer
        // SpectrumList_PeakPicker takes its short-circuit branch instead of running its
        // algorithm on the SDK's centroid arrays.
        var unifi = new UnifiSpectrum
        {
            MsLevel = 1,
            ScanPolarity = UnifiPolarity.Positive,
            DataIsContinuous = true,
            ScanRange = (50, 100),
            ArrayLength = 1,
            MzArray = new[] { 75.0 },
            IntensityArray = new[] { 1.0 },
        };
        var src = new FakeSource(RemoteApiType.Unifi, false, new[] { unifi });
        var list = new SpectrumList_UNIFI(src, null, false);
        var spec = list.GetSpectrum(0, getBinaryData: true, doCentroid: true);

        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_centroid_spectrum));
        Assert.IsTrue(spec.Params.HasCVParam(CVID.MS_profile_spectrum), "Profile lie should be set");
    }

    private static List<UnifiSpectrum> BuildSpectra(int count)
    {
        var list = new List<UnifiSpectrum>();
        for (int i = 0; i < count; i++)
            list.Add(new UnifiSpectrum
            {
                MsLevel = 1,
                ScanPolarity = UnifiPolarity.Positive,
                EnergyLevel = UnifiEnergyLevel.Low,
                DataIsContinuous = true,
                ScanRange = (50, 1200),
            });
        return list;
    }

    private sealed class FakeSource : IUnifiDataSource
    {
        private readonly IReadOnlyList<UnifiSpectrum> _spectra;

        public FakeSource(RemoteApiType api, bool hasIms, IReadOnlyList<UnifiSpectrum> spectra)
        {
            RemoteApi = api;
            HasIonMobilityData = hasIms;
            _spectra = spectra;
        }

        public RemoteApiType RemoteApi { get; }
        public bool HasIonMobilityData { get; }
        public int NumberOfSpectra => _spectra.Count;
        public IReadOnlyList<UnifiChromatogramInfo> ChromatogramInfo { get; init; } = Array.Empty<UnifiChromatogramInfo>();
        public Func<int, (int Channel, int Scan)> ChannelMap { get; set; } = _ => (0, 0);

        public int GetMsLevel(int index) => _spectra[index].MsLevel;
        public (int ChannelIndex, int ScanIndexInChannel) GetChannelAndScanIndex(int index) => ChannelMap(index);

        public void GetSpectrum(int index, UnifiSpectrum spectrum, bool getBinaryData, bool doCentroid)
        {
            var src = _spectra[index];
            spectrum.MsLevel = src.MsLevel;
            spectrum.RetentionTime = src.RetentionTime;
            spectrum.ScanPolarity = src.ScanPolarity;
            spectrum.EnergyLevel = src.EnergyLevel;
            spectrum.DriftTime = src.DriftTime;
            spectrum.DataIsContinuous = src.DataIsContinuous;
            spectrum.ScanRange = src.ScanRange;
            spectrum.ArrayLength = src.ArrayLength;
            if (getBinaryData)
            {
                spectrum.MzArray = src.MzArray;
                spectrum.IntensityArray = src.IntensityArray;
                spectrum.DriftTimeArray = src.DriftTimeArray;
            }
        }

        public void GetChromatogram(int index, UnifiChromatogram chromatogram, bool getBinaryData)
            => throw new NotSupportedException();
    }
}
