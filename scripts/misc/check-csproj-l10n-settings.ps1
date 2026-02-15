param(
    [Parameter(Mandatory=$true)]
    [string]$SolutionPath
)

# Read the solution file
$solutionContent = Get-Content $SolutionPath

# Extract project paths from the solution file
$projectPaths = $solutionContent | 
    Where-Object { $_ -match 'Project\(' } |
    ForEach-Object {
        if ($_ -match '"([^"]+\.csproj)"') {
            $matches[1]
        }
    }

# Get the solution directory to resolve relative paths
$solutionDir = Split-Path -Parent (Resolve-Path $SolutionPath)

# Check each project for .DotSettings file
$missingSettings = @()
foreach ($projectPath in $projectPaths) {
    # Filter out test projects
    if ($projectPath -like "*Test*") {
        continue
    }
    
    $fullProjectPath = Join-Path $solutionDir $projectPath
    $settingsPath = "$fullProjectPath.DotSettings"
    
    if (Test-Path $fullProjectPath) {
        if (-not (Test-Path $settingsPath)) {
            $projectName = Split-Path -Leaf $projectPath
            $missingSettings += [PSCustomObject]@{
                ProjectName = $projectName
                RelativePath = $projectPath
            }
        }
    }
}

# Output results
if ($missingSettings.Count -gt 0) {
    Write-Host "Projects without .DotSettings files (excluding Test projects):" -ForegroundColor Yellow
    $missingSettings | Format-Table -AutoSize
} else {
    Write-Host "All projects have .DotSettings files!" -ForegroundColor Green
}