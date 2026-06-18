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
// ReSharper disable InvalidXmlDocComment (for direct link into .NET 8.0)

namespace SkylineTool
{
    /// <summary>
    /// Contract for the JSON tool service hosted in Skyline. Provides typed access
    /// to document data, navigation, settings, reports, and commands.
    ///
    /// <para>For .NET Framework 4.7.2 tools, use <see cref="SkylineJsonToolClient"/>
    /// which implements this interface as a JSON-RPC 2.0 named pipe client.</para>
    ///
    /// <para>For .NET 8.0+ tools, link-compile IJsonToolService.cs, JsonToolModels.cs,
    /// JsonToolConstants.cs, and SkylineJsonToolClient.cs into your project.</para>
    ///
    /// <para>See also: <see cref="SkylineToolClient"/> for the legacy BinaryFormatter
    /// transport (deprecated).</para>
    /// </summary>
    public interface IJsonToolService
    {
        // --- Skyline process info ---

        /// <summary>
        /// Returns the Skyline version string including git hash (e.g. "26.1.1.238-6c3244bc0a").
        /// </summary>
        string GetVersion();

        /// <summary>
        /// Returns the Skyline process ID as a string.
        /// </summary>
        string GetProcessId();

        // --- Document info ---

        /// <summary>
        /// Returns the file path of the currently open document, or null if unsaved.
        /// </summary>
        string GetDocumentPath();

        /// <summary>
        /// Returns a lightweight overview of the document: type, target counts,
        /// replicate count, file path, and unsaved changes flag.
        /// </summary>
        DocumentStatus GetDocumentStatus();

        // --- Selection and navigation ---

        /// <summary>
        /// Returns the ElementLocators of the currently selected elements.
        /// </summary>
        SelectionInfo GetSelection();

        /// <summary>
        /// Returns a human-readable display text for the current selection in the Targets tree.
        /// </summary>
        string GetSelectionText();

        /// <summary>
        /// Returns the ElementLocator of the selected element matching the given type,
        /// or null if no element of that type is selected.
        /// </summary>
        /// <param name="elementType">Element type: "Molecule", "Precursor", "Transition", etc.</param>
        string GetSelectedElementLocator(string elementType);

        /// <summary>
        /// Navigates to a document element by its ElementLocator string.
        /// </summary>
        /// <param name="elementLocator">Primary ElementLocator to select.</param>
        /// <param name="additionalLocators">Optional newline-separated locators for multi-selection.</param>
        void SetSelectedElement(string elementLocator, string additionalLocators = null);

        /// <summary>
        /// Enumerates document tree elements at a specified depth, optionally scoped
        /// to a parent element.
        /// </summary>
        /// <param name="level">Tree level: "group", "molecule", "precursor", or "transition".</param>
        /// <param name="rootLocator">Optional ElementLocator to scope enumeration to a subtree.</param>
        LocationEntry[] GetLocations(string level, string rootLocator = null);

        // --- Replicates ---

        /// <summary>
        /// Returns the name of the currently active replicate, or empty if none.
        /// </summary>
        string GetReplicateName();

        /// <summary>
        /// Returns the names of all replicates in the document.
        /// </summary>
        string[] GetReplicateNames();

        /// <summary>
        /// Sets the active replicate by name.
        /// </summary>
        void SetReplicate(string replicateName);

        // --- Reports ---

        /// <summary>
        /// Exports a named report to a file and returns metadata including row count,
        /// column headers, and a preview of the first rows.
        /// </summary>
        /// <param name="reportName">Name of a report defined in Skyline (e.g. "Peak Area").</param>
        /// <param name="filePath">Output file path. Extension determines format (.csv, .tsv).</param>
        /// <param name="culture">"invariant" for consistent decimal separators, or "localized".</param>
        ReportMetadata ExportReport(string reportName, string filePath, string culture);

        /// <summary>
        /// Exports a custom report from a definition and returns metadata.
        /// </summary>
        /// <param name="definition">Report definition with column selection, filters, and sorting.</param>
        /// <param name="filePath">Output file path.</param>
        /// <param name="culture">"invariant" or "localized".</param>
        ReportMetadata ExportReportFromDefinition(ReportDefinition definition, string filePath, string culture);

