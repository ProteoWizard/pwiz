// Port of pwiz_tools/BiblioSpec/src/DotProduct.{h,cpp}

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Computes the dot-product score (also known as the cosine angle) between two processed
/// peak lists and writes the result onto a <see cref="Match"/>.
/// </summary>
/// <remarks>
/// Port of <c>BiblioSpec::DotProduct</c>. The cpp class only exposes a single static
/// <c>compare(Match&amp;)</c> method.
/// </remarks>
public static class DotProduct
{
    /// <summary>
    /// Compute the dot-product score between <paramref name="match"/>'s experimental and
    /// reference processed peak lists; assigns <see cref="MatchScoreType.Dotp"/> and
    /// <see cref="MatchScoreType.MatchedIons"/>.
    /// </summary>
    /// <remarks>
    /// <para>cpp DotProduct.cpp:37 <c>compare</c> + DotProduct.cpp:55 <c>getAngle</c>.</para>
    /// <para>Scoring formula (cpp parity, DotProduct.cpp:55-100):
    /// <code>
    /// // Walk both lists like a merge-sort:
    /// // - if exp.mz == ref.mz: contribute to expIntSqSum, refIntSqSum, expRefIntSum; matched++
    /// // - if exp.mz &lt; ref.mz : contribute exp.intensity^2 to expIntSqSum, advance exp
    /// // - if ref.mz &lt; exp.mz : contribute ref.intensity^2 to refIntSqSum, advance ref
    /// // Drain remaining peaks: add intensity^2 to their respective sum.
    /// // angle = expRefIntSum / sqrt(expIntSqSum * refIntSqSum)
    /// // if angle is NaN (both sums zero), angle = 0.
    /// </code></para>
    /// <para>cpp uses <c>double</c> for the multiplication explicitly (cpp DotProduct.cpp:93)
    /// to avoid float-overflow when intensities are large.</para>
    /// </remarks>
    public static void Compare(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (match.ExpSpec is null || match.RefSpec is null)
            throw new BlibException(false, "DotProduct.Compare requires both ExpSpec and RefSpec.");

        var exp = match.ExpSpec.ProcessedPeaks;
        var refp = match.RefSpec.ProcessedPeaks;

        // cpp parity: DotProduct.cpp:61-65 — init three running sums + matched-ion counter.
        double expIntSqSum = 0;
        double refIntSqSum = 0;
        double expRefIntSum = 0;
        var matchedIons = 0;

        var i = 0;
        var j = 0;
        // cpp parity: DotProduct.cpp:66-81 — merge-sort walk.
        while (i < exp.Count && j < refp.Count)
        {
            var e = exp[i];
            var r = refp[j];
            if (e.Mz == r.Mz)
            {
                expIntSqSum += (double)e.Intensity * e.Intensity;
                refIntSqSum += (double)r.Intensity * r.Intensity;
                expRefIntSum += (double)e.Intensity * r.Intensity;
                matchedIons++;
                i++;
                j++;
            }
            else if (e.Mz < r.Mz)
            {
                // cpp DotProduct.cpp:74 — pow((curExp->intensity), 2).
                expIntSqSum += Math.Pow(e.Intensity, 2);
                i++;
            }
            else
            {
                refIntSqSum += Math.Pow(r.Intensity, 2);
                j++;
            }
        }

        // cpp parity: DotProduct.cpp:84-91 — drain remaining peaks.
        while (i < exp.Count)
        {
            expIntSqSum += Math.Pow(exp[i].Intensity, 2);
            i++;
        }
        while (j < refp.Count)
        {
            refIntSqSum += Math.Pow(refp[j].Intensity, 2);
            j++;
        }

        // cpp DotProduct.cpp:95 — must keep the multiplication in double to avoid overflow.
        var angle = expRefIntSum / Math.Sqrt(expIntSqSum * refIntSqSum);
        if (double.IsNaN(angle)) angle = 0;

        match.SetScore(MatchScoreType.Dotp, angle);
        match.SetScore(MatchScoreType.MatchedIons, matchedIons);
    }
}
