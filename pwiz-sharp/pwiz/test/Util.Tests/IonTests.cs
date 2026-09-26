using Pwiz.Util.Chemistry;

namespace Pwiz.Util.Tests.Chemistry;

[TestClass]
public class IonTests
{
    private const double Epsilon = 1e-9;

    [TestMethod]
    public void MzAndNeutralMass_RoundTripAndArithmetic()
    {
        // [M+H]+, [M+2H]2+, [M-H]- all round-trip neutral mass cleanly.
        foreach (var (neutralMass, protonDelta) in new[] { (500.0, 1), (1000.0, 2), (500.0, -1) })
        {
            double mz = Ion.Mz(neutralMass, protonDelta);
            double recovered = Ion.NeutralMass(mz, protonDelta);
            Assert.AreEqual(neutralMass, recovered, Epsilon, $"round-trip protonDelta={protonDelta}");

            // m/z = (M + n*proton) / n by definition.
            double expected = (neutralMass + protonDelta * PhysicalConstants.Proton) / protonDelta;
            Assert.AreEqual(expected, mz, Epsilon, $"explicit formula protonDelta={protonDelta}");
        }

        // IonMass doesn't divide by charge, so a 0-proton + 1-neutron addition is valid.
        Assert.AreEqual(500.0 + PhysicalConstants.Neutron,
            Ion.IonMass(500.0, protonDelta: 0, neutronDelta: 1), Epsilon);
    }

    [TestMethod]
    public void Mz_NeutralMass_ZeroNetCharge_Throws()
    {
        // Equal protons and electrons → net charge 0 → division by zero rejected up-front.
        Assert.ThrowsException<ArgumentException>(
            () => Ion.NeutralMass(500.0, protonDelta: 1, electronDelta: 1));
        Assert.ThrowsException<ArgumentException>(
            () => Ion.Mz(500.0, protonDelta: 2, electronDelta: 2));
    }
}
