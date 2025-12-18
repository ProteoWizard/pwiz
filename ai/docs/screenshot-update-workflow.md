# Tutorial Screenshot Update Workflow

This document describes the workflow for reviewing and updating tutorial screenshots, including efficient conventions for developer-LLM collaboration using the ImageComparer tool.

## Overview

Tutorial screenshots need periodic review to ensure they reflect current Skyline UI. This typically happens:
- Before major releases (e.g., "FEATURE COMPLETE" milestones)
- After significant UI changes
- When new features affect tutorial content

The workflow involves comparing current screenshots against baseline versions (Git HEAD, web-published, or disk), categorizing differences, and either accepting, reverting, or fixing each one.

## ImageComparer Tool

### Location
```
pwiz_tools/Skyline/Executables/DevTools/ImageComparer/
```

### Key Files
- `ImageComparerWindow.cs` - Main form with comparison logic
- `ImageComparerWindow.Designer.cs` - Designer-generated UI code
- `ScreenshotInfo.cs` - Screenshot file parsing and diff calculation

### Running ImageComparer
Build and run the ImageComparer project from Visual Studio, or use a pre-built executable if available.

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `F11` | Next screenshot with diff |
| `Shift+F11` | Previous screenshot with diff |
| `PageDown` | Next screenshot |
| `PageUp` | Previous screenshot |
| `Ctrl+Right/Down` | Next screenshot |
| `Ctrl+Left/Up` | Previous screenshot |
| `F5` | Refresh all screenshots |
| `Ctrl+R` | Refresh current screenshot |
| `Ctrl+Tab` | Cycle image source (disk/web/git) |
| `F12` | Revert screenshot (restore from old source) |
| `Ctrl+Z` | Revert screenshot |
| `Ctrl+C` | Copy new image to clipboard |
| `Ctrl+Shift+C` | Copy old image to clipboard |
| `Ctrl+Alt+C` | Copy diff (highlighted) image to clipboard |
| `Ctrl+S` | Save diff image to `ai\.tmp\` folder |
| `Ctrl+V` | Paste image from clipboard (overwrites current) |
| `Ctrl+G` | Open screenshot URL in browser |

### Diff Image Saving (Ctrl+S)

When you press `Ctrl+S`, the diff image is saved to:
```
ai\.tmp\{TutorialName}-{Locale}-s-{Number}-diff-{PixelCount}px.png
```

Example: `ai\.tmp\DIA-en-s-06-diff-1234px.png`

This location enables easy sharing with LLM assistants for analysis.

## Decision Categories

For each screenshot difference, choose one of:

| Decision | When to Use | Action |
|----------|-------------|--------|
| **Accept** | Valid change from completed feature work | Keep the new screenshot |
| **Revert** | Unintended change, version-only change, or regression | Restore original (F12) |
| **Fix** | Bug discovered that needs code fix | Document as BUG-XXX, fix code, re-capture |

## Bug Documentation Convention

When a screenshot reveals a bug:

1. **Assign a bug ID**: `BUG-001`, `BUG-002`, etc. (sequential within the TODO file)
2. **Document in TODO file** under "Bugs Found" section:

```markdown
### BUG-001: Brief description
**Found in**: Tutorial/locale/s-XX.png
**Description**: What's wrong and why it's a bug
**Root cause**: (Once investigated)
**Fix**: (Proposed or implemented solution)
**Status**: Needs investigation | FIXED
```

3. **Mark affected screenshots** in the checklist:
```markdown
- [ ] s-06.png - **BUG-001** - Brief description
```

## LLM Interaction Conventions

### Short-Form Messages

To enable efficient communication, the following conventions are understood:

**Screenshot Reference**:
```
{Tutorial}/{Locale}/s-{Number}.png
```
Examples: `DIA/en/s-06.png`, `CustomReports/ja/s-15.png`

**Diff Image Location**:
```
ai\.tmp
```
When you say "look at the diff in ai\.tmp", the LLM should check for recently saved diff images.

**Quick Status Updates**:
```
s-06 accepted - [brief reason]
s-07 reverted - [brief reason]
s-08 BUG-001 - [brief description]
```

### Example Efficient Interactions

**Developer reports an issue:**
```
s-06 has an issue. please look at the diff in ai\.tmp
```

**LLM understands:**
1. Read the diff image from `ai\.tmp\*-s-06-diff-*.png`
2. Analyze what changed
3. Help diagnose whether it's a bug or expected change

**Developer categorizes multiple screenshots:**
```
DIA s-12, s-13, s-14 all accepted - ImportPeptideSearchDlg height fix working
```

**Developer reports a bug pattern:**
```
CustomReports s-06, s-08, s-09, s-15 all have BUG-001 - same missing icon issue
```

### Context Loading

When starting work on screenshot review:

1. **Read the active TODO file** for current branch context
2. **Check `ai\.tmp\`** for any saved diff images
3. **Understand the tutorial** being reviewed from the screenshot paths

## TODO File Integration

### Screenshot Review Section Structure

```markdown
### {Tutorial} Tutorial
#### English (en)
- [x] s-04.png - **Accepted** - Brief description of change
- [ ] s-06.png - **BUG-001** - Brief description of issue
- [x] s-12.png - **Reverted** - Only version change, not relevant

