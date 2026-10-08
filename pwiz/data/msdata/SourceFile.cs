using Pwiz.Data.Common.Params;

namespace Pwiz.Data.MsData.Sources;

/// <summary>Types of spectra expected in the file.</summary>
/// <remarks>Port of pwiz::msdata::FileContent.</remarks>
public sealed class FileContent : ParamContainer { }

/// <summary>Description of a source file. Port of pwiz::msdata::SourceFile.</summary>
public sealed class SourceFile : ParamContainer
{
    /// <summary>Unique id for this file within the document.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Name of the source file (no location).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URI-formatted location the file came from.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Creates an empty source file.</summary>
    public SourceFile() { }

    /// <summary>Creates a source file with the given id/name/location.</summary>
    public SourceFile(string id, string name = "", string location = "")
    {
        Id = id ?? string.Empty;
        Name = name ?? string.Empty;
        Location = location ?? string.Empty;
    }

    /// <inheritdoc/>
    public override bool IsEmpty =>
        string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Location) && base.IsEmpty;
}

/// <summary>Contact-info param bag. Port of pwiz::msdata::Contact.</summary>
public sealed class Contact : ParamContainer { }

/// <summary>Document-scoped metadata: file content + source-file list + contacts.</summary>
/// <remarks>Port of pwiz::msdata::FileDescription.</remarks>
public sealed class FileDescription
{
    /// <summary>Summary of spectrum types expected in the file.</summary>
    public FileContent FileContent { get; set; } = new();

    /// <summary>Source files this mzML was generated from.</summary>
    public List<SourceFile> SourceFiles { get; } = new();

    /// <summary>Contacts.</summary>
    public List<Contact> Contacts { get; } = new();

    /// <summary>True iff all fields are empty.</summary>
    public bool IsEmpty =>
        FileContent.IsEmpty && SourceFiles.Count == 0 && Contacts.Count == 0;
}
