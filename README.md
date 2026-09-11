
![ProteoWizard Logo](http://www.proteowizard.org/img/proteowizard-logo.jpg "ProteoWizard")

The ProteoWizard Library and Tools are a set of modular and extensible open-source, cross-platform tools and software libraries that facilitate proteomics data analysis.

The libraries enable rapid tool creation by providing a robust, pluggable development framework that simplifies and unifies data file access, and performs standard chemistry and LCMS dataset computations.

Core code and libraries are under the Apache open source license; the vendor libraries fall under various vendor-specific licenses.

## Features
* reference implementation of HUPO-PSI mzML standard mass spectrometry data format
* supports HUPO-PSI mzIdentML 1.1 standard mass spectrometry analysis format
* supports reading directly from many vendor raw data formats (on Windows)
* pure C# on .NET 10 (Windows and Linux), packaged as a library, `msconvert`, MSConvertGUI, SeeMS and BiblioSpec
* modular design, for testability and extensibility
* framework for rapid development of data analysis tools
* open source license suitable for both academic and commercial projects (Apache v2)

The original C++ implementation (libpwiz, the C++ `msconvert`, Bumbershoot, BiblioSpec-C++ and the
Boost.Build tree) lives on with its full history in [ProteoWizard/pwiz-cpp](https://github.com/ProteoWizard/pwiz-cpp).

## Official build status

| Build | Status |
| ------- | ------ |
| Core Windows .NET | ![Windows status](https://img.shields.io/teamcity/https/teamcity.labkey.org/s/ProteoWizard_CoreWindowsNet.svg?label=Windows) |
| Core Linux .NET | ![Linux status](https://img.shields.io/teamcity/https/teamcity.labkey.org/s/ProteoWizard_CoreLinuxNet.svg?label=Linux) |
| Skyline Windows .NET | ![Skyline status](https://img.shields.io/teamcity/https/teamcity.labkey.org/s/ProteoWizard_SkylineWindowsNet.svg?label=Skyline) |

Click [here](https://proteowizard.sourceforge.io/download.html) to visit the official download page.

## Developer quickstart

Requirements: the .NET SDK pinned in `global.json` (`scripts/ensure-dotnet.sh` installs it on Linux),
and on Windows the Visual Studio C++ toolset for the two remaining native pieces (Hardklor, MascotShim).

```bat
i-agree-to-the-vendor-licenses.bat   REM once per machine; enables the vendor SDK readers
build.bat                             REM restore + build Pwiz.sln + run the tests
```

Alternative entry points:

- Open `Pwiz.sln` in Visual Studio 2022 or Rider
- `build.sh` on Linux (no native vendor SDKs; Thermo, mzML/mzXML/MGF and friends only)
- `build.bat --help` lists the flags (`--without-mascot`, `--coverage`, `--automated`, ...)

Key locations:

- `pwiz/utility`, `pwiz/data/{common,msdata,identdata,tradata}`, `pwiz/analysis`: the library modules, each
  with its tests in `test/` and its fixtures beside it (CV ontologies in `data/common`, `*.data` under `analysis`)
- `pwiz/data/vendor_readers/<Vendor>`: one project per vendor SDK, next to `Reader_<Vendor>_Test.data`
- `pwiz/utility/bindings`: the native-host / Native AOT exports and the Bruker PRM scheduler P/Invoke
- `pwiz_tools/Commandline`: `msconvert`, `msbenchmark`
- `pwiz_tools/MSConvertGUI`, `pwiz_tools/SeeMS`, `pwiz_tools/BiblioSpec`, `pwiz_tools/BullseyeSharp`: the tools
- `pwiz_tools/Skyline`, `pwiz_tools/Shared`, `pwiz_tools/Osprey`: Skyline and its shared libraries
- `build/`: MSBuild targets shared by pwiz and Skyline (versioning, test-data extraction, vendor SDK pins)
- `vendor-archives/`: the encrypted vendor SDK archives the readers are built against
- `scripts/installer/`: the Inno Setup installer for the pwiz tools

Common tasks:

- Clean build outputs: `clean.bat` (`clean.bat --all` also wipes the extracted vendor SDKs)
- Run one test project: `dotnet test pwiz/test/MsData.Tests -c Release`

### Skyline development

Skyline lives under `pwiz_tools/Skyline` and depends on `pwiz_tools/Shared` and the pwiz library
projects under `pwiz/src`. Always work from a full checkout of this repository, not just the
`Skyline` subtree.

- Build Skyline and run its tests (recommended first step):

```bat
pwiz_tools\Skyline\build.bat
```

- Open Skyline in VS: `pwiz_tools/Skyline/Skyline.sln`

Full setup and troubleshooting guide: [How to Build Skyline](https://skyline.ms/wiki/home/software/Skyline/page.view?name=HowToBuildSkylineTip).

Skyline C# coding conventions: see `STYLEGUIDE.md`.

### Threading Guidelines

The project avoids `async`/`await` and .NET Task support in favor of deterministic threading:

- **Use `CommonActionUtil.RunAsync()`** (in Shared projects) or `ActionUtil.RunAsync()` (in Skyline) instead of `Task.Run()` or `async`/`await`
- **Avoid .NET thread pool** - Use allocated threads for more deterministic behavior and easier debugging
- **Prefer synchronous operations** on background threads when possible
- **Thread marshaling** - Use `Invoke()` for UI thread operations from background threads

Executables note: Projects under `pwiz_tools/Skyline/Executables` are separate solutions (most build stand-alone EXEs or developer tools, some ship with Skyline). They are not built by `Skyline.sln`, but should generally follow the same coding conventions unless a local project override is required. See the Tool Store: https://skyline.ms/tools.url

EditorConfig: Repository-wide `.editorconfig` enforces core C# naming/formatting so separate solutions (including `pwiz_tools/Skyline/Executables`) inherit consistent style in Visual Studio.

Notes for AI/code assistants:

- Prefer invoking `build.bat` / `pwiz_tools\Skyline\build.bat`; avoid ad-hoc compiler calls
- Do not reformat unrelated code; keep original indentation and spacing
- Use the existing solutions and `Directory.Build.props` chain instead of introducing new build systems