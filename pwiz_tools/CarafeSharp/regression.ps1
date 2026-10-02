<#
.SYNOPSIS
    CarafeSharp's golden regression: fine-tune on a packaged Osprey training export, predict
    the final library, and compare the run with the golden in regression.data.

.DESCRIPTION
    The isolated leg. It depends only on CarafeSharp and the test data packages:
      1. Builds CarafeSharp with build.ps1 (unless -NoBuild).
      2. Finds the inputs in the test data packages (testdata.json; the root is
         CARAFESHARP_TESTDATA, else <Downloads>/Perftests): the training export from the
         'export' package, and the library FASTA and pairing manifest from 'testfiles'.
      3. Writes a subset of the library: the pair groups of the pairing manifest whose
         peptide_pair_index is a multiple of 50, each with all its members (target, p_target,
         decoy, p_decoy). The subset manifest is exactly those groups' rows and the subset
         FASTA those peptides' records, so a CPU run predicts about 20,000 precursors instead
         of a million, and its library has a DecoyPairs table as a full library does.
      4. Runs CarafeSharp once: -tf all fine-tunes the RT and MS2 models on the export and
         predicts the library from the subset, with the library arguments of the CarafeSharp
         workflow's stage 4-5. The library is not rebuilt from the model folder with
         -model_dir, because meta.json carries Carafe's default lf_frag_mz_max of 1800.
      5. Runs the comparator, RegressionTest in CarafeSharp.Test (TestCategory Regression),
         through build.ps1 on the run folder.

    The comparator's checks are in docs/04-testing.md ("Regression"). The calibrated
    tolerances decide pass or fail on every machine and device. The exact hashes (training
    tables, model weights, library content) are compared and reported as SAME or DIFFERS,
    but never fail a run.

    Each run gets its own folder under -WorkDir, holding the CarafeSharp output (out/), its
    log, the subset inputs, regression-run.json (what was run, on what) and the comparator's
    regression-report.txt.

    -Leg Chained instead checks that the two tools work together, with no test data:
      1. Builds CarafeSharp and Osprey (unless -NoBuild or -OspreyExe).
      2. Extracts Osprey's committed Stellar subset (Osprey.Test/TestData/StellarSubset.zip:
         one isolation window of the three Stellar runs, 7 minutes, and a 358-precursor
         library) and writes a peptide FASTA of the library's peptides.
      3. Runs Osprey on it with --training-export.
      4. Runs CarafeSharp on the exports, -tf all with the same library arguments as the
         isolated leg, predicting a library from the peptide FASTA.
      5. Runs the comparator's chained checks: an export per run with the precursors a 1% run
         FDR keeps and the per-run second pass's q-values, non-empty training tables, both
         models and finite metrics, each run's isolation window in meta.json, and a library.
    It has no golden: the fine-tune on one isolation window says little about training, which
    the isolated leg gates.

.PARAMETER Dataset
    Stellar (default) or Astral. Each dataset is an entry of $datasets below and a folder of
    regression.data. Astral needs the Astral packages (carafesharp-testfiles-astral-v1 and
    carafesharp-export-astral-v1); its export has about four times Stellar's precursors, so a
    run is best made with -Torch cuda. Its golden, like Stellar's, comes from the CPU. -Leg Chained
    runs on Stellar only.

.PARAMETER Torch
    cpu (default) or cuda: the libtorch build to use and the device CarafeSharp runs on.
    A cuda run that falls back to the CPU is reported, and -CreateGolden refuses it.

.PARAMETER NoBuild
    Use the existing build.

.PARAMETER CreateGolden
    Make this run the dataset's golden, in regression.data/<dataset>. It refuses a working
    tree with changes (the golden records the commit it came from), a cuda run that fell
    back to the CPU, a run with -ExtraArgs, a fine-tuned model that does not beat the
    pretrained one on all four MS2 metrics, and a library whose DecoyPairs table leaves a
    target unpaired although its decoy was written. With a golden already there, it shows
    the differences and replaces it only with -Force.

.PARAMETER Force
    With -CreateGolden, replace an existing golden.

.PARAMETER ExtraArgs
    CarafeSharp arguments added to the run, for sensitivity checks: an option given here
    replaces the default's value (CarafeSharp takes the first occurrence of an option, so
    they are merged, not appended). Items are split at whitespace, so from pwsh -File write
    -ExtraArgs "-cor 0.7"; from PowerShell, -ExtraArgs '-cor','0.7' works too.


.PARAMETER CompareRun
    Compare an existing run folder instead of running CarafeSharp, for example after
    changing the comparator. With -CreateGolden the folder must come from a clean tree and
    have no -ExtraArgs.

.PARAMETER WorkDir
    Where the run folders go. Default CARAFESHARP_REGRESSION_WORKDIR, else
    TestResults/regression beside this script.

.PARAMETER RunName
    A label added to the run folder's name, such as the sensitivity check it makes.

.PARAMETER CarafeSharpExe
    Run this executable instead of this checkout's build (a snapshot of the build output, so
    the checkout can be rebuilt during a long run). Not allowed with -CreateGolden.

.PARAMETER Export
    Fine-tune on this training export instead of the packaged one, for example one Osprey has just
    written from the .raw on another platform. Its folder must hold no other training export. The
    comparator reports the export's SHA-256 against the golden's as information and gates on the
    calibrated tolerances: Osprey's scores differ in the last digit between platforms, so another
    platform's export is never byte-identical. Not allowed with -CreateGolden.
.PARAMETER Leg
    Isolated (default): fine-tune on the packaged export and compare with the golden.
    Chained: Osprey's export of the Stellar subset feeding CarafeSharp (see the description).

