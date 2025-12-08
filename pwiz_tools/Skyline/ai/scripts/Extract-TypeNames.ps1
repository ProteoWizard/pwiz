# Extract-TypeNames.ps1
# Extracts namespace and type (class/interface/enum/struct) names from C# source files
#
# Usage:
#   .\Extract-TypeNames.ps1 -FilePaths file1.cs,file2.cs
#   .\Extract-TypeNames.ps1 -CoverageFile TODO-...-coverage.txt
#   Get-Content file-list.txt | .\Extract-TypeNames.ps1

param(
    [Parameter(Mandatory=$false, ValueFromPipeline=$true, HelpMessage="List of .cs file paths")]
    [string[]]$FilePaths,

    [Parameter(Mandatory=$false, HelpMessage="Path to coverage patterns file (extracts .cs file paths from comments)")]
    [string]$CoverageFile,

    [Parameter(HelpMessage="Output format: FullyQualified (Namespace.Type), TypeOnly, or Namespace")]
    [ValidateSet("FullyQualified", "TypeOnly", "Namespace")]
    [string]$Format = "FullyQualified"
)

begin {
    $allFiles = @()

    # If CoverageFile is provided, extract .cs file paths from it
    if ($CoverageFile) {
        if (-not (Test-Path $CoverageFile)) {
            Write-Host "Error: Coverage file not found: $CoverageFile" -ForegroundColor Red
            exit 1
        }

        # Read uncommented lines that contain .cs file paths
        # Skip lines starting with # (comments) and blank lines
        $coverageContent = Get-Content $CoverageFile -Encoding UTF8
        foreach ($line in $coverageContent) {
            $trimmed = $line.Trim()

            # Skip comments and blank lines
            if ($trimmed -and -not $trimmed.StartsWith('#')) {
                # Line should be a .cs file path
                if ($trimmed -match '\.cs$') {
                    $allFiles += $trimmed
                }
            }
        }

        if ($allFiles.Count -eq 0) {
            Write-Host "Warning: No .cs file paths found in coverage file: $CoverageFile" -ForegroundColor Yellow
        } else {
            Write-Host "Found $($allFiles.Count) .cs files in coverage file" -ForegroundColor Gray
        }
    }
}

process {
    if ($FilePaths) {
        $allFiles += $FilePaths
    }
}

end {
    if ($allFiles.Count -eq 0) {
        Write-Host "Error: No files provided" -ForegroundColor Red
        Write-Host "Usage: .\Extract-TypeNames.ps1 -FilePaths file1.cs,file2.cs" -ForegroundColor Yellow
        exit 1
    }

    $results = @()
    $typeCount = 0

    foreach ($file in $allFiles) {
        if (-not (Test-Path $file)) {
            Write-Warning "File not found: $file"
            continue
        }

        $content = Get-Content $file -Raw -Encoding UTF8

        # Extract namespace using regex (handles namespace Name or namespace Name; format)
        $namespacePattern = 'namespace\s+([\w\.]+)'
        $namespaceMatch = [regex]::Match($content, $namespacePattern)

        if (-not $namespaceMatch.Success) {
            Write-Warning "No namespace found in: $file"
            continue
        }

        $namespace = $namespaceMatch.Groups[1].Value

        # Extract type declarations (class, interface, enum, struct, record)
        # This regex requires at least one modifier keyword before the type keyword
        # to avoid matching words in comments/documentation
        # Matches patterns like: "public class Foo", "internal interface IBar", etc.
        $typePattern = '\b(?:public|internal|private|protected)\s+(?:static\s+|abstract\s+|sealed\s+|partial\s+|readonly\s+|unsafe\s+)*(?:class|interface|enum|struct|record)\s+([\w]+)'

        $typeMatches = [regex]::Matches($content, $typePattern)

        if ($typeMatches.Count -eq 0) {
            Write-Warning "No types found in: $file"
            continue
        }

        foreach ($match in $typeMatches) {
            $typeName = $match.Groups[1].Value
            $typeCount++

            $result = switch ($Format) {
                "FullyQualified" { "$namespace.$typeName" }
                "TypeOnly" { $typeName }
                "Namespace" { $namespace }
            }

            $results += [PSCustomObject]@{
                File = $file
                Namespace = $namespace
                Type = $typeName
                FullyQualified = "$namespace.$typeName"
                Output = $result
            }
        }
    }

    # Report type count if using CoverageFile
    if ($CoverageFile -and $typeCount -gt 0) {
        Write-Host "Extracted $typeCount fully-qualified type names" -ForegroundColor Gray
    }

    # Return results
    $results
}
