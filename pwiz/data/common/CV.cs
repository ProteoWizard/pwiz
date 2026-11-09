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

    /// <summary>Default constructor — produces an "Unknown" term.</summary>
    public CVTermInfo() { }

    /// <summary>Convenience constructor: copies fields from <see cref="CvLookup.CvTermInfo(CVID)"/>.
    /// Mirrors the cpp <c>new CVTermInfo(cvid)</c> idiom that SeeMS and other downstream code use.</summary>
    public CVTermInfo(CVID cvid)
    {
        var src = CvLookup.CvTermInfo(cvid);
        Cvid = src.Cvid;
        Id = src.Id;
        Name = src.Name;
        Def = src.Def;
        IsObsolete = src.IsObsolete;
        ParentsIsA = src.ParentsIsA;
        ParentsPartOf = src.ParentsPartOf;
        OtherRelations = src.OtherRelations;
    }

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

    /// <summary>Short form of the name: the shortest of <see cref="Name"/> and the
    /// <see cref="ExactSynonyms"/> (e.g. "CID" for "collision-induced dissociation").
    /// Mirrors cpp <c>CVTermInfo::shortName()</c> (cv.inl:54), which likewise scans the
    /// exact synonyms for the shortest string, keeping the first on a length tie.</summary>
    public string ShortName
    {
        get
        {
            var result = Name;
            foreach (var synonym in ExactSynonyms)
                if (result.Length > synonym.Length)
                    result = synonym;
            return result;
        }
    }

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
/// <para>
/// An accession the embedded files don't define - a file written against a newer CV - is
/// resolved online: readers register the CVs a file declares with <see cref="RegisterSource"/>,
/// and a miss in <see cref="CvTermInfo(string)"/> loads the newest registered version of that
/// CV (or the latest one) and merges it into the table, the newest version winning. A term
/// learned that way has an unnamed CVID (value-block offset + accession number), which the
/// rest of pwiz handles like any other. See <see cref="OnlineUpdate"/>.
/// </para>
/// </remarks>
public static class CvLookup
{
    // Numeric offsets that convert CVID enum values to OBO accession numbers (see cv.hpp).
    internal const int OffsetMs = 0;
    internal const int OffsetPeff = 200_000_000;
    internal const int OffsetUnimod = 300_000_000;
    internal const int OffsetUo = 400_000_000;

    // Each prefix owns a block of 100,000,000 values, so a larger accession number would
    // collide with the next prefix's block.
    private const uint ValueBlockSize = 100_000_000;

    // Replaced wholesale, never mutated, when downloaded terms are merged, so lookups take no lock.
    private static CvTermTable s_table = CvTermTable.Embedded;

    internal static CvTermTable Table => Volatile.Read(ref s_table);

    internal static void Publish(CvTermTable table) => Volatile.Write(ref s_table, table);

    /// <summary>Returns CV term metadata for the given <paramref name="cvid"/>, or <see cref="CVTermInfo.Unknown"/>.</summary>
    public static CVTermInfo CvTermInfo(CVID cvid) => Table.TermInfo(cvid);

    /// <summary>
    /// Returns CV term metadata for the given accession id (e.g. "MS:1000031"), or
    /// <see cref="CVTermInfo.Unknown"/>. An MS, PEFF, UO or UNIMOD accession is matched by its
    /// number however it is padded (mz5 writes "UNIMOD:0000001"), and one that no loaded
    /// version defines triggers an online update first (see <see cref="OnlineUpdate"/>).
    /// </summary>
    public static CVTermInfo CvTermInfo(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var table = Table;
        var info = table.TermInfo(id);
        if (info.Cvid != CVID.CVID_Unknown || !CvOnlineUpdater.TryParseAccession(id, out var family, out var cvid))
            return info;
        if (table.Contains(cvid))
            return table.TermInfo(cvid);
        return CvOnlineUpdater.TryResolve(family, cvid, id) ? Table.TermInfo(cvid) : CVTermInfo.Unknown;
    }

    /// <summary>
    /// Returns true iff <paramref name="child"/> is a descendant of <paramref name="parent"/>
    /// via is_a relations (or equal). Mirrors pwiz's cvIsA.
    /// </summary>
    public static bool CvIsA(CVID child, CVID parent) => Table.IsA(child, parent);

    /// <summary>Returns the list of all known CVID values, including terms learned online.</summary>
    public static IReadOnlyList<CVID> AllCvids => Table.AllCvids;

    /// <summary>
    /// Returns a CV descriptor for a known prefix (<c>MS</c>, <c>UO</c>, <c>UNIMOD</c>). The
    /// version is always the embedded one, even after an online update: it is what pwiz itself
    /// writes terms from, and output must not depend on what earlier files happened to load.
    /// </summary>
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

    // ---- online update ----

