---
description: Run code coverage analysis for current branch
---
Read ai\docs\build-and-test-guide.md (Code Coverage Analysis section) to understand the coverage workflow. Key steps:
1. Run tests with -Coverage flag: pwiz_tools\Skyline\ai\Run-Tests.ps1 -UseTestList -Coverage
2. Analyze results: pwsh -File pwiz_tools/Skyline/ai/scripts/Analyze-Coverage.ps1 -CoverageJsonPath "ai/.tmp/coverage-{timestamp}.json" -PatternsFile "ai/todos/active/TODO-{branch}-coverage.txt"
3. For line-by-line analysis, import the .dcvr file in Visual Studio via ReSharper > Unit Tests > Coverage > Import from Snapshot
