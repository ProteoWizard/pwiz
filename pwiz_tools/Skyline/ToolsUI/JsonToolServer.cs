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
using System.Collections;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Documentation;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
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

        private readonly ToolService _toolService;
        private readonly string _pipeName;
        private readonly Thread _serverThread;
        private readonly Dictionary<string, MethodInfo> _methods;
        private volatile bool _stopping;

        public string PipeName { get { return _pipeName; } }

        public JsonToolServer(ToolService toolService)
        {
            _toolService = toolService;
            _pipeName = @"SkylineMcpJson-" + Guid.NewGuid().ToString(@"N");
            _serverThread = new Thread(ServerLoop) { IsBackground = true };
            _methods = GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.ReturnType == typeof(string) &&
                            m.GetParameters().All(p => p.ParameterType == typeof(string)))
                .ToDictionary(m => m.Name);
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
                [@"document_path"] = documentPath.ToForwardSlashPath()
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
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return new JObject { [@"error"] = ex.InnerException.ToString() }.ToString();
            }
            catch (Exception ex)
            {
                return new JObject { [@"error"] = ex.ToString() }.ToString();
            }
        }

        private object Dispatch(string method, string[] args)
        {
            if (method == @"QueryAvailableMethods")
                return string.Join(@",", _methods.Keys.OrderBy(k => k));

            if (!_methods.TryGetValue(method, out var methodInfo))
                throw new ArgumentException(@"Unknown method: " + method);

            var parameters = methodInfo.GetParameters();
            int requiredCount = parameters.Count(p => !p.HasDefaultValue);
            if (args.Length < requiredCount)
            {
                throw new ArgumentException(
                    string.Format(@"{0} requires at least {1} argument(s)", method, requiredCount));
            }

            var invokeArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                invokeArgs[i] = i < args.Length ? args[i] : parameters[i].DefaultValue;

            return methodInfo.Invoke(this, invokeArgs);
        }

        // 0-arg methods

        public string GetDocumentPath()
        {
            return _toolService.GetDocumentPath().ToForwardSlashPath();
        }

        public string GetVersion()
        {
            return Install.Version;
        }

        public string GetSelectionText()
        {
            return _toolService.GetDocumentLocationName();
        }

        public string GetSelection()
        {
            return JsonUiService.GetSelection();
        }

        public string GetReplicateName()
        {
            return _toolService.GetReplicateName();
        }

        public string GetReplicateNames()
        {
            var doc = Program.MainWindow.Document;
            var measuredResults = doc.Settings.MeasuredResults;
            if (measuredResults == null)
                return string.Empty;
            var sb = new StringBuilder();
            foreach (var chromSet in measuredResults.Chromatograms)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(chromSet.Name);
            }
            return sb.ToString();
        }

        public string GetProcessId()
        {
            return _toolService.GetProcessId().ToString();
        }

        public string GetSettingsListTypes()
        {
            var sb = new StringBuilder();
            foreach (var prop in typeof(Settings).GetProperties())
            {
                if (!IsSettingsListBase(prop.PropertyType))
                    continue;
                string title;
                try
                {
                    var list = prop.GetValue(Settings.Default) as IListEditorSupport;
                    title = list?.Title ?? prop.Name;
                }
                catch
                {
                    title = prop.Name;
                }
                sb.Append(prop.Name).Append('\t').AppendLine(title);
            }
            sb.Append(@"PersistedViews").Append('\t').AppendLine(@"Reports");
            return sb.ToString();
        }

        public string GetDocumentStatus()
        {
            var doc = Program.MainWindow.Document;
            string mode = doc.DocumentType.ToString();
            int groups = doc.MoleculeGroupCount;
            int molecules = doc.MoleculeCount;
            int precursors = doc.MoleculeTransitionGroupCount;
            int transitions = doc.MoleculeTransitionCount;
            int replicates = doc.Settings.MeasuredResults?.Chromatograms.Count ?? 0;
            string docPath = _toolService.GetDocumentPath();
            string docDisplay = string.IsNullOrEmpty(docPath)
                ? @"(unsaved)"
                : docPath.ToForwardSlashPath();

            return TextUtil.LineSeparate($@"Mode: {mode}",
                $@"Proteins/Lists: {groups}",
                $@"Peptides/Molecules: {molecules}",
                $@"Precursors: {precursors}",
                $@"Transitions: {transitions}",
                $@"Replicates: {replicates}",
                $@"Document: {docDisplay}");
        }

        public string GetAvailableTutorials()
        {
            return JsonTutorialCatalog.FormatCatalog();
        }

        public string GetReportDocTopics()
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

        // 1-arg methods

        public string GetSelectedElementLocator(string elementType)
        {
            return _toolService.GetSelectedElementLocator(elementType);
        }

        public string RunCommand(string commandArgs)
        {
            return RunCommandImpl(commandArgs, false);
        }

        public string RunCommandSilent(string commandArgs)
        {
            return RunCommandImpl(commandArgs, true);
        }

        public string GetSettingsListNames(string listType)
        {
            if (listType == @"PersistedViews")
                return GetPersistedViewNames();

            var prop = typeof(Settings).GetProperty(listType);
            if (prop == null)
                throw new ArgumentException(@"Unknown settings list type: " + listType);
            var value = prop.GetValue(Settings.Default);
            if (value == null)
                throw new ArgumentException(@"Settings list is null: " + listType);
            var sb = new StringBuilder();
            foreach (var item in (IEnumerable)value)
            {
                var keyContainer = item as IKeyContainer<string>;
                if (keyContainer != null)
                    sb.AppendLine(keyContainer.GetKey());
            }
            return sb.ToString();
        }

        public string GetReportDocTopic(string topicName)
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

        public string GetLocations(string level, string rootLocator = null)
        {
            var document = Program.MainWindow.Document;
            var elementRefs = new ElementRefs(document);

            // Parse level to target depth in the tree
            int targetDepth;
            switch (level.ToLowerInvariant())
            {
                case @"list":
                case @"group":
                    targetDepth = 1;
                    break;
                case @"molecule":
                    targetDepth = 2;
                    break;
                case @"precursor":
                    targetDepth = 3;
                    break;
                case @"transition":
                    targetDepth = 4;
                    break;
                default:
                    throw new ArgumentException(new LlmInstruction(
                        string.Format(@"Invalid level '{0}'. Use: group, molecule, precursor, transition.", level)));
            }

            // Resolve the root element
            IdentityPath rootPath;
            int rootDepth;
            if (string.IsNullOrEmpty(rootLocator))
            {
                rootPath = IdentityPath.ROOT;
                rootDepth = 0;
            }
            else
            {
                var elementRef = ElementRefs.FromObjectReference(ElementLocator.Parse(rootLocator));
                if (!(elementRef is NodeRef nodeRef))
                {
                    throw new ArgumentException(new LlmInstruction(
                        @"The rootLocator must refer to a document node."));
                }
                rootPath = nodeRef.ToIdentityPath(document);
                if (rootPath == null)
                {
                    throw new ArgumentException(new LlmInstruction(
                        string.Format(@"Element not found: {0}", rootLocator)));
                }
                rootDepth = rootPath.Length;
            }

            if (targetDepth <= rootDepth)
            {
                throw new ArgumentException(new LlmInstruction(
                    string.Format(@"Level '{0}' must be deeper than the root element.", level)));
            }

            var sb = new StringBuilder();
            EnumerateAtDepth(document, elementRefs,
                (DocNodeParent)document.FindNode(rootPath),
                rootPath, rootDepth, targetDepth, sb);
            return sb.ToString();
        }

        private static void EnumerateAtDepth(SrmDocument document, ElementRefs elementRefs,
            DocNodeParent currentNode, IdentityPath currentPath,
            int currentDepth, int targetDepth, StringBuilder sb)
        {
            if (currentNode == null)
                return;

            if (currentDepth + 1 == targetDepth)
            {
                // Children of currentNode are at the target depth
                foreach (var child in currentNode.Children)
                {
                    var childPath = new IdentityPath(currentPath, child.Id);
                    var nodeRef = elementRefs.GetNodeRef(childPath);
                    if (nodeRef == null)
                        continue;
                    if (sb.Length > 0)
                        sb.AppendLine();
                    sb.Append(nodeRef.Name);
                    sb.Append('\t');
                    sb.Append(nodeRef);
                }
            }
            else
            {
                // Recurse through children to reach target depth
                foreach (var child in currentNode.Children)
                {
                    if (!(child is DocNodeParent childParent))
                        continue;
                    var childPath = new IdentityPath(currentPath, child.Id);
                    EnumerateAtDepth(document, elementRefs, childParent,
                        childPath, currentDepth + 1, targetDepth, sb);
                }
            }
        }

        public string AddReportFromDefinition(string json)
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            var root = ParseJsonDefinition(json);
            var viewSpec = ResolveJsonReportDefinition(root, dataSchema);

            if (string.IsNullOrEmpty(viewSpec.Name) || viewSpec.Name == @"Custom")
            {
                throw new ArgumentException(new LlmInstruction(
                    @"The 'name' field is required when adding a report."));
            }

            var groupId = PersistedViews.MainGroup.Id;
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(groupId);
            var layout = new ViewSpecLayout(viewSpec, ViewLayoutList.EMPTY);
            viewSpecList = viewSpecList.ReplaceView(viewSpec.Name, layout);
            Settings.Default.PersistedViews.SetViewSpecList(groupId, viewSpecList);

            return new LlmInstruction(
                string.Format(@"Report {0} has been added to Skyline.", viewSpec.Name.SingleQuote()));
        }

        public string InsertSmallMoleculeTransitionList(string textCSV)
        {
            return JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.InsertSmallMoleculeTransitionList(textCSV,
                    @"Insert small molecule transition list"));
        }

        public string ImportFasta(string textFasta)
        {
            return JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.ImportFasta(new StringReader(textFasta),
                    Helpers.CountLinesInString(textFasta), false,
                    @"Import FASTA from MCP",
                    new SkylineWindow.ImportFastaInfo(false, textFasta)));
        }

        public string ImportProperties(string csvText)
        {
            return JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.ImportAnnotations(new StringReader(csvText),
                    new MessageInfo(MessageType.imported_annotations,
                        Program.MainWindow.Document.DocumentType,
                        @"Import properties from MCP")));
        }

        public string SetSelectedElement(string elementLocatorString, string additionalLocators = null)
        {
            return JsonUiService.SetSelection(elementLocatorString, additionalLocators);
        }

        public string SetReplicate(string replicateName)
        {
            return JsonUiService.SetReplicate(replicateName);
        }

        public string GetOpenForms()
        {
            return JsonUiService.GetOpenForms();
        }

        public string GetGraphData(string graphId, string filePath = null)
        {
            return JsonUiService.GetGraphData(graphId, filePath);
        }

        public string GetGraphImage(string graphId, string filePath = null)
        {
            return JsonUiService.GetGraphImage(graphId, filePath);
        }

        // Multi-arg methods

        public string ExportReport(string reportName, string filePath, string culture)
        {
            return ExportNamedReport(reportName, filePath, ParseCulture(culture));
        }

        public string ExportReportFromDefinition(string json, string filePath, string culture)
        {
            return ExportJsonDefinitionReport(json, filePath, ParseCulture(culture));
        }

        public string GetSettingsListItem(string listType, string itemName)
        {
            if (listType == @"PersistedViews")
                return GetPersistedViewItem(itemName);

            var prop = typeof(Settings).GetProperty(listType);
            if (prop == null)
                throw new ArgumentException(@"Unknown settings list type: " + listType);
            var value = prop.GetValue(Settings.Default);
            if (value == null)
                throw new ArgumentException(@"Settings list is null: " + listType);
            foreach (var item in (IEnumerable)value)
            {
                var keyContainer = item as IKeyContainer<string>;
                if (keyContainer == null || keyContainer.GetKey() != itemName)
                    continue;
                return SerializeSettingsItem(item);
            }
            throw new ArgumentException(@"Item not found: " + itemName);
        }

        public string GetTutorial(string name, string language = @"en", string filePath = null)
        {
            return JsonTutorialCatalog.FetchTutorial(name, language, filePath);
        }

        public string GetTutorialImage(string name, string imageFilename, string language = @"en", string filePath = null)
        {
            return JsonTutorialCatalog.FetchTutorialImage(name, imageFilename, language, filePath);
        }

        public string GetDocumentSettings(string filePath)
        {
            return SerializeSettingsToFile(
                Program.MainWindow.Document.Settings.ChangeMeasuredResults(null), filePath);
        }

        public string GetDefaultSettings(string filePath)
        {
            return SerializeSettingsToFile(SrmSettingsList.GetDefault(), filePath);
        }

        private string ExportNamedReport(string reportName, string filePath, DataSchemaLocalizer localizer)
        {
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

        private string ExportJsonDefinitionReport(string json, string filePath, DataSchemaLocalizer localizer)
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer);

            // Parse the JSON once; extract sort separately since it's not part of the report definition
            var root = ParseJsonDefinition(json);
            var viewSpec = ResolveJsonReportDefinition(root, dataSchema);
            var sortSpecs = ParseSortSpecs(root);

            var rowFactories = RowFactories.GetRowFactories(CancellationToken.None, dataSchema);

            string ext = Path.GetExtension(filePath);
            var exporter = ReportExporters.ForFilenameExtension(localizer, ext, TextUtil.EXT_CSV);

            DirectoryEx.CreateForFilePath(filePath);

            using (var saver = new FileSaver(filePath, true))
            {
                if (!saver.CanSave())
                    throw new IOException(@"Cannot write to " + filePath);
                IProgressStatus status = new ProgressStatus(string.Empty);
                rowFactories.ExportReport(saver.Stream, viewSpec, sortSpecs, null, exporter,
                    Program.MainWindow, ref status);
                saver.Commit();
            }

            string reportName = viewSpec.Name ?? @"Custom";
            return BuildReportMetadata(filePath, reportName);
        }

        private static DataSchemaLocalizer ParseCulture(string culture)
        {
            if (string.Equals(culture, @"localized", StringComparison.OrdinalIgnoreCase))
                return SkylineDataSchema.GetLocalizedSchemaLocalizer();
            return DataSchemaLocalizer.INVARIANT;
        }

        private static JObject ParseJsonDefinition(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(new LlmInstruction(
                    string.Format(@"Invalid JSON: {0}", ex.Message)));
            }
        }

        private static ViewSpec ResolveJsonReportDefinition(JObject root, SkylineDataSchema dataSchema)
        {
            var selectToken = root[@"select"];
            if (selectToken == null || selectToken.Type != JTokenType.Array || !selectToken.HasValues)
            {
                throw new ArgumentException(new LlmInstruction(
                    @"The 'select' array is required and must not be empty."));
            }

            var columnNames = selectToken.Select(t => (string)t).ToList();
            if (columnNames.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(new LlmInstruction(
                    @"Column names in 'select' must not be empty."));
            }

            string reportName = (string)root[@"name"] ?? @"Custom";

            try
            {
                var resolver = new ColumnResolver(dataSchema);
                var result = resolver.Resolve(columnNames);

                var columnSpecs = result.PropertyPaths.Select(p => new ColumnSpec(p)).ToList();

                var viewSpec = new ViewSpec()
                    .SetName(reportName)
                    .SetRowType(result.RowSourceType)
                    .SetColumns(columnSpecs);
                if (!result.SublistId.IsRoot)
                    viewSpec = viewSpec.SetSublistId(result.SublistId);

                // Apply filter specs
                var filterToken = root[@"filter"];
                if (filterToken is JArray filterArray && filterArray.Count > 0)
                    viewSpec = viewSpec.SetFilters(ParseFilterSpecs(filterArray, result));

                // Apply pivot specs
                var pivotReplicate = (bool?)root[@"pivotReplicate"];
                if (pivotReplicate == true)
                    viewSpec = viewSpec.SetSublistId(PropertyPath.Root);
                else if (pivotReplicate == false)
                {
                    viewSpec = viewSpec.SetSublistId(
                        SublistPaths.GetReplicateSublist(result.RowSourceType));
                }

                if ((bool?)root[@"pivotIsotopeLabel"] == true)
                    viewSpec = PivotReplicateAndIsotopeLabelWidget.PivotIsotopeLabel(viewSpec, true);

                return viewSpec;
            }
            catch (ColumnResolver.UnresolvedColumnsException ex)
            {
                throw new ArgumentException(FormatUnresolvedColumnsError(ex));
            }
        }

        private static LlmInstruction FormatUnresolvedColumnsError(
            ColumnResolver.UnresolvedColumnsException ex)
        {
            var parts = ex.UnresolvedColumns.Select(col =>
            {
                if (col.Suggestions.Count > 0)
                {
                    return string.Format(@"Unknown column {0}. Did you mean: {1}?",
                        col.Name.SingleQuote(),
                        string.Join(@", ", col.Suggestions.Select(s => s.SingleQuote())));
                }
                return string.Format(@"Unknown column {0}.", col.Name.SingleQuote());
            });
            return new LlmInstruction(string.Join(@" ", parts));
        }

        private static List<FilterSpec> ParseFilterSpecs(JArray filterArray,
            ColumnResolver.ResolveResult result)
        {
            var filters = new List<FilterSpec>();
            foreach (var item in filterArray)
            {
                string columnName = (string)item[@"column"];
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    throw new ArgumentException(new LlmInstruction(
                        @"Each filter must have a 'column' field."));
                }

                string opName = (string)item[@"op"];
                if (string.IsNullOrWhiteSpace(opName))
                {
                    throw new ArgumentException(new LlmInstruction(
                        string.Format(@"Filter on column {0} must have an 'op' field.",
                            columnName.SingleQuote())));
                }

                // Resolve column against the row source's full column index
                // ReSharper disable once AssignNullToNotNullAttribute
                if (!result.ColumnIndex.TryGetValue(columnName, out var propertyPath))
                {
                    var suggestions = ColumnResolver.FindSuggestions(columnName, result.ColumnIndex);
                    if (suggestions.Count > 0)
                    {
                        throw new ArgumentException(new LlmInstruction(
                            string.Format(@"Unknown filter column {0}. Did you mean: {1}?",
                                columnName.SingleQuote(),
                                string.Join(@", ", suggestions.Select(s => s.SingleQuote())))));
                    }
                    throw new ArgumentException(new LlmInstruction(
                        string.Format(@"Unknown filter column {0}.", columnName.SingleQuote())));
                }

                // Look up filter operation
                var operation = FilterOperations.GetOperation(opName);
                if (operation == null)
                {
                    var validOps = FilterOperations.ListOperations()
                        .Where(o => !string.IsNullOrEmpty(o.OpName))
                        .Select(o => o.OpName.SingleQuote());
                    throw new ArgumentException(new LlmInstruction(
                        string.Format(@"Unknown filter operation {0}. Valid operations: {1}.",
                            opName.SingleQuote(), string.Join(@", ", validOps))));
                }

                // Validate operand presence
                string operand = (string)item[@"value"];
                bool isUnaryOp = operation == FilterOperations.OP_IS_BLANK ||
                                 operation == FilterOperations.OP_IS_NOT_BLANK;
                if (!isUnaryOp && string.IsNullOrEmpty(operand))
                {
                    throw new ArgumentException(new LlmInstruction(
                        string.Format(@"Filter operation {0} on column {1} requires a 'value' field.",
                            opName.SingleQuote(), columnName.SingleQuote())));
                }

                var predicate = FilterPredicate.FromInvariantOperandText(operation, operand ?? string.Empty);
                filters.Add(new FilterSpec(propertyPath, predicate));
            }
            return filters;
        }

        private static List<RowFilter.ColumnSort> ParseSortSpecs(JObject root)
        {
            var sortToken = root[@"sort"];
            if (!(sortToken is JArray sortArray) || sortArray.Count == 0)
                return null;

            var sorts = new List<RowFilter.ColumnSort>();
            foreach (var item in sortArray)
            {
                string columnName = (string)item[@"column"];
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    throw new ArgumentException(new LlmInstruction(
                        @"Each sort item must have a 'column' field."));
                }

                string dirString = (string)item[@"direction"];
                var direction = ParseSortDirection(dirString);

                sorts.Add(new RowFilter.ColumnSort(new ColumnId(columnName), direction));
            }
            return sorts;
        }

        private static ListSortDirection ParseSortDirection(string direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
                return ListSortDirection.Ascending;
            if (string.Equals(direction, @"asc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, @"ascending", StringComparison.OrdinalIgnoreCase))
                return ListSortDirection.Ascending;
            if (string.Equals(direction, @"desc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, @"descending", StringComparison.OrdinalIgnoreCase))
                return ListSortDirection.Descending;
            throw new ArgumentException(new LlmInstruction(
                string.Format(@"Invalid sort direction {0}. Use 'asc' or 'desc'.",
                    direction.SingleQuote())));
        }

        private static string BuildReportMetadata(string filePath, string reportName)
        {
            var obj = new JObject();
            obj[@"file_path"] = filePath.ToForwardSlashPath();
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
                [@"result"] = result as string
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

        private static string SerializeSettingsToFile(SrmSettings settings, string filePath)
        {
            DirectoryEx.CreateForFilePath(filePath);
            using (var saver = new FileSaver(filePath, true))
            {
                if (!saver.CanSave())
                    throw new IOException(@"Cannot write to " + filePath);
                using (var writer = XmlWriter.Create(saver.Stream,
                           new XmlWriterSettings { Indent = true }))
                {
                    new XmlSerializer(typeof(SrmSettings)).Serialize(writer, settings);
                }
                saver.Commit();
            }
            return filePath.ToForwardSlashPath();
        }

        private static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;
            string text = Regex.Replace(html, @"<[^>]+>", string.Empty);
            return WebUtility.HtmlDecode(text);
        }

        private string RunCommandImpl(string args, bool silent)
        {
            var capture = new StringWriter();
            TextWriter output;

            if (silent)
                output = capture;
            else
                output = JsonUiService.CreateImmediateWindowTee(capture, args);

            // Run on the current thread (already a background pipe server thread).
            // The Immediate Window writer handles cross-thread writes via BeginInvoke.
            var parsedArgs = CommandLine.ParseArgs(args);
            var docBefore = Program.MainWindow.Document;
            var commandLine = new CommandLine(new CommandStatusWriter(output), docBefore);
            commandLine.Run(parsedArgs, true);

            // If the command modified the document, apply it back to SkylineWindow
            // as a single undo record with a RunCommand audit log entry.
            if (!ReferenceEquals(commandLine.Document, docBefore))
            {
                var docResult = commandLine.Document;
                Program.MainWindow.Invoke(new Action(() =>
                {
                    Program.MainWindow.ModifyDocument(
                        ToolsUIResources.ToolService_RunCommand_Run_command,
                        doc => docResult,
                        docPair => AuditLogEntry.CreateSimpleEntry(
                            MessageType.ran_command_line,
                            docPair.NewDocumentType, args));
                }));
            }

            return capture.ToString();
        }

        private static bool IsSettingsListBase(Type type)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SettingsListBase<>))
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        private static string GetPersistedViewNames()
        {
            var persistedViews = Settings.Default.PersistedViews;
            var sb = new StringBuilder();
            sb.AppendLine(@"# Main");
            foreach (var viewSpec in persistedViews.GetViewSpecList(PersistedViews.MainGroup.Id).ViewSpecs)
                sb.AppendLine(viewSpec.Name);
            sb.AppendLine(@"# External Tools");
            foreach (var viewSpec in persistedViews.GetViewSpecList(PersistedViews.ExternalToolsGroup.Id).ViewSpecs)
                sb.AppendLine(viewSpec.Name);
            return sb.ToString();
        }

        private static string GetPersistedViewItem(string itemName)
        {
            var persistedViews = Settings.Default.PersistedViews;
            var viewSpec = persistedViews.GetViewSpecList(PersistedViews.MainGroup.Id).GetView(itemName)
                           ?? persistedViews.GetViewSpecList(PersistedViews.ExternalToolsGroup.Id).GetView(itemName);
            if (viewSpec == null)
                throw new ArgumentException(@"View not found: " + itemName);
            return SerializeViewSpec(viewSpec);
        }

        private static string SerializeViewSpec(ViewSpec viewSpec)
        {
            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            {
                writer.WriteStartElement(@"view");
                viewSpec.WriteXml(writer);
                writer.WriteEndElement();
            }
            return sb.ToString();
        }

        private static string SerializeSettingsItem(object item)
        {
            var xmlSerializable = (IXmlSerializable)item;
            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            {
                writer.WriteStartElement(item.GetType().Name);
                xmlSerializable.WriteXml(writer);
                writer.WriteEndElement();
            }
            return sb.ToString();
        }

    }
}