    /// <summary>
    /// Whether an accession the loaded CVs don't define is looked up online. Defaults to
    /// <see cref="CvOnlineUpdateMode.Enabled"/>, or to what the <c>PWIZ_CV_ONLINE_UPDATE</c>
    /// environment variable says (<c>0</c>/<c>off</c>/<c>disabled</c>, or <c>cacheonly</c>).
    /// </summary>
    public static CvOnlineUpdateMode OnlineUpdate
    {
        get => CvOnlineUpdater.Mode;
        set => CvOnlineUpdater.Mode = value;
    }

    /// <summary>
    /// Raised with a human-readable line when an online update downloads a CV, or when a
    /// download or cache write fails. Lookups never throw for these; this is the only report.
    /// </summary>
    public static event Action<string>? OnlineUpdateMessage
    {
        add => CvOnlineUpdater.Message += value;
        remove => CvOnlineUpdater.Message -= value;
    }

    /// <summary>
    /// Records a CV that a file being read declares (an mzML <c>cvList</c> entry, say), so that
    /// an unknown term can be resolved from the version the file was written against. Cheap and
    /// offline: nothing is downloaded until a lookup misses. CVs other than psi-ms, UO and
    /// Unimod are ignored.
    /// </summary>
    public static void RegisterSource(CV cv)
    {
        ArgumentNullException.ThrowIfNull(cv);
        CvOnlineUpdater.Register(cv);
    }

    /// <summary>Drops every registered source and downloaded term and restores the online
    /// update settings to their defaults.</summary>
    internal static void ResetOnlineUpdateForTests() => CvOnlineUpdater.Reset();

    internal static string ExtractVersion(ObOntology obo)
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

    /// <summary>The CVID for an accession, or null when the prefix has no value block or the
    /// number does not fit in one.</summary>
    internal static CVID? CvidFor(string prefix, uint number)
    {
        if (number >= ValueBlockSize)
            return null;
        int? offset = prefix switch
        {
            "MS" => OffsetMs,
            "PEFF" => OffsetPeff,
            "UO" => OffsetUo,
            "UNIMOD" => OffsetUnimod,
            _ => null,
        };
        return offset is null ? null : (CVID)(offset.Value + (int)number);
    }

    internal static string AccessionFor(string prefix, uint number)
        => prefix == "UNIMOD"
            ? FormatAccessionVariable(prefix, (int)number)
            : FormatAccession(prefix, (int)number, 7);

    internal static string AccessionForCvid(CVID cvid, string prefix)
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

    internal static (string Prefix, string Suffix) SplitPrefix(string enumName)
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

    internal static class OboFiles
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

    internal static CVTermInfo BuildFromOboTerm(CVID cvid, string accession, OboTerm term)
    {
        // OBO parents have the child's prefix (the parser drops the rest); an id too large for
        // the prefix's value block, possible in a downloaded file, is dropped rather than wrapped
        // into another block.
        CVID[] TranslateParents(List<uint> parentIds) => parentIds
            .Select(id => CvidFor(term.Prefix, id))
            .OfType<CVID>()
            .ToArray();

        var isA = term.ParentsIsA.Count == 0
            ? (IReadOnlyList<CVID>)Array.Empty<CVID>()
            : TranslateParents(term.ParentsIsA);
        var partOf = term.ParentsPartOf.Count == 0
            ? (IReadOnlyList<CVID>)Array.Empty<CVID>()
            : TranslateParents(term.ParentsPartOf);

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
}

/// <summary>
/// An immutable snapshot of every known term: the embedded OBO files, plus any CV versions
/// loaded online. <see cref="CvLookup"/> swaps in a new one rather than mutating it.
/// </summary>
internal sealed class CvTermTable
{
    public static readonly CvTermTable Embedded = BuildEmbedded();

    private readonly Dictionary<CVID, CVTermInfo> _terms;
    private readonly Dictionary<string, CVID> _byAccession;
    private readonly CVID[] _allCvids;

    // CVIDs the embedded OBO files define, as opposed to enum values that only have a
    // fallback derived from their identifier.
    private readonly HashSet<CVID> _oboDefined;

    // The version each term loaded online came from, so a later, older version cannot replace it.
    private readonly Dictionary<CVID, CvVersion> _loadedVersions;

    private CvTermTable(Dictionary<CVID, CVTermInfo> terms, Dictionary<string, CVID> byAccession,
        HashSet<CVID> oboDefined, Dictionary<CVID, CvVersion> loadedVersions)
    {
        _terms = terms;
        _byAccession = byAccession;
        _oboDefined = oboDefined;
        _loadedVersions = loadedVersions;
        _allCvids = terms.Keys.ToArray();
        Array.Sort(_allCvids);
    }

    public IReadOnlyList<CVID> AllCvids => _allCvids;

    public CVTermInfo TermInfo(CVID cvid) =>
        _terms.TryGetValue(cvid, out var info) ? info : CVTermInfo.Unknown;

    public CVTermInfo TermInfo(string accession) =>
        _byAccession.TryGetValue(accession, out var cvid) ? TermInfo(cvid) : CVTermInfo.Unknown;

