using System.Globalization;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Tolerance + skip-line metadata loaded from a <c>.skip-lines</c> file. Mirrors
/// the cpp <c>CompareDetails</c> struct in <c>Compare.h</c>.
///
/// <para>A skip-lines file is optional. When present its first line may be
/// <c>CompareDetails: &lt;fieldIdx&gt; &lt;delta&gt;</c>. <c>fieldIdx == -1</c>
/// applies the delta to every tab-separated field that parses as a double;
/// any other index applies it only to that one zero-based column. All remaining
/// non-empty lines are key-phrases — if any of them appears as a substring of an
/// expected line, that line is treated as matching regardless of the observed
/// content.</para>
/// </summary>
public sealed class CompareDetails
{
    /// <summary>
    /// Default sentinel — no tolerance applied. cpp uses -999 to encode "no
    /// CompareDetails: header was present"; the value of the sentinel itself
    /// doesn't matter, only that it isn't -1 (the "all doubles" marker) or a
    /// non-negative real column index.
    /// </summary>
    public const int NoToleranceFieldIdx = -999;

    /// <summary>Zero-based column to apply <see cref="Delta"/> to, or -1 to apply
    /// the tolerance to every tab-separated numeric column, or
    /// <see cref="NoToleranceFieldIdx"/> for "no tolerance at all".</summary>
    public int FieldIdx { get; set; } = NoToleranceFieldIdx;

    /// <summary>Absolute tolerance for numeric comparison.</summary>
    public double Delta { get; set; }

    /// <summary>Key-phrases that, when present in the expected line, mark the
    /// line as "skip" — no comparison performed.</summary>
    public List<string> SkipLines { get; } = new();

    /// <summary>True iff <see cref="Delta"/> should apply to every numeric column.</summary>
    public bool AllowToleranceAllDoubles => FieldIdx == -1;

    /// <summary>
    /// Parse a <c>.skip-lines</c> file. Missing file is non-fatal (returns
    /// defaults) — matches cpp behavior, which prints a warning and continues.
    /// </summary>
    public static CompareDetails FromFile(string? skipLinesPath)
    {
        var details = new CompareDetails();
        if (string.IsNullOrWhiteSpace(skipLinesPath) || !File.Exists(skipLinesPath))
            return details;

        using var reader = new StreamReader(skipLinesPath);
        string? line = reader.ReadLine();
        if (line is null) return details;

        // First line may be "CompareDetails: <fieldIdx> <delta>"
        if (line.Contains("CompareDetails:", StringComparison.Ordinal))
        {
            // cpp parses with stringstream which is whitespace-delimited.
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            // parts[0] == "CompareDetails:", parts[1] == fieldIdx, parts[2] == delta
            if (parts.Length >= 3
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                details.FieldIdx = idx;
                details.Delta = d;
            }
            line = reader.ReadLine();
        }

        while (line is not null)
        {
            // cpp strips a trailing \r and any empty line.
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length > 0)
                details.SkipLines.Add(trimmed);
            line = reader.ReadLine();
        }
        return details;
    }
}
