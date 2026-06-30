using System.Text.RegularExpressions;
using Pwiz.Data.Common.Cv;
using Pwiz.Util.Proteome;

namespace Pwiz.Data.Common.Proteome;

/// <summary>How many of a peptide's termini must match the cleavage agent's
/// cut sites for it to be considered a valid digestion product.</summary>
public enum Specificity
{
    /// <summary>Neither terminus must match cleavage motif(s).</summary>
    NonSpecific = 0,
    /// <summary>Either or both termini must match cleavage motif(s).</summary>
    SemiSpecific = 1,
    /// <summary>Both termini must match cleavage motif(s).</summary>
    FullySpecific = 2,
}

/// <summary>Configuration constraints on which digestion products to emit.
/// Port of <c>pwiz::proteome::Digestion::Config</c>.</summary>
public sealed record DigestionConfig
{
    /// <summary>Maximum missed-cleavage count allowed. cpp default: 100000 (effectively unlimited).</summary>
    public int MaximumMissedCleavages { get; init; } = 100000;

    /// <summary>Minimum peptide length (residues).</summary>
    public int MinimumLength { get; init; }

    /// <summary>Maximum peptide length (residues). cpp default: 100000.</summary>
    public int MaximumLength { get; init; } = 100000;

    /// <summary>Required terminus specificity.</summary>
    public Specificity MinimumSpecificity { get; init; } = Specificity.FullySpecific;

    /// <summary>True to ignore an N-terminal methionine when scoring missed cleavages —
    /// reflects the ribosomal removal of initiator Met. cpp default: true.</summary>
    public bool ClipNTerminalMethionine { get; init; } = true;
}

/// <summary>A peptide produced by digestion, with the surrounding metadata —
/// offset within the parent polypeptide, missed-cleavage count, terminus
/// specificity, and prefix/suffix residues. Port of
/// <c>pwiz::proteome::DigestedPeptide</c>.</summary>
public sealed class DigestedPeptide : Peptide
{
    /// <summary>Zero-based offset of the peptide's N-terminus in the parent polypeptide.</summary>
    public int Offset { get; }

    /// <summary>Number of cleavage sites the peptide spans without cutting at them.</summary>
    public int MissedCleavages { get; }

    /// <summary>True iff the peptide's N-terminus is at a cleavage site.</summary>
    public bool NTerminusIsSpecific { get; }

    /// <summary>True iff the peptide's C-terminus is at a cleavage site.</summary>
    public bool CTerminusIsSpecific { get; }

    /// <summary>0..2 — number of termini matching the cleavage motif.</summary>
    public int SpecificTermini => (NTerminusIsSpecific ? 1 : 0) + (CTerminusIsSpecific ? 1 : 0);

    /// <summary>Residue preceding the peptide in the parent, or empty if N-terminal.</summary>
    public string NTerminusPrefix { get; }

    /// <summary>Residue following the peptide in the parent, or empty if C-terminal.</summary>
    public string CTerminusSuffix { get; }

    internal DigestedPeptide(string sequence, int offset, int missedCleavages,
        bool nTerminusIsSpecific, bool cTerminusIsSpecific,
        string nTerminusPrefix, string cTerminusSuffix)
        : base(sequence)
    {
        Offset = offset;
        MissedCleavages = missedCleavages;
        NTerminusIsSpecific = nTerminusIsSpecific;
        CTerminusIsSpecific = cTerminusIsSpecific;
        NTerminusPrefix = nTerminusPrefix;
        CTerminusSuffix = cTerminusSuffix;
    }
}

