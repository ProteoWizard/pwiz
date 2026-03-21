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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkylineTool;

namespace SkylineMcpServer;

/// <summary>
/// MCP-specific connection manager for Skyline instances.
/// Handles instance discovery, targeting, and diagnostic logging.
/// Delegates all IJsonToolService calls to <see cref="SkylineJsonToolClient"/>.
/// </summary>
public class SkylineConnection : IJsonToolService, IDisposable
{
    private readonly SkylineJsonToolClient _client;

    /// <summary>
    /// Identity string from the connected Skyline instance (e.g.,
    /// "Skyline-daily (64-bit) 26.1.1.238"). Readable after Dispose
    /// for error enrichment.
    /// </summary>
    public string SkylineVersion { get; private set; }

    /// <summary>
    /// When set, TryConnect targets this specific Skyline process instead of the
    /// most recently started one. Set via the skyline_set_instance MCP tool.
    /// </summary>
    public static int? TargetProcessId { get; set; }

    /// <summary>
    /// When true, requests include diagnostic logging.
    /// Set via the skyline_set_logging MCP tool.
    /// </summary>
    public static bool LoggingEnabled { get; set; }

    /// <summary>
    /// Diagnostic log content from the most recent response, or null if absent.
    /// </summary>
    public static string LastLog { get; set; }

    private SkylineConnection(SkylineJsonToolClient client)
    {
        _client = client;
        _client.LoggingEnabled = LoggingEnabled;
    }

    // --- IJsonToolService delegation to SkylineJsonToolClient ---

    // 0-arg methods
    public string GetDocumentPath() { return CallClient(c => c.GetDocumentPath()); }
    public string GetVersion() { return CallClient(c => c.GetVersion()); }
    public SelectionInfo GetSelection() { return CallClient(c => c.GetSelection()); }
    public string GetSelectionText() { return CallClient(c => c.GetSelectionText()); }
    public string GetReplicateName() { return CallClient(c => c.GetReplicateName()); }
    public string[] GetReplicateNames() { return CallClient(c => c.GetReplicateNames()); }
    public DocumentStatus GetDocumentStatus() { return CallClient(c => c.GetDocumentStatus()); }
    public string[] GetSettingsListTypes() { return CallClient(c => c.GetSettingsListTypes()); }
    public TutorialListItem[] GetAvailableTutorials() { return CallClient(c => c.GetAvailableTutorials()); }
    public ReportDocTopicSummary[] GetReportDocTopics(string dataSource = null) { return CallClient(c => c.GetReportDocTopics(dataSource)); }
    public string GetProcessId() { return CallClient(c => c.GetProcessId()); }
    public FormInfo[] GetOpenForms() { return CallClient(c => c.GetOpenForms()); }

    // 1-arg methods
    public string GetSelectedElementLocator(string elementType) { return CallClient(c => c.GetSelectedElementLocator(elementType)); }
    public string RunCommand(string[] args) { return CallClient(c => c.RunCommand(args)); }
    public string RunCommandSilent(string[] args) { return CallClient(c => c.RunCommandSilent(args)); }
    public string[] GetSettingsListNames(string listType, string groupName = null) { return CallClient(c => c.GetSettingsListNames(listType, groupName)); }
    public string[] GetSettingsListSelectedItems(string listType) { return CallClient(c => c.GetSettingsListSelectedItems(listType)); }
    public ReportDocTopicDetail GetReportDocTopic(string topicName, string dataSource = null) { return CallClient(c => c.GetReportDocTopic(topicName, dataSource)); }
    public void AddReportFromDefinition(ReportDefinition definition) { CallClientVoid(c => c.AddReportFromDefinition(definition)); }
    public void InsertSmallMoleculeTransitionList(string textCSV) { CallClientVoid(c => c.InsertSmallMoleculeTransitionList(textCSV)); }
    public void ImportProperties(string csvText) { CallClientVoid(c => c.ImportProperties(csvText)); }
    public void SetReplicate(string replicateName) { CallClientVoid(c => c.SetReplicate(replicateName)); }
    public string GetDocumentSettings(string filePath) { return CallClient(c => c.GetDocumentSettings(filePath)); }
    public string GetDefaultSettings(string filePath) { return CallClient(c => c.GetDefaultSettings(filePath)); }

