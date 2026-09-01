using System.Globalization;

namespace Pwiz.Data.Common.Obo;

/// <summary>
/// A single OBO-format controlled-vocabulary term.
/// Port of pwiz/data::Term.
/// </summary>
public sealed class OboTerm
{
    /// <summary>Sentinel id used when no id is assigned.</summary>
    public const uint MaxId = uint.MaxValue;

    /// <summary>Prefix ("MS", "UO", "UNIMOD").</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Numeric id (e.g. 1000031 for "MS:1000031").</summary>
    public uint Id { get; set; } = MaxId;

    /// <summary>Term name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Term definition.</summary>
    public string Def { get; set; } = string.Empty;

    /// <summary>Parents via the "is_a" relation.</summary>
    public List<uint> ParentsIsA { get; } = new();

    /// <summary>Parents via the "part_of" relation.</summary>
    public List<uint> ParentsPartOf { get; } = new();

    /// <summary>Other relations (name → (prefix, id)).</summary>
    public List<RelationEntry> Relations { get; } = new();

    /// <summary>Property-value annotations.</summary>
    public List<KeyValuePair<string, string>> PropertyValues { get; } = new();

    /// <summary>Exact synonyms.</summary>
    public List<string> ExactSynonyms { get; } = new();

    /// <summary>True iff the source OBO marks this term obsolete.</summary>
    public bool IsObsolete { get; set; }

    /// <summary>
    /// Raw <c>xref:</c> entries as key-value pairs (e.g. <c>delta_mono_mass → "42.010565"</c>).
    /// Used by Unimod term population; <see cref="OboParser"/> captures the first quoted value per xref.
    /// A single key may appear multiple times (e.g. multiple spec groups); values are preserved in order.
    /// </summary>
    public List<KeyValuePair<string, string>> Xrefs { get; } = new();
}

/// <summary>An entry in <see cref="OboTerm.Relations"/>: relation name, target prefix, target id.</summary>
public readonly record struct RelationEntry(string Name, string TargetPrefix, uint TargetId);

/// <summary>
/// A selectively-parsed OBO file. Matches the behavior of pwiz/data::OBO:
/// comments, dbxrefs, non-exact synonyms, and non-Term stanzas are ignored.
/// </summary>
public sealed class ObOntology
{
    /// <summary>Source filename (if loaded from disk).</summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>Header lines (before the first stanza).</summary>
    public List<string> Header { get; } = new();

    /// <summary>Set of term prefixes seen (e.g. "MS", "UO").</summary>
    public SortedSet<string> Prefixes { get; } = new(StringComparer.Ordinal);

    /// <summary>Parsed term table, keyed by numeric id.</summary>
    public SortedDictionary<uint, OboTerm> Terms { get; } = new();

    /// <summary>Loads an OBO file from disk.</summary>
    public static ObOntology Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var reader = new StreamReader(path);
        var obo = new ObOntology { Filename = path };
        OboParser.Parse(reader, obo);
        return obo;
    }

    /// <summary>Parses OBO content from a reader (used by <see cref="Load"/> and tests).</summary>
    public static ObOntology Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var obo = new ObOntology();
        OboParser.Parse(reader, obo);
        return obo;
    }
}

/// <summary>
/// Streaming OBO 1.2 parser. Port of pwiz/data::parseOBO.
/// Recognizes: <c>[Term]</c> stanzas with <c>id</c>, <c>name</c>, <c>def</c>, <c>is_a</c>,
/// <c>relationship</c>, <c>property_value</c>, <c>synonym</c> (EXACT only), <c>is_obsolete</c>.
/// </summary>
public static class OboParser
{
    /// <summary>Parses OBO content from <paramref name="reader"/> into <paramref name="target"/>.</summary>
    public static void Parse(TextReader reader, ObOntology target)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(target);

        OboTerm? current = null;
        string? currentStanzaType = null;
        bool inHeader = true;
        bool skipCurrent = false; // true when the current Term has a non-numeric id we can't represent

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (inHeader)
            {
                if (line.StartsWith('['))
                {
                    inHeader = false;
                    // fall through to stanza handling
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        target.Header.Add(line);
                    continue;
                }
            }

