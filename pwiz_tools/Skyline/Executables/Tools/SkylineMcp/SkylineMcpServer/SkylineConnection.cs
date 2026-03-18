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
using SkylineTool;
using JSON = SkylineTool.JsonToolConstants.JSON;

namespace SkylineMcpServer;

public class SkylineConnection : IDisposable
{
    private readonly NamedPipeClientStream _pipe;

    /// <summary>
    /// Identity string from the connected Skyline instance (e.g.,
    /// "Skyline-daily (64-bit) 26.1.1.238"). Readable after Dispose
    /// for error enrichment.
    /// </summary>
    public string SkylineVersion { get; private set; }

    /// <summary>
    /// When set, TryConnect targets this specific Skyline process instead of the
    /// most recently started one. Set via the skyline_set_instance MCP tool.
    /// </summary>
    public static int? TargetProcessId { get; set; }

    /// <summary>
    /// When true, requests include "log": true to enable diagnostic logging.
    /// Set via the skyline_set_logging MCP tool.
    /// </summary>
    public static bool LoggingEnabled { get; set; }

    /// <summary>
    /// Diagnostic log content from the most recent response, or null if absent.
    /// </summary>
    public static string LastLog { get; set; }

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
        if (infos.Count > 0)
            return null; // At least one live Skyline instance

        return "No Skyline instance is connected. " +
               "Start Skyline and choose Tools > AI Connector to connect.";
    }

    /// <summary>
    /// Connect to a Skyline instance. If TargetProcessId is set, connects to that
    /// specific instance; otherwise connects to the most recently started one.
    /// Returns null with a status message when no Skyline is available.
    /// </summary>
    public static (SkylineConnection Connection, string Error) TryConnect()
    {
        var infos = FindConnectionFiles();
        if (infos.Count == 0)
        {
            return (null, "No Skyline instance is connected. " +
                          "Start Skyline and choose Tools > AI Connector to connect.");
        }

        // If a specific instance is targeted, try it first
        if (TargetProcessId.HasValue)
        {
            var targeted = infos.FirstOrDefault(i => i.ProcessId == TargetProcessId.Value);
            if (targeted != null)
                return TryConnectToInstance(targeted);
            TargetProcessId = null; // Target no longer exists
        }

        // Sort by connected_at descending to prefer the most recent
        foreach (var info in infos.OrderByDescending(i => i.ConnectedAt))
        {
            var result2 = TryConnectToInstance(info);
            if (result2.Connection != null)
                return result2;

            if (result2.Error != null && result2.Error.Contains("not responding"))
                return result2;
        }

        // All connections failed
        return (null, "No Skyline instance is connected. " +
                      "Start Skyline and choose Tools > AI Connector to connect.");
    }

    /// <summary>
    /// Get information about all available Skyline instances, including document paths
    /// queried from each live instance.
    /// </summary>
    public static List<InstanceInfo> GetAvailableInstances()
    {
        var infos = FindConnectionFiles();
        var results = new List<InstanceInfo>();

        foreach (var info in infos)
        {
            string documentPath = null;
            try
            {
                var (connection, _) = TryConnectToInstance(info);
                if (connection != null)
                {
                    using (connection)
                    {
                        documentPath = connection.Call(nameof(IJsonToolService.GetDocumentPath));
                    }
                }
            }
            catch
            {
                // Best effort — document path will be null
            }

            results.Add(new InstanceInfo
            {
                ProcessId = info.ProcessId,
                SkylineVersion = info.SkylineVersion,
                ConnectedAt = info.ConnectedAt,
                DocumentPath = documentPath,
                IsTargeted = TargetProcessId.HasValue && TargetProcessId.Value == info.ProcessId
            });
        }

        return results;
    }

    private static (SkylineConnection Connection, string Error) TryConnectToInstance(ConnectionInfo info)
    {
        var pipe = new NamedPipeClientStream(".", info.PipeName, PipeDirection.InOut);
        try
        {
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;
            return (new SkylineConnection(pipe) { SkylineVersion = info.SkylineVersion }, null);
        }
        catch (TimeoutException)
        {
            pipe.Dispose();
            return (null, "Skyline is not responding. " +
                          "It may be busy processing data or showing a dialog. Try again in a moment.");
        }
        catch (IOException)
        {
            pipe.Dispose();
            return (null, null); // Connection failed silently — try next
        }
    }

    public string Call(string method, params string[] args)
    {
        object request = LoggingEnabled
            ? (object)new { method, args, log = true }
            : new { method, args };
        byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        _pipe.Write(requestBytes, 0, requestBytes.Length);
        _pipe.Flush();
        _pipe.WaitForPipeDrain();

        byte[] responseBytes = ReadAllBytes(_pipe);
        string responseJson = Encoding.UTF8.GetString(responseBytes);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        LastLog = root.TryGetProperty(nameof(JSON.log), out var logElement)
            ? logElement.GetString()
            : null;

        if (root.TryGetProperty(nameof(JSON.error), out var errorElement))
        {
            string error = errorElement.GetString();
            throw new InvalidOperationException(error ?? "Unknown error from Skyline");
        }

        if (root.TryGetProperty(nameof(JSON.result), out var resultElement))
        {
            if (resultElement.ValueKind == JsonValueKind.Null)
                return null;
            if (resultElement.ValueKind == JsonValueKind.Number)
                return resultElement.GetRawText();
            if (resultElement.ValueKind == JsonValueKind.Object ||
                resultElement.ValueKind == JsonValueKind.Array)
                return resultElement.GetRawText();
            return resultElement.GetString();
        }

        return null;
    }

    /// <summary>
    /// System.Text.Json options configured with snake_case naming to match
    /// the PascalCase POCO properties to the snake_case JSON wire format.
    /// </summary>
    private static readonly JsonSerializerOptions _snakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Call a method on the Skyline JSON tool service and deserialize the
    /// result into a typed POCO. The wire format remains JSON strings over
    /// named pipes; this method handles the deserialization.
    /// </summary>
    public T CallTyped<T>(string method, params string[] args)
    {
        string json = Call(method, args);
        if (string.IsNullOrEmpty(json))
            return default;
        return JsonSerializer.Deserialize<T>(json, _snakeCaseOptions);
    }

    public void Dispose()
    {
        _pipe.Dispose();
    }

    /// <summary>
    /// Find all connection files, cleaning up stale entries whose processes are no longer running.
    /// </summary>
    private static List<ConnectionInfo> FindConnectionFiles()
    {
        string dir = JsonToolConstants.GetConnectionDirectory();
        if (!Directory.Exists(dir))
            return new List<ConnectionInfo>();

        var results = new List<ConnectionInfo>();

        // Scan for connection-*.json files, cleaning up stale entries
        foreach (string file in Directory.GetFiles(dir,
            JsonToolConstants.CONNECTION_FILE_PREFIX + "*" + JsonToolConstants.CONNECTION_FILE_EXT))
        {
            var info = TryLoadConnectionFile(file);
            if (info == null)
                continue;
            if (IsSkylineProcess(info.ProcessId))
                results.Add(info);
            else
                TryDeleteFile(file);
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

    private static bool IsSkylineProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return Program.FunctionalTest ||
                   process.ProcessName.StartsWith("Skyline", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch { /* Best effort */ }
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

    public class InstanceInfo
    {
        public int ProcessId { get; set; }
        public string SkylineVersion { get; set; }
        public string ConnectedAt { get; set; }
        public string DocumentPath { get; set; }
        public bool IsTargeted { get; set; }
    }
}
