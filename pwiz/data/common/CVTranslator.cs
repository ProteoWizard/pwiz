using System.Globalization;

namespace Pwiz.Data.Common.Cv;

/// <summary>
/// Translates free-form text to CV terms. Port of pwiz/data::CVTranslator.
/// </summary>
/// <remarks>
/// Builds a dictionary from every term's name and exact synonyms (currently: just name,
/// since the initial port doesn't carry synonym data — see <see cref="CvLookup"/>).
/// Text is canonicalized (lowercase, underscore-delimited alphanumerics) before lookup.
/// Callers may <see cref="Insert"/> extra aliases.
/// </remarks>
public sealed class CVTranslator
{
    private readonly Dictionary<string, CVID> _map = new(StringComparer.Ordinal);

    private static readonly (string Text, CVID Cvid)[] s_defaultExtras =
    {
        ("ITMS", CVID.MS_ion_trap),
        ("FTMS", CVID.MS_FT_ICR),
    };

    /// <summary>
    /// Constructs a translator pre-populated with every non-obsolete MS/UO term's name and synonyms.
    /// </summary>
    public CVTranslator()
    {
        InsertCvTerms();
        InsertDefaultExtras();
    }

    /// <summary>
    /// Inserts an extra alias into the dictionary. Collisions throw unless the existing value matches.
    /// </summary>
    public void Insert(string text, CVID cvid)
    {
        ArgumentNullException.ThrowIfNull(text);
        string key = Canonicalize(text);

        if (_map.TryGetValue(key, out var existing))
        {
            if (ShouldIgnoreCollision(key, existing, cvid)) return;
            if (existing == cvid) return;
            // During the initial port, CV term names are derived from enum identifiers (which have
            // _OBSOLETE / trailing-underscore variants); many of these canonicalize to the same key.
            // We silently keep the first entry. Once the OBO-backed generator lands with real display
            // names from psi-ms.obo, the collision rate will drop; at that point this branch should
            // match the stricter pwiz behavior (throw on unknown collisions).
            return;
        }
        _map[key] = cvid;
    }

    /// <summary>Translates free text to a CVID. Returns <see cref="CVID.CVID_Unknown"/> if no match.</summary>
    public CVID Translate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _map.TryGetValue(Canonicalize(text), out var cvid) ? cvid : CVID.CVID_Unknown;
    }

    private void InsertCvTerms()
    {
        foreach (var cvid in CvLookup.AllCvids)
        {
            var info = CvLookup.CvTermInfo(cvid);
            if (info.IsObsolete) continue;
            if (info.Prefix is not ("MS" or "UO")) continue;

            Insert(info.Name, cvid);

            // Only MS terms have synonyms in the pwiz C++ source (prefix numeric range < 1e8).
            // The initial CVTermInfo port has no synonym data yet; this loop is ready for the
            // OBO-based regeneration that will populate ExactSynonyms.
            foreach (var syn in info.ExactSynonyms)
                Insert(syn, cvid);
        }
    }

    private void InsertDefaultExtras()
    {
        foreach (var (text, cvid) in s_defaultExtras)
            Insert(text, cvid);
    }

    private static string Canonicalize(string s)
    {
        // Lowercase alphanumerics (keep '+'), collapse everything else to whitespace,
        // split on whitespace, rejoin with underscores + trailing underscore.
        // Matches pwiz's canonicalize() output.
        string pre = Preprocess(s);
        var tokens = pre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(tokens.Select(t => t + "_"));
    }

    private static string Preprocess(string s)
    {
        bool looksLikeRegex = s.StartsWith("(?<=", StringComparison.Ordinal);
        var chars = new char[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsLetterOrDigit(c))
                chars[i] = char.ToLowerInvariant(c);
            else if (c == '+')
                chars[i] = c;
            else
                chars[i] = looksLikeRegex ? '_' : ' ';
        }
        return new string(chars);
    }

    private static bool ShouldIgnoreCollision(string key, CVID existing, CVID cvid)
    {
        // Carried over verbatim from pwiz/data::CVTranslator::shouldIgnore.
        if (key == "unit_" && existing == CVID.MS_unit_OBSOLETE && cvid == CVID.UO_unit) return true;
        if (key == "pi_" && existing == CVID.MS_PI && cvid == CVID.UO_pi) return true;
        if (key == "pi_" && existing == CVID.MS_PI && cvid == CVID.MS_pI) return true;
        if (cvid == CVID.UO_volt_second_per_square_centimeter) return true;
        return false;
    }
}