/// <summary>
/// Enumerates peptides from in-silico proteolytic digestion of a polypeptide.
/// Port of <c>pwiz::proteome::Digestion</c> — same shape (cleavage-agent CVID
/// or regex + <see cref="DigestionConfig"/>), exposed as an
/// <see cref="IEnumerable{T}"/>.
/// </summary>
/// <remarks>
/// <para>cpp's <c>Digestion::const_iterator</c> is replaced by an <c>IEnumerable</c> of
/// <see cref="DigestedPeptide"/> implemented via <c>yield return</c>. Same algorithm:
/// </para>
/// <list type="bullet">
///   <item>Compile the cleavage agent regex(es), find every cleavage site offset in the
///         polypeptide (the site value <c>i</c> means "cut between sequence[i] and sequence[i+1]";
///         -1 means the N-terminus, length-1 means the C-terminus).</item>
///   <item>For <see cref="Specificity.FullySpecific"/>: iterate pairs (begin, end) of sites
///         where end &gt; begin and the implied length and missed-cleavage count fit the
///         <see cref="DigestionConfig"/>.</item>
///   <item>For semi/non-specific: iterate every substring window of valid length, then
///         derive specificity by checking whether the window's edges are at known sites.</item>
/// </list>
/// </remarks>
public sealed class Digestion
{
    private readonly string _sequence;
    private readonly DigestionConfig _config;
    private readonly CVID _cleavageAgent;          // CVID_Unknown for regex-only configs
    private readonly List<int> _sites;             // sorted offsets where a cut occurs
    private readonly HashSet<int> _siteSet;        // O(1) "is offset a site?" lookups

    /// <summary>Constructs a digestion driven by a single predefined cleavage agent.</summary>
    public Digestion(Peptide polypeptide, CVID cleavageAgent, DigestionConfig? config = null)
        : this(polypeptide, new[] { cleavageAgent }, config)
    { }

    /// <summary>Constructs a digestion driven by multiple cleavage agents acting in concert.</summary>
    public Digestion(Peptide polypeptide, IEnumerable<CVID> cleavageAgents, DigestionConfig? config = null)
        : this(polypeptide,
               cleavageAgents.Select(GetCleavageAgentRegex).Where(r => r.Length > 0),
               config,
               cleavageAgents.Count() == 1 ? cleavageAgents.First() : CVID.CVID_Unknown)
    { }

    /// <summary>Constructs a digestion driven by a user-supplied regex (zero-width
    /// lookbehind/lookahead style — e.g. <c>"(?&lt;=[KR])(?!P)"</c> for trypsin).</summary>
    public Digestion(Peptide polypeptide, string cleavageAgentRegex, DigestionConfig? config = null)
        : this(polypeptide, new[] { cleavageAgentRegex }, config, CVID.CVID_Unknown)
    { }

    /// <summary>Constructs a digestion driven by multiple user-supplied regexes.</summary>
    public Digestion(Peptide polypeptide, IEnumerable<string> cleavageAgentRegexes, DigestionConfig? config = null)
        : this(polypeptide, cleavageAgentRegexes, config, CVID.CVID_Unknown)
    { }

    private Digestion(Peptide polypeptide, IEnumerable<string> cleavageAgentRegexes,
                      DigestionConfig? config, CVID cleavageAgent)
    {
        ArgumentNullException.ThrowIfNull(polypeptide);
        _sequence = polypeptide.Sequence;
        _config = config ?? new DigestionConfig();
        _cleavageAgent = cleavageAgent;
        (_sites, _siteSet) = ComputeSites(_sequence, cleavageAgentRegexes, cleavageAgent);
    }

    /// <summary>Enumerates every peptide produced by the digestion that satisfies the
    /// <see cref="DigestionConfig"/>. Use <see cref="System.Linq.Enumerable.ToList{T}"/>
    /// to materialize for repeated traversal.</summary>
    public IEnumerable<DigestedPeptide> Enumerate()
    {
        return _config.MinimumSpecificity == Specificity.FullySpecific
            ? EnumerateFullySpecific()
            : EnumerateNonOrSemiSpecific();
    }

