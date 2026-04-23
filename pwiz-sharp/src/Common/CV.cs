using System.Globalization;
using System.Reflection;
using Pwiz.Data.Common.Obo;

namespace Pwiz.Data.Common.Cv;

/// <summary>
/// A controlled vocabulary reference: id/URI/name/version, as embedded in mzML.
/// Port of pwiz/cv::CV.
/// </summary>
public sealed class CV
{
    /// <summary>The short label for the controlled vocabulary ("MS", "UO", "UNIMOD").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The URI of the OBO file / ontology resource.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Human-readable full name of the CV.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Version string (from OBO data-version or format-version).</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>True iff all four fields are empty.</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(Uri) &&
        string.IsNullOrEmpty(FullName) && string.IsNullOrEmpty(Version);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is CV that
           && Id == that.Id && Uri == that.Uri
           && FullName == that.FullName && Version == that.Version;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Uri, FullName, Version);
}

/// <summary>
/// CV term metadata: accession id, display name, definition, obsolescence flag, and relational data.
/// </summary>
/// <remarks>Port of pwiz/cv::CVTermInfo.</remarks>
public sealed class CVTermInfo
{
    /// <summary>Sentinel instance returned when a lookup fails.</summary>
    public static readonly CVTermInfo Unknown = new()
    {
        Cvid = CVID.CVID_Unknown,
        Id = "??:0000000",
        Name = "CVID_Unknown",
        Def = "CVID_Unknown",
        IsObsolete = false,
    };

    /// <summary>The enum value.</summary>
    public CVID Cvid { get; set; } = CVID.CVID_Unknown;

    /// <summary>The accession id (e.g. "MS:1000031").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The term name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The term definition / description.</summary>
    public string Def { get; set; } = string.Empty;

    /// <summary>True iff this term is marked obsolete in the source ontology.</summary>
    public bool IsObsolete { get; set; }

    /// <summary>Direct "is_a" parents.</summary>
    public IReadOnlyList<CVID> ParentsIsA { get; set; } = Array.Empty<CVID>();

    /// <summary>Direct "part_of" parents.</summary>
    public IReadOnlyList<CVID> ParentsPartOf { get; set; } = Array.Empty<CVID>();

    /// <summary>Relations other than is_a and part_of (e.g. "has_units", "has_order").</summary>
    public IReadOnlyList<KeyValuePair<string, CVID>> OtherRelations { get; set; } =
        Array.Empty<KeyValuePair<string, CVID>>();

    /// <summary>Exact synonyms (e.g. "FTMS" as a synonym for MS_FT_ICR).</summary>
    public IReadOnlyList<string> ExactSynonyms { get; set; } = Array.Empty<string>();

    /// <summary>Property-value annotations from OBO.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> PropertyValues { get; set; } =
        Array.Empty<KeyValuePair<string, string>>();

    /// <summary>Short form of the name.</summary>
    public string ShortName => Name;

    /// <summary>Prefix portion of the id ("MS", "UO", "UNIMOD", "PEFF").</summary>
    public string Prefix
    {
        get
        {
            int colon = Id.IndexOf(':');
            return colon > 0 ? Id[..colon] : string.Empty;
        }
    }
}

/// <summary>
/// Static access to CV term metadata and CV hierarchy.
/// </summary>
/// <remarks>
/// Data is loaded at class-init time from three OBO files embedded as assembly resources
/// (psi-ms.obo, unimod.obo, unit.obo). CVIDs that don't appear in the current OBO versions
/// fall back to name/id derived from the enum identifier.
/// </remarks>
public static class CvLookup
{
    // Numeric offsets that convert CVID enum values to OBO accession numbers (see cv.hpp).
    internal const int OffsetMs = 0;
    internal const int OffsetPeff = 200_000_000;
    internal const int OffsetUnimod = 300_000_000;
    internal const int OffsetUo = 400_000_000;

    private static readonly Dictionary<CVID, CVTermInfo> s_terms = BuildTermTable();
    private static readonly Dictionary<string, CVID> s_byAccession = BuildAccessionIndex();
    private static readonly CVID[] s_allCvids = BuildCvidList();

    /// <summary>Returns CV term metadata for the given <paramref name="cvid"/>, or <see cref="CVTermInfo.Unknown"/>.</summary>
    public static CVTermInfo CvTermInfo(CVID cvid) =>
        s_terms.TryGetValue(cvid, out var info) ? info : CVTermInfo.Unknown;

