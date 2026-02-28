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
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkylineMcpServer;

public class SkylineConnection : IDisposable
{
    private readonly NamedPipeClientStream _pipe;

    private SkylineConnection(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
    }

    public static SkylineConnection Connect()
    {
        string connectionFile = GetConnectionFilePath();
        if (!File.Exists(connectionFile))
        {
            throw new InvalidOperationException(
                "Skyline is not connected. Launch 'Connect to Claude' from Skyline's External Tools menu.");
        }

        string json = File.ReadAllText(connectionFile);
        var info = JsonSerializer.Deserialize<ConnectionInfo>(json);
        if (info == null)
        {
            throw new InvalidOperationException(
                "Invalid connection file. Launch 'Connect to Claude' from Skyline's External Tools menu.");
        }

        // Validate that the Skyline process is still alive
        try
        {
            Process.GetProcessById(info.ProcessId);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"Skyline process {info.ProcessId} is no longer running. Launch 'Connect to Claude' from Skyline to reconnect.");
        }

        // Connect to the bridge's JSON named pipe
        var pipe = new NamedPipeClientStream(".", info.PipeName, PipeDirection.InOut);
        try
        {
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;
        }
        catch (TimeoutException)
        {
            pipe.Dispose();
            throw new InvalidOperationException(
                "Skyline is not responding. It may be busy processing data or showing a dialog. Try again in a moment.");
        }
        catch (IOException)
        {
            pipe.Dispose();
            throw new InvalidOperationException(
                "Connection to Skyline was lost. Launch 'Connect to Claude' from Skyline to reconnect.");
        }

        return new SkylineConnection(pipe);
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

    private static string GetConnectionFilePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Skyline", "mcp", "connection.json");
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

    // POCO matching the connector's connection.json format
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

        [JsonPropertyName("document_path")]
        public string DocumentPath { get; set; } = string.Empty;
    }
}
