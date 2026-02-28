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
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using SkylineTool;

namespace SkylineMcpConnector
{
    public class JsonPipeServer : IDisposable
    {
        private readonly SkylineToolClient _client;
        private readonly string _pipeName;
        private readonly Thread _serverThread;
        private volatile bool _stopping;

        public string PipeName { get { return _pipeName; } }

        public JsonPipeServer(SkylineToolClient client)
        {
            _client = client;
            _pipeName = "SkylineMcpBridge-" + Guid.NewGuid().ToString("N");
            _serverThread = new Thread(ServerLoop) { IsBackground = true };
        }

        public void Start()
        {
            _serverThread.Start();
        }

        public void Dispose()
        {
            _stopping = true;
            // Connect to unblock WaitForConnection, then close
            try
            {
                using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut))
                {
                    client.Connect(500);
                }
            }
            catch
            {
                // Expected when shutting down
            }
        }

        private void ServerLoop()
        {
            while (!_stopping)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Message))
                    {
                        pipe.WaitForConnection();
                        if (_stopping)
                            break;

                        // Handle requests on this connection until client disconnects
                        while (pipe.IsConnected && !_stopping)
                        {
                            try
                            {
                                byte[] requestBytes = ReadAllBytes(pipe);
                                if (requestBytes.Length == 0)
                                    break;

                                string responseJson = HandleRequest(requestBytes);
                                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                                pipe.Write(responseBytes, 0, responseBytes.Length);
                                pipe.Flush();
                                pipe.WaitForPipeDrain();
                            }
                            catch (IOException)
                            {
                                break; // Client disconnected
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    if (!_stopping)
                        Thread.Sleep(100); // Brief pause before retrying
                }
            }
        }

        private string HandleRequest(byte[] requestBytes)
        {
            try
            {
                string json = Encoding.UTF8.GetString(requestBytes);
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    string method = root.GetProperty("method").GetString();
                    string[] args = root.TryGetProperty("args", out var argsElement)
                        ? ParseArgs(argsElement)
                        : Array.Empty<string>();

                    object result = Dispatch(method, args);
                    return SerializeResult(result);
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        private object Dispatch(string method, string[] args)
        {
            switch (method)
            {
                case "QueryAvailableMethods":
                    return string.Join(",", new[]
                    {
                        "GetDocumentPath", "GetVersion", "GetDocumentLocationName",
                        "GetReplicateName", "GetProcessId", "GetReport",
                        "GetReportFromDefinition", "GetSelectedElementLocator"
                    });

                case "GetDocumentPath":
                    string path = _client.GetDocumentPath();
                    return path != null ? path.Replace('\\', '/') : null;

                case "GetVersion":
                    var version = _client.GetSkylineVersion();
                    return version != null ? version.ToString() : null;

                case "GetDocumentLocationName":
                    return _client.GetDocumentLocationName();

                case "GetReplicateName":
                    return _client.GetReplicateName();

                case "GetProcessId":
                    return _client.GetProcessId();

                case "GetReport":
                    RequireArgs(method, args, 1);
                    return ReportToCsv(_client.GetReport(args[0]));

                case "GetReportFromDefinition":
                    RequireArgs(method, args, 1);
                    return ReportToCsv(_client.GetReportFromDefinition(args[0]));

                case "GetSelectedElementLocator":
                    RequireArgs(method, args, 1);
                    return _client.GetSelectedElementLocator(args[0]);

                default:
                    throw new ArgumentException("Unknown method: " + method);
            }
        }

        private static string SerializeResult(object result)
        {
            if (result == null)
                return JsonSerializer.Serialize(new { result = (string)null });
            if (result is int intVal)
                return JsonSerializer.Serialize(new { result = intVal });
            if (result is string strVal)
                return JsonSerializer.Serialize(new { result = strVal });
            return JsonSerializer.Serialize(new { result = result.ToString() });
        }

        private static string ReportToCsv(IReport report)
        {
            if (report == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", report.ColumnNames.Select(QuoteCsvField)));
            foreach (var row in report.Cells)
            {
                sb.AppendLine(string.Join(",", row.Select(QuoteCsvField)));
            }
            return sb.ToString();
        }

        private static string QuoteCsvField(string field)
        {
            if (field == null)
                return string.Empty;
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        private static string[] ParseArgs(JsonElement argsElement)
        {
            if (argsElement.ValueKind == JsonValueKind.Array)
            {
                int count = argsElement.GetArrayLength();
                var args = new string[count];
                for (int i = 0; i < count; i++)
                {
                    args[i] = argsElement[i].GetString();
                }
                return args;
            }
            return Array.Empty<string>();
        }

        private static void RequireArgs(string method, string[] args, int count)
        {
            if (args.Length < count)
                throw new ArgumentException(method + " requires " + count + " argument(s)");
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
    }
}
