<#
.SYNOPSIS
    Build and test SkylineBatch from LLM-assisted IDEs

.DESCRIPTION
    PowerShell script for building SkylineBatch.sln and running tests with MSTest.
    Designed for use in Cursor and other LLM-assisted development environments.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Debug)

.PARAMETER RunTests
    Run SkylineBatchTest.dll tests after building

.PARAMETER RunInspection
    Run ReSharper code inspection after building

.PARAMETER Verbosity
    MSBuild verbosity: quiet, minimal, normal, detailed, diagnostic (default: minimal)

.EXAMPLE
    .\Build-SkylineBatch.ps1
    Build SkylineBatch solution in Debug configuration

.EXAMPLE
    .\Build-SkylineBatch.ps1 -RunTests
    Build and run all tests

.EXAMPLE
    .\Build-SkylineBatch.ps1 -RunInspection -RunTests
    Build, run ReSharper inspection, and run tests

.EXAMPLE
    .\Build-SkylineBatch.ps1 -Configuration Release
    Build in Release configuration
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [Parameter(Mandatory=$false)]
    [switch]$RunTests = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$RunInspection = $false,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Platform = "Any CPU"

# Find MSBuild using vswhere (installed with VS 2022)
$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswherePath)) {
    Write-Host "❌ vswhere.exe not found - Visual Studio 2022 may not be installed" -ForegroundColor Red
    exit 1
}

$vsPath = & $vswherePath -latest -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsPath) {
    Write-Host "❌ Visual Studio installation not found" -ForegroundColor Red
    exit 1
}

$msbuildPath = Join-Path $vsPath "MSBuild\Current\Bin\amd64\MSBuild.exe"
if (-not (Test-Path $msbuildPath)) {
    Write-Host "❌ MSBuild not found at $msbuildPath" -ForegroundColor Red
    exit 1
}

Write-Host "Using MSBuild: $msbuildPath" -ForegroundColor Cyan
Write-Host ""

# Build solution
Write-Host "Building: SkylineBatch.sln ($Configuration|$Platform)" -ForegroundColor Cyan
$buildStart = Get-Date
$buildCmd = "& `"$msbuildPath`" SkylineBatch.sln /p:Configuration=$Configuration /p:Platform=`"$Platform`" /nologo /verbosity:$Verbosity"
Write-Host "Command: $buildCmd" -ForegroundColor Gray
Write-Host ""

