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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkylineTool;

namespace SkylineAiConnector
{
    public class ConnectionInfo
    {
        [JsonPropertyName("pipe_name")]
        public string PipeName { get; set; }

        [JsonPropertyName("process_id")]
        public int ProcessId { get; set; }

        [JsonPropertyName("connected_at")]
        public string ConnectedAt { get; set; }

        [JsonPropertyName("skyline_version")]
        public string SkylineVersion { get; set; }

        // Legacy single-file name for backward compatibility
        private const string LEGACY_CONNECTION_FILE = "connection.json";

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <summary>
        /// Build a ConnectionInfo for the current Skyline instance.
        /// </summary>
        public static ConnectionInfo Create(string legacyToolServiceName, string skylineVersion, int processId)
        {
            return new ConnectionInfo
            {
                PipeName = JsonToolConstants.GetJsonPipeName(legacyToolServiceName),
                ProcessId = processId,
                ConnectedAt = DateTime.UtcNow.ToString("o"),
                SkylineVersion = skylineVersion
            };
        }

        /// <summary>
        /// Write this connection info to a per-instance file in the deploy directory.
        /// The file name includes the pipe name so multiple Skyline instances can coexist.
        /// </summary>
        public void Save()
        {
            Directory.CreateDirectory(McpServerDeployer.DeployDir);
            string json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(JsonToolConstants.GetConnectionFilePath(PipeName), json);
        }

        /// <summary>
        /// Delete the connection file for a specific pipe name.
        /// </summary>
        public static void Delete(string pipeName)
        {
            string path = JsonToolConstants.GetConnectionFilePath(pipeName);
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// Load the most recently written connection file from the deploy directory.
        /// Scans for connection-*.json files (one per Skyline instance) and also
        /// checks the legacy connection.json for backward compatibility.
        /// </summary>
        public static ConnectionInfo Load()
        {
            string dir = McpServerDeployer.DeployDir;
            if (!Directory.Exists(dir))
                return null;

            // Find all connection files, sorted by write time descending
            var candidates = Directory.GetFiles(dir,
                    JsonToolConstants.CONNECTION_FILE_PREFIX + "*" + JsonToolConstants.CONNECTION_FILE_EXT)
                .Select(f => new FileInfo(f))
                .ToList();

            // Also check legacy file
            string legacyPath = Path.Combine(dir, LEGACY_CONNECTION_FILE);
            if (File.Exists(legacyPath))
                candidates.Add(new FileInfo(legacyPath));

            if (candidates.Count == 0)
                return null;

            // Try most recent first
            foreach (var file in candidates.OrderByDescending(f => f.LastWriteTimeUtc))
            {
                try
                {
                    string json = File.ReadAllText(file.FullName);
                    var info = JsonSerializer.Deserialize<ConnectionInfo>(json);
                    if (info != null && !string.IsNullOrEmpty(info.PipeName))
                        return info;
                }
                catch
                {
                    // Skip malformed files
                }
            }

            return null;
        }
    }
}
