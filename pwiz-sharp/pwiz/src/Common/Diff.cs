using System.Text.RegularExpressions;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;

namespace Pwiz.Data.Common.Diff;

/// <summary>
/// Static helpers that calculate set differences between objects in the pwiz data model.
/// Port of pwiz/data::diff_impl and the <c>Diff</c> template.
/// </summary>
/// <remarks>
/// The C++ <c>Diff&lt;T, Config&gt;</c> template maps here to static helper methods that return
/// a <see cref="DiffResult{T}"/>. This surfaces the same "what's in a but not b" / "what's in b but not a"
/// information that pwiz uses to explain mzML mismatches.
/// </remarks>
public static class Diff
{
    /// <summary>Diffs two strings. Non-equal values populate both sides.</summary>
    public static DiffResult<string> Of(string a, string b, DiffConfig? config = null)
        => a == b ? DiffResult<string>.Empty : new DiffResult<string>(a, b);

    /// <summary>
    /// Diffs two ids that may differ only by a trailing version number (e.g. "pwiz_1.2.3" vs "pwiz_1.2.4").
    /// When <see cref="DiffConfig.IgnoreVersions"/> is true, versions are stripped before comparison.
    /// </summary>
    public static DiffResult<string> OfIds(string a, string b, DiffConfig? config = null)
    {
        if (config?.IgnoreVersions == true)
        {
            string sa = StripTrailingVersion(a);
            string sb = StripTrailingVersion(b);
            return sa == sb ? DiffResult<string>.Empty : new DiffResult<string>(a, b);
        }
        return Of(a, b, config);
    }

    private static readonly Regex s_trailingVersion = new(@"[_\-]\d+(\.\d+)+$", RegexOptions.Compiled);

    private static string StripTrailingVersion(string s) =>
        s_trailingVersion.Replace(s ?? string.Empty, string.Empty);

    /// <summary>Diffs two CVID values.</summary>
    public static DiffResult<CVID> Of(CVID a, CVID b)
        => a == b ? DiffResult<CVID>.Empty : new DiffResult<CVID>(a, b);

    /// <summary>Diffs two CVParams.</summary>
    public static DiffResult<CVParam> Of(CVParam a, CVParam b, DiffConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Equals(b)) return DiffResult<CVParam>.Empty;
        return new DiffResult<CVParam>(a, b);
    }

    /// <summary>Diffs two UserParams.</summary>
    public static DiffResult<UserParam> Of(UserParam a, UserParam b, DiffConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Equals(b)) return DiffResult<UserParam>.Empty;
        return new DiffResult<UserParam>(a, b);
    }

    /// <summary>Diffs two ParamContainers.</summary>
    public static DiffResult<ParamContainer> Of(ParamContainer a, ParamContainer b, DiffConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Equals(b)) return DiffResult<ParamContainer>.Empty;

        var a_b = new ParamContainer();
        var b_a = new ParamContainer();

        foreach (var p in a.CVParams) if (!b.CVParams.Contains(p)) a_b.CVParams.Add(p);
        foreach (var p in b.CVParams) if (!a.CVParams.Contains(p)) b_a.CVParams.Add(p);
        foreach (var u in a.UserParams) if (!b.UserParams.Contains(u)) a_b.UserParams.Add(u);
        foreach (var u in b.UserParams) if (!a.UserParams.Contains(u)) b_a.UserParams.Add(u);

        return new DiffResult<ParamContainer>(a_b, b_a);
    }

    /// <summary>
    /// Compares two floating-point values with an absolute or relative tolerance. Matches
    /// pwiz C++ <c>Diff::operator()(double, double)</c>: a diff is ignored when
    /// <c>|a-b| &lt;= config.Precision * max(1, max(|a|, |b|))</c>. Using max(1, ...) keeps
    /// sub-unit values from being compared to an unreasonably tiny absolute window.
    /// </summary>
    public static bool FloatingPointEqual(double a, double b, DiffConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (double.IsNaN(a) && double.IsNaN(b)) return true;
        if (double.IsNaN(a) || double.IsNaN(b)) return false;
        double delta = Math.Abs(a - b);
        double scale = Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        return delta <= config.Precision * scale;
    }
}

/// <summary>
/// Result of a diff: left-only and right-only content. When empty, inputs were equal.
/// </summary>
#pragma warning disable CA1000 // exposing Empty on the generic type is idiomatic here (see DiffResult.Empty below).
public readonly struct DiffResult<T> : IEquatable<DiffResult<T>>
{
    /// <summary>Content present in "a" but not "b".</summary>
    public T? AMinusB { get; }

    /// <summary>Content present in "b" but not "a".</summary>
    public T? BMinusA { get; }

    internal DiffResult(T? aMinusB, T? bMinusA)
    {
        AMinusB = aMinusB;
        BMinusA = bMinusA;
    }

    /// <summary>True when both sides are empty (values were equal).</summary>
    public bool IsEmpty => EqualityComparer<T>.Default.Equals(AMinusB, default) &&
                           EqualityComparer<T>.Default.Equals(BMinusA, default);

    /// <summary>Sentinel for equal inputs.</summary>
    public static DiffResult<T> Empty => default;

    /// <inheritdoc/>
    public bool Equals(DiffResult<T> other)
        => EqualityComparer<T>.Default.Equals(AMinusB, other.AMinusB)
           && EqualityComparer<T>.Default.Equals(BMinusA, other.BMinusA);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DiffResult<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(AMinusB, BMinusA);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(DiffResult<T> left, DiffResult<T> right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(DiffResult<T> left, DiffResult<T> right) => !left.Equals(right);
}
