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
        ActionResult SelectSettingsListItems(string listType, string[] itemNames);

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
        ActionResult ImportFasta(string textFasta, string keepEmptyProteins = null);

        /// <summary>
        /// Imports annotation properties from CSV text where the first column
        /// contains ElementLocator paths.
        /// </summary>
        ActionResult ImportProperties(string csvText);

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
        /// Returns the connector's modal-nesting count: the number of fire-and-forget UI actions (a click, a value
        /// set posted by a verb such as <see cref="ClickFormButton"/> or <see cref="SetFormValue"/>) that have been
        /// posted but have not yet finished. An action that opens a modal dialog stays counted until that modal
        /// closes, so this is usually equal to the number of modal dialogs those actions have raised and left open.
        /// Poll it to wait until pending sets/clicks have actually been applied.
        /// </summary>
        int ModalNestingCount();

        /// <summary>
        /// Returns information about all open forms, panels, and dialogs.
        /// </summary>
        FormInfo[] GetOpenForms();

        /// <summary>
        /// Lists the interactive controls on a form so a caller can discover what is there -- and how to
        /// address it -- without reading the source. Each control reports its Name (informational), Type,
        /// the visible Label that names it, current Value, enabled/visible state, and the actions it
        /// supports. Match a control by its Label, or -- when it has none -- by its Type.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        ControlInfo[] GetControls(string formId);

        /// <summary>
        /// The most general way to interact with a control, menu item, or list item: locate it by the
        /// <paramref name="path"/> (only the set properties are used -- see <see cref="UiElementPath"/>),
        /// then perform <paramref name="action"/> on it. The action determines the type expected for
        /// <paramref name="value"/> and the type returned. Every control supports "get_actions" (returns
        /// <c>ActionInfo[]</c> -- each action's name, a description, and the value it takes) and
        /// "get_children" (returns <c>ControlInfo[]</c>);
        /// other actions are "click" (returns null), "set_value" (takes a value -- a bool, double, or
        /// string -- and returns null), and "get_value" (returns the control's current value, which is
        /// null or one of those same three types). The typed verbs (e.g.
        /// <see cref="ClickFormButton"/>) remain for the common cases.
        ///
        /// <para>Why a general method exists at all: <paramref name="action"/> is a NAME, not a method, so a new
        /// action costs a new UiAction in Skyline and nothing else. It is reachable over the wire the moment
        /// Skyline ships it -- no new interface method, no new MCP tool, no rebuilt and reinstalled MCP server --
        /// and a client discovers it at run time by asking the control "get_actions", which reports what it
        /// supports and what each action takes. A TYPED verb, by contrast, has to be added here, in
        /// SkylineJsonToolClient, in SkylineConnection and in SkylineTools, and the MCP shipped again. That is
        /// what the <c>UiAction</c> indirection buys: the action set can grow on Skyline's release cadence
        /// instead of the MCP's.</para>
        /// </summary>
        object PerformAction(UiElementPath path, string action, object value);

        /// <summary>
        /// Clicks an item on the MAIN Skyline window's menu bar by its visible path, e.g.
        /// "File > Import > Peptide Search". Each segment is matched against a menu item's text (mnemonic
        /// '&amp;' and trailing ellipsis ignored), case-insensitively. Waits out the click and reports in the
        /// <see cref="ActionResult"/> whether it completed or left a dialog open (whose text is in
        /// <see cref="ActionResult.Message"/>) for the caller to drive next.
        ///
        /// <para>The main menu is the one menu that needs no form id, so it has its own method. EVERY other menu --
        /// a form's toolbar, a grid's or a graph's right-click menu -- is reached by
        /// <see cref="ClickControlMenuItem"/>.</para>
        /// </summary>
        /// <param name="menuPath">Menu path; segments separated by '>' (also '|' or '/').</param>
        ActionResult ClickMainMenuItem(string menuPath);

        /// <summary>
        /// Clicks a control on an open form, matching <paramref name="button"/> against the control's
        /// name or visible text: a Button, a CheckBox or RadioButton, a custom IButtonControl (e.g. a
        /// StartPage tile), a ToolStrip / menu / toolbar item, or any other control. For a native
        /// dialog this accepts the dialog, or cancels it when <paramref name="button"/> names the
        /// cancel/close action. Waits out the click and reports in the <see cref="ActionResult"/>
        /// whether it completed or left a dialog open (whose text is in <see cref="ActionResult.Message"/>).
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="button">Control name or visible label.</param>
        ActionResult ClickFormButton(string formId, string button);

        /// <summary>
        /// Clicks an item on a menu belonging to a form, or to a control on it, by its '>'-separated path, e.g.
        /// "Reports > Replicates". WHICH menu is meant follows from <paramref name="control"/>:
        /// <list type="bullet">
        /// <item>EMPTY -- the form's own menu: its menu bar, else its first toolbar, else its right-click menu.</item>
        /// <item>a TOOLSTRIP (a toolbar, a grid's nav bar) -- an item on that strip.</item>
        /// <item>any OTHER control (a grid, a tree, a graph) -- an item on that control's RIGHT-CLICK menu, which is
        /// the only menu such a control has. For a grid, move to the target cell first with
        /// <see cref="SetCurrentCellAddress"/>.</item>
        /// </list>
        /// Each level's dropdown is opened as the path is walked, so items built on demand (the Document Grid's
        /// report list) are present before the next segment is matched.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="control">The menu-owning control's visible label, or its type when it has no label; empty
        /// for the form's own menu.</param>
        /// <param name="menuPath">Menu path; segments separated by '>' (also '|' or '/').</param>
        ActionResult ClickControlMenuItem(string formId, string control, string menuPath);

        /// <summary>
        /// Sets the value of a control on an open form. For a native file dialog the value is the
        /// file name(s) to open -- use <c>"a" "b"</c> quoting to select several -- and
        /// <paramref name="controlId"/> is ignored. For a WinForms form it sets the text, checked
        /// state, or selected item of the control named <paramref name="controlId"/> (a matched label
        /// sets the field it labels). <paramref name="controlId"/> may also be a grid cell locator
        /// "grid[column,row]" (grid name optional) to set that cell.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">Control name, a grid cell locator "grid[column,row]", or ignored for a native file dialog.</param>
        /// <param name="value">Text, "true"/"false", or item text, per control kind.</param>
        ActionResult SetFormValue(string formId, string controlId, string value);

        /// <summary>
        /// Returns the current value of a control on a form, found by its visible label: a text box's
        /// text, a combo box's selected item, a check/radio's checked state, or a CheckedListBox's checked
        /// items (their text, one per line).
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">The control's visible label, or null when the form has a single valued control.</param>
        string GetFormValue(string formId, string controlId);

        /// <summary>
        /// Returns all the choices a list control on a form offers -- a combo box, a list box, or a checked
        /// list box -- as their visible text, regardless of which are currently selected or checked. Unlike
        /// <see cref="GetFormValue"/> (which reports the current selection / checked items), this lists every
        /// available option, so a caller can see what there is to pick or check.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">The control's visible label or name, or null when the form has a single list control.</param>
        string[] GetOptions(string formId, string controlId);

        /// <summary>
        /// Pastes tab-separated <paramref name="text"/> into a grid on a form, starting at its current
        /// cell -- move there first with <see cref="SetCurrentCellAddress"/>. The text may be a multi-cell TSV
        /// block (it fills down and to the right). Works for the Document Grid (and other
        /// DataboundGridControl grids) and for a plain DataGridView.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">Grid control name, or null when the form has a single grid.</param>
        /// <param name="text">Tab-separated (and newline-separated) values to paste at the current cell.</param>
        ActionResult SetGridText(string formId, string controlId, string text);

        /// <summary>
        /// Moves the current cell of a grid on a form (move there before pasting with
        /// <see cref="SetGridText"/> or opening the cell's context menu). <paramref name="column"/> is the
        /// visible-column index and <paramref name="row"/> is the row index -- the same indices the grid
        /// reports columns and rows in.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">Grid control name, or null when the form has a single grid.</param>
        /// <param name="column">The target visible-column index.</param>
        /// <param name="row">The target row index.</param>
        ActionResult SetCurrentCellAddress(string formId, string controlId, int column, int row);

        /// <summary>
        /// Returns all the text in a grid on a form -- the column headers followed by every data row --
        /// as tab-separated columns and newline-separated rows. Works for the Document Grid (and other
        /// DataboundGridControl grids) and for a plain DataGridView.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="gridId">Grid control name, or null when the form has a single grid.</param>
        string GetGridText(string formId, string gridId);

        /// <summary>
        /// Dismisses an open dialog by clicking the button with the given caption, then waits until it has closed --
        /// e.g. "No" on a "replace it?" message box, for a choice that is neither the default nor the cancel button. A
        /// native file dialog has no caption-addressable button, so this throws for one; accept it with
        /// <see cref="DismissWithAcceptButton"/>. Same <see cref="ActionResult"/> semantics as that method.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="button">The visible caption of the button to click.</param>
        ActionResult DismissWithButton(string formId, string button);

        /// <summary>
        /// Accepts (confirms) an open dialog -- presses its default button, the equivalent of pressing Enter,
        /// without keying on a localized "OK" caption -- then waits until the dialog has closed and any work the
        /// accept resumes has finished. The <see cref="ActionResult.Completed"/> flag is true only when the
        /// connector knew which action opened the dialog and that action has finished; false (with a note in
        /// <see cref="ActionResult.Message"/>) when it cannot confirm that.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        ActionResult DismissWithAcceptButton(string formId);

        /// <summary>
        /// Cancels (dismisses) an open dialog -- presses its cancel button, or closes it when it has none --
        /// then waits until the dialog has closed. The dismissing counterpart of <see cref="DismissWithAcceptButton"/>,
        /// with the same <see cref="ActionResult"/> semantics.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        ActionResult DismissWithCancelButton(string formId);

        /// <summary>
        /// Exports graph data to a TSV file. Returns the file path.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/> (e.g. "GraphSummary:Title").</param>
        /// <param name="filePath">Output file path, or null for auto-generated temp path.</param>
        string GetGraphData(string formId, string filePath = null);

        /// <summary>
        /// Exports a graph as a PNG image. Returns the file path.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="filePath">Output file path, or null for auto-generated temp path.</param>
        string GetGraphImage(string formId, string filePath = null);

        /// <summary>
        /// Renders a graph as a PNG and returns the bytes inline together with a
        /// server-suggested file path the caller may write to as a fallback when
        /// the inline payload is too large. The file is NOT written by this call.
        /// Companion to <see cref="GetGraphImage"/> (file-based).
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        ImageBytesMetadata GetGraphImageBytes(string formId);

        /// <summary>
        /// Returns the region of DATA coordinates the graph is currently zoomed to --
        /// the X and Y axis ranges of the first (or only) pane -- as a
        /// <see cref="Rectangle"/>. The returned edges can be handed straight back to
        /// <see cref="ZoomGraphTo"/>, and they tell a caller what coordinate ranges are
        /// valid to pass to <see cref="ClickGraph"/> (whose <see cref="Rectangle.Bottom"/>
        /// edge is the X-axis line -- coordinates below it fall below the axis).
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/> (e.g. "GraphSummary:Title").</param>
        Rectangle GetGraphZoom(string formId);

        /// <summary>
        /// Zooms the graph's first (or only) pane so its axes span the DATA coordinates in
        /// <paramref name="bounds"/> (<see cref="Rectangle.Left"/>/<see cref="Rectangle.Right"/>
        /// set the X range, <see cref="Rectangle.Top"/>/<see cref="Rectangle.Bottom"/> the Y
        /// range). Returns the zoom actually applied, which may differ from the request when
        /// the graph clamps it to the available data range.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="bounds">The DATA-coordinate region to zoom to.</param>
        Rectangle ZoomGraphTo(string formId, Rectangle bounds);

        /// <summary>
        /// Clicks or drags on the graph in DATA coordinates, reproducing a real mouse
        /// gesture: the mouse goes down at the <see cref="Rectangle.Left"/>/<see cref="Rectangle.Top"/>
        /// corner of <paramref name="bounds"/> and is released at the
        /// <see cref="Rectangle.Right"/>/<see cref="Rectangle.Bottom"/> corner. A zero-size
        /// rectangle is a single click (e.g. to select a data point); a rectangle whose Y
        /// values fall below the X-axis drags a chromatogram peak boundary, exactly as the
        /// same gesture would if performed by hand. Operates on the first (or only) pane.
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="bounds">The DATA-coordinate gesture: down at Left/Top, up at Right/Bottom.</param>
        ActionResult ClickGraph(string formId, Rectangle bounds);

        /// <summary>
        /// Types text into one control on a form, whether or not it has the focus. Named for what it does: it
        /// delivers the CHARACTERS to that control's own window, it does not simulate key presses -- so the
        /// caller never has to arrange focus first, and the control is verified enabled first.
        ///
        /// <para>The text is literal -- no key names and nothing to escape. To press a key, use
        /// <see cref="SendKeyStroke"/>; to paste, use the "paste" action, which takes the text to paste and so
        /// needs neither the clipboard nor a keystroke.</para>
        ///
        /// <para>NOT for the Targets tree: <c>SequenceTree.OnKeyPress</c> forwards each character on with
        /// <c>SendKeys.Send</c>, which posts to the FOCUSED window -- so the characters land in whatever
        /// application is in front, arrive out of order, and leave the tree stuck editing a label.</para>
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">The control to type into, matched as <see cref="GetControls"/> reports it
        /// (its visible label, or its Type for a caption-less control).</param>
        /// <param name="text">The text to type, taken literally.</param>
        ActionResult SendText(string formId, string controlId, string text);

        /// <summary>
        /// Presses one key on a control, whether or not it has the focus -- e.g. to accept or step through the
        /// auto-completion popup <see cref="SendKeys"/> raises. The keystroke is atomic (there is no way to
        /// leave a key down), and the control is verified enabled first.
        ///
        /// <para>This raises the control's KeyDown with the named key and modifiers, which is where a WinForms
        /// handler reads a keystroke from. A key handled by the control's DEFAULT behavior rather than by a
        /// handler -- Backspace editing a text box, an arrow moving a plain list's selection -- will NOT take
        /// effect through this.</para>
        /// </summary>
        /// <param name="formId">Form identifier from <see cref="GetOpenForms"/>.</param>
        /// <param name="controlId">The control to press the key on, matched as <see cref="GetControls"/>
        /// reports it.</param>
        /// <param name="keyStroke">The key with any modifiers, '+'-separated and in any order, e.g.
        /// <c>"Down"</c>, <c>"Enter"</c>, <c>"Ctrl+V"</c>, <c>"Ctrl+Shift+Home"</c>.</param>
        ActionResult SendKeyStroke(string formId, string controlId, string keyStroke);

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
