<!--
  New Machine Setup Guide for LLM Assistants
  ===========================================
  This document is designed to be fetched and followed by Claude Code or similar
  LLM assistants. It guides setup of a pristine Windows machine for Skyline development.

  Target audience: LLM assistants helping a human set up their development environment.
  Human-readable version: https://skyline.ms/wiki-page.view?name=HowToBuildSkylineTip

  PUBLISHING: This file is attached to the NewMachineBootstrap wiki page on skyline.ms.
  - Shortcut URL: https://skyline.ms/new-machine-setup.url
  - Direct download: https://skyline.ms/home/software/Skyline/wiki-download.view?entityId=945040f8-bdf6-103e-9d4b-22f53556b982&name=new-machine-setup.md

  When updating this file, re-upload it as an attachment to the wiki page.
-->

# Skyline Development Environment Setup

You are helping a developer set up a Windows machine for Skyline development. Follow these phases in order, verifying each step before proceeding.

## Important Notes for the LLM Assistant

- **Ask before installing**: Always confirm with the user before running installers
- **Verify each step**: Run verification commands to confirm success before moving on
- **Handle existing installs**: Check if software is already installed and skip if so
- **Guide GUI steps**: For Visual Studio and other GUI installers, tell the user exactly what to click
- **Track progress**: Keep the user informed of what's done and what's next

---

## Phase 1: Prerequisites

### 1.1 Check for Node.js

Node.js provides npm, used for installing Claude Code and other development tools.

```powershell
node --version
npm --version
```

If not found, install Node.js LTS:
```powershell
winget install OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements
```

After installation, **restart the terminal** to get node/npm in PATH.

### 1.2 Check for Git

```powershell
git --version
```

If not found, install Git for Windows:
```powershell
winget install Git.Git --accept-source-agreements --accept-package-agreements
```

After installation, **restart the terminal** to get git in PATH.

### 1.3 PowerShell 7

PowerShell 7 (`pwsh`) is required for build scripts and AI tooling. Windows PowerShell 5.1 is not sufficient.

```powershell
pwsh --version
```

If not found or version is below 7.0, install:
```powershell
winget install Microsoft.PowerShell --accept-source-agreements --accept-package-agreements
```

After installation, **restart the terminal** and verify `pwsh --version` works.

### 1.4 Python

Python is required for AI tooling (MCP servers, LabKey integration).

```powershell
python --version
```

If not found, install Python 3.12:
```powershell
winget install Python.Python.3.12 --accept-source-agreements --accept-package-agreements
```

After installation, **restart the terminal** and verify `python --version` works.

### 1.5 Configure Git Line Endings

```powershell
git config --global core.autocrlf true
git config --global pull.rebase false
```

Verify:
```powershell
git config --global core.autocrlf
# Should output: true
```

### 1.6 SSH Key Setup

Check for existing SSH key:
```powershell
Test-Path ~/.ssh/id_rsa.pub
# or
Test-Path ~/.ssh/id_ed25519.pub
```

If no key exists, guide the user:
1. Generate a key: `ssh-keygen -t ed25519 -C "their-email@example.com"`
2. Accept default location, set a passphrase
3. Display the public key: `Get-Content ~/.ssh/id_ed25519.pub`
4. Tell user: "Copy this key and add it to GitHub at https://github.com/settings/keys"
5. **Wait for user to confirm** they've added the key before proceeding

### 1.7 Test GitHub SSH Access

```powershell
ssh -T git@github.com
```

Expected: "Hi username! You've successfully authenticated..."

If it fails with host key verification, the user needs to type "yes" to accept GitHub's fingerprint.

### 1.8 Configure Git Identity

After successful GitHub authentication, configure the Git identity for commits. Ask the user for their GitHub username and email, then run:

```powershell
git config --global user.name "their-github-username"
git config --global user.email "their-email@example.com"
```

> **Tip:** The username shown in the SSH test output ("Hi username!") is their GitHub username.

### 1.9 Clone the Repository

```powershell
# Create project directory
New-Item -ItemType Directory -Path C:\proj -Force
cd C:\proj

# Clone pwiz
git clone git@github.com:ProteoWizard/pwiz.git
```

This takes several minutes (large repository). Verify:
```powershell
Test-Path C:\proj\pwiz\pwiz_tools\Skyline\Skyline.sln
# Should be True
```