        /// <summary>
        /// Runs a named report and returns a windowed slice of rows inline (no file).
        /// The response is capped server-side to protect caller context size.
        /// Pass <paramref name="count"/> = 0 for shape-only introspection (total row count
        /// plus column names and types, with an empty rows array).
        /// </summary>
        /// <param name="reportName">Predefined / saved report name.</param>
        /// <param name="offset">0-based row index of the first row to return.</param>
        /// <param name="count">Number of rows to return; 0 returns shape only.</param>
        /// <param name="columns">Optional projection: subset of the report's columns.</param>
        /// <param name="filter">Optional additional filters applied to the named report.</param>
        /// <param name="includeMaxLength">When true, scans string columns and reports
        /// <see cref="ReportRowsColumn.MaxObservedLength"/>.</param>
        /// <param name="culture">"invariant" or "localized".</param>
        ReportRowsResult GetReportRows(string reportName, int offset, int count,
            string[] columns, ReportFilter[] filter, bool includeMaxLength, string culture);

        /// <summary>
        /// Runs a custom report from a definition and returns a windowed slice of rows
        /// inline (no file). The response is capped server-side. The definition already
        /// supports projection, filtering, sorting, and pivots, so this method does NOT
        /// duplicate those parameters.
        /// </summary>
        /// <param name="definition">Report definition (same shape as
        /// <see cref="ExportReportFromDefinition"/> accepts).</param>
        /// <param name="offset">0-based row index of the first row to return.</param>
        /// <param name="count">Number of rows to return; 0 returns shape only.</param>
        /// <param name="includeMaxLength">When true, scans string columns and reports
        /// <see cref="ReportRowsColumn.MaxObservedLength"/>.</param>
        /// <param name="culture">"invariant" or "localized".</param>
        ReportRowsResult GetReportFromDefinitionRows(ReportDefinition definition,
            int offset, int count, bool includeMaxLength, string culture);

        /// <summary>
        /// Saves a report definition to the user's Skyline settings. The definition
        /// must include a <see cref="ReportDefinition.Name"/>.
        /// </summary>
        void AddReportFromDefinition(ReportDefinition definition);

        /// <summary>
        /// Returns available report column documentation topics with column counts.
        /// </summary>
        /// <param name="dataSource">Optional data source: "document_grid" (default),
        /// "audit_log", "group_comparisons", or "candidate_peaks".</param>
        ReportDocTopicSummary[] GetReportDocTopics(string dataSource = null);

        /// <summary>
        /// Returns detailed column documentation for a specific report topic.
        /// </summary>
        /// <param name="topicName">Topic display name (case-insensitive, partial match supported).</param>
        /// <param name="dataSource">Optional data source filter.</param>
        ReportDocTopicDetail GetReportDocTopic(string topicName, string dataSource = null);

        // --- Settings lists ---

        /// <summary>
        /// Returns the names of all settings list types (e.g. "Enzymes", "Reports").
        /// </summary>
        string[] GetSettingsListTypes();

        /// <summary>
        /// Returns item names from a settings list.
        /// </summary>
        /// <param name="listType">Settings list name (e.g. "Enzymes", "Reports").</param>
        /// <param name="groupName">For Reports only: "main" or "external_tools" to filter by group.</param>
        string[] GetSettingsListNames(string listType, string groupName = null);

        /// <summary>
        /// Returns the XML definition of a single settings list item.
        /// </summary>
        string GetSettingsListItem(string listType, string itemName);

        /// <summary>
        /// Adds a settings item from its XML definition. Throws if the item
        /// already exists unless <paramref name="overwrite"/> is true.
        /// </summary>
        void AddSettingsListItem(string listType, string itemXml, bool overwrite = false);

        /// <summary>
        /// Returns the names of items from a settings list that are currently
        /// active in the document (e.g. selected enzyme, enabled modifications).
        /// </summary>
        string[] GetSettingsListSelectedItems(string listType);