.PARAMETER OspreyExe
    With -Leg Chained, run this Osprey executable instead of this checkout's build.
.PARAMETER Preflight
    Find the inputs and write the subset, print the CarafeSharp command, and stop.

.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/regression.ps1                       # CPU run against the golden
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/regression.ps1 -NoBuild -ExtraArgs "-lf_top_n_frag 19" -RunName top19
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/regression.ps1 -CreateGolden         # from a clean tree
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/regression.ps1 -Leg Chained          # Osprey's export feeding CarafeSharp
#>
#requires -Version 7
[CmdletBinding()]
param(
    [ValidateSet('Stellar', 'Astral')] [string]$Dataset = 'Stellar',
    [ValidateSet('cpu', 'cuda')] [string]$Torch = 'cpu',
    [switch]$NoBuild,
    [switch]$CreateGolden,
    [switch]$Force,
    [string[]]$ExtraArgs = @(),
    [string]$CompareRun,
    [string]$WorkDir,
    [string]$RunName,
    [string]$CarafeSharpExe,
    [string]$Export,
    [ValidateSet('Isolated', 'Chained')] [string]$Leg = 'Isolated',
    [string]$OspreyExe,
    [switch]$Preflight
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# pwsh -File passes -ExtraArgs "-cor 0.7" as one string, and cannot pass a value that starts
# with '-' on its own, so each item is split at whitespace.
$ExtraArgs = @($ExtraArgs | ForEach-Object { $_ -split '\s+' } | Where-Object { $_ })

$scriptRoot = Split-Path -Parent $PSCommandPath
$buildScript = Join-Path $scriptRoot 'build.ps1'
$goldenRoot = Join-Path $scriptRoot 'regression.data'

# The subset keeps the pair groups whose peptide_pair_index is a multiple of this, as
# LibraryParityTest.SUBSET_STRIDE keeps every 50th record, but whole groups, so the library
# still pairs its targets and decoys.
$subsetModulus = 50

# The datasets. Package paths are '/'-separated, relative to the package's top folder.
$datasets = @{
    Stellar = @{
        Folder        = 'stellar'
        Export        = @{ Package = 'export'; Path = 'stellar/Ste-2024-12-02_HeLa_4mz_sDIA_400-900_21.training.parquet' }
        LibraryFasta  = @{ Package = 'testfiles'; Path = 'stellar/carafe-osprey-entrapment/osprey_library_db_peptides.fasta' }
        Pairing       = @{ Package = 'testfiles'; Path = 'stellar/carafe-osprey-entrapment/osprey_library_db_pairing.tsv' }
        # -ms names the run the export belongs to; CarafeSharp keys the export by its stem and
        # never opens the run itself.
        RunFile       = 'Ste-2024-12-02_HeLa_4mz_sDIA_400-900_21.raw'
        Itol          = '0.4'
        ItolUnit      = 'Da'
        MinPeptideMz  = '400'
        MaxPeptideMz  = '900'
        ExportNote    = 'carafesharp-export-v1: the format 2 export Osprey #4708 (a5d15e6a4f, vendor reader) wrote ' +
                        'from the Stellar _21 .raw.'
        # -Leg Chained: Osprey's committed subset, relative to pwiz_tools/Osprey.
        Subset        = 'Osprey.Test/TestData/StellarSubset.zip'
        SubsetLibrary = 'stellar-subset-library.tsv'
    }
    Astral = @{
        Folder        = 'astral'
        Export        = @{ Package = 'astral-export'; Path = 'astral/Ast-2024-12-05_HeLa_3mzDIA_6mIIT_400-900_55.training.parquet' }
        LibraryFasta  = @{ Package = 'astral'; Path = 'astral/Carafe-Osprey-entrapment/osprey_library_db_peptides.fasta' }
        Pairing       = @{ Package = 'astral'; Path = 'astral/Carafe-Osprey-entrapment/osprey_library_db_pairing.tsv' }
        RunFile       = 'Ast-2024-12-05_HeLa_3mzDIA_6mIIT_400-900_55.raw'
        Itol          = '20'
        ItolUnit      = 'ppm'
        MinPeptideMz  = '400'
        MaxPeptideMz  = '900'
        ExportNote    = 'carafesharp-export-astral-v1: the format 2 export Osprey #4708 (a5d15e6a4f, vendor reader) wrote ' +
                        'from the Astral _55 .raw.'
    }
}
$config = $datasets[$Dataset]
$device = if ($Torch -eq 'cuda') { 'gpu' } else { 'cpu' }

# The library arguments of the CarafeSharp workflow's stage 4-5 (ai/scripts/CarafeSharp/
# Run-CarafeSharpWorkflow.ps1), with this dataset's tolerances and m/z window. CarafeSharp
# reports the XIC options (-itol, -rf, ...) as ignored: Osprey's export decides them.
function Get-LibraryArguments {
    return @(
        '-fdr', '0.01', '-itol', $config.Itol, '-itolu', $config.ItolUnit,
        '-rf', '-rf_rt_win', 'auto', '-cor', '0.8', '-min_mz', '200',
        '-n_ion_min', '2', '-c_ion_min', '2', '-mode', 'general', '-device', $device,
        '-enzyme', 'NoCut', '-miss_c', '1', '-fixMod', '1', '-varMod', '0', '-maxVar', '1', '-clip_n_m',
        '-minLength', '7', '-maxLength', '35',
        '-min_pep_mz', $config.MinPeptideMz, '-max_pep_mz', $config.MaxPeptideMz,
        '-min_pep_charge', '2', '-max_pep_charge', '3',
        '-lf_frag_mz_min', '200', '-lf_frag_mz_max', '1960', '-lf_top_n_frag', '20',
        '-lf_min_n_frag', '2', '-lf_frag_n_min', '2', '-lf_type', 'blib',
        '-se', 'Osprey', '-decoy_prefix', 'decoy_', '-nm', '-nf', '4', '-min_n', '4',
        '-valid', '-na', '0', '-fast')
}

# -Leg Chained's second library, from the saved model with -model: the arguments of
# Get-LibraryArguments that shape a library, so its spectra match the library training predicted.
function Get-SavedModelArguments {
    return @(
        '-device', $device, '-enzyme', 'NoCut', '-miss_c', '1', '-fixMod', '1', '-varMod', '0', '-maxVar', '1', '-clip_n_m',
        '-minLength', '7', '-maxLength', '35', '-min_pep_mz', $config.MinPeptideMz, '-max_pep_mz', $config.MaxPeptideMz,
        '-min_pep_charge', '2', '-max_pep_charge', '3',
        '-lf_frag_mz_min', '200', '-lf_frag_mz_max', '1960', '-lf_top_n_frag', '20',
        '-lf_min_n_frag', '2', '-lf_frag_n_min', '2', '-lf_type', 'blib', '-decoy_prefix', 'decoy_', '-fast')
}

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# Test data packages (the rules of CarafeSharp.Test/TestData.cs, in scripts/TestData.ps1)
# ---------------------------------------------------------------------------
. (Join-Path $scriptRoot 'scripts/TestData.ps1')

function Resolve-PackageFile([hashtable]$Entry) {
    $package = $packages | Where-Object { $_.id -eq $Entry.Package }
    if (-not $package) {
        throw "testdata.json lists no package '$($Entry.Package)'"
    }
    $folder = Join-Path $testDataRoot $package.folder
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) {
        throw "Missing test data: run build.ps1 -TestData Fetch, or extract $($package.zip) into $testDataRoot (or set CARAFESHARP_TESTDATA)."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $folder 'MANIFEST.sha256'))) {
        throw "$folder has no MANIFEST.sha256, so it is not a complete copy of $($package.zip). Delete the folder and extract the zip again."
    }
    $path = Join-Path $folder ($Entry.Path -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The test data package $folder has no $($Entry.Path)"
    }
    return [PSCustomObject]@{ Path = $path; Relative = "$($package.folder)/$($Entry.Path)" }
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# The pair groups the subset keeps: the manifest's header and the rows whose peptide_pair_index
# is a multiple of $subsetModulus. Returns the kept rows' sequences and the counts.
function Write-SubsetPairing([string]$Source, [string]$Target) {
    $sequences = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $groups = [System.Collections.Generic.HashSet[int]]::new()
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $reader = [IO.StreamReader]::new($Source, $utf8, $false, 1 -shl 16)
    $writer = [IO.StreamWriter]::new($Target, $false, $utf8)
    try {
        $headerLine = $reader.ReadLine()
        $names = @($headerLine -split "`t" | ForEach-Object { $_.Trim().ToLowerInvariant() })
        $sequenceColumn = [Array]::IndexOf($names, 'sequence')
        $pairColumn = [Array]::IndexOf($names, 'peptide_pair_index')
        if ($sequenceColumn -lt 0 -or $pairColumn -lt 0) {
            throw "$Source has no 'sequence' or 'peptide_pair_index' column"
        }
        $writer.Write("$headerLine`n")
        $rows = 0
        $invariant = [Globalization.CultureInfo]::InvariantCulture
        while ($null -ne ($line = $reader.ReadLine())) {
            if ($line.Length -eq 0) {
                continue
            }
            $cells = $line.Split("`t")
            $pairIndex = 0
            if (-not [int]::TryParse($cells[$pairColumn].Trim(), [Globalization.NumberStyles]::AllowLeadingSign, $invariant, [ref]$pairIndex)) {
                throw "$Source has a non-integer peptide_pair_index: $line"
            }
            if ($pairIndex % $subsetModulus -eq 0) {
                $writer.Write("$line`n")
                [void]$sequences.Add($cells[$sequenceColumn].Trim())
                [void]$groups.Add($pairIndex)
                $rows++
            }
        }
    } finally {
        $reader.Dispose()
        $writer.Dispose()
    }
    return [PSCustomObject]@{ Sequences = $sequences; Groups = $groups.Count; Rows = $rows }
}

