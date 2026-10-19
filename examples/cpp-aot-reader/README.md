# cpp-aot-reader

A minimal C++ example that opens an MS data file via pwiz-sharp's `MsData` reader,
without hosting the CLR or doing any C++/CLI interop. The .NET code is compiled to a
native shared library via [.NET 8 Native AOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/);
the C++ side just `#include`s a header and links to the import library.

This is the integration shape we'd use to wire pwiz-sharp into a fully-native C++
application — Skyline once it goes .NET 8 will still have managed call sites, but a
pure native consumer (e.g. someone writing a C++ tool that needs to read mzML and
doesn't want a CLR in process) gets a clean C ABI from this shim.

## Layout

| Path | Role |
|---|---|
| `../../src/MsData.NativeAot/` | The .NET shim project. `[UnmanagedCallersOnly]` exports wrapping `Pwiz.Data.MsData`. AOT-published to a self-contained native shared library. |
| `pwiz_msdata.h` | C header matching the shim's exports. Use this directly if you want the flat C ABI. |
| `pwiz_msdata.hpp` | Header-only C++ RAII wrapper over the C API. Exception-based error handling, `pwiz::msdata::File` opens/closes via constructor/destructor, no-copy/move-only semantics, `Spectrum` proxy + range-for support. |
| `main.cpp` | A ~50-line console app using the C++ wrapper. Opens a file, prints source id + spectrum count, probes a few spectra. |
| `CMakeLists.txt` | Configures the build. Default `PWIZ_MSDATA_NATIVE_DIR` points at the publish-output of the shim project; override with `-DPWIZ_MSDATA_NATIVE_DIR=...` if you've staged the artifacts elsewhere. |

## C++ API at a glance

```cpp
#include "pwiz_msdata.hpp"
namespace ms = pwiz::msdata;

try {
    ms::File file("data.mzML");

    auto sourceId = file.sourceId();     // std::string
    int count = file.spectrumCount();     // int (throws ms::Error on bad handle)

    auto first = file[0];                 // Spectrum proxy — cheap, no I/O
    std::string id = first.id();          // calls into the C API on demand
    int peaks = first.peakCount();        // reads binary data lazily

    for (auto spectrum : file) {          // range-for: walks every spectrum
        // ... use spectrum.id(), spectrum.peakCount(), spectrum.index() ...
    }
} catch (const ms::Error& e) {
    // .what() carries the AOT shim's thread-local last-error string;
    // .code() is the int error code from pwiz_msdata.h.
}
```

The `pwiz::msdata::File` class:

- **RAII** — constructor calls `pwiz_msdata_open`, destructor calls `pwiz_msdata_close`. Move-only (copying would double-close); move-assignment closes the existing handle first.
- **Exceptions** — every API failure throws `pwiz::msdata::Error`, capturing both the negative `rc` and the thread-local last-error string from the C API.
- **`Spectrum` proxy** — `file[index]` returns a lightweight object holding just a parent pointer + index. Property accessors (`.id()`, `.peakCount()`) call the C API on demand. The proxy is safe to copy/store but only valid while its parent `File` is alive.
- **Range-for** — `for (auto s : file) { ... }` walks every spectrum lazily.
- **Escape hatch** — `file.handle()` returns the underlying `pwiz_msdata_handle` if you need to drop down to the raw C API.

If you'd rather avoid C++ exceptions or RTTI, skip `pwiz_msdata.hpp` and program directly against `pwiz_msdata.h`. The two coexist; the wrapper is purely additive.

## Testing

Two layers, mirroring the cost / coverage trade:

| Layer | Where | How to run |
|---|---|---|
| **Managed shim logic** — UTF-8 truncation, GCHandle lifecycle, error codes, last-error thread-locality. Doesn't touch the AOT-compiled DLL. | `test/MsData.NativeAot.Tests/` (MSTest, .NET 8) | `dotnet test test/MsData.NativeAot.Tests/MsData.NativeAot.Tests.csproj` |
| **End-to-end AOT + native ABI** — actually loads the AOT-compiled `pwiz_msdata.dll`, runs `cpp_aot_reader.exe` against `tiny.pwiz.1.1.mzML`, asserts a golden-output regex match. Catches name-mangling regressions, missing exports, and ABI breaks that the managed-only tests can't see. | `examples/cpp-aot-reader/CMakeLists.txt` (CTest) | `./examples/cpp-aot-reader/run-tests.ps1 -Config Release` after publishing + building |

`run-tests.ps1` invokes `ctest --output-junit` and emits `##teamcity[importData type='junit']` so TeamCity surfaces each CTest case as its own test result in the build's Tests tab — no special TC runner config needed, the import-XML service message does it. Outside TC the service messages are harmless plain stdout.

For a clean from-scratch verify of both layers:

```pwsh
# Layer 1 — managed shim tests (no AOT publish required)
dotnet test test/MsData.NativeAot.Tests/MsData.NativeAot.Tests.csproj

# Layer 2 — end-to-end through the AOT DLL
dotnet publish src/MsData.NativeAot/MsData.NativeAot.csproj -c Release -r win-x64
cd examples/cpp-aot-reader
cmake -S . -B build
cmake --build build --config Release
./run-tests.ps1 -Config Release
```

## Build

### 1. Publish the AOT shim

```pwsh
# From the pwiz-sharp repo root. Pick the RID matching your target platform:
#   win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64.
dotnet publish src/MsData.NativeAot/MsData.NativeAot.csproj -c Release -r win-x64
```

Outputs land at `src/MsData.NativeAot/bin/Release/net8.0/<rid>/native/`:

- Windows: `pwiz_msdata.dll` + `pwiz_msdata.lib`
- Linux: `libpwiz_msdata.so`
- macOS: `libpwiz_msdata.dylib`

The .NET 8 ILC needs a C/C++ linker on `PATH`. On Windows that means running from a
Visual Studio "Developer PowerShell" (so `vswhere.exe` + `link.exe` resolve). On
Linux/macOS install `clang` / `lld` via the package manager.

### 2. Configure + build the C++ example

```pwsh
cd examples/cpp-aot-reader
cmake -S . -B build
cmake --build build --config Release
```

On Windows the `pwiz_msdata.dll` is copied next to the resulting `.exe` automatically.
On POSIX the linker rpath points at the publish dir so `libpwiz_msdata.so` resolves at
launch time; if you move the .exe elsewhere, set `LD_LIBRARY_PATH` or stage the .so
next to it.

### 3. Run

```pwsh
build\Release\cpp_aot_reader.exe path\to\sample.mzML
```

Example output for `tiny.pwiz.1.1.mzML`:

```
source id:       urn:lsid:psidev.info:mzML.instanceDocuments.tiny.pwiz
spectrum count:  4
  [     0] id=scan=19  peaks=15
  [     2] id=scan=21-1
  [     3] id=sample=1 period=1 cycle=22 experiment=1-1
```

## Supported formats

The shim registers pwiz-sharp's `DefaultReaderList` — same readers msconvert-sharp
uses for cross-platform formats:

- mzML (incl. indexed and gzipped)
- mzXML
- mzMLb (HDF5 — the publish dir bundles `hdf5.dll` / `hdf5_hl.dll`)
- mz5 (HDF5)
- MGF
- legacy MS1 / BMS1 / CMS1 / MS2 / BMS2 / CMS2
- Bruker BTDX

Vendor readers (Thermo .raw, Waters .raw, Bruker .d, Agilent .d, etc.) aren't included
in this shim — they require additional managed vendor projects + native vendor SDKs
that wouldn't AOT-compile cleanly. If you need vendor support, host a non-AOT
pwiz-sharp from your C++ app via `nethost` instead.

## API

See `pwiz_msdata.h`. All seven entry points (`pwiz_msdata_open`, `_close`,
`_spectrum_count`, `_spectrum_id`, `_spectrum_peak_count`, `_source_id`, `_last_error`)
are C-callable, return integers for status, and pass UTF-8 strings via `char*`. The
shim is thread-safe for parallel reads of independent handles.

## Why Native AOT vs the alternatives

| Alternative | What it is | Why not for this example |
|---|---|---|
| C++/CLI | Mixed-mode .NET assembly | Doesn't work on .NET 8 standalone — the binding model is going away |
| `nethost` / CLR hosting | Embed the CLR in the C++ process | Heavyweight (full JIT in-process); requires a complete .NET runtime alongside the app |
| COM interop | Expose .NET types as COM | Windows-only; verbose marshaling boilerplate |
| **Native AOT (this)** | Compile managed code to a self-contained native shared library | Smallest deployable; no CLR at runtime; clean C ABI; cross-platform |

The cost: not every .NET API AOT-compiles cleanly. pwiz-sharp's `MsData` core is
reflection-light (only `Assembly.GetManifestResourceStream` for the embedded OBO files,
which is AOT-compatible), so the shim publishes without trim warnings. Other parts of
pwiz-sharp may need more care — `Pwiz.Analysis`'s string-keyed `SpectrumListFactory`
dispatcher uses delegates that should be AOT-OK but haven't been validated; the
vendor-reader plugins use native SDK assemblies that AOT-trim aggressively and would
need explicit `DynamicDependency` annotations. Add modules to the shim only after
verifying they publish cleanly.
