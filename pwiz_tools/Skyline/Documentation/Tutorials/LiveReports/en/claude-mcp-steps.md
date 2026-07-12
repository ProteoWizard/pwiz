# Live Reports tutorial — MCP / AI Connector steps (Claude)

**Work in progress — through the "Peptide Areas" custom report, Pivot Replicate Name, and the Normalized Area column.**

These are the actual `skyline_*` MCP tool calls Claude (via Claude Code) used to drive the
[Live Reports tutorial](https://skyline.ms/tutorials/LiveReports.zip) end to end through the Skyline AI
Connector, in order, with notes on friction worth simplifying in the API (the friction list at the bottom is
the point of this file). This is the **Claude** transcript; a separate file will capture the same run driven
by other models (e.g. Gemini) so the two can be compared.

`{DATA}` = the LiveReports folder containing `Rat_plasma.sky` and `SampleInfo.txt`.

### Tools used

`skyline_invoke_menu_item`, `skyline_get_open_forms`, `skyline_get_controls`, `skyline_click_form_button`,
`skyline_set_form_value`, `skyline_perform_action` (`accept` / `check_item` / `select_tab` / `get_value` /
`get_children` / `click` / `set_value`), `skyline_click_toolstrip_item`, `skyline_set_current_cell_address`,
`skyline_get_grid_text`, `skyline_close_form`.

### How controls are addressed

- Controls are matched by the **visible text** a user sees, never by an internal control name.
- A caption-less field is addressed by its **adjacent label** (`"Name"` → the name textbox).
- A caption-less, label-less control is addressed by its **type** (`"TabControl"`, `"CheckedListBox"`).
- A form's single grid takes an **empty** `gridId` — the literal `"null"` is *not* special; pass an empty string.

---

## Get into the main window

A fresh launch shows the StartPage; the main window and menus do not exist yet.

```
skyline_click_form_button    "StartPage:Start Page" "Blank Document"
```

## Open the starting document (native Open dialog)

The native Windows Open dialog (`FileDialog:Open`, `IsNative=True`) ignores `controlId`; the value is the path.
Accept it with the **`accept` action** (not `click_form_button "Open"`): a native dialog has no
caption-addressable buttons — `accept` hits its default button, `close_form` cancels it (locale-proof; see
friction A).

```
skyline_invoke_menu_item     "File > Open"
skyline_set_form_value       "FileDialog:Open" "filename" "{DATA}\Rat_plasma.sky"
skyline_perform_action       form="FileDialog:Open" action="dismiss"
# Document: 48 proteins, 125 peptides, 721 transitions, 42 replicates.
```

## Enable Audit Logging

"Enable audit logging" lives on the grid's nav bar inside the `DataboundGridControl`; `click_form_button`
searches descendants and finds it. It is a checkable item (no `get_value`).

```
skyline_invoke_menu_item     "View > Live Reports > Audit Log"
skyline_click_form_button    "AuditLogForm:Audit Log: All Info" "Enable audit logging"
skyline_close_form           "AuditLogForm:Audit Log: All Info"
```

## Document Grid + Reports

The Document Grid opens on the last-used view; the form id is `"Document Grid: <ViewName>"`. The Reports
dropdown items are built on demand, so `click_toolstrip_item` opens the dropdown first. Read the whole grid as
TSV with an empty `gridId` for the single grid (see friction F).

```
skyline_invoke_menu_item     "View > Live Reports > Document Grid"
skyline_click_toolstrip_item "DocumentGridForm:Document Grid: Proteins" "Reports > Replicates"
skyline_get_grid_text        "DocumentGridForm:Document Grid: Replicates" ""
```

## Define Replicate Annotations (Cohort value list, SubjectID text)

`"Add"` is ambiguous (one per tab); the interactable-preference picks the visible/enabled one on the active
tab. The control *name* (e.g. `btnAddAnnotation`) is never matched — only visible text (see friction E).

```
skyline_invoke_menu_item     "Settings > Document Settings"
skyline_click_form_button    "DocumentSettingsDlg:Document Settings" "Add"
skyline_set_form_value       "DefineAnnotationDlg:Define Annotation" "Name" "Cohort"
skyline_set_form_value       "DefineAnnotationDlg:Define Annotation" "Type" "Value List"
skyline_set_form_value       "DefineAnnotationDlg:Define Annotation" "Values" "Healthy\nDiseased"   # \n -> CRLF
skyline_perform_action       form="DefineAnnotationDlg:Define Annotation" action="check_item" label="Applies to" value="Replicates"
skyline_click_form_button    "DefineAnnotationDlg:Define Annotation" "OK"
# Repeat Add for SubjectID (Type defaults to "Text"): Name=SubjectID, check Applies-to "Replicates", OK.
skyline_click_form_button    "DocumentSettingsDlg:Document Settings" "OK"
```

> **Re-run note:** a graceful `File > Exit` persists annotation *definitions* to global settings, so on a later
> run "Add Cohort" errors with "already defined" — instead **check** the existing one (see friction H):
> `skyline_perform_action form="DocumentSettingsDlg:Document Settings" action="check_item" type="CheckedListBox" value="Cohort"`

## Result File Rules (auto-fill Cohort/SubjectID from the result file names)

Select the "Result Files" tab on the dialog's caption-less `TabControl` (addressed by type). The `"..."` (Edit)
toolstrip button only opens the Rule Editor when a rules-grid row is **current** — set the current cell first,
then click `"..."` (see friction D; the `"..."` label only resolves after fix B).

```
skyline_invoke_menu_item         "Settings > Document Settings"
skyline_perform_action           form="DocumentSettingsDlg:Document Settings" action="select_tab" type="TabControl" value="Result Files"
skyline_click_form_button        "DocumentSettingsDlg:Document Settings" "Add"
skyline_set_current_cell_address "MetadataRuleSetEditor:Rule Set Editor" "dataGridViewRules" 0 0
skyline_click_form_button        "MetadataRuleSetEditor:Rule Set Editor" "..."

# Rule 1: D -> Diseased -> Cohort. (Pattern field label is "Pattern (regular expression)" -- needed fix C.)
skyline_set_form_value           "MetadataRuleEditor:Rule Editor" "Pattern (regular expression)" "D"
skyline_set_form_value           "MetadataRuleEditor:Rule Editor" "Replacement" "Diseased"
skyline_set_form_value           "MetadataRuleEditor:Rule Editor" "Target" "Cohort"
skyline_get_grid_text            "MetadataRuleEditor:Rule Editor" ""    # Preview: the 21 D_ files match -> Diseased
skyline_click_form_button        "MetadataRuleEditor:Rule Editor" "OK"

# Rule 2 (set cell row 1, "..."): H -> Healthy -> Cohort.
# Rule 3 (set cell row 2, "..."): Pattern "(.)_(...)" -> Replacement "$1$2" -> Target "SubjectID"
#         (regex groups -> SubjectID D102..H162). Verified in the Preview.
skyline_set_form_value           "MetadataRuleSetEditor:Rule Set Editor" "Name" "Cohort and SubjectID"
skyline_click_form_button        "MetadataRuleSetEditor:Rule Set Editor" "OK"
skyline_click_form_button        "DocumentSettingsDlg:Document Settings" "OK"
# Result: Cohort=Diseased/Healthy and SubjectID=D102..H162 fill in for all 42 replicates.
```

## Creating a list of samples (List Designer + paste SampleInfo.txt)

Fill the property grid (`dataGridViewProperties`), pasting the 4 property names down column 0. End the paste
with a **trailing blank row** (a line of just tabs) so the last property ("Name") is committed — see finding I:
a row commits when the paste enters the next row, and the ID/Display dropdowns only list committed properties.

```
skyline_invoke_menu_item         "Settings > Document Settings"
skyline_perform_action           form="DocumentSettingsDlg:Document Settings" action="select_tab" type="TabControl" value="Lists"
skyline_click_form_button        "DocumentSettingsDlg:Document Settings" "Add"
skyline_set_form_value           "ListDesigner:List Designer" "List name" "Samples"
skyline_set_current_cell_address "ListDesigner:List Designer" "dataGridViewProperties" 0 0
skyline_set_grid_text            "ListDesigner:List Designer" "dataGridViewProperties" "SubjectID\nSex\nWeight\nName\n\t\t\t"

# Weight is a Number; set the Property Type cell (a combo cell -- the handler sets it through the dropdown).
skyline_set_current_cell_address "ListDesigner:List Designer" "dataGridViewProperties" 1 2
skyline_set_grid_text            "ListDesigner:List Designer" "dataGridViewProperties" "Number"
skyline_set_form_value           "ListDesigner:List Designer" "ID property" "SubjectID"
skyline_set_form_value           "ListDesigner:List Designer" "Display property" "Name"   # only after Name is committed
skyline_click_form_button        "ListDesigner:List Designer" "Save"
skyline_click_form_button        "DocumentSettingsDlg:Document Settings" "OK"

# Show the list grid and paste the 14 sample rows (again with a trailing blank row so row 14 commits).
skyline_invoke_menu_item         "View > Live Reports > Lists > Samples"
skyline_set_current_cell_address "ListGridForm:List: Samples" "" 0 0
skyline_set_grid_text            "ListGridForm:List: Samples" "" "D102\tM\t190\tDrizzle\n...(14 rows)...\nH162\tM\t235\tScamper\n\t\t\t"
```

> **Re-run note:** the list *definition* persists to global settings (re-check "Samples" on the Lists tab),
> but its *data* rows live in the document and must be re-pasted.

## Changing annotation type (SubjectID → Lookup: Samples)

Populate the list **first** (above), then change the type, so the lookup resolves. (Changing the type while
the list is empty leaves the column showing the key.) The annotation-list editor is a generic `EditListDlg`,
whose form id carries a backtick: `` EditListDlg`2 ``.

```
skyline_invoke_menu_item     "Settings > Document Settings"
skyline_click_form_button    "DocumentSettingsDlg:Document Settings" "Edit List"          # the Annotations tab's Edit List
skyline_perform_action       form="EditListDlg`2:Define Annotations" action="select_item" label="Annotations" value="SubjectID"
skyline_click_form_button    "EditListDlg`2:Define Annotations" "Edit"
skyline_set_form_value       "DefineAnnotationDlg:Define Annotation" "Type" "Lookup: Samples"
skyline_click_form_button    "DefineAnnotationDlg:Define Annotation" "OK"
skyline_click_form_button    "EditListDlg`2:Define Annotations" "OK"
skyline_click_form_button    "DocumentSettingsDlg:Document Settings" "OK"
# Result: the SubjectID column in "Document Grid: Replicates" now shows the rat NAMES (Drizzle, Sniffles, ...)
# instead of D102/D103/...  get_grid_text returns the looked-up display value, not the key.
```

## Build the "Peptide Areas" custom report (the ViewEditor)

This is the most involved form. The available-fields tree, the chosen-columns list, and the reorder buttons all
live inside a `ChooseColumnsTab` UserControl, so they are reached by walking into it with `get_children` and
**re-parenting** the (parentless) paths it returns. The Remove / Up / Down buttons are image-only
`ToolStripButton`s addressed by their **tooltip** text.

Start the report from the built-in **Peptides** report and open the editor. (For a built-in view the Reports
menu item is "Customize Report…"; for a custom view it becomes "Edit Report…".)

```
skyline_click_toolstrip_item "DocumentGridForm:Document Grid: Replicates" "Reports > Peptides"
skyline_click_toolstrip_item "DocumentGridForm:Document Grid: Peptides"   "Reports > Customize Report"
```

Discover the controls inside the tab — `get_children` returns paths whose parent is null, which you re-parent
under the element you listed them from:

```
skyline_perform_action  form="ViewEditor:Customize Report" action="get_children" type="ChooseColumnsTab"
#   -> AvailableFieldsTree, ListView (the chosen columns), ToolStrip (the Remove/Up/Down buttons)
skyline_perform_action  form="ViewEditor:Customize Report" action="get_children" \
    path='{"parent": {"parent": {"parent": null, "text": "ViewEditor:Customize Report", "type": "Form"}, "type": "ChooseColumnsTab"}, "type": "ToolStrip"}'
#   -> ToolStripButton "Remove", "Up", "Down"  (image-only; addressed by their tooltip)
```

> Below, `{…X}` abbreviates the full re-parented `path` JSON for control *X*, built the same way as the two
> shown in full above.

The Peptides report starts with 10 columns. Trim to the first four (Peptide, Protein, Peptide Modified
Sequence, Standard Type) by selecting **index 4** in the chosen-columns ListView and clicking **Remove**. After
each Remove the selection auto-advances to index 4, so it is: set index 4 once, then click Remove six times.

```
skyline_perform_action  form="ViewEditor:Customize Report" action="set_selected_index" value="4" \
    path='{"parent": {"parent": {"parent": null, "text": "ViewEditor:Customize Report", "type": "Form"}, "type": "ChooseColumnsTab"}, "type": "ListView"}'
skyline_perform_action  form="ViewEditor:Customize Report" action="click" \
    path='{"parent": {"parent": {"parent": {"parent": null, "text": "ViewEditor:Customize Report", "type": "Form"}, "type": "ChooseColumnsTab"}, "type": "ToolStrip"}, "text": "Remove", "type": "ToolStripButton"}'
# ... click "Remove" five more times (the selection stays on index 4) ...
```

Add **Total Area** from the available-fields tree by checking it. `check_item` takes the `>`-separated node
path and **auto-expands** the lazily-built intermediate nodes:

```
skyline_perform_action  form="ViewEditor:Customize Report" action="check_item" \
    value="Proteins > Peptides > Precursors > Precursor Results > Total Area"  path='{…AvailableFieldsTree}'
```

Total Area inserts ahead of the selected "Standard Type", so click **Up** once to put Standard Type back in
front of it; then select Total Area (now index 4) and add the **Replicates** sublist after it:

```
skyline_perform_action  form="ViewEditor:Customize Report" action="click"             path='{…the "Up" ToolStripButton}'
skyline_perform_action  form="ViewEditor:Customize Report" action="set_selected_index" value="4"  path='{…the ListView}'
skyline_perform_action  form="ViewEditor:Customize Report" action="check_item" value="Replicates"  path='{…the AvailableFieldsTree}'
```

Name it, turn on **Pivot Replicate Name**, and accept:

```
skyline_set_form_value    "ViewEditor:Customize Report" "Report Name" "Peptide Areas"
skyline_set_form_value    "ViewEditor:Customize Report" "Pivot Replicate Name" "true"
skyline_click_form_button "ViewEditor:Customize Report" "OK"
# Result: "Document Grid: Peptide Areas" with Total Area spread into one column per replicate
#         (D_102_REP1 Total Area, D_102_REP2 Total Area, ...) across all 125 peptides.
```

## Add the Normalized Area column (finding a buried column)

Reopen the editor — it is a custom view now, so the menu item is **"Edit Report…"**. "Normalized Area" is *not*
under Precursor Results (the obvious guess); a wrong `check_item` path throws and pops Skyline's "Unexpected
Error" dialog (friction K), dismissed with "Don't Report". Use the editor's **Find Column** tool to locate it:

```
skyline_click_toolstrip_item "DocumentGridForm:Document Grid: Peptide Areas" "Reports > Edit Report"
skyline_perform_action    form="ViewEditor:Customize Report" action="click" path='{…the form ToolStrip "Find Column" button}'
skyline_set_form_value    "FindColumnDlg:Find Column" "Find what" "Normalized Area"
skyline_click_form_button "FindColumnDlg:Find Column" "Find Next"
skyline_click_form_button "FindColumnDlg:Find Column" "Close"
# Find highlights it in the tree: Proteins > Peptides > Peptide Results > Quantification > Normalized Area
```

Check it at the discovered path and accept:

```
skyline_perform_action    form="ViewEditor:Customize Report" action="check_item" \
    value="Proteins > Peptides > Peptide Results > Quantification > Normalized Area"  path='{…AvailableFieldsTree}'
skyline_click_form_button "ViewEditor:Customize Report" "OK"
# Result: each replicate now shows both "<replicate> Total Area" and "<replicate> Normalized Area".
```

---

## Friction / API-simplification opportunities (observed this run)

- **A. Native-dialog accept.** The intuitive first guess was `click_form_button "Open"`; the answer is the
  `accept` action. Consider surfacing `accept` in the typed tools, or accepting `"Open"`/`"Save"`/`"OK"` on a
  native dialog as aliases that map to the default button (still locale-proof).
- **B. [Fixed this session] Caption-less `"..."` Edit button** reported an empty label and was unmatchable.
  `NormalizeLabel` now keeps the original when trimming empties it, so `"..."` is addressable.
- **C. [Fixed this session] Labels with symbols** (e.g. "Pattern (regular expression)"): `get_controls`
  reported the *normalized* label but matching used the *raw* one, so the discovered label was unmatchable (and
  the parens disabled the loose match). `TextMatches` now also matches the normalized label.
- **D. The `"..."` Edit button silently no-ops** unless a rules-grid row is current. Consider auto-selecting the
  new row, or returning a clear "no current row" error instead of doing nothing.
- **E. `"Add"` ambiguity across tabs** works only because off-tab buttons report `Visible=false`. Names are
  never matched. A way to scope by tab, or a clearer error when ambiguous, would help.
- **F. `gridId` / single-grid forms:** pass an empty string; the literal `"null"` is taken as a grid name and
  fails. Easy to get wrong — consider treating `"null"`/`"none"` as the single grid too.
- **G. [Fixed this session] Reading a validation/alert message.** When OK is blocked by a `CommonAlertDlg`, the
  text lived in a plain Label (not an addressable control), so it took a screenshot to read. The form gate now
  includes the blocking dialog's message in its exception.
- **H. Global-settings leak** (affects repeatable scripting): a graceful `File > Exit` persists annotation
  *definitions* and list *definitions* to global settings, so re-runs must **check** them rather than **add**;
  list *data*, however, lives in the document and must be re-pasted.
- **I. [Fixed this session] Pasting into a plain (unbound) DataGridView** — the List Designer property grid, the
  Samples list grid — used a manual cell-edit loop that could not commit a new row or grow the grid (a direct
  `cell.Value` set is not pushed through the new row's `AddNew`), so a multi-row paste crashed. `DataGridViewPasteHandler`
  was split into a base (DataGridView only — sets each cell through the editing control, the way a user's
  keystrokes do) plus `BoundDataGridViewPasteHandler` (the document undo/batch-modify); `GridElement` uses the
  base, and the List Designer attaches it so users get Ctrl-V there too. A grid that lazily commits its new row
  still needs a trailing blank paste row so the last real row commits.
- **J. List Designer ID/Display-property dropdowns** are populated from the *committed* property rows, so
  setting "Display property" right after pasting fails ("Name" is still the new row) and silently does not
  persist. Work around with the trailing-blank-row paste (finding I) before setting the dropdown.
- **K. A fire-and-forget error surfaces as Skyline's "Unexpected Error" dialog** rather than a clean connector
  error: e.g. `set_value` on a combo whose item is not (yet) present, the old multi-row grid paste, or (seen
  again this run) a `check_item` with a wrong tree path. *Partly addressed:* the connector now reads a blocking
  dialog's text — a `CommonAlertDlg`'s or `ReportErrorDlg`'s message, or any other dialog's title — through the
  form gate, so the next call reports what is in the way. The deeper fix (validate the action synchronously so
  it never pops the dialog) is still open.
- **L. Addressing a control inside a UserControl is verbose.** The ViewEditor's tree, chosen-columns list, and
  Remove/Up/Down buttons live in a `ChooseColumnsTab`, so each action needs a hand-built, deeply nested `path`
  JSON because `get_children` returns *parentless* paths to re-parent. A `get_children` that returns absolute
  paths, or addressing by a dotted type chain (`ChooseColumnsTab/ListView`), would remove most of the boilerplate.
- **M. A wanted column is not always where you would guess** in the field tree (Normalized Area is under
  *Peptide Results > Quantification*, not Precursor Results). The editor's **Find Column** dialog locates it,
  but the connector cannot read which node Find selected — the path was read from a screenshot. A `get_value`
  on the tree (its selected node's path), or a "check the found node" action, would close that loop.

## Remaining (not yet captured here)

Number format (Total Area in scientific notation) and the Normalized-Area sub-properties; column averages;
row/column-header pivots (the Pivot Editor); changing the normalization method + a report filter; Group
Comparison + heat map with dendrograms; adding report definitions; inspecting the audit log; and exporting a
report.