    /// <summary>Returns every instance of <paramref name="peptide"/> in the parent polypeptide
    /// that satisfies the <see cref="DigestionConfig"/>.</summary>
    public List<DigestedPeptide> FindAll(Peptide peptide)
    {
        ArgumentNullException.ThrowIfNull(peptide);
        var result = new List<DigestedPeptide>();
        string needle = peptide.Sequence;
        if (needle.Length < _config.MinimumLength || needle.Length > _config.MaximumLength) return result;

        int searchFrom = 0;
        while (true)
        {
            int begin = _sequence.IndexOf(needle, searchFrom, StringComparison.Ordinal);
            if (begin < 0) break;
            int end = begin + needle.Length - 1;
            bool nSpecific = _siteSet.Contains(begin - 1);
            bool cSpecific = _siteSet.Contains(end);
            if (((nSpecific ? 1 : 0) + (cSpecific ? 1 : 0)) >= (int)_config.MinimumSpecificity)
            {
                int missed = CountMissedCleavages(begin, end);
                if (missed <= _config.MaximumMissedCleavages)
                {
                    result.Add(new DigestedPeptide(needle, begin, missed, nSpecific, cSpecific,
                        begin > 0 ? _sequence.Substring(begin - 1, 1) : string.Empty,
                        end + 1 < _sequence.Length ? _sequence.Substring(end + 1, 1) : string.Empty));
                }
            }
            searchFrom = begin + 1;
        }
        return result;
    }

    /// <summary>Returns the first instance of <paramref name="peptide"/> in the parent
    /// polypeptide. <paramref name="offsetHint"/> shifts the search start; the search wraps
    /// to 0 if the hint position has no match. Throws if no instance is found.</summary>
    public DigestedPeptide FindFirst(Peptide peptide, int offsetHint = 0)
    {
        ArgumentNullException.ThrowIfNull(peptide);
        string needle = peptide.Sequence;
        if (needle.Length < _config.MinimumLength || needle.Length > _config.MaximumLength)
            throw new InvalidOperationException($"Peptide \"{needle}\" not found in \"{_sequence}\"");
        if (offsetHint + needle.Length > _sequence.Length) offsetHint = 0;

        int begin = _sequence.IndexOf(needle, offsetHint, StringComparison.Ordinal);
        if (begin < 0 && offsetHint > 0) begin = _sequence.IndexOf(needle, 0, StringComparison.Ordinal);
        if (begin < 0)
            throw new InvalidOperationException($"Peptide \"{needle}\" not found in \"{_sequence}\"");

        // Walk forward through every instance until one satisfies the specificity gate.
        while (true)
        {
            int end = begin + needle.Length - 1;
            bool nSpecific = _siteSet.Contains(begin - 1);
            bool cSpecific = _siteSet.Contains(end);
            int specCount = (nSpecific ? 1 : 0) + (cSpecific ? 1 : 0);
            if (specCount >= (int)_config.MinimumSpecificity)
            {
                int missed = CountMissedCleavages(begin, end);
                if (missed <= _config.MaximumMissedCleavages)
                {
                    return new DigestedPeptide(needle, begin, missed, nSpecific, cSpecific,
                        begin > 0 ? _sequence.Substring(begin - 1, 1) : string.Empty,
                        end + 1 < _sequence.Length ? _sequence.Substring(end + 1, 1) : string.Empty);
                }
            }
            int nextBegin = _sequence.IndexOf(needle, begin + 1, StringComparison.Ordinal);
            if (nextBegin < 0)
                throw new InvalidOperationException($"Peptide \"{needle}\" not found in \"{_sequence}\"");
            begin = nextBegin;
        }
    }

    // ----- cleavage agent table (hand-curated; cpp mines OBO has_regexp relations) -----

    /// <summary>Registered cleavage agent CVIDs. Includes the pseudo-agents
    /// <see cref="CVID.MS_unspecific_cleavage"/> + <see cref="CVID.MS_no_cleavage"/>.</summary>
    public static IReadOnlyCollection<CVID> GetCleavageAgents() => s_agentToRegex.Keys;