        /// <summary>
        /// Sets which items from a settings list are active in the document.
        /// </summary>
        void SelectSettingsListItems(string listType, string[] itemNames);

        // --- Document modification ---

        /// <summary>
        /// Inserts a small molecule transition list from CSV text with column headers.
        /// </summary>
        void InsertSmallMoleculeTransitionList(string textCsv);

        /// <summary>
        /// Imports protein sequences in FASTA format into the document.
        /// </summary>
        /// <param name="textFasta">FASTA-formatted protein sequences.</param>
        /// <param name="keepEmptyProteins">"true" to keep proteins with no matching peptides.</param>
        void ImportFasta(string textFasta, string keepEmptyProteins = null);

        /// <summary>
        /// Imports annotation properties from CSV text where the first column
        /// contains ElementLocator paths.
        /// </summary>
        void ImportProperties(string csvText);

        // --- Commands ---

        /// <summary>
        /// Runs command-line arguments against the Skyline instance (same syntax as
        /// SkylineCmd). Output is echoed to Skyline's Immediate Window.
        /// </summary>
        /// <param name="args">Pre-parsed command-line arguments.</param>
        /// <returns>Command output text.</returns>
        string RunCommand(string[] args);

        /// <summary>
        /// Runs command-line arguments silently (no Immediate Window output).
        /// </summary>
        /// <param name="args">Pre-parsed command-line arguments.</param>
        /// <returns>Command output text.</returns>
        string RunCommandSilent(string[] args);

        // --- Settings export ---

        /// <summary>
        /// Exports the current document's settings as XML to a file.
        /// </summary>
        /// <param name="filePath">Output file path.</param>
        /// <returns>The file path written to.</returns>
        string GetDocumentSettings(string filePath);

        /// <summary>
        /// Exports Skyline's default settings as XML to a file (baseline for comparison).
        /// </summary>
        /// <param name="filePath">Output file path.</param>
        /// <returns>The file path written to.</returns>
        string GetDefaultSettings(string filePath);
        /// <summary>
        /// Changes the order of nodes in the Targets tree and Replicates based on the
        /// order of <paramref name="elementLocators"/>. Elements which are not included in
        /// elementLocators will be moved to the end of their parent's list of children.
        /// </summary>
        void ReorderElements(string[] elementLocators);

        // --- UI mode ---

        /// <summary>
        /// Returns the current UI mode: "proteomic", "small_molecules", or "mixed".
        /// </summary>
        string GetUiMode();

        /// <summary>
        /// Sets the UI mode.
        /// </summary>
        /// <param name="mode">"proteomic", "small_molecules", or "mixed".</param>
        void SetUiMode(string mode);

        // --- Undo/redo ---

        /// <summary>
        /// Returns the full undo/redo stack with descriptions and indices.
        /// Negative indices = undo steps, positive = redo steps.
        /// </summary>
        UndoRedoEntry[] GetUndoRedo();

        /// <summary>
        /// Navigates to a specific point in the undo/redo stack by index.
        /// Negative indices undo, positive indices redo.
        /// </summary>
        /// <param name="index">Target position in the undo/redo stack.</param>
        void SetUndoRedoPosition(int index);

        // --- UI state ---

        /// <summary>
        /// Returns information about all open forms, panels, and dialogs.
        /// </summary>
        FormInfo[] GetOpenForms();

        /// <summary>
        /// Invokes a main-menu item by its visible path, e.g. "File > Import > Peptide Search".
        /// Each segment is matched against a menu item's text (mnemonic '&amp;' and trailing
        /// ellipsis ignored) or its control name, case-insensitively. The click is posted
        /// asynchronously, so an item that opens a modal dialog returns immediately; poll
        /// <see cref="GetOpenForms"/> for the resulting form.
        /// </summary>
        /// <param name="menuPath">Menu path; segments separated by '>' (also '|' or '/').</param>
        void InvokeMenuItem(string menuPath);