# The FASTA records of the kept groups' peptides, written as '>' + header + '\n' + sequence +
# '\n' as LibraryParityTest writes its subset (FastaReader's header is the trimmed line after
# '>', its sequence every non-whitespace character up to the next '>'). Returns the count.
function Write-SubsetFasta([string]$Source, [string]$Target, $Sequences) {
    $whitespace = [char[]]@(' ', "`t", "`f", "`v")
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $reader = [IO.StreamReader]::new($Source, $utf8, $false, 1 -shl 16)
    $writer = [IO.StreamWriter]::new($Target, $false, $utf8)
    $records = 0
    try {
        $header = $null
        $sequence = [System.Text.StringBuilder]::new()
        $flush = {
            if ($null -ne $header) {
                $text = $sequence.ToString()
                if ($Sequences.Contains($text)) {
                    $writer.Write(">$header`n$text`n")
                    $records++
                }
            }
        }
        while ($null -ne ($line = $reader.ReadLine())) {
            $trimmed = $line.Trim()
            if ($trimmed.StartsWith('>')) {
                . $flush
                $header = $trimmed.Substring(1)
                [void]$sequence.Clear()
            } elseif ($trimmed.IndexOfAny($whitespace) -lt 0) {
                [void]$sequence.Append($trimmed)
            } else {
                [void]$sequence.Append(($trimmed -replace '\s', ''))
            }
        }
        . $flush
    } finally {
        $reader.Dispose()
        $writer.Dispose()
    }
    return $records
}

