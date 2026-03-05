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
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkylineMcpServer;

public class SkylineConnection : IDisposable
{
    private const string DEPLOY_FOLDER_NAME = ".skyline-mcp";
    private const string CONNECTION_FILE_PREFIX = "connection-";
    private const string CONNECTION_FILE_EXT = ".json";
    // Keep legacy name for backward compatibility during transition
    private const string LEGACY_CONNECTION_FILE = "connection.json";

    private readonly NamedPipeClientStream _pipe;

    private SkylineConnection(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
    }

    /// <summary>
    /// Human-readable status message describing the current Skyline connection state.
    /// Returns null when connected; returns a message when not connected.
    /// </summary>
    public static string GetConnectionStatus()
    {
        var infos = FindConnectionFiles();
        if (infos.Count == 0)
        {
            return "No Skyline instance is connected. " +
                   "Start Skyline and choose Tools > AI Connector to connect.";
        }

        // Try to find a live one
        foreach (var info in infos)
        {
            if (IsProcessAlive(info.ProcessId))
                return null; // At least one is alive — we can connect
        }

        // All stale
        CleanupStaleFiles(infos);
        return "No Skyline instance is connected. " +
               "Start Skyline and choose Tools > AI Connector to connect.";
    }

    /// <summary>
    /// Connect to the most recently started Skyline instance.
    /// Returns null with a status message when no Skyline is available,
    /// instead of throwing an exception.
    /// </summary>
    public static (SkylineConnection Connection, string Error) TryConnect()
    {
        var infos = FindConnectionFiles();
        if (infos.Count == 0)
        {
            return (null, "No Skyline instance is connected. " +
                          "Start Skyline and choose Tools > AI Connector to connect.");
        }

        // Sort by connected_at descending to prefer the most recent
        var sorted = infos.OrderByDescending(i => i.ConnectedAt).ToList();
        var stale = new List<ConnectionInfo>();

        foreach (var info in sorted)
        {
            if (!IsProcessAlive(info.ProcessId))
            {
                stale.Add(info);
                continue;
            }

            // Try to connect to the pipe
            var pipe = new NamedPipeClientStream(".", info.PipeName, PipeDirection.InOut);
            try
            {
                pipe.Connect(5000);
                pipe.ReadMode = PipeTransmissionMode.Message;
                CleanupStaleFiles(stale);
                return (new SkylineConnection(pipe), null);
            }
            catch (TimeoutException)
            {
                pipe.Dispose();
                // Skyline process is alive but not responding — may be busy
                CleanupStaleFiles(stale);
                return (null, "Skyline is not responding. " +
                              "It may be busy processing data or showing a dialog. Try again in a moment.");
            }
            catch (IOException)
            {
                pipe.Dispose();
                stale.Add(info);
            }
        }

        // All connections failed
        CleanupStaleFiles(stale);
        return (null, "No Skyline instance is connected. " +
                      "Start Skyline and choose Tools > AI Connector to connect.");
    }

    public string Call(string method, params string[] args)
    {
        var request = new { method, args };
        byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        _pipe.Write(requestBytes, 0, requestBytes.Length);
        _pipe.Flush();
        _pipe.WaitForPipeDrain();

        byte[] responseBytes = ReadAllBytes(_pipe);
        string responseJson = Encoding.UTF8.GetString(responseBytes);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorElement))
        {
            string error = errorElement.GetString();
            throw new InvalidOperationException(error ?? "Unknown error from Skyline");
        }

        if (root.TryGetProperty("result", out var resultElement))
        {
            if (resultElement.ValueKind == JsonValueKind.Null)
                return null;
            if (resultElement.ValueKind == JsonValueKind.Number)
                return resultElement.GetRawText();
            return resultElement.GetString();
        }

        return null;
    }

    public void Dispose()
    {
        _pipe.Dispose();
    }

    private static string GetConnectionDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            DEPLOY_FOLDER_NAME);
    }

    /// <summary>
    /// Find all connection files (both new pattern and legacy).
    /// </summary>
    private static List<ConnectionInfo> FindConnectionFiles()
    {
        string dir = GetConnectionDirectory();
        if (!Directory.Exists(dir))
            return new List<ConnectionInfo>();

        var results = new List<ConnectionInfo>();

        // Scan for connection-*.json files
        foreach (string file in Directory.GetFiles(dir, CONNECTION_FILE_PREFIX + "*" + CONNECTION_FILE_EXT))
        {
            var info = TryLoadConnectionFile(file);
            if (info != null)
                results.Add(info);
        }

        // Also check legacy connection.json
        string legacyPath = Path.Combine(dir, LEGACY_CONNECTION_FILE);
        if (File.Exists(legacyPath))
        {
            var info = TryLoadConnectionFile(legacyPath);
            if (info != null)
            {
                // Avoid duplicates if same pipe name
                if (!results.Any(r => r.PipeName == info.PipeName))
                    results.Add(info);
            }
        }

        return results;
    }

    private static ConnectionInfo TryLoadConnectionFile(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var info = JsonSerializer.Deserialize<ConnectionInfo>(json);
            if (info != null)
                info.FilePath = path;
            return info;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            Process.GetProcessById(processId);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Remove connection files for Skyline instances that are no longer running.
    /// </summary>
    private static void CleanupStaleFiles(List<ConnectionInfo> staleInfos)
    {
        foreach (var info in staleInfos)
        {
            if (string.IsNullOrEmpty(info.FilePath))
                continue;
            try
            {
                File.Delete(info.FilePath);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private static byte[] ReadAllBytes(PipeStream stream)
    {
        var memoryStream = new MemoryStream();
        do
        {
            var buffer = new byte[65536];
            int count = stream.Read(buffer, 0, buffer.Length);
            if (count == 0)
                return memoryStream.ToArray();
            memoryStream.Write(buffer, 0, count);
        } while (!stream.IsMessageComplete);
        return memoryStream.ToArray();
    }

    // POCO matching the Skyline connection file format
    private class ConnectionInfo
    {
        [JsonPropertyName("pipe_name")]
        public string PipeName { get; set; } = string.Empty;

        [JsonPropertyName("process_id")]
        public int ProcessId { get; set; }

        [JsonPropertyName("connected_at")]
        public string ConnectedAt { get; set; } = string.Empty;

        [JsonPropertyName("skyline_version")]
        public string SkylineVersion { get; set; } = string.Empty;

        [JsonIgnore]
        public string FilePath { get; set; }
    }
}