    /// <summary>Returns the cleavage-agent CVID matching <paramref name="agentName"/>
    /// (case-insensitive). Returns <see cref="CVID.CVID_Unknown"/> when the name is
    /// not a known agent.</summary>
    public static CVID GetCleavageAgentByName(string agentName)
    {
        ArgumentNullException.ThrowIfNull(agentName);
        return s_agentByName.TryGetValue(agentName.ToLowerInvariant(), out var v) ? v : CVID.CVID_Unknown;
    }

    /// <summary>Returns the cleavage-agent CVID whose canonical regex equals
    /// <paramref name="regex"/>. Returns <see cref="CVID.CVID_Unknown"/> on no match.</summary>
    public static CVID GetCleavageAgentByRegex(string regex)
    {
        ArgumentNullException.ThrowIfNull(regex);
        return s_agentByRegex.TryGetValue(regex, out var v) ? v : CVID.CVID_Unknown;
    }

    /// <summary>Returns the PSI-MS canonical Perl-style regex defining the cleavage sites for
    /// the given agent. Throws if the CVID isn't a known cleavage agent.</summary>
    public static string GetCleavageAgentRegex(CVID cleavageAgent)
    {
        if (!s_agentToRegex.TryGetValue(cleavageAgent, out var v))
            throw new ArgumentException($"CVID {cleavageAgent} is not a known cleavage agent");
        return v;
    }

    // Hand-curated map from cleavage agent CVID to canonical PSI-MS regex. Sourced from
    // the doc-comments in CVID.generated.cs (which carry the regex text). cpp builds this
    // dynamically by walking the OBO's has_regexp relations — that pipeline isn't ported
    // to sharp, so this table is the source of truth here.
    private static readonly Dictionary<CVID, string> s_agentToRegex = new()
    {
        [CVID.MS_Trypsin]                   = @"(?<=[KR])(?!P)",
        [CVID.MS_Arg_C]                     = @"(?<=R)(?!P)",
        [CVID.MS_Asp_N]                     = @"(?=[BD])",
        [CVID.MS_Asp_N_ambic]               = @"(?=[DE])",
        [CVID.MS_Chymotrypsin]              = @"(?<=[FYWL])(?!P)",
        [CVID.MS_CNBr]                      = @"(?<=M)",
        [CVID.MS_Formic_acid]               = @"((?<=D))|((?=D))",
        [CVID.MS_Lys_C]                     = @"(?<=K)(?!P)",
        [CVID.MS_Lys_C_P]                   = @"(?<=K)",
        [CVID.MS_PepsinA]                   = @"(?<=[FL])",
        [CVID.MS_TrypChymo]                 = @"(?<=[FYWLKR])(?!P)",
        [CVID.MS_Trypsin_P]                 = @"(?<=[KR])",
        [CVID.MS_V8_DE]                     = @"(?<=[BDEZ])(?!P)",
        [CVID.MS_V8_E]                      = @"(?<=[EZ])(?!P)",
        [CVID.MS_leukocyte_elastase]        = @"(?<=[ALIV])(?!P)",
        [CVID.MS_proline_endopeptidase]     = @"(?<=[HKR]P)(?!P)",
        [CVID.MS_glutamyl_endopeptidase]    = @"(?<=[^E]E)",
        [CVID.MS_2_iodobenzoate]            = @"(?<=W)",
        [CVID.MS_LysargiNase]               = @"(?=[KR])",
        // Pseudo-agents (no regex; iteration uses every offset as a "site").
        [CVID.MS_unspecific_cleavage]       = string.Empty,
        [CVID.MS_no_cleavage]               = string.Empty,
    };

    private static readonly Dictionary<string, CVID> s_agentByName = BuildNameMap();
    private static readonly Dictionary<string, CVID> s_agentByRegex = BuildRegexMap();

