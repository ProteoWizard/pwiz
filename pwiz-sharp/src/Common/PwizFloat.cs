using System.Globalization;

namespace Pwiz.Data.Common.Params;

/// <summary>
/// Formats doubles the way pwiz C++ does in XML / mzML output.
/// </summary>
/// <remarks>
/// Ports Boost.Spirit.Karma's <c>real_generator</c> with pwiz's <c>precision = 12</c> policy:
/// <list type="bullet">
///   <item>Values with <c>|x| &gt;= 1e5</c> or <c>|x| &lt; 1e-3</c> (and not zero) use scientific notation.</item>
///   <item>Otherwise fixed with up to 12 fractional digits, trailing zeros stripped.</item>
///   <item>Integer-valued doubles always keep a trailing <c>.0</c>.</item>
///   <item>Scientific exponent is formatted as <c>eSS</c> (zero-padded width 2), positive sign omitted.</item>
/// </list>
/// </remarks>
public static class PwizFloat
{
    private const int Precision = 12;

    /// <summary>Formats <paramref name="value"/> in pwiz's canonical XML form.</summary>
    public static string ToPwizString(double value)
    {
        if (double.IsNaN(value)) return "nan";
        if (double.IsPositiveInfinity(value)) return "inf";
        if (double.IsNegativeInfinity(value)) return "-inf";
        if (value == 0) return "0.0";

        double absValue = Math.Abs(value);
        if (absValue >= 1e5 || absValue < 1e-3)
        {
            int exp = (int)Math.Floor(Math.Log10(absValue));
            double mantissa = value / Math.Pow(10, exp);
            // Rounding can push |mantissa| to >=10 or <1; renormalize.
            if (Math.Abs(mantissa) >= 10.0) { mantissa /= 10.0; exp++; }
            else if (Math.Abs(mantissa) < 1.0 && mantissa != 0) { mantissa *= 10.0; exp--; }

            string m = FormatFixed(mantissa, Precision);
            string expStr = exp < 0
                ? "-" + (-exp).ToString("D2", CultureInfo.InvariantCulture)
                : exp.ToString("D2", CultureInfo.InvariantCulture);
            return m + "e" + expStr;
        }

        return FormatFixed(value, Precision);
    }

    private static string FormatFixed(double value, int precision)
    {
        string s = value.ToString("F" + precision.ToString(CultureInfo.InvariantCulture),
                                  CultureInfo.InvariantCulture);
        int dot = s.IndexOf('.');
        if (dot < 0) return s + ".0";
        int end = s.Length;
        while (end > dot + 2 && s[end - 1] == '0') end--;
        return s[..end];
    }
}
