# ImageExtractor

Extracts all 16x16 PNG icons from Visual Studio's ImageCatalog for use in Skyline UI development.

## Purpose

This tool provides Skyline developers with a library of 4,000+ professional 16x16 icons that can be browsed, used directly, or composed into custom Skyline icons. This is much more efficient than:
- Viewing icons one-by-one in dotPeek
- Creating icons from scratch
- Searching online for appropriate icons

## Output

Extracts **4,048 icons** (4.2MB total) to: `ai\.tmp\icons\`

Icon filenames are descriptive (e.g., `database.16.16.png`, `calculator.16.16.png`, `folder.16.16.png`).

## Prerequisites

1. **Visual Studio 2022 Community Edition** installed at the standard location
2. **JetBrains dotPeek** (free decompiler) - [Download here](https://www.jetbrains.com/decompiler/)
3. **.NET 8 SDK** (for running the extractor)

## Usage

### Step 1: Export RESX from Visual Studio DLL (one-time setup)

Using dotPeek:

1. Open dotPeek
2. File → Open → Navigate to:
   ```
   C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\en\Microsoft.VisualStudio.ImageCatalog.resources.dll
   ```
3. In Assembly Explorer, expand:
   - `Microsoft.VisualStudio.ImageCatalog.resources.dll`
   - Resources
   - `Microsoft.VisualStudio.ImageCatalog.g.en.resources`
4. Right-click on `Microsoft.VisualStudio.ImageCatalog.g.en.resources`
5. Select "Export to Project"
6. Choose RESX format
7. Save to: `<skyline-repo>\ai\.tmp\VisualStudio.ImageCatalog.resx`

### Step 2: Run the Extractor

```bash
cd pwiz_tools/Skyline/Executables/DevTools/ImageExtractor
dotnet run -c Release
```

The tool will:
1. Parse the RESX file to find all 16x16 PNG resources (4,048 icons)
2. Extract each PNG from the Visual Studio DLL
3. Save them to `ai\.tmp\icons\` with descriptive filenames

Output:
```
RESX Path: C:\proj\scratch\ai\.tmp\VisualStudio.ImageCatalog.resx
DLL Path: C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\en\Microsoft.VisualStudio.ImageCatalog.resources.dll
Output Directory: C:\proj\scratch\ai\.tmp\icons

Parsing RESX file...
Found 4048 16x16 PNG resources in RESX

Processing manifest resource: Microsoft.VisualStudio.ImageCatalog.g.en.resources
  Saved: database.16.16.png
  Saved: calculator.16.16.png
  ...

Done! Saved 4048 16x16 images to C:\proj\scratch\ai\.tmp\icons
```

## Finding Icons

### Method 1: Windows Explorer (Recommended)
1. Open `ai\.tmp\icons\` in Windows Explorer
2. Switch to "Large icons" or "Extra large icons" view
3. Use the search box to filter by keyword (e.g., "database", "folder", "calculator")

### Method 2: Command Line Search
```bash
# Find all database-related icons
ls ai\.tmp\icons\*database*.png

# Find all folder icons
ls ai\.tmp\icons\*folder*.png
```

## Example Use Cases

### Creating Custom Skyline Icons

You can compose elements from multiple icons. Examples:

**Background Proteome (.protdb)**
- Base: `database.16.16.png`
- Overlay: `>` symbol (FASTA file indicator)

**iRT Calculator (.irtdb)**
- Base: `calculator.16.16.png`
- Modifications: Add retention time context

**Ion Mobility Library (.imsdb)**
- Base: `database.16.16.png`
- Overlay: Ion mobility symbol

**Optimization Library (.optdb)**
- Base: `database.16.16.png`
- Overlay: Optimization symbol

## Implementation Details

The tool works by:

1. **Parsing the RESX** to get resource names (e.g., `"png/database.16.16.png"`)
2. **Loading the DLL** using `Assembly.LoadFrom()`
3. **Reading manifest resources** using `ResourceReader`
4. **Extracting PNG byte data** from the resource stream
5. **Converting to Bitmap** and saving as PNG files

This approach is more reliable than trying to programmatically navigate the internal DLL structure, which can vary between Visual Studio versions.

## Troubleshooting

**Error: "RESX file not found"**
- Ensure you've completed Step 1 (export from dotPeek)
- Check the path in `Program.cs` matches where you saved the RESX

**Error: "DLL not found"**
- Verify Visual Studio 2022 Community is installed at the default location
- Update the `dllPath` in `Program.cs` if using a different VS edition or location

**No icons extracted (saved count = 0)**
- Verify the RESX file was exported correctly from dotPeek
- Check that the RESX contains `<data name="png/...16.16.png"` entries

## Maintenance

The extracted icons are checked into `.gitignore` since they're derived from Visual Studio resources. Regenerate them as needed using the steps above.

If Visual Studio updates their ImageCatalog format significantly, the tool may need updates to handle new resource structures.
