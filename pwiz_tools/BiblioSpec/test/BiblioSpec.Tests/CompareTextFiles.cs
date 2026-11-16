using System.Globalization;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Line-by-line text comparison with skip-line + numeric-tolerance support.
/// Managed port of cpp <c>tests/CompareTextFiles.cpp</c> and the
/// <c>linesMatch</c> helper from <c>Compare.h</c>.
///
/// <para>Used to validate <see cref="BlibTool.BlibToMs2"/> output (<c>.ms2</c>
/// / <c>.lms2</c>) and <see cref="BlibTool.BlibSearch"/> output (<c>.report</c>)
/// against the checked-in <c>tests/reference/*.check|.report|.lms2</c> golden
/// files. Also used by <c>blib-test-tables</c> tests that dump a <c>.blib</c>
/// into text via <c>BlibBuild -d@</c>.</para>
/// </summary>
public static class CompareTextFiles
{
    /// <summary>
    /// Compare two text files line-by-line and assert they match within the
    /// rules in <paramref name="details"/>. Throws
    /// <see cref="AssertFailedException"/> on mismatch.
    /// </summary>
    /// <param name="observedPath">The file produced by the tool under test.</param>
    /// <param name="expectedPath">The checked-in golden reference file.</param>
    /// <param name="details">Optional skip-lines / tolerance info — usually
    /// loaded via <see cref="CompareDetails.FromFile(string?)"/>. Pass null for
    /// strict exact-match comparison.</param>
    public static void AssertMatch(string observedPath, string expectedPath, CompareDetails? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPath);
        if (!File.Exists(observedPath))
            throw new FileNotFoundException("Observed file does not exist", observedPath);
        if (!File.Exists(expectedPath))
            throw new FileNotFoundException("Expected file does not exist", expectedPath);

        details ??= new CompareDetails();

        using var expectedReader = new StreamReader(expectedPath);
        using var observedReader = new StreamReader(observedPath);

        int lineNum = 1;
        int skipLinesIndex = 0;
        string? expected = expectedReader.ReadLine();
        expected = Trim(expected);

        while (expected is not null)
        {
            string? observed = observedReader.ReadLine();
            if (observed is null)
                Assert.Fail($"The expected file has more lines than observed ({lineNum})");
            observed = Trim(observed);

            // cpp uses an in-order index into compareDetails.skipLines_, advancing only
            // when the *current* expected line contains the *current* skip key. Mirror
            // that exactly — it's why the order in skip-lines matters.
            if (skipLinesIndex < details.SkipLines.Count
                && expected!.Contains(details.SkipLines[skipLinesIndex], StringComparison.Ordinal))
            {
                lineNum++;
                expected = Trim(expectedReader.ReadLine());
                skipLinesIndex++;
                continue;
            }

            if (!LinesMatch(expected!, observed!, details))
            {
                Assert.Fail(
                    $"Line {lineNum} differs.\nexpected: {expected}\nobserved: {observed}");
            }
            lineNum++;
            expected = Trim(expectedReader.ReadLine());
        }

        // Extra-content check on the observed side.
        if (observedReader.ReadLine() is not null)
            Assert.Fail($"The observed file has more lines than the expected ({lineNum})");
    }

    /// <summary>
    /// Match a single (expected, observed) pair using the rules described on
    /// <see cref="CompareDetails"/>. Public for harness reuse — also feeds the
    /// SQLite-content comparator's line check.
    /// </summary>
    public static bool LinesMatch(string expectedRaw, string observedRaw, CompareDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        string expected = Trim(expectedRaw) ?? string.Empty;
        string observed = Trim(observedRaw) ?? string.Empty;

        // Both empty → match. Only one empty → mismatch.
        if (expected.Length == 0 && observed.Length == 0) return true;
        if (expected.Length == 0 || observed.Length == 0) return false;

        // cpp's "observed.compare(0, length, expected) == 0" — prefix match where
        // observed.length() bounds the compare. Treat as full equality with the
        // common-prefix tolerance that gives us.
        if (observed.Length <= expected.Length && expected.StartsWith(observed, StringComparison.Ordinal))
            return true;
        if (string.Equals(expected, observed, StringComparison.Ordinal))
            return true;

        // Skip-line key-phrase override: if any skip key appears in the expected
        // line, treat it as a match.
        foreach (var skip in details.SkipLines)
        {
            if (expected.Contains(skip, StringComparison.Ordinal))
                return true;
        }

        // Numeric-tolerance check: a single column (FieldIdx) or every numeric column.
        if (details.FieldIdx == CompareDetails.NoToleranceFieldIdx)
            return false; // no tolerance configured and exact match already failed.

        int startRange = details.AllowToleranceAllDoubles ? 0 : details.FieldIdx;
        int endRange = details.AllowToleranceAllDoubles
            ? expected.Count(c => c == '\t')
            : details.FieldIdx;

        string expectedRemaining = expected;
        string observedRemaining = observed;

        for (int fieldIdx = endRange; fieldIdx >= startRange; fieldIdx--)
        {
            double expectedField = GetField(fieldIdx, expected, out int expStart, out int expEnd, out string parsedExpected);
            double observedField = GetField(fieldIdx, observed, out int obsStart, out int obsEnd, out string parsedObserved);

            if (double.IsNaN(expectedField) || double.IsNaN(observedField))
            {
                if (!string.Equals(parsedExpected, parsedObserved, StringComparison.Ordinal))
                    return false;
                continue;
            }

            double diff = Math.Abs(expectedField - observedField);
            if (diff > details.Delta)
                return false;

            if (expStart >= 0 && expEnd > expStart && expEnd <= expectedRemaining.Length)
                expectedRemaining = expectedRemaining.Remove(expStart, expEnd - expStart);
            if (obsStart >= 0 && obsEnd > obsStart && obsEnd <= observedRemaining.Length)
                observedRemaining = observedRemaining.Remove(obsStart, obsEnd - obsStart);
        }

        // Cpp checks the leftover strings agree (after the tolerated numeric
        // columns have been excised). Replicate.
        if (observedRemaining.Length == 0) return true;
        return observedRemaining.Length <= expectedRemaining.Length
            && expectedRemaining.StartsWith(observedRemaining, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pull the <paramref name="fieldIdx"/>-th tab-separated field from
    /// <paramref name="line"/> and try to parse it as a double. Out-params
    /// give the substring's bounds for later removal.
    /// </summary>
    private static double GetField(int fieldIdx, string line, out int finalStart, out int finalEnd, out string field)
    {
        int start = line.IndexOf('\t', StringComparison.Ordinal);
        int counter = 1;
        while (counter < fieldIdx && start != -1)
        {
            start = line.IndexOf('\t', start + 1);
            counter++;
        }
        int end = start < 0 ? -1 : line.IndexOf('\t', start + 1);
        // fieldStart = start+1 means we skip the leading \t. cpp does the same.
        int fieldStart = start + 1;
        int fieldEnd = end < 0 ? line.Length : end;
        field = line.Substring(fieldStart, fieldEnd - fieldStart);
        finalStart = fieldStart;
        finalEnd = end; // matches cpp: -1 if no trailing tab, else position of trailing tab

        // cpp falls back to NaN when strtod consumes nothing.
        if (!double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            return double.NaN;
        return result;
    }

    private static string? Trim(string? raw) => raw?.TrimEnd('\r');
}
