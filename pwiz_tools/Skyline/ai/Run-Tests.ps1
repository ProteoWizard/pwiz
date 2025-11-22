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

.NOTES
    Author: LLM-assisted development
    Requires: TestRunner.exe built (run Build-Skyline.ps1 first if needed)
    Working directory: pwiz_tools\Skyline
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$TestName,
    
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
    [int]$Loop = 0,  # Number of iterations (0 = run forever, 1 = run once, 20 = run 20 times)
    
    [Parameter(Mandatory=$false)]
    [switch]$ReportHandles = $false,  # Enable handle count diagnostics
    
    [Parameter(Mandatory=$false)]
    [switch]$ReportHeaps = $false  # Enable heap count diagnostics (only useful when handles aren't leaking)
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

Write-Host "Running tests with TestRunner.exe" -ForegroundColor Cyan
Write-Host "  Test: $TestName" -ForegroundColor Gray
Write-Host "  Language(s): $languageParam" -ForegroundColor Gray
Write-Host "  UI Mode: $(if ($ShowUI) { 'On-screen (visible)' } else { 'Offscreen (hidden)' })" -ForegroundColor Gray
Write-Host "  Internet: $(if ($EnableInternet) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Gray
Write-Host "  Loop: $(if ($Loop -eq 0) { 'Forever' } else { "$Loop iterations" })" -ForegroundColor Gray
Write-Host "  Diagnostics: Handles=$(if ($ReportHandles) { 'on' } else { 'off' }), Heaps=$(if ($ReportHeaps) { 'on' } else { 'off' })" -ForegroundColor Gray
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
        
        if ($ReportHeaps) {
            $runnerArgs += "reportheaps=on"
        }

        $cmdLine = ".\TestRunner.exe " + ($runnerArgs -join ' ')
        Write-Host "Command: $cmdLine`n" -ForegroundColor Gray

        $testStart = Get-Date
        & .\TestRunner.exe $runnerArgs
        
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
        exit 0
    }
    finally {
        Pop-Location
    }
}
finally {
    Set-Location $initialLocation
}