    /// <summary>Returns CV term metadata for the given accession id (e.g. "MS:1000031").</summary>
    public static CVTermInfo CvTermInfo(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return s_byAccession.TryGetValue(id, out var cvid) ? CvTermInfo(cvid) : CVTermInfo.Unknown;
    }

    /// <summary>
    /// Returns true iff <paramref name="child"/> is a descendant of <paramref name="parent"/>
    /// via is_a relations (or equal). Mirrors pwiz's cvIsA.
    /// </summary>
    public static bool CvIsA(CVID child, CVID parent)
    {
        if (child == parent) return true;
        if (!s_terms.TryGetValue(child, out var info)) return false;

        foreach (var direct in info.ParentsIsA)
        {
            if (direct == parent) return true;
            if (CvIsA(direct, parent)) return true;
        }
        return false;
    }

    /// <summary>Returns the list of all known CVID values.</summary>
    public static IReadOnlyList<CVID> AllCvids => s_allCvids;

    /// <summary>Returns a CV descriptor for a known prefix (<c>MS</c>, <c>UO</c>, <c>UNIMOD</c>).</summary>
    public static CV GetCv(string prefix) => prefix switch
    {
        "MS" => new CV
        {
            Id = "MS",
            FullName = "Proteomics Standards Initiative Mass Spectrometry Ontology",
            Uri = "http://purl.obolibrary.org/obo/ms/psi-ms.obo",
            Version = ExtractVersion(OboFiles.PsiMs),
        },
        "UO" => new CV
        {
            Id = "UO",
            FullName = "Unit Ontology",
            Uri = "http://purl.obolibrary.org/obo/uo.obo",
            Version = ExtractVersion(OboFiles.Unit),
        },
        "UNIMOD" => new CV
        {
            Id = "UNIMOD",
            FullName = "UNIMOD",
            Uri = "http://www.unimod.org/obo/unimod.obo",
            Version = ExtractVersion(OboFiles.Unimod),
        },
        _ => throw new ArgumentException($"Unknown CV prefix: {prefix}", nameof(prefix)),
    };

    private static string ExtractVersion(ObOntology obo)
    {
        foreach (var line in obo.Header)
        {
            if (line.StartsWith("data-version:", StringComparison.Ordinal))
                return line["data-version:".Length..].Trim();
        }
        foreach (var line in obo.Header)
        {
            if (line.StartsWith("format-version:", StringComparison.Ordinal))
                return line["format-version:".Length..].Trim();
        }
        return string.Empty;
    }

    // ---- accession <-> CVID mapping ----

    private static string AccessionForCvid(CVID cvid, string prefix)
    {
        int numeric = (int)cvid;
        return prefix switch
        {
            "MS" => FormatAccession("MS", numeric - OffsetMs, 7),
            "PEFF" => FormatAccession("PEFF", numeric - OffsetPeff, 7),
            "UO" => FormatAccession("UO", numeric - OffsetUo, 7),
            "UNIMOD" => FormatAccessionVariable("UNIMOD", numeric - OffsetUnimod),
            _ => "??:0000000",
        };
    }

    private static string FormatAccession(string prefix, int number, int width)
        => $"{prefix}:{number.ToString("D" + width, CultureInfo.InvariantCulture)}";

    // UNIMOD uses variable-width accessions (e.g. "UNIMOD:1", "UNIMOD:12345"), not zero-padded.
    private static string FormatAccessionVariable(string prefix, int number)
        => $"{prefix}:{number.ToString(CultureInfo.InvariantCulture)}";

    private static (string Prefix, string Suffix) SplitPrefix(string enumName)
    {
        int underscore = enumName.IndexOf('_');
        if (underscore <= 0) return ("", enumName);
        string prefix = enumName[..underscore];
        string rest = enumName[(underscore + 1)..];
        return prefix switch
        {
            "MS" or "UO" or "PEFF" or "UNIMOD" => (prefix, rest),
            "CVID" => ("??", enumName),
            _ => ("", enumName),
        };
    }

    // ---- OBO loading ----

    private static class OboFiles
    {
        public static readonly ObOntology PsiMs = LoadEmbedded("Pwiz.Data.Common.Cv.psi-ms.obo");
        public static readonly ObOntology Unimod = LoadEmbedded("Pwiz.Data.Common.Cv.unimod.obo");
        public static readonly ObOntology Unit = LoadEmbedded("Pwiz.Data.Common.Cv.unit.obo");

