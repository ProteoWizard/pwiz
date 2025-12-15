---
description: Run code coverage analysis for current branch
---
Run code coverage analysis for the current branch's new code.

## IMPORTANT: Do NOT run tests immediately

Before running any tests, you MUST:

1. **Find the coverage file list** for this branch:
   - Look for `ai/todos/active/TODO-{branch-name}-coverage.txt`
   - This file lists the tests to run AND the production files to measure

2. **Update the SkylineTester test list** with the tests specified in the coverage file:
   - File: `pwiz_tools/Skyline/SkylineTester test list.txt`
   - Copy the test names from the "TESTS TO RUN FOR COVERAGE" section
   - The coverage file header contains the exact test names needed

3. **Run tests with -UseTestList and -Coverage**:
   ```powershell
   pwiz_tools\Skyline\ai\Run-Tests.ps1 -UseTestList -Coverage
   ```

4. **Analyze results** using the coverage file as the patterns file:
   ```powershell
   pwsh -File pwiz_tools/Skyline/ai/scripts/Analyze-Coverage.ps1 `
     -CoverageJsonPath "ai/.tmp/coverage-{timestamp}.json" `
     -PatternsFile "ai/todos/active/TODO-{branch-name}-coverage.txt"
   ```

## Coverage File Structure

The coverage file (`TODO-{branch}-coverage.txt`) contains:
- **Header comments**: Lists which tests to run
- **Uncommented paths**: Production .cs files to measure coverage for
- **Commented paths**: Excluded files (tests, auto-generated, dev tools)

## For Detailed Line-by-Line Analysis

Import the `.dcvr` snapshot file in Visual Studio:
ReSharper > Unit Tests > Coverage > Import from Snapshot

## Reference

See `ai/docs/build-and-test-guide.md` (Code Coverage Analysis section) for complete documentation.
