# ImageExtractor

Extracts PNG icons from Visual Studio's ImageCatalog, at any size, for use in Skyline UI development.

## Purpose

This tool provides Skyline developers with a library of 4,000+ professional icons that can be browsed, used directly, or composed into custom Skyline icons. This is much more efficient than:
- Viewing icons one-by-one in dotPeek
- Creating icons from scratch
- Searching online for appropriate icons

## How sizes work

The catalog DLL stores ready-made, hand-tuned PNG bitmaps **only at 16x16**. Every icon also has a XAML vector image, which is what Visual Studio itself scales at high DPI.

- **16** - the native 16x16 PNGs are copied out as-is (4,047 icons)
- **Any other size** (e.g. 20, 24, 32, 48) - the XAML vector image is rendered to a PNG of that size with WPF (4,067 icons per size)

## Output

Each size goes to its own folder under `ai\.tmp\icons\`:

```
ai\.tmp\icons\16x16\database.16.16.png
ai\.tmp\icons\32x32\database.32.32.png
ai\.tmp\icons\48x48\database.48.48.png
```

Filenames are descriptive and carry the size (e.g., `database.32.32.png`, `calculator.32.32.png`, `folder.32.32.png`), so the same icon has the same base name in every size folder. One size is roughly 4MB at 16x16 and 4MB at 48x48.

## Prerequisites

1. **Visual Studio 2022 Community Edition** installed at the standard location
2. **.NET 10 SDK** (for building and running the extractor)

## Usage

```bash
cd pwiz_tools/Skyline/Executables/DevTools/ImageExtractor

# Default: native 16x16 PNGs only
dotnet run -c Release

# One or more sizes; 16 uses the native PNGs, the rest are rendered from XAML
dotnet run -c Release -- 16 32
dotnet run -c Release -- 24 32 48
```

The tool will:
1. Load the Visual Studio ImageCatalog resources DLL
2. Copy out each native 16x16 PNG (if 16 was requested)
3. Render each XAML vector image at every other requested size
4. Save them to `ai\.tmp\icons\<size>x<size>\` with descriptive filenames

Output:
```
Project Root: C:\proj\pwiz
DLL Path: C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\en\Microsoft.VisualStudio.ImageCatalog.resources.dll
Output Directory: C:\proj\pwiz\ai\.tmp\icons
Sizes: 16, 32

Loading assembly and extracting images directly from DLL...

Found 1 manifest resource(s) in DLL

Processing manifest resource: Microsoft.VisualStudio.ImageCatalog.g.en.resources
  Saved: database.32.32.png
  Saved: database.16.16.png
  ...

Done!
  Saved 4047 16x16 images to C:\proj\pwiz\ai\.tmp\icons\16x16
  Saved 4067 32x32 images to C:\proj\pwiz\ai\.tmp\icons\32x32
```

## Finding Icons

### Method 1: Windows Explorer (Recommended)
1. Open `ai\.tmp\icons\16x16\` (or another size folder) in Windows Explorer
2. Switch to "Large icons" or "Extra large icons" view
3. Use the search box to filter by keyword (e.g., "database", "folder", "calculator")

### Method 2: Command Line Search
```bash
# Find all database-related icons at 32x32
ls ai\.tmp\icons\32x32\*database*.png

# Find all folder icons at 16x16
ls ai\.tmp\icons\16x16\*folder*.png
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

### High-DPI Variants

Render the same icon at 20, 24 and 32 pixels to build the 125%, 150% and 200%
scaling variants of a toolbar or tree-view image list from one source.

## Implementation Details

The tool works by:

1. **Loading the DLL** using `Assembly.LoadFrom()`
2. **Reading manifest resources** using `ResourceReader`
3. For `png/<name>.16.16.png` entries: **converting the PNG bytes to a Bitmap** and saving them
4. For `xaml/<name>.baml` entries: **loading the compiled XAML** with `Baml2006Reader` and `XamlReader`, wrapping the root in a `Viewbox` of the requested size, and rendering it with `RenderTargetBitmap` to a PNG

Rendering from the vector images is why sizes other than 16 are available at all; there are no larger bitmaps in the DLL to extract.

## Troubleshooting

**Error: "DLL not found"**
- Verify Visual Studio 2022 Community is installed at the default location
- Update `DLL_PATH` in `Program.cs` if using a different VS edition or location

**No icons extracted (saved count = 0)**
- Check that the DLL contains `png/...16.16.png` and `xaml/....baml` entries; a major
  Visual Studio update may have changed the resource layout

**"Skipped ...: unsupported XAML root"**
- The vector image's root element is not a `FrameworkElement`, `Drawing`, `Brush` or
  `ImageSource`. Add a case to `CreateVisual` in `Program.cs` for the new root type

## Maintenance

The extracted icons live under `ai/`, which is in `.gitignore`, since they are derived from Visual Studio resources. Regenerate them as needed using the steps above.

If Visual Studio updates their ImageCatalog format significantly, the tool may need updates to handle new resource structures.