            if (line.StartsWith('['))
            {
                if (!skipCurrent) FinishTerm(current, currentStanzaType, target);
                current = null;
                skipCurrent = false;
                currentStanzaType = line.Trim('[', ']').Trim();
                if (currentStanzaType == "Term")
                    current = new OboTerm();
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (current is null) continue; // ignoring non-Term stanzas
            if (skipCurrent) continue;

            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            string tag = line[..colon].Trim();
            string rest = line[(colon + 1)..].Trim();

            switch (tag)
            {
                case "id":
                    if (!TryParseId(rest, out string prefix, out uint id))
                    {
                        // Non-numeric term ids (e.g. NCIT:C25330) — the pwiz CVID enum only
                        // covers numeric prefixes (MS/UO/UNIMOD/PEFF), so we skip them entirely.
                        skipCurrent = true;
                        break;
                    }
                    current.Prefix = prefix;
                    current.Id = id;
                    target.Prefixes.Add(prefix);
                    break;

                case "name":
                    current.Name = rest;
                    break;

                case "def":
                    current.Def = ExtractQuoted(rest);
                    break;

                case "is_a":
                    if (TryParseId(StripTrailingComment(rest), out _, out uint parentId))
                        current.ParentsIsA.Add(parentId);
                    break;

                case "relationship":
                    ParseRelationship(rest, current);
                    break;

                case "property_value":
                    ParsePropertyValue(rest, current);
                    break;

                case "synonym":
                    if (rest.Contains("EXACT", StringComparison.Ordinal))
                        current.ExactSynonyms.Add(ExtractQuoted(rest));
                    break;

                case "is_obsolete":
                    current.IsObsolete = rest.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;

                case "xref":
                    ParseXref(rest, current);
                    break;

                // Tags we intentionally skip: comment, alt_id, creation_date, created_by, etc.
            }
        }

        if (!skipCurrent) FinishTerm(current, currentStanzaType, target);
    }

    private static void FinishTerm(OboTerm? term, string? stanzaType, ObOntology target)
    {
        if (term is null || stanzaType != "Term") return;
        if (term.Id == OboTerm.MaxId) return; // id line never seen or skipped
        target.Terms[term.Id] = term;
    }

    private static bool TryParseId(string text, out string prefix, out uint id)
    {
        prefix = string.Empty;
        id = 0;
        int colon = text.IndexOf(':');
        if (colon < 0) return false;
        prefix = text[..colon].Trim();
        string idPart = text[(colon + 1)..].Trim();
        return uint.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    private static string StripTrailingComment(string s)
    {
        int bang = s.IndexOf('!');
        return bang >= 0 ? s[..bang].Trim() : s;
    }

    private static string ExtractQuoted(string s)
    {
        int first = s.IndexOf('"');
        if (first < 0) return s;
        int second = s.IndexOf('"', first + 1);
        if (second < 0) return s[(first + 1)..];
        return s.Substring(first + 1, second - first - 1);
    }

    private static void ParseRelationship(string rest, OboTerm term)
    {
        // Format: "relation_name TARGET_PREFIX:TARGET_ID [! comment]"
        string body = StripTrailingComment(rest);
        int space = body.IndexOf(' ');
        if (space <= 0) return;
        string relName = body[..space].Trim();
        string target = body[(space + 1)..].Trim();

        if (!TryParseId(target, out string targetPrefix, out uint targetId))
            return; // non-numeric target (e.g. NCIT:C25330) — skip

        if (relName == "part_of" && targetPrefix == term.Prefix)
        {
            term.ParentsPartOf.Add(targetId);
        }
        else
        {
            term.Relations.Add(new RelationEntry(relName, targetPrefix, targetId));
        }
    }

    private static void ParseXref(string rest, OboTerm term)
    {
        // Format variants we handle:
        //   xref: key "value"
        //   xref: key "value" [optional scope tags]
        //   xref: key VALUE (no quotes — seen rarely, e.g. dbxref tokens)
        // We only capture the "key: \"value\"" form used by Unimod; other xrefs are dropped.
        string body = StripTrailingComment(rest);
        int space = body.IndexOf(' ');
        if (space <= 0) return;
        string key = body[..space].Trim();
        string remainder = body[(space + 1)..].Trim();
        if (remainder.Length == 0) return;

        string value;
        if (remainder[0] == '"')
        {
            int second = remainder.IndexOf('"', 1);
            if (second < 0) return;
            value = remainder.Substring(1, second - 1);
        }
        else
        {
            // Non-quoted xref value (e.g. "RESID:AA0048"). Not interesting for Unimod, skip.
            return;
        }
        term.Xrefs.Add(new KeyValuePair<string, string>(key, value));
    }

    private static void ParsePropertyValue(string rest, OboTerm term)
    {
        // Format: "name value [xsd:type]"
        string body = StripTrailingComment(rest);
        int space = body.IndexOf(' ');
        if (space <= 0) return;
        string name = body[..space].Trim();
        string valueRaw = body[(space + 1)..].Trim();
        string value = ExtractQuoted(valueRaw);
        if (value == valueRaw)
        {
            // not quoted; strip any trailing xsd: annotation
            int xsd = value.IndexOf(" xsd:", StringComparison.Ordinal);
            if (xsd >= 0) value = value[..xsd];
        }
        term.PropertyValues.Add(new KeyValuePair<string, string>(name, value));
    }
}
