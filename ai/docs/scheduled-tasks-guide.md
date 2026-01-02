# Scheduled Tasks Guide

This guide covers running Claude Code automatically on a schedule using Windows Task Scheduler.

## Overview

Claude Code can run non-interactively using the `-p` (print) flag:

```bash
claude -p "Read .claude/commands/pw-daily.md and follow it"
```

This enables automated daily reports without manual intervention.

**Note:** Slash commands (`/pw-daily`) and Skills don't work in `-p` mode. See [Non-Interactive Mode Limitations](#non-interactive-mode-limitations) for workarounds.

## Prerequisites

- Claude Code installed and authenticated
- Gmail MCP configured (see `ai/docs/mcp/gmail.md`)
- LabKey MCP configured for skyline.ms access
- PowerShell 7 (`pwsh`)
- **MCP permissions configured** (see below)

## CRITICAL: MCP Permissions for Command-Line Automation

MCP tools require explicit permission in `.claude/settings.local.json` to work in non-interactive mode. Without these permissions, Claude Code will silently fall back to using stale cached files instead of querying live data.

### Required Configuration

MCP tools require explicit permission in `.claude/settings.local.json` to work in non-interactive mode.

**IMPORTANT**: Wildcards (e.g., `mcp__labkey__*`) do NOT work in `-p` mode. Each tool must be listed explicitly by name.

### How to Configure

1. Start an interactive Claude Code session in your project
2. Describe the command-line operation you want to automate
3. Ask Claude to write the necessary `permissions.allow` entries to `.claude/settings.local.json`
4. Review the list - remove any tools with unwanted side effects (e.g., `delete_*`, `update_*`)

### Example: Daily Report Permissions

The daily report (`/pw-daily`) requires these MCP tools:

```json
{
  "permissions": {
    "allow": [
      "mcp__labkey__get_daily_test_summary",
      "mcp__labkey__save_exceptions_report",
      "mcp__labkey__get_support_summary",
      "mcp__labkey__get_run_failures",
      "mcp__labkey__get_run_leaks",
      "mcp__labkey__save_test_failure_history",
      "mcp__gmail__search_emails",
      "mcp__gmail__read_email",
      "mcp__gmail__send_email",
      "mcp__gmail__modify_email",
      "mcp__gmail__batch_modify_emails"
    ]
  }
}
```

Note: Destructive tools like `delete_email`, `update_wiki_page` are intentionally excluded.

### Verification

Test from command line before relying on scheduled tasks:

```powershell
claude -p "Call mcp__labkey__get_daily_test_summary with today's date and tell me how many test runs it found"
```

If it returns actual run counts (not a permission error), the configuration is working.

## Non-Interactive Mode Limitations

When running Claude Code with `-p` (print/non-interactive mode), several features don't work:

| Feature | Works in `-p` mode? | Workaround |
|---------|---------------------|------------|
| Slash commands (`/pw-daily`) | ❌ No | Read the command file directly and follow instructions |
| Skills (`Skill(name)`) | ❌ No | Include relevant documentation reading in the prompt |
| Interactive approval | ❌ No | Pre-authorize tools in `.claude/settings.local.json` |
| MCP wildcards | ❌ No | List each tool explicitly |
| CLAUDE.md auto-loading | ⚠️ Partial | Explicitly instruct to read it in the prompt |

### Prompt Design for Non-Interactive Mode

Structure your prompts to work around these limitations:

```
You are running as a scheduled automation task. Slash commands and skills do not work.

FIRST: Read ai/CLAUDE.md to understand project rules.
THEN: Read .claude/commands/your-command.md and follow those instructions.

Key points:
- Use pwsh (not powershell) for shell commands
- MCP tools are pre-authorized: [list relevant tools]
- [Any other context the session needs]
```

## Command-Line Options for Automation

Key flags for scheduled tasks:

| Flag | Purpose | Example |
|------|---------|---------|
| `-p "prompt"` | Run non-interactively | `claude -p "Read .claude/commands/pw-daily.md and follow it"` |
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
