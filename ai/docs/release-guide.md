# Release Guide

Guide to Skyline release management, including release notes, installers, and Git tagging.

## Release Notes Locations

### Major Release Notes

Major release notes (for stable Skyline releases) are maintained on the wiki:

**Location**: skyline.ms → `/home/software/Skyline` → wiki page `Release%20Notes`

Use MCP tools to access:
```
get_wiki_page(page_name="Release%20Notes", container_path="/home/development")
```

### Skyline-daily (Beta) Release Notes

Beta release notes for Skyline-daily are stored in a database table:

**Location**: skyline.ms → `/home/software/Skyline/daily` → `announcement.Announcement` table

These are managed through the LabKey interface for daily releases.

## Installer Documentation

Installer documentation is on the development wiki:

**Container**: skyline.ms → `/home/development`

### Wiki Pages

| Page | Purpose |
|------|---------|
| `installers` | General installer overview |
| `ClickOnce-installers` | ClickOnce deployment for Skyline |
| `WIX-installers` | WiX-based MSI installers |
| `DeployToDockerHub` | Docker container deployment |
| `renew-code-sign` | Code signing certificate renewal |
| `MakingSpecialInstaller` | Custom/special installer builds |
| `release-prep` | Pre-release preparation checklist |
| `test-upgrade` | Upgrade testing procedures |

Access with MCP tools:
```
get_wiki_page(page_name="installers", container_path="/home/development")
get_wiki_page(page_name="release-prep", container_path="/home/development")
```

## Git Tags for Releases

All Skyline releases since 24.1 have Git tags in the format:

| Release Type | Tag Format | Example |
|--------------|------------|---------|
| Major release | `Skyline-YY.n.0.D` | `Skyline-25.1.0.237` |
| Daily (beta) | `Skyline-daily-YY.n.p.D` | `Skyline-daily-25.1.1.147` |

Where:
- `YY` = Year (24, 25, etc.)
- `n` = Release counter within year (typically 1)
- `p` = Patch level (0 for major releases, 1+ for dailies)
- `D` = Day of year (1-366, not zero-padded)

**Version decoding example**: `25.1.1.147` = Year 2025, release 1, patch 1, day 147 (May 27)

**Sorting note**: Because the day component is not zero-padded, lexicographic sorting does not work correctly (e.g., `25.1.1.10` sorts after `25.1.1.1` but before `25.1.1.2`). Use numeric or "natural sort" for correct ordering.

### Finding a Release Tag

```bash
# Fetch latest tags
git fetch --tags origin

# Find tag for a specific version
git tag -l "*25.1.1.147*"

# List all tags for a version series
git tag -l "Skyline-daily-25.1*"

# Show commit for a tag
git show Skyline-daily-25.1.1.147 --no-patch
```

### Comparing Releases

```bash
# What changed between two releases?
git log Skyline-daily-25.1.1.147..Skyline-daily-25.1.1.174 --oneline

# Full diff between releases
git diff Skyline-24.1.0.199..Skyline-25.1.0.142
```

## Release Workflow

### Pre-Release Checklist

1. **Read release-prep wiki page** for current checklist
2. **Run nightly tests** and resolve failures
3. **Update version numbers** in appropriate files
4. **Review pending PRs** for inclusion/exclusion
5. **Prepare release notes** (major or daily)

### Creating a Release

<!-- TODO: Document the actual release process steps -->

### Post-Release Tasks

1. **Create Git tag** for the release
2. **Update release notes** on wiki/announcement table
3. **Deploy installers** per installer documentation
4. **Notify users** as appropriate

## Slash Commands

| Command | Purpose |
|---------|---------|
| (none yet) | Release-specific commands to be added |

## Related Documentation

- **ai/docs/version-control-guide.md** - Git conventions
- **ai/docs/build-and-test-guide.md** - Building and testing
