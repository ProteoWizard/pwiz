# SharedBatch

Shared library providing common infrastructure for Skyline batch processing tools
(AutoQC and SkylineBatch). Contains UI components, Skyline installation discovery,
configuration management, and process management utilities.

## Author

Brendan MacLean, MacCoss Lab

## Components

- **FindSkylineForm** - UI for locating Skyline/Skyline-Daily installations
- **ShareConfigDlg** - configuration sharing dialogs
- **SkylineSettings** - manages Skyline installation paths and versions
- **LaunchBatch** - sub-component that launches ClickOnce (.appref-ms) applications
  with configuration file arguments

## Usage

This is a library project, not a standalone tool. Referenced by AutoQC and SkylineBatch.
