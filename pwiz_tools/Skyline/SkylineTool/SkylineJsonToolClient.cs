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
using System.Text;
using System.Text.Json;
using System.Threading;
using JSON_RPC = SkylineTool.JsonToolConstants.JSON_RPC;
// ReSharper disable InvalidXmlDocComment (for direct link into .NET 8.0)

namespace SkylineTool
{
    /// <summary>
    /// JSON-RPC 2.0 client for the Skyline JSON tool service. Connects to a
    /// running Skyline instance via named pipe and provides a fully typed
    /// <see cref="IJsonToolService"/> implementation. Replaces
    /// <see cref="SkylineToolClient"/> (which uses the deprecated BinaryFormatter
    /// transport) with modern JSON-RPC.
    ///
    /// <para><b>.NET Framework 4.7.2 tools</b>: Reference SkylineTool.dll.
    /// The System.Text.Json dependency is included. Create a
    /// <see cref="System.IO.Pipes.NamedPipeClientStream"/> connected to the
    /// Skyline pipe and pass it to the constructor.</para>
    ///
    /// <para><b>.NET 8.0+ tools</b>: Link-compile IJsonToolService.cs,
    /// JsonToolConstants.cs, JsonToolModels.cs, and SkylineJsonToolClient.cs
    /// into your project. System.Text.Json is built into .NET 8.0.</para>
    ///
    /// <para><b>Usage</b>:</para>
    /// <code>
    /// using (var client = SkylineJsonToolClient.Connect(pipeName))
    /// {
    ///     string path = client.GetDocumentPath();
    ///     var status = client.GetDocumentStatus();
    ///     var report = client.ExportReport("Peak Area", "output.csv", "invariant");
    /// }
    /// </code>
    /// </summary>
    public class SkylineJsonToolClient : IJsonToolService, IDisposable
    {
        private static readonly JsonSerializerOptions _snakeCaseOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        private readonly NamedPipeClientStream _pipe;

        /// <summary>
        /// When true, requests include "_log": true to enable diagnostic logging.
        /// The server returns timing and internal step details in the response.
        /// </summary>
        public bool LoggingEnabled { get; set; }

        /// <summary>
        /// Diagnostic log content from the most recent response, or null if absent.
        /// </summary>
        public string LastLog { get; private set; }

        /// <summary>
        /// Gives up on a call that is still waiting for Skyline's response. A caller that will not wait forever (the
        /// MCP bounds every call) sets this, then disposes the connection: Skyline sees the disconnect and abandons
        /// the call, freeing its single-instance pipe server for the next one.
        ///
        /// <para>The wait is an asynchronous read for exactly this reason. A caller blocked in a SYNCHRONOUS read
        /// cannot drop the connection at all -- Windows keeps the pipe handle open until the pending read returns, so
        /// disposing the stream does not actually disconnect it and Skyline goes on waiting. Cancelling an overlapped
        /// read releases the handle, so the dispose really does disconnect. Requires the pipe to have been opened with
        /// <see cref="PipeOptions.Asynchronous"/>.</para>
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        /// Connects to Skyline's tool pipe and returns a client for it. The preferred way in: the connection has
        /// two settings this class depends on and neither is obvious from the outside, so it owns both rather than
        /// asking every caller to remember them.
        ///
        /// <para><see cref="PipeOptions.Asynchronous"/> is what lets <see cref="CancellationToken"/> abandon a call
        /// at all, and message <see cref="PipeStream.ReadMode"/> is what makes a response arrive as one message
        /// (<see cref="ReadAllBytes"/> reads until <see cref="PipeStream.IsMessageComplete"/>). Getting either wrong
        /// fails quietly and far from the mistake, so the constructor rejects a pipe that has neither.</para>
        /// </summary>
        /// <param name="pipeName">Skyline's pipe name, from its connection-*.json discovery file.</param>
        /// <param name="timeoutMillis">How long to wait for Skyline to accept the connection.</param>
        public static SkylineJsonToolClient Connect(string pipeName, int timeoutMillis = 5000)
        {
            var pipe = new NamedPipeClientStream(@".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                pipe.Connect(timeoutMillis);
                pipe.ReadMode = PipeTransmissionMode.Message;
                return new SkylineJsonToolClient(pipe);
            }
            catch
            {
                pipe.Dispose(); // Do not leak the handle when the connection never became a client.
                throw;
            }
        }

