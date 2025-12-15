<#
.SYNOPSIS
    Run Skyline tests with TestRunner.exe - for LLM-assisted IDE use

.DESCRIPTION
    Wrapper for TestRunner.exe that supports natural language-style commands:
    - "Run CodeInspection test in English"
    - "Run current SkylineTester tests in all languages"
    - "Run TestPanoramaDownloadFile in Japanese"

.PARAMETER TestName
    Name of specific test to run, or:
    - "Test.dll" / "TestData.dll" / "TestFunctional.dll" - Run all tests in DLL
    - "@filepath" - Load test names from file (e.g., "@SkylineTester test list.txt")
    Note: Not required when using -UseTestList

.PARAMETER UseTestList
    Read tests from "SkylineTester test list.txt" file.
    Enables bidirectional sync with SkylineTester UI.

.PARAMETER UpdateTestList
    Write specified tests to "SkylineTester test list.txt" before running.
    Use with -TestName to update the shared test list for SkylineTester.

.PARAMETER Language
    Language(s) to run tests in:
    - "en" or "en-US" - English only (default)
    - "ja" or "ja-JP" - Japanese only
    - "zh" or "zh-CHS" - Chinese Simplified only
    - "fr" or "fr-FR" - French only
    - "tr" or "tr-TR" - Turkish only
    - "all" - All supported languages
    - "en,ja" - Multiple languages (comma-separated)

.PARAMETER Configuration
    Debug or Release (default: Debug)

.EXAMPLE
    .\Run-Tests.ps1 -TestName CodeInspection
    Run CodeInspection test in English (default)

.EXAMPLE
    .\Run-Tests.ps1 -TestName CodeInspection -Language all
    Run CodeInspection test in all 5 languages (en, ja, zh, fr, tr)

.EXAMPLE
    .\Run-Tests.ps1 -TestName TestPanoramaDownloadFile -Language ja
    Run TestPanoramaDownloadFile in Japanese only (offscreen - UI hidden)

.EXAMPLE
    .\Run-Tests.ps1 -TestName TestPanoramaDownloadFile -Language ja -ShowUI
    Run TestPanoramaDownloadFile in Japanese with visible UI (verify Japanese characters)

.EXAMPLE
    .\Run-Tests.ps1 -TestName "@SkylineTester test list.txt" -Language en,ja
    Run tests listed in SkylineTester test list.txt in English and Japanese

.EXAMPLE
    .\Run-Tests.ps1 -TestName Test.dll
    Run all tests in Test.dll (fast unit tests) in English

.EXAMPLE
    .\Run-Tests.ps1 -UseTestList
    Run tests from "SkylineTester test list.txt" (bidirectional sync with SkylineTester)

.EXAMPLE
    .\Run-Tests.ps1 -TestName "TestA,TestB,TestC" -UpdateTestList
    Update "SkylineTester test list.txt" with specified tests, then run them

.NOTES
    Author: LLM-assisted development
    Requires: TestRunner.exe built (run Build-Skyline.ps1 first if needed)
    Working directory: pwiz_tools\Skyline
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$TestName,

    [Parameter(Mandatory=$false)]
    [switch]$UseTestList = $false,  # Read tests from "SkylineTester test list.txt"

    [Parameter(Mandatory=$false)]
    [switch]$UpdateTestList = $false,  # Write tests to "SkylineTester test list.txt"

    [Parameter(Mandatory=$false)]
    [string]$Language = "en",

    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [Parameter(Mandatory=$false)]
    [switch]$ShowUI = $false,  # Run on-screen (offscreen=off) to see the UI

    [Parameter(Mandatory=$false)]
    [switch]$EnableInternet = $false,

    [Parameter(Mandatory=$false)]
    [switch]$TeamCityCleanup = $false,  # Use TeamCity-style cleanup (DesiredCleanupLevel=all) for local testing/debugging

    [Parameter(Mandatory=$false)]
    [int]$Loop = 0,  # Number of iterations (0 = run forever, 1 = run once, 20 = run 20 times)

    [Parameter(Mandatory=$false)]
    [switch]$ReportHandles = $false,  # Enable handle count diagnostics

    [Parameter(Mandatory=$false)]
    [switch]$SortHandlesByCount = $false,  # Sort handle types by count (descending) - leaking types rise to top

    [Parameter(Mandatory=$false)]
    [switch]$ReportHeaps = $false,  # Enable heap count diagnostics (only useful when handles aren't leaking)

    [Parameter(Mandatory=$false)]
    [switch]$Coverage = $false,  # Run with dotCover code coverage and export to JSON

    [Parameter(Mandatory=$false)]
    [string]$CoverageOutputPath = ""  # Path for coverage JSON output (default: ai\.tmp\coverage-{timestamp}.json)
)

