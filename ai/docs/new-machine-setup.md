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

### 1.3 Configure Git Line Endings

```powershell
git config --global core.autocrlf true
git config --global pull.rebase false
```

Verify:
```powershell
git config --global core.autocrlf
# Should output: true
```

### 1.4 SSH Key Setup

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

### 1.5 Test GitHub SSH Access

```powershell
ssh -T git@github.com
```

Expected: "Hi username! You've successfully authenticated..."

If it fails with host key verification, the user needs to type "yes" to accept GitHub's fingerprint.

### 1.6 Clone the Repository

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

---

## Phase 2: Visual Studio Installation

### 2.1 Check for Visual Studio 2022

```powershell
Test-Path "C:\Program Files\Microsoft Visual Studio\2022\*\Common7\IDE\devenv.exe"
```

If not found, guide the user:

1. Open browser to: https://visualstudio.microsoft.com/downloads/
2. Download **Visual Studio 2022 Community** (free)
3. Run the installer

### 2.2 Required Workloads

Tell the user to select these workloads in the Visual Studio Installer:
- **.NET desktop development** (required)
- **Desktop development with C++** (required)

Also verify in "Individual components":
- **.NET Framework 4.7.2 targeting pack** (should be included, but verify)

Tell user: "Click Install and wait for completion. This may take 15-30 minutes."

### 2.3 Verify C++ Tools

After VS installation, open a NEW terminal and run:
```powershell
where.exe cl
```

If `cl` is not found, the C++ workload wasn't installed correctly. User needs to:
1. Open Visual Studio Installer
2. Click "Modify" on their VS 2022 installation
3. Ensure "Desktop development with C++" is checked
4. Click "Modify" to install

---

## Phase 3: Initial Build

### 3.1 Run quickbuild.bat

```powershell
cd C:\proj\pwiz
.\quickbuild.bat
```

This script:
- Downloads vendor SDKs (first run only)
- Accepts license agreements
- May take 10-20 minutes on first run

**Note**: If it prompts about vendor licenses, the user should read and accept if they agree.

### 3.2 Verify Build Artifacts

```powershell
Test-Path "C:\proj\pwiz\pwiz_tools\Skyline\bin\x64\Release\Skyline.exe"
```

If the build failed, check the output for errors. Common issues:
- Missing C++ tools: See Phase 2.3
- NuGet errors: See Phase 4 troubleshooting

---

## Phase 4: Visual Studio Configuration

### 4.1 Open Skyline Solution

Tell the user:
1. Open Visual Studio 2022
2. File > Open > Project/Solution
3. Navigate to: `C:\proj\pwiz\pwiz_tools\Skyline\Skyline.sln`

### 4.2 Configure Build Settings

Tell the user:
1. In the toolbar, change configuration from "Debug" to **"Release"**
2. Change platform from "Any CPU" to **"x64"**

### 4.3 Configure Test Settings

Tell the user:
1. Test menu > Configure Run Settings > Select Solution Wide runsettings File
2. Navigate to: `C:\proj\pwiz\pwiz_tools\Skyline\TestSettings_x64.runsettings`

### 4.4 Disable "Just My Code"

Tell the user:
1. Tools > Options > Debugging > General
2. Uncheck "Enable Just My Code"
3. Click OK

### 4.5 Build in Visual Studio

Tell the user:
1. Build menu > Build Solution (or Ctrl+Shift+B)
2. Wait for build to complete
3. Check Output window for "Build succeeded"

---

## Phase 5: Verify Setup

### 5.1 Run a Quick Test

```powershell
cd C:\proj\pwiz\pwiz_tools\Skyline
.\bin\x64\Release\TestRunner.exe test=TestA
```

This runs a fast subset of tests. Success means the environment is working.

### 5.2 Summary Checklist

Run these verification commands:
```powershell
# Git configured
git config --global core.autocrlf  # Should be: true

# Repository cloned
Test-Path C:\proj\pwiz\pwiz_tools\Skyline\Skyline.sln  # Should be: True

# Build artifacts exist
Test-Path C:\proj\pwiz\pwiz_tools\Skyline\bin\x64\Release\Skyline.exe  # Should be: True
Test-Path C:\proj\pwiz\pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe  # Should be: True
```

---

## Phase 6: AI Tooling (Optional)

Once the basic setup is complete, the developer can add AI-assisted development tools.

Now that the repository is cloned, run:
```powershell
cd C:\proj\pwiz
pwsh -File ai\scripts\Verify-Environment.ps1
```

If PowerShell 7 is not installed, install it first:
```powershell
winget install Microsoft.PowerShell
```

Then restart the terminal and run the verification script. It will identify any missing AI tooling components.

For full AI tooling setup, see: `C:\proj\pwiz\ai\docs\developer-setup-guide.md`

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
