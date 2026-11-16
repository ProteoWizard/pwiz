<#
.SYNOPSIS
    CarafeSharp's test data packages: where they go, and fetching and verifying them.

.DESCRIPTION
    Dot-source this file. It follows the rules of CarafeSharp.Test/TestData.cs, which reads the
    same testdata.json:
      * The packages are extracted under one root: CARAFESHARP_TESTDATA when it is set, else
        <Downloads>/Perftests, where <Downloads> is SKYLINE_DOWNLOAD_PATH, else the user's
        Downloads folder (the known-folder registry value on Windows, as the Skyline and Osprey
        perf tests take it).
      * Each zip holds one top folder named like it, ending in MANIFEST.sha256 ("<sha256>  <path>"
        lines, paths relative to the top folder), the zip's last entry: a folder without it is an
        extraction that stopped part way.

    Fetch downloads a package's zip into the root (kept there, as the perf tests keep theirs),
    checks its size and SHA-256 against testdata.json, and extracts it to a temporary folder
    that is renamed into place only when complete. A package already present is left alone.
    Verify checks every file of each package against its MANIFEST.sha256, a zip kept in the
    root against testdata.json, and the committed pretrained_models.zip against its pin.
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-TestDataDownloadsPath {
    if ($env:SKYLINE_DOWNLOAD_PATH) {
        return $env:SKYLINE_DOWNLOAD_PATH
    }
    if ($IsWindows) {
        $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders'
        $value = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).'{374DE290-123F-4565-9164-39C4925E467B}'
        if ($value) {
            return [Environment]::ExpandEnvironmentVariables($value)
        }
    }
    return Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
}

# The folder the packages are extracted in. With -Create a missing default root is made; a
# CARAFESHARP_TESTDATA that names no folder is an error either way, as in TestData.cs.
function Get-TestDataRoot([switch]$Create) {
    if ($env:CARAFESHARP_TESTDATA) {
        if (-not (Test-Path -LiteralPath $env:CARAFESHARP_TESTDATA -PathType Container)) {
            throw "CARAFESHARP_TESTDATA names a folder that does not exist: $env:CARAFESHARP_TESTDATA"
        }
        return (Resolve-Path -LiteralPath $env:CARAFESHARP_TESTDATA).ProviderPath
    }
    $root = Join-Path (Get-TestDataDownloadsPath) 'Perftests'
    if ($Create -and -not (Test-Path -LiteralPath $root)) {
        New-Item -ItemType Directory -Path $root | Out-Null
    }
    return $root
}

function Get-TestDataPackages([string]$PackageList) {
    return @((Get-Content -LiteralPath $PackageList -Raw | ConvertFrom-Json).packages)
}

