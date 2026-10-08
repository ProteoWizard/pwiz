using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Filters;

/// <summary>
/// Accepts spectra whose ordinal index is in the given <see cref="IntegerSet"/>.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_IndexSet.
/// </summary>
public sealed class IndexSetPredicate : ISpectrumPredicate
{
    private readonly IntegerSet _set;
    private int _seen;

    /// <summary>Creates a predicate over the given set of indices.</summary>
    public IndexSetPredicate(IntegerSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity)
    {
        if (_set.Contains(identity.Index))
        {
            _seen++;
            return PredicateDecision.Accept;
        }
        return PredicateDecision.Reject;
    }

    /// <inheritdoc/>
    public bool Done => _set.HasUpperBound(_seen - 1) && _seen >= _set.Count;

    /// <inheritdoc/>
    public string Describe() => "set of spectrum indices";
}

/// <summary>
/// Accepts spectra whose native "scan=NNN" id is in the given set.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_ScanNumberSet.
/// </summary>
public sealed class ScanNumberSetPredicate : ISpectrumPredicate
{
    private readonly IntegerSet _set;

    /// <summary>Creates a predicate over the given set of scan numbers.</summary>
    public ScanNumberSetPredicate(IntegerSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity)
    {
        string scan = Id.Value(identity.Id, "scan");
        if (string.IsNullOrEmpty(scan)) return PredicateDecision.Reject;
        if (!int.TryParse(scan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            return PredicateDecision.Reject;
        return _set.Contains(n) ? PredicateDecision.Accept : PredicateDecision.Reject;
    }

    /// <inheritdoc/>
    public string Describe() => "set of scan numbers";
}

/// <summary>
/// Accepts spectra whose id matches exactly one in the given set.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_IdSet.
/// </summary>
public sealed class IdSetPredicate : ISpectrumPredicate
{
    private readonly HashSet<string> _ids;

    /// <summary>Creates a predicate over the given set of ids.</summary>
    public IdSetPredicate(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        _ids = new HashSet<string>(ids, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) =>
        _ids.Contains(identity.Id) ? PredicateDecision.Accept : PredicateDecision.Reject;

    /// <inheritdoc/>
    public string Describe() => "set of spectrum ids";
}

/// <summary>
/// Accepts spectra with MS level in the given set (e.g. MS1 only, MS2+ only).
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_MSLevelSet.
/// </summary>
public sealed class MsLevelPredicate : ISpectrumPredicate
{
    private readonly IntegerSet _set;

    /// <summary>Creates a predicate over the given set of MS levels.</summary>
    public MsLevelPredicate(IntegerSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    /// <summary>Creates a predicate matching a single MS level (e.g. only MS2).</summary>
    public MsLevelPredicate(int msLevel) : this(new IntegerSet(msLevel)) { }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        int msLevel = spectrum.Params.CvParamValueOrDefault(CVID.MS_ms_level, 0);
        return _set.Contains(msLevel);
    }

    /// <inheritdoc/>
    public string Describe() => "set of MS levels";
}

/// <summary>
/// Accepts spectra with scan start time in [<see cref="Low"/>, <see cref="High"/>] seconds.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_ScanTimeRange.
/// </summary>
public sealed class ScanTimeRangePredicate : ISpectrumPredicate
{
    /// <summary>Lower bound of the accepted time range (seconds).</summary>
    public double Low { get; }

    /// <summary>Upper bound of the accepted time range (seconds).</summary>
    public double High { get; }

    /// <summary>If true (default), short-circuits when a spectrum's time passes <see cref="High"/>.</summary>
    public bool AssumeSorted { get; }

    private bool _done;

    /// <summary>Creates a scan-time-range predicate.</summary>
    public ScanTimeRangePredicate(double lowSeconds, double highSeconds, bool assumeSorted = true)
    {
        Low = lowSeconds;
        High = highSeconds;
        AssumeSorted = assumeSorted;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        if (spectrum.ScanList.Scans.Count == 0) return false;

        var scan = spectrum.ScanList.Scans[0];
        var timeParam = scan.CvParam(CVID.MS_scan_start_time);
        if (timeParam.IsEmpty) return false;

        double t = timeParam.TimeInSeconds();
        if (AssumeSorted && t > High) _done = true;
        return t >= Low && t <= High;
    }

    /// <inheritdoc/>
    public bool Done => _done;

    /// <inheritdoc/>
    public string Describe() => "scan time range";
}

/// <summary>
/// Accepts spectra with the given polarity. Pass <see cref="CVID.MS_positive_scan"/> or <see cref="CVID.MS_negative_scan"/>.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_Polarity.
/// </summary>
public sealed class PolarityPredicate : ISpectrumPredicate
{
    private readonly CVID _polarity;

    /// <summary>Creates a predicate for the given polarity CV term.</summary>
    public PolarityPredicate(CVID polarity)
    {
        if (polarity != CVID.MS_positive_scan && polarity != CVID.MS_negative_scan)
            throw new ArgumentException(
                $"Polarity must be MS_positive_scan or MS_negative_scan; got {polarity}", nameof(polarity));
        _polarity = polarity;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FastMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        return spectrum.Params.HasCVParam(_polarity);
    }

    /// <inheritdoc/>
    public string Describe() => "polarity";
}

/// <summary>
/// Accepts spectra whose default array length (number of peaks) is in the given set.
/// Port of pwiz::analysis::SpectrumList_FilterPredicate_DefaultArrayLengthSet.
/// </summary>
public sealed class DefaultArrayLengthPredicate : ISpectrumPredicate
{
    private readonly IntegerSet _set;

    /// <summary>Creates a predicate over the given set of array lengths.</summary>
    public DefaultArrayLengthPredicate(IntegerSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => DetailLevel.FullMetadata;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => PredicateDecision.NeedSpectrum;

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        return _set.Contains(spectrum.DefaultArrayLength);
    }

    /// <inheritdoc/>
    public string Describe() => "number of spectrum data points";
}

/// <summary>Composes two predicates with AND semantics.</summary>
public sealed class AndPredicate : ISpectrumPredicate
{
    private readonly ISpectrumPredicate _a, _b;

    /// <summary>Creates a predicate accepting iff both <paramref name="a"/> and <paramref name="b"/> accept.</summary>
    public AndPredicate(ISpectrumPredicate a, ISpectrumPredicate b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        _a = a; _b = b;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel =>
        (DetailLevel)System.Math.Max((int)_a.SuggestedDetailLevel, (int)_b.SuggestedDetailLevel);

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity)
    {
        var da = _a.Accept(identity);
        if (da == PredicateDecision.Reject) return PredicateDecision.Reject;
        var db = _b.Accept(identity);
        if (db == PredicateDecision.Reject) return PredicateDecision.Reject;
        if (da == PredicateDecision.Accept && db == PredicateDecision.Accept) return PredicateDecision.Accept;
        return PredicateDecision.NeedSpectrum;
    }

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum) => _a.Accept(spectrum) && _b.Accept(spectrum);

    /// <inheritdoc/>
    public bool Done => _a.Done || _b.Done;

    /// <inheritdoc/>
    public string Describe() => $"({_a.Describe()}) AND ({_b.Describe()})";
}

/// <summary>Wraps another predicate with the opposite polarity (Include ↔ Exclude semantics).</summary>
public sealed class NegatedPredicate : ISpectrumPredicate
{
    private readonly ISpectrumPredicate _inner;

    /// <summary>Creates a predicate whose accept result is the negation of <paramref name="inner"/>.</summary>
    public NegatedPredicate(ISpectrumPredicate inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public DetailLevel SuggestedDetailLevel => _inner.SuggestedDetailLevel;

    /// <inheritdoc/>
    public PredicateDecision Accept(SpectrumIdentity identity) => _inner.Accept(identity) switch
    {
        PredicateDecision.Accept => PredicateDecision.Reject,
        PredicateDecision.Reject => PredicateDecision.Accept,
        _ => PredicateDecision.NeedSpectrum,
    };

    /// <inheritdoc/>
    public bool Accept(Spectrum spectrum) => !_inner.Accept(spectrum);

    /// <inheritdoc/>
    public string Describe() => $"NOT ({_inner.Describe()})";
}
