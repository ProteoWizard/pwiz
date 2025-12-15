---
description: Propose commit message from diff and rules
---
Read commit message rules in ai\WORKFLOW.md and ai\CRITICAL-RULES.md, review the staged changes (`git diff --staged`), and propose an appropriate commit message following project conventions.

## Commit Message Checklist
Before proposing a commit message, verify:
- [ ] **Past tense** ("Added", "Fixed", "Updated" - not "Add", "Fix", "Update")
- [ ] **≤10 lines total** (including blank lines and attribution)
- [ ] **TODO reference included** (`See ai/todos/active/TODO-YYYYMMDD_feature.md`)
- [ ] **Co-Authored-By line** at the end (exactly: `Co-Authored-By: Claude <noreply@anthropic.com>`)
- [ ] **No emojis or markdown links** in commit message