# The packages a test run reads: those of the default pass (no testCategory), plus those of
# $TestCategory; or the packages named in $Ids ('all' for every one).
function Select-TestDataPackages($Packages, [string]$TestCategory, [string[]]$Ids) {
    # pwsh -File passes a list as one comma-separated argument.
    $Ids = @($Ids | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
    if ($Ids) {
        if ($Ids -contains 'all') {
            return @($Packages)
        }
        foreach ($id in $Ids) {
            if (-not ($Packages | Where-Object { $_.id -eq $id })) {
                throw "testdata.json has no package '$id' (it has: $(($Packages | ForEach-Object { $_.id }) -join ', '))"
            }
        }
        return @($Packages | Where-Object { $Ids -contains $_.id })
    }
    return @($Packages | Where-Object { -not $_.testCategory -or ($TestCategory -and $_.testCategory -eq $TestCategory) })
}

function Get-TestDataSha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-TestDataZip($Package, [string]$ZipPath) {
    $length = (Get-Item -LiteralPath $ZipPath).Length
    if ($length -ne [long]$Package.size) {
        return "is $length bytes, not $($Package.size)"
    }
    $sha = Get-TestDataSha256 $ZipPath
    if ($sha -ne $Package.sha256) {
        return "has SHA-256 $sha, not $($Package.sha256)"
    }
    return $null
}

# Streams $Url to $Target through a .partial file, reporting progress every 10 seconds.
function Save-TestDataZip([string]$Url, [string]$Target, [long]$Size) {
    $partial = "$Target.partial"
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromHours(4)
    try {
        $response = $client.GetAsync($Url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Download failed ($([int]$response.StatusCode) $($response.ReasonPhrase)): $Url"
        }
        $source = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $file = [System.IO.File]::Create($partial)
        try {
            $buffer = [byte[]]::new(1 -shl 20)
            $written = 0L
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $lastReport = 0
            while (($read = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $file.Write($buffer, 0, $read)
                $written += $read
                if ($clock.Elapsed.TotalSeconds - $lastReport -ge 10) {
                    $lastReport = $clock.Elapsed.TotalSeconds
                    Write-Host ("    {0:N0} of {1:N0} MB, {2:N1} MB/s" -f ($written / 1MB), ($Size / 1MB), ($written / 1MB / $clock.Elapsed.TotalSeconds))
                }
            }
        } finally {
            $file.Dispose()
            $source.Dispose()
        }
    } finally {
        $client.Dispose()
    }
    Move-Item -LiteralPath $partial -Destination $Target -Force
}

# Fetches one package into $Root; returns the problem, or $null when the package is in place.
function Invoke-TestDataFetch($Package, [string]$Root) {
    $folder = Join-Path $Root $Package.folder
    if (Test-Path -LiteralPath $folder) {
        if (Test-Path -LiteralPath (Join-Path $folder 'MANIFEST.sha256')) {
            Write-Host "  $($Package.folder): present"
            return $null
        }
        return "$folder has no MANIFEST.sha256, so its extraction stopped part way: delete the folder and fetch again"
    }
    if ($Package.url -eq 'PLACEHOLDER' -or $Package.sha256 -eq 'PLACEHOLDER') {
        return "$($Package.zip) is not published yet (testdata.json has no URL for it)"
    }
    $zip = Join-Path $Root $Package.zip
    if (Test-Path -LiteralPath $zip) {
        $problem = Test-TestDataZip $Package $zip
        if ($problem) {
            Write-Host "  $($Package.zip) in $Root $problem; downloading it again" -ForegroundColor Yellow
        }
    } else {
        $problem = 'missing'
    }
    if ($problem) {
        Write-Host ("  {0}: downloading {1:N0} MB from {2}" -f $Package.folder, ($Package.size / 1MB), $Package.url)
        Save-TestDataZip $Package.url $zip ([long]$Package.size)
        $problem = Test-TestDataZip $Package $zip
        if ($problem) {
            return "the downloaded $($Package.zip) $problem"
        }
    }
    # Extracted beside the final folder and renamed into place, so a folder by the package's
    # name is always complete. ExtractToDirectory refuses an entry outside the destination.
    $staging = Join-Path $Root ('.extracting-' + $Package.folder)
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    Write-Host "  $($Package.folder): extracting into $Root"
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $staging)
    $extracted = Join-Path $staging $Package.folder
    if (-not (Test-Path -LiteralPath (Join-Path $extracted 'MANIFEST.sha256'))) {
        return "$($Package.zip) has no $($Package.folder)/MANIFEST.sha256; it is not a CarafeSharp test data package"
    }
    Move-Item -LiteralPath $extracted -Destination $folder
    Remove-Item -LiteralPath $staging -Recurse -Force
    return $null
}

# Checks one extracted package against its MANIFEST.sha256 (and its zip, when one is kept in
# $Root); returns the problems found.
function Invoke-TestDataVerify($Package, [string]$Root) {
    $problems = @()
    $folder = Join-Path $Root $Package.folder
    $zip = Join-Path $Root $Package.zip
    if (Test-Path -LiteralPath $zip) {
        $problem = Test-TestDataZip $Package $zip
        if ($problem) {
            $problems += "$zip $problem"
        }
    }
    if (-not (Test-Path -LiteralPath $folder)) {
        return $problems + "$folder is missing: fetch $($Package.zip)"
    }
    $manifest = Join-Path $folder 'MANIFEST.sha256'
    if (-not (Test-Path -LiteralPath $manifest)) {
        return $problems + "$folder has no MANIFEST.sha256, so its extraction stopped part way: delete the folder and fetch again"
    }
    $checked = 0
    $number = 0
    foreach ($line in Get-Content -LiteralPath $manifest) {
        $number++
        if ($line.Length -eq 0 -or $line.StartsWith('#')) {
            continue
        }
        $entry = [regex]::Match($line, '^([0-9a-f]{64})  (.+)$')
        if (-not $entry.Success) {
            $problems += "$manifest line $number is not '<sha256>  <path>'"
            continue
        }
        $sha = $entry.Groups[1].Value
        $relative = $entry.Groups[2].Value
        $path = Join-Path $folder ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $problems += "$($Package.folder)/$relative is missing"
        } elseif ((Get-TestDataSha256 $path) -ne $sha) {
            $problems += "$($Package.folder)/$relative does not match MANIFEST.sha256"
        }
        $checked++
    }
    if ($checked -eq 0) {
        $problems += "$manifest lists no files, so $folder is not a package extracted from its zip"
    }
    if ($problems.Count -eq 0) {
        Write-Host "  $($Package.folder): $checked files match MANIFEST.sha256"
    }
    return $problems
}

