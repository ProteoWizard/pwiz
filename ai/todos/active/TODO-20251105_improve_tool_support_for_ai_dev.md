# TODO: Improve Tool Support for AI-Assisted Development

## Branch Information
- **Branch**: `Skyline/work/20251105_improve_tool_support_for_ai_dev`
- **Created**: 2025-11-05
- **Objective**: Enable LLM-assisted IDEs (Cursor, VS Code + Copilot/Claude Code) to run builds, tests, and static analysis autonomously to reduce friction and improve code quality validation
- **PR**: (To be added when created)

## Background

AI-assisted development has been successful but has two major friction points:

### Friction Point #1: Build/Test Delegation (ADDRESSED)
**Previous state**: LLM agents could not build or run tests, requiring manual copy-paste of errors/warnings from Visual Studio 2022 to the LLM environment.

**Problem**: 
- Slowed iteration cycles (edit → developer builds → copy errors → LLM fixes → repeat)
- LLMs couldn't validate their own changes
- Risk of committing code with compilation errors or test failures

**Solution achieved**:
- Created `Build-Skyline.ps1` PowerShell automation script
- Established project-specific `ai/` directory pattern (`pwiz_tools/Skyline/ai/`)
- LLMs can now run full build → inspect → test cycle autonomously
- Created pre-commit validation workflow documentation

### Friction Point #2: RESX File Workflow (DEFERRED)
**Current state**: Working with resource strings in .resx files is error-prone and slow with LLMs compared to ReSharper refactoring in Visual Studio.

**Problem**:
- Each localization change touches 4 files (en, ja, zh .resx + .Designer.cs)
- Move operations between .resx files touch 8 files
- LLMs frequently forget files or make mistakes
- ReSharper handles this seamlessly with refactoring

**Status**: Deferred to future work - focus on build/test tooling first

## Completed Work

### Phase 1: Build Automation ✅
- [x] Created `ai/BUILD-TEST.md` - Quick reference for build/test commands
- [x] Created `pwiz_tools/Skyline/ai/Build-Skyline.ps1` - PowerShell automation script
  - Finds MSBuild using vswhere automatically
  - Builds entire solution by default (matches VS Ctrl+Shift+B)
  - Supports specific project targets (Test, TestFunctional, etc.)
  - Clear colored output with success/failure indicators
- [x] Established pattern: Repository-wide `ai/` for guidance, project-specific `ai/` for tooling
- [x] Updated `ai/README.md` to document project-specific ai directories
- [x] Updated `ai/WORKFLOW.md` to reflect LLM build capability

### Phase 2: Test Execution ✅
- [x] Integrated TestRunner.exe execution into Build-Skyline.ps1
- [x] Support for running specific tests (`-TestName CodeInspectionTest`)
- [x] Support for running all tests in a DLL (`-RunTests`)
- [x] Proper exit code handling and log file output

### Phase 3: ReSharper Code Inspection ✅
- [x] Installed ReSharper CLI tools as .NET global tool (JetBrains.ReSharper.GlobalTools)
- [x] Integrated `jb inspectcode` into Build-Skyline.ps1
- [x] Discovered existing TeamCity validation tool: `OutputParser.exe`
- [x] Used OutputParser.exe for exact parity with TeamCity validation
- [x] Fixed project-specific .DotSettings handling (removed `--no-buildin-settings`)
- [x] Eliminated 111 false-positive LocalizableElement warnings in SkylineTester/TestRunner

### Phase 4: Pre-Commit Workflow Documentation ✅
- [x] Created `pwiz_tools/Skyline/ai/PRE-COMMIT.md` - Mandatory validation workflow
- [x] Created `pwiz_tools/Skyline/ai/README.md` - Documents project-specific tooling
- [x] Updated `ai/README.md` with prominent pre-commit validation section
- [x] Documented ReSharper CLI tool installation (modern .NET global tool method)

### Testing and Validation ✅
- [x] Demonstrated build error detection (introduced syntax error, LLM detected and fixed)
- [x] Demonstrated ReSharper warning detection (introduced redundant initializer, detected via inspection)
- [x] Confirmed OutputParser.exe integration works correctly
- [x] Validated entire workflow: Build → Inspect → Test

