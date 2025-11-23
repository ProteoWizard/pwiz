# TODO: AI/SkylineTester Integration - Run-Tests.ps1 Enhancements

## Branch Information
- **Branch**: Skyline/work/20251123_ai_skyline_tester_integration
- **Created**: 2025-11-23
- **PR**: #3681

## Objective

Complete the bidirectional integration between LLM-driven test execution (Run-Tests.ps1) and human-driven test execution (SkylineTester) by adding test list file support and standardized logging to Run-Tests.ps1.

**Prerequisites:**
- ✅ SkylineTester auto-restore feature (TODO-skyline_tester_auto_restore.md) - **must be completed first**
- ✅ PR #3667 merged to master (httpclient_to_progress improvements)

## Problem Statement

Currently, LLM agents and human developers use different approaches to run tests with TestRunner.exe:

**SkylineTester approach (human-driven):**
- Developer selects tests in SkylineTester UI (Tests tab)
- SkylineTester writes selected tests to `SkylineTester test list.txt`
- Uses `test="@SkylineTester test list.txt"` to run the list
- Logs to `SkylineTester.log` and `SkylineTester Results/` directory
- Developer copies command-line from Output tab → Visual Studio debug args

**Run-Tests.ps1 approach (LLM-driven):**
- LLM specifies tests directly as comma-separated list on command-line
- Logs to files in `bin\x64\Debug\` directory (same folder as TestRunner.exe)
- No shared test list file with SkylineTester
- Test management is ad-hoc, challenging to maintain consistency across sprint

**Friction points:**
1. No shared test list between human and LLM workflows
2. Different logging locations make it hard to compare results
3. LLM can't easily "run the tests I selected in SkylineTester"
4. Developer can't easily hand off a test set to LLM for a sprint
5. Log files scattered across different locations

## Goals

1. **Shared test list**: Enable Run-Tests.ps1 to read/write `SkylineTester test list.txt`
2. **Bidirectional UI sync**: SkylineTester automatically checks tests from `SkylineTester test list.txt` on startup
3. **Consistent logging**: Use predictable, human-friendly log locations
4. **Seamless handoff**: Developer selects tests in SkylineTester → LLM runs same tests
5. **Reverse handoff**: LLM can update test list → Developer sees tests pre-checked in SkylineTester
6. **Workflow persistence**: Test selections persist across SkylineTester restarts and LLM sessions
7. **Discoverability**: Clear documentation of the integration pattern

## Proposed Solution

### 1. Enhance Run-Tests.ps1 with Test List File Support

**New parameter:** `-UseTestList`
```powershell
# Run tests from SkylineTester test list.txt
.\ai\Run-Tests.ps1 -UseTestList

# Run specific tests and UPDATE SkylineTester test list.txt
.\ai\Run-Tests.ps1 -TestName "TestA,TestB,TestC" -UpdateTestList
```

**Behavior:**
- `-UseTestList`: Read tests from `SkylineTester test list.txt`, run with TestRunner.exe
- `-UpdateTestList`: Write specified tests to `SkylineTester test list.txt` before running
- Default (no flags): Current behavior (tests specified directly, no test list file)

### 2. Standardize Logging Locations

**Proposed structure:**
```
pwiz_tools\Skyline\
├── SkylineTester.log                    # SkylineTester UI log
├── SkylineTester test list.txt          # Shared test list (human + LLM)
├── SkylineTestsAI.log                   # LLM test execution log
├── SkylineTester Results\               # Test results (human + LLM)
│   ├── [timestamp]_TestName.log         # Individual test logs
│   └── ...
└── bin\x64\Debug\
    └── TestRunner.exe
