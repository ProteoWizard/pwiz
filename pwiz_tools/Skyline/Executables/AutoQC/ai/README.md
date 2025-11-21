# AutoQC LLM Tooling

Scripts and tools for LLM-assisted development of AutoQC.

## Contents

### Build-AutoQC.ps1
PowerShell script for building and testing AutoQC from LLM-assisted IDEs.

**Usage** (from `pwiz_tools\Skyline\Executables\AutoQC`):
```powershell
# Build solution
.\ai\Build-AutoQC.ps1

# Build and run tests
.\ai\Build-AutoQC.ps1 -RunTests

# Build, inspect, and test
.\ai\Build-AutoQC.ps1 -RunInspection -RunTests

# Release build
.\ai\Build-AutoQC.ps1 -Configuration Release
```

## Panorama Credentials for Testing

Many AutoQC tests require Panorama credentials. Tests will fail gracefully with setup instructions if credentials are not configured.

### Setting Up Credentials

**Option 1: PowerShell (Recommended)**
```powershell
# Set User-level environment variables (no admin needed)
[Environment]::SetEnvironmentVariable("PANORAMAWEB_USERNAME", "your.name@yourdomain.edu", "User")
[Environment]::SetEnvironmentVariable("PANORAMAWEB_PASSWORD", "your_password", "User")

# Restart Visual Studio or LLM-assisted IDE to pick up the new variables
```

**Option 2: Windows GUI**
1. Windows Search → "Environment Variables"
2. Click "Edit environment variables for your account"
3. Under "User variables" (NOT System variables), add:
   - `PANORAMAWEB_USERNAME` = `your.name@yourdomain.edu`
   - `PANORAMAWEB_PASSWORD` = `your_password`
4. Restart Visual Studio or LLM-assisted IDE

### Clearing Credentials

```powershell
# Clear User-level variables (no admin needed)
[Environment]::SetEnvironmentVariable("PANORAMAWEB_USERNAME", $null, "User")
[Environment]::SetEnvironmentVariable("PANORAMAWEB_PASSWORD", $null, "User")

# Restart Visual Studio or LLM-assisted IDE
```

### Important Notes

- **Use "User" level, NOT "Machine" level**
  - User-level: No admin privileges required, affects only your account
  - Machine-level: Requires admin, affects all users (not recommended)
- **Restart required**: Environment variable changes require restart of Visual Studio/IDE
- **Fresh PowerShell**: Use a NEW PowerShell window to verify variables are set correctly
- **Verification**: `[Environment]::GetEnvironmentVariable("PANORAMAWEB_PASSWORD", "User")`

### For LLM-Assisted Development

When tests fail with missing credentials:
1. The error message references `TestUtils.cs` documentation (lines 40-66)
2. Follow the setup instructions above
3. Verify with a fresh PowerShell: `.\ai\Build-AutoQC.ps1 -RunTests`
4. Expected: 19/19 tests pass if credentials are valid, 12/19 fail gracefully if not set

## Differences from Skyline

AutoQC uses **standard MSTest** for testing:
- Tests run with `vstest.console.exe` (Visual Studio Test Platform)
- No multi-language support (English only)
- No custom test runner infrastructure
- Simpler test execution model

## Purpose

This directory follows the pattern established in `pwiz_tools/Skyline/ai/`:
- **Project-specific tooling** lives in project's `ai/` directory
- **Repository-wide guidance** lives in `/ai/` at repository root

See [../../../../ai/docs/documentation-maintenance.md](../../../../ai/docs/documentation-maintenance.md) for the full LLM documentation system architecture.

