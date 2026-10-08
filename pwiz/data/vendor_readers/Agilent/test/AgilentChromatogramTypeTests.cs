using Agilent.MassSpectrometry.DataAnalysis;
using Pwiz.Data.Common.Cv;

namespace Pwiz.Vendor.Agilent.Tests;

/// <summary>
/// Covers <see cref="ChromatogramList_Agilent.TranslateAsChromatogramType"/>, the port of cpp
/// <c>translateAsChromatogramType</c> (<c>Reader_Agilent_Detail.cpp:231</c>). None of the shipped
/// <c>.d</c> fixtures has a pump or sampler signal, so the reference mzMLs exercise only the
/// absorption branch; this asserts the whole table.
/// </summary>
[TestClass]
public class AgilentChromatogramTypeTests
{
    [TestMethod]
    public void TranslateAsChromatogramType_ClassifiesEveryDeviceBucket()
    {
        // (device type, signal description, is instrument curve) -> expected chromatogram term.
        var cases = new[]
        {
            // Pumps classify on the description, case-insensitively, and are kept whether or not
            // they came from the InstrumentCurves table - that is where LowflowPump's traces live.
            (DeviceType.LowFlowPump,             " Flow",      false, CVID.MS_flow_rate_chromatogram),
            (DeviceType.LowFlowPump,             " Pressure",  false, CVID.MS_pressure_chromatogram),
            (DeviceType.LowFlowPump,             " Flow",      true,  CVID.MS_flow_rate_chromatogram),
            (DeviceType.LowFlowPump,             " Pressure",  true,  CVID.MS_pressure_chromatogram),
            (DeviceType.BinaryPump,              "FLOW",       true,  CVID.MS_flow_rate_chromatogram),
            (DeviceType.QuaternaryPump,          "pressure",   true,  CVID.MS_pressure_chromatogram),
            (DeviceType.CapillaryPump,           "Solvent B",  true,  CVID.CVID_Unknown),
            (DeviceType.NanoPump,                "",           false, CVID.CVID_Unknown),

            // Samplers fall through to the pump branch in cpp rather than having their own.
            (DeviceType.ALS,                     " Pressure",  true,  CVID.MS_pressure_chromatogram),
            (DeviceType.WellPlateSampler,        " Flow",      false, CVID.MS_flow_rate_chromatogram),
            (DeviceType.CTC,                     "Temperature", true, CVID.CVID_Unknown),

            // Detectors: absorption from the Chromatograms table, dropped from InstrumentCurves.
            (DeviceType.DiodeArrayDetector,      "Sig=272,16", false, CVID.MS_absorption_chromatogram),
            (DeviceType.DiodeArrayDetector,      "Sig=272,16", true,  CVID.CVID_Unknown),
            (DeviceType.VariableWavelengthDetector, "",        false, CVID.MS_absorption_chromatogram),
            (DeviceType.FlameIonizationDetector,  "",          false, CVID.MS_absorption_chromatogram),
            (DeviceType.CompactLC1220DAD,         "",          false, CVID.MS_absorption_chromatogram),

            // Everything else - including the MS device itself - contributes no chromatogram.
            (DeviceType.Unknown,                 " Pressure",  false, CVID.CVID_Unknown),
            (DeviceType.QuadrupoleTimeOfFlight,  " Flow",      false, CVID.CVID_Unknown),
            (DeviceType.ThermostattedColumnCompartment, "Temperature", true, CVID.CVID_Unknown),
        };

        foreach (var (deviceType, description, isInstrumentCurve, expected) in cases)
        {
            var signal = new AgilentSignal("Device1", "A", description, isInstrumentCurve, deviceType);
            Assert.AreEqual(expected, ChromatogramList_Agilent.TranslateAsChromatogramType(signal),
                $"{deviceType} / \"{description}\" / instrumentCurve={isInstrumentCurve}");
        }
    }
}
