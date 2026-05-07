# TutorialLocalization

Aggregates localized tutorial content from language-specific folders into a merged ZIP
file with embedded localization CSV mappings for multi-language Skyline tutorial support.

## Author

Nicholas Shulman, MacCoss Lab

## Usage

```
TutorialLocalization <folder> [--output MergedTutorials.zip]
```

Reads language subfolders within the specified folder, processes localization CSVs, and
creates a merged ZIP archive suitable for distribution with Skyline.
