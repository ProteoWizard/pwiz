# Skyline Tutorial Documentation Style Guide

Style conventions and patterns for writing Skyline tutorial HTML documentation.

## HTML Structure

Every tutorial uses this standard template:

```html
<html>

<head>
    <meta charset="utf-8">
    <link rel="stylesheet" type="text/css" href="../../shared/SkylineStyles.css">
    <script src="../../shared/skyline.js" type="text/javascript"></script>
</head>

<body onload="skylineOnload();">
    <h1 class="document-title">Tutorial Title Here</h1>
    <!-- Content -->
</body>

</html>
```

- The `skylineOnload()` function generates the table of contents from `<h1>` headings
- Use `class="document-title"` only on the first `<h1>` (the tutorial title)
- Subsequent `<h1>` tags become TOC entries

## Getting Started Section

Every tutorial should have a "Getting Started" section with:

1. ZIP file download link to skyline.ms/tutorials/
2. Instructions to extract to a user folder (e.g., `C:\Users\{author-user-name}\Documents`)
3. Description of the resulting folder structure

### When Opening an Existing Document

If the tutorial opens an existing Skyline document, you do NOT need:
- "Reset to defaults" instructions
- "Choose Proteomics/Molecule interface" instructions

The document's settings and interface mode are stored in the .sky file and will be applied automatically when opened.

### When Starting a New Document

If the tutorial creates a new document from scratch, include:
- Instructions to click **Blank Document** on the Start Page
- Reset to defaults: **Settings** menu > **Default** > Click **No**
- Interface selection if needed (Proteomics vs Molecule)

## Formatting Conventions

### UI Element Patterns

Use `<b>` tags for all UI elements. Follow these exact patterns:

**Menu items (single level):**
```html
On the <b>File</b> menu, click <b>Open</b>.
```

**Menu items (nested):**
```html
On the <b>View</b> menu, choose <b>Live Reports</b>, and click <b>Document Grid</b>.
```

**Buttons:**
```html
Click the <b>OK</b> button.
```
- Always say "Click the X button" (not "Click on..." or just "Click X")

**Text fields:**
```html
In the <b>Name</b> field, enter "Rat (NIST)".
```

**Dropdown lists:**
```html
In the <b>Background Proteome</b> dropdown list, choose <b>Edit list</b>.
```
- Do not include UI characters like ellipses (`...`) or angle brackets (`<`, `>`)
- The user sees `<Edit list...>` but we write just `Edit list`

**Checkboxes:**
```html
Check the <b>Auto-select</b> checkbox.
```

**Keyboard shortcuts:**
```html
On the <b>View</b> menu, choose <b>Auto-Zoom</b> and click <b>Best Peak</b> (F11).
```
- Include shortcuts in parentheses after the menu path

**Tips:**
```html
<p>
    <b><span class="green">Tip!</span></b> Hover with the cursor over the protein/peptide/precursor/transition
    to get specific information on the respective item.
</p>
```
- Use for helpful hints that aren't required steps
- The `class="green"` renders "Tip!" in green

**Notes (boxed):**
```html
<table>
    <tr>
        <td><b>Note</b>: These data were collected on a Q-Exactive, in which both MS1 and MS2 scans
            were performed using the Orbitrap.</td>
    </tr>
</table>
```
- Use for important information the reader should be aware of
- The single-cell table gets a rectangular border from `SkylineStyles.css` (all tables have borders by default)
- Always bold "Note" followed by a colon

### Bulleted vs Non-Bulleted Text

**Critical rule:** All user actions MUST be bulleted. The user should be able to read only the bulleted text and successfully complete the tutorial.

**Bulleted (`<ul><li>`):** Required user actions
```html
<ul>
    <li>On the <b>File</b> menu, click <b>Open</b>.</li>
    <li>Navigate to the tutorial folder and select "MyDocument.sky".</li>
    <li>Click the <b>Open</b> button.</li>
</ul>
```

**Non-bulleted (`<p>`):** Explanatory text, background information, descriptions
- This is optional reading for the user
- Helps users understand why they are doing something
- Should not contain required actions

### Images/Screenshots

**Naming:** Use sequential numbering: `s-01.png`, `s-02.png`, etc.

**Size:** Screenshots should fit on a 1080p monitor at 100% magnification with taskbar visible.

**Preceding text:** Most screenshots should be preceded by explanatory text like:
```html
<p class="keep-next">
    The Skyline window should now look like this:
</p>
<p>
    <img src="s-01.png" />
</p>
```

The `class="keep-next"` keeps the text and image together when printing to PDF, preventing them from being split across pages.

**Image tag format:**
```html
<p><img src="s-01.png"/></p>
```

**Composite figures:**

For displaying multiple images side by side (typically graphs extracted from the UI):
```html
<table class="comp-fig">
    <tr>
        <td class="comp-fig">
            <img src="s-33.png" />
        </td>
        <td class="comp-fig">
            <img src="s-34.png" />
        </td>
    </tr>
</table>
```

## Writing Style

### Voice and Perspective