# Splits arguments into options, each with the value that follows it when the next token is
# not itself an option.
# -Leg Chained: a peptide FASTA of a DIA-NN library's peptides, one record each (the library
# step digests it with -enzyme NoCut, as the workflow's peptide FASTA). Returns the record count.
function Write-PeptideFasta([string]$Library, [string]$Target) {
    $peptides = [ordered]@{}
    foreach ($row in Import-Csv -LiteralPath $Library -Delimiter "`t") {
        if ($row.Decoy -ne '1' -and -not $peptides.Contains($row.StrippedPeptide)) {
            $peptides[$row.StrippedPeptide] = $row.ProteinID
        }
    }
    $builder = [System.Text.StringBuilder]::new()
    $index = 0
    foreach ($peptide in $peptides.Keys) {
        $index++
        # One accession per record: the subset's synthetic protein and the peptide's number.
        $accession = ($peptides[$peptide] -split '\|')[1]
        [void]$builder.Append(">sp|$accession-$index|$($accession)_SUBSET`n$peptide`n")
    }
    [IO.File]::WriteAllText($Target, $builder.ToString(), [System.Text.UTF8Encoding]::new($false))
    return $peptides.Count
}

function Split-Options([string[]]$Tokens) {
    $options = [System.Collections.Generic.List[object]]::new()
    for ($i = 0; $i -lt $Tokens.Count; $i++) {
        if (-not $Tokens[$i].StartsWith('-')) {
            throw "Expected an option at '$($Tokens[$i])' in: $($Tokens -join ' ')"
        }
        $group = @($Tokens[$i])
        if ($i + 1 -lt $Tokens.Count -and -not $Tokens[$i + 1].StartsWith('-')) {
            $group += $Tokens[++$i]
        }
        $options.Add($group)
    }
    return , $options
}

# The default arguments with each option -ExtraArgs names replaced by its -ExtraArgs form,
# and the other -ExtraArgs options appended.
function Merge-Arguments([string[]]$Base, [string[]]$Extra) {
    if ($Extra.Count -eq 0) {
        return $Base
    }
    $extraOptions = Split-Options $Extra
    $names = @($extraOptions | ForEach-Object { $_[0] })
    $merged = @()
    foreach ($group in (Split-Options $Base)) {
        $index = [Array]::IndexOf($names, $group[0])
        if ($index -ge 0) {
            $merged += $extraOptions[$index]
            $names[$index] = $null
        } else {
            $merged += $group
        }
    }
    for ($i = 0; $i -lt $names.Count; $i++) {
        if ($null -ne $names[$i]) {
            $merged += $extraOptions[$i]
        }
    }
    return $merged
}

function Get-GitState {
    $commit = (& git -C $scriptRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) {
        return [PSCustomObject]@{ Commit = 'unknown'; Branch = 'unknown'; Dirty = $true; Changes = @('not a git checkout') }
    }
    $branch = (& git -C $scriptRoot rev-parse --abbrev-ref HEAD 2>$null)
    $changes = @(& git -C $scriptRoot status --porcelain 2>$null)
    return [PSCustomObject]@{ Commit = "$commit".Trim(); Branch = "$branch".Trim(); Dirty = $changes.Count -gt 0; Changes = $changes }
}

function Get-ProcessorName {
    if ($IsWindows) {
        return (Get-CimInstance Win32_Processor | Select-Object -First 1).Name.Trim()
    }
    $model = Select-String -Path '/proc/cpuinfo' -Pattern '^model name\s*:\s*(.*)$' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($model) {
        return $model.Matches[0].Groups[1].Value.Trim()
    }
    return [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
}

# Runs the comparator (RegressionTest) on $RunFolder through build.ps1 -NoBuild; returns its exit code.
function Invoke-Comparator([string]$RunFolder, [string]$CreateFolder, [string]$LogName) {
    $saved = @{
        CARAFESHARP_REGRESSION_RUN    = $env:CARAFESHARP_REGRESSION_RUN
        CARAFESHARP_REGRESSION_CREATE = $env:CARAFESHARP_REGRESSION_CREATE
        CARAFESHARP_REGRESSION_DATA   = $env:CARAFESHARP_REGRESSION_DATA

    }
    try {
        $env:CARAFESHARP_REGRESSION_RUN = $RunFolder
        $env:CARAFESHARP_REGRESSION_CREATE = $CreateFolder
        $env:CARAFESHARP_REGRESSION_DATA = Join-Path $goldenRoot $config.Folder

        $log = Join-Path $RunFolder $LogName
        & pwsh -NoProfile -File $buildScript -NoBuild -Torch $Torch -TestName 'RegressionTest' -RequireData *>&1 |
            Tee-Object -FilePath $log | Out-Host
        return $LASTEXITCODE
    } finally {
        foreach ($name in $saved.Keys) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name])
        }
    }
}

# The values of a JSON document by path, for showing how two goldens differ.
function Get-JsonLeaves($Node, [string]$Prefix, [hashtable]$Leaves) {
    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Node.PSObject.Properties) {
            Get-JsonLeaves $property.Value "$Prefix/$($property.Name)" $Leaves
        }
    } elseif ($Node -is [System.Collections.IList] -and -not ($Node -is [string])) {
        for ($i = 0; $i -lt $Node.Count; $i++) {
            Get-JsonLeaves $Node[$i] "$Prefix[$i]" $Leaves
        }
    } else {
        $Leaves[$Prefix] = if ($null -eq $Node) { 'null' } else { [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $Node) }
    }
}

