---
name: version-control
description: Use when performing Git or GitHub operations including commits, pushes, PRs, or branch creation. Activate BEFORE running git commit, git push, gh pr create, or similar commands.
---

# Version Control for Skyline/ProteoWizard

Before any Git or GitHub operation, read the relevant documentation:

## Required Reading

- **ai/docs/version-control-guide.md** - Commit message format, PR format, branch naming
- **ai/docs/release-cycle-guide.md** - Current release phase, cherry-pick policy
- **ai/WORKFLOW.md** - Git workflows, TODO system, branch lifecycle

## Commit Message Format

```
<Title in past tense>

* bullet point 1
* bullet point 2

See ai/todos/active/TODO-YYYYMMDD_feature.md

Co-Authored-By: Claude <noreply@anthropic.com>
```

**Rules:**
- Past tense title ("Added feature" not "Add feature")
- Bullet points use `* ` prefix (not `-`)
- TODO reference required for feature branches
- Co-Authored-By required when LLM contributed
- Maximum 10 lines total
- No emojis, no markdown links

**ai-context branch**: Omit TODO reference (no active TODO)

## Branch Naming

`Skyline/work/YYYYMMDD_feature_name`

## Cherry-Pick to Release

When in FEATURE COMPLETE or patch mode, bug fix PRs should be cherry-picked to the release branch:

1. Check current release phase in `ai/docs/release-cycle-guide.md`
2. If fixing a bug during FEATURE COMPLETE: add label `Cherry pick to release`
3. The cherry-pick happens automatically after PR merge

**Current release branch**: `Skyline/skyline_26_1` (check release-cycle-guide.md for updates)

## Commands

Use `/pw-pcommit` or `/pw-pcommitfull` for guided commits.

See ai/docs/version-control-guide.md for complete specification.