        /// <summary>
        /// Invokes an item on a graph form's right-click context menu by its visible path, e.g.
        /// "Normalize To > None". The menu is built the way a right-click would build it, then the
        /// item is matched by text (mnemonic '&amp;' and trailing ellipsis ignored) or control name,
        /// the same way <see cref="InvokeMenuItem"/> matches the main menu.
        /// </summary>
        /// <param name="formId">Graph form identifier from <see cref="GetOpenForms"/> (HasGraph=true).</param>
        /// <param name="menuPath">Menu path; segments separated by '>' (also '|' or '/').</param>
        void InvokeContextMenuItem(string formId, string menuPath);

        /// <summary>
        /// Clicks a control on an open form, matching <paramref name="button"/> against the control's
        /// name or visible text: a Button, a CheckBox or RadioButton, a custom IButtonControl (e.g. a
        /// StartPage tile), a ToolStrip / menu / toolbar item, or any other control. For a native
        /// dialog this accepts the dialog, or cancels it when <paramref name="button"/> names the
        /// cancel/close action. The click is posted asynchronously when it may open a modal dialog.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="button">Control name or visible label.</param>
        void ClickFormButton(string formId, string button);

        /// <summary>
        /// Clicks an item on a form's ToolStrip (toolbar / menu strip) by its path, e.g.
        /// "Reports &gt; Replicates". Each level's dropdown is opened first so items built on demand
        /// (not in the static menu, e.g. the Document Grid's report list) are present before matching.
        /// Each segment is matched by item name or visible text, like <see cref="InvokeMenuItem"/>.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="menuPath">Toolbar/menu path; segments separated by '>' (also '|' or '/').</param>
        void ClickToolStripItem(string formId, string menuPath);

        /// <summary>
        /// Sets the value of a control on an open form. For a native file dialog the value is the
        /// file name(s) to open -- use <c>"a" "b"</c> quoting to select several -- and
        /// <paramref name="controlId"/> is ignored. For a WinForms form it sets the text, checked
        /// state, or selected item of the control named <paramref name="controlId"/>.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">Control name (WinForms); ignored for a native file dialog.</param>
        /// <param name="value">Text, "true"/"false", or item text, per control kind.</param>
        void SetFormValue(string formId, string controlId, string value);

        /// <summary>
        /// Pastes tab-separated <paramref name="text"/> into a grid on a form, starting at the given
        /// anchor cell, the way typing/pasting there would. Works for the Document Grid (and other
        /// DataboundGridControl grids) and for a plain DataGridView. <paramref name="column"/> and
        /// <paramref name="row"/> are zero-based indices into the grid's visible columns and its rows.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">Grid control name, or null when the form has a single grid.</param>
        /// <param name="column">Zero-based anchor column (index into visible columns).</param>
        /// <param name="row">Zero-based anchor row.</param>
        /// <param name="text">Tab-separated (and newline-separated) values to paste.</param>
        void SetGridText(string formId, string controlId, int column, int row, string text);

        /// <summary>
        /// Returns all the text in a grid on a form -- the column headers followed by every data row --
        /// as tab-separated columns and newline-separated rows. Works for the Document Grid (and other
        /// DataboundGridControl grids) and for a plain DataGridView.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="gridId">Grid control name, or null when the form has a single grid.</param>
        string GetGridText(string formId, string gridId);

        /// <summary>
        /// Closes an open form: a dialog, a docked or floating tool window (e.g. the Document Grid or
        /// Audit Log), or a native dialog (which is cancelled).
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        void CloseForm(string formId);

        /// <summary>
        /// Checks or unchecks an item in a CheckedListBox or a TreeView on a form. For a CheckedListBox
        /// <paramref name="item"/> is matched by its display text; for a TreeView it is a '&gt;'-separated
        /// path of node texts, e.g. "Peptides &gt; Precursors &gt; Precursor Results".
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">List/tree control name, or null when the form has a single one.</param>
        /// <param name="item">Item display text, or a '&gt;'-separated node path for a TreeView.</param>
        /// <param name="isChecked">True to check the item, false to uncheck it.</param>
        void SetItemChecked(string formId, string controlId, string item, bool isChecked);

