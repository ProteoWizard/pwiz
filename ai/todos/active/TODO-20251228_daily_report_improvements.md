# Daily Report System Improvements

**Branch**: ai-context
**Created**: 2025-12-28
**Status**: Active

## Context

This TODO captures improvements to the scheduled daily analysis system (`/pw-daily`). The system generates daily reports on nightly tests, exceptions, and support board activity.

## Completed (2025-12-28)

- [x] Added inbox emails as primary data source (nightly, exceptions, hang alerts)
- [x] Documented email color coding (green/yellow/red/gray) in nightly-tests.md
- [x] Added email archiving step to keep inbox clean
- [x] Added self-improvement reflection step
- [x] Historical JSON storage for trend analysis (Step 6, 8 in /pw-daily)

## Completed (2025-12-30)

- [x] Pattern detection MCP tools (`analyze_daily_patterns`, `save_daily_summary`)
- [x] Prioritized Action Items output (SYSTEMIC, NEW, EXTERNAL, MISSING, RESOLVED)
- [x] Updated `/pw-daily` Steps 6 and 8 to use new MCP tools
- [x] Stack trace normalization utility (`stacktrace.py`) - internal module, no MCP tools

## Completed (2025-12-29)

- [x] Added `fetch_labkey_page` MCP tool for authenticated web page access
- [x] Effort level modes (quick/standard/deep) for controlling session depth
- [x] Execution logging (Step 13) - writes to `ai/.tmp/logs/daily-session-YYYYMMDD.md`
- [x] Developer email learning loop (Step 11) - analyzes forwarded developer emails as training signal
- [x] Regression investigation step (Step 10) - git blame for new failures

## Backlog

### Tier 1: High Value, Achievable Now

