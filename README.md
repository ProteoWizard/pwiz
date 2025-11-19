
![ProteoWizard Logo](http://www.proteowizard.org/img/proteowizard-logo.jpg "ProteoWizard")

The ProteoWizard Library and Tools are a set of modular and extensible open-source, cross-platform tools and software libraries that facilitate proteomics data analysis.

The libraries enable rapid tool creation by providing a robust, pluggable development framework that simplifies and unifies data file access, and performs standard chemistry and LCMS dataset computations.

Core code and libraries are under the Apache open source license; the vendor libraries fall under various vendor-specific licenses.

## Features
* reference implementation of HUPO-PSI mzML standard mass spectrometry data format
* supports HUPO-PSI mzIdentML 1.1 standard mass spectrometry analysis format
* supports reading directly from many vendor raw data formats (on Windows)
* modern C++ techniques and design principles
* cross-platform with native compilers (MSVC on Windows, gcc on Linux, darwin on OSX)
* modular design, for testability and extensibility
* framework for rapid development of data analysis tools
* open source license suitable for both academic and commercial projects (Apache v2)

## Official build status

| OS      | Status |
| ------- | ------ |
| Windows | ![Windows status](https://img.shields.io/teamcity/https/teamcity.labkey.org/s/bt83.svg?label=VS%202022) |
| Native Linux | ![Linux status](https://img.shields.io/teamcity/https/teamcity.labkey.org/s/bt17.svg?label=GCC%204.9) |
| Wine Linux | ![Docker-Wine status](https://img.shields.io/teamcity/https/teamcity.labkey.org/s/ProteoWizardAndSkylineDockerContainerWineX8664.svg?label=Docker-Wine) |

Click [here](https://proteowizard.sourceforge.io/download.html) to visit the official download page.

### Unofficial toolsets
![Unofficial toolset build status](https://github.com/ProteoWizard/pwiz/actions/workflows/build_and_test.yml/badge.svg)
| OS      | Toolset    |
| ------- | -------    |
| Linux   | GCC 13    |
| ~~OS X~~    | ~~Clang 12~~   |

## Developer quickstart (Cursor)

This repository uses native builds (MSVC on Windows, GCC on Linux) and Boost.Build/Jamfiles. For a fast local build on Windows:

- Open a PowerShell or Developer Command Prompt
- Run: `quickbuild.bat`

Alternative entry points:

- Open `pwiz.sln` in Visual Studio 2022 and build the solution
- Use `quickbuild.sh` on Linux/macOS

Key locations:

- C++ libraries and tools: `pwiz/`, `pwiz_tools/`, `pwiz_aux/`
- Command-line apps (e.g., msconvert): `pwiz_tools/commandline/`
- MSVC build output (after quickbuild): `build-nt-x86/msvc-release-x86_64/`
- Third-party deps and Boost.Build: `libraries/`

Common tasks:

- Clean build outputs: `clean.bat`
- Build quickly with defaults: `quickbuild.bat`
- Documentation entry point: `doc/index.html`

### Skyline development

Skyline lives under `pwiz_tools/Skyline` and depends on `pwiz_tools/Shared` and the full ProteoWizard tree. Always work from a full checkout of this repository, not just the `Skyline` subtree.

- Build entire repo (recommended first step):

```bat
bs.bat
```

This calls the app toolset build (e.g., `pwiz_tools\build-apps.bat 64 --i-agree-to-the-vendor-licenses toolset=msvc-14.3 %*`) to build ProteoWizard libraries, command-line tools, and Skyline.

- Open Skyline in VS: `pwiz_tools/Skyline/Skyline.sln`
- Ensure `.NET` Developer Pack is installed if prompted

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

Notes for AI/code assistants (Cursor):

- Prefer invoking `quickbuild.bat` on Windows; avoid ad-hoc compiler calls
- Do not reformat unrelated code; keep original indentation and spacing
- Use existing Jamfiles/solution instead of introducing new build systems
- When adding C++ files, update the appropriate Jamfile or Visual Studio project as needed