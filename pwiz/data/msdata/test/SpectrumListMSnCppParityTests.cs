using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.MSn;
using Pwiz.Data.MsData.Spectra;

namespace Pwiz.Data.MsData.Tests.MSn;

/// <summary>
/// Port of cpp's <c>SpectrumList_MSn_Test.cpp</c>. cpp reads a fixed two-scan fixture in each of
/// the MSn flavours and asserts absolute values - scan ids, TIC, base peak, isolation target,
/// possible charge states, array lengths. The fixtures here are cpp's own, extracted verbatim
/// into <c>TestData/msn/</c>.
/// </summary>
/// <remarks>
/// MSnRoundTripTests writes with the port's serializer and reads it back with the port's reader.
/// That cannot catch a reader and writer that agree with each other but disagree with pwiz: a
/// mis-scaled TIC, a dropped possible-charge-state, or an off-by-one array length would survive
/// the round trip untouched. These cases read bytes pwiz C++ produced and check pwiz's numbers.
/// </remarks>
[TestClass]
public class SpectrumListMSnCppParityTests
{
    /// <summary>The flavours cpp drives through its test(): the scan=116 / scan=118 fixture.</summary>
    private static readonly (string Fixture, MSnType Type, int MsLevel)[] V2Formats =
    {
        ("testMS1", MSnType.Ms1, 1),
        ("testBMS1", MSnType.Bms1, 1),
        ("testMS2", MSnType.Ms2, 2),
        ("testBMS2", MSnType.Bms2, 2),
        ("testCMS2", MSnType.Cms2, 2),
    };

    /// <summary>The flavours cpp drives through its test_v3(): the scan=36 / scan=508 fixture,
    /// which carries EZ lines and so reports determined charges rather than possible ones.</summary>
    private static readonly (string Fixture, MSnType Type, int MsLevel)[] V3Formats =
    {
        ("testCMS1_v3", MSnType.Cms1, 1),
        ("testMS2_v3", MSnType.Ms2, 2),
        ("testCMS2_v3", MSnType.Cms2, 2),
    };

