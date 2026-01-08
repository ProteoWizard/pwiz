# Set up scheduled Claude Code daily analysis system

## Branch Information
- **Branch**: `ai-context`
- **Base**: `ai-context`
- **Created**: 2025-12-26
- **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/3732

## Objective

Create an automated daily analysis system where Claude Code:
1. Runs daily reports (nightly tests, exceptions, support board)
2. Maintains longitudinal context from past reports
3. Performs trend analysis and anomaly detection
4. Surfaces actionable insights that might otherwise be missed

## Tasks

### Phase 1: Basic Infrastructure
- [x] Research Claude Code non-interactive execution (`-p` flag, `--allowedTools`, `--max-turns`)
- [x] Create `/pw-daily` command for consolidated reports
- [x] Create `Invoke-DailyReport.ps1` automation script
- [x] Document Windows Task Scheduler setup
- [x] Reports already go to `ai/.tmp/` via MCP server

### Phase 2: Longitudinal Analysis (Future)
- [ ] Historical context storage (e.g., `ai/.tmp/history/`)
- [ ] Trend detection algorithms
- [ ] Anomaly highlighting in daily summary
- [ ] Comparison with past week/month baselines

### Phase 3: Email Delivery
- [x] Gmail MCP already integrated in `Invoke-DailyReport.ps1`

## Files Created

| File | Purpose |
|------|---------|
| `.claude/commands/pw-daily.md` | Consolidated daily report command |
| `ai/scripts/Invoke-DailyReport.ps1` | Task Scheduler automation script |
| `ai/docs/scheduled-tasks-guide.md` | Setup documentation |

## Progress Log

### 2025-12-26 - Phase 1 Complete

**Research findings:**
- Claude Code `-p` flag runs non-interactively
- `--allowedTools` auto-approves specific tools without prompts
- `--max-turns` limits agentic iterations
- `--model` selects the model

**Created:**
- `/pw-daily` command that runs all three reports (nightly, exceptions, support)
- `Invoke-DailyReport.ps1` with parameters for recipient, model, dry-run
- `scheduled-tasks-guide.md` with Task Scheduler setup instructions

**Tested:**
- Script dry-run works correctly
- MCP server already saves reports to `ai/.tmp/` with consistent naming

**Next steps:**
- User should test actual execution (non-dry-run)
- Configure Task Scheduler when ready
- Phase 2 (longitudinal analysis) is future work

### 2025-12-27 - Sprint Complete

**Verified:**
- Scheduled task ran successfully at 8:30 AM
- Email delivered to brendanx@proteinms.net via Gmail MCP
- Report included nightly tests, exceptions, and support summary

**Outcome:**
- Phase 1 (infrastructure) and Phase 3 (email) complete
- Phase 2 (longitudinal analysis) deferred to future issue
- Proof-of-concept validated; system will run daily for monitoring