# The committed pretrained archive against the pin in PretrainedModels.cs; returns the problem or $null.
function Test-PretrainedModels([string]$CarafeRoot) {
    $zip = Join-Path $CarafeRoot 'models/alphapeptdeep-v1/pretrained_models.zip'
    $source = Join-Path $CarafeRoot 'CarafeSharp.Models/PretrainedModels.cs'
    $pin = [regex]::Match((Get-Content -LiteralPath $source -Raw), 'PINNED_SHA256 = @"([0-9a-f]{64})"')
    if (-not $pin.Success) {
        return "found no PINNED_SHA256 in $source"
    }
    if (-not (Test-Path -LiteralPath $zip)) {
        return "$zip is missing"
    }
    $sha = Get-TestDataSha256 $zip
    if ($sha -ne $pin.Groups[1].Value) {
        return "$zip has SHA-256 $sha, not the pinned $($pin.Groups[1].Value)"
    }
    Write-Host "  pretrained_models.zip: matches its pin"
    return $null
}

# The committed Chronologer weights and encoding against the version and pins in ChronologerFiles.cs;
# returns the problems found, none when both match.
function Test-ChronologerModel([string]$CarafeRoot) {
    $source = Join-Path $CarafeRoot 'CarafeSharp.Models/ChronologerFiles.cs'
    $text = Get-Content -LiteralPath $source -Raw
    $version = [regex]::Match($text, 'const string VERSION = @"(\d+)"')
    if (-not $version.Success) {
        return @("found no VERSION in $source")
    }
    $folder = Join-Path $CarafeRoot "models/chronologer-$($version.Groups[1].Value)"
    $problems = @()
    $files = @(
        @{ Name = "Chronologer_$($version.Groups[1].Value).pt"; Pin = 'PINNED_WEIGHTS_SHA256' },
        @{ Name = "Chronologer_$($version.Groups[1].Value).preprocessing.json"; Pin = 'PINNED_ENCODING_SHA256' }
    )
    foreach ($file in $files) {
        $pin = [regex]::Match($text, "$($file.Pin) = @""([0-9a-f]{64})""")
        $path = Join-Path $folder $file.Name
        if (-not $pin.Success) {
            $problems += "found no $($file.Pin) in $source"
        } elseif (-not (Test-Path -LiteralPath $path)) {
            $problems += "$path is missing"
        } elseif (($sha = Get-TestDataSha256 $path) -ne $pin.Groups[1].Value) {
            $problems += "$path has SHA-256 $sha, not the pinned $($pin.Groups[1].Value)"
        } else {
            Write-Host "  $($file.Name): matches its pin"
        }
    }
    return $problems
}
