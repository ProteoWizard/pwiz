$sharp = "C:\dev\pwiz-msconvert-pr\pwiz-sharp\Tools\MsBenchmark\Tools\MsBenchmark\Tools\MsBenchmark\Tools\Commandline\MsBenchmark\Tools\Commandline\MsBenchmark\src\bin\Release\net8.0\msbenchmark-sharp.exe"
$cpp = "C:\dev\pwiz\build-nt-x86\msvc-release-x86_64\msbenchmark.exe"
$file = "D:\test\diaumpire-real\MRC5-input.mzML"

function Run($label, $exe, $argList) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    foreach ($a in $argList) { $psi.ArgumentList.Add($a) }
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.UseShellExecute = $false
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = [System.Diagnostics.Process]::Start($psi)
    $peakPM = 0
    while (-not $proc.HasExited) {
        $proc.Refresh()
        if ($proc.PrivateMemorySize64 -gt $peakPM) { $peakPM = $proc.PrivateMemorySize64 }
        if ($proc.PeakPagedMemorySize64 -gt $peakPM) { $peakPM = $proc.PeakPagedMemorySize64 }
        Start-Sleep -Milliseconds 5
    }
    $proc.Refresh()
    if ($proc.PeakPagedMemorySize64 -gt $peakPM) { $peakPM = $proc.PeakPagedMemorySize64 }
    $proc.StandardOutput.ReadToEnd() | Out-Null
    $proc.StandardError.ReadToEnd() | Out-Null
    $sw.Stop()
    Write-Host ("{0,-45} Wall: {1,7:N2}s   PeakPM: {2,5:N0}MiB" -f $label, $sw.Elapsed.TotalSeconds, ($peakPM/1MB))
}

Write-Host "Input: $file ($(((Get-Item $file).Length/1MB).ToString('N0')) MiB)"
Write-Host ""

Write-Host "== 1 spectrum (filter ""index [45000,45000]"") =="
Run "sharp full-data            " $sharp @("spectra", "full-data",        $file, "--filter", "index [45000,45000]")
Run "cpp   full-data            " $cpp   @("spectra", "full-data",        $file, "--filter", "index [45000,45000]")
Run "sharp full-metadata        " $sharp @("spectra", "full-metadata",    $file, "--filter", "index [45000,45000]")
Run "cpp   full-metadata        " $cpp   @("spectra", "full-metadata",    $file, "--filter", "index [45000,45000]")
Run "sharp instant-metadata     " $sharp @("spectra", "instant-metadata", $file, "--filter", "index [45000,45000]")
Run "cpp   instant-metadata     " $cpp   @("spectra", "instant-metadata", $file, "--filter", "index [45000,45000]")
Write-Host ""

Write-Host "== 100 spectra (filter ""index [45000,45099]"") =="
Run "sharp full-data            " $sharp @("spectra", "full-data",        $file, "--filter", "index [45000,45099]")
Run "cpp   full-data            " $cpp   @("spectra", "full-data",        $file, "--filter", "index [45000,45099]")
Run "sharp full-metadata        " $sharp @("spectra", "full-metadata",    $file, "--filter", "index [45000,45099]")
Run "cpp   full-metadata        " $cpp   @("spectra", "full-metadata",    $file, "--filter", "index [45000,45099]")
Run "sharp instant-metadata     " $sharp @("spectra", "instant-metadata", $file, "--filter", "index [45000,45099]")
Run "cpp   instant-metadata     " $cpp   @("spectra", "instant-metadata", $file, "--filter", "index [45000,45099]")
Write-Host ""

Write-Host "== Full enumeration (90,460 spectra) =="
Run "sharp full-data            " $sharp @("spectra", "full-data",        $file)
Run "cpp   full-data            " $cpp   @("spectra", "full-data",        $file)
Run "sharp full-metadata        " $sharp @("spectra", "full-metadata",    $file)
Run "cpp   full-metadata        " $cpp   @("spectra", "full-metadata",    $file)
Run "sharp instant-metadata     " $sharp @("spectra", "instant-metadata", $file)
Run "cpp   instant-metadata     " $cpp   @("spectra", "instant-metadata", $file)
