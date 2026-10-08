using Agilent.MassSpectrometry.DataAnalysis;

namespace Pwiz.Vendor.Agilent.Tests;

[TestClass]
public class AgilentPeakFilterTests
{
    /// <summary>
    /// Asking MHDAC to centroid requires handing it a peak filter object; asking for profile
    /// must not.
    /// </summary>
    /// <remarks>
    /// <para>A null peak filter does NOT mean "no filtering" to MHDAC - it means "do not
    /// centroid". Given null, the SDK ignores <c>DesiredMSStorageType.PeakElseProfile</c> and
    /// returns whatever the file stores. cpp always constructs a default-valued
    /// <c>MsdrPeakFilter</c> for the centroid call (MassHunterData.cpp:712-724) and passes null
    /// only for the quadrupole devices MHDAC cannot centroid at all.</para>
    /// <para>Covered here rather than in the reference harness because the decision is what
    /// matters and it needs neither the SDK nor a data file: the harness deliberately does not
    /// assert that a selected MS level must change a spectrum, since declining is legitimate for
    /// quadrupole data. <c>Reader_Agilent_Neg_MS_002_1scan_ProfileOnlyVendorCentroid</c> covers
    /// the reader end to end on the one fixture that centroids on the fly.</para>
    /// </remarks>
    [TestMethod]
    public void PeakFilter_SuppliedExactlyWhenAskingTheSdkToCentroid()
    {
        (bool PreferProfile, DeviceType Device, bool Expected, string Why)[] cases =
        {
            // Centroid requested on a TOF-family device: MHDAC can centroid, so it needs the filter.
            (false, DeviceType.QuadrupoleTimeOfFlight, true, "Q-TOF centroid request"),
            (false, DeviceType.TimeOfFlight, true, "TOF centroid request"),

            // Centroid requested on a device MHDAC cannot centroid: cpp passes null here too.
            (false, DeviceType.Quadrupole, false, "quadrupole cannot be centroided"),
            (false, DeviceType.TandemQuadrupole, false, "tandem quadrupole cannot be centroided"),

            // Profile requested: never a filter, whatever the device.
            (true, DeviceType.QuadrupoleTimeOfFlight, false, "Q-TOF profile request"),
            (true, DeviceType.TimeOfFlight, false, "TOF profile request"),
            (true, DeviceType.Quadrupole, false, "quadrupole profile request"),
            (true, DeviceType.TandemQuadrupole, false, "tandem quadrupole profile request"),
        };

        foreach (var (preferProfile, device, expected, why) in cases)
            Assert.AreEqual(expected, AgilentRawData.NeedsPeakFilter(preferProfile, device), why);
    }
}
