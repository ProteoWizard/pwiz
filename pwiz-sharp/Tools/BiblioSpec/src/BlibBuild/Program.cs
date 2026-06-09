// Port of pwiz_tools/BiblioSpec/src/BlibBuild.cpp.
//
// Main entry point for the BlibBuild CLI. Strips the optional `-e <expected>` arg
// (used by the cpp test harness to assert that a specific error string is emitted),
// then constructs a BlibBuilder, runs parseCommandArgs / init / BuildLibrary / commit,
// and translates exceptions to exit codes.

using Pwiz.Tools.BiblioSpec;

namespace Pwiz.Tools.BiblioSpec.BlibBuild;

/// <summary>
/// BlibBuild CLI entry point. cpp parity: BlibBuild.cpp:94 main().
/// </summary>
public static class Program
{
    /// <summary>
    /// Exit codes mirror the cpp tool:
    /// <list type="bullet">
    /// <item>0 — success (or expected error matched).</item>
    /// <item>1 — <see cref="BlibException"/> or build failed.</item>
    /// <item>2 — other unhandled exception.</item>
    /// </list>
    /// </summary>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Shared argv preprocessing: -e capture, --out=PATH rewrite, --unicode strip.
        // See CliPreproc for cpp-parity refs.
        var (argv, expectedError) = CliPreproc.Strip(args);

        var builder = new BlibBuilder();
        var foundExpectedError = false;

        try
        {
            builder.ParseCommandArgs(argv);
            builder.Init();

            var success = builder.BuildLibrary();

            // cpp parity: BlibBuild.cpp:248 — scan per-file errors for the expectedError
            // substring; success is forgiven when the expected error was found.
            if (!string.IsNullOrEmpty(expectedError))
            {
                foreach (var err in builder.Errors)
                {
                    if (err.Contains(expectedError, StringComparison.Ordinal))
                    {
                        foundExpectedError = true;
                        break;
                    }
                }
                if (foundExpectedError)
                {
                    // cpp parity: BlibBuild.cpp:259 — undo the active transaction so we
                    // don't write partial state for a negative test.
                    builder.UndoActiveTransaction();
                    success = true;
                }
                else
                {
                    // cpp parity: BlibBuild.cpp:262 — we expected an error that didn't happen.
                    Console.Error.WriteLine($"FAILED: This negative test expected an error containing \"{expectedError}\"");
                    success = false;
                }
            }

            if (!builder.IsScoreLookupMode && !foundExpectedError)
            {
                if (builder.IsEmpty())
                {
                    builder.AbortCurrentLibrary();
                    if (success)
                    {
                        // cpp parity: BlibBuild.cpp:271 — Verbosity::error throws BlibException.
                        Verbosity.Error("No spectra were found for the new library.");
                    }
                }
                else
                {
                    builder.CollapseSources();
                    builder.Commit();
                }
            }

            Verbosity.CloseLogfile();
            return success ? 0 : 1;
        }
        catch (BlibException ex)
        {
            // cpp parity: BlibBuild.cpp:226 — record + emit; if the message contains the
            // expected error string, exit 0. Skip the WriteErrorLines call when the
            // exception was already logged by Verbosity.Error (avoids the C#-specific
            // double-print since cpp's Verbosity::log calls exit() directly).
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
            // cpp parity: BlibBuild.cpp:284 — std::exception fall-through, exit 1.
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

        // We reached here via the catch path. Translate to cpp's "expected error matched?"
        // semantics: BlibBuild.cpp:258 — when foundExpectedError, exit 0; otherwise if an
        // -e was provided that wasn't matched, complain + exit 1.
        Verbosity.CloseLogfile();

        if (foundExpectedError)
            return 0;

        if (!string.IsNullOrEmpty(expectedError))
        {
            // cpp parity: BlibBuild.cpp:262 — "FAILED: This negative test expected an error containing ..."
            Console.Error.WriteLine($"FAILED: This negative test expected an error containing \"{expectedError}\"");
        }
        return 1;
    }

    /// <summary>
    /// cpp <c>WriteErrorLines</c> at BlibBuild.cpp:78. Splits a (possibly multi-line)
    /// message and writes each line prefixed with <c>ERROR:</c>.
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
