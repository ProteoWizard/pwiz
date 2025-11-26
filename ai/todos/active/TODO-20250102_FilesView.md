# TODO-20250102_FilesView.md

## Branch Information
- **Branch**: `Skyline/work/20250102_FilesView`
- **Created**: 2025-01-02
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: [#3334](https://github.com/ProteoWizard/pwiz/pull/3334)
- **Objective**: Complete the Files view feature implementation to make it ready for merge to master as "phase 1", ensuring it appears automatically when documents are opened as a tabbed view with the primary "Targets" view, passes nightly stress tests, and is ready for Skyline-daily beta release

## Background

This is a long-running branch (since January 2, 2025) that implements a Files view feature in Skyline. The Files view appears as a tabbed view alongside the primary "Targets" (SequenceTree) view, showing files related to the document (e.g., .sky files, .view files, audit logs, chromatogram cache files, spectral libraries, etc.).

The primary developer (Eddie O'Neil) has left the team and has limited time to devote to finishing it. The current developer is taking over primary responsibility for getting it to a state where it is ready to merge to master at least as a "phase 1". After that, bugs can still be fixed, but it should also be fairly ready to release in the Skyline-daily beta release, and it needs to not break nightly stress tests.

Significant effort has already been focused on making the Files view appear automatically when documents are opened (as a tabbed view with the primary "Targets" view) so that it will get noticed more quickly and show up in many tutorials when screenshots are retaken using the automated screenshot mechanism.

## Current State

- Files view appears automatically when documents are opened (as a tabbed view with Targets view)
- Files view shows files related to the document (SkylineFile, SkylineAuditLog, SkylineViewFile, etc.)
- Test infrastructure exists (`FilesTreeFormTest.cs`)
- Recent merge from master completed (conflict in `SkylineFiles.cs` resolved)

## Work Required

### Phase 1: Merge Readiness

#### 1. Code Quality & Build
- [ ] Ensure code builds without warnings
- [ ] Run ReSharper inspection and fix any issues
- [ ] Verify all tests pass (unit, functional, tutorial)
- [ ] Check for any remaining TODO comments or incomplete implementations
- [ ] Review and fix any linter errors

#### 2. Testing
- [ ] Run nightly stress tests and ensure they pass
- [ ] Verify Files view appears correctly when documents are opened
- [ ] Test Files view tab behavior (showing/hiding, activation)
- [ ] Test Files view with various document types (proteomic, small molecule, mixed)
- [ ] Test Files view with saved/unsaved documents
- [ ] Test Files view with documents that have associated files (.view, audit logs, cache files, libraries)
- [ ] Verify automated screenshot mechanism works correctly with Files view
- [ ] Test Files view in different UI modes

#### 3. Documentation & Resources
- [ ] Ensure all user-facing strings are in resource files (no hardcoded English)
- [ ] Verify resource strings are properly localized
- [ ] Review any new UI elements for accessibility
- [ ] Update any relevant documentation

#### 4. Integration & Compatibility
- [ ] Verify Files view works with existing document workflows
- [ ] Test Files view with Panorama integration
- [ ] Test Files view with remote file sources
- [ ] Ensure Files view doesn't break existing functionality
- [ ] Test Files view with large documents

#### 5. Performance
- [ ] Verify Files view doesn't cause performance regressions
- [ ] Test Files view with documents containing many files
- [ ] Ensure Files view initialization doesn't block UI

### Phase 2: Post-Merge (Future Work)

These items can be addressed after merge but should be tracked:

- [ ] Address any bugs discovered in beta testing
- [ ] Performance optimizations if needed
- [ ] Additional features or refinements based on user feedback
- [ ] Further integration with other Skyline features

## Known Issues & Risks

- Long-running branch - may have accumulated technical debt
- Need to ensure compatibility with all existing workflows
- Must not break nightly stress tests
- Files view visibility in tutorials/screenshots is important

## Related Files

Key files related to Files view implementation:
- `pwiz_tools/Skyline/Controls/FilesTreeForm.cs` (if exists)
- `pwiz_tools/Skyline/TestFunctional/FilesTreeFormTest.cs`
- `pwiz_tools/Skyline/Skyline.cs` (Files view initialization)
- `pwiz_tools/Skyline/SkylineGraphs.cs` (Files view display logic)
- `pwiz_tools/Skyline/Model/Files/` (Files model classes)

## Progress Tracking

### Recent Work
- [x] Merged master into branch (2025-01-XX)
  - Resolved conflict in `SkylineFiles.cs`
  - Applied changes from master:
    - Added comment to `using System.Net;` (HttpStatusCode)
    - Changed `dlg.InitializeDialog(progressMonitor)` to `dlg.LoadServerData(progressMonitor)`
    - Changed `MessageDlg.ShowException` to `ExceptionUtil.DisplayOrReportException`

### Next Steps
1. Review current test status
2. Run build and verify no warnings
3. Run functional tests
4. Address any failing tests
5. Run nightly stress tests
6. Fix any issues found

## Notes

- This branch has been in development since January 2, 2025
- Primary goal is to get to a mergeable "phase 1" state
- Files view should appear automatically when documents are opened
- Must not break existing functionality or nightly tests
- Ready for beta release after merge

