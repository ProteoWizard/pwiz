using System.Globalization;
using System.Reflection;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Obo;

namespace Pwiz.Data.Common.Unimod;

/// <summary>
/// The site a <see cref="UnimodModification"/> can apply to (single-letter residues, termini, any).
/// Port of pwiz/data::unimod::Site (bitfield).
/// </summary>
#pragma warning disable CS1591 // residue / terminus member names are self-documenting against the unimod spec
[Flags]
public enum UnimodSite : int
{
#pragma warning disable CA1008
    Any = 1 << 0,
#pragma warning restore CA1008
    NTerminus = 1 << 1,
    CTerminus = 1 << 2,
    Alanine = 1 << 3,
    Cysteine = 1 << 4,
    AsparticAcid = 1 << 5,
    GlutamicAcid = 1 << 6,
    Phenylalanine = 1 << 7,
    Glycine = 1 << 8,
    Histidine = 1 << 9,
    Isoleucine = 1 << 10,
    Lysine = 1 << 11,
    Leucine = 1 << 12,
    Methionine = 1 << 13,
    Asparagine = 1 << 14,
    Proline = 1 << 15,
    Glutamine = 1 << 16,
    Arginine = 1 << 17,
    Serine = 1 << 18,
    Threonine = 1 << 19,
    Selenocysteine = 1 << 20,
    Valine = 1 << 21,
    Tryptophan = 1 << 22,
    Tyrosine = 1 << 23,
}
#pragma warning restore CS1591

/// <summary>Position constraint for a modification specificity. Port of pwiz/data::unimod::Position.</summary>
public enum UnimodPosition
{
    /// <summary>Anywhere in the peptide.</summary>
    Anywhere,
    /// <summary>N-terminus of a peptide.</summary>
    AnyNTerminus,
    /// <summary>C-terminus of a peptide.</summary>
    AnyCTerminus,
    /// <summary>N-terminus of a protein.</summary>
    ProteinNTerminus,
    /// <summary>C-terminus of a protein.</summary>
    ProteinCTerminus,
}

/// <summary>Classification flags for a modification. Port of pwiz/data::unimod::Classification (bitfield).</summary>
#pragma warning disable CS1591 // bitfield member names are self-documenting against the unimod spec
[Flags]
public enum UnimodClassification : int
{
#pragma warning disable CA1008
    Any = 1 << 0,
#pragma warning restore CA1008
    Artifact = 1 << 1,
    ChemicalDerivative = 1 << 2,
    CoTranslational = 1 << 3,
    IsotopicLabel = 1 << 4,
    Multiple = 1 << 5,
    NLinkedGlycosylation = 1 << 6,
    NonStandardResidue = 1 << 7,
    OLinkedGlycosylation = 1 << 8,
    OtherGlycosylation = 1 << 9,
    Other = 1 << 10,
    PostTranslational = 1 << 11,
    PreTranslational = 1 << 12,
    Substitution = 1 << 13,
    SynthPepProtectGP = 1 << 14,
}
#pragma warning restore CS1591

/// <summary>
/// A Unimod modification. Port of pwiz/data::unimod::Modification.
/// </summary>
public sealed class UnimodModification
{
    /// <summary>The CV id for this modification.</summary>
    public CVID Cvid { get; set; } = CVID.CVID_Unknown;

    /// <summary>Display name (e.g. "Phospho").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Delta composition string as it appears in the OBO (e.g. "H(2) C(2) O").</summary>
    public string DeltaComposition { get; set; } = string.Empty;

    /// <summary>Monoisotopic delta mass (u).</summary>
    public double DeltaMonoisotopicMass { get; set; }

    /// <summary>Average delta mass (u).</summary>
    public double DeltaAverageMass { get; set; }

    /// <summary>True iff approved / reviewed in Unimod.</summary>
    public bool Approved { get; set; }

    /// <summary>Where this modification can occur.</summary>
    public List<UnimodSpecificity> Specificities { get; } = new();
}

/// <summary>A specific context where a <see cref="UnimodModification"/> applies.</summary>
public sealed class UnimodSpecificity
{
    /// <summary>Allowed residue site(s).</summary>
    public UnimodSite Site { get; set; }

