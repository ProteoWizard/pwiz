# TODO-skyline_parquet_support.md

## Objective
Add support for exporting Skyline reports in Apache Parquet format, providing a columnar storage option that offers better compression and faster query performance for large datasets.

## Background

Apache Parquet is a columnar storage format optimized for analytical workloads. Adding Parquet export support to Skyline will provide users with:

- **Better compression**: Columnar storage typically achieves better compression ratios than row-based formats
- **Faster analytics**: Columnar format enables efficient queries by reading only required columns
- **Industry standard**: Wide support across data analysis tools (pandas, Apache Spark, R, etc.)
- **Large dataset handling**: Efficient for datasets with many columns where only subsets are needed

## Initial Setup (Completed)

- [x] Downloaded Parquet.Net 5.4.0 and all dependencies
- [x] Placed libraries in `pwiz_tools\Shared\Lib\Parquet\`
- [x] Added reference to Parquet.dll in Skyline.csproj

## Dependencies Installed

Main library:
- Parquet.Net 5.4.0

Supporting libraries (in `pwiz_tools\Shared\Lib\Parquet\`):
- IronCompress 1.7.0
- Microsoft.IO.RecyclableMemoryStream 3.0.1
- System.Linq.AsyncEnumerable 10.0.0
- System.Reflection.Emit.Lightweight 4.7.0
- System.Text.Json 10.0.0
- System.IO.Pipelines 10.0.0
- System.Text.Encodings.Web 10.0.0

## Planned Implementation

### Phase 1: Core Export Functionality
- [ ] Create `ParquetReportExporter` class in appropriate namespace
- [ ] Implement IReportExporter interface (if one exists) or create export method
- [ ] Handle data type mapping (Skyline types → Parquet types)
- [ ] Support basic column types (string, numeric, boolean, datetime)

### Phase 2: UI Integration
- [ ] Add "Parquet (*.parquet)" option to export file dialog
- [ ] Add to Export menu as export format option
- [ ] Ensure proper file extension handling (.parquet)

### Phase 3: Advanced Features
- [ ] Support for nested/complex column types (if applicable)
- [ ] Compression options (Snappy, Gzip, etc.)
- [ ] Row group size configuration for large datasets
- [ ] Schema customization options

### Phase 4: Testing
- [ ] Unit tests for data type conversion
- [ ] Functional tests for export workflow
- [ ] Test with various report types
- [ ] Validate exported files can be read by external tools
- [ ] Performance testing with large datasets

## Technical Considerations

### Data Type Mapping
Need to map Skyline report column types to Parquet data types:
- Text columns → String
- Numeric columns → Double/Int32/Int64
- Boolean columns → Boolean
- DateTime columns → Timestamp

### Threading
- Export operations should run on background thread (use `ActionUtil.RunAsync()`)
- NO async/await keywords (per Skyline coding standards)
- Progress reporting via UI thread marshaling

### Error Handling
- Handle file I/O errors
- Validate data before export
- User-friendly error messages via resource strings (.resx)

### Localization
- All UI strings must be in .resx files
- Error messages must be localizable
- Test assertions must use resource strings (translation-proof testing)

## Resources

- Parquet.Net documentation: https://github.com/aloneguid/parquet-dotnet
- Apache Parquet specification: https://parquet.apache.org/docs/
- Existing export implementations in Skyline codebase for reference

## Questions to Resolve

- Which existing export format implementation should serve as a template?
- Should Parquet export support all report types or specific ones initially?
- What compression codec should be the default?
- Should we expose advanced Parquet options (compression, row group size) to users?

## Success Criteria

- Users can export any Skyline report to .parquet format
- Exported files are valid Parquet format readable by standard tools
- Export performance is comparable to CSV export for similar-sized datasets
- All strings are properly localized
- Zero ReSharper warnings
- All tests pass in all supported locales