#### 1. ~~Level 1 Automation - Pattern Detection~~ ✅ COMPLETED 2025-12-30
**Problem**: Reports show data but don't highlight patterns requiring immediate attention
**Solution**: Add pattern detection to daily report:
- Detect NEW failures (not in yesterday's run)
- Detect ALL-MACHINES-AFFECTED pattern → flag for immediate attention
- Flag tests involving known external services (Koina, Panorama)
- Track "expected fixes" and verify next day
**Implementation**: Created `patterns.py` MCP module with `analyze_daily_patterns` and `save_daily_summary` tools

#### 2. ~~Parse Installation ID from Exceptions~~ ✅ COMPLETED 2025-12-31
**Problem**: Cannot distinguish "1 user hit this 4 times" from "4 users hit this once each"
**Solution**: Parse Installation ID from exception email HTML
**Implementation**: Regex for `Installation ID: XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX`
**Value**: Prioritize bugs blocking multiple users vs one-off issues
**Completed**: Enhanced `save_exceptions_report` with full exception history tracking (see Progress Log)

#### 3. ~~Stack Trace Normalization for Pattern Matching~~ ✅ COMPLETED 2025-12-30
**Problem**: Same underlying bug produces slightly different stack traces due to:
- Line number changes between versions
- Minor call stack variations (async state machines, lambda wrappers)
- Different entry points reaching same buggy code
This makes it hard to correlate:
- Nightly test failures with the same root cause
- User exceptions matching known test failures
- Recurring issues across versions

**Solution**: Create a normalization function that produces a "fingerprint" from stack traces:
1. Strip line numbers (e.g., `File.cs:123` → `File.cs`)
2. Remove async state machine frames (`MoveNext`, `d__0`)
3. Collapse lambda/closure frames
4. Extract top N "signature" frames (the meaningful ones)
5. Hash the normalized result for fast comparison

**Implementation**: Add to `ai/mcp/LabKeyMcp/tools/`:
```python
def normalize_stack_trace(raw_trace: str) -> dict:
    """Returns {
        'fingerprint': 'abc123...',  # Hash for fast matching
        'signature_frames': ['Namespace.Class.Method', ...],  # Top frames
        'normalized': '...'  # Full normalized trace
    }"""
```

**Use cases**:
- `analyze_daily_patterns`: Group test failures by fingerprint, not exact trace
- `save_exceptions_report`: Dedupe exceptions with same fingerprint
- Cross-reference: "This user exception matches TestFoo failure fingerprint"

**Value**:
- Dramatically reduce noise in daily reports
- Identify that 10 "different" exceptions are really 1 bug
- Connect production issues to test coverage gaps

**Implementation**: Created `ai/mcp/LabKeyMcp/tools/stacktrace.py` as internal utility (no MCP tools).
- `normalize_stack_trace()` returns `NormalizedTrace` dataclass with fingerprint, signature_frames, normalized text
- `fingerprint_matches()` convenience function for comparing two traces
- `group_by_fingerprint()` groups list of traces by their fingerprint
- Filters async noise (MoveNext, d__, AsyncMethodBuilder), framework frames
- Collapses lambdas (<Method>b__0 → Method) and closures

#### 12. ~~Test Failure Fingerprinting in Daily Reports~~ ✅ COMPLETED 2026-01-02
**Problem**: Test failures in daily reports lack context - no stack trace info, no clickable links, no fingerprint for correlation

#### 13. ~~Run Timing Comparison Tool~~ ✅ COMPLETED 2026-01-02
**Problem**: When test counts drop significantly, no easy way to identify which tests are responsible
**Solution**: Created `save_run_comparison(run_id_before, run_id_after, container_path)` MCP tool
- Uses existing `compare_run_timings` query deployed on skyline.ms
- Categorizes tests as NEW, REMOVED, SLOWDOWN (>50%), SPEEDUP (>20%)
- Ranks by total time impact to highlight biggest contributors
- Saves detailed report to `ai/.tmp/run-comparison-{before}-{after}.md`
**Value**: Immediately identifies root cause of test count anomalies (e.g., TestImportHundredsOfReplicates +2524%)

**Solution** (for item 12): For each failing test:
1. Call `get_run_failures(run_id)` to fetch the stack trace
2. Generate fingerprint using existing `stacktrace.py` normalization
3. Extract brief exception info (type, message, location)
4. Include clickable URL to skyline.ms failure view
5. Store fingerprint in daily summary JSON for historical tracking

**Email format enhancement**:
```html
<li><strong>TestPeakAreaRelativeAbundanceGraph</strong> - KAIPOT-PC1
  <br><code>Assert.AreEqual: Expected:&lt;0&gt;. Actual:&lt;125&gt;</code>
  <br>at PeakAreaRelativeAbundanceGraphTest.cs:472
  <br><a href="https://skyline.ms/...">View full stack trace</a>
</li>
```

**Enhanced JSON schema**:
```json
{
  "nightly_failures": {
    "TestName": {
      "computers": ["COMPUTER1"],
      "fingerprint": "a3f8b2c1...",
      "exception_type": "AssertFailedException",
      "exception_brief": "Assert.AreEqual: Expected:<0>. Actual:<125>",
      "location": "File.cs:472"
    }
  }
}
```

**URL pattern**: `https://skyline.ms/home/development/{folder}/testresults-showFailures.view?end={MM}%2F{DD}%2F{YYYY}&failedTest={TestName}`

**Value**:
- Immediate actionability - see exception without clicking through
- Historical correlation - "this fingerprint has failed 5 times in 7 days"
- Pattern detection - identify flaky tests vs new regressions by fingerprint

### Tier 2: High Value, Requires C# Code Changes

#### 4. Automated Stack Trace Capture for Hangs
**Problem**: Hang alerts notify developers to RDP and attach debuggers, but developers rarely catch it in time
**Solution**: Have SkylineNightly automatically capture thread/stack traces from hung TestRunner.exe
**Implementation options**:
- ProcDump.exe (SysInternals) to capture minidump
- ClrMD (Microsoft.Diagnostics.Runtime) for managed stack traces
- Post extracted traces to LabKey
**Value**: Transforms hang alerts from "notification to act" to "automated diagnosis"
**Files**: SkylineNightly project

#### 5. TestRunner Flush Logging for Hung Test Identification
**Problem**: Hang alert emails show last *completed* test, not the *hung* test (log only flushes on line completion)
**Solution**: Flush test name immediately when starting:
```
[Flush] "Starting: TestFoo"
... test runs ...
[Flush] "Completed: TestFoo (passed, 45s)"
```
**Files**: TestRunner.exe output code
**Value**: Know which test is hung immediately, not after 8 AM report

#### 6. ~~Historical Regression Detection~~ ✅ COMPLETED 2026-01-09
**Problem**: Intermittent failures can persist for months before someone traces when they started (e.g., June bug found in October)
**Solution**: For each failing test, query past 30/90/365 days to find introduction date
**Output format**:
```
TestFoo - INTERMITTENT REGRESSION DETECTED
- First failure: 2025-06-15 (after commit abc123)
- Failure rate: 12% (47 failures in 390 runs since June)
- Before June: 0 failures in 180 runs
- Recommendation: Investigate commit abc123
```
**Value**: Could have caught June→October bug months earlier

**Implementation**: See Progress Log 2026-01-09 for full plan. Creating `nightly_history.py` module modeled after `exceptions.py` with:
- `backfill_nightly_history(since_date)` - One-time backfill from LabKey
- `query_test_history(test_name)` - Look up specific test's history
- `record_test_fix(test_name, fingerprint, pr_number)` - Record fixes
- History file: `ai/.tmp/history/nightly-history.json`

### Tier 3: Medium Value

#### 7. ~~Track Recurring Missing Computers~~ ⚠️ PARTIAL 2026-01-02
**Problem**: Same computers missing for multiple days with no investigation
**Solution**: Track "days missing" and escalate after threshold (e.g., 3 days)
**Depends on**: Historical JSON (now available)
**Implementation**: Created `computers.py` MCP module - `list_computer_status` works, but `deactivate/reactivate` blocked by admin-only permission on `setUserActive` endpoint (requires LabKey Server testresults module change)

#### 8. Date Boundary Verification
**Problem**: Occasional mismatch between email data and MCP data due to different time windows
**Solution**: Log explicit date ranges from each source, flag discrepancies
**Value**: Accuracy and trust in reports

#### 9. Git Integration for Developer Attribution
**Problem**: Reports show failures but don't identify who should investigate
**Solution**:
- Get commit author from git hash: `git show --format='%an <%ae>' -s <hash>`
- Map stack trace files to code owners
- Suggest "@developer should investigate"
**Value**: Actionable recommendations, not just data

### Tier 4: Low Priority / Quick Fixes

#### 10. Verify Leak Type Distinction
**Problem**: Reports say "leak" but email distinguishes "Handle leak" vs "Memory and handle leak"
**Solution**: Verify `leak_type` from `leaks_by_date` query is surfaced in reports
**Status**: Data already available, just needs verification

#### 11. Standardize Email Subject Format
**Problem**: Subject lines vary slightly ("Daily Summary" vs "Daily Report")
**Solution**: Use consistent format: `Skyline Daily Summary - Month DD, YYYY`
**Priority**: Cosmetic

## Future Ideas (Not Currently Planned)

### Browser Automation for Visual Analysis
Claude Code can analyze images but cannot acquire them autonomously. A browser MCP like `superpowers-chrome` would enable:
- Navigate to skyline.ms test history pages
- Capture failure history graphs
- Autonomous visual regression detection

**Status**: Requires external dependency, deferred

### Investigation Patterns Reference
The following patterns from developer reports represent ideal automated investigation:

| Pattern | Trigger | Automation Feasibility |
|---------|---------|------------------------|
| New widespread failure | Test failing on all machines | High - compare with yesterday |
| Intermittent regression | Test failing periodically | High - query historical data |
| Cascading leak effect | All machines show same leak | High - detect unanimous pattern |
| External service issue | Koina/Panorama tests fail | Medium - known service list |
| Fix verification | Expected fix committed | High - track pending fixes |

## Progress Log

### 2026-01-09: Nightly Test History Implementation Plan

**Goal**: Create historical tracking for nightly test failures, leaks, and hangs - mirroring what we built for exceptions in `exception-history.json`.

**Problem with current 30-day dashboard**:
- A test that fails on all machines for 2 days creates 20 failures
- Those 20 failures dominate "Top Failures" for the next 28 days
- No way to mark something as "fixed" to clear the noise
- Result: Developers learn to ignore the Top Failures table

**What fix tracking enables**:
- ACTIVE issues: Failing recently with no fix recorded
- CHRONIC issues: Low-grade failures over months (flaky tests)
- RESOLVED issues: Fixed and staying fixed (hidden by default)
- REGRESSIONS: Fixed but came back (high priority alert)

#### Schema: `ai/.tmp/history/nightly-history.json`

```json
{
  "_schema_version": 1,
  "_last_updated": "2026-01-09",
  "_backfill_start": "2025-01-09",

  "test_failures": {
    "TestPeakPickingTutorial": {
      "by_fingerprint": {
        "a3f8b2c1e4d7...": {
          "fingerprint": "a3f8b2c1e4d7...",
          "signature": "CheckPointsTypeRT → RunUI → MarshaledInvoke → Invoke",
          "exception_type": "NullReferenceException",
          "exception_brief": "Object reference not set to an instance of an object",
          "location": "PeakPickingTutorialTest.cs:544",
          "first_seen": "2025-06-15",
          "last_seen": "2026-01-09",
          "reports": [
            {
              "run_id": 79897,
              "date": "2026-01-09",
              "computer": "BRENDANX-UW5",
              "folder": "Nightly x64",
              "git_hash": "abc123..."
            }
          ],
          "fix": null
        }
      }
    }
  },

  "test_leaks": {
    "TestMethodRefinementTutorial": {
      "first_seen": "2025-08-01",
      "last_seen": "2026-01-09",
      "reports": [
        {
          "run_id": 79905,
          "date": "2026-01-09",
          "computer": "EKONEIL01",
          "folder": "Nightly x64",
          "handle_leak": null,
          "memory_leak_bytes": 315977
        }
      ],
      "fix": null
    }
  },

  "test_hangs": {
    "TestAssayLibraryImportAsSmallMolecules": {
      "first_seen": "2025-10-15",
      "last_seen": "2026-01-07",
      "reports": [
        {
          "run_id": 79850,
          "date": "2026-01-07",
          "computer": "BRENDANX-UW7",
          "folder": "Performance Tests"
        }
      ],
      "fix": null
    }
  },

  "run_counts": {
    "TestPeakPickingTutorial": {
      "total": 2847,
      "by_machine": {
        "BRENDANX-UW5": 365,
        "KAIPOT-PC1": 340
      }
    }
  },

  "machine_health": {
    "BRENDANX-UW5": {
      "total_test_runs": 125000,
      "failures": 47,
      "leaks": 12,
      "hangs": 8,
      "last_seen": "2026-01-09"
    }
  }
}
```

#### Key Design Decisions

1. **Failures grouped by fingerprint** (like exceptions)
   - Same test can fail different ways → different fingerprints
   - Enables "this specific bug has failed 47 times"
   - Uses existing `stacktrace.py` normalization

2. **Leaks track both handle and memory separately**
   - `handle_leak`: float (average handles leaked)
   - `memory_leak_bytes`: int (bytes leaked)
   - Managed memory leaks are systemic (all machines) → easy to find
   - Handle leaks can be machine-specific → harder to find

3. **Hangs are machine-sensitive**
   - Faster machines more prone to race conditions
   - Track per-machine for pattern detection

4. **Run counts enable failure rate calculation**
   - Per-machine counts allow "47% on BRENDANX-UW5, 2% elsewhere"
   - Distinguishes flaky tests from sick machines

5. **Fix field on all three types** (failures, leaks, hangs)
   - `{"pr": "PR#1234", "date": "2026-01-05", "commit": "abc123"}`
   - Enables regression detection: "failed AFTER fix merged"

#### MCP Tools to Create

**File**: `ai/mcp/LabKeyMcp/tools/nightly_history.py`

1. **`backfill_nightly_history(since_date)`**
   - Query `failures_with_traces_by_date` for failures with stack traces
   - Query `leaks_by_date` for leak data
   - Query for hangs (check available queries)
   - Fingerprint failures using `stacktrace.py`
   - Build run counts from run data
   - Save to `nightly-history.json`

2. **`query_test_history(test_name)`**
   - Look up a specific test's full history
   - Return: fingerprints, failure rate, machines affected, fix status
   - Output: "47 failures (12% rate) since June 2025, last seen yesterday"

3. **`record_test_fix(test_name, fingerprint, pr_number, ...)`**
   - Record that a failure/leak/hang has been fixed
   - Similar to `record_exception_fix`

4. **Update `analyze_daily_patterns`**
   - Use nightly history for richer context
   - Change "NEW" to mean "first failure EVER or since last fix"
   - Add failure rate to output

#### LabKey Queries Needed

From existing queries (verify availability):
- `failures_with_traces_by_date` - failures with stack traces
- `leaks_by_date` - leak type and amounts
- `runs_by_date` - run counts for denominator
- Need to identify hang query (may need to create)

#### Implementation Steps

1. [x] Create `nightly_history.py` module structure
2. [x] Implement `_load_nightly_history()` / `_save_nightly_history()`
3. [x] Implement `backfill_nightly_history` - failures first
4. [x] Add leaks to backfill
5. [x] Add hangs to backfill
6. [x] Implement `query_test_history`
7. [x] Implement `record_test_fix`
8. [x] Update `analyze_daily_patterns` to use history ✅ 2026-01-09
9. [x] Test with real data, verify fingerprinting works (3,626 failures → 1,372 fingerprints)
10. [x] Register tools in `__init__.py`
11. [x] Add `query_test_history` to `Invoke-DailyReport.ps1` allowed tools

#### Server-Side Queries Created (2026-01-09)

- `failures_history.sql` - Deployed to all 6 test folders
- `leaks_history.sql` - Deployed to all 6 test folders
- `hangs_history.sql` - Deployed to all 6 test folders

#### Backfill Results (2026-01-09)

One year of history successfully backfilled:
- **Failures**: 3,626 records, 1,014 unique tests, 1,372 fingerprints
- **Leaks**: 5,197 records, 259 unique tests
- **Hangs**: 291 records, 102 unique tests

Top failing tests (external service dependent):
- TestKoinaBuildLibraryFromCsv: 144 failures (4 fingerprints) - Koina service
- TestPanoramaDownloadFile: 142 failures (7 fingerprints) - Panorama service
- TestKoinaConnection: 120 failures (2 fingerprints) - Koina service

#### `analyze_daily_patterns` Integration (2026-01-09)

Updated `patterns.py` to use nightly history for richer pattern analysis:

**New features**:
- Load nightly history at startup and show stats (e.g., "3,626 failures from 1,014 tests loaded")
- Enhanced failure categorization:
  - 🆕 **NEW** now means "first time ever in history" (truly new failures)
  - 🔄 **RECURRING** for failures seen before (with history stats)
  - ⚠️🔄 **REGRESSION** for failures after recorded fixes
  - ⏳ **CHRONIC** section for intermittent issues spanning 30+ days
- Historical context in action items (e.g., "[history: 47 failures since 2025-06-15]")
- Added `_get_test_history_context()` helper to extract:
  - Total failures, fingerprint count, first/last seen dates
  - Fix status and regression detection
  - Chronic flag (>30 days between first/last failure)

**Pattern Legend updated** (2026-01-09):
- 🔴 SYSTEMIC - 3+ machines affected
- 🆕 NEW - First time ever
- 🔄 RECURRING - Seen before, returned
- ⚠️🔄 REGRESSION - After recorded fix
- ⏳ CHRONIC - 30+ day intermittent
- 🌐 EXTERNAL - Network-dependent tests
- ⚠️ MISSING - Computers absent multiple days
- ✅ INTERMITTENT - Single incident, likely no fix needed
- ⏸️ CHECK PRs - Multi-day pattern stopped, search for fix

#### "Resolved" Section Reworked (2026-01-09)

**Problem**: The old "Resolved Since Yesterday" section was misleading. A test that failed yesterday and passed today was marked "resolved" even if it was just intermittent.

**Solution**: Renamed to "Not Failing Today" with historical context:
- If 1-day pattern: "likely intermittent" - don't waste time investigating
- If multi-day pattern that stopped: "check PRs for fix" - worth investigating

**Example**:
```
## 📉 Not Failing Today

- ✅ TestRInstaller (leak) - not leaking today, may be intermittent
- ⏸️ TestPeakAreaRelativeAbundanceGraph (failure) - had 7 failures from 2026-01-04 to 2026-01-08, check PRs for fix
```

**Future enhancement**: Automatically search `gh pr list --state merged --search "TestName"` to find if a PR was merged that might have fixed the issue.

#### Future Enhancement: Test Dependency Tags

**Idea**: Tag tests with their external dependencies to improve pattern analysis.

| Tag | Tests | Implication |
|-----|-------|-------------|
| `network-dependent` | Koina*, Panorama*, UniProt* | Expect intermittent failures from network/service issues |
| `locale-sensitive` | Tests that fail on specific languages | May need localization fixes |
| `perf-sensitive` | Tests with race conditions on fast machines | May need timing adjustments |

**Benefits**:
- Don't flag network failures as code regressions
- Group related failures in reports (e.g., "3 Koina tests failing - likely service issue")
- Prioritize investigation appropriately

### 2026-01-07
- **Session stats**: Scheduled automation hit max turns (30) after ~4 minutes. Email sent successfully but archiving and logging skipped.
- **Configuration fix needed**: Increase `maxTurns` to 50-75 in scheduled task settings for daily report workflow.

- **Session observations**:
  - TestPeakPickingTutorial failing on both trunk (BRENDANX-UW5) and release branch (KAIPOT-PC1) - continuing pattern from Jan 6
  - BRENDANX-UW7 hang detected during Performance Tests on TestAssayLibraryImportAsSmallMolecules - explains missing from Nightly x64
  - 9 exceptions reported, 8 unique issues - 2 related to retention time regression (Prediction.cs:939), same user hit twice
  - Pattern analysis couldn't run (no history for today) - expected, daily summary JSON now created for tomorrow's comparison

- **Votes for existing items**:
  | Item | Vote | Reasoning |
  |------|------|-----------|
  | #5 TestRunner Flush Logging | ⭐⭐⭐ | Hang alert for BRENDANX-UW7 showed "TestAssayLibraryImportAsSmallMolecules" was the COMPLETED test; would be helpful to know the hung test immediately |
  | #6 Historical Regression Detection | ⭐⭐ | TestPeakPickingTutorial and TestPeakAreaRelativeAbundanceGraph recurring - still no insight into when these started |
  | Same-Test-Both-Branches Detection | ⭐⭐ | TestPeakPickingTutorial failed on both branches again - validates Jan 6 idea about release cycle awareness |

- **Process improvement: Max turns configuration**
  The scheduled task needs higher `maxTurns` setting. Current 30 turns (~4 min) is insufficient for full workflow:
  - Email reading: 4-5 turns
  - MCP report generation: 3 turns
  - Failure detail fetching: 5-7 turns
  - Daily summary save: 1 turn
  - Email sending: 1 turn
  - Archiving, logging, TODO update: 5-10 turns

  Recommend `maxTurns: 75` for standard mode daily reports.

### 2026-01-06
- **Bug found**: `analyze_daily_patterns` output is malformed - shows dictionary keys instead of computer names:
  ```
  🔴 SYSTEMIC FAILURE: TestPeakPickingTutorial (failing on 5 machines: computers, exception_brief, exception_type, fingerprint, location)
  ```
  Should show actual computers like `KAIPOT-PC1, BRENDANX-UW5`. Fix needed in `patterns.py`.

- **Session observations**:
  - Inbox emails were from Jan 2-4 but report was for Jan 6 - date boundary confusion (supports Item #8)
  - TestPeakPickingTutorial failed on BOTH trunk and release branch - significant since FEATURE COMPLETE just released (branches are nearly identical code)
  - Multiple commits in same nightly window (9ddd1acdf vs edaac36bb) - could compare pass/fail by commit cohort
  - BRENDANX-UW6 and DSHTEYN-DEV01 missing 7+ days with no alarms (Item #7 blocked by permissions)

- **Votes for existing items**:
  | Item | Vote | Reasoning |
  |------|------|-----------|
  | #8 Date Boundary Verification | ⭐⭐⭐ | Hit this today - inbox emails from days ago created confusion about which data was fresh vs stale |
  | #6 Historical Regression Detection | ⭐⭐⭐ | TestPeakPickingTutorial and TestPeakAreaRelativeAbundanceGraph are recurring - knowing "first failed Dec 15, 12% failure rate" would help prioritize |
  | #4 Automated Stack Trace Capture | ⭐⭐ | Performance Tests had a hang (TestPeakBoundaryCompare) - automatic capture would eliminate manual RDP triage |

- **New idea: Same-Test-Both-Branches Detection with Release Cycle Awareness**

  **Problem**: When the same test fails on both trunk (Nightly x64) and release branch, the significance depends on where we are in the release cycle. Today, TestPeakPickingTutorial failed on both - but this happened the day after FEATURE COMPLETE (26.0.9.004), when the branches are nearly identical code.

  **Release cycle context** (from `ai/docs/release-guide.md`):
  - **Just after FEATURE COMPLETE**: Branches are nearly identical (delta ≈ 0). Same-test failures indicate fundamental issues in the shared codebase - high priority.
  - **Mid-cycle stabilization**: Release branch stabilizes while master accumulates new features. Same-test failures could be coincidence (different root causes) - medium priority.
  - **Just before next FEATURE COMPLETE**: Branches maximally diverged. Same-test failures are likely unrelated issues - lower priority for correlation.

  **Solution**: When detecting same-test-both-branches:
  1. Query the commit delta between master HEAD and release branch HEAD
  2. Estimate release cycle phase: "FEATURE COMPLETE +N days" or "~M weeks until next release"
  3. Adjust priority/messaging based on phase:
     - Early: "⚠️ BOTH BRANCHES: TestFoo - branches nearly identical, likely fundamental issue"
     - Mid: "📊 BOTH BRANCHES: TestFoo - branches diverged, may be different root causes"
     - Late: "ℹ️ BOTH BRANCHES: TestFoo - branches maximally diverged"

  **Implementation**: Add to `analyze_daily_patterns`:
  - Track which folder each failure comes from
  - Detect intersection of failures between "Nightly x64" and "Release Branch"
  - Query release branch creation date or commit count delta to estimate cycle phase

- **New idea: Commit Cohort Analysis**

  **Problem**: Nightly runs build from different commits depending on when they started. Today's runs split between `9ddd1acdf` (earlier) and `edaac36bb` (later). A test that fails on the old commit but passes on the new commit suggests a fix was merged between them.

  **Solution**: Group runs by git hash and compare:
  - "TestFoo: FAILED on 9ddd1acdf (3 machines), PASSED on edaac36bb (2 machines) → likely fixed by commits between"
  - Could even identify the specific commits: `git log 9ddd1acdf..edaac36bb --oneline`

  **Value**: Identifies fix verification automatically - "this failure is a stale echo from older commits"

  **Implementation**:
  - `analyze_daily_patterns` already has access to git hashes per run
  - Group failures by hash, compare pass/fail patterns
  - Flag "stale echo" when newer commits show passing

### 2026-01-03
- Enhanced Exception History with Schema v2:
  - **Problem**: Large LabKey queries (25,000+ tokens) were burning context to answer simple questions like "which report has this email?"
  - **Solution**: Store richer, normalized data locally so queries can be answered from history file
  - Schema v2 stores individual reports with:
    - `row_id` for direct URL generation (no more guessing URLs)
    - `email` per report (not just aggregated)
    - `comment` - user's description normalized to single line (max 300 chars)
    - `reply` - developer response text, date, and author
  - URL template fixed: `announcements-thread.view` (not `details.view`)
  - Reply matching required EntityId (not RowId) - Parent field uses GUIDs
  - Fix annotation preservation during backfill migration (2 existing fixes retained)
  - Updated `query_exception_history` output:
    - Shows user comments under each email contact
    - Shows developer replies with 💬 marker and full text
    - Direct clickable URLs to each report
  - Backfill stats: 703 exceptions, 31 replies found, 21 bugs with replies, 38 with user comments
  - **Value**: Can now answer "what did the developer reply to this user?" without any LabKey query

### 2026-01-02 (continued)
- Implemented Test Failure Fingerprinting (Item 12): ✅ COMPLETED
  - Goal: Include exception fingerprints, brief error info, and clickable URLs in daily report emails
  - Data validation: `get_run_failures(run_id)` returns full stack traces with exception messages
  - URL pattern confirmed: `testresults-showFailures.view?end={date}&failedTest={TestName}`
  - Updated `/pw-daily` workflow:
    - Added Step 5 (Fetch Failure Details and Fingerprints) - calls `get_run_failures()` for each failing run
    - Added Enhanced Test Failure Format section with HTML template showing exception details and clickable URLs
    - Updated Step 9 (Save Daily Summary) with enhanced JSON schema including fingerprint, exception_type, exception_brief, location
  - Updated `daily-summary-20260102.json` with enhanced failure data as example:
    - TestPeakAreaRelativeAbundanceGraph: `Assert.AreEqual failed. Expected:<0>. Actual:<125>` at line 472
    - TestPeakPickingTutorial: `NullReferenceException: Object reference not set` at line 544
  - Tomorrow's automated report will follow this enhanced workflow

- Implemented `save_run_comparison` MCP tool (Item 13): ✅ COMPLETED
  - Purpose: Compare test durations between two runs to identify slowdowns, new tests, removed tests
  - Use case: When daily report shows significant test count drop, compare baseline vs changed run
  - Query: Uses `compare_run_timings` parameterized query (already deployed on skyline.ms)
  - Output: Saves to `ai/.tmp/run-comparison-{before}-{after}.md` with:
    - Summary table (total time before/after, net change, counts by category)
    - Major slowdowns section (>50% slower)
    - New/removed tests sections
    - Top impact analysis (which tests contributed most to time change)
  - Example: Identified `TestImportHundredsOfReplicates` as cause of 12/15 test count drop
    - Run 79482 (12/14): 9,507 tests, 136s avg duration
    - Run 79497 (12/15): 8,391 tests, 3,568s avg duration (+2524%)
    - This single test consumed ~57 extra minutes per pass, crowding out ~1,100 other tests
  - Updated `/pw-daily` Follow-up Investigation section with usage documentation

### 2026-01-02
- Implemented Computer Status Management (Item 7):
  - Created `ai/mcp/LabKeyMcp/tools/computers.py` with 4 MCP tools:
    - `deactivate_computer(computer_name, reason, alarm_date, alarm_note)` - Flips `userdata.active=false` in LabKey
    - `reactivate_computer(computer_name)` - Flips `userdata.active=true` in LabKey
    - `list_computer_status(container_path)` - Shows active/inactive computers with alarm info
    - `check_computer_alarms()` - Shows due/upcoming reactivation reminders
  - Created `all_computers.sql` server-side query (LEFT OUTER JOIN user+userdata, includes active flag)
  - Uses LabKey's existing `testresults-setUserActive.view` API endpoint
  - Reuses `LabKeySession` from wiki.py for CSRF token handling
  - Local alarm tracking in `ai/.tmp/history/computer-status.json`
  - **BLOCKED**: `setUserActive` endpoint requires admin permission (even full editor can't call it)
    - Requires LabKey Server testresults module modification to change permission level
    - `list_computer_status` works (read-only), deactivate/reactivate blocked until permissions fixed
  - Use case examples: BRENDANX-UW6 (tutorial screenshots), DSHTEYN-DEV01 (debugging)

### 2026-01-01
- Fixed localized stack trace parsing - now parses 100% of exceptions (was 21% unparseable):
  - Refactored `LOCALE_KEYWORDS` table in `stacktrace.py` for maintainability
  - Added 15 locales: English, Chinese-Simplified, Chinese-Traditional, Japanese, German, French, Spanish, Turkish, Korean, Russian, Czech, Italian, Portuguese, Polish, Hebrew
  - Fixed regex bug: `\s+` in optional file/line group was matching newlines, causing one frame's match to consume the next line; changed to `[^\S\n]+`
  - Fixed LabKey QueryFilter: `isblank` filter requires empty string `""` not `None`
  - Added unparseable RowId tracking to `backfill_exception_history` for debugging

### 2025-12-31
- Enhanced Exception Reporting with Stack Trace Normalization and History Tracking:
  - Applied `stacktrace.py` normalization to `save_exceptions_report`
  - Added `Parent IS NULL` filter to exclude developer responses from exception counts
  - Implemented persistent exception history (`ai/.tmp/history/exception-history.json`):
    - Tracks unique bugs by fingerprint across days/weeks/months
    - Tracks unique users (Installation ID) per fingerprint
    - Tracks user email addresses for follow-up
    - Tracks Skyline versions affected
    - 9-month retention aligned to release cycle
  - Added `backfill_exception_history` MCP tool:
    - One-time backfill from major release (2025-05-22)
    - Populated with 730 exceptions → 195 unique fingerprints
    - 68 multi-user bugs, 47 bugs with contact emails
  - Added `record_exception_fix` MCP tool:
    - Record PR#, commit, version when fixing a bug
    - Future reports annotate known fixes and detect regressions
  - Added `query_exception_history` MCP tool:
    - Query "what should I focus on?" across all tracked exceptions
    - Priority scoring by user count, email availability, report frequency
  - Enhanced report output with status annotations:
    - 🆕 NEW - First seen today
    - 📧 Has user email (contact for follow-up)
    - 👥 Multi-user history (X reports from Y users since date)
    - ✅ KNOWN - Fixed in PR# (merged date)
    - 🔴 REGRESSION? - Report from version after fix

### 2025-12-30
- Implemented Stack Trace Normalization (Item 3):
  - Created `ai/mcp/LabKeyMcp/tools/stacktrace.py` as internal utility (not MCP-exposed)
  - Key decision: Internal utility keeps MCP API footprint lean, preserves context
  - Functions: `normalize_stack_trace()`, `fingerprint_matches()`, `group_by_fingerprint()`
  - Returns `NormalizedTrace` dataclass with fingerprint, signature_frames, normalized text
  - Filters: async noise (MoveNext, d__, AsyncMethodBuilder), framework frames
  - Normalizes: lambdas (<Method>b__0 → Method), closures (<>c__DisplayClass)
  - Path normalization: uses `pwiz_tools` as anchor to strip machine-specific prefixes

- Added `save_daily_failures` MCP tool:
  - Queries all 6 test folders for failures with stack traces
  - Uses new server-side query `failures_with_traces_by_date` (8AM-8AM window)
  - Groups failures by fingerprint to identify unique bugs
  - Tested with 2025-12-05 data: 27 failures grouped into 4 unique bugs
  - Systemic issues (TestScheduleMethodDlg, TestWatersConnectExportMethodDlg) correctly grouped

- Implemented Level 1 Automation - Pattern Detection:
  - Created `ai/mcp/LabKeyMcp/tools/patterns.py` with two new MCP tools:
    - `analyze_daily_patterns(report_date, days_back)` - Compares today vs history, returns prioritized Action Items
    - `save_daily_summary(...)` - Saves structured JSON for historical comparison
  - Pattern detection includes:
    - 🔴 SYSTEMIC: Issues affecting 3+ machines (code bug, not environment)
    - 🆕 NEW: First appearance since yesterday (regression)
    - 🌐 EXTERNAL: Tests involving Koina, Panorama, UniProt, Prosit
    - ⚠️ MISSING N DAYS: Computers absent for consecutive days
    - ✅ RESOLVED: Issues fixed since yesterday
  - Updated `/pw-daily` command (Steps 6 and 8) to use new MCP tools
  - Registered patterns module in tools/__init__.py
  - Added new tools to reporting machine's `.claude/settings.local.json`

- **Investigation needed**: `--allowedTools` vs `settings.local.json` interaction
  - The script `Invoke-DailyReport.ps1` passes `--allowedTools` with wildcards (e.g., `mcp__labkey__*`)
  - Wildcards don't work - we've confirmed tools must be listed explicitly
  - Currently, permissions work via `settings.local.json` on the reporting machine
  - **Question**: Does `--allowedTools` do anything useful? Options:
    1. `--allowedTools` alone is sufficient (no settings.local.json needed)
    2. Both are required (settings.local.json grants permission, --allowedTools filters)
    3. `--allowedTools` is ignored in `-p` mode, only settings.local.json matters
  - **Test plan**: After tomorrow's successful report with settings.local.json, try:
    - Remove a tool from settings.local.json but keep it in --allowedTools
    - If it still works → --allowedTools is sufficient
    - If it fails → settings.local.json is required
  - **If --allowedTools works**: Update script to use explicit tool list instead of wildcards
  - **If settings.local.json required**: Document this clearly, possibly remove --allowedTools from script

### 2025-12-29
- Added `fetch_labkey_page` MCP tool - enables fetching authenticated LabKey pages (failure details, stack traces)
- Implemented effort level modes in `/pw-daily`:
  - `quick`: 1-2 min, report only
  - `standard`: 15-30 min, investigate new failures, git blame (new default)
  - `deep`: Full session, comprehensive analysis, learn from developer emails
- Added execution logging (Step 13) - system now records what it analyzed and decided
- Added developer email learning loop (Step 11) - forwarded developer analyses become training signal
- Added regression investigation step (Step 10) - git blame/log to trace failures to commits
- Key insight: Developer email forwarding creates a feedback loop for continuous improvement

### 2025-12-28
- Created TODO from collaborative analysis session
- Implemented: email data sources, color coding docs, archiving, self-improvement step
- Analyzed 9 real developer report examples to identify valuable patterns
- Proved visual analysis capability (can analyze images, cannot acquire them)
- Implemented Historical JSON storage (Steps 6 and 8 in /pw-daily)
  - Schema: date, nightly summary/failures/leaks/hangs/missing, exceptions by signature, support
  - Files saved to `ai/.tmp/history/daily-summary-YYYYMMDD.json`
  - Enables trend analysis across days/weeks/months