$scriptRoot = Split-Path -Parent $PSCommandPath
$skylineRoot = Split-Path -Parent $scriptRoot
$initialLocation = Get-Location

# Ensure UTF-8 output for status symbols regardless of terminal settings
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Language mapping (supports short codes)
$languageMap = @{
    "en" = "en-US"
    "ja" = "ja-JP"
    "zh" = "zh-CHS"
    "fr" = "fr-FR"
    "tr" = "tr-TR"
    "all" = "all"
}

# Expand language shortcuts
$expandedLanguages = $Language.Split(',') | ForEach-Object {
    $lang = $_.Trim()
    if ($languageMap.ContainsKey($lang)) {
        $languageMap[$lang]
    } else {
        $lang  # Already full form like "en-US"
    }
}
$languageParam = $expandedLanguages -join ','

# Test list file path (shared with SkylineTester)
$testListFile = Join-Path $skylineRoot "SkylineTester test list.txt"

# Handle -UseTestList and -UpdateTestList parameters
if ($UseTestList) {
    if ($TestName) {
        Write-Host "⚠️ Warning: -TestName ignored when -UseTestList is specified" -ForegroundColor Yellow
    }

    if (-not (Test-Path $testListFile)) {
        Write-Host "❌ Test list file not found: $testListFile" -ForegroundColor Red
        Write-Host "Create tests in SkylineTester first, or use -TestName parameter" -ForegroundColor Yellow
        exit 1
    }

    # Read tests from file (skip comments and blank lines)
    $testsFromFile = Get-Content $testListFile |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') }

    if ($testsFromFile.Count -eq 0) {
        Write-Host "❌ No tests found in: $testListFile" -ForegroundColor Red
        Write-Host "The file exists but contains no valid test names" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "📋 Using test list from: $testListFile" -ForegroundColor Cyan
    Write-Host "   Found $($testsFromFile.Count) test(s)" -ForegroundColor Gray

    # Use @file syntax for TestRunner
    $TestName = "@$testListFile"
}
elseif ($UpdateTestList) {
    if (-not $TestName) {
        Write-Host "❌ -UpdateTestList requires -TestName parameter" -ForegroundColor Red
        Write-Host "Example: .\Run-Tests.ps1 -TestName 'TestA,TestB' -UpdateTestList" -ForegroundColor Yellow
        exit 1
    }

    # Parse test names (comma-separated)
    $testNames = $TestName.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }

    # Write new test list
    $header = "# SkylineTester test list"
    $timestamp = "# Updated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') by Run-Tests.ps1"
    $content = @($header, $timestamp, "") + $testNames
    $content | Set-Content $testListFile -Encoding UTF8

    Write-Host "📝 Updated test list: $testListFile" -ForegroundColor Cyan
    Write-Host "   Wrote $($testNames.Count) test(s)" -ForegroundColor Gray
}
elseif (-not $TestName) {
    Write-Host "❌ Either -TestName or -UseTestList is required" -ForegroundColor Red
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\Run-Tests.ps1 -TestName CodeInspection" -ForegroundColor Cyan
    Write-Host "  .\Run-Tests.ps1 -UseTestList" -ForegroundColor Cyan
    exit 1
}

# Determine output directory
$Platform = "x64"
$outputDir = "bin\$Platform\$Configuration"
$testRunner = "$outputDir\TestRunner.exe"

# Handle @file references - convert to absolute path if needed
$testParam = $TestName
if ($TestName -match '^@(.+)$') {
    $testFile = $matches[1]
    # If path is relative, make it absolute from Skyline directory
    if (-not [System.IO.Path]::IsPathRooted($testFile)) {
        $testFile = Join-Path (Get-Location) $testFile
    }
    # Convert to absolute path that TestRunner.exe can find from bin\x64\Debug
    $testParam = "@$testFile"
}

