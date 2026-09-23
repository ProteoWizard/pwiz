<#
.SYNOPSIS
End-to-end test of a built Skyline / Skyline-daily installer: silent install, deployment
and registry checks, a SkylineCmd smoke, silent uninstall, cleanup checks.

.DESCRIPTION
The successor of the Jamfile's TestSkylineInstall / TestSkylineUninstall (which drove the
WiX .msi through msiexec). Fails with a non-zero exit code and a message at the first
check that does not hold.

Refuses to run when the installer's channel is already installed for the requested scope:
an Inno install with the same AppId upgrades in place, so the test would replace and then
remove a real installation. Uninstall it first.

File associations are left out of the install by default (/TASKS=""): the .sky / .skyd /
.skyp default values are shared with whatever else registered them on the machine, and the
test would take them over for its channel and then release them on uninstall, leaving a
developer's real registration undone. Pass -WithAssociations on a machine where that does
not matter (a CI agent) to have them installed and checked as well.

.PARAMETER SetupPath
The Setup.exe to test. Default: the newest <channel>-Setup-*.exe under ..\..\bin\installer
(the NoNetRuntime variant is excluded; it is the same payload without the runtime).

.PARAMETER AllUsers
Test a per-machine install. Requires an elevated shell; without one Setup would raise a
UAC prompt, so the script refuses instead.

.PARAMETER WithAssociations
Install and verify the file associations too (see above).

.USAGE
    pwsh -File pwiz_tools/Skyline/Executables/Installer/Test-Installer.ps1
    pwsh -File pwiz_tools/Skyline/Executables/Installer/Test-Installer.ps1 -SetupPath C:\path\Skyline-daily-Setup-26.1.1.245.exe
#>
#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $SetupPath,
    [switch] $AllUsers,
    [switch] $WithAssociations
)
$ErrorActionPreference = 'Stop'

$installerDir = $PSScriptRoot
$skylineDir = (Resolve-Path (Join-Path $installerDir '..\..')).Path

# The AppIds from Setup.iss for the two channels, plus Inno's "_is1" uninstall-key suffix.
# Any other product name (build.ps1 -ProductName) is its own AppId.
$appIds = @{
    'Skyline'       = '{67DE971E-A042-4EF7-A93C-3F85D2A3D241}'
    'Skyline-daily' = '{C701F69C-B553-4E3E-90D0-5676DD615570}'
}
$uninstallRoot = 'Software\Microsoft\Windows\CurrentVersion\Uninstall'

function Fail([string] $message) {
    Write-Host "FAILED: $message" -ForegroundColor Red
    exit 1
}

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal] $identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Registry access goes through the 64-bit view explicitly: Setup.iss installs in 64-bit
# mode, so its keys live there even if this script ran from a 32-bit host.
function Open-RegistryKey([string] $path) {
    $hive = if ($AllUsers) { [Microsoft.Win32.RegistryHive]::LocalMachine } else { [Microsoft.Win32.RegistryHive]::CurrentUser }
    $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, [Microsoft.Win32.RegistryView]::Registry64)
    return $root.OpenSubKey($path)
}

function Get-RegistryValue([string] $path, [string] $name) {
    $key = Open-RegistryKey $path
    if ($null -eq $key) { return $null }
    try { return $key.GetValue($name) } finally { $key.Dispose() }
}

# ----- locate the installer, read channel + version off it -----

if (-not $SetupPath) {
    $buildDir = Join-Path $skylineDir 'bin\installer'
    $SetupPath = Get-ChildItem $buildDir -Filter 'Skyline*-Setup-*.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike '*NoNetRuntime*' } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $SetupPath) {
        Fail "No Skyline*-Setup-*.exe under $buildDir. Run build.ps1 first, or pass -SetupPath."
    }
}
$SetupPath = (Resolve-Path $SetupPath).Path

# Setup.iss stamps AppName (the product name) into ProductName and the numeric version
# into FileVersion.
$versionInfo = (Get-Item $SetupPath).VersionInfo
$appName = $versionInfo.ProductName.Trim()
$version = $versionInfo.FileVersion.Trim()
if (-not $appName) {
    Fail "$SetupPath reports no product name."
}
$appId = if ($appIds.ContainsKey($appName)) { $appIds[$appName] } else { $appName }
$uninstallKeyPath = "$uninstallRoot\${appId}_is1"
# The product's ProgIds drop the hyphen (Skyline-daily -> SkylineDaily.Document.1, any other
# product name loses its hyphens): a ProgId allows no punctuation but periods.
$progId = $(if ($appName -eq 'Skyline-daily') { 'SkylineDaily' } else { $appName -replace '-', '' }) + '.Document.1'
$scope = if ($AllUsers) { 'per-machine' } else { 'per-user' }
Write-Host "==> $appName $version, ${scope}: $SetupPath" -ForegroundColor Cyan

if ($AllUsers -and -not (Test-Elevated)) {
    Fail "-AllUsers needs an elevated shell (Setup would otherwise raise a UAC prompt)."
}
if ($null -ne (Get-RegistryValue $uninstallKeyPath 'InstallLocation')) {
    Fail ("$appName is already installed $scope at " + (Get-RegistryValue $uninstallKeyPath 'InstallLocation') +
          "; this test would upgrade it in place and then remove it. Uninstall it first.")
}

# ----- install -----

