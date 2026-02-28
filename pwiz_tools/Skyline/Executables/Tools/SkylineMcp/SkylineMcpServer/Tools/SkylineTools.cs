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
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace SkylineMcpServer.Tools;

[McpServerToolType]
public static class SkylineTools
{
    [McpServerTool(Name = "skyline_get_document_path"),
     Description("Get the file path of the currently open Skyline document.")]
    public static string GetDocumentPath()
    {
        using var connection = SkylineConnection.Connect();
        string path = connection.Call("GetDocumentPath");
        return path ?? "No document is open in Skyline.";
    }

    [McpServerTool(Name = "skyline_get_version"),
     Description("Get the version of the running Skyline instance.")]
    public static string GetVersion()
    {
        using var connection = SkylineConnection.Connect();
        string version = connection.Call("GetVersion");
        return version ?? "Unknown version";
    }

    [McpServerTool(Name = "skyline_get_selection"),
     Description("Get the currently selected element in Skyline (protein, peptide, precursor, transition, etc.). Returns the name/description of whatever is selected in the document tree.")]
    public static string GetSelection()
    {
        using var connection = SkylineConnection.Connect();
        string selection = connection.Call("GetDocumentLocationName");
        return string.IsNullOrEmpty(selection)
            ? "Nothing is currently selected in Skyline."
            : selection;
    }

    [McpServerTool(Name = "skyline_get_replicate"),
     Description("Get the name of the currently active replicate in Skyline.")]
    public static string GetReplicate()
    {
        using var connection = SkylineConnection.Connect();
        string replicate = connection.Call("GetReplicateName");
        return string.IsNullOrEmpty(replicate)
            ? "No replicate is currently selected."
            : replicate;
    }

    [McpServerTool(Name = "skyline_get_report"),
     Description("Run a named Skyline report and return results. For large reports, saves full data to a CSV file and returns a summary with preview rows and the file path. Use the Read tool to explore the full dataset.")]
    public static string GetReport(
        [Description("The name of a Skyline report to run (e.g., 'Peak Area', 'Transition Results')")] string reportName,
        [Description("Output file path. If not specified, saves to a temp directory. Extension determines format (.csv, .tsv, .parquet).")] string filePath = null,
        [Description("Output format when filePath is not specified: csv, tsv, or parquet (default: csv)")] string format = "csv",
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        filePath ??= GetTempReportPath(reportName, format);
        string culture = invariant ? "invariant" : "localized";

        using var connection = SkylineConnection.Connect();
        string metadata = connection.Call("ExportReport", reportName, filePath, culture);
        return FormatReportResult(metadata);
    }

    [McpServerTool(Name = "skyline_get_report_from_definition"),
     Description("Run a custom Skyline report from an XML report definition and return results. Use this when you need specific columns not available in predefined reports. The XML format follows Skyline's report schema.")]
    public static string GetReportFromDefinition(
        [Description("XML report definition in Skyline report schema format")] string reportDefinitionXml,
        [Description("Output file path. If not specified, saves to a temp directory. Extension determines format (.csv, .tsv, .parquet).")] string filePath = null,
        [Description("Output format when filePath is not specified: csv, tsv, or parquet (default: csv)")] string format = "csv",
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        filePath ??= GetTempReportPath("Custom", format);
        string culture = invariant ? "invariant" : "localized";

        using var connection = SkylineConnection.Connect();
        string metadata = connection.Call("ExportReportFromDefinition", reportDefinitionXml, filePath, culture);
        return FormatReportResult(metadata);
    }

    [McpServerTool(Name = "skyline_get_settings_list_types"),
     Description("Enumerate all settings list types available in Skyline (enzymes, modifications, reports, etc.). Returns tab-separated lines of PropertyName and Title. Use this to discover what configuration lists exist before querying their contents.")]
    public static string GetSettingsListTypes()
    {
        using var connection = SkylineConnection.Connect();
        string result = connection.Call("GetSettingsListTypes");
        return string.IsNullOrEmpty(result)
            ? "No settings lists found."
            : result;
    }

    [McpServerTool(Name = "skyline_get_settings_list_names"),
     Description("Get item names from a specific settings list. Use skyline_get_settings_list_types first to discover available list types. For PersistedViews (reports), names are grouped by Main and External Tools sections.")]
    public static string GetSettingsListNames(
        [Description("The settings list property name (e.g., 'EnzymeList', 'PersistedViews')")] string listType)
    {
        using var connection = SkylineConnection.Connect();
        string result = connection.Call("GetSettingsListNames", listType);
        return string.IsNullOrEmpty(result)
            ? "No items found in " + listType + "."
            : result;
    }

    [McpServerTool(Name = "skyline_get_settings_list_item"),
     Description("Get the XML definition of a single item from a settings list. Useful for inspecting report definitions, enzyme cut rules, modification details, etc.")]
    public static string GetSettingsListItem(
        [Description("The settings list property name (e.g., 'EnzymeList', 'PersistedViews')")] string listType,
        [Description("The name of the item to retrieve (e.g., 'Trypsin', 'Peak Area')")] string itemName)
    {
        using var connection = SkylineConnection.Connect();
        string result = connection.Call("GetSettingsListItem", listType, itemName);
        return string.IsNullOrEmpty(result)
            ? "Item not found: " + itemName + " in " + listType + "."
            : result;
    }

