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
using JSON_RPC = SkylineTool.JsonToolConstants.JSON_RPC;

namespace SkylineTool
{
    /// <summary>
    /// JSON-RPC 2.0 client for the Skyline JSON tool service.
    /// Connects to a Skyline instance via named pipe and provides a fully
    /// typed IJsonToolService proxy. Replaces SkylineToolClient (which uses
    /// the deprecated BinaryFormatter transport) with modern JSON-RPC.
    ///
    /// Link-compiled into both .NET Framework 4.7.2 (SkylineAiConnector)
    /// and .NET 8.0 (SkylineMcpServer). Uses System.Text.Json.
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

        public SkylineJsonToolClient(NamedPipeClientStream pipe)
        {
            _pipe = pipe;
        }

        // --- IJsonToolService implementation ---

        // 0-arg methods
        public string GetDocumentPath() { return Call(nameof(GetDocumentPath)); }
        public string GetVersion() { return Call(nameof(GetVersion)); }
        public string GetSelection() { return Call(nameof(GetSelection)); }
        public string GetSelectionText() { return Call(nameof(GetSelectionText)); }
        public string GetReplicateName() { return Call(nameof(GetReplicateName)); }
        public string[] GetReplicateNames() { return CallTyped<string[]>(nameof(GetReplicateNames)); }
        public string GetDocumentStatus() { return Call(nameof(GetDocumentStatus)); }
        public string[] GetSettingsListTypes() { return CallTyped<string[]>(nameof(GetSettingsListTypes)); }
        public TutorialListItem[] GetAvailableTutorials() { return CallTyped<TutorialListItem[]>(nameof(GetAvailableTutorials)); }
        public string GetProcessId() { return Call(nameof(GetProcessId)); }
        public FormInfo[] GetOpenForms() { return CallTyped<FormInfo[]>(nameof(GetOpenForms)); }

        public ReportDocTopicSummary[] GetReportDocTopics(string scope = null)
        {
            return scope == null
                ? CallTyped<ReportDocTopicSummary[]>(nameof(GetReportDocTopics))
                : CallTyped<ReportDocTopicSummary[]>(nameof(GetReportDocTopics), scope);
        }

        // 1-arg methods
        public string GetSelectedElementLocator(string elementType)
        {
            return Call(nameof(GetSelectedElementLocator), elementType);
        }
        public string RunCommand(string[] args)
        {
            return Call(nameof(RunCommand), JsonSerializer.Serialize(args));
        }
        public string RunCommandSilent(string[] args)
        {
            return Call(nameof(RunCommandSilent), JsonSerializer.Serialize(args));
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

        public string GetReportDocTopic(string topicName, string scope = null)
        {
            return scope == null
                ? Call(nameof(GetReportDocTopic), topicName)
                : Call(nameof(GetReportDocTopic), topicName, scope);
        }

        public string AddReportFromDefinition(ReportDefinition definition)
        {
            return Call(nameof(AddReportFromDefinition),
                JsonSerializer.Serialize(definition, _snakeCaseOptions));
        }

        public string InsertSmallMoleculeTransitionList(string textCSV)
        {
            return Call(nameof(InsertSmallMoleculeTransitionList), textCSV);
        }
        public string ImportProperties(string csvText) { return Call(nameof(ImportProperties), csvText); }
        public string SetReplicate(string replicateName) { return Call(nameof(SetReplicate), replicateName); }
        public string GetDocumentSettings(string filePath) { return Call(nameof(GetDocumentSettings), filePath); }
        public string GetDefaultSettings(string filePath) { return Call(nameof(GetDefaultSettings), filePath); }

        // 2-arg methods
        public LocationEntry[] GetLocations(string level, string rootLocator = null)
        {
            return rootLocator == null
                ? CallTyped<LocationEntry[]>(nameof(GetLocations), level)
                : CallTyped<LocationEntry[]>(nameof(GetLocations), level, rootLocator);
        }

        public string SetSelectedElement(string elementLocator, string additionalLocators = null)
        {
            return additionalLocators == null
                ? Call(nameof(SetSelectedElement), elementLocator)
                : Call(nameof(SetSelectedElement), elementLocator, additionalLocators);
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

        public string GetFormImage(string formId, string filePath = null)
        {
            return filePath == null
                ? Call(nameof(GetFormImage), formId)
                : Call(nameof(GetFormImage), formId, filePath);
        }

        public string GetSettingsListItem(string listType, string itemName)
        {
            return Call(nameof(GetSettingsListItem), listType, itemName);
        }

        public string SelectSettingsListItems(string listType, string[] itemNames)
        {
            return Call(nameof(SelectSettingsListItems), listType,
                JsonSerializer.Serialize(itemNames));
        }

        public string ImportFasta(string textFasta, string keepEmptyProteins = null)
        {
            return keepEmptyProteins == null
                ? Call(nameof(ImportFasta), textFasta)
                : Call(nameof(ImportFasta), textFasta, keepEmptyProteins);
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
                JsonSerializer.Serialize(definition, _snakeCaseOptions), filePath, culture);
        }

        public TutorialMetadata GetTutorial(string name, string language = "en", string filePath = null)
        {
            return CallTyped<TutorialMetadata>(nameof(GetTutorial), name, language, filePath);
        }

        public string AddSettingsListItem(string listType, string itemXml, bool overwrite = false)
        {
            return overwrite
                ? Call(nameof(AddSettingsListItem), listType, itemXml, "true")
                : Call(nameof(AddSettingsListItem), listType, itemXml);
        }

        // 4-arg methods
        public TutorialImageMetadata GetTutorialImage(string name, string imageFilename,
            string language = "en", string filePath = null)
        {
            return CallTyped<TutorialImageMetadata>(nameof(GetTutorialImage),
                name, imageFilename, language, filePath);
        }

        // --- JSON-RPC 2.0 transport ---

        private string Call(string method, params string[] args)
        {
            // Build JSON-RPC 2.0 request
            object request = LoggingEnabled
                ? (object)new { jsonrpc = JsonToolConstants.JSONRPC_VERSION, method, @params = args, id = 1, _log = true }
                : new { jsonrpc = JsonToolConstants.JSONRPC_VERSION, method, @params = args, id = 1 };
            byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
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
                    throw new InvalidOperationException(message);
                }

                if (root.TryGetProperty(nameof(JSON_RPC.result), out var resultElement))
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
        }

        private T CallTyped<T>(string method, params string[] args)
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
