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
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Documentation;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// JSON named pipe server hosted inside the Skyline process.
    /// Replaces the connector's JsonPipeServer by dispatching directly
    /// to ToolService methods without BinaryFormatter serialization.
    /// </summary>
    public class JsonToolServer : IDisposable
    {
        private const string DEPLOY_FOLDER_NAME = @".skyline-mcp";
        private const string CONNECTION_FILE_NAME = @"connection.json";

        private static readonly string[] AVAILABLE_METHODS =
        {
            @"GetDocumentPath",
            @"GetVersion",
            @"GetDocumentLocationName",
            @"GetReplicateName",
            @"GetProcessId",
            @"ExportReport",
            @"ExportReportFromDefinition",
            @"GetSelectedElementLocator",
            @"RunCommand",
            @"RunCommandSilent",
            @"GetSettingsListTypes",
            @"GetSettingsListNames",
            @"GetSettingsListItem",
            @"GetReportDocTopics",
            @"GetReportDocTopic",
            @"InsertSmallMoleculeTransitionList",
            @"ImportFasta",
            @"ImportProperties"
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
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DEPLOY_FOLDER_NAME);
        }

        private static string GetConnectionFilePath()
        {
            return Path.Combine(GetConnectionDirectory(), CONNECTION_FILE_NAME);
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

                case "ExportReport":
                    RequireArgs(method, args, 2);
                    return ExportNamedReport(args);

                case "ExportReportFromDefinition":
                    RequireArgs(method, args, 2);
                    return ExportDefinitionReport(args);

                case "GetSelectedElementLocator":
                    RequireArgs(method, args, 1);
                    return _toolService.GetSelectedElementLocator(args[0]);

                case "RunCommand":
                    RequireArgs(method, args, 1);
                    return _toolService.RunCommand(args[0]);

                case "RunCommandSilent":
                    RequireArgs(method, args, 1);
                    return _toolService.RunCommand(args[0], true);

                case "GetSettingsListTypes":
                    return _toolService.GetSettingsListTypes();

                case "GetSettingsListNames":
                    RequireArgs(method, args, 1);
                    return _toolService.GetSettingsListNames(args[0]);

                case "GetSettingsListItem":
                    RequireArgs(method, args, 2);
                    return _toolService.GetSettingsListItem(args[0], args[1]);

                case "GetReportDocTopics":
                    return GetReportDocTopics();

                case "GetReportDocTopic":
                    RequireArgs(method, args, 1);
                    return GetReportDocTopic(args[0]);

                case "InsertSmallMoleculeTransitionList":
                    RequireArgs(method, args, 1);
                    return InvokeOnUiThread(() =>
                        Program.MainWindow.InsertSmallMoleculeTransitionList(args[0],
                            @"Insert small molecule transition list"));

                case "ImportFasta":
                    RequireArgs(method, args, 1);
                    return InvokeOnUiThread(() =>
                        Program.MainWindow.ImportFasta(new StringReader(args[0]),
                            Helpers.CountLinesInString(args[0]), false,
                            @"Import FASTA from MCP",
                            new SkylineWindow.ImportFastaInfo(false, args[0])));

                case "ImportProperties":
                    RequireArgs(method, args, 1);
                    return InvokeOnUiThread(() =>
                        Program.MainWindow.ImportAnnotations(new StringReader(args[0]),
                            new MessageInfo(MessageType.imported_annotations,
                                Program.MainWindow.Document.DocumentType,
                                @"Import properties from MCP")));

                default:
                    throw new ArgumentException(@"Unknown method: " + method);
            }
        }

        private string ExportNamedReport(string[] args)
        {
            string reportName = args[0];
            string filePath = args[1];
            var localizer = ParseCulture(args, 2);

            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer);
            var rowFactories = RowFactories.GetRowFactories(CancellationToken.None, dataSchema);

            var viewName = FindReportViewName(reportName);
            string ext = Path.GetExtension(filePath);
            var exporter = ReportExporters.ForFilenameExtension(localizer, ext, TextUtil.EXT_CSV);

            DirectoryEx.CreateForFilePath(filePath);

            using (var saver = new FileSaver(filePath, true))
            {
                if (!saver.CanSave())
                    throw new IOException(@"Cannot write to " + filePath);
                IProgressStatus status = new ProgressStatus(string.Empty);
                rowFactories.ExportReport(saver.Stream, viewName, exporter,
                    Program.MainWindow, ref status);
                saver.Commit();
            }

            return BuildReportMetadata(filePath, reportName);
        }

        private string ExportDefinitionReport(string[] args)
        {
            string xml = args[0];
            string filePath = args[1];
            var localizer = ParseCulture(args, 2);

            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            var reportOrViewSpecList = ReportSharing.DeserializeReportList(memoryStream);
            if (reportOrViewSpecList.Count == 0)
                throw new ArgumentException(@"No report definition found");
            if (reportOrViewSpecList.Count > 1)
                throw new ArgumentException(@"Too many report definitions");
            var reportOrViewSpec = reportOrViewSpecList.First();
            if (null == reportOrViewSpec.ViewSpecLayout)
                throw new ArgumentException(@"The report definition uses the old format.");

            var viewSpecLayout = reportOrViewSpec.ViewSpecLayout;
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer);
            var rowFactories = RowFactories.GetRowFactories(CancellationToken.None, dataSchema);

            string ext = Path.GetExtension(filePath);
            var exporter = ReportExporters.ForFilenameExtension(localizer, ext, TextUtil.EXT_CSV);

            DirectoryEx.CreateForFilePath(filePath);

            using (var saver = new FileSaver(filePath, true))
            {
                if (!saver.CanSave())
                    throw new IOException(@"Cannot write to " + filePath);
                IProgressStatus status = new ProgressStatus(string.Empty);
                rowFactories.ExportReport(saver.Stream, viewSpecLayout.ViewSpec,
                    viewSpecLayout.DefaultViewLayout, exporter, Program.MainWindow, ref status);
                saver.Commit();
            }

            string reportName = viewSpecLayout.Name ?? @"Custom";
            return BuildReportMetadata(filePath, reportName);
        }

        private static string BuildReportMetadata(string filePath, string reportName)
        {
            var obj = new JObject();
            obj[@"file_path"] = filePath.Replace('\\', '/');
            obj[@"report_name"] = reportName;

            string ext = Path.GetExtension(filePath);
            if (string.Equals(ext, TextUtil.EXT_PARQUET, StringComparison.OrdinalIgnoreCase))
            {
                obj[@"format"] = @"parquet";
                return obj.ToString(Newtonsoft.Json.Formatting.None);
            }

            var previewLines = new string[6];
            int lineCount = 0;
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (lineCount < 6)
                        previewLines[lineCount] = line;
                    lineCount++;
                }
            }

            int rowCount = Math.Max(0, lineCount - 1);
            obj[@"row_count"] = rowCount;

            if (lineCount > 0)
                obj[@"columns"] = previewLines[0];

            int previewCount = Math.Min(lineCount, 6);
            if (previewCount > 0)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < previewCount; i++)
                {
                    if (i > 0)
                        sb.AppendLine();
                    sb.Append(previewLines[i]);
                }
                obj[@"preview"] = sb.ToString();
            }

            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static DataSchemaLocalizer ParseCulture(string[] args, int index)
        {
            if (args.Length > index && string.Equals(args[index], @"localized", StringComparison.OrdinalIgnoreCase))
                return SkylineDataSchema.GetLocalizedSchemaLocalizer();
            return DataSchemaLocalizer.INVARIANT;
        }

        private static ViewName FindReportViewName(string reportName)
        {
            var persistedViews = Settings.Default.PersistedViews;
            if (persistedViews.GetViewSpecList(PersistedViews.MainGroup.Id).GetView(reportName) != null)
                return PersistedViews.MainGroup.Id.ViewName(reportName);
            if (persistedViews.GetViewSpecList(PersistedViews.ExternalToolsGroup.Id).GetView(reportName) != null)
                return PersistedViews.ExternalToolsGroup.Id.ViewName(reportName);
            throw new ArgumentException(@"Report not found: " + reportName);
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

        private string InvokeOnUiThread(Action action)
        {
            string error = null;
            Program.MainWindow.Invoke(new Action(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }));
            return error ?? @"OK";
        }

        private string GetReportDocTopics()
        {
            string html = GenerateReportDocHtml();
            var sb = new StringBuilder();
            // Match <div id="qualified.name"><span class="RowType">DisplayName</span>
            var matches = Regex.Matches(html,
                @"<div\s+id=""([^""]+)""><span\s+class=""RowType"">([^<]+)</span>");
            foreach (Match match in matches)
            {
                string qualifiedName = WebUtility.HtmlDecode(match.Groups[1].Value);
                string displayName = WebUtility.HtmlDecode(match.Groups[2].Value);
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(displayName);
                sb.Append('\t');
                sb.Append(qualifiedName);
            }
            return sb.ToString();
        }

        private string GetReportDocTopic(string topicName)
        {
            string html = GenerateReportDocHtml();
            // Find all section boundaries
            var divMatches = Regex.Matches(html,
                @"<div\s+id=""([^""]+)""><span\s+class=""RowType"">([^<]+)</span>");
            // Strip spaces from the search term for flexible matching (e.g., "Transition Result" -> "TransitionResult")
            string normalizedTopic = topicName.Replace(@" ", string.Empty);
            int matchIndex = -1;
            // Pass 1: exact match on qualified type name or display name
            for (int i = 0; i < divMatches.Count; i++)
            {
                string qualifiedName = WebUtility.HtmlDecode(divMatches[i].Groups[1].Value);
                string displayName = WebUtility.HtmlDecode(divMatches[i].Groups[2].Value);
                if (string.Equals(qualifiedName, topicName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(displayName, normalizedTopic, StringComparison.OrdinalIgnoreCase))
                {
                    matchIndex = i;
                    break;
                }
            }
            // Pass 2: partial match on display name
            if (matchIndex < 0)
            {
                for (int i = 0; i < divMatches.Count; i++)
                {
                    string displayName = WebUtility.HtmlDecode(divMatches[i].Groups[2].Value);
                    if (displayName.IndexOf(normalizedTopic, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchIndex = i;
                        break;
                    }
                }
            }
            if (matchIndex < 0)
                return null;

            // Extract section HTML between this div and the next
            int sectionStart = divMatches[matchIndex].Index;
            int sectionEnd = matchIndex + 1 < divMatches.Count
                ? divMatches[matchIndex + 1].Index
                : html.Length;
            string sectionHtml = html.Substring(sectionStart, sectionEnd - sectionStart);

            string qualName = WebUtility.HtmlDecode(divMatches[matchIndex].Groups[1].Value);
            string dispName = WebUtility.HtmlDecode(divMatches[matchIndex].Groups[2].Value);

            // Convert HTML table to plain text
            var sb = new StringBuilder();
            sb.AppendLine(dispName + @" (" + qualName + @")");
            sb.AppendLine();
            sb.AppendLine(@"Name" + '\t' + @"Description" + '\t' + @"Type");

            // Extract table rows: <tr><td ...>Name</td><td ...>Description</td><td ...>Type</td></tr>
            var rowMatches = Regex.Matches(sectionHtml,
                @"<tr><td[^>]*>(.*?)</td><td[^>]*>(.*?)</td><td[^>]*>(.*?)</td>",
                RegexOptions.Singleline);
            foreach (Match row in rowMatches)
            {
                string name = StripHtmlTags(row.Groups[1].Value);
                string description = StripHtmlTags(row.Groups[2].Value);
                string type = StripHtmlTags(row.Groups[3].Value);
                sb.AppendLine(name + '\t' + description + '\t' + type);
            }
            return sb.ToString();
        }

        private string GenerateReportDocHtml()
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            var rootColumn = ColumnDescriptor.RootColumn(dataSchema, typeof(SkylineDocument));
            var generator = new DocumentationGenerator(rootColumn)
            {
                IncludeHidden = false
            };
            return generator.GenerateDocumentation(rootColumn);
        }

        private static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            string text = Regex.Replace(html, @"<[^>]+>", string.Empty);
            return WebUtility.HtmlDecode(text);
        }
    }
}
