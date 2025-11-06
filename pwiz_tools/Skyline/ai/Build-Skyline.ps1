<#
.SYNOPSIS
    Build and test Skyline projects - for LLM-assisted IDE use

.DESCRIPTION
    Helper script for Cursor, VS Code + Copilot, VS Code + Claude Code to perform
    iterative builds and tests without requiring manual Visual Studio interaction.

.PARAMETER Target
    What to build: Skyline, Test, TestFunctional, All, Clean, Rebuild

.PARAMETER Configuration
    Debug or Release (default: Debug)

.PARAMETER RunTests
    Run tests after successful build (default: false)

.PARAMETER Verbosity
    MSBuild verbosity: quiet, minimal, normal, detailed, diagnostic (default: minimal)

.EXAMPLE
    .\Build-Skyline.ps1
    Build entire solution in Debug configuration (default - matches Visual Studio Ctrl+Shift+B)

.EXAMPLE
    .\Build-Skyline.ps1 -Target Skyline
    Build just the Skyline project in Debug configuration

.EXAMPLE
    .\Build-Skyline.ps1 -RunTests
    Build entire solution and run unit tests (Test.dll)

.EXAMPLE
    .\Build-Skyline.ps1 -RunInspection
    Build entire solution and run ReSharper code inspection

.EXAMPLE
    .\Build-Skyline.ps1 -RunTests -TestName CodeInspection
    Build and run the CodeInspection test specifically

.EXAMPLE
    .\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection
    Pre-commit validation: Build, run inspection, and run CodeInspection test

.EXAMPLE
    .\Build-Skyline.ps1 -Configuration Release
    Build entire solution in Release configuration

.NOTES
    Author: LLM-assisted development
    Requires: Visual Studio 2022, initial full build with bs.bat
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Solution", "Skyline", "Test", "TestData", "TestFunctional", "Clean", "Rebuild")]
    [string]$Target = "Solution",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [Parameter(Mandatory=$false)]
    [switch]$RunTests = $false,
    
    [Parameter(Mandatory=$false)]
    [string]$TestName = $null,  # Specific test to run (e.g., "CodeInspection")
    
    [Parameter(Mandatory=$false)]
    [switch]$RunInspection = $false,  # Run ReSharper code inspection
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal"
)

# Find MSBuild using vswhere
$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswherePath)) {
    Write-Error "vswhere.exe not found. Is Visual Studio 2022 installed?"
    exit 1
}

$vsPath = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsPath) {
    Write-Error "Visual Studio 2022 with MSBuild not found"
    exit 1
}

$msbuild = "$vsPath\MSBuild\Current\Bin\amd64\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    Write-Error "MSBuild not found at: $msbuild"
    exit 1
}

Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan

# Determine MSBuild target
$Platform = "x64"
$buildArgs = @(
    "Skyline.sln"
)

# Add target parameter only for specific targets
# "Solution" builds entire solution (default Visual Studio behavior - no /t: parameter)
if ($Target -ne "Solution") {
    $msbuildTarget = switch ($Target) {
        "Skyline" { "Skyline" }
        "Test" { "Test" }
        "TestData" { "TestData" }
        "TestFunctional" { "TestFunctional" }
        "Clean" { "Clean" }
        "Rebuild" { "Rebuild" }
    }
    $buildArgs += "/t:$msbuildTarget"
}

# Add common build parameters
$buildArgs += @(
    "/p:Configuration=$Configuration"
    "/p:Platform=$Platform"
    "/nologo"
    "/verbosity:$Verbosity"
)

Write-Host "`nBuilding: $Target ($Configuration|$Platform)" -ForegroundColor Yellow
Write-Host "Command: & `"$msbuild`" $($buildArgs -join ' ')`n" -ForegroundColor Gray

# Execute build
$buildStart = Get-Date
& $msbuild $buildArgs
$buildExitCode = $LASTEXITCODE
$buildDuration = (Get-Date) - $buildStart

