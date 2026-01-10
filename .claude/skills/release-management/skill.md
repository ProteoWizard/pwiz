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

`YY.N.B.DDD` where:
- `YY` = Year - 2000 (25, 26, etc.)
- `N` = Release ordinal within year (0=unreleased, 1=first official)
- `B` = Branch type: 0=release, 1=daily, 9=feature complete
- `DDD` = Day of year, **zero-padded** (001-365), from git commit date

### Release Types

| Type | Version | Branch |
|------|---------|--------|
| Daily (beta) | `YY.N.1.DDD` | master |
| Feature Complete | `YY.N.9.DDD` | Skyline/skyline_YY_N |
| Major Release | `YY.N.0.DDD` | Skyline/skyline_YY_N |
| Patch | `YY.N.0.DDD` | Skyline/skyline_YY_N |

### Git Tags

| Type | Format | Example |
|------|--------|---------|
| Major | `Skyline-YY.N.0.DDD` | `Skyline-26.1.0.045` |
| Daily | `Skyline-daily-YY.N.B.DDD` | `Skyline-daily-26.0.9.004` |

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

**Planned**: `/pw-release <type>` where type is:
- `complete` - FEATURE COMPLETE release (fully documented)
- `major` - Major release (26.1.0)
- `patch` - Patch to existing release
- `rc` - Release candidate

See ai/docs/release-guide.md "Future Automation" section for details.
