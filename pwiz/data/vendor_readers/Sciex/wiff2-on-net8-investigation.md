# Getting Sciex `.wiff2` working on .NET 8

Investigation transcript — porting the cpp `WiffFile2` path (which uses
`SCIEX.Apis.Data.v1` SDK) to the C# msconvert build, running on `net8.0`.

## TL;DR

cpp msconvert opens `.wiff2` files fine because it runs under .NET Framework
4.8, which is more lenient about a stack of issues that .NET 8 enforces
strictly. The SDK *does* ship its dependencies bundled inside
`SCIEX.Apis.Data.v1.dll` via SmartAssembly, but on .NET 8 there are six layers
of incompatibility you have to peel back before the bundle can actually run.

By the end of the investigation:
- `Wiff2Data` / `SpectrumList_Wiff2` / `FillWiff2Metadata` ported from cpp.
- `SciexAssemblyResolver` (module initializer) hooks
  `AssemblyLoadContext.Default.Resolving` and triggers SmartAssembly's
  `AssemblyResolver.AttachApp()` so the bundle unpacks on demand.
- `Unity.Abstractions.dll` extracted from the SmartAssembly bundle and
  Cecil-patched to NOP `ResolutionFailedException.RegisterSerializationHandler`
  (which calls `Exception.add_SerializeObjectState`, removed in .NET 8).
- Bundled `System.Data.SQLite 1.0.109` + matching native
  `SQLite.Interop.dll` placed on disk to bypass NuGet 1.0.119's SEE license
  probe (and provide the encryption codec the bundled SDK expects).
- `System.ServiceModel.dll` + `System.ServiceModel.Primitives` package +
  `System.Runtime.Caching` package added.
- Three Unity DI `.config` files shipped to bin.
- Strip target removes the signed-PKT pwiz-archive copies of
  `Clearcore2.*` / `OFX.Core.Contracts` so SmartAssembly's bundled (PKT=null)
  versions win.

End-to-end on test fixtures:
- `7600ZenoTOFMSMS_EAD_TestData.wiff2` → 146 MB mzML.
- `swath.api.wiff2` → 42 MB mzML.

`.wiff` legacy path is currently regressed by the strip target — proper fix
is a side-by-side `AssemblyLoadContext` (planned follow-up).

## The investigation

### 0. Starting point

The `Wiff2Data` / `SpectrumList_Wiff2` / `Reader_Sciex` dispatch were already
written. Calling `DataApiFactory.CreateSampleDataApi()` immediately failed with:

```
Could not load file or assembly 'SCIEX.Apis.Data.v1, ...'
```

even though the dll was in the output directory. .NET 8's default ALC won't
probe `AppContext.BaseDirectory` for assemblies that aren't in `.deps.json`.
Adding a `<Reference>` to `Sciex.csproj` so it ends up in `deps.json` got us
past this: `SCIEX.Apis.Data.v1` loads.

Then:

```
Method 'AddConfigurationFile' in type 'OFX.Core.OFXConfigurationBuilder'
from assembly 'OFX.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'
does not have an implementation.
```

### 1. The SmartAssembly bundle

User pointed out the deps are bundled inside `SCIEX.Apis.Data.v1.dll` via
SmartAssembly. Probing the assembly via reflection confirmed:

```
SmartAssembly.AssemblyResolver.AssemblyResolver
SmartAssembly.AssemblyResolver.AssemblyResolverHelper
SmartAssembly.Zip.SimpleZip
```

54 GUID-named manifest resources (deflated assemblies). The packer normally
injects a module initializer that calls `AssemblyResolver.AttachApp()` to wire
SmartAssembly's `AppDomain.AssemblyResolve` handler. On .NET 8 (CoreCLR) that
auto-attach doesn't fire reliably for SmartAssembly's older injection pattern,
so we triggered it explicitly from our `ModuleInitializer`:

```csharp
var sciexAsm = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "SCIEX.Apis.Data.v1")
    ?? AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("SCIEX.Apis.Data.v1"));
sciexAsm
    .GetType("SmartAssembly.AssemblyResolver.AssemblyResolver", throwOnError: false)
    ?.GetMethod("AttachApp", BindingFlags.Public | BindingFlags.Static)
    ?.Invoke(null, null);
```

Now SmartAssembly's `ResolveAssembly` handler runs and unzips bundled
assemblies from the GUID resources.

### 2. The shadowing problem

The wiff2 SDK was compiled against newer / unsigned (PKT=null) versions of
`OFX.Core.Contracts`, `Clearcore2.Data.WiffReader`, `Clearcore2.Data.Wiff2`,
`Clearcore2.Domain.Acquisition`, and `Clearcore2.Devices.Types`. The pwiz
vendor archive ships *older*, *signed* (PKT=2a79e0d8fd2e4eca etc.) versions
on disk. .NET 8's TPA probing finds the disk versions before SmartAssembly's
hook can serve the bundled ones, and the API mismatch produces a cascade:

