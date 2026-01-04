# Translation Guide

Guide for updating localized RESX files and preparing translation tables for Japanese (ja) and Chinese Simplified (zh-CHS) translations.

## Overview

Skyline maintains translations in two languages:
- **Japanese (ja)** - `.ja.resx` files
- **Chinese Simplified (zh-CHS)** - `.zh-CHS.resx` files

The translation workflow involves:
1. Syncing localized RESX files with English (copying non-text properties)
2. Generating CSV files with strings needing translation
3. Sending CSVs to translators
4. Importing translated CSVs back into RESX files

## Tools

### ResourcesOrganizer

Location: `pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer/`

A .NET 8 tool that:
- Reads RESX files into a SQLite database
- Compares current resources against `LastReleaseResources.db`
- Generates localization CSV files for translators
- Imports translated CSVs back into RESX files

### LastReleaseResources.db

Location: `pwiz_tools/Skyline/Translation/LastReleaseResources.db`

SQLite database containing resources from the last major release. Used to:
- Identify which strings are new or changed
- Preserve existing translations for unchanged strings

## Boost Build Targets

### IncrementalUpdateResxFiles

**When to use**: During development cycle when UI changes occur.

**What it does**:
- Updates `.ja.resx` and `.zh-CHS.resx` files
- Syncs non-text properties (layout, size, location) from English RESX files
- Reverts to English any strings NOT found in `LastReleaseResources.db`
- Does NOT add "NeedsReview:" comments

**How to run**:
```cmd
cd C:\proj\pwiz
bjam pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//IncrementalUpdateResxFiles
```

### FinalizeResxFiles

**When to use**: After visual freeze (FEATURE COMPLETE), before sending to translators.

**What it does**:
- Everything `IncrementalUpdateResxFiles` does, PLUS
- Adds "NeedsReview:" comments to strings that changed since last release
- These comments mark strings that need translator review

**How to run**:
```cmd
cd C:\proj\pwiz
bjam pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//FinalizeResxFiles
```

## Generating Translation CSVs

After running `FinalizeResxFiles`, generate CSV files for translators:

```cmd
cd C:\proj\pwiz
pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\GenerateLocalizationCsvFiles.bat
```

**Output files** (in `pwiz_tools/Skyline/Translation/Scratch/`):
- `localization.ja.csv` - Japanese translation table
- `localization.zh-CHS.csv` - Chinese Simplified translation table

These CSVs contain only strings with "NeedsReview:" comments.

## Importing Translated CSVs

After receiving translated CSVs back from translators:

```cmd
cd C:\proj\pwiz\pwiz_tools\Skyline\Translation\Scratch

# Import the CSV files into the database
..\Executables\DevTools\ResourcesOrganizer\scripts\exe\ResourcesOrganizer.exe importLocalizationCsv

# Export updated RESX files
..\Executables\DevTools\ResourcesOrganizer\scripts\exe\ResourcesOrganizer.exe exportResx resxFiles.zip

# Extract to project root
cd C:\proj\pwiz
libraries\7za.exe x -y pwiz_tools\Skyline\Translation\Scratch\resxFiles.zip
```

## Workflow Summary

### During Development

Run `IncrementalUpdateResxFiles` periodically to keep localized RESX files in sync:
- Syncs control positions and sizes
- Keeps existing translations intact
- New strings show in English

### At Feature Complete

1. Run `FinalizeResxFiles` - adds "NeedsReview:" comments
2. Run `GenerateLocalizationCsvFiles.bat` - creates CSV files
3. Send CSV files to translators
4. Wait for translations

### After Receiving Translations

1. Place translated CSVs in `Translation/Scratch/`
2. Run `importLocalizationCsv`
3. Run `exportResx`
4. Extract ZIP to project root
5. Build and test

## File Locations

| File/Folder | Purpose |
|-------------|---------|
| `pwiz_tools/Skyline/Translation/` | Translation working directory |
| `Translation/LastReleaseResources.db` | Baseline from last major release |
| `Translation/Scratch/` | Working directory for CSV and DB files |
| `Executables/DevTools/ResourcesOrganizer/scripts/` | Batch scripts |
| `Executables/DevTools/ResourcesOrganizer/Jamfile.jam` | Boost Build targets |

## Related Documentation

- **ai/docs/release-guide.md** - Release management overview
- **ai/docs/screenshot-update-workflow.md** - Screenshots and RESX sync notes
- **pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer/README.md** - Tool documentation
