using System.Buffers;
using System.Globalization;
using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Params;

/// <summary>
/// A controlled-vocabulary tag-value pair. Port of pwiz/data::CVParam.
/// </summary>
/// <remarks>
/// <see cref="Value"/> is always stored as a string (matches mzML serialization);
/// numeric getters parse on demand via <see cref="ValueAs{T}"/>.
/// </remarks>
public sealed class CVParam : IEquatable<CVParam>
{
    /// <summary>The CV term identifier.</summary>
    public CVID Cvid { get; set; }

    /// <summary>The string-form value (may be empty for non-valued params).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The units CV term (or <see cref="CVID.CVID_Unknown"/> if not applicable).</summary>
    public CVID Units { get; set; }

    /// <summary>Creates an empty CVParam.</summary>
    public CVParam() : this(CVID.CVID_Unknown) { }

    /// <summary>Creates a non-valued CVParam (e.g. a boolean flag CV term).</summary>
    public CVParam(CVID cvid, CVID units = CVID.CVID_Unknown)
    {
        Cvid = cvid;
        Units = units;
    }

    /// <summary>Creates a CVParam with a string value.</summary>
    public CVParam(CVID cvid, string value, CVID units = CVID.CVID_Unknown)
    {
        Cvid = cvid;
        Value = value ?? string.Empty;
        Units = units;
    }

    /// <summary>Creates a CVParam with a double value, formatted to match pwiz C++.</summary>
    public CVParam(CVID cvid, double value, CVID units = CVID.CVID_Unknown)
        : this(cvid, PwizFloat.ToPwizString(value), units) { }

    /// <summary>Creates a CVParam with a float value, formatted to match pwiz C++.</summary>
    public CVParam(CVID cvid, float value, CVID units = CVID.CVID_Unknown)
        : this(cvid, PwizFloat.ToPwizString(value), units) { }

    /// <summary>Creates a CVParam with an integer value.</summary>
    public CVParam(CVID cvid, int value, CVID units = CVID.CVID_Unknown)
        : this(cvid, value.ToString(CultureInfo.InvariantCulture), units) { }

    /// <summary>Creates a CVParam with a long value.</summary>
    public CVParam(CVID cvid, long value, CVID units = CVID.CVID_Unknown)
        : this(cvid, value.ToString(CultureInfo.InvariantCulture), units) { }

    /// <summary>Creates a CVParam with a bool value ("true"/"false" per mzML).</summary>
    public CVParam(CVID cvid, bool value, CVID units = CVID.CVID_Unknown)
        : this(cvid, value ? "true" : "false", units) { }

    /// <summary>Returns the <see cref="Value"/> parsed as <typeparamref name="T"/>.</summary>
    public T ValueAs<T>()
    {
        if (string.IsNullOrEmpty(Value))
            return (T)Convert.ChangeType(0, typeof(T), CultureInfo.InvariantCulture)!;

        if (typeof(T) == typeof(bool))
            return (T)(object)(Value == "true");

        return (T)Convert.ChangeType(Value, typeof(T), CultureInfo.InvariantCulture)!;
    }

    /// <summary>Display name of this CV term.</summary>
    public string Name => CvLookup.CvTermInfo(Cvid).Name;

    /// <summary>Display name of the units CV term.</summary>
    public string UnitsName => CvLookup.CvTermInfo(Units).Name;

    /// <summary>
    /// Converts the value to seconds based on <see cref="Units"/>.
    /// Supported: UO_second, UO_minute, UO_hour, UO_millisecond, UO_microsecond, UO_nanosecond,
    /// UO_picosecond, MS_second_OBSOLETE, MS_minute_OBSOLETE. Returns 0 for other units.
    /// </summary>
    public double TimeInSeconds() => TimeConversion.ToSeconds(Units, ValueAs<double>());

    private static readonly SearchValues<char> s_exponentChars = SearchValues.Create("eE");

    /// <summary>Returns the value without scientific notation (throws if not a parseable double).</summary>
    public string ValueFixedNotation()
    {
        if (Value.AsSpan().IndexOfAny(s_exponentChars) < 0) return Value;
        double v = double.Parse(Value, CultureInfo.InvariantCulture);
        return v.ToString("0.0###############", CultureInfo.InvariantCulture);
    }

    /// <summary>True iff all three fields are at their default values.</summary>
    public bool IsEmpty =>
        Cvid == CVID.CVID_Unknown && string.IsNullOrEmpty(Value) && Units == CVID.CVID_Unknown;

    /// <inheritdoc/>
    public bool Equals(CVParam? other) =>
        other is not null && Cvid == other.Cvid && Value == other.Value && Units == other.Units;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CVParam);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Cvid, Value, Units);

    /// <inheritdoc/>
    public override string ToString()
    {
        string s = Name;
        if (!string.IsNullOrEmpty(Value)) s += ": " + Value;
        if (Units != CVID.CVID_Unknown) s += " " + UnitsName + "(s)";
        return s;
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(CVParam? a, CVParam? b) => Equals(a, b);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(CVParam? a, CVParam? b) => !Equals(a, b);
}

internal static class TimeConversion
{
    internal static double ToSeconds(CVID units, double value) => units switch
    {
        CVID.UO_second => value,
        CVID.UO_minute => value * 60,
        CVID.UO_hour => value * 3600,
        CVID.UO_millisecond => value * 1e-3,
        CVID.UO_microsecond => value * 1e-6,
        CVID.UO_nanosecond => value * 1e-9,
        CVID.UO_picosecond => value * 1e-12,
        CVID.MS_second_OBSOLETE => value,
        CVID.MS_minute_OBSOLETE => value * 60,
        _ => 0.0,
    };
}