### Phase 5: Documentation Reorganization and Balance ✅
- [x] Identified documentation violations of TODO-20251105_reorg_md_docs.md goals
  - BUILD-TEST.md (286 lines) violated "no new core files" rule
  - Pushed total from 707 → 1005 lines (exceeded <1000 target)
  - Documentation used overly promotional "MANDATORY" language with red emojis
- [x] Created `ai/docs/documentation-maintenance.md` (550+ lines)
  - Comprehensive guide for LLMs on maintaining ai/ documentation system
  - Decision tree: where does new content go?
  - Common mistakes with ❌/✅ examples
  - Validation checklist and red flags
  - Prevention system for future documentation violations
- [x] Moved `ai/BUILD-TEST.md` → `ai/docs/build-and-test-guide.md`
  - Proper placement in detailed docs (unlimited size)
  - Updated all cross-references throughout codebase
- [x] Updated `ai/WORKFLOW.md` with minimal Build-Skyline.ps1 reference (+28 lines)
  - Essential commands only in core file
  - Pointer to detailed guide for complete reference
  - Added "Commit Messages" section with <10 line guideline
- [x] Balanced promotional tone throughout all documentation
  - Changed "MANDATORY" → "Recommended" (7 locations)
  - Removed 🔴 red emojis
  - Framed scripts as optional helpers, not requirements
- [x] Restored TODO-20251105 goals
  - Core files: 753 lines (was 1005, now under <1000 target) ✅
  - No new core files in ai/ root ✅
  - "Append-hostile" architecture maintained ✅

## Remaining Work

### Phase 6: Resolve Inspection Discrepancies
**Goal**: Understand and resolve 9 warnings reported by ReSharper 2025.2.4 CLI but not shown in VS 2022

**Current discrepancies** (as of 2025-11-06):
- 6 RedundantExplicitArrayCreation warnings
  - `CommonUtil\SystemUtil\FileLockingProcessFinder.cs:130`
  - `Controls\SequenceTree.cs:1522`
  - `Util\Adduct.cs:93`
  - `TestFunctional\PasteMoleculesTest.cs:1410`
  - `TestFunctional\PasteMoleculesTest.cs:2713`
  - `TestFunctional\ToolServiceTest.cs:198`
- 3 CSharpErrors in `EditUI\AssociateProteinsDlg.cs:445-447`
  - "Ambiguous reference" on ComponentResourceManager.GetString() calls
  - Code compiles fine, likely false positive or metadata issue

**Investigation tasks**:
- [ ] Check if RedundantExplicitArrayCreation is downgraded to SUGGESTION in .DotSettings
- [ ] Investigate CSharpErrors in AssociateProteinsDlg - are these real or false positives?
- [ ] Compare ReSharper version in VS 2022 vs. CLI tool (2025.2.4)
- [ ] Consider whether these should be fixed or excluded
- [ ] Document why TeamCity (ReSharper 9.0 from 2014) reports zero warnings on master
- [ ] Consider upgrading TeamCity ReSharper tools to match local development

### Phase 7: Optimize Inspection Performance (Optional)
**Current**: Full solution inspection takes ~16 minutes

**Possible optimizations**:
- [ ] Investigate if incremental analysis is possible
- [ ] Check if caching can be enabled
- [ ] Consider running inspection only on changed files during iteration
- [ ] Full inspection remains mandatory pre-commit

### Phase 8: Documentation and Finalization
- [ ] Document discrepancy resolution in PRE-COMMIT.md
- [ ] Update ai/docs/build-and-test-guide.md with lessons learned
- [ ] Add note about ReSharper version differences (CLI vs. VS 2022 vs. TeamCity)
- [x] Create handoff documentation for maintaining this tooling (ai/docs/documentation-maintenance.md)

## Tools & Scripts Created

### Build-Skyline.ps1
**Location**: `pwiz_tools\Skyline\ai\Build-Skyline.ps1`

**Capabilities**:
- Build entire solution or specific projects
- Run tests (all or specific test names)
- Run ReSharper code inspection with TeamCity parity
- Automatic MSBuild discovery via vswhere
- XML output parsing via OutputParser.exe
- Clear success/failure reporting with colored output

**Usage**:
```powershell
# Build entire solution
.\ai\Build-Skyline.ps1

# Pre-commit validation (MANDATORY)
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspectionTest

# Build and run specific test
.\ai\Build-Skyline.ps1 -RunTests -TestName MyTest
```

### Documentation Created

