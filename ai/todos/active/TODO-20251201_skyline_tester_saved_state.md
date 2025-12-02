# TODO-20251201_skyline_tester_saved_state.md

## Branch Information
- **Branch**: `Skyline/work/20251201_skyline_tester_saved_state`
- **Created**: 2025-12-01
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Restore tutorial selections, screenshot settings, and smart tab navigation to streamline edit-build-test workflow

## Objective

Improve SkylineTester session persistence by restoring tutorial selections, tutorial screenshot settings, and returning to the correct tab (not Output) on startup.

## Problem Statement

The current SkylineTester state restoration is incomplete, creating friction in the edit-build-test cycle:

**Tutorial Tab State (Not Preserved):**
- Checked tutorial(s) are not restored on restart
- "Starting shot:" screenshot number is not remembered
- User must re-check tutorial checkbox and re-enter screenshot number on every SkylineTester restart

**Output Tab Issue (Wrong Tab Restored):**
- When tests run, they automatically switch to Output tab
- Output tab is saved as active tab
- On next startup, SkylineTester opens on Output tab (empty, no actions)
- User must click back to Tests/Tutorials tab to continue work

**Current Working Behavior (Tests Tab):**
- Tests tab selections ARE restored correctly (via `SkylineTester test list.txt`)
- Tri-state checkbox indicators (gray text for partial selection) work well
- This provides the desired streamlined workflow for Tests tab

## Desired Behavior

### Tutorial Tab Persistence
When developer works with tutorials:
1. Check tutorial in Tutorials tab (e.g., "TestAbsoluteQuantificationTutorial")
2. Enter screenshot number in "Starting shot:" field (e.g., "28")
3. Click Run (or F5)
4. Close SkylineTester, make code changes
5. Reopen SkylineTester → tutorial checkbox and screenshot number automatically restored
6. Press F5 to re-run without re-selecting

### Smart Tab Restoration
When SkylineTester switches to Output tab during test run:
1. User is working in Tests or Tutorials tab
2. User clicks Run (test execution begins, auto-switches to Output tab)
3. User closes SkylineTester
4. User reopens SkylineTester → **opens on original tab (Tests or Tutorials), not Output**

**Rationale:** Output tab is transient/informational only. The "active" tab for workflow purposes is the tab the user was on before running tests.

## Goals

1. **Tutorial selections persist** - same as Tests tab behavior
2. **Tutorial screenshot number persists** - avoid re-typing on every run
3. **Smart tab restoration** - return to Tests/Tutorials tab, not Output
4. **Tri-state indicators for Tutorials** - apply existing gray-text pattern (already implemented)
5. **Consistency** - Tests and Tutorials tabs should behave the same way

## Proposed Solution

### 1. Tutorial State File
Create `SkylineTester tutorial state.txt` (parallel to existing `SkylineTester test list.txt`):

```
# SkylineTester tutorial state
# Tutorial: TestAbsoluteQuantificationTutorial
# StartingShot: 28
TestAbsoluteQuantificationTutorial
```

**Format:**
- Line 1-2: Comments with metadata
- Line 3+: Checked tutorial name(s) (one per line)
- Special comment `# StartingShot: N` to persist screenshot number

### 2. TabTutorials.cs - Save and Restore

**Save tutorial state** (when user checks/unchecks tutorial or changes screenshot number):
- Write checked tutorials to `SkylineTester tutorial state.txt`
- Include `# StartingShot: N` comment line
- Use existing pattern from `TabTests.GetTestList()`

**Restore tutorial state** (on SkylineTester startup):
- Read `SkylineTester tutorial state.txt` (if exists)
- Check matching tutorials in tutorialsTree
- Parse `# StartingShot: N` and set screenshot field
- Apply tri-state checkbox colors to parent nodes (already implemented)
- Use existing pattern from `SkylineTesterWindow.RestoreCheckedTestsFromFile()`

### 3. Smart Tab Restoration

**Track "workflow tab"** (separate from "active tab"):
- Save two pieces of state:
  - `lastActiveTab`: The tab currently visible (may be Output)
  - `lastWorkflowTab`: The tab user was working in before Output appeared
- When saving state:
  - If current tab is Output → save `lastWorkflowTab` as the tab to restore
  - Otherwise → save current tab normally
