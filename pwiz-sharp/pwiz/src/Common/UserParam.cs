using System.Globalization;
using Pwiz.Data.Common.Cv;

namespace Pwiz.Data.Common.Params;

/// <summary>
/// A free-form user parameter. Use <see cref="CVParam"/> when a CV term is available.
/// Port of pwiz/data::UserParam.
/// </summary>
public sealed class UserParam : IEquatable<UserParam>
{
    /// <summary>Parameter name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parameter value (may be empty).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Datatype hint (e.g. "xsd:float").</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Optional CV term describing the units of the value.</summary>
    public CVID Units { get; set; } = CVID.CVID_Unknown;

    /// <summary>Creates an empty UserParam.</summary>
    public UserParam() { }

    /// <summary>Creates a UserParam with the given fields.</summary>
    public UserParam(string name, string value = "", string type = "", CVID units = CVID.CVID_Unknown)
    {
        Name = name ?? string.Empty;
        Value = value ?? string.Empty;
        Type = type ?? string.Empty;
        Units = units;
    }

    /// <summary>Returns the <see cref="Value"/> parsed as <typeparamref name="T"/>.</summary>
    public T ValueAs<T>()
    {
        if (string.IsNullOrEmpty(Value))
            return (T)Convert.ChangeType(0, typeof(T), CultureInfo.InvariantCulture)!;

        if (typeof(T) == typeof(bool))
            return (T)(object)(Value == "true");

        return (T)Convert.ChangeType(Value, typeof(T), CultureInfo.InvariantCulture)!;
    }

    /// <summary>Converts the value to seconds using <see cref="Units"/>. See <see cref="CVParam.TimeInSeconds"/>.</summary>
    public double TimeInSeconds() => TimeConversion.ToSeconds(Units, ValueAs<double>());

    /// <summary>True iff all four fields are empty/default.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Value)
        && string.IsNullOrEmpty(Type) && Units == CVID.CVID_Unknown;

    /// <inheritdoc/>
    public bool Equals(UserParam? other) =>
        other is not null && Name == other.Name && Value == other.Value
        && Type == other.Type && Units == other.Units;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as UserParam);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Value, Type, Units);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(UserParam? a, UserParam? b) => Equals(a, b);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(UserParam? a, UserParam? b) => !Equals(a, b);
}
