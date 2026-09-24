# Targeted Method Editing, driven through the Skyline MCP

Every step of the **Targeted Method Editing** tutorial (`Tutorials/MethodEdit/en/index.html`), with the MCP
calls that performed it and a screenshot of the result. Driven live on 2026-09-24 against the Release x64
build of branch `Skyline/work/20260921_typing_in_sequence_tree`, built from the working tree committed as
`c679ab986e` (the version string reads 26.1.1.266 `1914685d0e`), from the Start Page through to the
exported SCIEX transition lists.

- **Data:** a fresh extraction of `MethodEdit.zip` to `D:\Downloads\Tutorials\MethodEdit_20260924\MethodEdit`
- **Outcome:** 36 proteins, 71 peptides, 71 precursors, **355 transitions** (the tutorial's count), saved as
  `MethodEditTutorial.sky` and exported as `Yeast_list_0001..0005.csv` (75 + 75 + 75 + 75 + 55 = 355 rows).
  Every intermediate count the tutorial implies matched.
- **Screenshots:** `images/s-NN.png` correspond to the tutorial's own `s-NN.png`; `images/NN-*.png` are
  extra captures of steps the tutorial describes but does not picture.

## How to read the calls

Calls are written `tool(arg=value)` with the `skyline_` prefix dropped. A few conventions recur:

- **A verb that opens a dialog says it "did not complete" and names the form it left open.** That is how a
  dialog's id is discovered, not an error.
- **A control with no visible label is addressed by its type** (`TabControl`, `SequenceTree`,
  `CheckedListBox`, `ListView`, `TextBox`); a labelled one by its label. `get_controls(formId)` lists both.
- **A native file dialog takes the whole path as its value** (`controlId` ignored) and is committed with
  `dismiss_with_accept_button`.
- **Copying a file in Notepad** is done by putting the file on the real clipboard
  (`Get-Content -Raw <file> | Set-Clipboard`), then using Skyline's own paste, just as a reader does.
- **Anything asynchronous is polled.** The completion pop-up, a tooltip and a background settings change
  all take effect a moment after the call returns; the next read (`get_open_forms`,
  `get_document_status`) is how the caller finds out. That is deliberate: a reader has to watch for the
  same things.

## What did not work, and what stood in for it

| Tutorial step | What happened | Stand-in used here |
|---|---|---|
| Resize/arrange the main window to the tutorial's layout | No verb sizes a window on this build | None: captures are at the default 736x546, so tree labels and the spectrum pane are clipped (s-04, s-13) |
| Switch to the Proteomics interface | Worked, but the three tool strips on the main window have no labels; `get_controls` lists them as `MenuStrip`, `StatusStrip`, `ToolStrip`, and `type="ToolStrip"` matches the menu bar first | `path` with `"index": 2` |
| "Press the down arrow key" / `Home` on the Targets tree | `send_key_stroke` raises the tree's `KeyDown` only, so keys TreeView handles natively (arrows, Home) do nothing (#4671 item 4) | `set_selection` with an element locator, or `select_item` on the tree (a click) |
| View > Libraries > Ion Types > B | "Menu item not found": the submenu is empty until the ion types in Transition Settings change (#4671 item 3). After the change in section 5 it was populated, with B already on | None; s-05 has no purple b-ions |
| Graph right-click menu (same Ion Types choice) | "msGraphExtension has no context menu" | None |
| Address the Build Library grid by its label "Input Files" | "No control matching 'Input Files'" although `get_controls` prints that label (the error also names the wrong action, `set_grid_text`, for `get_grid_text`) | `type="BuildLibraryGridView"`; cell locator `[2,0]` for Score Threshold |
| "Peptides" count box beside Limit peptides per protein | The trailing label is not the box's label on this build | `controlId="TextBox"` (the only one on the tab) |
| Resize the Insert grid's columns by dragging header dividers | No verb | None; s-10 at default widths |
| Delete key on the Targets tree | No effect (same `KeyDown`-only limit) | `click_main_menu_item("Edit > Delete")` |
| s-16: "Press the Enter key" to accept YBL087C | Enter with no row selected accepts the **typed** text: it created an empty list named `ybl087` | Undo, retype, `Down` then `Enter`. The tutorial test selects row 0 itself before its screenshot, so it never exercises a bare Enter |
| Hover a node, click its drop-arrow | No hover-and-click verb | `Space` on the selected node opens the same pick-list |
| Drag and drop proteins | No drag verb for the tree | Skipped; the tutorial only asks the reader to try it, and it does not change the document |
| File Explorer and spreadsheet views of the output | Outside Skyline | Row counts and first lines read from the files |

Items 3 and 4 were fixed on the abandoned `Skyline/work/20260916_mcp_methodedit_gaps` branch, as were the
window verbs, grid labels and trailing labels; none of that is on this branch.

---

## 1. Getting started

```
get_form_image(formId="StartPage:Start Page")
click_form_button(formId="StartPage:Start Page", button="Blank Document")
```

![Start page](images/00-start-page.png)

The first `get_form_image` of a session opens a screen-capture consent dialog in Skyline, which a person
has to answer once.

```
click_main_menu_item(menuPath="Settings > Default")
  -> did not complete; left 'MultiButtonMsgDlg:Skyline' open ("save your current settings?")
dismiss_with_button(formId="MultiButtonMsgDlg:Skyline", button="No")
```

The user interface control in the upper right is a drop-down button on the third tool strip:

```
perform_action(form="SkylineWindow:Skyline", action="click",
  path={"parent":{"parent":{"parent":{"text":"SkylineWindow:Skyline","type":"Form"},
                            "type":"ToolStrip","index":2},
                  "text":"User interface selection","type":"ToolStripDropDownButton"},
        "text":"Proteomics interface","type":"ToolStripButton"})
get_ui_mode()   -> proteomic
```

## 2. Creating a MS/MS spectral library

```
click_main_menu_item(menuPath="Settings > Peptide Settings")
perform_action(form="PeptideSettingsUI:Peptide Settings", action="select_tab", type="TabControl", value="Library")
perform_action(form=..., action="get_options", label="Libraries")   -> []   (no libraries left over)
click_form_button(formId="PeptideSettingsUI:Peptide Settings", button="Build")
set_form_value(formId="BuildLibraryDlg:Build Library", controlId="Name", value="Yeast (Atlas)")
click_form_button(formId="BuildLibraryDlg:Build Library", button="Browse")   -> 'Dialog:Save As'
set_form_value(formId="Dialog:Save As", controlId="", value="...\MethodEdit\Library\Yeast (Atlas).blib")
dismiss_with_accept_button(formId="Dialog:Save As")
```

![Build Library, first page](images/01-build-library-page1.png)

```
click_form_button(formId="BuildLibraryDlg:Build Library", button="Next")
click_form_button(formId="BuildLibraryDlg:Build Library", button="Add Files")   -> 'Dialog:Add Input Files'
set_form_value(formId="Dialog:Add Input Files", controlId="", value="\"...\Yeast_atlas\interact-prob.pep.xml\"")
dismiss_with_accept_button(formId="Dialog:Add Input Files")
perform_action(form="BuildLibraryDlg:Build Library", action="get_grid_text", type="BuildLibraryGridView")
  -> interact-prob.pep.xml  PeptideProphet confidence  0.95
set_form_value(formId="BuildLibraryDlg:Build Library", controlId="[2,0]", value="0.95")
```

"In the Score Threshold field, enter 0.95" is a cell of the input-file grid. The default is already 0.95; it
is set anyway, by column index, because the grid could not be named by its label (see the table above).
Finish was disabled until the file had been read; a second `get_controls` showed it enabled.

![Build Library input files](images/02-build-library-input-files.png)

```
click_form_button(formId="BuildLibraryDlg:Build Library", button="Finish")
get_open_forms()   # polled until 'BuildLibraryDlg:Build Library' was gone
perform_action(form="PeptideSettingsUI:Peptide Settings", action="check_item", label="Libraries", value="Yeast (Atlas)")
```

**s-01**

![s-01](images/s-01.png)

## 3. Creating a background proteome

```
perform_action(form="PeptideSettingsUI:Peptide Settings", action="select_tab", type="TabControl", value="Digestion")
perform_action(form=..., action="get_options", label="Background proteome")
  -> ["None","<Add...>","<Edit current...>","<Edit list...>"]
set_form_value(formId="PeptideSettingsUI:Peptide Settings", controlId="Background proteome", value="<Add...>")
  -> left 'BuildBackgroundProteomeDlg:Edit Background Proteome' open
click_form_button(formId="BuildBackgroundProteomeDlg:Edit Background Proteome", button="Create")
set_form_value(formId="Dialog:Create Background Proteome", controlId="", value="...\MethodEdit\FASTA\Yeast")
dismiss_with_accept_button(formId="Dialog:Create Background Proteome")
click_form_button(formId="BuildBackgroundProteomeDlg:Edit Background Proteome", button="Add File")
set_form_value(formId="Dialog:Add FASTA File", controlId="", value="\"...\MethodEdit\FASTA\sgd_yeast.fasta\"")
dismiss_with_accept_button(formId="Dialog:Add FASTA File")
  -> did not complete; left 'MessageDlg:Skyline' open, quoting its text
```

The accept reports the message box it raised, with its text, so the reason is known without a capture. The
tutorial does not mention this box.

![61 repeated sequences](images/03-repeated-sequences.png)

```
dismiss_with_accept_button(formId="MessageDlg:Skyline")
```

**s-02**: 5801 proteins.

![s-02](images/s-02.png)

```
dismiss_with_accept_button(formId="BuildBackgroundProteomeDlg:Edit Background Proteome")
```

**s-03**

![s-03](images/s-03.png)

```
dismiss_with_accept_button(formId="PeptideSettingsUI:Peptide Settings")
```

## 4. Pasting FASTA sequences

```powershell
Get-Content -Raw '...\FASTA\Fasta.txt' | Set-Clipboard
```

```
click_main_menu_item(menuPath="Edit > Paste")
  -> did not complete; left 'EmptyProteinsDlg:Skyline' open
```

![Empty proteins prompt](images/04-empty-proteins.png)

The tutorial does not mention this prompt either; **Keep** gives the 35 proteins it expects.

```
dismiss_with_button(formId="EmptyProteinsDlg:Skyline", button="Keep")
get_document_status()   -> 35 proteins, 25 peptides, 25 precursors, 75 transitions
```

"Press the down arrow key until the first pasted peptide is selected": `send_key_stroke` with `Home` and
`Up` on the tree left the selection on the blank element (see the table), so the peptide was selected
directly:

```
get_locations(level="molecule")   -> first: Molecule:/YAL005C/VDIIANDQGNR
set_selection(elementLocator="Molecule:/YAL005C/VDIIANDQGNR")
```

**s-04**: status bar `4/35 prot  1/25 pep  1/25 prec  1/75 tran`.

![s-04](images/s-04.png)

`View > Libraries > Ion Types > B` failed here with "Menu item not found" (#4671 item 3), and the spectrum
graph's context menu is not reachable. Expanding the peptide and precursor ("click the +") and moving to
rank 1:

```
perform_action(form="SequenceTreeForm:Targets", action="expand", type="SequenceTree", value=["YAL005C", 0])
perform_action(form="SequenceTreeForm:Targets", action="expand", type="SequenceTree", value=["YAL005C", 0, 0])
set_selection(elementLocator="Transition:/YAL005C/VDIIANDQGNR/light++/y7+")
```

**s-05**: y7 (rank 1) in red. Without the b-ion overlay, none are purple.

![s-05](images/s-05.png)

## 5. Transition settings

```
click_main_menu_item(menuPath="Settings > Transition Settings")
perform_action(form="TransitionSettingsUI:Transition Settings", action="select_tab", type="TabControl", value="Filter")
set_form_value(formId="TransitionSettingsUI:Transition Settings", controlId="Precursor charges", value="2, 3")
get_form_value(formId="TransitionSettingsUI:Transition Settings", controlId="Ion charges")   -> 1
set_form_value(formId="TransitionSettingsUI:Transition Settings", controlId="Ion types", value="y, b")
```

**s-06**

![s-06](images/s-06.png)

```
perform_action(form=..., action="select_tab", type="TabControl", value="Library")
set_form_value(formId="TransitionSettingsUI:Transition Settings", controlId="product ions", value="5")
```

**s-07**

![s-07](images/s-07.png)

```
dismiss_with_accept_button(formId="TransitionSettingsUI:Transition Settings")
get_document_status()   -> 35 / 28 / 31 / 155
```

**s-08**: b5 added to VDIIANDQGNR, and a new first peptide in YAL005C.

![s-08](images/s-08.png)

With `b` now in the ion types, the Ion Types submenu is populated:

```
perform_action(form="SkylineWindow:Skyline", action="get_children",
  path=<View > Libraries > Ion Types>)   -> A, B (checked), C, X, Y (checked), Z, Z•, Z′
```

## 6. Using a public spectral library

```
click_main_menu_item(menuPath="Settings > Peptide Settings")
perform_action(form="PeptideSettingsUI:Peptide Settings", action="select_tab", type="TabControl", value="Library")
click_form_button(formId="PeptideSettingsUI:Peptide Settings", button="Edit list")
click_form_button(formId="EditListDlg`2:Edit Libraries", button="Add")
set_form_value(formId="EditLibraryDlg:Edit Library", controlId="Name", value="Yeast (GPM)")
click_form_button(formId="EditLibraryDlg:Edit Library", button="Browse")   -> 'Dialog:Open'
set_form_value(formId="Dialog:Open", controlId="", value="\"...\MethodEdit\Library\yeast_cmp_20.hlf\"")
dismiss_with_accept_button(formId="Dialog:Open")
dismiss_with_accept_button(formId="EditLibraryDlg:Edit Library")
dismiss_with_accept_button(formId="EditListDlg`2:Edit Libraries")
perform_action(form="PeptideSettingsUI:Peptide Settings", action="check_item", label="Libraries", value="Yeast (GPM)")
```

**s-09**

![s-09](images/s-09.png)

```
dismiss_with_accept_button(formId="PeptideSettingsUI:Peptide Settings")
get_document_status()   -> 35 / 28 / 31 / 155   (library still loading)
get_document_status()   -> 35 / 182 / 219 / 1058
```

## 7. Limiting peptides per protein

```
click_main_menu_item(menuPath="Settings > Peptide Settings")
perform_action(form="PeptideSettingsUI:Peptide Settings", action="uncheck_item", label="Libraries", value="Yeast (Atlas)")
set_form_value(formId=..., controlId="Rank peptides by", value="Expect")
set_form_value(formId=..., controlId="Limit peptides per protein", value="true")
set_form_value(formId=..., controlId="Peptides", value="3")   -> "No control matching 'Peptides'"
set_form_value(formId=..., controlId="TextBox", value="3")
```

![Limit peptides per protein](images/05-limit-peptides-per-protein.png)

```
dismiss_with_accept_button(formId="PeptideSettingsUI:Peptide Settings")
click_main_menu_item(menuPath="Refine > Remove Empty Proteins")
get_document_status()   -> 19 / 182 / 219 / 1058   (stale)
get_document_status()   -> 19 / 47 / 47 / 223
```

## 8. Inserting a protein list

```powershell
Get-Content -Raw '...\FASTA\Protein List.txt' | Set-Clipboard
```

```
set_selection(elementLocator="/Insert")          # the blank element at the end
click_main_menu_item(menuPath="Edit > Insert > Proteins")   -> 'PasteDlg:Insert'
send_key_stroke(formId="PasteDlg:Insert", controlId="DataGridViewEx", keyStroke="Ctrl+V")
perform_action(form="PasteDlg:Insert", action="get_grid_text", type="DataGridViewEx")
  -> 17 rows, Description and Sequence filled from the background proteome
```

A real `Ctrl+V` matters: the grid resolves each ID against the background proteome in its own paste
handler. The column-resizing steps have no verb.

**s-10**: the Accession, Preferred Name, Gene and Species columns, which the tutorial says will be empty,
are now filled.

![s-10](images/s-10.png)

```
click_form_button(formId="PasteDlg:Insert", button="Insert")
click_main_menu_item(menuPath="Refine > Remove Empty Proteins")
get_document_status()   -> 24 / 58 / 58 / 278
```

## 9. Inserting a peptide list

```powershell
Get-Content -Raw '...\FASTA\Peptide List.txt' | Set-Clipboard
```

```
perform_action(form="SequenceTreeForm:Targets", action="select_item", type="SequenceTree", value="YAL003W")
click_main_menu_item(menuPath="Edit > Paste")
get_selection()   -> MoleculeGroup:/peptides1
```

"Type 'Primary Peptides' and press Enter" is done by typing into the tree, as a reader does:

```
send_text(formId="SequenceTreeForm:Targets", controlId="SequenceTree", text="Primary Peptides")
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Enter")
get_locations(level="group")   -> first: Primary Peptides
```

**s-11**: `1/25 prot  1/70 pep  1/70 prec  1/338 tran`.

![s-11](images/s-11.png)

```
click_main_menu_item(menuPath="Edit > Undo")    # twice
click_main_menu_item(menuPath="Edit > Undo")
get_document_status()   -> 24 / 58 / 58 / 278
click_main_menu_item(menuPath="Edit > Insert > Peptides")
send_key_stroke(formId="PasteDlg:Insert", controlId="DataGridViewEx", keyStroke="Ctrl+V")
```

**s-12**: Protein Name and Protein Description resolved for all 12 peptides.

![s-12](images/s-12.png)

```
click_form_button(formId="PasteDlg:Insert", button="Insert")
get_document_status()   -> 35 / 70 / 70 / 338
```

## 10. Simple refinement

```
click_main_menu_item(menuPath="Edit > Find")    # modeless: the verb completes and the form stays open
set_form_value(formId="FindNodeDlg:Find", controlId="Find what", value="IPEE")
click_form_button(formId="FindNodeDlg:Find", button="Find Next")
get_selection()   -> Molecule:/YAL034W-A/IPEEYLDANVFR
get_graph_image(formId="GraphSpectrum:Library Match")
dismiss_with_cancel_button(formId="FindNodeDlg:Find")
```

**s-13**, rendered straight from the graph. The docked pane is only about 180 px wide at the default window
size, so the spectrum is compressed.

![s-13](images/s-13.png)

```
click_main_menu_item(menuPath="Refine > Advanced")
set_form_value(formId="RefineDlg:Refine", controlId="Min transitions per precursor", value="5")
```

![Refine, min transitions](images/06-refine-min-transitions.png)

```
dismiss_with_accept_button(formId="RefineDlg:Refine")
get_document_status()   -> 35 / 64 / 64 / 320
```

**s-14**: 70 peptides reduced to 64, in the status bar.

![s-14](images/s-14.png)

## 11. Checking peptide uniqueness

```
perform_action(form="SequenceTreeForm:Targets", action="select_item", type="SequenceTree", value="YDL245C")
click_main_menu_item(menuPath="Edit > Unique Peptides")   -> 'UniquePeptidesDlg:Unique Peptides'
```

**s-15**: SASWVPPSR is shared with five other hexose transporters.

![s-15](images/s-15.png)

```
dismiss_with_cancel_button(formId="UniquePeptidesDlg:Unique Peptides")
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Delete")   # no effect
click_main_menu_item(menuPath="Edit > Delete")
get_document_status()   -> 34 / 63 / 63 / 315
perform_action(form="SequenceTreeForm:Targets", action="select_item", type="SequenceTree", value="YDL244W")
click_main_menu_item(menuPath="Edit > Unique Peptides")
```

![Unique Peptides for YDL244W](images/07-unique-peptides-ydl244w.png)

```
dismiss_with_cancel_button(formId="UniquePeptidesDlg:Unique Peptides")
```

## 12. Direct document editing: auto-completion

### Protein name

```
set_selection(elementLocator="/Insert")
send_text(formId="SequenceTreeForm:Targets", controlId="SequenceTree", text="ybl087")
get_open_forms()   -> no pop-up yet
get_open_forms()   -> StatementCompletionForm:StatementCompletionForm
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Down")
get_form_image(formId="SkylineWindow:Skyline")
get_form_image(formId="StatementCompletionForm:StatementCompletionForm")
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Enter")
get_locations(level="molecule", rootLocator="MoleculeGroup:/YBL087C")   -> 3 peptides
```

The pop-up is looked up in the background and shows up on a later read, not by the time `send_text`
returns. The first attempt pressed `Enter` straight away, as the tutorial says, and got an empty peptide
list named `ybl087`, the text as typed. It was undone (`Edit > Undo`) and redone with `Down` first.

**s-16**: the main window clips the pop-up at its right edge; the pop-up captured on its own is complete.

![s-16](images/s-16.png)

![s-16 pop-up](images/s-16-popup.png)

### Protein description

```
send_text(formId="SequenceTreeForm:Targets", controlId="SequenceTree", text="eft2")
get_open_forms()   -> no pop-up yet
get_open_forms()   -> StatementCompletionForm:StatementCompletionForm
perform_action(form="StatementCompletionForm:StatementCompletionForm", action="get_options", type="ListView")
  -> 6 matches, YDR385W (EFT2) first
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Down")
```

**s-17**

![s-17](images/s-17.png)

![s-17 pop-up](images/s-17-popup.png)

```
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Enter")
get_document_status()   -> 36 / 69 / 69 / 345
```

### Peptide sequence

The tutorial presses Caps-Lock; `send_text` types the characters as given, so the text is written in
upper case.

```
send_text(formId="SequenceTreeForm:Targets", controlId="SequenceTree", text="IQGP")
get_open_forms()   -> no pop-up yet
perform_action(form="StatementCompletionForm:StatementCompletionForm", action="get_options", type="ListView")
  -> IQGPNYVPGK  YDR385W ...
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Down")
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Enter")
get_locations(level="molecule", rootLocator="MoleculeGroup:/YDR385W")
  -> STAISLYSEMSDEDVK, AEQLYEGPADDANC[+57]IAIK, IQGPNYVPGK, AYLPVNESFGFTGELR
get_document_status()   -> 36 / 70 / 70 / 350
```

**s-18**: IQGPNYVPGK went into the existing YDR385W, as the tutorial says.

![s-18](images/s-18.png)

## 13. Pop-up pick-lists

The tutorial hovers a node until a drop-arrow appears and clicks it. `Space` on the selected node opens the
same pick-list.

```
perform_action(form="SequenceTreeForm:Targets", action="select_item", type="SequenceTree", value="YBL087C")
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Space")
  -> PopupPickList:PopupPickList in get_open_forms
perform_action(form="PopupPickList:PopupPickList", action="get_options", type="CheckedListBox")
  -> the 3 peptides in the document (filtered)
perform_action(form="PopupPickList:PopupPickList", action="click",
  path={"parent":{"parent":{"text":"PopupPickList:PopupPickList","type":"Form"},"type":"ToolStrip"},
        "text":"Filter","type":"ToolStripButton"})              # the funnel
perform_action(form=..., action="check_item", type="CheckedListBox", value="K.VMPAIVVR.Q [73, 80] (rank 6)")
```

**s-19**

![s-19](images/s-19.png)

```
perform_action(form="PopupPickList:PopupPickList", action="click", path=<ToolStrip > "OK">)   # green check
```

Then a precursor's product ions:

```
perform_action(form="SequenceTreeForm:Targets", action="expand", type="SequenceTree", value=["YBL087C", 0])
set_selection(elementLocator="Precursor:/YBL087C/ISLGLPVGAIMNC[+57.021464]ADNSGAR/light+++")
send_key_stroke(formId="SequenceTreeForm:Targets", controlId="SequenceTree", keyStroke="Space")
perform_action(form="PopupPickList:PopupPickList", action="click", path=<ToolStrip > "Filter">)
perform_action(form=..., action="uncheck_item", type="CheckedListBox", value="N [y9] - 964.3901+ (rank 4)")
perform_action(form=..., action="uncheck_item", type="CheckedListBox", value="D [y6] - 619.2794+ (rank 5)")
perform_action(form=..., action="click", path=<ToolStrip > "Find (Ctrl + F)">)     # binoculars
send_text(formId="PopupPickList:PopupPickList", controlId="TextBox", text="b ++")
perform_action(form=..., action="check_item", type="CheckedListBox", value="L [b5] - 242.6601++")
perform_action(form=..., action="check_item", type="CheckedListBox", value="V [b7] - 340.7207++")
```

**s-20**

![s-20](images/s-20.png)

```
perform_action(form="PopupPickList:PopupPickList", action="click", path=<ToolStrip > "OK">)
get_locations(level="transition", rootLocator="Precursor:/YBL087C/ISLGLPVGAIMNC[+57.021464]ADNSGAR/light+++")
  -> b5+, b9+, b10+, b5++, b7++
get_document_status()   -> 36 / 71 / 71 / 355
```

## 14. Bigger picture: node tips

`show_tooltip` rests the mouse on the selected node, as a reader hovering does. Skyline comes to the front
and the tree takes the focus, which the tip needs. The tip is a window of its own, listed as `NodeTip:` about
half a second later, and captured whole.

```
perform_action(form="SequenceTreeForm:Targets", action="select_item", type="SequenceTree", value="YBL087C")
perform_action(form="SequenceTreeForm:Targets", action="show_tooltip", type="SequenceTree")
get_open_forms()   -> no tip yet
get_open_forms()   -> NodeTip:
get_form_image(formId="NodeTip:")
```

**s-21**

![s-21](images/s-21.png)

```
set_selection(elementLocator="Precursor:/YBL087C/ISLGLPVGAIMNC[+57.021464]ADNSGAR/light+++")
perform_action(form="SequenceTreeForm:Targets", action="show_tooltip", type="SequenceTree")
get_open_forms()   -> no tip yet
get_open_forms()   -> NodeTip:
get_form_image(formId="NodeTip:")
```

**s-22**: the two b++ ions just added are among the blue (in-document) entries.

![s-22](images/s-22.png)

## 15. Drag and drop

Skipped: there is no verb for dragging a tree node.

## 16. Preparing to measure

```
click_main_menu_item(menuPath="Settings > Transition Settings")
perform_action(form="TransitionSettingsUI:Transition Settings", action="select_tab", type="TabControl", value="Prediction")
set_form_value(formId=..., controlId="Collision energy", value="SCIEX")
set_form_value(formId=..., controlId="Declustering potential", value="SCIEX")
```

![Prediction tab, SCIEX](images/08-transition-prediction-sciex.png)

```
perform_action(form=..., action="select_tab", type="TabControl", value="Instrument")
set_form_value(formId=..., controlId="Max m/z", value="1800")
dismiss_with_accept_button(formId="TransitionSettingsUI:Transition Settings")
click_main_menu_item(menuPath="File > Save")   -> 'Dialog:Save As'
set_form_value(formId="Dialog:Save As", controlId="", value="...\MethodEdit\MethodEditTutorial.sky")
dismiss_with_accept_button(formId="Dialog:Save As")
get_document_status()   -> 36 / 71 / 71 / 355, no unsaved changes
click_main_menu_item(menuPath="File > Export > Transition List")   -> 'ExportMethodDlg:Export Transition List'
click_form_button(formId="ExportMethodDlg:Export Transition List", button="Multiple methods")
set_form_value(formId=..., controlId="Ignore proteins", value="true")
set_form_value(formId=..., controlId="Max transitions per sample injection", value="75")
get_form_value(formId=..., controlId="Instrument type")   -> SCIEX
```

`Ignore proteins` and `Max transitions per sample injection` are disabled until `Multiple methods` is
chosen, as for a reader.

**s-23**: `Methods: 5`.

![s-23](images/s-23.png)

```
dismiss_with_accept_button(formId="ExportMethodDlg:Export Transition List")   -> 'Dialog:Export Transition List'
set_form_value(formId="Dialog:Export Transition List", controlId="", value="...\MethodEdit\Yeast_list.csv")
dismiss_with_accept_button(formId="Dialog:Export Transition List")
```

The tutorial's last two pictures are of File Explorer and a spreadsheet, outside Skyline. The files:

```
Yeast_list_0001.csv   75 rows
Yeast_list_0002.csv   75 rows
Yeast_list_0003.csv   75 rows
Yeast_list_0004.csv   75 rows
Yeast_list_0005.csv   55 rows   (355 in all)

618.291139,879.405417,20,YIL075C.LDQDSTSENVK.+2y8.light,80,29.3
618.291139,764.378474,20,YIL075C.LDQDSTSENVK.+2y7.light,80,29.3
618.291139,677.346445,20,YIL075C.LDQDSTSENVK.+2y6.light,80,29.3
```

Precursor *m/z*, product *m/z*, dwell time, extended peptide, declustering potential and collision energy,
in the order the tutorial describes. The reference spreadsheet shows DP 76.2 / CE 31 where this gives
80 / 29.3: the SCIEX equations have changed since that screenshot. The *m/z* values match.