    /// <summary>Required position.</summary>
    public UnimodPosition Position { get; set; }

    /// <summary>Hidden from casual listings.</summary>
    public bool Hidden { get; set; }

    /// <summary>Classification flags.</summary>
    public UnimodClassification Classification { get; set; }
}

/// <summary>
/// Free-function equivalents of pwiz/data::unimod namespace helpers, backed by the embedded unimod.obo.
/// </summary>
public static class Unimod
{
    private static readonly List<UnimodModification> s_modifications = LoadFromObo();

    /// <summary>Returns a <see cref="UnimodSite"/> from a residue code, 'x' (Any), 'n' (NTerminus), 'c' (CTerminus).</summary>
    public static UnimodSite SiteFromSymbol(char symbol) => symbol switch
    {
        'x' or 'X' => UnimodSite.Any,
        'n' or 'N' when !char.IsUpper(symbol) => UnimodSite.NTerminus,
        'c' or 'C' when !char.IsUpper(symbol) => UnimodSite.CTerminus,
        'A' => UnimodSite.Alanine,
        'R' => UnimodSite.Arginine,
        'D' => UnimodSite.AsparticAcid,
        'E' => UnimodSite.GlutamicAcid,
        'F' => UnimodSite.Phenylalanine,
        'G' => UnimodSite.Glycine,
        'H' => UnimodSite.Histidine,
        'I' => UnimodSite.Isoleucine,
        'K' => UnimodSite.Lysine,
        'L' => UnimodSite.Leucine,
        'M' => UnimodSite.Methionine,
        'N' => UnimodSite.Asparagine,
        'P' => UnimodSite.Proline,
        'Q' => UnimodSite.Glutamine,
        'S' => UnimodSite.Serine,
        'T' => UnimodSite.Threonine,
        'U' => UnimodSite.Selenocysteine,
        'V' => UnimodSite.Valine,
        'W' => UnimodSite.Tryptophan,
        'Y' => UnimodSite.Tyrosine,
        'C' => UnimodSite.Cysteine,
        _ => throw new ArgumentException($"Unknown residue symbol: '{symbol}'", nameof(symbol)),
    };

    /// <summary>Returns the <see cref="UnimodPosition"/> for a position-specifier CV id.</summary>
    public static UnimodPosition PositionFromCvid(CVID cvid = CVID.CVID_Unknown) => cvid switch
    {
        CVID.CVID_Unknown => UnimodPosition.Anywhere,
        CVID.MS_modification_specificity_peptide_N_term => UnimodPosition.AnyNTerminus,
        CVID.MS_modification_specificity_peptide_C_term => UnimodPosition.AnyCTerminus,
        _ => throw new ArgumentException($"Unsupported position CVID: {cvid}", nameof(cvid)),
    };

    /// <summary>Returns all Unimod modifications loaded from the embedded unimod.obo.</summary>
    public static IReadOnlyList<UnimodModification> Modifications => s_modifications;

    /// <summary>Returns the modification matching <paramref name="cvid"/>, or null if not found.</summary>
    public static UnimodModification? Modification(CVID cvid)
    {
        foreach (var m in s_modifications) if (m.Cvid == cvid) return m;
        return null;
    }

    /// <summary>Returns the modification matching <paramref name="title"/> (case-sensitive), or null if not found.</summary>
    public static UnimodModification? Modification(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        foreach (var m in s_modifications) if (m.Name == title) return m;
        return null;
    }

