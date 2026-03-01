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
    /// </summary>
    public static class ChatAppRegistry
    {
        private const string MCP_SERVER_NAME = "skyline";
        private const string MCP_SERVERS_KEY = "mcpServers";
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
            return HasJsonEntry(ClaudeDesktopConfigPath,
                root => root[MCP_SERVERS_KEY]?[MCP_SERVER_NAME]);
        }

        public static void AddToClaudeDesktop()
        {
            EditJsonFile(ClaudeDesktopConfigPath, root =>
            {
                var servers = EnsureObject(root, MCP_SERVERS_KEY);
                servers[MCP_SERVER_NAME] = BuildDesktopServerEntry();
            });
        }

        public static void RemoveFromClaudeDesktop()
        {
            EditJsonFile(ClaudeDesktopConfigPath, root =>
            {
                var servers = root[MCP_SERVERS_KEY]?.AsObject();
                servers?.Remove(MCP_SERVER_NAME);
            });
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
            return HasJsonEntry(ClaudeCodeConfigPath,
                root => root[MCP_SERVERS_KEY]?[MCP_SERVER_NAME]);
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

        // -- Helpers --

        private static JsonObject BuildDesktopServerEntry()
        {
            string exePath = McpServerDeployer.DeployedExePath.Replace('\\', '/');
            return new JsonObject
            {
                ["command"] = exePath
            };
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
