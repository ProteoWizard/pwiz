# TODO-audit_resharper_localization_settings.md

## Branch Information
- **Branch**: `Skyline/work/20251114_audit_resharper_localization_settings`
- **Created**: 2025-11-14
- **PR**: (TBD)
- **Objective**: Audit and configure ReSharper localization settings for all .csproj files in Skyline.sln to ensure user-facing strings are properly flagged for translation

## Background

During testing, we discovered that ReSharper's localization warnings are not enabled for several DLLs in the Skyline solution, despite these projects containing user-facing strings that require translation to Chinese and Japanese. The main Skyline EXE project has localization settings properly configured via `.csproj.DotSettings` files, but many supporting projects do not.

### Current State
Using the PowerShell script `scripts/misc/check-csproj-l10n-settings.ps1`, we identified the following projects without `.DotSettings` files:

**High Priority (contain user-facing strings):**
- `PanoramaClient.csproj` - Contains user-facing messages (confirmed issue found here)
- `CommonUtil.csproj` - Likely contains user-facing utility messages

**Medium Priority (may contain user-facing strings):**
- `ProteowizardWrapper.csproj` - Wrapper library, may have error messages
- `CommonMsData.csproj` - Data access library, may have error messages
- `SkylineTool.csproj` - Tool project, likely minimal UI
- `SkylineCmd.csproj` - Command-line tool, may have user messages

**Low Priority / Excluded:**
- `ZedGraph.csproj` - 3rd party open-source charting library (has its own translation)
- `BullseyeSharp.csproj` - 3rd party build tool (submodule)
- `SkylineNightly.csproj` - Internal test tool (not user-facing)
- `SkylineNightlyShim.csproj` - Internal test tool (not user-facing)

### Localization Settings
Projects with proper localization settings have a `.csproj.DotSettings` file containing:

```xml
<wpf:ResourceDictionary xml:space="preserve" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:s="clr-namespace:System;assembly=mscorlib" xmlns:ss="urn:shemas-jetbrains-com:settings-storage-xaml" xmlns:wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
	<s:String x:Key="/Default/CodeEditing/Localization/Localizable/@EntryValue">Yes</s:String>
	<s:String x:Key="/Default/CodeEditing/Localization/LocalizableInspector/@EntryValue">Pessimistic</s:String>
</wpf:ResourceDictionary>
```

Where:
- **`Localizable: Yes`** - Enables localization inspection for the project
- **`LocalizableInspector: Pessimistic`** - Assumes all string literals should be localized unless explicitly marked otherwise (strictest mode)

## Task Checklist

### Phase 1: Audit and Discovery
- [x] Run `check-csproj-l10n-settings.ps1` to confirm current state
- [ ] For each high-priority project, do a quick audit of string literals:
  - [x] `PanoramaClient.csproj` - Contains many user-facing strings and has existing .resx resources
  - [x] `CommonUtil.csproj` - Contains user-facing messages and .resx resources
- [ ] For each medium-priority project, do a quick audit:
  - [x] `ProteowizardWrapper.csproj` - Mostly technical strings; some exceptions thrown; likely OK to enable
  - [x] `CommonMsData.csproj` - Has multiple resource files and user-facing messages
  - [x] `SkylineTool.csproj` - Minimal strings; mostly protocol/identifiers
  - [x] `SkylineCmd.csproj` - Has resources and user messages (CLI output)
- [ ] Document findings: which projects definitely need settings vs. which can be skipped

Findings from script (missing .DotSettings):
- High priority: PanoramaClient, CommonUtil
- Medium priority: CommonMsData, SkylineCmd, SkylineTool, ProteowizardWrapper
- Exclusions/low: ZedGraph (3rd party), BullseyeSharp (3rd party), SkylineNightly, SkylineNightlyShim (internal tools)

### Phase 2: Configure High Priority Projects
- [x] Copy template `.DotSettings` file to high-priority projects
- [x] `PanoramaClient.csproj.DotSettings` - Created
- [x] `CommonUtil.csproj.DotSettings` - Created
- [ ] Open each project in Visual Studio and verify ReSharper shows localization warnings
- [ ] Commit the `.DotSettings` files with message: "Enable ReSharper localization inspection for [project names]"

### Phase 3: Address Flagged String Literals (High Priority)
- [ ] Review all strings flagged by ReSharper in `PanoramaClient`
- [ ] For each flagged string, determine if it needs localization:
  - [ ] User-facing messages → Move to resources for translation
  - [ ] Internal/debug strings → Mark with `[Localizable(false)]` attribute
  - [ ] Constants/identifiers → Mark with `[Localizable(false)]` attribute
- [ ] Review all strings flagged by ReSharper in `CommonUtil`
- [ ] Apply same categorization as above
- [ ] Commit changes incrementally by category/file

### Phase 4: Configure Medium Priority Projects (Optional)
- [x] Based on Phase 1 audit, decide which medium-priority projects need settings
  - Selected now: `SkylineCmd`, `CommonMsData`
  - Defer: `ProteowizardWrapper` (technical messages), `SkylineTool` (minimal UI)
- [x] Create `.DotSettings` files for selected projects
  - [x] `SkylineCmd.csproj.DotSettings` - Created
  - [x] `CommonMsData.csproj.DotSettings` - Created
- [ ] Address any critical user-facing strings found
- [x] Document decision for projects that don't need settings (currently deferring `ProteowizardWrapper`, `SkylineTool`)

