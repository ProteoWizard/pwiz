---
name: release-management
description: Use when discussing Skyline releases, version numbers, release notes, installers, or release preparation. Activate for questions about release versions, finding release tags, preparing releases, or understanding release history.
---

# Release Management for Skyline

**Full documentation**: ai/docs/release-guide.md

## When to Read What

- **Before release work**: Read ai/docs/release-guide.md
- **For installer questions**: Query wiki pages in `/home/development`
- **For release notes**: Query wiki/announcement tables

## Quick Reference

### Version Format

`YY.n.p.D` where:
- `YY` = Year (24, 25, etc.)
- `n` = Release counter (typically 1)
- `p` = Patch level (0=major, 1+=daily)
- `D` = Day of year (not zero-padded)

### Git Tags

| Type | Format | Example |
|------|--------|---------|
| Major | `Skyline-YY.n.0.D` | `Skyline-25.1.0.237` |
| Daily | `Skyline-daily-YY.n.p.D` | `Skyline-daily-25.1.1.147` |

### Finding Releases

```bash
git fetch --tags origin
git tag -l "*25.1*"
git show Skyline-daily-25.1.1.147 --no-patch
```

### Key Wiki Pages (on /home/development)

- `installers` - Installer overview
- `release-prep` - Pre-release checklist
- `ClickOnce-installers` - ClickOnce deployment
- `WIX-installers` - MSI installers
- `test-upgrade` - Upgrade testing

### Release Notes Locations

- **Major releases**: `/home/software/Skyline` wiki page `Release%20Notes`
- **Daily releases**: `/home/software/Skyline/daily` → `announcement.Announcement` table

## MCP Tools for Release Work

```python
# Get installer documentation
get_wiki_page(page_name="release-prep", container_path="/home/development")

# Get major release notes
get_wiki_page(page_name="Release%20Notes", container_path="/home/software/Skyline")
```

## Slash Commands

(None yet - release-specific commands to be added)