    private static Dictionary<string, CVID> BuildNameMap()
    {
        var map = new Dictionary<string, CVID>(StringComparer.OrdinalIgnoreCase);
        // Map both the enum name (e.g. "Trypsin") and the cpp display name (e.g. "Trypsin/P").
        foreach (var kv in s_agentToRegex)
        {
            string n = kv.Key.ToString();
            if (n.StartsWith("MS_", StringComparison.Ordinal)) n = n[3..];
            n = n.Replace('_', ' ');
            map[n] = kv.Key;
        }
        // Common synonyms cpp picks up from OBO exactSynonym relations.
        map["Trypsin/P"] = CVID.MS_Trypsin_P;
        map["Lys-C"] = CVID.MS_Lys_C;
        map["Lys-C/P"] = CVID.MS_Lys_C_P;
        map["Lys-N"] = CVID.MS_LysargiNase;
        map["Arg-C"] = CVID.MS_Arg_C;
        map["Asp-N"] = CVID.MS_Asp_N;
        map["Asp-N ambic"] = CVID.MS_Asp_N_ambic;
        map["V8-DE"] = CVID.MS_V8_DE;
        map["V8-E"] = CVID.MS_V8_E;
        map["Glu-C"] = CVID.MS_glutamyl_endopeptidase;
        return map;
    }

    private static Dictionary<string, CVID> BuildRegexMap()
    {
        var map = new Dictionary<string, CVID>(StringComparer.Ordinal);
        foreach (var kv in s_agentToRegex)
            if (!string.IsNullOrEmpty(kv.Value)) map[kv.Value] = kv.Key;
        return map;
    }

    // ----- site detection -----

