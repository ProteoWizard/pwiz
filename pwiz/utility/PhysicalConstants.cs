namespace Pwiz.Util.Chemistry;

/// <summary>
/// Fundamental physical constants used in mass spectrometry calculations.
/// Values are in unified atomic mass units (u).
/// </summary>
/// <remarks>
/// Port of constants from pwiz/utility/chemistry/Chemistry.hpp.
/// Values must match the C++ source exactly.
/// </remarks>
public static class PhysicalConstants
{
    /// <summary>Mass of a proton in unified atomic mass units.</summary>
    public const double Proton = 1.00727646688;

    /// <summary>Mass of a neutron in unified atomic mass units.</summary>
    public const double Neutron = 1.00866491560;

    /// <summary>Mass of an electron in unified atomic mass units.</summary>
    public const double Electron = 0.00054857991;
}
