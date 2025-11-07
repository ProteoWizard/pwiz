# AI Documentation Index - Detailed Guides

This directory contains comprehensive, detailed documentation for LLM-assisted development. These files are **unlimited in size** and provide encyclopedic detail.

**For quick reference, see the core files in [ai/](../)** - they're kept small (<200 lines) for fast loading.

## Available Guides

### [documentation-maintenance.md](documentation-maintenance.md)
**Guide for LLMs on maintaining this documentation system**
- Decision tree: where does new content go?
- Core file line limits and enforcement
- Common mistakes and corrections
- Validation checklist before commits
- Red flags for documentation violations

**When to read:** Before creating/modifying any .md files in the ai/ system. **READ THIS FIRST** if you're adding documentation.

### [build-and-test-guide.md](build-and-test-guide.md)
**Comprehensive build and test command reference**
- PowerShell Build-Skyline.ps1 usage
- MSBuild commands for iterative builds
- TestRunner.exe execution patterns
- ReSharper code inspection workflows
- Pre-commit validation procedures
- Output interpretation and troubleshooting

**When to read:** When building/testing from LLM-assisted IDEs, debugging build failures.

### [project-context.md](project-context.md)
**Full project context with detailed examples**
- Complete DRY principle examples (with code samples)
- Detailed threading patterns
- Comprehensive exception handling patterns
- Resource string workflow with examples
- Translation-proof testing with detailed examples
- Full architectural patterns

**When to read:** When you need deep understanding of project patterns and gotchas.

### [style-guide.md](style-guide.md)
**Comprehensive C# coding standards**
- Complete control flow examples
- Full naming convention details
- ASCII vs Unicode guidance with examples
- Debug.WriteLine patterns
- File header templates with all variations
- Resource string generation rules
- UI guidelines with menu patterns

**When to read:** Before writing new code, when uncertain about style details.

### [workflow-guide.md](workflow-guide.md)
**Complete workflow guide with all templates**
- Full TODO file templates (backlog, active, completed)
- All 5 workflows with detailed examples
- TODO file structure templates
- Handoff prompt templates
- Emergency procedures (hotfixes, rollbacks)
- CI/CD integration details
- Best practices with examples

**When to read:** When creating TODOs, planning work, completing PRs.

### [testing-patterns.md](testing-patterns.md)
**Comprehensive testing patterns and examples**
- Full test project details with characteristics
- Test execution tools (TestRunner, SkylineTester, SkylineNightly)
- Complete dependency injection patterns with examples
- Full AssertEx API reference
- HttpClientTestHelper detailed usage
- Translation-proof testing comprehensive guide
- Test performance optimization with metrics

**When to read:** Before writing tests, when implementing complex test patterns.

## Core vs Detailed Documentation

### Core Files ([ai/](../))
- **Size**: <200 lines each
- **Purpose**: Essential rules and quick reference
- **Load**: Every session
- **Content**: Constraints, common patterns, key workflows

### Detailed Docs ([ai/docs/](.))
- **Size**: Unlimited
- **Purpose**: Comprehensive examples and deep dives
- **Load**: On-demand, when needed
- **Content**: Full examples, edge cases, historical context

## Quick Navigation

**Need to know:**
- **What's critical?** → [../CRITICAL-RULES.md](../CRITICAL-RULES.md)
- **Project basics?** → [../MEMORY.md](../MEMORY.md)
- **Git workflows?** → [../WORKFLOW.md](../WORKFLOW.md)
- **Style rules?** → [../STYLEGUIDE.md](../STYLEGUIDE.md)
- **Testing basics?** → [../TESTING.md](../TESTING.md)

**Need detailed examples:**
- **Doc maintenance?** → [documentation-maintenance.md](documentation-maintenance.md)
- **Build/test commands?** → [build-and-test-guide.md](build-and-test-guide.md)
- **Project patterns?** → [project-context.md](project-context.md)
- **Style examples?** → [style-guide.md](style-guide.md)
- **Workflow templates?** → [workflow-guide.md](workflow-guide.md)
- **Testing patterns?** → [testing-patterns.md](testing-patterns.md)

## Growth Strategy

When adding new information:
1. **Critical constraint?** → Add to [CRITICAL-RULES.md](../CRITICAL-RULES.md) (bare rule only)
2. **Common pattern?** → Add to core file (MEMORY, STYLEGUIDE, etc.) with pointer to details
3. **Detailed example?** → Add to corresponding detailed doc in this directory
4. **New category?** → Create new detailed doc, add to this index

**Goal**: Keep core files small, grow detailed docs as needed.
