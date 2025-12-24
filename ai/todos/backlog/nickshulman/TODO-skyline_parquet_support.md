# TODO-skyline_parquet_support.md

## Objective
Add support for exporting Skyline reports in Apache Parquet format, providing a columnar storage option that offers better compression and faster query performance for large datasets.

## Background

Apache Parquet is a columnar storage format optimized for analytical workloads. Adding Parquet export support to Skyline will provide users with:

- **Better compression**: Columnar storage typically achieves better compression ratios than row-based formats
- **Faster analytics**: Columnar format enables efficient queries by reading only required columns
- **Industry standard**: Wide support across data analysis tools (pandas, Apache Spark, R, etc.)
- **Large dataset handling**: Efficient for datasets with many columns where only subsets are needed

## Current Status

### Phase 1: Core Infrastructure (COMPLETED ✓)

**Library Setup:**
- [x] Downloaded Parquet.Net 3.3.4 (net45 version for .NET Framework 4.7.2 compatibility)
- [x] Renamed to Parquet2.dll to avoid naming conflict with native Apache Arrow parquet.dll
- [x] Placed in `pwiz_tools\Shared\Lib\Parquet\Parquet2.dll`
- [x] Added reference to Skyline.csproj with proper assembly identity

**Architecture Refactoring:**
- [x] Created `IRowItemExporter` interface (Stream-based export abstraction)
- [x] Refactored `RowItemExporter` to implement `IRowItemExporter`
- [x] Refactored `RowFactories.ExportReport()` to accept `IRowItemExporter` instead of char separator
- [x] Updated all callers to create and pass `RowItemExporter` instances:
  - `ExportLiveReportDlg.cs`
  - `CommandLine.cs`
  - `ToolDescription.cs`

**Parquet Implementation:**
- [x] Created `ParquetRowItemExporter` class implementing `IRowItemExporter`
- [x] Implemented data type mapping for basic types:
  - string → DataField&lt;string&gt;
  - int/int? → DataField&lt;int?&gt;
  - long/long? → DataField&lt;long?&gt;
  - double/double? → DataField&lt;double?&gt;
  - float/float? → DataField&lt;float?&gt;
  - bool/bool? → DataField&lt;bool?&gt;
  - DateTime/DateTime? → DataField&lt;DateTime?&gt;
  - decimal/decimal? → DataField&lt;decimal?&gt;
  - Unknown types → DataField&lt;string&gt; (ToString() fallback)
- [x] Used Parquet.Net 3.3.4 synchronous API (no async/await per Skyline standards)
- [x] Added required using statements (pwiz.Skyline.Model.Hibernate for Formats)

**Build Status:**
- [x] **Build succeeds** (Release configuration)
- ⚠️ Warning: Assembly identity mismatch (Parquet2.dll contains 'Parquet' identity) - non-blocking

### Files Modified

**New Files:**
- `pwiz_tools/Skyline/Model/Databinding/IRowItemExporter.cs`
- `pwiz_tools/Skyline/Model/Databinding/ParquetRowItemExporter.cs`

**Modified Files:**
- `pwiz_tools/Skyline/Skyline.csproj` - Added Parquet2 reference
- `pwiz_tools/Skyline/Model/Databinding/RowItemExporter.cs` - Implements IRowItemExporter
- `pwiz_tools/Skyline/Model/Databinding/RowFactories.cs` - Accepts IRowItemExporter parameter
- `pwiz_tools/Skyline/Controls/Databinding/ExportLiveReportDlg.cs` - Creates RowItemExporter
- `pwiz_tools/Skyline/CommandLine.cs` - Creates RowItemExporter
- `pwiz_tools/Skyline/Model/Tools/ToolDescription.cs` - Creates RowItemExporter

**Library Files:**
- `pwiz_tools/Shared/Lib/Parquet/Parquet2.dll` (196 KB, assembly version 3.0.0.0)
- `pwiz_tools/Shared/Lib/Parquet/Parquet2.pdb`
- `pwiz_tools/Shared/Lib/Parquet/System.Reflection.Emit.Lightweight.dll` (dependency)

### Phase 2: UI Integration (TODO)

- [ ] Add .parquet file extension to ExportLiveReportDlg file dialog filter
- [ ] Wire up ParquetRowItemExporter when .parquet extension is selected
- [ ] Add localized resource strings for "Parquet (*.parquet)" filter text
- [ ] Test file save dialog workflow end-to-end

### Phase 3: Command Line Support (TODO)

- [ ] Add --report-format=parquet option to SkylineCmd
- [ ] Update command line help text
- [ ] Add to DatabindingResources.resx for localization

### Phase 4: Testing (TODO)

**Unit Tests:**
- [ ] Test ParquetRowItemExporter data type conversions
- [ ] Test handling of null values
- [ ] Test schema generation with various column types
- [ ] Test fallback to string for unknown types

