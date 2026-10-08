using System.Globalization;
using System.Text.RegularExpressions;

namespace Pwiz.Util.Chemistry;

/// <summary>Units for <see cref="MZTolerance"/>.</summary>
public enum MZToleranceUnits
{
    /// <summary>Absolute m/z (daltons).</summary>
    Mz,

    /// <summary>Parts per million of the measured value.</summary>
    Ppm,
}

/// <summary>
/// An m/z tolerance expressed as an absolute value (daltons) or a relative value (ppm).
/// </summary>
/// <remarks>Port of pwiz/utility/chemistry/MZTolerance.hpp/.cpp.</remarks>
public readonly record struct MZTolerance(double Value, MZToleranceUnits Units = MZToleranceUnits.Mz)
{
    /// <summary>Applies the tolerance to <paramref name="d"/>, widening its value.</summary>
    public static double operator +(double d, MZTolerance t) => t.Units switch
    {
        MZToleranceUnits.Mz => d + t.Value,
        MZToleranceUnits.Ppm => d + Math.Abs(d) * t.Value * 1e-6,
        _ => throw new InvalidOperationException($"Unknown MZTolerance units: {t.Units}"),
    };

    /// <summary>Applies the tolerance to <paramref name="d"/>, narrowing its value.</summary>
    public static double operator -(double d, MZTolerance t) => t.Units switch
    {
        MZToleranceUnits.Mz => d - t.Value,
        MZToleranceUnits.Ppm => d - Math.Abs(d) * t.Value * 1e-6,
        _ => throw new InvalidOperationException($"Unknown MZTolerance units: {t.Units}"),
    };

    /// <summary>Returns true iff <paramref name="a"/> is strictly within (b - tolerance, b + tolerance).</summary>
    public static bool IsWithinTolerance(double a, double b, MZTolerance tolerance)
        => a > b - tolerance && a < b + tolerance;

    /// <summary>
    /// Returns true iff <paramref name="b"/> - <paramref name="a"/> is greater than the tolerance
    /// (useful when walking sorted mass lists).
    /// </summary>
    public static bool LessThanTolerance(double a, double b, MZTolerance tolerance)
        => a < b - tolerance;

    /// <summary>
    /// Formats the tolerance as "value units" (e.g. "5ppm", "0.01mz"), using invariant culture for the value.
    /// </summary>
    public override string ToString()
    {
        string units = Units == MZToleranceUnits.Ppm ? "ppm" : "mz";
        return Value.ToString("R", CultureInfo.InvariantCulture) + units;
    }

    private static readonly Regex s_parseRegex = new(
        @"^\s*(?<value>[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?)\s*(?<units>[A-Za-z/]+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Parses a string like "5ppm", "5 ppm", "0.01 mz", "4.2 da", "4.2 daltons", "5.0 PPM".
    /// Units recognized (case-insensitive): "mz", "m/z", any token starting with "da" (daltons), "ppm".
    /// </summary>
    /// <exception cref="FormatException">Thrown when the input is malformed or uses unrecognized units.</exception>
    public static MZTolerance Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var match = s_parseRegex.Match(s);
        if (!match.Success)
            throw new FormatException($"Unable to parse MZTolerance: '{s}'");

        double value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        string unitsText = match.Groups["units"].Value.ToLowerInvariant();

        MZToleranceUnits units = unitsText switch
        {
            "mz" or "m/z" => MZToleranceUnits.Mz,
            "ppm" => MZToleranceUnits.Ppm,
            _ when unitsText.StartsWith("da", StringComparison.Ordinal) => MZToleranceUnits.Mz,
            _ => throw new FormatException($"Unrecognized MZTolerance units: '{match.Groups["units"].Value}'"),
        };

        return new MZTolerance(value, units);
    }

    /// <summary>
    /// Tries to parse a string like "5ppm" or "0.01 mz". Returns false on malformed input.
    /// </summary>
    public static bool TryParse(string? s, out MZTolerance result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        try
        {
            result = Parse(s);
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }
}
