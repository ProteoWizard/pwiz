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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkylineMcpConnector
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

        [JsonPropertyName("document_path")]
        public string DocumentPath { get; set; }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static string GetConnectionDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Skyline", "mcp");
        }

        public static string GetConnectionFilePath()
        {
            return Path.Combine(GetConnectionDirectory(), "connection.json");
        }

        public void Save()
        {
            string directory = GetConnectionDirectory();
            Directory.CreateDirectory(directory);
            string json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(GetConnectionFilePath(), json);
        }

        public static void Delete()
        {
            string path = GetConnectionFilePath();
            if (File.Exists(path))
                File.Delete(path);
        }

        public static ConnectionInfo Load()
        {
            string path = GetConnectionFilePath();
            if (!File.Exists(path))
                return null;
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ConnectionInfo>(json);
        }
    }
}