    [TestMethod]
    public void MSnFlavours_ReadCppsValues()
    {
        var failures = new List<string>();
        foreach (var (fixture, type, msLevel) in V2Formats)
            Try(failures, fixture, () => CheckCppExpectations(Read(fixture, type), msLevel, fixture));
        foreach (var (fixture, type, msLevel) in V3Formats)
            Try(failures, fixture, () => CheckCppV3Expectations(Read(fixture, type), msLevel, fixture));

        Assert.AreEqual(0, failures.Count,
            $"{failures.Count} of {V2Formats.Length + V3Formats.Length} MSn flavours disagree with pwiz C++:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static void Try(List<string> failures, string fixture, Action check)
    {
        try { check(); }
        catch (Exception ex) { failures.Add($"  {fixture}: {ex.GetType().Name}: {ex.Message}"); }
    }

    /// <summary>cpp's test_v3(): a different fixture, and EZ lines give a determined charge plus
    /// an accurate mass where the v2 fixture only knows the charge might be 2 or 3.</summary>
    private static void CheckCppV3Expectations(MSData msd, int msLevel, string where)
    {
        var list = msd.Run.SpectrumList!;
        Assert.AreEqual(2, list.Count, $"{where}: spectrum count");
        Assert.AreEqual("scan=36", list.SpectrumIdentity(0).Id, $"{where}: id[0]");
        Assert.AreEqual("scan=508", list.SpectrumIdentity(1).Id, $"{where}: id[1]");

        var s = list.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual(msLevel, s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), $"{where}: ms level");
        Assert.AreEqual(296.2, s.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 5e-1, $"{where}: TIC");
        Assert.AreEqual(109.3, s.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 5e-1,
            $"{where}: base peak");
        Assert.AreEqual(25, s.DefaultArrayLength, $"{where}: defaultArrayLength[0]");

        if (msLevel == 1)
        {
            Assert.AreEqual(0, s.Precursors.Count, $"{where}: MS1 has no precursor");
        }
        else
        {
            Assert.AreEqual(1, s.Precursors.Count, $"{where}: precursor count");
            var ion = s.Precursors[0].SelectedIons[0];
            Assert.AreEqual(612.19,
                s.Precursors[0].IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>(),
                5e-2, $"{where}: isolation target");
            Assert.AreEqual(611.1855, ion.CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(), 5e-2,
                $"{where}: selected ion m/z");
            Assert.IsTrue(ion.CvParam(CVID.MS_possible_charge_state).IsEmpty,
                $"{where}: an EZ line gives a determined charge, not a possible one");
            CollectionAssert.AreEqual(new[] { "1" }, ChargeStates(ion), $"{where}: charge states");
            CollectionAssert.AreEqual(new[] { 611.1855 }, AccurateMasses(ion), $"{where}: accurate mass");
        }

        var s1 = list.GetSpectrum(1, getBinaryData: true);
        Assert.AreEqual(1, s1.ScanList.Scans.Count, $"{where}: scan count[1]");
        Assert.AreEqual(6.2752,
            s1.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time).TimeInSeconds() / 60, 5e-4,
            $"{where}: scan start time (minutes)");

        if (msLevel != 1)
        {
            // cpp: this scan has TWO selected ions, charges 3 then 2, with their own masses.
            Assert.AreEqual(2, s1.Precursors[0].SelectedIons.Count, $"{where}: selected ion count[1]");
            Assert.AreEqual(441.23,
                s1.Precursors[0].IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>(),
                5e-2, $"{where}: isolation target[1]");
            Assert.AreEqual(440.2471843,
                s1.Precursors[0].SelectedIons[0].CvParam(CVID.MS_selected_ion_m_z).ValueAs<double>(), 1e-4,
                $"{where}: selected ion m/z[1]");
            var allCharges = s1.Precursors[0].SelectedIons.SelectMany(ChargeStates).ToList();
            CollectionAssert.AreEqual(new[] { "3", "2" }, allCharges, $"{where}: charge states[1]");
            var allMasses = s1.Precursors[0].SelectedIons.SelectMany(AccurateMasses).ToList();
            Assert.AreEqual(2, allMasses.Count, $"{where}: accurate mass count[1]");
            Assert.AreEqual(1318.7270, allMasses[0], 5e-4, $"{where}: accurate mass[0]");
            Assert.AreEqual(880.4527, allMasses[1], 5e-4, $"{where}: accurate mass[1]");
        }
    }

    private static List<string> ChargeStates(SelectedIon ion) =>
        ion.CVParams.Where(p => p.Cvid == CVID.MS_charge_state).Select(p => p.Value).ToList();

