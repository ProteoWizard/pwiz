# cpp-nethost-reader

A C++ example that reads MS data files — **including vendor formats** — with pwiz-sharp
loaded into the process via [nethost/hostfxr](https://learn.microsoft.com/dotnet/core/tutorials/netcore-hosting).

This is the companion to [`../cpp-aot-reader`](../cpp-aot-reader). Both consume the same
flat C API from the same managed sources; they differ only in how a native process gets
hold of it, and what that buys.

| | cpp-aot-reader | cpp-nethost-reader (this) |
|---|---|---|
| Managed side | `pwiz/src/MsData.NativeAot` (Native AOT) | `pwiz/src/MsData.Hosted` (ordinary library) |
| Vendor readers | ✗ | ✓ Thermo, Waters, Bruker, Agilent, Sciex, Shimadzu, UNIFI, UIMF, Mobilion |
| Deployment | one self-contained native `.dll` | .NET runtime + managed assemblies + vendor natives |
| Binding | link time, via import library | run time, via `hostfxr` |
| Startup | none | runtime init |
| C API | identical | identical |

**Why not just AOT everything?** Vendor readers bind native vendor SDK assemblies that ILC
trims aggressively and that have no AOT-compatible form. That isn't a gap to be closed later;
it's the reason this second backend exists.

## How it works

The managed exports are `[UnmanagedCallersOnly]`, and that attribute is what makes one set of
sources serve both backends. Native AOT exports them as unmangled symbols; hostfxr can hand
back a raw function pointer to the very same methods using the `UNMANAGEDCALLERSONLY_METHOD`
sentinel. `MsData.Hosted.csproj` therefore does not fork the code — it `<Compile Include>`s
`Exports.cs` and `VendorBootstrap.cs` from the AOT project, so a new export lands in both
backends by construction.

`pwiz_msdata_host.hpp` does the loading in three steps:

1. `get_hostfxr_path()` — the one piece that must be linked (`nethost`).
2. `hostfxr_initialize_for_runtime_config()` + `hostfxr_get_runtime_delegate()` — boot a
   runtime and obtain `load_assembly_and_get_function_pointer`.
3. One `load_assembly_and_get_function_pointer()` call per export, filling a dispatch table.

Two things reliably cost people an afternoon here:

- The method name passed in step 3 is the **managed method name** (`Open`), *not* the
  `EntryPoint` string from `[UnmanagedCallersOnly]` (`pwiz_msdata_open`). That attribute
  argument only governs the AOT-exported symbol name. Passing it here fails with a bare
  "method not found".
- A class library emits **no** `.runtimeconfig.json` unless the csproj sets
  `<EnableDynamicLoading>true</EnableDynamicLoading>`, and step 2 has nothing to point at
  without it.

## Build

```pwsh
# 1. The managed shim (from the pwiz-sharp repo root)
dotnet build pwiz/src/MsData.Hosted/MsData.Hosted.csproj -c Release -p:IAgreeToVendorLicenses=true

# 2. The C++ host
cd examples/cpp-nethost-reader
cmake -S . -B build
cmake --build build --config Release
```

```pwsh
build\Release\cpp_nethost_reader.exe D:\data\my-run.raw
```

Output for a Thermo `.raw`:

```
backend:         nethost (vendor readers available)
source id:       090701-LTQVelos-unittest-01
spectrum count:  85
  [     0] id=controllerType=0 controllerNumber=1 scan=1  peaks=870
  [     1] id=controllerType=0 controllerNumber=1 scan=2  peaks=20399
  [     2] id=controllerType=0 controllerNumber=1 scan=3  peaks=500
```

CMake bakes the configured `PWIZ_MSDATA_HOSTED_DIR` in as a default so the exe can run with
just a data-file argument; pass an explicit assembly directory as the first argument to
override (e.g. when you've staged the assemblies somewhere else).

### nethost: DLL vs static

The host pack ships both `nethost.lib` (a 1.7 KB import library for `nethost.dll`) and
`libnethost.lib` (1.5 MB, static). This example defaults to the **DLL** and copies
`nethost.dll` next to the executable, because `libnethost.lib` is compiled against the
static `/MT` CRT — linking it forces `/MT` on the entire consuming application and produces
`LNK2038 RuntimeLibrary mismatch` against the default `/MD`. Changing an existing
application's CRT to satisfy a hosting shim is rarely acceptable, so static is opt-in:

```pwsh
cmake -S . -B build -DPWIZ_NETHOST_STATIC=ON
```

## Deployment notes specific to the hosted backend

These are properties of pwiz-sharp's vendor support, not of hosting per se, but a native
consumer meets all of them at once.

**Vendor SDKs download on first use.** `VendorSdkLoader` ships no vendor SDK. When the JIT
first binds a `ThermoFisher.*` / `Clearcore2.*` / `MIDAC.*` assembly, the resolver fetches a
pinned archive from `raw.githubusercontent.com` into
`%LOCALAPPDATA%\ProteoWizard\vendor\<Vendor>-<ShortSha>\`. So a native app that "just reads a
Thermo file" acquires a network dependency and a per-user cache on first run. Offline or
shared deployments should pre-populate the cache and point at it with
`%PROGRAMDATA%\ProteoWizard\vendor-cache-root.txt`.

**`pwiz_msdata_init` is mandatory here.** It installs that resolver, and it has to run before
any vendor reader type is touched. The AOT backend doesn't need it (calling `open` directly
falls back to the built-in readers); this one does.

**Runtime switches travel in the runtimeconfig.** `MsData.Hosted.csproj` sets
`InvariantGlobalization=false` (the Thermo SDK needs real `en-US` culture data — the root
`Directory.Build.props` turns invariant globalization *on* for core libraries) and
`EnableUnsafeBinaryFormatterSerialization=true` (the wiff2 SDK deserializes bundled resources
through `BinaryFormatter`). Both are read by hostfxr when it boots the runtime and have no
escape hatch afterwards, so getting them wrong surfaces as a confusing failure deep inside a
reader rather than at startup.

**Native vendor dependencies still apply.** Agilent's `BaseTof.dll` is mixed-mode and imports
MSVCR120/MSVCP120; without the VC++ 2013 redistributable it fails as `ERROR_MOD_NOT_FOUND`,
which reads like a missing file rather than a missing dependency. x64 only, and most vendor
readers are Windows-only.

## Testing

```pwsh
ctest --test-dir build -C Release --output-on-failure
```

| Test | Needs |
|---|---|
| `hosted_tiny_mzml_smoke` | nothing — proves the hosting path end to end on a checked-in mzML |
| `hosted_missing_file_errors` | nothing — a bad path must fail cleanly, not crash |
| `hosted_vendor_read` | opt-in: `-DPWIZ_VENDOR_TEST_FILE=<path>` plus a primed vendor SDK cache |

The vendor case is opt-in on purpose: it needs real vendor data and a populated cache, so it
cannot run on a clean agent without setup. The first two cover the part that can.

## Known gap

`../cpp-aot-reader/pwiz_msdata.hpp` (the AOT RAII wrapper) and the `File` class in
`pwiz_msdata_host.hpp` present the same shape but are separate implementations, because the
AOT one calls linked symbols directly. Routing both through a dispatch table would let one
wrapper serve both backends; it is a contained refactor that hasn't been done yet.
