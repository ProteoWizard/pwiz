using System.Text;
using System.Text.RegularExpressions;

namespace Pwiz.Util.Tests;

/// <summary>
/// Project-wide source-text rules. Every rule is checked in a single walk of
/// <c>pwiz/</c> + <c>Tools/</c>; hits aggregate by rule and surface together in one
/// failure message, so adding a new rule never costs another tree walk and one
/// rule's hit doesn't suppress reporting on another.
/// </summary>
/// <remarks>
/// Add a rule by appending to <see cref="Rules"/>. Each rule supplies its own regex
/// and the human-readable preamble that goes above its hit list. All rules skip
/// single-line comments (<c>//</c>) and multi-line-comment continuations (<c>*</c>);
/// add per-rule overrides if a future rule needs to match comment bodies.
/// </remarks>
[TestClass]
public class CodeInspectionTests
{
    /// <summary>One source-text rule. <see cref="Pattern"/> runs against every line
    /// that survives the comment filter; matches accumulate under
    /// <see cref="Name"/> in the failure report.</summary>
    private sealed record InspectionRule(string Name, string FailureSummary, Regex Pattern);

    private static readonly InspectionRule[] Rules =
    {
        new(
            Name: "IsRecordMode_NeverCommittedTrue",
            FailureSummary: "Vendor harness record-mode flag is committed in true state. "
                + "Flip back to false; in true state CI runs the harness in record mode and "
                + "skips the diff check.",
            // Word-boundary on either side keeps `MyIsRecordMode` / `IsRecordModerate` from matching.
            Pattern: new Regex(@"\bIsRecordMode\s*=\s*true\b", RegexOptions.Compiled)),
    };

    [TestMethod]
    public void NoCommittedSourceTextViolations()
    {
        string root = FindPwizSharpRoot()
            ?? throw new InvalidOperationException(
                "could not locate pwiz-sharp source root from " + AppContext.BaseDirectory);

        // hitsByRule[ruleName] = list of "<relative path>:<lineno>: <trimmed line>" entries
        var hitsByRule = new Dictionary<string, List<string>>();

        foreach (var file in EnumerateSourceFiles(root))
        {
            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                // Skip comment lines once per line so doc strings that intentionally mention
                // a banned pattern (e.g., explaining the convention) don't false-positive on
                // any rule. Multi-line `/* ... */` blocks aren't handled — none of the doc
                // snippets in this codebase put a banned literal inside one.
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith('*'))
                    continue;

                foreach (var rule in Rules)
                {
                    if (!rule.Pattern.IsMatch(line)) continue;
                    if (!hitsByRule.TryGetValue(rule.Name, out var list))
                        hitsByRule[rule.Name] = list = new List<string>();
                    list.Add(MakeRelative(file, root) + ":" + lineNumber + ": " + line.Trim());
                }
            }
        }

        if (hitsByRule.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Code inspection found violations in {hitsByRule.Count} rule(s):");
        // Iterate Rules (not the dictionary) so failure ordering matches rule declaration —
        // newly-added rules slot in at a predictable position in the report.
        foreach (var rule in Rules)
        {
            if (!hitsByRule.TryGetValue(rule.Name, out var list)) continue;
            sb.AppendLine();
            sb.AppendLine($"[{rule.Name}] {rule.FailureSummary}");
            foreach (var hit in list)
                sb.AppendLine("  " + hit);
        }
        Assert.Fail(sb.ToString());
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        // Walk pwiz/ (core) + Tools/ (BiblioSpec, MsConvert, ...) but skip build artifacts.
        // EnumerateFiles is lazy and we bail on any obj/bin segment so we don't read into
        // nuget package caches.
        foreach (var top in new[] { "pwiz", "Tools" })
        {
            string topDir = Path.Combine(root, top);
            if (!Directory.Exists(topDir)) continue;
            foreach (var f in Directory.EnumerateFiles(topDir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                               StringComparison.OrdinalIgnoreCase))
                    continue;
                if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                               StringComparison.OrdinalIgnoreCase))
                    continue;
                yield return f;
            }
        }
    }

    private static string MakeRelative(string path, string root)
    {
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? path[(root.Length + 1)..].Replace('\\', '/')
            : path;
    }

    /// <summary>Locate the pwiz-sharp/ root via the shared anchor in TestHarness.
    /// Returns null on failure rather than throwing — keeps the caller's existing
    /// Inconclusive-skip path intact when the test runs from an unexpected location.</summary>
    private static string? FindPwizSharpRoot()
    {
        try { return Pwiz.TestHarness.PwizSharpPaths.Root; }
        catch { return null; }
    }
}