### Phase 5: Documentation and Process
- [ ] Update project documentation about localization expectations
- [ ] Add note about `.DotSettings` files in relevant developer docs
- [ ] Consider adding `check-csproj-l10n-settings.ps1` to periodic code health checks
- [ ] Document patterns for when to use `[Localizable(false)]` attribute

## Tools & Scripts

### check-csproj-l10n-settings.ps1
Located at: `scripts/misc/check-csproj-l10n-settings.ps1`

**Purpose**: Scans a solution file and reports which .csproj files are missing `.DotSettings` localization configuration files.

**Usage**:
```powershell
.\scripts\misc\check-csproj-l10n-settings.ps1 -SolutionPath ".\pwiz_tools\Skyline\Skyline.sln"
```

**Output**: Lists projects without `.DotSettings` files (excluding projects with "Test" in the name)

### Quick String Literal Audit (PowerShell)
```powershell
# Quick check for string literals in a project
Get-ChildItem -Path ".\path\to\project" -Filter "*.cs" -Recurse | 
    Select-String -Pattern '"[^"]{10,}"' | 
    Where-Object { $_.Line -notmatch '//|AssemblyInfo|namespace|using' } |
    Select-Object -First 20
```

This shows longer string literals (10+ chars) in the code, excluding common non-user-facing patterns.

### ReSharper Attribute for Non-Localizable Strings
```csharp
using System.ComponentModel;

// For individual strings that shouldn't be localized:
[Localizable(false)]
string debugMessage = "Internal error code: 0x1234";
```

## Risks & Considerations

### Risk: Large Volume of Flagged Strings
- **Impact**: Enabling "Pessimistic" mode may reveal hundreds of strings in each project
- **Mitigation**: Work incrementally, one project at a time. Use `[Localizable(false)]` liberally for non-user-facing strings
- **Alternative**: Could use "Optimistic" mode initially, then switch to "Pessimistic" later

### Risk: Breaking Existing Translations
- **Impact**: Moving strings to resource files might break existing translations if not careful
- **Mitigation**: Review existing resource files before making changes. Ensure consistency with current translation patterns

### Risk: False Positives
- **Impact**: ReSharper may flag strings that are genuinely non-localizable (SQL queries, file paths, protocols)
- **Mitigation**: Use `[Localizable(false)]` attribute liberally. This is expected and normal

### Risk: Third-Party Code
- **Impact**: Some projects contain or wrap third-party code we can't easily translate
- **Mitigation**: Document which projects are excluded and why. Accept that some command-line tools won't be translated

### Consideration: Test Projects
- **Note**: Test projects are automatically excluded from the audit script (filter by "Test" in name)
- **Rationale**: Test strings don't need translation as they're only used internally

### Consideration: Incremental Approach
- **Strategy**: It's better to enable settings and address issues in one project at a time rather than enabling all at once
- **Benefit**: Easier to review, test, and commit incremental changes

## Success Criteria

- [ ] All high-priority projects (`PanoramaClient`, `CommonUtil`) have `.DotSettings` files configured
- [ ] All user-facing strings in high-priority projects are either:
  - Moved to resource files for translation, OR
  - Marked with `[Localizable(false)]` if non-user-facing
- [ ] No ReSharper localization warnings remain unaddressed in high-priority projects
- [ ] Medium-priority projects are audited and decisions documented
- [ ] Script `check-csproj-l10n-settings.ps1` is committed to `scripts/misc/`
- [ ] Team documentation updated with localization best practices
- [ ] All changes build successfully and pass existing tests

## Handoff Prompt for Branch Creation

```
I need to audit and configure ReSharper localization settings for the Skyline solution.

Background: We discovered that ReSharper is not warning about string literals needing localization in several DLLs. The main Skyline EXE has proper settings, but supporting libraries like PanoramaClient and CommonUtil do not.

Please:
1. Read this TODO file completely to understand the scope
2. Create branch: Skyline/work/YYYYMMDD_audit_resharper_localization_settings
3. Move this TODO from todos/backlog/ to todos/active/ with the date prefix
4. Run the audit script to confirm current state: .\scripts\misc\check-csproj-l10n-settings.ps1 -SolutionPath ".\pwiz_tools\Skyline\Skyline.sln"
5. Start with Phase 1: Audit high-priority projects to understand scope of work

The goal is to ensure all user-facing strings in our DLLs are properly flagged for translation to Chinese and Japanese.
```

## Notes

### Alternative Approaches Considered

1. **Assembly-level attribute**: Could add `[assembly: Localizable(true)]` to `AssemblyInfo.cs`
   - **Pro**: Single-line change per project
   - **Con**: Less flexible than `.DotSettings` files, team-wide ReSharper setting

2. **EditorConfig approach**: Could use `.editorconfig` for localization settings
   - **Pro**: Standard format, works across tools
   - **Con**: ReSharper `.DotSettings` is more established in this codebase

3. **Enable all projects at once**: Could add settings to all projects immediately
   - **Pro**: Complete coverage immediately
   - **Con**: Overwhelming number of warnings, harder to review systematically

### Decision: Incremental .DotSettings Approach
We're using `.DotSettings` files applied incrementally because:
- Consistent with existing projects that have localization configured
- Allows targeted, reviewable changes
- Team already familiar with this approach
- Can be committed to source control and shared

### Related Work
This work may spawn follow-up TODOs for:
- Moving specific user-facing strings to resource files
- Improving translation infrastructure for libraries
- Documenting localization patterns for future development
