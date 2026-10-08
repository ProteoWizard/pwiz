using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pwiz.Util.Misc;

/// <summary>
/// A sorted union of closed integer intervals. Supports parsing the pwiz list syntax
/// (<c>"[-3,2] 5 8-9 10-"</c>) and efficient membership / enumeration over large ranges.
/// </summary>
/// <remarks>Port of pwiz::util::IntegerSet.</remarks>
public sealed class IntegerSet : IEnumerable<int>
{
    /// <summary>A single closed interval [<see cref="Begin"/>, <see cref="End"/>].</summary>
    public readonly record struct Interval(int Begin, int End)
    {
        /// <summary>True iff <paramref name="n"/> is within this interval.</summary>
        public bool Contains(int n) => n >= Begin && n <= End;

        /// <inheritdoc/>
        public override string ToString() =>
            Begin == End
                ? Begin.ToString(CultureInfo.InvariantCulture)
                : $"[{Begin.ToString(CultureInfo.InvariantCulture)},{End.ToString(CultureInfo.InvariantCulture)}]";
    }

    private readonly List<Interval> _intervals = new();

    /// <summary>Creates an empty IntegerSet.</summary>
    public IntegerSet() { }

    /// <summary>Creates an IntegerSet containing a single integer.</summary>
    public IntegerSet(int value) => Insert(value, value);

    /// <summary>Creates an IntegerSet with a single closed interval [<paramref name="begin"/>, <paramref name="end"/>].</summary>
    public IntegerSet(int begin, int end) => Insert(begin, end);

    /// <summary>All intervals in the set, sorted by <see cref="Interval.Begin"/>.</summary>
    public IReadOnlyList<Interval> Intervals => _intervals;

    /// <summary>True iff the set is empty.</summary>
    public bool IsEmpty => _intervals.Count == 0;

    /// <summary>The number of intervals currently stored (after coalescing).</summary>
    public int IntervalCount => _intervals.Count;

    /// <summary>
    /// Total number of integers in the set (or <see cref="long.MaxValue"/>-ish for open-ended intervals).
    /// Matches pwiz's <c>size()</c>: sum of (end - begin + 1) across intervals.
    /// </summary>
    public long Count
    {
        get
        {
            long total = 0;
            foreach (var iv in _intervals) total += (long)iv.End - iv.Begin + 1;
            return total;
        }
    }

    /// <summary>True iff <paramref name="n"/> is in the set.</summary>
    public bool Contains(int n)
    {
        foreach (var iv in _intervals)
            if (iv.Contains(n)) return true;
        return false;
    }

    /// <summary>
    /// True iff <paramref name="n"/> is an upper bound of the set
    /// (no interval extends beyond <paramref name="n"/>).
    /// </summary>
    public bool HasUpperBound(int n)
    {
        if (_intervals.Count == 0) return true;
        return _intervals[^1].End <= n;
    }

    /// <summary>Inserts a single integer.</summary>
    public void Insert(int value) => Insert(value, value);

    /// <summary>Inserts a closed interval [<paramref name="begin"/>, <paramref name="end"/>].</summary>
    public void Insert(int begin, int end)
    {
        if (begin > end) (begin, end) = (end, begin);

        // Find where this interval fits, coalescing any overlapping/adjacent ones.
        int i = 0;
        while (i < _intervals.Count && _intervals[i].End < begin - 1) i++;

        int newBegin = begin;
        int newEnd = end;

        while (i < _intervals.Count && _intervals[i].Begin <= end + 1)
        {
            newBegin = Math.Min(newBegin, _intervals[i].Begin);
            newEnd = Math.Max(newEnd, _intervals[i].End);
            _intervals.RemoveAt(i);
        }

        _intervals.Insert(i, new Interval(newBegin, newEnd));
    }

    /// <summary>Inserts a pre-built interval.</summary>
    public void Insert(Interval interval) => Insert(interval.Begin, interval.End);

    private static readonly Regex s_tokenRe =
        new(@"\[\s*(?<b>-?\d+)\s*,\s*(?<e>-?\d+)\s*\]" +
            @"|(?<b2>-?\d+)\s*-\s*(?<e2>-?\d+)" +
            @"|(?<b3>-?\d+)\s*-" +
            @"|(?<b4>-?\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses a whitespace-delimited list of closed intervals.
    /// Supported forms: <c>n</c>, <c>a-b</c>, <c>a-</c> (open-ended upwards), <c>[a,b]</c>.
    /// Example: <c>"[-3,2] 5 8-9 10-"</c>.
    /// </summary>
    public void Parse(string intervalList)
    {
        ArgumentNullException.ThrowIfNull(intervalList);
        foreach (Match m in s_tokenRe.Matches(intervalList))
        {
            int b, e;
            if (m.Groups["b"].Success)
            {
                b = int.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture);
                e = int.Parse(m.Groups["e"].Value, CultureInfo.InvariantCulture);
            }
            else if (m.Groups["b2"].Success)
            {
                b = int.Parse(m.Groups["b2"].Value, CultureInfo.InvariantCulture);
                e = int.Parse(m.Groups["e2"].Value, CultureInfo.InvariantCulture);
            }
            else if (m.Groups["b3"].Success)
            {
                b = int.Parse(m.Groups["b3"].Value, CultureInfo.InvariantCulture);
                e = int.MaxValue;
            }
            else
            {
                b = int.Parse(m.Groups["b4"].Value, CultureInfo.InvariantCulture);
                e = b;
            }
            Insert(b, e);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<int> GetEnumerator()
    {
        foreach (var iv in _intervals)
            for (int n = iv.Begin; n <= iv.End; n++)
                yield return n;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _intervals.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(_intervals[i]);
        }
        return sb.ToString();
    }

    // ---- predefined sets ----

    /// <summary>The empty set.</summary>
    public static readonly IntegerSet Empty = new();

    /// <summary>Positive integers {1, 2, 3, ...}.</summary>
    public static readonly IntegerSet Positive = new(1, int.MaxValue);

    /// <summary>Negative integers {..., -3, -2, -1}.</summary>
    public static readonly IntegerSet Negative = new(int.MinValue, -1);

    /// <summary>Whole numbers {0, 1, 2, ...}.</summary>
    public static readonly IntegerSet Whole = new(0, int.MaxValue);
}
