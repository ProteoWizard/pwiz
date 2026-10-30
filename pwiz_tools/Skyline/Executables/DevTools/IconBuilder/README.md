# IconBuilder

Produces the per-DPI PNG set (20, 24 and 32 px for 16 px icons; 18, 21 and 28 for 14 px
icons) for every small Skyline icon, from one manifest and two kinds of source. The output
is uniform, `<Name>.<Size>.png`, whatever the source kind, so the rest of the codebase never
needs to know how an icon was made.

## Why

Skyline's icons are hand-tuned 16x16 bitmaps. At 125%, 150% and 200% DPI the image lists
need larger versions, and scaling the 16 px art at run time gives blurry results (issue
#4599). This tool makes the larger sizes once, at development time, from a better source.

## Sources

| Kind | Where | What it is |
|------|-------|------------|
| `vector` | `Icons/Vector/<Name>.xaml` | A WPF `Canvas` in 32-unit coordinates (28 for 14 px icons), hand-traced from the hq2x upscale of the original. Rendered with WPF at each size. |
| `raster` | `Icons/Raster/<Name>.<W>.<H>.png` | The original 16 px bitmap upscaled 2x with hq2x (a pixel-art upscaler), magenta key applied first. Copied at its own size and bicubic-scaled to the others, with the same resampling Skyline's `DpiUtil` uses. |

Every icon has both a vector drawing and an hq2x raster checked in; `icons.manifest` says
which one is used. Vector is used where the trace matches or improves on the original;
raster is kept for the painterly icons whose per-pixel shading a trace cannot reproduce.

The native 16 px (or 14 px) bitmap in `pwiz_tools/Skyline/Resources` stays the hand-tuned
original in both cases; this tool emits only the larger sizes.

## Manifest

`icons.manifest`, one line per icon:

```
# Name                     kind    sizes
Save                       raster  20,24,32
Folder                     vector  20,24,32
FragmentS                  vector  18,21,28
```

Change an icon's source kind by editing its line; nothing else references the kind.

`common.manifest` lists pwiz.Common's icons (`Shared/Common/Resources`) in the same format. It
starts with directives, relative to the manifest's folder, that say where its sources live and
where its outputs go; `icons.manifest` uses the defaults (`Icons/Vector`, `Icons/Raster`,
`pwiz_tools/Skyline/Resources/Icons`):

```
@vector Icons\Vector\Common
@raster Icons\Raster\Common
@output ..\..\..\..\Shared\Common\Resources\Icons
```

## Usage

```bash
cd pwiz_tools/Skyline/Executables/DevTools/IconBuilder
dotnet run -c Release                      # every icon in every manifest, each into its output folder
dotnet run -c Release -- Save Copy Paste   # just these icons
dotnet run -c Release -- --manifest icons.manifest --out <dir>   # one manifest, somewhere else
```

Icon names in the manifest are the `Resources.resx` property names (`add_pro32`, `HomeIcon1`,
`Properties_Button`), not the bitmap file names, because that is how callers ask for them.

Requires the .NET 10 SDK. The tool finds `icons.manifest` by walking up from the current
directory or the exe and also builds every other `*.manifest` next to it; `--manifest <path>`
builds just that one. Icon names given on the command line must be in one of the manifests.

## Updating a source

**Vector**: edit the XAML. One-unit strokes sit on .5 coordinates and two-unit strokes on
whole coordinates so both land on whole pixels at 2x. Re-run the tool.

**Raster**: when a 16 px original changes, regenerate its hq2x image with
`Tools/hq2x_icons.py`. It needs the `hqx` package, which in turn needs Pillow older than
11, so use a throwaway venv:

```powershell
python -m venv hqxenv
hqxenv\Scripts\python.exe -m pip install "pillow<11" hqx
hqxenv\Scripts\python.exe Tools\hq2x_icons.py <folder of Name.16.16.png> Icons\Raster
```

The input PNGs must already have the magenta key applied (transparent background); the
script upscales color and alpha separately so edges stay clean. Then re-run the tool.

## Output and how Skyline uses it

`pwiz_tools/Skyline/Resources/Icons/<Name>.<Size>.png`, 32-bit ARGB with real alpha, checked
in. `Skyline.csproj` embeds the folder with one wildcard, and the DPI-aware loader
`DpiUtil.GetIcon(control, nameof(Resources.X), Resources.X, key)` (added with the system-DPI
awareness work, issue #4599) returns the size the control's DPI calls for, or the native bitmap
at 100%; without an embedded file it falls back to resampling the native. `IconResourcesTest`
fails when any small icon in `Resources.resx` is missing one of its sizes, so re-run this tool
after adding an icon.

Consumers must put the results in 32-bit image lists with `TransparentColor =
Color.Transparent`; a 24-bit list with a magenta key would flatten the alpha.
