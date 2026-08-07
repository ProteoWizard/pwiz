# SeeMS on pwiz-sharp: data-layer port notes

This is a *data-layer* port of `pwiz_tools/SeeMS` to pwiz-sharp — `DataSource.cs` and a
small console driver. The goal is to compare what reading mass-spec files looks like
through the new pure-C# pwiz-sharp library vs the old C++/CLI `pwiz_bindings_cli`.
The WinForms / DigitalRune docking / ZedGraph charting UI of the original SeeMS is not
ported (estimated 4–8 weeks on top of this MVP and no .NET 8-compatible equivalents
exist for two of the three UI deps).

## What it does

`seems-sharp` opens a vendor file and prints what the SeeMS WinForms shell shows on
file-open: counts, MS-level breakdown, RT range, first spectrum's peak data, chromatogram
list. Run on the pwiz vendor test fixtures:

| Fixture | Format | Spectra | Load time | Metadata pass |
|---|---|---|---|---|
| `090701-LTQVelos-unittest-01.raw` | Thermo | 85 | 194 ms | 72 ms |
| `timsTOF_autoMSMS_Urine_50s_neg.d` | Bruker TSF | 50 | 176 ms | 208 ms |
| `091204_NFDM_008.raw` | Waters | 41 | 47 ms | 10 ms |

Numbers are first-run on a warm OS file cache; not a benchmark, just sanity-check that
the data-layer ergonomics aren't broken.

## API surface differences (cpp/CLI → pwiz-sharp)

| pwiz_CLI | pwiz-sharp | Notes |
|---|---|---|
| `pwiz.CLI.msdata.MSData` | `Pwiz.Data.MsData.MSData` | namespace re-rooted at `Pwiz.Data.MsData` |
| `MSData.run.spectrumList` | `MSData.Run.SpectrumList` | members are PascalCase throughout |
| `MSData.run.id` | `MSData.Run.Id` | |
| `ReaderList.FullReaderList.read(path, msd, runIndex, config)` | `ReaderList.Default.Read(path, msd, config)` | no `runIndex` parameter yet (gap, see below) |
| `new ReaderConfig { simAsSpectra = … }` | `new ReaderConfig { SimAsSpectra = … }` | PascalCase fields |
| `SpectrumListSimple` / `ChromatogramListSimple` | same | identical shape |
| `spectrum.id` | `spectrum.Id` | |
| `spectrum.getMZArray()` | `spectrum.GetMZArray()` | returns nullable `BinaryDataArray?` |
| `spectrum.getArrayByCVID(CVID.MS_…)` | `spectrum.GetArrayByCvid(CVID.MS_…)` | note `Cvid` casing (matches existing pwiz-sharp convention) |
| `binaryArray.data` | `binaryArray.Data` | `IReadOnlyList<double>` instead of cpp/CLI `Indexer<double>` |
| `spec.cvParam(CVID.X).value` | `spec.Params.CvParam(CVID.X).Value` | params are nested under `.Params` (matches mzML's grouping) |
| `spec.cvParam(CVID.X).timeInSeconds()` | `spec.Params.CvParam(CVID.X).TimeInSeconds()` | |
| `spectrumList.spectrum(i, true)` | `spectrumList.GetSpectrum(i, getBinaryData: true)` | named param more discoverable than `bool` |
| `spectrumList.spectrum(i, DetailLevel.FastMetadata)` | `spectrumList.GetSpectrum(i, DetailLevel.FastMetadata)` | enum members preserved |
| `pwiz.CLI.msdata.id.abbreviate(id)` | `Pwiz.Data.MsData.Spectra.Id.Abbreviate(id)` | static helper relocated |

## Things that are *better* on pwiz-sharp

1. **No native interop step.** The cpp/CLI version requires `pwiz_bindings_cli.dll` (a
   C++/CLI assembly) which has to be built with vc145+ on Windows-only. pwiz-sharp builds
   on any .NET 8 toolchain (`dotnet build` works on Linux/macOS too, modulo vendor SDKs).
2. **`IDisposable` plumbing.** cpp SeeMS just leaks `MSData` — comments in `Types.cs`
   even acknowledge "this breaks if you access Element from a 'using' block (but why?)"
   because the cpp/CLI Spectrum doesn't have a clean Dispose contract. pwiz-sharp's
   `MSData.Dispose` cascades cleanly through `Run` → `SpectrumList` → vendor backing
   class → native handle, so `using var source = new SpectrumSource(path)` releases the
   vendor handle at end of scope. This port uses that pattern (see `Program.cs`).
3. **Nullable reference types** flow through. `GetMZArray()` returns `BinaryDataArray?`
   so the compiler nags you about the empty-spectrum case; the cpp/CLI version returned
   a default-constructed zero-length array silently.
4. **PascalCase + `Get`-prefix on accessors** matches every other .NET library; the cpp/CLI
   shape (`spectrum.id`, `getMZArray`) was a Java-via-C++/CLI artifact that surprised
   anyone who'd written C# for more than a week.
5. **No `Indexer<T>` wrapper around binary arrays** — cpp/CLI exposed a custom indexer
   class to avoid copying the binary array out of the C++ `vector<double>`. pwiz-sharp's
   `IReadOnlyList<double>` is plain CLR memory, indexable directly, no marshalling.

## Things that are *worse* (or just different)

1. **`ReaderConfig` is a subset.** Three flags SeeMS used aren't ported yet:
   - `ignoreZeroIntensityPoints`
   - `acceptZeroLengthSpectra`
   - `allowMsMsWithoutPrecursor`
   The port silently drops them and notes the gap in `GetReaderConfig`'s doc comment.
2. **No `runIndex` parameter on `ReaderList.Read`.** Multi-run WIFF files only load run 0.
   `MSDataRunPath`'s `RunIndex` field is parsed (so `path:N` URIs round-trip) but
   currently advisory.
3. **Vendor reader registration is manual.** cpp has a `FullReaderList` singleton that
   already includes Thermo / Bruker / Waters / Agilent / Sciex. pwiz-sharp has
   `ReaderList.Default` (built-in mzML / MGF) and you have to call
   `SpectrumSource.CreateFullReaderList()` (we wrote one) to attach the vendor readers.
   That's two extra lines per call site — minor, but a paper cut.
4. **No `MSDataFile.Read` static factory.** cpp has `MSDataFile::read(path, msd, …)`;
   pwiz-sharp has `ReaderList.Default.Read(path, msd, …)` — semantically equivalent,
   but the migration of every call site is "instantiate or reuse a ReaderList instance"
   instead of just calling a static.
5. **Unicode console output is ugly out of the box.** The em-dash in the RT-range print
   line came out as `?` until I switched to ASCII `-`. Cosmetic; on cpp/CLI the SeeMS
   UI shell handled this transparently because WinForms text rendering is Unicode-clean.
6. **No SeeMS-level wrappers (`MassSpectrum` / `Chromatogram` from `Types.cs`).** The
   cpp port has UI-side caching wrappers around each spectrum/chromatogram (annotation
   state, last-element-accessed cache to avoid re-fetching). pwiz-sharp uses
   `MSData.Run.SpectrumList.GetSpectrum(i)` directly. Whether this is better or worse
   depends on access pattern: random-access UI clicks would benefit from the cache,
   sequential reads don't care.
7. ~~**No `IterationListener` plumbing for progress reporting.**~~ Resolved: the data
   structures were already ported in `Util/IterationListener.cs`; mzML / mzXML / MGF
   writers now broadcast per-spectrum updates through an optional
   `IterationListenerRegistry` property, and `msconvert-sharp -v` prints progress lines.
   Vendor *readers* still don't fire updates while loading a file (the cpp readers do for
   long Bruker / Waters loads); that's a follow-up.

