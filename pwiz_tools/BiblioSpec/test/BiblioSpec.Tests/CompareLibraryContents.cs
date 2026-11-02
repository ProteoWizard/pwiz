using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Semantic comparison of two <c>.blib</c> SQLite libraries against a
/// checked-in text-dump reference. Managed port of cpp
/// <c>tests/CompareLibraryContents.cpp</c>.
///
/// <para>The reference <c>.check</c> file is a plain text dump produced by a
/// small set of <c>SELECT</c>s (<c>LibInfo</c>, <c>Modifications</c>,
/// <c>RefSpectra</c>, <c>SpectrumSourceFiles</c>, <c>ScoreTypes</c>,
/// <c>RetentionTimes</c>). To compare we re-run those <c>SELECT</c>s against
/// the observed library, apply the same regex post-processing (trim noisy
/// decimal digits, scrub developer-specific paths, optionally normalize
/// backslashes to forward slashes), and feed the resulting line stream into
/// the same <see cref="CompareTextFiles.LinesMatch"/> machinery used for plain
/// text outputs.</para>
/// </summary>
public static class CompareLibraryContents
{
    /// <summary>
    /// Compare the observed <c>.blib</c> against the checked-in reference text
    /// file. Throws <see cref="AssertFailedException"/> on mismatch.
    /// </summary>
    /// <param name="observedBlibPath">Path to the SQLite library produced by the tool.</param>
    /// <param name="expectedCheckPath">Path to the <c>.check</c> reference file
    /// (line-by-line text dump of the queries below).</param>
    /// <param name="details">Optional skip-lines / tolerance info (loaded from a
    /// <c>.skip-lines</c> file). Pass null for strict comparison.</param>
    public static void AssertMatch(string observedBlibPath, string expectedCheckPath, CompareDetails? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(observedBlibPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCheckPath);
        if (!File.Exists(observedBlibPath))
            throw new FileNotFoundException("Observed .blib does not exist", observedBlibPath);
        if (!File.Exists(expectedCheckPath))
            throw new FileNotFoundException("Expected .check does not exist", expectedCheckPath);

        details ??= new CompareDetails();
        var observedLines = ExtractLines(observedBlibPath);

        int lineNum = 0;
        bool inScoreTypesSection = false;
        bool inRefSpectraIdSectionExpected = false;
        bool inRefSpectraIdSectionObserved = false;
        const string refSpectraHint = "RefSpectraID\tRedundantRefSpectraID\t";

        using var expectedReader = new StreamReader(expectedCheckPath);
        string? expected = expectedReader.ReadLine();

        while (expected is not null)
        {
            if (lineNum >= observedLines.Count)
            {
                WriteObserved(observedLines, observedBlibPath, expectedCheckPath);
                Assert.Fail(
                    $"The expected input has more lines than what was observed ({observedLines.Count})");
            }

            string observed = observedLines[lineNum];

            // Cpp may need to fast-forward observed lines past a section the
            // expected ones haven't reached yet (the RefSpectraID section).
            while (inRefSpectraIdSectionExpected && !inRefSpectraIdSectionObserved)
            {
                inRefSpectraIdSectionObserved = observed.StartsWith(refSpectraHint, StringComparison.Ordinal);
                if (inRefSpectraIdSectionObserved)
                    break;
                lineNum++;
                if (lineNum >= observedLines.Count)
                    break;
                observed = observedLines[lineNum];
            }

            if (!CompareTextFiles.LinesMatch(expected, observed, details))
            {
                WriteObserved(observedLines, observedBlibPath, expectedCheckPath);
                Assert.Fail(
                    $"Line {lineNum + 1} differs.\nexpected: {expected}\nobserved: {observed}");
            }

            expected = expectedReader.ReadLine();
            lineNum++;
            if (expected is not null)
            {
                if (expected.Contains("probabilityType", StringComparison.Ordinal))
                    inScoreTypesSection = true;
                if (expected.StartsWith(refSpectraHint, StringComparison.Ordinal))
                    inRefSpectraIdSectionExpected = true;
            }
        }

        if (lineNum < observedLines.Count && !inScoreTypesSection)
        {
            WriteObserved(observedLines, observedBlibPath, expectedCheckPath);
            Assert.Fail(
                $"Observed output has more lines ({observedLines.Count}) than expected ({lineNum}) " +
                $"starting at line \"{observedLines[lineNum]}\"");
        }
    }

    /// <summary>
    /// Open the .blib, run the same battery of <c>SELECT</c>s the cpp comparator
    /// uses, apply the same per-row post-processing, and return one row per line.
    /// </summary>
    internal static List<string> ExtractLines(string libPath)
    {
        var output = new List<string>();
        var csb = new SQLiteConnectionStringBuilder
        {
            DataSource = libPath,
            ReadOnly = true,
        };
        using var conn = new SQLiteConnection(csb.ToString());
        conn.Open();

        // The number-trimming regex the cpp comparator applies to RefSpectra and
        // RetentionTimes — limits "small" doubles to 8 decimals (4 integer digits) and
        // "large" doubles to 2 decimals (5+ integer digits). Pre-compile once.
        var trimNumberRegex = new Regex(
            @"((?:\d{5,}\.\d{0,2})|(?:\d{1,4}\.\d{0,8}))\d*",
            RegexOptions.Compiled);

        // SpectrumSourceFiles paths differ between machines; cpp scrubs the
        // path prefix before the /BiblioSpec/tests/ marker. Use the same shape.
        var sourceFilePathRegex = new Regex(
            @"\t[^\t]*/BiblioSpec/tests",
            RegexOptions.Compiled);

        AppendQuery(conn, output,
            "SELECT libLSID, numSpecs, majorVersion, minorVersion FROM LibInfo",
            row => row, swapSlash: false);

        // Modifications table: cpp's reference .check files apply the same number-trim shape
        // (truncate >8 decimal digits for "small" doubles, >2 for "large"). Applying here too
        // keeps Modifications.mass byte-exact even when the underlying mass calc has the float
        // precision tail that .NET's G15 would otherwise expose.
        AppendQuery(conn, output, "select * from Modifications",
            row => trimNumberRegex.Replace(row, "$1"), swapSlash: false);

        AppendQuery(conn, output, "select * from RefSpectra",
            row => trimNumberRegex.Replace(row, "$1"), swapSlash: false);

        AppendQuery(conn, output, "select * from SpectrumSourceFiles",
            row => sourceFilePathRegex.Replace(row, "\t/BiblioSpec/tests"), swapSlash: true);

        AppendQuery(conn, output, "select * from ScoreTypes",
            row => row, swapSlash: false);

        AppendQuery(conn, output, "select * from RetentionTimes",
            row => trimNumberRegex.Replace(row, "$1"), swapSlash: false);

        return output;
    }

