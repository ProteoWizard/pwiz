namespace Pwiz.Util.Chemistry;

/// <summary>A single isotope: mass (u) and natural abundance (mole fraction).</summary>
/// <remarks>Port of pwiz/chemistry::MassAbundance.</remarks>
public readonly record struct MassAbundance(double Mass, double Abundance);
