using System.Globalization;
using System.Numerics;
using System.Text;

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
/// Formats floats and doubles the way pwiz C++ does in XML / mzML output.
/// </summary>
/// <remarks>
/// <para>Ports <c>boost::spirit::karma</c>'s <c>real_inserter::call_n</c>
/// (boost/spirit/home/karma/numeric/detail/real_utils.hpp:69) together with the
/// <c>real_policies</c> defaults (real_policies.hpp), as pwiz configures them:</para>
/// <list type="bullet">
///   <item><c>pwiz::util::toString(double)</c> (String.cpp:107) and
///     <c>XMLWriter::Attributes::add(name, double)</c> (XMLWriter.cpp:43) use
///     <c>double12_policy</c>: automatic notation, 12 fractional digits. Every cvParam
///     written from a double goes through this (<c>ParamContainer::set</c>, ParamTypes.cpp:279).</item>
///   <item><c>pwiz::util::toString(float)</c> (String.cpp:124) uses <c>float5_policy</c>:
///     automatic notation, 5 fractional digits — and because karma instantiates the
///     generator on <c>float</c>, every intermediate is computed in <b>single</b> precision.</item>
///   <item><c>ChromatogramList_Agilent</c> (ChromatogramList_Agilent.cpp:245) uses
///     <c>nosci_policy</c>: forced fixed notation, 9 fractional digits.</item>
/// </list>
/// <para>Behavior, all of it emergent from the ported algorithm rather than special-cased:
/// values with <c>|x| &gt;= 1e5</c> or <c>|x| &lt; 1e-3</c> (and not zero) use scientific
/// notation; otherwise fixed. Trailing zeros are stripped but the decimal point and at
/// least one fractional digit always remain (<c>123</c> prints as <c>123.0</c>). The
/// exponent is <c>e</c> plus an optional <c>-</c> plus at least two digits.</para>
/// <para><b>The rounding is binary, not decimal.</b> karma splits the value with
/// <c>modf</c>, scales only the <em>fractional</em> part by <c>10^precision</c> in the
/// number's own floating point type, and takes <c>floor(x + 0.5)</c>. Rounding the whole
/// value in the decimal domain instead (<c>Math.Round(v, 12, MidpointRounding.AwayFromZero)</c>,
/// which is what this class used to do) disagrees on every value whose scaled fraction
/// lands near <c>.5</c>: 296.2006143414705 has a fractional part that scales to
/// 200614341470.497955, so karma emits <c>296.20061434147</c>, while the decimal view sees
/// a tie and rounds up to <c>296.200614341471</c>. That accounted for thousands of
/// last-digit cvParam differences against the C++ reference output.</para>
/// </remarks>
public static class PwizFloat
{
    private const int Precision = 12;

    /// <summary><c>std::numeric_limits&lt;double&gt;::min()</c> — the smallest positive normal double.</summary>
    private const double DoubleMinNormal = 2.2250738585072014E-308;

    /// <summary><c>std::numeric_limits&lt;float&gt;::min()</c> — the smallest positive normal float.</summary>
    private const float FloatMinNormal = 1.17549435E-38f;

    /// <summary><c>std::numeric_limits&lt;long long&gt;::max()</c> as a double, karma's cutoff for
    /// formatting an integral floating point value through <c>long long</c> instead of digit-by-digit
    /// floating point arithmetic (numeric_utils.hpp:660).</summary>
    private const double MaxLong = 9.2233720368547758E18;

    /// <summary>1e0 .. 1e308 — the exact values of karma's <c>pow10</c> lookup table
    /// (boost/spirit/home/support/detail/pow10.hpp:54). Built by parsing the decimal literals so the
    /// entries are the same correctly-rounded doubles the C++ compiler produces; <c>Math.Pow(10, n)</c>
    /// is off by an ulp for some n above 1e22.</summary>
    private static readonly double[] Pow10Table = CreatePow10Table();

    private static double[] CreatePow10Table()
    {
        var table = new double[309];
        for (int i = 0; i < table.Length; i++)
            table[i] = double.Parse("1e" + i.ToString(CultureInfo.InvariantCulture),
                                    NumberStyles.Float, CultureInfo.InvariantCulture);
        return table;
    }

    /// <summary>
    /// Formats a float in pwiz's canonical XML form — <c>pwiz::util::toString(float)</c>,
    /// i.e. karma's <c>float5_policy</c>: 5 fractional digits, computed in single precision.
    /// </summary>
    public static string ToPwizString(float value)
    {
        // karma stack-overflows on subnormals, so pwiz clamps them to the smallest normal
        // first (String.cpp:116, XMLWriter.cpp:47).
        if (value > 0) value = MathF.Max(FloatMinNormal, value);
        else if (value < 0) value = MathF.Min(-FloatMinNormal, value);
        return Generate(value, 5, forceFixed: false);
    }