    /// <summary>
    /// Run one <c>SELECT</c> and append a header row (column names) plus one
    /// row per result to <paramref name="output"/>. Per-row content goes through
    /// <paramref name="rowTransform"/> after optional backslash normalization.
    /// </summary>
    private static void AppendQuery(
        SQLiteConnection conn,
        List<string> output,
        string sql,
        Func<string, string> rowTransform,
        bool swapSlash)
    {
        SQLiteDataReader? reader = null;
        try
        {
            using var cmd = new SQLiteCommand(sql, conn);
            try
            {
                reader = cmd.ExecuteReader();
            }
            catch (SQLiteException)
            {
                // cpp behavior: a missing table / failed prepare silently returns
                // without contributing rows. Some legacy .blibs lack some of these
                // tables (RetentionTimes was added later) — that should match the
                // reference dump which also omits the section.
                return;
            }

            // Column-header row, tab-joined.
            var header = new System.Text.StringBuilder();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (i > 0) header.Append('\t');
                header.Append(reader.GetName(i));
            }
            output.Add(header.ToString());

            while (reader.Read())
            {
                var row = new System.Text.StringBuilder();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) row.Append('\t');
                    // cpp parity: CompareLibraryContents.cpp:94 uses sqlite3_column_text() which
                    // applies SQLite's REAL → TEXT formatting: integral values render as "0.0" /
                    // "62746.0" (with a trailing decimal), non-integral as %!.15g — e.g.
                    // "9.09316666". .NET's Convert.ToString(double) drops the trailing ".0", so we
                    // detect doubles and format with that quirk preserved.
                    string val;
                    if (reader.IsDBNull(i))
                    {
                        val = string.Empty;
                    }
                    else
                    {
                        // SQLite REAL is most commonly returned as double, but vendor-extension
                        // schemas / non-default type mappings may surface float (single) or
                        // decimal. The integral-to-"0.0" formatting quirk must apply uniformly.
                        var raw = reader.GetValue(i);
                        // System.Data.SQLite maps TINYINT to unsigned `byte`, which turns SQLite's
                        // signed-int -1 into 255. cpp's sqlite3_column_text would render "-1". The
                        // only TINYINT column in our schema is SpectrumSourceFiles.workflowType,
                        // which legitimately stores -1 (unknown). Reinterpret as signed.
                        if (raw is byte b) raw = (sbyte)b;
                        double? asDouble = raw switch
                        {
                            double d => d,
                            float f => f,
                            decimal m => (double)m,
                            _ => null,
                        };
                        if (asDouble is { } dv)
                        {
                            if (dv == Math.Truncate(dv) && !double.IsInfinity(dv) && Math.Abs(dv) < 1e15)
                            {
                                val = dv.ToString("0.0", CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                // cpp's %g uses lowercase 'e' for exponent and SQLite's "!.15g"
                                // emits scientific values like "1.0e-14" (decimal point preserved
                                // when mantissa rounds to integer); .NET's G15 drops the ".0",
                                // emitting "1e-14". Normalize both axes.
                                val = dv.ToString("G15", CultureInfo.InvariantCulture).Replace('E', 'e');
                                int eIdx = val.IndexOf('e', StringComparison.Ordinal);
                                if (eIdx > 0 && val.AsSpan(0, eIdx).IndexOf('.') < 0)
                                    val = val.Insert(eIdx, ".0");
                            }
                        }
                        else
                        {
                            val = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
                        }
                    }
                    // cpp marks first column NULL-as-empty (it never is in practice for these
                    // tables), other columns NULL/empty as "N/A".
                    if (i > 0 && val.Length == 0)
                        val = "N/A";
                    row.Append(val);
                }
                string line = row.ToString();
                if (swapSlash)
                    line = line.Replace('\\', '/');
                line = rowTransform(line);
                output.Add(line);
            }
        }
        finally
        {
            reader?.Dispose();
        }
    }

    /// <summary>
    /// When a comparison fails, drop the observed dump beside the reference
    /// (named <c>&lt;blibName&gt;.observed</c>) so the diff is easy to inspect
    /// — same convention the cpp comparator uses.
    /// </summary>
    private static void WriteObserved(IReadOnlyList<string> observedLines, string libPath, string expectedCheckPath)
    {
        // cpp puts the .observed file next to the reference (replacing /output/ with
        // /reference/). We just put it next to the .check so the file appears beside
        // the expected for easy diffing.
        string outPath;
        try
        {
            outPath = expectedCheckPath + ".observed";
        }
        catch (Exception)
        {
            outPath = libPath + ".observed";
        }
        try
        {
            File.WriteAllLines(outPath, observedLines);
        }
        catch (IOException)
        {
            // Don't let a write failure mask the actual comparison failure.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
