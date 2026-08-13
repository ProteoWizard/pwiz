# SkylineBatch

GUI application for configuring and running batch processing workflows with Skyline.
Allows users to set up multi-step data processing pipelines that run unattended.

## Author

Ali Marsh, MacCoss Lab

## Usage

Standalone Windows Forms application. Create batch configurations specifying:

- Input data files and Skyline document templates
- Processing steps (import, refine, export)
- Skyline version to use (Skyline or Skyline-Daily)
- Output locations and report formats

Configurations can be saved, shared, and scheduled for automated execution.

## Dependencies

- SharedBatch (common batch utilities)
- Skyline/SkylineRunner (for executing batch operations)
- log4net (logging)

## Related Projects

- **SharedBatch** - shared library for batch tool infrastructure
- **AutoQC** - real-time QC monitoring (also uses SharedBatch)
