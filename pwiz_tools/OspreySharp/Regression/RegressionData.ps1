<#
.SYNOPSIS
    Self-contained (pwiz-standalone) test-data acquisition for the OspreySharp
    overnight regression: download a panorama zip into the shared perf-test
    downloads folder, extract it, and skip the download when the extracted tree
    is already present (TestPerf semantics).

.DESCRIPTION
    Dot-source this file to get Get-RegressionData. It mirrors the mechanism
    Skyline perf tests use (pwiz_tools/Skyline/TestUtil/AbstractUnitTest.cs:
    DownloadZipFile + GetTargetZipFilePath, ExtensionTestContext.ExtractTestFiles
    with DoNotOverwrite), re-homed into pwiz with NO ai/ dependency:

      * Downloads folder is PathEx.GetDownloadsPath()'s rule -- the
        SKYLINE_DOWNLOAD_PATH override first, else the Windows known Downloads
        folder (honours a relocated Downloads, e.g. on D:), so the osprey data
        lands beside the existing Skyline perf-test datasets.
      * The URL's second-to-last segment, capitalized, is the local subfolder
        (".../perftests/osprey-testfiles-mzML.zip" -> "<Downloads>\Perftests").
      * The zip carries a top-level "<name>\" dir; it extracts INTO the
        Perftests folder, yielding "<Downloads>\Perftests\osprey-testfiles-mzML\".
      * Skip-if-present: if that extracted root already exists (and -Force is
        not set), the download + extract are skipped entirely. CI agents start
        clean and download every night; developer machines reuse the copy.
#>

$ErrorActionPreference = 'Stop'

function Get-WindowsDownloadsFolder {
    <#
    The user's Downloads folder, honoring relocation. Mirrors
    PathEx.GetDownloadsPath: SKYLINE_DOWNLOAD_PATH env override first, then the
    {374DE290-...} known-folder registry value, then UserProfile\Downloads.
    #>
    if ($env:SKYLINE_DOWNLOAD_PATH) { return $env:SKYLINE_DOWNLOAD_PATH }
    $reg = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders'
    $guidName = '{374DE290-123F-4565-9164-39C4925E467B}'
    try {
        $val = (Get-ItemProperty -Path $reg -Name $guidName -ErrorAction Stop).$guidName
        if ($val) { return [Environment]::ExpandEnvironmentVariables($val) }
    } catch { }
    return (Join-Path $env:USERPROFILE 'Downloads')
}

function Get-TargetZipPath {
    <#
    Replicate Skyline's GetTargetZipFilePath: local subfolder = capitalized
    second-to-last URL segment; returns the target folder, the local zip path,
    and the extracted-root path (target\<zip base name>).
    #>
    param([string]$Url, [string]$DownloadsPath)

    $downloads = if ($DownloadsPath) { $DownloadsPath } else { Get-WindowsDownloadsFolder }
    $segments = $Url.Split('/')
    $urlFolder = $segments[$segments.Length - 2]
    $localFolder = [char]::ToUpper($urlFolder[0]) + $urlFolder.Substring(1)
    $targetFolder = Join-Path $downloads $localFolder
    $fileName = $segments[$segments.Length - 1]
    $zipPath = Join-Path $targetFolder $fileName
    $extractedRoot = Join-Path $targetFolder ([System.IO.Path]::GetFileNameWithoutExtension($fileName))
    return [pscustomobject]@{
        TargetFolder = $targetFolder; ZipPath = $zipPath; ExtractedRoot = $extractedRoot
    }
}

