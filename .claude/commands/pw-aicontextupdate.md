---
description: Update ai-context branch from master (quick rebase)
---
Rebase ai-context onto master to get the latest code and documentation changes.

This rebases ai-context onto master, keeping it as a linear history on top of master.

Run this command:
```powershell
pwsh -File ./ai/scripts/sync-ai-context.ps1 -Direction FromMaster -Push
```

After syncing, briefly summarize what new changes came from master.
