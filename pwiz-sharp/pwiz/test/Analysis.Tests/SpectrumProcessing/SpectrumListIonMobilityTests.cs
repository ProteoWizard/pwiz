using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Port of cpp <c>SpectrumList_IonMobility_Test</c>. Mirrors cpp's pattern: one entry
/// point (<see cref="IonMobility"/>) that calls helpers for each dispatch path —
/// mzML CV-param sniff (drift-time, 1/K0, FAIMS, legacy user param, 3-array combined),
/// vendor-capability dispatch via <see cref="IIonMobilitySpectrumList"/>, CCS dispatch
/// via <see cref="IIonMobilityCcsConversion"/>, and the SONAR helper paths.
/// </summary>
[TestClass]
public class SpectrumListIonMobilityTests
{
    [TestMethod]
    public void IonMobility()
    {
        TestMzMlDriftTimeSniff();
        TestMzMlInverseReducedSniff();
        TestMzMlFaimsCompensationSniff();
        TestMzMlLegacyUserParamSniff();
        TestMzMlCombinedIonMobilityArray();
        TestEmptyListYieldsNone();
        TestVendorDispatchViaInterface();
        TestSonarDispatch();
        TestCcsConversionThrowsWhenNotSupported();
    }

    // Spectrum with a per-scan drift-time CV param => DriftTimeMsec.
    private static void TestMzMlDriftTimeSniff()
    {
        var pl = NewMzMlList(scanCvid: CVID.MS_ion_mobility_drift_time);
        var wrapper = new SpectrumList_IonMobility(pl);
        Assert.AreEqual(IonMobilityUnits.DriftTimeMsec, wrapper.IonMobilityUnits, "mzML drift-time scan CV");
        Assert.IsFalse(wrapper.HasCombinedIonMobility, "drift-time scan: not combined");
        Assert.IsFalse(wrapper.IsWatersSonarData, "drift-time scan: not SONAR");
    }

    // Spectrum with a per-scan 1/K0 CV param => InverseReducedIonMobilityVsecPerCm2.
    private static void TestMzMlInverseReducedSniff()
    {
        var pl = NewMzMlList(scanCvid: CVID.MS_inverse_reduced_ion_mobility);
        var wrapper = new SpectrumList_IonMobility(pl);
        Assert.AreEqual(IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2, wrapper.IonMobilityUnits,
            "mzML 1/K0 scan CV");
    }

    // Spectrum with a FAIMS compensation-voltage CV param on the spectrum (NOT scan) => CompensationV.
    private static void TestMzMlFaimsCompensationSniff()
    {
        var pl = NewMzMlList(spectrumCvid: CVID.MS_FAIMS_compensation_voltage);
        var wrapper = new SpectrumList_IonMobility(pl);
        Assert.AreEqual(IonMobilityUnits.CompensationV, wrapper.IonMobilityUnits, "mzML FAIMS CV");
    }

    // Legacy "drift time" user param on the scan (older mzML conventions) => DriftTimeMsec.
    private static void TestMzMlLegacyUserParamSniff()
    {
        var pl = NewMzMlList(scanUserParamName: "drift time");
        var wrapper = new SpectrumList_IonMobility(pl);
        Assert.AreEqual(IonMobilityUnits.DriftTimeMsec, wrapper.IonMobilityUnits, "mzML legacy user param");
    }

    // Spectrum with an IM binary array (3-array format) => combined + units inferred from array type.
    private static void TestMzMlCombinedIonMobilityArray()
    {
        var pl = NewMzMlList(arrayCvid: CVID.MS_mean_ion_mobility_array);
        var wrapper = new SpectrumList_IonMobility(pl);
        Assert.AreEqual(IonMobilityUnits.DriftTimeMsec, wrapper.IonMobilityUnits, "combined-IM array DT");
        Assert.IsTrue(wrapper.HasCombinedIonMobility, "combined-IM array: HasCombinedIonMobility");

        // And the 1/K0 array variant.
        var plK0 = NewMzMlList(arrayCvid: CVID.MS_raw_inverse_reduced_ion_mobility_array);
        var wrapperK0 = new SpectrumList_IonMobility(plK0);
        Assert.AreEqual(IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2, wrapperK0.IonMobilityUnits,
            "combined-IM array 1/K0");
        Assert.IsTrue(wrapperK0.HasCombinedIonMobility, "combined-IM array 1/K0: HasCombinedIonMobility");
    }

