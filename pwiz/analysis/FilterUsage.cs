using System.Text;

namespace Pwiz.Analysis;

/// <summary>
/// Help text for one msconvert filter: its name, an argument synopsis, and a description.
/// Port of the cpp factories' <c>UsageInfo</c> pairs.
/// </summary>
/// <param name="Name">Filter name as users type it, e.g. <c>msLevel</c>.</param>
/// <param name="Arguments">Argument synopsis, e.g. <c>&lt;mslevels&gt;</c>; empty for a filter that takes none.</param>
/// <param name="Details">Description shown by detailed help; may span lines.</param>
public sealed record FilterUsage(string Name, string Arguments, string Details);

/// <summary>
/// Formats the filter help that <see cref="SpectrumListFactory"/> and
/// <see cref="ChromatogramListFactory"/> share, in the layout of cpp's
/// <c>SpectrumListFactory::usage</c> / <c>ChromatogramListFactory::usage</c>.
/// </summary>
internal static class FilterUsageFormatter
{
    private const string IntSetHelp =
        "Many filters take 'int_set' arguments.  An 'int_set' is a list of intervals of the form [a,b] or a[-][b].\n" +
        "For example '[0,3]' and '0-3' both mean 'the set of integers from 0 to 3 inclusive'.\n" +
        "'1-' means 'the set of integers from 1 to the largest allowable number'.  \n" +
        "'9' is also an integer set, equivalent to '[9,9]'.\n" +
        "'[0,2] 5-7' is the set '0 1 2 5 6 7'. \n\n";

    /// <summary>
    /// The heading, then each filter's name and arguments; with <paramref name="detailedHelp"/>,
    /// also <paramref name="detailedNote"/>, the int_set explanation, and each filter's details.
    /// </summary>
    internal static string Format(string heading, string detailedNote, IEnumerable<FilterUsage> filters,
                                  bool detailedHelp, string? moreHelpPrompt, int maxLineLength)
    {
        var sb = new StringBuilder();
        sb.Append('\n').Append(heading).Append('\n').Append('=', heading.Length).Append('\n');
        if (!detailedHelp)
        {
            if (moreHelpPrompt is not null)
                sb.Append(moreHelpPrompt).Append('\n');
        }
        else
        {
            sb.Append('\n').Append(detailedNote).Append("\n\n").Append(IntSetHelp);
        }

        foreach (var filter in filters)
        {
            sb.Append(filter.Name).Append(' ').Append(filter.Arguments).Append('\n');
            if (detailedHelp)
                sb.Append(filter.Details).Append("\n\n");
        }
        sb.Append('\n');
        return WordWrap(sb.ToString(), maxLineLength);
    }

    /// <summary>
    /// Detailed help for the one filter named <paramref name="filterName"/> (exact case, as cpp
    /// matched it), or, for an unknown name, the list of names that are known.
    /// </summary>
    internal static string FormatSingle(IEnumerable<FilterUsage> filters, string filterName)
    {
        var sb = new StringBuilder();
        foreach (var filter in filters)
        {
            if (filter.Name == filterName)
                return sb.Append(filter.Name).Append(' ').Append(filter.Arguments).Append('\n')
                         .Append(filter.Details).Append('\n').ToString();
        }

        sb.Append("Invalid filter name: ").Append(filterName).Append("\n\n").Append("Supported filters are:\n");
        foreach (var filter in filters)
            sb.Append(filter.Name).Append('\n');
        return sb.ToString();
    }

    /// <summary>
    /// Breaks each line longer than <paramref name="maxLineLength"/> at its last space that
    /// fits, or at its first space when no space fits; a line with no spaces stays long.
    /// <paramref name="maxLineLength"/> of zero or less means no wrapping.
    /// </summary>
    private static string WordWrap(string text, int maxLineLength)
    {
        if (maxLineLength <= 0)
            return text;

        var chars = text.ToCharArray();
        int lineStart = 0;
        while (lineStart < chars.Length)
        {
            int lineEnd = Array.IndexOf(chars, '\n', lineStart);
            if (lineEnd < 0)
                lineEnd = chars.Length;
            if (lineEnd - lineStart <= maxLineLength)
            {
                lineStart = lineEnd + 1;
                continue;
            }

            int breakAt = Array.LastIndexOf(chars, ' ', lineStart + maxLineLength, maxLineLength + 1);
            if (breakAt <= lineStart)
                breakAt = Array.IndexOf(chars, ' ', lineStart + maxLineLength, lineEnd - lineStart - maxLineLength);
            if (breakAt < 0)
            {
                lineStart = lineEnd + 1;
                continue;
            }
            chars[breakAt] = '\n';
            lineStart = breakAt + 1;
        }
        return new string(chars);
    }
}