        private static ObOntology LoadEmbedded(string resourceName)
        {
            var asm = typeof(CvLookup).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Missing embedded OBO resource '{resourceName}'. " +
                    "Check the EmbeddedResource entries in Pwiz.Data.Common.csproj.");
            using var reader = new StreamReader(stream);
            return ObOntology.Parse(reader);
        }
    }

    private static Dictionary<CVID, CVTermInfo> BuildTermTable()
    {
        // Build accession → (OBO prefix, OBO term) lookup from all three ontologies.
        var oboByAccession = new Dictionary<string, (string Prefix, OboTerm Term)>(StringComparer.Ordinal);
        foreach (var obo in new[] { OboFiles.PsiMs, OboFiles.Unimod, OboFiles.Unit })
        {
            foreach (var term in obo.Terms.Values)
            {
                string key = term.Prefix == "UNIMOD"
                    ? FormatAccessionVariable(term.Prefix, (int)term.Id)
                    : FormatAccession(term.Prefix, (int)term.Id, 7);
                oboByAccession[key] = (term.Prefix, term);
            }
        }

        var values = Enum.GetValues<CVID>();
        var dict = new Dictionary<CVID, CVTermInfo>(values.Length);

        foreach (var cvid in values)
        {
            string enumName = cvid.ToString();
            var (prefix, _) = SplitPrefix(enumName);
            string accession = AccessionForCvid(cvid, prefix);

            CVTermInfo info;
            if (oboByAccession.TryGetValue(accession, out var found))
            {
                info = BuildFromOboTerm(cvid, accession, found.Term);
            }
            else
            {
                // No matching OBO entry — fall back to enum-derived info (e.g. PEFF terms not in psi-ms.obo,
                // or CVIDs from older ontology revisions than what ships in /pwiz/data/common/).
                string derived = enumName.Contains('_') ? enumName[(enumName.IndexOf('_') + 1)..] : enumName;
                bool obsolete = derived.EndsWith("_OBSOLETE", StringComparison.Ordinal);
                if (obsolete) derived = derived[..^"_OBSOLETE".Length];
                string displayName = derived.Replace('_', ' ');

                info = new CVTermInfo
                {
                    Cvid = cvid,
                    Id = accession,
                    Name = displayName,
                    Def = displayName,
                    IsObsolete = obsolete,
                };
            }
            dict[cvid] = info;
        }

        dict[CVID.CVID_Unknown] = CVTermInfo.Unknown;
        return dict;
    }

    private static CVTermInfo BuildFromOboTerm(CVID cvid, string accession, OboTerm term)
    {
        // Translate OBO parent numeric ids to CVIDs (same prefix as the child).
        CVID TranslateParent(uint parentId)
        {
            int numeric = term.Prefix switch
            {
                "MS" => (int)parentId + OffsetMs,
                "UO" => (int)parentId + OffsetUo,
                "UNIMOD" => (int)parentId + OffsetUnimod,
                "PEFF" => (int)parentId + OffsetPeff,
                _ => 0,
            };
            return (CVID)numeric;
        }

        var isA = term.ParentsIsA.Count == 0
            ? (IReadOnlyList<CVID>)Array.Empty<CVID>()
            : term.ParentsIsA.Select(TranslateParent).ToArray();
        var partOf = term.ParentsPartOf.Count == 0
            ? (IReadOnlyList<CVID>)Array.Empty<CVID>()
            : term.ParentsPartOf.Select(TranslateParent).ToArray();

        return new CVTermInfo
        {
            Cvid = cvid,
            Id = accession,
            Name = term.Name,
            Def = term.Def,
            IsObsolete = term.IsObsolete,
            ParentsIsA = isA,
            ParentsPartOf = partOf,
            ExactSynonyms = term.ExactSynonyms.Count == 0
                ? Array.Empty<string>()
                : term.ExactSynonyms.ToArray(),
            PropertyValues = term.PropertyValues.Count == 0
                ? Array.Empty<KeyValuePair<string, string>>()
                : term.PropertyValues.ToArray(),
        };
    }

    private static Dictionary<string, CVID> BuildAccessionIndex()
    {
        var dict = new Dictionary<string, CVID>(s_terms.Count, StringComparer.Ordinal);
        foreach (var kv in s_terms)
            dict[kv.Value.Id] = kv.Key;
        return dict;
    }

    private static CVID[] BuildCvidList()
    {
        var all = Enum.GetValues<CVID>();
        Array.Sort(all);
        return all;
    }
}
