# Documentation Maintenance Guide for LLMs

**READ THIS BEFORE creating new .md files or modifying existing documentation.**

This guide explains how to maintain the ai/ documentation system without violating its design principles.

## Core Principle: Append-Hostile Architecture

The ai/ documentation system is **intentionally designed to resist unbounded growth**:

- **Core files are finished reference cards** - not living documents
- **Core files have strict line limits** - exceeding them is a design failure
- **New information goes to ai/docs/** - never grows core files beyond targets
- **No new core files** - the structure is complete

## The Five Core Files (NEVER exceed limits)

| File | Purpose | Line Limit | Current |
|------|---------|------------|---------|
| **CRITICAL-RULES.md** | Bare constraints only, no explanations | <100 | 81 |
| **MEMORY.md** | Project context pointers | <200 | 144 |
| **WORKFLOW.md** | Git workflows, TODO system, build basics | <200 | 166 |
| **STYLEGUIDE.md** | Essential coding patterns | <200 | 162 |
| **TESTING.md** | Testing essentials | <200 | 154 |
| **TOTAL** | **All core files combined** | **<1000** | **707** |

**These limits are not suggestions - they are design constraints.**

## Decision Tree: Where Does My Content Go?

### When Adding New Documentation

```
START: I want to add documentation about X

├─ Is X a new core topic (build system, testing, workflow, style)?
│  ├─ YES → STOP. The five core files already cover all topics.
│  │         Add details to existing ai/docs/ files or create new one there.
│  └─ NO → Continue
│
├─ Is X a critical constraint (NEVER/ALWAYS rule)?
│  ├─ YES → Check CRITICAL-RULES.md line count
│  │  ├─ Under 100 lines → Add to CRITICAL-RULES.md
│  │  └─ At/over 100 lines → Add to relevant ai/docs/ file, reference in CRITICAL-RULES.md
│  └─ NO → Continue
│
├─ Is X a pointer to important project context?
│  ├─ YES → Check MEMORY.md line count
│  │  ├─ Under 200 lines → Add brief pointer to MEMORY.md
│  │  └─ At/over 200 lines → Add details to ai/docs/project-context.md
│  └─ NO → Continue
│
├─ Is X a git workflow or TODO system process?
│  ├─ YES → Check WORKFLOW.md line count
│  │  ├─ Under 200 lines → Add essential pattern to WORKFLOW.md
│  │  └─ At/over 200 lines → Add details to ai/docs/workflow-guide.md
│  └─ NO → Continue
│
├─ Is X a coding style or naming convention?
│  ├─ YES → Check STYLEGUIDE.md line count
│  │  ├─ Under 200 lines → Add essential rule to STYLEGUIDE.md
│  │  └─ At/over 200 lines → Add details to ai/docs/style-guide.md
│  └─ NO → Continue
│
└─ Is X about writing or structuring tests?
   ├─ YES → Check TESTING.md line count
   │  ├─ Under 200 lines → Add essential pattern to TESTING.md
   │  └─ At/over 200 lines → Add details to ai/docs/testing-patterns.md
   └─ NO → Add to ai/docs/ with descriptive filename

RESULT: NEVER create new .md files in ai/ root (except README.md already exists)
```

## Common Mistakes and Corrections

### ❌ MISTAKE 1: Creating New Core Files

**Wrong:**
```
ai/
├── BUILD-TEST.md          ← NEW 286-line file
├── DEPLOYMENT.md          ← NEW file
├── ARCHITECTURE.md        ← NEW file
```

**Why wrong:** Violates "five core files only" rule. Adds 286 lines to total (707 → 993).

**Correct:**
```
ai/docs/
├── build-and-test-guide.md     ← Detailed reference (unlimited size)
├── deployment-guide.md         ← Deployment details
├── architecture-overview.md    ← Architecture deep-dive

ai/WORKFLOW.md                   ← Add ~15 line pointer to build guide
```

### ❌ MISTAKE 2: Growing Core Files Beyond Limits

**Wrong:**
```markdown
# ai/TESTING.md (154 → 380 lines)

## [Existing content...]

## MSBuild Commands                    ← Adding 100 lines
[Detailed MSBuild reference...]

## ReSharper Code Inspection            ← Adding 80 lines
[Complete inspection workflow...]

## Pre-Commit Validation Workflow       ← Adding 46 lines
[Detailed validation steps...]
```

**Why wrong:** Grows TESTING.md from 154 → 380 lines (exceeds <200 limit). Total: 707 → 933 lines.

**Correct:**
```markdown
# ai/TESTING.md (154 → 165 lines)

## [Existing content...]

## Build and Test Automation (Optional)

For build/test commands, see [docs/build-and-test-guide.md](docs/build-and-test-guide.md).

Quick pre-commit validation:
```powershell
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection
```

# ai/docs/build-and-test-guide.md (NEW, 286 lines)
[All the detailed MSBuild, ReSharper, and pre-commit content]
```

### ❌ MISTAKE 3: Duplicating Content Across Files

**Wrong:**
```markdown
# ai/WORKFLOW.md
## Build Commands
[200 lines of MSBuild details]

# ai/BUILD-TEST.md
## Build Commands
[Same 200 lines duplicated]
```

**Why wrong:** Violates DRY. Maintenance burden. Wastes tokens.

**Correct:**
```markdown
# ai/WORKFLOW.md
## Build Automation
See [docs/build-and-test-guide.md](docs/build-and-test-guide.md).

# ai/docs/build-and-test-guide.md
## Build Commands
[Single source of truth - 200 lines]
```

### ❌ MISTAKE 4: Over-Promoting Optional Tooling

**Wrong:**
```markdown
## 🔴 MANDATORY: Pre-Commit Validation

**Before committing ANY LLM-generated code**, run:
```

**Why wrong:** Creates false impression that optional helper scripts are required infrastructure.

**Correct:**
```markdown
## Build and Test Automation (Optional)

**Recommended before committing** (helps prevent CI failures):
```

Pattern: Optional tools should be framed as helpers, not requirements.

## When You're Tempted to Add Documentation

**Ask yourself:**

1. **Does this belong in existing ai/docs/ files?**
   - If yes: Add to appropriate ai/docs/ file
   - If no: Continue

2. **Is this a new detailed topic needing its own guide?**
   - If yes: Create **ai/docs/topic-name.md** (NOT ai/topic-name.md)
   - If no: Continue

3. **Is this a brief pointer/reference for a core file?**
   - Check if adding it exceeds the core file's line limit
   - If under limit: Add brief pointer, put details in ai/docs/
   - If at/over limit: Only add to ai/docs/, reference from core file if critical

4. **Does this content already exist elsewhere?**
   - Search ai/docs/ first
   - If exists: Add to existing file or update it
   - If not: Create new ai/docs/ file

## Project-Specific ai/ Directories

**Pattern:** Projects can have their own `ai/` directories for **tooling only**.

**Correct usage:**
```
pwiz_tools/Skyline/ai/
├── Build-Skyline.ps1          ← PowerShell automation script
├── Run-Tests.ps1              ← Test execution helper
├── PRE-COMMIT.md              ← Workflow guide for these scripts
└── README.md                  ← Brief overview of scripts
```

**Purpose:** Build/test automation scripts specific to that project.

**NOT for:**
- ❌ Coding guidelines (goes in root ai/STYLEGUIDE.md)
- ❌ Project architecture (goes in root ai/docs/)
- ❌ Testing patterns (goes in root ai/TESTING.md or ai/docs/testing-patterns.md)

**Rule:** Project-specific ai/ contains **executable tooling and their usage docs**, not general guidance.

## File Size Management Strategy

### If You Must Add to Core Files

**Before adding content:**

1. **Check current line count:**
   ```bash
   wc -l ai/CRITICAL-RULES.md ai/MEMORY.md ai/WORKFLOW.md ai/STYLEGUIDE.md ai/TESTING.md
   ```

2. **Calculate impact:**
   - Current total: 707 lines
   - Your addition: X lines
   - New total: 707 + X
   - Target: <1000 lines
   - Remaining budget: 293 lines

3. **If addition exceeds budget:**
   - Move existing detailed content from core file → ai/docs/
   - Replace with brief pointer
   - Add your new content to ai/docs/ as well

### Slimming Strategy (When Core Files Grow Too Large)

If you find core files have grown beyond limits:

1. **Identify detailed sections** (examples, long explanations, edge cases)
2. **Move to appropriate ai/docs/ file** (create if needed)
3. **Replace with pointer:**
   ```markdown
   See [docs/detailed-topic.md](docs/detailed-topic.md) for complete details.
   ```
4. **Verify new line count is under limit**

### Example: Slimming WORKFLOW.md

**Before (250 lines - exceeds <200 limit):**
```markdown
## Workflow 1: Feature Branch
[10 lines of essential pattern]
[90 lines of detailed examples, edge cases, troubleshooting]

## Workflow 2: Bugfix
[10 lines of essential pattern]
[85 lines of detailed scenarios]
```

**After (165 lines - under limit):**
```markdown
## Workflow 1: Feature Branch
[10 lines of essential pattern]

See [docs/workflow-guide.md#workflow-1](docs/workflow-guide.md#workflow-1) for detailed examples.

## Workflow 2: Bugfix
[10 lines of essential pattern]

See [docs/workflow-guide.md#workflow-2](docs/workflow-guide.md#workflow-2) for complete guide.
```

## README.md Files: Entry Points Only

### ai/README.md (Root Entry Point)

**Purpose:** Quick start guide pointing to the five core files

**Contains:**
- Quick start (which files to read first)
- Core files overview with line counts
- Top 10 critical constraints (from CRITICAL-RULES.md)
- Brief pointer to detailed docs
- File size targets and current counts

**Does NOT contain:**
- Detailed workflows
- Complete coding guidelines
- Full build/test documentation
- Architecture details

**Current size:** ~180 lines (acceptable for entry point)

### ai/docs/README.md (Detailed Docs Index)

**Purpose:** Navigation index for ai/docs/ detailed guides

**Contains:**
- List of all ai/docs/ files with brief descriptions
- "When to read what" guidance
- Core vs detailed documentation philosophy

**Does NOT contain:**
- The detailed content itself (that's in the other ai/docs/ files)

### Project ai/README.md (e.g., pwiz_tools/Skyline/ai/README.md)

**Purpose:** Brief overview of project-specific tooling

**Contains:**
- List of scripts with one-line descriptions
- Basic usage examples
- Pointer to detailed guide

**Does NOT contain:**
- Complete script documentation
- Detailed workflows
- Troubleshooting guides (those go in project ai/*.md like PRE-COMMIT.md)

**Size limit:** <100 lines (it's a navigation file)

## Validation Checklist

Before committing documentation changes, verify:

- [ ] **No new .md files in ai/ root** (except existing five core files + README.md)
- [ ] **Core files under line limits:**
  - [ ] CRITICAL-RULES.md: <100 lines
  - [ ] MEMORY.md: <200 lines
  - [ ] WORKFLOW.md: <200 lines
  - [ ] STYLEGUIDE.md: <200 lines
  - [ ] TESTING.md: <200 lines
  - [ ] Total: <1000 lines
- [ ] **Detailed content in ai/docs/** with descriptive filenames
- [ ] **No duplication** between core files and ai/docs/
- [ ] **No promotional language** (MANDATORY, 🔴, etc.) for optional tooling
- [ ] **Cross-references updated** if moving/renaming files
- [ ] **Project-specific ai/ directories** only contain tooling, not guidance

## Quick Reference: File Structure

```
ai/
├── CRITICAL-RULES.md          # <100 lines - bare constraints only
├── MEMORY.md                  # <200 lines - project context pointers
├── WORKFLOW.md                # <200 lines - git/TODO/build essentials
├── STYLEGUIDE.md              # <200 lines - essential patterns
├── TESTING.md                 # <200 lines - testing essentials
├── README.md                  # ~180 lines - entry point and quick start
│
├── docs/                      # Detailed guides (unlimited size)
│   ├── README.md              # Index for detailed docs
│   ├── build-and-test-guide.md
│   ├── project-context.md
│   ├── style-guide.md
│   ├── testing-patterns.md
│   ├── workflow-guide.md
│   └── [future detailed guides...]
│
└── todos/                     # Task tracking
    ├── active/
    ├── backlog/
    ├── completed/
    └── archive/

pwiz_tools/Skyline/ai/         # Project-specific tooling only
├── Build-Skyline.ps1          # Automation scripts
├── Run-Tests.ps1
├── PRE-COMMIT.md              # Script usage workflow
└── README.md                  # <100 lines - tooling overview
```

## Red Flags: Signs You're Doing It Wrong

🚩 **Created new file in ai/ root** (not ai/docs/)
- Fix: Move to ai/docs/ or integrate with existing core file

🚩 **Core file exceeds line limit**
- Fix: Move details to ai/docs/, replace with pointer

🚩 **Total core files exceed 1000 lines**
- Fix: Slim all core files, move details to ai/docs/

🚩 **Used "MANDATORY" or 🔴 for optional tooling**
- Fix: Change to "Recommended" or "Optional"

🚩 **Duplicated content across multiple files**
- Fix: Choose single source of truth, add pointers from other locations

🚩 **Added coding guidelines to project-specific ai/ directory**
- Fix: Move to root ai/STYLEGUIDE.md or ai/docs/style-guide.md

🚩 **Created ai/docs/ file that duplicates core file topic**
- Fix: Ensure core file references ai/docs/ file; they should complement, not duplicate

## Commands and Skills: Reference, Don't Embed

**`.claude/commands/` and `.claude/skills/` are entry points, not encyclopedias.**

The `ai/` folder is the living, growing, adapting LLM context. Commands and skills should be concise references INTO that content, not duplicates of it.

### Command Size Guidelines

| Size | Status | Action |
|------|--------|--------|
| <2,000 chars | Good | Concise reference |
| 2,000-5,000 chars | Review | Consider extracting to ai/docs/ |
| >5,000 chars | Refactor | Move content to ai/docs/, keep command as pointer |

### Why This Matters

1. **Submodule separation**: If ai/ becomes a submodule, .claude/ stays in main repo. Keeping .claude/ minimal means it rarely needs updates.
2. **Single source of truth**: Documentation in ai/docs/ can be improved once, benefiting all commands that reference it.
3. **Context limits**: Large commands consume token budget before the LLM even starts working.
4. **Maintainability**: Embedded documentation drifts from the source of truth.

### Command Structure Pattern

```markdown
---
description: Brief one-line description
---

# Command Name

Brief overview (2-3 sentences max).

**Read**: ai/docs/detailed-guide.md for full instructions.

## Quick Reference

[Essential steps - enough to jog memory, not full documentation]

1. Step one (brief)
2. Step two (brief)
3. Step three (brief)

## Related

- Link to detailed guide
- Link to related commands
```

### Example: Refactoring an Overgrown Command

**Before** (embedded - 30,000+ chars):
```markdown
# /pw-daily

[700 lines of detailed instructions, formats, examples, edge cases...]
```

**After** (reference - <2,000 chars):
```markdown
# /pw-daily

Generate daily consolidated report (nightly tests, exceptions, support).

**Read**: ai/docs/daily-report-guide.md for full instructions.

## Quick Reference

1. Determine dates (nightly: 8AM boundary, exceptions: yesterday)
2. Query MCP: get_daily_test_summary, save_exceptions_report, get_support_summary
3. Analyze patterns with analyze_daily_patterns
4. Send HTML email summary
5. Archive processed emails

## Arguments

- Date: YYYY-MM-DD (optional)
- Effort: quick | standard | deep (default: standard)
```

---

## Summary: The Golden Rules

1. **Five core files only** - no new core files in ai/ root
2. **Line limits are constraints** - not suggestions
3. **Core files are reference cards** - pointers to details, not encyclopedias
4. **Details go to ai/docs/** - unlimited size allowed there
5. **No duplication** - single source of truth, pointers everywhere else
6. **Optional tools are optional** - frame as helpers, not requirements
7. **Project ai/ = tooling only** - scripts and their usage, not guidance
8. **When in doubt, add to ai/docs/** - it's always safe there
9. **Commands and skills are entry points** - reference ai/docs/, don't embed

## When You See This Guide Being Violated

If you notice documentation that violates these principles:

1. **Don't just fix the symptom** (e.g., changing "MANDATORY" to "Recommended")
2. **Fix the structural problem** (e.g., moving BUILD-TEST.md to ai/docs/)
3. **Propose improvements to this guide** if it didn't prevent the problem
4. **Reference this guide in your explanation** when suggesting fixes

This guide exists because LLMs have a natural tendency to create documentation that grows unbounded. The ai/ system fights this tendency through intentional architectural constraints.

---

**Last Updated:** 2025-11-06 (PR #3667 review)
**Rationale:** Created in response to PR #3667 adding BUILD-TEST.md (286 lines) to ai/ root, violating reorganization goals from TODO-20251105_reorg_md_docs.md