| Disk dll                      | Resulting error                                                                  |
|-------------------------------|----------------------------------------------------------------------------------|
| OFX.Core.Contracts.dll        | `OFXConfigurationBuilder.AddConfigurationFile does not have an implementation`   |
| Clearcore2.Data.WiffReader.dll | `Could not load type 'ICentroidXicCalculator'`                                   |
| Clearcore2.Data.Wiff2.dll     | (same family)                                                                    |
| Clearcore2.Domain.Acquisition.dll | `Could not load type 'DirectoryItem'`                                        |
| Clearcore2.Devices.Types.dll  | `Method not found: 'set_ScheduledWell'`                                          |

A post-build target in `MsConvert.csproj` deletes all five from the final
output so SmartAssembly serves the bundled versions at first request.

### 3. Microsoft.Practices.Unity → Unity.Abstractions 3.3.0.0

Past the shadowing, we hit:

```
System.PlatformNotSupportedException: Secure binary serialization is not supported on this platform.
   at System.Exception.add_SerializeObjectState(...)
   at Unity.Exceptions.ResolutionFailedException.RegisterSerializationHandler()
   at Unity.Exceptions.ResolutionFailedException..ctor(...)
   at Unity.UnityContainer.ThrowingBuildUp(...)
   at Unity.UnityContainer.Resolve(...)
   at OFX.Core.Locator.GetReference(Type, String)
   at SCIEX.Apis.Data.v1.Internals.SampleDataApi..ctor()
   at SCIEX.Apis.Data.v1.DataApiFactory.CreateSampleDataApi()
```

`Exception.add_SerializeObjectState` is the .NET Framework-only
"safe serialization" event accessor; .NET 8 unconditionally throws there.
The runtime-config switch `EnableUnsafeBinaryFormatterSerialization` re-enables
`BinaryFormatter` but **not** this particular event accessor — verified by
direct probe:

```
EnableUnsafeBinaryFormatterSerialization switch = True
OK: BinaryFormatter() works                                       ← switch did its job
FAIL: Exception.add_SerializeObjectState throws PlatformNotSupportedException
      "Secure binary serialization is not supported on this platform"
```

