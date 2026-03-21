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
using Newtonsoft.Json.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
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
using JSON_RPC = SkylineTool.JsonToolConstants.JSON_RPC;

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
        /// Shared Newtonsoft.Json settings configured with snake_case naming.
        /// Used for POCO serialization/deserialization in the dispatch layer so
        /// PascalCase C# properties map to snake_case JSON keys.
        /// </summary>
        private static readonly JsonSerializerSettings _snakeCaseSettings =
            new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };

        // JSON-RPC 2.0 request/response POCOs for typed deserialization

        private class JsonRpcRequest
        {
            [JsonProperty(nameof(JSON_RPC.method))]
            public string Method { get; set; }
            [JsonProperty(nameof(JSON_RPC.@params))]
            public string[] Params { get; set; }
            [JsonProperty(nameof(JSON_RPC.id))]
            public int Id { get; set; }
            [JsonProperty(nameof(JSON_RPC._log))]
            public bool Log { get; set; }
        }

        private class JsonRpcResponse
        {
            [JsonProperty(nameof(JSON_RPC.jsonrpc))]
            public string Jsonrpc { get; set; } = JsonToolConstants.JSONRPC_VERSION;
            [JsonProperty(nameof(JSON_RPC.result))]
            public object Result { get; set; }
            [JsonProperty(nameof(JSON_RPC.error))]
            public JsonRpcError Error { get; set; }
            [JsonProperty(nameof(JSON_RPC.id))]
            public int Id { get; set; }
            [JsonProperty(nameof(JSON_RPC._log), NullValueHandling = NullValueHandling.Ignore)]
            public string Log { get; set; }

            // JSON-RPC 2.0: result present only on success, error present only on failure
            public bool ShouldSerializeResult() { return Error == null; }
            public bool ShouldSerializeError() { return Error != null; }
        }

        private class JsonRpcError
        {
            [JsonProperty(nameof(JSON_RPC.code))]
            public int Code { get; set; }
            [JsonProperty(nameof(JSON_RPC.message))]
            public string Message { get; set; }
        }

        private class JsonRpcException : Exception
        {
            public int Code { get; }
            public JsonRpcException(int code, string message) : base(message) { Code = code; }
        }

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
            var info = new
            {
                pipe_name = _pipeName,
                process_id = Process.GetCurrentProcess().Id,
                connected_at = DateTime.UtcNow.ToString(@"o"),
                skyline_version = Install.ProgramNameAndVersion
            };
            File.WriteAllText(JsonToolConstants.GetConnectionFilePath(_pipeName),
                JsonConvert.SerializeObject(info, Newtonsoft.Json.Formatting.Indented));

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
                    var obj = JsonConvert.DeserializeAnonymousType(json, new { process_id = 0 });
                    int pid = obj.process_id;
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
            int id = 0;
            try
            {
                string json = Encoding.UTF8.GetString(requestBytes);
                var request = JsonConvert.DeserializeObject<JsonRpcRequest>(json);
                id = request.Id;
                string[] args = request.Params ?? Array.Empty<string>();

                _currentLog = request.Log ? new ToolLog() : null;

                try
                {
                    object result = Dispatch(request.Method, args);
                    return SerializeResult(result, id);
                }
                finally
                {
                    _currentLog = null;
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return SerializeError(ex.InnerException, id, GetErrorCode(ex.InnerException));
            }
            catch (Exception ex)
            {
                return SerializeError(ex, id, GetErrorCode(ex));
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
            {
                throw new JsonRpcException(JsonToolConstants.ERROR_METHOD_NOT_FOUND,
                    LlmInstruction.SpaceSeparate(@"Unknown method:", method));
            }

            var parameters = methodInfo.GetParameters();
            int requiredCount = parameters.Count(p => !p.HasDefaultValue);
            if (args.Length < requiredCount)
            {
                throw new JsonRpcException(JsonToolConstants.ERROR_INVALID_PARAMS,
                    LlmInstruction.Format(@"{0} requires at least {1} argument(s)",
                        method, requiredCount.ToString()));
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
            return JsonConvert.DeserializeObject(json, targetType, _snakeCaseSettings);
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

        public SelectionInfo GetSelection()
        {
            return JsonUiService.GetSelection();
        }

        public string GetReplicateName()
        {
            return _toolService.GetReplicateName();
        }

        public string[] GetReplicateNames()
        {
            var doc = Program.MainWindow.Document;
            var measuredResults = doc.Settings.MeasuredResults;
            if (measuredResults == null)
                return Array.Empty<string>();
            return measuredResults.Chromatograms.Select(c => c.Name).ToArray();
        }

        public string GetProcessId()
        {
            return _toolService.GetProcessId().ToString();
        }

        public string[] GetSettingsListTypes()
        {
            return LlmNameMap.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public DocumentStatus GetDocumentStatus()
        {
            var doc = Program.MainWindow.Document;
            string docPath = _toolService.GetDocumentPath();

            string groupsLabel, moleculesLabel;
            if (doc.DocumentType == SrmDocument.DOCUMENT_TYPE.small_molecules)
            {
                groupsLabel = @"Lists";
                moleculesLabel = @"Molecules";
            }
            else
            {
                groupsLabel = @"Proteins/Lists";
                moleculesLabel = doc.DocumentType == SrmDocument.DOCUMENT_TYPE.mixed
                    ? @"Peptides/Molecules"
                    : @"Peptides";
            }

            return new DocumentStatus
            {
                DocumentPath = string.IsNullOrEmpty(docPath) ? null : docPath.ToForwardSlashPath(),
                DocumentType = doc.DocumentType.ToString(),
                Groups = doc.MoleculeGroupCount,
                GroupsLabel = groupsLabel,
                Molecules = doc.MoleculeCount,
                MoleculesLabel = moleculesLabel,
                Precursors = doc.MoleculeTransitionGroupCount,
                Transitions = doc.MoleculeTransitionCount,
                Replicates = doc.Settings.MeasuredResults?.Chromatograms.Count ?? 0,
                HasUnsavedChanges = Program.MainWindow.Dirty,
            };
        }

        public TutorialListItem[] GetAvailableTutorials()
        {
            return JsonTutorialCatalog.GetCatalog();
        }

        public ReportDocTopicSummary[] GetReportDocTopics(string dataSource = null)
        {
            var topics = GetTopicList(dataSource);
            return topics.Select(t => new ReportDocTopicSummary
            {
                Name = t.DisplayName,
                ColumnCount = t.Columns.Count,
            }).ToArray();
        }

        // 1-arg methods

        public string GetSelectedElementLocator(string elementType)
        {
            return _toolService.GetSelectedElementLocator(elementType);
        }

        public string RunCommand(string[] args)
        {
            return RunCommandImpl(args, false);
        }

        public string RunCommandSilent(string[] args)
        {
            return RunCommandImpl(args, true);
        }

        public string[] GetSettingsListNames(string listType, string groupName = null)
        {
            string propName = ResolveLlmListType(listType);
            if (propName == nameof(PersistedViews))
                return GetPersistedViewNames(groupName);

            var prop = typeof(Settings).GetProperty(propName);
            if (prop == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Unknown settings list type:", listType));
            var value = prop.GetValue(Settings.Default);
            if (value == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Settings list is null:", listType));
            var names = new List<string>();
            foreach (var item in (IEnumerable)value)
            {
                var keyContainer = item as IKeyContainer<string>;
                if (keyContainer != null)
                    names.Add(keyContainer.GetKey());
            }
            return names.ToArray();
        }

        public ReportDocTopicDetail GetReportDocTopic(string topicName, string dataSource = null)
        {
            var topics = GetTopicList(dataSource);
            var matchedTopic = FindMatchingTopic(topicName, topics);
            if (matchedTopic == null)
                return null;

            return new ReportDocTopicDetail
            {
                Name = matchedTopic.DisplayName,
                Columns = matchedTopic.Columns.Select(col => new ColumnDefinition
                {
                    Name = col.InvariantName,
                    Description = col.Description.FlattenToSingleLine(),
                    Type = col.TypeName,
                }).ToArray(),
            };
        }

        private IList<ColumnResolver.TopicInfo> GetTopicList(string scope)
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT, Program.MainWindow.ModeUI);
            var reader = new ReportDefinitionReader(dataSchema);
            return reader.GetTopics(scope);
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

        public LocationEntry[] GetLocations(string level, string rootLocator = null)
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

            var results = new List<LocationEntry>();
            EnumerateAtDepth(document, elementRefs,
                (DocNodeParent)document.FindNode(rootPath),
                rootPath, rootDepth, targetDepth, results);
            return results.ToArray();
        }

        private static void EnumerateAtDepth(SrmDocument document, ElementRefs elementRefs,
            DocNodeParent currentNode, IdentityPath currentPath,
            int currentDepth, int targetDepth, List<LocationEntry> results)
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
                    results.Add(new LocationEntry
                    {
                        Name = nodeRef.Name,
                        Locator = nodeRef.ToString(),
                    });
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
                        childPath, currentDepth + 1, targetDepth, results);
                }
            }
        }

        public void AddReportFromDefinition(ReportDefinition definition)
        {
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT, Program.MainWindow.ModeUI);
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
        }

        public void InsertSmallMoleculeTransitionList(string textCSV)
        {
            JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.InsertSmallMoleculeTransitionList(textCSV,
                    @"Insert small molecule transition list"));
        }

        public void ImportFasta(string textFasta, string keepEmptyProteins = null)
        {
            bool? keepEmpty = keepEmptyProteins == null ? (bool?)null : bool.Parse(keepEmptyProteins);
            JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.ImportFasta(new StringReader(textFasta),
                    Helpers.CountLinesInString(textFasta), false,
                    @"Import FASTA from MCP",
                    new SkylineWindow.ImportFastaInfo(false, textFasta),
                    keepEmpty));
        }

        public void ImportProperties(string csvText)
        {
            JsonUiService.InvokeOnUiThread(() =>
                Program.MainWindow.ImportAnnotations(new StringReader(csvText),
                    new MessageInfo(MessageType.imported_annotations,
                        Program.MainWindow.Document.DocumentType,
                        @"Import properties from MCP")));
        }

        public void SetSelectedElement(string elementLocatorString, string additionalLocators = null)
        {
            JsonUiService.SetSelection(elementLocatorString, additionalLocators);
        }

        public void SetReplicate(string replicateName)
        {
            JsonUiService.SetReplicate(replicateName);
        }

        public FormInfo[] GetOpenForms()
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

        public void AddSettingsListItem(string listType, string itemXml, bool overwrite = false)
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
        }

        public string[] GetSettingsListSelectedItems(string listType)
        {
            var selector = ResolveDocumentSelector(listType);
            var settings = Program.MainWindow.Document.Settings;
            return selector.GetSelectedItems(settings);
        }

        public void SelectSettingsListItems(string listType, string[] itemNames)
        {
            var selector = ResolveDocumentSelector(listType);

            if (selector.SingleSelect && itemNames.Length != 1)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"{0} requires exactly one item.", listType));
            }

            JsonUiService.InvokeOnUiThread(() =>
            {
                try
                {
                    Program.MainWindow.ModifyDocument(
                        LlmInstruction.Format(@"Select {0} items", listType),
                        doc => doc.ChangeSettings(selector.SetSelectedItems(doc.Settings, itemNames)),
                        docPair => AuditLogEntry.CreateSimpleEntry(
                            MessageType.ran_command_line,
                            docPair.NewDocumentType,
                            string.Join(@", ", itemNames)));
                }
                catch (SettingsListItemNotFoundException ex)
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Item {0} not found in {1}. Use skyline_add_settings_list_item to add it first.",
                        ex.ItemKey.SingleQuote(), listType));
                }
            });
        }

        private ISettingsListDocumentSelection ResolveDocumentSelector(string listType)
        {
            string propName = ResolveLlmListType(listType);
            var prop = typeof(Settings).GetProperty(propName);
            if (prop == null)
                throw new ArgumentException(LlmInstruction.SpaceSeparate(@"Unknown settings list type:", listType));
            var value = prop.GetValue(Settings.Default);
            if (value is ISettingsListDocumentSelection selector)
                return selector;
            throw new ArgumentException(LlmInstruction.SpaceSeparate(
                @"Selection is not supported for settings list:", listType + @".",
                @"Use skyline_get_document_settings to see the current document configuration."));
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
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer, Program.MainWindow.ModeUI);
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
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer, Program.MainWindow.ModeUI);

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
            string reportName = definition.Name ?? JsonToolConstants.DEFAULT_REPORT_NAME;
            Log(string.Format(@"Resolving report '{0}' with {1} columns: {2}",
                reportName, definition.Select?.Length ?? 0,
                definition.Select != null ? string.Join(@", ", definition.Select) : string.Empty));

            var reader = new ReportDefinitionReader(dataSchema);
            var viewSpec = reader.CreateViewSpec(definition, definition.DataSource);
            Log(string.Format(@"Resolved via {0} row source", viewSpec.RowSource));
            return viewSpec;
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

        private string SerializeResult(object result, int id)
        {
            var response = new JsonRpcResponse
            {
                Result = result,
                Id = id,
                Log = _currentLog?.HasContent == true ? _currentLog.ToString() : null,
            };
            return JsonConvert.SerializeObject(response, _snakeCaseSettings);
        }

        private string SerializeError(Exception ex, int id, int code)
        {
            var response = new JsonRpcResponse
            {
                Error = new JsonRpcError { Code = code, Message = ex.Message },
                Id = id,
                Log = _currentLog?.HasContent == true ? _currentLog.ToString() : null,
            };
            return JsonConvert.SerializeObject(response);
        }

        private static int GetErrorCode(Exception ex)
        {
            return ex is JsonRpcException rpcEx ? rpcEx.Code : JsonToolConstants.ERROR_INTERNAL;
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

        private string RunCommandImpl(string[] args, bool silent)
        {
            var capture = new StringWriter();
            string argsDisplay = string.Join(@" ", args);
            TextWriter output;

            if (silent)
                output = capture;
            else
                output = JsonUiService.CreateImmediateWindowTee(capture, argsDisplay);

            // Run on the current thread (already a background pipe server thread).
            // The Immediate Window writer handles cross-thread writes via BeginInvoke.
            var parsedArgs = args;
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

        private static string[] GetPersistedViewNames(string groupName)
        {
            var persistedViews = Settings.Default.PersistedViews;
            var groups = new[] { PersistedViews.MainGroup, PersistedViews.ExternalToolsGroup };
            if (groupName != null)
            {
                var match = groups.FirstOrDefault(g =>
                    string.Equals(g.Id.Name, groupName, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown report group: {0}. Valid groups: main, external_tools", groupName));
                }
                groups = new[] { match };
            }
            var names = new List<string>();
            foreach (var group in groups)
                foreach (var viewSpec in persistedViews.GetViewSpecList(group.Id).ViewSpecs)
                    names.Add(viewSpec.Name);
            return names.ToArray();
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
