using Pwiz.Data.Common.Params;

namespace Pwiz.Data.IdentData;

/// <summary>Single-letter residue with its monoisotopic mass. Port of <c>Residue</c>.</summary>
public sealed class Residue
{
    /// <summary>One-letter residue code.</summary>
    public char Code { get; set; }

    /// <summary>Monoisotopic mass in daltons.</summary>
    public double Mass { get; set; }

    /// <summary>True when neither field is set.</summary>
    public bool IsEmpty => Code == '\0' && Mass == 0;
}

/// <summary>An ambiguous residue (e.g. X) with multiple possible interpretations encoded as
/// CV / user params. Port of <c>AmbiguousResidue</c>.</summary>
public sealed class AmbiguousResidue : ParamContainer
{
    /// <summary>One-letter ambiguous residue code (e.g. 'X').</summary>
    public char Code { get; set; }

    /// <inheritdoc/>
    public override bool IsEmpty => Code == '\0' && base.IsEmpty;
}

/// <summary>Residue mass table used during a search. Port of <c>MassTable</c>.</summary>
public sealed class MassTable
{
    /// <summary>Identifier (mzIdentML <c>id</c> attribute).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>MS levels this mass table applies to.</summary>
    public List<int> MsLevel { get; } = new();

    /// <summary>Residue masses.</summary>
    public List<Residue> Residues { get; } = new();

    /// <summary>Ambiguous residues with their CV-described possible interpretations.</summary>
    public List<AmbiguousResidue> AmbiguousResidue { get; } = new();

    /// <summary>True when no entries are recorded.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Id)
        && MsLevel.Count == 0
        && Residues.Count == 0
        && AmbiguousResidue.Count == 0;
}
