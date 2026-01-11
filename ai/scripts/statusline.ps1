<#
.SYNOPSIS
    Claude Code statusline script - displays project, git branch, model, and context usage.

.DESCRIPTION
    This script provides a dynamic status line for Claude Code showing:
    - Project directory name
    - Current git branch
    - Model name (e.g., Opus, Sonnet)
    - Context window usage percentage

    Example output: pwiz-ai [ai-context] | Opus | 36% used

.SETUP
    This is a personal preference setting, not a project-wide setting.
    To enable, add the following to your personal Claude Code settings:

    Windows: %USERPROFILE%\.claude\settings.json
    macOS/Linux: ~/.claude/settings.json

    Contents:
    {
      "statusLine": {
        "type": "command",
        "command": "pwsh -NoProfile -File C:\\path\\to\\statusline.ps1"
      }
    }

    Adjust the path to where you have this script located.
    You can copy this script to ~/.claude/statusline.ps1 for convenience.

.NOTES
    The script receives JSON data from Claude Code via stdin containing:
    - workspace.project_dir: The project root directory
    - model.display_name: Current model name
    - context_window.current_usage: Token usage statistics
    - context_window.context_window_size: Total context window size

    Context warning: Claude Code warns at ~10% remaining, so watching
    "% used" helps you know when you're approaching that threshold.
#>

$input_json = $input | Out-String | ConvertFrom-Json

# Get project directory name
$project_dir = $input_json.workspace.project_dir
$project_name = Split-Path $project_dir -Leaf

# Get model display name
$model = $input_json.model.display_name

# Get git branch
$git_info = ""
try {
    Push-Location $project_dir
    $branch = git branch --show-current 2>$null
    if ($branch) {
        $git_info = " [$branch]"
    }
    Pop-Location
} catch { }

# Calculate context percentage
$ctx = ""
if ($input_json.context_window.current_usage) {
    $usage = $input_json.context_window.current_usage
    $current = $usage.input_tokens + $usage.cache_creation_input_tokens + $usage.cache_read_input_tokens
    $size = $input_json.context_window.context_window_size
    if ($size -gt 0) {
        $pct = [math]::Floor(($current * 100) / $size)
        $ctx = " | $pct% used"
    }
}

Write-Host "$project_name$git_info | $model$ctx"
