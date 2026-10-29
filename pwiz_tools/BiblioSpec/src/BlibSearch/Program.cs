// Port of pwiz_tools/BiblioSpec/src/BlibSearch.cpp.
//
// Main entry point for the BlibSearch CLI. Strips the optional `-e <expected>` arg (test
// harness convention, same as BlibBuild) then constructs a BlibSearch, parses args, runs
// the search, and translates exceptions into exit codes.

using Pwiz.Tools.BiblioSpec;

// NOTE: the root namespace here is BlibSearchExe (not BlibSearch) because the BiblioSpec
// library already defines a class called BlibSearch; a namespace named BlibSearch would
// shadow / collide with that type (CS0435 / CS0118).
namespace Pwiz.Tools.BiblioSpec.BlibSearchExe;

/// <summary>
/// BlibSearch CLI entry point. cpp parity: BlibSearch.cpp:75 main().
/// </summary>
public static class Program
{
    /// <summary>
    /// Exit codes mirror the cpp tool:
    /// <list type="bullet">
    /// <item>0 — success (or expected error matched, or usage requested).</item>
    /// <item>1 — BlibException or other unhandled exception.</item>
    /// </list>
    /// </summary>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // For BlibSearch, `--out=PATH` is the report-file path, NOT another library positional
        // (cpp's Jamfile uses `@<path>` here, distinct from build/filter's `=`). Rewrite to
        // `--report-file=PATH` BEFORE CliPreproc.Strip — otherwise Strip would append PATH as
        // a trailing positional and BlibSearch would treat it as an extra .blib library.
        var prepped = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            prepped[i] = args[i].StartsWith("--out=", StringComparison.Ordinal)
                ? string.Concat("--report-file=", args[i].AsSpan("--out=".Length))
                : args[i];
        }

        // Shared argv preprocessing: -e capture, --out=PATH rewrite, --unicode strip.
        // See CliPreproc for cpp-parity refs.
        var (argv, expectedError) = CliPreproc.Strip(prepped);

        // No args (or just help) → print usage + exit 0.
        if (argv.Length == 0)
        {
            BlibSearch.Usage();
            return 0;
        }

        var foundExpectedError = false;

        try
        {
            using var search = new BlibSearch();
            if (!search.ParseArgs(argv))
            {
                BlibSearch.Usage();
                return 0;
            }
            search.Run();
            Verbosity.CloseLogfile();
            return 0;
        }
        catch (BlibException ex)
        {
            if (!ex.AlreadyLogged)
            {
                WriteErrorLines(ex.Message);
            }
            if (!string.IsNullOrEmpty(expectedError) && ex.Message.Contains(expectedError, StringComparison.Ordinal))
            {
                foundExpectedError = true;
            }
        }
        catch (Exception ex)
        {
            WriteErrorLines(ex.Message);
            if (!string.IsNullOrEmpty(expectedError) && ex.Message.Contains(expectedError, StringComparison.Ordinal))
            {
                foundExpectedError = true;
            }
        }

        Verbosity.CloseLogfile();

        if (foundExpectedError) return 0;

        if (!string.IsNullOrEmpty(expectedError))
        {
            Console.Error.WriteLine($"FAILED: This negative test expected an error containing \"{expectedError}\"");
        }
        return 1;
    }

    /// <summary>cpp <c>WriteErrorLines</c> at BlibBuild.cpp:78 — split a multi-line message,
    /// prefix each line with <c>ERROR:</c>.</summary>
    private static void WriteErrorLines(string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        using var reader = new StringReader(s);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            Console.Error.WriteLine("ERROR: " + line);
        }
    }
}
