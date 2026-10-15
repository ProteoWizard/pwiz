// Port of pwiz_tools/BiblioSpec/src/PSM.h

using System.Globalization;

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// A modification on a peptide: the 1-based position and the mass delta.
/// </summary>
/// <remarks>Port of <c>BiblioSpec::SeqMod</c> (PSM.h:43). cpp default ctor sets
/// position=-1, deltaMass=0; we mirror that with the explicit no-arg use site.</remarks>
public readonly record struct SeqMod(int Position, double DeltaMass)
{
    /// <summary>cpp parity: PSM.h:47 default ctor sets position=-1, deltaMass=0.</summary>
    public SeqMod() : this(-1, 0) { }
}

/// <summary>
/// A protein record carrying just an accession. Used as the element of <see cref="PSM.Proteins"/>.
/// </summary>
/// <remarks>
/// cpp parity: PSM.h:57 <c>Protein</c> struct. The cpp code uses <c>set&lt;const Protein*&gt;</c>
/// for pointer-uniqueness; this C# port uses <see cref="HashSet{T}"/> with record-value-equality
/// on <see cref="Accession"/>, which preserves "one protein per accession" without depending on
/// pointer identity.
/// </remarks>
public sealed record Protein(string Accession)
{
    /// <summary>cpp default ctor: empty accession.</summary>
    public Protein() : this(string.Empty) { }
}

/// <summary>
/// A Peptide Spectrum Match: data needed to turn a search result into a library reference
/// spectrum. Spectra can be identified by name, scan number, or zero-based index;
/// most file types use only one of the three.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::PSM</c> (PSM.h:72). N.B. PSM is misleading because BiblioSpec
/// also stores small-molecule matches in this same type (see <see cref="SmallMolMetadata"/>).</para>
/// <para>cpp has a virtual destructor so it can be subclassed (see <see cref="NonRedundantPSM"/>);
/// in C# we declare it as a non-sealed class.</para>
/// </remarks>
public class PSM
{
    /// <summary>
    /// Marker string used by BiblioSpec for precursor-only library entries that have no MS2
    /// scan. Port of cpp <c>PRECURSOR_WITHOUT_MS2_SCAN</c> (PSM.h:110).
    /// </summary>
    public const string PrecursorWithoutMs2Scan = "#PRECURSOR_ONLY#";

    /// <summary>Charge of the spectrum precursor.</summary>
    public int Charge { get; set; }

    /// <summary>Unmodified peptide sequence, <c>[A-Z]*</c>.</summary>
    public string UnmodSeq { get; set; } = string.Empty;

    /// <summary>Modified peptide sequence with inline mass shifts, e.g. <c>"PEPM[+15.99]TIDE"</c>.</summary>
    public string ModifiedSeq { get; set; } = string.Empty;

    /// <summary>List of modifications applied to the peptide sequence.</summary>
    public List<SeqMod> Mods { get; } = new();

    /// <summary>Spectrum scan number (sentinel <c>-1</c> = unset). cpp PSM.h:77.</summary>
    public int SpecKey { get; set; } = -1;

    /// <summary>Zero-based index of the spectrum within its file (sentinel <c>-1</c> = unset). cpp PSM.h:78.</summary>
    public int SpecIndex { get; set; } = -1;

    /// <summary>Score associated with this PSM (interpretation depends on score type).</summary>
    public double Score { get; set; }

    /// <summary>
    /// Ion mobility value — drift time, inverse reduced mobility, or compensation voltage;
    /// units determined by <see cref="IonMobilityType"/>.
    /// </summary>
    public double IonMobility { get; set; }

    /// <summary>Units of <see cref="IonMobility"/>.</summary>
    public IonMobilityType IonMobilityType { get; set; } = IonMobilityType.None;

    /// <summary>Collisional cross section.</summary>
    public double Ccs { get; set; }

    /// <summary>
    /// Spectrum name — the <c>parentFileName</c> attribute from the scanOrigin element, or
    /// reader-supplied native id. cpp PSM.h:83.
    /// </summary>
    public string SpecName { get; set; } = string.Empty;

    /// <summary>
    /// Set of proteins this PSM maps to. Value-equality on the record's <see cref="Protein.Accession"/>
    /// is used to deduplicate; cpp uses pointer-uniqueness on <c>const Protein*</c>.
    /// </summary>
    public HashSet<Protein> Proteins { get; } = new();

    /// <summary>Small-molecule metadata (only populated for small-molecule library entries).</summary>
    public SmallMolMetadata SmallMolMetadata { get; } = new();

