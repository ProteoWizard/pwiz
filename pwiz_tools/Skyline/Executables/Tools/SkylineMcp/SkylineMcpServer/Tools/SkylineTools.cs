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
        return Invoke(connection =>
        {
            string path = connection.Call("GetDocumentPath");
            return path ?? "(unsaved)";
        });
    }

    [McpServerTool(Name = "skyline_get_version"),
     Description("Get the version of the running Skyline instance.")]
    public static string GetVersion()
    {
        return Invoke(connection =>
        {
            string version = connection.Call("GetVersion");
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
            string selection = connection.Call("GetSelection");
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
            string replicate = connection.Call("GetReplicateName");
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
            string result = connection.Call("GetReplicateNames");
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
                ? connection.Call("GetLocations", level)
                : connection.Call("GetLocations", level, rootLocator);
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
            string culture = invariant ? "invariant" : "localized";
            string metadata = connection.Call("ExportReport", reportName, filePath, culture);
            return FormatReportResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_get_report_from_definition"),
     Description("Run a custom Skyline report from a JSON report definition and return results. Use this when you need specific columns not available in predefined reports. The JSON format uses a 'select' array of column display names (PascalCase, invariant). Use skyline_get_report_doc_topics and skyline_get_report_doc_topic to discover available column names. Example: {\"select\": [\"ProteinName\", \"PeptideModifiedSequence\", \"PrecursorMz\", \"BestRetentionTime\", \"Area\"]}. The row source is automatically inferred from the selected columns. " +
        "Optional 'filter' array filters rows: [{\"column\": \"Area\", \"op\": \">\", \"value\": \"1000\"}, {\"column\": \"ProteinName\", \"op\": \"contains\", \"value\": \"INS\"}]. " +
        "Valid filter ops: 'equals', '<>', '>', '<', '>=', '<=', 'contains', 'notcontains', 'startswith', 'notstartswith', 'isnullorblank', 'isnotnullorblank'. " +
        "Filter columns can reference any column in the data model, not just selected ones. 'value' is required for all ops except 'isnullorblank'/'isnotnullorblank'. " +
        "Optional 'sort' array sorts results: [{\"column\": \"Area\", \"direction\": \"desc\"}]. Sort columns must be in the 'select' list. Direction: 'asc' (default) or 'desc'. " +
        "Optional 'pivotReplicate': true pivots replicates into columns (one column per replicate); false forces one row per replicate. Omit to use default inference. " +
        "Optional 'pivotIsotopeLabel': true pivots isotope label types into columns.")]
    public static string GetReportFromDefinition(
        [Description("JSON report definition with a 'select' array of column names. Example: {\"select\": [\"ProteinName\", \"PrecursorMz\", \"Area\"]}. Optional 'name' field for the report name. Optional 'filter' array to filter rows and 'sort' array to sort results.")] string reportDefinitionJson,
        [Description("Output file path. If not specified, saves to a temp directory. Extension determines format (.csv, .tsv, .parquet).")] string filePath = null,
        [Description("Output format when filePath is not specified: csv, tsv, or parquet (default: csv)")] string format = "csv",
        [Description("Use invariant locale for consistent decimal separators and full precision (default: true). Set to false for localized format.")] bool invariant = true)
    {
        return Invoke(connection =>
        {
            filePath ??= GetTempReportPath("Custom", format);
            string culture = invariant ? "invariant" : "localized";
            string metadata = connection.Call("ExportReportFromDefinition", reportDefinitionJson, filePath, culture);
            return FormatReportResult(metadata);
        });
    }

    [McpServerTool(Name = "skyline_add_report"),
     Description("Save a custom report definition to the user's Skyline session. Uses the same JSON format as skyline_get_report_from_definition but persists the report so it appears in Skyline's report list. The 'name' field is required. Use skyline_get_report_doc_topics and skyline_get_report_doc_topic to discover available column names. " +
        "Supports optional 'filter', 'pivotReplicate', and 'pivotIsotopeLabel' fields - see skyline_get_report_from_definition for format details. " +
        "Note: 'sort' is ignored when saving reports, as Skyline report definitions do not include a default sort order.")]
    public static string AddReport(
        [Description("JSON report definition with a required 'name' field and a 'select' array of column names. Example: {\"name\": \"My Report\", \"select\": [\"ProteinName\", \"PrecursorMz\", \"Area\"]}. Optional 'filter', 'pivotReplicate', and 'pivotIsotopeLabel' fields.")] string reportDefinitionJson)
    {
        return Invoke(connection =>
        {
            return connection.Call("AddReportFromDefinition", reportDefinitionJson);
        });
    }

    [McpServerTool(Name = "skyline_get_settings_list_types"),
     Description("Enumerate all settings list types available in Skyline (enzymes, modifications, reports, etc.). Returns tab-separated lines of PropertyName and Title. Use this to discover what configuration lists exist before querying their contents.")]
    public static string GetSettingsListTypes()
    {
        return Invoke(connection =>
        {
            string result = connection.Call("GetSettingsListTypes");
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
            string result = connection.Call("GetSettingsListNames", listType);
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
            string result = connection.Call("GetSettingsListItem", listType, itemName);
            return string.IsNullOrEmpty(result)
                ? $"Item not found: {itemName} in {listType}."
                : result;
        });
    }

    [McpServerTool(Name = "skyline_run_command"),
     Description("Run a command line against the running Skyline instance. Uses the same command syntax as SkylineCmd/SkylineRunner. Commands are echoed to Skyline's Immediate Window for user visibility. Examples: '--report-name=\"Peak Area\" --report-file=output.csv', '--import-file=results.raw', '--refine-cv-remove-above-cutoff=20'. Use '--help' to see all available commands.")]
    public static string RunCommand(
        [Description("Command line arguments in SkylineCmd format (e.g., '--report-name=\"Peak Area\" --report-file=output.csv')")] string commandArgs)
    {
        return Invoke(connection =>
        {
            string output = connection.Call("RunCommand", commandArgs);
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
            string output = connection.Call("RunCommandSilent", "--help=sections");
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
            string output = connection.Call("RunCommandSilent", $"--help={section} --help=no-borders");
            return string.IsNullOrEmpty(output)
                ? $"No help found for section: {section}"
                : output;
        });
    }

    [McpServerTool(Name = "skyline_get_report_doc_topics"),
     Description("List available report column documentation topics. Returns tab-separated lines of DisplayName and QualifiedTypeName for each entity type (e.g., Molecule, Precursor, Transition). Use skyline_get_report_doc_topic to get column details for a specific topic.")]
    public static string GetReportDocTopics()
    {
        return Invoke(connection =>
        {
            string result = connection.Call("GetReportDocTopics");
            return string.IsNullOrEmpty(result)
                ? "No report documentation topics found."
                : result;
        });
    }

    [McpServerTool(Name = "skyline_get_report_doc_topic"),
     Description("Get column documentation for a specific report entity type. Returns a table of column names, descriptions, and types. Use skyline_get_report_doc_topics to discover available topics.")]
    public static string GetReportDocTopic(
        [Description("The topic name (display name like 'Molecule' or qualified type name). Case-insensitive partial match on display name.")] string topic)
    {
        return Invoke(connection =>
        {
            string result = connection.Call("GetReportDocTopic", topic);
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
            return connection.Call("InsertSmallMoleculeTransitionList", textCsv);
        });
    }

    [McpServerTool(Name = "skyline_import_fasta"),
     Description("Import protein sequences in FASTA format into the Skyline document. Skyline will digest proteins using current enzyme settings and add peptides and transitions based on current transition settings. Each protein starts with a '>' header line followed by sequence lines.")]
    public static string ImportFasta(
        [Description("Protein sequences in standard FASTA format. Each protein starts with a '>' header line (e.g., '>sp|P01308|INS_HUMAN Insulin') followed by one or more sequence lines.")] string textFasta)
    {
        return Invoke(connection =>
        {
            return connection.Call("ImportFasta", textFasta);
        });
    }

    [McpServerTool(Name = "skyline_import_properties"),
     Description("Import properties (annotations) into the Skyline document. The input is CSV text where the first column contains ElementLocator paths identifying targets or replicates, and remaining columns are annotation names with values. Export a report containing locators first to understand the document structure. The locator format uses paths like /MoleculeGroup[name='Lipids']/Molecule[name='CE 18:1'].")]
    public static string ImportProperties(
        [Description("CSV text where the first column is ElementLocator (paths identifying document elements) and remaining columns are annotation names with values.")] string csvText)
    {
        return Invoke(connection =>
        {
            return connection.Call("ImportProperties", csvText);
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
                return connection.Call("SetSelectedElement", elementLocator);
            return connection.Call("SetSelectedElement", elementLocator, additionalLocators);
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
            return connection.Call("SetReplicate", replicateName);
        });
    }

    [McpServerTool(Name = "skyline_get_document_status"),
     Description("Get a lightweight overview of the current Skyline document including document type (proteomic, small_molecules, mixed), target counts (proteins/lists, peptides/molecules, precursors, transitions), replicate count, and file path. Much faster than running a report for basic document info.")]
    public static string GetDocumentStatus()
    {
        return Invoke(connection =>
        {
            string result = connection.Call("GetDocumentStatus");
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
            string result = connection.Call("GetOpenForms");
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
                ? connection.Call("GetGraphData", graphId)
                : connection.Call("GetGraphData", graphId, filePath);
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
                ? connection.Call("GetGraphImage", graphId)
                : connection.Call("GetGraphImage", graphId, filePath);
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
                ? connection.Call("GetFormImage", formId)
                : connection.Call("GetFormImage", formId, filePath);
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
            string result = connection.Call("GetDocumentSettings", filePath);
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
            string result = connection.Call("GetDefaultSettings", filePath);
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
            string result = connection.Call("GetAvailableTutorials");
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
            string result = connection.Call("GetTutorial", name, language, filePath);
            if (string.IsNullOrEmpty(result))
                return $"Tutorial not found: {name}";

            // Parse the JSON to give a friendly summary
            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;
            string savedPath = root.GetProperty("file_path").GetString();
            string title = root.GetProperty("title").GetString();
            int lineCount = root.GetProperty("line_count").GetInt32();

            var sb = new StringBuilder();
            sb.AppendLine($"Tutorial: {title}");
            sb.AppendLine($"Lines: {lineCount}");
            sb.AppendLine($"File: {savedPath}");
            sb.AppendLine();
            sb.AppendLine("Table of Contents:");
            foreach (var entry in root.GetProperty("toc").EnumerateArray())
            {
                int level = entry.GetProperty("level").GetInt32();
                string heading = entry.GetProperty("heading").GetString();
                int line = entry.GetProperty("line").GetInt32();
                string indent = level > 1 ? "  " : "";
                sb.AppendLine($"{indent}- {heading} (line {line})");
            }
            sb.AppendLine();
            sb.AppendLine("Use the Read tool to read sections from the file.");
            return sb.ToString();
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
            string result = connection.Call("GetTutorialImage", name, imageFilename, language, filePath);
            if (string.IsNullOrEmpty(result))
                return $"Image not found: {imageFilename} in tutorial {name}";

            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;
            string savedPath = root.GetProperty("file_path").GetString();

            return $"Image downloaded: {savedPath}\n\nUse the Read tool to view this image.";
        });
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
        try
        {
            var (connection, error) = SkylineConnection.TryConnect();
            if (connection == null)
                return error;

            using (connection)
            {
                return action(connection);
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

            return ErrorDetailLevel == ErrorDetail.Full
                ? $"Error: {ex}"
                : $"Error: {ex.Message}";
        }
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
