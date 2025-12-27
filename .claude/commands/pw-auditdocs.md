---
description: Audit Claude Code documentation and configuration
---

# Audit Documentation

Run `ai/scripts/audit-docs.ps1` to check documentation sizes.

## Usage

```powershell
# Full audit (all sections)
pwsh -File ai/scripts/audit-docs.ps1

# Specific sections
pwsh -File ai/scripts/audit-docs.ps1 -Section skills
pwsh -File ai/scripts/audit-docs.ps1 -Section commands
pwsh -File ai/scripts/audit-docs.ps1 -Section ai
pwsh -File ai/scripts/audit-docs.ps1 -Section docs
pwsh -File ai/scripts/audit-docs.ps1 -Section mcp
```

## Sections

| Section | Path | Metric |
|---------|------|--------|
| skills | .claude/skills/*/SKILL.md | Character count (30k limit) |
| commands | .claude/commands/*.md | Character count |
| ai | ai/*.md | Line count |
| docs | ai/docs/*.md | Line count |
| mcp | ai/docs/mcp/*.md | Line count |

## Thresholds

Skills and commands have size limits in Claude Code:
- **Warning**: 20,000 characters
- **Error**: 30,000 characters

## Fixing Size Issues

If a skill or command exceeds limits:
1. Move detailed content to `ai/docs/`
2. Reference with: `See ai/docs/<topic>.md for details`
3. Keep the skill/command as a summary