- On startup:
  - Restore to `lastWorkflowTab` if it was set
  - This skips Output tab and returns to Tests/Tutorials

**Implementation approach:**
- Add `_lastWorkflowTab` field to track tab before Output switch
- Update tab switching logic to set `_lastWorkflowTab` when switching TO Output
- Modify `LoadSettings()` to restore `_lastWorkflowTab` instead of `lastActiveTab` when appropriate

### 4. File Persistence

**Files to modify:**
- `SkylineTesterWindow.cs` - Add `RestoreTutorialState()` similar to `RestoreCheckedTestsFromFile()`
- `TabTutorials.cs` - Add tutorial state save/restore methods
- Settings persistence - Update tab tracking logic

**New files created:**
- `pwiz_tools/Skyline/SkylineTester tutorial state.txt` (auto-generated)

## Implementation Plan

### Phase 1: Tutorial State Persistence
- [ ] Add `SkylineTester tutorial state.txt` file format
- [ ] Implement `SaveTutorialState()` in TabTutorials.cs
- [ ] Write checked tutorials to file when user changes selection
- [ ] Write `# StartingShot: N` comment when screenshot field changes
- [ ] Implement `RestoreTutorialState()` in SkylineTesterWindow.cs
- [ ] Read tutorial state file on startup
- [ ] Check matching tutorials in tutorialsTree
- [ ] Parse and restore screenshot number
- [ ] Apply tri-state checkbox colors to tutorial parent nodes

### Phase 2: Smart Tab Restoration
- [ ] Add `_lastWorkflowTab` tracking field
- [ ] Update tab switch logic to track workflow tab
- [ ] When switching TO Output tab → save current tab as `_lastWorkflowTab`
- [ ] Modify `LoadSettings()` to restore workflow tab instead of Output
- [ ] Test: Tests tab → Run → Close → Reopen → Opens on Tests tab
- [ ] Test: Tutorials tab → Run → Close → Reopen → Opens on Tutorials tab

### Phase 3: Testing
- [ ] Test tutorial checkbox persistence across restarts
- [ ] Test screenshot number persistence
- [ ] Test tab restoration from Tests tab
- [ ] Test tab restoration from Tutorials tab
- [ ] Test mixed workflow (Tests → Tutorials → Tests)
- [ ] Verify tri-state indicators work in Tutorials tab
- [ ] Verify existing Tests tab behavior unchanged

### Phase 4: Documentation
- [ ] Document tutorial state file format
- [ ] Document tab restoration behavior
- [ ] Add code comments explaining workflow tab tracking
- [ ] Update commit message with rationale

## Success Criteria

### Tutorial Persistence
- [x] Tri-state checkbox indicators already work in Tutorials tab (from previous PR)
- [ ] Checked tutorials persist across SkylineTester restarts
- [ ] Screenshot number persists across restarts
- [ ] `SkylineTester tutorial state.txt` file created automatically
- [ ] File follows same pattern as `SkylineTester test list.txt`

### Tab Restoration
- [ ] Running tests from Tests tab → reopen → Tests tab active
- [ ] Running tests from Tutorials tab → reopen → Tutorials tab active
- [ ] Output tab never saved as active tab on startup
- [ ] Manually clicking Output tab and closing → Output tab remembered (explicit user action)

## Technical Details

### Tutorial State File Format

**Location:** `pwiz_tools/Skyline/SkylineTester tutorial state.txt`

**Example:**
```
# SkylineTester tutorial state
# StartingShot: 28
# Updated: 2025-12-01 14:30:00
TestAbsoluteQuantificationTutorial
```

**Multiple tutorials:**
```
# SkylineTester tutorial state
# StartingShot: 1
# Updated: 2025-12-01 14:30:00
TestAbsoluteQuantificationTutorial
TestTargetedMSMSTutorial
```

**Parsing rules:**
- Lines starting with `#` are comments
- Special comment `# StartingShot: N` extracts screenshot number
- Blank lines ignored
- Tutorial names trimmed

### Tab Tracking Logic

**Current behavior (problematic):**
```csharp
// On tab switch
private void Tabs_SelectedIndexChanged(object sender, EventArgs e)
{
    _currentTab = Tabs.SelectedTab;  // Always saves current tab
}

// On settings save
SaveSettings()
{
    settings.Add("lastActiveTab", _currentTab.Name);  // Saves Output tab!
}
```

