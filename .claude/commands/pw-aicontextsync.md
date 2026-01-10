---
description: Weekly ai-context branch sync workflow
---
Read ai\docs\ai-context-branch-strategy.md, then perform the weekly sync:

## Part 1: Rebase onto master
1. Switch to ai-context branch
2. Preview rebase: `pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -DryRun`
3. Rebase onto master: `pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -Push`

## Part 2: Update TOC
4. Regenerate TOC: `pwsh -File ./ai/scripts/Generate-TOC.ps1`
5. If any NEW descriptions needed, review files and add descriptions
6. Commit and push TOC updates if changed (use `--force-with-lease`)

## Part 3: Sync TO master (if requested)
7. Preview squash: `pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -DryRun`
8. Squash and push: `pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction ToMaster -Push`
9. Create PR: `gh pr create --base master --head ai-context --title "Weekly ai-context sync"`
10. Instruct user to merge with **"Rebase and merge"** (NOT squash!)

## Summary
Summarize what documentation changes are being promoted to master.