        /// <summary>
        /// Wraps a pipe the caller connected itself. Prefer <see cref="Connect(string,int)"/>, which opens the pipe
        /// the way this class needs it; this overload exists for a caller that must own the connection.
        /// </summary>
        /// <exception cref="ArgumentException">The pipe was not opened for overlapped I/O, or is not in message
        /// read mode -- see <see cref="Connect(string,int)"/> for why both matter.</exception>
        public SkylineJsonToolClient(NamedPipeClientStream pipe)
        {
            if (pipe == null)
                throw new ArgumentNullException(nameof(pipe));
            if (!pipe.IsAsync)
                throw new ArgumentException(
                    @"The pipe must be opened with PipeOptions.Asynchronous, or a call can never be abandoned.",
                    nameof(pipe));
            // ReadMode can only be read once connected, and every caller connects before wrapping.
            if (pipe.IsConnected && pipe.ReadMode != PipeTransmissionMode.Message)
                throw new ArgumentException(
                    @"The pipe must be in message read mode, or a response longer than one buffer is truncated.",
                    nameof(pipe));
            _pipe = pipe;
        }

        // --- IJsonToolService implementation ---

        // 0-arg methods
        public string GetDocumentPath() { return Call(nameof(GetDocumentPath)); }
        public string GetVersion() { return Call(nameof(GetVersion)); }
        public SelectionInfo GetSelection() { return CallTyped<SelectionInfo>(nameof(GetSelection)); }
        public string GetSelectionText() { return Call(nameof(GetSelectionText)); }
        public string GetReplicateName() { return Call(nameof(GetReplicateName)); }
        public string[] GetReplicateNames() { return CallTyped<string[]>(nameof(GetReplicateNames)); }
        public DocumentStatus GetDocumentStatus() { return CallTyped<DocumentStatus>(nameof(GetDocumentStatus)); }
        public string[] GetSettingsListTypes() { return CallTyped<string[]>(nameof(GetSettingsListTypes)); }
        public TutorialListItem[] GetAvailableTutorials() { return CallTyped<TutorialListItem[]>(nameof(GetAvailableTutorials)); }
        public string GetProcessId() { return Call(nameof(GetProcessId)); }
        public int ModalNestingCount() { return CallTyped<int>(nameof(ModalNestingCount)); }
        public FormInfo[] GetOpenForms() { return CallTyped<FormInfo[]>(nameof(GetOpenForms)); }
        public ControlInfo[] GetControls(string formId) { return CallTyped<ControlInfo[]>(nameof(GetControls), formId); }
        // Returns the result as raw JSON text (object/array) or a string; the caller interprets it by action.
        public object PerformAction(UiElementPath path, string action, object value) { return Call(nameof(PerformAction), path, action, value); }
        public string GetUiMode() { return Call(nameof(GetUiMode)); }
        public UndoRedoEntry[] GetUndoRedo() { return CallTyped<UndoRedoEntry[]>(nameof(GetUndoRedo)); }

        public ReportDocTopicSummary[] GetReportDocTopics(string dataSource = null)
        {
            return dataSource == null
                ? CallTyped<ReportDocTopicSummary[]>(nameof(GetReportDocTopics))
                : CallTyped<ReportDocTopicSummary[]>(nameof(GetReportDocTopics), dataSource);
        }

        // UI interaction
        public ActionResult ClickMainMenuItem(string menuPath) { return CallTyped<ActionResult>(nameof(ClickMainMenuItem), menuPath); }
        public ActionResult ClickFormButton(string formId, string button) { return CallTyped<ActionResult>(nameof(ClickFormButton), formId, button); }
        public ActionResult ClickControlMenuItem(string formId, string control, string menuPath) { return CallTyped<ActionResult>(nameof(ClickControlMenuItem), formId, control, menuPath); }
        public ActionResult SetFormValue(string formId, string controlId, string value) { return CallTyped<ActionResult>(nameof(SetFormValue), formId, controlId, value); }
        public string GetFormValue(string formId, string controlId) { return Call(nameof(GetFormValue), formId, controlId); }
        public string[] GetOptions(string formId, string controlId) { return CallTyped<string[]>(nameof(GetOptions), formId, controlId); }
        public ActionResult SetGridText(string formId, string controlId, string text) { return CallTyped<ActionResult>(nameof(SetGridText), formId, controlId, text); }
        public ActionResult SetCurrentCellAddress(string formId, string controlId, int column, int row) { return CallTyped<ActionResult>(nameof(SetCurrentCellAddress), formId, controlId, column, row); }
        public string GetGridText(string formId, string gridId) { return Call(nameof(GetGridText), formId, gridId); }
        public ActionResult DismissWithButton(string formId, string button) { return CallTyped<ActionResult>(nameof(DismissWithButton), formId, button); }
        public ActionResult DismissWithCancelButton(string formId) { return CallTyped<ActionResult>(nameof(DismissWithCancelButton), formId); }
        public ActionResult DismissWithAcceptButton(string formId) { return CallTyped<ActionResult>(nameof(DismissWithAcceptButton), formId); }