# Determine log file name
$testNameForLog = $TestName -replace '@.*[\\/]', '' -replace '\.dll$', '' -replace '\.txt$', ''
$logFile = "$testNameForLog.log"

# Find dotCover if coverage is requested
$dotCoverExe = $null
if ($Coverage) {
    $pwizRoot = Split-Path -Parent (Split-Path -Parent $skylineRoot)
    
    # Search for dotCover Command Line Tools (separate download from JetBrains)
    # Expected location: libraries\jetbrains.dotcover.commandlinetools\{version}\tools\dotCover.exe
    $searchLocations = @()
    
    # Check in libraries directory (most common location for command-line tools)
    if ($pwizRoot) {
        $libPath = Join-Path $pwizRoot "libraries"
        if (Test-Path $libPath) {
            # Look for any version of dotcover commandlinetools
            $dotCoverDirs = Get-ChildItem -Path $libPath -Directory -Filter "*dotcover*commandlinetools*" -ErrorAction SilentlyContinue
            foreach ($dir in $dotCoverDirs) {
                $exePath = Get-ChildItem -Path $dir.FullName -Recurse -Filter "dotCover.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($exePath) {
                    $dotCoverExe = $exePath.FullName
                    break
                }
            }
        }
        
        # Also check the specific path that TestRunner uses
        if (-not $dotCoverExe) {
            $specificPath = Join-Path $pwizRoot "libraries\jetbrains.dotcover.commandlinetools\2023.3.3\tools\dotCover.exe"
            if (Test-Path $specificPath) {
                $dotCoverExe = $specificPath
            }
        }
    }
    
    # Check .NET global tools location (if installed via: dotnet tool install --global JetBrains.dotCover.CommandLineTools)
    if (-not $dotCoverExe) {
        $dotnetToolsPath = Join-Path $env:USERPROFILE ".dotnet\tools\dotCover.exe"
        if (Test-Path $dotnetToolsPath) {
            $dotCoverExe = $dotnetToolsPath
        }
    }
    
    # Check other common locations (user might have extracted it elsewhere)
    if (-not $dotCoverExe) {
        $otherLocations = @(
            "${env:USERPROFILE}\Downloads\dotCover*.exe",
            "${env:USERPROFILE}\dotCover*\dotCover.exe",
            "C:\tools\dotCover*\dotCover.exe"
        )
        
        foreach ($pattern in $otherLocations) {
            $matches = Get-ChildItem -Path (Split-Path $pattern -Parent) -Filter (Split-Path $pattern -Leaf) -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($matches) {
                $dotCoverExe = $matches.FullName
                break
            }
        }
    }
    
    if (-not $dotCoverExe) {
        Write-Host "❌ dotCover.exe not found. Coverage requires JetBrains dotCover Command Line Tools." -ForegroundColor Red
        Write-Host "" -ForegroundColor Yellow
        Write-Host "   The dotCover Command Line Tools are a separate download from JetBrains." -ForegroundColor Yellow
        Write-Host "   Download from: https://www.jetbrains.com/dotcover/download/" -ForegroundColor Cyan
        Write-Host "" -ForegroundColor Yellow
        Write-Host "   Expected location: libraries\jetbrains.dotcover.commandlinetools\{version}\tools\dotCover.exe" -ForegroundColor Gray
        Write-Host "   Or extract the zip file anywhere and update the search paths in this script." -ForegroundColor Gray
        Write-Host "" -ForegroundColor Yellow
        Write-Host "   Run tests without -Coverage flag to skip coverage analysis." -ForegroundColor Yellow
        exit 1
    }
    
    # Determine coverage output path
    if ([string]::IsNullOrEmpty($CoverageOutputPath)) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $aiTmpDir = Join-Path (Split-Path -Parent (Split-Path -Parent $skylineRoot)) "ai\.tmp"
        if (-not (Test-Path $aiTmpDir)) {
            New-Item -ItemType Directory -Path $aiTmpDir -Force | Out-Null
        }
        $CoverageOutputPath = Join-Path $aiTmpDir "coverage-$timestamp.json"
    }
    
    Write-Host "📊 Coverage enabled - dotCover: $dotCoverExe" -ForegroundColor Cyan
    Write-Host "   Coverage output: $CoverageOutputPath" -ForegroundColor Gray
}

