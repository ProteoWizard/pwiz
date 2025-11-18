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
    .\Build-Skyline.ps1 -QuickInspection
    Build and run quick inspection on modified projects only (~1-5 min, ideal for iteration)

.EXAMPLE
    .\Build-Skyline.ps1 -RunInspection
    Build entire solution and run full ReSharper code inspection (~20-25 min, pre-commit)

.EXAMPLE
    .\Build-Skyline.ps1 -RunTests -TestName CodeInspection
    Build and run the CodeInspection test specifically

.EXAMPLE
    .\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection
    Pre-commit validation: Build, run full inspection, and run CodeInspection test

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
    [switch]$RunInspection = $false,  # Run ReSharper code inspection (full solution, ~20-25 min)
    
    [Parameter(Mandatory=$false)]
    [switch]$QuickInspection = $false,  # Run quick inspection on modified projects only (~1-5 min)
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal"
)

$scriptRoot = Split-Path -Parent $PSCommandPath
$skylineRoot = Split-Path -Parent $scriptRoot
$initialLocation = Get-Location

try {
    Set-Location $skylineRoot

# Ensure UTF-8 output for status symbols regardless of terminal settings
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

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

# Helper function to get modified projects from git
function Get-ProjectNameFromPath {
    param(
        [string]$FullPath,
        [string]$RepoRoot
    )

    $currentDir = Split-Path $FullPath -Parent
    while ($currentDir -and $currentDir.StartsWith($RepoRoot)) {
        $folderName = Split-Path $currentDir -Leaf
        if (-not [string]::IsNullOrWhiteSpace($folderName)) {
            $candidateCsproj = Join-Path $currentDir "$folderName.csproj"
            if (Test-Path $candidateCsproj) {
                return $folderName
            }
        }
        $parentDir = Split-Path $currentDir -Parent
        if ($parentDir -eq $currentDir) {
            break
        }
        $currentDir = $parentDir
    }

    return $null
}

function Get-ModifiedProjects {
    try {
        # Get modified files from git (excluding deleted files)
        $modifiedFiles = git diff --name-only HEAD 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Not a git repository or git not available - inspecting all projects"
            return $null
        }
        
        if (-not $modifiedFiles) {
            Write-Host "No modified files detected in git working directory" -ForegroundColor Yellow
            return @()
        }

        $repoRoot = (git rev-parse --show-toplevel 2>$null)
        if (-not $repoRoot) {
            Write-Warning "Unable to determine repository root - inspecting all projects"
            return $null
        }
        $repoRoot = [System.IO.Path]::GetFullPath($repoRoot.Trim())
        
        # Directories to skip (documentation, build artifacts)
        $skipDirs = @('ai', 'bin', 'obj', 'Executables')
        
        # Map files to project names
        $projects = @{}
        foreach ($file in $modifiedFiles) {
            # Only consider C# source files
            if ($file -notmatch '\.(cs|csproj|resx)$') {
                continue
            }
            
            # Normalize to forward slashes and skip obvious non-code directories
            $normalizedPath = $file -replace '\\', '/'
            if ($normalizedPath -match '^pwiz_tools/Skyline/(ai|bin|obj)/') {
                continue
            }
            
            $fullPath = if ([System.IO.Path]::IsPathRooted($file)) {
                [System.IO.Path]::GetFullPath($file)
            }
            else {
                [System.IO.Path]::GetFullPath((Join-Path $repoRoot $file))
            }

            if (-not (Test-Path $fullPath)) {
                continue
            }

            $projectName = Get-ProjectNameFromPath -FullPath $fullPath -RepoRoot $repoRoot

            if (-not $projectName) {
                # As a fallback, try to map files under Skyline root to the Skyline project
                if ($normalizedPath -match '^pwiz_tools/Skyline/' -or $normalizedPath -notmatch '^pwiz_tools/') {
                    $projectName = "Skyline"
                }
            }

            if ($projectName -and ($skipDirs -notcontains $projectName)) {
                $projects[$projectName] = $true
            }
        }
        
        $projectList = @($projects.Keys)
        if ($projectList.Count -gt 0) {
            Write-Host "Detected $($projectList.Count) modified project(s): $($projectList -join ', ')" -ForegroundColor Cyan
        }
        else {
            Write-Host "No C# code changes detected (only documentation/artifacts modified)" -ForegroundColor Yellow
        }
        return $projectList
    }
    catch {
        Write-Warning "Error detecting modified projects: $_"
        return $null
    }
}

