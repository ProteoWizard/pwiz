// Port of pwiz_tools/BiblioSpec/src/BlibFilter.cpp main() at BlibFilter.cpp:96-122.
//
// Strips the optional `-e <expected>` arg (used by the cpp test harness to assert that a
// specific error string is emitted), then constructs a BlibFilter, runs ParseCommandArgs /
// Init / BeginTransaction / BuildNonRedundantLib / EndTransaction / Commit, and translates
// exceptions to exit codes.

using Pwiz.Tools.BiblioSpec;
using BlibFilterLib = Pwiz.Tools.BiblioSpec.BlibFilter;

namespace Pwiz.Tools.BiblioSpec.BlibFilterTool;

/// <summary>
/// BlibFilter CLI entry point. cpp parity: BlibFilter.cpp:96 main().
/// </summary>
public static class Program
{
    /// <summary>
    /// Exit codes mirror the cpp tool:
    /// <list type="bullet">
    /// <item>0 — success (or expected error matched).</item>
    /// <item>1 — <see cref="BlibException"/> or filter failed.</item>
    /// <item>2 — other unhandled exception.</item>
    /// </list>
    /// </summary>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Shared argv preprocessing: -e capture, --out=PATH rewrite, --unicode strip.
        var (argv, expectedError) = CliPreproc.Strip(args);

        // Refuse to filter a library in place. cpp BlibFilter doesn't guard this — Init()
        // honors Overwrite=true and deletes the output BEFORE opening the input for read,
        // so `BlibFilter foo.blib foo.blib` would destroy the source. The post-strip
        // positionals are <redundant-library> <filtered-library>.
        if (argv.Length >= 2)
        {
            var redundant = argv[^2];
            var filtered = argv[^1];
            try
            {
                if (string.Equals(Path.GetFullPath(redundant), Path.GetFullPath(filtered),
                    StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine(
                        "ERROR: Input (redundant) and output (filtered) libraries must be different files.");
                    return 1;
                }
            }
            catch
            {
                // GetFullPath can throw on invalid characters; defer that error to Init().
            }
        }

        using var filter = new BlibFilterLib();
        var foundExpectedError = false;

        try
        {
            filter.ParseCommandArgs(argv);
            filter.Init();

            filter.BeginTransaction();
            Verbosity.Debug("About to begin filtering.");
            filter.BuildNonRedundantLib();
            Verbosity.Debug("Finished filtering.");
            filter.EndTransaction();
            filter.Commit();

            Verbosity.CloseLogfile();
            return 0;
        }
        catch (BlibException ex)
        {
            // cpp parity: BlibFilter.cpp:115-116 — `cerr << "ERROR: " << e.what();`.
            // Skip if Verbosity::error already wrote the line.
            if (!ex.AlreadyLogged)
                WriteErrorLines(ex.Message);
            if (!string.IsNullOrEmpty(expectedError) && ex.Message.Contains(expectedError, StringComparison.Ordinal))
                foundExpectedError = true;
        }
        catch (Exception ex)
        {
            // cpp parity: BlibFilter.cpp:117-118 — `cerr << "ERROR: " << e.what();`.
            WriteErrorLines(ex.Message);
            if (!string.IsNullOrEmpty(expectedError) && ex.Message.Contains(expectedError, StringComparison.Ordinal))
            {
                foundExpectedError = true;
            }
            else
            {
                Verbosity.CloseLogfile();
                return 2;
            }
        }

        Verbosity.CloseLogfile();

        if (foundExpectedError)
            return 0;

        if (!string.IsNullOrEmpty(expectedError))
        {
            Console.Error.WriteLine($"FAILED: This negative test expected an error containing \"{expectedError}\"");
        }
        return 1;
    }

    /// <summary>
    /// cpp <c>WriteErrorLines</c> equivalent. Splits a (possibly multi-line) message and
    /// writes each line prefixed with <c>ERROR:</c>.
    /// </summary>
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