#### Japanese (ja)
- [x] s-04.png - **Accepted** - Same as English
...
```

### Progress Tracking Section

```markdown
## Progress Tracking

### Current Status
- [ ] ImageComparer review of all screenshots (in progress - Tutorial1, Tutorial2 complete)
- [x] BUG-001 fixed (brief description)
- [ ] BUG-002 investigation (brief description)
- [ ] Screenshots re-captured after fixes
- [ ] Final verification
```

## Typical Workflow Session

### 1. Setup
```bash
# Ensure on correct branch
git checkout Skyline/work/YYYYMMDD_update_screenshots_XX_X_X

# Open ImageComparer
# Navigate to Tutorials folder
```

### 2. Review Loop
For each screenshot with differences:
1. Examine the diff in ImageComparer
2. If unclear, press `Ctrl+S` to save diff image
3. Decide: Accept, Revert, or document as Bug
4. Tell the LLM the decision in short form
5. LLM updates TODO file

### 3. Bug Investigation
When bugs are found:
1. LLM investigates the root cause
2. LLM proposes fix
3. Developer reviews and applies fix
4. Re-run affected tutorial to re-capture screenshots
5. Verify fix in ImageComparer

### 4. Completion
1. All screenshots categorized
2. All bugs fixed and re-captured
3. Final verification pass
4. Commit and PR

## Multi-Language Considerations

Screenshots exist in multiple locales:
- `en` - English
- `ja` - Japanese
- `zh-CHS` - Chinese Simplified

Often the same bug affects all locales. When documenting:
```markdown
### BUG-003: ImportPeptideSearchDlg height increased
**Found in**: DIA s-10.png (en, ja, zh-CHS)
```

And when accepting identical changes across locales, batch them:
```
DIA s-10 accepted across all locales - height fix working
```

### RESX File Synchronization

When investigating screenshot differences, you may notice that localized `.resx` files (`.ja.resx`, `.zh-CHS.resx`) have different control positions or properties than the English `.resx` file. **This is expected** - the localized files are only synchronized periodically through a translation workflow.

**Why files differ:**
- Developers modify the English `.resx` file during feature development
- Localized files are not automatically updated
- A synchronization step copies non-text properties from English to localized files

**Synchronization tool:**
```
pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer/
```

**Boost Build targets:**
- `IncrementalUpdateResxFiles` - Updates `.ja.resx` and `.zh-CHS.resx` files by syncing properties from English
- `FinalizeResxFiles` - Run after visual freeze to prepare files for translation

**When reviewing screenshots:**
1. **Option 1 (quick)**: Note that RESX files are out of sync; fix only the English `.resx` file
2. **Option 2 (thorough)**: Run `IncrementalUpdateResxFiles` to sync all localized files, then re-capture screenshots

**Important**: If Option 1 is chosen and localized RESX files remain out of sync, create a follow-up TODO in `ai/todos/backlog/` when moving the current TODO to `completed/`. This ensures the RESX synchronization and localized screenshot re-capture is tracked for the next release cycle.

## Common Patterns

### Scrollbar Thumb Changes
Minor scrollbar position/size changes due to added fields are typically **accepted** - they reflect legitimate UI growth.

### Version String Changes
Screenshots showing only Skyline version changes (e.g., "25.1" to "26.0") should typically be **reverted** unless the tutorial specifically discusses versions.

### Font Rendering Differences
Tiny pixel differences in text rendering (anti-aliasing, hinting) that don't affect readability should be **accepted** if no functional change occurred.

### Dialog Size Changes
Unexpected dialog size changes often indicate a bug in AutoSize, MinimumSize, or layout logic. Document as a bug and investigate.

### Import Progress / Timing-Dependent Forms
Forms like `AllChromatogramsGraph` (import progress monitor) show timing-dependent state that varies between runs. These are typically **reverted** and flagged for future consistency improvements.

## Improving Screenshot Consistency

When reverting screenshots, always consider whether the underlying cause can be fixed to prevent future false-positive changes. This is especially important when an entire class of screenshots needs reverting.

### Questions to Ask
1. **Is this timing-dependent?** Progress monitors, animations, or async operations may need deterministic state during capture.
2. **Does it show version/date/time?** Consider setting fixed values during screenshot capture mode.
3. **Is it a rendering artifact?** 1-2 pixel differences from font anti-aliasing or graph rendering may be unavoidable.
4. **Is it a layout issue?** AutoSize, MinimumSize, or anchor issues can often be fixed in code.

### Document for Future Work
When a class of screenshots repeatedly needs reverting:
1. Add a "Future Work" section to the current TODO documenting the issue
2. Extract to `ai/todos/backlog/` before merging
3. Include: affected screenshots, root cause analysis, prior work done, proposed solution

### Examples of Consistency Improvements
- **Dates/times in Audit Log**: Fixed to deterministic values during tutorial capture
- **Version strings**: Could be set to fixed values during screenshot mode
- **Progress monitors**: May need snapshot-at-percentage or deterministic timing

The goal is to reduce false-positive diffs so reviews can focus on actual changes rather than noise.

## Related Documentation

- [workflow-guide.md](workflow-guide.md) - General branch and TODO workflow
- [build-and-test-guide.md](build-and-test-guide.md) - Running tutorial tests
- TODO file for current work: `ai/todos/active/TODO-YYYYMMDD_update_screenshots_*.md`
