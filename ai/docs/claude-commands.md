# Claude Code Slash Commands for ProteoWizard/Skyline

Quick reference for all `pw-*` project-specific slash commands.

**Tip:** Type `pw-` in Claude Code to see all available commands.

---

## Core Workflow

| Command | Description |
|---------|-------------|
| `/pw-startup [TODO-file]` | Start work on a backlog item. Reads STARTUP.md and the specified TODO. |
| `/pw-startfix [TODO-file]` | Start a bug fix for completed work (Workflow 3a). |
| `/pw-uptodo` | Update current branch's TODO file with progress. |
| `/pw-wrap` | End-of-session summary and TODO update. |

## Documentation Review

| Command | Description |
|---------|-------------|
| `/pw-rbuildtest` | Review build and test guide (`ai/docs/build-and-test-guide.md`). |
| `/pw-rcrw` | Review critical rules and workflow (`CRITICAL-RULES.md` + `WORKFLOW.md`). |
| `/pw-rstyle` | Review style guide (`STYLEGUIDE.md`). |
| `/pw-rtesting` | Review testing guides (`TESTING.md` + `docs/testing-patterns.md`). |
| `/pw-rmemory` | Review project memory and context (`MEMORY.md`). |

## Context Recovery

| Command | Description |
|---------|-------------|
| `/pw-context` | Full context reload: STARTUP.md, rules, workflow, and active TODO. |
| `/pw-handoff` | Prepare handoff summary for another developer or future session. |

## Pre-Commit

| Command | Description |
|---------|-------------|
| `/pw-checkrules` | Verify staged/modified files against critical rules. |
| `/pw-pcommit` | Review rules + staged diff, propose commit message. |
| `/pw-pcommitfull` | Full pre-commit: update TODO + propose commit message. |

## PR & Collaboration

| Command | Description |
|---------|-------------|
| `/pw-adopt [pr-number]` | Adopt a PR branch from another developer. |
| `/pw-respond` | Review PR comments and propose response plan. |

## Developer Setup

| Command | Description |
|---------|-------------|
| `/pw-configure` | Guide through agentic coding environment setup. |
| `/pw-confightml` | Sync developer setup guide MD and HTML versions. |

## AI Context Branch

| Command | Description |
|---------|-------------|
| `/pw-aicontext` | Begin ai-context branch documentation work. |
| `/pw-aicontextsync` | Weekly ai-context branch sync workflow (uses sync script). |

## Help

| Command | Description |
|---------|-------------|
| `/pw-help` | Show this command reference. |

---

## Common Workflows

### Starting a New Feature
```
/pw-startup TODO-feature_name.md
```

### Mid-Session Context Recovery
```
/pw-context
```

### Before Committing
```
/pw-pcommitfull
```

### End of Session
```
/pw-wrap
```

### Reviewing PR Feedback
```
/pw-respond
```

---

## Notes

- All commands use `pw-` prefix to avoid conflicts with built-in Claude commands
- Commands are thin wrappers that point to documentation in `ai/` folder
- Type `pw-` and press Tab to see all available commands
- Commands work with Windows backslash paths

## See Also

- [STARTUP.md](../todos/STARTUP.md) - How to start work on a TODO
- [WORKFLOW.md](../WORKFLOW.md) - Git workflows and TODO system
- [CRITICAL-RULES.md](../CRITICAL-RULES.md) - Absolute constraints
- [ai-context-branch-strategy.md](ai-context-branch-strategy.md) - Branch strategy for ai/ documentation