    // No spectra to probe + no vendor interface => units default to None.
    private static void TestEmptyListYieldsNone()
    {
        var wrapper = new SpectrumList_IonMobility(new SpectrumListSimple());
        Assert.AreEqual(IonMobilityUnits.None, wrapper.IonMobilityUnits, "empty list: None");
        Assert.IsFalse(wrapper.HasCombinedIonMobility, "empty list: not combined");
        Assert.IsFalse(wrapper.IsWatersSonarData, "empty list: not SONAR");
        Assert.IsFalse(wrapper.CanConvertIonMobilityAndCcs(IonMobilityUnits.DriftTimeMsec),
            "empty list: cannot convert");
    }

    // A list implementing IIonMobilitySpectrumList wins over the mzML sniff.
    private static void TestVendorDispatchViaInterface()
    {
        var vendor = new FakeVendorList(IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2,
            hasCombined: true, isWatersSonar: false, canConvert: true);
        var wrapper = new SpectrumList_IonMobility(vendor);
        Assert.AreEqual(IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2, wrapper.IonMobilityUnits,
            "vendor: units come from the interface");
        Assert.IsTrue(wrapper.HasCombinedIonMobility, "vendor: HasCombinedIonMobility");
        Assert.IsFalse(wrapper.IsWatersSonarData, "vendor: not SONAR");
        // CanConvert must check that the queried units match the list's.
        Assert.IsTrue(wrapper.CanConvertIonMobilityAndCcs(IonMobilityUnits.InverseReducedIonMobilityVsecPerCm2),
            "vendor: convertible (matching units)");
        Assert.IsFalse(wrapper.CanConvertIonMobilityAndCcs(IonMobilityUnits.DriftTimeMsec),
            "vendor: rejected (mismatched units)");
        // CCS dispatch returns the fake's pre-baked answer.
        Assert.AreEqual(123.0, wrapper.IonMobilityToCcs(1.5, 500.0, 2), 0.0, "vendor: CCS dispatch");
        Assert.AreEqual(0.42, wrapper.CcsToIonMobility(123.0, 500.0, 2), 0.0, "vendor: IM dispatch");
    }

    // SONAR dispatch goes through IWatersSonarSpectrumList. Non-sonar wrappers throw.
    private static void TestSonarDispatch()
    {
        var vendor = new FakeVendorList(IonMobilityUnits.WatersSonar,
            hasCombined: false, isWatersSonar: true, canConvert: false);
        var wrapper = new SpectrumList_IonMobility(vendor);
        Assert.IsTrue(wrapper.IsWatersSonarData, "sonar: IsWatersSonarData");
        Assert.AreEqual((10, 15), wrapper.SonarMzToBinRange(500.0, 1.0), "sonar: bin range");
        // Fake's formula: 500 + bin * 1.5 -> bin=13 -> 519.5
        Assert.AreEqual(519.5, wrapper.SonarBinToPrecursorMz(13), "sonar: bin -> mz");

        // Non-sonar wrapper: sonar helpers throw.
        var nonSonar = new SpectrumList_IonMobility(new SpectrumListSimple());
        Assert.ThrowsException<InvalidOperationException>(() => nonSonar.SonarMzToBinRange(500.0, 1.0));
        Assert.ThrowsException<InvalidOperationException>(() => nonSonar.SonarBinToPrecursorMz(0));
    }

