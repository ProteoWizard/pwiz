# Gmail MCP Server

This document describes how to set up the Gmail MCP server for sending emails from Claude Code.

## Overview

The Gmail MCP server enables Claude Code to send emails, read messages, manage labels, and more. This is used for automated report delivery (daily nightly test reports, exception summaries, etc.).

**Server**: [GongRzhe/Gmail-MCP-Server](https://github.com/GongRzhe/Gmail-MCP-Server) (874+ stars, actively maintained)

**Account**: `claude.c.skyline@gmail.com`

## Quick Start (New Developer Setup)

The Google Cloud project is already configured. You just need to set up your local machine:

1. **Get the OAuth credentials file** from a team member or the shared credentials location
2. **Store it locally:**
   ```powershell
   New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.gmail-mcp"
   # Copy gcp-oauth.keys.json to ~/.gmail-mcp/
   ```
3. **Authenticate** (opens browser, sign in as `claude.c.skyline@gmail.com`):
   ```
   npx @gongrzhe/server-gmail-autoauth-mcp auth
   ```
4. **Register with Claude Code:**
   ```
   claude mcp add gmail -- npx @gongrzhe/server-gmail-autoauth-mcp
   ```
5. **Verify:** `claude mcp list` should show gmail as connected

Skip to [Available Tools](#available-tools) once connected.

---

## Full Setup (First-Time / New Account)

This section documents the one-time Google Cloud setup. Only needed if setting up a new Gmail account or recreating the project.

### Prerequisites

- Node.js 18+ (`node --version`)
- Access to Google Cloud Console

### Account Strategy

**Do all setup logged in as `claude.c.skyline@gmail.com`.**

This keeps everything self-contained:
- Google Cloud project owned by the shared account
- OAuth credentials accessible to anyone with account access
- No cross-account test user configuration needed
- Easy handoff - just share account credentials, not transfer GCP projects

### Setup Instructions

### Step 1: Create Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Click the project dropdown (top-left) → "New Project"
3. Name it (e.g., "Claude Gmail MCP") → Create
4. Wait for project creation, then select it

### Step 2: Enable Gmail API

1. In the left sidebar: "APIs & Services" → "Library"
2. Search for "Gmail API"
3. Click on "Gmail API" → "Enable"

### Step 3: Configure OAuth Consent Screen

1. "APIs & Services" → "OAuth consent screen"
2. Select "External" (unless you have Google Workspace) → "Create"
3. **App Information:**
   - App name: "Claude Code Gmail"
   - User support email: `claude.c.skyline@gmail.com`
4. **Audience:**
   - Leave defaults (External)
5. **Contact Information:**
   - Developer contact email: `claude.c.skyline@gmail.com`
6. **Finish:**
   - Check the agreement checkbox
   - Click "Create"
7. **Add Test Users** (if prompted or in OAuth consent screen settings):
   - Add `claude.c.skyline@gmail.com` as a test user
   - This is required while the app is in "Testing" mode

### Step 4: Create OAuth Credentials

1. "APIs & Services" → "Credentials"
2. "Create Credentials" → "OAuth client ID"
3. Application type: **Desktop app**
4. Name: "Claude Code"
5. Click "Create"
6. Click "Download JSON" on the popup
7. Rename the downloaded file to `gcp-oauth.keys.json`

### Step 5: Store Credentials File

On Windows (PowerShell):
```powershell
# Create directory
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.gmail-mcp"

# Move the renamed file from Downloads
Move-Item "$env:USERPROFILE\Downloads\gcp-oauth.keys.json" "$env:USERPROFILE\.gmail-mcp\"
```

### Step 6: Authenticate (One-Time Browser Flow)

Run the authentication command:

```
npx @gongrzhe/server-gmail-autoauth-mcp auth
```

This will:
1. Open your default browser
2. Prompt you to sign in to Google (use `claude.c.skyline@gmail.com`)
3. Ask you to grant Gmail permissions
4. Save credentials to `~/.gmail-mcp/credentials.json`

After this, authentication is automatic (tokens refresh automatically).

### Step 7: Register with Claude Code

```
claude mcp add gmail -- npx @gongrzhe/server-gmail-autoauth-mcp
```

Verify registration:
```
claude mcp list
```

Expected output:
```
gmail: npx @gongrzhe/server-gmail-autoauth-mcp - ✓ Connected
labkey: python C:/proj/pwiz/ai/mcp/LabKeyMcp/server.py - ✓ Connected
```

## Available Tools

Once configured, these MCP tools become available:

| Tool | Description | Status |
|------|-------------|--------|
| `send_email` | Send an email with optional attachments | Tested |
| `search_emails` | Search emails using Gmail syntax | Tested |
| `read_email` | Read a specific email by ID | Tested |
| `modify_email` | Modify email labels (archive, mark read, etc.) | Tested |
| `create_draft` | Create a draft email | Available |
| `send_draft` | Send an existing draft | Available |
| `list_labels` | List all Gmail labels | Available |
| `create_label` | Create a new label | Available |
| `create_filter` | Create an email filter | Available |
| `download_attachment` | Download email attachment to local file | Available |
| `delete_email` | Permanently delete an email | **No permission** |
| `batch_delete` | Delete multiple emails | **No permission** |

### Scope Limitations

The MCP server uses `gmail.modify` scope which allows sending, reading, and label management but **not permanent deletion**. This is sufficient for automated report delivery. To archive emails instead of deleting, use `modify_email` to remove the `INBOX` label.

If permanent deletion is needed, re-authenticate with the full `https://mail.google.com/` scope (requires updating the OAuth consent screen scopes in Google Cloud Console).

## Testing

After setup, test by asking Claude Code to send a test email:

```
Send a test email to [your-email] with subject "Gmail MCP Test" and body "Hello from Claude Code!"
```

## Troubleshooting

### "Access blocked: This app's request is invalid"

The OAuth consent screen may not be properly configured. Ensure:
- You added test users (Step 3.6)
- The Gmail API is enabled (Step 2)

### "Invalid credentials" error

Re-run the authentication:
```
npx @gongrzhe/server-gmail-autoauth-mcp auth
```

### MCP server not connecting

1. Check credentials file exists: `dir $env:USERPROFILE\.gmail-mcp`
2. Verify Node.js version: `node --version` (need 18+)
3. Try running manually: `npx @gongrzhe/server-gmail-autoauth-mcp`

### Token expired

Tokens should auto-refresh. If issues persist, delete and re-authenticate:
```powershell
Remove-Item "$env:USERPROFILE\.gmail-mcp\credentials.json"
npx @gongrzhe/server-gmail-autoauth-mcp auth
```

## Security Notes

- OAuth credentials are stored in `~/.gmail-mcp/`
- Never commit these files to version control
- The `credentials.json` contains refresh tokens - treat as sensitive
- Consider using a dedicated Gmail account (like `claude.c.skyline@gmail.com`) rather than personal email

## Related

- [GitHub Issue #3733](https://github.com/ProteoWizard/pwiz/issues/3733) - Gmail MCP integration
- [GitHub Issue #3732](https://github.com/ProteoWizard/pwiz/issues/3732) - Scheduled daily analysis (uses this capability)
- [GongRzhe/Gmail-MCP-Server](https://github.com/GongRzhe/Gmail-MCP-Server) - Upstream project
