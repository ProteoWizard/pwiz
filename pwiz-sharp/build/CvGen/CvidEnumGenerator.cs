using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pwiz.Data.Common.Obo;

namespace Pwiz.Build.CvGen;

/// <summary>
/// Generates the <c>CVID</c> enum source from OBO files. Port of the enum half of pwiz cpp
/// <c>cvgen.cpp</c> (+ <c>cvgen_common.hpp</c>): the identifiers, values, doc comments and
/// synonym aliases come out identical to what cvgen wrote into <c>cv.hpp</c> for the same
/// OBOs, so code written against the cpp-generated enum keeps compiling. The relational half
/// of cvgen (the term tables in <c>cv.cpp</c>) has no C# counterpart because
/// <c>Pwiz.Data.Common.Cv.CvLookup</c> reads the embedded OBOs at run time.
/// </summary>
public static class CvidEnumGenerator
{
    /// <summary>
    /// Spacing between the value ranges of successive prefixes: MS terms keep their accession
    /// number, the second prefix seen adds 100000000, the third 200000000, and so on.
    /// </summary>
    public const long EnumBlockSize = 100_000_000;

    // cpp cvgen_common.hpp utf8EncodingMap: readable identifiers for the non-ASCII characters
    // the ontologies actually use; anything else non-ASCII is hex-encoded byte by byte.
    private static readonly (byte[] Utf8, string Encoded)[] s_utf8Encodings = new[]
    {
        ("\u00B2", "__sq__"),        // superscript 2
        ("\u00B3", "__cu__"),        // superscript 3
        ("\u00B9", "__sup1__"),      // superscript 1
        ("\u00B0", "__deg__"),       // degree sign
        ("\u00C5", "__angstrom__"),  // angstrom
        ("\u03B1", "__alpha__"),     // greek alpha
        ("\u03B2", "__beta__"),      // greek beta
        ("\u03B3", "__gamma__"),     // greek gamma
        ("\u03B4", "__delta__"),     // greek delta
        ("\u00B5", "__micro__"),     // micro sign
        ("\u0394", "__Delta__"),     // greek Delta
        ("\u00B1", "__plusminus__"), // plus-minus
    }.Select(pair => (Encoding.UTF8.GetBytes(pair.Item1), pair.Item2)).ToArray();