1. **`ai/docs/build-and-test-guide.md`** (286 lines, moved from ai/BUILD-TEST.md)
   - Complete build/test command reference
   - MSBuild commands, test execution, ReSharper inspection
   - Pre-commit validation workflow
   - Output interpretation guide

2. **`ai/docs/documentation-maintenance.md`** (550+ lines, NEW in Phase 5)
   - Comprehensive guide for LLMs on maintaining ai/ documentation
   - Decision tree for content placement
   - Common mistakes with examples
   - Validation checklist and prevention system

3. **`pwiz_tools/Skyline/ai/PRE-COMMIT.md`** (187 lines)
   - Recommended validation workflow before commits
   - ReSharper CLI installation instructions
   - Common issues from LLMs and how to fix them
   - Exit code interpretation

4. **`pwiz_tools/Skyline/ai/README.md`** (89 lines)
   - Documents project-specific LLM tooling directory
   - Explains separation: guidance (root ai/) vs tooling (project ai/)
   - Establishes pattern for other projects (SkylineBatch, AutoQC)

## Key Discoveries

### OutputParser.exe
**Location**: `pwiz_tools\Skyline\Executables\LocalizationHelper\OutputParser.exe`  
**Source**: `pwiz_tools\Skyline\Executables\LocalizationHelper\OutputParser\`

**Purpose**: TeamCity's validation tool for ReSharper inspection results
- Enforces `MAX_ISSUES_ALLOWED = 0` (zero tolerance for warnings)
- Filters to WARNING and ERROR severity only
- Returns exit code 0 (pass) or 1 (fail)

**Integration**: Build-Skyline.ps1 uses this exact tool for perfect TeamCity parity.

### ReSharper Settings Layers
**Discovery**: The `--no-buildin-settings` flag was preventing project-specific `.DotSettings` files from being loaded.

**Project-specific settings** that must be respected:
- `SkylineTester\SkylineTester.csproj.DotSettings` - Disables localization (`Localizable=No`)
- `TestRunner\TestRunner.csproj.DotSettings` - Disables localization (`Localizable=No`)

**Fix**: Removed `--no-buildin-settings` to allow ReSharper to discover and apply project-specific overrides.

### ReSharper Version Landscape
- **Visual Studio 2022**: Modern ReSharper (likely 2024.x or 2025.x)
- **Command-line (installed)**: ReSharper 2025.2.4 (.NET global tool)
- **TeamCity (legacy)**: ReSharper 9.0 from 2014

**Implication**: Version differences may explain some warning discrepancies. TeamCity may need modernization.

## Risks & Considerations

### Risk: Long Inspection Runtime (~16 minutes)
**Impact**: Pre-commit validation takes significant time  
**Mitigation**: 
- Developers can run just build/test during iteration
- Full inspection only required before commit
- Consider investigating caching/incremental analysis

### Risk: Version Mismatch with TeamCity
**Impact**: Local validation with ReSharper 2025.2.4 may not perfectly match TeamCity's 2014 version  
**Mitigation**: 
- Using OutputParser.exe provides some consistency
- May need to upgrade TeamCity ReSharper tools
- Document known discrepancies

### Risk: False Positives from Modern ReSharper
**Impact**: 9 warnings shown in CLI but not VS 2022  
**Mitigation**: 
- Investigate each type
- Fix genuine issues, document false positives
- Consider downgrading specific inspections in .DotSettings if needed

### Consideration: RESX Workflow Still Manual
**Status**: Friction point #2 deferred to future work  
**Rationale**: Build/test automation is higher priority and more valuable

## Success Criteria

### Minimum Viable (Achieved! ✅)
- [x] LLM-assisted IDEs can build Skyline.sln with MSBuild
- [x] LLM-assisted IDEs can run tests with TestRunner.exe
- [x] LLM-assisted IDEs can run ReSharper code inspection
- [x] Pre-commit validation workflow documented and functional
- [x] Project-specific ai/ directory pattern established

### Complete Success (In Progress)
- [x] Build automation works (Build-Skyline.ps1)
- [x] Test execution works (TestRunner integration)
- [x] ReSharper inspection works (jb inspectcode + OutputParser.exe)
- [x] TeamCity parity achieved for localization checks (eliminated false positives)
- [x] Documentation reorganization complete (Phase 5)
  - Core files back under 1000 lines (753 total)
  - Balanced tone throughout (removed "MANDATORY" language)
  - Created documentation-maintenance.md prevention system
  - Added commit message guidelines (<10 lines) to WORKFLOW.md
- [ ] Resolve or document 9 remaining warning discrepancies
- [ ] CodeInspectionTest passes in automation
- [ ] Process validated across Cursor, VS Code + Copilot, VS Code + Claude Code

### Stretch Goals (Future)
- [ ] RESX file workflow optimization (friction point #2)
- [ ] Incremental inspection for faster iteration
- [ ] Upgrade TeamCity to modern ReSharper tools
- [ ] Automated pre-commit hooks

## Files Modified/Created

### New Files (Phase 1-4)
- `pwiz_tools/Skyline/ai/Build-Skyline.ps1` - Build automation script (PowerShell)
- `pwiz_tools/Skyline/ai/Run-Tests.ps1` - Test execution helper (PowerShell)
- `pwiz_tools/Skyline/ai/README.md` - Project tooling documentation
- `pwiz_tools/Skyline/ai/PRE-COMMIT.md` - Pre-commit validation workflow

### New Files (Phase 5 - Documentation Reorganization)
- `ai/docs/build-and-test-guide.md` - Moved from ai/BUILD-TEST.md (286 lines)
- `ai/docs/documentation-maintenance.md` - LLM documentation maintenance guide (550+ lines)

### Modified Files (Phase 1-4)
- `ai/README.md` - Added build automation section, balanced promotional tone
- `ai/WORKFLOW.md` - Added Build-Skyline.ps1 section (+18 lines)
- `pwiz_tools/Skyline/TestData/ResourcesTest.cs` - Fixed test error (removed incomplete `int x`)

### Modified Files (Phase 5 - Documentation Reorganization)
- `ai/README.md` - Removed BUILD-TEST.md references, updated file size targets
- `ai/WORKFLOW.md` - Replaced outdated build section with balanced version
- `ai/docs/README.md` - Added documentation-maintenance.md and build-and-test-guide.md to index
- `pwiz_tools/Skyline/ai/README.md` - Updated BUILD-TEST.md reference → build-and-test-guide.md
- `pwiz_tools/Skyline/ai/PRE-COMMIT.md` - Changed "MANDATORY" → "Recommended", updated references

### Deleted Files (Phase 5)
- `ai/BUILD-TEST.md` - Moved to ai/docs/build-and-test-guide.md
- `ai/todos/active/TODO-20251106_improve_tool_support_for_ai_dev.md` - Duplicate file, removed

### Test Files (Used for Validation, Can Be Reverted)
- `pwiz_tools/Skyline/SkylineFiles.cs:120` - Added redundant initializer (`= null`)
  - **Action before commit**: Remove this test warning

## Handoff Prompt for Branch Creation

```
I'm working on improving tool support for LLM-assisted development environments.