if ($buildExitCode -ne 0) {
    Write-Host "`n❌ Build FAILED (exit code: $buildExitCode) in $($buildDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Red
    exit $buildExitCode
}

Write-Host "`n✅ Build succeeded in $($buildDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green

# Run tests if requested
if ($RunTests -and $Target -ne "Clean") {
    $outputDir = "bin\$Platform\$Configuration"
    $testRunner = "$outputDir\TestRunner.exe"
    
    if (-not (Test-Path $testRunner)) {
        Write-Warning "TestRunner.exe not found at: $testRunner"
        Write-Warning "Build the TestRunner project first or skip -RunTests"
        exit 1
    }
    
    # Determine which test DLL to run
    $testDll = switch ($Target) {
        "Solution" { "Test.dll" }  # Default to fast unit tests when building entire solution
        "Test" { "Test.dll" }
        "TestData" { "TestData.dll" }
        "TestFunctional" { "TestFunctional.dll" }
        default { "Test.dll" }
    }
    
    # If specific test name provided, use it; otherwise run all tests in DLL
    $testParam = if ($TestName) { "test=$TestName" } else { "test=$testDll" }
    
    $testLog = "$outputDir\$($testDll -replace '\.dll$', '.log')"
    
    if ($TestName) {
        Write-Host "`nRunning specific test: $TestName" -ForegroundColor Yellow
    } else {
        Write-Host "`nRunning tests: $testDll" -ForegroundColor Yellow
    }
    Write-Host "Test log: $testLog`n" -ForegroundColor Gray
    
    Push-Location $outputDir
    try {
        $testStart = Get-Date
        & .\TestRunner.exe log=$testLog buildcheck=1 $testParam
        $testExitCode = $LASTEXITCODE
        $testDuration = (Get-Date) - $testStart
        
        if ($testExitCode -ne 0) {
            Write-Host "`n❌ Tests FAILED (exit code: $testExitCode) in $($testDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Red
            Write-Host "See log: $testLog" -ForegroundColor Gray
            exit $testExitCode
        }
        
        Write-Host "`n✅ All tests passed in $($testDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Run ReSharper code inspection if requested
if ($RunInspection -and $Target -ne "Clean") {
    Write-Host "`nRunning ReSharper code inspection..." -ForegroundColor Yellow
    
    # Check for ReSharper .NET global tool (jb command)
    try {
        $jbPath = & where.exe jb 2>$null
        if (-not $jbPath) {
            throw "jb command not found"
        }
    } catch {
        Write-Host "`n❌ ReSharper command-line tools not installed" -ForegroundColor Red
        Write-Host "`nTo enable code inspection, install ReSharper CLI tools:" -ForegroundColor Yellow
        Write-Host "  dotnet tool install -g JetBrains.ReSharper.GlobalTools" -ForegroundColor Cyan
        Write-Host "`nDocumentation: https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html" -ForegroundColor Gray
        Write-Host "`nSkipping code inspection...`n" -ForegroundColor Yellow
    }
    
    if ($jbPath) {
        Write-Host "Using: jb inspectcode (version $(& jb --version))" -ForegroundColor Cyan
        
        $inspectionOutput = "bin\$Platform\$Configuration\InspectCodeOutput.xml"
        $dotSettings = "Skyline.sln.DotSettings"
        
        $inspectStart = Get-Date
        Write-Host "`nRunning ReSharper code inspection (typically 20-25 minutes for full solution)..." -ForegroundColor Cyan
        
        # Run inspection matching TeamCity configuration:
        # --severity WARNING: Only report warnings and errors (not suggestions/hints)
        # --format Xml: Use XML format for compatibility with existing OutputParser.exe
        # --profile: Use solution settings, which automatically discovers project-specific .DotSettings
        #   (SkylineTester.csproj.DotSettings and TestRunner.csproj.DotSettings disable localization)
        # --no-swea: Disable solution-wide analysis (not needed for warnings)
        # NOTE: Removed --no-buildin-settings to allow project-specific .DotSettings to be respected
        & jb inspectcode Skyline.sln `
            --profile=$dotSettings `
            --output=$inspectionOutput `
            --format=Xml `
            --severity=WARNING `
            --no-swea `
            --properties=Configuration=$Configuration `
            --verbosity=WARN
        
        $inspectExitCode = $LASTEXITCODE
        $inspectDuration = (Get-Date) - $inspectStart
        
        # Use TeamCity's OutputParser.exe for exact parity with CI validation
        $outputParserPath = "Executables\LocalizationHelper\OutputParser.exe"
        
        if (Test-Path $outputParserPath) {
            Write-Host "`nParsing inspection results with OutputParser.exe (TeamCity validator)..." -ForegroundColor Cyan
            
            # OutputParser.exe validates inspection output and enforces TeamCity rules:
            # - MAX_ISSUES_ALLOWED = 0 (zero warnings required)
            # - Only counts WARNING and ERROR severity
            # - Exit code 0 = passed, 1 = failed
            & $outputParserPath $inspectionOutput
            $parserExitCode = $LASTEXITCODE
            
            if ($parserExitCode -ne 0) {
                Write-Host "`n❌ Code inspection FAILED in $($inspectDuration.TotalSeconds.ToString('F1'))s - TeamCity validation requires zero warnings" -ForegroundColor Red
                Write-Host "Fix all warnings and errors listed above before committing" -ForegroundColor Red
                exit $parserExitCode
            } else {
                Write-Host "`n✅ Code inspection passed - zero warnings/errors in $($inspectDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
            }
        } else {
            # Fallback: Basic XML parsing if OutputParser.exe not available
            Write-Warning "OutputParser.exe not found at $outputParserPath - using basic parsing"
            
            if (Test-Path $inspectionOutput) {
                try {
                    [xml]$xml = Get-Content $inspectionOutput
                    $issueTypes = $xml.GetElementsByTagName("IssueType")
                    $severities = @{}
                    foreach ($issueType in $issueTypes) {
                        $severities[$issueType.Id] = $issueType.Severity
                    }
                    
                    $allIssues = @()
                    $projects = $xml.GetElementsByTagName("Project")
                    foreach ($project in $projects) {
                        foreach ($issue in $project.ChildNodes) {
                            if ($issue.Name -eq "Issue") {
                                $severity = $severities[$issue.TypeId]
                                if ($severity -eq "WARNING" -or $severity -eq "ERROR") {
                                    $allIssues += [PSCustomObject]@{
                                        File = $issue.File
                                        Line = $issue.Line
                                        TypeId = $issue.TypeId
                                        Message = $issue.Message
                                        Severity = $severity
                                    }
                                }
                            }
                        }
                    }
                    
                    $errors = @($allIssues | Where-Object { $_.Severity -eq "ERROR" })
                    $warnings = @($allIssues | Where-Object { $_.Severity -eq "WARNING" })
                    
                    Write-Host "`nInspection Results:" -ForegroundColor Cyan
                    Write-Host "  Errors: $($errors.Count), Warnings: $($warnings.Count)" -ForegroundColor Gray
                    
                    if ($errors.Count -gt 0 -or $warnings.Count -gt 0) {
                        ($allIssues | Select-Object -First 20) | ForEach-Object {
                            $loc = if ($_.Line) { "$($_.File):$($_.Line)" } else { $_.File }
                            $color = if ($_.Severity -eq "ERROR") { "Red" } else { "Yellow" }
                            Write-Host "  [$($_.Severity)] $loc - $($_.Message)" -ForegroundColor $color
                        }
                        Write-Host "`n❌ Code inspection FAILED - $($errors.Count + $warnings.Count) issue(s) found" -ForegroundColor Red
                        exit 1
                    } else {
                        Write-Host "`n✅ Code inspection passed" -ForegroundColor Green
                    }
                } catch {
                    Write-Warning "Failed to parse: $_"
                }
            }
        }
    }
}

Write-Host "`n✅ All operations completed successfully" -ForegroundColor Green
exit 0

