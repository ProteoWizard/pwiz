ResourcesOrganizer reads resources from .resx files and puts them into a SQLite database.

The Visual Studio solution is:
pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\ResourcesOrganizer.sln

The project requires .Net 8 to build.

## Jamfile Targets (Recommended)

The simplest way to use ResourcesOrganizer is via Jamfile targets. Run from the project root:

### Generate Localization CSV Files

```cmd
b.bat pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//GenerateLocalizationCsvFiles
```

Creates `localization.ja.csv` and `localization.zh-CHS.csv` in `pwiz_tools\Skyline\Translation\Scratch\` containing strings needing translation. The CSV includes columns for:
- **Name**: Resource key (empty for consolidated entries)
- **English**: Text to translate
- **Translation**: Empty column for translators
- **Issue**: Localization issues (e.g., "English text changed")
- **FileCount/File**: Source .resx file(s) for context

### Import Localization CSV Files

```cmd
b.bat pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//ImportLocalizationCsvFiles
```

Place translated CSV files in `pwiz_tools\Skyline\Translation\Scratch\` (keeping filenames `localization.ja.csv` and `localization.zh-CHS.csv`), then run this target to:
1. Import translations into the database
2. Export updated .resx files
3. Extract them to the project

Verify success by checking build output shows "changed X/Y matching records" and ends with "SUCCESS".

### Finalize Resx Files (Pre-Release)

```cmd
b.bat pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer//FinalizeResxFiles
```

Run before creating a release branch to update .ja and .zh-CHS .resx files with comments for strings added since the last release.

## Manual Scripts

The "scripts" folder contains batch files for manual operation:

>`pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\readResxFiles.bat`
>
>Creates "resources.db" containing information from all .resx files

>`pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\GenerateLocalizationCsvFiles.bat`
>
>Creates localization CSV files for translation

>`pwiz_tools\Skyline\Executables\DevTools\ResourcesOrganizer\scripts\ImportLocalizationCsvFiles.bat`
>
>Imports translated CSV files and updates .resx files

## Command-Line Tool

The ResourcesOrganizer executable supports additional commands:

>`ResourcesOrganizer.exe importLocalizationCsv --db <database> --input <csv> --language <lang>`
>
>Imports a single CSV file into the database

>`ResourcesOrganizer.exe exportResx --db <database> <output.zip>`
>
>Exports database contents to a zip of .resx files