    public bool Contains(CVID cvid) => _terms.ContainsKey(cvid);

    // Recursion is safe because WithSource never publishes a table with an is_a cycle.
    public bool IsA(CVID child, CVID parent)
    {
        if (child == parent) return true;
        if (!_terms.TryGetValue(child, out var info)) return false;

        foreach (var direct in info.ParentsIsA)
        {
            if (direct == parent) return true;
            if (IsA(direct, parent)) return true;
        }
        return false;
    }

    /// <summary>
    /// This table with <paramref name="source"/> merged in, or null when the merge would give
    /// the is_a hierarchy a cycle. Each term keeps the newest version that defines it: a version
    /// older than the embedded one only fills gaps, never reverting a term the embedded files
    /// define, and never replacing a term an even newer download supplied.
    /// </summary>
    public CvTermTable? WithSource(CvSource source)
    {
        var family = source.Family;
        bool newerThanEmbedded = source.Version.CompareTo(family.EmbeddedVersion) > 0;
        var terms = new Dictionary<CVID, CVTermInfo>(_terms);
        var byAccession = new Dictionary<string, CVID>(_byAccession, StringComparer.Ordinal);
        var loadedVersions = new Dictionary<CVID, CvVersion>(_loadedVersions);

        foreach (var term in source.Obo.Terms)
        {
            // psi-ms.obo also carries copies of UO and NCIT terms; each CV speaks only for its own.
            if (!family.Prefixes.Contains(term.Prefix)) continue;
            if (CvLookup.CvidFor(term.Prefix, term.Id) is not { } cvid) continue;
            if (loadedVersions.TryGetValue(cvid, out var loaded)
                    ? loaded.CompareTo(source.Version) >= 0
                    : !newerThanEmbedded && _oboDefined.Contains(cvid))
                continue;

            var info = CvLookup.BuildFromOboTerm(cvid, CvLookup.AccessionFor(term.Prefix, term.Id), term);
            terms[cvid] = info;
            byAccession[info.Id] = cvid;
            loadedVersions[cvid] = source.Version;
        }

        var table = new CvTermTable(terms, byAccession, _oboDefined, loadedVersions);
        return table.HasIsACycle() ? null : table;
    }

    /// <summary>True when some term is, through is_a, its own ancestor; <see cref="IsA"/>
    /// would never return for it.</summary>
    private bool HasIsACycle()
    {
        // Iterative depth-first search: a node met again while still on the path closes a cycle.
        var state = new Dictionary<CVID, bool>(_terms.Count); // false: on the path; true: done
        var path = new Stack<(CVID Cvid, int NextParent)>();
        foreach (var root in _terms.Keys)
        {
            if (state.ContainsKey(root)) continue;
            state[root] = false;
            path.Push((root, 0));
            while (path.Count > 0)
            {
                var (cvid, next) = path.Pop();
                var parents = _terms.TryGetValue(cvid, out var info) ? info.ParentsIsA : Array.Empty<CVID>();
                if (next == parents.Count)
                {
                    state[cvid] = true;
                    continue;
                }
                path.Push((cvid, next + 1));
                var parent = parents[next];
                if (!state.TryGetValue(parent, out bool done))
                {
                    state[parent] = false;
                    path.Push((parent, 0));
                }
                else if (!done)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static CvTermTable BuildEmbedded()
    {
        var obos = new[] { CvLookup.OboFiles.PsiMs, CvLookup.OboFiles.Unimod, CvLookup.OboFiles.Unit };

        // Build accession → OBO term lookup from all three ontologies.
        var oboByAccession = new Dictionary<string, OboTerm>(StringComparer.Ordinal);
        foreach (var obo in obos)
        {
            foreach (var term in obo.Terms)
                oboByAccession[CvLookup.AccessionFor(term.Prefix, term.Id)] = term;
        }

        var values = Enum.GetValues<CVID>();
        var terms = new Dictionary<CVID, CVTermInfo>(values.Length);
        var oboDefined = new HashSet<CVID>();

        foreach (var cvid in values)
        {
            string enumName = cvid.ToString();
            var (prefix, _) = CvLookup.SplitPrefix(enumName);
            string accession = CvLookup.AccessionForCvid(cvid, prefix);

            CVTermInfo info;
            if (oboByAccession.TryGetValue(accession, out var found))
            {
                info = CvLookup.BuildFromOboTerm(cvid, accession, found);
                oboDefined.Add(cvid);
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
            terms[cvid] = info;
        }

        terms[CVID.CVID_Unknown] = CVTermInfo.Unknown;

        var byAccession = new Dictionary<string, CVID>(terms.Count, StringComparer.Ordinal);
        foreach (var kv in terms)
            byAccession[kv.Value.Id] = kv.Key;

        return new CvTermTable(terms, byAccession, oboDefined, new Dictionary<CVID, CvVersion>());
    }
}
