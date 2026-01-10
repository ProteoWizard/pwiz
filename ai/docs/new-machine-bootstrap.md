# Quick Start: New Machine Setup with AI Assistance

This guide gets you from a pristine Windows machine to a working Skyline development environment in about an hour, with an AI assistant guiding most of the process.

**Prerequisites:** Windows 10/11, internet connection, GitHub account with SSH key access to ProteoWizard/pwiz

---

## Step 1: Open PowerShell

Press <kbd>Win</kbd> + <kbd>X</kbd>, then click **Windows PowerShell** (or **Terminal** on Windows 11).

*Note: You're using the built-in Windows PowerShell. We'll upgrade to PowerShell 7 later.*

## Step 2: Install Git

Git provides version control and—importantly—Git Bash, which Claude Code uses to run commands on Windows.

```powershell
winget install Git.Git --source winget --accept-source-agreements --accept-package-agreements
```

> **Note:** The `--source winget` flag avoids Microsoft Store SSL certificate errors that can occur on fresh Windows installations.

**Close and reopen PowerShell** after installation to get `git` in your PATH.

Verify:
```powershell
git --version
```

## Step 3: Install Claude Code

```powershell
irm https://claude.ai/install.ps1 | iex
```

This downloads and runs Anthropic's standalone installer. It installs to `%USERPROFILE%\.local\bin\claude.exe`.

## Step 4: Add Claude to PATH

The installer often fails to add Claude to your PATH correctly. Run this to fix it:

```powershell
# Add Claude install location to PATH permanently
$claudePath = "$env:USERPROFILE\.local\bin"
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", "User") + ";$claudePath", "User")
```

**Now close PowerShell completely and reopen it.** The PATH change requires a fresh terminal session.

## Step 5: Verify Installation

```powershell
claude --version
```

If you see "claude is not recognized", see Troubleshooting below.

If successful, continue to Step 6.

## Step 6: Create Project Folder

Before starting Claude Code, create the folder where you'll clone the ProteoWizard repository:

```powershell
New-Item -ItemType Directory -Path C:\proj -Force
cd C:\proj
```

**Why this matters:** When Claude Code starts, it asks "Do you trust the files in this folder?" You want to answer this in an empty project folder—not your home directory, which may contain sensitive files like `.netrc`, `.ssh`, or other credentials.

## Step 7: Start Claude Code

```powershell
claude
```

Follow the prompts to authenticate with your Anthropic account.

**Recommended:** Use a **Claude Pro** or **Claude Max** subscription. These are straightforward monthly subscriptions at [claude.ai](https://claude.ai) that work immediately with Claude Code.

> **Note:** Claude Code also supports API keys, but if you don't already have one set up and know how to use it, don't go down that path—it requires separate billing configuration and is meant for developers building integrations, not end users.

## Step 8: Let Claude Code Guide the Rest

Once authenticated, paste this prompt into Claude Code:

```
Help me set up a Skyline development environment on this Windows machine.

Fetch and follow the instructions at:
https://skyline.ms/new-machine-setup.url

Guide me through each step, verifying success before moving on.
Ask me before running any installers.
```

Claude Code will then:
1. Configure Git settings (line endings, etc.)
2. Help you set up SSH keys for GitHub
3. Clone the pwiz repository
4. Guide you through Visual Studio installation
5. Run the initial build
6. Verify everything works

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
| `winget` not recognized | You're on older Windows. Download Git from https://git-scm.com instead. For Claude Code, use the npm fallback below. |
| `winget` SSL certificate errors | Add `--source winget` to the command (already included in commands above). |
| `git` not recognized after install | Close and reopen PowerShell to refresh PATH. |
| `claude` not recognized after install | See detailed steps below |
| Standalone installer fails | Use the npm fallback installation below |
| Authentication fails | Sign up for Claude Pro or Max at [claude.ai](https://claude.ai). Avoid the API key path unless you already have one configured. |
| Claude Code can't fetch the setup page | Check your internet connection. The AI will still help with general guidance. |

### `claude` not recognized - Detailed Fix

First, check if Claude Code was actually installed:

```powershell
Test-Path "$env:USERPROFILE\.local\bin\claude.exe"
```

**If the file doesn't exist:** The standalone installer failed. Use the npm fallback below.

**If the file exists but `claude` isn't recognized:**
1. Make sure you ran the PATH fix command in Step 4
2. Make sure you **closed and reopened PowerShell** after running it (not just opened a new tab—close the whole window)
3. If still not working, try opening a fresh PowerShell window and run `claude --version`

### Fallback: Install Claude Code via npm

If the standalone installer doesn't work, install via npm instead:

1. **Install Node.js:**
   ```powershell
   winget install OpenJS.NodeJS.LTS --source winget --accept-source-agreements --accept-package-agreements
   ```
   Close and reopen PowerShell.

2. **Install Claude Code via npm:**
   ```powershell
   npm install -g @anthropic-ai/claude-code
   ```

3. **Verify:**
   ```powershell
   claude --version
   ```

If `claude` still isn't recognized after npm install, check and fix PATH:
```powershell
# Check if npm installed it
Test-Path "$env:APPDATA\npm\claude.cmd"

# If it exists, add npm to PATH
$npmPath = "$env:APPDATA\npm"
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", "User") + ";$npmPath", "User")
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
```

---

*Last updated:* 2026-01-08
