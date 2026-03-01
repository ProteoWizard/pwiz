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
using System.IO;

namespace SkylineMcpConnector
{
    public static class McpServerDeployer
    {
        public const string MCP_SERVER_EXE = "SkylineMcpServer.exe";
        private const string MCP_SERVER_SUBFOLDER = "mcp-server";
        private const string DEPLOY_FOLDER_NAME = ".skyline-mcp";
        private const string SERVER_SUBFOLDER = "server";

        /// <summary>
        /// The deployment target directory: %USERPROFILE%/.skyline-mcp/
        /// </summary>
        public static string DeployDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    DEPLOY_FOLDER_NAME);
            }
        }

        /// <summary>
        /// The server/ subdirectory where MCP server files are deployed.
        /// </summary>
        public static string ServerDir
        {
            get { return Path.Combine(DeployDir, SERVER_SUBFOLDER); }
        }

        /// <summary>
        /// Full path to the deployed MCP server executable.
        /// </summary>
        public static string DeployedExePath
        {
            get { return Path.Combine(ServerDir, MCP_SERVER_EXE); }
        }

        /// <summary>
        /// The mcp-server/ subfolder next to the running connector executable.
        /// When installed via Skyline External Tools, this is where the tool ZIP
        /// extracts the MCP server binaries.
        /// </summary>
        private static string SourceDir
        {
            get
            {
                string connectorDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(connectorDir, MCP_SERVER_SUBFOLDER);
            }
        }

        /// <summary>
        /// Deploy the MCP server to ~/.skyline-mcp/ if needed.
        /// Copies all files from the mcp-server/ subfolder.
        /// Returns true if deployment occurred, false if already up to date.
        /// Throws if the source directory is missing.
        /// </summary>
        public static bool Deploy()
        {
            if (!Directory.Exists(SourceDir))
            {
                throw new InvalidOperationException(
                    "MCP server files not found at: " + SourceDir +
                    "\nThe tool installation may be incomplete.");
            }

            string sourceExe = Path.Combine(SourceDir, MCP_SERVER_EXE);
            if (!File.Exists(sourceExe))
            {
                throw new InvalidOperationException(
                    "MCP server executable not found: " + sourceExe);
            }

            // Check if deployment is needed by comparing the exe timestamp and size.
            // Size check is needed because Skyline tool re-install extracts from ZIP,
            // which can preserve original timestamps even when content has changed.
            if (File.Exists(DeployedExePath))
            {
                var sourceTime = File.GetLastWriteTimeUtc(sourceExe);
                var deployedTime = File.GetLastWriteTimeUtc(DeployedExePath);
                var sourceSize = new FileInfo(sourceExe).Length;
                var deployedSize = new FileInfo(DeployedExePath).Length;
                if (sourceTime <= deployedTime && sourceSize == deployedSize)
                    return false; // Already up to date
            }

            // Create target directory
            Directory.CreateDirectory(ServerDir);

            // Copy all files from source to server/ subdirectory
            foreach (string sourceFile in Directory.GetFiles(SourceDir))
            {
                string fileName = Path.GetFileName(sourceFile);
                string targetFile = Path.Combine(ServerDir, fileName);
                File.Copy(sourceFile, targetFile, true);
            }

            return true;
        }

        /// <summary>
        /// Get the claude mcp add command for registering the MCP server.
        /// Uses forward slashes for the path (Claude Code convention).
        /// </summary>
        public static string GetRegistrationCommand()
        {
            string exePath = DeployedExePath.Replace('\\', '/');
            return "claude mcp add skyline -- " + exePath;
        }
    }
}
