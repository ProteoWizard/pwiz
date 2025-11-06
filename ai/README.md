# AI-Assisted Development Documentation

This directory contains all documentation for LLM-assisted development on the Skyline/ProteoWizard project.

## Quick Start

**New LLM session? Start here:**
1. Read [CRITICAL-RULES.md](CRITICAL-RULES.md) - Absolute constraints (<100 lines)
2. Read [MEMORY.md](MEMORY.md) - Project context and critical gotchas (~150 lines)
3. Read [WORKFLOW.md](WORKFLOW.md) - Git workflows and TODO system (~170 lines)
4. Read active TODO in [todos/active/](todos/active/) - Current branch context

**Total: <500 lines for essential context.**

## Core Files (Read Every Session)

These files are kept small (<200 lines each) for quick loading:

- **[CRITICAL-RULES.md](CRITICAL-RULES.md)** (81 lines)
  - Bare constraints only, no explanations
  - File format, async patterns, testing rules, naming conventions
  - Absolute prohibitions (NEVER sections)

- **[MEMORY.md](MEMORY.md)** (144 lines)
  - Project scale (900K LOC, 17 years, 8 devs)
  - Critical gotchas (async/await, resource strings, translation-proof testing)
  - Threading, DRY principles, build system
  - Project structure and testing overview

- **[WORKFLOW.md](WORKFLOW.md)** (166 lines)
  - Git branch strategy (master, release, work branches)
  - TODO file system (backlog, active, completed, archive)
  - Key workflows (start work, daily dev, complete/merge)
  - LLM tool guidelines

- **[STYLEGUIDE.md](STYLEGUIDE.md)** (162 lines)
  - C# coding conventions for Skyline
  - File format, naming, control flow
  - Resource strings (localization)
  - Async programming (NO async/await)
  - File headers and AI attribution

- **[TESTING.md](TESTING.md)** (154 lines)
  - Test project selection (Test, TestFunctional, etc.)
  - Critical testing rules (translation-proof, structure)
  - Common patterns (functional tests, assertions)
  - AssertEx quick reference

## Detailed Documentation (Read On-Demand)

The [docs/](docs/) subdirectory contains comprehensive guides:

- **[docs/README.md](docs/README.md)** - Index of detailed documentation
- **[docs/project-context.md](docs/project-context.md)** - Full project context with detailed examples
- **[docs/style-guide.md](docs/style-guide.md)** - Comprehensive C# coding standards
- **[docs/workflow-guide.md](docs/workflow-guide.md)** - Complete workflow guide with all templates
- **[docs/testing-patterns.md](docs/testing-patterns.md)** - Comprehensive testing patterns and examples

## TODO System

The [todos/](todos/) directory tracks work items for LLM-assisted development:

```
todos/
  active/      # Currently being worked on (committed to branch)
  backlog/     # Ready to start, fully planned
  completed/   # Recently completed (keep 1-3 months for reference)
  archive/     # Old completed work (>3 months old)
```

**TODO File Naming:**
- `TODO-feature_name.md` - Backlog (no date)
- `TODO-20251105_feature_name.md` - Active (with date)

**Lifecycle:**
1. Plan in `backlog/` on master
2. Move to `active/` when starting work (claim it on master, then branch)
3. Update with every commit on branch
4. Move to `completed/` after PR merge
5. Archive after 3 months

See [WORKFLOW.md](WORKFLOW.md) for complete TODO workflow.

## When to Read What

### Starting a New Session
1. [CRITICAL-RULES.md](CRITICAL-RULES.md) - Know the constraints
2. [MEMORY.md](MEMORY.md) - Understand the project
3. [WORKFLOW.md](WORKFLOW.md) - Know the process
4. [todos/active/TODO-*.md](todos/active/) - Get current context

### Before Writing Code
- [STYLEGUIDE.md](STYLEGUIDE.md) - C# conventions
- [CRITICAL-RULES.md](CRITICAL-RULES.md) - Absolute rules
- [docs/style-guide.md](docs/style-guide.md) - Detailed examples (if needed)

### Before Writing Tests
- [TESTING.md](TESTING.md) - Quick testing reference
- [CRITICAL-RULES.md](CRITICAL-RULES.md) - Testing constraints
- [docs/testing-patterns.md](docs/testing-patterns.md) - Comprehensive patterns (if needed)

### When Handling Edge Cases
- [docs/project-context.md](docs/project-context.md) - Detailed gotchas and patterns
- [MEMORY.md](MEMORY.md) - Common issues summary

### When Creating/Completing Work
- [WORKFLOW.md](WORKFLOW.md) - Quick workflows
- [docs/workflow-guide.md](docs/workflow-guide.md) - Complete templates and examples

## Information Architecture

### Core Files Philosophy
- **Append-hostile** - Finished reference cards, not growing documents
- **<200 lines each** - Quick to load and scan
- **Pointers to details** - Link to docs/ for comprehensive information
- **Essential only** - Critical rules, common patterns, key workflows

### Detailed Docs Philosophy
- **Unlimited size** - Encyclopedic detail
- **Comprehensive examples** - Full code samples and patterns
- **Indexed** - Easy to find specific information
- **Supplementary** - Read when you need deep understanding

## Project Statistics

- **Code**: ~900,000 lines (C#, C++, JavaScript)
- **History**: 17+ years of evolution
- **Team**: 8 active developers
- **Testing**: 100+ hours daily automated testing
- **Localization**: English, Chinese, Japanese, Turkish, French

## Critical Constraints (Top 10)

See [CRITICAL-RULES.md](CRITICAL-RULES.md) for full list:

1. **NO** `async`/`await` keywords - Use `ActionUtil.RunAsync()`
2. **ALL** user-facing text in .resx files - NO string literals
3. **NEVER** English text in test assertions - Use resource strings
4. **ALWAYS** use `AssertEx.Contains()` not `Assert.IsTrue(string.Contains())`
5. **NEVER** create multiple `[TestMethod]` for related validations
6. **Line endings**: CRLF (`\r\n`) - Windows standard
7. **Indentation**: Spaces only (no tabs)
8. **Naming**: `_camelCase` for private fields, `PascalCase` for types
9. **Build**: Use `quickbuild.bat` - DO NOT introduce new build systems
10. **Quality**: Zero warnings, ReSharper green

## File Size Targets (Met!)

- CRITICAL-RULES.md: 81 lines (target <100) ✅
- MEMORY.md: 144 lines (target <200) ✅
- WORKFLOW.md: 166 lines (target <150, close!) ✅
- STYLEGUIDE.md: 162 lines (target <200) ✅
- TESTING.md: 154 lines (target <200) ✅
- **Total**: 707 lines (target <1000) ✅

## See Also

- **Root [README.md](../README.md)** - ProteoWizard project overview (for humans)
- **Root [.cursorrules](../.cursorrules)** - Cursor IDE configuration
- **[doc/](../doc/)** - ProteoWizard website and documentation (for humans)

---

**This `ai/` directory is for LLM context only.** For human-facing documentation, see the root `README.md` and `doc/` directory.
