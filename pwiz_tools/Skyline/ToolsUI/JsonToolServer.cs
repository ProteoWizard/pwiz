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
using System.Threading;
using Newtonsoft.Json.Linq;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// JSON named pipe server hosted inside the Skyline process.
    /// Replaces the connector's JsonPipeServer by dispatching directly
    /// to ToolService methods without BinaryFormatter serialization.
    /// </summary>
    public class JsonToolServer : IDisposable
    {
        private static readonly string[] AVAILABLE_METHODS =
        {
            @"GetDocumentPath",
            @"GetVersion",
            @"GetDocumentLocationName",
            @"GetReplicateName",
            @"GetProcessId",
            @"GetReport",
            @"GetReportFromDefinition",
            @"GetSelectedElementLocator",
            @"RunCommand",
            @"GetSettingsListTypes",
            @"GetSettingsListNames",
            @"GetSettingsListItem"
        };

        private readonly ToolService _toolService;
        private readonly string _pipeName;
        private readonly Thread _serverThread;
        private volatile bool _stopping;

        public string PipeName { get { return _pipeName; } }

        public JsonToolServer(ToolService toolService)
        {
            _toolService = toolService;
            _pipeName = @"SkylineMcpJson-" + Guid.NewGuid().ToString("N");
            _serverThread = new Thread(ServerLoop) { IsBackground = true };
        }

        public void Start()
        {
            WriteConnectionInfo();
            _serverThread.Start();
        }

        public void Dispose()
        {
            _stopping = true;
            // Connect to unblock WaitForConnection, then close
            try
            {
                using var client = new NamedPipeClientStream(@".", _pipeName, PipeDirection.InOut);
                client.Connect(500);
            }
            catch
            {
                // Expected when shutting down
            }
            DeleteConnectionInfo();
        }

        private void WriteConnectionInfo()
        {
            string directory = GetConnectionDirectory();
            Directory.CreateDirectory(directory);
            string documentPath = _toolService.GetDocumentPath();
            string skylineVersion = Install.Version ?? @"unknown";
            var info = new JObject
            {
                [@"pipe_name"] = _pipeName,
                [@"process_id"] = Process.GetCurrentProcess().Id,
                [@"connected_at"] = DateTime.UtcNow.ToString(@"o"),
                [@"skyline_version"] = skylineVersion,
                [@"document_path"] = documentPath != null ? documentPath.Replace('\\', '/') : null
            };
            File.WriteAllText(GetConnectionFilePath(), info.ToString());
        }

        private static void DeleteConnectionInfo()
        {
            try
            {
                string path = GetConnectionFilePath();
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static string GetConnectionDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, @"Skyline", @"mcp");
        }

        private static string GetConnectionFilePath()
        {
            return Path.Combine(GetConnectionDirectory(), @"connection.json");
        }

        private void ServerLoop()
        {
            while (!_stopping)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Message);
                    pipe.WaitForConnection();
                    if (_stopping)
                        break;

                    // Handle requests on this connection until client disconnects
                    while (pipe.IsConnected && !_stopping)
                    {
                        try
                        {
                            var requestBytes = ReadAllBytes(pipe);
                            if (requestBytes.Length == 0)
                                break;

                            string responseJson = HandleRequest(requestBytes);
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
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
                var root = JObject.Parse(json);
                string method = (string) root[@"method"];
                string[] args = ParseArgs(root[@"args"]);

                object result = Dispatch(method, args);
                return SerializeResult(result);
            }
            catch (Exception ex)
            {
                return new JObject { [@"error"] = ex.Message }.ToString();
            }
        }

        private object Dispatch(string method, string[] args)
        {
            switch (method)
            {
                case "QueryAvailableMethods":
                    return string.Join(@",", AVAILABLE_METHODS);

                case "GetDocumentPath":
                    string path = _toolService.GetDocumentPath();
                    return path?.Replace('\\', '/');

                case "GetVersion":
                    return Install.Version;

                case "GetDocumentLocationName":
                    return _toolService.GetDocumentLocationName();

                case "GetReplicateName":
                    return _toolService.GetReplicateName();

                case "GetProcessId":
                    return _toolService.GetProcessId();

                case "GetReport":
                    RequireArgs(method, args, 1);
                    return _toolService.GetReport(@"MCP", args[0]);

                case "GetReportFromDefinition":
                    RequireArgs(method, args, 1);
                    return _toolService.GetReportFromDefinition(args[0]);

                case "GetSelectedElementLocator":
                    RequireArgs(method, args, 1);
                    return _toolService.GetSelectedElementLocator(args[0]);

                case "RunCommand":
                    RequireArgs(method, args, 1);
                    return _toolService.RunCommand(args[0]);

                case "GetSettingsListTypes":
                    return _toolService.GetSettingsListTypes();

                case "GetSettingsListNames":
                    RequireArgs(method, args, 1);
                    return _toolService.GetSettingsListNames(args[0]);

                case "GetSettingsListItem":
                    RequireArgs(method, args, 2);
                    return _toolService.GetSettingsListItem(args[0], args[1]);

                default:
                    throw new ArgumentException(@"Unknown method: " + method);
            }
        }

        private static string SerializeResult(object result)
        {
            var obj = new JObject
            {
                [@"result"] = result switch
                {
                    null => null,
                    int intVal => intVal,
                    string strVal => strVal,
                    _ => result.ToString()
                }
            };
            return obj.ToString();
        }

        private static string[] ParseArgs(JToken argsToken)
        {
            if (argsToken == null || argsToken.Type != JTokenType.Array)
                return Array.Empty<string>();
            var array = (JArray)argsToken;
            var args = new string[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                args[i] = (string)array[i];
            }
            return args;
        }

        private static void RequireArgs(string method, string[] args, int count)
        {
            if (args.Length < count)
                throw new ArgumentException(method + @" requires " + count + @" argument(s)");
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