    // 2-arg methods
    public LocationEntry[] GetLocations(string level, string rootLocator = null) { return CallClient(c => c.GetLocations(level, rootLocator)); }
    public void SetSelectedElement(string elementLocator, string additionalLocators = null) { CallClientVoid(c => c.SetSelectedElement(elementLocator, additionalLocators)); }
    public string GetGraphData(string graphId, string filePath = null) { return CallClient(c => c.GetGraphData(graphId, filePath)); }
    public string GetGraphImage(string graphId, string filePath = null) { return CallClient(c => c.GetGraphImage(graphId, filePath)); }
    public string GetFormImage(string formId, string filePath = null) { return CallClient(c => c.GetFormImage(formId, filePath)); }
    public string GetSettingsListItem(string listType, string itemName) { return CallClient(c => c.GetSettingsListItem(listType, itemName)); }
    public void SelectSettingsListItems(string listType, string[] itemNames) { CallClientVoid(c => c.SelectSettingsListItems(listType, itemNames)); }
    public void ImportFasta(string textFasta, string keepEmptyProteins = null) { CallClientVoid(c => c.ImportFasta(textFasta, keepEmptyProteins)); }

    // 3-arg methods
    public ReportMetadata ExportReport(string reportName, string filePath, string culture) { return CallClient(c => c.ExportReport(reportName, filePath, culture)); }
    public ReportMetadata ExportReportFromDefinition(ReportDefinition definition, string filePath, string culture) { return CallClient(c => c.ExportReportFromDefinition(definition, filePath, culture)); }
    public TutorialMetadata GetTutorial(string name, string language = "en", string filePath = null) { return CallClient(c => c.GetTutorial(name, language, filePath)); }
    public void AddSettingsListItem(string listType, string itemXml, bool overwrite = false) { CallClientVoid(c => c.AddSettingsListItem(listType, itemXml, overwrite)); }

    // 4-arg methods
    public TutorialImageMetadata GetTutorialImage(string name, string imageFilename, string language = "en", string filePath = null) { return CallClient(c => c.GetTutorialImage(name, imageFilename, language, filePath)); }

    /// <summary>
    /// Delegates to the client and captures the diagnostic log.
    /// </summary>
    private T CallClient<T>(Func<SkylineJsonToolClient, T> action)
    {
        var result = action(_client);
        LastLog = _client.LastLog;
        return result;
    }

