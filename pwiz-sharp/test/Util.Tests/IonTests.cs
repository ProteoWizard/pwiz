using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class IonTests
{
    private const double Epsilon = 1e-9;

    [TestMethod]
    public void NeutralMass_SingleProton_InvertsIonMass()
    {
        // Round-trip: start with neutral mass, ionize, recover.
        const double neutral = 500.0;
        double mzValue = Ion.Mz(neutral, protonDelta: 1);
        double recovered = Ion.NeutralMass(mzValue, protonDelta: 1);
        Assert.AreEqual(neutral, recovered, Epsilon);
    }

    [TestMethod]
    public void Mz_SingleProton_AddsProtonAndDividesByCharge()
    {
        // +1H ion at neutral mass 999 should have m/z = 999 + proton mass.
        double mzValue = Ion.Mz(999.0, protonDelta: 1);
        Assert.AreEqual(999.0 + PhysicalConstants.Proton, mzValue, Epsilon);
    }

    [TestMethod]
    public void Mz_DoubleCharge_DividesByTwo()
    {
        // [M+2H]2+ ion: m/z = (M + 2*proton) / 2
        const double m = 1000.0;
        double mzValue = Ion.Mz(m, protonDelta: 2);
        Assert.AreEqual((m + 2 * PhysicalConstants.Proton) / 2.0, mzValue, Epsilon);
    }

    [TestMethod]
    public void IonMass_NeutralAddition_NoChargeNeeded()
    {
        // IonMass doesn't divide, so it should work with no charge.
        double iMass = Ion.IonMass(500.0, protonDelta: 0, neutronDelta: 1);
        Assert.AreEqual(500.0 + PhysicalConstants.Neutron, iMass, Epsilon);
    }

    [TestMethod]
    public void NeutralMass_ZeroCharge_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => Ion.NeutralMass(500.0, protonDelta: 1, electronDelta: 1));
    }

    [TestMethod]
    public void Mz_ZeroCharge_Throws()
    {
        Assert.ThrowsException<ArgumentException>(
            () => Ion.Mz(500.0, protonDelta: 2, electronDelta: 2));
    }

    [TestMethod]
    public void NeutralMass_NegativeIon_ProducesPositiveMass()
    {
        // Deprotonated [M-H]- ion: protonDelta = -1, charge = -1
        // m/z ≈ (neutral - proton) / -1 = -(neutral - proton), which is negative.
        // But C++ convention: m/z is reported as positive for negative ions, so callers pass |m/z|.
        // Round-trip test: compute mz for a -1 ion, recover neutral.
        const double neutral = 500.0;
        double mzValue = Ion.Mz(neutral, protonDelta: -1);
        double recovered = Ion.NeutralMass(mzValue, protonDelta: -1);
        Assert.AreEqual(neutral, recovered, Epsilon);
    }
}
