# AutoQC

Automated Quality Control application that monitors mass spectrometer output and
automatically processes incoming data files through Skyline for real-time QC tracking.

## Author

Vagisha Sharma, MacCoss Lab

## Usage

GUI application launched from the Windows Start Menu or directly as an executable.
Opens `.qcfg` configuration files that define:

- Which folder to monitor for new data files
- Which Skyline document to use for processing
- Which Skyline installation to use (Skyline or Skyline-Daily)
- Panorama server upload settings (optional)

AutoQC watches for new raw data files, launches SkylineRunner in batch mode to import
and process them, and optionally uploads results to a Panorama server for team-wide
quality monitoring.

## Dependencies

- Skyline or Skyline-Daily installation
- SharedBatch (common batch utilities)
- log4net (logging)

## Related Projects

- **SharedBatch** - shared library for Skyline batch tool infrastructure
- **SkylineBatch** - batch processing tool for scheduled Skyline workflows
