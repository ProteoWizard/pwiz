# Scheduled Tasks Guide

This guide covers running Claude Code automatically on a schedule using Windows Task Scheduler.

## Overview

Claude Code can run non-interactively using the `-p` (print) flag:

```bash
claude -p "run /pw-daily"
```

This enables automated daily reports without manual intervention.

## Prerequisites

- Claude Code installed and authenticated
- Gmail MCP configured (see `ai/docs/mcp/gmail.md`)
- LabKey MCP configured for skyline.ms access
- PowerShell 7 (`pwsh`)

## Command-Line Options for Automation

Key flags for scheduled tasks:

| Flag | Purpose | Example |
|------|---------|---------|
| `-p "prompt"` | Run non-interactively | `claude -p "run /pw-daily"` |
| `--output-format json` | Structured output for parsing | |
| `--allowedTools "..."` | Auto-approve specific tools | `--allowedTools "Read,Glob,Grep"` |
| `--max-turns N` | Limit iterations | `--max-turns 20` |
| `--model` | Specify model | `--model claude-sonnet-4-20250514` |

## Output Locations

All outputs stay within the project under `ai/.tmp/`:

| Type | Location | Pattern |
|------|----------|---------|
| Nightly report | `ai/.tmp/` | `nightly-report-YYYYMMDD.md` |
| Exceptions report | `ai/.tmp/` | `exceptions-report-YYYYMMDD.md` |
| Support report | `ai/.tmp/` | `support-report-YYYYMMDD.md` |
| Automation logs | `ai/.tmp/scheduled/` | `daily-YYYYMMDD-HHMM.log` |

## The Daily Report Script

The script `ai/scripts/Invoke-DailyReport.ps1` handles:
- Pulling latest `ai-context` branch (ensures latest scripts/commands)
- Running Claude Code with `/pw-daily`
- Emailing results via Gmail MCP
- Logging to `ai/.tmp/scheduled/`
- Auto-cleanup of logs older than 30 days

### Usage

```powershell
# Run with defaults (emails to brendanx@uw.edu)
pwsh -Command "& './ai/scripts/Invoke-DailyReport.ps1'"

# Different recipient
pwsh -Command "& './ai/scripts/Invoke-DailyReport.ps1' -Recipient 'team@example.com'"

# Preview without executing
pwsh -Command "& './ai/scripts/Invoke-DailyReport.ps1' -DryRun"
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Recipient` | `brendanx@uw.edu` | Email address for the report |
| `-Model` | `claude-sonnet-4-20250514` | Claude model to use |
| `-MaxTurns` | `30` | Maximum agentic turns |
| `-DryRun` | (switch) | Print command without executing |

## Task Scheduler Setup

### Step 1: Test the Script Manually

```powershell
cd C:\proj\pwiz-ai
pwsh -Command "& './ai/scripts/Invoke-DailyReport.ps1' -DryRun"
```

Then run without `-DryRun` to verify it works.

### Step 2: Create Scheduled Task

**Option A: PowerShell (recommended)**

Run as Administrator:

```powershell
$taskAction = New-ScheduledTaskAction `
    -Execute "pwsh" `
    -Argument "-NoProfile -File C:\proj\pwiz-ai\ai\scripts\Invoke-DailyReport.ps1" `
    -WorkingDirectory "C:\proj\pwiz-ai"

$taskTrigger = New-ScheduledTaskTrigger -Daily -At 8:30AM

$taskSettings = New-ScheduledTaskSettingsSet `
    -RunOnlyIfNetworkAvailable `
    -StartWhenAvailable `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries

$task = New-ScheduledTask `
    -Action $taskAction `
    -Trigger $taskTrigger `
    -Settings $taskSettings `
    -Description "Daily Claude Code report: nightly tests, exceptions, support"

Register-ScheduledTask `
    -TaskName "Claude-Daily-Report" `
    -InputObject $task `
    -User "$env:USERNAME" `
    -RunLevel Highest
```

**Option B: Task Scheduler GUI**

1. Open "Task Scheduler" (search in Start menu)
2. Click "Create Basic Task"
3. Name: "Claude-Daily-Report"
4. Trigger: Daily at 8:30 AM
5. Action: Start a program
   - Program: `pwsh`
   - Arguments: `-NoProfile -File C:\proj\pwiz-ai\ai\scripts\Invoke-DailyReport.ps1`
   - Start in: `C:\proj\pwiz-ai`
6. Finish, then edit task properties:
   - Check "Run with highest privileges"
   - Check "Run only when network is available"

## Configuration Options

### Change Recipients

Pass the `-Recipient` parameter:

```powershell
$taskAction = New-ScheduledTaskAction `
    -Execute "pwsh" `
    -Argument "-NoProfile -File C:\proj\pwiz-ai\ai\scripts\Invoke-DailyReport.ps1 -Recipient 'team@example.com'" `
    -WorkingDirectory "C:\proj\pwiz-ai"
```

### Change Schedule

Modify the trigger time:

```powershell
$taskTrigger = New-ScheduledTaskTrigger -Daily -At 8:30AM
```

### Weekend Skip

For weekday-only execution:

```powershell
$taskTrigger = New-ScheduledTaskTrigger -Weekly `
    -DaysOfWeek Monday,Tuesday,Wednesday,Thursday,Friday `
    -At 9:00AM
```

## Troubleshooting

### Task Runs but No Email

1. Check log file in `ai/.tmp/scheduled/`
2. Verify Gmail MCP is configured: `claude mcp list`
3. Test email manually: ask Claude to send a test email

### Task Doesn't Run

1. Check Task Scheduler history (right-click task → History)
2. Ensure user has "Log on as batch job" permission
3. Verify network connectivity at scheduled time
4. Check that `pwsh` is in the system PATH

### Claude Code Errors

1. Check API key is valid: `claude --version`
2. Verify working directory exists
3. Check `--allowedTools` includes all needed tools

### MCP Connection Issues

1. Re-authenticate Gmail MCP: `npx @gongrzhe/server-gmail-autoauth-mcp auth`
2. Check LabKey MCP: `claude mcp list`

## Log Management

The script auto-deletes logs older than 30 days. Logs are kept in `ai/.tmp/scheduled/` which is gitignored.

To view recent logs:

```powershell
Get-ChildItem ai/.tmp/scheduled/*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 5
```

## Security Considerations

- Script runs with user credentials
- API keys stored in Claude Code's secure storage
- Gmail OAuth tokens in `~/.gmail-mcp/`
- LabKey credentials in `~/.netrc`

## Related

- `ai/scripts/Invoke-DailyReport.ps1` - The automation script
- `ai/docs/mcp/gmail.md` - Gmail MCP setup
- `ai/docs/mcp/nightly-tests.md` - Nightly test data
- `ai/docs/mcp/exceptions.md` - Exception triage
- `ai/docs/mcp/support.md` - Support board access
- `.claude/commands/pw-daily.md` - Daily report command
