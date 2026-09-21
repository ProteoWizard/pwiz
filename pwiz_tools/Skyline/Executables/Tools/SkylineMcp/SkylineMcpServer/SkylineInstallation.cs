/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace SkylineMcpServer;

/// <summary>
/// Describes one installed Skyline release detected on this machine.
/// Mirrors the discovery logic in
/// pwiz_tools/Skyline/Executables/SharedBatch/SharedBatch/SkylineInstallations.cs
/// without taking a project reference, since the MCP server is intentionally
/// standalone. Returns POCOs the LLM (and tests) can reason about, rather
/// than writing Settings.Default side-effects.
/// </summary>
public class SkylineInstallation
{
    public const string SKYLINE = "Skyline";
    public const string SKYLINE_DAILY = "Skyline-daily";

    public const string SCOPE_CLICK_ONCE = "user_clickonce";
    public const string SCOPE_ADMIN = "system_admin";

    /// <summary>
    /// Display name, currently the same as <see cref="Release"/>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Canonical release identifier: <see cref="SKYLINE"/> or
    /// <see cref="SKYLINE_DAILY"/>.
    /// </summary>
    public string Release { get; set; }

    /// <summary>
    /// Version string. For admin installs this is
    /// FileVersionInfo.ProductVersion on the GUI exe. For ClickOnce installs
    /// this is parsed from the deployment URL inside the .appref-ms when
    /// possible, otherwise "ClickOnce (auto-update)".
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// <see cref="SCOPE_ADMIN"/> for installs under %ProgramFiles%,
    /// <see cref="SCOPE_CLICK_ONCE"/> for per-user ClickOnce installs.
    /// </summary>
    public string InstallScope { get; set; }

    /// <summary>
    /// Path to launch the GUI. For admin installs this is Skyline.exe /
    /// Skyline-daily.exe directly. For ClickOnce this is the .appref-ms
    /// shortcut, which must be launched via Process.Start with
    /// UseShellExecute=true.
    /// </summary>
    public string GuiPath { get; set; }

    /// <summary>
    /// Path to SkylineCmd.exe for admin installs, or null for ClickOnce
    /// installs (SkylineCmd.exe is not part of the ClickOnce payload).
    /// SkylineCmd.exe uses its own user.config separate from the GUI;
    /// the tool description explains how --save-settings populates it.
    /// </summary>
    public string CliPath { get; set; }

    /// <summary>
    /// Path to the bundled SkylineRunner.exe / SkylineDailyRunner.exe shim
    /// for ClickOnce installs, or null for admin installs. The shim
    /// launches the user's GUI Skyline in headless CMD mode so it runs
    /// in the same user.config as the GUI (custom reports etc. visible).
    /// </summary>
    public string RunnerPath { get; set; }

    /// <summary>
    /// Enumerate all detected Skyline installs. Returns admin installs
    /// from %ProgramFiles% and ClickOnce installs from the Programs
    /// folder. When both are present for the same release, both are
    /// returned and the caller chooses.
    /// </summary>
    public static List<SkylineInstallation> FindAll()
    {
        var results = new List<SkylineInstallation>();
        results.AddRange(FindAdministrative());
        results.AddRange(FindClickOnce());
        return results;
    }

    // SharedBatch constants kept identical so future maintainers can grep
    // both implementations together.
    private const string SKYLINE_EXE = "Skyline.exe";
    private const string SKYLINE_DAILY_EXE = "Skyline-daily.exe";
    private const string SKYLINE_CMD_EXE = "SkylineCmd.exe";
    private const string SKYLINE_RUNNER_EXE = "SkylineRunner.exe";
    private const string SKYLINE_DAILY_RUNNER_EXE = "SkylineDailyRunner.exe";
    private const string APPREF_EXT = ".appref-ms";
    private const string MACCOSS_LAB_FOLDER = "MacCoss Lab, UW";

    private static IEnumerable<SkylineInstallation> FindAdministrative()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var (release, guiExe) in new[]
                 {
                     (SKYLINE, SKYLINE_EXE),
                     (SKYLINE_DAILY, SKYLINE_DAILY_EXE)
                 })
        {
            string installDir = Path.Combine(programFiles, release);
            string guiPath = Path.Combine(installDir, guiExe);
            string cliPath = Path.Combine(installDir, SKYLINE_CMD_EXE);
            if (!Directory.Exists(installDir) || !File.Exists(cliPath))
                continue;
            yield return new SkylineInstallation
            {
                Name = release,
                Release = release,
                Version = TryGetFileVersion(File.Exists(guiPath) ? guiPath : cliPath),
                InstallScope = SCOPE_ADMIN,
                GuiPath = File.Exists(guiPath) ? guiPath : null,
                CliPath = cliPath,
                RunnerPath = null
            };
        }
    }

    private static IEnumerable<SkylineInstallation> FindClickOnce()
    {
        string baseDir = AppContext.BaseDirectory;
        foreach (var (release, runnerExe) in new[]
                 {
                     (SKYLINE, SKYLINE_RUNNER_EXE),
                     (SKYLINE_DAILY, SKYLINE_DAILY_RUNNER_EXE)
                 })
        {
            string shortcutPath = FindClickOnceShortcut(release);
            if (shortcutPath == null)
                continue;
            // The runner shim is bundled with the MCP server via Content Include,
            // so it sits next to this assembly. We do not File.Exists-check here:
            // if the shim is missing the MCP server itself is broken (packaging
            // bug), and a shell invocation will surface that loudly. Returning the
            // expected path keeps the row invariant that every install has exactly
            // one of CliPath / RunnerPath populated.
            string runnerPath = Path.Combine(baseDir, runnerExe);
            yield return new SkylineInstallation
            {
                Name = release,
                Release = release,
                Version = TryParseClickOnceVersion(shortcutPath),
                InstallScope = SCOPE_CLICK_ONCE,
                GuiPath = shortcutPath,
                CliPath = null,
                RunnerPath = runnerPath
            };
        }
    }

    private static string FindClickOnceShortcut(string release)
    {
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        string shortcutName = release + APPREF_EXT;
        foreach (string candidate in new[]
                 {
                     Path.Combine(programs, MACCOSS_LAB_FOLDER, shortcutName),
                     Path.Combine(programs, release, shortcutName)
                 })
        {
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static string TryGetFileVersion(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            return info.ProductVersion ?? info.FileVersion ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    // ClickOnce .appref-ms is UTF-8 text containing a deployment URL that
    // usually carries the version (e.g. ".../skyline-23.1.0.380/...").
    // Some files have a UTF-16 BOM; ReadAllText auto-detects. Parsing this
    // is best-effort: if the version isn't in the URL, return a label that
    // makes it clear the install auto-updates.
    private static readonly Regex VERSION_IN_URL = new(@"(\d+\.\d+\.\d+\.\d+)", RegexOptions.Compiled);

    private static string TryParseClickOnceVersion(string apprefPath)
    {
        try
        {
            string contents = File.ReadAllText(apprefPath);
            var match = VERSION_IN_URL.Match(contents);
            if (match.Success)
                return match.Groups[1].Value;
        }
        catch
        {
            // Fall through to the generic label
        }
        return "ClickOnce (auto-update)";
    }
}
