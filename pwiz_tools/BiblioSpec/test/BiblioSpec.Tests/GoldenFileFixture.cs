namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Locates the cpp BiblioSpec test inputs + reference directory in a sibling
/// pwiz/ checkout. Cached as a static singleton — discovery walks the file
/// system and we don't want to re-pay that per test.
///
/// <para>Standard dev layout: <c>C:/dev/pwiz/</c> + <c>C:/dev/pwiz-sharp/</c>
/// (or, like this checkout, <c>C:/dev/pwiz-msconvert-pr/pwiz-sharp/</c>). The
/// fixture walks up from the test runner's <c>AppContext.BaseDirectory</c>
/// looking for any sibling that exposes
/// <c>pwiz_tools/BiblioSpec/tests/inputs/</c>.</para>
///
/// <para>When the cpp tree isn't checked out alongside (uncommon but possible
/// on a slimmed-down developer machine), <see cref="TryLocate"/> returns null
/// and the harness Inconclusive-skips the affected tests.</para>
/// </summary>
public sealed class GoldenFileFixture
{
    private static readonly Lazy<GoldenFileFixture?> _instance = new(TryLocate);

    /// <summary>The singleton fixture, or null if no sibling pwiz tree was found.</summary>
    public static GoldenFileFixture? Instance => _instance.Value;

    /// <summary>Absolute path to <c>pwiz_tools/BiblioSpec/tests/inputs/</c>.</summary>
    public string InputsDir { get; }

    /// <summary>Absolute path to <c>pwiz_tools/BiblioSpec/tests/reference/</c>.</summary>
    public string ReferenceDir { get; }

    /// <summary>
    /// A scratch output directory under the runner's bin folder — each test run
    /// gets a fresh subdir. Cleaned up by the OS / next-run reuse; tests should
    /// not assume cross-run cleanup.
    /// </summary>
    public string OutputDir { get; }

    private GoldenFileFixture(string inputsDir, string referenceDir, string outputDir)
    {
        InputsDir = inputsDir;
        ReferenceDir = referenceDir;
        OutputDir = outputDir;
    }

    /// <summary>
    /// Resolve a checked-in input file by name. Throws on missing — callers
    /// should already have decided whether their test can proceed via
    /// <see cref="GoldenFileFixture.Instance"/> being non-null.
    /// </summary>
    public string InputFile(string fileName) => Path.Combine(InputsDir, fileName);

    /// <summary>Resolve a checked-in reference file by name.</summary>
    public string ReferenceFile(string fileName) => Path.Combine(ReferenceDir, fileName);

    /// <summary>Resolve a per-test output path (under the runner's bin folder).</summary>
    public string OutputFile(string fileName) => Path.Combine(OutputDir, fileName);

    /// <summary>
    /// Try to locate the cpp test fixture. Walks up from the runner's base dir
    /// looking for a sibling that contains <c>pwiz_tools/BiblioSpec/tests/</c>.
    /// </summary>
    private static GoldenFileFixture? TryLocate()
    {
        // Relative-from-test pattern: <some-root>/pwiz/pwiz_tools/BiblioSpec/tests/inputs
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            // Direct match: the loop directory itself contains the pwiz_tools path
            // (rare — only if someone runs the tests from inside the cpp tree).
            var direct = TryMatchAt(dir);
            if (direct is not null) return direct;

            // Sibling match: every immediate sibling of `dir` could be the pwiz checkout.
            var parent = Directory.GetParent(dir);
            if (parent is not null)
            {
                foreach (var sibling in parent.EnumerateDirectories())
                {
                    var hit = TryMatchAt(sibling.FullName);
                    if (hit is not null) return hit;
                }
            }
            dir = parent?.FullName;
        }
        return null;
    }

    private static GoldenFileFixture? TryMatchAt(string root)
    {
        string inputs = Path.Combine(root, "pwiz_tools", "BiblioSpec", "tests", "inputs");
        if (!Directory.Exists(inputs)) return null;

        string reference = Path.Combine(root, "pwiz_tools", "BiblioSpec", "tests", "reference");
        if (!Directory.Exists(reference)) return null;

        // Stage the scratch output under the test runner's bin folder so it travels
        // with the test results and is gitignored.
        string output = Path.Combine(AppContext.BaseDirectory, "bibliospec-output");
        Directory.CreateDirectory(output);
        return new GoldenFileFixture(inputs, reference, output);
    }
}