```

**Changes:**
- Run-Tests.ps1 logs to `SkylineTestsAI.log` (not `bin\x64\Debug\*.log`)
- Run-Tests.ps1 writes results to `SkylineTester Results\` (same as SkylineTester)
- Consistent with SkylineTester's `results="...\SkylineTester Results"` parameter

### 3. SkylineTester Auto-Restore (Prerequisite - Separate TODO)

**Status:** ✅ **Must be completed first** (see TODO-skyline_tester_auto_restore.md)

SkylineTester auto-restore feature provides:
- Tests from `SkylineTester test list.txt` automatically checked on startup
- Foundation for bidirectional sync with Run-Tests.ps1
- "Check Failed Tests" workflow persistence across sessions

**This TODO assumes SkylineTester auto-restore is already merged.**

### 4. Integration Workflows

#### Workflow A: Human → LLM Handoff
```
1. Developer selects tests in SkylineTester (Tests tab)
2. SkylineTester writes to SkylineTester test list.txt (existing behavior)
3. Developer tells LLM: "Run the tests in the SkylineTester test list"
4. LLM runs: .\ai\Run-Tests.ps1 -UseTestList
5. Results appear in SkylineTester Results\ directory
6. Developer can review in SkylineTester or via log files
```

#### Workflow B: LLM → Human Handoff
```
1. LLM identifies tests to run: "TestA, TestB, TestC"
2. LLM runs: .\ai\Run-Tests.ps1 -TestName "TestA,TestB,TestC" -UpdateTestList
3. LLM reports: "I've updated SkylineTester test list.txt with 3 tests"
4. Developer opens SkylineTester → tests AUTOMATICALLY checked (new behavior)
5. Developer can review, modify, or re-run the same set
```

#### Workflow C: Sprint Test Set Management
```
1. Developer curates test set for sprint in SkylineTester
2. SkylineTester writes to SkylineTester test list.txt (existing behavior)
3. Throughout sprint, LLM runs: .\ai\Run-Tests.ps1 -UseTestList
4. Developer can close/reopen SkylineTester anytime → tests remain checked
5. Consistent test validation without re-specifying tests each time
```

#### Workflow D: Iterative Debugging with Failed Tests (NEW)
```
1. Developer runs tests in SkylineTester, some fail
2. Developer clicks "Check Failed Tests" button → only failed tests checked
3. SkylineTester updates SkylineTester test list.txt with failed tests
4. Developer closes SkylineTester, makes fixes in code
5. Developer reopens SkylineTester → failed tests AUTOMATICALLY re-checked
6. Developer runs tests again to verify fixes
7. (Optional) LLM can also run same failed tests: .\ai\Run-Tests.ps1 -UseTestList
```

### 5. Implementation Plan

#### Phase 1: Run-Tests.ps1 - Test List File Support ✅ COMPLETED
- [x] Add `-UseTestList` switch parameter
- [x] Add `-UpdateTestList` switch parameter
- [x] Read tests from `SkylineTester test list.txt` when `-UseTestList` specified
- [x] Write tests to `SkylineTester test list.txt` when `-UpdateTestList` specified
- [x] Validate file format (one test per line, comments with #)
- [x] Backup existing file before overwriting (when `-UpdateTestList`)

#### Phase 2: Run-Tests.ps1 - Standardize Logging (Deferred)
- [ ] Change default log location from `bin\x64\Debug\*.log` to `SkylineTestsAI.log`
- [ ] Add `-LogFile` parameter (optional, defaults to `SkylineTestsAI.log`)
- [ ] Use `results="SkylineTester Results"` parameter for TestRunner.exe
- [x] Report log file path at end of execution (already implemented)

Note: Current logging behavior in `bin\x64\Debug\` is working well, deferring standardization.

#### Phase 3: Run-Tests.ps1 - Enhanced Output ✅ COMPLETED
- [x] Report which test list file was used (if any)
- [x] Show count of tests read from file
- [x] Warn if `SkylineTester test list.txt` doesn't exist (when `-UseTestList`)
- [x] Confirm when test list file is updated (when `-UpdateTestList`)
- [x] Show backup file path when test list is updated

#### Phase 4: Documentation ✅ COMPLETED
- [x] Update `pwiz_tools/Skyline/ai/README.md` with integration patterns
- [x] Document `SkylineTester test list.txt` format and bidirectional sync
- [x] Add examples of `-UseTestList` and `-UpdateTestList` usage
- [x] Document "Check Failed Tests" → LLM workflow
- [x] Update `ai/docs/build-and-test-guide.md` with workflow examples

## Technical Details

### SkylineTester test list.txt Format

**Example:**
```
# SkylineTester test list
# Generated: 2025-11-07 14:23:45
TestPanoramaDownloadFile
TestLibraryBuildNotification
CodeInspection
# TestSlowPerformance  # Commented out - too slow for sprint
```

**Format rules:**
- One test name per line
- Lines starting with `#` are comments (ignored by TestRunner.exe)
- Blank lines are ignored
- Test names should match `[TestMethod]` names exactly