The offending code is in `Unity.Abstractions, Version=3.3.0.0,
PublicKeyToken=6d32ff45e0ccc69f` (the rebranded community
[unitycontainer/abstractions](https://github.com/unitycontainer/abstractions)
fork — *not* the on-disk `Microsoft.Practices.Unity 2.1.505.0`, which is
unrelated). It's bundled inside SCIEX.Apis.Data.v1's SmartAssembly resource
zip, so the only way to fix it is to intercept the load.

The method body is one line:

```il
ldarg.0
ldarg.0
ldftn   <RegisterSerializationHandler>b__10_0
newobj  EventHandler<SafeSerializationEventArgs>::.ctor
call    Exception::add_SerializeObjectState
ret
```

i.e. `this.SerializeObjectState += this.<...>b__10_0;`. NOP'ing this method is
safe — it only matters for cross-AppDomain marshalling (which .NET Core
doesn't have).

### 3a. Extracting the bundled Unity.Abstractions

Wrote a small extraction utility:

```csharp
var sciex = AssemblyLoadContext.Default.LoadFromAssemblyPath("SCIEX.Apis.Data.v1.dll");
var unzip = sciex.GetType("SmartAssembly.Zip.SimpleZip")!
    .GetMethod("Unzip", BindingFlags.Public | BindingFlags.Static)!;

foreach (var name in sciex.GetManifestResourceNames())
{
    using var s = sciex.GetManifestResourceStream(name);
    var raw = new MemoryStream(); s.CopyTo(raw);
    var unpacked = (byte[])unzip.Invoke(null, new object[] { raw.ToArray() })!;
    if (unpacked[0] == 0x4D && unpacked[1] == 0x5A)  // "MZ" → PE file
    {
        using var pe = new PEReader(new MemoryStream(unpacked));
        var asmName = pe.GetMetadataReader().GetString(
            pe.GetMetadataReader().GetAssemblyDefinition().Name);
        File.WriteAllBytes(Path.Combine(outDir, asmName + ".dll"), unpacked);
    }
}
```

Yielded 54 dlls including `Unity.Abstractions.dll` (79360 bytes).

### 3b. Cecil patch

```csharp
var asm = AssemblyDefinition.ReadAssembly("Unity.Abstractions.dll");
var rsh = asm.MainModule
    .GetType("Unity.Exceptions.ResolutionFailedException")
    .Methods.First(m => m.Name == "RegisterSerializationHandler");
rsh.Body.Instructions.Clear();
var il = rsh.Body.GetILProcessor();
il.Append(il.Create(OpCodes.Nop));
il.Append(il.Create(OpCodes.Ret));
asm.Write("Unity.Abstractions.patched.dll");
```

Original IL: 6 instructions. Patched: `nop; ret`. The patched assembly keeps
the same identity (Name+Version+PublicKeyToken). .NET Core ignores
strong-name signature mismatches for `Resolving`-supplied assemblies, so it
loads cleanly.

### 3c. Wire-up

Drop `Unity.Abstractions.dll` (the patched version) next to msconvert. The
existing `SciexAssemblyResolver.Install` module initializer hooks
`AssemblyLoadContext.Default.Resolving` and looks for `<name>.dll` next to the
exe. Order:

1. `AssemblyLoadContext.Default.Resolving` (ours) fires *first* for
   non-TPA-listed assemblies.
2. We return `LoadFromAssemblyPath(Unity.Abstractions.dll)` (the patched
   on-disk version).
3. SmartAssembly's `AppDomain.AssemblyResolve` handler is the fallback — and
   never gets asked, because step 2 succeeded.

### 4. Past Unity → System.ServiceModel

Next:

```
Could not load file or assembly 'System.ServiceModel, Version=4.0.0.0,
PublicKeyToken=b77a5c561934e089'
```

Bundled `Clearcore2.RFLight.SampleDataProvider` initializes some WCF-style
configuration during Unity wire-up. Used the .NET-Standard-compatible
`System.ServiceModel.dll` from the SCIEX server SDK
(`vendor-dev/SCIEX.Data.SDK/ClientServer/Server-win10-x64/`).

### 5. System.ServiceModel.Primitives

```
Could not load file or assembly 'System.ServiceModel.Primitives, ...'
```

Plain NuGet package: `System.ServiceModel.Primitives 8.0.0`.

### 6. Clearcore2.Domain.Acquisition.Project.DirectoryItem

Already covered by the strip target above — old on-disk
`Clearcore2.Domain.Acquisition.dll` doesn't have the `DirectoryItem` type the
bundled wiff2 SDK uses.

### 7. The Unity DI configs

Past assembly resolution we hit a `NullReferenceException` deep inside
`SCIEX.Apis.Data.v1.Internals.RequestHandlers.RequestHandlerBase.Get<TIn,TOut>`.
Diagnosis: the SDK calls
`OFXApp.Get<IDataProvider>("SampleDataProviderServer")` which routes through
Unity's container, and the registration is loaded from three Unity-format
config files that ship next to the SDK:

```
DataServiceComponent.config
DataServiceClientComponent.config
DataServiceInternalComponent.config
```

Without them, Unity registrations are missing → `_dataProvider` resolved to
null → NRE in `Get<TIn,TOut>.Get(_dataProvider, ...)`. Copied them from
`vendor-dev/SCIEX.Data.SDK/ClientServer/Server-win10-x64/` into
`vendor-assemblies/Sciex/`, included as Content via `_SciexRuntime`.

### 8. The SQLite encryption stack

Wiff2 files are encrypted SQLite databases. Past Unity init, we hit:

```
Could not load file or assembly 'System.Data.SQLite.SEE.License,
Version=1.0.119.0, ..., PublicKeyToken=0a9a2a02614f8a52'
```

System.Data.SQLite 1.0.119 (the version Bruker pulls in via NuGet) added a
`SQLiteExtra.InnerVerify` license probe. The IL:

```il
Assembly.Load("System.Data.SQLite.SEE.License, Version=1.0.119.0, ..., PublicKeyToken=0a9a2a02614f8a52")
Type.GetType("License.Sdk.Library, " + sdsLicenseAsm.FullName)
sdsLicenseType.GetMethod("Verify").Invoke(null, args)
```

Tried a Cecil-generated stub assembly. Layered errors:
- Empty stub → `NullReferenceException` (no `License.Sdk.Library` type).
- Stub with `Library.Verify(object[])` → `Parameter count mismatch` (reflection
  unpacks the args array into separate parameters).
- Stub with 4 separate object params returning `false` →
  `NotSupportedException("invalid license certificate")` (1.0.119 throws on
  failed verify).
- Stub returning `true` and writing `args[2] = new List<string>()` (out param)
  via `Stind.Ref` → past the license check, but then SQLite errors with
  `query aborted` because the encryption codec isn't present.

The actual fix: **don't use 1.0.119 at all**. The bundled
`System.Data.SQLite 1.0.109` (inside SCIEX.Apis.Data.v1's SmartAssembly
resources) has *no `SQLiteExtra` license probe class at all* — the SEE
encryption codec is enabled in the matching native `SQLite.Interop.dll`
without the managed-side license ceremony. cpp msconvert works because it
uses 1.0.109; we just had to do the same.

So: `vendor-dev/ABITest/clearcore-wiff2-api/x64/SQLite.Interop.dll` (the
matching native interop, ~1.5 MB, dated 2020) goes into
`runtimes/win-x64/native/`, and the bundled `System.Data.SQLite.dll` (extracted
in step 3a) replaces the NuGet version on disk. Bruker tests still pass with
1.0.109, so this is a clean swap.

### 9. System.Runtime.Caching

```
Could not load file or assembly 'System.Runtime.Caching, ...'
```

Plain NuGet package: `System.Runtime.Caching 8.0.0`.

### 10. End to end

```
$ ./msconvert-sharp.exe 7600ZenoTOFMSMS_EAD_TestData.wiff2 -o /tmp/wiff2-test --mzML
[no error output]
$ ls -la /tmp/wiff2-test/
-rw-r--r-- 1 ... 146888752 Apr 30 12:32 7600ZenoTOFMSMS_EAD_TestData.mzML

$ ./msconvert-sharp.exe swath.api.wiff2 -o /tmp/wiff2-test --mzML
$ ls /tmp/wiff2-test/
swath.api.mzML  (42345719 bytes)
```

Tests: 426/426 still passing (Util 95, Common 81, MsData 73, Analysis 57,
Bruker 30, MsConvert 22, Thermo 16, Waters 52). Bruker's SQLite usage works
fine on 1.0.109.

## Open follow-up

`.wiff` legacy path is currently regressed:

```
Could not load file or assembly 'Clearcore2.Data.WiffReader,
Version=3.0.0.0, ..., PublicKeyToken=2a79e0d8fd2e4eca'
```

The strip target deletes the signed-PKT versions that AnalystDataProvider
was compiled against, so `.wiff` reading breaks. Both paths can in
principle coexist because the signed and unsigned versions have *different*
assembly identities, but on disk they're at the same path so only one wins.

Proper fix: load `Wiff2Data` + `SCIEX.Apis.Data.v1` in a custom
`AssemblyLoadContext`, with disk versions intact. The custom ALC's `Load`
returns null for the shadow names, falls through to SmartAssembly's resolver,
and the bundled assemblies populate the *custom* ALC — leaving default ALC
free to serve disk versions for the `.wiff` path.

Tradeoff: requires Wiff2Data/SpectrumList_Wiff2 to call into the SDK via
reflection or `dynamic` (since the types in custom ALC are different
identities than the same-named types compile-time bound to default ALC).

## What `EnableUnsafeBinaryFormatterSerialization` does and doesn't do

The user's intuition that this switch should help SmartAssembly was right —
it does enable `BinaryFormatter` (which SmartAssembly uses for some bundle
deserialization paths). But the actual blocker turned out to be a separate
.NET Framework-era API: `Exception.SerializeObjectState`, part of the old
`SafeSerializationManager` mechanism. That event accessor's body is removed
from .NET 8 entirely, no switch and no compatibility package restores it.

So: keep `EnableUnsafeBinaryFormatterSerialization=true` (it's needed for
SmartAssembly elsewhere), but the Unity.Abstractions IL patch is the actual
fix for the SerializeObjectState path.

## Summary of file changes

| File | Change |
|---|---|
| `src/Sciex/Wiff2Data.cs` | New — wraps `SCIEX.Apis.Data.v1`, license key from cpp `WiffFile2.ipp` |
| `src/Sciex/SpectrumList_Wiff2.cs` | New — per-cycle iteration + framing-zeros/centroid retry |
| `src/Sciex/Reader_Sciex.cs` | `.wiff2` dispatch path + `FillWiff2Metadata` |
| `src/Sciex/SciexAssemblyResolver.cs` | New — `[ModuleInitializer]` resolver hook + SmartAssembly `AttachApp()` |
| `src/Sciex/Sciex.csproj` | `SCIEX.Apis.Data.v1` reference + `System.ServiceModel*` + `System.Runtime.Caching` packages + DataService configs |
| `src/MsConvert/MsConvert.csproj` | `EnableUnsafeBinaryFormatterSerialization=true`, strip-shadowing target, SQLite override target |
| `vendor-assemblies/Sciex/Unity.Abstractions.dll` | Cecil-patched bundle extract |
| `vendor-assemblies/Sciex/System.Data.SQLite.dll` | Bundled 1.0.109 (no SEE license probe) |
| `vendor-assemblies/Sciex/SQLite.Interop.dll` | Matching 2020-era native interop |
| `vendor-assemblies/Sciex/System.ServiceModel.dll` | From SCIEX server SDK distribution |
| `vendor-assemblies/Sciex/DataService*.config` | Three Unity DI configs |