    /// <summary>
    /// Reset all fields to their default empty / zero values. Mirrors cpp PSM.h:94 <c>clear()</c>.
    /// </summary>
    public virtual void Clear()
    {
        Charge = 0;
        UnmodSeq = string.Empty;
        ModifiedSeq = string.Empty;
        Mods.Clear();
        SpecKey = -1;
        SpecIndex = -1;
        Score = 0;
        IonMobility = 0;
        IonMobilityType = IonMobilityType.None;
        Ccs = 0;
        SpecName = string.Empty;
        SmallMolMetadata.Clear();
        Proteins.Clear();
    }

    /// <summary>
    /// True if <paramref name="str"/> begins with the <see cref="PrecursorWithoutMs2Scan"/> marker
    /// (i.e. the PSM represents a precursor-only entry).
    /// </summary>
    /// <remarks>cpp parity: PSM.h:112 — uses <c>boost::starts_with</c>; we use ordinal-comparison.</remarks>
    public static bool IsPrecursorOnlyIdentifier(string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return str.StartsWith(PrecursorWithoutMs2Scan, StringComparison.Ordinal);
    }

    /// <summary>True if this PSM's <see cref="SpecName"/> identifies it as a precursor-only entry.</summary>
    public bool IsPrecursorOnly() => IsPrecursorOnlyIdentifier(SpecName);

    /// <summary>
    /// Populate <see cref="SpecName"/> with the precursor-only marker string composed of
    /// molecule name, formula, modified sequence, adduct, and charge.
    /// </summary>
    /// <remarks>cpp parity: PSM.h:122 — <c>boost::lexical_cast&lt;string&gt;(charge)</c> →
    /// <c>charge.ToString(InvariantCulture)</c>.</remarks>
    public void SetPrecursorOnly()
    {
        SpecName = string.Concat(
            PrecursorWithoutMs2Scan, "_",
            SmallMolMetadata.MoleculeName, "_",
            SmallMolMetadata.ChemicalFormula, "_",
            ModifiedSeq, "_",
            SmallMolMetadata.PrecursorAdduct,
            Charge.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// True when the PSM is identifiable (has a spec key, index, or name) AND it has enough
    /// content to be a useful library entry (a complete-enough small-molecule record, or a
    /// non-empty peptide sequence with a charge).
    /// </summary>
    /// <remarks>cpp parity: PSM.h:129.</remarks>
    public bool IsCompleteEnough()
    {
        return (SpecKey >= 0 || SpecIndex >= 0 || !string.IsNullOrEmpty(SpecName))
            && (SmallMolMetadata.IsCompleteEnough()
                || (Charge != 0 && !string.IsNullOrEmpty(UnmodSeq)));
    }

    /// <summary>
    /// Return whichever identifier is set: <see cref="SpecName"/> first, then <see cref="SpecKey"/>,
    /// then <see cref="SpecIndex"/>. Returns the empty string if none are set.
    /// </summary>
    /// <remarks>cpp parity: PSM.h:136 — <c>boost::lexical_cast&lt;string&gt;</c> →
    /// invariant culture ToString.</remarks>
    public string IdAsString()
    {
        if (!string.IsNullOrEmpty(SpecName))
            return SpecName;
        if (SpecKey >= 0)
            return SpecKey.ToString(CultureInfo.InvariantCulture);
        if (SpecIndex >= 0)
            return SpecIndex.ToString(CultureInfo.InvariantCulture);
        return string.Empty;
    }
}

/// <summary>
/// PSM variant used by the non-redundant library writer; overrides the fileId and copies
/// fields normally set by <c>buildTables()</c>.
/// </summary>
/// <remarks>cpp parity: PSM.h:149 <c>NonRedundantPSM</c>.</remarks>
public class NonRedundantPSM : PSM
{
    /// <summary>Overrides the <c>fileId</c> field set by <c>buildTables()</c>.</summary>
    public int FileId { get; set; }

    /// <summary>Overrides the <c>copies</c> field set by <c>buildTables()</c>.</summary>
    public int Copies { get; set; }
}

/// <summary>
/// Sort PSMs by <see cref="PSM.SpecKey"/> ascending. cpp parity: PSM.h:155 <c>PSMSpecKeySorter</c>.
/// </summary>
public sealed class PsmSpecKeySorter : IComparer<PSM>
{
    /// <inheritdoc/>
    public int Compare(PSM? x, PSM? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        return x.SpecKey.CompareTo(y.SpecKey);
    }
}

/// <summary>
/// Sort PSMs by <see cref="PSM.SpecIndex"/> ascending. cpp parity: PSM.h:159 <c>PSMSpecIndexSorter</c>.
/// </summary>
public sealed class PsmSpecIndexSorter : IComparer<PSM>
{
    /// <inheritdoc/>
    public int Compare(PSM? x, PSM? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        return x.SpecIndex.CompareTo(y.SpecIndex);
    }
}
