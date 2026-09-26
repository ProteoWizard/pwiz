using System.Globalization;
using Pwiz.Data.Common.Proteome;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Proteome;

/// <summary>
/// Command-line-style factory for stacking <see cref="ProteinList"/> wrappers onto a
/// <see cref="ProteomeData"/>. Port of cpp's <c>pwiz::analysis::ProteinListFactory</c>.
/// </summary>
/// <remarks>
/// Each <c>Wrap</c> call takes a single string of the form <c>"command arg…"</c> and
/// replaces <see cref="ProteomeData.ProteinList"/> with the corresponding wrapper.
/// Supported commands:
/// <list type="bullet">
///   <item><c>index &lt;int-set&gt;</c> — keep proteins whose ordinal is in the set
///         (uses <see cref="IntegerSet"/>'s parser, e.g. <c>"[3,5] 7 9"</c>).</item>
///   <item><c>id &lt;filepath-or-semicolon-list&gt;</c> — keep proteins whose id is in
///         the list. The argument is either a filepath whose lines are ids or a
///         semicolon-delimited inline list.</item>
///   <item><c>decoyGenerator &lt;mode&gt; &lt;prefix&gt;</c> — append decoys. mode is
///         <c>reverse</c> or <c>shuffle[=seed]</c>.</item>
/// </list>
/// Unknown commands are warned to stderr and ignored (cpp parity).
/// </remarks>
public static class ProteinListFactory
{
    /// <summary>Applies <paramref name="wrapper"/> to <paramref name="pd"/>. Unknown
    /// commands log a warning to <see cref="Console.Error"/> and leave the list
    /// untouched.</summary>
    public static void Wrap(ProteomeData pd, string wrapper)
    {
        ArgumentNullException.ThrowIfNull(pd);
        ArgumentNullException.ThrowIfNull(wrapper);
        if (pd.ProteinList is null)
            throw new InvalidOperationException("[ProteinListFactory.Wrap] ProteomeData has no ProteinList.");

        // Split "command arg…" once; arg may contain whitespace and is passed through.
        int sp = wrapper.IndexOf(' ');
        string command = sp < 0 ? wrapper : wrapper[..sp];
        string arg = sp < 0 ? string.Empty : wrapper[(sp + 1)..];

        ProteinList? filter = command switch
        {
            "index"          => CreateIndexFilter(pd, arg),
            "id"             => CreateIdFilter(pd, arg),
            "decoyGenerator" => CreateDecoyGenerator(pd, arg),
            _ => null,
        };

        if (filter is null)
        {
            // Unknown command (or known command that returned null) — match cpp's
            // soft-fail behavior: warn and leave the list untouched.
            Console.Error.WriteLine($"[ProteinListFactory] Ignoring wrapper: {wrapper}");
            return;
        }

        pd.ProteinList = filter;
    }

    /// <summary>Applies each wrapper string in order.</summary>
    public static void Wrap(ProteomeData pd, IEnumerable<string> wrappers)
    {
        ArgumentNullException.ThrowIfNull(wrappers);
        foreach (var w in wrappers) Wrap(pd, w);
    }

    /// <summary>User-facing usage text.</summary>
    public static string Usage()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Filter options:");
        sb.AppendLine();
        sb.AppendLine("index int_set");
        sb.AppendLine("id filepath to a line-by-line list OR semicolon-delimited list of protein ids (unique accession strings)");
        sb.AppendLine("decoyGenerator <reverse|shuffle[=random seed]> <decoy prefix>");
        sb.AppendLine();
        sb.AppendLine("'int_set' means that a set of integers must be specified, as a list of intervals of the form [a,b] or a[-][b]");
        sb.AppendLine();
        return sb.ToString();
    }

    private static ProteinList_Filter CreateIndexFilter(ProteomeData pd, string arg)
    {
        var indexSet = new IntegerSet();
        indexSet.Parse(arg);
        return new ProteinList_Filter(pd.ProteinList!, new ProteinFilterPredicate_IndexSet(indexSet));
    }

    private static ProteinList_Filter CreateIdFilter(ProteomeData pd, string arg)
    {
        // cpp accepts either a filepath (lines = ids) or a semicolon-delimited inline list.
        // Match that: if the unsplit arg is an existing filepath, read it; otherwise treat
        // arg as the inline list and split on ';'.
        IEnumerable<string> ids;
        if (!arg.Contains(';') && File.Exists(arg))
        {
            ids = File.ReadAllLines(arg)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0);
        }
        else
        {
            ids = arg.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        return new ProteinList_Filter(pd.ProteinList!, new ProteinFilterPredicate_IdSet(ids));
    }

    private static ProteinList_DecoyGenerator CreateDecoyGenerator(ProteomeData pd, string arg)
    {
        // arg: "<mode> <prefix>"; mode is "reverse" or "shuffle[=seed]".
        int sp = arg.IndexOf(' ');
        string mode = sp < 0 ? arg : arg[..sp];
        string prefix = sp < 0 ? string.Empty : arg[(sp + 1)..].Trim();

        if (prefix.Length == 0)
            throw new ArgumentException("[ProteinListFactory.decoyGenerator] no decoy prefix provided");

        IDecoyGeneratorPredicate predicate;
        if (string.Equals(mode, "reverse", StringComparison.OrdinalIgnoreCase))
        {
            predicate = new DecoyGeneratorPredicate_Reversed(prefix);
        }
        else if (mode.StartsWith("shuffle", StringComparison.OrdinalIgnoreCase))
        {
            // "shuffle" or "shuffle=<seed>"
            int seed = 0;
            int eq = mode.IndexOf('=');
            if (eq >= 0 && !int.TryParse(mode[(eq + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                seed = 0;
            predicate = new DecoyGeneratorPredicate_Shuffled(prefix, seed);
        }
        else
        {
            throw new ArgumentException(
                $"[ProteinListFactory.decoyGenerator] invalid decoy mode '{mode}' (expected 'reverse' or 'shuffle')");
        }

        return new ProteinList_DecoyGenerator(pd.ProteinList!, predicate);
    }
}