Context: We've created build automation (Build-Skyline.ps1) that enables Cursor and other LLM-assisted IDEs to run MSBuild, TestRunner.exe, and ReSharper code inspection autonomously. This eliminates the friction of manual copy-paste from Visual Studio.

Current status: Core functionality complete and tested. Remaining work is resolving 9 warning discrepancies between ReSharper 2025.2.4 CLI and Visual Studio 2022.

Read ai/todos/active/TODO-20251105_improve_tool_support_for_ai_dev.md for complete context.

Next: Investigate the 9 remaining inspection warnings and determine if they're genuine issues, false positives, or need .DotSettings configuration.
```

## Notes

### Key Insight: Settings Layer Architecture
ReSharper has layered settings (Global → Solution → Project). The `--no-buildin-settings` flag was suppressing project-level overrides, causing false positives. Removing this flag fixed 111 spurious warnings.

### Key Pattern: Project-Specific AI Directories
Established pattern for organizing LLM tooling:
- **Repository-wide** (`/ai/`): Rules, patterns, workflows, style guides
- **Project-specific** (`pwiz_tools/Skyline/ai/`): Build scripts, test helpers, automation

This pattern scales to other projects: SkylineBatch, AutoQC, etc.

### Validation Methodology
Demonstrated complete autonomous cycle:
1. Introduce compilation error → LLM detects via MSBuild
2. Introduce ReSharper warning → LLM detects via inspection
3. LLM fixes both without Git diff or Visual Studio

### TeamCity Alignment
Using the exact same `OutputParser.exe` that TeamCity uses ensures local validation matches CI requirements (MAX_ISSUES_ALLOWED = 0).

