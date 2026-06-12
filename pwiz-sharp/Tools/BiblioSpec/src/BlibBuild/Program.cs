// Port of pwiz_tools/BiblioSpec/src/BlibBuild.cpp.
//
// Main entry point for the BlibBuild CLI. Strips the optional `-e <expected>` arg
// (used by the cpp test harness to assert that a specific error string is emitted),
// then constructs a BlibBuilder, runs parseCommandArgs / init / BuildLibrary / commit,
// and translates exceptions to exit codes.

using Pwiz.Data.MsData.Readers;
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

        // Vendor SDK-backed readers register themselves with the shared format-detection
        // dispatcher so PwizSharpSpecFileReader can open .raw / .wiff / .baf / .lcd / etc.
        // when a pep.xml or msms.txt references one. The vendor projects can't live in
        // Pwiz.Data.MsData without dragging the native SDKs into every consumer, so each
        // exposes a static `AddTo(ReaderList)` helper that we wire up at startup.
        RegisterVendorReaders();

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
                Verbosity.WriteErrorLines(ex.Message);
            }
            if (!string.IsNullOrEmpty(expectedError) && ex.Message.Contains(expectedError, StringComparison.Ordinal))
            {
                foundExpectedError = true;
            }
        }
        catch (Exception ex)
        {
            // cpp parity: BlibBuild.cpp:284 — std::exception fall-through, exit 1.
            Verbosity.WriteErrorLines(ex.Message);
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
    /// Register every vendor reader project we're linked against with the shared
    /// <see cref="ReaderList.AdditionalReaders"/> list, so the default
    /// format-detection dispatcher can open vendor file formats. Idempotent — calling
    /// twice (e.g. across test runs in the same process) won't double-register.
    /// </summary>
    private static void RegisterVendorReaders()
    {
        if (ReaderList.AdditionalReaders.Count > 0) return;
        ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Bruker.Reader_Bruker());
        ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
        ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Shimadzu.Reader_Shimadzu());
        ReaderList.AdditionalReaders.Add(new Pwiz.Vendor.Thermo.Reader_Thermo());
    }

}