Write-Host "Running tests with TestRunner.exe" -ForegroundColor Cyan
Write-Host "  Test: $TestName" -ForegroundColor Gray
Write-Host "  Language(s): $languageParam" -ForegroundColor Gray
Write-Host "  UI Mode: $(if ($ShowUI) { 'On-screen (visible)' } else { 'Offscreen (hidden)' })" -ForegroundColor Gray
Write-Host "  Internet: $(if ($EnableInternet) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Gray
Write-Host "  Loop: $(if ($Loop -eq 0) { 'Forever' } else { "$Loop iterations" })" -ForegroundColor Gray
Write-Host "  Diagnostics: Handles=$(if ($ReportHandles) { 'on' } else { 'off' }), Heaps=$(if ($ReportHeaps) { 'on' } else { 'off' })" -ForegroundColor Gray
Write-Host "  Coverage: $(if ($Coverage) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Gray
Write-Host "  TeamCity Cleanup: $(if ($TeamCityCleanup) { 'Enabled (DesiredCleanupLevel=all)' } else { 'Disabled' })" -ForegroundColor Gray
Write-Host "  Log: $outputDir\$logFile`n" -ForegroundColor Gray

# Build TestRunner command line
# buildcheck=1: Verify build succeeded - BUT forces language to en-US and loop=1
#   -> Only use for single-language English runs
# loop=1: Run tests exactly once (default loop=0 means run forever - dangerous!)
# test=...: What to test (method name, DLL, or @file)
# language=...: Which languages to test in
# offscreen=...: Run with UI hidden (on) or visible (off)
# log=...: Where to write output
$offscreenParam = if ($ShowUI) { "off" } else { "on" }

# Only use buildcheck for English-only runs (buildcheck forces language=en-US and loop=1)
$useBuildCheck = ($languageParam -eq "en-US")