$tasks = if ($WithAssociations) { 'associate' } else { '' }
$log = Join-Path ([System.IO.Path]::GetTempPath()) "$appName-$version-install.log"
$installArgs = @(
    $(if ($AllUsers) { '/ALLUSERS' } else { '/CURRENTUSER' }),
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/TASKS=`"$tasks`"", "/LOG=`"$log`""
)
Write-Host "==> install: $($installArgs -join ' ')"
$proc = Start-Process -FilePath $SetupPath -ArgumentList $installArgs -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Fail "Setup exited with $($proc.ExitCode); see $log"
}

$failure = $null
try {
    # ----- deployment + registry -----
    $installDir = Get-RegistryValue $uninstallKeyPath 'InstallLocation'
    if (-not $installDir) { throw "Setup succeeded but the uninstall key $uninstallKeyPath has no InstallLocation." }
    $installDir = $installDir.TrimEnd('\')
    Write-Host "    installed to $installDir"

    $expectedDir = if ($AllUsers) { Join-Path $env:ProgramFiles $appName } else { Join-Path $env:LOCALAPPDATA "Programs\$appName" }
    if ($installDir -ne $expectedDir) { throw "Installed to $installDir, expected $expectedDir." }
    if ((Get-RegistryValue $uninstallKeyPath 'DisplayVersion') -ne $version) {
        throw "DisplayVersion is '$(Get-RegistryValue $uninstallKeyPath 'DisplayVersion')', expected $version."
    }

    # The channel exe is what runs, whatever the product is called.
    $channelExe = @('Skyline-daily.exe', 'Skyline.exe') | Where-Object { Test-Path (Join-Path $installDir $_) } | Select-Object -First 1
    if (-not $channelExe) { throw "Neither Skyline-daily.exe nor Skyline.exe is in the install." }
    $channelDll = [System.IO.Path]::ChangeExtension($channelExe, '.dll')
    foreach ($file in @($channelDll, 'SkylineCmd.exe', 'msconvert.exe', 'BlibBuild.exe', 'SkylineDoc.ico')) {
        if (-not (Test-Path (Join-Path $installDir $file))) { throw "Required file missing from the install: $file" }
    }

    $recordedDir = Get-RegistryValue "Software\MacCossLabUW\$appName" 'InstallDir'
    if (($recordedDir ?? '').TrimEnd('\') -ne $installDir) {
        throw "Software\MacCossLabUW\$appName\InstallDir is '$recordedDir', expected $installDir."
    }
    if ((Get-RegistryValue "Software\MacCossLabUW\$appName" 'Version') -ne $version) {
        throw "Software\MacCossLabUW\$appName\Version is not $version."
    }

    $programs = if ($AllUsers) { [Environment]::GetFolderPath('CommonPrograms') } else { [Environment]::GetFolderPath('Programs') }
    $shortcut = Join-Path $programs "MacCoss Lab, UW\$appName.lnk"
    if (-not (Test-Path $shortcut)) { throw "Start Menu shortcut missing: $shortcut" }

    if ($WithAssociations) {
        if ((Get-RegistryValue 'Software\Classes\.sky' '') -ne $progId) {
            throw ".sky is not associated with $progId."
        }
        $command = Get-RegistryValue "Software\Classes\$progId\shell\open\command" ''
        if ($command -ne "`"$installDir\$channelExe`" --opendoc `"%1`"") {
            throw "$progId open command is '$command'."
        }
    }

    # ----- smoke: the installed SkylineCmd runs on the installed runtime and reports its version -----
    $cmd = Join-Path $installDir 'SkylineCmd.exe'
    $output = & $cmd --version 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "SkylineCmd --version exited with ${LASTEXITCODE}: $output" }
    if ($output -notlike "*$version*") { throw "SkylineCmd --version did not report ${version}: $output" }
    Write-Host "    SkylineCmd: $($output.Trim())"
}
catch {
    $failure = $_.Exception.Message
}

# ----- uninstall (always, so a failed check does not leave the install behind) -----

$uninstallString = Get-RegistryValue $uninstallKeyPath 'UninstallString'
if ($uninstallString) {
    Write-Host "==> uninstall"
    $proc = Start-Process -FilePath $uninstallString.Trim('"') -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru
    # Inno's uninstaller re-launches a copy of itself from %TEMP% and the parent exits at
    # once, so the uninstall key going away is the completion signal.
    $deadline = (Get-Date).AddMinutes(3)
    while ((Get-Date) -lt $deadline -and $null -ne (Get-RegistryValue $uninstallKeyPath 'UninstallString')) {
        Start-Sleep -Milliseconds 500
    }
}

if ($failure) { Fail $failure }

if ($null -ne (Get-RegistryValue $uninstallKeyPath 'UninstallString')) { Fail "The uninstall key is still present after uninstall." }
if (Test-Path $installDir) { Fail "The install directory is still present after uninstall: $installDir" }
if ($null -ne (Open-RegistryKey "Software\MacCossLabUW\$appName")) { Fail "Software\MacCossLabUW\$appName is still present after uninstall." }
if (Test-Path $shortcut) { Fail "The Start Menu shortcut is still present after uninstall: $shortcut" }
if ($WithAssociations -and (Get-RegistryValue 'Software\Classes\.sky' '') -eq $progId) {
    Fail ".sky is still associated with $progId after uninstall."
}
if ($WithAssociations -and $null -ne (Open-RegistryKey "Software\Classes\$progId")) {
    Fail "$progId is still registered after uninstall."
}

Write-Host "PASSED: $appName $version $scope install, smoke and uninstall" -ForegroundColor Green
exit 0
