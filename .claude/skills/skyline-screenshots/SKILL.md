---
name: skyline-screenshots
description: Use when reviewing tutorial screenshots, using ImageComparer, analyzing screenshot diffs, or updating screenshots for releases. Activate for s-XX.png references in tutorial context.
---

# Tutorial Screenshot Updates

When working with tutorial screenshot review or updates, consult **ai/docs/screenshot-update-workflow.md**.

## Quick Reference

**ImageComparer location**: `pwiz_tools/Skyline/Executables/DevTools/ImageComparer/`

**Diff images saved to**: `ai\.tmp\{Tutorial}-{Locale}-s-{Number}-diff-{PixelCount}px.png`

**Key shortcuts**: `Ctrl+S` save diff, `Ctrl+Alt+C` copy diff, `F11` next diff, `F12` revert

**Decisions**: Accept (valid change), Revert (restore original), Fix (BUG-XXX)

## Short-Form Conventions

When developer says things like:
- `s-06 has an issue. look at the diff` → Read diff image from ai\.tmp, analyze change
- `DIA s-10 accepted` → Update TODO, mark as accepted
- `s-06, s-08 BUG-001` → Document bug affecting multiple screenshots

## When Developer References Diff Image

Check `ai\.tmp\` for recently saved diff images matching the screenshot pattern and analyze them.
