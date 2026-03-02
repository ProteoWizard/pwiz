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
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SkylineMcpConnector
{
    /// <summary>
    /// Detects chat app installations and registers/unregisters the Skyline
    /// MCP server in each app's configuration.
    /// - Claude Desktop: direct JSON edit of claude_desktop_config.json
    /// - Claude Code: delegates to the `claude` CLI for user-scope registration
    /// - Gemini CLI: direct JSON edit of ~/.gemini/settings.json
    /// - VS Code (Copilot): direct JSON edit of %APPDATA%/Code/User/mcp.json
    /// - Cursor: direct JSON edit of ~/.cursor/mcp.json
    /// </summary>
    public static class ChatAppRegistry
    {
        private const string MCP_SERVER_NAME = "skyline";
        private const string MCP_SERVERS_KEY = "mcpServers";
        private const string SERVERS_KEY = "servers"; // VS Code uses a different key
        private const string CLAUDE_PROCESS_NAME = "claude";
        private const string WINDOWS_APPS_PATH_MARKER = @"\WindowsApps\";

        private static readonly JsonSerializerOptions WRITE_OPTIONS = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        // -- Claude Desktop --

        private static string ClaudeDesktopConfigPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Claude", "claude_desktop_config.json");
            }
        }

        public static bool IsClaudeDesktopInstalled()
        {
            return File.Exists(ClaudeDesktopConfigPath);
        }

        public static bool IsRegisteredInClaudeDesktop()
        {
            return IsRegisteredInJsonConfig(ClaudeDesktopConfigPath, MCP_SERVERS_KEY);
        }

        public static void AddToClaudeDesktop()
        {
            AddToJsonConfig(ClaudeDesktopConfigPath, MCP_SERVERS_KEY, BuildServerEntry());
        }

        public static void RemoveFromClaudeDesktop()
        {
            RemoveFromJsonConfig(ClaudeDesktopConfigPath, MCP_SERVERS_KEY);
        }

        /// <summary>
        /// True if any Claude Desktop processes (from the Windows Store app) are running.
        /// Distinguished from Claude Code by the WindowsApps install path.
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

        public static bool IsClaudeCodeInstalled()
        {
            return File.Exists(ClaudeCodeConfigPath);
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
            RunClaudeCli($@"mcp add -s user {MCP_SERVER_NAME} -- {exePath}");
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
            JsonObject root;
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            edit(root);

            string directory = Path.GetDirectoryName(configPath);
            if (directory != null)
                Directory.CreateDirectory(directory);
            File.WriteAllText(configPath, root.ToJsonString(WRITE_OPTIONS));
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
        /// Get all Claude Desktop processes (Windows Store app under WindowsApps).
        /// Excludes Claude Code processes which run from a different path.
        /// </summary>
        private static Process[] GetClaudeDesktopProcesses()
        {
            var result = new List<Process>();
            foreach (var process in Process.GetProcessesByName(CLAUDE_PROCESS_NAME))
            {
                try
                {
                    string path = process.MainModule?.FileName;
                    if (path != null && path.IndexOf(WINDOWS_APPS_PATH_MARKER, StringComparison.OrdinalIgnoreCase) >= 0)
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
    }
}