    /// <summary>Formats <paramref name="value"/> in pwiz's canonical XML form —
    /// <c>pwiz::util::toString(double)</c>, i.e. karma's <c>double12_policy</c>.</summary>
    public static string ToPwizString(double value)
    {
        if (value > 0) value = Math.Max(DoubleMinNormal, value);
        else if (value < 0) value = Math.Min(-DoubleMinNormal, value);
        return Generate(value, Precision, forceFixed: false);
    }

    /// <summary>Mirrors C printf <c>%.10g</c> formatting — the cpp Shimadzu chromatogram-id
    /// builder uses this for Q1/Q3/CE values: up to 10 significant digits, trailing zeros
    /// stripped, no decimal point on whole numbers (e.g. <c>1059</c> not <c>1059.0</c>;
    /// <c>50.647</c> kept). Different from <see cref="ToKarmaNoSci"/> (which keeps the
    /// trailing <c>.0</c> for karma natural-real form).</summary>
    public static string ToPrintfG10(double v) =>
        v.ToString("G10", CultureInfo.InvariantCulture);

    /// <summary>Mirrors the <c>nosci_policy</c> the cpp vendor readers use to build chromatogram
    /// IDs (e.g. <c>"- SRM SIC Q1=309.0 Q3=228.996 start=0.00005 end=4.505483333"</c>):
    /// forced fixed notation (never scientific, so 0.00005 stays <c>0.00005</c> instead of
    /// becoming <c>5.0e-05</c>) with 9 fractional digits — ChromatogramList_Agilent.cpp:245.
    /// Used by ChromatogramList_Agilent (and any future vendor reader that builds composite
    /// chromatogram IDs from per-transition doubles).</summary>
    public static string ToKarmaNoSci(double value) => Generate(value, 9, forceFixed: true);

