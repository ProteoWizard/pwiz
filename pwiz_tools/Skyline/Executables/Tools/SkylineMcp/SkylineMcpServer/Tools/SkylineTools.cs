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
using System.Linq;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using SkylineTool;

namespace SkylineMcpServer.Tools;

[McpServerToolType]
public static class SkylineTools
{
    [McpServerTool(Name = "skyline_get_document_path"),
     Description("Get the file path of the currently open Skyline document.")]
    public static string GetDocumentPath()
    {
        return Invoke(connection =>
        {
            string path = connection.Call(nameof(IJsonToolService.GetDocumentPath));
            return path ?? "(unsaved)";
        });
    }

    [McpServerTool(Name = "skyline_get_version"),
     Description("Get the version of the running Skyline instance.")]
    public static string GetVersion()
    {
        return Invoke(connection =>
        {
            string version = connection.Call(nameof(IJsonToolService.GetVersion));
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
            string selection = connection.Call(nameof(IJsonToolService.GetSelection));
            return string.IsNullOrEmpty(selection)
                ? "Nothing is currently selected in Skyline."
                : selection;
        });
    }

    [McpServerTool(Name = "skyline_get_replicate"),
     Description("Get the name of the currently active replicate in Skyline.")]
    public static string GetReplicate()
    {
        return Invoke(connection =>
        {
            string replicate = connection.Call(nameof(IJsonToolService.GetReplicateName));
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
            string result = connection.Call(nameof(IJsonToolService.GetReplicateNames));
            return string.IsNullOrEmpty(result)
                ? "No replicates in the document."
                : result;
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
            string result = string.IsNullOrEmpty(rootLocator)
                ? connection.Call(nameof(IJsonToolService.GetLocations), level)
                : connection.Call(nameof(IJsonToolService.GetLocations), level, rootLocator);
            return string.IsNullOrEmpty(result)
                ? "No elements found at the specified level."
                : result;
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
            var metadata = connection.CallTyped<ReportMetadata>(nameof(IJsonToolService.ExportReport), reportName, filePath, culture);
            return FormatReportResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_get_report_from_definition"),
     Description("Run a custom Skyline report from a JSON report definition and return results. Use this when you need specific columns not available in predefined reports. The JSON format uses a 'select' array of column display names (PascalCase, invariant). Use skyline_get_report_doc_topics and skyline_get_report_doc_topic to discover available column names. Example: {\"select\": [\"ProteinName\", \"PeptideModifiedSequence\", \"PrecursorMz\", \"BestRetentionTime\", \"Area\"]}. The row source is automatically inferred from the selected columns. " +
        "Optional 'scope' field targets a specific reporting scope: 'document_grid' (default, auto-detected), 'audit_log', 'group_comparisons', or 'candidate_peaks'. " +
        "Optional 'filter' array filters rows: [{\"column\": \"Area\", \"op\": \">\", \"value\": \"1000\"}, {\"column\": \"ProteinName\", \"op\": \"contains\", \"value\": \"INS\"}]. " +
        "Valid filter ops: 'equals', '<>', '>', '<', '>=', '<=', 'contains', 'notcontains', 'startswith', 'notstartswith', 'isnullorblank', 'isnotnullorblank'. " +
        "Filter columns can reference any column in the data model, not just selected ones. 'value' is required for all ops except 'isnullorblank'/'isnotnullorblank'. " +
        "Optional 'sort' array sorts results: [{\"column\": \"Area\", \"direction\": \"desc\"}]. Sort columns must be in the 'select' list. Direction: 'asc' (default) or 'desc'. " +
        "Optional 'pivot_replicate': true pivots replicates into columns (one column per replicate); false forces one row per replicate. Omit to use default inference. " +
        "Optional 'pivot_isotope_label': true pivots isotope label types into columns.")]
    public static string GetReportFromDefinition(
        [Description("JSON report definition with a 'select' array of column names. Example: {\"select\": [\"ProteinName\", \"PrecursorMz\", \"Area\"]}. Optional 'name' field for the report name. Optional 'filter' array to filter rows, 'sort' array to sort results, and 'scope' to target a specific reporting scope.")] string reportDefinitionJson,
        [Description("Output file path. If not specified, saves to a temp directory. Extension determines format (.csv, .tsv, .parquet).")] string filePath = null,
        [Description("Output format when filePath is not specified: csv, tsv, or parquet (default: csv)")] string format = "csv",
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        return Invoke(connection =>
        {
            filePath ??= GetTempReportPath(JsonToolConstants.DEFAULT_REPORT_NAME, format);
            string culture = invariant ? JsonToolConstants.CULTURE_INVARIANT : JsonToolConstants.CULTURE_LOCALIZED;
            var metadata = connection.CallTyped<ReportMetadata>(nameof(IJsonToolService.ExportReportFromDefinition), reportDefinitionJson, filePath, culture);
            return FormatReportResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_add_report"),
     Description("Save a custom report definition to the user's Skyline session. Uses the same JSON format as skyline_get_report_from_definition but persists the report so it appears in Skyline's report list. The 'name' field is required. Use skyline_get_report_doc_topics and skyline_get_report_doc_topic to discover available column names. " +
        "Supports optional 'scope', 'filter', 'pivot_replicate', and 'pivot_isotope_label' fields - see skyline_get_report_from_definition for format details. " +
        "Note: 'sort' is ignored when saving reports, as Skyline report definitions do not include a default sort order.")]
    public static string AddReport(
        [Description("JSON report definition with a required 'name' field and a 'select' array of column names. Example: {\"name\": \"My Report\", \"select\": [\"ProteinName\", \"PrecursorMz\", \"Area\"]}. Optional 'scope', 'filter', 'pivot_replicate', and 'pivot_isotope_label' fields.")] string reportDefinitionJson)
    {
        return Invoke(connection =>
        {
            return connection.Call(nameof(IJsonToolService.AddReportFromDefinition), reportDefinitionJson);
        });
    }

    [McpServerTool(Name = "skyline_get_settings_list_types"),
     Description("Enumerate all settings list types available in Skyline (enzymes, modifications, reports, etc.). Returns tab-separated lines of PropertyName and Title. Use this to discover what configuration lists exist before querying their contents.")]
    public static string GetSettingsListTypes()
    {
        return Invoke(connection =>
        {
            string result = connection.Call(nameof(IJsonToolService.GetSettingsListTypes));
            return string.IsNullOrEmpty(result)
                ? "No settings lists found."
                : result;
        });
    }

    [McpServerTool(Name = "skyline_get_settings_list_names"),
     Description("Get item names from a specific settings list. Use skyline_get_settings_list_types first to discover available list types. For PersistedViews (reports), names are grouped by Main and External Tools sections.")]
    public static string GetSettingsListNames(
        [Description("The settings list property name (e.g., 'EnzymeList', 'PersistedViews')")] string listType)
    {
        return Invoke(connection =>
        {
            string result = connection.Call(nameof(IJsonToolService.GetSettingsListNames), listType);
            return string.IsNullOrEmpty(result)
                ? $"No items found in {listType}."
                : result;
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
            string result = connection.Call(nameof(IJsonToolService.GetSettingsListItem), listType, itemName);
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
        return Invoke(connection => overwrite
            ? connection.Call(nameof(IJsonToolService.AddSettingsListItem), listType, itemXml, "true")
            : connection.Call(nameof(IJsonToolService.AddSettingsListItem), listType, itemXml));
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
            string result = connection.Call(
                nameof(IJsonToolService.GetSettingsListSelectedItems), listType);
            return string.IsNullOrEmpty(result)
                ? $"No items are currently selected in {listType}."
                : result;
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
            connection.Call(nameof(IJsonToolService.SelectSettingsListItems),
                listType, JsonSerializer.Serialize(itemNames)));
    }

    [McpServerTool(Name = "skyline_run_command"),
     Description("Run a command line against the running Skyline instance. Uses the same command syntax as SkylineCmd/SkylineRunner. Commands are echoed to Skyline's Immediate Window for user visibility. Examples: '--report-name=\"Peak Area\" --report-file=output.csv', '--import-file=results.raw', '--refine-cv-remove-above-cutoff=20'. Use '--help' to see all available commands.")]
    public static string RunCommand(
        [Description("Command line arguments in SkylineCmd format (e.g., '--report-name=\"Peak Area\" --report-file=output.csv')")] string commandArgs)
    {
        return Invoke(connection =>
        {
            string output = connection.Call(nameof(IJsonToolService.RunCommand), commandArgs);
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
            string output = connection.Call(nameof(IJsonToolService.RunCommandSilent), "--help=sections");
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
            string output = connection.Call(nameof(IJsonToolService.RunCommandSilent), $"--help={section} --help=no-borders");
            return string.IsNullOrEmpty(output)
                ? $"No help found for section: {section}"
                : output;
        });
    }

    [McpServerTool(Name = "skyline_get_report_doc_topics"),
     Description("List available report column documentation topics. Returns tab-separated lines " +
        "of DisplayName and ColumnCount for each entity type (e.g., Molecule, Precursor, Transition). " +
        "Use skyline_get_report_doc_topic to get column details for a specific topic. " +
        "Supports multiple scopes: 'document_grid' (default), 'audit_log', 'group_comparisons', " +
        "'candidate_peaks'.")]
    public static string GetReportDocTopics(
        [Description("Reporting scope: 'document_grid' (default), 'audit_log', 'group_comparisons', " +
            "or 'candidate_peaks'. Each scope has its own column namespace.")] string scope = null)
    {
        return Invoke(connection =>
        {
            string result = string.IsNullOrEmpty(scope)
                ? connection.Call(nameof(IJsonToolService.GetReportDocTopics))
                : connection.Call(nameof(IJsonToolService.GetReportDocTopics), scope);
            return string.IsNullOrEmpty(result)
                ? "No report documentation topics found."
                : result;
        });
    }

    [McpServerTool(Name = "skyline_get_report_doc_topic"),
     Description("Get column documentation for a specific report entity type. Returns a table of " +
        "column names, descriptions, and types. Use skyline_get_report_doc_topics to discover " +
        "available topics. Supports scopes: 'document_grid' (default), 'audit_log', " +
        "'group_comparisons', 'candidate_peaks'.")]
    public static string GetReportDocTopic(
        [Description("The topic name (display name like 'Molecule' or qualified type name). " +
            "Case-insensitive partial match on display name.")] string topic,
        [Description("Reporting scope: 'document_grid' (default), 'audit_log', 'group_comparisons', " +
            "or 'candidate_peaks'.")] string scope = null)
    {
        return Invoke(connection =>
        {
            string result = string.IsNullOrEmpty(scope)
                ? connection.Call(nameof(IJsonToolService.GetReportDocTopic), topic)
                : connection.Call(nameof(IJsonToolService.GetReportDocTopic), topic, scope);
            return string.IsNullOrEmpty(result)
                ? $"No documentation found for topic: {topic}"
                : result;
        });
    }

    [McpServerTool(Name = "skyline_insert_small_molecule_transition_list"),
     Description("Insert a small molecule transition list into the Skyline document. The input is CSV text with column headers in the first row. Skyline determines column meaning from headers. Common headers: MoleculeGroup, PrecursorName, ProductName, PrecursorFormula, ProductFormula, PrecursorAdduct, ProductAdduct, PrecursorMz, ProductMz, PrecursorCharge, ProductCharge, PrecursorRT, LabelType, CAS, InChiKey, HMDB, SMILES, Note. This is the same format as Edit > Insert > Transition List in the Skyline UI. If Skyline cannot interpret the headers, it returns an error - adjust header names and retry.")]
    public static string InsertSmallMoleculeTransitionList(
        [Description("CSV text with column headers in the first row and data rows. Common headers: MoleculeGroup, PrecursorName, PrecursorFormula, PrecursorAdduct, PrecursorMz, PrecursorCharge, ProductFormula, ProductAdduct, ProductMz, ProductCharge, PrecursorRT, LabelType, CAS, InChiKey, HMDB, SMILES, Note.")] string textCsv)
    {
        return Invoke(connection =>
        {
            return connection.Call(nameof(IJsonToolService.InsertSmallMoleculeTransitionList), textCsv);
        });
    }

    [McpServerTool(Name = "skyline_import_fasta"),
     Description("Import protein sequences in FASTA format into the Skyline document. Skyline will digest proteins using current enzyme settings and add peptides and transitions based on current transition settings. Each protein starts with a '>' header line followed by sequence lines.")]
    public static string ImportFasta(
        [Description("Protein sequences in standard FASTA format. Each protein starts with a '>' header line (e.g., '>sp|P01308|INS_HUMAN Insulin') followed by one or more sequence lines.")] string textFasta)
    {
        return Invoke(connection =>
        {
            return connection.Call(nameof(IJsonToolService.ImportFasta), textFasta);
        });
    }

    [McpServerTool(Name = "skyline_import_properties"),
     Description("Import properties (annotations) into the Skyline document. The input is CSV text where the first column contains ElementLocator paths identifying targets or replicates, and remaining columns are annotation names with values. Export a report containing locators first to understand the document structure. The locator format uses paths like /MoleculeGroup[name='Lipids']/Molecule[name='CE 18:1'].")]
    public static string ImportProperties(
        [Description("CSV text where the first column is ElementLocator (paths identifying document elements) and remaining columns are annotation names with values.")] string csvText)
    {
        return Invoke(connection =>
        {
            return connection.Call(nameof(IJsonToolService.ImportProperties), csvText);
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
            if (string.IsNullOrEmpty(additionalLocators))
                return connection.Call(nameof(IJsonToolService.SetSelectedElement), elementLocator);
            return connection.Call(nameof(IJsonToolService.SetSelectedElement), elementLocator, additionalLocators);
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
            return connection.Call(nameof(IJsonToolService.SetReplicate), replicateName);
        });
    }

    [McpServerTool(Name = "skyline_get_document_status"),
     Description("Get a lightweight overview of the current Skyline document including document type (proteomic, small_molecules, mixed), target counts (proteins/lists, peptides/molecules, precursors, transitions), replicate count, and file path. Much faster than running a report for basic document info.")]
    public static string GetDocumentStatus()
    {
        return Invoke(connection =>
        {
            string result = connection.Call(nameof(IJsonToolService.GetDocumentStatus));
            return result ?? "No document information available.";
        });
    }

    [McpServerTool(Name = "skyline_get_open_forms"),
     Description("Enumerate all open forms in the Skyline window. Returns tab-separated lines " +
        "with form type, title, whether it contains a ZedGraph graph, dock state, and a stable " +
        "identifier in TypeName:Title format (e.g., 'GraphSummary:Peak Areas - Replicate Comparison'). " +
        "Use the identifier with skyline_get_graph_data, skyline_get_graph_image, and skyline_get_form_image. " +
        "DockState values: Floating, Document, DockTop/Left/Bottom/Right, DockTopAutoHide/etc., Dialog.")]
    public static string GetOpenForms()
    {
        return Invoke(connection =>
        {
            string result = connection.Call(nameof(IJsonToolService.GetOpenForms));
            return string.IsNullOrEmpty(result)
                ? "No forms are currently open in Skyline."
                : result;
        });
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
            string result = string.IsNullOrEmpty(filePath)
                ? connection.Call(nameof(IJsonToolService.GetGraphData), graphId)
                : connection.Call(nameof(IJsonToolService.GetGraphData), graphId, filePath);
            return string.IsNullOrEmpty(result)
                ? "No data in graph."
                : $"Graph data saved to: {result}\n\nUse the Read tool to examine the data.";
        });
    }

    [McpServerTool(Name = "skyline_get_graph_image"),
     Description("Export a PNG image of a Skyline graph. Saves to a file and returns the " +
        "path. Use the Read tool to view the image. Use skyline_get_open_forms to discover " +
        "graph IDs.")]
    public static string GetGraphImage(
        [Description("Graph identifier from skyline_get_open_forms (e.g., 'GraphSummary:Peak Areas - Replicate Comparison')")] string graphId,
        [Description("Output file path. If not specified, saves to a temp directory.")] string filePath = null)
    {
        return Invoke(connection =>
        {
            string result = string.IsNullOrEmpty(filePath)
                ? connection.Call(nameof(IJsonToolService.GetGraphImage), graphId)
                : connection.Call(nameof(IJsonToolService.GetGraphImage), graphId, filePath);
            return $"Graph image saved to: {result}\n\nUse the Read tool to view this image.";
        });
    }

    [McpServerTool(Name = "skyline_get_form_image"),
     Description("Export a PNG screenshot of any open Skyline form, dialog, or dockable panel. " +
        "The screenshot is captured from the screen with non-Skyline content automatically redacted. " +
        "Use skyline_get_open_forms to discover form IDs. For graphs, prefer skyline_get_graph_image " +
        "which renders directly without screen capture.")]
    public static string GetFormImage(
        [Description("Form identifier from skyline_get_open_forms (e.g., 'SequenceTreeForm:Targets', 'PeptideSettingsUI:Peptide Settings')")] string formId,
        [Description("Output file path. If not specified, saves to a temp directory.")] string filePath = null)
    {
        return Invoke(connection =>
        {
            string result = string.IsNullOrEmpty(filePath)
                ? connection.Call(nameof(IJsonToolService.GetFormImage), formId)
                : connection.Call(nameof(IJsonToolService.GetFormImage), formId, filePath);
            if (result == "Screen capture denied by user.")
                return result;
            return $"Form image saved to: {result}\n\nUse the Read tool to view this image.";
        });
    }

    [McpServerTool(Name = "skyline_get_document_settings"),
     Description("Export the current document's settings (enzyme, transitions, filters, modifications, full-scan, etc.) as XML to a file. Strips replicate/results data for size. Returns the file path. Compare against skyline_get_default_settings to find what differs from defaults.")]
    public static string GetDocumentSettings(
        [Description("Output file path. If not specified, saves to a temp directory.")] string filePath = null)
    {
        return Invoke(connection =>
        {
            filePath ??= GetTempSettingsPath("document");
            string result = connection.Call(nameof(IJsonToolService.GetDocumentSettings), filePath);
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
            string result = connection.Call(nameof(IJsonToolService.GetDefaultSettings), filePath);
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
            string result = connection.Call(nameof(IJsonToolService.GetAvailableTutorials));
            return string.IsNullOrEmpty(result)
                ? "No tutorials available."
                : result;
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
            var metadata = connection.CallTyped<TutorialMetadata>(
                nameof(IJsonToolService.GetTutorial), name, language, filePath);
            if (metadata == null)
                return $"Tutorial not found: {name}";

            return FormatTutorialResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_get_tutorial_image"),
     Description("Download a screenshot image from a Skyline tutorial. Use this to view " +
        "tutorial screenshots referenced in the markdown content (e.g., '[Screenshot: s-01.png]'). " +
        "The image is downloaded from GitHub (pinned to the running Skyline version) and saved " +
        "to a local file. Use the Read tool to view the image at the returned file path.")]
    public static string GetTutorialImage(
        [Description("Tutorial name (e.g., 'MethodEdit', 'DIA-TTOF'). " +
            "Use the Name column from skyline_get_available_tutorials.")] string name,
        [Description("Image filename from the tutorial markdown " +
            "(e.g., 's-01.png', 's-ttof-label-free-proteome-quantification.png').")] string imageFilename,
        [Description("Language code (default: 'en').")] string language = "en",
        [Description("Output file path. If not specified, saves to a temp directory.")] string filePath = null)
    {
        return Invoke(connection =>
        {
            var metadata = connection.CallTyped<TutorialImageMetadata>(
                nameof(IJsonToolService.GetTutorialImage), name, imageFilename, language, filePath);
            if (metadata == null)
                return $"Image not found: {imageFilename} in tutorial {name}";

            return $"Image downloaded: {metadata.FilePath}\n\nUse the Read tool to view this image.";
        });
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
                string docPath = connection.Call(nameof(IJsonToolService.GetDocumentPath)) ?? "(unsaved)";
                string version = connection.Call(nameof(IJsonToolService.GetVersion)) ?? "unknown";
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

    /// <summary>
    /// Wraps every MCP tool call in consistent connection handling and exception handling.
    /// When Skyline is not connected, returns a helpful message instead of throwing.
    /// The connection is established per-call and disposed after each call.
    /// </summary>
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
                string result = action(connection);
                return AppendDiagnosticLog(result);
            }
        }
        catch (Exception ex)
        {
            // Check if this is a broken pipe (Skyline exited mid-call)
            if (ex is IOException)
            {
                return "Skyline disconnected during the operation. " +
                       "The Skyline process may have exited or been restarted. Try again.";
            }

            // Enrich version mismatch errors with Skyline and MCP server identity
            if (ex is InvalidOperationException && ex.Message.Contains("Unknown method:"))
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
