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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using JSON = SkylineTool.JsonToolConstants.JSON;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// JSON named pipe server hosted inside the Skyline process.
    /// Replaces the connector's JsonPipeServer by dispatching directly
    /// to ToolService methods without BinaryFormatter serialization.
    /// </summary>
    public class JsonToolServer : IJsonToolService, IDisposable
    {

        /// <summary>
        /// Shared Newtonsoft.Json serializer configured with snake_case naming.
        /// Used for POCO serialization/deserialization in the dispatch layer so
        /// PascalCase C# properties map to snake_case JSON keys.
        /// </summary>
        private static readonly JsonSerializer _snakeCaseSerializer = JsonSerializer.Create(
            new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            });

        private readonly ToolService _toolService;
        private readonly string _pipeName;
        private readonly Thread _serverThread;
        private readonly Dictionary<string, MethodInfo> _methods;
        private volatile bool _stopping;
        private ToolLog _currentLog;

        public string PipeName { get { return _pipeName; } }

        public JsonToolServer(ToolService toolService, string legacyToolServiceName)
        {
            _toolService = toolService;
            _pipeName = JsonToolConstants.GetJsonPipeName(legacyToolServiceName);
            _serverThread = new Thread(ServerLoop) { IsBackground = true };

            // Build method dictionary from IJsonToolService interface, mapped to
            // implementations on this class. Supports typed parameters and return values.
            _methods = typeof(IJsonToolService).GetMethods()
                .ToDictionary(m => m.Name, m => GetType().GetMethod(m.Name,
                    m.GetParameters().Select(p => p.ParameterType).ToArray()));
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
                using var client = new NamedPipeClientStream(@".", _pipeName, PipeDirection.InOut);
                client.Connect(500);
            }
            catch
            {
                // Expected when shutting down
            }
            DeleteConnectionInfo();
        }

        public void WriteConnectionInfo()
        {
            string dir = JsonToolConstants.GetConnectionDirectory();
            Directory.CreateDirectory(dir);
            var obj = new JObject
            {
                [nameof(JSON.pipe_name)] = _pipeName,
                [nameof(JSON.process_id)] = Process.GetCurrentProcess().Id,
                [nameof(JSON.connected_at)] = DateTime.UtcNow.ToString(@"o"),
                [nameof(JSON.skyline_version)] = Install.ProgramNameAndVersion
            };
            File.WriteAllText(JsonToolConstants.GetConnectionFilePath(_pipeName), obj.ToString());

            // Clean up stale files from dead instances
            CleanupStaleConnectionFiles(dir);
        }

        private void DeleteConnectionInfo()
        {
            try
            {
                string path = JsonToolConstants.GetConnectionFilePath(_pipeName);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static void CleanupStaleConnectionFiles(string dir)
        {
            foreach (string file in Directory.GetFiles(dir,
                JsonToolConstants.CONNECTION_FILE_PREFIX + @"*" + JsonToolConstants.CONNECTION_FILE_EXT))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var obj = JObject.Parse(json);
                    int pid = (int)obj[nameof(JSON.process_id)];
                    if (!IsSkylineProcess(pid))
                        File.Delete(file);
                }
                catch
                {
                    // Ignore parse/access errors
                }
            }
        }

        private static bool IsSkylineProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return Program.FunctionalTest ||
                       process.ProcessName.StartsWith(@"Skyline", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
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

        public string HandleRequest(byte[] requestBytes)
        {
            try
            {
                string json = Encoding.UTF8.GetString(requestBytes);
                var root = JObject.Parse(json);
                string method = (string) root[nameof(JSON.method)];
                string[] args = ParseArgs(root[nameof(JSON.args)]);

                bool logRequested = root[nameof(JSON.log)]?.Value<bool>() == true;
                _currentLog = logRequested ? new ToolLog() : null;

                try
                {
                    object result = Dispatch(method, args);
                    return SerializeResult(result);
                }
                finally
                {
                    _currentLog = null;
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return SerializeError(ex.InnerException);
            }
            catch (Exception ex)
            {
                return SerializeError(ex);
            }
        }

        /// <summary>
        /// Writes a diagnostic line to the request-scoped log, if logging is enabled.
        /// No-op when logging is not active -- safe to call unconditionally.
        /// </summary>
        protected void Log(string message)
        {
            _currentLog?.Write(message);
        }

        private object Dispatch(string method, string[] args)
        {
            if (method == @"QueryAvailableMethods")
                return string.Join(@",", _methods.Keys.OrderBy(k => k));

            if (!_methods.TryGetValue(method, out var methodInfo))
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Unknown method:", method));

            var parameters = methodInfo.GetParameters();
            int requiredCount = parameters.Count(p => !p.HasDefaultValue);
            if (args.Length < requiredCount)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"{0} requires at least {1} argument(s)", method, requiredCount.ToString()));
            }

            var invokeArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i >= args.Length)
                    invokeArgs[i] = parameters[i].DefaultValue;
                else if (parameters[i].ParameterType == typeof(string))
                    invokeArgs[i] = args[i];
                else
                    invokeArgs[i] = DeserializeArg(args[i], parameters[i].ParameterType);
            }

            return methodInfo.Invoke(this, invokeArgs);
        }

        private static object DeserializeArg(string json, Type targetType)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            return JToken.Parse(json).ToObject(targetType, _snakeCaseSerializer);
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
            return TextUtil.LineSeparate(LlmNameMap.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        }

        public string GetDocumentStatus()
        {
            // All labels in this method are LLM-facing (not localizable)
            var doc = Program.MainWindow.Document;
            string mode = doc.DocumentType.ToString();
            int groups = doc.MoleculeGroupCount;
            int molecules = doc.MoleculeCount;
            int precursors = doc.MoleculeTransitionGroupCount;
            int transitions = doc.MoleculeTransitionCount;
            int replicates = doc.Settings.MeasuredResults?.Chromatograms.Count ?? 0;
            string docPath = _toolService.GetDocumentPath();
            string docDisplay = string.IsNullOrEmpty(docPath)
                ? new LlmInstruction(@"(unsaved)")
                : docPath.ToForwardSlashPath();

            LlmInstruction groupsLabel, moleculesLabel;
            if (doc.DocumentType == SrmDocument.DOCUMENT_TYPE.small_molecules)
            {
                groupsLabel = new LlmInstruction(@"Lists");
                moleculesLabel = new LlmInstruction(@"Molecules");
            }
            else
            {
                // proteomic and mixed both support proteins and free-form peptide lists
                groupsLabel = new LlmInstruction(@"Proteins/Lists");
                moleculesLabel = doc.DocumentType == SrmDocument.DOCUMENT_TYPE.mixed
                    ? new LlmInstruction(@"Peptides/Molecules")
                    : new LlmInstruction(@"Peptides");
            }

            bool dirty = Program.MainWindow.Dirty;

            return TextUtil.LineSeparate($@"Mode: {mode}",
                $@"{groupsLabel}: {groups}",
                $@"{moleculesLabel}: {molecules}",
                $@"Precursors: {precursors}",
                $@"Transitions: {transitions}",
                $@"Replicates: {replicates}",
                $@"Document: {docDisplay}",
                $@"Saved: {(dirty ? new LlmInstruction(@"no (unsaved changes)") : new LlmInstruction(@"yes"))}");
        }

        public string GetAvailableTutorials()
        {
            return JsonTutorialCatalog.FormatCatalog();
        }

        public string GetReportDocTopics()
        {
            var topics = GetTopicList();
            var sb = new StringBuilder();
            foreach (var topic in topics)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(topic.DisplayName);
                sb.Append(TextUtil.SEPARATOR_TSV);
                sb.Append(topic.Columns.Count);
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
            string propName = ResolveLlmListType(listType);
            if (propName == nameof(PersistedViews))
                return GetPersistedViewNames();

            var prop = typeof(Settings).GetProperty(propName);
            if (prop == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Unknown settings list type:", listType));
            var value = prop.GetValue(Settings.Default);
            if (value == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Settings list is null:", listType));
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
            var topics = GetTopicList();
            var matchedTopic = FindMatchingTopic(topicName, topics);
            if (matchedTopic == null)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine(matchedTopic.DisplayName);
            sb.AppendLine();
            sb.AppendLine(LlmInstruction.TabSeparate(@"Name", @"Description", @"Type"));
            foreach (var col in matchedTopic.Columns)
                sb.AppendLine(col.InvariantName + TextUtil.SEPARATOR_TSV +
                              col.Description.FlattenToSingleLine() + TextUtil.SEPARATOR_TSV +
                              col.TypeName);
            return sb.ToString();
        }

        private IList<ColumnResolver.TopicInfo> GetTopicList()
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            var resolver = new ColumnResolver(dataSchema);
            return resolver.GetTopics();
        }

        private static ColumnResolver.TopicInfo FindMatchingTopic(string topicName,
            IList<ColumnResolver.TopicInfo> topics)
        {
            string normalized = topicName.Replace(@" ", string.Empty);
            // Pass 1: exact match (ignoring spaces)
            foreach (var topic in topics)
            {
                if (string.Equals(topic.DisplayName.Replace(@" ", string.Empty),
                        normalized, StringComparison.OrdinalIgnoreCase))
                    return topic;
            }
            // Pass 2: partial match (e.g., "Peptide" matches "PeptideResult")
            foreach (var topic in topics)
            {
                string dn = topic.DisplayName.Replace(@" ", string.Empty);
                if (dn.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf(dn, StringComparison.OrdinalIgnoreCase) >= 0)
                    return topic;
            }
            return null;
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
                case JsonToolConstants.LEVEL_GROUP:
                    targetDepth = 1;
                    break;
                case JsonToolConstants.LEVEL_MOLECULE:
                    targetDepth = 2;
                    break;
                case JsonToolConstants.LEVEL_PRECURSOR:
                    targetDepth = 3;
                    break;
                case JsonToolConstants.LEVEL_TRANSITION:
                    targetDepth = 4;
                    break;
                default:
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Invalid level '{0}'. Use: group, molecule, precursor, transition.", level));
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
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Element not found: {0}", rootLocator));
                }
                rootDepth = rootPath.Length;
            }

            if (targetDepth <= rootDepth)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Level '{0}' must be deeper than the root element.", level));
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

        public string AddReportFromDefinition(ReportDefinition definition)
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            var viewSpec = ResolveReportDefinition(definition, dataSchema);

            if (string.IsNullOrEmpty(viewSpec.Name) || viewSpec.Name == JsonToolConstants.DEFAULT_REPORT_NAME)
            {
                throw new ArgumentException(new LlmInstruction(
                    @"The 'name' field is required when adding a report."));
            }

            var groupId = PersistedViews.MainGroup.Id;
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(groupId);
            var layout = new ViewSpecLayout(viewSpec, ViewLayoutList.EMPTY);
            viewSpecList = viewSpecList.ReplaceView(viewSpec.Name, layout);
            Settings.Default.PersistedViews.SetViewSpecList(groupId, viewSpecList);
            Log(string.Format(@"Saved report '{0}' to {1}", viewSpec.Name, groupId));

            return LlmInstruction.Format(@"Report {0} has been added to Skyline.", viewSpec.Name.SingleQuote());
        }

        public string InsertSmallMoleculeTransitionList(string textCSV)
        {
            return JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.InsertSmallMoleculeTransitionList(textCSV,
                    @"Insert small molecule transition list"));
        }

        public string ImportFasta(string textFasta, string keepEmptyProteins = null)
        {
            bool? keepEmpty = keepEmptyProteins == null ? (bool?)null : bool.Parse(keepEmptyProteins);
            return JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.ImportFasta(new StringReader(textFasta),
                    Helpers.CountLinesInString(textFasta), false,
                    @"Import FASTA from MCP",
                    new SkylineWindow.ImportFastaInfo(false, textFasta),
                    keepEmpty));
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

        public string GetFormImage(string formId, string filePath = null)
        {
            return JsonUiService.GetFormImage(formId, filePath);
        }

        // Multi-arg methods

        public ReportMetadata ExportReport(string reportName, string filePath, string culture)
        {
            return ExportNamedReport(reportName, filePath, ParseCulture(culture));
        }

        public ReportMetadata ExportReportFromDefinition(ReportDefinition definition, string filePath, string culture)
        {
            return ExportJsonDefinitionReport(definition, filePath, ParseCulture(culture));
        }

        public string GetSettingsListItem(string listType, string itemName)
        {
            string propName = ResolveLlmListType(listType);
            if (propName == nameof(PersistedViews))
                return GetPersistedViewItem(itemName);

            var prop = typeof(Settings).GetProperty(propName);
            if (prop == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Unknown settings list type:", listType));
            var value = prop.GetValue(Settings.Default);
            if (value == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Settings list is null:", listType));
            foreach (var item in (IEnumerable)value)
            {
                var keyContainer = item as IKeyContainer<string>;
                if (keyContainer == null || keyContainer.GetKey() != itemName)
                    continue;
                return SerializeSettingsItem(item);
            }
            throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Item not found:", itemName));
        }

        public string AddSettingsListItem(string listType, string itemXml, bool overwrite = false)
        {
            string propName = ResolveLlmListType(listType);
            if (propName == nameof(PersistedViews))
            {
                throw new ArgumentException(LlmInstruction.SpaceSeparate(
                    @"Use skyline_add_report to add reports, not AddSettingsListItem."));
            }

            var prop = typeof(Settings).GetProperty(propName);
            if (prop == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Unknown settings list type:", listType));
            var value = prop.GetValue(Settings.Default);
            if (value == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Settings list is null:", listType));

            Type itemType = GetSettingsListItemType(prop.PropertyType);
            if (itemType == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Cannot determine item type for:", listType));

            object item = DeserializeSettingsItem(itemType, itemXml);

            var keyContainer = (IKeyContainer<string>)item;
            string itemName = keyContainer.GetKey();

            bool exists = false;
            foreach (var existing in (IEnumerable)value)
            {
                if (existing is IKeyContainer<string> kc && kc.GetKey() == itemName)
                {
                    exists = true;
                    break;
                }
            }

            if (exists && !overwrite)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Item {0} already exists in {1}. Set overwrite to true to replace it.",
                    itemName.SingleQuote(), listType));
            }

            // MappedList.InsertItem handles upsert (removes existing key before inserting)
            var addMethod = value.GetType().GetMethod(@"Add", new[] { itemType });
            if (addMethod == null)
                throw new InvalidOperationException(LlmInstruction.SpaceSeparate(@"No Add method found on:", value.GetType().Name));
            addMethod.Invoke(value, new[] { item });

            return exists
                ? LlmInstruction.Format(@"Replaced {0} in {1}.", itemName.SingleQuote(), listType)
                : LlmInstruction.Format(@"Added {0} to {1}.", itemName.SingleQuote(), listType);
        }

        public TutorialMetadata GetTutorial(string name, string language = @"en", string filePath = null)
        {
            return JsonTutorialCatalog.FetchTutorial(name, language, filePath);
        }

        public TutorialImageMetadata GetTutorialImage(string name, string imageFilename, string language = @"en", string filePath = null)
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

        private ReportMetadata ExportNamedReport(string reportName, string filePath, DataSchemaLocalizer localizer)
        {
            Log(string.Format(@"Exporting named report '{0}'", reportName));
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
                    throw new IOException(LlmInstruction.SpaceSeparate(@"Cannot write to", filePath));
                IProgressStatus status = new ProgressStatus(string.Empty);
                rowFactories.ExportReport(saver.Stream, viewName, exporter,
                    Program.MainWindow, ref status);
                saver.Commit();
            }

            return BuildReportMetadata(filePath, reportName);
        }

        private ReportMetadata ExportJsonDefinitionReport(ReportDefinition definition, string filePath, DataSchemaLocalizer localizer)
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer);

            var viewSpec = ResolveReportDefinition(definition, dataSchema);
            var sortSpecs = ParseSortSpecs(definition);

            var rowFactories = RowFactories.GetRowFactories(CancellationToken.None, dataSchema);

            string ext = Path.GetExtension(filePath);
            var exporter = ReportExporters.ForFilenameExtension(localizer, ext, TextUtil.EXT_CSV);

            var rowTransforms = new List<IRowTransform>();
            if (sortSpecs != null && sortSpecs.Count > 0)
            {
                rowTransforms.Add(RowFilter.Empty.SetColumnSorts(sortSpecs));
            }
            // When the viewSpec has pivot operations (PivotKey/PivotValue), the export
            // must use BindingListSource instead of streaming to process cross-tab totals.
            // RowFactories.ExportReport uses streaming when layout has no row transforms,
            // bypassing pivot processing. Force BindingListSource by adding
            // a no-op sort on the first GroupBy column.
            if (viewSpec.HasTotals && rowTransforms.Count == 0)
            {
                Log(@"Pivot detected: injecting sort for BindingListSource path");
                var groupByCol = viewSpec.Columns.FirstOrDefault(c => c.Total == TotalOperation.GroupBy);
                if (groupByCol != null)
                {
                    rowTransforms.Add(RowFilter.Empty.SetColumnSorts(new[]
                    {
                        new RowFilter.ColumnSort(new ColumnId(groupByCol.PropertyPath.ToString()),
                            ListSortDirection.Ascending)
                    }));
                }
            }

            ViewLayout layout = null;
            if (rowTransforms.Count > 0)
            {
                layout = new ViewLayout(string.Empty).ChangeRowTransforms(rowTransforms);
            }

            DirectoryEx.CreateForFilePath(filePath);

            using (var saver = new FileSaver(filePath, true))
            {
                if (!saver.CanSave())
                    throw new IOException(LlmInstruction.SpaceSeparate(@"Cannot write to", filePath));
                IProgressStatus status = new ProgressStatus(string.Empty);
                rowFactories.ExportReport(saver.Stream, viewSpec, layout, exporter,
                    Program.MainWindow, ref status);
                saver.Commit();
            }

            Log(string.Format(@"Exported report to {0}", filePath));
            string reportName = viewSpec.Name ?? JsonToolConstants.DEFAULT_REPORT_NAME;
            return BuildReportMetadata(filePath, reportName);
        }

        private static DataSchemaLocalizer ParseCulture(string culture)
        {
            if (string.Equals(culture, JsonToolConstants.CULTURE_LOCALIZED, StringComparison.OrdinalIgnoreCase))
                return SkylineDataSchema.GetLocalizedSchemaLocalizer();
            return DataSchemaLocalizer.INVARIANT;
        }

        private ViewSpec ResolveReportDefinition(ReportDefinition definition, SkylineDataSchema dataSchema)
        {
            if (definition.Select == null || definition.Select.Length == 0)
            {
                throw new ArgumentException(new LlmInstruction(
                    @"The 'select' array is required and must not be empty."));
            }

            var columnNames = definition.Select.ToList();
            if (columnNames.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(new LlmInstruction(
                    @"Column names in 'select' must not be empty."));
            }

            string reportName = definition.Name ?? JsonToolConstants.DEFAULT_REPORT_NAME;
            Log(string.Format(@"Resolving report '{0}' with {1} columns: {2}",
                reportName, columnNames.Count, string.Join(@", ", columnNames)));

            try
            {
                var resolver = new ColumnResolver(dataSchema);
                var result = resolver.Resolve(columnNames);

                var columnSpecs = result.PropertyPaths.Select(p => new ColumnSpec(p)).ToList();
                Log(string.Format(@"Resolved {0} columns via {1} row source, sublist={2}",
                    result.PropertyPaths.Count, result.RowSourceType.Name, result.SublistId));

                var viewSpec = new ViewSpec()
                    .SetName(reportName)
                    .SetRowType(result.RowSourceType)
                    .SetColumns(columnSpecs);
                if (!result.SublistId.IsRoot)
                    viewSpec = viewSpec.SetSublistId(result.SublistId);

                // Apply UI mode: use explicit value or default to current SkylineWindow mode
                string uiMode = definition.Uimode;
                if (string.IsNullOrEmpty(uiMode))
                    uiMode = UiModes.FromDocumentType(Program.MainWindow.ModeUI);
                viewSpec = viewSpec.SetUiMode(uiMode);

                // Apply filter specs
                var filters = definition.Filter;
                if (filters != null && filters.Length > 0)
                {
                    viewSpec = viewSpec.SetFilters(ParseFilterSpecs(filters, result));
                    Log(string.Format(@"Applied {0} filter(s)", filters.Length));
                }

                // Apply pivot specs
                if (definition.PivotReplicate == true)
                {
                    viewSpec = viewSpec.SetSublistId(PropertyPath.Root);
                    Log(@"pivot_replicate=true: set sublist to root");
                }
                else if (definition.PivotReplicate == false)
                {
                    viewSpec = viewSpec.SetSublistId(
                        SublistPaths.GetReplicateSublist(result.RowSourceType));
                    Log(@"pivot_replicate=false: set sublist to replicate");
                }

                if (definition.PivotIsotopeLabel == true)
                {
                    viewSpec = PivotReplicateAndIsotopeLabelWidget.PivotIsotopeLabel(viewSpec, true);
                    Log(@"pivot_isotope_label=true: applied isotope label pivot");
                }

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
            return LlmInstruction.SpaceSeparate(parts.ToArray());
        }

        private static List<FilterSpec> ParseFilterSpecs(ReportFilter[] reportFilters,
            ColumnResolver.ResolveResult result)
        {
            var filters = new List<FilterSpec>();
            foreach (var item in reportFilters)
            {
                string columnName = item.Column;
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    throw new ArgumentException(new LlmInstruction(
                        @"Each filter must have a 'column' field."));
                }

                string opName = item.Op;
                if (string.IsNullOrWhiteSpace(opName))
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Filter on column {0} must have an 'op' field.",
                            columnName.SingleQuote()));
                }

                // Resolve column against the row source's full column index
                // ReSharper disable once AssignNullToNotNullAttribute
                if (!result.ColumnIndex.TryGetValue(columnName, out var columnInfo))
                {
                    var suggestions = ColumnResolver.FindSuggestions(columnName, result.ColumnIndex.Keys);
                    if (suggestions.Count > 0)
                    {
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Unknown filter column {0}. Did you mean: {1}?",
                                columnName.SingleQuote(),
                                string.Join(@", ", suggestions.Select(s => s.SingleQuote()))));
                    }
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown filter column {0}.", columnName.SingleQuote()));
                }

                // Look up filter operation
                var operation = FilterOperations.GetOperation(opName);
                if (operation == null)
                {
                    var validOps = FilterOperations.ListOperations()
                        .Where(o => !string.IsNullOrEmpty(o.OpName))
                        .Select(o => o.OpName.SingleQuote());
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown filter operation {0}. Valid operations: {1}.",
                            opName.SingleQuote(), string.Join(@", ", validOps)));
                }

                // Validate operand presence
                string operand = item.Value;
                bool isUnaryOp = operation == FilterOperations.OP_IS_BLANK ||
                                 operation == FilterOperations.OP_IS_NOT_BLANK;
                if (!isUnaryOp && string.IsNullOrEmpty(operand))
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Filter operation {0} on column {1} requires a 'value' field.",
                            opName.SingleQuote(), columnName.SingleQuote()));
                }

                var predicate = FilterPredicate.FromInvariantOperandText(operation, operand ?? string.Empty);
                filters.Add(new FilterSpec(columnInfo.PropertyPath, predicate));
            }
            return filters;
        }

        private static List<RowFilter.ColumnSort> ParseSortSpecs(ReportDefinition definition)
        {
            var sortItems = definition.Sort;
            if (sortItems == null || sortItems.Length == 0)
                return null;

            var sorts = new List<RowFilter.ColumnSort>();
            foreach (var item in sortItems)
            {
                string columnName = item.Column;
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    throw new ArgumentException(new LlmInstruction(
                        @"Each sort item must have a 'column' field."));
                }

                var direction = ParseSortDirection(item.Direction);
                sorts.Add(new RowFilter.ColumnSort(new ColumnId(columnName), direction));
            }
            return sorts;
        }

        private static ListSortDirection ParseSortDirection(string direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
                return ListSortDirection.Ascending;
            if (string.Equals(direction, JsonToolConstants.SORT_ASC, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, @"ascending", StringComparison.OrdinalIgnoreCase))
                return ListSortDirection.Ascending;
            if (string.Equals(direction, JsonToolConstants.SORT_DESC, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(direction, @"descending", StringComparison.OrdinalIgnoreCase))
                return ListSortDirection.Descending;
            throw new ArgumentException(LlmInstruction.Format(
                @"Invalid sort direction {0}. Use 'asc' or 'desc'.",
                    direction.SingleQuote()));
        }

        private static ReportMetadata BuildReportMetadata(string filePath, string reportName)
        {
            var metadata = new ReportMetadata
            {
                FilePath = filePath.ToForwardSlashPath(),
                ReportName = reportName
            };

            string ext = Path.GetExtension(filePath);
            if (string.Equals(ext, TextUtil.EXT_PARQUET, StringComparison.OrdinalIgnoreCase))
            {
                metadata.Format = @"parquet";
                return metadata;
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

            metadata.RowCount = Math.Max(0, lineCount - 1);

            if (lineCount > 0)
                metadata.Columns = previewLines[0];

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
                metadata.Preview = sb.ToString();
            }

            return metadata;
        }

        private static ViewName FindReportViewName(string reportName)
        {
            var persistedViews = Settings.Default.PersistedViews;
            if (persistedViews.GetViewSpecList(PersistedViews.MainGroup.Id).GetView(reportName) != null)
                return PersistedViews.MainGroup.Id.ViewName(reportName);
            if (persistedViews.GetViewSpecList(PersistedViews.ExternalToolsGroup.Id).GetView(reportName) != null)
                return PersistedViews.ExternalToolsGroup.Id.ViewName(reportName);
            throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Report not found:", reportName));
        }

        private string SerializeResult(object result)
        {
            var obj = new JObject();
            if (result == null)
                obj[nameof(JSON.result)] = null;
            else if (result is string s)
                obj[nameof(JSON.result)] = s;
            else
                obj[nameof(JSON.result)] = JToken.FromObject(result, _snakeCaseSerializer);
            AppendLog(obj);
            return obj.ToString();
        }

        private string SerializeError(Exception ex)
        {
            var obj = new JObject
            {
                [nameof(JSON.error)] = ex.ToString()
            };
            AppendLog(obj);
            return obj.ToString();
        }

        private void AppendLog(JObject obj)
        {
            if (_currentLog != null && _currentLog.HasContent)
                obj[nameof(JSON.log)] = _currentLog.ToString();
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

        private static string SerializeSettingsToFile(SrmSettings settings, string filePath)
        {
            DirectoryEx.CreateForFilePath(filePath);
            using (var saver = new FileSaver(filePath, true))
            {
                if (!saver.CanSave())
                    throw new IOException(LlmInstruction.SpaceSeparate(@"Cannot write to", filePath));
                using (var writer = XmlWriter.Create(saver.Stream,
                           new XmlWriterSettings { Indent = true }))
                {
                    new XmlSerializer(typeof(SrmSettings)).Serialize(writer, settings);
                }
                saver.Commit();
            }
            return filePath.ToForwardSlashPath();
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
            var commandLine = new CommandLine(new CommandStatusWriter(output), docBefore,
                Program.MainWindow.DocumentFilePath);

            // Override document operations to go through SkylineWindow UI,
            // so --in/--new/--save/--out show LongWaitDlg progress and properly
            // update DocumentFilePath and clean state.
            commandLine.DocumentOperations = new SkylineWindowDocumentOperations();

            commandLine.Run(parsedArgs, true);

            // If the command modified the document, apply it back to SkylineWindow
            // as a single undo record with a RunCommand audit log entry.
            // Skip if the host already has the current doc (from --in/--new/--save/--out
            // going through SkylineWindowDocumentOperations).
            var docAfter = commandLine.Document;
            if (!ReferenceEquals(docAfter, docBefore) &&
                !ReferenceEquals(docAfter, Program.MainWindow.Document))
            {
                Program.MainWindow.Invoke(new Action(() =>
                {
                    Program.MainWindow.ModifyDocument(
                        ToolsUIResources.ToolService_RunCommand_Run_command,
                        doc => docAfter,
                        docPair => AuditLogEntry.CreateSimpleEntry(
                            MessageType.ran_command_line,
                            docPair.NewDocumentType, args));
                }));
            }

            return capture.ToString();
        }

        /// <summary>
        /// <see cref="IDocumentOperations"/> implementation that delegates to
        /// SkylineWindow UI methods, providing LongWaitDlg progress and proper
        /// DocumentFilePath/clean-state management.
        /// </summary>
        private class SkylineWindowDocumentOperations : IDocumentOperations
        {
            public SrmDocument OpenDocument(string skylineFile)
            {
                bool success = false;
                Program.MainWindow.Invoke(new Action(() =>
                {
                    success = Program.MainWindow.OpenFile(skylineFile);
                }));
                if (!success)
                    return null;
                return WaitForDocumentLoaded();
            }

            public SrmDocument NewDocument(string skylineFile, bool overwrite)
            {
                Program.MainWindow.Invoke(new Action(() =>
                {
                    if (overwrite)
                    {
                        FileEx.SafeDelete(skylineFile);
                        FileEx.SafeDelete(Path.ChangeExtension(skylineFile, ChromatogramCache.EXT));
                    }
                    Program.MainWindow.NewDocument(true);
                    // Save empty document to set DocumentFilePath so subsequent
                    // --save commands know the correct path
                    Program.MainWindow.SaveDocument(skylineFile);
                }));
                return WaitForDocumentLoaded();
            }

            /// <summary>
            /// Wait for background loaders to finish so that the document returned
            /// to <see cref="CommandLine"/> is fully loaded, matching the contract
            /// of the command-line <see cref="IDocumentOperations"/> implementation.
            /// Without this wait, background loading can advance the MainWindow document
            /// past the snapshot returned here, causing stale-document undo entries.
            /// </summary>
            private static SrmDocument WaitForDocumentLoaded()
            {
                var doc = Program.MainWindow.Document;
                if (doc.IsLoaded)
                    return doc;

                using (var loaded = new ManualResetEventSlim())
                {
                    EventHandler<DocumentChangedEventArgs> handler = (s, e) =>
                    {
                        if (Program.MainWindow.Document.IsLoaded)
                            loaded.Set();
                    };
                    ((IDocumentContainer)Program.MainWindow).Listen(handler);
                    try
                    {
                        // Re-check after subscribing to avoid missed-signal race
                        if (!Program.MainWindow.Document.IsLoaded)
                            loaded.Wait(TimeSpan.FromMinutes(5));
                    }
                    finally
                    {
                        ((IDocumentContainer)Program.MainWindow).Unlisten(handler);
                    }
                }
                return Program.MainWindow.Document;
            }

            public bool SaveDocument(SrmDocument doc, string saveFile)
            {
                bool success = false;
                Program.MainWindow.Invoke(new Action(() =>
                {
                    // Apply any in-memory modifications (from --refine, etc.) before saving
                    var currentDoc = Program.MainWindow.Document;
                    if (!ReferenceEquals(doc, currentDoc))
                    {
                        Program.MainWindow.ModifyDocument(
                            ToolsUIResources.ToolService_RunCommand_Run_command,
                            d => doc,
                            docPair => AuditLogEntry.CreateSimpleEntry(
                                MessageType.ran_command_line,
                                docPair.NewDocumentType, string.Empty));
                    }
                    // Use no-arg SaveDocument when path is null (--save without --in),
                    // which falls back to the window's current DocumentFilePath
                    if (string.IsNullOrEmpty(saveFile))
                        success = Program.MainWindow.SaveDocument();
                    else
                        success = Program.MainWindow.SaveDocument(saveFile);
                }));
                return success;
            }
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

        /// <summary>
        /// Maps LlmName values to Settings property names for all settings list types.
        /// Includes PersistedViews which is not a SettingsListBase but is exposed as a list type.
        /// </summary>
        private static readonly Dictionary<string, string> LlmNameMap = BuildLlmNameMap();

        private static Dictionary<string, string> BuildLlmNameMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in typeof(Settings).GetProperties())
            {
                if (!IsSettingsListBase(prop.PropertyType))
                    continue;
                var attr = prop.PropertyType.GetCustomAttribute<LlmNameAttribute>();
                if (attr != null)
                    map[attr.Name] = prop.Name;
            }
            // PersistedViews is not a SettingsListBase but is handled as a settings list type
            var pvAttr = typeof(PersistedViews).GetCustomAttribute<LlmNameAttribute>();
            if (pvAttr != null)
                map[pvAttr.Name] = nameof(PersistedViews);
            return map;
        }

        /// <summary>
        /// Returns the LlmName for a settings list class, or null if the attribute is not present.
        /// </summary>
        public static string GetSettingsListName<T>()
        {
            return typeof(T).GetCustomAttribute<LlmNameAttribute>()?.Name;
        }

        /// <summary>
        /// Resolves a listType parameter that may be either an LlmName or a property name.
        /// LlmName takes priority; falls back to the raw value for backward compatibility.
        /// </summary>
        private string ResolveLlmListType(string listType)
        {
            return LlmNameMap.TryGetValue(listType, out string propName) ? propName : listType;
        }

        private static string GetPersistedViewNames()
        {
            var persistedViews = Settings.Default.PersistedViews;
            var sb = new StringBuilder();
            sb.AppendLine(new LlmInstruction(@"# Main"));
            foreach (var viewSpec in persistedViews.GetViewSpecList(PersistedViews.MainGroup.Id).ViewSpecs)
                sb.AppendLine(viewSpec.Name);
            sb.AppendLine(new LlmInstruction(@"# External Tools"));
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
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"View not found:", itemName));
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

        private static Type GetSettingsListItemType(Type listType)
        {
            var type = listType;
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SettingsListBase<>))
                    return type.GetGenericArguments()[0];
                type = type.BaseType;
            }
            return null;
        }

        private static object DeserializeSettingsItem(Type itemType, string xml)
        {
            if (!xml.TrimStart().StartsWith(@"<"))
            {
                throw new ArgumentException(LlmInstruction.SpaceSeparate(
                    @"Expected XML content but received:",
                    xml.Substring(0, Math.Min(50, xml.Length))));
            }

            using (var reader = XmlReader.Create(new StringReader(xml)))
            {
                reader.MoveToContent();
                var deserialize = itemType.GetMethod(@"Deserialize",
                    BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(XmlReader) }, null);
                if (deserialize == null)
                {
                    throw new InvalidOperationException(LlmInstruction.SpaceSeparate(
                        @"No static Deserialize(XmlReader) method found on:", itemType.Name));
                }
                return deserialize.Invoke(null, new object[] { reader });
            }
        }

        /// <summary>
        /// Request-scoped diagnostic log. When enabled, tool methods can append
        /// timestamped lines that are returned to the caller in the response JSON.
        /// </summary>
        private class ToolLog
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

            public void Write(string message)
            {
                _sb.Append(_stopwatch.ElapsedMilliseconds.ToString().PadLeft(6));
                _sb.Append(@" ms  ");
                _sb.AppendLine(message);
            }

            public bool HasContent => _sb.Length > 0;

            public override string ToString()
            {
                return _sb.ToString();
            }
        }

    }
}