### TestRunner.exe Parameters

**Current SkylineTester command-line:**
```bash
TestRunner.exe status=on \
  results="SkylineTester Results" \
  offscreen=False \
  loop=1 \
  runsmallmoleculeversions=on \
  language=en-US,zh-CHS,fr-FR \
  perftests=on \
  test="@SkylineTester test list.txt"
```

**Proposed Run-Tests.ps1 command-line (with `-UseTestList`):**
```bash
TestRunner.exe status=on \
  results="SkylineTester Results" \
  offscreen=on \
  language=en \
  test="@SkylineTester test list.txt" \
  > SkylineTestsAI.log 2>&1
```

**Key differences:**
- `offscreen=on` (LLM doesn't need UI)
- `language=en` by default (can be overridden with `-Language` parameter)
- Simpler defaults (no `loop`, `runsmallmoleculeversions`, `perftests` unless specified)
- Output redirected to `SkylineTestsAI.log`

## Success Criteria

### Run-Tests.ps1
- [x] Can read `SkylineTester test list.txt` with `-UseTestList`
- [x] Can write `SkylineTester test list.txt` with `-UpdateTestList`
- [x] Backs up existing file before overwriting (with timestamp)
- [ ] Logs written to `SkylineTestsAI.log` by default (deferred - current behavior works)
- [ ] Results written to `SkylineTester Results\` directory (deferred)
- [x] Backward compatible: existing Run-Tests.ps1 usage still works

### Integration Workflows
- [x] Workflow A works: Developer selects in UI → LLM runs same tests
- [x] Workflow B works: LLM specifies tests → Developer sees them pre-checked
- [x] Workflow C works: Sprint test set persists across sessions (human + LLM)
- [x] Workflow D works: "Check Failed Tests" → close → fix → reopen → still checked

### Documentation
- [x] All 4 workflows documented with examples
- [x] Test list file format documented
- [x] `-UseTestList` and `-UpdateTestList` parameters documented
- [x] SkylineTester auto-restore behavior documented

## Non-Goals (Future Work)

- Modifying SkylineTester UI to show LLM test runs (out of scope)
- Real-time synchronization between LLM and SkylineTester (not needed)
- Parsing SkylineTester.log to extract developer preferences (too complex)
- Adding SkylineTester features to Run-Tests.ps1 (loop, offscreen control, etc.)

## Open Questions

1. **Test list file location**: Should it always be `SkylineTester test list.txt`, or support custom paths?
   - Recommendation: Default to `SkylineTester test list.txt`, add optional `-TestListFile` parameter

2. **Test list file ownership**: What if both human and LLM try to update simultaneously?
   - Recommendation: Document that test list file is a handoff mechanism, not concurrent

3. **Log file rotation**: Should `SkylineTestsAI.log` be rotated/archived automatically?
   - Recommendation: Simple append for now, manual cleanup (add to documentation)

4. **Language parameter**: Should `-UseTestList` also read language preferences from somewhere?
   - Recommendation: Keep simple - LLM specifies language explicitly, defaults to `en`

5. **Results directory**: Should LLM results be in a subdirectory of `SkylineTester Results\`?
   - Recommendation: Same directory, timestamped logs distinguish human vs LLM runs

## Files to Modify

### PowerShell Scripts
- `pwiz_tools/Skyline/ai/Run-Tests.ps1` - Add test list file support, standardize logging

### Documentation
- `pwiz_tools/Skyline/ai/README.md` - Document integration workflows
- `pwiz_tools/Skyline/ai/PRE-COMMIT.md` - Add integration pattern examples
- `ai/docs/build-and-test-guide.md` - Update with new Run-Tests.ps1 parameters, all 4 workflows
- `ai/WORKFLOW.md` (optional) - Brief mention of test list integration

## Example Usage

### Current Usage (Still Supported)
```powershell
# Run specific test
.\ai\Run-Tests.ps1 -TestName CodeInspection

# Run multiple tests
.\ai\Run-Tests.ps1 -TestName "TestA,TestB,TestC"

# Run with specific language
.\ai\Run-Tests.ps1 -TestName CodeInspection -Language ja
```

### New Usage (Test List Integration)
```powershell
# Use tests from SkylineTester test list.txt
.\ai\Run-Tests.ps1 -UseTestList

# Use tests from custom file
.\ai\Run-Tests.ps1 -UseTestList -TestListFile "my-sprint-tests.txt"

# Update SkylineTester test list with new tests, then run
.\ai\Run-Tests.ps1 -TestName "TestA,TestB" -UpdateTestList

# Check what's in the test list without running
.\ai\Run-Tests.ps1 -UseTestList -WhatIf  # (future enhancement)
```

## Benefits

1. **Reduced cognitive load**: Developer doesn't need to remember/specify test names
2. **True bidirectional sync**: Test selections persist across both SkylineTester and LLM sessions
3. **Consistency**: Same test set runs in SkylineTester and via LLM
4. **Visibility**: Developer can see what tests LLM ran (automatically checked in SkylineTester UI)
5. **Efficiency**: One-time test selection in SkylineTester → multiple LLM runs
6. **Workflow persistence**: "Check Failed Tests" → close → fix → reopen → tests still checked
7. **Traceability**: Shared log locations make it easy to compare human vs LLM results
8. **Flexibility**: Both workflows remain independent, integration is opt-in
9. **Zero learning curve**: Automatic behavior, no new UI or commands to learn

## Risks & Mitigations

**Risk**: Developer expects SkylineTester UI to show live LLM test progress
- **Mitigation**: Document that integration is file-based handoff, not real-time sync

**Risk**: Test list file gets corrupted by LLM writing invalid format
- **Mitigation**: Run-Tests.ps1 validates format before writing, backs up existing file

**Risk**: Confusion about which log file to check (SkylineTestsAI.log vs SkylineTester.log)
- **Mitigation**: Clear documentation, Run-Tests.ps1 reports log file path at end

**Risk**: Breaking existing Run-Tests.ps1 usage
- **Mitigation**: New parameters are opt-in, existing usage remains unchanged

## Notes

- This integration leverages existing `SkylineTester test list.txt` format - minimal changes to SkylineTester
- SkylineTester already writes to the file when tests are checked/unchecked - we just add auto-restore on startup
- TestRunner.exe already supports `test="@filepath"` syntax - we're just exposing it to LLMs
- Log locations align with SkylineTester conventions - feels native to developers
- File-based handoff is simple, robust, and doesn't require process coordination
- The auto-restore feature makes "Check Failed Tests" workflow much more powerful (survives SkylineTester restarts)

## Dependencies

**Prerequisites (must be completed first):**
1. TODO-skyline_tester_auto_restore.md - SkylineTester auto-restore feature
2. TODO-20251107_httpclient_to_progress.md merged to master - Run-Tests.ps1 improvements

**This TODO should only be started after both prerequisites are merged.**

## Related Work

- TODO-skyline_tester_auto_restore.md - **Prerequisite** - SkylineTester auto-restore (Phase 1)
- TODO-20251107_httpclient_to_progress.md - **Prerequisite** - Run-Tests.ps1 improvements
- PR #3667 - Build/test automation tooling foundation
- `ai/docs/build-and-test-guide.md` - Current test execution documentation