### 1.10 Restart in Project Directory

**Important:** Claude Code works best when launched from the project root. Now that the repository is cloned and PowerShell 7 is installed:

1. Exit Claude Code (type `exit` or `/exit`)
2. Close the current terminal
3. Open PowerShell 7: Press **Win+S**, type `pwsh`, press Enter
4. Navigate to project: `cd C:\proj\pwiz`
5. Start Claude: `claude`
6. Tell Claude: *"Please read ai/docs/new-machine-setup.md - I'm at Phase 2. Please continue."*

This ensures Claude has proper access to project files and configuration.

---

## Phase 2: Visual Studio Installation

### 2.1 Check for Visual Studio

List installed Visual Studio versions:
```powershell
Get-ChildItem "C:\Program Files\Microsoft Visual Studio" -Directory | Select-Object Name
```

Expected output shows a version folder:
- **VS 2026**: Folder named `18`
- **VS 2022**: Folder named `2022`

If no Visual Studio folder exists, guide the user:

1. Open browser to: https://visualstudio.microsoft.com/downloads/
2. Download **Visual Studio 2026 Community** (free, recommended for new installs)
3. Run the installer

> **Alternative:** Visual Studio 2022 is also fully supported if you prefer it or already have it installed.

### 2.2 Required Workloads

Tell the user to select these workloads in the Visual Studio Installer:
- **.NET desktop development** (required)
- **Desktop development with C++** (required)

Also verify in "Individual components":
- **.NET Framework 4.7.2 targeting pack** (should be included, but verify)

If the targeting pack is not available in the VS installer, download the Developer Pack directly:
- https://dotnet.microsoft.com/download/dotnet-framework/net472

Tell user: "Click Install and wait for completion. This may take 15-30 minutes."

### 2.3 Verify Installation

After VS installation, verify the edition is installed:
```powershell
# For VS 2026:
Get-ChildItem "C:\Program Files\Microsoft Visual Studio\18" -Directory -ErrorAction SilentlyContinue | Select-Object Name

# For VS 2022:
Get-ChildItem "C:\Program Files\Microsoft Visual Studio\2022" -Directory -ErrorAction SilentlyContinue | Select-Object Name
```

Expected output shows one of: `Community`, `Professional`, or `Enterprise`.

> **Note:** The C++ compiler (`cl.exe`) is NOT in the system PATH. This is expected. It's only available from the Developer Command Prompt or when invoked through MSBuild. The real verification is whether the build succeeds in Phase 4.

If the Visual Studio folder is empty or missing the edition subfolder, the user needs to:
1. Open Visual Studio Installer
2. Click "Modify" on their VS installation
3. Ensure both workloads are checked:
   - ".NET desktop development"
   - "Desktop development with C++"
4. Click "Modify" to install

---

## Phase 3: Developer Tools

These tools are essential for productive Skyline development.

### 3.1 Essential Tools

Install these core tools (all available via winget except ReSharper):

**TortoiseGit** - Windows Explorer integration for Git:
```powershell
winget install TortoiseGit.TortoiseGit --accept-source-agreements --accept-package-agreements
```
After installation, restart Windows Explorer to enable TortoiseGit status icons:
1. Open **Task Manager** (Ctrl+Shift+Esc)
2. Find **Windows Explorer** in the list
3. Right-click it → **Restart**
4. Open File Explorer and navigate to `C:\proj\pwiz` - you should see green checkmarks on files indicating Git status

**Notepad++** - Lightweight text editor with syntax highlighting:
```powershell
winget install Notepad++.Notepad++ --accept-source-agreements --accept-package-agreements
```

**ReSharper** - JetBrains code analysis extension for Visual Studio:
1. Go to: https://www.jetbrains.com/resharper/download/
2. Download and run the installer
3. A JetBrains license is required (30-day trial available)
4. Restart Visual Studio after installation

> **Note:** ReSharper requires a paid license but is highly recommended. The Skyline team uses it extensively.

### 3.2 Optional Utilities

These are useful but not required. **Offer to install these for the user** - since they're quick winget installs, most developers will appreciate having them:

**WinMerge** - File and folder comparison tool:
```powershell
winget install WinMerge.WinMerge --accept-source-agreements --accept-package-agreements
```