        // 1-arg methods
        public string GetSelectedElementLocator(string elementType)
        {
            return Call(nameof(GetSelectedElementLocator), elementType);
        }
        public string RunCommand(string[] args)
        {
            return Call(nameof(RunCommand), (object) args);
        }
        public string RunCommandSilent(string[] args)
        {
            return Call(nameof(RunCommandSilent), (object) args);
        }
        public string[] GetSettingsListNames(string listType, string groupName = null)
        {
            return groupName == null
                ? CallTyped<string[]>(nameof(GetSettingsListNames), listType)
                : CallTyped<string[]>(nameof(GetSettingsListNames), listType, groupName);
        }
        public string[] GetSettingsListSelectedItems(string listType)
        {
            return CallTyped<string[]>(nameof(GetSettingsListSelectedItems), listType);
        }

        public ReportDocTopicDetail GetReportDocTopic(string topicName, string dataSource = null)
        {
            return dataSource == null
                ? CallTyped<ReportDocTopicDetail>(nameof(GetReportDocTopic), topicName)
                : CallTyped<ReportDocTopicDetail>(nameof(GetReportDocTopic), topicName, dataSource);
        }

        public void AddReportFromDefinition(ReportDefinition definition)
        {
            Call(nameof(AddReportFromDefinition), definition);
        }

        public void InsertSmallMoleculeTransitionList(string textCSV)
        {
            Call(nameof(InsertSmallMoleculeTransitionList), textCSV);
        }
        public void ImportProperties(string csvText) { Call(nameof(ImportProperties), csvText); }
        public void SetReplicate(string replicateName) { Call(nameof(SetReplicate), replicateName); }
        public void SetUiMode(string mode) { Call(nameof(SetUiMode), mode); }
        public void SetUndoRedoPosition(int index) { Call(nameof(SetUndoRedoPosition), index); }
        public string GetDocumentSettings(string filePath) { return Call(nameof(GetDocumentSettings), filePath); }
        public string GetDefaultSettings(string filePath) { return Call(nameof(GetDefaultSettings), filePath); }
        public void ReorderElements(string[] elementLocators) { Call(nameof(ReorderElements), (object) elementLocators); }

        // 2-arg methods
        public LocationEntry[] GetLocations(string level, string rootLocator = null)
        {
            return rootLocator == null
                ? CallTyped<LocationEntry[]>(nameof(GetLocations), level)
                : CallTyped<LocationEntry[]>(nameof(GetLocations), level, rootLocator);
        }

        public void SetSelectedElement(string elementLocator, string additionalLocators = null)
        {
            if (additionalLocators == null)
                Call(nameof(SetSelectedElement), elementLocator);
            else
                Call(nameof(SetSelectedElement), elementLocator, additionalLocators);
        }

        public string GetGraphData(string graphId, string filePath = null)
        {
            return filePath == null
                ? Call(nameof(GetGraphData), graphId)
                : Call(nameof(GetGraphData), graphId, filePath);
        }

        public string GetGraphImage(string graphId, string filePath = null)
        {
            return filePath == null
                ? Call(nameof(GetGraphImage), graphId)
                : Call(nameof(GetGraphImage), graphId, filePath);
        }

        public ImageBytesMetadata GetGraphImageBytes(string graphId)
        {
            return CallTyped<ImageBytesMetadata>(nameof(GetGraphImageBytes), graphId);
        }

        public string GetFormImage(string formId, string filePath = null)
        {
            return filePath == null
                ? Call(nameof(GetFormImage), formId)
                : Call(nameof(GetFormImage), formId, filePath);
        }

        public ImageBytesMetadata GetFormImageBytes(string formId)
        {
            return CallTyped<ImageBytesMetadata>(nameof(GetFormImageBytes), formId);
        }

        public string GetSettingsListItem(string listType, string itemName)
        {
            return Call(nameof(GetSettingsListItem), listType, itemName);
        }

        public void SelectSettingsListItems(string listType, string[] itemNames)
        {
            Call(nameof(SelectSettingsListItems), listType, itemNames);
        }

        public void ImportFasta(string textFasta, string keepEmptyProteins = null)
        {
            if (keepEmptyProteins == null)
                Call(nameof(ImportFasta), textFasta);
            else
                Call(nameof(ImportFasta), textFasta, keepEmptyProteins);
        }

        // 3-arg methods
        public ReportMetadata ExportReport(string reportName, string filePath, string culture)
        {
            return CallTyped<ReportMetadata>(nameof(ExportReport), reportName, filePath, culture);
        }

        public ReportMetadata ExportReportFromDefinition(ReportDefinition definition,
            string filePath, string culture)
        {
            return CallTyped<ReportMetadata>(nameof(ExportReportFromDefinition),
                definition, filePath, culture);
        }

        public TutorialMetadata GetTutorial(string name, string language = "en", string filePath = null)
        {
            return CallTyped<TutorialMetadata>(nameof(GetTutorial), name, language, filePath);
        }

