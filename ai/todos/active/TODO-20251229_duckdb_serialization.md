# TODO-20251229_duckdb_serialization.md

## Branch Information
- **Branch**: `master` (experimental feature)
- **Created**: 2025-12-29
- **Status**: In Progress
- **GitHub Issue**: None yet
- **PR**: None yet

## Objective

Add ability to save Skyline documents in DuckDB format (.skydb) as an alternative to XML. This provides a relational database format that is efficient for large documents and enables SQL queries on document data.

## Background

DuckDB is an embedded analytical database (like SQLite but optimized for analytics). Using DuckDB format for Skyline documents could provide:
- Better performance for documents with millions of transitions
- SQL query capability for document analysis
- Smaller file sizes for sparse documents (with dynamic schema)
- Potential for incremental updates

## Implementation Summary

### Dependencies Added

**DuckDB.NET v1.4.3** (netstandard2.0 target):
- `pwiz_tools/Shared/Lib/DuckDB.NET.Bindings.dll` - Managed bindings
- `pwiz_tools/Shared/Lib/DuckDB.NET.Data.dll` - ADO.NET provider
- `pwiz_tools/Shared/Lib/DuckDB/x64/duckdb.dll` - Native library (34MB, x64 only)

**System.Memory 4.0.2.0**:
- Updated from 4.0.1.2 to resolve DuckDB.NET dependency
- Copied from Visual Studio IDE PublicAssemblies folder

### Files Created/Modified

**New Files**:
- `pwiz_tools/Skyline/Model/Serialization/DuckDb/DuckDbSerializer.cs` - Main serializer

**Modified Files**:
- `pwiz_tools/Skyline/Skyline.csproj` - Added DuckDB references and native DLL
- `pwiz_tools/Skyline/SkylineFiles.cs` - Route .skydb files to DuckDbSerializer
- `pwiz_tools/Skyline/app.config` - Assembly binding redirect for System.Memory

### Key Classes

1. **`ColumnDef<T>`** - Column metadata with value extractor lambda
2. **`TableSchema<T>`** - Dynamic schema management per table
3. **`DuckDbSerializer`** - Main serializer with:
   - Discovery phase to find used columns
   - Dynamic CREATE TABLE generation
   - Appender-based bulk inserts for performance

### Schema Design

Tables are created dynamically based on document content. Core tables:
- `document` - Format version, software version
- `molecule_group` - Proteins and peptide lists
- `molecule` - Peptides and small molecules
- `transition_group` - Precursors
- `transition` - Product ions
- Plus ~40 settings tables matching Skyline_Current.xsd

### Dynamic Schema Feature

The schema only includes columns that have non-null values in the document:
1. **Discovery pass**: Scans all items to find which columns have data
2. **Schema creation**: Generates CREATE TABLE with only used columns
3. **Data writing**: Writes only the columns in the schema

This reduces file size and schema complexity for documents that don't use all features.

## Tasks

### Completed
- [x] Download and configure DuckDB.NET libraries
- [x] Create DuckDbSerializer class with schema based on Skyline_Current.xsd
- [x] Implement Appender API for bulk inserts (performance)
- [x] Fix compile errors (AppendNullValue, property names, reserved words)
- [x] Resolve System.Memory version conflict
- [x] Implement dynamic schema feature (only include used columns)
- [x] Quote reserved words in schema (e.g., "semi")

### Remaining
- [ ] Add deserialization (read .skydb back into SrmDocument)
- [ ] Write settings tables (currently only content tables)
- [ ] Add chromatogram/peak data tables
- [ ] Add results tables
- [ ] Write unit tests
- [ ] Add UI for Save As .skydb
- [ ] Performance testing with large documents
- [ ] Consider compression options

## Key Files

- `pwiz_tools/Skyline/Model/Serialization/DuckDb/DuckDbSerializer.cs` - Main implementation
- `pwiz_tools/Skyline/SkylineFiles.cs:SaveDocument()` - Entry point
- `pwiz_tools/Skyline/TestUtil/Schemas/Skyline_Current.xsd` - XML schema reference

## Technical Notes

### DuckDB Reserved Words
Column names like `semi` must be quoted with double quotes in SQL. In C# verbatim strings, use `""semi""`.

### Appender API
DuckDB's Appender API provides bulk insert performance (~10x faster than individual INSERTs). Pattern:
```csharp
using (var appender = connection.CreateAppender("table_name"))
{
    var row = appender.CreateRow();
    row.AppendValue(value1);
    row.AppendNullValue(); // for nulls
    row.EndRow();
}
```

### Null Handling
Extension method `AppendNullableValue()` handles nullable types:
- Calls `AppendNullValue()` for null
- Calls `AppendValue()` for non-null

## Progress Log

### 2025-12-29 - Session 1
- Initial implementation of DuckDbSerializer
- Schema based on Skyline_Current.xsd with ~45 tables
- Changed all primary keys from INTEGER to BIGINT (long)
- Implemented Appender API for bulk inserts
- Fixed multiple compile errors
- Resolved System.Memory 4.0.2.0 dependency issue
- Added dynamic schema feature to only include used columns
- Fixed reserved word issue with "semi" column
- Build successful, basic serialization working