    private static List<UnimodModification> LoadFromObo()
    {
        var asm = typeof(Unimod).Assembly;
        using var stream = asm.GetManifestResourceStream("Pwiz.Data.Common.Cv.unimod.obo")
            ?? throw new InvalidOperationException("Missing embedded unimod.obo resource.");
        using var reader = new StreamReader(stream);
        var obo = ObOntology.Parse(reader);

        var list = new List<UnimodModification>(obo.Terms.Count);
        foreach (var term in obo.Terms.Values)
        {
            if (term.Prefix != "UNIMOD") continue;

            var mod = new UnimodModification
            {
                Cvid = (CVID)((int)term.Id + CvLookup.OffsetUnimod),
                Name = term.Name,
            };

            // Flatten xrefs by key; some keys (spec_N_*) repeat per specificity group.
            var specGroups = new SortedDictionary<int, SpecBuilder>();
            foreach (var xref in term.Xrefs)
            {
                string key = xref.Key;
                string value = xref.Value;

                switch (key)
                {
                    case "delta_mono_mass":
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mono))
                            mod.DeltaMonoisotopicMass = mono;
                        break;
                    case "delta_avge_mass":
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var avg))
                            mod.DeltaAverageMass = avg;
                        break;
                    case "delta_composition":
                        mod.DeltaComposition = value;
                        break;
                    case "approved":
                        mod.Approved = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        TryParseSpecKey(key, value, specGroups);
                        break;
                }
            }

            foreach (var sb in specGroups.Values)
            {
                var spec = sb.Build();
                if (spec is not null) mod.Specificities.Add(spec);
            }

            list.Add(mod);
        }

        return list;
    }

    private static void TryParseSpecKey(string key, string value, SortedDictionary<int, SpecBuilder> groups)
    {
        // Keys: spec_N_site, spec_N_position, spec_N_classification, spec_N_hidden, ...
        const string prefix = "spec_";
        if (!key.StartsWith(prefix, StringComparison.Ordinal)) return;
        int tail = prefix.Length;
        int underscore = key.IndexOf('_', tail);
        if (underscore < 0) return;
        if (!int.TryParse(key[tail..underscore], out int groupId)) return;
        string field = key[(underscore + 1)..];

        if (!groups.TryGetValue(groupId, out var sb))
        {
            sb = new SpecBuilder();
            groups[groupId] = sb;
        }

        switch (field)
        {
            case "site": sb.Site = value; break;
            case "position": sb.Position = value; break;
            case "classification": sb.Classification = value; break;
            case "hidden": sb.Hidden = value == "1"; break;
            // other fields (group, misc_notes) are not needed for this port
        }
    }

    private sealed class SpecBuilder
    {
        public string? Site { get; set; }
        public string? Position { get; set; }
        public string? Classification { get; set; }
        public bool Hidden { get; set; }

        public UnimodSpecificity? Build()
        {
            if (Site is null) return null;
            return new UnimodSpecificity
            {
                Site = ParseSite(Site),
                Position = ParsePosition(Position),
                Classification = ParseClassification(Classification),
                Hidden = Hidden,
            };
        }

        private static UnimodSite ParseSite(string s)
        {
            if (s.Length == 1) return Unimod.SiteFromSymbol(s[0]);
            return s switch
            {
                "N-term" => UnimodSite.NTerminus,
                "C-term" => UnimodSite.CTerminus,
                _ => UnimodSite.Any,
            };
        }

        private static UnimodPosition ParsePosition(string? p) => p switch
        {
            "Anywhere" or null => UnimodPosition.Anywhere,
            "Any N-term" => UnimodPosition.AnyNTerminus,
            "Any C-term" => UnimodPosition.AnyCTerminus,
            "Protein N-term" => UnimodPosition.ProteinNTerminus,
            "Protein C-term" => UnimodPosition.ProteinCTerminus,
            _ => UnimodPosition.Anywhere,
        };

        private static UnimodClassification ParseClassification(string? c) => c switch
        {
            null => UnimodClassification.Any,
            "Artefact" or "Artifact" => UnimodClassification.Artifact,
            "Chemical derivative" => UnimodClassification.ChemicalDerivative,
            "Co-translational" => UnimodClassification.CoTranslational,
            "Isotopic label" => UnimodClassification.IsotopicLabel,
            "Multiple" => UnimodClassification.Multiple,
            "N-linked glycosylation" => UnimodClassification.NLinkedGlycosylation,
            "Non-standard residue" => UnimodClassification.NonStandardResidue,
            "O-linked glycosylation" => UnimodClassification.OLinkedGlycosylation,
            "Other glycosylation" => UnimodClassification.OtherGlycosylation,
            "Other" => UnimodClassification.Other,
            "Post-translational" => UnimodClassification.PostTranslational,
            "Pre-translational" => UnimodClassification.PreTranslational,
            "AA substitution" => UnimodClassification.Substitution,
            "Synth. pep. protect. gp." => UnimodClassification.SynthPepProtectGP,
            _ => UnimodClassification.Other,
        };
    }
}