        public void AddSettingsListItem(string listType, string itemXml, bool overwrite = false)
        {
            if (overwrite)
                Call(nameof(AddSettingsListItem), listType, itemXml, true);
            else
                Call(nameof(AddSettingsListItem), listType, itemXml);
        }

        // 4-arg methods
        public TutorialImageMetadata GetTutorialImage(string name, string imageFilename,
            string language = "en", string filePath = null)
        {
            return CallTyped<TutorialImageMetadata>(nameof(GetTutorialImage),
                name, imageFilename, language, filePath);
        }

        public ImageBytesMetadata GetTutorialImageBytes(string name, string imageFilename,
            string language = "en")
        {
            return CallTyped<ImageBytesMetadata>(nameof(GetTutorialImageBytes),
                name, imageFilename, language);
        }

        // 5-arg methods
        public ReportRowsResult GetReportFromDefinitionRows(ReportDefinition definition,
            int offset, int count, bool includeMaxLength, string culture)
        {
            return CallTyped<ReportRowsResult>(nameof(GetReportFromDefinitionRows),
                definition, offset, count, includeMaxLength, culture);
        }

        // 7-arg methods
        public ReportRowsResult GetReportRows(string reportName, int offset, int count,
            string[] columns, ReportFilter[] filter, bool includeMaxLength, string culture)
        {
            return CallTyped<ReportRowsResult>(nameof(GetReportRows),
                reportName, offset, count, columns, filter, includeMaxLength, culture);
        }

        // --- JSON-RPC 2.0 transport ---

        private string Call(string method, params object[] args)
        {
            // Build JSON-RPC 2.0 request
            object request = LoggingEnabled
                // ReSharper disable once RedundantCast (for .NET 8.0)
                ? (object) new { jsonrpc = JsonToolConstants.JSONRPC_VERSION, method, @params = args, id = 1, _log = true }
                : new { jsonrpc = JsonToolConstants.JSONRPC_VERSION, method, @params = args, id = 1 };
            byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, _snakeCaseOptions));
            _pipe.Write(requestBytes, 0, requestBytes.Length);
            _pipe.Flush();
            _pipe.WaitForPipeDrain();

            byte[] responseBytes = ReadAllBytes(_pipe);
            string responseJson = Encoding.UTF8.GetString(responseBytes);

            using (var doc = JsonDocument.Parse(responseJson))
            {
                var root = doc.RootElement;

                LastLog = root.TryGetProperty(nameof(JSON_RPC._log), out var logElement)
                    ? logElement.GetString()
                    : null;

                if (root.TryGetProperty(nameof(JSON_RPC.error), out var errorElement))
                {
                    string message = errorElement.TryGetProperty(nameof(JSON_RPC.message), out var msgElement)
                        ? msgElement.GetString()
                        : "Unknown error from Skyline";
                    int code = errorElement.TryGetProperty(nameof(JSON_RPC.code), out var codeElement) &&
                               codeElement.ValueKind == JsonValueKind.Number
                        ? codeElement.GetInt32()
                        : JsonToolConstants.ERROR_INTERNAL;
                    throw new JsonRpcException(code, message);
                }

                if (root.TryGetProperty(nameof(JSON_RPC.result), out var resultElement))
                {
                    switch (resultElement.ValueKind)
                    {
                        case JsonValueKind.Null:
                            return null;
                        // Anything that is not a JSON string keeps its JSON form: a number, a bool (what
                        // get_value returns for a check box or radio button), or a whole object or array.
                        // GetString() throws on all of these, so none of them may fall through to it.
                        case JsonValueKind.Number:
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                        case JsonValueKind.Object:
                        case JsonValueKind.Array:
                            return resultElement.GetRawText();
                        default:
                            return resultElement.GetString();
                    }
                }

                return null;
            }
        }

        private T CallTyped<T>(string method, params object[] args)
        {
            string json = Call(method, args);
            if (string.IsNullOrEmpty(json))
                return default(T);
            return JsonSerializer.Deserialize<T>(json, _snakeCaseOptions);
        }

        public void Dispose()
        {
            _pipe.Dispose();
        }

        // Reads the response, honoring CancellationToken so a caller that has waited long enough can abandon the call
        // (see CancellationToken). ReadAsync is what makes that possible: on a pipe opened Asynchronous it is real
        // overlapped I/O, so cancelling it releases the handle and the connection can then actually be dropped.
        private byte[] ReadAllBytes(PipeStream stream)
        {
            var memoryStream = new MemoryStream();
            do
            {
                var buffer = new byte[65536];
                int count = stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken)
                    .GetAwaiter().GetResult();
                if (count == 0)
                    return memoryStream.ToArray();
                memoryStream.Write(buffer, 0, count);
            } while (!stream.IsMessageComplete);
            return memoryStream.ToArray();
        }
    }
}