    [McpServerTool(Name = "skyline_run_command"),
     Description("Run a command line against the running Skyline instance. Uses the same command syntax as SkylineCmd/SkylineRunner. Commands are echoed to Skyline's Immediate Window for user visibility. Examples: '--report-name=\"Peak Area\" --report-file=output.csv', '--import-file=results.raw', '--refine-cv-remove-above-cutoff=20'. Use '--help' to see all available commands.")]
    public static string RunCommand(
        [Description("Command line arguments in SkylineCmd format (e.g., '--report-name=\"Peak Area\" --report-file=output.csv')")] string commandArgs)
    {
        using var connection = SkylineConnection.Connect();
        string output = connection.Call("RunCommand", commandArgs);
        if (string.IsNullOrEmpty(output))
            return "Command completed with no output.";
        return output;
    }

    [McpServerTool(Name = "skyline_get_cli_help_sections"),
     Description("List available CLI help sections. Returns section names (one per line) that can be passed to skyline_get_cli_help for detailed help on each topic.")]
    public static string GetCliHelpSections()
    {
        using var connection = SkylineConnection.Connect();
        string output = connection.Call("RunCommandSilent", "--help=sections");
        if (string.IsNullOrEmpty(output))
            return "No help sections available.";
        return output;
    }

    [McpServerTool(Name = "skyline_get_cli_help"),
     Description("Get detailed CLI help for a specific section. Use skyline_get_cli_help_sections to discover available sections. Section matching is case-insensitive and supports partial matches.")]
    public static string GetCliHelp(
        [Description("The help section name (e.g., 'import', 'export', 'refine'). Case-insensitive partial match.")] string section)
    {
        using var connection = SkylineConnection.Connect();
        string output = connection.Call("RunCommandSilent", "--help=" + section + " --help=no-borders");
        if (string.IsNullOrEmpty(output))
            return "No help found for section: " + section;
        return output;
    }

    [McpServerTool(Name = "skyline_get_report_doc_topics"),
     Description("List available report column documentation topics. Returns tab-separated lines of DisplayName and QualifiedTypeName for each entity type (e.g., Molecule, Precursor, Transition). Use skyline_get_report_doc_topic to get column details for a specific topic.")]
    public static string GetReportDocTopics()
    {
        using var connection = SkylineConnection.Connect();
        string result = connection.Call("GetReportDocTopics");
        return string.IsNullOrEmpty(result)
            ? "No report documentation topics found."
            : result;
    }

    [McpServerTool(Name = "skyline_get_report_doc_topic"),
     Description("Get column documentation for a specific report entity type. Returns a table of column names, descriptions, and types. Use skyline_get_report_doc_topics to discover available topics.")]
    public static string GetReportDocTopic(
        [Description("The topic name (display name like 'Molecule' or qualified type name). Case-insensitive partial match on display name.")] string topic)
    {
        using var connection = SkylineConnection.Connect();
        string result = connection.Call("GetReportDocTopic", topic);
        return string.IsNullOrEmpty(result)
            ? "No documentation found for topic: " + topic
            : result;
    }

    private static string FormatReportResult(string metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
            return "Report returned no data. The report may not exist or the document may be empty.";

        using var doc = JsonDocument.Parse(metadataJson);
        var root = doc.RootElement;

        string filePath = root.TryGetProperty("file_path", out var fpEl) ? fpEl.GetString() : null;
        string reportName = root.TryGetProperty("report_name", out var rnEl) ? rnEl.GetString() : "Report";
        int rowCount = root.TryGetProperty("row_count", out var rcEl) ? rcEl.GetInt32() : -1;
        string columns = root.TryGetProperty("columns", out var colEl) ? colEl.GetString() : null;
        string preview = root.TryGetProperty("preview", out var pvEl) ? pvEl.GetString() : null;
        string format = root.TryGetProperty("format", out var fmtEl) ? fmtEl.GetString() : null;

        var sb = new StringBuilder();
        sb.AppendLine($"Report: {reportName}");
        if (rowCount >= 0)
            sb.AppendLine($"Rows: {rowCount}");
        if (columns != null)
            sb.AppendLine($"Columns: {columns}");
        if (format != null)
            sb.AppendLine($"Format: {format}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(preview))
        {
            sb.AppendLine("Preview:");
            sb.AppendLine(preview);
            if (rowCount > 5)
                sb.AppendLine($"... ({rowCount - 5} more rows)");
            sb.AppendLine();
        }

        if (filePath != null)
        {
            sb.AppendLine($"Full data saved to: {filePath}");
            sb.AppendLine("Use the Read tool to explore the full dataset.");
        }

        return sb.ToString();
    }

    private static string GetTempReportPath(string reportName, string format)
    {
        string tmpDir = Environment.GetEnvironmentVariable("SKYLINE_MCP_TMP_DIR");
        if (string.IsNullOrEmpty(tmpDir))
        {
            tmpDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Skyline", "mcp", "tmp");
        }
        Directory.CreateDirectory(tmpDir);

        string safeName = string.Join("_", reportName.Split(Path.GetInvalidFileNameChars()));
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string ext = format.StartsWith(".") ? format : "." + format;
        string fileName = $"skyline-report-{safeName}-{timestamp}{ext}";
        return Path.Combine(tmpDir, fileName);
    }
}