function Save-UrlToFile {
    <#
    Stream a URL to a file via HttpClient (reliable for large zips; reports
    size on completion). Throws on a non-success status.
    #>
    param([string]$Url, [string]$OutFile)

    Add-Type -AssemblyName System.Net.Http
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromMinutes(30)
    try {
        $resp = $client.GetAsync($Url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).
            GetAwaiter().GetResult()
        if (-not $resp.IsSuccessStatusCode) {
            throw "Download failed ($([int]$resp.StatusCode) $($resp.ReasonPhrase)): $Url"
        }
        $src = $resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $dst = [System.IO.File]::Create($OutFile)
        try {
            $src.CopyTo($dst)
        } finally {
            $dst.Dispose(); $src.Dispose()
        }
    } finally {
        $client.Dispose()
    }
}

function Expand-ZipNoOverwrite {
    <#
    Extract a zip into a destination, leaving any already-present file untouched
    (DoNotOverwrite). New files are written; existing ones are skipped. This is
    what makes a partially-staged tree safe to re-extract.
    #>
    param([string]$ZipPath, [string]$DestFolder)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # Canonical destination root for the zip-slip guard below.
    $destRoot = [System.IO.Path]::GetFullPath($DestFolder).TrimEnd('\', '/')
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $zip.Entries) {
            $destPath = Join-Path $DestFolder $entry.FullName
            # Zip-slip guard: reject any entry whose resolved path escapes the
            # destination root (e.g. a "../" or absolute-path entry), even though
            # the zip comes from a trusted host -- defense in depth.
            $full = [System.IO.Path]::GetFullPath($destPath)
            if ($full -ne $destRoot -and
                -not $full.StartsWith($destRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Zip entry escapes destination (path traversal): $($entry.FullName)"
            }
            if ($entry.FullName.EndsWith('/')) {
                if (-not (Test-Path $destPath)) { New-Item -ItemType Directory -Path $destPath -Force | Out-Null }
                continue
            }
            $dir = Split-Path -Parent $destPath
            if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            if (-not (Test-Path $destPath)) {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $false)
            }
        }
    } finally {
        $zip.Dispose()
    }
}

function Get-RegressionData {
    <#
    Ensure the regression data zip is downloaded + extracted, skipping when the
    extracted tree is already present. Returns the extracted-root path (which
    contains the per-dataset subfolders, e.g. stellar\ and astral\).

    -Url           the panorama zip URL
    -DownloadsPath override the downloads folder (default: Windows Downloads)
    -Force         re-download + re-extract even if the tree is present
    -Log           a scriptblock { param($msg) } for progress (default Write-Host)
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [string]$DownloadsPath,
        [switch]$Force,
        [scriptblock]$Log
    )
    if (-not $Log) { $Log = { param($m) Write-Host "  $m" -ForegroundColor Gray } }

    $t = Get-TargetZipPath -Url $Url -DownloadsPath $DownloadsPath

    if ((Test-Path $t.ExtractedRoot) -and -not $Force) {
        & $Log ("data present, skipping download: {0}" -f $t.ExtractedRoot)
        return $t.ExtractedRoot
    }

    New-Item -ItemType Directory -Path $t.TargetFolder -Force | Out-Null
    if ($Force -or -not (Test-Path $t.ZipPath)) {
        & $Log ("downloading {0}" -f $Url)
        $sw = [Diagnostics.Stopwatch]::StartNew()
        Save-UrlToFile -Url $Url -OutFile $t.ZipPath
        $sw.Stop()
        & $Log ("downloaded {0:N1} MB in {1:N0}s" -f ((Get-Item $t.ZipPath).Length / 1MB), $sw.Elapsed.TotalSeconds)
    } else {
        & $Log ("zip present, re-extracting: {0}" -f $t.ZipPath)
    }

    & $Log ("extracting into {0}" -f $t.TargetFolder)
    Expand-ZipNoOverwrite -ZipPath $t.ZipPath -DestFolder $t.TargetFolder
    if (-not (Test-Path $t.ExtractedRoot)) {
        throw "Extraction did not produce expected root: $($t.ExtractedRoot)"
    }
    & $Log ("data ready: {0}" -f $t.ExtractedRoot)
    return $t.ExtractedRoot
}
