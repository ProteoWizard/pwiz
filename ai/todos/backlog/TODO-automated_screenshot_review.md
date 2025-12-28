# TODO-automated_screenshot_review.md

## Branch Information
- **Branch**: (to be created when work starts)
- **Base**: `master`
- **Created**: (pending)
- **Status**: Backlog
- **PR**: (pending)
- **Objective**: Enable autonomous screenshot review triggered by PR merges or scheduled CI runs

## Background

The 26.0.9 screenshot review (TODO-20251215_update_screenshots_26_0_9.md) found 10 bugs through manual ImageComparer review of ~195 screenshots. This process was time-consuming and required significant human expertise to categorize changes as accept/revert/fix.

An automated system could catch unintentional UI changes as they happen, rather than discovering them months later during release preparation.

## Vision

Each code change is automatically reviewed for UI impact. Unintentional changes (like BUG-001 through BUG-010 found in the 26.0.9 review) are caught when they happen, not months later during release preparation.

## Implementation Phases

### Phase 1: CLI Diff Tool

Extract ImageComparer's diff generation logic to a command-line tool.

**Input**: Directory of screenshots to compare against git HEAD

**Output** to `ai/.tmp/screenshot-review/`:
- `report.json` - Machine-readable diff data:
  - File paths (old/new)
  - Dimensions before/after
  - Pixel count changed
  - Percentage changed
  - Tutorial/locale metadata
- `report.md` - Human-readable summary grouped by tutorial
- `{Tutorial}-{Locale}-{Number}-diff.png` - Diff images for each changed screenshot with header metadata overlay

**Key features**:
- Reuse existing `ScreenshotInfo` parsing logic
- Reuse existing pixel diff algorithm
- Add header metadata to diff images: pixel count, dimensions, tutorial/locale
- Support filtering by tutorial, locale, or pixel threshold

### Phase 2: Claude Code Review Command

Create a slash command `/pw-screenshot-review` that:

1. Runs the CLI diff tool from Phase 1
2. Reads the generated `report.json` and diff images
3. Produces initial recommendations with reasoning:
   - **Accept** - Valid change from known feature work
   - **Revert** - Timing inconsistency or rendering artifact
   - **Investigate** - Potential bug requiring human review
4. Groups changes by likely cause:
   - Files view additions
   - RT alignment/imputation changes
   - Control layout shifts
   - Scrollbar thumb changes
   - New menu items/buttons
5. Flags potential bugs for human review with specific concerns
6. Generates draft TODO entries for bugs found

**Required context**:
- Recent PR descriptions and commit messages
- Known feature work in progress
- Historical patterns from previous reviews

### Phase 3: Autonomous CI Integration

**Triggering**:
- Run screenshot generation on PR merge (requires standardized test environment)
- Or scheduled nightly run on master branch
- Store baseline screenshots in known location

**Workflow**:
1. CI triggers screenshot capture on merge
2. CLI tool generates diff report
3. Claude Code review command analyzes report
4. System posts results:
   - PR comment with summary (if triggered by merge)
   - Create tracking issue (if bugs suspected)
   - Update dashboard/notification

**Environment requirements**:
- Consistent Windows version and display settings
- Fixed screen resolution (e.g., 1920x1080)
- Consistent font rendering settings
- No other windows/overlays during capture

## Technical Considerations

### Existing Code to Reuse

- `ImageComparer/ScreenshotInfo.cs` - Screenshot metadata parsing
- `ImageComparer/ImageComparerWindow.cs` - Pixel diff algorithm
- `ScreenshotProcessingExtensions` - Border cleanup algorithm

### New Code Needed

- CLI wrapper for diff generation
- JSON/Markdown report formatters
- Slash command implementation
- CI integration scripts

### Storage and Baseline Management

- Baseline screenshots stored in git (current approach)
- Or stored in artifact storage with git hash reference
- Need strategy for baseline updates after accepted changes

## Tasks

### Phase 1
- [ ] Extract diff algorithm to standalone library
- [ ] Create CLI tool with JSON/Markdown output
- [ ] Add header metadata overlay to diff images
- [ ] Add filtering options (tutorial, locale, threshold)

### Phase 2
- [ ] Create `/pw-screenshot-review` slash command
- [ ] Implement change categorization logic
- [ ] Add bug detection heuristics
- [ ] Generate draft TODO entries

### Phase 3
- [ ] Define CI environment requirements
- [ ] Create CI workflow for screenshot capture
- [ ] Implement notification/issue creation
- [ ] Set up baseline management

## Priority

Low to Medium - This is infrastructure investment that pays off over time. Start with Phase 1 which provides immediate value for manual reviews, then iterate.

## Related

- TODO-screenshot_consistency_improvements.md - Addresses root causes of non-deterministic captures
- ai/docs/screenshot-update-workflow.md - Documents current manual process
