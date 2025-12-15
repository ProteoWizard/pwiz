---
argument-hint: [pr-number]
description: Adopt a PR branch from another developer
---
Adopt work from PR #$ARGUMENTS:
1. Fetch the PR branch and check it out
2. Review the PR description and any existing comments on GitHub
3. Check if a TODO file exists in ai\todos\active\ for this branch
4. If no TODO exists, create one based on the PR description and current state
5. Read ai\todos\STARTUP.md to establish context
6. Summarize the current state and what needs to be done
