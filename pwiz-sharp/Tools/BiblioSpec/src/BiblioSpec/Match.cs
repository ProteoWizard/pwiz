// Port of pwiz_tools/BiblioSpec/src/Match.{h,cpp}

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Score-type tag for a <see cref="Match"/>. Numeric values mirror cpp <c>SCORE_TYPE</c>
/// at Match.h:40 because they index into the per-match score array.
/// </summary>
public enum MatchScoreType
{
    /// <summary>Dot product (cpp DOTP = 0).</summary>
    Dotp = 0,
    /// <summary>q-value, no correction (cpp RAW_PVAL = 1).</summary>
    RawPval = 1,
    /// <summary>q-value, bonferroni corrected (cpp BONF_PVAL = 2).</summary>
    BonfPval = 2,
    /// <summary>q-value (cpp QVAL = 3).</summary>
    Qval = 3,
    /// <summary>Posterior error probability (cpp PEP = 4).</summary>
    Pep = 4,
    /// <summary>Number of binned peaks shared (cpp MATCHED_IONS = 5).</summary>
    MatchedIons = 5,
}

/// <summary>
/// A single query→library pairing carrying scores (dot product, matched-ion count, p-values),
/// rank, and the originating library id.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::Match</c> at Match.{h,cpp}. cpp parity:</para>
/// <list type="bullet">
///   <item>Default constructor initialises all scores to <c>-1</c> and rank/libId to <c>-1</c>
///         (Match.cpp:29).</item>
///   <item>The <see cref="ExpSpec"/> and <see cref="RefSpec"/> are stored as references — the
///         cpp version holds raw pointers and never owns them.</item>
/// </list>
/// </remarks>
public sealed class Match
{
    // Score count matches cpp NUM_SCORE_TYPES at Match.h:48 — equal to the highest enum + 1.
    private const int NumScoreTypes = 6;
    private readonly double[] _scores;

    /// <summary>Constructs an empty match with cpp sentinel scores (<c>-1</c>) and rank.</summary>
    /// <remarks>cpp Match.cpp:29.</remarks>
    public Match()
    {
        _scores = new double[NumScoreTypes];
        for (var i = 0; i < NumScoreTypes; i++) _scores[i] = -1;
        Rank = -1;
        MatchLibId = -1;
    }

    /// <summary>Constructs a match bound to the given query and library spectra.</summary>
    /// <remarks>cpp Match.cpp:43.</remarks>
    public Match(Spectrum experimentalSpec, RefSpectrum referenceSpec) : this()
    {
        ExpSpec = experimentalSpec;
        RefSpec = referenceSpec;
    }

    /// <summary>The query (experimental) spectrum (cpp <c>localSpec_</c>).</summary>
    public Spectrum? ExpSpec { get; }

    /// <summary>The library (reference) spectrum (cpp <c>localRef_</c>).</summary>
    public RefSpectrum? RefSpec { get; }

    /// <summary>The display rank within this query's match list (1 = best). -1 until set.</summary>
    public int Rank { get; set; }

    /// <summary>
    /// Library id (1-based) the <see cref="RefSpec"/> came from; 0 for decoys.
    /// </summary>
    /// <remarks>cpp <c>matchLibID_</c>. Stored separately because in cpp BlibSearch sets it on
    /// the Match before the RefSpectrum's own LibId is touched.</remarks>
    public int MatchLibId { get; set; }

    /// <summary>Set the score for the given <paramref name="type"/>.</summary>
    public void SetScore(MatchScoreType type, double score)
    {
        _scores[(int)type] = score;
    }

    /// <summary>Get the score for the given <paramref name="type"/> (or <c>-1</c> if never set).</summary>
    public double GetScore(MatchScoreType type) => _scores[(int)type];
}
