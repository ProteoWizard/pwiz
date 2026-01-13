---
name: skyline-tester
description: Use when working with SkylineTester UI, running tests via the GUI, configuring test runs, or discussing SkylineTester tabs (Build, Tests, Tutorials, Forms, etc.). Activate when user mentions SkylineTester, the test list file, or asks about running tests through the GUI.
---

# SkylineTester Context

When working with SkylineTester, consult these documentation files for comprehensive reference.

## Primary Documentation

Read **ai/docs/skylinetester-guide.md** for:
- Key file paths (log, test list, results)
- Tab overview (Forms, Tutorials, Tests, Build, Quality, Nightly, Output, Run Stats)
- Common workflows
- Integration with Run-Tests.ps1
- Command-line options
- Troubleshooting

## Key File Paths (Quick Reference)

All paths relative to project root:

| File | Relative Path |
|------|---------------|
| **SkylineTester.exe** | `pwiz_tools/Skyline/bin/x64/Debug/SkylineTester.exe` |
| **TestRunner.exe** | `pwiz_tools/Skyline/bin/x64/Debug/TestRunner.exe` |
| **Log file** | `pwiz_tools/Skyline/SkylineTester.log` |
| **Test list** | `pwiz_tools/Skyline/SkylineTester test list.txt` |
| **Results** | `pwiz_tools/Skyline/SkylineTester Results/` |
| **Settings** | `pwiz_tools/Skyline/SkylineTester.xml` |
| **Tutorial screenshots** | `pwiz_tools/Skyline/Documentation/Tutorials/<tutorial>/<lang>/` |
| **ImageComparer** | `pwiz_tools/Skyline/Executables/DevTools/ImageComparer/` |

## The Test List as a Shared Resource

The test list file is shared between SkylineTester, Run-Tests.ps1, and Visual Studio debugger:
- Developer selects tests in SkylineTester → LLM runs same tests with `-UseTestList`
- LLM updates test list with `-UpdateTestList` → Developer sees tests pre-checked in SkylineTester
- VS debugger can be configured to read TestRunner.exe tests from this file

See [skylinetester-guide.md](../docs/skylinetester-guide.md#the-test-list-as-a-shared-resource) for details.

## Common Tasks

### Check test output
```powershell
Get-Content 'pwiz_tools/Skyline/SkylineTester.log' -Tail 50
```

### Run tests from shared test list
```powershell
./pwiz_tools/Skyline/ai/Run-Tests.ps1 -UseTestList
```

### Update test list for SkylineTester
```powershell
./pwiz_tools/Skyline/ai/Run-Tests.ps1 -TestName TestFoo -UpdateTestList
```

## Related Documentation

- **ai/docs/skylinetester-guide.md** - Comprehensive reference (tabs, workflows, integration)
- **ai/docs/skylinetester-debugging-guide.md** - Debugging hung tests, automated dev-build-test cycles
- **ai/docs/build-and-test-guide.md** - Build system and Run-Tests.ps1 details

## Source Code

Main implementation files:
- `pwiz_tools/Skyline/SkylineTester/SkylineTesterWindow.cs` - Main window and UI logic
- `pwiz_tools/Skyline/SkylineTester/TabBuild.cs` - Build tab
- `pwiz_tools/Skyline/SkylineTester/TabTests.cs` - Tests tab
- `pwiz_tools/Skyline/SkylineTester/TabTutorials.cs` - Tutorials tab
- `pwiz_tools/Skyline/SkylineTester/Main.cs` - VS detection, prerequisites