    /// <summary>
    /// The karma <c>real_inserter::call_n</c> workhorse (real_utils.hpp:69), generic over the
    /// floating point type so <c>float</c> values round in single precision exactly as karma's
    /// <c>real_generator&lt;float, ...&gt;</c> does.
    /// </summary>
    /// <param name="value">the value to format.</param>
    /// <param name="precision">maximum number of fractional digits — the policy's <c>precision()</c>.</param>
    /// <param name="forceFixed">true for a policy whose <c>floatfield()</c> always returns
    /// <c>fixed</c>; false for the default, which picks fixed or scientific by magnitude.</param>
    private static string Generate<T>(T value, int precision, bool forceFixed)
        where T : IFloatingPointIeee754<T>
    {
        // real_inserter::call short-circuits these two before consulting the policy.
        if (T.IsNaN(value)) return "nan";
        if (T.IsInfinity(value)) return T.IsNegative(value) ? "-inf" : "inf";

        T one = T.One;
        T ten = T.CreateChecked(10);
        T half = T.CreateChecked(0.5);

        // karma tests the sign bit, so -0.0 is "negative" here; the sign is dropped again below
        // if both the integral and fractional parts come out zero.
        bool sign = T.IsNegative(value);
        bool scientific = !forceFixed && IsScientific(value);
        T n = sign ? -value : value;

        // Scientific notation normalizes the value into [1, 10) first. Note the asymmetry: big
        // values are divided by a power of ten, small ones are multiplied by one — worth
        // reproducing, because x / 1e-4 and x * 1e4 do not round alike.
        int dim = 0;
        bool precexpOffset = false;
        if (scientific)
        {
            T log = T.Log10(n);
            if (log > T.Zero)
            {
                dim = ToInt(T.Truncate(log));
                n /= Pow10<T>(dim);
            }
            else if (n < one)
            {
                int exp = ToInt(T.Truncate(-log));
                dim = -exp;

                // Denormalized values would overflow the pow10 table, so scale in two steps.
                int maxExponent10 = MaxExponent10<T>();
                if (exp > maxExponent10)
                {
                    n *= Pow10<T>(maxExponent10);
                    n *= Pow10<T>(exp - maxExponent10);
                }
                else
                    n *= Pow10<T>(exp);

                if (n < one)
                {
                    n *= ten;
                    --dim;
                    precexpOffset = true;
                }
            }
        }

        // Split into integral and fractional parts, then round the fractional part alone by
        // scaling it up by 10^precision and taking floor(x + 0.5). This is the whole ballgame:
        // the rounding happens on the scaled *binary* value, so a decimal midpoint is usually
        // not a midpoint here.
        T integerPart = T.Truncate(n);
        T fractionalPart = n - integerPart;
        T precexp = Pow10<T>(precision);

        if (precexpOffset)
        {
            // The value picked up an extra factor of ten during normalization; karma keeps one
            // more digit through the rounding and lets the truncation below discard it.
            fractionalPart = T.Floor((fractionalPart * precexp + half) * ten) / ten;
        }
        else
            fractionalPart = T.Floor(fractionalPart * precexp + half);

        if (fractionalPart >= precexp)
        {
            // Rounding overflowed into the integral part, e.g. 1.9999999999999 at precision 12.
            fractionalPart = T.Floor(fractionalPart - precexp);
            integerPart += one;
            if (integerPart >= ten && scientific)
            {
                integerPart /= ten;
                ++dim;
            }
        }

        // Strip trailing zeros from the fractional digits (no policy here asks for them),
        // shrinking the emitted precision to match.
        T longIntPart = T.Floor(integerPart);
        T longFracPart = fractionalPart;
        T fracPartFloor = longFracPart;
        int prec = precision;
        if (!T.IsZero(longFracPart))
        {
            while (prec != 0 && Remainder10(longFracPart) == 0)
            {
                longFracPart = T.Floor(longFracPart / ten);
                --prec;
            }
        }
        else
            prec = 0;

        if (precision != prec)
            longFracPart = fracPartFloor / Pow10<T>(precision - prec);

        var sb = new StringBuilder(32);

        // A negative value that rounds away to zero loses its sign.
        if (sign && !(T.IsZero(longIntPart) && T.IsZero(longFracPart)))
            sb.Append('-');

        AppendIntegerDigits(sb, longIntPart);
        sb.Append('.');

        // fraction_part(): left-pad with zeros so the fraction occupies `prec` digits. A zero
        // fraction still emits one digit, which is what keeps whole numbers looking like "1.0".
        T digits = T.IsZero(longFracPart) ? one : T.Ceiling(T.Log10(longFracPart + one));
        for (T pad = T.CreateChecked(prec); digits < pad; digits += one)
            sb.Append('0');
        if (precision != 0)
            AppendIntegerDigits(sb, longFracPart);

        if (scientific)
        {
            sb.Append('e');
            if (dim < 0) sb.Append('-');
            int absDim = Math.Abs(dim);
            if (absDim < 10) sb.Append('0'); // C99 requires at least two exponent digits
            sb.Append(absDim.ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary><c>real_policies::floatfield()</c> — zero and anything in [1e-3, 1e5) is fixed,
    /// everything else is scientific. The C++ comparison promotes a float operand to double,
    /// so compare in double here too.</summary>
    private static bool IsScientific<T>(T value) where T : IFloatingPointIeee754<T>
    {
        if (T.IsZero(value)) return false;
        double abs = Math.Abs(ToDouble(value));
        return abs >= 1e5 || abs < 1e-3;
    }

    /// <summary><c>traits::remainder&lt;10&gt;</c> for a floating point value:
    /// <c>cast_to_long(fmod(n, 10))</c>, i.e. the low decimal digit (numeric_utils.hpp:527).</summary>
    private static long Remainder10<T>(T value) where T : IFloatingPointIeee754<T>
        => (long)Math.Floor(ToDouble(value % T.CreateChecked(10)));

    /// <summary><c>karma::int_inserter&lt;10&gt;</c> applied to an integral-valued float or double
    /// (numeric_utils.hpp:645): values inside <c>long long</c> range are truncated to an integer and
    /// printed; larger ones are peeled digit by digit in floating point.</summary>
    private static void AppendIntegerDigits<T>(StringBuilder sb, T value) where T : IFloatingPointIeee754<T>
    {
        double d = ToDouble(value);
        if (Math.Abs(d) < MaxLong)
        {
            sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
            return;
        }

        T ten = T.CreateChecked(10);
        T remaining = value;
        int exp = 0;
        int start = sb.Length;
        do
        {
            sb.Append((char)('0' + Remainder10(remaining)));
            remaining = T.Floor(value / Pow10<T>(Math.Min(++exp, 308)));
        } while (!T.IsZero(remaining));

        // The digits came out least-significant first.
        for (int i = start, j = sb.Length - 1; i < j; i++, j--)
            (sb[i], sb[j]) = (sb[j], sb[i]);
    }

    /// <summary>karma's <c>pow10</c> table entry, narrowed to <typeparamref name="T"/> the way
    /// <c>pow10_helper&lt;float&gt;</c> narrows the double table (pow10.hpp:93).</summary>
    private static T Pow10<T>(int exponent) where T : IFloatingPointIeee754<T>
        => T.CreateSaturating(Pow10Table[exponent]);

    /// <summary><c>std::numeric_limits&lt;T&gt;::max_exponent10</c>.</summary>
    private static int MaxExponent10<T>() where T : IFloatingPointIeee754<T>
        => typeof(T) == typeof(float) ? 38 : 308;

    private static double ToDouble<T>(T value) where T : IFloatingPointIeee754<T>
        => double.CreateTruncating(value);

    /// <summary><c>traits::truncate_to_long</c> for an already-truncated value.</summary>
    private static int ToInt<T>(T value) where T : IFloatingPointIeee754<T>
        => (int)ToDouble(value);
}
