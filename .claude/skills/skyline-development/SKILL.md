---
name: skyline-development
description: Use when working on Skyline/ProteoWizard code, tests, builds, documentation, TODOs, or sprints. Activate for any C# code changes, test writing, PR preparation, starting a TODO or sprint, or questions about the codebase. Also activate when user mentions TODO files, backlog items, or ai/ documentation.
---

# Skyline Development Context

When working on any Skyline/ProteoWizard task, consult these documentation files for essential context.

## Starting Work on a TODO

When starting a new TODO or sprint, read **ai/todos/STARTUP.md** first. It provides:
- Essential context files to read (CRITICAL-RULES.md, MEMORY.md, WORKFLOW.md)
- Git branch workflow (create from master, copy TODO to active/)
- TODO lifecycle (backlog → active → completed)

## Continuing Work on Current Branch

When asked to continue work on the current branch or its TODO:
1. Run `git branch --show-current` to get branch name (e.g., `Skyline/work/20251126_files_view`)
2. Extract date and feature name from branch (e.g., `20251126_files_view`)
3. Read the TODO file at `ai/todos/active/TODO-{date}_{feature}.md`
4. The TODO contains: objective, PR link, completed tasks, remaining work, and context

## Core Files (Read for Every Code Task)

Read these files to understand project constraints and patterns:

1. **ai/CRITICAL-RULES.md** - Absolute constraints (NO async/await, resource strings only, CRLF line endings, naming conventions)
2. **ai/MEMORY.md** - Project context (900K LOC, 17 years, 8 devs, critical gotchas)
3. **ai/WORKFLOW.md** - Git workflows, TODO system, commit message format
4. **ai/STYLEGUIDE.md** - C# coding conventions for Skyline
5. **ai/TESTING.md** - Testing rules (translation-proof, test structure)

## When to Read What

- **Before writing code**: Read CRITICAL-RULES.md, STYLEGUIDE.md
- **Before writing tests**: Read TESTING.md, ai/docs/testing-patterns.md
- **Before committing**: Read WORKFLOW.md (commit message rules, Co-Authored-By attribution)
- **Before building/testing**: Read ai/docs/build-and-test-guide.md
- **For detailed patterns**: Read files in ai/docs/

## Key Constraints (Quick Reference)

- **NO `async`/`await`** - Use `ActionUtil.RunAsync()` instead
- **ALL user-facing text in .resx files** - No string literals
- **NEVER English text in test assertions** - Use resource strings
- **Line endings: CRLF** - Windows standard
- **Private fields: `_camelCase`** - Not `m_` prefix
- **Always include `Co-Authored-By: Claude <noreply@anthropic.com>`** in commits

## Slash Commands Available

Type `/pw-` to see all project-specific commands. Key ones:
- `/pw-context` - Full context reload
- `/pw-rcrw` - Review critical rules and workflow
- `/pw-pcommitfull` - Pre-commit with TODO update
- `/pw-help` - Full command reference