        /// <summary>
        /// Selects or deselects an item in a ListBox/CheckedListBox or a TreeView on a form. For a list
        /// <paramref name="item"/> is matched by its display text; for a TreeView it is a '&gt;'-separated
        /// path of node texts.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">List/tree control name, or null when the form has a single one.</param>
        /// <param name="item">Item display text, or a '&gt;'-separated node path for a TreeView.</param>
        /// <param name="selected">True to select the item, false to deselect it.</param>
        void SetItemSelected(string formId, string controlId, string item, bool selected);

        /// <summary>
        /// Exports graph data to a TSV file. Returns the file path.
        /// </summary>
        /// <param name="graphId">Form identifier from <see cref="GetOpenForms"/> (e.g. "GraphSummary:Title").</param>
        /// <param name="filePath">Output file path, or null for auto-generated temp path.</param>
        string GetGraphData(string graphId, string filePath = null);

        /// <summary>
        /// Exports a graph as a PNG image. Returns the file path.
        /// </summary>
        /// <param name="graphId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="filePath">Output file path, or null for auto-generated temp path.</param>
        string GetGraphImage(string graphId, string filePath = null);

        /// <summary>
        /// Renders a graph as a PNG and returns the bytes inline together with a
        /// server-suggested file path the caller may write to as a fallback when
        /// the inline payload is too large. The file is NOT written by this call.
        /// Companion to <see cref="GetGraphImage"/> (file-based).
        /// </summary>
        /// <param name="graphId">Form identifier from <see cref="GetOpenForms"/>.</param>
        ImageBytesMetadata GetGraphImageBytes(string graphId);

        /// <summary>
        /// Captures a screenshot of any open form as a PNG image. Returns the file path.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="filePath">Output file path, or null for auto-generated temp path.</param>
        string GetFormImage(string formId, string filePath = null);

        /// <summary>
        /// Captures a form screenshot as a PNG and returns the bytes inline together
        /// with a server-suggested file path the caller may write to as a fallback.
        /// The file is NOT written by this call. Companion to <see cref="GetFormImage"/>
        /// (file-based). Permission denial is signaled as an inline error from the caller.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        ImageBytesMetadata GetFormImageBytes(string formId);

        // --- Tutorials ---

        /// <summary>
        /// Returns the catalog of available Skyline tutorials.
        /// </summary>
        TutorialListItem[] GetAvailableTutorials();

        /// <summary>
        /// Fetches a tutorial's content as markdown, saves to a file, and returns
        /// metadata including title, table of contents, and file path.
        /// </summary>
        /// <param name="name">Tutorial folder name (e.g. "MethodEdit", "DIA-TTOF").</param>
        /// <param name="language">Language code: "en" (default), "ja", "zh-CHS".</param>
        /// <param name="filePath">Output file path, or null for auto-generated temp path.</param>
        TutorialMetadata GetTutorial(string name, string language = "en", string filePath = null);

        /// <summary>
        /// Fetches a tutorial image, saves to a file, and returns the file path.
        /// </summary>
        /// <param name="name">Tutorial folder name.</param>
        /// <param name="imageFilename">Image filename from the tutorial markdown (e.g. "s-01.png").</param>
        /// <param name="language">Language code.</param>
        /// <param name="filePath">Output file path, or null for auto-generated temp path.</param>
        TutorialImageMetadata GetTutorialImage(string name, string imageFilename, string language = "en", string filePath = null);

        /// <summary>
        /// Fetches a tutorial image and returns the bytes inline together with a
        /// server-suggested file path the caller may write to as a fallback when
        /// the inline payload is too large. The file is NOT written by this call.
        /// Companion to <see cref="GetTutorialImage"/> (file-based).
        /// </summary>
        /// <param name="name">Tutorial folder name.</param>
        /// <param name="imageFilename">Image filename from the tutorial markdown.</param>
        /// <param name="language">Language code.</param>
        ImageBytesMetadata GetTutorialImageBytes(string name, string imageFilename, string language = "en");
    }
}