    /// <summary>
    /// Delegates a void call to the client and captures the diagnostic log.
    /// </summary>
    private void CallClientVoid(Action<SkylineJsonToolClient> action)
    {
        action(_client);
        LastLog = _client.LastLog;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    // --- MCP-specific connection management ---

    /// <summary>
    /// Human-readable status message describing the current Skyline connection state.
    /// Returns null when connected; returns a message when not connected.
    /// </summary>
    public static string GetConnectionStatus()
    {
        var infos = FindConnectionFiles();
        if (infos.Count > 0)
            return null; // At least one live Skyline instance

        return "No Skyline instance is connected. " +
               "Start Skyline and choose Tools > AI Connector to connect.";
    }

    /// <summary>
    /// Connect to a Skyline instance. If TargetProcessId is set, connects to that
    /// specific instance; otherwise connects to the most recently started one.
    /// Returns null with a status message when no Skyline is available.
    /// </summary>
    public static (SkylineConnection Connection, string Error) TryConnect()
    {
        var infos = FindConnectionFiles();
        if (infos.Count == 0)
        {
            return (null, "No Skyline instance is connected. " +
                          "Start Skyline and choose Tools > AI Connector to connect.");
        }

        // If a specific instance is targeted, try it first
        if (TargetProcessId.HasValue)
        {
            var targeted = infos.FirstOrDefault(i => i.ProcessId == TargetProcessId.Value);
            if (targeted != null)
                return TryConnectToInstance(targeted);
            TargetProcessId = null; // Target no longer exists
        }

        // Sort by connected_at descending to prefer the most recent
        foreach (var info in infos.OrderByDescending(i => i.ConnectedAt))
        {
            var result2 = TryConnectToInstance(info);
            if (result2.Connection != null)
                return result2;

            if (result2.Error != null && result2.Error.Contains("not responding"))
                return result2;
        }

        // All connections failed
        return (null, "No Skyline instance is connected. " +
                      "Start Skyline and choose Tools > AI Connector to connect.");
    }

    /// <summary>
    /// Get information about all available Skyline instances, including document paths
    /// queried from each live instance.
    /// </summary>
    public static List<InstanceInfo> GetAvailableInstances()
    {
        var infos = FindConnectionFiles();
        var results = new List<InstanceInfo>();

        foreach (var info in infos)
        {
            string documentPath = null;
            try
            {
                var (connection, _) = TryConnectToInstance(info);
                if (connection != null)
                {
                    using (connection)
                    {
                        documentPath = connection.GetDocumentPath();
                    }
                }
            }
            catch
            {
                // Best effort - document path will be null
            }

            results.Add(new InstanceInfo
            {
                ProcessId = info.ProcessId,
                SkylineVersion = info.SkylineVersion,
                ConnectedAt = info.ConnectedAt,
                DocumentPath = documentPath,
                IsTargeted = TargetProcessId.HasValue && TargetProcessId.Value == info.ProcessId
            });
        }

        return results;
    }

    private static (SkylineConnection Connection, string Error) TryConnectToInstance(ConnectionInfo info)
    {
        var pipe = new NamedPipeClientStream(".", info.PipeName, PipeDirection.InOut);
        try
        {
            pipe.Connect(5000);
            pipe.ReadMode = PipeTransmissionMode.Message;
            var client = new SkylineJsonToolClient(pipe);
            return (new SkylineConnection(client) { SkylineVersion = info.SkylineVersion }, null);
        }
        catch (TimeoutException)
        {
            pipe.Dispose();
            return (null, "Skyline is not responding. " +
                          "It may be busy processing data or showing a dialog. Try again in a moment.");
        }
        catch (IOException)
        {
            pipe.Dispose();
            return (null, null); // Connection failed silently - try next
        }
    }

    /// <summary>
    /// Find all connection files, cleaning up stale entries whose processes are no longer running.
    /// </summary>
    private static List<ConnectionInfo> FindConnectionFiles()
    {
        string dir = JsonToolConstants.GetConnectionDirectory();
        if (!Directory.Exists(dir))
            return new List<ConnectionInfo>();

        var results = new List<ConnectionInfo>();

        // Scan for connection-*.json files, cleaning up stale entries
        foreach (string file in Directory.GetFiles(dir,
            JsonToolConstants.CONNECTION_FILE_PREFIX + "*" + JsonToolConstants.CONNECTION_FILE_EXT))
        {
            var info = TryLoadConnectionFile(file);
            if (info == null)
                continue;
            if (IsSkylineProcess(info.ProcessId))
                results.Add(info);
            else
                TryDeleteFile(file);
        }

        return results;
    }

    private static ConnectionInfo TryLoadConnectionFile(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var info = JsonSerializer.Deserialize<ConnectionInfo>(json);
            if (info != null)
                info.FilePath = path;
            return info;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSkylineProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return Program.FunctionalTest ||
                   process.ProcessName.StartsWith("Skyline", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch { /* Best effort */ }
    }

    // POCO matching the Skyline connection file format
    private class ConnectionInfo
    {
        [JsonPropertyName("pipe_name")]
        public string PipeName { get; set; } = string.Empty;

        [JsonPropertyName("process_id")]
        public int ProcessId { get; set; }

        [JsonPropertyName("connected_at")]
        public string ConnectedAt { get; set; } = string.Empty;

        [JsonPropertyName("skyline_version")]
        public string SkylineVersion { get; set; } = string.Empty;

        [JsonIgnore]
        public string FilePath { get; set; }
    }

    public class InstanceInfo
    {
        public int ProcessId { get; set; }
        public string SkylineVersion { get; set; }
        public string ConnectedAt { get; set; }
        public string DocumentPath { get; set; }
        public bool IsTargeted { get; set; }
    }
}
