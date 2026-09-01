# pwiz-sharp

C# port of the ProteoWizard C++ core, targeting .NET 8.

## Status

**Phase 1 — Foundation (in progress)**

| Project | Ports | State |
|---|---|---|
| `src/Pwiz.Util` | `pwiz/utility/misc`, `pwiz/utility/chemistry`, `pwiz/utility/math` | skeleton |
| `src/Pwiz.Data.Common` | `pwiz/data/common` (CV, ParamContainer, Unimod, Index) | skeleton |

Future phases add MSData, analysis, vendor readers, and msconvert CLI. See the plan doc.

## Build

```
cd pwiz-sharp
dotnet build
dotnet test
```

Targets `net8.0` only. No Windows-specific TFMs at this layer.
