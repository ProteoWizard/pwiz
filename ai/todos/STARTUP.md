# Starting Work on a TODO

This guide helps you start work on any TODO in this directory.

## Quick Start

When starting work on a TODO (e.g., `TODO-compress_vendor_test_data.md`):

### Step 1: Read Essential Context (~500 lines total)
1. [../CRITICAL-RULES.md](../CRITICAL-RULES.md) - 81 lines - Absolute constraints
2. [../MEMORY.md](../MEMORY.md) - 144 lines - Project context and gotchas
3. [../WORKFLOW.md](../WORKFLOW.md) - 166 lines - Git workflows and TODO system
4. The TODO file itself (in `backlog/` or `active/`)

### Step 2: Follow Workflow 1

See [../WORKFLOW.md](../WORKFLOW.md) for complete details. Summary:

**On master - claim the work:**
```bash
git checkout master
git pull origin master
git mv ai/todos/backlog/TODO-feature_name.md ai/todos/active/TODO-20251105_feature_name.md
git commit -m "Start feature_name work - move TODO to active"
git push origin master
```

**Create branch:**
```bash
git checkout -b Skyline/work/20251105_feature_name
```

**Update TODO header and commit:**
```bash
# Edit TODO: add Branch, Created, PR fields
git add ai/todos/active/TODO-20251105_feature_name.md
git commit -m "Update TODO with branch information"
git push -u origin Skyline/work/20251105_feature_name
```

### Step 3: Begin Development

Follow the tasks in the TODO file. Update the TODO with every commit.

## TODO Locations

- **backlog/** - Ready to start (no date prefix)
- **active/** - Currently being worked on (with date prefix)
- **completed/** - Finished work (keep 1-3 months)
- **archive/** - Old completed work (>3 months)

## Key Principles

1. **Update TODO every commit** - Track progress and decisions
2. **Follow CRITICAL-RULES.md** - No async/await, resource strings only, CRLF line endings
3. **Use DRY principles** - Extract helpers when duplication exceeds 3 lines
4. **Translation-proof testing** - Never use English text literals in assertions
5. **Ask developer to build/test** - AI agents cannot run builds or tests

## Need More Detail?

- [../README.md](../README.md) - Main AI documentation index
- [../docs/workflow-guide.md](../docs/workflow-guide.md) - Complete workflow guide with all templates
- [../docs/testing-patterns.md](../docs/testing-patterns.md) - Comprehensive testing guide
- [../docs/style-guide.md](../docs/style-guide.md) - Detailed coding standards
- [../docs/project-context.md](../docs/project-context.md) - Full project context with examples

## Works With All Tools

This startup guide works with:
- Cursor IDE
- VS Code + Claude Code extension
- VS Code + GitHub Copilot extension
- Any LLM tool reading this repository

---

**The ultimate startup prompt:**
```
Read ai/todos/STARTUP.md and let's begin work on TODO-feature_name.md
```
