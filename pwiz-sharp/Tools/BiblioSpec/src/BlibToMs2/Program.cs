// Port of pwiz_tools/BiblioSpec/src/BlibToMs2.cpp main() — argv tweaks + exception → exit code mapping.

using Pwiz.Tools.BiblioSpec;

namespace Pwiz.Tools.BiblioSpec.BlibToMs2;

/// <summary>
/// BlibToMs2 CLI entry point. cpp parity: BlibToMs2.cpp:44 main().
/// </summary>
public static class Program
{
    /// <summary>
    /// Exit codes mirror the cpp tool:
    /// <list type="bullet">
    /// <item>0 — success (or expected error matched).</item>
    /// <item>1 — <see cref="BlibException"/> or run failed.</item>
    /// <item>2 — other unhandled exception.</item>
    /// </list>
    /// </summary>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // CliPreproc.Strip rewrites `--out=PATH` into a trailing positional argument because
        // BlibBuild treats the last positional as the output library. BlibToMs2 instead expects
        // ONE positional (the input .blib) and reads --out= as a long option (handled in
        // ParseCommandArgs at BlibToMs2.cs:324). Leave --out= in place; only do -e capture and
        // --unicode strip here.
        var (argv, expectedError) = CliPreproc.Strip(args, rewriteOut: false);

        try
        {
            var opts = BlibToMs2Runner.ParseCommandArgs(argv, out var showHelp);
            if (showHelp)
            {
                BlibToMs2Runner.PrintUsage();
                return 0;
            }

            var runner = new BlibToMs2Runner(opts);
            runner.Run();

            if (!string.IsNullOrEmpty(expectedError))
            {
                // We expected an error and didn't get one — that's a negative-test failure.
                Console.Error.WriteLine(
                    $"FAILED: This negative test expected an error containing \"{expectedError}\"");
                return 1;
            }
            return 0;
        }
        catch (BlibException ex)
        {
            // cpp parity: BlibToMs2 has no top-level catch, so a BlibException would propagate
            // up to the runtime's default handler. We match the BlibBuild pattern instead:
            // print + return 1, with the "expected error" override.
            if (!ex.AlreadyLogged)
            {
                WriteErrorLines(ex.Message);
            }
            if (!string.IsNullOrEmpty(expectedError) && ex.Message.Contains(expectedError, StringComparison.Ordinal))
            {
                return 0;
            }
            return 1;
        }
        catch (Exception ex)
        {
            WriteErrorLines(ex.Message);
            if (!string.IsNullOrEmpty(expectedError) && ex.Message.Contains(expectedError, StringComparison.Ordinal))
            {
                return 0;
            }
            return 2;
        }
    }

    /// <summary>Split a (possibly multi-line) message and prefix each line with <c>ERROR:</c>.</summary>
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
