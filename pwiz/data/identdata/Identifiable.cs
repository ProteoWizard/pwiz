namespace Pwiz.Data.IdentData;

/// <summary>
/// Base for types in the mzIdentML schema that have a unique <see cref="Id"/> and an optional
/// human-readable <see cref="Name"/>. Port of <c>pwiz::identdata::Identifiable</c>.
/// </summary>
public abstract class Identifiable
{
    /// <summary>Unique identifier within the mzIdentML document scope.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Optional human-readable name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>True when this object carries no information beyond its defaults.</summary>
    public virtual bool IsEmpty => string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Name);
}
