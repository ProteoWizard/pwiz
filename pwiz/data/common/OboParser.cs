using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Parsed terms in file order. A numeric id is unique only within a prefix: psi-ms.obo
    /// carries both MS:1002001 and PEFF:1002001 (and a copy of the UO:0000001.. unit terms), so
    /// this is a list rather than an id-keyed table - see <see cref="FindTerm"/>.
    /// </summary>
    public List<OboTerm> Terms { get; } = new();

    /// <summary>The term with this accession (e.g. "MS", 1000031), or null when the file has none.</summary>
    public OboTerm? FindTerm(string prefix, uint id)
    {
        foreach (var term in Terms)
        {
            if (term.Id == id && term.Prefix == prefix)
                return term;
        }
        return null;
    }

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
/// Recognizes: <c>[Term]</c> stanzas with <c>id</c>, <c>name</c>, <c>def</c> (or <c>comment</c>
/// when there is no def), <c>is_a</c>, <c>relationship</c>, <c>property_value</c>,
/// <c>synonym</c> / <c>exact_synonym</c> (EXACT only), <c>is_obsolete</c>, <c>xref</c>.
/// Term ids, names and synonyms come out exactly as cpp obo.cpp reads them, which is what
/// lets the CvGen build tool reproduce cvgen.cpp's enum identifiers.
/// </summary>
public static class OboParser
{
    // def: "(OBSOLETE )?text" [dbxrefs] {trailing qualifiers}. The text group is greedy and
    // the dbxref group optional, so the closing quote is the last one that still leaves a
    // valid tail - which is how escaped quotes inside the text survive and an NCIT
    // {...="NCI"} qualifier does not swallow the definition. Same regex as cpp parse_def.
    private static readonly Regex s_defRegex = new("^\"(OBSOLETE )?(.*)\"(\\s*\\[.*\\].*)?$", RegexOptions.Compiled);
    private static readonly Regex s_synonymRegex = new("^\"(.*)\"\\s*(\\w+)?.*$", RegexOptions.Compiled);
    private static readonly Regex s_exactSynonymRegex = new("^\"(.*)\".*$", RegexOptions.Compiled);

    /// <summary>Parses OBO content from <paramref name="reader"/> into <paramref name="target"/>.</summary>
    public static void Parse(TextReader reader, ObOntology target)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(target);

        OboTerm? current = null;
        string? currentStanzaType = null;
        bool inHeader = true;
        bool skipCurrent = false; // true when the current Term has an id that does not parse

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (inHeader)
            {
                // The header ends at the first BLANK line, as cpp obo.cpp parse() does - not at
                // the first stanza. unimod.obo has a blank line after its second line, so the
                // two disagreed about whether "saved-by" and "default-namespace" are header:
                // a "remark: namespace:" after a blank line would have given CvGen one prefix
                // more than cvgen, shifting every later value block by 100,000,000.
                if (string.IsNullOrWhiteSpace(line))
                {
                    inHeader = false;
                    continue;
                }
                if (!line.StartsWith('['))
                {
                    target.Header.Add(line);
                    continue;
                }
                inHeader = false;
                // fall through to stanza handling
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
                        // cpp obo.cpp would throw here; skipping the stanza is the lenient
                        // equivalent for an id neither side can represent.
                        skipCurrent = true;
                        break;
                    }
                    current.Prefix = prefix;
                    current.Id = id;
                    target.Prefixes.Add(prefix);
                    break;

                case "name":
                    current.Name = Unescape(rest);
                    break;

                case "def":
                    ParseDef(rest, current);
                    break;

                case "comment":
                    // Some OBO representations carry the definition in "comment" instead of
                    // "def"; a def that follows overrides it (cpp obo.cpp parse_comment_as_def).
                    if (current.Def.Length == 0)
                    {
                        current.Def = rest;
                        current.IsObsolete = rest.Contains("obsolete", StringComparison.OrdinalIgnoreCase);
                    }
                    break;

                case "is_a":
                    // Same-prefix only, as cpp parse_is_a. A parent id is stored bare and
                    // CvLookup re-homes it into the CHILD's value block, so keeping a
                    // cross-prefix parent would invent a relation: psi-ms.obo's five
                    // "is_a: UO:0000000 ! unit" lines (MS:1000040/43/46, MS:1000807,
                    // MS:1002814) would each land as (CVID)0 - the MS root - making CvIsA
                    // answer true where cpp answers false.
                    if (TryParseId(StripTrailingComment(rest), out string parentPrefix, out uint parentId)
                        && parentPrefix == current.Prefix)
                        current.ParentsIsA.Add(parentId);
                    break;

                case "relationship":
                    ParseRelationship(rest, current);
                    break;

                case "property_value":
                    ParsePropertyValue(rest, current);
                    break;

                case "synonym":
                {
                    // Format: "text" SCOPE [dbxrefs]; only EXACT synonyms are kept.
                    var match = s_synonymRegex.Match(rest);
                    if (match.Success && match.Groups[2].Value == "EXACT")
                        AddSynonym(current, match.Groups[1].Value);
                    break;
                }