try {
    Invoke-Expression $buildCmd
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    $buildDuration = (Get-Date) - $buildStart
    Write-Host ""
    Write-Host "✅ Build succeeded in $($buildDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

# Run ReSharper inspection if requested
if ($RunInspection) {
    Write-Host ""
    Write-Host "Running ReSharper code inspection..." -ForegroundColor Cyan
    
    # Check if ReSharper CLI tools are installed
    $jbPath = $null
    try {
        $jbPath = Get-Command jb -ErrorAction Stop | Select-Object -ExpandProperty Source
    } catch {
        Write-Host "❌ ReSharper command-line tools not installed" -ForegroundColor Red
        Write-Host ""
        Write-Host "To enable code inspection, install ReSharper CLI tools:" -ForegroundColor Yellow
        Write-Host "  dotnet tool install -g JetBrains.ReSharper.GlobalTools" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Documentation: https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Skipping code inspection..." -ForegroundColor Yellow
        Write-Host ""
    }
    
    if ($jbPath) {
        Write-Host "Using: jb inspectcode (version $(& jb --version))" -ForegroundColor Cyan
        
        $inspectionOutput = "bin\$Configuration\InspectCodeOutput.xml"
        
        $inspectStart = Get-Date
        Write-Host ""
        Write-Host "Running ReSharper code inspection (typically 2-5 minutes for SkylineBatch)..." -ForegroundColor Cyan
        
        & jb inspectcode SkylineBatch.sln `
            --output=$inspectionOutput `
            --format=Xml `
            --severity=WARNING `
            --no-swea `
            --properties=Configuration=$Configuration `
            --verbosity=WARN
        
        $inspectExitCode = $LASTEXITCODE
        $inspectDuration = (Get-Date) - $inspectStart
        
        # Parse results (simplified - no OutputParser.exe for SkylineBatch yet)
        if (Test-Path $inspectionOutput) {
            try {
                [xml]$xml = Get-Content $inspectionOutput
                $issueTypes = $xml.GetElementsByTagName("IssueType")
                $severities = @{}
                foreach ($issueType in $issueTypes) {
                    $severities[$issueType.Id] = $issueType.Severity
                }
                
                $warnings = @()
                $errors = @()
                $projects = $xml.GetElementsByTagName("Project")
                foreach ($project in $projects) {
                    foreach ($issue in $project.ChildNodes) {
                        if ($issue.Name -eq "Issue") {
                            $severity = $severities[$issue.TypeId]
                            if ($severity -eq "WARNING") {
                                $warnings += $issue
                            } elseif ($severity -eq "ERROR") {
                                $errors += $issue
                            }
                        }
                    }
                }
                
                Write-Host ""
                Write-Host "Inspection Results:" -ForegroundColor Cyan
                Write-Host "  Errors: $($errors.Count)" -ForegroundColor $(if ($errors.Count -gt 0) { "Red" } else { "Green" })
                Write-Host "  Warnings: $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -gt 0) { "Yellow" } else { "Green" })
                
                if ($errors.Count -gt 0 -or $warnings.Count -gt 0) {
                    Write-Host ""
                    foreach ($error in $errors) {
                        Write-Host "  ERROR: $($error.File):$($error.Line) - $($error.Message)" -ForegroundColor Red
                    }
                    foreach ($warning in $warnings) {
                        Write-Host "  WARNING: $($warning.File):$($warning.Line) - $($warning.Message)" -ForegroundColor Yellow
                    }
                    Write-Host ""
                    Write-Host "❌ Code inspection FAILED in $($inspectDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Red
                    exit 1
                } else {
                    Write-Host ""
                    Write-Host "✅ Code inspection passed - zero warnings/errors in $($inspectDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
                }
            } catch {
                Write-Host "⚠ Failed to parse inspection results: $_" -ForegroundColor Yellow
            }
        } else {
            Write-Host "⚠ Inspection output file not found" -ForegroundColor Yellow
        }
    }
}

# Run tests if requested
if ($RunTests) {
    Write-Host ""
    Write-Host "Running SkylineBatch tests..." -ForegroundColor Cyan
    
    # Find vstest.console.exe
    $vstestPath = Join-Path $vsPath "Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
    if (-not (Test-Path $vstestPath)) {
        # Try alternate location
        $vstestPath = Join-Path $vsPath "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
    }
    
    if (-not (Test-Path $vstestPath)) {
        Write-Host "❌ vstest.console.exe not found" -ForegroundColor Red
        exit 1
    }
    
    $testDll = "SkylineBatchTest\bin\$Configuration\SkylineBatchTest.dll"
    if (-not (Test-Path $testDll)) {
        Write-Host "❌ Test assembly not found: $testDll" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Using: $vstestPath" -ForegroundColor Gray
    Write-Host "Test assembly: $testDll" -ForegroundColor Gray
    Write-Host ""
    
    $testStart = Get-Date
    & $vstestPath $testDll /Logger:console
    $testExitCode = $LASTEXITCODE
    $testDuration = (Get-Date) - $testStart
    
    Write-Host ""
    if ($testExitCode -eq 0) {
        Write-Host "✅ All tests passed in $($testDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
    } else {
        Write-Host "❌ Tests failed in $($testDuration.TotalSeconds.ToString('F1'))s" -ForegroundColor Red
        exit $testExitCode
    }
}

Write-Host ""
Write-Host "✅ All operations completed successfully" -ForegroundColor Green