**Proposed behavior (smart restoration):**
```csharp
private TabPage _lastWorkflowTab;  // Tab user was working in

// On tab switch
private void Tabs_SelectedIndexChanged(object sender, EventArgs e)
{
    if (Tabs.SelectedTab == OutputPage && _lastWorkflowTab == null)
    {
        // Switching TO Output - remember where we came from
        _lastWorkflowTab = _currentTab;
    }
    else if (Tabs.SelectedTab != OutputPage)
    {
        // User explicitly selected a workflow tab
        _lastWorkflowTab = null;  // Clear override
    }
    _currentTab = Tabs.SelectedTab;
}

// On settings save
SaveSettings()
{
    var tabToSave = _lastWorkflowTab ?? _currentTab;
    if (tabToSave == OutputPage)
        tabToSave = TestsPage;  // Fallback to Tests if somehow Output leaked through
    settings.Add("lastActiveTab", tabToSave.Name);
}
```

## Example Workflow (Before vs After)

### Current Workflow (Tutorial Development)
```
1. Open SkylineTester
2. Click Tutorials tab
3. Check "TestAbsoluteQuantificationTutorial"
4. Type "28" in Starting shot field
5. Press F5 (runs, switches to Output tab)
6. Close SkylineTester
7. Make code changes
8. Open SkylineTester (Output tab shown, empty)
9. Click Tutorials tab
10. Check "TestAbsoluteQuantificationTutorial" AGAIN
11. Type "28" AGAIN in Starting shot field
12. Press F5

Total: 12 steps, 3 wasted on re-selecting
```

### Improved Workflow
```
1. Open SkylineTester (Tutorials tab shown)
2. Tutorial already checked, screenshot already "28"
3. Press F5
4. (Make code changes)
5. Open SkylineTester (Tutorials tab shown)
6. Press F5

Total: 6 steps (50% reduction)
```

## Implementation Notes

### File Locations
- Tests state: `SkylineTester test list.txt` (existing)
- Tutorial state: `SkylineTester tutorial state.txt` (new)
- Both in same directory: `pwiz_tools/Skyline/`

### Code Reuse Opportunities
- `RestoreCheckedTestsFromFile()` pattern can be replicated for tutorials
- `ApplyTriStateToNode()` already works for any TreeView (Tests or Tutorials)
- Tab tracking logic is self-contained in `SkylineTesterWindow.cs`

### Edge Cases
- Tutorial state file doesn't exist → start with no tutorials checked (current behavior)
- Tutorial state file corrupted → log error, ignore file, start fresh
- Screenshot field empty/invalid → default to 1
- No workflow tab tracked → fallback to Tests tab

## Related Work

- PR #3680 - SkylineTester auto-restore checked tests feature
- PR #3680 - Tri-state checkbox indicators (gray text for partial selection)
- `SkylineTester test list.txt` format and restoration logic

## Benefits

1. **Streamlined tutorial development** - No re-selecting tutorial or screenshot number
2. **Consistent UX** - Tests and Tutorials tabs behave identically
3. **Reduced friction** - Edit-build-test cycle becomes faster
4. **Better focus** - Opening SkylineTester returns to working tab, not empty Output
5. **Leverages existing patterns** - Reuses test list restoration logic
6. **Low risk** - Isolated changes, fallback to current behavior if files missing

## Non-Goals

- Persisting Output tab content (it's regenerated on each run)
- Persisting other SkylineTester settings (out of scope)
- Changing tab switching behavior (only restoration behavior)
- Modifying tutorialsTree structure (tri-state already works)

## Risks & Mitigations

**Risk:** Tutorial state file corrupted by manual editing
- **Mitigation:** Robust parsing, skip invalid lines, log errors

**Risk:** User expects Output tab on startup (unlikely)
- **Mitigation:** Only skip Output if it was auto-selected by test run

**Risk:** Screenshot number format changes
- **Mitigation:** Use simple `# StartingShot: N` comment format, easy to update

**Risk:** Tab tracking logic breaks tab switching
- **Mitigation:** Minimal changes, fallback to current tab if tracking fails
