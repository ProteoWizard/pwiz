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
    Claude model to use. Default: claude-sonnet-4-20250514

.PARAMETER MaxTurns
    Maximum agentic turns. Default: 30

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
    [string]$Model = "claude-sonnet-4-20250514",
    [int]$MaxTurns = 30,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Configuration
$WorkDir = "C:\proj\pwiz"
$LogDir = Join-Path $WorkDir "ai\.tmp\scheduled"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmm"
$LogFile = Join-Path $LogDir "daily-$Timestamp.log"

# Ensure log directory exists
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

# Build the prompt
$Prompt = @"
Run /pw-daily and then email the consolidated summary to $Recipient using the Gmail MCP. Include key findings in the email body.
"@

# Build allowed tools list
$AllowedTools = "Read,Glob,Grep,mcp__labkey__*,mcp__gmail__send_email"

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
    Write-Host "  Log file: $LogFile"
    Write-Host "  Command: claude $($ClaudeArgs -join ' ')"
    exit 0
}

# Log start
$StartTime = Get-Date
"[$StartTime] Starting Claude Code daily report" | Out-File -FilePath $LogFile -Encoding UTF8

# Change to project directory
Push-Location $WorkDir

try {
    # Run Claude Code
    $Output = & claude @ClaudeArgs 2>&1
    $ExitCode = $LASTEXITCODE

    # Log output
    $Output | Out-File -FilePath $LogFile -Append -Encoding UTF8

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
