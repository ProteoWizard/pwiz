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
using System.IO.Pipes;using System.Linq;
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
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
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
            public JToken[] Params { get; set; }
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

                            // Give long-running verbs a way to notice this client disconnecting, so a
                            // blocked call abandons and frees this single-instance server.
                            JsonUiService.SetClientConnectedCheck(() => IsClientConnected(pipe));
                            string responseJson;
                            try
                            {
                                responseJson = HandleRequest(requestBytes);
                            }
                            finally
                            {
                                JsonUiService.SetClientConnectedCheck(null);
                            }
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

        // Reliable "client still connected" check for a server thread busy in a verb (no read in
        // progress). NamedPipeServerStream.IsConnected does not detect a disconnect without I/O, so
        // peek the pipe -- PeekNamedPipe returns false once the client has closed its end.
        private static bool IsClientConnected(NamedPipeServerStream pipe)
        {
            try
            {
                if (!pipe.IsConnected)
                    return false;
                var handle = pipe.SafePipeHandle;
                if (handle == null || handle.IsInvalid || handle.IsClosed)
                    return false;
                return Kernel32.PeekNamedPipe(handle.DangerousGetHandle(),
                    IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string HandleRequest(byte[] requestBytes)
        {
            int id = 0;
            try
            {
                string json = Encoding.UTF8.GetString(requestBytes);
                var request = JsonConvert.DeserializeObject<JsonRpcRequest>(json);
                if (request?.Method == null)
                {
                    return SerializeError(
                        new JsonRpcException(JsonToolConstants.ERROR_INVALID_REQUEST,
                            @"Invalid JSON-RPC request: missing method"),
                        id, JsonToolConstants.ERROR_INVALID_REQUEST);
                }
                id = request.Id;
                JToken[] args = request.Params ?? Array.Empty<JToken>();

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
            catch (JsonReaderException ex)
            {
                return SerializeError(ex, id, JsonToolConstants.ERROR_PARSE);
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

        private object Dispatch(string method, JToken[] args)
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
                else
                    invokeArgs[i] = ConvertArg(args[i], parameters[i].ParameterType);
            }

            return methodInfo.Invoke(this, invokeArgs);
        }

        private static object ConvertArg(JToken token, Type targetType)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (targetType == typeof(string))
                return token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            return token.ToObject(targetType, _snakeCaseSerializer);
        }

        private static readonly JsonSerializer _snakeCaseSerializer = JsonSerializer.Create(_snakeCaseSettings);

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

        public string GetUiMode()
        {
            // Access on UI thread since Program.ModeUI may read Settings.Default
            string mode = null;
            Program.MainWindow.Invoke(new Action(() => mode = Program.ModeUI.ToString()));
            return mode;
        }

        public void SetUiMode(string mode)
        {
            if (!Enum.TryParse(mode, true, out SrmDocument.DOCUMENT_TYPE docType) ||
                docType == SrmDocument.DOCUMENT_TYPE.none)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Invalid UI mode '{0}'. Must be 'proteomic', 'small_molecules', or 'mixed'.", mode));
            }
            Program.MainWindow.Invoke(new Action(() =>
            {
                Program.MainWindow.SetUIMode(docType);
            }));
        }

        public UndoRedoEntry[] GetUndoRedo()
        {
            // Capture undo/redo descriptions on the UI thread to avoid concurrent
            // enumeration of the UndoManager stacks which are not thread-safe.
            List<string> undoDescriptions = null;
            List<string> redoDescriptions = null;
            Program.MainWindow.Invoke(new Action(() =>
            {
                var undoMgr = Program.MainWindow.GetUndoManager();
                undoDescriptions = undoMgr.UndoDescriptions.ToList();
                redoDescriptions = undoMgr.RedoDescriptions.ToList();
            }));

            var entries = new List<UndoRedoEntry>();

            // Undo entries: index -1 = most recent undoable change, -2 = next, etc.
            int undoIndex = -1;
            foreach (var desc in undoDescriptions)
                entries.Add(new UndoRedoEntry { Index = undoIndex--, Description = desc });

            // Redo entries: index +1 = most recent redoable change, +2 = next, etc.
            int redoIndex = 1;
            foreach (var desc in redoDescriptions)
                entries.Add(new UndoRedoEntry { Index = redoIndex++, Description = desc });

            return entries.ToArray();
        }

        public void SetUndoRedoPosition(int index)
        {
            if (index == 0)
                return; // Already at current state

            Program.MainWindow.Invoke(new Action(() =>
            {
                var undoMgr = Program.MainWindow.GetUndoManager();
                if (index < 0)
                {
                    // Undo: index -1 = undo top (stack index 0), -2 = undo 2 deep, etc.
                    int stackIndex = -index - 1;
                    if (stackIndex >= undoMgr.UndoCount)
                        throw new ArgumentOutOfRangeException(nameof(index),
                            LlmInstruction.Format(@"Undo index {0} is out of range. Only {1} undo steps available.",
                                index, undoMgr.UndoCount));
                    undoMgr.UndoRestore(stackIndex);
                }
                else
                {
                    // Redo: index +1 = redo top (stack index 0), +2 = redo 2 deep, etc.
                    int stackIndex = index - 1;
                    if (stackIndex >= undoMgr.RedoCount)
                        throw new ArgumentOutOfRangeException(nameof(index),
                            LlmInstruction.Format(@"Redo index {0} is out of range. Only {1} redo steps available.",
                                index, undoMgr.RedoCount));
                    undoMgr.RedoRestore(stackIndex);
                }
            }));
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

        string IJsonToolService.RunCommand(string[] args) => RunCommandImpl(args, false);
        string IJsonToolService.RunCommandSilent(string[] args) => RunCommandImpl(args, true);

        public string RunCommand(params string[] args) => RunCommandImpl(args, false);
        public string RunCommandSilent(params string[] args) => RunCommandImpl(args, true);

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

        public void ReorderElements(string[] elementLocators)
        {
            JsonUiService.InvokeOnUiThread(() =>
            {
                var orderedElements = elementLocators.Select(locator =>
                    ElementRefs.FromObjectReference(ElementLocator.Parse(locator))).ToList();
                lock (Program.MainWindow.GetDocumentChangeLock())
                {
                    var originalDocument = Program.MainWindow.Document;
                    var reorderer = new ElementReorderer(CancellationToken.None, originalDocument);
                    var newDocument = reorderer.SetNewOrder(orderedElements);
                    if (!ReferenceEquals(newDocument, originalDocument))
                    {
                        Program.MainWindow.ModifyDocument(
                            ToolsUIResources.ToolService_ReorderElements_Elements_reordered_by_external_tool,
                            doc =>
                            {
                                if (!ReferenceEquals(doc, originalDocument))
                                {
                                    throw new InvalidOperationException(Resources
                                        .SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
                                }
                                return newDocument;
                            },
                            pair => AuditLogEntry.CreateSingleMessageEntry(
                                new MessageInfo(MessageType.reordered_elements, newDocument.DocumentType)));
                    }
                }
            });
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

        public ControlInfo[] GetControls(string formId)
        {
            return JsonUiService.GetControls(formId);
        }

        public object PerformAction(UiElementPath path, string action, object value)
        {
            return JsonUiService.PerformAction(path, action, value);
        }

        public void InvokeMenuItem(string menuPath)
        {
            JsonUiService.InvokeMenuItem(menuPath);
        }

        public void ClickFormButton(string formId, string button)
        {
            JsonUiService.ClickFormButton(formId, button);
        }

        public void ClickToolStripItem(string formId, string menuPath)
        {
            JsonUiService.ClickToolStripItem(formId, menuPath);
        }

        public void SetFormValue(string formId, string controlId, string value)
        {
            JsonUiService.SetFormValue(formId, controlId, value);
        }

        public string GetFormValue(string formId, string controlId)
        {
            return JsonUiService.GetFormValue(formId, controlId);
        }

        public void SetGridText(string formId, string controlId, string text)
        {
            JsonUiService.SetGridText(formId, controlId, text);
        }

        public void SetCurrentCellAddress(string formId, string controlId, System.Drawing.Point cell)
        {
            JsonUiService.SetCurrentCellAddress(formId, controlId, cell);
        }

        public string GetGridText(string formId, string gridId)
        {
            return JsonUiService.GetGridText(formId, gridId);
        }

        public void CloseForm(string formId)
        {
            JsonUiService.CloseForm(formId);
        }

        public string GetGraphData(string graphId, string filePath = null)
        {
            return JsonUiService.GetGraphData(graphId, filePath);
        }

        public string GetGraphImage(string graphId, string filePath = null)
        {
            return JsonUiService.GetGraphImage(graphId, filePath);
        }

        public ImageBytesMetadata GetGraphImageBytes(string graphId)
        {
            return JsonUiService.GetGraphImageBytes(graphId);
        }

        public string GetFormImage(string formId, string filePath = null)
        {
            return JsonUiService.GetFormImage(formId, filePath);
        }

        public ImageBytesMetadata GetFormImageBytes(string formId)
        {
            return JsonUiService.GetFormImageBytes(formId);
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

        public ReportRowsResult GetReportRows(string reportName, int offset, int count,
            string[] columns, ReportFilter[] filter, bool includeMaxLength, string culture)
        {
            ValidateWindow(offset, count);
            var localizer = ParseCulture(culture);
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer, Program.MainWindow.ModeUI);
            var rowFactories = RowFactories.GetRowFactories(CancellationToken.None, dataSchema);

            var viewName = FindReportViewName(reportName);
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(viewName.GroupId);
            var viewSpec = viewSpecList.GetView(viewName.Name);
            var layout = viewSpecList.GetViewLayouts(viewName.Name).DefaultLayout;

            if (filter != null && filter.Length > 0)
                viewSpec = ApplyFilterToNamedReport(viewSpec, filter, dataSchema);

            return MaterializeReportRows(viewSpec, layout, rowFactories, dataSchema,
                offset, count, includeMaxLength, columns, reportName, localizer);
        }

        public ReportRowsResult GetReportFromDefinitionRows(ReportDefinition definition,
            int offset, int count, bool includeMaxLength, string culture)
        {
            ValidateWindow(offset, count);
            var localizer = ParseCulture(culture);
            var document = Program.MainWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(document, localizer, Program.MainWindow.ModeUI);
            var rowFactories = RowFactories.GetRowFactories(CancellationToken.None, dataSchema);

            var viewSpec = ResolveReportDefinition(definition, dataSchema);
            var sortSpecs = ParseSortSpecs(definition);
            var rowTransforms = new List<IRowTransform>();
            if (sortSpecs != null && sortSpecs.Count > 0)
                rowTransforms.Add(RowFilter.Empty.SetColumnSorts(sortSpecs));
            if (viewSpec.HasTotals && rowTransforms.Count == 0)
            {
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
            ViewLayout layout = rowTransforms.Count > 0
                ? new ViewLayout(string.Empty).ChangeRowTransforms(rowTransforms)
                : null;

            string reportName = viewSpec.Name ?? JsonToolConstants.DEFAULT_REPORT_NAME;
            return MaterializeReportRows(viewSpec, layout, rowFactories, dataSchema,
                offset, count, includeMaxLength, null, reportName, localizer);
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

        public ImageBytesMetadata GetTutorialImageBytes(string name, string imageFilename, string language = @"en")
        {
            return JsonTutorialCatalog.FetchTutorialImageBytes(name, imageFilename, language);
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

        // --- Inline rows materialization ---

        // Cell text longer than this is truncated with an explicit "..." suffix so a
        // misjudged window cannot blow caller context on a single long-text cell.
        // internal (not const) so tests can drive the truncation path with smaller caps.
        internal static int MaxCellLength = 200;

        // Hard cap on the serialized row payload. ~4 chars/token gives a ~25K token
        // budget, matching what the LLM caller can absorb without losing context to a
        // single tool response. Rows past this cap are dropped and truncated_at is set
        // so the caller can resume at offset = truncated_at.
        internal static int MaxResponseChars = 100_000;

        // Number of rows sampled when include_max_length is requested on a large
        // dataset. Beyond this, max_length_sampled is set to true on string columns to
        // tell the caller the value is a lower bound estimate.
        internal static int MaxLengthSampleRows = 200;

        // Upper bound on count to keep a single request from materializing an
        // unbounded list of rows in memory and to avoid the (offset + count) overflow
        // that would silently shift the window to negative range.
        internal static int MaxRowCount = 10_000;

        // String marker appended to a truncated cell value so the caller can detect
        // truncation without parsing the response wrapper.
        private const string CELL_TRUNCATION_MARKER = @"...";

        private static void ValidateWindow(int offset, int count)
        {
            if (offset < 0)
                throw new ArgumentException(new LlmInstruction(@"offset must be >= 0."));
            if (count < 0)
                throw new ArgumentException(new LlmInstruction(@"count must be >= 0. Use count = 0 for shape-only introspection."));
            if (count > MaxRowCount)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"count {0} exceeds the per-request maximum of {1}. Paginate with offset/count.",
                    count.ToString(), MaxRowCount.ToString()));
            }
        }

        private ViewSpec ApplyFilterToNamedReport(ViewSpec viewSpec, ReportFilter[] filters,
            SkylineDataSchema dataSchema)
        {
            // Resolve filter columns against the report's row source type. The named
            // report has fixed RowSource and viewSpec.Columns; filter columns may
            // reference any column in the data model, not just selected ones.
            if (string.IsNullOrEmpty(viewSpec.RowSource))
            {
                throw new ArgumentException(new LlmInstruction(
                    @"Cannot apply additional filters to a report without a row source."));
            }

            // Find the row source type via the registered factories.
            var rowFactories = RowFactories.GetRowFactories(CancellationToken.None, dataSchema);
            if (!rowFactories.TryGetRowSource(viewSpec.RowSource, out _, out var rowType))
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Cannot apply additional filters: unknown row source {0}.",
                    viewSpec.RowSource.SingleQuote()));
            }

            var resolver = new ColumnResolver(dataSchema);
            var availableColumns = resolver.GetAvailableColumns(rowType);
            var columnsByName = new Dictionary<string, PropertyPath>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in availableColumns)
                columnsByName[col.InvariantName] = col.PropertyPath;

            var filterSpecs = new List<FilterSpec>(viewSpec.Filters);
            foreach (var f in filters)
            {
                if (string.IsNullOrWhiteSpace(f.Column))
                {
                    throw new ArgumentException(new LlmInstruction(
                        @"Each filter must have a 'column' field."));
                }
                if (!columnsByName.TryGetValue(f.Column, out var propertyPath))
                {
                    var suggestions = ColumnResolver.FindSuggestions(f.Column, columnsByName.Keys);
                    string hint = suggestions.Count > 0
                        ? @" Did you mean: " + string.Join(@", ", suggestions.Select(s => s.SingleQuote())) + @"?"
                        : string.Empty;
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown filter column {0}.{1}", f.Column.SingleQuote(), hint));
                }
                if (string.IsNullOrWhiteSpace(f.Op))
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Filter on column {0} must have an 'op' field.", f.Column.SingleQuote()));
                }
                var operation = FilterOperations.GetOperation(f.Op);
                if (operation == null)
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown filter operation {0}.", f.Op.SingleQuote()));
                }
                bool isUnary = operation == FilterOperations.OP_IS_BLANK ||
                               operation == FilterOperations.OP_IS_NOT_BLANK;
                if (!isUnary && string.IsNullOrEmpty(f.Value))
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Filter operation {0} on column {1} requires a 'value' field.",
                        f.Op.SingleQuote(), f.Column.SingleQuote()));
                }
                var predicate = FilterPredicate.FromInvariantOperandText(operation, f.Value ?? string.Empty);
                filterSpecs.Add(new FilterSpec(propertyPath, predicate));
            }
            return viewSpec.SetFilters(filterSpecs);
        }

        private ReportRowsResult MaterializeReportRows(ViewSpec viewSpec, ViewLayout layout,
            RowFactories rowFactories, SkylineDataSchema dataSchema,
            int offset, int count, bool includeMaxLength, string[] requestedColumns,
            string reportName, DataSchemaLocalizer localizer)
        {
            if (!rowFactories.TryGetRowSource(viewSpec.RowSource, out var rowSource, out var rowSourceType))
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"The row type {0} cannot be exported.", viewSpec.RowSource.SingleQuote()));
            }
            var viewInfo = new ViewInfo(dataSchema, rowSourceType, viewSpec);

            // Mirror RowFactories.ExportReport: use streaming when there are no
            // row transforms; otherwise materialize through BindingListSource so
            // sorts, pivots, and filters get applied uniformly with the file path.
            // The BindingListSource is held open across the entire materialization
            // so its ItemProperties and RowItems are guaranteed to be valid for
            // the duration of BuildReportRowsResult.
            RowItemEnumerator enumerator = null;
            BindingListSource bindingListSource = null;
            try
            {
                if (layout == null || layout.RowTransforms.Count == 0)
                {
                    enumerator = viewInfo.GetStreamingRowItemEnumerator(CancellationToken.None, rowSource);
                }
                if (enumerator == null)
                {
                    bindingListSource = new BindingListSource(CancellationToken.None);
                    if (layout != null)
                        bindingListSource.ApplyLayout(layout);
                    bindingListSource.SetView(viewInfo, rowSource);
                    enumerator = new RowItemList(bindingListSource.ReportResults.RowItems)
                    {
                        ItemProperties = bindingListSource.ItemProperties
                    };
                }
                layout?.ApplyFormats(enumerator.ColumnFormats);

                // Total can come from two cheap sources:
                //  - non-streaming: BindingListSource has materialized the rows already
                //    (BigList.Count is O(1)).
                //  - streaming: RowItemEnumerator.Length is populated up front for the
                //    common "no filter, at most one collection" case, so we still get
                //    O(1) total without draining the enumerator.
                // Clamp to int.MaxValue defensively; a single report past that limit is
                // well outside what the inline tools target.
                int? knownTotal = null;
                long? totalLong = bindingListSource?.ReportResults.RowItems.Count
                                  ?? enumerator.Length;
                if (totalLong.HasValue)
                    knownTotal = totalLong.Value > int.MaxValue ? int.MaxValue : (int)totalLong.Value;
                return BuildReportRowsResult(enumerator, localizer, offset, count,
                    includeMaxLength, requestedColumns, reportName, knownTotal);
            }
            finally
            {
                enumerator?.Dispose();
                bindingListSource?.Dispose();
            }
        }

        private static ReportRowsResult BuildReportRowsResult(RowItemEnumerator enumerator,
            DataSchemaLocalizer localizer, int offset, int count, bool includeMaxLength,
            string[] requestedColumns, string reportName, int? knownTotalRows)
        {
            var allProperties = enumerator.ItemProperties.ToList();
            // Resolve optional column projection to the indices we will return.
            // Names match against both DisplayName and InvariantName so the caller can
            // pass either the localized header value or the invariant column id that
            // get_report_doc_topic returns.
            int[] columnIndices = ResolveColumnProjection(allProperties, requestedColumns);
            int columnCount = columnIndices.Length;

            // Use a DsvWriter purely for its culture-aware GetFormattedValue path. The
            // separator is irrelevant -- we never feed the writer rows -- but it matches
            // what the file-export tools produce so values round-trip.
            var dsvWriter = new DsvWriter(localizer.FormatProvider, localizer.Language, ',')
            {
                ColumnFormats = enumerator.ColumnFormats
            };

            var maxLengths = new int?[columnCount];
            // Compute column types up front so the scan can target every column whose
            // serialized value is text-shaped. That covers both raw strings and the
            // entity wrappers (Peptide, Protein, Replicate, ModifiedSequence, ...) that
            // map to the "other" bucket -- in named reports the latter are the common
            // case, so restricting the scan to typeof(string) would silently produce
            // null max_observed_length on most useful reports.
            string[] columnTypes = new string[columnCount];
            bool[] isTextCol = new bool[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                columnTypes[i] = GetSimpleTypeName(allProperties[columnIndices[i]].PropertyType);
                isTextCol[i] = columnTypes[i] == @"string" || columnTypes[i] == @"other";
            }

            // Shape-only fast path: when there is no window and no length scan to perform,
            // skip per-row work entirely. For non-streaming (BindingListSource) we already
            // know the total from RowItems.Count; for streaming we still have to walk to
            // count, but we avoid formatting any cells.
            bool needRows = count > 0;
            bool needLengthScan = includeMaxLength;

            var capturedRows = new List<string[]>(Math.Min(count, 1024));
            // (long) cast in windowEnd guards against int overflow when a caller passes a
            // count near int.MaxValue together with a large offset.
            long windowEnd = (long)offset + count;
            int scanLimit = MaxLengthSampleRows;
            int rowIndex = 0;

            if (!needRows && !needLengthScan && knownTotalRows.HasValue)
            {
                // Shape-only call against the non-streaming path: total is free.
                rowIndex = knownTotalRows.Value;
            }
            else
            {
                while (enumerator.MoveNext())
                {
                    bool inWindow = rowIndex >= offset && rowIndex < windowEnd;
                    bool scanThisRow = needLengthScan && rowIndex < scanLimit;
                    if (inWindow || scanThisRow)
                    {
                        var row = inWindow ? new string[columnCount] : null;
                        for (int c = 0; c < columnCount; c++)
                        {
                            var pd = allProperties[columnIndices[c]];
                            string value = dsvWriter.GetFormattedValue(enumerator.Current, pd);
                            if (inWindow)
                                row[c] = TruncateCell(value);
                            if (scanThisRow && isTextCol[c] && value != null)
                            {
                                int len = value.Length;
                                if (maxLengths[c] == null || len > maxLengths[c].Value)
                                    maxLengths[c] = len;
                            }
                        }
                        if (inWindow)
                            capturedRows.Add(row);
                    }
                    rowIndex++;
                    // Once we've passed both the window and the scan range, the only
                    // remaining work is counting rows. If the total is already known,
                    // skip the rest of the enumeration entirely. When length scanning
                    // is off, we have nothing to wait for past the window.
                    bool windowDone = rowIndex >= windowEnd;
                    bool scanDone = !needLengthScan || rowIndex >= scanLimit;
                    if (windowDone && scanDone && knownTotalRows.HasValue)
                    {
                        rowIndex = knownTotalRows.Value;
                        break;
                    }
                }
            }
            int totalRows = rowIndex;

            // Build column descriptors. Use the column's invariant caption as Name so
            // callers get the same identifier they'd pass back in the definition language
            // -- this also matches what filter/projection resolution accepts.
            var resultColumns = new ReportRowsColumn[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                var pd = allProperties[columnIndices[i]];
                resultColumns[i] = new ReportRowsColumn
                {
                    Name = pd.DisplayName,
                    Type = columnTypes[i],
                };
                if (includeMaxLength && isTextCol[i])
                {
                    resultColumns[i].MaxObservedLength = maxLengths[i] ?? 0;
                    // Per the spec: max_length_sampled is set to true only when the
                    // value is approximate (we hit the sample cap). Otherwise the value
                    // is exact -- the field is omitted so consumers can distinguish
                    // sampled from exact without an extra comparison.
                    if (totalRows > scanLimit)
                        resultColumns[i].MaxLengthSampled = true;
                }
            }

            // Cap total payload by dropping rows from the tail. We have already truncated
            // long cells, so this only fires when the window itself produces too much text.
            // Per-row sizes are precomputed and we subtract on drop to keep this O(N).
            int? truncatedAt = null;
            bool windowTruncated = false;
            int keptRows = capturedRows.Count;
            int headerOverhead = EstimateHeaderChars(resultColumns);
            var rowSizes = new int[capturedRows.Count];
            int approxSize = headerOverhead;
            for (int i = 0; i < capturedRows.Count; i++)
            {
                rowSizes[i] = EstimateRowChars(capturedRows[i]);
                approxSize += rowSizes[i];
            }
            while (keptRows > 0 && approxSize > MaxResponseChars)
            {
                keptRows--;
                approxSize -= rowSizes[keptRows];
            }
            if (keptRows < capturedRows.Count)
            {
                windowTruncated = true;
                truncatedAt = offset + keptRows;
                capturedRows.RemoveRange(keptRows, capturedRows.Count - keptRows);
            }

            return new ReportRowsResult
            {
                Report = reportName,
                TotalRows = totalRows,
                Columns = resultColumns,
                Rows = capturedRows.ToArray(),
                Window = new ReportRowsWindow
                {
                    Offset = offset,
                    Count = capturedRows.Count,
                    Truncated = windowTruncated
                },
                TruncatedAt = truncatedAt
            };
        }

        private static int[] ResolveColumnProjection(IList<DataPropertyDescriptor> properties,
            string[] requested)
        {
            if (requested == null || requested.Length == 0)
            {
                var all = new int[properties.Count];
                for (int i = 0; i < all.Length; i++)
                    all[i] = i;
                return all;
            }
            // Index columns by both the localized DisplayName (what the caller sees in
            // the CSV header) and the invariant column caption (what get_report_doc_topic
            // returns). Filter resolution keys by InvariantName for the same reason; this
            // keeps both surfaces consistent for the caller. Two passes so DisplayName
            // always wins -- a single dict-set loop would let a later column's invariant
            // caption shadow an earlier column's display name in pivoted / localized
            // reports where the two namespaces collide.
            var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < properties.Count; i++)
                byName[properties[i].DisplayName] = i;
            for (int i = 0; i < properties.Count; i++)
            {
                string invariant = properties[i].ColumnCaption?.GetCaption(DataSchemaLocalizer.INVARIANT);
                if (!string.IsNullOrEmpty(invariant) && !byName.ContainsKey(invariant))
                    byName[invariant] = i;
            }
            var indices = new int[requested.Length];
            for (int i = 0; i < requested.Length; i++)
            {
                if (!byName.TryGetValue(requested[i], out int idx))
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown column {0}. Available columns: {1}.",
                        requested[i].SingleQuote(),
                        string.Join(@", ", properties.Select(p => p.DisplayName.SingleQuote()))));
                }
                indices[i] = idx;
            }
            return indices;
        }

        private static string TruncateCell(string value)
        {
            if (value == null)
                return null;
            if (value.Length <= MaxCellLength)
                return value;
            return value.Substring(0, MaxCellLength) + CELL_TRUNCATION_MARKER;
        }

        // Per-cell and per-row JSON overhead constants used by the payload-size
        // estimator. Approximate, not byte-exact: the estimator only decides when
        // to drop tail rows.
        private const int PAYLOAD_PER_CELL_OVERHEAD = 4;   // "", + structural punctuation
        private const int PAYLOAD_PER_ROW_OVERHEAD = 4;    // [], + newline
        private const int PAYLOAD_PER_COLUMN_HEADER_OVERHEAD = 32; // field names + structural punctuation

        private static int EstimateHeaderChars(ReportRowsColumn[] columns)
        {
            int total = 0;
            foreach (var col in columns)
                total += (col.Name?.Length ?? 0) + (col.Type?.Length ?? 0) + PAYLOAD_PER_COLUMN_HEADER_OVERHEAD;
            return total;
        }

        private static int EstimateRowChars(string[] row)
        {
            int total = PAYLOAD_PER_ROW_OVERHEAD;
            for (int c = 0; c < row.Length; c++)
            {
                total += PAYLOAD_PER_CELL_OVERHEAD;
                if (row[c] != null)
                    total += row[c].Length;
            }
            return total;
        }

        private static string GetSimpleTypeName(Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(string))
                return @"string";
            if (underlying == typeof(bool))
                return @"boolean";
            if (underlying == typeof(int) || underlying == typeof(long) ||
                underlying == typeof(short) || underlying == typeof(byte))
                return @"integer";
            if (underlying == typeof(double) || underlying == typeof(float) ||
                underlying == typeof(decimal))
                return @"number";
            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
                return @"datetime";
            // Unknown / unmapped types (Guid, Color, enums, custom proteomics types) get
            // a stable "other" label so callers don't see raw CLR type names that can
            // shift on refactor.
            return @"other";
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
            if (ex is JsonRpcException rpcEx)
                return rpcEx.Code;
            if (ex is ArgumentException || ex is FormatException)
                return JsonToolConstants.ERROR_INVALID_PARAMS;
            return JsonToolConstants.ERROR_INTERNAL;
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
            // Run on a background thread and return immediately if the command pops a dialog (alert
            // -> throws its text; native dialog -> returns), instead of blocking on a modal.
            // See JsonUiService.RunWithDialogWatch.
            return JsonUiService.RunWithDialogWatch(() => RunCommandCore(args, silent));
        }

        private string RunCommandCore(string[] args, bool silent)
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
            public bool Dirty
            {
                get
                {
                    bool dirty = false;
                    Program.MainWindow.Invoke(new Action(() => dirty = Program.MainWindow.Dirty));
                    return dirty;
                }
            }

            public SrmDocument OpenDocument(string skylineFile)
            {
                bool success = false;
                Program.MainWindow.Invoke(new Action(() =>
                {
                    success = Program.MainWindow.LoadFile(skylineFile);
                }));
                if (!success)
                    return null;
                return WaitForDocumentLoaded();
            }

            public SrmDocument NewDocument(string skylineFile, bool overwrite)
            {
                Program.MainWindow.Invoke(new Action(() =>
                {
                    if (skylineFile != null && overwrite)
                    {
                        FileEx.SafeDelete(skylineFile);
                        FileEx.SafeDelete(Path.ChangeExtension(skylineFile, ChromatogramCache.EXT));
                    }
                    // Forced — dirty check is handled by the CLI layer
                    // before reaching this point via --discard-changes.
                    Program.MainWindow.NewDocument(true);
                    if (skylineFile != null)
                    {
                        // Save empty document to set DocumentFilePath so subsequent
                        // --save commands know the correct path
                        Program.MainWindow.SaveDocument(skylineFile);
                    }
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