    /// <summary>cpp records the EZ line's accurate mass as a userParam, not a CV term.</summary>
    private static List<double> AccurateMasses(SelectedIon ion) =>
        ion.UserParams.Where(p => p.Name == "accurate mass")
           .Select(p => double.Parse(p.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();


    /// <summary>cpp's test(): the same assertions for every flavour, keyed off the MS level.</summary>
    private static void CheckCppExpectations(MSData msd, int msLevel, string where)
    {
        var list = msd.Run.SpectrumList!;
        Assert.AreEqual(2, list.Count, $"{where}: spectrum count");

        // ---- scan 0 (scan=116) ----
        Assert.AreEqual("scan=116", list.SpectrumIdentity(0).Id, $"{where}: id[0]");
        Assert.AreEqual(0, list.SpectrumIdentity(0).Index, $"{where}: index[0]");

        var s = list.GetSpectrum(0, getBinaryData: true);
        Assert.AreEqual("scan=116", s.Id, $"{where}: spectrum[0].id");
        Assert.AreEqual(msLevel, s.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), $"{where}: ms level");
        Assert.AreEqual(385.4, s.Params.CvParam(CVID.MS_total_ion_current).ValueAs<double>(), 5e-1,
            $"{where}: TIC");
        Assert.AreEqual(65.0, s.Params.CvParam(CVID.MS_base_peak_intensity).ValueAs<double>(), 5e-1,
            $"{where}: base peak intensity");

        if (msLevel == 1)
        {
            Assert.AreEqual(0, s.Precursors.Count, $"{where}: MS1 has no precursor");
        }
        else
        {
            Assert.AreEqual(1, s.Precursors.Count, $"{where}: precursor count");
            var precursor = s.Precursors[0];
            Assert.AreEqual(1, precursor.SelectedIons.Count, $"{where}: selected ion count");
            Assert.AreEqual(536.39,
                precursor.IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>(), 5e-2,
                $"{where}: isolation target");
            AssertPossibleChargeStates(precursor.SelectedIons[0], where + " scan 0");
        }

        Assert.AreEqual(106, s.DefaultArrayLength, $"{where}: defaultArrayLength[0]");
        Assert.AreEqual(2, s.BinaryDataArrays.Count, $"{where}: binary array count");
        Assert.IsNotNull(s.GetMZArray(), $"{where}: m/z array present");
        Assert.IsNotNull(s.GetIntensityArray(), $"{where}: intensity array present");
        Assert.AreEqual(106, s.GetMZArray()!.Data.Count, $"{where}: m/z array length");

        // ---- scan 1 (scan=118) ----
        Assert.AreEqual("scan=118", list.SpectrumIdentity(1).Id, $"{where}: id[1]");
        Assert.AreEqual(1, list.SpectrumIdentity(1).Index, $"{where}: index[1]");

        var s1 = list.GetSpectrum(1, getBinaryData: true);
        Assert.AreEqual("scan=118", s1.Id, $"{where}: spectrum[1].id");
        Assert.AreEqual(msLevel, s1.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0), $"{where}: ms level[1]");
        Assert.AreEqual(1, s1.ScanList.Scans.Count, $"{where}: scan count[1]");
        Assert.AreEqual(0.4573 * 60,
            s1.ScanList.Scans[0].CvParam(CVID.MS_scan_start_time).TimeInSeconds(), 5e-4,
            $"{where}: scan start time");

        if (msLevel == 1)
        {
            Assert.AreEqual(0, s1.Precursors.Count, $"{where}: MS1 has no precursor[1]");
        }
        else
        {
            Assert.AreEqual(1, s1.Precursors.Count, $"{where}: precursor count[1]");
            Assert.AreEqual(464.98,
                s1.Precursors[0].IsolationWindow.CvParam(CVID.MS_isolation_window_target_m_z).ValueAs<double>(),
                1e-5, $"{where}: isolation target[1]");
            AssertPossibleChargeStates(s1.Precursors[0].SelectedIons[0], where + " scan 1");
        }

        Assert.AreEqual(85, s1.DefaultArrayLength, $"{where}: defaultArrayLength[1]");
        Assert.AreEqual(85, s1.GetMZArray()!.Data.Count, $"{where}: m/z array length[1]");
    }

    /// <summary>
    /// cpp is specific here: these scans carry POSSIBLE charge states (2 and 3), not a determined
    /// one, so charge_state must be absent and two possible_charge_state params must be present.
    /// Collapsing them into a single charge would silently invent certainty the data lacks.
    /// </summary>
    private static void AssertPossibleChargeStates(SelectedIon ion, string where)
    {
        Assert.IsTrue(ion.CvParam(CVID.MS_charge_state).IsEmpty,
            $"{where}: charge_state must be absent when only possible charges are known");
        var charges = ion.CVParams
            .Where(p => p.Cvid == CVID.MS_possible_charge_state)
            .Select(p => p.Value)
            .ToList();
        CollectionAssert.AreEqual(new[] { "2", "3" }, charges, $"{where}: possible charge states");
    }

    private static MSData Read(string fixture, MSnType type)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "msn", fixture + ".txt");
        byte[] bytes = type.IsText()
            ? File.ReadAllBytes(path)
            // cpp stores the binary flavours base64 and decodes them with its own lenient decoder;
            // .NET's requires the padding cpp's literals leave off.
            : Convert.FromBase64String(Pad(File.ReadAllText(path).Trim()));

        var msd = new MSData();
        using var stream = new MemoryStream(bytes);
        new SerializerMSn(type).Read(stream, msd);
        return msd;
    }

    private static string Pad(string base64) =>
        base64.Length % 4 == 0 ? base64 : base64 + new string('=', 4 - base64.Length % 4);
}
