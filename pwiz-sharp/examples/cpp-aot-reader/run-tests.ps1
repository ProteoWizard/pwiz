# Wraps `ctest` and re-emits results as TeamCity service messages so per-test
# pass/fail status surfaces in TC's "Tests" tab when this script runs as a TC
# build step. When run outside TC the service messages are harmless plain stdout
# lines (TC's parser is the only consumer; nothing else cares).
#
# Usage:
#   .\run-tests.ps1                    # default config: Release, build dir = ./build
#   .\run-tests.ps1 -Config Debug      # use a different cmake config
#   .\run-tests.ps1 -BuildDir mybuild  # use a different cmake build dir

[CmdletBinding()]
param(
    [string] $Config   = 'Release',
    [string] $BuildDir = (Join-Path $PSScriptRoot 'build')
)

$ErrorActionPreference = 'Stop'

# Resolve cmake / ctest. Either on PATH already, or shipped with VS (the same
# location publish docs use).
function Resolve-Tool([string] $name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $vsBundled = Join-Path 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin' "$name.exe"
    if (Test-Path $vsBundled) { return $vsBundled }
    throw "$name not found on PATH or in the Visual Studio 18 CMake bundle"
}

$ctest = Resolve-Tool 'ctest'

if (-not (Test-Path $BuildDir)) {
    throw "Build dir not found: $BuildDir. Run cmake -S . -B $BuildDir first."
}

# CTest --output-junit lands the per-test results in JUnit format, which TC's
# importData service message knows how to consume.
$junitPath = Join-Path $BuildDir 'ctest-results.xml'
if (Test-Path $junitPath) { Remove-Item $junitPath }

# CTest exit code: non-zero if any test failed. We don't fail this script on it
# — the importData service message reports per-test status to TC, and TC marks
# the build red based on that, not on our exit code. But we still propagate the
# code so command-line consumers see the same signal.
Write-Host "##teamcity[blockOpened name='cpp_aot_reader CTest']"
try {
    & $ctest `
        --test-dir $BuildDir `
        --build-config $Config `
        --output-on-failure `
        --output-junit $junitPath
    $ctestExit = $LASTEXITCODE
}
finally {
    Write-Host "##teamcity[blockClosed name='cpp_aot_reader CTest']"
}

if (Test-Path $junitPath) {
    # Quote the path for service-message escaping — TC's parser handles single-
    # quoted attribute values with backslash + the standard service-message escapes.
    $escaped = $junitPath.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace(']', '|]')
    Write-Host "##teamcity[importData type='junit' path='$escaped' verbose='true']"
} else {
    Write-Host "##teamcity[message text='ctest did not produce $junitPath; no per-test results imported' status='WARNING']"
}

exit $ctestExit
