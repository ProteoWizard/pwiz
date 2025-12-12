<!--
  Developer Environment Setup for LLM-Assisted IDEs
  =================================================
  This document lives inside the pwiz repository (ai/docs/).
  Audience: Human developers preparing their machine for LLM-assisted workflows.
-->

# Developer Environment Setup for LLM-Assisted IDEs

This guide helps Skyline developers configure a Windows workstation so that AI-assisted IDEs (Cursor, VS Code + Copilot/Claude, etc.) can build, test, and inspect Skyline autonomously. Follow these steps after cloning `pwiz`.

> **Quick checklist**
>
> 1. Install PowerShell 7 (UTF-8 terminal support)
> 2. Configure PowerShell 7 profile for permanent UTF-8 encoding
> 3. Update Cursor/VS Code terminal settings to use PowerShell 7 + UTF-8
> 4. Install Claude Code CLI for agentic coding workflows
> 5. Install ReSharper command-line tools (`jb inspectcode`)
> 6. Install GitHub CLI (`gh`) for agentic PR workflows
> 7. Configure Git and global line ending settings
> 8. Install a Markdown viewer for browser-based docs
> 9. Optional: Install additional helpers (Everything Search, Diff tools)

---

## Using an LLM to Automate Setup

Much of this workstation prep can be delegated to an AI assistant (Cursor, Copilot Chat, Claude Code, etc.). Open a chat alongside your workspace and paste the following prompt:

```
You are configuring a Skyline development workstation for LLM-assisted IDE workflows.
Work inside the pwiz repository at C:\proj\pwiz.
Follow the checklist in ai/docs/developer-setup-guide.md and report progress after each step.
Automate any terminal commands you can run safely (PowerShell 7 install, dotnet tool installs, git config, etc.).
Call out any steps that require human action (Visual Studio workloads, browser extensions, antivirus exclusions).
Stop if you encounter errors and describe how to resolve them.
```

The assistant can then execute commands (or suggest them) directly within the IDE terminal. Remember to confirm actions that require elevated privileges or system restarts.

---

## 1. Terminal Environment (UTF-8, PowerShell 7)

LLM tooling expects UTF-8 output. Windows PowerShell 5.1 defaults to CP1252 and corrupts emoji/status icons.

### Install PowerShell 7

```powershell
winget install Microsoft.PowerShell
```

Restart Cursor/VS Code after install.

### Configure Permanent UTF-8 Encoding

PowerShell 7 may still default to OEM US (CP437) for console output. Configure your profile to permanently set UTF-8:

1. Open your PowerShell 7 profile for editing:
   ```powershell
   notepad $PROFILE
   ```

2. If the file doesn't exist, create it first:
   ```powershell
   New-Item -Path $PROFILE -ItemType File -Force
   notepad $PROFILE
   ```

3. Add these lines to the profile:
   ```powershell
   # Set UTF-8 encoding for console output and pipeline
   [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
   $OutputEncoding = [System.Text.Encoding]::UTF8
   ```

4. Save and close notepad, then reload the profile:
   ```powershell
   . $PROFILE
   ```

### Verify Installation

```powershell
$PSVersionTable.PSVersion      # Should show 7.x
[Console]::OutputEncoding      # Should show: BodyName: utf-8, CodePage: 65001
```

### Cursor / VS Code settings

Add to `.vscode/settings.json` (already checked-in in this repo):

```json
{
  "terminal.integrated.defaultProfile.windows": "PowerShell",
  "terminal.integrated.env.windows": {
    "LANG": "en_US.UTF-8",
    "LC_ALL": "en_US.UTF-8"
  }
}
```

This ensures *all* terminal sessions launched inside Cursor/VS Code use PowerShell 7 with UTF-8 output.

---

## 2. Required Command-Line Tools

### Claude Code CLI

Claude Code is Anthropic's agentic coding tool that runs in the terminal. It understands your codebase and can execute commands, edit files, and handle git workflows through natural language.

**Install (run in PowerShell 7 as Administrator):**