# Run ReSharper code inspection if requested
if (($RunInspection -or $QuickInspection) -and $Target -ne "Clean") {
    $isQuickMode = $QuickInspection -and -not $RunInspection
    $modeLabel = if ($isQuickMode) { "Quick inspection (modified projects only)" } else { "Full solution inspection" }
    
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Running ReSharper code inspection" -ForegroundColor Cyan
    Write-Host "Mode: $modeLabel" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
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
        
        $inspectionOutput = "bin\$Platform\$Configuration\InspectCodeOutput.xml"
        $dotSettings = "Skyline.sln.DotSettings"
        
        # Set up persistent cache directory for faster subsequent runs
        # Located in bin/ so it's automatically ignored by git and cleaned by clean.bat
        $cacheDir = "bin\.inspectcode-cache"
        if (-not (Test-Path $cacheDir)) {
            New-Item -ItemType Directory -Path $cacheDir | Out-Null
            Write-Host "Created inspection cache directory: $cacheDir" -ForegroundColor Gray
        }
        
        # Determine which projects to inspect
        $projectFilter = @()
        if ($isQuickMode) {
            $modifiedProjects = Get-ModifiedProjects
            if ($modifiedProjects -eq $null) {
                # Git detection failed, fall back to full inspection
                Write-Warning "Falling back to full solution inspection"
            }
            elseif ($modifiedProjects.Count -eq 0) {
                Write-Host "No modified projects - skipping inspection" -ForegroundColor Yellow
                $jbPath = $null  # Skip inspection
            }
            else {
                $projectFilter = $modifiedProjects
            }
        }
        
        if ($jbPath) {
            $inspectStart = Get-Date
            $perProjectEstimate = 2
            $skylineEstimate = 10
            $estimatedTime = if ($projectFilter.Count -gt 0) {
                $hasSkyline = $projectFilter -contains "Skyline"
                if ($hasSkyline) {
                    $totalEstimate = $skylineEstimate + ($projectFilter.Count - 1) * $perProjectEstimate
                    "~${skylineEstimate} minutes for Skyline + ~${perProjectEstimate} minutes per additional project (~$totalEstimate minutes total for $($projectFilter.Count) project(s))"
                }
                else {
                    $totalEstimate = [Math]::Max($perProjectEstimate, $projectFilter.Count * $perProjectEstimate)
                    "~${perProjectEstimate} minutes per project (~$totalEstimate minutes total for $($projectFilter.Count) project(s))"
                }
            } else {
                "typically 20-25 minutes for full solution"
            }
            Write-Host "`nRunning ReSharper code inspection ($estimatedTime)..." -ForegroundColor Cyan
            
            # Build inspectcode command
            $inspectArgs = @(
                "inspectcode", "Skyline.sln",
                "--profile=$dotSettings",
                "--output=$inspectionOutput",
                "--format=Xml",
                "--severity=WARNING",
                "--no-swea",
                "--no-build",  # Solution already built by MSBuild above
                "--caches-home=$cacheDir",  # Enable persistent caching
                "--properties=Configuration=$Configuration",
                "--verbosity=WARN"
            )
            
            # Add project filters for quick mode
            if ($projectFilter.Count -gt 0) {
                foreach ($project in $projectFilter) {
                    $inspectArgs += "--project=$project"
                }
            }
            
            # Run inspection matching TeamCity configuration:
            # --severity WARNING: Only report warnings and errors (not suggestions/hints)
            # --format Xml: Use XML format for compatibility with existing OutputParser.exe
            # --profile: Use solution settings, which automatically discovers project-specific .DotSettings
            #   (SkylineTester.csproj.DotSettings and TestRunner.csproj.DotSettings disable localization)
            # --no-swea: Disable solution-wide analysis (not needed for warnings)
            # --no-build: Skip build phase (solution already built by MSBuild above)
            # --caches-home: Use persistent cache for faster subsequent runs (IDE-like performance)
            # --project: Filter to specific projects (quick inspection mode only)
            # NOTE: Removed --no-buildin-settings to allow project-specific .DotSettings to be respected
            & jb $inspectArgs
            
            $inspectExitCode = $LASTEXITCODE
            $inspectDuration = (Get-Date) - $inspectStart
        }
    }
    
    if ($jbPath) {
        
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
}
finally {
    Set-Location $initialLocation
}