function Show-GoldenDifferences([string]$OldPath, [string]$NewPath) {
    $old = @{}
    $new = @{}
    Get-JsonLeaves (Get-Content -Raw -LiteralPath $OldPath | ConvertFrom-Json) '' $old
    Get-JsonLeaves (Get-Content -Raw -LiteralPath $NewPath | ConvertFrom-Json) '' $new
    $keys = @($old.Keys) + @($new.Keys) | Sort-Object -Unique
    $count = 0
    foreach ($key in $keys) {
        $a = $old[$key]
        $b = $new[$key]
        if ($a -cne $b) {
            Write-Host ("  {0}: {1} -> {2}" -f $key, $(if ($null -eq $a) { '(none)' } else { $a }), $(if ($null -eq $b) { '(none)' } else { $b }))
            $count++
        }
    }
    if ($count -eq 0) {
        Write-Host '  (no differences)'
    }
}

# ---------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------
if ($CreateGolden -and $ExtraArgs.Count -gt 0) {
    throw '-CreateGolden with -ExtraArgs: a golden is made with the default arguments only.'
}
if ($CreateGolden -and $Export) {
    throw '-CreateGolden with -Export: a golden is made from the packaged export.'
}
if ($CreateGolden -and $CarafeSharpExe) {
    throw '-CreateGolden with -CarafeSharpExe: a golden is made with this checkout''s build, whose commit it records.'
}
if ($Leg -eq 'Chained') {
    if (-not $config.Subset) {
        throw "-Leg Chained runs on Osprey's committed Stellar subset; $Dataset has none."
    }
    if ($CreateGolden) {
        throw '-Leg Chained has no golden: its checks are structural (see the description).'
    }
    if ($Export) {
        throw '-Leg Chained runs Osprey for its exports; -Export is for the isolated leg.'
    }
} elseif ($OspreyExe) {
    throw '-OspreyExe is for -Leg Chained.'
}
$git = Get-GitState
if ($CreateGolden -and $git.Dirty) {
    throw ("-CreateGolden needs a clean working tree, because the golden records the commit it came from. Changes:`n  " +
           ($git.Changes -join "`n  "))
}

$packageList = Join-Path $scriptRoot 'testdata.json'
$packages = (Get-Content -Raw -LiteralPath $packageList | ConvertFrom-Json).packages
$testDataRoot = Get-TestDataRoot

