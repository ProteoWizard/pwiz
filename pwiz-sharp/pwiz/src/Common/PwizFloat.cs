using System.Globalization;

namespace Pwiz.Data.Common.Params;

/// <summary>
/// Encodes pwiz C++-style XML <c>id</c> / <c>idref</c> attribute values. Mirrors cpp
/// <c>encode_xml_id</c> (XMLWriter.cpp:369) — first character must be NCNameStartChar
/// (<c>[A-Za-z_]</c>); subsequent characters must be NCNameChar (<c>[A-Za-z0-9_.-]</c>);
/// anything else (including UTF-8 multi-byte sequences) gets replaced with
/// <c>_xNNNN_</c> per byte. Used by MzmlWriter so unicode / space-bearing run ids and
/// software ids round-trip with the cpp reference mzMLs.
/// </summary>
public static class XmlIdEncoding
{
    /// <summary>Returns <paramref name="id"/> encoded for use as an XML id/idref. Empty
    /// input round-trips as empty (cpp throws but the writer guards beforehand).</summary>
    public static string Encode(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        // UTF-8 byte-oriented encoding to match cpp's per-byte loop. Walk the byte
        // representation; emit valid NCName bytes literally, encode everything else.
        var bytes = System.Text.Encoding.UTF8.GetBytes(id);
        var sb = new System.Text.StringBuilder(bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            bool ok = i == 0 ? IsNCNameStartChar(b) : IsNCNameChar(b);
            if (ok)
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append("_x").Append(b.ToString("x4", CultureInfo.InvariantCulture)).Append('_');
            }
        }
        return sb.ToString();
    }

    private static bool IsNCNameStartChar(byte c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';

    private static bool IsNCNameChar(byte c) =>
        IsNCNameStartChar(c) || (c >= '0' && c <= '9') || c == '.' || c == '-';

    /// <summary>Reverses <see cref="Encode"/> — collapses every <c>_xNNNN_</c> escape back to
    /// its source byte. cpp's reader decodes on the way in so the in-memory MSData id matches
    /// what the file's writer started with; mirror that so a round-trip read/write of a cpp
    /// reference mzML keeps the same logical IDs.</summary>
    public static string Decode(string s)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains("_x", System.StringComparison.Ordinal))
            return s;
        var bytes = new System.Collections.Generic.List<byte>(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            // Match _xNNNN_ — exactly four hex digits framed by underscores.
            if (i + 6 < s.Length && s[i] == '_' && s[i + 1] == 'x' && s[i + 6] == '_'
                && IsHex(s[i + 2]) && IsHex(s[i + 3]) && IsHex(s[i + 4]) && IsHex(s[i + 5]))
            {
                int b = (HexVal(s[i + 2]) << 12) | (HexVal(s[i + 3]) << 8) | (HexVal(s[i + 4]) << 4) | HexVal(s[i + 5]);
                // b is the original byte value (cpp encodes with leading 0s up to 4 hex digits).
                bytes.Add((byte)b);
                i += 7;
            }
            else
            {
                // Literal char — fall back to UTF-8 encoding (handles non-ASCII passthrough).
                foreach (byte ub in System.Text.Encoding.UTF8.GetBytes(new[] { s[i] }))
                    bytes.Add(ub);
                i++;
            }
        }
        return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static int HexVal(char c) =>
        c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
}

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

    /// <summary>
    /// Formats a float in pwiz's canonical XML form. pwiz C++ emits floats via boost
    /// lexical_cast which historically uses <c>numeric_limits&lt;float&gt;::digits10 = 6</c>
    /// (6 significant figures) — in scientific notation that's 5 fractional digits in the
    /// mantissa. The double overload keeps 12 fractional digits.
    /// </summary>
    public static string ToPwizString(float value) => Format(value, 5);

    /// <summary>Formats <paramref name="value"/> in pwiz's canonical XML form.</summary>
    public static string ToPwizString(double value) => Format(value, Precision);

    /// <summary>Mirrors C printf <c>%.10g</c> formatting — the cpp Shimadzu chromatogram-id
    /// builder uses this for Q1/Q3/CE values: up to 10 significant digits, trailing zeros
    /// stripped, no decimal point on whole numbers (e.g. <c>1059</c> not <c>1059.0</c>;
    /// <c>50.647</c> kept). Different from <see cref="ToKarmaNoSci"/> (which keeps the
    /// trailing <c>.0</c> for karma natural-real form).</summary>
    public static string ToPrintfG10(double v) =>
        v.ToString("G10", CultureInfo.InvariantCulture);

    /// <summary>Mirrors <c>boost::spirit::karma::nosci</c> double formatting that the cpp
    /// vendor readers use to build chromatogram IDs (e.g.
    /// <c>"- SRM SIC Q1=309.0 Q3=228.996 start=0.00005 end=4.505483333"</c>): always fixed
    /// notation (never scientific), up to 9 fractional digits, trailing zeros stripped past
    /// the first one — so whole numbers keep the trailing <c>.0</c> and fractional values
    /// keep just enough digits to be unambiguous. <c>karma::nosci</c> with pwiz's precision
    /// policy produces this form; the chromatogram reference mzMLs it generated diff per-byte
    /// against G-format output otherwise.
    /// Used by ChromatogramList_Agilent (and any future vendor reader that builds composite
    /// chromatogram IDs from per-transition floats).</summary>
    public static string ToKarmaNoSci(double v)
    {
        if (v == 0) return "0.0";
        // Always fixed notation — cpp's karma::nosci suppresses scientific even for very
        // small magnitudes (0.00005 stays "0.00005", not "5E-05"). 9 fractional digits
        // matches the precision the reference mzMLs were generated with.
        string fixedStr = v.ToString("F9", CultureInfo.InvariantCulture);
        int dot = fixedStr.IndexOf('.');
        if (dot < 0) return fixedStr + ".0";
        int end = fixedStr.Length;
        while (end > dot + 2 && fixedStr[end - 1] == '0') end--;
        return fixedStr[..end];
    }

    private static string Format(double value, int precision)
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

            string m = FormatFixed(mantissa, precision);
            string expStr = exp < 0
                ? "-" + (-exp).ToString("D2", CultureInfo.InvariantCulture)
                : exp.ToString("D2", CultureInfo.InvariantCulture);
            return m + "e" + expStr;
        }

        return FormatFixed(value, precision);
    }

    private static string FormatFixed(double value, int precision)
    {
        // Pre-round with AwayFromZero to match Boost.Spirit.Karma's default tie-break (and
        // pwiz's reference output). C#'s "F" format defaults to banker's rounding which would
        // emit e.g. "632.26562" instead of "632.26563" for the exact float 632.265625.
        double rounded = Math.Round(value, precision, MidpointRounding.AwayFromZero);
        string s = rounded.ToString("F" + precision.ToString(CultureInfo.InvariantCulture),
                                    CultureInfo.InvariantCulture);
        int dot = s.IndexOf('.');
        if (dot < 0) return s + ".0";
        int end = s.Length;
        while (end > dot + 2 && s[end - 1] == '0') end--;
        return s[..end];
    }
}