    private static (List<int> sites, HashSet<int> set) ComputeSites(string sequence,
        IEnumerable<string> regexes, CVID cleavageAgent)
    {
        var sites = new List<int>();
        var siteSet = new HashSet<int>();
        // -1 is always a "site" so the N-terminus counts as specific.
        sites.Add(-1);
        siteSet.Add(-1);

        if (cleavageAgent == CVID.MS_unspecific_cleavage)
        {
            // Every offset is a potential cut.
            for (int i = 0; i < sequence.Length; i++) { sites.Add(i); siteSet.Add(i); }
            return (sites, siteSet);
        }

        // Only walk the regexes if cleavageAgent != MS_no_cleavage (where regex list is empty).
        foreach (string r in regexes)
        {
            if (string.IsNullOrEmpty(r)) continue;
            Regex rx;
            try { rx = new Regex(r, RegexOptions.CultureInvariant); }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Bad cleavage regex \"{r}\": {ex.Message}", ex);
            }
            // cpp's iteration treats a regex match's *position* as the cleavage offset.
            // For lookbehind-only patterns like "(?<=[KR])(?!P)", the match is zero-width
            // and sits *between* residues; the match.Index then equals the offset of the
            // residue that follows the cut. Site index = match.Index - 1 maps to cpp's
            // "between seq[i] and seq[i+1]" convention.
            foreach (Match m in rx.Matches(sequence))
            {
                int site = m.Index - 1;
                if (site >= 0 && site < sequence.Length && siteSet.Add(site)) sites.Add(site);
            }
        }
        // The C-terminus is always a site too.
        int cterm = sequence.Length - 1;
        if (siteSet.Add(cterm)) sites.Add(cterm);
        sites.Sort();
        return (sites, siteSet);
    }

    private int CountMissedCleavages(int beginOffset, int endOffset)
    {
        if (_cleavageAgent == CVID.MS_unspecific_cleavage || _cleavageAgent == CVID.MS_no_cleavage)
            return 0;
        int missed = 0;
        for (int i = beginOffset; i < endOffset; i++)
            if (_siteSet.Contains(i)) missed++;
        // Clip N-terminal Met: if the peptide starts at offset 0 with M and we counted
        // the implicit cleavage at -1, undo it. (cpp does this in the iterator too.)
        if (missed > 0 && _config.ClipNTerminalMethionine && beginOffset == 0
            && _sequence.Length > 0 && _sequence[0] == 'M' && _siteSet.Contains(-1))
            missed--;
        return missed;
    }

    // ----- enumeration: fully-specific path -----

    private IEnumerable<DigestedPeptide> EnumerateFullySpecific()
    {
        if (_sites.Count < 2) yield break;
        for (int bi = 0; bi < _sites.Count; bi++)
        {
            int begin = _sites[bi];
            for (int ei = bi + 1; ei < _sites.Count; ei++)
            {
                int end = _sites[ei];
                int curMissed = ei - bi - 1;
                if (curMissed > 0 && _config.ClipNTerminalMethionine && begin < 0
                    && _sequence.Length > 0 && _sequence[0] == 'M')
                    curMissed--;
                if (curMissed > _config.MaximumMissedCleavages) break;

                int length = end - begin;
                if (length > _config.MaximumLength) break;
                if (length < _config.MinimumLength) continue;

                int peptideStart = begin + 1;
                int peptideEnd = end + 1;  // exclusive
                string subseq = _sequence[peptideStart..peptideEnd];
                string prefix = begin >= 0 && begin < _sequence.Length
                    ? _sequence.Substring(begin, 1) : string.Empty;
                string suffix = peptideEnd < _sequence.Length
                    ? _sequence.Substring(peptideEnd, 1) : string.Empty;
                yield return new DigestedPeptide(subseq, peptideStart, curMissed,
                    nTerminusIsSpecific: true, cTerminusIsSpecific: true,
                    nTerminusPrefix: prefix, cTerminusSuffix: suffix);
            }
        }
    }

    // ----- enumeration: semi / non-specific path -----

    private IEnumerable<DigestedPeptide> EnumerateNonOrSemiSpecific()
    {
        // cpp's iterator walks every (beginNonSpecific, endNonSpecific) substring window
        // satisfying minimumLength <= window <= maximumLength, then derives specificity
        // from the surrounding site set. We do the same with a simpler nested loop;
        // peptide construction handles the edge cases (prefix at start, suffix at end).
        int maxLen = _sequence.Length;
        for (int beginNS = -1; beginNS < maxLen; beginNS++)
        {
            int minEnd = beginNS + _config.MinimumLength;
            for (int endNS = minEnd; endNS < maxLen; endNS++)
            {
                int curLength = endNS - beginNS;
                if (curLength > _config.MaximumLength) break;
                if (curLength < _config.MinimumLength) continue;

                int missed = 0;
                if (_cleavageAgent != CVID.MS_unspecific_cleavage && _cleavageAgent != CVID.MS_no_cleavage)
                {
                    for (int i = beginNS + 1; i < endNS; i++)
                        if (_siteSet.Contains(i)) missed++;
                    if (missed > 0 && _config.ClipNTerminalMethionine && beginNS < 0
                        && _sequence.Length > 0 && _sequence[0] == 'M')
                        missed--;
                }
                if (missed > _config.MaximumMissedCleavages) continue;

                bool nSpec = _siteSet.Contains(beginNS);
                bool cSpec = _siteSet.Contains(endNS);
                int spec = (nSpec ? 1 : 0) + (cSpec ? 1 : 0);
                if (spec < (int)_config.MinimumSpecificity) continue;

                int peptideStart = beginNS + 1;
                int peptideEnd = endNS + 1;
                string subseq = _sequence[peptideStart..peptideEnd];
                string prefix = beginNS >= 0 && beginNS < _sequence.Length
                    ? _sequence.Substring(beginNS, 1) : string.Empty;
                string suffix = peptideEnd < _sequence.Length
                    ? _sequence.Substring(peptideEnd, 1) : string.Empty;
                yield return new DigestedPeptide(subseq, peptideStart, missed,
                    nSpec, cSpec, prefix, suffix);
            }
        }
    }
}