```powershell
irm https://claude.ai/install.ps1 | iex
```

**Known Issue: PATH not updated**

The installer may not add Claude Code to your PATH. If `claude` is not recognized after installation, add it manually:

```powershell
# Add to user PATH permanently
$claudePath = "$env:USERPROFILE\.local\bin"
[Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", "User") + ";$claudePath", "User")

# Refresh current session's PATH
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
```

**Verify:**
```powershell
claude --version
```

**Authenticate:**

After installation, run `claude` and follow the prompts to authenticate with your Anthropic account (requires Claude Pro, Max, or Team subscription, or API key).

**VS Code Extension:**

Claude Code also has a VS Code extension that provides a visual interface. Install from the VS Code marketplace: search for "Claude Code" by Anthropic. Note that the CLI and extension are separate products with different update schedules.

> **Tip:** The CLI stores conversation history in `~/.claude/projects/`. Use `claude --resume` to continue previous sessions.

### ReSharper Command-Line Tools (inspectcode)

Needed for pre-commit validation (`Build-Skyline.ps1 -RunInspection` / `-QuickInspection`).

```powershell
dotnet tool install -g JetBrains.ReSharper.GlobalTools
```

Verify:
```powershell
jb --version
jb tool list
```

> Recommended: match the version running on TeamCity (`2023.1.1`). Our scripts use the latest stable (`2025.2.4`) and produce zero-warning output.

### dotCover Command-Line Tools (code coverage)

Needed for code coverage analysis (`Run-Tests.ps1 -Coverage`).

```powershell
dotnet tool install --global JetBrains.dotCover.CommandLineTools --version 2025.1.7
```

Verify:
```powershell
dotCover --version
```

> **Important**: Use version 2025.1.7 or earlier. dotCover 2025.3.0+ has a known bug with JSON export that causes "Object reference not set to an instance of an object" errors.
>
> Note: dotCover Command Line Tools are separate from the ReSharper IDE extension. Install both for complete tooling support.

### GitHub CLI (gh)

The GitHub CLI enables AI agents (Claude Code, Copilot, etc.) to interact with GitHub directly—reviewing PRs, fetching issue details, and checking CI status without leaving the IDE terminal.

**Install:**
```powershell
winget install GitHub.cli --accept-source-agreements --accept-package-agreements
```

**Authenticate (must run in interactive PowerShell 7 terminal outside VS Code/Cursor):**

> **Important:** The `gh auth login` command requires an interactive terminal. It will not work inside an AI agent's shell context. Open a separate PowerShell 7 window to run this.

```powershell
gh auth login
```

Follow the interactive prompts:
1. Select **GitHub.com** as the account
2. Select **SSH** as your preferred protocol for Git operations
3. Answer **No** to generating a new SSH key (you should already have SSH configured via Git/TortoiseGit)
4. Select **Login with a web browser**
5. Copy the one-time code displayed, then press Enter to open the browser
6. In the browser, click **Continue** to sign in with your active GitHub account
7. Paste the one-time code (Ctrl+V) and click **Continue**
8. Click **Continue** again if asked
9. Click **Authorize github**
10. Complete your 2FA (authenticator app, security key, etc.)
11. Browser shows "Congratulations, you're all set!"

**Verify:**
```powershell
gh auth status
gh pr view 3700   # Test with any open PR
```

Expected output shows your logged-in account, protocol (ssh), and token scopes (gist, read:org, repo).

### Git Configuration (line endings)

Skyline requires CRLF on Windows. Configure once globally:

```powershell
git config --global core.autocrlf true
git config --global pull.rebase false
```

### Visual Studio Components

Ensure VS 2022 has the **.NET Desktop Development** and **Desktop Development with C++** workloads. Run `quickbuild.bat` once to pull vendor SDKs.

---

## 3. IDE Extensions & Settings

### Cursor / VS Code

- Install extensions:
  - C# Dev Kit (or official C# extension)
  - PowerShell
  - Claude Code (for visual Claude integration)
  - Markdown All in One (for inline preview)
