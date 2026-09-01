using Agilent.MassSpectrometry.DataAnalysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;

namespace Pwiz.Vendor.Agilent.Tests;

/// <summary>
/// Covers the CommonInstrumentParams block: the MS:1003761 instrument class cvParam
/// (<see cref="Reader_Agilent.TranslateInstrumentClass"/>) and the free-text "instrument model"
/// userParam beside it.
///
/// The reference-mzML harness cannot see the second one. Its diff ignores userParams entirely,
/// so the reader read the device NAME from the wrong SDK - the MassSpec SDK's device-TYPE name
/// ("QTOF") rather than the "IM-MS QTOF" an ion mobility file actually carries - through every
/// green run of the whole Agilent suite.
/// </summary>
[TestClass]
public class AgilentInstrumentMetadataTests
{
    [TestMethod]
    public void TranslateInstrumentClass_MapsEveryDeviceType()
    {
        // (device type, file has ion mobility data) -> expected instrument class term.
        var cases = new[]
        {
            (DeviceType.Quadrupole,             false, CVID.MS_quadrupole),
            (DeviceType.IonTrap,                false, CVID.MS_ion_trap),
            (DeviceType.TimeOfFlight,           false, CVID.MS_time_of_flight),
            (DeviceType.TandemQuadrupole,       false, CVID.MS_triple_quadrupole),
            (DeviceType.QuadrupoleTimeOfFlight, false, CVID.MS_quadrupole_time_of_flight),

            // Ion mobility wins over the device type, and is the only way to reach Q-IMS-TOF:
            // MIDAC exposes no device table, so cpp reports DeviceType_Unknown for these files.
            (DeviceType.Unknown,                true,  CVID.MS_quadrupole_ion_mobility_time_of_flight),
            (DeviceType.QuadrupoleTimeOfFlight, true,  CVID.MS_quadrupole_ion_mobility_time_of_flight),

            // Non-MS devices and unclassifiable ones contribute nothing.
            (DeviceType.Unknown,                false, CVID.CVID_Unknown),
            (DeviceType.Mixed,                  false, CVID.CVID_Unknown),
            (DeviceType.DiodeArrayDetector,     false, CVID.CVID_Unknown),
            (DeviceType.BinaryPump,             false, CVID.CVID_Unknown),
        };

        foreach (var (deviceType, hasIms, expected) in cases)
        {
            CVID actual = Reader_Agilent.TranslateInstrumentClass(deviceType, hasIms);
            Assert.AreEqual(expected, actual, $"{deviceType}, hasIonMobilityData={hasIms}");
        }
    }

    [TestMethod]
    public void InstrumentParams_CarryClassTermAndTheDeviceNameTheFileHolds()
    {
        // (fixture, expected class term, expected "instrument model" userParam value).
        // MIDAC's FileInfo.InstrumentName is a different SOURCE from the MassSpec SDK's
        // GetDeviceName, not a different spelling of one - hence the IM case.
        AssertInstrumentParams("ImsSynthCCS.d",
            CVID.MS_quadrupole_ion_mobility_time_of_flight, "IM-MS QTOF");
        AssertInstrumentParams("GFb_4Scan_TimeSegs_1530_100ng.d",
            CVID.MS_triple_quadrupole, "TandemQuadrupole");
    }

    private static void AssertInstrumentParams(string fixtureDirName, CVID expectedClass,
        string expectedDeviceName)
    {
        string? root = FindTestDataRoot();
        if (root is null)
        {
            Assert.Inconclusive("Agilent test data tree not found.");
            return;
        }
        string path = Path.Combine(root, fixtureDirName);
        if (!Directory.Exists(path))
        {
            Assert.Inconclusive($"{fixtureDirName} not present under test data.");
            return;
        }

        var msd = new MSData();
        new Reader_Agilent().Read(path, msd);

        var group = msd.ParamGroups.SingleOrDefault(p => p.Id == "CommonInstrumentParams");
        Assert.IsNotNull(group, $"{fixtureDirName}: no CommonInstrumentParams group");

        var terms = group.CVParams.Select(p => p.Cvid).ToList();
        CollectionAssert.Contains(terms, CVID.MS_Agilent_instrument_model,
            $"{fixtureDirName}: vendor model term");
        CollectionAssert.Contains(terms, expectedClass,
            $"{fixtureDirName}: instrument class term");

        var model = group.UserParams.SingleOrDefault(p => p.Name == "instrument model");
        Assert.IsNotNull(model, $"{fixtureDirName}: no instrument model userParam");
        Assert.AreEqual(expectedDeviceName, model.Value, fixtureDirName);
    }

    private static string? FindTestDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "pwiz", "data", "vendor_readers", "Agilent",
                "Reader_Agilent_Test.data");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