    /// <summary>
    /// Renders the complete <c>CVID.generated.cs</c> for <paramref name="obos"/>, which must be
    /// in the order cvgen received them (psi-ms.obo, unimod.obo, unit.obo): each OBO's prefixes
    /// are numbered in that order, and the numbering is what the enum values encode. Each OBO's
    /// <see cref="ObOntology.Filename"/> must be set, because psi-ms.obo carries a copy of the
    /// unit terms that cvgen ignores in favor of unit.obo's.
    /// </summary>
    public static string Generate(IReadOnlyList<ObOntology> obos)
    {
        ArgumentNullException.ThrowIfNull(obos);

        // IsIgnoredTerm keys off the filename, as cpp parseStanza does, so an ontology parsed
        // from a stream would silently keep psi-ms.obo's 53 duplicated UO terms - which
        // renumbers every value block after UO and emits each of those identifiers twice.
        // Refuse rather than generate that.
        foreach (var obo in obos)
        {
            if (string.IsNullOrEmpty(obo.Filename))
                throw new ArgumentException("Every ObOntology must carry the Filename it was loaded from; the psi-ms.obo unit-term rule is keyed on it.", nameof(obos));
        }

        var multiplierByPrefix = new Dictionary<string, int>(StringComparer.Ordinal);
        var termsByObo = new List<IReadOnlyList<OboTerm>>();
        foreach (var obo in obos)
        {
            // Accession order, as cvgen's std::set<Term> iterates. That set was keyed on the
            // number alone, so cvgen silently dropped PEFF:1002001-1002003 behind MS:1002001-
            // 1002003; they are kept here (their values do not collide), the one deliberate
            // difference from cv.hpp.
            var terms = obo.Terms.Where(term => !IsIgnoredTerm(obo, term)).OrderBy(term => term.Id).ToList();
            termsByObo.Add(terms);
            // cpp assigns each prefix the map's size at the time, so a prefix that a later OBO
            // repeats is renumbered; kept so the values can only ever match cvgen's.
            foreach (var prefix in Prefixes(obo, terms))
                multiplierByPrefix[prefix] = multiplierByPrefix.Count;
        }

        var sb = new StringBuilder();
        WriteHeader(sb, obos);
        sb.Append("    CVID_Unknown = -1");

        // Every identifier written so far, so a synonym that escapes to a name already taken
        // (a term with both "TOF/TOF" and "TOF-TOF" synonyms) is dropped rather than emitted twice.
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < obos.Count; ++i)
        {
            var terms = termsByObo[i];
            var correctedNames = CorrectedEnumNames(terms, multiplierByPrefix);
            foreach (var term in terms)
            {
                string enumName = correctedNames[term];
                sb.Append(",\n\n    /// ").Append(term.Name).Append(": ").Append(term.Def).Append('\n')
                  .Append("    ").Append(enumName).Append(" = ")
                  .Append(EnumValue(term, multiplierByPrefix).ToString(CultureInfo.InvariantCulture));
                // CorrectedEnumNames only disambiguates term against term within one OBO, so a
                // term identifier can still collide with a synonym alias or with a term in
                // another OBO. cvgen emitted the duplicate and let its own build fail; here the
                // artifact is committed, so the failure would surface as CS0102 in
                // CVID.generated.cs long after this ran. Fail where the cause is.
                if (!emitted.Add(enumName))
                    throw new InvalidOperationException($"Duplicate CVID enumerator '{enumName}' for {term.Prefix}:{term.Id}. Two terms (or a term and a synonym alias) escape to the same identifier; cvgen's per-OBO de-collision does not cover this case.");

                if (term.Prefix != "MS")
                    continue;
                foreach (var synonym in term.ExactSynonyms)
                {
                    string synonymName = EnumName(term.Prefix, synonym, term.IsObsolete);
                    if (!emitted.Add(synonymName))
                        continue;
                    sb.Append(",\n\n    /// ").Append(synonym).Append(" (").Append(term.Name).Append("): ").Append(term.Def).Append('\n')
                      .Append("    ").Append(synonymName).Append(" = ").Append(enumName);
                }
            }
        }
        sb.Append("\n}\n");
        return sb.ToString();
    }

    /// <summary>The enum identifier for a term: prefix, escaped name, and <c>_OBSOLETE</c> when obsolete.</summary>
    public static string EnumName(string prefix, string name, bool isObsolete)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(name);
        return prefix + "_" + ToEscapedCharacters(name) + (isObsolete ? "_OBSOLETE" : "");
    }

    /// <summary>The enum value for a term: its accession number plus its prefix's block offset.</summary>
    public static long EnumValue(OboTerm term, IReadOnlyDictionary<string, int> multiplierByPrefix)
    {
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(multiplierByPrefix);
        return term.Id + EnumBlockSize * multiplierByPrefix[term.Prefix];
    }

    /// <summary>
    /// Turns a term name into an identifier: ASCII letters, digits and '_' pass through, every
    /// other ASCII character becomes '_', and non-ASCII characters become either their entry
    /// in the readable table (micro sign to <c>__micro__</c>) or one <c>_xNN_</c> per UTF-8 byte.
    /// </summary>
    public static string ToEscapedCharacters(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        byte[] utf8 = Encoding.UTF8.GetBytes(name);
        var encoded = new StringBuilder(utf8.Length * 2);
        for (int i = 0; i < utf8.Length;)
        {
            byte b = utf8[i];
            if (b < 0x80)
            {
                encoded.Append(char.IsAsciiLetterOrDigit((char)b) || b == '_' ? (char)b : '_');
                ++i;
                continue;
            }

            var known = s_utf8Encodings.FirstOrDefault(pair => utf8.AsSpan(i).StartsWith(pair.Utf8));
            if (known.Encoded is not null)
            {
                encoded.Append(known.Encoded);
                i += known.Utf8.Length;
                continue;
            }

            int sequenceLength = (b & 0xE0) == 0xC0 ? 2 : (b & 0xF0) == 0xE0 ? 3 : (b & 0xF8) == 0xF0 ? 4 : 1;
            for (int j = 0; j < sequenceLength && i + j < utf8.Length; ++j)
                encoded.Append("_x").Append(utf8[i + j].ToString("X2", CultureInfo.InvariantCulture)).Append('_');
            i += sequenceLength;
        }
        return encoded.ToString();
    }

    // cvgen ignores the unit terms that psi-ms.obo duplicates (unit.obo is the source of those).
    private static bool IsIgnoredTerm(ObOntology obo, OboTerm term)
        => term.Prefix == "UO" && Path.GetFileName(obo.Filename).EndsWith("psi-ms.obo", StringComparison.OrdinalIgnoreCase);

    // cpp OBO::prefixes: the header's "remark: namespace:" declarations plus every kept term's
    // prefix, as an ordered set - that order is what numbers the value blocks, so the capture
    // has to be cpp's exactly. Its regex is "remark: namespace:\s*(\w+).*", and \w stops at
    // the first non-word character: "remark: namespace: PSI-MS" declares PSI, not PSI-MS, and
    // taking the whole token would sort differently and shift every block after it.
    private static readonly Regex s_namespaceRemark = new(@"^remark:\s*namespace:\s*(\w+)", RegexOptions.Compiled);

    private static SortedSet<string> Prefixes(ObOntology obo, IEnumerable<OboTerm> terms)
    {
        var prefixes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var line in obo.Header)
        {
            var match = s_namespaceRemark.Match(line);
            if (match.Success)
                prefixes.Add(match.Groups[1].Value);
        }
        foreach (var term in terms)
            prefixes.Add(term.Prefix);
        return prefixes;
    }

    // Two terms in one OBO can escape to the same identifier ("Trans" and "trans" are the
    // classic); cvgen keeps both apart by suffixing each with its own value.
    private static Dictionary<OboTerm, string> CorrectedEnumNames(IReadOnlyList<OboTerm> terms, IReadOnlyDictionary<string, int> multiplierByPrefix)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            string name = EnumName(term.Prefix, term.Name, term.IsObsolete);
            occurrences[name] = occurrences.GetValueOrDefault(name) + 1;
        }

        var corrected = new Dictionary<OboTerm, string>();
        foreach (var term in terms)
        {
            string name = EnumName(term.Prefix, term.Name, term.IsObsolete);
            if (occurrences[name] > 1)
                name += "_" + EnumValue(term, multiplierByPrefix).ToString(CultureInfo.InvariantCulture);
            corrected[term] = name;
        }
        return corrected;
    }

    private static void WriteHeader(StringBuilder sb, IReadOnlyList<ObOntology> obos)
    {
        sb.Append("// This file is generated from the OBO files below by pwiz-sharp/build/CvGen.\n")
          .Append("// Do not edit by hand: run `dotnet run --project pwiz-sharp/build/CvGen` to regenerate.\n");
        foreach (var obo in obos)
        {
            sb.Append("//   ").Append(Path.GetFileName(obo.Filename));
            foreach (var line in obo.Header)
            {
                if (line.StartsWith("format-version:", StringComparison.Ordinal)
                    || line.StartsWith("data-version:", StringComparison.Ordinal)
                    || line.StartsWith("date:", StringComparison.Ordinal))
                    sb.Append("  ").Append(line);
            }
            sb.Append('\n');
        }
        sb.Append('\n')
          .Append("// ReSharper disable All\n")
          .Append("#pragma warning disable CS1591, CS1570, CS1572, CS1573, CS1574, CA1707, CA1028, CA1008\n")
          .Append('\n')
          .Append("namespace Pwiz.Data.Common.Cv;\n")
          .Append('\n')
          .Append("/// <summary>\n")
          .Append("/// Enumeration of controlled vocabulary (CV) terms, generated from OBO file(s).\n")
          .Append("/// Port of pwiz/cv::CVID; values are the numeric CV accession numbers.\n")
          .Append("/// </summary>\n")
          .Append("public enum CVID\n")
          .Append("{\n");
    }
}