if (-not $CompareRun) {
    if ($Leg -eq 'Isolated') {
        if ($Export) {
            if (-not (Test-Path -LiteralPath $Export -PathType Leaf)) {
                throw "-Export names a file that does not exist: $Export"
            }
            $exportPath = (Resolve-Path -LiteralPath $Export).ProviderPath
            # CarafeSharp is given the export's folder (-i), and reads every export in it.
            $others = @(Get-ChildItem -LiteralPath (Split-Path -Parent $exportPath) -Filter '*.training.parquet' |
                Where-Object { $_.FullName -ne $exportPath })
            if ($others.Count -gt 0) {
                throw "-Export: $(Split-Path -Parent $exportPath) holds other training exports ($($others[0].Name)); CarafeSharp would train on all of them."
            }
            $exportFile = [PSCustomObject]@{ Path = $exportPath; Relative = $exportPath }
        } else {
            $exportFile = Resolve-PackageFile $config.Export
        }
        $libraryFasta = Resolve-PackageFile $config.LibraryFasta
        $pairing = Resolve-PackageFile $config.Pairing
    } else {
        $ospreyRoot = Join-Path (Split-Path -Parent $scriptRoot) 'Osprey'
        $subsetZip = Join-Path $ospreyRoot ($config.Subset -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $subsetZip -PathType Leaf)) {
            throw "Osprey's subset test data is not at $subsetZip."
        }
    }

    $binFolder = if ($Torch -eq 'cuda') { 'bin-cuda' } else { 'bin' }
    $exeName = if ($IsWindows) { 'CarafeSharp.exe' } else { 'CarafeSharp' }
    $exe = if ($CarafeSharpExe) { $CarafeSharpExe } else { Join-Path $scriptRoot "CarafeSharp/$binFolder/x64/Release/net10.0/$exeName" }

    if (-not $NoBuild -and -not $CarafeSharpExe) {
        Write-Step "Building CarafeSharp (libtorch $Torch)"
        & pwsh -NoProfile -File $buildScript -Torch $Torch -NoTests
        if ($LASTEXITCODE -ne 0) {
            throw "build.ps1 failed (exit $LASTEXITCODE)"
        }
    }
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "CarafeSharp not found at $exe. Build it with build.ps1$(if ($Torch -eq 'cuda') { ' -Torch cuda' }), or drop -NoBuild."
    }
    if ($Leg -eq 'Chained') {
        $ospreyExeName = if ($IsWindows) { 'Osprey.exe' } else { 'Osprey' }
        $osprey = if ($OspreyExe) { $OspreyExe } else { Join-Path $ospreyRoot "Osprey/bin/x64/Release/net10.0/$ospreyExeName" }
        if (-not $NoBuild -and -not $OspreyExe) {
            Write-Step 'Building Osprey'
            & pwsh -NoProfile -File (Join-Path $ospreyRoot 'build.ps1') -NoTests
            if ($LASTEXITCODE -ne 0) {
                throw "Osprey's build.ps1 failed (exit $LASTEXITCODE)"
            }
        }
        if (-not (Test-Path -LiteralPath $osprey)) {
            throw "Osprey not found at $osprey. Build it with pwiz_tools/Osprey/build.ps1, or drop -NoBuild."
        }
    }

    # ---------------------------------------------------------------------------
    # Run
    # ---------------------------------------------------------------------------
    if (-not $WorkDir) {
        $WorkDir = if ($env:CARAFESHARP_REGRESSION_WORKDIR) { $env:CARAFESHARP_REGRESSION_WORKDIR } else { Join-Path $scriptRoot 'TestResults/regression' }
    }
    $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $name = "$($Dataset.ToLowerInvariant())-$($Leg.ToLowerInvariant())-$Torch-$stamp" + $(if ($RunName) { "-$RunName" } else { '' })
    $runFolder = Join-Path $WorkDir $name
    if (Test-Path -LiteralPath $runFolder) {
        throw "Run folder already exists: $runFolder"
    }
    $inputFolder = Join-Path $runFolder 'inputs'
    $outFolder = Join-Path $runFolder 'out'
    New-Item -ItemType Directory -Force -Path $inputFolder | Out-Null
    $arguments = Merge-Arguments (Get-LibraryArguments) $ExtraArgs

    $info = [ordered]@{
        format            = 'carafesharp-regression-run-1'
        dataset           = $Dataset
        leg               = $Leg.ToLowerInvariant()
        torch             = $Torch
        device_requested  = $device
        device_used       = $null
        commit            = $git.Commit
        branch            = $git.Branch
        dirty             = $git.Dirty
        changes           = @($git.Changes)
        os                = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        os_platform       = if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } else { 'other' }
        processor         = Get-ProcessorName
        logical_processors = [Environment]::ProcessorCount
        omp_num_threads   = $env:OMP_NUM_THREADS
        carafesharp_exe   = $exe
        custom_exe        = [bool]$CarafeSharpExe
        extra_args        = @($ExtraArgs)
        arguments         = @($arguments)
    }

    if ($Leg -eq 'Isolated') {
        Write-Step "Subset of $($libraryFasta.Relative): the pair groups whose peptide_pair_index is a multiple of $subsetModulus"
        $subsetFasta = Join-Path $inputFolder 'library_subset_peptides.fasta'
        $subsetPairing = Join-Path $inputFolder 'library_subset_pairing.tsv'
        $subset = Write-SubsetPairing $pairing.Path $subsetPairing
        $subsetRecords = Write-SubsetFasta $libraryFasta.Path $subsetFasta $subset.Sequences
        Write-Host ("  {0} pair groups: {1} pairing rows, {2} FASTA records" -f $subset.Groups, $subset.Rows, $subsetRecords)
        if ($subsetRecords -lt $subset.Sequences.Count) {
            throw "$($libraryFasta.Relative) has no record for $($subset.Sequences.Count - $subsetRecords) of the subset's peptides."
        }

        $exportFolder = Split-Path -Parent $exportFile.Path
        $runFile = Join-Path $exportFolder $config.RunFile
        $cliArgs = @('-db', $subsetFasta, '-i', $exportFolder, '-ms', $runFile, '-o', $outFolder,
            '-pairing_manifest', $subsetPairing) + $arguments + @('-tf', 'all')

        $info.subset_rule = "the pair groups whose peptide_pair_index is a multiple of $subsetModulus, with all their members"
        $info.subset_groups = $subset.Groups
        $info.subset_records = $subsetRecords
        $info.subset_pairing_rows = $subset.Rows
        $info.export_note = $config.ExportNote
        $info.other_export = [bool]$Export
        $info.inputs = [ordered]@{
            export          = [ordered]@{ path = $exportFile.Relative; sha256 = Get-Sha256 $exportFile.Path }
            library_fasta   = [ordered]@{ path = $libraryFasta.Relative; sha256 = Get-Sha256 $libraryFasta.Path }
            library_pairing = [ordered]@{ path = $pairing.Relative; sha256 = Get-Sha256 $pairing.Path }
            subset_fasta    = [ordered]@{ path = 'inputs/library_subset_peptides.fasta'; sha256 = Get-Sha256 $subsetFasta }
            subset_pairing  = [ordered]@{ path = 'inputs/library_subset_pairing.tsv'; sha256 = Get-Sha256 $subsetPairing }
        }
    } else {
        Write-Step "Osprey's Stellar subset ($subsetZip)"
        Expand-Archive -LiteralPath $subsetZip -DestinationPath $inputFolder
        $runFiles = @(Get-ChildItem -LiteralPath $inputFolder -Filter '*.mzML' | Sort-Object Name | ForEach-Object { $_.FullName })
        $subsetLibrary = Join-Path $inputFolder $config.SubsetLibrary
        $subsetFasta = Join-Path $inputFolder 'subset_peptides.fasta'
        $fastaRecords = Write-PeptideFasta $subsetLibrary $subsetFasta
        Write-Host ("  {0} runs; {1} library peptides, one FASTA record each" -f $runFiles.Count, $fastaRecords)

        $ospreyFolder = Join-Path $runFolder 'osprey'
        New-Item -ItemType Directory -Force -Path $ospreyFolder | Out-Null
        $ospreyArgs = @($runFiles | ForEach-Object { '--input', $_ }) + @('--library', $subsetLibrary,
            '--output', (Join-Path $ospreyFolder 'output.blib'), '--work-dir', $ospreyFolder, '--resolution', 'unit', '--training-export')
        # Osprey writes each run's export into its work directory.
        $exportFolder = $ospreyFolder
        $cliArgs = @('-db', $subsetFasta, '-i', $exportFolder, '-ms', ($runFiles -join ','), '-o', $outFolder) + $arguments + @('-tf', 'all')

        $info.osprey_exe = $osprey
        $info.custom_osprey_exe = [bool]$OspreyExe
        $info.osprey_arguments = @($ospreyArgs)
        $info.runs = $runFiles.Count
        $info.subset_fasta_records = $fastaRecords
        $info.export_folder = 'osprey'
        $info.inputs = [ordered]@{
            subset_zip     = [ordered]@{ path = "pwiz_tools/Osprey/$($config.Subset)"; sha256 = Get-Sha256 $subsetZip }
            subset_library = [ordered]@{ path = "inputs/$($config.SubsetLibrary)"; sha256 = Get-Sha256 $subsetLibrary }
            subset_fasta   = [ordered]@{ path = 'inputs/subset_peptides.fasta'; sha256 = Get-Sha256 $subsetFasta }
        }
        $info.osprey_exit_code = $null
        $info.osprey_minutes = $null
    }
    $info.started = (Get-Date).ToString('o')
    $info.finished = $null
    $info.minutes = $null
    $info.exit_code = $null
    $infoPath = Join-Path $runFolder 'regression-run.json'
    $info | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $infoPath -Encoding utf8

    if ($Leg -eq 'Chained') {
        Write-Step "Osprey: search the subset with --training-export ($ospreyFolder)"
        Write-Host "$osprey $($ospreyArgs -join ' ')" -ForegroundColor DarkGray
        if (-not $Preflight) {
            $ospreyLog = Join-Path $runFolder 'osprey.log'
            $ospreyClock = [Diagnostics.Stopwatch]::StartNew()
            & $osprey @ospreyArgs *>&1 | Tee-Object -FilePath $ospreyLog | Out-Host
            $ospreyExit = $LASTEXITCODE
            $ospreyClock.Stop()
            $info.osprey_exit_code = $ospreyExit
            $info.osprey_minutes = [math]::Round($ospreyClock.Elapsed.TotalMinutes, 2)
            $info | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $infoPath -Encoding utf8
            if ($ospreyExit -ne 0) {
                throw "Osprey failed (exit $ospreyExit); log: $ospreyLog"
            }
            $exports = @(Get-ChildItem -LiteralPath $exportFolder -Filter '*.training.parquet')
            Write-Host ("Osprey finished in {0:F1} min: {1} training exports" -f $ospreyClock.Elapsed.TotalMinutes, $exports.Count) -ForegroundColor Green
            if ($exports.Count -eq 0) {
                throw "Osprey wrote no training export into $exportFolder; log: $ospreyLog"
            }
        }
    }

    Write-Step "CarafeSharp: fine-tune and library ($runFolder)"
    Write-Host "$exe $($cliArgs -join ' ')" -ForegroundColor DarkGray
    if ($Preflight) {
        Write-Host 'Preflight only: stopping before CarafeSharp runs.' -ForegroundColor Yellow
        exit 0
    }
    $log = Join-Path $runFolder 'carafesharp.log'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    & $exe @cliArgs *>&1 | Tee-Object -FilePath $log | Out-Host
    $exitCode = $LASTEXITCODE
    $clock.Stop()

    $fellBack = Select-String -LiteralPath $log -SimpleMatch 'running on the CPU' -Quiet
    $info.device_used = if ($device -eq 'gpu' -and -not $fellBack) { 'cuda' } else { 'cpu' }
    $info.finished = (Get-Date).ToString('o')
    $info.minutes = [math]::Round($clock.Elapsed.TotalMinutes, 2)
    $info.exit_code = $exitCode
    $info | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $infoPath -Encoding utf8
    if ($exitCode -ne 0) {
        throw "CarafeSharp failed (exit $exitCode); log: $log"
    }
    Write-Host ("CarafeSharp finished in {0:F1} min on {1}" -f $clock.Elapsed.TotalMinutes, $info.device_used) -ForegroundColor Green
    if ($Torch -eq 'cuda' -and $fellBack) {
        $message = "CarafeSharp was asked for the GPU and fell back to the CPU (see $log)."
        if ($CreateGolden) {
            throw "$message -CreateGolden refuses a CPU fallback on a GPU request."
        }
        Write-Warning "$message The run is compared as a CPU run."
    }

    if ($Leg -eq 'Chained') {
        # The saved model reused as a user would: a library predicted from it with -model and no
        # training, over the command line's wider precursor window.
        $savedModel = Join-Path $outFolder 'carafe_fine_tuned_model.carafemodel'
        $savedFolder = Join-Path $runFolder 'saved-model-library'
        $savedArgs = @('-db', $subsetFasta, '-model', $savedModel, '-o', $savedFolder) + (Get-SavedModelArguments)
        Write-Step "CarafeSharp: a library from the saved model ($savedFolder)"
        Write-Host "$exe $($savedArgs -join ' ')" -ForegroundColor DarkGray
        $savedLog = Join-Path $runFolder 'carafesharp-saved-model.log'
        & $exe @savedArgs *>&1 | Tee-Object -FilePath $savedLog | Out-Host
        $savedExit = $LASTEXITCODE
        $info.saved_model_library = 'saved-model-library'
        $info.saved_model_exit_code = $savedExit
        $info | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $infoPath -Encoding utf8
        if ($savedExit -ne 0) {
            throw "CarafeSharp failed to predict from the saved model (exit $savedExit); log: $savedLog"
        }

        # The saved model fine-tuned further, as a user would on new runs. Here the runs are the
        # same, so it starts from the models the first training chose and scores them on the
        # same held-out rows.
        $furtherFolder = Join-Path $runFolder 'fine-tuned-further'
        $furtherArgs = @('-i', $exportFolder, '-ms', ($runFiles -join ','), '-model', $savedModel, '-o', $furtherFolder) +
            $arguments + @('-tf', 'all')
        Write-Step "CarafeSharp: the saved model fine-tuned further ($furtherFolder)"
        Write-Host "$exe $($furtherArgs -join ' ')" -ForegroundColor DarkGray
        $furtherLog = Join-Path $runFolder 'carafesharp-fine-tuned-further.log'
        & $exe @furtherArgs *>&1 | Tee-Object -FilePath $furtherLog | Out-Host
        $furtherExit = $LASTEXITCODE
        $info.fine_tuned_further = 'fine-tuned-further'
        $info.fine_tuned_further_exit_code = $furtherExit
        $info | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $infoPath -Encoding utf8
        if ($furtherExit -ne 0) {
            throw "CarafeSharp failed to fine-tune the saved model further (exit $furtherExit); log: $furtherLog"
        }
    }
} else {
    $runFolder = (Resolve-Path -LiteralPath $CompareRun).ProviderPath
    $infoPath = Join-Path $runFolder 'regression-run.json'
    if (-not (Test-Path -LiteralPath $infoPath)) {
        throw "$runFolder has no regression-run.json: it is not a regression.ps1 run folder."
    }
    $recorded = Get-Content -Raw -LiteralPath $infoPath | ConvertFrom-Json
    if ($recorded.dataset -ne $Dataset) {
        throw "$runFolder is a $($recorded.dataset) run, not $Dataset."
    }
    if ($CreateGolden -and ($recorded.dirty -or @($recorded.extra_args).Count -gt 0 -or $recorded.custom_exe -or $recorded.other_export -or $recorded.exit_code -ne 0)) {
        throw "-CreateGolden -CompareRun needs a completed run made from a clean tree with this checkout's build, the packaged export and no -ExtraArgs; $runFolder is not one."
    }
    if ($CreateGolden -and $Torch -eq 'cuda' -and $recorded.device_used -ne 'cuda') {
        throw "-CreateGolden refuses a CPU fallback on a GPU request: $runFolder ran on $($recorded.device_used)."
    }
    if ($CreateGolden -and $recorded.leg -eq 'chained') {
        throw "-CreateGolden: $runFolder is a chained run, which has no golden."
    }
    Write-Step "Comparing the existing run $runFolder"
}
$runLeg = if ($CompareRun) { if ($recorded.leg) { $recorded.leg } else { 'isolated' } } else { $Leg.ToLowerInvariant() }