**Functional Tests:**
- [ ] Export various report types (Peptide, Protein, Transition, etc.)
- [ ] Validate exported .parquet files with external tools:
  - Python pandas: `pd.read_parquet()`
  - Apache Spark
  - R arrow package
- [ ] Test with large datasets (>10K rows)
- [ ] Test with many columns (>100 columns)
- [ ] Test special characters in column names
- [ ] Test special characters in data values

**Regression Tests:**
- [ ] Verify CSV/TSV export still works (ensure refactoring didn't break existing exports)
- [ ] Run full test suite to ensure no regressions

### Phase 5: Documentation (TODO)

- [ ] Add to Skyline user documentation
- [ ] Update tutorial if applicable
- [ ] Document Parquet export limitations (if any)
- [ ] Add release notes entry

## Technical Details

### Library Choice: Parquet.Net 3.3.4

**Why version 3.3.4?**
- Latest version compatible with .NET Framework 4.7.2
- Minimal dependencies (only System.Reflection.Emit.Lightweight)
- lib/net45 version available (not just .NET Standard)
- Synchronous API aligns with Skyline coding standards (no async/await)

**Why not 5.4.0+?**
- Newer versions only provide .NET Standard 2.0 assemblies
- Assembly resolution issues with MSBuild when using .NET Standard version
- More dependencies to manage
- Async-first API conflicts with Skyline's no async/await rule

### Naming Conflict Resolution

**Problem:** Skyline already has `libraries/arrow/parquet.dll` (Apache Arrow C++ native library)

**Solution:** Renamed .NET library to `Parquet2.dll`
- Avoids file name conflicts
- Assembly identity remains 'Parquet' internally (causes warning but non-blocking)
- MSBuild and runtime resolve correctly

### API Compatibility (Parquet.Net 3.3.4 vs. 5.x)

**Version 3.3.4 API:**
```csharp
using (var writer = new ParquetWriter(schema, stream))
{
    using (var groupWriter = writer.CreateRowGroup())
    {
        groupWriter.WriteColumn(dataColumn);
    }
}
```

**Version 5.x+ API (NOT compatible with Skyline):**
```csharp
using (var writer = await ParquetWriter.CreateAsync(schema, stream))
{
    using (var groupWriter = writer.CreateRowGroup())
    {
        await groupWriter.WriteColumnAsync(dataColumn);
    }
}
```

### Data Flow Architecture

```
User selects export → ExportLiveReportDlg
    ↓
Creates RowItemExporter (CSV) OR ParquetRowItemExporter (Parquet)
    ↓
Calls RowFactories.ExportReport(stream, viewName, exporter, ...)
    ↓
Gets RowItemEnumerator with PropertyDescriptors
    ↓
Exporter builds columns/schema and writes to stream
    ↓
File saved via FileSaver
```

## Known Issues / Limitations

1. **Assembly Identity Warning:** Parquet2.dll has internal assembly name 'Parquet' causing MSB3110 warning (non-blocking)
2. **System.Buffers/System.Memory:** These are provided automatically by .NET Framework 4.7.2, explicit references cause issues
3. **Type Mapping:** Unknown types fall back to string representation (may lose type information)

## Questions Resolved

- ✓ Which library version? → Parquet.Net 3.3.4 (net45)
- ✓ How to handle async API? → Version 3.3.4 has synchronous API
- ✓ How to integrate with existing export? → Created IRowItemExporter abstraction
- ✓ Naming conflict with Arrow parquet.dll? → Renamed to Parquet2.dll

## Questions Still Open

- How should compression be configured (Snappy, Gzip, None)?
- Should row group size be configurable for large exports?
- Should Parquet export be available for all report types or limited subset?
- What should the default behavior be for null values?

## Next Steps (Priority Order)

1. **Add UI integration** - Wire up file dialog filter for .parquet extension
2. **Manual testing** - Export a simple report and validate with Python pandas
3. **Add unit tests** - Test data type conversions and schema generation
4. **Add functional test** - Full export workflow test
5. **Documentation** - Update user guide with Parquet export instructions

## Success Criteria

- [x] Core export functionality compiles and builds
- [ ] Users can export reports to .parquet via UI
- [ ] Exported files are valid Parquet format
- [ ] Files readable by pandas, Spark, R
- [ ] Export performance comparable to CSV
- [ ] All strings localized via .resx
- [ ] Zero ReSharper warnings
- [ ] All tests pass

## Resources

- Parquet.Net 3.x documentation: https://github.com/aloneguid/parquet-dotnet/tree/3.x
- Apache Parquet specification: https://parquet.apache.org/docs/
- Skyline export code reference: `RowItemExporter.cs`, `DsvWriter.cs`
