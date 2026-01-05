# Quick Start: New Machine Setup with AI Assistance

This guide gets you from a pristine Windows machine to a working Skyline development environment in about an hour, with an AI assistant guiding most of the process.

**Prerequisites:** Windows 10/11, internet connection, GitHub account with SSH key access to ProteoWizard/pwiz

---

## Step 1: Open PowerShell

Press <kbd>Win</kbd> + <kbd>X</kbd>, then click **Windows PowerShell** (or **Terminal** on Windows 11).

*Note: You're using the built-in Windows PowerShell. We'll upgrade to PowerShell 7 later.*

## Step 2: Install Node.js

Node.js provides the npm package manager needed for Claude Code and other development tools.

```powershell
winget install OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements
```

**Close and reopen PowerShell** after installation to get `npm` in your PATH.

Verify:
```powershell
npm --version
```

## Step 3: Install Claude Code

```powershell
npm install -g @anthropic-ai/claude-code
```

Wait for the installation to complete.

## Step 4: Verify Installation

Try running:

```powershell
claude --version
```

If you see "claude is not recognized", see Troubleshooting below.

If successful, continue to Step 5.

## Step 5: Start Claude Code

```powershell
claude
```

Follow the prompts to authenticate with your Anthropic account. You'll need:
- Claude Pro, Max, or Team subscription, **OR**
- An Anthropic API key

## Step 6: Let Claude Code Guide the Rest

Once authenticated, paste this prompt into Claude Code:

```
Help me set up a Skyline development environment on this Windows machine.

Fetch and follow the instructions at:
https://skyline.ms/new-machine-setup.url

Guide me through each step, verifying success before moving on.
Ask me before running any installers.
```

Claude Code will then:
1. Install Git (if needed)
2. Configure Git settings
3. Help you set up SSH keys for GitHub
4. Clone the pwiz repository
5. Guide you through Visual Studio installation
6. Run the initial build
7. Verify everything works

---

## What Happens Next

The AI assistant will walk you through each step interactively. It will:
- **Check what's already installed** and skip unnecessary steps
- **Verify each step worked** before moving on
- **Explain any errors** and help you fix them
- **Tell you exactly what to click** in GUI installers

For Visual Studio installation, you'll need to interact with the installer GUI directly, but Claude Code will tell you exactly which workloads to select.

---

## Alternative: Manual Setup

If you prefer to set up without AI assistance, see the traditional guide: [How to Build Skyline](wiki-page.view?name=HowToBuildSkylineTip)

---

## After Setup: Add AI Development Tools

Once your basic environment is working, you can add AI-assisted development tools. In Claude Code, navigate to the repository and run:

```powershell
cd C:\proj\pwiz
/pw-configure
```

This adds: PowerShell 7 (UTF-8 support), ReSharper CLI, GitHub CLI, LabKey MCP server, and other tools that enhance the AI-assisted development workflow.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `winget` not recognized | You're on older Windows. Download Node.js from https://nodejs.org instead. |
| `npm` not recognized after install | Close and reopen PowerShell to refresh PATH. |
| `claude` not recognized after install | See detailed steps below |
| Authentication fails | Ensure you have a Claude Pro/Max/Team subscription or valid API key |
| Claude Code can't fetch the setup page | Check your internet connection. The AI will still help with general guidance. |

### `claude` not recognized - Detailed Fix

First, check if Claude Code was actually installed:

```powershell
Test-Path "$env:APPDATA\npm\claude.cmd"
```

**If the file doesn't exist:** The npm install failed. Try again:
```powershell
npm install -g @anthropic-ai/claude-code
```

**If the file exists but `claude` isn't recognized:** Add npm to your PATH:
```powershell
# Add npm global folder to PATH permanently
$npmPath = "$env:APPDATA\npm"
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", "User") + ";$npmPath", "User")

# Refresh current session
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
```

Then verify: `claude --version`

---

*Last updated:* 2026-01-02
