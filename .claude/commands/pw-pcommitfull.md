---
description: Full pre-commit with TODO update and message proposal
---
Perform full pre-commit workflow:
1. Read ai\docs\version-control-guide.md for exact commit message format
2. Review the complete diff of staged changes (`git diff --staged`)
3. Update the current branch TODO file with progress (per /pw-uptodo)
4. Propose commit message in required format (title + bullets + TODO + co-authorship)

## Required Format
```
<Title in past tense>

* bullet 1
* bullet 2
* bullet 3

See ai/todos/active/TODO-YYYYMMDD_feature.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

## Troubleshooting

**CRLF Warning**: If git shows `LF will be replaced by CRLF the next time Git touches it`:
```powershell
pwsh -Command "& './ai/scripts/fix-crlf.ps1'"
git add <fixed-files>
```
This ensures consistent line endings before commit.
