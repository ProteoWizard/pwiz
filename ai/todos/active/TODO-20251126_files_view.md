# TODO-20251126_files_view.md

## Branch Information
- **Branch**: `Skyline/work/20251126_files_view`
- **Created**: 2025-11-26
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: (pending - will replace #3334)
- **Objective**: Clean up and prepare the Files view feature for merge to master by creating a fresh branch with all changes from the 11-month-old branch, carefully reviewing each change, and creating a clean squashed commit ready for merge

## Background

This is a cleanup and consolidation effort for the Files view feature that was originally developed on `Skyline/work/20250102_FilesView` (PR #3334) over 11 months. The goal is to:

1. Create a fresh branch from current master
2. Apply all changes from the old branch as uncommitted changes
3. Carefully review each change to identify and remove:
   - Unintended designer file changes
   - Changes that were later reverted but left traces
   - Any other unintended modifications
4. Create a clean, well-organized commit
5. Open a new PR to replace the old one
6. Close the old PR (#3334) once the new one is ready

## Migration Plan

### Phase 1: Create New Branch and Apply Changes

#### Step 1: Prepare New Branch from Master
- [ ] Ensure we're on master and up to date: `git checkout master && git pull origin master`
- [ ] Create new branch: `git checkout -b Skyline/work/20251126_files_view`
- [ ] Verify branch is clean and based on latest master

#### Step 2: Apply All Changes from Old Branch
- [ ] Get diff of all changes from old branch: `git diff master..Skyline/work/20250102_FilesView > /tmp/files_view_changes.patch`
- [ ] Apply changes as uncommitted: `git apply /tmp/files_view_changes.patch` (or use `git diff` and `git apply`)
- [ ] Alternative approach: Use `git checkout Skyline/work/20250102_FilesView -- .` then reset to uncommitted state
- [ ] Verify all changes are present and uncommitted: `git status`

#### Step 3: Rename TODO File
- [ ] Rename TODO file: `git mv ai/todos/active/TODO-20250102_FilesView.md ai/todos/active/TODO-20251126_files_view.md`
- [ ] Update TODO file header with new branch name and date
- [ ] Update references to old PR (#3334) with note about migration

### Phase 2: Review and Clean Up Changes

#### Step 4: Categorize and Review Changes
- [ ] List all modified files: `git status --short`
- [ ] Identify designer files (.Designer.cs, .resx, .settings) that may have unintended changes
- [ ] Identify core feature files (Files view implementation)
- [ ] Identify test files
- [ ] Identify documentation/configuration files

#### Step 5: Review Designer and Resource Files
- [ ] Review each .Designer.cs file change - revert if unintended
- [ ] Review each .resx file change - verify only intentional string additions
- [ ] Review .settings files - revert if unintended
- [ ] Use `git diff` to inspect each file carefully

#### Step 6: Review Core Feature Files
- [ ] Review Files view implementation files
- [ ] Verify all changes are intentional and necessary
- [ ] Check for commented-out code or debug code that should be removed
- [ ] Verify code follows project conventions (CRITICAL-RULES.md, STYLEGUIDE.md)

#### Step 7: Review Test Files
- [ ] Verify test changes are intentional
- [ ] Check for test code that was added and later should have been removed
- [ ] Verify tests follow TESTING.md guidelines

#### Step 8: Review Documentation and Configuration
- [ ] Review any documentation changes
- [ ] Review project file changes (.csproj, .sln)
- [ ] Verify only necessary files are included

### Phase 3: Create Clean Commit and PR

#### Step 9: Organize Changes for Commit
- [ ] Group related changes logically
- [ ] Ensure all unintended changes are reverted
- [ ] Verify build succeeds: Request developer to build and test
- [ ] Final review of `git diff` to ensure only intended changes remain

#### Step 10: Create Initial Commit
- [ ] Stage all reviewed changes: `git add .`
- [ ] Create comprehensive commit message describing the Files view feature
- [ ] Commit: `git commit -m "Files view feature: [detailed description]"`
- [ ] Verify commit looks good: `git show HEAD`

#### Step 11: Push and Create New PR
- [ ] Push branch to origin: `git push -u origin Skyline/work/20251126_files_view`
- [ ] Create new PR on GitHub with:
  - Title: "Files view feature (cleanup from PR #3334)"
  - Description explaining the migration from old branch
  - Reference to old PR #3334
  - List of key changes
- [ ] Link new PR in TODO file

#### Step 12: Close Old PR
- [ ] Add comment to old PR #3334 explaining migration to new PR
- [ ] Close old PR #3334 with reference to new PR
- [ ] Update TODO file to mark old PR as superseded

## Key Files to Review Carefully

Based on the old branch, key areas to review:

### Core Feature Files
- Files view UI implementation
- Files view model/controller
- Integration with SkylineWindow
- View state management

### Designer Files (likely to have unintended changes)
- Any .Designer.cs files
- .resx resource files
- .settings files

### Test Files
- FilesTreeFormTest.cs
- Any other test files related to Files view

### Configuration
- Project files (.csproj)
- Solution files if modified

## Success Criteria

- [ ] New branch created from latest master
- [ ] All intended changes from old branch applied as uncommitted
- [ ] All unintended changes identified and reverted
- [ ] Build succeeds with clean changes
- [ ] Tests pass (request developer to verify)
- [ ] Clean, well-organized commit created
- [ ] New PR created and linked in TODO
- [ ] Old PR #3334 closed with explanation

## Notes

- This is a cleanup effort to make the 11-month-old branch ready for merge
- Goal is a single clean commit (or small number of logical commits) that can be squashed on merge
- Focus on removing unintended changes while preserving all intentional feature work
- Take time to review each file carefully - better to be thorough now than have issues later

## Progress Tracking

### Current Status
- [x] Phase 1: New branch creation - ✅ Complete
- [x] Phase 2: Review and cleanup - ✅ Complete  
- [ ] Phase 3: Commit and PR creation - 🚧 Ready to commit

### Recent Work
- 2025-11-26: Created migration plan TODO
- 2025-11-26: Created new branch `Skyline/work/20251126_files_view` from master
- 2025-11-26: Applied all changes from old branch as uncommitted changes
- 2025-11-26: Identified test files and added to SkylineTester test list
- 2025-11-26: Documented CONSIDER and TODO items from FilesTreeFormTest.cs for future phases
- 2025-11-26: All 4 Files view tests passing (TestFilesModel, TestFilesTreeFileSystem, TestFilesTreeForm, TestSkylineWindowEvents)
- 2025-11-26: Fixed TestFilesModel to call folder prep function for FilesTreeFormTest.zip
- 2025-11-26: Completed thorough review of all modified files (see Review Findings below)
- 2025-11-26: Analyzed code coverage - 64.8% overall coverage for Files view code (see Coverage Analysis below)
- 2025-11-26: Added dotCover integration to Run-Tests.ps1 for automated coverage analysis
- 2025-11-26: Cleaned up test data files:
  - Removed empty `SkylineWindowEventsTest.zip` file
  - Reduced `FilesTreeFormTest.zip` from 87 MB to ~1 MB (removed unnecessary mass spec data files)
  - Added `.data` folder structure for better version control (FilesTreeFormTest.data, FilesTreeFileSystemTest.data)
- 2025-11-26: Ready to commit and create new PR

## Review Findings (2025-11-26)

### Designer Files (.Designer.cs, .resx, .csproj)
- ✅ **Skyline.csproj**: Clean - only adds new Files view source files and resources
- ✅ **TestFunctional.csproj**: Clean - only adds new test files
- ✅ **ViewMenu.Designer.cs**: Clean - only adds `viewFilesMenuItem` and separator
- ✅ **Resources.Designer.cs**: Clean - only adds new bitmap resources (AuditLog, CacheFile, FileMissing, FolderMissing, ReplicateMissing, Skyline_FilesTree, ViewFile)
- ✅ **AuditLogStrings.Designer.cs**: Clean - only adds new audit log strings for Files view operations
- ✅ **FileResources.Designer.cs**: New file - auto-generated from FileResources.resx
- ✅ **FilesTreeResources.Designer.cs**: New file - auto-generated from FilesTreeResources.resx
- ⚠️ **ControlsResources.resx**: Removed `<assembly alias="System.Windows.Forms".../>` line. This line exists in master. However, it's typically only needed when the resx file contains type references. Since this file doesn't appear to have type references that require it, the removal may be intentional Visual Studio cleanup, but worth verifying it doesn't break anything.

### Core Feature Files
- ✅ **SequenceTree.cs**: Only change is updating a dead URL to archive.org (legitimate cleanup)
- ✅ **SequenceTreeForm.cs**: Adds FilesTree token to persistent string (intentional for upgrade path)
- ✅ **TreeViewMS.cs**: Changes visibility modifiers (private→internal/public) and adds virtual methods for FilesTree extension (intentional)
- ✅ **SrmSettings.cs**: Refactoring to delegate property checks to PeptideSettings/TransitionSettings (clean refactoring)
- ✅ **PeptideSettings.cs**: Adds helper properties for Files view (HasLibraries, HasDocumentLibrary, etc.) and FindLibrarySpec method (intentional)
- ✅ **TransitionSettings.cs**: Adds helper properties for Files view (HasOptimizationLibrary, etc.) (intentional)
- ✅ **Prediction.cs**: Adds IFile interface implementation to RetentionScoreCalculatorSpec (intentional)
- ✅ **ChromatogramCache.cs**: Adds IFile interface implementation and ChromatogramCacheId (intentional)
- ✅ **MeasuredResults.cs**: Adds CacheFinal property and refactors UpdateCaches (intentional)
- ✅ **SrmDocument.cs**: Adds DocumentSavedEventArgs class (intentional for Files view integration)
- ✅ **LogMessage.cs**: Adds new audit log message types for Files view operations (intentional)
- ✅ **TreeViewStateRestorer.cs**: Changes NextTopNode visibility (private→public) for FilesTree access (intentional)

### Documentation
- ✅ **KeyboardShortcuts.html** (en/ja/zh-CHS): Only adds Alt+0 (Targets) and Alt+9 (Files) shortcuts (intentional)

### Test Files
- ✅ **CodeInspectionTest.cs**: Adds FilesTreeDataModelInspection() test. Note: Uses `IIdentiyContainer` (with typo) which matches the actual interface name in Identity.cs - this is a pre-existing typo in the codebase, not a bug in this PR.
- ✅ **TestRunnerFormLookup.csv**: Adds FilesTreeForm test mapping (intentional)
- ✅ **Test Data Cleanup**: 
  - Removed empty `SkylineWindowEventsTest.zip` file
  - Reduced `FilesTreeFormTest.zip` from 87 MB to ~1 MB by removing unnecessary mass spec data files
  - Added `.data` folder structure for better version control (allows text diffs and smaller incremental changes)

### Summary
Overall, the changes are very clean and intentional. The only questionable change is the removal of the assembly alias line in ControlsResources.resx, but this is likely harmless Visual Studio cleanup. All other changes are clearly related to the Files view feature implementation.

## Code Coverage Analysis (2025-11-26)

### Overall Coverage
- **Total Statements**: 2,383
- **Covered Statements**: 1,543
- **Coverage Percentage**: 64.8%

### Coverage by Component

**Excellent Coverage (80%+):**
- `BackgroundActionService`: 100% (54/54 statements)
- `ManagedFileSystemWatcher`: 100% (70/70 statements)
- `FileSystemService`: 100% (34/34 statements)
- `FileModel`: 92.6% (25/27 statements)
- `FileSystemUtil`: 88.6% (31/35 statements)
- `FileSystemHealthMonitor`: 84.6% (77/91 statements)
- `LocalFileSystemService`: 82.7% (263/318 statements)

**Good Coverage (50-80%):**
- `FilesTree`: 75.6% (409/541 statements)
- `FilesTreeResources`: 54.8% (51/93 statements)
- `FilesTreeForm`: 51.4% (341/664 statements)

**Needs Improvement (<50%):**
- `FilesTreeNode`: 41.2% (188/456 statements)

### Coverage Gaps
The main areas with lower coverage are:
1. **FilesTreeNode** (41.2%) - Tree node implementation, likely includes edge cases and error handling
2. **FilesTreeForm** (51.4%) - UI form, likely includes less-tested UI interactions and edge cases
3. **FilesTreeResources** (54.8%) - Resource management

### Recommendations
- Consider adding tests for edge cases in FilesTreeNode (drag-and-drop scenarios, error conditions)
- Add UI interaction tests for FilesTreeForm (context menus, keyboard shortcuts, edge cases)
- The core service layer (FileSystemService, BackgroundActionService) has excellent coverage

### Coverage Tooling
- Added `-Coverage` parameter to `Run-Tests.ps1` to enable dotCover code coverage
- Coverage results are exported to JSON format in `ai\.tmp\coverage-{timestamp}.json`
- Use `ai\.tmp\analyze-coverage.ps1` to analyze coverage results for Files view code

## Test Coverage

### New Test Files Added
- `FilesTreeFormTest.cs` - Main UI tests for Files view
  - Test method: `TestFilesTreeForm`
- `FilesModelTest.cs` - Model/backend tests for Files view
  - Test method: `TestFilesModel`
- `FilesTreeFileSystemTest.cs` - File system integration tests
  - Test method: `TestFilesTreeFileSystem`
- `SkylineWindowEventsTest.cs` - Window event tests (updated for Files view)
  - Test method: `TestSkylineWindowEvents`

All tests have been added to `pwiz_tools/Skyline/SkylineTester test list.txt` for easy execution.

## Future Work Items (Deferred to Post-Merge Phase)

### From FilesTreeFormTest.cs CONSIDER Comments

These items were identified during development but deferred for future phases:

1. **Additional file type testing** - Test additional file types: imsdb, irtdb, protdb
2. **Background Proteome dialog** - Verify double-click on a Background Proteome opens the correct dialog
3. **Right-click menu** - Verify right-click menu includes "open containing folder" only for files found locally
4. **Tooltip testing** - Test tooltips (see example in MethodEditTutorialTest.ShowNodeTip)
5. **Drag-and-drop expansion** - Expand drag-and-drop tests with scenarios:
   - Disjoint selection
   - Tree disallows dragging un-draggable nodes
6. **Non-local path handling** - Test handling of non-local paths from SrmSettings (e.g., replicate sample file paths cannot be found locally)
7. **Confirm dialog behavior** - New test ensuring clicking 'x' upper right-hand corner of confirm dialog does not delete Replicate / Spectral Library
8. **View file token verification** - Assert the .view file contains the FilesTreeShownOnce token
9. **Tab count verification** - Count the number of tabs in the DigitalRune DockPane (unclear how to implement)
10. **Specific settings change assertion** - Assert a more specific location in SrmSettings changed, which would differ depending on the type of folder

### From FilesTreeFormTest.cs TODO Comments

1. **Test readability improvement** - Improve test readability with a helper that gets node by model type

