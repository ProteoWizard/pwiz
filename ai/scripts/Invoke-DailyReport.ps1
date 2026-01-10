<#
.SYNOPSIS
    Runs Claude Code daily report and emails results.

.DESCRIPTION
    Invokes Claude Code in non-interactive mode to run /pw-daily,
    which generates nightly test, exception, and support reports,
    then emails a summary via Gmail MCP.

    Designed for Windows Task Scheduler automation.

.PARAMETER Recipient
    Email address to send the report to. Default: brendanx@uw.edu

.PARAMETER Model
    Claude model to use. Default: claude-opus-4-5-20251101

.PARAMETER MaxTurns
    Maximum agentic turns. Default: 100

.PARAMETER DryRun
    If set, prints the command without executing.

.EXAMPLE
    .\Invoke-DailyReport.ps1
    Runs the daily report with default settings.

.EXAMPLE
    .\Invoke-DailyReport.ps1 -Recipient "team@example.com"
    Sends the report to a different recipient.

.EXAMPLE
    .\Invoke-DailyReport.ps1 -DryRun
    Shows what would be executed without running.

.NOTES
    See ai/docs/scheduled-tasks-guide.md for Task Scheduler setup.
#>

param(
    [string]$Recipient = "brendanx@uw.edu",
    [string]$Model = "claude-opus-4-5-20251101",
    [int]$MaxTurns = 100,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Ensure UTF-8 encoding throughout the pipeline
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# Configuration
$WorkDir = "C:\proj\pwiz-ai"
$LogDir = Join-Path $WorkDir "ai\.tmp\scheduled"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmm"
$LogFile = Join-Path $LogDir "daily-$Timestamp.log"

# Ensure log directory exists
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

# Build the prompt
# Note: Slash commands (/pw-daily) and Skills don't work in -p mode.
# Instead, we instruct Claude to read the command file and follow it directly.
$Prompt = @"
You are running as a scheduled automation task. Slash commands and skills do not work in non-interactive mode.

FIRST: Read ai/CLAUDE.md to understand project rules (especially: use pwsh not powershell, backslashes for file paths).

THEN: Read .claude/commands/pw-daily.md and follow those instructions to generate the daily report.

Email recipient: $Recipient

Key points:
- Use MCP tools directly (mcp__labkey__*, mcp__gmail__*) - they are pre-authorized
- Use pwsh (not powershell) for any shell commands
- The report date is the date in your environment info
- Include key findings in the email body
- If MCP tools fail, send an ERROR email as specified in pw-daily.md
"@

# Build allowed tools list
# - Read/Write/Glob/Grep: File operations for reports and TODO files
# - LabKey MCP tools: Test/exception/support data (wildcards don't work - must be explicit)
# - Gmail MCP tools: Read inbox, send summary, archive processed emails
$AllowedTools = @(
    "Read",
    "Write",
    "Glob",
    "Grep",
    # LabKey MCP - nightly tests, exceptions, support
    "mcp__labkey__check_computer_alarms",
    "mcp__labkey__get_daily_test_summary",
    "mcp__labkey__save_exceptions_report",
    "mcp__labkey__get_support_summary",
    "mcp__labkey__get_run_failures",
    "mcp__labkey__get_run_leaks",
    "mcp__labkey__save_test_failure_history",
    "mcp__labkey__analyze_daily_patterns",
    "mcp__labkey__save_daily_summary",
    "mcp__labkey__query_test_history",
    # Gmail MCP - read notifications, send report, archive
    "mcp__gmail__search_emails",
    "mcp__gmail__read_email",
    "mcp__gmail__send_email",
    "mcp__gmail__modify_email",
    "mcp__gmail__batch_modify_emails"
) -join ","

# Build the command
$ClaudeArgs = @(
    "-p", $Prompt,
    "--allowedTools", $AllowedTools,
    "--max-turns", $MaxTurns,
    "--model", $Model
)

if ($DryRun) {
    Write-Host "Would execute:" -ForegroundColor Cyan
    Write-Host "  Working directory: $WorkDir"
    Write-Host "  Git pull: git pull origin ai-context"
    Write-Host "  Log file: $LogFile"
    Write-Host "  Command: claude $($ClaudeArgs -join ' ')"
    exit 0
}

# Log start
$StartTime = Get-Date
"[$StartTime] Starting Claude Code daily report" | Out-File -FilePath $LogFile -Encoding UTF8

# Pull latest ai-context branch
"[$(Get-Date)] Pulling latest ai-context branch..." | Out-File -FilePath $LogFile -Append -Encoding UTF8
Push-Location $WorkDir
try {
    $GitOutput = git pull origin ai-context 2>&1
    $GitOutput | Out-File -FilePath $LogFile -Append -Encoding UTF8
    if ($LASTEXITCODE -ne 0) {
        "[$(Get-Date)] WARNING: Git pull failed, continuing with existing version" | Out-File -FilePath $LogFile -Append -Encoding UTF8
    } else {
        "[$(Get-Date)] Git pull successful" | Out-File -FilePath $LogFile -Append -Encoding UTF8
    }
}
finally {
    Pop-Location
}

# Change to project directory
Push-Location $WorkDir

try {
    # Run Claude Code with real-time logging
    "[$(Get-Date)] Starting Claude Code..." | Out-File -FilePath $LogFile -Append -Encoding UTF8
    & claude @ClaudeArgs 2>&1 | ForEach-Object {
        $_ | Out-File -FilePath $LogFile -Append -Encoding UTF8
    }
    $ExitCode = $LASTEXITCODE

    # Log completion
    $EndTime = Get-Date
    $Duration = $EndTime - $StartTime
    "[$EndTime] Completed with exit code $ExitCode (duration: $($Duration.TotalMinutes.ToString('F1')) minutes)" | Out-File -FilePath $LogFile -Append -Encoding UTF8

    if ($ExitCode -ne 0) {
        Write-Error "Claude Code exited with code $ExitCode. See log: $LogFile"
    }
}
finally {
    Pop-Location
}

# Clean up old logs (keep 30 days)
Get-ChildItem -Path $LogDir -Filter "*.log" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item -Force

exit $ExitCode