try {
    Set-Location $skylineRoot

    # Ensure TestRunner build exists
    $Platform = "x64"
    $outputDir = "bin\$Platform\$Configuration"
    $testRunner = Join-Path $skylineRoot "$outputDir\TestRunner.exe"
    if (-not (Test-Path $testRunner)) {
        Write-Host "❌ TestRunner.exe not found at: $testRunner" -ForegroundColor Red
        Write-Host "Build first with: .\ai\Build-Skyline.ps1" -ForegroundColor Yellow
        return
    }

    Push-Location $outputDir
    try {
        # Build full command line for display
        $commonArgs = @("test=$testParam", "language=$languageParam", "offscreen=$offscreenParam", "log=$logFile")

        # buildcheck forces loop=1, so don't use it when we want to loop multiple times
        if ($useBuildCheck -and ($Loop -eq 0 -or $Loop -eq 1)) {
            $runnerArgs = @("buildcheck=1") + $commonArgs
        } else {
            # Use loop parameter if specified, otherwise default to 1 (run once)
            $loopValue = if ($Loop -gt 0) { $Loop } else { 1 }
            $runnerArgs = @("loop=$loopValue") + $commonArgs
        }

        if ($EnableInternet) {
            $runnerArgs += "internet=on"
        }
        
        if ($ReportHandles) {
            $runnerArgs += "reporthandles=on"
        }

        if ($SortHandlesByCount) {
            $runnerArgs += "sorthandlesbycount=on"
        }

        if ($ReportHeaps) {
            $runnerArgs += "reportheaps=on"
        }
        
        if ($TeamCityCleanup) {
            $runnerArgs += "teamcitycleanup=on"
        }

        # Build dotCover command if coverage is enabled
        $testExecutable = ".\TestRunner.exe"
        $testArguments = $runnerArgs
        $coverageSnapshot = $null
        
        if ($Coverage) {
            # Save .dcvr snapshot to ai\.tmp directory (same location as JSON output)
            # Extract timestamp from JSON path to ensure matching filenames
            $coverageBaseName = [System.IO.Path]::GetFileNameWithoutExtension($CoverageOutputPath)
            $aiTmpDir = Split-Path -Parent $CoverageOutputPath
            $coverageSnapshot = Join-Path $aiTmpDir "$coverageBaseName.dcvr"
            $testRunnerFullPath = (Resolve-Path ".\TestRunner.exe").Path
            $testExecutable = $dotCoverExe
            
            # Check dotCover version to use appropriate syntax
            $dotCoverVersion = & $dotCoverExe --version 2>&1 | Select-String "dotCover" | ForEach-Object { if ($_ -match '(\d+\.\d+\.\d+)') { [version]$matches[1] } }
            $useNewSyntax = $dotCoverVersion -and $dotCoverVersion -ge [version]"2025.3.0"
            
            if ($useNewSyntax) {
                # New syntax (2025.3.0+)
                $dotCoverFilters = "/Filters=+:module=TestRunner /Filters=+:module=Skyline-daily /Filters=+:module=Skyline* /Filters=+:module=CommonTest " +
                                   "/Filters=+:module=Test* /Filters=+:module=MSGraph /Filters=+:module=ProteomeDb /Filters=+:module=BiblioSpec " +
                                   "/Filters=+:module=pwiz.Common* /Filters=+:module=ProteowizardWrapper* /Filters=+:module=BullseyeSharp /Filters=+:module=PanoramaClient " +
                                   "/Filters=-:class=alglib /Filters=-:class=Inference.*"
                $testArguments = @(
                    "cover",
                    $dotCoverFilters,
                    "--target-executable", $testRunnerFullPath,
                    "--snapshot-output", $coverageSnapshot,
                    "--"
                ) + $runnerArgs
            } else {
                # Old syntax (pre-2025.3.0) - each filter is a separate argument
                $testArguments = @(
                    "cover",
                    "/Filters=+:module=TestRunner",
                    "/Filters=+:module=Skyline-daily",
                    "/Filters=+:module=Skyline*",
                    "/Filters=+:module=CommonTest",
                    "/Filters=+:module=Test*",
                    "/Filters=+:module=MSGraph",
                    "/Filters=+:module=ProteomeDb",
                    "/Filters=+:module=BiblioSpec",
                    "/Filters=+:module=pwiz.Common*",
                    "/Filters=+:module=ProteowizardWrapper*",
                    "/Filters=+:module=BullseyeSharp",
                    "/Filters=+:module=PanoramaClient",
                    "/Filters=-:class=alglib",
                    "/Filters=-:class=Inference.*",
                    "/Output=$coverageSnapshot",
                    "/ReturnTargetExitCode",
                    "/AnalyzeTargetArguments=false",
                    "/TargetExecutable=$testRunnerFullPath",
                    "--"
                ) + $runnerArgs
            }
        }
        
        $cmdLine = "$testExecutable " + ($testArguments -join ' ')
        Write-Host "Command: $cmdLine`n" -ForegroundColor Gray

        $testStart = Get-Date
        & $testExecutable $testArguments
        
        $exitCode = $LASTEXITCODE
        $duration = (Get-Date) - $testStart
        
        # Check if no tests were found (common if using class name instead of method name)
        $logContent = Get-Content $logFile -Raw
        if ($logContent -match "No tests found" -and $TestName -notmatch '@|\.dll$') {
            Write-Host "`n⚠️ No tests found for: $TestName" -ForegroundColor Yellow
            Write-Host "`nSearching for [TestMethod] in class ${TestName}..." -ForegroundColor Cyan
            
            # Search for the test class and find [TestMethod] annotations
            $classFile = Get-ChildItem -Recurse -Filter "${TestName}.cs" | Select-Object -First 1
            if ($classFile) {
                Write-Host "Found class file: $($classFile.FullName)" -ForegroundColor Gray
                
                $content = Get-Content $classFile.FullName -Raw
                $methodMatches = [regex]::Matches($content, '\[TestMethod\]\s+public\s+void\s+(\w+)\s*\(')
                
                if ($methodMatches.Count -gt 0) {
                    Write-Host "`nFound [TestMethod] in ${TestName}:" -ForegroundColor Green
                    foreach ($match in $methodMatches) {
                        $methodName = $match.Groups[1].Value
                        Write-Host "  • $methodName" -ForegroundColor Cyan
                    }
                    Write-Host "`nTip: Use the method name, not the class name. For example:" -ForegroundColor Yellow
                    Write-Host "  .\ai\Run-Tests.ps1 -TestName $($methodMatches[0].Groups[1].Value) -Language en" -ForegroundColor Cyan
                } else {
                    Write-Host "No [TestMethod] found in $($classFile.Name)" -ForegroundColor Yellow
                }
            } else {
                Write-Host "Class file ${TestName}.cs not found" -ForegroundColor Yellow
            }
            
            exit 1
        }
        
        if ($exitCode -ne 0) {
            Write-Host "`n❌ Tests FAILED (exit code: $exitCode) in $($duration.TotalSeconds.ToString('F1'))s" -ForegroundColor Red
            Write-Host "`nLog file: $outputDir\$logFile" -ForegroundColor Gray
            
            # Show last 30 lines of log for quick diagnosis
            Write-Host "`nLast 30 lines of log:" -ForegroundColor Yellow
            Get-Content $logFile -Tail 30 | ForEach-Object { Write-Host "  $_" }
            
            exit $exitCode
        }
        
        Write-Host "`n✅ All tests PASSED in $($duration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
        Write-Host "Full log: $outputDir\$logFile" -ForegroundColor Gray
        
        # Export coverage to JSON if enabled
        if ($Coverage -and $coverageSnapshot -and (Test-Path $coverageSnapshot)) {
            Write-Host "`n📊 Exporting coverage to JSON..." -ForegroundColor Cyan
            
            # Check dotCover version to use appropriate syntax
            $dotCoverVersion = & $dotCoverExe --version 2>&1 | Select-String "dotCover" | ForEach-Object { if ($_ -match '(\d+\.\d+\.\d+)') { [version]$matches[1] } }
            $useNewSyntax = $dotCoverVersion -and $dotCoverVersion -ge [version]"2025.3.0"
            
            if ($useNewSyntax) {
                # New syntax (2025.3.0+) - but has bug, so we use old version
                $exportArgs = @(
                    "report",
                    "--snapshot-source", $coverageSnapshot,
                    "--json-report-output", $CoverageOutputPath
                )
            } else {
                # Old syntax (pre-2025.3.0) - uses --Source and --Output
                $exportArgs = @(
                    "report",
                    "--Source", $coverageSnapshot,
                    "--Output", $CoverageOutputPath,
                    "--ReportType", "JSON"
                )
            }
            
            $exportProcess = Start-Process -FilePath $dotCoverExe -ArgumentList $exportArgs -Wait -NoNewWindow -PassThru
            if ($exportProcess.ExitCode -eq 0 -and (Test-Path $CoverageOutputPath)) {
                Write-Host "✅ Coverage exported to:" -ForegroundColor Green
                Write-Host "   JSON:     $CoverageOutputPath" -ForegroundColor Gray
                Write-Host "   Snapshot: $coverageSnapshot" -ForegroundColor Gray
                Write-Host "`n   Analyze coverage with ai\scripts\Analyze-Coverage.ps1:" -ForegroundColor Cyan
                Write-Host "     -CoverageJsonPath `"$CoverageOutputPath`"" -ForegroundColor Gray
                Write-Host "     -PatternsFile <path-to-patterns-file>" -ForegroundColor Gray
                Write-Host "     -ReportTitle <report-title>" -ForegroundColor Gray
                Write-Host "   (See ai\docs\build-and-test-guide.md for examples)" -ForegroundColor Gray
                Write-Host "`n   Or open snapshot in Visual Studio: ReSharper > Unit Tests > Coverage > Import from Snapshot" -ForegroundColor Gray
            } else {
                Write-Host "⚠️ Failed to export coverage to JSON (exit code: $($exportProcess.ExitCode))" -ForegroundColor Yellow
                Write-Host "   Coverage snapshot saved at: $coverageSnapshot" -ForegroundColor Gray
                Write-Host "   Note: You can open the snapshot in Visual Studio (ReSharper > Unit Tests > Coverage) to export JSON" -ForegroundColor Gray
                Write-Host "   Or try manual export: dotCover report --Source `"$coverageSnapshot`" --Output `"$CoverageOutputPath`" --ReportType JSON" -ForegroundColor Gray
            }
        }
        
        exit 0
    }
    finally {
        Pop-Location
    }
}
finally {
    Set-Location $initialLocation
}