    // CCS conversion on a list without IIonMobilityCcsConversion throws.
    private static void TestCcsConversionThrowsWhenNotSupported()
    {
        var pl = NewMzMlList(scanCvid: CVID.MS_ion_mobility_drift_time);
        var wrapper = new SpectrumList_IonMobility(pl);
        // mzML sniff path -> units set, but no CCS interface -> conversion throws.
        Assert.AreEqual(IonMobilityUnits.DriftTimeMsec, wrapper.IonMobilityUnits, "ccs: sniff worked");
        Assert.IsFalse(wrapper.CanConvertIonMobilityAndCcs(IonMobilityUnits.DriftTimeMsec), "ccs: cannot convert");
        Assert.ThrowsException<InvalidOperationException>(() => wrapper.IonMobilityToCcs(1.5, 500, 2));
        Assert.ThrowsException<InvalidOperationException>(() => wrapper.CcsToIonMobility(150, 500, 2));
    }

    // --- helpers ---

    // Builds a SpectrumListSimple with a single spectrum carrying the supplied CV signals.
    // Any unsupplied parameter is omitted, so callers can isolate one sniff path at a time.
    private static SpectrumListSimple NewMzMlList(
        CVID scanCvid = CVID.CVID_Unknown,
        CVID spectrumCvid = CVID.CVID_Unknown,
        CVID arrayCvid = CVID.CVID_Unknown,
        string? scanUserParamName = null)
    {
        var pl = new SpectrumListSimple();
        var spectrum = new Spectrum { Index = 0, Id = "scan=1", DefaultArrayLength = 0 };
        var scan = new Scan();
        if (scanCvid != CVID.CVID_Unknown)
            scan.CVParams.Add(new CVParam(scanCvid, "1.5"));
        if (scanUserParamName is not null)
            scan.UserParams.Add(new UserParam(scanUserParamName, "1.5"));
        spectrum.ScanList.Scans.Add(scan);
        if (spectrumCvid != CVID.CVID_Unknown)
            spectrum.Params.CVParams.Add(new CVParam(spectrumCvid, "30.0"));
        if (arrayCvid != CVID.CVID_Unknown)
        {
            var arr = new BinaryDataArray();
            arr.CVParams.Add(new CVParam(arrayCvid, string.Empty));
            spectrum.BinaryDataArrays.Add(arr);
        }
        pl.Spectra.Add(spectrum);
        return pl;
    }

    // Fake vendor list — implements both capability interfaces. The mzML sniff path
    // shouldn't fire when this is the inner list.
    private sealed class FakeVendorList : SpectrumListBase,
        IIonMobilitySpectrumList, IIonMobilityCcsConversion, IWatersSonarSpectrumList
    {
        private readonly bool _canConvert;
        public FakeVendorList(IonMobilityUnits units, bool hasCombined, bool isWatersSonar, bool canConvert)
        {
            IonMobilityUnits = units;
            HasCombinedIonMobility = hasCombined;
            IsWatersSonar = isWatersSonar;
            _canConvert = canConvert;
        }
        public override int Count => 0;
        public override SpectrumIdentity SpectrumIdentity(int index) => throw new NotImplementedException();
        public override Spectrum GetSpectrum(int index, bool getBinaryData = false) => throw new NotImplementedException();

        // IIonMobilitySpectrumList
        public IonMobilityUnits IonMobilityUnits { get; }
        public bool HasCombinedIonMobility { get; }
        public bool IsWatersSonar { get; }

        // IIonMobilityCcsConversion
        public bool CanConvertIonMobilityAndCcs => _canConvert;
        public double IonMobilityToCcs(double ionMobility, double mz, int charge) => 123.0;
        public double CcsToIonMobility(double ccs, double mz, int charge) => 0.42;

        // IWatersSonarSpectrumList — fixed responses so the asserts can pin them.
        public (int Start, int End) SonarMzToBinRange(double precursorMz, double tolerance) => (10, 15);
        public double SonarBinToPrecursorMz(int bin) => 500.0 + bin * 1.5;
    }
}
