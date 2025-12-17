---
name: ai-context-documentation
description: Use when working on ai/ documentation (MEMORY.md, WORKFLOW.md, CRITICAL-RULES.md, etc.), TODOs (backlog planning, active work, completed records), .claude/ files (skills, commands), or discussing the ai-context branch workflow. Activate for creating or modifying documentation, planning new features via TODOs, or questions about documentation structure.
---

# AI Context Documentation

When working on ai/ documentation, TODOs, or the ai-context branch, consult these resources.

## Core Documentation Files

1. **ai/MEMORY.md** - Project context, gotchas, patterns
2. **ai/WORKFLOW.md** - Git workflows, TODO system, commit messages
3. **ai/CRITICAL-RULES.md** - Absolute constraints
4. **ai/STYLEGUIDE.md** - C# coding conventions
5. **ai/TESTING.md** - Testing patterns and rules

## Branch Strategy

Read **ai/docs/ai-context-branch-strategy.md** for:
- When to use ai-context vs master
- Sync scripts and commands
- Merge workflow (FromMaster daily, ToMaster weekly)

## Quick Commands

- `/pw-aicontextupdate` - Sync ai-context FROM master (safe, daily)
- `/pw-aicontextsync` - Full bidirectional sync workflow (weekly)

## Creating New Documentation

### New Skill
```
.claude/skills/{skill-name}/SKILL.md
```

Required frontmatter:
```yaml
---
name: skill-name
description: When to activate this skill (one sentence)
---
```

### New Slash Command
```
.claude/commands/pw-{command-name}.md
```

Required frontmatter:
```yaml
---
description: Brief description of what the command does
---
```

### New Guide Document
```
ai/docs/{topic-name}.md
```

- Use descriptive kebab-case names
- Include cross-references to related docs
- Update relevant skills to reference new docs

## Documentation Locations

| Type | Location | Branch |
|------|----------|--------|
| Skills | `.claude/skills/` | ai-context |
| Commands | `.claude/commands/` | ai-context |
| Guides | `ai/docs/` | ai-context |
| TODOs (backlog) | `ai/todos/backlog/` | ai-context |
| TODOs (active) | `ai/todos/active/` | feature branch |
| TODOs (completed) | `ai/todos/completed/` | merged to master |

## Key Principle

Documentation work belongs on the **ai-context** branch to avoid churning master with frequent small commits. Batch merge to master weekly or when documentation stabilizes.