## UI dependencies — actually fine

I claimed in an earlier draft that `DigitalRune.Windows.Docking` and ZedGraph were dead
.NET-Framework-only libraries that would block a .NET 8 port. Both claims were wrong.
This `Program.cs` includes a runtime probe that loads + instantiates `DockPanel`:

```
[probe] DigitalRune.Docking instantiated OK (base: System.Windows.Forms.Panel, size: 200x100)
```

`DigitalRune.Windows.Docking 1.3.5` (the on-disk DLL at `pwiz_tools/Shared/Lib/`) is a
.NET Framework 4.x WinForms library, and it loads and instantiates cleanly on .NET 8 —
same compatibility path the Thermo and Bruker .NET Framework SDKs in pwiz-sharp's
vendor projects already take. .NET 8's WinForms is API-compatible with .NET Framework
WinForms, so any library that doesn't trip the API blocklist (`AppDomain.CreateDomain`,
`BinaryFormatter` without the opt-in, removed `System.Configuration` paths) Just Works.
DigitalRune.Docking doesn't.

ZedGraph isn't a checked-in DLL — it's source-built from `pwiz_tools/Shared/zedgraph/`.
That's even easier to port: bump its `<TargetFramework>` to `net8.0-windows` (or
multi-target `net48;net8.0-windows`) and it compiles. ZedGraph uses `System.Drawing`,
which works on .NET 8 as `System.Drawing.Common` (Windows-only since .NET 6).

`pwiz.MSGraph` is internal pwiz source under `pwiz_tools/Shared/MSGraph/` — same story.