Always address the reader as "you" - never use "we":
- **Correct:** "You will see the dialog appear."
- **Correct:** "You need to select the file first."
- **Incorrect:** "We will now open the settings dialog."
- **Incorrect:** "Next, we need to configure the options."

The author is instructing the reader, not accompanying them through the tutorial.

### Spelling and Grammar

- Use American English spelling
- Proofread carefully - common errors include:
  - "confidently" not "confidentally"
  - "identified" not "identifed"

### Technical Accuracy

- Verify all menu paths exist in current Skyline version
- Test all instructions yourself before publishing
- Ensure referenced features work as described

## Required Sections

Every tutorial must include:

### Introduction

The opening paragraphs before "Getting Started" should:
- Explain the scientific or practical context for the tutorial
- Describe what problem or workflow the tutorial addresses
- Set expectations for what the reader will learn

### Conclusion

A closing section that:
- Summarizes what was accomplished in the tutorial
- Highlights key features or concepts learned
- May suggest related tutorials or webinars for further learning

### Bibliography

If the tutorial references citable scientific work, include a Bibliography section:

```html
<h1>Bibliography</h1>
<p class="bibliography">
    1. Author, A. <i>et al</i>. Title of paper. <i>Journal Name</i> <b>volume</b>, pages (year).
</p>
```

## Website Publication Requirements

To publish a tutorial on skyline.ms, the following are needed:

### Abstract Blurb

A short description (2-4 sentences) summarizing what the tutorial covers. This appears on the tutorial's wiki page. Example:

> "This tutorial provides hands-on experience with DIA data using a workflow combining DIA and DDA acquisition modes. It covers defining and exporting DIA isolation schemes, building spectral libraries from DDA data, selecting peptides and transitions for targeted proteins, and importing/analyzing DIA runs in Skyline."

### Version Information

Track which Skyline version the tutorial was written for, and note revisions:
- "Written for Skyline v24.1"
- "Revised for Skyline v25.1"

### Tutorial Page Components

Each tutorial page on skyline.ms includes:
- Title
- Abstract/description blurb
- Version information
- Cover image (`cover.png`)
- Link to PDF version
- Link to HTML version (viewable in browser)
- Link to tutorial data files (ZIP)
- Related webinars (if applicable)

### Cover Image

Each tutorial needs a `cover.png` image that appears on the website. The tutorial test generates this when run in CoverShotMode:

```csharp
CoverShotName = "TutorialName";
```

The test should take a representative screenshot that captures the key visual elements of what the tutorial demonstrates.

## Tutorial Test Patterns

Tutorial tests in `TestTutorial/` automate screenshot capture and verify the tutorial workflow.

### Screenshot Capture Methods

**Full window/form:**
```csharp
PauseForScreenShot(SkylineWindow);
PauseForScreenShot(editDialog);
PauseForScreenShot<EditGroupComparisonDlg>();  // By type
```

**Graph only (no window border UI):**
```csharp
PauseForGraphScreenShot();  // Generic graph
PauseForRetentionTimeGraphScreenShot();  // RT-specific graphs
```

Prefer graph-only screenshots when the surrounding UI isn't relevant to what you're demonstrating. This is especially appropriate for:
- Graphs that are the focus of the text discussion
- Sequences of screenshots showing the same graph changing
- Composite figures where space is limited

### Screenshot Descriptions

Add descriptions to `PauseForScreenShot()` calls for maintainability:
```csharp
PauseForScreenShot("Volcano plot showing fold change distribution");
```

This helps when paused at a screenshot during test maintenance - you can read what should be captured and search for that text in the code.

### Screenshot Annotations

`AbstractFunctionalTest` provides utilities for annotating screenshots with:
- Red (or other color) boxes
- Ellipses
- Arrows

### Cover Shot

Each tutorial test should capture a cover image for the website:
```csharp
CoverShotName = "TutorialName";
// ... at an appropriate point in the test:
TakeCoverShot();
```

### Review Guidelines for LLMs

When reviewing tutorial screenshots, consider:
1. **Graph-only vs full window:** If text discusses only the graph, consider using `PauseForGraphScreenShot()` instead of capturing the entire window
2. **Repetitive elements:** If the same UI element (like a grid) appears beside a graph in multiple screenshots but the text focuses on the graph, consider showing the grid once then switching to graph-only
3. **Match text to image:** Ensure what's described in the tutorial text matches what's visible in the screenshot

## File Organization

Tutorial files are located in:
```
pwiz_tools/Skyline/Documentation/Tutorials/{TutorialName}/{language}/
```

Where `{language}` is one of:
- `en` - English
- `ja` - Japanese
- `zh-CHS` - Chinese (Simplified)

Each tutorial folder contains:
- `index.html` - The tutorial content
- `s-01.png`, `s-02.png`, etc. - Screenshots
- Any other supporting images

Shared resources are in:
```
pwiz_tools/Skyline/Documentation/Tutorials/shared/
```

Including:
- `SkylineStyles.css` - Common styles
- `skyline.js` - TOC generation and other scripts
- Shared images (icons, common UI elements)
