// Argv preprocessing shared across all 4 BiblioSpec CLI tools (BlibBuild / BlibFilter /
// BlibSearch / BlibToMs2). cpp's BlibBuild.cpp does the equivalent inline in main(); the
// other tools' cpp originals rely on boost::program_options which we don't have. Centralising
// here means a new long-form flag is added once, not four times — and ensures the off-by-one
// rule for `-e <expected>` doesn't drift across tools (the Stage A port had BlibSearch and
// BlibToMs2 using a wrong bound).

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Argv preprocessing for BiblioSpec CLI tools. Strips test-harness flags and rewrites
/// long-form options that <see cref="BlibMaker.ParseCommandArgs"/> doesn't understand,
/// returning the remaining argv plus any captured negative-test "expected error" string.
/// </summary>
public static class CliPreproc
{
    /// <summary>
    /// Preprocess <paramref name="args"/> and return (rewritten argv, expectedError).
    /// </summary>
    /// <remarks>
    /// <para>Performs:</para>
    /// <list type="bullet">
    /// <item><c>-e &lt;msg&gt;</c> is stripped; the value is returned as <c>expectedError</c>.
    /// cpp parity: BlibBuild.cpp:108-122 requires the <c>-e</c> arg to precede AT LEAST 2
    /// trailing positionals (typically <c>&lt;input&gt; &lt;library&gt;</c>), so the loop
    /// bound is <c>list.Count - 2</c>. Phase 4 Stage A had BlibSearch and BlibToMs2 using
    /// <c>list.Count - 1</c>, which mis-consumed the trailing positional when -e appeared
    /// second-to-last.</item>
    /// <item><c>--out=PATH</c> is rewritten — the flag is removed and <c>PATH</c> is appended
    /// as the trailing positional, so <see cref="BlibMaker.ParseCommandArgs"/> picks it up as
    /// the library name. cpp uses boost::program_options for this directly.</item>
    /// <item><c>--unicode</c> is dropped — it's a Windows UTF-8 path-mode toggle (cpp calls
    /// <c>enable_utf8_path_operations()</c>) that's a no-op in .NET 8.</item>
    /// </list>
    /// </remarks>
    public static (string[] argv, string expectedError) Strip(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var list = new List<string>(args);
        var expectedError = string.Empty;

        // cpp parity: BlibBuild.cpp:108 — -e must precede at least 2 trailing args.
        for (var idx = 0; idx < list.Count - 2; idx++)
        {
            if (list[idx] != "-e") continue;
            expectedError = list[idx + 1];
            list.RemoveAt(idx); // remove "-e"
            list.RemoveAt(idx); // remove its value
            break;
        }

        // Translate --out=PATH into the trailing positional that BlibMaker treats as the
        // library name. We only handle ONE --out (the cpp tools also only take one).
        for (var idx = 0; idx < list.Count; idx++)
        {
            const string outPrefix = "--out=";
            if (!list[idx].StartsWith(outPrefix, StringComparison.Ordinal)) continue;
            var outPath = list[idx][outPrefix.Length..];
            list.RemoveAt(idx);
            list.Add(outPath);
            break;
        }

        // --unicode is a no-op for .NET 8 paths.
        list.RemoveAll(a => a == "--unicode");

        return (list.ToArray(), expectedError);
    }
}
