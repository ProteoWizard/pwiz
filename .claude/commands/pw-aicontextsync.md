---
description: Weekly ai-context branch sync workflow
---
Read ai\docs\ai-context-branch-strategy.md, then perform the weekly update:
1. Switch to ai-context branch
2. Use pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -DryRun to preview changes
3. Sync FROM master using the script with -Push flag
4. Regenerate ai/TOC.md: pwsh -File ./ai/scripts/Generate-TOC.ps1
5. If any NEW descriptions are needed, review files and add descriptions
6. Commit TOC updates if changed
7. If syncing TO master, prepare the PR from ai-context back to master
8. Summarize what documentation changes are being promoted
