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

namespace SkylineTool
{
    /// <summary>
    /// Attribute providing a stable, culture-invariant, user-friendly name for settings
    /// list classes. Used by the MCP/JSON tool layer so LLMs can refer to settings lists
    /// by recognizable names (e.g. "Isotope Modifications") instead of internal class
    /// names (e.g. "HeavyModList").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LlmNameAttribute : Attribute
    {
        public string Name { get; }
        public LlmNameAttribute(string name) { Name = name; }
    }

    /// <summary>
    /// Contract for the JSON tool service hosted in Skyline.
    /// Method names are used via nameof() for compile-time checked dispatch.
    /// JsonToolServer implements this interface.
    /// </summary>
    public interface IJsonToolService
    {
        // 0-arg methods
        string GetDocumentPath();
        string GetVersion();
        SelectionInfo GetSelection();
        string GetSelectionText();
        string GetReplicateName();
        string[] GetReplicateNames();
        DocumentStatus GetDocumentStatus();
        string[] GetSettingsListTypes();
        TutorialListItem[] GetAvailableTutorials();
        ReportDocTopicSummary[] GetReportDocTopics(string dataSource = null);
        string GetProcessId();
        FormInfo[] GetOpenForms();

        // 1-arg methods
        string GetSelectedElementLocator(string elementType);
        string RunCommand(string[] args);
        string RunCommandSilent(string[] args);
        string[] GetSettingsListNames(string listType, string groupName = null);
        string[] GetSettingsListSelectedItems(string listType);
        ReportDocTopicDetail GetReportDocTopic(string topicName, string dataSource = null);
        void AddReportFromDefinition(ReportDefinition definition);
        void InsertSmallMoleculeTransitionList(string textCSV);
        void ImportProperties(string csvText);
        void SetReplicate(string replicateName);
        string GetDocumentSettings(string filePath);
        string GetDefaultSettings(string filePath);

        // 2-arg methods
        LocationEntry[] GetLocations(string level, string rootLocator = null);
        void SetSelectedElement(string elementLocator, string additionalLocators = null);
        string GetGraphData(string graphId, string filePath = null);
        string GetGraphImage(string graphId, string filePath = null);
        string GetFormImage(string formId, string filePath = null);
        string GetSettingsListItem(string listType, string itemName);
        void SelectSettingsListItems(string listType, string[] itemNames);
        void ImportFasta(string textFasta, string keepEmptyProteins = null);

        // 3-arg methods
        ReportMetadata ExportReport(string reportName, string filePath, string culture);
        ReportMetadata ExportReportFromDefinition(ReportDefinition definition, string filePath, string culture);
        TutorialMetadata GetTutorial(string name, string language = "en", string filePath = null);
        void AddSettingsListItem(string listType, string itemXml, bool overwrite = false);

        // 4-arg methods
        TutorialImageMetadata GetTutorialImage(string name, string imageFilename, string language = "en", string filePath = null);
    }

    /// <summary>
    /// Shared constants for JSON property names and API values used across
    /// Skyline, SkylineMcpConnector, and SkylineMcpServer.
    /// </summary>
    public static class JsonToolConstants
    {
        // ReSharper disable InconsistentNaming

        /// <summary>Connection file and MCP status property names (use nameof() as keys).</summary>
        public enum JSON
        {
            pipe_name, process_id, connected_at, skyline_version,     // connection file
            status, auto_connect, version,                            // MCP connector status
        }

        /// <summary>JSON-RPC 2.0 protocol field names (use nameof() as keys).</summary>
        public enum JSON_RPC
        {
            jsonrpc, method, @params, id, result, error, code, message, _log,
        }

        // ReSharper restore InconsistentNaming

        // --- JSON-RPC 2.0 protocol constants ---

        public const string JSONRPC_VERSION = @"2.0";
        public const int ERROR_METHOD_NOT_FOUND = -32601;
        public const int ERROR_INVALID_PARAMS = -32602;
        public const int ERROR_INTERNAL = -32603;

        // --- API value constants ---

        public const string LEVEL_GROUP = @"group";
        public const string LEVEL_MOLECULE = @"molecule";
        public const string LEVEL_PRECURSOR = @"precursor";
        public const string LEVEL_TRANSITION = @"transition";

        public const string CULTURE_INVARIANT = @"invariant";
        public const string CULTURE_LOCALIZED = @"localized";

        public const string SORT_ASC = @"asc";
        public const string SORT_DESC = @"desc";

        public const string DEFAULT_REPORT_NAME = @"Custom";

        // --- Connection file infrastructure ---

        public const string DEPLOY_FOLDER_NAME = @".skyline-mcp";
        public const string CONNECTION_FILE_PREFIX = @"connection-";
        public const string CONNECTION_FILE_EXT = @".json";
        public const string JSON_PIPE_PREFIX = @"SkylineMcpJson-";

        public static string GetJsonPipeName(string legacyToolServiceName)
        {
            return JSON_PIPE_PREFIX + legacyToolServiceName.Replace(@"-", string.Empty);
        }

        public static string GetConnectionDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DEPLOY_FOLDER_NAME);
        }

        public static string GetConnectionFilePath(string pipeName)
        {
            return Path.Combine(GetConnectionDirectory(),
                CONNECTION_FILE_PREFIX + pipeName + CONNECTION_FILE_EXT);
        }
    }
}