**EmEditor** - Text editor optimized for very large files (useful for large .sky XML files):
```powershell
winget install Emurasoft.EmEditor --accept-source-agreements --accept-package-agreements
```

**WinSCP** - SFTP/SCP client for file transfers:
```powershell
winget install WinSCP.WinSCP --accept-source-agreements --accept-package-agreements
```

### 3.3 AI Development Tools

These CLI tools support AI-assisted development workflows:

**GitHub CLI** - For PR creation, issue management:
```powershell
winget install GitHub.cli --accept-source-agreements --accept-package-agreements
```

After installation, authenticate:
```powershell
gh auth login
```
(Choose GitHub.com, HTTPS, authenticate with browser)

> **Note:** When the browser asks for an 8-digit code, look in the terminal window you started from - the code is displayed there, not in an authenticator app.

**ReSharper CLI** - Code inspection from command line:
```powershell
dotnet tool install -g JetBrains.ReSharper.GlobalTools
```

**dotCover CLI** - Code coverage analysis:
```powershell
dotnet tool install --global JetBrains.dotCover.CommandLineTools
```

**Python packages** - For MCP servers and LabKey integration:
```powershell
pip install mcp labkey
```

---

## Phase 4: Initial Build

### 4.1 Vendor License Agreement

Before creating the build scripts, **ask the user to review the vendor licenses**:

> "Building Skyline requires accepting vendor SDK licenses. Please review the licenses at:
> http://proteowizard.org/licenses.html
>
> Do you agree to these license terms?"

**Wait for explicit confirmation before proceeding.** The build scripts include `--i-agree-to-the-vendor-licenses` which indicates acceptance.

### 4.2 Create Build Scripts

Once the user agrees to the licenses, create two batch files at `C:\proj\pwiz`:

**b.bat** - General build script (single line):
```batch
@call "%~dp0pwiz_tools\build-apps.bat" 64 --i-agree-to-the-vendor-licenses toolset=msvc-14.5 %*
```

**bs.bat** - Skyline-specific build:
```batch
call "%~dp0b.bat" pwiz_tools\Skyline//Skyline.exe
```

> **Note on toolset**: Use `toolset=msvc-14.5` for VS 2026, or `toolset=msvc-14.3` for VS 2022.

### 4.3 Run the Build

**Important:** The build must run in a native Windows environment, not through Claude Code's bash shell.

Tell the user:
1. Open a **new Command Prompt or PowerShell window**
2. Run:
   ```cmd
   cd C:\proj\pwiz
   bs.bat
   ```
3. Wait for the build to complete (10-20 minutes on first run)
4. The build downloads vendor SDKs on first run

### 4.4 Verify Build Artifacts

After the user reports the build completed, verify:
```powershell
Test-Path "C:\proj\pwiz\pwiz_tools\Skyline\bin\x64\Release\Skyline.exe"
```

If the build failed, check `C:\proj\pwiz\build64.log` for errors. Common issues:
- Missing C++ tools: See Phase 2.3
- NuGet errors: See Troubleshooting section

---

## Phase 5: Visual Studio Configuration

### 5.1 Open Skyline Solution

Tell the user:
1. Open Visual Studio
2. File > Open > Project/Solution
3. Navigate to: `C:\proj\pwiz\pwiz_tools\Skyline\Skyline.sln`

### 5.2 Configure Build Settings

Tell the user:
1. In the toolbar, change configuration from "Debug" to **"Release"**
2. Change platform from "Any CPU" to **"x64"**

### 5.3 Configure Test Settings

Tell the user:
1. Test menu > Configure Run Settings > Select Solution Wide runsettings File
2. Navigate to: `C:\proj\pwiz\pwiz_tools\Skyline\TestSettings_x64.runsettings`

### 5.4 Disable "Just My Code"

Tell the user:
1. Tools > Options > Debugging > General
2. Uncheck "Enable Just My Code"
3. Click OK

### 5.5 Build in Visual Studio

Tell the user:
1. Build menu > Build Solution (or Ctrl+Shift+B)
2. Wait for build to complete
3. Check Output window for "Build succeeded"

---

## Phase 6: Verify Setup

> **For LLM assistants:** Read `ai/docs/build-and-test-guide.md` for detailed information about the AI build and test scripts. Key points: always use `Build-Skyline.ps1` and `Run-Tests.ps1` (never call MSBuild or TestRunner directly).

