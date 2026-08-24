using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>Bibliographic reference for the document. Port of <c>BibliographicReference</c>.</summary>
public sealed class BibliographicReference : Identifiable
{
    /// <summary>Authors string.</summary>
    public string Authors { get; set; } = string.Empty;
    /// <summary>Publication name.</summary>
    public string Publication { get; set; } = string.Empty;
    /// <summary>Publisher name.</summary>
    public string Publisher { get; set; } = string.Empty;
    /// <summary>Editor name(s).</summary>
    public string Editor { get; set; } = string.Empty;
    /// <summary>Year of publication.</summary>
    public int Year { get; set; }
    /// <summary>Volume number.</summary>
    public string Volume { get; set; } = string.Empty;
    /// <summary>Issue number.</summary>
    public string Issue { get; set; } = string.Empty;
    /// <summary>Page range.</summary>
    public string Pages { get; set; } = string.Empty;
    /// <summary>Title of the work.</summary>
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(Authors)
        && string.IsNullOrEmpty(Publication)
        && string.IsNullOrEmpty(Publisher)
        && string.IsNullOrEmpty(Editor)
        && Year == 0
        && string.IsNullOrEmpty(Volume)
        && string.IsNullOrEmpty(Issue)
        && string.IsNullOrEmpty(Pages)
        && string.IsNullOrEmpty(Title);
}

/// <summary>A contact (person or organization). Port of <c>pwiz::identdata::Contact</c>.</summary>
public abstract class Contact : IdentifiableParamContainer { }

/// <summary>An organization (company, university, etc.). Port of <c>Organization</c>.</summary>
public sealed class Organization : Contact
{
    /// <summary>Optional parent organization (for hierarchical relationships).</summary>
    public Organization? Parent { get; set; }
}

/// <summary>A person contact. Port of <c>Person</c>.</summary>
public sealed class Person : Contact
{
    /// <summary>Surname / family name.</summary>
    public string LastName { get; set; } = string.Empty;
    /// <summary>Given name.</summary>
    public string FirstName { get; set; } = string.Empty;
    /// <summary>Middle initials.</summary>
    public string MidInitials { get; set; } = string.Empty;
    /// <summary>Organizations the person is affiliated with.</summary>
    public List<Organization> Affiliations { get; } = new();

    /// <inheritdoc/>
    public override bool IsEmpty =>
        base.IsEmpty
        && string.IsNullOrEmpty(LastName)
        && string.IsNullOrEmpty(FirstName)
        && string.IsNullOrEmpty(MidInitials)
        && Affiliations.Count == 0;
}

/// <summary>The role a contact plays in some context (provider, analyst, ...). Port of
/// <c>ContactRole</c>. Cpp models this as a CVParam with an attached Contact pointer; we keep
/// the same shape so downstream code reads naturally.</summary>
public sealed class ContactRole
{
    /// <summary>The role term (e.g. <c>MS_software_vendor</c>).</summary>
    public CVParam Role { get; set; } = new(CVID.CVID_Unknown);

    /// <summary>The contact filling the role.</summary>
    public Contact? ContactPtr { get; set; }

    /// <summary>True when no role is assigned and no contact is referenced.</summary>
    public bool IsEmpty => Role.IsEmpty && ContactPtr is null;
}