                case "exact_synonym":
                {
                    // Pre-1.2 OBO spelling of an EXACT synonym.
                    var match = s_exactSynonymRegex.Match(rest);
                    if (match.Success)
                        AddSynonym(current, match.Groups[1].Value);
                    break;
                }

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
        target.Terms.Add(term);
    }

    private static void ParseDef(string rest, OboTerm term)
    {
        var match = s_defRegex.Match(rest);
        if (!match.Success)
        {
            term.Def = ExtractQuoted(rest);
            return;
        }
        term.Def = match.Groups[2].Value;
        // Assignment, not |=: cpp parse_def overwrites whatever an earlier is_obsolete said.
        term.IsObsolete = match.Groups[1].Success;
    }

    private static bool TryParseId(string text, out string prefix, out uint id)
    {
        prefix = string.Empty;
        id = 0;
        int colon = text.IndexOf(':');
        if (colon < 0) return false;
        prefix = text[..colon].Trim();
        string idPart = TranslateLetters(text[(colon + 1)..].Trim());
        return uint.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    // An accession like NCIT:C25330 is not numeric, and the CVID enum needs a number: cpp
    // obo.cpp unescape(id, translateLetters=true) maps A..Z to 1..26 (C25330 -> 325330),
    // which is what the generated NCIT_* enum values encode.
    private static string TranslateLetters(string id)
    {
        if (!id.Any(char.IsAsciiLetter))
            return id;
        var sb = new StringBuilder(id.Length + 4);
        foreach (char c in id)
        {
            if (char.IsAsciiLetter(c))
                sb.Append(char.ToUpperInvariant(c) - 'A' + 1);
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string StripTrailingComment(string s)
    {
        int bang = s.IndexOf('!');
        return bang >= 0 ? s[..bang].Trim() : s;
    }

    // Mirrors cpp obo.cpp add_synonym. A synonym that differs from the term name only in which
    // punctuation it uses ("LTQ Velos ETD" vs "LTQ Velos/ETD") escapes to the very same enum
    // identifier as the term, so the differing characters are hex-encoded instead of
    // underscored. Note this comparison rule is NOT the generator's escaping rule - cpp has
    // the same two rules and they differ there too ('_' and the named UTF-8 escapes are
    // special to CvidEnumGenerator.ToEscapedCharacters and not to this one) - so unifying
    // them would diverge from cvgen rather than fix anything.
    private static void AddSynonym(OboTerm term, string synonym)
    {
        string unescaped = Unescape(synonym);
        string name = Unescape(term.Name);
        if (NormalizeForComparison(unescaped) != NormalizeForComparison(name))
        {
            term.ExactSynonyms.Add(unescaped);
            return;
        }

        // Equal normalizations mean equal UTF-16 lengths, so this compares position for
        // position. cpp encodes one _xNN_ per differing BYTE; this encodes one per byte of the
        // differing CHARACTER, which is the same thing for the ASCII punctuation every real
        // case involves, and unlike cpp cannot emit a lone continuation byte (which is not
        // valid UTF-8 and would come back as U+FFFD).
        var encoded = new StringBuilder(unescaped.Length * 2);
        for (int i = 0; i < unescaped.Length; ++i)
        {
            if (unescaped[i] == name[i])
            {
                encoded.Append(unescaped[i]);
                continue;
            }
            foreach (byte b in Encoding.UTF8.GetBytes(unescaped[i].ToString()))
                encoded.Append("_x").Append(b.ToString("X2", CultureInfo.InvariantCulture)).Append('_');
        }
        term.ExactSynonyms.Add(encoded.ToString());
    }

    private static string NormalizeForComparison(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    // OBO escapes the characters that are significant to its own syntax (a term name like
    // "(?<=[ALIV])(?\!P)" would otherwise start a comment at the '!'). Mirrors cpp obo.cpp
    // unescape(), so names and synonyms match the cpp-generated cv.cpp byte for byte.
    private static string Unescape(string s)
    {
        if (!s.Contains('\\')) return s;
        return s.Replace("\\!", "!").Replace("\\:", ":").Replace("\\,", ",")
                .Replace("\\(", "(").Replace("\\)", ")")
                .Replace("\\[", "[").Replace("\\]", "]")
                .Replace("\\{", "{").Replace("\\}", "}");
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

        // "xsd:" targets name a value TYPE, not a term, and must be skipped before the id is
        // parsed (cpp parse_relationship does the same). TryParseId translates letters to
        // digits for accessions like NCIT:C25330, so without this guard psi-ms.obo's 1366
        // "relationship: has_value_type xsd:<type>" lines yield 1155 fabricated relations
        // (xsd:float -> 61215120) and 211 silently dropped ones, sorted only by which
        // spellings happen to overflow uint.
        if (target.StartsWith("xsd:", StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryParseId(target, out string targetPrefix, out uint targetId))
            return; // target id neither numeric nor letter-encoded

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
