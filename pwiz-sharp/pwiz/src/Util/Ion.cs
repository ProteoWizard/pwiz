namespace Pwiz.Util.Chemistry;

/// <summary>
/// Conversions between m/z and neutral mass for ions.
/// </summary>
/// <remarks>Port of pwiz/utility/chemistry/Ion.hpp.</remarks>
public static class Ion
{
    /// <summary>
    /// Converts the m/z of an ion to a neutral mass.
    /// </summary>
    /// <param name="mz">The m/z to convert.</param>
    /// <param name="protonDelta">The number of extra protons attached to the ion.</param>
    /// <param name="electronDelta">The number of extra electrons attached to the ion.</param>
    /// <param name="neutronDelta">The number of extra neutrons attached to the ion.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="protonDelta"/> equals <paramref name="electronDelta"/> (charge would be zero).</exception>
    public static double NeutralMass(double mz, int protonDelta, int electronDelta = 0, int neutronDelta = 0)
    {
        int charge = protonDelta - electronDelta;
        if (charge == 0)
            throw new ArgumentException("m/z with protonDelta == electronDelta is impossible (charge would be zero).");

        return mz * charge
               - (PhysicalConstants.Proton * protonDelta
                  + PhysicalConstants.Electron * electronDelta
                  + PhysicalConstants.Neutron * neutronDelta);
    }

    /// <summary>
    /// Converts a neutral mass to an ionized mass (does not divide by charge).
    /// </summary>
    public static double IonMass(double neutralMass, int protonDelta, int electronDelta = 0, int neutronDelta = 0)
    {
        return neutralMass
               + PhysicalConstants.Proton * protonDelta
               + PhysicalConstants.Electron * electronDelta
               + PhysicalConstants.Neutron * neutronDelta;
    }

    /// <summary>
    /// Converts a neutral mass to an m/z.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="protonDelta"/> equals <paramref name="electronDelta"/> (charge would be zero).</exception>
    public static double Mz(double neutralMass, int protonDelta, int electronDelta = 0, int neutronDelta = 0)
    {
        int charge = protonDelta - electronDelta;
        if (charge == 0)
            throw new ArgumentException("m/z with protonDelta == electronDelta is impossible (charge would be zero).");

        return IonMass(neutralMass, protonDelta, electronDelta, neutronDelta) / charge;
    }
}