## Current state of the port

| Piece | Status | File |
|---|---|---|
| `MSDataRunPath` (path:runIndex parsing) | Done | `DataSource.cs` |
| `SpectrumSource` (load + IDisposable) | Done | `DataSource.cs` |
| `IterationListener` wiring (mzML/mzXML/MGF writers) | Done | `MsData/Mzml*.cs`, `MsData/MgfSerializer.cs` |
| Multi-target ZedGraph + MSGraph | Done | `pwiz-sharp/src/{ZedGraph,MSGraph}/` |
| `ReaderConfig` flags (zero-intensity / zero-length / msMs-without-precursor / RunIndex) | Done | `MsData/IReader.cs` |
| pwiz proteome subset (AminoAcid / Peptide / Fragmentation / Modification) | Done | `Util/Proteome/` |
| `Types.cs` (GraphItem / MassSpectrum / Chromatogram / AnnotationSettings) | Done | `SeeMS/Types.cs` |
| `Annotation.cs` core (PeptideFragmentationAnnotation) | Done (algo only, panel UI deferred) | `SeeMS/Annotation.cs` |
| `Processing.cs` core (PeakPickingProcessor) | Done (algo only, panel UI deferred) | `SeeMS/Processing.cs` |
| WinForms shell (main form + dock + grids + graph + open) | Minimal version done | `SeeMS/MainForm.cs` |

`seems-sharp.exe` now launches a working WinForms app: `File → Open` opens any
pwiz-supported data file, the spectrum & chromatogram lists populate, and clicking a row
renders the spectrum or chromatogram in an MSGraphControl pane. Run with `--probe <path>`
for the original console-mode diagnostic that verifies the load path and reports counts.

## What's missing for a full SeeMS port

The 80% case (open a file, look at spectra/chromatograms, render them) works today. The
remaining work is mostly UI-panel filigree from the original SeeMS — substantial but
mechanical:

- **Per-processor / per-annotation options panels.** Each cpp `IProcessing` /
  `IAnnotation` carried its own WinForms `Panel` of options (smoothing-iterations
  dropdown, peak-pick threshold slider, peptide-fragment ion-series checkboxes, etc.).
  The pwiz-sharp ports of `Processing.cs` and `Annotation.cs` keep the core algorithms
  but expose configuration as plain C# properties; if the UI needs to surface those knobs,
  the panels are a per-class WinForms wiring exercise.
- **`OpenDataSourceDialog` (~1100 lines)** — the cpp version is a tree-view file picker
  that browses recent dirs / favorites and previews vendor file types. The pwiz-sharp
  shell uses a plain `OpenFileDialog`, which works for every supported format but loses
  the recent-files UI. Worth porting if SeeMS-sharp is going to be used regularly.
- **`Manager.cs` orchestration (~1500 lines)** — multi-source open, drag-and-drop file
  list, multi-document UI. The minimal shell holds one source at a time; multi-source
  needs the `ManagedDataSource` placeholder in `Types.cs` to grow into the real form.
- **Peptide-fragmentation annotation panel.** The algorithmic core
  (`PeptideFragmentationAnnotation`) is ported, but the cpp version's panel (sequence
  textbox, charge dropdowns, ion-series checkboxes, fragment-info datagrid) is a
  separate ~600-line WinForms binding job.
- **Spectrum / chromatogram list filtering and column customization.** The cpp
  `SpectrumListForm` includes the pwiz `DataGridViewAutoFilter` widget; the minimal
  shell uses a vanilla `DataGridView` with no filtering UI.

Realistic effort for "SeeMS-sharp running with feature parity": **3–6 weeks** of one-engineer
work (vs. the 2–3 months I overestimated when I assumed the UI deps were unsalvageable).
Most of it is mechanical pwiz_CLI → pwiz-sharp namespace+casing fixes; the only new
library work is the proteome/peptide-fragmentation port.

## Bottom line

For the *data layer* — opening files, iterating spectra, pulling binary arrays — pwiz-sharp
is a clean drop-in for pwiz_CLI. The API shape changes are mostly mechanical (PascalCase,
Get-prefix, `.Run.SpectrumList` instead of `.run.spectrumList`). The lifecycle story is
strictly better thanks to `IDisposable`. The few missing flags on `ReaderConfig` are
adds, not redesigns.

For the *UI*, the SeeMS dependencies (DigitalRune.Docking, ZedGraph, pwiz.MSGraph) all
either run unmodified on .NET 8 (DigitalRune) or are source-built and trivially
multi-targetable (ZedGraph, MSGraph). The full UI port is a mechanical migration, not a
rewrite — bottlenecked by the proteome-side annotation code (peptide fragmentation
masses), not the UI stack.
