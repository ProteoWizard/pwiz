namespace Pwiz.Util.Misc;

/// <summary>Floating-point comparison helpers.</summary>
/// <remarks>Port of pwiz/utility/misc/almost_equal.hpp.</remarks>
public static class FloatingPoint
{
    // Machine epsilon (aka numeric_limits::epsilon()). .NET's double.Epsilon is the smallest
    // positive value, NOT machine epsilon, so we define the right constants here.
    internal const double DoubleMachineEpsilon = 2.2204460492503131e-16;
    internal const float SingleMachineEpsilon = 1.1920929e-7f;

    /// <summary>
    /// True iff |a − b| / scale &lt; <paramref name="multiplier"/> × ε, where
    /// scale = a if a ≠ 0 else 1 and ε is the machine epsilon. Matches pwiz's <c>almost_equal</c>.
    /// </summary>
    public static bool AlmostEqual(double a, double b, int multiplier = 1)
    {
        double scale = a == 0.0 ? 1.0 : a;
        return System.Math.Abs((a - b) / scale) < multiplier * DoubleMachineEpsilon;
    }

    /// <summary>True iff |a − b| / scale &lt; <paramref name="multiplier"/> × ε (float version).</summary>
    public static bool AlmostEqual(float a, float b, int multiplier = 1)
    {
        float scale = a == 0.0f ? 1.0f : a;
        return System.Math.Abs((a - b) / scale) < multiplier * SingleMachineEpsilon;
    }
}