- Enable EditorConfig support (`"editorconfig.enable": true`).
- Optional (highly recommended):
  - **Markdown Viewer** browser extension (Chrome/Edge) for external viewing of `ai/` docs.

### Visual Studio 2022

- Install ReSharper Ultimate (IDE integration).
- Configure MSTest runsettings (see skyline.ms build guide).

---

## 4. Browser / Documentation Experience

LLM tooling frequently references markdown files (`ai/README.md`, `ai/docs/…`). Improve readability by installing a Markdown viewer with GitHub-style rendering:

- Chrome/Edge: [Markdown Viewer (yzane)](https://chrome.google.com/webstore/detail/markdown-viewer/ckkdlimhmcjmikdlpkmbgfkaikojcbjk)
- Configure it to allow local file access for `C:\proj\pwiz`.

---

## 5. Optional Productivity Tools

- **EverythingSearch** (voidtools) — instant file searches across pwiz (`pwiz_tools/Skyline/…` has thousands of files).
- **Beyond Compare** or **WinMerge** — graphical diff for reviewing LLM-generated file changes.
- **Git Credential Manager** — simplifies HTTPS authentication if SSH keys aren't available.

---

## 6. Environment Validation

Run these scripts after setup to confirm everything works:

```powershell
cd C:\proj\pwiz\pwiz_tools\Skyline
\ai\Build-Skyline.ps1 -RunTests -QuickInspection
\ai\Build-Skyline.ps1 -RunInspection         # Full validation (~20-25 min)
\ai\Run-Tests.ps1 -TestName CodeInspection
```

Expected output:
- `[OK] Build succeeded ...`
- `[OK] All tests passed ...`
- `[OK] Code inspection passed ...` (QuickInspection ~1-5 min, full ~20-25 min)

If you see warning/errors, the scripts will fail with `[FAILED]` and clear messages.

---

## 7. Common Issues & Fixes

| Symptom | Cause | Fix |
|---------|-------|-----|
| Emoji/Unicode characters render as `â✓…` | Terminal using CP1252/CP437 | Install PowerShell 7, add UTF-8 config to `$PROFILE` |
| `claude` not found after install | PATH not updated by installer | Add `$env:USERPROFILE\.local\bin` to PATH (see Claude Code section) |
| `jb` not found | ReSharper CLI tools missing | `dotnet tool install -g JetBrains.ReSharper.GlobalTools` |
| `dotCover` not found | dotCover CLI tools missing | `dotnet tool install --global JetBrains.dotCover.CommandLineTools --version 2025.1.7` |
| dotCover JSON export fails with "Object reference not set" | dotCover 2025.3.0+ bug | Uninstall and install 2025.1.7: `dotnet tool uninstall --global JetBrains.dotCover.CommandLineTools && dotnet tool install --global JetBrains.dotCover.CommandLineTools --version 2025.1.7` |
| `gh` not found | GitHub CLI not installed | `winget install GitHub.cli` |
| `gh auth login` hangs in agent terminal | Requires interactive terminal | Run `gh auth login` in separate PowerShell 7 window outside IDE |
| CRLF/LF diffs everywhere | `core.autocrlf` not set | `git config --global core.autocrlf true` |
| `TestRunner.exe` missing | Build not run | `.\ai\Build-Skyline.ps1` |
| `inspectcode` builds solution again | `--no-build` flag missing | Use latest script (already includes `--no-build`) |

---

## 8. Resources

- **Public developer setup guide**: [How to Build Skyline](https://skyline.ms/home/software/Skyline/wiki-page.view?name=HowToBuildSkylineTip)
- **LLM tooling docs**: `ai/docs/build-and-test-guide.md`
- **Documentation maintenance system**: `ai/docs/documentation-maintenance.md`
- **Claude Code documentation**: [code.claude.com/docs](https://code.claude.com/docs)

---

## 9. Feedback

If you discover additional steps that improve the human + LLM workflow, update this document and the public site. Consistency between the two keeps every developer productive.
