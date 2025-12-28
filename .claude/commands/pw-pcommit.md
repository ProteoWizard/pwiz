---
description: Propose commit message from diff and rules
---
Read ai\docs\version-control-guide.md for the exact commit message format, then review the staged changes (`git diff --staged`), and propose an appropriate commit message.

## Required Format
```
<Title in past tense>

* bullet 1
* bullet 2
* bullet 3

See ai/todos/active/TODO-YYYYMMDD_feature.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

## Checklist
- [ ] **Past tense title** ("Added", "Fixed", "Moved" - not "Add", "Fix")
- [ ] **Bullet points** (1-5 points, each starting with `* `)
- [ ] **TODO reference** (`See ai/todos/active/TODO-YYYYMMDD_feature.md`)
- [ ] **Co-Authored-By** at the end
- [ ] **No emojis or markdown links**
- [ ] **≤10 lines total**
