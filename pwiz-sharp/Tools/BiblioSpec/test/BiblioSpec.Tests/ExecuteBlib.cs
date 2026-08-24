using System.Diagnostics;
using System.Text;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// Drives one of the four BiblioSpec CLI tools (see <see cref="BlibTool"/>) via
/// <see cref="Process.Start(ProcessStartInfo)"/> and captures stdout / stderr / exit code.
///
/// <para>This is the managed analogue of the cpp <c>tests/ExecuteBlib.cpp</c> harness,
/// which Jamfile invokes with the tool name plus argv. The cpp version uses
/// <c>bnw::system</c> + shell redirection; here we exec the .exe directly so we
/// don't need a cmd.exe / sh round-trip and we get a clean stdout / stderr split.</para>
///
/// <para>Tool exe resolution: looks for
/// <c>Tools/BiblioSpec/src/&lt;ToolName&gt;/bin/&lt;Config&gt;/net8.0/&lt;tool-name&gt;.exe</c>
/// relative to <see cref="AppContext.BaseDirectory"/> walking up to the pwiz-sharp
/// root. When the exe is missing (tools not yet ported, or wrong config built),
/// callers should treat that as an <see cref="Assert.Inconclusive(string)"/> case —
/// <see cref="TryResolveToolPath"/> exposes the path-or-null directly so callers
/// can decide whether to skip or fail.</para>
/// </summary>
public static class ExecuteBlib
{
    /// <summary>
    /// Run the given BiblioSpec CLI tool with the given args and capture output.
    /// Mirrors the cpp <c>executeBlib(vector&lt;string&gt;)</c> signature in shape.
    /// </summary>
    /// <param name="tool">Which BiblioSpec CLI tool to execute.</param>
    /// <param name="args">Argv (excluding argv[0]). Each element is passed as a
    /// separate process argument — do not pre-quote.</param>
    /// <param name="workingDir">Working directory for the child process. Typically
    /// the test fixture's output dir so that any relative output paths land there.</param>
    /// <param name="stdout">Captured stdout (UTF-8).</param>
    /// <param name="stderr">Captured stderr (UTF-8).</param>
    /// <returns>The child process exit code. A non-zero exit does NOT throw —
    /// the test harness decides what to do with it (some tests expect failure
    /// via the cpp <c>-e@&lt;expected-error&gt;</c> convention).</returns>
    /// <exception cref="AssertInconclusiveException">Thrown via
    /// <see cref="Assert.Inconclusive(string)"/> if the tool exe is not found —
    /// makes the harness compile and run cleanly before the tools land.</exception>
    public static int Execute(
        BlibTool tool,
        string[] args,
        string workingDir,
        out string stdout,
        out string stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDir);

        string? exePath = TryResolveToolPath(tool);
        if (exePath is null)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            Assert.Inconclusive(
                $"BiblioSpec tool exe '{ToolExeName(tool)}' not found. " +
                $"The {tool} tool has not been ported yet, or the matching " +
                $"Tools/BiblioSpec/src/{tool}/bin/<Config>/net8.0/ output is stale.");
            return -1; // unreachable; Assert.Inconclusive throws
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {exePath}");

        // Read both streams to completion concurrently before WaitForExit
        // to avoid the classic deadlock where a child fills one pipe's buffer
        // while we block on the other.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit();
        stdout = stdoutTask.GetAwaiter().GetResult();
        stderr = stderrTask.GetAwaiter().GetResult();
        return proc.ExitCode;
    }

    /// <summary>
    /// Maps a <see cref="BlibTool"/> to the canonical exe filename. The cpp
    /// build emits <c>BlibBuild.exe</c> (Pascal-case) and the pwiz-sharp tool
    /// projects will follow the same convention so existing scripts keep working.
    /// </summary>
    public static string ToolExeName(BlibTool tool) => tool switch
    {
        BlibTool.BlibBuild => "BlibBuild.exe",
        BlibTool.BlibFilter => "BlibFilter.exe",
        BlibTool.BlibSearch => "BlibSearch.exe",
        BlibTool.BlibToMs2 => "BlibToMs2.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(tool)),
    };

    /// <summary>
    /// Resolve the absolute path of the tool exe by walking up from the test
    /// runner's base directory until we find a sibling <c>src/</c> tree, then
    /// looking under <c>BiblioSpec/src/&lt;Tool&gt;/bin/&lt;Config&gt;/net8.0/</c>.
    /// </summary>
    /// <param name="tool">Which CLI tool to resolve.</param>
    /// <returns>The absolute exe path, or null if no candidate exists. Returning
    /// null (rather than throwing) lets callers Inconclusive-skip cleanly.</returns>
    public static string? TryResolveToolPath(BlibTool tool)
    {
        string exeName = ToolExeName(tool);
        // Tools live at Tools/BiblioSpec/src/<Tool>/<Tool>.csproj — one project
        // per CLI tool, matching the cpp BiblioSpec tool exe names.
        string projectName = tool.ToString();

        // The build config can be Debug or Release; prefer the same config as the
        // test runner itself. AppContext.BaseDirectory ends with bin/<Config>/net8.0/.
        string config = InferConfig() ?? "Debug";

        // Walk up looking for a sibling src/ tree.
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(
                dir, "src", projectName, "bin", config, "net8.0", exeName);
            if (File.Exists(candidate))
                return candidate;

            // Also try the opposite config in case the user only built one of them.
            string otherConfig = config == "Debug" ? "Release" : "Debug";
            string altCandidate = Path.Combine(
                dir, "src", projectName, "bin", otherConfig, "net8.0", exeName);
            if (File.Exists(altCandidate))
                return altCandidate;

            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string? InferConfig()
    {
        // bin/<Config>/<tfm>/  →  walk back two levels and grab the directory name.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        return dir.Parent?.Name; // <Config>
    }
}