### 6.1 Build with AI Scripts

Verify the AI build scripts work correctly:
```powershell
cd C:\proj\pwiz
pwsh -Command "& './pwiz_tools/Skyline/ai/Build-Skyline.ps1'"
```

This builds the entire Skyline solution using MSBuild (matching Visual Studio's Ctrl+Shift+B).

### 6.2 Run CodeInspection Test

Run the CodeInspection test to verify test execution works:
```powershell
pwsh -Command "& './pwiz_tools/Skyline/ai/Run-Tests.ps1' -TestName CodeInspection"
```

This validates that ReSharper code inspection passes. Success means the environment is fully working.

### 6.3 Summary Checklist

Run these verification commands:
```powershell
# Git configured
git config --global core.autocrlf  # Should be: true

# Repository cloned
Test-Path C:\proj\pwiz\pwiz_tools\Skyline\Skyline.sln  # Should be: True

# Build artifacts exist (from bs.bat in Phase 4)
Test-Path C:\proj\pwiz\pwiz_tools\Skyline\bin\x64\Release\Skyline-daily.exe  # Should be: True
Test-Path C:\proj\pwiz\pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe  # Should be: True
```

---

## Phase 7: AI Tooling (Optional)

Once the basic setup is complete, configure AI-assisted development tools. Prerequisites from earlier phases (PowerShell 7, Python, GitHub CLI, etc.) should already be installed.

### 7.1 Verify Environment

Run the verification script to check all AI tooling components:
```powershell
cd C:\proj\pwiz
pwsh -Command "& './ai/scripts/Verify-Environment.ps1'"
```

This checks for all required tools and reports any missing components.

### 7.2 LabKey API Credentials

The LabKey MCP server needs credentials for skyline.ms access. Create a `.netrc` file:

```powershell
# Create the file (replace YOUR_API_KEY with your actual key)
@"
machine skyline.ms
login your.email@example.com
password YOUR_API_KEY
"@ | Out-File -FilePath "$env:USERPROFILE\.netrc" -Encoding ASCII
```

To get an API key:
1. Go to https://skyline.ms
2. Log in with your account
3. Click your username → API Keys
4. Create a new API key

### 7.3 MCP Server Configuration

Register the LabKey MCP server with Claude Code:
```powershell
claude mcp add labkey -- python C:\proj\pwiz\ai\mcp\LabKeyMcp\server.py
```

For Gmail integration (optional, for automated reports):
```powershell
claude mcp add gmail -- npx @gongrzhe/server-gmail-autoauth-mcp
```

See `ai/docs/mcp/gmail.md` for Gmail OAuth setup instructions.

### 7.4 Verify MCP Servers

Check that MCP servers are connected:
```powershell
claude mcp list
```

Expected output shows both servers connected:
```
labkey: python C:/proj/pwiz/ai/mcp/LabKeyMcp/server.py - ✓ Connected
gmail: npx @gongrzhe/server-gmail-autoauth-mcp - ✓ Connected
```

For full AI tooling documentation, see: `C:\proj\pwiz\ai\docs\developer-setup-guide.md`

---

## Phase 8: Nightly Test Setup (Optional)

Set up this machine to run Skyline nightly tests. This downloads the latest test harness from TeamCity and configures a scheduled task.

### 8.1 Choose a Nightly Folder Location

**Ask the developer where they want to store nightly test data.**

> **Important:** Nightly tests generate significant disk I/O. If the machine has a spinning hard drive (HDD), use that instead of the SSD. SSDs wear out faster under the sustained write stress that nightly tests cause.

Common locations:
- `D:\Nightly` - if D: is an HDD
- `E:\Nightly` - on machines where E: is the HDD
- `C:\Nightly` - only if no HDD is available (SSD will work, but has longevity trade-offs)

For the commands below, replace `<NightlyFolder>` with the chosen path.

### 8.2 Download SkylineNightly

Download and extract the nightly test harness:

```powershell
# Create the nightly directory (replace <NightlyFolder> with chosen path)
New-Item -ItemType Directory -Force -Path '<NightlyFolder>'

# Download SkylineNightly.zip from TeamCity (public guest access)
$url = 'https://teamcity.labkey.org/guestAuth/repository/download/bt209/.lastFinished/SkylineNightly.zip?branch=master'
Invoke-WebRequest -Uri $url -OutFile '<NightlyFolder>\SkylineNightly.zip'

# Extract the archive
Expand-Archive -Path '<NightlyFolder>\SkylineNightly.zip' -DestinationPath '<NightlyFolder>' -Force
```

Verify the extraction:
```powershell
Get-ChildItem '<NightlyFolder>'
# Should show: SkylineNightly.exe, SkylineNightlyShim.exe, DotNetZip.dll, etc.
```

### 8.3 Configure Antivirus Exclusions

**This step requires Administrator privileges.**

Nightly tests create and delete thousands of files. Real-time antivirus scanning significantly slows tests and can cause spurious failures. Add exclusions for both the source code and nightly test folders:

1. Open **Windows Security** (search for it in Start menu)
2. Go to **Virus & threat protection** → **Manage settings**
3. Scroll to **Exclusions** → **Add or remove exclusions**
4. Click **Add an exclusion** → **Folder**
5. Add these folders:
   - `C:\proj\pwiz` (source code)
   - `<NightlyFolder>` (nightly tests)

> **Security note:** These exclusions reduce protection for these folders. Only add them on development machines where you understand the trade-offs.

### 8.4 Configure Nightly Tests

Run SkylineNightly as Administrator to configure the scheduled task:

**Option A - File Explorer (easiest):**
1. Open Windows File Explorer
2. Navigate to `<NightlyFolder>`
3. Right-click **SkylineNightly.exe** → **Run as administrator**

**Option B - PowerShell:**
1. Open an **elevated PowerShell** (Run as Administrator)
2. Run:
   ```powershell
   cd <NightlyFolder>
   .\SkylineNightly.exe
   ```

In the GUI that appears:
1. Configure your preferred test schedule (typically overnight, e.g., 10 PM or later)
2. Save the configuration

The scheduled task will run `SkylineNightlyShim.exe` at your chosen time, which:
- Updates itself and `SkylineNightly.exe` from TeamCity
- Downloads the latest Skyline build
- Runs the full test suite
- Uploads results to skyline.ms

### 8.5 Verify Scheduled Task

Check that the task was created:
```powershell
Get-ScheduledTask -TaskName '*Skyline*'
```

You can also view it in Task Scheduler (taskschd.msc).

### 8.6 Test the Nightly Build (Recommended)

Before relying on the scheduled task, verify everything works by running a quick test:

1. In the SkylineNightly GUI, click the **"Now"** button
2. Wait for SkylineTester to appear and show progress
3. Verify these steps complete successfully:
   - **Checkout**: Status bar shows "Checking out Skyline (master)"
   - **Build**: Skyline compiles without errors
   - **First test**: `AaantivirusTestExclusion` passes (this test fails if antivirus exclusions aren't configured correctly)
4. Once you see tests running successfully, you can click **Stop** to end the test run

This validates that the build environment works, tests can execute, and antivirus exclusions are properly configured.

---

## Troubleshooting

### NuGet Package Errors (NU1101)

If you see "Unable to find package" errors:
1. In Visual Studio: Tools > NuGet Package Manager > Package Manager Settings
2. Click "Package Sources"
3. Add a new source:
   - Name: `nuget.org`
   - Source: `https://api.nuget.org/v3/index.json`
4. Click OK and rebuild

### Antivirus Blocking Tests

If tests fail randomly or builds are slow:
1. Open Windows Security
2. Virus & threat protection > Manage settings
3. Scroll to Exclusions > Add or remove exclusions
4. Add folder exclusion: `C:\proj\pwiz`

**Note**: This requires admin privileges and the user should understand the security implications.

### SSH Connection Refused

If `ssh -T git@github.com` fails:
- Firewall may be blocking port 22
- Try HTTPS instead: `git remote set-url origin https://github.com/ProteoWizard/pwiz.git`
- User will need to set up a GitHub Personal Access Token for HTTPS auth

---

## Success Criteria

The setup is complete when:
1. `git config --global core.autocrlf` returns `true`
2. `C:\proj\pwiz\pwiz_tools\Skyline\Skyline.sln` exists
3. `C:\proj\pwiz\pwiz_tools\Skyline\bin\x64\Release\Skyline.exe` exists
4. Visual Studio can build the solution without errors
5. `TestRunner.exe test=TestA` passes

Congratulate the user and let them know they're ready to develop Skyline!
