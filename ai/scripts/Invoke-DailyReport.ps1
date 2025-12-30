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
# - mcp__labkey__*: All LabKey MCP tools for test/exception/support data
# - mcp__gmail__search_emails,mcp__gmail__read_email: Read inbox notifications
# - mcp__gmail__send_email: Send the daily summary
$AllowedTools = "Read,Write,Glob,Grep,mcp__labkey__*,mcp__gmail__search_emails,mcp__gmail__read_email,mcp__gmail__send_email"

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
