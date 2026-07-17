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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SkylineTool;

namespace SkylineMcpServer.Tools;

[McpServerToolType]
public static class SkylineTools
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [McpServerTool(Name = "skyline_get_document_path"),
     Description("Get the file path of the currently open Skyline document.")]
    public static string GetDocumentPath()
    {
        return Invoke(connection =>
        {
            string path = connection.GetDocumentPath();
            return path ?? "(unsaved)";
        });
    }

    [McpServerTool(Name = "skyline_get_version"),
     Description("Get the version of the running Skyline instance.")]
    public static string GetVersion()
    {
        return Invoke(connection =>
        {
            string version = connection.GetVersion();
            return version ?? "Unknown version";
        });
    }

    [McpServerTool(Name = "skyline_get_selection"),
     Description("Get the currently selected element in Skyline (protein, peptide, precursor, " +
        "transition, etc.). Returns the ElementLocator of the selection, which can be passed " +
        "to skyline_set_selection. For multi-selection, returns one locator per line.")]
    public static string GetSelection()
    {
        return Invoke(connection =>
        {
            var selection = connection.GetSelection();
            return selection.Locators.Length == 0
                ? "Nothing is currently selected in Skyline."
                : string.Join("\n", selection.Locators);
        });
    }

    [McpServerTool(Name = "skyline_get_replicate"),
     Description("Get the name of the currently active replicate in Skyline.")]
    public static string GetReplicate()
    {
        return Invoke(connection =>
        {
            string replicate = connection.GetReplicateName();
            return string.IsNullOrEmpty(replicate)
                ? "No replicate is currently selected."
                : replicate;
        });
    }

    [McpServerTool(Name = "skyline_get_replicate_names"),
     Description("Get the names of all replicates in the Skyline document, one per line. " +
        "Use skyline_set_replicate to activate a specific replicate.")]
    public static string GetReplicateNames()
    {
        return Invoke(connection =>
        {
            string[] result = connection.GetReplicateNames();
            return result.Length == 0
                ? "No replicates in the document."
                : string.Join("\n", result);
        });
    }

    [McpServerTool(Name = "skyline_get_locations"),
     Description("Enumerate document tree elements at a specified level, optionally scoped " +
        "to a parent element. Returns tab-separated Name and ElementLocator per line. " +
        "Use the returned locators with skyline_set_selection to navigate, or as rootLocator " +
        "for deeper enumeration.")]
    public static string GetLocations(
        [Description("Tree level to enumerate: 'group' (proteins/molecule lists), " +
            "'molecule' (peptides/molecules), 'precursor', or 'transition'.")]
        string level,
        [Description("Optional ElementLocator to scope enumeration to a specific parent. " +
            "If omitted, enumerates from the document root. Get locators from previous " +
            "skyline_get_locations calls or from report columns like ProteinLocator.")]
        string rootLocator = null)
    {
        return Invoke(connection =>
        {
            var entries = connection.GetLocations(level, rootLocator);
            if (entries == null || entries.Length == 0)
                return "No elements found at the specified level.";

            var sb = new StringBuilder();
            sb.AppendLine("Name\tLocator");
            foreach (var entry in entries)
                sb.AppendLine($"{entry.Name}\t{entry.Locator}");
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_get_report"),
     Description("Run a named Skyline report and return results. For large reports, saves full data to a CSV file and returns a summary with preview rows and the file path. Use the Read tool to explore the full dataset.")]
    public static string GetReport(
        [Description("The name of a Skyline report to run (e.g., 'Peak Area', 'Transition Results')")] string reportName,
        [Description("Output file path. If not specified, saves to a temp directory. Extension determines format (.csv, .tsv, .parquet).")] string filePath = null,
        [Description("Output format when filePath is not specified: csv, tsv, or parquet (default: csv)")] string format = "csv",
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        return Invoke(connection =>
        {
            filePath ??= GetTempReportPath(reportName, format);
            string culture = invariant ? JsonToolConstants.CULTURE_INVARIANT : JsonToolConstants.CULTURE_LOCALIZED;
            var metadata = connection.ExportReport(reportName, filePath, culture);
            return FormatReportResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_get_report_from_definition"),
     Description("Run a custom Skyline report from a JSON report definition and return results. Use this when you need specific columns not available in predefined reports. The JSON format uses a 'select' array of column display names (PascalCase, invariant). Use skyline_get_report_doc_topics and skyline_get_report_doc_topic to discover available column names. Example: {\"select\": [\"ProteinName\", \"PeptideModifiedSequence\", \"PrecursorMz\", \"BestRetentionTime\", \"Area\"]}. The row source is automatically inferred from the selected columns. " +
        "Optional 'data_source' field targets a specific reporting data source: 'document_grid' (default, auto-detected), 'audit_log', 'group_comparisons', or 'candidate_peaks'. " +
        "Optional 'filter' array filters rows: [{\"column\": \"Area\", \"op\": \">\", \"value\": \"1000\"}, {\"column\": \"ProteinName\", \"op\": \"contains\", \"value\": \"INS\"}]. " +
        "Valid filter ops: 'equals', '<>', '>', '<', '>=', '<=', 'contains', 'notcontains', 'startswith', 'notstartswith', 'isnullorblank', 'isnotnullorblank'. " +
        "Filter columns can reference any column in the data model, not just selected ones. 'value' is required for all ops except 'isnullorblank'/'isnotnullorblank'. " +
        "Optional 'sort' array sorts results: [{\"column\": \"Area\", \"direction\": \"desc\"}]. Sort columns must be in the 'select' list. Direction: 'asc' (default) or 'desc'. " +
        "Optional 'pivot_replicate': true pivots replicates into columns (one column per replicate); false forces one row per replicate. Omit to use default inference. " +
        "Optional 'pivot_isotope_label': true pivots isotope label types into columns.")]
    public static string GetReportFromDefinition(
        [Description("JSON report definition with a 'select' array of column names. Example: {\"select\": [\"ProteinName\", \"PrecursorMz\", \"Area\"]}. Optional 'name' field for the report name. Optional 'filter' array to filter rows, 'sort' array to sort results, and 'data_source' to target a specific reporting data source.")] string reportDefinitionJson,
        [Description("Output file path. If not specified, saves to a temp directory. Extension determines format (.csv, .tsv, .parquet).")] string filePath = null,
        [Description("Output format when filePath is not specified: csv, tsv, or parquet (default: csv)")] string format = "csv",
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        return Invoke(connection =>
        {
            filePath ??= GetTempReportPath(JsonToolConstants.DEFAULT_REPORT_NAME, format);
            string culture = invariant ? JsonToolConstants.CULTURE_INVARIANT : JsonToolConstants.CULTURE_LOCALIZED;
            var definition = JsonSerializer.Deserialize<ReportDefinition>(reportDefinitionJson, SnakeCaseOptions);
            var metadata = connection.ExportReportFromDefinition(definition, filePath, culture);
            return FormatReportResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_get_report_rows"),
     Description("Run a named Skyline report and return a windowed slice of rows directly in the response, without writing to a file. Companion to skyline_get_report (file-based). Use this for small validation reads (e.g., 10 peptides x 5 columns) to close the validation loop on a document edit. " +
        "The response is capped server-side at roughly 25K tokens: long string cells are truncated with a '...' suffix, and if the row payload still exceeds the cap, trailing rows are dropped and 'truncated_at' is set to the next row index so the caller can resume with offset=truncated_at. " +
        "Per-request count is bounded (currently 10,000); larger result sets must be paginated. " +
        "IDIOM: pass count=0 to get the shape only (total_rows, columns with types, empty rows array). This is the canonical way to discover an unfamiliar report's columns, plan windowing, or verify shape after an edit. count must be explicit (no default) so 'fetch everything' is never silently truncated. " +
        "When include_max_length=true, the response includes max_observed_length on text columns (type 'string' or 'other' -- the latter includes entity wrappers like Peptide/Replicate that serialize to text) so you can decide a safe count for the next call. On large datasets this is a sampled estimate over the first 200 rows; max_length_sampled=true on a column tells you that column's value is a lower bound. " +
        "Optional filter follows the same syntax skyline_get_report_from_definition accepts. Optional columns projects to a subset of the report's columns; column names match either the localized display name in the report header or the invariant column id returned by skyline_get_report_doc_topic. " +
        "No snapshot isolation across paginated calls: if the document changes between calls, the window shifts; for read-immediately-after-write under a single agent this is fine.")]
    public static string GetReportRows(
        [Description("The name of a Skyline report to run (e.g., 'Peak Area', 'Transition Results').")] string reportName,
        [Description("Number of rows to return. REQUIRED. Pass 0 for shape-only introspection (returns total_rows and columns with no row data).")] int count,
        [Description("0-based row index of the first row to return. Defaults to 0.")] int offset = 0,
        [Description("Optional projection: subset of the report's columns by display name. When omitted, returns all of the report's columns.")] string[] columns = null,
        [Description("Optional additional filters as JSON, applied on top of the named report. Same syntax as the 'filter' field of skyline_get_report_from_definition: a JSON array of {column, op, value} objects, e.g. [{\"column\": \"PrecursorMz\", \"op\": \">\", \"value\": \"500\"}].")] string filterJson = null,
        [Description("When true, scans string columns and reports max_observed_length per column. Off by default to keep the hot path cheap.")] bool includeMaxLength = false,
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        return Invoke(connection =>
        {
            string culture = invariant ? JsonToolConstants.CULTURE_INVARIANT : JsonToolConstants.CULTURE_LOCALIZED;
            ReportFilter[] filter = null;
            if (!string.IsNullOrWhiteSpace(filterJson))
                filter = JsonSerializer.Deserialize<ReportFilter[]>(filterJson, SnakeCaseOptions);
            var result = connection.GetReportRows(reportName, offset, count, columns, filter, includeMaxLength, culture);
            return JsonSerializer.Serialize(result, SnakeCaseOptions);
        });
    }

    [McpServerTool(Name = "skyline_get_report_from_definition_rows"),
     Description("Run a custom Skyline report from a JSON report definition and return a windowed slice of rows directly in the response, without writing to a file. Companion to skyline_get_report_from_definition (file-based). Use this for small validation reads where a file round-trip is friction; the file form remains the right answer for batch / export / R / Python use. " +
        "The definition already supports projection, filtering, sorting, and pivots via its 'select', 'filter', 'sort', and 'pivot_*' fields, so this tool does NOT duplicate those parameters. " +
        "The response is capped server-side at roughly 25K tokens: long string cells are truncated with a '...' suffix, and if the row payload still exceeds the cap, trailing rows are dropped and 'truncated_at' is set so the caller can resume with offset=truncated_at. " +
        "Per-request count is bounded (currently 10,000); larger result sets must be paginated. " +
        "IDIOM: pass count=0 to get the shape only (total_rows, columns with types, empty rows array). This is the canonical way to verify shape and plan windowing before pulling data. count must be explicit (no default). " +
        "When include_max_length=true, the response includes max_observed_length on text columns (type 'string' or 'other'); on large datasets this is a sampled estimate over the first 200 rows with max_length_sampled=true on the sampled columns. " +
        "No snapshot isolation across paginated calls: if the document changes between calls, the window shifts.")]
    public static string GetReportFromDefinitionRows(
        [Description("JSON report definition with a 'select' array of column names. Example: {\"select\": [\"ProteinName\", \"PrecursorMz\", \"Area\"]}. Same shape as skyline_get_report_from_definition accepts; use its column-discovery tools (skyline_get_report_doc_topics / skyline_get_report_doc_topic) to find column names.")] string reportDefinitionJson,
        [Description("Number of rows to return. REQUIRED. Pass 0 for shape-only introspection (returns total_rows and columns with no row data).")] int count,
        [Description("0-based row index of the first row to return. Defaults to 0.")] int offset = 0,
        [Description("When true, scans string columns and reports max_observed_length per column. Off by default to keep the hot path cheap.")] bool includeMaxLength = false,
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        return Invoke(connection =>
        {
            string culture = invariant ? JsonToolConstants.CULTURE_INVARIANT : JsonToolConstants.CULTURE_LOCALIZED;
            var definition = JsonSerializer.Deserialize<ReportDefinition>(reportDefinitionJson, SnakeCaseOptions);
            var result = connection.GetReportFromDefinitionRows(definition, offset, count, includeMaxLength, culture);
            return JsonSerializer.Serialize(result, SnakeCaseOptions);
        });
    }

    [McpServerTool(Name = "skyline_add_report"),
     Description("Save a custom report definition to the user's Skyline session. Uses the same JSON format as skyline_get_report_from_definition but persists the report so it appears in Skyline's report list. The 'name' field is required. Use skyline_get_report_doc_topics and skyline_get_report_doc_topic to discover available column names. " +
        "Supports optional 'data_source', 'filter', 'pivot_replicate', and 'pivot_isotope_label' fields - see skyline_get_report_from_definition for format details. " +
        "Note: 'sort' is ignored when saving reports, as Skyline report definitions do not include a default sort order.")]
    public static string AddReport(
        [Description("JSON report definition with a required 'name' field and a 'select' array of column names. Example: {\"name\": \"My Report\", \"select\": [\"ProteinName\", \"PrecursorMz\", \"Area\"]}. Optional 'data_source', 'filter', 'pivot_replicate', and 'pivot_isotope_label' fields.")] string reportDefinitionJson)
    {
        return Invoke(connection =>
        {
            var definition = JsonSerializer.Deserialize<ReportDefinition>(reportDefinitionJson, SnakeCaseOptions);
            connection.AddReportFromDefinition(definition);
            return $"Report '{definition.Name}' added.";
        });
    }

    [McpServerTool(Name = "skyline_get_settings_list_types"),
     Description("Enumerate all settings list types available in Skyline (enzymes, modifications, reports, etc.). Returns tab-separated lines of PropertyName and Title. Use this to discover what configuration lists exist before querying their contents.")]
    public static string GetSettingsListTypes()
    {
        return Invoke(connection =>
        {
            string[] result = connection.GetSettingsListTypes();
            return result.Length == 0
                ? "No settings lists found."
                : string.Join("\n", result);
        });
    }

    [McpServerTool(Name = "skyline_get_settings_list_names"),
     Description("Get item names from a specific settings list. Use skyline_get_settings_list_types first to discover available list types. For PersistedViews (reports), names are grouped by Main and External Tools sections.")]
    public static string GetSettingsListNames(
        [Description("The settings list property name (e.g., 'EnzymeList', 'PersistedViews')")] string listType)
    {
        return Invoke(connection =>
        {
            string[] result = connection.GetSettingsListNames(listType);
            return result.Length == 0
                ? $"No items found in {listType}."
                : string.Join("\n", result);
        });
    }

    [McpServerTool(Name = "skyline_get_settings_list_item"),
     Description("Get the XML definition of a single item from a settings list. Useful for inspecting report definitions, enzyme cut rules, modification details, etc.")]
    public static string GetSettingsListItem(
        [Description("The settings list property name (e.g., 'EnzymeList', 'PersistedViews')")] string listType,
        [Description("The name of the item to retrieve (e.g., 'Trypsin', 'Peak Area')")] string itemName)
    {
        return Invoke(connection =>
        {
            string result = connection.GetSettingsListItem(listType, itemName);
            return string.IsNullOrEmpty(result)
                ? $"Item not found: {itemName} in {listType}."
                : result;
        });
    }

    [McpServerTool(Name = "skyline_add_settings_list_item"),
     Description("Add a new settings item to a Skyline settings list. " +
        "Use skyline_get_settings_list_item to see the XML format for existing items, " +
        "then modify the XML for a new item. Rejects duplicates unless overwrite is true.")]
    public static string AddSettingsListItem(
        [Description("The settings list type (e.g., 'Enzymes', 'Structural Modifications')")] string listType,
        [Description("XML definition of the settings item")] string itemXml,
        [Description("Set to true to replace an existing item with the same name")] bool overwrite = false)
    {
        return Invoke(connection =>
        {
            connection.AddSettingsListItem(listType, itemXml, overwrite);
            return overwrite ? $"Replaced in {listType}." : $"Added to {listType}.";
        });
    }

    [McpServerTool(Name = "skyline_get_settings_list_selected_items"),
     Description("Get the names of items from a settings list that are currently active " +
        "in the document. For example, which enzyme is selected, which modifications are " +
        "enabled, which annotations are included. Returns one name per line. " +
        "Use skyline_get_settings_list_names to see all available items.")]
    public static string GetSettingsListSelectedItems(
        [Description("The settings list type (e.g., 'Enzymes', 'Structural Modifications')")] string listType)
    {
        return Invoke(connection =>
        {
            string[] result = connection.GetSettingsListSelectedItems(listType);
            return result.Length == 0
                ? $"No items are currently selected in {listType}."
                : string.Join("\n", result);
        });
    }

    [McpServerTool(Name = "skyline_select_settings_list_items"),
     Description("Set which items from a settings list are active in the document. " +
        "This replaces the current selection — the provided list becomes the full active set. " +
        "For single-select lists (e.g., Enzymes), provide exactly one item. " +
        "For multi-select lists (e.g., Structural Modifications), provide all desired items. " +
        "Items must exist in the settings list (use skyline_add_settings_list_item to add new ones).")]
    public static string SelectSettingsListItems(
        [Description("The settings list type (e.g., 'Enzymes', 'Structural Modifications')")] string listType,
        [Description("Array of item names to activate")] string[] itemNames)
    {
        return Invoke(connection =>
        {
            connection.SelectSettingsListItems(listType, itemNames);
            return $"Selected {itemNames.Length} item(s) in {listType}.";
        });
    }

    [McpServerTool(Name = "skyline_run_command"),
     Description("Run any SkylineCmd command line against the running Skyline instance. This exposes the entire SkylineCmd CLI surface (over 100 flags) inside the live Skyline session so it acts on the open document. Many other MCP tools (skyline_save_document, skyline_import_fasta, skyline_insert_small_molecule_transition_list, skyline_new_document) are thin wrappers around specific flags here; use this tool directly when you need a flag a wrapper does not expose, or for combined operations (e.g., '--in=file --refine-... --out=newfile' in one call). Output is echoed to Skyline's Immediate Window and returned in the response, including error messages, so format problems come back as text rather than as modal dialogs the user has to copy. To discover what is available, call skyline_get_cli_help_sections for the list of section names, then skyline_get_cli_help with a section (e.g., 'import', 'refine', 'export', 'report') for the flags in that section. Examples: '--report-name=\"Peak Area\" --report-file=output.csv', '--in=path/to/doc.sky --discard-changes', '--refine-cv-remove-above-cutoff=20', '--out=path/to/save.sky'.")]
    public static string RunCommand(
        [Description("Command line arguments in SkylineCmd format (e.g., '--report-name=\"Peak Area\" --report-file=output.csv'). Use skyline_get_cli_help_sections / skyline_get_cli_help to discover available flags.")] string commandArgs)
    {
        return Invoke(connection =>
        {
            string[] args = SplitCommandArgs(commandArgs);
            string output = connection.RunCommand(args);
            return string.IsNullOrEmpty(output)
                ? "Command completed with no output."
                : output;
        });
    }

    [McpServerTool(Name = "skyline_get_cli_help_sections"),
     Description("List available CLI help sections. Returns section names (one per line) that can be passed to skyline_get_cli_help for detailed help on each topic.")]
    public static string GetCliHelpSections()
    {
        return Invoke(connection =>
        {
            string output = connection.RunCommandSilent(new[] { "--help=sections" });
            return string.IsNullOrEmpty(output)
                ? "No help sections available."
                : output;
        });
    }

    [McpServerTool(Name = "skyline_get_cli_help"),
     Description("Get detailed CLI help for a specific section. Use skyline_get_cli_help_sections to discover available sections. Section matching is case-insensitive and supports partial matches.")]
    public static string GetCliHelp(
        [Description("The help section name (e.g., 'import', 'export', 'refine'). Case-insensitive partial match.")] string section)
    {
        return Invoke(connection =>
        {
            string output = connection.RunCommandSilent(new[] { $"--help={section}", "--help=no-borders" });
            return string.IsNullOrEmpty(output)
                ? $"No help found for section: {section}"
                : output;
        });
    }

    [McpServerTool(Name = "skyline_get_report_doc_topics"),
     Description("List available report column documentation topics. Returns tab-separated lines " +
        "of DisplayName and ColumnCount for each entity type (e.g., Molecule, Precursor, Transition). " +
        "Use skyline_get_report_doc_topic to get column details for a specific topic. " +
        "Supports multiple data sources: 'document_grid' (default), 'audit_log', 'group_comparisons', " +
        "'candidate_peaks'.")]
    public static string GetReportDocTopics(
        [Description("Reporting data source: 'document_grid' (default), 'audit_log', 'group_comparisons', " +
            "or 'candidate_peaks'. Each data source has its own column namespace.")] string dataSource = null)
    {
        return Invoke(connection =>
        {
            var topics = connection.GetReportDocTopics(dataSource);
            if (topics == null || topics.Length == 0)
                return "No report documentation topics found.";

            var sb = new StringBuilder();
            sb.AppendLine("Name\tColumnCount");
            foreach (var topic in topics)
                sb.AppendLine($"{topic.Name}\t{topic.ColumnCount}");
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_get_report_doc_topic"),
     Description("Get column documentation for a specific report entity type. Returns a table of " +
        "column names, descriptions, and types. Use skyline_get_report_doc_topics to discover " +
        "available topics. Supports data sources: 'document_grid' (default), 'audit_log', " +
        "'group_comparisons', 'candidate_peaks'.")]
    public static string GetReportDocTopic(
        [Description("The topic name (display name like 'Molecule' or qualified type name). " +
            "Case-insensitive partial match on display name.")] string topic,
        [Description("Reporting data source: 'document_grid' (default), 'audit_log', 'group_comparisons', " +
            "or 'candidate_peaks'.")] string dataSource = null)
    {
        return Invoke(connection =>
        {
            var detail = connection.GetReportDocTopic(topic, dataSource);
            if (detail == null)
                return $"No documentation found for topic: {topic}";

            var sb = new StringBuilder();
            sb.AppendLine(detail.Name);
            sb.AppendLine();
            sb.AppendLine("Name\tDescription\tType");
            foreach (var col in detail.Columns)
                sb.AppendLine($"{col.Name}\t{col.Description}\t{col.Type}");
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_insert_small_molecule_transition_list"),
     Description("Insert a small molecule transition list into the Skyline document from a CSV file. The first row contains column headers; Skyline determines column meaning from headers. Common headers: MoleculeGroup, PrecursorName, ProductName, PrecursorFormula, ProductFormula, PrecursorAdduct, ProductAdduct, PrecursorMz, ProductMz, PrecursorCharge, ProductCharge, PrecursorRT, LabelType, CAS, InChiKey, HMDB, SMILES, Note. Same format as Edit > Insert > Transition List in the Skyline UI. Internally invokes 'SkylineCmd --import-transition-list=path' so any header or row parse errors are returned from the Skyline Immediate Window as part of this tool's response.")]
    public static string InsertSmallMoleculeTransitionList(
        [Description("Path to a CSV file with column headers in the first row and data rows. Common headers: MoleculeGroup, PrecursorName, PrecursorFormula, PrecursorAdduct, PrecursorMz, PrecursorCharge, ProductFormula, ProductAdduct, ProductMz, ProductCharge, PrecursorRT, LabelType, CAS, InChiKey, HMDB, SMILES, Note.")] string csvPath)
    {
        return Invoke(connection => connection.RunCommand(new[] { "--import-transition-list=" + csvPath }));
    }

    [McpServerTool(Name = "skyline_import_fasta"),
     Description("Import protein sequences in FASTA format into the Skyline document from a file. Skyline will digest proteins using current enzyme settings and add peptides and transitions based on current transition settings. Each protein in the file starts with a '>' header line followed by sequence lines. Internally invokes 'SkylineCmd --import-fasta=path' so any FASTA parse errors are returned from the Skyline Immediate Window as part of this tool's response.")]
    public static string ImportFasta(
        [Description("Path to a FASTA file. Each protein in the file starts with a '>' header line (e.g., '>sp|P01308|INS_HUMAN Insulin') followed by one or more sequence lines.")] string fastaPath)
    {
        return Invoke(connection => connection.RunCommand(new[] { "--import-fasta=" + fastaPath }));
    }

    [McpServerTool(Name = "skyline_import_properties"),
     Description("Import properties (annotations) into the Skyline document. The input is CSV text where the first column contains ElementLocator paths identifying targets or replicates, and remaining columns are annotation names with values. Export a report containing locators first to understand the document structure. The locator format uses paths like /MoleculeGroup[name='Lipids']/Molecule[name='CE 18:1'].")]
    public static string ImportProperties(
        [Description("CSV text where the first column is ElementLocator (paths identifying document elements) and remaining columns are annotation names with values.")] string csvText)
    {
        return Invoke(connection =>
        {
            connection.ImportProperties(csvText);
            return "Properties imported.";
        });
    }

    [McpServerTool(Name = "skyline_set_selection"),
     Description("Navigate to a specific element in the Skyline document tree by its " +
        "ElementLocator string. Get locators from report columns like PeptideLocator, " +
        "PrecursorLocator, ProteinLocator, or TransitionLocator.")]
    public static string SetSelection(
        [Description("ElementLocator string (e.g. from a PeptideLocator report column)")] string elementLocator,
        [Description("Optional newline-separated list of additional ElementLocator strings " +
            "to add to the selection. The first locator is always the primary (focused) " +
            "selection; these are secondary selections.")] string additionalLocators = null)
    {
        return Invoke(connection =>
        {
            connection.SetSelectedElement(elementLocator, additionalLocators);
            return "Selection set.";
        });
    }

    [McpServerTool(Name = "skyline_set_replicate"),
     Description("Set the active replicate in Skyline by name. Use skyline_get_replicate " +
        "to see the current replicate, or run a report with ReplicateName to list all.")]
    public static string SetReplicate(
        [Description("Name of the replicate to activate")] string replicateName)
    {
        return Invoke(connection =>
        {
            connection.SetReplicate(replicateName);
            return $"Replicate set to: {replicateName}";
        });
    }

    [McpServerTool(Name = "skyline_get_document_status"),
     Description("Get a lightweight overview of the current Skyline document including document type (proteomic, small_molecules, mixed), target counts (proteins/lists, peptides/molecules, precursors, transitions), replicate count, and file path. Much faster than running a report for basic document info.")]
    public static string GetDocumentStatus()
    {
        return Invoke(connection =>
        {
            var status = connection.GetDocumentStatus();
            if (status == null)
                return "No document information available.";

            var sb = new StringBuilder();
            sb.AppendLine($"Document Path: {status.DocumentPath ?? "(unsaved)"}");
            sb.AppendLine($"Document Type: {status.DocumentType}");
            sb.AppendLine($"{status.GroupsLabel}: {status.Groups}");
            sb.AppendLine($"{status.MoleculesLabel}: {status.Molecules}");
            sb.AppendLine($"Precursors: {status.Precursors}");
            sb.AppendLine($"Transitions: {status.Transitions}");
            sb.AppendLine($"Replicates: {status.Replicates}");
            sb.AppendLine($"Has Unsaved Changes: {status.HasUnsavedChanges}");
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_get_open_forms"),
     Description("Enumerate all open forms in the Skyline window. Returns tab-separated lines with form type, " +
        "title, whether it contains a ZedGraph graph, dock state, a stable identifier in TypeName:Title " +
        "format (e.g., 'GraphSummary:Peak Areas - Replicate Comparison'), whether the form is a native OS window, " +
        "its SubType, and Message. (New columns are appended, so a positional reader keyed on the earlier ones " +
        "keeps working.) " +
        "Use the identifier with skyline_get_graph_data, skyline_get_graph_image, and skyline_get_form_image. " +
        "DockState values: Floating, Document, DockTop/Left/Bottom/Right, DockTopAutoHide/etc., Dialog. " +
        "IsNative=True marks a native OS dialog, whose Type is always 'Dialog'; its SubType says which kind -- " +
        "'OpenFileDialog', 'SaveFileDialog', 'FolderBrowserDialog' or 'MessageBox'. That matters because a file " +
        "dialog's error box carries the file dialog's own caption, so both share one Id: SubType tells them apart, " +
        "and a verb addressing that Id acts on the TOPMOST (the box -- the one in the way). " +
        "Message is what a window SAYS (a message box's body, an alert's text), truncated -- read it to see whether " +
        "a form is blocking you and why, without capturing an image. skyline_get_form_image works for these too.")]
    public static string GetOpenForms()
    {
        return Invoke(connection =>
        {
            var forms = connection.GetOpenForms();
            if (forms == null || forms.Length == 0)
                return "No forms are currently open in Skyline.";

            var sb = new StringBuilder();
            // The first five columns are the ORIGINAL set, in their original order, so a reader that matches by
            // position still finds them. Every column added since -- IsNative, SubType, Message -- is appended.
            sb.AppendLine("Type\tTitle\tHasGraph\tDockState\tId\tIsNative\tSubType\tMessage");
            foreach (var form in forms)
                sb.AppendLine($"{form.Type}\t{form.Title}\t{form.HasGraph}\t{form.DockState}\t{form.Id}\t" +
                              $"{form.IsNative}\t{form.SubType}\t{form.DetailedMessage}");
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_get_controls"),
     Description("List the interactive controls on a form so you can discover what is there -- and how " +
        "to address it -- without reading source code. Returns tab-separated lines with each control's " +
        "Type, the visible Label that names it (its own caption, or the label beside a caption-less " +
        "field), Enabled, and internal Name (informational). Hidden controls (e.g. on an unselected tab) " +
        "are not listed -- select the tab first. Use the 'get_value' action for a " +
        "control's current value and 'get_actions' for the actions it supports. Address a control by its " +
        "Label (e.g. set_form_value with \"Ion match tolerance\"); a control with no Label is addressed by " +
        "its Type (e.g. \"TreeView\"). Get the formId from skyline_get_open_forms.")]
    public static string GetControls(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId)
    {
        return Invoke(connection =>
        {
            var controls = connection.GetControls(formId);
            if (controls == null || controls.Length == 0)
                return $"No interactive controls found on {formId}.";

            var sb = new StringBuilder();
            sb.AppendLine("Type\tLabel\tEnabled\tName");
            foreach (var c in controls)
                sb.AppendLine($"{c.Path?.Type}\t{c.Path?.Text}\t{c.Enabled}\t{c.Name}");
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_perform_action"),
     Description("The most general way to interact with a control, menu item, or list item: locate it by " +
        "its Label (the visible text that names it) and/or its Type (for a caption-less control, e.g. " +
        "\"TreeView\") among the form's controls, then perform an action. Only the properties you set are " +
        "used to match. Actions: 'get_actions' (lists the actions this control supports, each with a " +
        "description and the value it takes -- call this first when unsure); 'get_children' " +
        "(lists child elements as JSON UiElementPaths -- each already parented onto the element you listed, " +
        "so pass one straight back as 'path'); 'click'; 'set_value' (uses 'value'); 'get_value' " +
        "(returns the current value); 'check_item'/'uncheck_item'/'select_item'/'unselect_item' (a " +
        "list/tree/list-view item by its text, value the item -- a TreeView node by a '>'-separated path); " +
        "'set_selected_index' (a list, value the index); 'get_grid_text'/'set_grid_text' (a grid's text); " +
        "'set_current_cell_address' (value a [column, row] array, e.g. [0, 1]); 'select_tab' (a TabControl, value the tab text); " +
        "'expand'/'collapse' (a TreeView node, value a JSON array path whose segments are a child's text or " +
        "its index, e.g. [\"Peptides\", 0]); 'paste' (value the text to paste into a text box, a grid, the " +
        "Targets tree, or the main Skyline window -- without using the clipboard); 'select_all' (selects all " +
        "of a paste-capable element's content, e.g. before paste to replace it); 'rename_node' (the Targets " +
        "tree, value the new name for the selected node). " +
        "For a control's right-click menu, pass path as the JSON {\"parent\": <the control's " +
        "UiElementPath>, \"type\": \"ContextMenu\"}, then get_children to list its items or " +
        "click to invoke one (for a grid, move to the cell first with skyline_set_current_cell_address). When " +
        "path is given it is used as-is and label/type are ignored. Discover controls with " +
        "skyline_get_controls; the typed tools (skyline_click_form_button, ...) remain for common cases.")]
    public static string PerformAction(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string form,
        [Description("Action: get_actions, get_children, click, set_value, get_value, check_item, uncheck_item, select_item, unselect_item, set_selected_index, get_grid_text, set_grid_text, set_current_cell_address, select_tab, expand, collapse, paste, select_all, rename_node")] string action,
        [Description("Visible label that names the control (optional)")] string label = null,
        [Description("Control type for a caption-less control, e.g. TreeView/ListView (optional)")] string type = null,
        [Description("Value for set_value/set_grid_text, the text for paste/rename_node, a [column, row] array for set_current_cell_address, the tab text for select_tab, or a JSON array path for expand/collapse (optional)")] string value = null,
        [Description("A full UiElementPath as JSON (e.g. one straight from get_children, or wrapped as a ContextMenu); overrides label/type when given (optional)")] string path = null)
    {
        return Invoke(connection =>
        {
            var target = string.IsNullOrEmpty(path)
                ? new UiElementPath(new UiElementPath(null, form, null, "Form"), label, null, type)
                : JsonSerializer.Deserialize<UiElementPath>(path,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            // The result is the action's return (raw JSON for arrays, a string for get_value, or empty).
            var text = connection.PerformAction(target, action, value)?.ToString();
            return string.IsNullOrEmpty(text) ? $"Performed '{action}'." : text;
        });
    }

    [McpServerTool(Name = "skyline_click_main_menu_item"),
     Description("Click an item on the MAIN Skyline window's menu bar by its visible path, e.g. " +
        "'File > Import > Peptide Search'. Segments are separated by '>' and matched against each " +
        "menu item's visible text (the mnemonic '&' and a trailing ellipsis are ignored) or its " +
        "control name, case-insensitively. The click is posted asynchronously, so a menu item that " +
        "opens a dialog returns immediately; call skyline_get_open_forms to find the resulting form. " +
        "For a menu on any OTHER window -- a form's toolbar, or a control's right-click menu -- use " +
        "skyline_click_control_menu_item.")]
    public static string ClickMainMenuItem(
        [Description("Menu path with '>'-separated segments, e.g. 'File > Import > Peptide Search'")] string menuPath)
    {
        return Invoke(connection =>
        {
            var result = connection.ClickMainMenuItem(menuPath);
            return DescribeAction(result, $"Clicked menu item: {menuPath}");
        });
    }

    [McpServerTool(Name = "skyline_click_control_menu_item"),
     Description("Click an item on a menu belonging to a form, or to a control on it, by its path, e.g. " +
        "'Reports > Replicates' (the 'Reports' toolbar button, then 'Replicates' in its dropdown). Which menu " +
        "is meant follows from 'control': leave it EMPTY for the form's own menu (its menu bar, else its first " +
        "toolbar, else its right-click menu); name a TOOLBAR to click an item on that toolbar; name any OTHER " +
        "control (a grid, a tree, a graph) to click an item on that control's RIGHT-CLICK menu. Each level's " +
        "dropdown is opened first so items built on demand -- which skyline_click_form_button cannot reach -- are " +
        "present before matching. Segments are '>'-separated and matched by item name or visible text. Use for " +
        "the Document Grid 'Reports' dropdown, a graph's right-click menu, etc.")]
    public static string ClickControlMenuItem(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("Control that owns the menu, from skyline_get_controls. Empty for the form's own menu.")] string control,
        [Description("Menu path with '>'-separated segments, e.g. 'Reports > Replicates'")] string menuPath)
    {
        return Invoke(connection =>
        {
            var result = connection.ClickControlMenuItem(formId, control, menuPath);
            return DescribeAction(result, $"Clicked menu item '{menuPath}' on {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_click_form_button"),
     Description("Click a control on an open form, matching it by control name or visible text: a " +
        "button, a checkbox or radio button, a toolbar/menu item, an item in a checked-list box (its " +
        "check is toggled), or any other control. To dismiss a dialog instead, use " +
        "skyline_dismiss_with_accept_button / skyline_dismiss_with_cancel_button / skyline_dismiss_with_button, " +
        "which wait for it to close. The click is posted asynchronously, so a button that opens another " +
        "dialog returns immediately; call skyline_get_open_forms to find the resulting form.")]
    public static string ClickFormButton(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("Control name or visible label, e.g. 'Add Files', 'OK', or a checkbox label")] string button)
    {
        return Invoke(connection =>
        {
            var result = connection.ClickFormButton(formId, button);
            return DescribeAction(result, $"Clicked '{button}' on {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_dismiss_with_accept_button"),
     Description("Accept (confirm) an open dialog by pressing its default button -- the equivalent of pressing " +
        "Enter, without matching a localized 'OK' caption -- then wait until the dialog has closed. Use this to " +
        "commit a native file dialog (Type 'Dialog', IsNative=True -- it has no caption-addressable button) or to " +
        "click a WinForms dialog's default button. If accepting opens another dialog it reports not-completed and " +
        "names it (drive that one next).")]
    public static string DismissWithAcceptButton(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId)
    {
        return Invoke(connection =>
        {
            var result = connection.DismissWithAcceptButton(formId);
            return DescribeAction(result, $"Accepted {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_dismiss_with_cancel_button"),
     Description("Cancel (dismiss) an open dialog by pressing its cancel button, or closing it when it has none, " +
        "then wait until it has closed. A message box with only affirmative choices (e.g. Yes/No) has no cancel " +
        "affordance -- dismiss such a box with skyline_dismiss_with_button instead.")]
    public static string DismissWithCancelButton(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId)
    {
        return Invoke(connection =>
        {
            var result = connection.DismissWithCancelButton(formId);
            return DescribeAction(result, $"Cancelled {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_dismiss_with_button"),
     Description("Dismiss an open dialog by clicking the button with the given caption, then wait until it has " +
        "closed -- e.g. 'No' on a 'replace it?' message box, when neither the default (accept) nor the cancel " +
        "button is wanted. A native file dialog has no caption-addressable button, so commit one with " +
        "skyline_dismiss_with_accept_button.")]
    public static string DismissWithButton(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("The visible caption of the button to click, e.g. 'No' or 'Yes'")] string button)
    {
        return Invoke(connection =>
        {
            var result = connection.DismissWithButton(formId, button);
            return DescribeAction(result, $"Clicked '{button}' on {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_set_form_value"),
     Description("Set the value of a control on an open form. For a native file dialog " +
        "(Type 'FileDialog') the value is the file name(s) to open and controlId is ignored; select " +
        "several files by quoting each path and separating with spaces, e.g. \"C:\\a.raw\" \"C:\\b.raw\". " +
        "For a WinForms form it sets the text, the checked state ('true'/'false'), or the selected " +
        "item of the control named by controlId; a matched label sets the field it labels. controlId " +
        "may also be a grid cell locator 'grid[column,row]' (grid name optional) to set that cell.")]
    public static string SetFormValue(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("Control name, a grid cell locator 'grid[column,row]', or ignored for a native file dialog")] string controlId,
        [Description("Value to set: text, 'true'/'false' for a checkbox, item text for a combo box, " +
            "or space-separated quoted file paths for a native file dialog")] string value)
    {
        return Invoke(connection =>
        {
            var result = connection.SetFormValue(formId, controlId, value);
            return DescribeAction(result, $"Set value on {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_get_form_value"),
     Description("Get the current value of a control on an open form, found by its visible label: a text " +
        "box's text, a combo box's selected item, a check/radio's checked state ('True'/'False'), or a " +
        "CheckedListBox's checked items (their text, one per line). Pass null for controlId when the form " +
        "has a single valued control.")]
    public static string GetFormValue(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("The control's visible label, or null when the form has a single valued control")] string controlId)
    {
        return Invoke(connection => connection.GetFormValue(formId, controlId) ?? string.Empty);
    }

    [McpServerTool(Name = "skyline_set_grid_text"),
     Description("Paste tab-separated values into a grid on a form, starting at its current cell, the " +
        "way typing/pasting there would. Move to the target cell first with skyline_set_current_cell_address. " +
        "Use for the Document Grid and other data grids -- e.g. to fill annotation columns or a rules " +
        "grid. The text may be a multi-cell block: separate cell values with tabs and rows with " +
        "newlines (it fills down and to the right). Works for DataboundGridControl grids and plain " +
        "DataGridView grids.")]
    public static string SetGridText(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("Grid control name on the form, or null when the form has a single grid")] string controlId,
        [Description("Tab-separated (and newline-separated) values to paste at the current cell")] string text)
    {
        return Invoke(connection =>
        {
            var result = connection.SetGridText(formId, controlId, text);
            return DescribeAction(result, $"Pasted grid text on {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_set_current_cell_address"),
     Description("Move the current cell of a grid on a form, so the next skyline_set_grid_text pastes " +
        "there, or a context menu (a path with Type 'ContextMenu' on the grid) opens for that " +
        "cell. column and row are zero-based indices into the grid's visible columns and its rows -- " +
        "the same indices skyline_get_grid_text reports.")]
    public static string SetCurrentCellAddress(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("Grid control name on the form, or null when the form has a single grid")] string controlId,
        [Description("Zero-based column index (into the grid's visible columns)")] int column,
        [Description("Zero-based row index")] int row)
    {
        return Invoke(connection =>
        {
            var result = connection.SetCurrentCellAddress(formId, controlId, column, row);
            return DescribeAction(result, $"Moved to cell (column {column}, row {row}) on {formId}.");
        });
    }

    [McpServerTool(Name = "skyline_get_grid_text"),
     Description("Get all the data in a grid on a form as tab-separated text: the column headers " +
        "followed by every row, columns separated by tabs and rows by newlines. Use for the Document " +
        "Grid and other data grids. Works for DataboundGridControl grids and plain DataGridView grids.")]
    public static string GetGridText(
        [Description("Form identifier from skyline_get_open_forms (TypeName:Title)")] string formId,
        [Description("Grid control name on the form, or null when the form has a single grid")] string gridId)
    {
        return Invoke(connection => connection.GetGridText(formId, gridId));
    }


    [McpServerTool(Name = "skyline_get_graph_data"),
     Description("Extract tab-separated data from a Skyline graph. Returns the same data as " +
        "Skyline's Copy Data clipboard format, including pane titles, axis labels, and all " +
        "curve data points. Use skyline_get_open_forms to discover graph IDs.")]
    public static string GetGraphData(
        [Description("Graph identifier from skyline_get_open_forms (e.g., 'GraphSummary:Peak Areas - Replicate Comparison')")] string graphId,
        [Description("Output file path. If not specified, saves to a temp directory. " +
            "Extension determines format (.tsv default).")] string filePath = null)
    {
        return Invoke(connection =>
        {
            string result = connection.GetGraphData(graphId, filePath);
            return string.IsNullOrEmpty(result)
                ? "No data in graph."
                : $"Graph data saved to: {result}\n\nUse the Read tool to examine the data.";
        });
    }

    [McpServerTool(Name = "skyline_get_graph_image"),
     Description("Export a PNG image of a Skyline graph. By default returns the PNG inline as an " +
        "MCP ImageContentBlock so the model sees the image without a separate Read step. Large " +
        "images fall back to disk; set returnFormat='file' to force the file-path behavior, or " +
        "'inline' to require the inline form (errors if the image exceeds the inline cap or the " +
        "connected Skyline does not support inline images). Use skyline_get_open_forms to discover graph IDs.")]
    public static CallToolResult GetGraphImage(
        [Description("Graph identifier from skyline_get_open_forms (e.g., 'GraphSummary:Peak Areas - Replicate Comparison')")] string graphId,
        [Description("Return shape: 'auto' (default, inline with file fallback), 'inline' (always inline, error if too big or Skyline too old), or 'file' (write to disk and return the path).")] string returnFormat = RETURN_AUTO,
        [Description("Output file path. Honored only on the file path (returnFormat='file' or auto-fell-back-to-file). Ignored otherwise.")] string filePath = null)
    {
        return InvokeContent(connection => InvokeImage(connection, returnFormat, filePath,
            bytesCall: c => c.GetGraphImageBytes(graphId),
            fileCall: (c, fp) => SavedToPath("Graph image", c.GetGraphImage(graphId, fp))));
    }

    // Note: the handshake wording below describes the SHAPE of the response,
    // not the exact runtime strings. The runtime messages live in
    // JsonUiService.LLM_MSG_SCREEN_CAPTURE_* and may be localized in the
    // future; the description here intentionally stays abstract so the two
    // do not drift out of sync.
    [McpServerTool(Name = "skyline_get_form_image"),
     Description("Export a PNG screenshot of any open Skyline form, dialog, or dockable panel. " +
        "By default returns the PNG inline as an MCP ImageContentBlock. The screenshot is captured " +
        "from the screen with non-Skyline content automatically redacted. Set returnFormat='file' to " +
        "force file-on-disk, or 'inline' to require inline (errors if too big or Skyline too old). " +
        "Use skyline_get_open_forms to discover form IDs. For graphs, prefer skyline_get_graph_image " +
        "which renders directly without screen capture. " +
        "Two-phase permission handshake: on the first call of a session, when the user has not yet " +
        "authorized screen capture, this tool opens a confirmation dialog inside Skyline and returns a " +
        "permission-required message. That is the documented handshake, not an error. Tell the user a " +
        "dialog opened in Skyline, ask them to grant or deny it, then call this tool again. After the " +
        "user grants, subsequent calls capture normally; if they deny, subsequent calls return a " +
        "denied message without re-prompting.")]
    public static CallToolResult GetFormImage(
        [Description("Form identifier from skyline_get_open_forms (e.g., 'SequenceTreeForm:Targets', 'PeptideSettingsUI:Peptide Settings')")] string formId,
        [Description("Return shape: 'auto' (default, inline with file fallback), 'inline' (always inline, error if too big or Skyline too old), or 'file' (write to disk and return the path).")] string returnFormat = RETURN_AUTO,
        [Description("Output file path. Honored only on the file path. Ignored otherwise.")] string filePath = null)
    {
        return InvokeContent(connection => InvokeImage(connection, returnFormat, filePath,
            bytesCall: c => c.GetFormImageBytes(formId),
            fileCall: (c, fp) =>
            {
                string result = c.GetFormImage(formId, fp);
                // Older Skyline returns screen-capture denial / desktop-unavailable
                // messages here as plain strings (with a leading "Screen capture"
                // prefix) instead of a file path. Pass those through verbatim so
                // the response shape matches the legacy file-based behavior.
                if (IsScreenCaptureDenial(result))
                    return TextContent(result, ignoredFilePath: null);
                return SavedToPath("Form image", result);
            }));
    }

    [McpServerTool(Name = "skyline_get_document_settings"),
     Description("Export the current document's settings (enzyme, transitions, filters, modifications, full-scan, etc.) as XML to a file. Strips replicate/results data for size. Returns the file path. Compare against skyline_get_default_settings to find what differs from defaults.")]
    public static string GetDocumentSettings(
        [Description("Output file path. If not specified, saves to a temp directory.")] string filePath = null)
    {
        return Invoke(connection =>
        {
            filePath ??= GetTempSettingsPath("document");
            string result = connection.GetDocumentSettings(filePath);
            return $"Document settings saved to: {result}\nUse the Read tool to examine the settings XML.";
        });
    }

    [McpServerTool(Name = "skyline_get_default_settings"),
     Description("Export Skyline's default settings as XML to a file. This is the baseline for new documents — compare against skyline_get_document_settings to find what the user has changed.")]
    public static string GetDefaultSettings(
        [Description("Output file path. If not specified, saves to a temp directory.")] string filePath = null)
    {
        return Invoke(connection =>
        {
            filePath ??= GetTempSettingsPath("defaults");
            string result = connection.GetDefaultSettings(filePath);
            return $"Default settings saved to: {result}\nUse the Read tool to examine the settings XML.";
        });
    }

    [McpServerTool(Name = "skyline_get_available_tutorials"),
     Description("List all available Skyline tutorials with their categories, descriptions, " +
        "wiki page URLs, and data download URLs. Returns tab-separated lines: " +
        "Category, Name, Title, Description, WikiUrl, ZipUrl. Use skyline_get_tutorial " +
        "to fetch the full content of a specific tutorial.")]
    public static string GetAvailableTutorials()
    {
        return Invoke(connection =>
        {
            var tutorials = connection.GetAvailableTutorials();
            if (tutorials == null || tutorials.Length == 0)
                return "No tutorials available.";

            var sb = new StringBuilder();
            sb.AppendLine("Category\tName\tTitle\tDescription\tWikiUrl\tZipUrl");
            foreach (var t in tutorials)
                sb.AppendLine($"{t.Category}\t{t.Name}\t{t.Title}\t{t.Description}\t{t.WikiUrl}\t{t.ZipUrl}");
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_get_tutorial"),
     Description("Fetch the content of a Skyline tutorial as markdown text. The tutorial " +
        "is fetched from GitHub (pinned to the running Skyline version), converted to " +
        "markdown, and saved to a local file. Returns a JSON object with file_path, " +
        "title, and toc (table of contents with headings and line numbers). Use the " +
        "Read tool with offset/limit to read specific sections from the file. " +
        "Use skyline_get_available_tutorials to discover tutorial names.")]
    public static string GetTutorial(
        [Description("Tutorial name (e.g., 'MethodEdit', 'DIA-TTOF', 'SmallMolecule'). " +
            "Use the Name column from skyline_get_available_tutorials.")] string name,
        [Description("Language code (default: 'en'). Also available: 'ja' (Japanese), " +
            "'zh-CHS' (Simplified Chinese) for some tutorials.")] string language = "en",
        [Description("Output file path. If not specified, saves to a temp directory.")] string filePath = null)
    {
        return Invoke(connection =>
        {
            var metadata = connection.GetTutorial(name, language, filePath);
            if (metadata == null)
                return $"Tutorial not found: {name}";

            return FormatTutorialResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_get_tutorial_image"),
     Description("Download a screenshot image from a Skyline tutorial. By default returns the PNG " +
        "inline as an MCP ImageContentBlock so the model sees the image without a separate Read step. " +
        "Use this to view tutorial screenshots referenced in the markdown content (e.g., " +
        "'[Screenshot: s-01.png]'). The image is downloaded from GitHub (pinned to the running " +
        "Skyline version). Set returnFormat='file' to force file-on-disk, or 'inline' to require " +
        "inline (errors if too big or Skyline too old).")]
    public static CallToolResult GetTutorialImage(
        [Description("Tutorial name (e.g., 'MethodEdit', 'DIA-TTOF'). " +
            "Use the Name column from skyline_get_available_tutorials.")] string name,
        [Description("Image filename from the tutorial markdown " +
            "(e.g., 's-01.png', 's-ttof-label-free-proteome-quantification.png').")] string imageFilename,
        [Description("Language code (default: 'en').")] string language = "en",
        [Description("Return shape: 'auto' (default, inline with file fallback), 'inline' (always inline, error if too big or Skyline too old), or 'file' (write to disk and return the path).")] string returnFormat = RETURN_AUTO,
        [Description("Output file path. Honored only on the file path. Ignored otherwise.")] string filePath = null)
    {
        return InvokeContent(connection => InvokeImage(connection, returnFormat, filePath,
            bytesCall: c => c.GetTutorialImageBytes(name, imageFilename, language),
            fileCall: (c, fp) =>
            {
                var metadata = c.GetTutorialImage(name, imageFilename, language, fp);
                if (metadata == null)
                {
                    // Match the legacy non-error response so callers that ask for
                    // a not-yet-supported tutorial image get a TextContentBlock,
                    // not a tool error.
                    return TextContent($"Image not found: {imageFilename} in tutorial {name}",
                        ignoredFilePath: null);
                }
                return SavedToPath("Image downloaded", metadata.FilePath);
            }));
    }

    [McpServerTool(Name = "skyline_list_installed"),
     Description("Enumerate Skyline releases installed on this machine. Returns one tab-separated row per install with columns " +
        "Release\tVersion\tScope\tGuiPath\tCliPath\tRunnerPath. " +
        "Scope is 'system_admin' for installs under %ProgramFiles% or 'user_clickonce' for per-user ClickOnce installs (both may coexist for the same release). " +
        "GuiPath launches the Skyline UI: directly for admin installs, or via shell-execute of the .appref-ms shortcut for ClickOnce. " +
        "For batch/CLI use from a shell or Python script, every install exposes exactly one of CliPath or RunnerPath:\n\n" +
        "  - CliPath (admin installs only) is SkylineCmd.exe next to Skyline.exe. SkylineCmd.exe uses its OWN user.config separate from the GUI, " +
        "so custom reports / default-settings presets created in the GUI are not visible by default. To make them visible, run the relevant commands once " +
        "with --save-settings appended (e.g. '--report-add=my.skyr --save-settings'), which persists the in-memory Settings.Default at the end of the run. " +
        "(SkylineCmd --ui opens a GUI for manual configuration as a human escape hatch.)\n" +
        "  - RunnerPath (ClickOnce installs only) is the bundled SkylineRunner.exe / SkylineDailyRunner.exe shim that launches the user's GUI Skyline in headless CMD mode. " +
        "Because it goes through the GUI binary, it shares the GUI's user.config — custom reports etc. are visible without --save-settings priming. " +
        "Use this when the script needs the user's existing GUI state.\n\n" +
        "When no Skyline release is detected, the response is a single line starting with 'No Skyline release detected' instead of the column-header row. " +
        "This tool reports filesystem state only; use skyline_get_instances for running/connected instances.")]
    public static string ListInstalled()
    {
        var installs = SkylineInstallation.FindAll();
        if (installs.Count == 0)
        {
            return "No Skyline release detected. " +
                   "Install Skyline from https://skyline.ms or check that the install location is one of " +
                   "%ProgramFiles%\\Skyline[-daily] or the per-user ClickOnce location under Start Menu Programs.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Release\tVersion\tScope\tGuiPath\tCliPath\tRunnerPath");
        foreach (var install in installs)
        {
            sb.AppendLine(string.Join("\t",
                install.Release,
                install.Version,
                install.InstallScope,
                install.GuiPath ?? string.Empty,
                install.CliPath ?? string.Empty,
                install.RunnerPath ?? string.Empty));
        }
        // Trim only the trailing line ending. Using bare TrimEnd() would also strip
        // the terminating tab that separates the empty final field of the last row
        // (e.g. on an admin-only machine where RunnerPath is empty), turning that
        // row into a malformed 5-column line.
        return sb.ToString().TrimEnd('\r', '\n');
    }

    [McpServerTool(Name = "skyline_get_instances"),
     Description("List all connected Skyline instances. Returns process ID, version, document " +
        "path, and whether each is the active target. When multiple Skyline windows are open, " +
        "use skyline_set_instance to select which one to work with.")]
    public static string GetInstances()
    {
        var instances = SkylineConnection.GetAvailableInstances();
        if (instances.Count == 0)
        {
            return "No Skyline instances are connected. " +
                   "Start Skyline and choose Tools > AI Connector to connect.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Connected Skyline instances: {instances.Count}");
        sb.AppendLine();
        foreach (var inst in instances)
        {
            string targeted = inst.IsTargeted ? " [ACTIVE TARGET]" : "";
            string docPath = inst.DocumentPath ?? "(unsaved)";
            sb.AppendLine($"Process ID: {inst.ProcessId}{targeted}");
            sb.AppendLine($"  Version: {inst.SkylineVersion}");
            sb.AppendLine($"  Document: {docPath}");
            sb.AppendLine($"  Connected: {inst.ConnectedAt}");
            sb.AppendLine();
        }

        if (!instances.Any(i => i.IsTargeted) && instances.Count > 1)
        {
            sb.AppendLine("No specific instance is targeted. Calls go to the most recently connected instance.");
            sb.AppendLine("Use skyline_set_instance to target a specific instance by process ID.");
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "skyline_set_instance"),
     Description("Set which Skyline instance to target for subsequent tool calls. Use " +
        "skyline_get_instances to see available instances and their process IDs. " +
        "Pass 0 to clear the target and revert to auto-selecting the most recent instance.")]
    public static string SetInstance(
        [Description("Process ID of the Skyline instance to target. Use 0 to clear.")] int processId)
    {
        if (processId == 0)
        {
            SkylineConnection.TargetProcessId = null;
            return "Cleared instance target. Calls will go to the most recently connected Skyline instance.";
        }

        SkylineConnection.TargetProcessId = processId;

        // Verify the target is reachable
        try
        {
            var (connection, error) = SkylineConnection.TryConnect();
            if (connection == null)
            {
                SkylineConnection.TargetProcessId = null;
                return $"Failed to connect to Skyline process {processId}: {error}\n" +
                       "Target has been cleared.";
            }

            using (connection)
            {
                string docPath = connection.GetDocumentPath() ?? "(unsaved)";
                string version = connection.GetVersion() ?? "unknown";
                return $"Now targeting Skyline process {processId} (v{version})\n" +
                       $"Document: {docPath}";
            }
        }
        catch (Exception ex)
        {
            SkylineConnection.TargetProcessId = null;
            return $"Failed to connect to Skyline process {processId}: {ex.Message}\n" +
                   "Target has been cleared.";
        }
    }

    [McpServerTool(Name = "skyline_save_document"),
     Description("Save the current Skyline document. With no filePath, saves to the current " +
        "document path (equivalent to File > Save). With filePath, saves there and that becomes " +
        "the new document path (equivalent to File > Save As). When filePath refers to an " +
        "existing file, the save fails with an 'already exists' message unless overwrite is true; " +
        "this gives the LLM a chance to confirm with the user before clobbering existing data. " +
        "Wraps skyline_run_command with --save (or --out=PATH [--overwrite] when filePath is provided); " +
        "use that tool directly to combine saving with other operations in one call. " +
        "Returns the Skyline Immediate Window output.")]
    public static string SaveDocument(
        [Description("Optional path to save to. When omitted, saves to the current document path. " +
            "When provided, saves there and that becomes the new document path " +
            "(same as File > Save As).")] string filePath = null,
        [Description("When saving with a filePath that already exists, set true to overwrite. " +
            "Default false returns an 'already exists' error so the LLM can confirm with the user " +
            "before clobbering an existing file. Ignored when filePath is omitted.")] bool overwrite = false)
    {
        return Invoke(connection =>
        {
            string[] args;
            if (string.IsNullOrEmpty(filePath))
                args = new[] { "--save" };
            else if (overwrite)
                args = new[] { "--out=" + filePath, "--overwrite" };
            else
                args = new[] { "--out=" + filePath };
            string output = connection.RunCommand(args);
            return string.IsNullOrEmpty(output)
                ? "Document saved."
                : output;
        });
    }

    [McpServerTool(Name = "skyline_new_document"),
     Description("Create a new blank Skyline document, optionally with a specific UI mode and/or " +
        "saved settings preset. If the current document has unsaved changes, you must set " +
        "discardChanges to true or save first (skyline_save_document). " +
        "Wraps skyline_run_command with --new (plus --discard-changes and --doc-settings-name=NAME " +
        "as needed); use that tool directly for richer new-document flows.")]
    public static string NewDocument(
        [Description("UI mode for the new document: 'proteomic', 'small_molecules', or 'mixed'. " +
            "If omitted, keeps the current UI mode.")] string uiMode = null,
        [Description("Name of a saved settings preset from the Settings menu (e.g. 'Default'). " +
            "If omitted, uses the current default settings.")] string startSettings = null,
        [Description("Set to true to discard unsaved changes. If false (default) and the " +
            "document has unsaved changes, the operation will fail with an error.")] bool discardChanges = false)
    {
        return Invoke(connection =>
        {
            // Build RunCommand args for --new (without path = UI mode)
            var args = new List<string> { "--new" };
            if (discardChanges)
                args.Add("--discard-changes");
            if (!string.IsNullOrEmpty(startSettings))
                args.Add("--doc-settings-name=" + startSettings);

            string output = connection.RunCommand(args.ToArray());

            // Set UI mode if requested
            if (!string.IsNullOrEmpty(uiMode))
                connection.SetUiMode(uiMode);

            var status = connection.GetDocumentStatus();
            var sb = new StringBuilder();
            sb.AppendLine($"New document created (UI mode: {status.DocumentType}).");
            if (!string.IsNullOrEmpty(startSettings))
                sb.AppendLine($"Settings '{startSettings}' applied.");
            if (!string.IsNullOrEmpty(output?.Trim()))
                sb.AppendLine(output.Trim());
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_get_ui_mode"),
     Description("Get the current Skyline UI mode: 'proteomic', 'small_molecules', or 'mixed'. " +
        "The UI mode controls which interface elements are shown and how labels like " +
        "'Peptides' vs 'Molecules' are displayed.")]
    public static string GetUiMode()
    {
        return Invoke(connection => connection.GetUiMode());
    }

    [McpServerTool(Name = "skyline_set_ui_mode"),
     Description("Set the Skyline UI mode. Controls which interface elements are shown.")]
    public static string SetUiMode(
        [Description("UI mode: 'proteomic', 'small_molecules', or 'mixed'.")] string mode)
    {
        return Invoke(connection =>
        {
            connection.SetUiMode(mode);
            return $"UI mode set to '{connection.GetUiMode()}'.";
        });
    }

    [McpServerTool(Name = "skyline_get_undo_redo"),
     Description("Get the full undo/redo stack with descriptions and indices. " +
        "Index -1 = most recent undoable change, -2 = next oldest, etc. " +
        "Index +1 = most recent redoable change, +2 = next, etc. " +
        "An empty result means no undo or redo steps are available.")]
    public static string GetUndoRedo()
    {
        return Invoke(connection =>
        {
            var entries = connection.GetUndoRedo();
            if (entries.Length == 0)
                return "No undo or redo steps available.";

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                string prefix = entry.Index < 0 ? "undo" : "redo";
                sb.AppendLine($"[{entry.Index}] {prefix}: {entry.Description}");
            }
            return sb.ToString().TrimEnd();
        });
    }

    [McpServerTool(Name = "skyline_set_undo_redo_position"),
     Description("Navigate to a specific point in the undo/redo stack by index. " +
        "Use negative indices to undo (e.g. -1 undoes the last change, -3 undoes the last 3). " +
        "Use positive indices to redo (e.g. +1 redoes one step). " +
        "Get available indices from skyline_get_undo_redo.")]
    public static string SetUndoRedoPosition(
        [Description("Target index: negative to undo, positive to redo.")] int index)
    {
        return Invoke(connection =>
        {
            connection.SetUndoRedoPosition(index);
            if (index == 0)
                return "Already at current state.";
            return index < 0
                ? $"Undone to position {index}."
                : $"Redone to position {index}.";
        });
    }

    [McpServerTool(Name = "skyline_set_logging"),
     Description("Enable or disable diagnostic logging for Skyline MCP tool calls. " +
        "When enabled, subsequent tool responses include a diagnostic log " +
        "showing timing and internal steps. Use 'true' to enable, 'false' to disable.")]
    public static string SetLogging(
        [Description("'true' to enable diagnostic logging, 'false' to disable.")] string enabled)
    {
        bool value = string.Equals(enabled, @"true", StringComparison.OrdinalIgnoreCase);
        SkylineConnection.LoggingEnabled = value;
        return value ? @"Diagnostic logging enabled" : @"Diagnostic logging disabled";
    }

    /// <summary>
    /// Controls the level of detail in error messages returned to the LLM.
    /// </summary>
    public enum ErrorDetail
    {
        /// <summary>Only the exception message.</summary>
        Message,
        /// <summary>Full exception including type, message, inner exceptions, and stack trace.</summary>
        Full
    }

    /// <summary>
    /// Error reporting level for MCP tool responses. Defaults to Full so that
    /// LLMs and developers always see the complete error context.
    /// </summary>
    public static ErrorDetail ErrorDetailLevel { get; set; } = ErrorDetail.Full;

    // --- Image return-format wiring ---

    private const string RETURN_AUTO = "auto";
    private const string RETURN_INLINE = "inline";
    private const string RETURN_FILE = "file";
    private const string MIME_PNG = "image/png";

    // Default cap on the raw byte payload before base64 encoding. ~500 KB raw
    // becomes ~666 KB once base64-encoded for transport, which is the practical
    // ceiling we want to put in an LLM tool response. Tunable via the
    // SKYLINE_MCP_INLINE_IMAGE_CAP_BYTES environment variable so tests (and
    // operators in unusual contexts) can shrink or grow the cap without
    // recompiling. Same pattern as the MaxResponseChars knob on the row tools.
    private const int DEFAULT_INLINE_IMAGE_CAP_BYTES = 500_000;
    private const string ENV_INLINE_IMAGE_CAP_BYTES = "SKYLINE_MCP_INLINE_IMAGE_CAP_BYTES";

    // Recognized prefix for screen-capture denial / desktop-unavailable
    // responses returned by the legacy file-based GetFormImage path. These
    // are not paths and must not be wrapped in "saved to: ..." text.
    private const string SCREEN_CAPTURE_PREFIX = "Screen capture";

    private static int GetInlineImageCapBytes()
    {
        string raw = Environment.GetEnvironmentVariable(ENV_INLINE_IMAGE_CAP_BYTES);
        if (int.TryParse(raw, out int parsed) && parsed > 0)
            return parsed;
        return DEFAULT_INLINE_IMAGE_CAP_BYTES;
    }

    private static bool IsScreenCaptureDenial(string result)
    {
        return !string.IsNullOrEmpty(result) &&
               result.StartsWith(SCREEN_CAPTURE_PREFIX, StringComparison.Ordinal);
    }

    /// <summary>
    /// Shared image-tool body. Handles returnFormat dispatch, inline cap fallback,
    /// version-skew fallback (older Skyline lacking the *Bytes JSON-RPC method),
    /// and the per-mode response shapes:
    /// <list type="bullet">
    ///   <item>auto: bytes call; if too large or Skyline too old, fall back to the file call</item>
    ///   <item>inline: bytes call; error response if cap exceeded or Skyline too old</item>
    ///   <item>file: file call delegate returns the formatted CallToolResult directly</item>
    /// </list>
    /// The bytes path also honors <see cref="ImageBytesMetadata.Message"/> - when
    /// the server has a structured non-image response (e.g. permission denial),
    /// emit it as text content without flagging the call as an error.
    /// </summary>
    private static CallToolResult InvokeImage(
        SkylineConnection connection,
        string returnFormat,
        string filePath,
        Func<SkylineConnection, ImageBytesMetadata> bytesCall,
        Func<SkylineConnection, string, CallToolResult> fileCall)
    {
        returnFormat = string.IsNullOrEmpty(returnFormat) ? RETURN_AUTO : returnFormat.ToLowerInvariant();
        bool filePathProvided = !string.IsNullOrEmpty(filePath);

        if (returnFormat == RETURN_FILE)
            return fileCall(connection, filePath);

        if (returnFormat != RETURN_INLINE && returnFormat != RETURN_AUTO)
        {
            throw new ArgumentException(
                $"Invalid returnFormat '{returnFormat}'. Use 'auto', 'inline', or 'file'.");
        }

        // auto / inline both attempt the bytes path first
        ImageBytesMetadata bytes;
        try
        {
            bytes = bytesCall(connection);
        }
        catch (Exception ex) when (IsMethodNotFound(ex))
        {
            // Version skew: the connected Skyline does not expose the bytes
            // method yet (JSON-RPC ERROR_METHOD_NOT_FOUND). inline surfaces this
            // as an explicit error so the caller knows to retry with auto or file;
            // auto silently falls back. Matching on the typed error code (rather
            // than the message text) keeps unrelated InvalidOperationExceptions
            // from accidentally triggering the fallback.
            if (returnFormat == RETURN_INLINE)
            {
                throw new InvalidOperationException(
                    $"Inline image return is not supported by the connected Skyline. " +
                    $"Retry with returnFormat='auto' or 'file'. ({ex.Message})", ex);
            }
            return fileCall(connection, filePath);
        }

        if (bytes == null)
            return TextContent("No image data returned.", null);

        // Structured non-image response (denial / desktop unavailable). Treated
        // as a normal text result so callers see the same shape as file mode.
        if (bytes.Data == null || bytes.Data.Length == 0)
        {
            string message = string.IsNullOrEmpty(bytes.Message)
                ? "No image data returned."
                : bytes.Message;
            return TextContent(message, null);
        }

        int cap = GetInlineImageCapBytes();
        bool overCap = bytes.Data.Length > cap;

        if (overCap && returnFormat == RETURN_INLINE)
        {
            throw new InvalidOperationException(
                $"Image {JsonToolConstants.MSG_INLINE_CAP_EXCEEDED} ({bytes.Data.Length} bytes > {cap} bytes). " +
                $"Retry with returnFormat='auto' or 'file'.");
        }

        if (overCap)
        {
            // auto fallback: write bytes to the server-suggested path (or the
            // caller-provided override) and return a TextContentBlock describing the file.
            string target = filePathProvided ? filePath : bytes.FilePath;
            try
            {
                WriteBytesToDisk(bytes.Data, target);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Disk-full / ACL denial / read-only target on the WRAPPER side.
                // InvokeContent maps bare IOException to "Skyline disconnected",
                // which would mislead the caller, so surface a tool error here
                // that names the actual write failure and the attempted target.
                return ErrorResult(
                    $"Image {JsonToolConstants.MSG_INLINE_CAP_EXCEEDED} ({bytes.Data.Length} bytes > {cap} bytes), " +
                    $"but writing the fallback file failed: {ex.Message} (target: {NormalizePath(target)}). " +
                    "Retry with returnFormat='file' and an explicit filePath, or free up disk space.");
            }
            return TextContent(
                $"Image {JsonToolConstants.MSG_INLINE_CAP_EXCEEDED} ({bytes.Data.Length} bytes > {cap} bytes). " +
                $"Saved to: {NormalizePath(target)}\nUse the Read tool to view.",
                ignoredFilePath: null);
        }

        // Inline success path. If the caller passed a filePath it's ignored here -
        // note that in the response so they aren't surprised.
        var content = new List<ContentBlock>
        {
            new ImageContentBlock
            {
                Data = Convert.ToBase64String(bytes.Data),
                MimeType = string.IsNullOrEmpty(bytes.MimeType) ? MIME_PNG : bytes.MimeType
            }
        };
        if (filePathProvided)
        {
            content.Add(new TextContentBlock
            {
                Text = $"Note: filePath ignored on inline return (returnFormat='{returnFormat}')."
            });
        }
        return new CallToolResult { Content = content };
    }

    private static CallToolResult SavedToPath(string successPrefix, string filePath)
    {
        return TextContent($"{successPrefix} saved to: {filePath}\n\nUse the Read tool to view this image.", null);
    }

    private static CallToolResult TextContent(string text, string ignoredFilePath)
    {
        var content = new List<ContentBlock> { new TextContentBlock { Text = text } };
        if (!string.IsNullOrEmpty(ignoredFilePath))
            content.Add(new TextContentBlock { Text = $"Note: filePath '{ignoredFilePath}' ignored." });
        return new CallToolResult { Content = content };
    }

    private static void WriteBytesToDisk(byte[] data, string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, data);
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    /// <summary>
    /// Wraps every MCP tool call in consistent connection handling and exception handling.
    /// When Skyline is not connected, returns a helpful message instead of throwing.
    /// The connection is established per-call and disposed after each call.
    /// </summary>
    // Turns an ActionResult into the tool's reply: the plain done-message when the action completed, or that
    // message plus the reason it is not known to have completed (e.g. a dialog it left open) otherwise.
    private static string DescribeAction(ActionResult result, string doneMessage)
    {
        if (result.Completed)
            return doneMessage;
        // When the action left a dialog open, name it so the caller can drive it directly (get_controls /
        // set_value / dismiss / click) without a get_open_forms round-trip.
        string formHint = string.IsNullOrEmpty(result.FormId)
            ? " poll skyline_get_open_forms for any dialog it opened."
            : $" it left form '{result.FormId}' open; drive it with get_controls / set_form_value / dismiss / click.";
        return string.IsNullOrEmpty(result.Message)
            ? $"{doneMessage} This did not complete;{formHint}"
            : $"{doneMessage} This did not complete: {result.Message}.{formHint}";
    }

    /// <summary>
    /// How long one Skyline call may block before the MCP gives up on it. Skyline's pipe server is single-instance
    /// and serves one request at a time, so a verb riding a long operation (a big document load sitting behind its
    /// LongWaitDlg) would otherwise pin the connection and lock out every later call -- including the very call that
    /// would cancel the dialog. On timeout we drop the connection; Skyline peeks the pipe, sees the client is gone,
    /// abandons the waiting call (the work itself keeps running) and is immediately free to serve the next one.
    /// </summary>
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    // The message a timed-out call returns. It has to tell the caller two non-obvious things: the operation is still
    // running (nothing was undone), and Skyline is nevertheless reachable again right now.
    private const string CALL_TIMED_OUT_MESSAGE =
        "This call did not finish in time and was abandoned, so the connection to Skyline was dropped and Skyline " +
        "can accept new commands again. Skyline is STILL DOING the work it started (a long document load, an " +
        "import) -- nothing was undone or cancelled. Call skyline_get_open_forms to find the progress dialog, then " +
        "skyline_dismiss_with_cancel_button on it to actually cancel the operation, or simply wait and retry.";

    /// <summary>
    /// Runs one call to Skyline, giving up after <see cref="CallTimeout"/>. The deadline is applied to the response
    /// read itself (which is cancellable overlapped I/O), so the timeout releases the pipe handle -- and the
    /// <c>using (connection)</c> around this then really disconnects, which is the signal Skyline abandons the call on.
    /// </summary>
    private static T RunWithTimeout<T>(SkylineConnection connection, Func<T> call)
    {
        using (var timeout = new CancellationTokenSource(CallTimeout))
        {
            connection.CancellationToken = timeout.Token;
            return call();
        }
    }

    private static string Invoke(Func<SkylineConnection, string> action)
    {
        SkylineConnection connection = null;
        try
        {
            string error;
            (connection, error) = SkylineConnection.TryConnect();
            if (connection == null)
                return error;

            using (connection)
            {
                string result = RunWithTimeout(connection, () => action(connection));
                return AppendDiagnosticLog(result);
            }
        }
        catch (Exception ex)
        {
            // Gave up waiting. The read was cancelled and the connection dropped (the using block above) -- which is
            // what tells Skyline to abandon the call it was still working on.
            if (ex is OperationCanceledException)
                return CALL_TIMED_OUT_MESSAGE;

            // Check if this is a broken pipe (Skyline exited mid-call)
            if (ex is IOException)
            {
                return "Skyline disconnected during the operation. " +
                       "The Skyline process may have exited or been restarted. Try again.";
            }

            // Enrich version mismatch errors with Skyline and MCP server identity.
            // Match on the JSON-RPC error code (-32601 method-not-found) instead of
            // the message text so unrelated InvalidOperationExceptions whose messages
            // happen to mention "Unknown method:" cannot trigger the version-mismatch path.
            if (IsMethodNotFound(ex))
            {
                string skylineId = connection?.SkylineVersion;
                if (!string.IsNullOrEmpty(skylineId))
                {
                    return $"Error: {ex.Message}\n\n" +
                           $"This method is not available in {skylineId}. " +
                           "A newer version of Skyline may be required.";
                }
            }

            return ErrorDetailLevel == ErrorDetail.Full
                ? $"Error: {ex}"
                : $"Error: {ex.Message}";
        }
    }

    private static bool IsMethodNotFound(Exception ex)
    {
        return ex is JsonRpcException rpc && rpc.Code == JsonToolConstants.ERROR_METHOD_NOT_FOUND;
    }

    /// <summary>
    /// CallToolResult-returning counterpart of <see cref="Invoke"/>. Used by tools
    /// that emit non-text content blocks (e.g. <see cref="ImageContentBlock"/>).
    /// Maps the same error categories (no connection, broken pipe, version skew,
    /// generic exceptions) to <see cref="CallToolResult"/> shapes with
    /// <see cref="CallToolResult.IsError"/>=true for failures so MCP clients can
    /// distinguish success from error responses uniformly.
    /// </summary>
    private static CallToolResult InvokeContent(Func<SkylineConnection, CallToolResult> action)
    {
        SkylineConnection connection = null;
        try
        {
            string error;
            (connection, error) = SkylineConnection.TryConnect();
            if (connection == null)
                return ErrorResult(error);

            using (connection)
            {
                var result = RunWithTimeout(connection, () => action(connection));
                AppendDiagnosticLog(result);
                return result;
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
                return ErrorResult(CALL_TIMED_OUT_MESSAGE);

            if (ex is IOException)
            {
                return ErrorResult("Skyline disconnected during the operation. " +
                                   "The Skyline process may have exited or been restarted. Try again.");
            }

            if (IsMethodNotFound(ex))
            {
                string skylineId = connection?.SkylineVersion;
                if (!string.IsNullOrEmpty(skylineId))
                {
                    return ErrorResult($"Error: {ex.Message}\n\n" +
                                       $"This method is not available in {skylineId}. " +
                                       "A newer version of Skyline may be required.");
                }
            }

            string message = ErrorDetailLevel == ErrorDetail.Full
                ? $"Error: {ex}"
                : $"Error: {ex.Message}";
            return ErrorResult(message);
        }
    }

    private static CallToolResult ErrorResult(string message)
    {
        return new CallToolResult
        {
            Content = new List<ContentBlock> { new TextContentBlock { Text = message } },
            IsError = true
        };
    }

    private static void AppendDiagnosticLog(CallToolResult result)
    {
        string log = SkylineConnection.LastLog;
        if (string.IsNullOrEmpty(log) || result?.Content == null)
            return;
        result.Content.Add(new TextContentBlock { Text = "\n--- Diagnostic Log ---\n" + log });
        SkylineConnection.LastLog = null;
    }

    private static string AppendDiagnosticLog(string result)
    {
        string log = SkylineConnection.LastLog;
        if (!string.IsNullOrEmpty(log))
        {
            result = result + "\n\n--- Diagnostic Log ---\n" + log;
            SkylineConnection.LastLog = null;
        }
        return result;
    }

    private static string FormatReportResult(ReportMetadata metadata)
    {
        if (metadata == null)
            return "Report returned no data. The report may not exist or the document may be empty.";

        var sb = new StringBuilder();
        sb.AppendLine($"Report: {metadata.ReportName ?? "Report"}");
        if (metadata.RowCount.HasValue)
            sb.AppendLine($"Rows: {metadata.RowCount.Value}");
        if (metadata.Columns != null)
            sb.AppendLine($"Columns: {metadata.Columns}");
        if (metadata.Format != null)
            sb.AppendLine($"Format: {metadata.Format}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(metadata.Preview))
        {
            sb.AppendLine("Preview:");
            sb.AppendLine(metadata.Preview);
            if (metadata.RowCount > 5)
                sb.AppendLine($"... ({metadata.RowCount.Value - 5} more rows)");
            sb.AppendLine();
        }

        if (metadata.FilePath != null)
        {
            sb.AppendLine($"Full data saved to: {metadata.FilePath}");
            sb.AppendLine("Use the Read tool to explore the full dataset.");
        }

        return sb.ToString();
    }

    private static string FormatTutorialResult(TutorialMetadata metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tutorial: {metadata.Title}");
        sb.AppendLine($"Lines: {metadata.LineCount}");
        sb.AppendLine($"File: {metadata.FilePath}");
        sb.AppendLine();
        sb.AppendLine("Table of Contents:");
        foreach (var entry in metadata.Toc ?? Array.Empty<TocEntry>())
        {
            string indent = entry.Level > 1 ? "  " : "";
            sb.AppendLine($"{indent}- {entry.Heading} (line {entry.Line})");
        }
        sb.AppendLine();
        sb.AppendLine("Use the Read tool to read sections from the file.");
        return sb.ToString();
    }

    private static string GetTempSettingsPath(string label)
    {
        string tmpDir = Environment.GetEnvironmentVariable("SKYLINE_MCP_TMP_DIR");
        if (string.IsNullOrEmpty(tmpDir))
        {
            tmpDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Skyline", "mcp", "tmp");
        }
        Directory.CreateDirectory(tmpDir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string fileName = $"skyline-settings-{label}-{timestamp}.xml";
        return Path.Combine(tmpDir, fileName);
    }

    /// <summary>
    /// Splits a command-line string into individual arguments, respecting
    /// double-quoted segments (e.g., <c>--name="Peak Area" --file=out.csv</c>
    /// becomes <c>["--name=Peak Area", "--file=out.csv"]</c>).
    /// </summary>
    private static string[] SplitCommandArgs(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine))
            return Array.Empty<string>();

        var args = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args.ToArray();
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
