/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SkylineAiConnector
{
    /// <summary>
    /// Detects chat app installations and registers/unregisters the Skyline
    /// MCP server in each app's configuration.
    /// - Claude Desktop: direct JSON edit of claude_desktop_config.json
    /// - Claude Code: delegates to the `claude` CLI for user-scope registration
    /// - Gemini CLI: direct JSON edit of ~/.gemini/settings.json
    /// - VS Code (Copilot): direct JSON edit of %APPDATA%/Code/User/mcp.json
    /// - Cursor: direct JSON edit of ~/.cursor/mcp.json
    /// - Codex: delegates to the `codex` CLI to edit ~/.codex/config.toml, which it reads directly
    /// </summary>
    public static class ChatAppRegistry
    {
        private const string MCP_SERVER_NAME = "skyline";
        private const string MCP_SERVERS_KEY = "mcpServers";
        private const string SERVERS_KEY = "servers"; // VS Code uses a different key
        private const string CLAUDE_PROCESS_NAME = "claude";
        private const string CLAUDE_CLI_EXE = CLAUDE_PROCESS_NAME + ".exe";
        private const string WINDOWS_APPS_PATH_MARKER = @"\WindowsApps\";
        private const string INSTALLER_PATH_MARKER = @"\" + CLAUDE_INSTALLER_FOLDER + @"\";

        private static readonly JsonSerializerOptions WRITE_OPTIONS = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        // -- Claude Desktop --

        private const string CLAUDE_DESKTOP_CONFIG = "claude_desktop_config.json";
        private const string CLAUDE_DATA_FOLDER = "Claude";
        private const string CLAUDE_STORE_PACKAGE_PREFIX = "Claude_";
        private const string CLAUDE_INSTALLER_FOLDER = "AnthropicClaude";

        /// <summary>
        /// The config files to keep the Skyline entry in, the one Claude Desktop most likely reads
        /// first. A Windows Store install can virtualize its app data into its package folder, where
        /// that copy shadows %APPDATA%\Claude, so every existing copy is written. Current Claude
        /// Desktop builds do not create the file until a local MCP server is added; when there is
        /// none, it goes in the data folder the installed app already uses.
        /// </summary>
        private static string[] ClaudeDesktopConfigPaths
        {
            get
            {
                var dataFolders = GetClaudeDesktopDataFolders();
                var existingFiles = dataFolders.Select(folder => Path.Combine(folder, CLAUDE_DESKTOP_CONFIG))
                    .Where(File.Exists).ToArray();
                if (existingFiles.Length > 0)
                    return existingFiles;
                string dataFolder = dataFolders.FirstOrDefault(Directory.Exists) ?? dataFolders.Last();
                return new[] { Path.Combine(dataFolder, CLAUDE_DESKTOP_CONFIG) };
            }
        }

        /// <summary>
        /// True if Claude Desktop is installed. Checks for the app's data folder, its Windows Store
        /// package, or the folder of the standalone installer - not for the config file, which
        /// current Claude Desktop builds no longer create on their own.
        /// </summary>
        public static bool IsClaudeDesktopInstalled()
        {
            if (GetClaudeDesktopDataFolders().Any(Directory.Exists))
                return true;
            if (GetClaudeStorePackageDirectories().Length > 0)
                return true;
            return Directory.Exists(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                CLAUDE_INSTALLER_FOLDER));
        }

        public static bool IsRegisteredInClaudeDesktop()
        {
            return IsRegisteredInJsonConfig(ClaudeDesktopConfigPaths[0], MCP_SERVERS_KEY);
        }

        public static void AddToClaudeDesktop()
        {
            foreach (string configPath in ClaudeDesktopConfigPaths)
                AddToJsonConfig(configPath, MCP_SERVERS_KEY, BuildServerEntry());
        }

        public static void RemoveFromClaudeDesktop()
        {
            // Nothing to remove without a config file, and writing one here would create the
            // data folder that makes Claude Desktop look installed.
            foreach (string configPath in ClaudeDesktopConfigPaths.Where(File.Exists))
                RemoveFromJsonConfig(configPath, MCP_SERVERS_KEY);
        }

        /// <summary>
        /// True if any Claude Desktop processes (Windows Store app or standalone installer) are
        /// running. Distinguished from Claude Code by the install path.
        /// </summary>
        public static bool IsClaudeDesktopRunning()
        {
            var processes = GetClaudeDesktopProcesses();
            foreach (var p in processes)
                p.Dispose();
            return processes.Length > 0;
        }

        /// <summary>
        /// Kill all running Claude Desktop processes so that the config file
        /// can be safely written without being overwritten on app exit.
        /// </summary>
        public static void StopClaudeDesktop()
        {
            foreach (var process in GetClaudeDesktopProcesses())
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch
                {
                    // Best effort - process may have exited between check and kill
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        // -- Claude Code --

        private static string ClaudeCodeConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude.json");
            }
        }

        /// <summary>
        /// True if the claude CLI that registration runs is installed. ~/.claude.json alone is not
        /// enough: the Code tab in Claude Desktop creates it too, using an embedded copy that is not
        /// on PATH, and that tab already sees the servers registered with Claude Desktop.
        /// </summary>
        public static bool IsClaudeCodeInstalled()
        {
            return File.Exists(ClaudeCodeConfigPath) && IsOnPath(CLAUDE_CLI_EXE);
        }

        public static bool IsRegisteredInClaudeCode()
        {
            // User-scope MCPs are at the top-level mcpServers key in ~/.claude.json
            return IsRegisteredInJsonConfig(ClaudeCodeConfigPath, MCP_SERVERS_KEY);
        }

        /// <summary>
        /// Register using the Claude CLI with user scope.
        /// </summary>
        public static void AddToClaudeCode()
        {
            string exePath = McpServerDeployer.DeployedExePath.Replace('\\', '/');
            // Quoted so a user profile path with a space stays one argument
            RunClaudeCli($@"mcp add -s user {MCP_SERVER_NAME} -- {QuoteArgument(exePath)}");
        }

        public static void RemoveFromClaudeCode()
        {
            RunClaudeCli($@"mcp remove -s user {MCP_SERVER_NAME}");
        }

        // -- Gemini CLI --

        private static string GeminiCliConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".gemini", "settings.json");
            }
        }

        public static bool IsGeminiCliInstalled()
        {
            // Check for gemini.cmd in npm global bin
            string npmGemini = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm", "gemini.cmd");
            return File.Exists(npmGemini);
        }

        public static bool IsRegisteredInGeminiCli()
        {
            return IsRegisteredInJsonConfig(GeminiCliConfigPath, MCP_SERVERS_KEY);
        }

        public static void AddToGeminiCli()
        {
            AddToJsonConfig(GeminiCliConfigPath, MCP_SERVERS_KEY, BuildServerEntry());
        }

        public static void RemoveFromGeminiCli()
        {
            RemoveFromJsonConfig(GeminiCliConfigPath, MCP_SERVERS_KEY);
        }

        // -- Antigravity --

        private static string AntigravityConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".gemini", "config", "mcp_config.json");
            }
        }

        public static bool IsAntigravityInstalled()
        {
            string antigravityDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gemini", "antigravity");
            return Directory.Exists(antigravityDir) || File.Exists(AntigravityConfigPath);
        }

        public static bool IsRegisteredInAntigravity()
        {
            return IsRegisteredInJsonConfig(AntigravityConfigPath, MCP_SERVERS_KEY);
        }

        public static void AddToAntigravity()
        {
            AddToJsonConfig(AntigravityConfigPath, MCP_SERVERS_KEY, BuildServerEntry());
        }

        public static void RemoveFromAntigravity()
        {
            RemoveFromJsonConfig(AntigravityConfigPath, MCP_SERVERS_KEY);
        }

        // -- VS Code (GitHub Copilot) --

        private static string VSCodeConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Code", "User", "mcp.json");
            }
        }

        public static bool IsVSCodeInstalled()
        {
            string vscodePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe");
            return File.Exists(vscodePath);
        }

        public static bool IsRegisteredInVSCode()
        {
            return IsRegisteredInJsonConfig(VSCodeConfigPath, SERVERS_KEY);
        }

        public static void AddToVSCode()
        {
            // VS Code requires "type": "stdio" in the server entry
            AddToJsonConfig(VSCodeConfigPath, SERVERS_KEY, BuildVSCodeServerEntry());
        }

        public static void RemoveFromVSCode()
        {
            RemoveFromJsonConfig(VSCodeConfigPath, SERVERS_KEY);
        }

        // -- Cursor --

        private static string CursorConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cursor", "mcp.json");
            }
        }

        public static bool IsCursorInstalled()
        {
            string cursorPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "cursor", "Cursor.exe");
            return File.Exists(cursorPath);
        }

        public static bool IsRegisteredInCursor()
        {
            return IsRegisteredInJsonConfig(CursorConfigPath, MCP_SERVERS_KEY);
        }

        public static void AddToCursor()
        {
            AddToJsonConfig(CursorConfigPath, MCP_SERVERS_KEY, BuildServerEntry());
        }

        public static void RemoveFromCursor()
        {
            RemoveFromJsonConfig(CursorConfigPath, MCP_SERVERS_KEY);
        }

        // -- Codex --

        private const string CODEX_SERVER_TABLE = "[mcp_servers." + MCP_SERVER_NAME + "]";

        private static string CodexHome
        {
            get
            {
                string codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
                if (!string.IsNullOrEmpty(codexHome))
                    return codexHome;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".codex");
            }
        }

        public static bool IsCodexInstalled()
        {
            // Every Codex front end (CLI, desktop app, IDE extension) creates this folder on first run
            return Directory.Exists(CodexHome);
        }

        /// <summary>
        /// Read from config.toml rather than through `codex mcp list`, which loads auth and
        /// cloud configuration and can refresh tokens over the network.
        /// </summary>
        public static bool IsRegisteredInCodex()
        {
            string configPath = Path.Combine(CodexHome, "config.toml");
            try
            {
                if (!File.Exists(configPath))
                    return false;
                foreach (string line in File.ReadLines(configPath))
                {
                    if (line.Trim() == CODEX_SERVER_TABLE)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void AddToCodex()
        {
            RunCodexCli("mcp", "add", MCP_SERVER_NAME, "--", McpServerDeployer.DeployedExePath);
        }

        public static void RemoveFromCodex()
        {
            RunCodexCli("mcp", "remove", MCP_SERVER_NAME);
        }

        /// <summary>
        /// Find the codex CLI the user runs from a terminal first, falling back to the copy
        /// bundled with the desktop app.
        /// </summary>
        private static string FindCodexExe()
        {
            foreach (string pathEntry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(pathEntry))
                    continue;
                string directory = pathEntry.Trim().Trim('"');
                string exePath = Path.Combine(directory, "codex.exe");
                if (File.Exists(exePath))
                    return exePath;
                // npm, whatever its prefix, puts a codex.cmd shim beside its node_modules. Launch the
                // native binary directly to avoid cmd.exe argument quoting.
                if (File.Exists(Path.Combine(directory, "codex.cmd")))
                {
                    string npmExe = FindNpmCodexExe(Path.Combine(directory, "node_modules", "@openai"));
                    if (npmExe != null)
                        return npmExe;
                }
            }

            // The desktop app bundles a CLI in a versioned directory that is not on PATH.
            string newestExe = null;
            foreach (string directory in GetDirectoriesSafe(Path.Combine(
                         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "OpenAI", "Codex", "bin"), "*"))
            {
                string exePath = Path.Combine(directory, "codex.exe");
                if (File.Exists(exePath) && (newestExe == null || File.GetLastWriteTimeUtc(exePath) > File.GetLastWriteTimeUtc(newestExe)))
                    newestExe = exePath;
            }
            return newestExe;
        }

        /// <summary>
        /// The npm package ships a native codex.exe per platform under vendor\&lt;target triple&gt;,
        /// either in @openai/codex itself or in a platform package such as @openai/codex-win32-x64.
        /// </summary>
        private static string FindNpmCodexExe(string openAiPackagesDir)
        {
            string targetTriple = RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "aarch64-pc-windows-msvc"
                : "x86_64-pc-windows-msvc";
            foreach (string package in GetDirectoriesSafe(openAiPackagesDir, "codex*"))
            {
                try
                {
                    foreach (string exePath in Directory.EnumerateFiles(package, "codex.exe", SearchOption.AllDirectories))
                    {
                        if (exePath.IndexOf(targetTriple, StringComparison.OrdinalIgnoreCase) >= 0)
                            return exePath;
                    }
                }
                catch (IOException)
                {
                    // Dangling junction left by npm link, or similar
                }
                catch (UnauthorizedAccessException)
                {
                    // Unlike EnumerationOptions on .NET Core, .NET Framework does not skip inaccessible folders
                }
            }
            return null;
        }

        /// <summary>
        /// Claude Desktop data folders, the one the app most likely reads first: each Windows Store
        /// package's virtualized %LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\, then the
        /// standard %APPDATA%\Claude\.
        /// </summary>
        private static List<string> GetClaudeDesktopDataFolders()
        {
            var dataFolders = GetClaudeStorePackageDirectories()
                .Select(packageDir => Path.Combine(packageDir, "LocalCache", "Roaming", CLAUDE_DATA_FOLDER))
                .ToList();
            dataFolders.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                CLAUDE_DATA_FOLDER));
            return dataFolders;
        }

        private static string[] GetClaudeStorePackageDirectories()
        {
            return GetDirectoriesSafe(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages"),
                CLAUDE_STORE_PACKAGE_PREFIX + "*");
        }

        private static string[] GetDirectoriesSafe(string directory, string searchPattern)
        {
            try
            {
                return Directory.Exists(directory)
                    ? Directory.GetDirectories(directory, searchPattern)
                    : Array.Empty<string>();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private static void RunCodexCli(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FindCodexExe() ?? throw new InvalidOperationException("Codex CLI was not found."),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Registration is user-wide; do not pick up a project-specific Codex configuration.
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                // .NET Framework has no ProcessStartInfo.ArgumentList
                Arguments = string.Join(" ", arguments.Select(QuoteArgument))
            };
            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start Codex CLI.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000))
            {
                process.Kill();
                throw new InvalidOperationException("Codex CLI timed out.");
            }
            output.GetAwaiter().GetResult();
            string errorText = error.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Codex CLI failed: " +
                    (string.IsNullOrWhiteSpace(errorText) ? "exit code " + process.ExitCode : errorText.Trim()));
            }
        }

        /// <summary>
        /// Quote one command-line argument the way CommandLineToArgvW parses it back, as
        /// ProcessStartInfo.ArgumentList does on .NET Core: backslashes are literal except
        /// before a quote, where each must be doubled.
        /// </summary>
        private static string QuoteArgument(string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
                return argument;
            var sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                sb.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
                sb.Append(c);
                backslashes = 0;
            }
            sb.Append('\\', backslashes * 2);
            return sb.Append('"').ToString();
        }

        // -- Shared JSON config helpers --

        /// <summary>
        /// Add/update the skyline MCP server entry in a JSON config file.
        /// Creates the file and parent directory if they don't exist.
        /// </summary>
        private static void AddToJsonConfig(string configPath, string serversKey, JsonObject serverEntry)
        {
            EditJsonFile(configPath, root =>
            {
                var servers = EnsureObject(root, serversKey);
                servers[MCP_SERVER_NAME] = serverEntry;
            });
        }

        /// <summary>
        /// Remove the skyline MCP server entry from a JSON config file.
        /// </summary>
        private static void RemoveFromJsonConfig(string configPath, string serversKey)
        {
            EditJsonFile(configPath, root =>
            {
                var servers = root[serversKey]?.AsObject();
                servers?.Remove(MCP_SERVER_NAME);
            });
        }

        /// <summary>
        /// Check whether the skyline MCP server is registered in a JSON config file.
        /// </summary>
        private static bool IsRegisteredInJsonConfig(string configPath, string serversKey)
        {
            return HasJsonEntry(configPath,
                root => root[serversKey]?[MCP_SERVER_NAME]);
        }

        // -- Server entry builders --

        /// <summary>
        /// Standard server entry used by Claude Desktop, Gemini CLI, and Cursor.
        /// </summary>
        private static JsonObject BuildServerEntry()
        {
            string exePath = McpServerDeployer.DeployedExePath.Replace('\\', '/');
            return new JsonObject
            {
                ["command"] = exePath
            };
        }

        /// <summary>
        /// VS Code server entry — requires "type": "stdio".
        /// </summary>
        private static JsonObject BuildVSCodeServerEntry()
        {
            string exePath = McpServerDeployer.DeployedExePath.Replace('\\', '/');
            return new JsonObject
            {
                ["type"] = "stdio",
                ["command"] = exePath
            };
        }

        // -- Helpers --

        /// <summary>
        /// True if a file of this name is in a PATH folder, the way Process.Start finds "claude"
        /// for RunClaudeCli.
        /// </summary>
        private static bool IsOnPath(string fileName)
        {
            foreach (string pathEntry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(pathEntry))
                    continue;
                if (File.Exists(Path.Combine(pathEntry.Trim().Trim('"'), fileName)))
                    return true;
            }
            return false;
        }

        private static void RunClaudeCli(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new InvalidOperationException("Failed to start claude CLI");
                process.WaitForExit(10000);
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new InvalidOperationException(
                        "claude CLI failed: " + (string.IsNullOrEmpty(error) ? "exit code " + process.ExitCode : error));
                }
            }
        }

        private static bool HasJsonEntry(string configPath, Func<JsonObject, JsonNode> findEntry)
        {
            if (!File.Exists(configPath))
                return false;
            try
            {
                string json = File.ReadAllText(configPath);
                var root = JsonNode.Parse(json)?.AsObject();
                return root != null && findEntry(root) != null;
            }
            catch
            {
                return false;
            }
        }

        private static void EditJsonFile(string configPath, Action<JsonObject> edit)
        {
            JsonObject root = null;
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        // "as", not AsObject(): a well-formed file whose root is not an object (an array,
                        // say) is as unusable to us as a malformed one, and AsObject() throws for it.
                        root = JsonNode.Parse(json) as JsonObject;
                        if (root == null)
                            TryBackupFile(configPath); // Replaced below -- keep whatever was there.
                    }
                }
                catch (JsonException)
                {
                    // The existing config is malformed. Back it up before we overwrite it with a fresh
                    // config below, so the user can recover whatever was there if it mattered.
                    TryBackupFile(configPath);
                }
                // Any other failure -- most likely the file being locked by the chat app that owns it --
                // means we do not know what is in the config, only that something is. Overwriting it here
                // would silently discard every other MCP server the user has registered, and the ".bak"
                // copy would fail under the same lock, so there would be nothing to recover from. Let it
                // out instead: every caller reports the failure and leaves the file alone.
            }

            if (root == null)
            {
                root = new JsonObject();
            }

            edit(root);

            string directory = Path.GetDirectoryName(configPath);
            if (directory != null)
                Directory.CreateDirectory(directory);
            File.WriteAllText(configPath, root.ToJsonString(WRITE_OPTIONS));
        }

        // Makes a best-effort copy of a file alongside the original (".bak") before it is overwritten.
        // Any failure is ignored: the backup is a convenience, not something the caller depends on.
        private static void TryBackupFile(string filePath)
        {
            try
            {
                File.Copy(filePath, filePath + ".bak", true);
            }
            catch
            {
                // Best effort -- if the backup cannot be written, proceed with the overwrite anyway.
            }
        }

        private static JsonObject EnsureObject(JsonObject parent, string key)
        {
            if (parent[key] is JsonObject existing)
                return existing;
            var obj = new JsonObject();
            parent[key] = obj;
            return obj;
        }

        /// <summary>
        /// Get all Claude Desktop processes (Windows Store app under WindowsApps, or the standalone
        /// installer under AnthropicClaude). Excludes Claude Code processes, which run from other paths.
        /// </summary>
        private static Process[] GetClaudeDesktopProcesses()
        {
            var result = new List<Process>();
            foreach (var process in Process.GetProcessesByName(CLAUDE_PROCESS_NAME))
            {
                try
                {
                    string path = process.MainModule?.FileName;
                    if (path != null &&
                        (path.IndexOf(WINDOWS_APPS_PATH_MARKER, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf(INSTALLER_PATH_MARKER, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        result.Add(process);
                        continue;
                    }
                }
                catch
                {
                    // Access denied or process exited - skip
                }
                process.Dispose();
            }
            return result.ToArray();
        }

        /// <summary>
        /// Returns true if the MCP server is registered in any installed AI client.
        /// Used to auto-expand the setup panel on first launch.
        /// </summary>
        public static bool AnyClientRegistered()
        {
            return (IsClaudeDesktopInstalled() && IsRegisteredInClaudeDesktop()) ||
                   (IsClaudeCodeInstalled() && IsRegisteredInClaudeCode()) ||
                   (IsGeminiCliInstalled() && IsRegisteredInGeminiCli()) ||
                   (IsAntigravityInstalled() && IsRegisteredInAntigravity()) ||
                   (IsVSCodeInstalled() && IsRegisteredInVSCode()) ||
                   (IsCursorInstalled() && IsRegisteredInCursor()) ||
                   (IsCodexInstalled() && IsRegisteredInCodex());
        }
    }
}
