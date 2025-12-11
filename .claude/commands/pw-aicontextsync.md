---
description: Weekly ai-context branch sync workflow
---
Read ai\docs\ai-context-branch-strategy.md, then perform the weekly update:
1. Use pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -DryRun to preview changes
2. Sync FROM master using the script with -Push flag
3. If syncing TO master, prepare the PR from ai-context back to master
4. Summarize what documentation changes are being promoted
