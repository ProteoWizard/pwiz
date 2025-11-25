# AI Context Branch Strategy

## Problem Statement

Frequent commits to `ai/todos/` on master are causing friction for team members:
- PRs show "out-of-date with base branch" frequently
- Developers must wait for quiet periods to merge
- TeamCity builds trigger unnecessarily for documentation-only changes
- Current workaround (override merge rules) works but feels ad-hoc

## Key Insight

The `ai/` folder serves a different purpose than code:
- **Code**: Requires full CI validation, careful merge timing
- **ai/todos/**: LLM context restoration, rapid iteration, low risk

These have different change velocities and risk profiles. They shouldn't impose the same merge friction.

## Recommended Solution: Dedicated ai-context Branch

### Overview

1. Create a long-lived `ai-context` branch for rapid ai/ iteration
2. Commit ai/ changes there without blocking team PRs
3. Batch-merge to master periodically (daily or when stabilized)
4. Feature branches continue branching from master (stable)

### Benefits

- Rapid iteration on ai/ documentation without blocking colleagues
- Git version control retained (history, diffs, blame)
- No submodule complexity
- Clear separation of concerns
- Feature branches see stable ai/ context from master

### Branch Workflow

```
master (stable)
  │
  ├── feature/my-feature (branches from master)
  │
  └── ai-context (long-lived, rapid ai/ changes)
        │
        └── periodically merged back to master
```

## Implementation Steps

### 1. Create the ai-context branch

```bash
git checkout master
git pull origin master
git checkout -b ai-context
git push -u origin ai-context
```

### 2. Add new backlog TODOs to ai-context

```bash
# On ai-context branch
# Add/modify files in ai/todos/backlog/
git add ai/todos/backlog/
git commit -m "Add backlog TODOs for [brief description]"
git push origin ai-context
```

### 3. When ready to sync to master

```bash
git checkout master
git pull origin master
git merge ai-context --no-ff -m "Merge ai-context: batch update ai/ documentation"
git push origin master
```

Or create a PR from ai-context → master for visibility.

### 4. After merging, continue on ai-context

```bash
git checkout ai-context
git merge master  # Keep ai-context up to date with any master changes
```

## Policy Guidelines

### What goes on ai-context (bypass full CI)
- New TODO files in `ai/todos/backlog/`
- Updates to `ai/todos/work/` session notes
- MEMORY.md, WORKFLOW.md refinements
- Any file under `ai/` that doesn't affect build/test

### What still goes through normal PRs
- Code changes (always)
- Changes to `ai/` that accompany code changes (same PR)
- CRITICAL-RULES.md updates that affect team workflow (needs review)

### Merge frequency to master
- Daily batch merge if changes accumulated
- Immediate merge if a feature branch needs updated context
- No merge needed if ai-context is just holding backlog items

## Team Communication

Suggested message to team:

> To reduce master churn from ai/ documentation updates, we're adopting a dedicated `ai-context` branch for rapid iteration. 
>
> - ai/-only changes will accumulate on `ai-context`
> - Batch merges to master will happen daily or when stabilized
> - Your feature branches won't see constant "out-of-date" warnings
> - The issue tracker remains our project management tool; ai/todos is LLM context
>
> If you need the latest ai/ context on your feature branch, you can cherry-pick from ai-context or wait for the next batch merge.

## Future Considerations

- GitHub Actions could automate daily ai-context → master merges
- Branch protection rules could be lighter for ai-context → master PRs
- If team adopts ai/ workflows broadly, each developer could have personal ai-context branches

## Related

- TeamCity: Also disabling build triggers for changes under `ai/` (separate configuration change)
- Issue tracker: Remains the source of truth for project planning; ai/todos is implementation context for LLM sessions