# ---------------------------------------------------------------------------
# Compare, or make the golden
# ---------------------------------------------------------------------------
$goldenFolder = Join-Path $goldenRoot $config.Folder
$goldenPath = Join-Path $goldenFolder 'golden.json'
if (-not $CreateGolden) {
    if (-not (Test-Path -LiteralPath $goldenPath)) {
        throw "No golden at $goldenPath. Make one with -CreateGolden."
    }
    $chained = $runLeg -eq 'chained'
    Write-Step $(if ($chained) { 'Checking the chained run' } else { "Comparing with $goldenPath" })
    $code = Invoke-Comparator $runFolder '' 'comparator.log'
    $report = Join-Path $runFolder 'regression-report.txt'
    if (Test-Path -LiteralPath $report) {
        Get-Content -LiteralPath $report | Out-Host
    }
    $failed = if ($chained) { 'failed its checks' } else { "is outside the golden's tolerances" }
    $passed = if ($chained) { 'passed its checks' } else { "is within the golden's tolerances" }
    if ($code -ne 0) {
        Write-Host "REGRESSION FAILED: $runFolder $failed (report: $report)" -ForegroundColor Red
        exit 1
    }
    Write-Host "REGRESSION PASSED: $runFolder $passed (report: $report)" -ForegroundColor Green
    exit 0
}

