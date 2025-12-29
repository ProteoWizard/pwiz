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

## Backlog

### Tier 1: High Value, Achievable Now

#### 1. Level 1 Automation - Pattern Detection
**Problem**: Reports show data but don't highlight patterns requiring immediate attention
**Solution**: Add pattern detection to daily report:
- Detect NEW failures (not in yesterday's run)
- Detect ALL-MACHINES-AFFECTED pattern → flag for immediate attention
- Flag tests involving known external services (Koina, Panorama)
- Track "expected fixes" and verify next day
**Implementation**: Compare today's results to historical JSON (now available)

#### 2. Parse Installation ID from Exceptions
**Problem**: Cannot distinguish "1 user hit this 4 times" from "4 users hit this once each"
**Solution**: Parse Installation ID from exception email HTML
**Implementation**: Regex for `Installation ID: XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX`
**Value**: Prioritize bugs blocking multiple users vs one-off issues

### Tier 2: High Value, Requires C# Code Changes

#### 3. Automated Stack Trace Capture for Hangs
**Problem**: Hang alerts notify developers to RDP and attach debuggers, but developers rarely catch it in time
**Solution**: Have SkylineNightly automatically capture thread/stack traces from hung TestRunner.exe
**Implementation options**:
- ProcDump.exe (SysInternals) to capture minidump
- ClrMD (Microsoft.Diagnostics.Runtime) for managed stack traces
- Post extracted traces to LabKey
**Value**: Transforms hang alerts from "notification to act" to "automated diagnosis"
**Files**: SkylineNightly project

#### 4. TestRunner Flush Logging for Hung Test Identification
**Problem**: Hang alert emails show last *completed* test, not the *hung* test (log only flushes on line completion)
**Solution**: Flush test name immediately when starting:
```
[Flush] "Starting: TestFoo"
... test runs ...
[Flush] "Completed: TestFoo (passed, 45s)"
```
**Files**: TestRunner.exe output code
**Value**: Know which test is hung immediately, not after 8 AM report

#### 5. Historical Regression Detection
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

### Tier 3: Medium Value

#### 6. Track Recurring Missing Computers
**Problem**: Same computers missing for multiple days with no investigation
**Solution**: Track "days missing" and escalate after threshold (e.g., 3 days)
**Depends on**: Historical JSON (now available)

#### 7. Date Boundary Verification
**Problem**: Occasional mismatch between email data and MCP data due to different time windows
**Solution**: Log explicit date ranges from each source, flag discrepancies
**Value**: Accuracy and trust in reports

#### 8. Git Integration for Developer Attribution
**Problem**: Reports show failures but don't identify who should investigate
**Solution**:
- Get commit author from git hash: `git show --format='%an <%ae>' -s <hash>`
- Map stack trace files to code owners
- Suggest "@developer should investigate"
**Value**: Actionable recommendations, not just data

### Tier 4: Low Priority / Quick Fixes

#### 9. Verify Leak Type Distinction
**Problem**: Reports say "leak" but email distinguishes "Handle leak" vs "Memory and handle leak"
**Solution**: Verify `leak_type` from `leaks_by_date` query is surfaced in reports
**Status**: Data already available, just needs verification

#### 10. Standardize Email Subject Format
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

### 2025-12-28
- Created TODO from collaborative analysis session
- Implemented: email data sources, color coding docs, archiving, self-improvement step
- Analyzed 9 real developer report examples to identify valuable patterns
- Proved visual analysis capability (can analyze images, cannot acquire them)
- Implemented Historical JSON storage (Steps 6 and 8 in /pw-daily)
  - Schema: date, nightly summary/failures/leaks/hangs/missing, exceptions by signature, support
  - Files saved to `ai/.tmp/history/daily-summary-YYYYMMDD.json`
  - Enables trend analysis across days/weeks/months