Write-Step 'Making the golden candidate'
$candidate = Join-Path $runFolder 'golden'
$code = Invoke-Comparator $runFolder $candidate 'golden-create.log'
if ($code -ne 0) {
    throw "The comparator refused the run as a golden (log: $(Join-Path $runFolder 'golden-create.log'))."
}
if (Test-Path -LiteralPath $goldenPath) {
    Write-Step "A golden exists; this run compared with it:"
    Invoke-Comparator $runFolder '' 'comparator.log' | Out-Null
    $report = Join-Path $runFolder 'regression-report.txt'
    if (Test-Path -LiteralPath $report) {
        Get-Content -LiteralPath $report | Out-Host
    }
    Write-Step 'golden.json values that change:'
    Show-GoldenDifferences $goldenPath (Join-Path $candidate 'golden.json')
    if (-not $Force) {
        Write-Host "A golden already exists at $goldenPath. Review the differences above and rerun with -Force to replace it." -ForegroundColor Yellow
        Write-Host "(The candidate is in $candidate; -CompareRun $runFolder -CreateGolden -Force installs it without a new run.)" -ForegroundColor Yellow
        exit 1
    }
}
New-Item -ItemType Directory -Force -Path $goldenFolder | Out-Null
Get-ChildItem -LiteralPath $candidate -File | Copy-Item -Destination $goldenFolder -Force
Write-Host "Golden written to $goldenFolder from $runFolder. Commit regression.data/$($config.Folder)." -ForegroundColor Green
exit 0
